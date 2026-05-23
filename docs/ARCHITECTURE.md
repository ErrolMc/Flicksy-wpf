# Flicksy — Architecture Reference

Token-optimized map of the current build. Read this first; jump to specific files only when the change requires it. Keep this doc updated whenever the structure changes (see [CLAUDE.md](../CLAUDE.md)).

## 1. Solution shape

3 projects, all `net10.0-windows`, defined in [Flicksy.slnx](../Flicksy.slnx). No project references between them — they communicate by **launching each other as separate processes** and passing media file paths.

| Project | OutputType | UI tech | Role |
| --- | --- | --- | --- |
| [Flicksy.Agent](../Flicksy.Agent) | WinExe | WinForms (tray) | Background tray app. Registers global hotkey `Ctrl+Shift+Alt+S`. Launches `Flicksy.Snipper.exe`. |
| [Flicksy.Snipper](../Flicksy.Snipper) | WinExe | WPF + WinForms interop | Screen-region selection. Modes: **snip** (bitmap → PNG) or **record** (ffmpeg gdigrab → MP4). Launches `Flicksy.PostSnip.exe <mediaPath>`. |
| [Flicksy.PostSnip](../Flicksy.PostSnip) | WinExe | WPF (MVVM) | Image/video editor. Opens passed media, lets user annotate image or scrub video, saves output. |

### 1.1 Inter-process contract

- **Agent → Snipper**: spawned with no args. Resolved via sibling-folder probing in [AgentApplicationContext.ResolveSnipperExecutablePath](../Flicksy.Agent/AgentApplicationContext.cs).
- **Snipper → PostSnip**: spawned with the media path as first arg (quoted). See [SnipperSessionController.TryLaunchPostSnipWithMedia](../Flicksy.Snipper/SnipperSessionController.cs).
- **PostSnip startup arg parsing**: [App.ResolveStartupMediaPath](../Flicksy.PostSnip/App.xaml.cs) accepts `--launch-file <path>`, a positional first arg, or falls back to `LaunchPostSnipWithFilePath` in [appsettings.json](../Flicksy.PostSnip/appsettings.json) (used for dev launches without going through Snipper).
- **Temp media files**: written to `%TEMP%/flicksy-snip-{guid}.png` or `%TEMP%/flicksy-recording-{guid}.mp4`. PostSnip deletes them on close unless `PreserveMediaFile` is set ([PostSnipViewModel.DeleteMediaFile](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs)). Set by `App` when launched with an explicit arg so dev runs don't nuke user files.

### 1.2 External dependencies

- **FFmpeg shared libs** (avcodec-*.dll etc.). Required by PostSnip (FFMediaToolkit) and used as a CLI by Snipper (gdigrab capture). PostSnip probes a long list of locations via [FfmpegLocator](../Flicksy.PostSnip/Media/FfmpegLocator.cs): `FFMPEG_HOME` env var, `PATH`, winget shared FFmpeg packages, `C:\ffmpeg\bin`, app-local `lib\ffmpeg`.
- **NuGet** (PostSnip only, see [Flicksy.PostSnip.csproj](../Flicksy.PostSnip/Flicksy.PostSnip.csproj)):
  - `CommunityToolkit.Mvvm` — `[ObservableProperty]`, `[RelayCommand]`.
  - `FFMediaToolkit` — video decoding.
  - `Microsoft.Extensions.Hosting` + `DependencyInjection` — DI container.

## 2. Flicksy.Agent (tray host)

Trivial. 3 files.

| File | Purpose |
| --- | --- |
| [Program.cs](../Flicksy.Agent/Program.cs) | WinForms entry point, runs `AgentApplicationContext`. |
| [AgentApplicationContext.cs](../Flicksy.Agent/AgentApplicationContext.cs) | Tray icon + context menu (Open Snipper / Exit). Owns the hotkey window. Resolves and starts `Flicksy.Snipper.exe`. |
| [HotKeyWindow.cs](../Flicksy.Agent/HotKeyWindow.cs) | `RegisterHotKey` P/Invoke for `Ctrl+Shift+Alt+S`. Calls back the supplied `Action` on `WM_HOTKEY`. |

## 3. Flicksy.Snipper (capture)

