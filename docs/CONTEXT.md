# Flicksy

A Windows desktop tool with two surfaces, each its own process:

- **Snip editor** — fast capture-and-annotate flow. One image or video in, drawings on top, save out. Lives in `Flicksy.PostSnip.exe` (existing `PostSnipWindow`, the project formerly called `Flicksy.Editor`).
- **Video editor** — non-linear video editor (NLE) for assembling clips on a timeline. Lives in `Flicksy.VideoEditor.exe`, sibling to `Flicksy.PostSnip.exe`.

Both processes share rendering primitives via the `Flicksy.Drawing` class library: drawing items, tools, undo plumbing, and FFmpeg playback primitives. Each process has its own document, its own undo stack, and its own ffmpeg init. They communicate only by file paths and CLI args, like the existing Agent → Snipper → Editor chain.

## Language

### Surfaces

**Snip**:
A one-shot annotation session over a single captured image or video. Lives in `PostSnipWindow`. No time axis on the document — drawings are timeless overlays painted on top of the captured media.
_Avoid_: "snipping tool" (overloaded with the Windows utility), "annotation".

**Video editor**:
The NLE surface for assembling multiple clips into a finished video. Lives in `VideoEditorWindow`. Document is a timeline.
_Avoid_: "movie editor", "video maker".

### Video editor document

**Project**:
The root document of a video editing session. Contains `ProjectSettings`, an ordered list of `Track`s, and (eventually) is serializable to a `.flicksy` JSON file. While unsaved, lives at a temp-folder path.
_Avoid_: "movie", "composition", "sequence".

**ProjectSettings**:
Mutable project-level settings: `Framerate`, `Resolution` (the canonical composite canvas), `AudioSampleRate`. `Resolution` can be changed mid-project.
_Avoid_: "project config".

**Track**:
An ordered horizontal lane on the timeline that contains `Clip`s. Has a `TrackKind` (`Video` / `Audio` / `Overlay`) used by the UI to constrain which clips can be dropped. The compositor does not branch on `TrackKind` — it branches on clip type.
_Avoid_: "layer", "row", "channel".

**Clip**:
The unit of content on a `Track`, occupying a half-open interval `[TimelineStart, TimelineStart+Duration)` in project time. Abstract base; concrete types are `MediaClip` and `GraphicsClip`. Clips on the same track do not overlap.
_Avoid_: "segment", "block", "item" (collides with `DrawingItem`).

**MediaClip**:
A `Clip` that references a `MediaSource` by id. Holds `MediaSourceId`, `SourceIn`, `SourceOut` (in source time), `TimelineStart`, `Speed`, `Streams` (`Video` / `Audio` / `Both`, default `Both`), per-clip `Transform` (position/scale/rotate/crop), `FilterChain`, `Volume`. Timeline duration is `(SourceOut - SourceIn) / Speed`. The underlying file path lives on the `MediaSource`, not the clip — many clips can share one source. `Streams` controls what the compositor renders: dropping a video+audio source on a Video track creates `Streams=Both`; the per-clip **Split audio** command flips a `Streams=Both` clip to `Streams=Video` and adds a paired `Streams=Audio` clip on a freshly-created Audio track named `"<source video track> (Audio)"` (with `" N"` de-collision suffix if the name is already in use). Each split creates its own new track. Video tracks accept `Streams ∈ {Both, Video}` in any mix; Audio tracks accept `Streams=Audio` only.
_Avoid_: "video clip" (it can be audio-only), "source clip".

**MediaSource**:
A first-class entity in `Project.MediaSources` representing an imported video/audio file the project knows about. Probed at import via `FFMediaToolkit`. Fields: `Id`, `SourcePath`, `DisplayName` (defaults to filename, user-renameable), `Duration`, `HasVideo`, `HasAudio`, video-only `Width`/`Height`/`SourceFramerate`, audio-only `SampleRate`/`ChannelCount`, and a runtime `IsMissing` flag. The Media bin in the UI is a view over `Project.MediaSources`. `MediaClip`s reference a `MediaSource` by `Id`, so relocating one missing file fixes every clip that used it.
_Avoid_: "asset", "media item", "import", "bin entry" (the bin is just the UI for the list).

