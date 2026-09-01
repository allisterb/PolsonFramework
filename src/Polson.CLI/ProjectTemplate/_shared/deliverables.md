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