`App.xaml.cs` → constructs `SnipperSessionController` → shows `PreSnipOverlayWindow`. Shutdown is `OnExplicitShutdown` so windows can close/reopen without exiting.

| File | Purpose |
| --- | --- |
| [App.xaml.cs](../Flicksy.Snipper/App.xaml.cs) | Bootstraps the session controller. |
| [SnipperSessionController.cs](../Flicksy.Snipper/SnipperSessionController.cs) | State machine: PreSnip → (snip captured → launch PostSnip) **or** PreSnip → VideoRecordingOverlay → record → launch PostSnip. Shuts the app down when no overlays remain. |
| [ScreenRecorder.cs](../Flicksy.Snipper/ScreenRecorder.cs) | Spawns `ffmpeg` CLI with `gdigrab` at 30 fps, `libx264 veryfast yuv420p`. Stop = write `q` to stdin, with kill-fallback. Capture rect is clamped to `VirtualScreen` and rounded to even dimensions. |
| [Overlays/PreSnipOverlayWindow.xaml(.cs)](../Flicksy.Snipper/Overlays/PreSnipOverlayWindow.xaml.cs) | Full-screen overlay. Snapshots screen to `_backgroundBitmap` (so the cursor freezes), then user drags a selection. Mode buttons (Snip/Record) switch the on-confirm callback. |
| [Overlays/VideoRecordingOverlayWindow.xaml(.cs)](../Flicksy.Snipper/Overlays/VideoRecordingOverlayWindow.xaml.cs) | Sits over the chosen rect with Start/Stop + elapsed timer. Calls `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` so the overlay doesn't end up in the recording. |

Per-screen behavior: `PreSnipOverlayWindow` is created with the bounds of the screen the cursor is on at hotkey time (`Screen.FromPoint`), not the primary screen.

## 4. Flicksy.PostSnip (annotation + playback)

WPF MVVM. Entry: [App.xaml.cs](../Flicksy.PostSnip/App.xaml.cs). Folders are organized **by responsibility, not by feature** — change requests that touch one feature usually touch one file per folder.

### 4.1 Top-level layout

```
Flicksy.PostSnip/
├── App.xaml(.cs)              ← DI bootstrap, ffmpeg init, startup-arg parsing
├── PostSnipWindow.xaml(.cs)   ← The only window. Pan/zoom viewport + chrome
├── appsettings.json           ← Dev-only LaunchPostSnipWithFilePath fallback
│
├── ViewModels/                ← MVVM ViewModels (CommunityToolkit.Mvvm)
├── Source/                    ← Drawing item model (Pen / Shape / Text + base)
├── Interaction/               ← Tool/gesture system (IDrawingSurface/Tool, Router, Tools, Config)
├── Undo/                      ← UndoManager + IUndoableCommand + concrete commands
├── Media/                     ← IVideoPlayer + FFmpegVideoPlayer + FfmpegLocator
├── Controllers/               ← Glue between ViewModel + WPF (currently text editor only)
├── Controls/                  ← UserControls + DrawingView (the canvas)
├── Helpers/                   ← BitmapExtensions (toolbar icons), DrawingMath
├── Properties/Resources.resx  ← Embedded toolbar PNG icons
└── Resources/                 ← Source PNGs for the embedded resources
```

### 4.2 ViewModels (`Flicksy.PostSnip/ViewModels`)

