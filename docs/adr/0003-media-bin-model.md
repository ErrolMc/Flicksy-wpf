# Media bin model

## Decision

Issue #9 (the Media bin) extends the document model from [ADR 0002](./0002-video-editor-document-model.md) in two ways:

**`MediaSource` becomes a first-class document entity.** `Project` gains an `ObservableCollection<MediaSource> MediaSources`. A `MediaSource` is the project's record of one imported file and holds `Id` (Guid), `SourcePath`, `DisplayName` (user-renameable, defaults to filename), `Duration`, `HasVideo`/`HasAudio`, video-only `Width`/`Height`/`SourceFramerate`, audio-only `SampleRate`/`ChannelCount`, and a runtime `IsMissing` flag. The bin UI is a view over `Project.MediaSources`. Imports dedupe by normalized path (`Path.GetFullPath` + `OrdinalIgnoreCase`).

**`MediaClip` references its source by id, and gains a `Streams` field.** `MediaClip.SourcePath` (`string`) is replaced by `MediaClip.MediaSourceId` (`Guid`) — the path lives on the `MediaSource` only. A new `ClipStreams` enum (`Video | Audio | Both`, default `Both`) controls which streams of the referenced source the compositor renders. Track-kind / stream compatibility is enforced by the drop matrix and the split operation, not by the model — the model is permissive about which `Streams` value can live on which `TrackKind`.

## Why

- **`MediaSource` as a first-class entity** lets one path change ("relocate this missing file") fix every clip that referenced it, gives a natural home for probed metadata (duration, dimensions, stream availability) without re-probing on every render, and is what every NLE does. The alternative (clips carry `SourcePath`, the bin is derived from current timeline contents at load time) makes relocate-when-missing painful, eliminates the imported-but-unused entry, and forces probed metadata to be duplicated on every clip of the same source.

- **Source reference by id** keeps relocate surgical: only `MediaSource.SourcePath` changes, every clip continues to resolve via the stable `Id`. The alternative (each clip stores its own path) forces every clip to be updated on every relocate and creates an integrity hole — two clips of the "same" source can disagree on the path.

- **Per-clip `Streams` field** lets the user split one `Streams=Both` clip's audio off independently of other clips on the same track, without introducing a linked-clips concept. The alternative (per-track `RendersAudio` flag) forces split to be track-scope, which is coarser than the per-clip flow users expect from FCP and Resolve.

- **Model permissive, UI opinionated** localizes compatibility rules. The drop matrix and split operation are the only places that need updating if track-kind/stream-kind compatibility changes; the model and serialization stay stable.

## Considered Options

- **Bin as a ViewModel-only list (not in the document model).** Rejected. Clips still need to reference imports somehow; the bin would have to be reconstructed from the set of distinct `SourcePath`s on the timeline at load. Lost the imported-but-unused entry, lost the natural home for probed metadata, lost the surgical relocate.

- **Global cross-project media library.** Rejected. Adds a new persistence model (where on disk? owned by what?) for marginal benefit. Per-project matches the rest of the document model's scoping.

- **Dedupe by content hash.** Rejected. Hashing every file on import is significant cost for large videos. Path-based dedupe matches the filesystem's own identity model and user expectations.

- **Per-track `RendersAudio` flag instead of per-clip `Streams`.** Rejected. Forces split to be track-scope. Adding `Streams` later would be a breaking model change; starting per-clip is no harder and is forward-compatible with track-scope behaviors when needed.

- **Linked-clip pairs (Premiere-style: video+audio sources create two linked `MediaClip`s).** Rejected for this slice. Would require a `LinkedClipId` field plus invariant-maintenance across move/trim/split/delete on every clip operation — its own design pass. The per-clip `Streams` design admits a linked-clips overlay later without restructuring.

## Consequences

- `Project.CreateFromSourceFile(path)` now creates a `MediaSource` first, adds it to `MediaSources`, then constructs a `MediaClip` referencing that `MediaSource.Id`. Same restructure for `Project.CreateStub`.

- Save/load (future) carries `Project.MediaSources` as part of the project. Thumbnail persistence is its own decision at that point; the in-memory thumbnail worker added in #9 does not persist.

- The compositor branches on `MediaClip.Streams` (and on clip type, as before) to decide what to render. `TrackKind` is purely a UI hint constraining drops and split outputs.

- Relocate is a `MediaSource`-level operation. Every clip referencing the `MediaSource.Id` is affected automatically. Clips whose `SourceOut` exceeds the new source's duration, or whose `Streams` requires a stream the new source lacks, render as red on the timeline — same visual treatment as the source-missing case.

- `Track` will need an `IsMuted` flag wired to the existing header toggle when playback lands (#10/#11). That decision is outside this ADR because it doesn't affect persistence or document shape — it's a render-time flag.
