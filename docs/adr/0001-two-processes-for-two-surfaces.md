# Two processes for the two surfaces

## Decision

The snip editor (`Flicksy.PostSnip.exe`) and the video editor (`Flicksy.VideoEditor.exe`) are **separate processes**. They share rendering primitives (drawing items, tools, undo manager, FFmpeg playback) via a new class library project `Flicksy.Drawing`. Neither WinExe project references the other.

This brings the solution to **5 projects**: `Flicksy.Agent`, `Flicksy.Snipper`, `Flicksy.PostSnip`, `Flicksy.VideoEditor`, `Flicksy.Drawing`.

## Why

- **Memory footprint.** The NLE will hold decoded video frames, thumbnails, audio buffers, and (eventually) GPU surfaces. None of that should be loaded when the user just wants to annotate a screenshot.
- **Crash isolation.** The compositor is going to be the most fault-prone code in the product (multi-stream decode, GPU interop, audio mixing, filter chains). A compositor crash should not kill an in-progress snip annotation.
- **Independent evolution.** The video editor is an order of magnitude larger than the snip editor and will dominate the codebase. Keeping them in one process means every video-editor build affects the snip editor's startup time and binary size.

## Considered Options

- **Same process, two windows.** Rejected — concentrates the failure modes and memory cost on every launch, even for a 5-second snip.
- **Source duplication of shared code.** Rejected — `DrawingItem`, the tools, and undo are non-trivial (~20 files) and will diverge fast. The convergence between snip annotations and graphics-clip overlays is the whole point of having a single drawing model.
- **Source-linked `.cs` files via csproj `<Compile Include="..\..\Other\X.cs"/>`.** Rejected — same source compiled into two assemblies produces two distinct types; namespace/identity coupling becomes brittle.

## Consequences

- **The "no project references between projects" rule weakens** to "no project references between WinExe projects." Both PostSnip and VideoEditor reference `Flicksy.Drawing`. This is consistent with the original rule's intent (don't couple runnable artifacts to each other) and gives shared code a clean home.
- **What goes in `Flicksy.Drawing` is itself an architectural choice.** Anything in the library cannot reference anything in either WinExe project. Crop and layer-move commands stay in PostSnip because they don't generalize to clips on a timeline; the video editor will have its own clip-level analogues.
- **FFmpeg is initialized twice** (once per process). Acceptable — `FfmpegLocator.Initialize` is cheap and idempotent.
- **`Flicksy.Snipper` only knows about PostSnip.** The video editor is launched either by the user (Agent tray menu, Welcome window) or by PostSnip's "Launch in video editor" button — Snipper itself stays focused on capture-then-hand-off-to-PostSnip.
