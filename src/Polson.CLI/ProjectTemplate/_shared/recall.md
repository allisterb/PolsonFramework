## What you already know

**If this project has run before, `Recall` is where its past lives.** Every earlier run left its
stages, the reasoning it wrote into `Stage.note`, the scripts it ran, the artifacts they produced and
the failures — and none of it is in your context, because you did not have that run.

Call it before you plan, and again whenever you are about to attempt something an earlier pass may
already have an answer for:

```javascript
Recall('how the arcade proportions were set')
```

Read the `runs` field first, because three outcomes look alike and are not:

| `runs` | `count` | What it means |
| ---: | ---: | :--- |
| `0` | `0` | **This project has never run.** Nothing to remember. Proceed from the brief. |
| `>0` | `0` | Earlier runs exist and none mentioned this. The past is real but silent — proceed, and leave a note that makes the next run's recall better. |
| `>0` | `>0` | Each result is one stage of one earlier run, with what it said it was doing and what it made. |

**Open what it recalls.** A result's `artifacts` are paths you can load —
`Skia.Image.load('artifacts/003_blocking.webp')` — and looking at what an earlier pass actually
produced tells you things its notes never could. This is the one moment in a run where you can build
on work you did not do, so take it rather than reading the note and moving on.

> [!IMPORTANT]
> **Write for the run that comes after this one.** Your `Stage.note` entries *are* the next run's
> memory — they are what `Recall` searches, and they are weighted above everything else in the
> record precisely because they are the only part written for a reader rather than for the machine.
>
> A note saying `rendered stage 3` recalls nothing to anybody. A note saying *"the cypress rhythm
> read as wallpaper at even spacing, so they are now clustered in threes"* is a finding the next run
> can use without repeating the mistake that produced it. Write the reason, not the action — the
> action is already recorded beside it.

`Recall` is memory across sessions. `History` is the scripts you ran moments ago in this one, and
`Search` is design theory and the API. Three different questions; do not reach for the wrong one.