**GraphicsClip**:
A `Clip` that holds a list of `DrawingItem`s (the existing pen/shape/text items) visible only between `TimelineStart` and `TimelineStart+Duration`. Has a `Transform` like `MediaClip`. The Snip editor's drawings live in a flat list of `DrawingItem`s; the Video editor's drawings live inside a `GraphicsClip`.
_Avoid_: "overlay clip" (overlay is a *track kind*, not a clip kind), "text clip" (it's more general).

**Transition**:
A blended boundary between two adjacent `MediaClip`s on the same track. Properties: `LeftClip`, `RightClip`, `Type` (crossfade, fade-to-black, wipe, ...), `Duration`. **Not a `Clip`** — it is a relationship between two clips, stored on the track in a separate list.
_Avoid_: "fade", "blend", "transition clip".

**TimelineTime / SourceTime**:
Two distinct time domains. `TimelineTime` is position in the assembled project; `SourceTime` is position inside a source file. A `MediaClip` maps a `[SourceIn, SourceOut]` window in `SourceTime` to a `[TimelineStart, TimelineStart+Duration]` window in `TimelineTime`, possibly scaled by `Speed`.
_Avoid_: using bare "time" when which one matters.

**Frame**:
The canonical unit of `TimelineTime`. All clip positions and durations are integer frame counts at the project's `Framerate`. Sub-frame audio offsets use samples, not fractional frames.
_Avoid_: "second" or `double`-seconds as a storage unit.

### Shared primitives (snip + video editor)

**DrawingItem**:
A visual primitive (`PenStrokeItem`, `ShapeItem`, `TextItem`) with `Geometry`, a `MatrixTransform`, hit-testing, and a `Render(DrawingContext)` method. Used by both surfaces:
- In the snip editor: lives directly in `DrawingViewModel.Items`, always visible.
- In the video editor: lives inside a `GraphicsClip`, visible only during the clip's time window.
_Avoid_: "shape" (collides with `ShapeItem`), "annotation", "overlay element".

**Drawing tool**:
A handler implementing `IDrawingTool` (`PenTool`, `ShapeTool`, `TextTool`, `SelectTool`, `EraseTool`) that converts pointer gestures into `DrawingItem` mutations. Used by both surfaces unchanged — the tools don't know whether the items they emit will end up in a flat list or a `GraphicsClip`.
_Avoid_: "brush" (means stroke color/fill in WPF).

## Flagged ambiguities

**"Overlay"** is overloaded:
- A `TrackKind.Overlay` is a track that the UI lets you put `GraphicsClip`s on.
- A `GraphicsClip` is not called an "overlay clip" — that's a track-kind property leaking into the clip name.
- The snip editor's `SelectionOverlayView` / `CropOverlayView` are *UI overlays* in WPF terms, unrelated to the video editor concept.
Disambiguate with `TrackKind.Overlay` or "UI overlay" rather than bare "overlay".

**"Item"**:
- `DrawingItem` is a visual primitive (pen stroke, shape, text).
- A `Clip` is *not* an item; never call it one. `DrawingViewModel.Items` is the snip editor's flat drawing list — in the video editor the equivalent name is `Project.Tracks[k].Clips`.

## Example dialogue

> **Dev:** Where should the per-clip volume control live in the UI?
> **Domain:** It's a property of the `MediaClip` — only `MediaClip`s have audio. `GraphicsClip`s and `Transition`s don't.
> **Dev:** What about the overall mix?
> **Domain:** That's not a project-level setting; it's just every `MediaClip`'s `Volume`. You can mute a whole track in the UI but the model truth is per-clip.
> **Dev:** And when the user splits a clip at the playhead?
> **Domain:** You're splitting at a `TimelineTime`, which is an integer `Frame`. Convert that to the clip's `SourceTime` via the speed mapping, then create two `MediaClip`s sharing the original `SourcePath` — one with the original `SourceIn` and the new in-between `SourceOut`, the other with the in-between point as its new `SourceIn` and the original `SourceOut`. Any `Transition` attached to the original on its right edge moves to the right new clip.