| ViewModel | Owns / Coordinates |
| --- | --- |
| [PostSnipViewModel](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs) | Root VM. Holds `Player`, `ImageEditTools`, `Drawing`, `SelectionOverlay`. Loads image/video, raises `SaveDialogRequested`/`CloseRequested`/`ErrorOccurred` for the window code-behind. Cross-VM wiring: subscribes to `Drawing.SelectedItem`/`EditingTextItem` + `ImageEditTools.SelectedTool`/`IsSelectActive` to keep selection overlay + text-edit lifecycle consistent. |
| [DrawingViewModel](../Flicksy.PostSnip/ViewModels/DrawingViewModel.cs) | `ObservableCollection<DrawingItem> Items` (z-ordered), `SelectedItem`, `EditingTextItem`, `History` (UndoManager). All gesture transitions (`BeginPenStroke`/`End...`, `BeginShape`/`End...`, `BeginText`/`BeginEditText`/`EndEditText`, `BeginTextStyleEdit`/`End...`). Layer move + delete commands. |
| [ImageEditToolsViewModel](../Flicksy.PostSnip/ViewModels/ImageEditToolsViewModel.cs) | `SelectedTool` enum (`ImageEditTool.Select/Pen/Erase/Shapes/Text/Crop`). Pen/Shape/Text sub-settings VMs. Popup open-state with 250ms debounce to stop reopen-on-close cycles. |
| [CropOverlayViewModel](../Flicksy.PostSnip/ViewModels/CropOverlayViewModel.cs) | Non-destructive crop state: `ImageWidth`/`Height`, `CommittedCrop` (persistent), `WorkingCrop` (mid-edit), `IsActive`. `EffectiveCrop`/`CurrentViewBounds` are what the view+window read. `BeginEdit`/`CommitEdit`/`CancelEdit` drive lifecycle; `CommitEdit` pushes a `CropCommand` if the rect changed. Holds a ref to `Drawing.History` for the push. |
| [PenSettingsViewModel](../Flicksy.PostSnip/ViewModels/PenSettingsViewModel.cs) / [ShapeSettingsViewModel](../Flicksy.PostSnip/ViewModels/ShapeSettingsViewModel.cs) / [TextSettingsViewModel](../Flicksy.PostSnip/ViewModels/TextSettingsViewModel.cs) | Tool-specific settings (size, color, font, etc). Shape+Text own a [FillSettingsViewModel](../Flicksy.PostSnip/ViewModels/FillSettingsViewModel.cs) + [OutlineSettingsViewModel](../Flicksy.PostSnip/ViewModels/OutlineSettingsViewModel.cs). Both fill/outline VMs expose `SyncFromBrush(...)` so the popup can reflect the selected item's existing style. |
| [SelectionOverlayViewModel](../Flicksy.PostSnip/ViewModels/SelectionOverlayViewModel.cs) | `SelectedItem` + `IsActive` + `ShowHandles` + cached `CanonicalBounds`. Subscribes to the item's `Geometry`/`Transform.Changed` so the overlay redraws when the item moves. |

### 4.3 Drawing model (`Flicksy.PostSnip/Source`)

All items inherit [DrawingItem](../Flicksy.PostSnip/Source/DrawingItem.cs) which provides `Geometry`, `MatrixTransform Transform`, abstract `CanonicalBounds`/`HitTest(localPoint)`/`Render(DrawingContext)`, and `Translate/Scale/RotateFrom(baseMatrix, ...)` helpers.

| Item | Geometry | Notes |
| --- | --- | --- |
| [PenStrokeItem](../Flicksy.PostSnip/Source/PenStrokeItem.cs) | Catmull-Rom-style smoothed `PathGeometry` over a `PointCollection`. | Brush + thickness immutable per stroke. Bounds inflated by thickness/2. |
| [ShapeItem](../Flicksy.PostSnip/Source/ShapeItem.cs) | `Square` (Rect), `Circle` (Ellipse), `Line` (LineGeometry), `Arrow` (PathGeometry: shaft + filled arrowhead triangle). | `EffectiveFill`/`EffectiveStroke` exposed for the XAML data template — arrow's "fill" is its outline brush so the head fills solidly. `IsDegenerate` predicate suppresses commit on tap-without-drag. |
| [TextItem](../Flicksy.PostSnip/Source/TextItem.cs) | `FormattedText.BuildGeometry(origin)`. | Properties are mutable via `SetText`/`SetFontFamily`/`SetFontSize`/`SetFill`/`SetOutline`. Geometry rebuilds on every mutation. `IsEditing` flag tracks the in-place editor. |

### 4.4 Interaction system (`Flicksy.PostSnip/Interaction`)

Decouples gesture handlers from the WPF host so the canvas (or a future video-editor canvas) can swap in tools without changing them.

