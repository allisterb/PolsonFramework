> [!IMPORTANT]
> **If the brief names an output format, that format is the deliverable.** A brief asking for *"an
> SVG"* is answered by a `.svg` file, not by a picture of one. This is decided in your first script,
> not at the end: `outSvg` saves the vector markup of a `SnapPaper`, and a scene drawn on a raster
> canvas has none to save — `outSvg` then writes nothing at all and the run still reports success.
> A live run was asked for an SVG, built every stage on `createCanvas`, and delivered `.webp`.
>
> So: **read the brief for a format before you choose a surface.** Build on `Snap(width, height)`
> when the answer is vector, and pass **both** `outFile` and `outSvg` on every stage that draws one —
> the render is what the director looks at, the SVG is what they asked for. `polson://manual/14`
> covers the decision and the vector API.

**Write these with your host's ordinary file-writing tool**, as plain files in this project
directory. They are the one category of file you author yourself: `scripts/` is written for you by
the server as it runs what you send it, and `artifacts/` is written by `outFile` — neither is yours
to hand-write.

> [!IMPORTANT]
> If your host *also* offers a separate **artifact** or **workspace document** writer, that is a
> different thing and it will refuse a path inside this project — its store lives under the host's
> own directory, not here. A live run lost a turn to exactly that, on a refusal reading
> `… is not a valid artifact path; artifacts must be in …/brain/…`.
>
> If you see that, you have reached for the wrong tool rather than hit a restriction: write the file
> with the ordinary editor instead. Do not try to satisfy the message by writing into the path it
> names — a deliverable in the host's scratch directory is one nobody will find, and it disappears
> with the session.

### Never delete anything. Rename it aside and say why.

If you find a file that looks stale — a report from an earlier run, a script that no longer matches
the drawing — **do not remove it.** Move it out of the way and leave a note:

```bash
mv stale-thing.md stale-thing.previous.md
```

then a `Stage.note` saying what you moved and why you judged it stale.

Deleting is never part of drawing, and the cost is asymmetric: a stale file that survives costs a
moment's confusion, while a deleted one may have been the only record of something. A live run
removed two files this project had asked it to write, because they were left over from a previous
run. Its judgement was right and the method was not, and nothing it deleted could be recovered from
inside the project.

This applies to the whole directory, including things you wrote yourself earlier in the run. If a
file genuinely must go, say so and let the director do it.
