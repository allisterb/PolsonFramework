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
`polson://manual/*` **with `ReadDoc(uri)`** — `ReadDoc('polson://sdk/core/Chart')` returns the whole
document in the tool result. Do not grep the filesystem for the implementation, do not search the web,
and do not work from memory of a similar library. The API is large and specific, and a call invented
from memory that happens to sound plausible fails in ways that cost more than the lookup.

> **Name the tool, because "read `polson://sdk/*`" does not say how, and on some hosts the obvious
> route answers with nothing.** A host may offer `load_mcp_resource`, which replies *"resource
> contents temporarily inserted and removed"* and no text — the document is real and reaches the
> model for exactly one turn, but the reply reads like a failure, so an agent tries it once and stops.
> `ReadDoc` returns the document in the tool result, where it stays.
>
> Measured on three runs of one brief: the agent that read nothing hand-rolled a chart from
> `Scale` and `Layout` and never learned the `Chart` toolkit existed; the two that read
> `polson://manual/13` found `Chart.createColumnChart` named there and used it.

**`Search` ranks passages by word overlap. It is not syntax-aware, and adding words makes it worse.**
There are no operators — no `AND`, `OR`, quoting or field terms — and a longer query does not narrow
the result, it **dilutes** it: every extra word spreads the score over more subject areas, and with
`k` defaulting to 5 the thing you wanted drops silently off the end. Measured against one corpus,
watching where the `Chart` reference lands:

| query | `Chart` ranks |
| :--- | :--- |
| `chart` | 1st |
| `column chart` | 1st |
| `column chart zero baseline` | 1st |
| `column chart zero baseline editorial layout` | 3rd |
| `editorial typography and layout Scale column chart` | 4th |
| the same, with `scope: 'all'` | **absent** |

That last row is a real run: the agent asked one broad question, never saw that a chart toolkit
existed, and rebuilt a column chart by hand out of `Scale` and `Layout`.

So: **one topic per search, and several searches rather than one long one.** Narrow `scope` to
`'sdk'` or `'manual'` when you know which you want, since `'all'` spends the same five slots across
both. Raise `k` when you are surveying rather than looking something up. And a **dotted call name**
(`Chart.createColumnChart`) is not searched at all — it is resolved exactly against the generated
symbol index and comes back with `confidence: 'direct'`, which is the authoritative answer to "does
this call exist and what are its arguments".

**A ranked list is never evidence that something is absent.** If you are asking whether a capability
exists, `polson://sdk/symbols` settles it and `Search` does not.

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