- [IDrawingSurface](../Flicksy.PostSnip/Interaction/IDrawingSurface.cs) — host capabilities: dimensions, content scale (zoom), cursor set/get, pointer capture, `TryGetCanvasPoint(MouseEventArgs, ...)`.
- [IDrawingTool](../Flicksy.PostSnip/Interaction/IDrawingTool.cs) — gesture interface: `OnPointerDown/Move/Up/Hover` + `IsActive` flag for in-progress gestures.
- [ToolRouter](../Flicksy.PostSnip/Interaction/ToolRouter.cs) — dispatches pointer events. Prefers any tool with `IsActive == true` over the currently-selected tool, so a gesture that started under tool A still receives Move/Up after the user toggles to tool B.
- [InputSmoothing](../Flicksy.PostSnip/Interaction/InputSmoothing.cs) — single-pole EMA for pen jitter.
- [Config/IPenConfig](../Flicksy.PostSnip/Interaction/Config/IPenConfig.cs), [IShapeConfig](../Flicksy.PostSnip/Interaction/Config/IShapeConfig.cs), [ITextConfig](../Flicksy.PostSnip/Interaction/Config/ITextConfig.cs) — per-tool settings exposed by the host (`DrawingView` implements all three from its dependency properties).

Tools (`Flicksy.PostSnip/Interaction/Tools`):

| Tool | Gesture | Pushes undo on |
| --- | --- | --- |
| [SelectTool](../Flicksy.PostSnip/Interaction/Tools/SelectTool.cs) | Click to select, drag inside bounds to move, drag corner to scale (anchor = opposite corner). Double-click TextItem → open editor. Hover sets resize cursors per corner+rotation. | `TransformCommand` on pointer up (only if matrix changed). |
| [PenTool](../Flicksy.PostSnip/Interaction/Tools/PenTool.cs) | Down begins stroke (seeds smoother). Move appends smoothed points gated by `max(1.5, thickness/2)` minimum distance. | `AddItemCommand` (via `DrawingViewModel.EndPenStroke`). |
| [ShapeTool](../Flicksy.PostSnip/Interaction/Tools/ShapeTool.cs) | Drag to size. **Shift** = constrain (square/circle: equal sides; line/arrow: 45° snap). | `AddItemCommand` (via `EndShape`). Degenerate shapes are removed without an undo entry. |
| [EraseTool](../Flicksy.PostSnip/Interaction/Tools/EraseTool.cs) | Down + drag deletes whichever item is topmost under the pointer. | One `RemoveItemCommand` per delete, bundled into a `CompositeCommand` on pointer up if >1. |
| [TextTool](../Flicksy.PostSnip/Interaction/Tools/TextTool.cs) | Click on TextItem → `BeginEditText`. Click on empty → `BeginText` + `BeginEditText`. No drag. | `AddItemCommand` or `TextEditCommand` pushed by `DrawingViewModel.EndEditText`. |

Rotation lives on the **SelectionOverlayView** (not a tool) because it interacts with the puck handle drawn outside the item's bounds — see [SelectionOverlayView.OnRotateHandleMouseDown](../Flicksy.PostSnip/Controls/SelectionOverlayView.xaml.cs). It pushes its own `TransformCommand` analogously to SelectTool's scale/translate.

Crop is **not** an `IDrawingTool` — it edits image-level state rather than the drawing collection. Selecting the Crop toolbar button drives [CropOverlayViewModel](../Flicksy.PostSnip/ViewModels/CropOverlayViewModel.cs) via [PostSnipViewModel](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs)'s tool-change handler (BeginEdit on enter, CommitEdit on leave). All gesture handling (resize / move / draw new) lives in [CropOverlayView.xaml.cs](../Flicksy.PostSnip/Controls/CropOverlayView.xaml.cs). New rects are clamped to the original image bounds.

### 4.5 Undo (`Flicksy.PostSnip/Undo`)

[UndoManager](../Flicksy.PostSnip/Undo/UndoManager.cs): two stacks, capped at 100 entries. Exposes `UndoCommand`/`RedoCommand` RelayCommands. `Push` clears redo and trims oldest.

Convention: commands are pushed **after** the change has already mutated state (gestures mutate live for visual feedback). `Redo()` is therefore only invoked when stepping forward through the redo stack, never on the initial push.

