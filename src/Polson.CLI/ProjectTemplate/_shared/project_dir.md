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
