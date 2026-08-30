## Two things go through the MCP server, and nothing else

**1. Every mark you make.** Drawing happens by executing JavaScript through the Polson MCP server —
`ExecuteScript`, plus `RenderSvg` and `MeasureSvgPath` where they fit. That is the only way artwork
is produced on this project.

Concretely, and these are the ways it actually goes wrong:

- **Do not write SVG, HTML or any image file yourself**, with a file-writing tool or any other means.
  Hand-authored markup that happens to render is not a deliverable here.
- **Do not compute the drawing in your head and record the result as prose.** A scale worked out in a
  note — *"23.77 px per year"* — is not the same as calling `Scale.linear(...)`, and the arithmetic
  nobody executed is the arithmetic nobody checked.
- **Do not use a shell, an interpreter, or another tool to generate or post-process artwork.**
- **Do not hand-write files into `scripts/`.** The server writes and numbers those itself as it runs
  what you send it; files you put there are not scripts that ran.

**2. Every fact you need about the API.** Look things up with `Search`, and read `polson://sdk/*` and
`polson://manual/*`. Do not grep the filesystem for the implementation, do not search the web, and do
not work from memory of a similar library. The API is large and specific, and a call invented from
memory that happens to sound plausible fails in ways that cost more than the lookup.

**3. Never reach either of them through a command line.** Polson ships a CLI, and it can draw and it
can search — `polson eval`, `polson report`, and the rest. **None of it is yours to run.** Do not
invoke it from a shell, a terminal tool, a task runner, or anything else that starts a process.
The same goes for `dotnet`, `node`, `python`, ImageMagick, or any other program that could produce or
alter an image.

The two routes look equivalent from where you sit and are not. A script sent to `ExecuteScript` is
saved, numbered and recorded against the stage you declared; the identical script handed to the CLI
leaves an image and no trace of how it came to exist. One is a run; the other is a picture of
unknown provenance in a directory that claims to be a run.

### Why this is not busywork

The run record is the deliverable as much as the picture is. Every script you execute is saved,
numbered and timestamped; every render is recorded with the artifact it produced; every
`Stage.note(...)` is kept in order. Together those make a finished run legible to someone who was not
there — which is the whole point of working this way.

Work done outside the engine leaves none of that. It also **does not go unnoticed**: `polson report`
reconciles what the directory contains against what the server recorded, so an artifact with no
render event and a `scripts/` file that never executed are both named in the report. A run can look
complete and reconcile to nothing.

### If the engine cannot do what you need

Say so, plainly, and stop — do not route around it.

That is a real finding and it is wanted: an API or documentation gap you hit is worth more than a
picture you produced by other means, because the gap will cost every future run and the workaround
helps only this one. Record it where this project keeps its findings, or in a `Stage.note(...)` if it
has nowhere else, and then either take a documented approach or ask the director.