| Command | When |
| --- | --- |
| [AddItemCommand](../Flicksy.PostSnip/Undo/Commands/AddItemCommand.cs) | New item committed (pen stroke end, shape end, text commit on new item). |
| [RemoveItemCommand](../Flicksy.PostSnip/Undo/Commands/RemoveItemCommand.cs) | Single delete (Delete key, single-tap erase). |
| [CompositeCommand](../Flicksy.PostSnip/Undo/Commands/CompositeCommand.cs) | Multi-step bundle (drag-erase that removed several items). Preserves selection across the bundle. |
| [TransformCommand](../Flicksy.PostSnip/Undo/Commands/TransformCommand.cs) | Move/scale/rotate gesture end. Snapshots before/after `Matrix`. |
| [MoveLayerCommand](../Flicksy.PostSnip/Undo/Commands/MoveLayerCommand.cs) | Layer up/down toolbar buttons. |
| [TextEditCommand](../Flicksy.PostSnip/Undo/Commands/TextEditCommand.cs) | Existing TextItem's text changed in place. |
| [TextStyleCommand](../Flicksy.PostSnip/Undo/Commands/TextStyleCommand.cs) | Batch of font/size/fill/outline changes from the Text settings popup (captured on open, pushed on close). Uses `TextStyleSnapshot`. |
| [CropCommand](../Flicksy.PostSnip/Undo/Commands/CropCommand.cs) | Crop committed (push at `CropOverlayViewModel.CommitEdit` if before/after differ). Undo/redo call `ApplyCommittedCrop`. |

### 4.6 Media (`Flicksy.PostSnip/Media`)

| File | Purpose |
| --- | --- |
| [IVideoPlayer](../Flicksy.PostSnip/Media/IVideoPlayer.cs) | Abstraction: `Open/Play/Pause/Seek/Close`, `FrameReady`/`PositionChanged`/`StateChanged`/`MediaEnded` events. |
| [FFmpegVideoPlayer](../Flicksy.PostSnip/Media/FFmpegVideoPlayer.cs) | Decodes ahead into a `BlockingCollection<VideoFrame>` (capacity 6) on a Task; presents on `CompositionTarget.Rendering` ticks. `_seekLock` is **`TryEnter`** in the render path so a background scrub seek can't stall the UI for tens of ms. Uses `ArrayPool<byte>` for frame buffers — every code path that takes a frame is responsible for returning the buffer. |
| [FfmpegLocator](../Flicksy.PostSnip/Media/FfmpegLocator.cs) | One-time `Initialize()` at app startup; sets `FFMediaToolkit.FFmpegLoader.FFmpegPath`. See §1.2 for probe order. |
| [VideoFrame](../Flicksy.PostSnip/Media/VideoFrame.cs) | Plain struct: `Buffer`, `BufferLength`, `Width`, `Height`, `Stride`, `Pts`. |
| [PlaybackState](../Flicksy.PostSnip/Media/PlaybackState.cs) | `Idle`/`Loading`/`Paused`/`Playing`/`Ended`. |

### 4.7 Controls (`Flicksy.PostSnip/Controls`)

| Control | Notes |
| --- | --- |
| [DrawingView](../Flicksy.PostSnip/Controls/DrawingView/DrawingView.xaml) + [.xaml.cs](../Flicksy.PostSnip/Controls/DrawingView/DrawingView.xaml.cs) + [.DependencyProperties.cs](../Flicksy.PostSnip/Controls/DrawingView/DrawingView.DependencyProperties.cs) | The canvas. Renders all items via DataTemplates (PenStrokeItem/ShapeItem/TextItem → WPF `Path`). Implements `IDrawingSurface`/`IPenConfig`/`IShapeConfig`/`ITextConfig` and wires a `ToolRouter`. Rebuilds tool instances when its `DataContext` (the `DrawingViewModel`) changes. Hosts the in-place text editor TextBox in `EditOverlayCanvas`, managed by [TextEditingController](../Flicksy.PostSnip/Controllers/TextEditingController.cs). |
| [ImageEditToolsView](../Flicksy.PostSnip/Controls/ImageEditToolsView.xaml.cs) | Centered toolbar. Click on already-active Pen/Shapes/Text toggles its settings popup. Opening the Text popup begins a `TextStyleCommand` snapshot; closing pushes the diff. |
| [SelectionOverlayView](../Flicksy.PostSnip/Controls/SelectionOverlayView.xaml.cs) | Corner handles + rotate puck. Projects item canonical bounds through `item.Transform` and the host's `ContentToViewport` transform (so handles stay screen-sized regardless of zoom). Owns the rotate gesture. |
| [CropOverlayView](../Flicksy.PostSnip/Controls/CropOverlayView.xaml.cs) | Snipping-tool-style crop UI: dim shade over the image area outside the crop, white outline, L-shaped corner brackets, edge midpoint markers. Visible only while `CropOverlayViewModel.IsActive`. Owns all crop gestures (corner/edge resize, move, draw-new). Uses `ContentToViewport` like `SelectionOverlayView` so the handles render at fixed pixel size. |
| [VideoSurface](../Flicksy.PostSnip/Controls/VideoSurface.xaml.cs) | Subscribes to `IVideoPlayer.FrameReady`. Writes BGRA32 pixels into a `WriteableBitmap` sized to the video's first frame. |
| [VideoPlaybackOverlay](../Flicksy.PostSnip/Controls/VideoPlaybackOverlay.xaml.cs) | Transport bar. Two scrub sources: slider (mouse drag) and keyboard (Left/Right arrows step one frame). Scrub targets coalesce through a capacity-1 `Channel<long>` with `DropOldest`; a worker calls `IVideoPlayer.Seek` and yields ~16ms so the render loop can present the seeked frame. |
| [FillSettingsView](../Flicksy.PostSnip/Controls/FillSettingsView.xaml) / [OutlineSettingsView](../Flicksy.PostSnip/Controls/OutlineSettingsView.xaml) / [PenSettingsView](../Flicksy.PostSnip/Controls/PenSettingsView.xaml) / [ShapeSettingsView](../Flicksy.PostSnip/Controls/ShapeSettingsView.xaml) / [TextSettingsView](../Flicksy.PostSnip/Controls/TextSettingsView.xaml) | Popup content for the toolbar. |

