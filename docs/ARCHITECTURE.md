# Flicksy — Architecture Reference

Token-optimized map of the current build. Read this first; jump to specific files only when the change requires it. Keep this doc updated whenever the structure changes (see [CLAUDE.md](../CLAUDE.md)).

## 1. Solution shape

6 projects, defined in [Flicksy.slnx](../Flicksy.slnx). Four WinExes communicate by **launching each other as separate processes** (no project refs between them); both interactive editors reference the shared Drawing library, and PostSnip additionally references Icons.

| Project | OutputType | UI tech | TFM | Role |
| --- | --- | --- | --- | --- |
| [Flicksy.Agent](../Flicksy.Agent) | WinExe | WinForms (tray) | net10.0-windows | Background tray app. Registers global hotkey `Ctrl+Shift+Alt+S`. Launches `Flicksy.Snipper.exe`. |
| [Flicksy.Snipper](../Flicksy.Snipper) | WinExe | WPF + WinForms interop | net10.0-windows | Screen-region selection. Modes: **snip** (bitmap → PNG) or **record** (ffmpeg gdigrab → MP4). Launches `Flicksy.PostSnip.exe <mediaPath>`. |
| [Flicksy.PostSnip](../Flicksy.PostSnip) | WinExe | WPF (MVVM) | net10.0-windows | Image/video editor. Opens passed media, lets user annotate image or scrub video, saves output. References Drawing + Icons. |
| [Flicksy.VideoEditor](../Flicksy.VideoEditor) | WinExe | WPF (MVVM) | net10.0-windows | Multi-clip video editor. Arg-driven entry: no args → Welcome, `--new-video-project` → empty editor, positional path → editor with source. References Drawing. |
| [Flicksy.Drawing](../Flicksy.Drawing) | Library | WPF (MVVM) | net10.0-windows | Shared drawing primitives: `DrawingItem` hierarchy, tool system, undo manager, FFmpeg playback, `DrawingView` + selection overlay. References Icons. |
| [Flicksy.Icons](../Flicksy.Icons) | Library | none (assets only) | net10.0 | Icon PNGs + strongly-typed `Flicksy.Icons.Properties.Resources` accessor. Exposed to consumers as the alias `Images` via csproj-level `<Using Include="..." Alias="Images" />` (alias is `Images` not `Icons` because the `Flicksy.Icons` namespace would shadow `Icons` per C# §13.6 lookup order). |

**Convention**: no project refs between WinExes; class libraries (Drawing, Icons) may be referenced by any consumer. Drawing references Icons; Icons references nothing.

### 1.1 Inter-process contract

- **Agent → Snipper**: spawned with no args. Resolved via sibling-folder probing in [AgentApplicationContext.ResolveSnipperExecutablePath](../Flicksy.Agent/AgentApplicationContext.cs).
- **Agent → VideoEditor**: tray menu's `New Video Project` item spawns `Flicksy.VideoEditor.exe --new-video-project`. Resolved via [AgentApplicationContext.ResolveVideoEditorExecutablePath](../Flicksy.Agent/AgentApplicationContext.cs) (same sibling-folder probe pattern).
- **Snipper → PostSnip**: spawned with the media path as first arg (quoted). See [SnipperSessionController.TryLaunchPostSnipWithMedia](../Flicksy.Snipper/SnipperSessionController.cs).
- **PostSnip → VideoEditor**: `Launch in video editor` button on `PostSnipWindow` (visible only when `IsVideoLoaded`) spawns `Flicksy.VideoEditor.exe "<videoPath>"`. Handler sets `PreserveMediaFile=true` first so the temp video survives PostSnip closing. Resolved via [PostSnipViewModel.ResolveVideoEditorExecutablePath](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs).
- **PostSnip startup arg parsing**: [App.ResolveStartupMediaPath](../Flicksy.PostSnip/App.xaml.cs) accepts `--launch-file <path>`, a positional first arg, or falls back to `LaunchPostSnipWithFilePath` in [appsettings.json](../Flicksy.PostSnip/appsettings.json) (used for dev launches without going through Snipper).
- **VideoEditor startup arg parsing**: [App.ResolveStartupMode](../Flicksy.VideoEditor/App.xaml.cs) returns a [StartupMode](../Flicksy.VideoEditor/StartupMode.cs) discriminated record — no args → `Welcome`, `--new-video-project` → `EmptyEditor`, positional first arg that's an existing file → `EditorWithSource(path)`. Unrecognized args fall back to `Welcome`. `Welcome` and `EmptyEditor` windows are resolved from the DI host; `EditorWithSource` bypasses DI to construct the VM around [Project.CreateFromSourceFile](../Flicksy.VideoEditor/Project/Project.cs) so the path can flow in.
- **Temp media files**: written to `%TEMP%/flicksy-snip-{guid}.png` or `%TEMP%/flicksy-recording-{guid}.mp4`. PostSnip deletes them on close unless `PreserveMediaFile` is set ([PostSnipViewModel.DeleteMediaFile](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs)). Set by `App` when launched with an explicit arg so dev runs don't nuke user files, and by the PostSnip → VideoEditor handoff so the editor opens against the same temp file.

### 1.2 External dependencies

- **FFmpeg shared libs** (avcodec-*.dll etc.). Required by Drawing (FFMediaToolkit, used inside `FFmpegVideoPlayer`) and as a CLI by Snipper (gdigrab capture). Drawing probes a long list of locations via [FfmpegLocator](../Flicksy.Drawing/Media/FfmpegLocator.cs): `FFMPEG_HOME` env var, `PATH`, winget shared FFmpeg packages, `C:\ffmpeg\bin`, app-local `lib\ffmpeg`. PostSnip calls `FfmpegLocator.Initialize()` at startup.
- **NuGet by project**:
  - **Drawing** ([csproj](../Flicksy.Drawing/Flicksy.Drawing.csproj)): `CommunityToolkit.Mvvm`, `FFMediaToolkit`.
  - **PostSnip** ([csproj](../Flicksy.PostSnip/Flicksy.PostSnip.csproj)): `CommunityToolkit.Mvvm`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`. (FFMediaToolkit is transitive via Drawing.)
  - **Resources** ([csproj](../Flicksy.Icons/Flicksy.Icons.csproj)): `System.Drawing.Common`, `System.Resources.Extensions`.

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

## 4. Editor surface (PostSnip + Drawing)

The annotation+playback surface is split across **Flicksy.PostSnip** (WinExe + snip-specific orchestration) and **Flicksy.Drawing** (shared rendering library). Convention from §1: anything the future video editor will reuse lives in Drawing; anything specific to the snip flow (crop, image toolbar, video transport UI) stays in PostSnip. Sub-section paths below indicate which project owns the file.

### 4.1 Top-level layout

```
Flicksy.PostSnip/
├── App.xaml(.cs)
├── PostSnipWindow.xaml(.cs)
├── appsettings.json
├── ViewModels/
│   PostSnipViewModel
│   ImageEditToolsViewModel
│   CropOverlayViewModel
│   {Pen,Shape,Text,Fill,Outline}SettingsViewModel
├── Controls/
│   ImageEditToolsView
│   {Pen,Shape,Text,Fill,Outline}SettingsView
│   CropOverlayView
│   VideoPlaybackOverlay
└── Undo/Commands/
    CropCommand

Flicksy.Drawing/
├── Source/        ← DrawingItem hierarchy + ShapeKind
├── Interaction/   ← Tool/router/config
├── Undo/          ← UndoManager + shared commands
├── Media/         ← IVideoPlayer + FFmpeg
├── Controllers/   ← TextEditingController
├── Controls/
│   DrawingView/   ← The canvas
│   SelectionOverlayView
│   VideoSurface
│   TextEditingHost.cs  ← attached property
├── Helpers/
│   BitmapExtensions
│   DrawingMath
└── ViewModels/
    DrawingViewModel
    SelectionOverlayViewModel
```

Icon PNGs (rotate puck, shape options, toolbar buttons) live in **Flicksy.Icons** — see §7.

### 4.2 ViewModels

| ViewModel | Owns / Coordinates |
| --- | --- |
| [PostSnipViewModel](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs) | Root VM. Holds `Player`, `ImageEditTools`, `Drawing`, `SelectionOverlay`. Loads image/video, raises `SaveDialogRequested`/`CloseRequested`/`ErrorOccurred` for the window code-behind. Cross-VM wiring: subscribes to `Drawing.SelectedItem`/`EditingTextItem` + `ImageEditTools.SelectedTool`/`IsSelectActive` to keep selection overlay + text-edit lifecycle consistent. |
| [DrawingViewModel](../Flicksy.Drawing/ViewModels/DrawingViewModel.cs) | `ObservableCollection<DrawingItem> Items` (z-ordered), `SelectedItem`, `EditingTextItem`, `History` (UndoManager). All gesture transitions (`BeginPenStroke`/`End...`, `BeginShape`/`End...`, `BeginText`/`BeginEditText`/`EndEditText`, `BeginTextStyleEdit`/`End...`). Layer move + delete commands. |
| [ImageEditToolsViewModel](../Flicksy.PostSnip/ViewModels/ImageEditToolsViewModel.cs) | `SelectedTool` enum (`ImageEditTool.Select/Pen/Erase/Shapes/Text/Crop`). Pen/Shape/Text sub-settings VMs. Popup open-state with 250ms debounce to stop reopen-on-close cycles. |
| [CropOverlayViewModel](../Flicksy.PostSnip/ViewModels/CropOverlayViewModel.cs) | Non-destructive crop state: `ImageWidth`/`Height`, `CommittedCrop` (persistent), `WorkingCrop` (mid-edit), `IsActive`. `EffectiveCrop`/`CurrentViewBounds` are what the view+window read. `BeginEdit`/`CommitEdit`/`CancelEdit` drive lifecycle; `CommitEdit` pushes a `CropCommand` if the rect changed. Holds a ref to `Drawing.History` for the push. |
| [PenSettingsViewModel](../Flicksy.PostSnip/ViewModels/PenSettingsViewModel.cs) / [ShapeSettingsViewModel](../Flicksy.PostSnip/ViewModels/ShapeSettingsViewModel.cs) / [TextSettingsViewModel](../Flicksy.PostSnip/ViewModels/TextSettingsViewModel.cs) | Tool-specific settings (size, color, font, etc). Shape+Text own a [FillSettingsViewModel](../Flicksy.PostSnip/ViewModels/FillSettingsViewModel.cs) + [OutlineSettingsViewModel](../Flicksy.PostSnip/ViewModels/OutlineSettingsViewModel.cs). Both fill/outline VMs expose `SyncFromBrush(...)` so the popup can reflect the selected item's existing style. |
| [SelectionOverlayViewModel](../Flicksy.Drawing/ViewModels/SelectionOverlayViewModel.cs) | `SelectedItem` + `IsActive` + `ShowHandles` + cached `CanonicalBounds`. Subscribes to the item's `Geometry`/`Transform.Changed` so the overlay redraws when the item moves. |

### 4.3 Drawing model (Flicksy.Drawing/Source)

All items inherit [DrawingItem](../Flicksy.Drawing/Source/DrawingItem.cs) which provides `Geometry`, `MatrixTransform Transform`, abstract `CanonicalBounds`/`HitTest(localPoint)`/`Render(DrawingContext)`, and `Translate/Scale/RotateFrom(baseMatrix, ...)` helpers.

| Item | Geometry | Notes |
| --- | --- | --- |
| [PenStrokeItem](../Flicksy.Drawing/Source/PenStrokeItem.cs) | Catmull-Rom-style smoothed `PathGeometry` over a `PointCollection`. | Brush + thickness immutable per stroke. Bounds inflated by thickness/2. |
| [ShapeItem](../Flicksy.Drawing/Source/ShapeItem.cs) | `Square` (Rect), `Circle` (Ellipse), `Line` (LineGeometry), `Arrow` (PathGeometry: shaft + filled arrowhead triangle). | `EffectiveFill`/`EffectiveStroke` exposed for the XAML data template — arrow's "fill" is its outline brush so the head fills solidly. `IsDegenerate` predicate suppresses commit on tap-without-drag. |
| [TextItem](../Flicksy.Drawing/Source/TextItem.cs) | `FormattedText.BuildGeometry(origin)`. | Properties are mutable via `SetText`/`SetFontFamily`/`SetFontSize`/`SetFill`/`SetOutline`. Geometry rebuilds on every mutation. `IsEditing` flag tracks the in-place editor. |

### 4.4 Interaction system (Flicksy.Drawing/Interaction)

Decouples gesture handlers from the WPF host so the canvas (or a future video-editor canvas) can swap in tools without changing them.

- [IDrawingSurface](../Flicksy.Drawing/Interaction/IDrawingSurface.cs) — host capabilities: dimensions, content scale (zoom), cursor set/get, pointer capture, `TryGetCanvasPoint(MouseEventArgs, ...)`.
- [IDrawingTool](../Flicksy.Drawing/Interaction/IDrawingTool.cs) — gesture interface: `OnPointerDown/Move/Up/Hover` + `IsActive` flag for in-progress gestures.
- [ToolRouter](../Flicksy.Drawing/Interaction/ToolRouter.cs) — dispatches pointer events. Prefers any tool with `IsActive == true` over the currently-selected tool, so a gesture that started under tool A still receives Move/Up after the user toggles to tool B.
- [InputSmoothing](../Flicksy.Drawing/Interaction/InputSmoothing.cs) — single-pole EMA for pen jitter.
- [Config/IPenConfig](../Flicksy.Drawing/Interaction/Config/IPenConfig.cs), [IShapeConfig](../Flicksy.Drawing/Interaction/Config/IShapeConfig.cs), [ITextConfig](../Flicksy.Drawing/Interaction/Config/ITextConfig.cs) — per-tool settings exposed by the host (`DrawingView` implements all three from its dependency properties).

Tools (`Flicksy.PostSnip/Interaction/Tools`):

| Tool | Gesture | Pushes undo on |
| --- | --- | --- |
| [SelectTool](../Flicksy.Drawing/Interaction/Tools/SelectTool.cs) | Click to select, drag inside bounds to move, drag corner to scale (anchor = opposite corner). Double-click TextItem → open editor. Hover sets resize cursors per corner+rotation. | `TransformCommand` on pointer up (only if matrix changed). |
| [PenTool](../Flicksy.Drawing/Interaction/Tools/PenTool.cs) | Down begins stroke (seeds smoother). Move appends smoothed points gated by `max(1.5, thickness/2)` minimum distance. | `AddItemCommand` (via `DrawingViewModel.EndPenStroke`). |
| [ShapeTool](../Flicksy.Drawing/Interaction/Tools/ShapeTool.cs) | Drag to size. **Shift** = constrain (square/circle: equal sides; line/arrow: 45° snap). | `AddItemCommand` (via `EndShape`). Degenerate shapes are removed without an undo entry. |
| [EraseTool](../Flicksy.Drawing/Interaction/Tools/EraseTool.cs) | Down + drag deletes whichever item is topmost under the pointer. | One `RemoveItemCommand` per delete, bundled into a `CompositeCommand` on pointer up if >1. |
| [TextTool](../Flicksy.Drawing/Interaction/Tools/TextTool.cs) | Click on TextItem → `BeginEditText`. Click on empty → `BeginText` + `BeginEditText`. No drag. | `AddItemCommand` or `TextEditCommand` pushed by `DrawingViewModel.EndEditText`. |

Rotation lives on the **SelectionOverlayView** (not a tool) because it interacts with the puck handle drawn outside the item's bounds — see [SelectionOverlayView.OnRotateHandleMouseDown](../Flicksy.Drawing/Controls/SelectionOverlayView.xaml.cs). It pushes its own `TransformCommand` analogously to SelectTool's scale/translate.

Crop is **not** an `IDrawingTool` — it edits image-level state rather than the drawing collection. Selecting the Crop toolbar button drives [CropOverlayViewModel](../Flicksy.PostSnip/ViewModels/CropOverlayViewModel.cs) via [PostSnipViewModel](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs)'s tool-change handler (BeginEdit on enter, CommitEdit on leave). All gesture handling (resize / move / draw new) lives in [CropOverlayView.xaml.cs](../Flicksy.PostSnip/Controls/CropOverlayView.xaml.cs). New rects are clamped to the original image bounds.

### 4.5 Undo (Flicksy.Drawing/Undo + Flicksy.PostSnip/Undo/Commands/CropCommand)

[UndoManager](../Flicksy.Drawing/Undo/UndoManager.cs): two stacks, capped at 100 entries. Exposes `UndoCommand`/`RedoCommand` RelayCommands. `Push` clears redo and trims oldest.

Convention: commands are pushed **after** the change has already mutated state (gestures mutate live for visual feedback). `Redo()` is therefore only invoked when stepping forward through the redo stack, never on the initial push.

| Command | When |
| --- | --- |
| [AddItemCommand](../Flicksy.Drawing/Undo/Commands/AddItemCommand.cs) | New item committed (pen stroke end, shape end, text commit on new item). |
| [RemoveItemCommand](../Flicksy.Drawing/Undo/Commands/RemoveItemCommand.cs) | Single delete (Delete key, single-tap erase). |
| [CompositeCommand](../Flicksy.Drawing/Undo/Commands/CompositeCommand.cs) | Multi-step bundle (drag-erase that removed several items). Preserves selection across the bundle. |
| [TransformCommand](../Flicksy.Drawing/Undo/Commands/TransformCommand.cs) | Move/scale/rotate gesture end. Snapshots before/after `Matrix`. |
| [MoveLayerCommand](../Flicksy.Drawing/Undo/Commands/MoveLayerCommand.cs) | Layer up/down toolbar buttons. |
| [TextEditCommand](../Flicksy.Drawing/Undo/Commands/TextEditCommand.cs) | Existing TextItem's text changed in place. |
| [TextStyleCommand](../Flicksy.Drawing/Undo/Commands/TextStyleCommand.cs) | Batch of font/size/fill/outline changes from the Text settings popup (captured on open, pushed on close). Uses `TextStyleSnapshot`. |
| [CropCommand](../Flicksy.PostSnip/Undo/Commands/CropCommand.cs) | Crop committed (push at `CropOverlayViewModel.CommitEdit` if before/after differ). Undo/redo call `ApplyCommittedCrop`. |

### 4.6 Media (Flicksy.Drawing/Media)

| File | Purpose |
| --- | --- |
| [IVideoPlayer](../Flicksy.Drawing/Media/IVideoPlayer.cs) | Abstraction: `Open/Play/Pause/Seek/Close`, `FrameReady`/`PositionChanged`/`StateChanged`/`MediaEnded` events. |
| [FFmpegVideoPlayer](../Flicksy.Drawing/Media/FFmpegVideoPlayer.cs) | Decodes ahead into a `BlockingCollection<VideoFrame>` (capacity 6) on a Task; presents on `CompositionTarget.Rendering` ticks. `_seekLock` is **`TryEnter`** in the render path so a background scrub seek can't stall the UI for tens of ms. Uses `ArrayPool<byte>` for frame buffers — every code path that takes a frame is responsible for returning the buffer. |
| [FfmpegLocator](../Flicksy.Drawing/Media/FfmpegLocator.cs) | One-time `Initialize()` at app startup; sets `FFMediaToolkit.FFmpegLoader.FFmpegPath`. See §1.2 for probe order. |
| [VideoFrame](../Flicksy.Drawing/Media/VideoFrame.cs) | Plain struct: `Buffer`, `BufferLength`, `Width`, `Height`, `Stride`, `Pts`. |
| [PlaybackState](../Flicksy.Drawing/Media/PlaybackState.cs) | `Idle`/`Loading`/`Paused`/`Playing`/`Ended`. |

### 4.7 Controls (split across projects)

| Control | Notes |
| --- | --- |
| [DrawingView](../Flicksy.Drawing/Controls/DrawingView/DrawingView.xaml) + [.xaml.cs](../Flicksy.Drawing/Controls/DrawingView/DrawingView.xaml.cs) + [.DependencyProperties.cs](../Flicksy.Drawing/Controls/DrawingView/DrawingView.DependencyProperties.cs) | The canvas. Renders all items via DataTemplates (PenStrokeItem/ShapeItem/TextItem → WPF `Path`). Implements `IDrawingSurface`/`IPenConfig`/`IShapeConfig`/`ITextConfig` and wires a `ToolRouter`. Rebuilds tool instances when its `DataContext` (the `DrawingViewModel`) changes. Hosts the in-place text editor TextBox in `EditOverlayCanvas`, managed by [TextEditingController](../Flicksy.Drawing/Controllers/TextEditingController.cs). |
| [ImageEditToolsView](../Flicksy.PostSnip/Controls/ImageEditToolsView.xaml.cs) | Centered toolbar. Click on already-active Pen/Shapes/Text toggles its settings popup. Opening the Text popup begins a `TextStyleCommand` snapshot; closing pushes the diff. |
| [SelectionOverlayView](../Flicksy.Drawing/Controls/SelectionOverlayView.xaml.cs) | Corner handles + rotate puck. Projects item canonical bounds through `item.Transform` and the host's `ContentToViewport` transform (so handles stay screen-sized regardless of zoom). Owns the rotate gesture. |
| [CropOverlayView](../Flicksy.PostSnip/Controls/CropOverlayView.xaml.cs) | Snipping-tool-style crop UI: dim shade over the image area outside the crop, white outline, L-shaped corner brackets, edge midpoint markers. Visible only while `CropOverlayViewModel.IsActive`. Owns all crop gestures (corner/edge resize, move, draw-new). Uses `ContentToViewport` like `SelectionOverlayView` so the handles render at fixed pixel size. |
| [VideoSurface](../Flicksy.Drawing/Controls/VideoSurface.xaml.cs) | Subscribes to `IVideoPlayer.FrameReady`. Writes BGRA32 pixels into a `WriteableBitmap` sized to the video's first frame. |
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

## 5. Flicksy.VideoEditor (multi-clip editor)

Shell stage — process boots, parses args, routes to Welcome or the editor shell, and initializes FFmpeg. The shell is a top toolbar + 5-column body (rail · panel · center · panel · rail). The center column has Preview + Transport stacked above a Timeline that renders the document's tracks/clips and a playhead overlay (visual layout: [video-editor/TIMELINE.md](video-editor/TIMELINE.md)). Future code will reuse Drawing for per-clip annotation work.

### 5.1 Files

| File | Purpose |
| --- | --- |
| [App.xaml](../Flicksy.VideoEditor/App.xaml) / [App.xaml.cs](../Flicksy.VideoEditor/App.xaml.cs) | No `StartupUri`. `OnStartup`: `FfmpegLocator.Initialize()`, build a `Microsoft.Extensions.Hosting` DI host registering `WelcomeWindow`, `VideoEditorWindow`, `VideoEditorViewModel`, and `Project.CreateEmpty()` (all transient), call `ResolveStartupMode(e.Args)`, resolve/construct the matching window, `Show()` it. `EditorWithSource` constructs manually (passing `Project.CreateFromSourceFile(path)`) since DI can't supply the runtime path. Exposes `App.Services` static for other windows (e.g. `WelcomeWindow`) to resolve from. `OnExit` stops + disposes the host. |
| [StartupMode.cs](../Flicksy.VideoEditor/StartupMode.cs) | Discriminated record: `Welcome` \| `EmptyEditor` \| `EditorWithSource(string Path)`. |
| [Windows/WelcomeWindow.xaml(.cs)](../Flicksy.VideoEditor/Windows/WelcomeWindow.xaml.cs) | Three stacked buttons: `New Video Project` (primary, enabled), `Open Project` (disabled placeholder for save/load), `Open Recent` (disabled placeholder). `New Video Project` click resolves a `VideoEditorWindow` from `App.Services` (built around `Project.CreateEmpty()` via the registered factory), reassigns `Application.Current.MainWindow` (so the default `OnMainWindowClose` shutdown mode now tracks the editor), then closes the Welcome window. Dark titlebar via `DwmSetWindowAttribute`. |
| [Windows/VideoEditorWindow.xaml(.cs)](../Flicksy.VideoEditor/Windows/VideoEditorWindow.xaml.cs) | Editor shell. Top toolbar (project name `TextBox`, Undo/Redo, Export placeholder) + 5-column grid below. Ctors: parameterless and `(string? sourcePath)` both build a default `VideoEditorViewModel`; `(VideoEditorViewModel, string? sourcePath = null)` is the canonical one used by `App`/`WelcomeWindow`. `Ctrl+Z`/`Ctrl+Y` `InputBindings` invoke the VM's `UndoCommand`/`RedoCommand`. |

### 5.2 Entry modes

| Args | Mode | Window shown |
| --- | --- | --- |
| _(none)_ | `Welcome` | `WelcomeWindow` |
| `--new-video-project` | `EmptyEditor` | blank `VideoEditorWindow` |
| `<path-to-existing-file>` (positional, first arg) | `EditorWithSource(path)` | `VideoEditorWindow` with filename in title |
| anything else | falls back to `Welcome` | `WelcomeWindow` |

`ResolveStartupMode` validates the positional path with `File.Exists`; a non-existent path falls through to `Welcome` rather than opening a broken editor.

### 5.3 Document model

Project document lives in [Flicksy.VideoEditor/Project/](../Flicksy.VideoEditor/Project). JSON-serializable POCOs — no `Brush`/`Geometry`/`MatrixTransform` on any public property. The only allowed reference into Drawing is `DrawingItem` (held by `GraphicsClip`). Shape and rationale: [ADR 0002](adr/0002-video-editor-document-model.md).

Timeline positions and clip durations are integer frames at `ProjectSettings.Framerate`. `MediaClip` source-time is `TimeSpan` (source-framerate-independent).

| Type | Role | Key fields |
| --- | --- | --- |
| [Project](../Flicksy.VideoEditor/Project/Project.cs) | Document root. Static factories `CreateEmpty()` (4 tracks: 2 Video, 1 Overlay, 1 Audio + empty `MediaSources`), `CreateFromSourceFile(path)` (probes via `MediaSource.Probe`, appends the source, then drops a single `MediaClip` referencing it at `TimelineStart=0` on the first video track), and `CreateStub()` (three `MediaSource`s with hardcoded metadata + `IsMissing=true` so fake paths never probe — every stub bin entry and clip will render red once 1b / step 2 land, intentionally exercising the missing-source UI). | `ProjectSettings Settings`, `ObservableCollection<MediaSource> MediaSources`, `ObservableCollection<Track> Tracks` |
| [ProjectSettings](../Flicksy.VideoEditor/Project/ProjectSettings.cs) | Project-wide rendering settings. | `Framerate=30`, `ResolutionWidth=1920`, `ResolutionHeight=1080`, `AudioSampleRate=48000` — all observable |
| [Track](../Flicksy.VideoEditor/Project/Track.cs) | Ordered clips + transitions, of a single kind. | `Guid Id` (init), `TrackKind Kind` (init), `Name`, `ObservableCollection<Clip> Clips`, `List<Transition> Transitions` |
| [TrackKind](../Flicksy.VideoEditor/Project/TrackKind.cs) | Enum: `Video`, `Overlay`, `Audio`. | |
| [Clip](../Flicksy.VideoEditor/Project/Clip.cs) (abstract) | Common clip surface. | `Guid Id` (init), `int TimelineStart`, abstract `int Duration { get; }` |
| [MediaSource](../Flicksy.VideoEditor/Project/MediaSource.cs) | Project's record of one imported video/audio file — the bin UI is a view over `Project.MediaSources`. Static factory `Probe(path)` opens via `FFMediaToolkit` (`MediaMode.AudioVideo`); throws on `MediaFile.Open` failure (caller surfaces the per-file error). `IsMissing` is the runtime flag for a source whose file is no longer openable; only an explicit re-probe flips it today (load-time detection lands with save/load). See [ADR 0003](adr/0003-media-bin-model.md). | `Guid Id` (init), `SourcePath`, `DisplayName`, `Duration`, `HasVideo`/`HasAudio`, video-only `Width`/`Height`/`SourceFramerate`, audio-only `SampleRate`/`ChannelCount`, `IsMissing` |
| [MediaClip](../Flicksy.VideoEditor/Project/MediaClip.cs) | References a `MediaSource` by id; the file path lives on the source only. `Duration` is computed from `(SourceOut - SourceIn) / Speed * Framerate`. `Framerate` mirrors the parent project's setting and is set by `Project` factories — the clip stores it locally so `Duration` stays a parameterless getter without a back-reference. `Source` is a transient `[JsonIgnore]` convenience ref populated by `Project` factories (and, in the future, the load resolver); **all mutations drive off `MediaSourceId` lookups in `Project.MediaSources`** — never trust the denormalized ref as source of truth. | `Guid MediaSourceId` (init), `SourceIn`/`SourceOut` (`TimeSpan`), `Speed=1.0`, `Volume=1.0`, `ClipStreams Streams` (default `Both`), `Framerate`, transient `MediaSource? Source`, `Transform2D Transform`, `ObservableCollection<Filter> Filters` |
| [ClipStreams](../Flicksy.VideoEditor/Project/ClipStreams.cs) | Enum: `Both`, `Video`, `Audio`. Which streams of the referenced `MediaSource` the compositor renders. Track-kind / stream compatibility is enforced by the drop matrix and split operation (step 2), not by the model. | |
| [GraphicsClip](../Flicksy.VideoEditor/Project/GraphicsClip.cs) | Time-bounded container of `DrawingItem`s. Settable duration lives on `DurationFrames` (C# disallows widening a get-only abstract override with a setter; `Duration` reads through `DurationFrames`). | `int DurationFrames`, `Transform2D Transform`, `ObservableCollection<DrawingItem> Items` |
| [Transform2D](../Flicksy.VideoEditor/Project/Transform2D.cs) | Per-clip 2D transform. No `MatrixTransform` — the compositor builds the matrix at render time. | `Point Position`, `Vector Scale`, `double RotationDegrees`, `Rect? CropRect` — all observable |
| [Transition](../Flicksy.VideoEditor/Project/Transition.cs) | Boundary object between two adjacent clips on a track (not a clip — see ADR 0002). Stored in `Track.Transitions`. | `Guid LeftClipId` (init), `Guid RightClipId` (init), `TransitionType Type`, `int Duration` (frames) |
| [TransitionType](../Flicksy.VideoEditor/Project/TransitionType.cs) | Enum (v1 set): `Crossfade`, `FadeToBlack`, `FadeFromBlack`, `WipeLeft`, `WipeRight`. | |
| [Filter](../Flicksy.VideoEditor/Project/Filter.cs) | Abstract marker. Concrete filter types land in a later slice. | |

No UI consumes this model yet; later issues (#7 timeline, #10 compositor) bind to it.

### 5.4 ViewModels

Rooted at [VideoEditorViewModel](../Flicksy.VideoEditor/ViewModels/VideoEditorViewModel.cs). The shell binds to this VM as its `DataContext`; the per-surface sub-VMs (`Preview`, `Transport`, `Timeline`, `Inspector`, `MediaBin`) are owned references. The center-column controls have their `DataContext` rebound onto the matching sub-VM in [VideoEditorWindow.xaml](../Flicksy.VideoEditor/Windows/VideoEditorWindow.xaml) so each view binds against its own VM directly.

| VM | Owns / Coordinates |
| --- | --- |
| [VideoEditorViewModel](../Flicksy.VideoEditor/ViewModels/VideoEditorViewModel.cs) | Root. Holds `Project`, the five sub-VMs (`Preview`, `Transport`, `Timeline`, `Inspector`, `MediaBin`), plus shell UI state: `ProjectName`, `SelectedClip` (`Clip?`), `CurrentLeftTab` ([LeftRailTab](../Flicksy.VideoEditor/ViewModels/LeftRailTab.cs)), `CurrentRightTab` ([RightRailTab](../Flicksy.VideoEditor/ViewModels/RightRailTab.cs)), `IsLeftPanelOpen`, `IsRightPanelOpen`. `LeftRailItems`/`RightRailItems` are built once in the ctor for the rails. `UndoCommand`/`RedoCommand`/`ExportCommand` are no-ops in this slice. |
| [PreviewViewModel](../Flicksy.VideoEditor/ViewModels/PreviewViewModel.cs) | Backs the Preview surface. Currently just exposes `ProjectSettings` so the view can aspect-lock to the project resolution. Will hold the rendered-frame `ImageSource` once the compositor lands (#10/#11). |
| [TransportViewModel](../Flicksy.VideoEditor/ViewModels/TransportViewModel.cs) | Backs the Transport bar. Owns `Playhead` (int frame), `IsPlaying`, and snapshotted `TotalFrames` (max `TimelineStart+Duration` across all clips, captured in the ctor). Computed readouts: `CurrentTimecode`, `TotalTimecode`, `PlayPauseLabel`. Commands: `PlayPauseCommand` flips `IsPlaying`, `PrevFrameCommand`/`NextFrameCommand` step `Playhead` by ±1 clamped to `[0, TotalFrames]`. All no-op stubs — real playback wiring lands in #11. |
| [TimelineViewModel](../Flicksy.VideoEditor/ViewModels/TimelineViewModel.cs) | Backs the timeline surface. Holds the document `Project`, the sibling `TransportViewModel` (for the `Playhead` overlay and zoom centering), `PixelsPerFrame` (zoom level, clamped `[0.25, 60]` via `ZoomBy`), and `SelectedClip`. Selection is two-way mirrored with `VideoEditorViewModel.SelectedClip` via `PropertyChanged` so a clip click in the timeline flows up to the root, and any clear from the root flows back down. |
| [InspectorViewModel](../Flicksy.VideoEditor/ViewModels/InspectorViewModel.cs) | Stub — populated in #15–#18. |
| [MediaBinViewModel](../Flicksy.VideoEditor/ViewModels/MediaBinViewModel.cs) | Backs the [MediaPanel](../Flicksy.VideoEditor/Controls/Panels/MediaPanel.xaml). Holds the `Project` ref, mirrors `Project.MediaSources` 1:1 into an `ObservableCollection<MediaSourceViewModel>` named `MediaSources` (kept in sync via `CollectionChanged`), and exposes single-select `SelectedSource` + `IsEmpty`/`HasEntries` for the empty-state hint. `ImportCommand` opens an `OpenFileDialog` (`Multiselect=true`, video/audio extension filter) and routes per-file through `TryImportFile` — normalizes path (`Path.GetFullPath` + `OrdinalIgnoreCase`), silently dedupes against existing entries, calls `MediaSource.Probe`, and on probe-throw shows a per-file `MessageBox` without adding an entry. `TryImportFiles` is the shared entry point that Explorer drag-drop also calls. `RevealCommand` runs `explorer.exe /select,"<path>"`. **Probe is synchronous on the UI thread** (50–150 ms per file, sequential for multi-file imports). Thumbnail decode runs on a single background worker over an unbounded `Channel<MediaSource>`; results post back via the UI dispatcher. Audio-only sources short-circuit the worker and get `Images.music_file` immediately; missing sources skip both paths. |
| [MediaSourceViewModel](../Flicksy.VideoEditor/ViewModels/MediaSourceViewModel.cs) | Per-entry wrapper around a `MediaSource` for the bin's view. Carries the WPF `ImageSource? Thumbnail` so the document model stays free of WPF types (POCO invariant — see ADR 0002). The view binds against `Source.*` for model state. |
| [RailItem](../Flicksy.VideoEditor/ViewModels/RailItem.cs) | Plain record-like class consumed by `RailView.ItemsSource`. `Label` + `Glyph` (placeholder icon) + `Tag` (the `LeftRailTab`/`RightRailTab` value the rail selects on click). |

### 5.5 Controls

| Control | Purpose |
| --- | --- |
| [PreviewView](../Flicksy.VideoEditor/Controls/PreviewView.xaml.cs) | Center-column preview surface. `DataContext` is [PreviewViewModel](../Flicksy.VideoEditor/ViewModels/PreviewViewModel.cs). A single `Image` with `Stretch=Uniform`; its source is a `DrawingImage` wrapping a filled rect at `ProjectSettings.ResolutionWidth × ResolutionHeight`, so the project resolution dictates aspect ratio and Uniform stretching produces the letterbox automatically. Subscribes to `ProjectSettings.PropertyChanged` to regenerate on resolution edits; real frame content arrives by reassigning `PreviewImage.Source` once the compositor lands (#10/#11). |
| [TransportView](../Flicksy.VideoEditor/Controls/TransportView.xaml.cs) | Center-column transport bar. `DataContext` is [TransportViewModel](../Flicksy.VideoEditor/ViewModels/TransportViewModel.cs). Prev/PlayPause/Next buttons centered between left-aligned current-timecode and right-aligned total-timecode labels — all bindings hit the transport VM directly, no code-behind logic. |
| [TimelineView](../Flicksy.VideoEditor/Controls/TimelineView.xaml.cs) | Center-column timeline surface. `DataContext` is [TimelineViewModel](../Flicksy.VideoEditor/ViewModels/TimelineViewModel.cs). A 2×2 grid with three `ScrollViewer`s: `RulerScroller` (row 0, col 1) hosts [TimeRulerView](../Flicksy.VideoEditor/Controls/Timeline/TimeRulerView.cs), `HeadersScroller` (row 1, col 0) hosts the pinned per-track headers, and `MainScroller` (row 1, col 1) hosts the lanes + [PlayheadView](../Flicksy.VideoEditor/Controls/Timeline/PlayheadView.cs) overlay. `RulerScroller` and `HeadersScroller` have hidden scrollbars and are slaved to `MainScroller.{HorizontalOffset, VerticalOffset}` via `ScrollChanged` — so headers stay put when the timeline pans right, ruler stays aligned when content scrolls horizontally. Small 12px margins on the slave scrollers reserve gutter space matching `MainScroller`'s scrollbars. Wheel + click handlers attach to the outer `Border` so they fire over any sub-scroller: plain wheel = horizontal pan, Shift+wheel = vertical pan, Ctrl+wheel = `TimelineViewModel.ZoomBy` centered on the playhead. Empty tracks (`Clips.Count == 0`) collapse out of both ItemsControls via a shared `DataTrigger` — `ObservableCollection<T>` raises `PropertyChanged("Count")` so the trigger reacts as clips are added/removed. |
| [TimeRulerView](../Flicksy.VideoEditor/Controls/Timeline/TimeRulerView.cs) | `FrameworkElement` (no XAML) drawn via `OnRender`. Picks a tick step from a fixed `{1,2,5,10,15,30,60,…}`-seconds ladder so labels stay ≥70px apart at any `PixelsPerFrame`. Minor ticks at quarter-steps when they're ≥8px apart. Width measured from `Transport.TotalFrames × PixelsPerFrame`. Subscribes to `PixelsPerFrame` changes for redraw. Click + drag scrubs the playhead. |
| [TrackHeaderView](../Flicksy.VideoEditor/Controls/Timeline/TrackHeaderView.xaml.cs) | Left-side per-track header (120×56 px to align with the matching lane row). Shows `Track.Name` plus three visual-only Mute / Lock / Disable `ToggleButton`s — not bound to model state yet; see issue #10 comment thread for the planned `Track.{Muted, Locked, Disabled}` fields. |
| [ClipsLaneView](../Flicksy.VideoEditor/Controls/Timeline/ClipsLaneView.cs) | `Canvas` subclass that owns a `Dictionary<Clip, ClipView>` and creates one [ClipView](../Flicksy.VideoEditor/Controls/Timeline/ClipView.xaml.cs) per `Track.Clips` entry at `Canvas.Left = TimelineStart × PixelsPerFrame`, width `Duration × PixelsPerFrame`. Two DPs (`Track`, `Timeline`) make ownership explicit. Subscribes to `Track.Clips.CollectionChanged` (full rebuild — clip counts are small), each clip's `PropertyChanged` for `TimelineStart`/`Duration` re-layout, and `Timeline.PropertyChanged` for `PixelsPerFrame` re-layout + `SelectedClip` highlight propagation. `MeasureOverride` reports `TotalFrames × PixelsPerFrame` so the host ScrollViewer scrolls correctly. |
| [ClipView](../Flicksy.VideoEditor/Controls/Timeline/ClipView.xaml.cs) | Per-clip visual. Typed `DataTemplate`s in the XAML pick the per-subtype look (`MediaClip` = blue + `Source.DisplayName`, `GraphicsClip` = purple + "Graphics" label) — add a new subtype by adding another `DataTemplate`. `IsSelected` DP, pushed down by `ClipsLaneView`, toggles a yellow border. Click walks the visual tree to find the host `TimelineViewModel` and writes `SelectedClip` directly (root mirrors back). |
| [PlayheadView](../Flicksy.VideoEditor/Controls/Timeline/PlayheadView.cs) | `FrameworkElement` overlay. Draws a single red vertical line at `Transport.Playhead × PixelsPerFrame` across its full height. Hit-test-transparent so clip clicks pass through. Subscribes to both `TimelineViewModel.PixelsPerFrame` and `TransportViewModel.Playhead` for redraw. |
| [RailView](../Flicksy.VideoEditor/Controls/Rail/RailView.xaml.cs) | Vertical icon-button strip backing both rails. DPs: `ItemsSource` (`IEnumerable` of `RailItem`), `SelectedTag` (two-way, the selected item's `Tag`), `IsPanelOpen` (two-way), `ItemsEnabled` (gates every button — currently unused; the right rail is hidden entirely when there's no selection). Click handling lives entirely in `PreviewMouseLeftButtonDown`: clicking a new item sets `SelectedTag` + `IsPanelOpen = true`; clicking the already-selected item toggles `IsPanelOpen`. `ListBox`'s built-in selection is bypassed because `ListBoxItem.Focusable=False` (set so the icons don't show a focus rect) makes `HandleMouseButtonDown` bail before raising `SelectionChanged`. |
| [MediaPanel](../Flicksy.VideoEditor/Controls/Panels/MediaPanel.xaml) | The Media tab's left-panel content. `DataContext` is [MediaBinViewModel](../Flicksy.VideoEditor/ViewModels/MediaBinViewModel.cs) (rebound in [VideoEditorWindow.xaml](../Flicksy.VideoEditor/Windows/VideoEditorWindow.xaml) via `DataContext="{Binding MediaBin}"`). Top toolbar with an Import button; body swaps between a "No media imported" hint (when `IsEmpty`) and a `ListBox` whose `ItemsPanel` is a `WrapPanel` of 120×100 cells (thumbnail/glyph + name + duration badge). `AllowDrop` + `Drop` route Explorer file drops through `MediaBinViewModel.TryImportFiles` so dedup + probe-failure handling stay uniform with the Import button. Right-click context menu on a cell offers **Reveal in Explorer** only (rename / relocate / remove arrive in 1b); the menu reaches `RevealCommand` via `PlacementTarget.Tag.RevealCommand` because the cell's `ItemContainerStyle` sets `Tag` to the bin VM. |
| [StubSurface](../Flicksy.VideoEditor/Controls/StubSurface.cs) | Base `UserControl` for placeholder panel/inspector content. Centers a label with the tab name. Subclasses are one-line `public sealed class XxxPanel : StubSurface { public XxxPanel() : base("Xxx") { } }`. |

Stub panel + inspector classes (one per rail tab; replaced by real controls as later slices land):

- `Controls/Panels/`: `TextPanel`, `ShapesPanel`, `PenPanel`, `TransitionsPanel` (the Media tab is now backed by the real `MediaPanel` above)
- `Controls/Inspectors/`: `SpeedInspector`, `AudioInspector`, `AdjustColorsInspector`, `FiltersInspector`, `FadeInspector`

Converters in [Converters/](../Flicksy.VideoEditor/Converters):

| Converter | Purpose |
| --- | --- |
| [EnumToVisibilityConverter](../Flicksy.VideoEditor/Converters/EnumToVisibilityConverter.cs) | `value == ConverterParameter` → `Visible`, else `Collapsed`. Used to swap left/right panel content by `CurrentLeftTab` / `CurrentRightTab`. |
| [DurationConverter](../Flicksy.VideoEditor/Converters/DurationConverter.cs) | Formats a `TimeSpan` as `m:ss` (under an hour) or `h:mm:ss`. Used by the bin cell's duration badge. |

### 5.6 Shell layout

[VideoEditorWindow.xaml](../Flicksy.VideoEditor/Windows/VideoEditorWindow.xaml) is the only editor window. Two rows: top toolbar (auto) + body (star). Body is 5 columns: left rail (44 fixed) · left panel (`x:Name="LeftPanelColumn"`, 280 default, MinWidth 0) · center (`*`, MinWidth 320) · right panel (`x:Name="RightPanelColumn"`, same) · right rail (`x:Name="RightRailColumn"`, 44 default, MinWidth 0). Both panels host all five stub controls stacked in the same `Grid`, each toggled visible by `EnumToVisibilityConverter` against the corresponding `CurrentXxxTab` value.

Only the left panel is user-resizable. Its `GridSplitter` is a direct child of the outer body grid (not nested inside the panel content) — a splitter only resizes its direct parent grid's columns. Placement: `Grid.Column="1"`, HorizontalAlignment=Right, default `BasedOnAlignment` resize behavior, which combined with the alignment picks the splitter's own column + the next (so dragging shrinks the panel and grows the center, or vice versa). Splitter `Visibility` is bound to `IsLeftPanelOpen` so a collapsed panel hides its splitter. The right panel is fixed-width (280) — no splitter. The center column has three outer rows: Preview (`*`, MinHeight 200), a horizontal `GridSplitter` (`Auto`), and a Transport+Timeline sub-grid (`2*`, MinHeight 200). The sub-grid stacks Transport (`Auto`) above Timeline (`*`, MinHeight 160); grouping them this way means the splitter resizes Preview vs the whole bottom block, and Transport keeps a fixed height while Timeline absorbs the remainder.

[VideoEditorWindow.xaml.cs](../Flicksy.VideoEditor/Windows/VideoEditorWindow.xaml.cs) owns the panel collapse logic. Binding `ColumnDefinition.Width` to a converter would have worked for toggle, but `GridSplitter` writes `Width` directly on drag — that would clobber the binding. Instead, the code-behind subscribes to `ViewModel.PropertyChanged` for `IsLeftPanelOpen`/`IsRightPanelOpen` and writes the column `Width` itself: for the left panel, collapse stashes the current `Width` in a private field (so a drag-resized value is preserved) and sets `Width=0`, expand restores from the stash. The right panel always toggles 0 ↔ 280 since it can't be resized.

The right rail itself is also code-behind-managed: when `SelectedClip` is null, the right-rail column collapses to 0 (rail hidden) and `IsRightPanelOpen` is forced false. When a clip is selected, the column expands back to 44.

## 6. End-to-end flow (cheat sheet)



1. User presses `Ctrl+Shift+Alt+S`. [HotKeyWindow](../Flicksy.Agent/HotKeyWindow.cs) → [LaunchSnipper](../Flicksy.Agent/AgentApplicationContext.cs).
2. [PreSnipOverlayWindow](../Flicksy.Snipper/Overlays/PreSnipOverlayWindow.xaml.cs) appears on cursor's monitor with a frozen-screen background. User picks **Snip** or **Record** + drags a rect.
3. **Snip path**: bitmap → PNG in `%TEMP%`, copied to clipboard, then `Flicksy.PostSnip.exe "<path>"` ([SnipperSessionController.OnSnipCaptured](../Flicksy.Snipper/SnipperSessionController.cs)).
4. **Record path**: [VideoRecordingOverlayWindow](../Flicksy.Snipper/Overlays/VideoRecordingOverlayWindow.xaml.cs) → ffmpeg gdigrab → MP4 in `%TEMP%` → `Flicksy.PostSnip.exe "<path>"`.
5. PostSnip [App.OnStartup](../Flicksy.PostSnip/App.xaml.cs) initializes FFmpeg, builds the DI host, resolves `PostSnipWindow`, loads the media (`LoadImage` or `LoadVideoAsync`).
6. User annotates (Pen/Shape/Text/Erase via tools), navigates (pan/zoom/scrub), undoes (Ctrl+Z), saves (PNG or copied MP4) or cancels. PostSnip deletes the temp file on close unless `PreserveMediaFile` was set.

**VideoEditor entry paths**:

- **No args**: `Flicksy.VideoEditor.exe` → DI resolves [WelcomeWindow](../Flicksy.VideoEditor/Windows/WelcomeWindow.xaml.cs). Clicking `New Video Project` resolves a `VideoEditorWindow` from `App.Services` (VM built over `Project.CreateEmpty()` via the registered transient factory), reassigns `MainWindow`, then closes Welcome.
- **Agent tray → VideoEditor**: tray menu's `New Video Project` item ([AgentApplicationContext.LaunchVideoEditor](../Flicksy.Agent/AgentApplicationContext.cs)) spawns `Flicksy.VideoEditor.exe --new-video-project` → [App.ResolveStartupMode](../Flicksy.VideoEditor/App.xaml.cs) → `EmptyEditor` → `VideoEditorWindow` resolved from DI (empty project), Welcome skipped.
- **PostSnip → VideoEditor**: after a recording opens in PostSnip, the chrome's `Launch in video editor` button (video-only) sets `PreserveMediaFile=true` and spawns `Flicksy.VideoEditor.exe "<videoPath>"` ([PostSnipViewModel.LaunchInVideoEditor](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs)) → `EditorWithSource(path)` → `VideoEditorWindow` constructed manually around `Project.CreateFromSourceFile(path)` (DI doesn't supply the runtime path) so the video opens as the first clip on Video 1.

## 7. Flicksy.Icons (icon assets)

`net10.0` class library. No WPF; consumers convert `System.Drawing.Bitmap` to `ImageSource` via `BitmapExtensions.ToImageSource()` (in Drawing).

| File | Purpose |
| --- | --- |
| [Resources/*.png](../Flicksy.Icons/Resources) | Toolbar + shape + rotate-puck icons + media-bin audio glyph, 19 PNGs. |
| [Resources/music-file.png](../Flicksy.Icons/Resources/music-file.png) | Audio-source glyph used by the media bin (`Images.music_file`). |
| [Properties/Resources.resx](../Flicksy.Icons/Properties/Resources.resx) | ResXFileRef entries pointing at the PNGs. |
| [Properties/Resources.Designer.cs](../Flicksy.Icons/Properties/Resources.Designer.cs) | Strongly-typed `public` accessor. **Hand-edited from internal → public**; csproj `Generator` is `PublicResXFileCodeGenerator` so future regens stay public. |

The alias is **declared once per consumer csproj** as a csproj-level global using; no per-file `using` directive is needed:

```xml
<ItemGroup>
  <Using Include="Flicksy.Icons.Properties.Resources" Alias="Images" />
</ItemGroup>
```

[Flicksy.Drawing.csproj](../Flicksy.Drawing/Flicksy.Drawing.csproj), [Flicksy.PostSnip.csproj](../Flicksy.PostSnip/Flicksy.PostSnip.csproj), and [Flicksy.VideoEditor.csproj](../Flicksy.VideoEditor/Flicksy.VideoEditor.csproj) all declare this. Call sites use it bare: `Images.rotate`, `Images.circle`, `Images.cursor.ToImageSource()`, `Images.music_file.ToImageSource()`, etc.

The alias name is `Images` rather than `Icons` because a using-alias of `Icons` would be shadowed by the `Flicksy.Icons` namespace at every call site (C# §13.6 resolves namespace members before using aliases).

## 8. Conventions seen in this codebase

- **MVVM via CommunityToolkit.Mvvm**: `[ObservableProperty]` on private fields generates the public property; `[RelayCommand]` on a private method generates a public `XxxCommand`. Don't hand-roll PropertyChanged.
- **Tool extensibility**: new tools implement [IDrawingTool](../Flicksy.Drawing/Interaction/IDrawingTool.cs), get instantiated + registered in [DrawingView.OnDataContextChanged](../Flicksy.Drawing/Controls/DrawingView/DrawingView.xaml.cs), and depend on small `IXxxConfig` interfaces — not on `DrawingView` directly.
- **Undo commands**: state is mutated live during the gesture; the command is pushed at the **end** of the gesture with before/after snapshots. Multi-step bundles use [CompositeCommand](../Flicksy.Drawing/Undo/Commands/CompositeCommand.cs).
- **No emojis, comments only when WHY is non-obvious** (see existing comments — most explain a subtle invariant or a workaround).
- **No file watcher / hot reload / live config** — `appsettings.json` is read once at startup.
- **No tests** in the repo currently.

## 9. Where to look for common changes

| Change request | Primary file(s) |
| --- | --- |
| Add a new drawing tool | new file in [Interaction/Tools/](../Flicksy.Drawing/Interaction/Tools), config interface in [Interaction/Config/](../Flicksy.Drawing/Interaction/Config), wire in [DrawingView.OnDataContextChanged](../Flicksy.Drawing/Controls/DrawingView/DrawingView.xaml.cs) + toolbar enum in [ImageEditToolsViewModel](../Flicksy.PostSnip/ViewModels/ImageEditToolsViewModel.cs) + button in [ImageEditToolsView.xaml](../Flicksy.PostSnip/Controls/ImageEditToolsView.xaml). |
| Add a new drawing item type | new class in [Source/](../Flicksy.Drawing/Source) inheriting `DrawingItem`, DataTemplate in [DrawingView.xaml](../Flicksy.Drawing/Controls/DrawingView/DrawingView.xaml). |
| Change the global hotkey | [HotKeyWindow](../Flicksy.Agent/HotKeyWindow.cs). |
| Change capture format/quality | [ScreenRecorder.BuildArguments](../Flicksy.Snipper/ScreenRecorder.cs). |
| Add a new undoable action | new `IUndoableCommand` in [Drawing/Undo/Commands/](../Flicksy.Drawing/Undo/Commands) (shared) or [PostSnip/Undo/Commands/](../Flicksy.PostSnip/Undo/Commands) (snip-specific like crop), push from the call site **after** mutation. |
| Change crop UI / behavior | [CropOverlayView](../Flicksy.PostSnip/Controls/CropOverlayView.xaml.cs) for visuals + gestures, [CropOverlayViewModel](../Flicksy.PostSnip/ViewModels/CropOverlayViewModel.cs) for state. The save side lives in [PostSnipViewModel.SaveImageWithDrawing](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs). |
| Modify video playback behavior | [FFmpegVideoPlayer](../Flicksy.Drawing/Media/FFmpegVideoPlayer.cs) (engine) + [VideoPlaybackOverlay](../Flicksy.PostSnip/Controls/VideoPlaybackOverlay.xaml.cs) (UI). |
| Modify save format | [PostSnipViewModel.Save](../Flicksy.PostSnip/ViewModels/PostSnipViewModel.cs) + `SaveImageWithDrawing`. |
| Change toolbar layout | [ImageEditToolsView.xaml](../Flicksy.PostSnip/Controls/ImageEditToolsView.xaml) + [PostSnipWindow.xaml](../Flicksy.PostSnip/PostSnipWindow.xaml). |
| Add a new shared icon | drop PNG into [Flicksy.Icons/Resources/](../Flicksy.Icons/Resources), add entry to [Resources.resx](../Flicksy.Icons/Properties/Resources.resx), regenerate `Resources.Designer.cs` (or hand-add a public property). Consume via `Images.<name>`. |
| Add a video-editor rail tab | extend [LeftRailTab](../Flicksy.VideoEditor/ViewModels/LeftRailTab.cs) or [RightRailTab](../Flicksy.VideoEditor/ViewModels/RightRailTab.cs), add a `RailItem` in [VideoEditorViewModel](../Flicksy.VideoEditor/ViewModels/VideoEditorViewModel.cs) ctor, add a stub class in `Controls/Panels/` or `Controls/Inspectors/`, wire its `Visibility` in [VideoEditorWindow.xaml](../Flicksy.VideoEditor/Windows/VideoEditorWindow.xaml) with `EnumToVisibilityConverter`. |
| Add a new timeline clip subtype | new class in [Project/](../Flicksy.VideoEditor/Project) inheriting `Clip`, plus a `DataTemplate` keyed to the type in [ClipView.xaml](../Flicksy.VideoEditor/Controls/Timeline/ClipView.xaml). |
| Import a media source | `MediaBinViewModel.TryImportFile` in [MediaBinViewModel](../Flicksy.VideoEditor/ViewModels/MediaBinViewModel.cs) (dedupe + probe + error surface); model side is [MediaSource.Probe](../Flicksy.VideoEditor/Project/MediaSource.cs). UI affordances: the Import button + Explorer drag-drop in [MediaPanel.xaml](../Flicksy.VideoEditor/Controls/Panels/MediaPanel.xaml). Extension filter lives on `MediaBinViewModel.AcceptedExtensions`. |
| Change timeline pan/zoom behavior | wheel handling in [TimelineView.xaml.cs](../Flicksy.VideoEditor/Controls/TimelineView.xaml.cs); clamp range on [TimelineViewModel.ZoomBy](../Flicksy.VideoEditor/ViewModels/TimelineViewModel.cs). |
| Change app-wide chrome (scrollbars, future common styles) | implicit styles in [Flicksy.VideoEditor/App.xaml](../Flicksy.VideoEditor/App.xaml). |
