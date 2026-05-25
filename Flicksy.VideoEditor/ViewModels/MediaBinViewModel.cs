using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;
using Flicksy.Drawing.Helpers;
using Flicksy.VideoEditor.Project;
using Microsoft.Win32;

namespace Flicksy.VideoEditor.ViewModels;

/// <summary>
/// Backs the media bin (#9 step 1a). Owns a wrapper collection
/// (<see cref="MediaSources"/>) that mirrors <see cref="Project.Project.MediaSources"/>
/// 1:1, plus single-selection state and the Import / Reveal commands. Probe runs on the UI
/// thread (50–150 ms per file, sequential for multi-file imports); thumbnail decode is
/// off-loaded to a single background worker over an unbounded
/// <see cref="Channel{T}"/> and posted back via the UI dispatcher. Audio-only sources
/// short-circuit the worker and get the static music-file glyph immediately.
/// </summary>
public sealed partial class MediaBinViewModel : ObservableObject
{
    // Extensions accepted by the Import dialog filter and the Explorer drag-drop path.
    // Must stay in sync between the two so dedup + probe-failure handling are uniform.
    private static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v",
        ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg",
    };

    private const string DialogFilter =
        "Media files|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.m4v;*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg|All files|*.*";

    private readonly Project.Project _project;
    private readonly Dispatcher _dispatcher;
    private readonly Channel<MediaSource> _thumbnailQueue;
    private readonly Dictionary<Guid, MediaSourceViewModel> _wrappersById = new();
    private ImageSource? _audioGlyph;

    public MediaBinViewModel(Project.Project project)
    {
        _project = project;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        MediaSources = new ObservableCollection<MediaSourceViewModel>();

        foreach (var source in project.MediaSources)
        {
            AddWrapper(source);
        }
        project.MediaSources.CollectionChanged += OnProjectMediaSourcesChanged;

        _thumbnailQueue = Channel.CreateUnbounded<MediaSource>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });
        // Fire-and-forget worker. Lives for the lifetime of the editor window.
        _ = Task.Run(ProcessThumbnailsAsync);
    }

    public ObservableCollection<MediaSourceViewModel> MediaSources { get; }

    [ObservableProperty]
    private MediaSourceViewModel? selectedSource;

    public bool IsEmpty => MediaSources.Count == 0;

    public bool HasEntries => MediaSources.Count > 0;

    [RelayCommand]
    private void Import()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = DialogFilter,
            Title = "Import media",
        };
        if (dialog.ShowDialog() != true) return;

        foreach (var path in dialog.FileNames)
        {
            TryImportFile(path);
        }
    }

    public void TryImportFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var ext = Path.GetExtension(path);
            if (!AcceptedExtensions.Contains(ext)) continue;
            TryImportFile(path);
        }
    }

    private void TryImportFile(string rawPath)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(rawPath);
        }
        catch (Exception ex)
        {
            ShowImportError(rawPath, ex);
            return;
        }

        // Silent dedupe — already-imported sources skip without a message.
        if (_project.MediaSources.Any(s => string.Equals(s.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        MediaSource source;
        try
        {
            source = MediaSource.Probe(fullPath);
        }
        catch (Exception ex)
        {
            ShowImportError(fullPath, ex);
            return;
        }

        _project.MediaSources.Add(source);
    }

    [RelayCommand]
    private void Reveal(MediaSourceViewModel? entry)
    {
        var path = entry?.Source.SourcePath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch
        {
            // Reveal is best-effort — explorer.exe can fail on a deleted file or restricted path.
        }
    }

    private void OnProjectMediaSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (MediaSource source in e.OldItems)
            {
                RemoveWrapper(source);
            }
        }
        if (e.NewItems is not null)
        {
            foreach (MediaSource source in e.NewItems)
            {
                AddWrapper(source);
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _wrappersById.Clear();
            MediaSources.Clear();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasEntries));
        }
    }

    private void AddWrapper(MediaSource source)
    {
        var wrapper = new MediaSourceViewModel(source);

        // Audio-only sources display a static glyph — no decode needed.
        // Video sources (whether they also have audio or not) get a midpoint frame thumb.
        // Missing sources skip both paths; the placeholder cell shows until 1b paints them red.
        if (source.HasAudio && !source.HasVideo)
        {
            wrapper.Thumbnail = GetAudioGlyph();
        }
        else if (source.HasVideo && !source.IsMissing)
        {
            _thumbnailQueue.Writer.TryWrite(source);
        }

        _wrappersById[source.Id] = wrapper;
        MediaSources.Add(wrapper);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasEntries));
    }

    private void RemoveWrapper(MediaSource source)
    {
        if (!_wrappersById.Remove(source.Id, out var wrapper)) return;
        MediaSources.Remove(wrapper);
        if (SelectedSource == wrapper) SelectedSource = null;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasEntries));
    }

    private ImageSource GetAudioGlyph()
    {
        // Lazy first-touch — never accessed on stub-only projects that have no audio.
        return _audioGlyph ??= Images.music_file.ToImageSource();
    }

    private async Task ProcessThumbnailsAsync()
    {
        await foreach (var source in _thumbnailQueue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            ImageSource? thumb;
            try
            {
                thumb = DecodeVideoThumbnail(source.SourcePath);
            }
            catch
            {
                // Decode failure keeps the placeholder. Silent per spec — runtime IsMissing
                // flips only on explicit re-probe, never on thumbnail failure alone.
                continue;
            }

            if (thumb is null) continue;

            _ = _dispatcher.InvokeAsync(() =>
            {
                if (_wrappersById.TryGetValue(source.Id, out var wrapper))
                {
                    wrapper.Thumbnail = thumb;
                }
            });
        }
    }

    private static ImageSource? DecodeVideoThumbnail(string path)
    {
        var options = new MediaOptions
        {
            StreamsToLoad = MediaMode.Video,
            VideoPixelFormat = ImagePixelFormat.Bgra32,
        };

        using var file = MediaFile.Open(path, options);
        if (!file.HasVideo) return null;

        var duration = file.Video.Info.Duration;
        var seekTo = duration > TimeSpan.Zero
            ? TimeSpan.FromTicks(duration.Ticks / 2)
            : TimeSpan.Zero;

        if (!file.Video.TryGetFrame(seekTo, out var frame)) return null;

        var width = frame.ImageSize.Width;
        var height = frame.ImageSize.Height;
        if (width <= 0 || height <= 0) return null;

        // BitmapSource.Create copies the pixel buffer internally — safe to dispose
        // MediaFile after this call without dangling pointers.
        var source = BitmapSource.Create(
            width,
            height,
            96, 96,
            PixelFormats.Bgra32,
            palette: null,
            pixels: frame.Data.ToArray(),
            stride: frame.Stride);

        const double TargetWidth = 200.0;
        var scale = TargetWidth / width;
        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }

    private void ShowImportError(string path, Exception ex)
    {
        var fileName = string.Empty;
        try { fileName = Path.GetFileName(path); } catch { fileName = path; }

        MessageBox.Show(
            $"Couldn't import {fileName}:\n\n{ex.Message}",
            "Import failed",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    public static bool IsAcceptedMediaPath(string path)
        => !string.IsNullOrWhiteSpace(path) && AcceptedExtensions.Contains(Path.GetExtension(path));
}
