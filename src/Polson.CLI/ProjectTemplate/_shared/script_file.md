### Run the file, do not retype it

> [!IMPORTANT]
> Once the artwork is more than a screenful, **keep it on disk and run it from there**:
>
> ```javascript
> ExecuteScript({ scriptFile: 'artwork.js', outFile: 'artifacts/stage3.webp' })
> ```
>
> Edit the file with your ordinary editor — change the one layer function you are working on — then
> run it. Do not paste the whole program into `script` on every iteration.
>
> This is not a style preference. On a measured multi-agent run, 850 KB of JavaScript went over the
> wire in 55 calls; the twenty largest took a mean of **three minutes each to emit**, while the engine
> executed them in a median of **45 ms**. Consecutive large scripts shared **71%** of their lines —
> so most of the run's time went into retyping code that had not changed, and a stage that works that
> way gets slower as the picture gets better.
>
> Give **either** `script` or `scriptFile`, never both: which one you meant would be a guess, and the
> symptom of a wrong guess is an edit that appears not to have taken effect.
>
> The record is unaffected. The server copies **what actually ran** into `scripts/`, so editing the
> file afterwards never rewrites the history of an earlier execution — and `script.start` names the
> file the source came from.
>
> `script` is still right for a probe: a few lines to measure a colour, check a font, or test one
> call. Use the file for the artwork and the inline form for questions.
