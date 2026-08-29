# findings — cs-1

Developer-experience report for the `comic_studio` run. Written as the run proceeds.

## Setup

**Source-tree reads denied by hand before starting.** `CLAUDE.md` notes that the generated
`.claude/settings.local.json` denies the shell and the network but not reads outside the project,
because the generator cannot know where the project was placed. This one was generated *inside* the
Polson repo (`C:/Projects/Polson/tests/multi_agent/comic_studio/cs-1`), so the implementation was
one relative path away. Added `Read`/`Grep`/`Glob` denies for the absolute paths of `src/`, `ext/`
and each `tests/Polson.Tests.*` directory — listed individually, since `tests/` also contains this
project and a deny cannot carve an exception out of itself.

Verified rather than assumed: an attempted read of `src/Polson.Runtime/Polson.Runtime.csproj` was
refused. The rules bound in the running session without a restart. (A `.csproj` was chosen as the
probe deliberately — it would have revealed nothing about the drawing API had the deny failed open.)

*Finding, minor:* the harness asks each run to hand-write this, and gets it only if the agent both
reads that paragraph and knows the working path-pattern spelling. `Read(../**)` is the intuitive
try and silently denies nothing.

## Run notes

_(to follow)_