### 4.8 Window (`Flicksy.PostSnip/PostSnipWindow`)

[PostSnipWindow.xaml](../Flicksy.PostSnip/PostSnipWindow.xaml) is the only top-level window:
- DockPanel: top chrome (New / tools panel / Save+Cancel) + central viewport.
- `<Window.InputBindings>` wires `Ctrl+Z` / `Ctrl+Y` to `Drawing.History.UndoCommand`/`RedoCommand`.
- Image viewport uses a `Canvas` (not Grid) so the image renders at natural pixel size, with `ScaleTransform`+`TranslateTransform` doing fit/pan/zoom.
- Code-behind ([.xaml.cs](../Flicksy.PostSnip/PostSnipWindow.xaml.cs)) handles: dark titlebar via `DwmSetWindowAttribute`, mouse-wheel zoom/pan, horizontal wheel via `WM_MOUSEHWHEEL` hook, middle-button pan, Delete-key to delete selected item, and adapters for `SaveDialogRequested`/`CloseRequested`/`ErrorOccurred` from the VM.
- `TryAutoFit` and `ClampOffsets` use `CropOverlayViewModel.CurrentViewBounds` (committed crop when not editing, full image while editing) instead of the raw image size. `ImageContent.Clip` is set to a `RectangleGeometry` for the committed crop whenever a crop is active and the user isn't editing; cleared otherwise. Both fire when `CropOverlay.ViewBoundsChanged` is raised.

### 4.9 Save flow

[PostSnipViewModel.Save](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs):
- **Image with drawings or crop**: render `ImageSource` + all `DrawingItem.Render(dc)` calls into a `DrawingVisual` (translated by `-cropOrigin` when cropped) → `RenderTargetBitmap` sized to the crop in pixels (or full image when uncropped) → PNG via `PngBitmapEncoder`.
- **Image without drawings or crop** or **video**: copy the source file to the destination (no re-encode).
- Save dialog is shown by the window code-behind (the VM raises `SaveDialogRequested`).

## 5. End-to-end flow (cheat sheet)

