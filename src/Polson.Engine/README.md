# Polson Engine

A JavaScript drawing engine, run as a command or served to an agent over the Model Context Protocol. A script draws
with a Snap.svg-style vector API (`Snap`), an HTML-canvas-style raster API (`createCanvas`), and toolkits built on them:
construction drawing (`Drawing`), logos (`Logo`), charts (`Chart`), scenes, meshes and characters (`Mesh`, `Face`,
`Character`), and motion (`Motion`, which writes animated WebP, MP4 with ffmpeg, and Synfig `.sif`). The engine renders
headlessly with Skia and returns PNG, WebP, JPEG, SVG or print PDF.

It is the drawing half of the Polson studio, without the studio: no cloud services, no design manuals, no workflows.

## Running it

One executable per platform, with nothing to install:

```
polson-engine eval drawing.js --format png --img drawing.png
polson-engine server --project-dir ./work            # MCP over stdio
polson-engine server --http --port 5100 --project-dir ./work
```

For an MCP host such as Claude Code, in its `.mcp.json`:

```json
{ "mcpServers": { "polson": { "command": "/path/to/polson-engine", "args": ["server", "--project-dir", "/path/to/work"] } } }
```

The server's tools are `ExecuteScript`, `RenderSvg`, `InspectScript`, `MeasureSvgPath`, `CompareImages`, `ScanText`,
`History`, `Recall`, `Search` and `ReadDoc`. `Search` and `ReadDoc` serve the SDK reference that is built in, so an agent
needs nothing but the server: `ReadDoc('polson://sdk/index')` is the map.

## Settings

`appsettings.json` beside the executable, if present; `appsettings.example.json` lists the keys. A blank value is unset.

- `Server:DefaultTimeoutSeconds`: the script limit (30 s; `--timeout` overrides).
- `Server:HttpPort`: the port for `--http` (`--port` overrides).
- `Tools:FFmpeg`, `Tools:Potrace`, `Tools:Blender`: paths to optional tools. Each is otherwise looked for on `PATH`, and
  a call that needs a missing one refuses and says what to install. None is bundled: ffmpeg's usual builds and potrace
  are GPL programs run as separate processes.
- `Poses:Library`, `Characters:Stock`: folders of pose clips and stock bodies for `Character`, which this package does
  not include.

Text is set in the fonts installed on the machine; a family that is missing falls back to the platform default.

## The sandbox

A script is untrusted code, and the engine treats it so whoever wrote it:

- no `eval`, no `new Function`, no reflection, and no member of any .NET library object it is handed: only the SDK's own
  members, and plain values such as colours and points;
- a time limit (30 s), a statement limit (2,000,000), a recursion limit (100) and a regular-expression timeout;
- no raster larger than 32,767 pixels a side or 268,435,456 pixels in all;
- no external reference in SVG: an image or `<use>` href to a file or a URL is not fetched. A picture goes into a
  document as an object (`paper.image(bitmap, ...)`), inlined as a data URI;
- under `server`, every file a script or a tool names is resolved inside `--project-dir` (the working directory when it
  is not given), and a path outside it is refused.

`eval` runs your own script with your own file access: it has no project directory, so it contains nothing.

These limits are per script. A script that makes many large rasters is bounded by the time limit rather than by a
memory budget, so a server open to untrusted callers should run under an operating-system memory limit too. The HTTP
transport has no authentication. It listens on localhost, answers only a loopback `Host` header, and grants CORS only to
loopback origins, so a web page cannot drive it; do not put it behind anything that exposes it beyond the machine.

## Licence

AGPL-3.0 (`LICENSE`). Running the engine as a separate program, from a shell or as an MCP server your harness talks to,
does not extend the licence to your code. If you pass the executable on, pass on the offer of its source; if you modify
it and let others use it over a network, publish your changes. Linking its libraries into your own program makes the
combination a work based on the engine. The third-party works it contains, and their licences, are in
`THIRD-PARTY-NOTICES.md`, `licenses/` and `notices/`.
