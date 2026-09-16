## Work inside the project directory

**Everything you read and everything you write lives in this project's own directory.** It is your
whole working surface, and it is meant to be: the brief, the scripts, the artifacts, the run record
and the deliverables are all here, and a finished run is readable from this one folder without
knowing anything about the machine it ran on.

The engine already holds you to this. `outFile`, `outSvg`, `scriptFile` and `Skia.Image.load(...)`
all resolve relative to the project directory, and a path that lands outside it is refused with a
message naming the root rather than quietly writing somewhere else. **Hold yourself to the same line
with every other tool** — file readers and writers, editors, and anything you run in a shell.

The reason is the record rather than secrecy. `polson report` reconciles what this directory contains
against what the server recorded, so work done here is accounted for and work done elsewhere is
invisible to it — and a project directory is archived, replayed and handed to a reader as a unit. A
file written outside it is not part of the run in any sense that survives the session.

So if you need something that is not in this directory, **ask the director for it** rather than going
to find it. A path you were given is part of the brief; a path you went looking for is not.

### One folder here is not yours to read: `previous/`

If this project has been run before, `polson reset` moved that run aside into
`previous/<timestamp>/` — its record, its scripts, its renders, and whatever it wrote about itself.
**Do not read it.** Not the findings, not the old artifacts, not the archived event log.

It is there so a person can go back to it, not so the next run can start from it. A reset is a
director saying *begin again*, and the archive is the evidence being kept rather than handed
forward — so reading it spends a large part of your context re-deriving a run somebody deliberately
set aside, and anchors this one on decisions that were made about a different attempt.

**What carries forward is what the director put in the brief.** If they want a previous run's
findings honoured, they will say so, and the words will be in `brief.md` where you have already read
them. Silence there means a clean start is the ask.

> A timestamped folder is also not a run. A reset interrupted by an open file finishes on the next
> pass, so one run's work can be split across two stamps — which is one more reason the directory
> repays a careful human reader and misleads a quick one.
