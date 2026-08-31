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
