---

# This run is a test of the framework, not only a commission

Do the work in this brief properly — a finding from a run that did not really try is worth nothing.
But **your honest experience of using the API matters as much as the artwork**. Record every error,
friction, surprise and limitation in `findings.md` as it happens. Do not smooth over friction, and do
not quietly work around a bug: a bug you route around silently is a bug that survives.

## Ground rule: no peeking at the implementation

You may read **only** what the MCP server exposes: its tool definitions and its `polson://sdk/*` and
`polson://manual/*` resources.

**Do not inspect Polson's C# source, tests, or implementation files**, and do not read `docs/*.md`
from disk — the manuals reach you through MCP resources and the `Search` tool, and reading them any
other way defeats the test. Do not infer method names or parameters from files on disk. If you cannot
work out how to use an API from the MCP resources and tool descriptions alone, **that is an API or
documentation defect** — record it and try another documented approach.

{{ISOLATION}}

If you find yourself *wanting* to look at the implementation, that is the single most valuable signal
this run produces. **Write down what you wanted to know and why the published resources did not tell
you**, then solve it from `polson://sdk/*`, `polson://manual/*` and `Search` instead. A run where you
never needed the source is a good result; a run where you needed it and said so is a better one. A
run where you read it is worthless, because it can no longer say whether the published API was
sufficient — which is the only thing this run measures.

### One boundary *is* enforced by the server, and it is worth proving

The MCP server confines its own writes to this folder, whatever your file tools can do. `outFile`
writes through the server, so it is covered by the server's own rule rather than by any host
permission: paths resolve against this folder, and one that escapes — an absolute path, or a `..`
traversal — is refused with an error naming the project directory.

Prove it once, before starting: call `ExecuteScript` with any trivial script and
`outFile: '../escaped.webp'`. It must be **refused**. Report it on one line. **If it succeeds, stop
and report that** — a write escaping the project is a real defect in the server, not a test quirk.

Keep every path relative, like `artifacts/03_counter.webp`.

## Leave time for the report

Of your {{DEADLINE_MINUTES}} minutes, **plan to spend the last {{TEST_REPORT_MINUTES}} writing
`findings.md`** — about {{TEST_WORK_MINUTES}} on the work itself. A run that produces good artwork
and no report has failed at the thing it was for.

Keep `findings.md` open as you go and write entries when they happen. Reconstructed at the end, a
report loses exactly what makes it useful: what you tried first, what you expected, and what
surprised you.

The deadline is under test too. Whether it was achievable for this brief, where the time actually
went, and whether the `[studio runtime]` notices arrived usefully or as an interruption — all of that
belongs in the report.

## The record is under test as well

Every script you execute is saved to `scripts/` and the run keeps its own account in
`events/server.jsonl` — renders, durations, byte counts and errors are recorded for you, and you do
not need to write your scripts out yourself.

What is *not* recorded for you is what you were trying to do. That is `Stage`:

```javascript
Stage.begin('Concept');
Stage.note('three directions from one idea; testing whether the hull reads without the sail');
Stage.expect('the accent stays under 15% of the frame');
```

Going back is normal and worth recording — reopening a stage is exactly what a reader wants to see,
and it is invisible unless you declare it. Ask, in `findings.md`: did declaring stages fit how you
actually worked, or was it paperwork bolted onto it? Was the vocabulary the right granularity? Did
anything you wanted to record have nowhere to go?

## What `findings.md` must cover

Structure it as you like, but cover these — and whatever the section below raises:

- **What broke.** Errors, wrong results, misleading documentation, anything you worked around.
- **What you could not find.** Every time you searched and did not get what you needed. If you
  concluded a capability did not exist, say what you searched for — a wrong "it doesn't exist" is the
  most expensive failure this run looks for, and if it turned out to exist after all, that is the
  single most valuable thing you can report.
- **What misled you.** Answers you acted on that were wrong for your task. Costlier than finding
  nothing.
- **What you hand-rolled** that the SDK already provided.
- **The run record.** Did `Stage` fit how you worked? Were the names right?
- **The boundary.** Whether the out-of-folder `outFile` was refused, and any tool refusal that blocked
  something the published API told you to do. Most importantly: **every time you wanted to look at the
  implementation** — what you wanted to know, and what you did instead.
- **Time and iterations.** Roughly how many attempts to a first correct call, and where the time went.

{{TEST_FOCUS}}
