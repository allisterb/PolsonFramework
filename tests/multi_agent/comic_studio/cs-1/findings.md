# findings — cs-1

Developer-experience report for the `comic_studio` run. Written as the run proceeds.

## Setup

**The source-tree denies were already in place and were verified, not assumed.**
`.claude/settings.local.json` denies `Bash`, `BashOutput`, `KillShell`, `WebFetch` and `WebSearch`,
and additionally denies `Read`/`Grep`/`Glob` on absolute paths under `src/`, `ext/` and each
`tests/Polson.Tests.*` directory. Those absolute-path denies are hand-added — `CLAUDE.md` explains
that the generator cannot add them itself, because it cannot know where the project was placed.

First action of the run was the self-test: `Read` on `src/Polson.Runtime/Polson.Runtime.csproj`,
which was **refused**. A `.csproj` is the right probe — it would have leaked nothing about the
drawing API had the deny failed open.

*Finding, minor:* the deny patterns spell the path `C:/Projects/Polson/...` while the project
actually sits at `C:/projects/Polson/...`, lowercase. The rules still bind, so the matcher is
case-insensitive here — but a harness whose entire validity rests on those denies should not depend
on that being true, and nothing in the docs says which way it goes. The probe is what makes the
run trustworthy; the config alone would not.

## Run notes

_(to follow)_