1. User presses `Ctrl+Shift+Alt+S`. [HotKeyWindow](../Flicksy.Agent/HotKeyWindow.cs) → [LaunchSnipper](../Flicksy.Agent/AgentApplicationContext.cs).
2. [PreSnipOverlayWindow](../Flicksy.Snipper/Overlays/PreSnipOverlayWindow.xaml.cs) appears on cursor's monitor with a frozen-screen background. User picks **Snip** or **Record** + drags a rect.
3. **Snip path**: bitmap → PNG in `%TEMP%`, copied to clipboard, then `Flicksy.PostSnip.exe "<path>"` ([SnipperSessionController.OnSnipCaptured](../Flicksy.Snipper/SnipperSessionController.cs)).
4. **Record path**: [VideoRecordingOverlayWindow](../Flicksy.Snipper/Overlays/VideoRecordingOverlayWindow.xaml.cs) → ffmpeg gdigrab → MP4 in `%TEMP%` → `Flicksy.PostSnip.exe "<path>"`.
5. PostSnip [App.OnStartup](../Flicksy.PostSnip/App.xaml.cs) initializes FFmpeg, builds the DI host, resolves `PostSnipWindow`, loads the media (`LoadImage` or `LoadVideoAsync`).
6. User annotates (Pen/Shape/Text/Erase via tools), navigates (pan/zoom/scrub), undoes (Ctrl+Z), saves (PNG or copied MP4) or cancels. PostSnip deletes the temp file on close unless `PreserveMediaFile` was set.

## 6. Conventions seen in this codebase

- **MVVM via CommunityToolkit.Mvvm**: `[ObservableProperty]` on private fields generates the public property; `[RelayCommand]` on a private method generates a public `XxxCommand`. Don't hand-roll PropertyChanged.
- **Tool extensibility**: new tools implement [IDrawingTool](../Flicksy.PostSnip/Interaction/IDrawingTool.cs), get instantiated + registered in [DrawingView.OnDataContextChanged](../Flicksy.PostSnip/Controls/DrawingView/DrawingView.xaml.cs), and depend on small `IXxxConfig` interfaces — not on `DrawingView` directly.
- **Undo commands**: state is mutated live during the gesture; the command is pushed at the **end** of the gesture with before/after snapshots. Multi-step bundles use [CompositeCommand](../Flicksy.PostSnip/Undo/Commands/CompositeCommand.cs).
- **No emojis, comments only when WHY is non-obvious** (see existing comments — most explain a subtle invariant or a workaround).
- **No file watcher / hot reload / live config** — `appsettings.json` is read once at startup.
- **No tests** in the repo currently.

## 7. Where to look for common changes

| Change request | Primary file(s) |
| --- | --- |
| Add a new drawing tool | new file in [Interaction/Tools/](../Flicksy.PostSnip/Interaction/Tools), config interface in [Interaction/Config/](../Flicksy.PostSnip/Interaction/Config), wire in [DrawingView.OnDataContextChanged](../Flicksy.PostSnip/Controls/DrawingView/DrawingView.xaml.cs) + toolbar enum in [ImageEditToolsViewModel](../Flicksy.PostSnip/ViewModels/ImageEditToolsViewModel.cs) + button in [ImageEditToolsView.xaml](../Flicksy.PostSnip/Controls/ImageEditToolsView.xaml). |
| Add a new drawing item type | new class in [Source/](../Flicksy.PostSnip/Source) inheriting `DrawingItem`, DataTemplate in [DrawingView.xaml](../Flicksy.PostSnip/Controls/DrawingView/DrawingView.xaml). |
| Change the global hotkey | [HotKeyWindow](../Flicksy.Agent/HotKeyWindow.cs). |
| Change capture format/quality | [ScreenRecorder.BuildArguments](../Flicksy.Snipper/ScreenRecorder.cs). |
| Add a new undoable action | new `IUndoableCommand` in [Undo/Commands/](../Flicksy.PostSnip/Undo/Commands), push from the call site **after** mutation. |
| Change crop UI / behavior | [CropOverlayView](../Flicksy.PostSnip/Controls/CropOverlayView.xaml.cs) for visuals + gestures, [CropOverlayViewModel](../Flicksy.PostSnip/ViewModels/CropOverlayViewModel.cs) for state. The save side lives in [PostSnipViewModel.SaveImageWithDrawing](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs). |
| Modify video playback behavior | [FFmpegVideoPlayer](../Flicksy.PostSnip/Media/FFmpegVideoPlayer.cs) (engine) + [VideoPlaybackOverlay](../Flicksy.PostSnip/Controls/VideoPlaybackOverlay.xaml.cs) (UI). |
| Modify save format | [PostSnipViewModel.Save](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs) + `SaveImageWithDrawing`. |
| Change toolbar layout | [ImageEditToolsView.xaml](../Flicksy.PostSnip/Controls/ImageEditToolsView.xaml) + [PostSnipWindow.xaml](../Flicksy.PostSnip/PostSnipWindow.xaml). |
