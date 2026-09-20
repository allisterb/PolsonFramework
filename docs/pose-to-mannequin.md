# Reading a pose off an image and driving the mannequin

*Spiked 2026-09-20, from the deformable-body review in `reference/articles/`. The review's own
"Route 1" — MediaPipe Pose to limb vectors to joint rotations — is the one recommendation in it
that needs no body model, no SMPL, no new install and no licence. This is whether it works.*

**It works, with two caveats that are worth more than the result.** A pose comes back to about
**2–6° mean** on ordinary poses and the mapping itself is exact; the failures are all the
detector's, and they are the kind that render perfectly.

Code and drivers: `tools/pose-spike/`. The probe is `src/vision/pose_landmarks.py`, which mirrors
`face_landmarks.py` in shape and in containment — separate process, bytes over a pipe, nothing
written to disk, no path crossing the boundary. **Nothing here is wired into the SDK.** Record it
as a capability proven rather than a capability shipped.

## Why this route rather than the review's other five

Every other route it recommends — SMPLify-X, SMPLer-X, HMR 2.0, ROMP, HybrIK, CLIFF, and the
`base_body.obj` chassis — terminates in SMPL topology, whose licence is the unasked question of
that whole document. This one terminates in a call we already own.

And the asymmetry our own MediaPipe ledger row recorded *against* the face route runs in our
favour here. That row notes `PoseLandmarkerResult` carries `pose_world_landmarks` **and face alone
has none**. For a body that is the whole game: pose hands back metric 3D joints, so a bone
direction is read rather than recovered by fitting a model to 2D points.

`python-mediapipe/` already had the package. The only new artifact is the weights,
`models/pose_landmarker_full.task` (9,398,198 bytes, sha256 `5134a3aa…`), fetched by hand from the
same bucket as the face bundle into the same gitignored folder.

## The mapping is exact; only the detector is noisy

Two tests, deliberately separate, because they fail for different reasons and one implementation
is shared between them (`pose_map.js`, concatenated into both).

**Analytically** — build a figure at a known pose, read back the joints it actually placed, run
them through the mapping, compare. No detector in the loop, so any error here is the mapping's:

| case | max error |
| :--- | ---: |
| standing | 0.156° |
| lean + tilt | 0.143° |
| arm thrown up | 0.000° |
| lunge | 0.150° |
| seated-ish | 0.153° |

The residual 0.15° is one known approximation: the spine lean is measured about the hip midpoint
where the construction rotates about `pelvis.center`, and the two differ by `spineOffset * 0.4`.
Recoverable if it ever matters; it is two orders of magnitude below detector noise.

**Through the detector** — render the figure, send the render to BlazePose, map, rebuild, compare:

| case | mean | max | worst term |
| :--- | ---: | ---: | :--- |
| standing | 2.24° | 4.38° | pelvicTiltDeg |
| lean + tilt | 4.87° | 7.13° | shoulderTiltDeg |
| lunge | 5.45° | 13.89° | rightLeg.kneeDeg |
| seated-ish | 17.98° | 32.27° | rightLeg.kneeDeg |
| **arm thrown up** | **60.1°** | **168.99°** | **leftArm.elbowDeg** |

`tools/pose-spike/detector-overlay.png` shows it: truth in graphite, recovery in red.

## Four findings, in the order they cost time

### 1. `shoulderTiltDeg` in `pose` is silently ignored

`spineDeg` and `neckDeg` are read from `options.pose`. `shoulderTiltDeg`, `pelvicTiltDeg`,
`spineOffset` and `shoulderSpanHeads` are read from the **options root**. A tilt passed inside
`pose` changes nothing and says nothing — asserted, not assumed:

```
shoulderTiltDeg -14 at options root -> shoulder line -14.00 deg
shoulderTiltDeg -14 inside pose     -> shoulder line  -6.00 deg (unposed reads -6.00)
```

The figure builds, renders and looks entirely plausible at the canon's default tilt. This cost the
analytic test a run and read as a mapping bug for as long as it took to find. **It is the same
shape as every other silent-no-op this project has scars from**, and the toolkit already refuses
unknown option names elsewhere — `createParametricHead` refuses a misspelled parameter by name.
Worth doing here.

### 2. The detector is gated on tone polarity, not on anatomy

The solid mannequin was not detected at any padding, in any pose. That looked like "a drawn figure
is outside the detector's domain" and was not. Same geometry, contrast swept:

| figure | ground | ratio | found |
| :--- | :--- | ---: | :--- |
| `#b9ad99` | `#e8e4dc` | 1.74 | no |
| `#9c8f79` | `#e8e4dc` | 2.50 | no |
| `#6d6353` | `#e8e4dc` | 4.65 | no |
| `#4a4338` | `#e8e4dc` | 7.70 | **yes** (0.862) |
| `#2a2a2a` | `#e8e4dc` | 11.32 | **yes** (0.709) |
| `#2a2a2a` | `#ffffff` | 14.35 | **yes** (0.815) |
| `#b9ad99` | `#ffffff` | 2.21 | no |
| **`#ffffff`** | **`#2a2a2a`** | **14.35** | **no** |

The threshold sits between 4.65 and 7.70 — **and the last row is the finding.** A white figure on
a dark ground at *identical* contrast ratio fails. It is not contrast, it is **dark figure on
light ground**, which is what BlazePose's training data overwhelmingly is.

Once polarity is right, the geometry barely matters:

| geometry at `#2a2a2a` on `#ffffff` | found | visibility |
| :--- | :--- | ---: |
| wireframe | no | — |
| solid (detached masses) | yes | 0.733 |
| silhouette (unioned) | yes | 0.798 |
| silhouette + real head and neck | yes | 0.815 |

Only the wireframe fails, and it is lines rather than area. **The detached-masses mannequin works
fine** — the gaps between limb segments were never the problem.

### 3. A featureless silhouette leaves the detector guessing which way the figure faces

MediaPipe's `left_*` is the **subject's** left; the toolkit's `leftArm` is **page**-left
(`leftShoulder` is laid at `originX - span/2`). So they mirror — but only for a figure facing us,
which is not a thing to assume.

Deciding the side from the data instead, and deciding it *once* from the shoulders, produced a
**173° pelvic tilt error on a figure standing straight up**: on a featureless silhouette the
detector's own left/right labelling comes out internally inconsistent, shoulders one way and hips
the other. Ordering **each pair by its own x** is not an approximation of the right answer — given
that Polson's left/right is page-space and nothing else, it *is* the definition. That alone took
the two catastrophic cases from 173° and 127° down to 18° and 12°.

**Drawing a face settles it.** With `drawComicEye`/`drawComicBrow`/`drawComicNose`/
`drawComicMouth` on the head, the facing conflict disappears on every case and `standing` goes
from 8.95° mean to **2.24°**.

### 4. But a face also buys a confident wrong answer

`arm thrown up` was previously *dropped* — `leftWrist` visibility 0.35, below the gate, so the
limb fell back to the canon and the pose came back honest and incomplete. With a face drawn, the
detector becomes confident about that arm and puts the forearm **169° out**, folding it back the
wrong way.

That is the failure mode worth carrying out of this spike. **A raised or foreshortened limb is
ambiguous to a monocular detector, and its confidence is not a measure of its correctness.** The
visibility gate is doing real work — `poseFromJoints` drops a limb whose joints fall below 0.5 and
reports it in `dropped`, because `PoseLimb` falls back to the canon for any key it is not given,
so a dropped arm comes out standing rather than broken.

## On a real photograph

Everything above is a figure **this system drew**, which makes the error numbers a best case: the
proportions are exactly the 8-head canon, the joints sit exactly where the mapping expects, and
there is no clothing, hair, foreshortening or background. A photograph has all of those, and it
has no ground truth — nobody can say what Bolt's elbow angle "really" was in degrees — so this is
judged by eye. `photo_chain.py <slug>` produces the three-panel comparison.

| | detected | dropped | reads as the pose? |
| :--- | :--- | :--- | :--- |
| Usain Bolt (photograph) | yes | **both legs** | torso and arms yes, legs no |
| David (marble statue) | yes | none | yes, contrapposto included |
| Vitruvian Man (drawing) | **no** | — | — |

**The framing matters more than the realism.** A marble statue recovered completely and a
photograph did not, because the photograph is cropped at the thigh: the ankles fall outside the
frame, the detector extrapolates them at 0.25 and 0.44 visibility, and the gate refuses both legs.
That is the gate working — it declined to invent legs from off-frame guesses — but the practical
consequence is that **a tightly framed reference gives you a partial pose**, and half the figure
comes back at the canon's default.

David's one defect is instructive: `rightArm` bend comes back at **−177.4°**, the forearm folded
almost exactly back along the upper arm. It is very nearly right — his hand *is* at his shoulder,
holding the sling — but a near-180° fold is the degenerate end of a two-segment planar limb, and
with the canon's forearm length it draws as a spike rather than as a bent arm.

## On a DRAWN figure, which is the case that actually matters

Photographs were only ever the control; this studio poses illustrated characters. `Assets.cutout`
would be the right source — it is what the arranged route produces — but asset requisition reports
a budget of **0 of 0** in this CLI context, so these are public-domain lead images that happen to
be drawn full figures. `illustrations.py` fetches and tests them.

| drawn subject | found | pad | mean visibility | limbs recovered |
| :--- | :--- | ---: | ---: | :--- |
| The Blue Boy (oil painting) | yes | 0 | 0.870 | **all four** |
| Uncle Sam (poster illustration) | yes | 0 | 0.789 | — |
| Pinkie (oil painting) | yes | 0 | 0.617 | — |
| **Popeye (flat cartoon)** | **yes** | **480** | 0.601 | **none — all four dropped** |
| Little Nemo (comic panel) | **no** | — | — | — |
| Vitruvian Man (pen drawing) | **no** | — | — | — |

**The axis is anatomical plausibility, not drawn-versus-photographed.** A 1770 oil painting
recovers as cleanly as a photograph — the Blue Boy's hand-on-hip elbow comes back at 93.3°, which
is right, and panel 3 is a mannequin a director could use. A flat cartoon does not.

**Popeye is the case to look at, because of *how* it fails.** It reports `found: true` at 0.601
mean visibility — and then every limb joint lands below the gate: `rightElbow` 0.09, `rightWrist`
0.08, `rightKnee` 0.20, `rightAnkle` 0.18. The detector crumpled the whole skeleton into his
torso. His proportions are the reason: enormous forearms, vestigial upper arms, no neck, huge
feet — BlazePose was trained on people, and he is not one.

So **all four limbs were dropped and only the torso came back**. That is the visibility gate
earning its place for the third time: without it, a figure would have been built from joints the
detector had 8% confidence in, and it would have rendered perfectly.

**The practical consequence.** Posing from a reference photograph, or from a realistically
proportioned illustration, works. Posing from a *stylised* character — which is the comic case —
gives you a torso and nothing else. The studio's own mannequin is fine because it is built to an
8-head canon; it is the stylisation the detector cannot follow, and stylisation is the point of a
comic character.

**One earlier note is corrected by this.** "A measured decision on the padding ladder — every case
came back `pad=0`, so it may be dead weight" was wrong. Popeye needed **pad=480**, because a
trimmed transparent cutout arrives at 100% of frame and the detector's scale window rejects it.
That is exactly the case the ladder was carried over from the face probe to handle, and it is the
case `Assets.cutout` produces every time. Keep it.

**One correction worth recording, because it was mine and not the detector's.** The first overlay
anchored on the shoulder midpoint using the canon's constants — shoulder line at 1.4H, pelvis at
3.6H, torso 2.2H — and put the head off the shoulder by half a head. `originX` is the axis of the
*unposed* figure and `spineDeg` rotates the upper body about `pelvis.center`, so with any lean
neither the shoulder midpoint nor the projected torso length is where the constants say. Measuring
a trial figure's own shoulders and hips, and anchoring on the **pelvis** because it is the pivot
the lean does not move, fixes it exactly. The recovered *angles* were right the whole time.

## What this is good for

**A starting pose an author then adjusts, not a transcription.** At 2–6° on ordinary poses the
recovered figure reads as the same pose; at 18–60° on unusual ones it does not. It replaces typing
eight angles from scratch with correcting two.

It would want, before shipping:

- **The options-root trap fixed** (finding 1) — an unknown key inside `pose` refused by name.
- **A `Pose` SDK surface** on the potrace pattern: `Pose.available`, `Pose.detect(image)`,
  separate process, `available` gated on configuration.
- **The silent-failure guard made loud.** A caller needs `facing conflict` and `dropped` in front
  of them, because both are invisible in the render.
- ~~**A measured decision on the padding ladder.**~~ **Settled, against the guess.** It was carried
  over from the face probe on faith and every case here came back `pad=0`, so it looked like dead
  weight — but Popeye needed **pad=480**, because a trimmed transparent cutout arrives at 100% of
  frame and the detector's scale window rejects it. That is the case `Assets.cutout` produces every
  time. Keep it.

## The gap this leaves, now that the other end exists

**A mesh can be posed as of 2026-09-20** — `mesh.posable`, `mesh.joints`, `mesh.pose(...)`, on
SharpGLTF's `SkinnedTransform`; see `polson://sdk/core/Mesh` and `polson://manual/26` §7f. So both
ends are built and **nothing joins them**.

The detector answers in the mannequin's vocabulary — screen-space `shoulderDeg` absolute,
`elbowDeg` relative, 90° being straight down — and a glTF skeleton wants rotations about each
joint's **own** axes, in whatever frame its exporter chose. Those are different coordinate systems
and neither is convertible to the other without knowing the rig's bind orientation per joint.

That mapping is the remaining piece, and it is not a small one. It is also worth weighing against
what §"On a DRAWN figure" measures: the detector reads a photograph or a realistically-proportioned
illustration well and a stylised character not at all, so the chain would be *reference photograph →
rigged character*, never *comic panel → rigged character*.

## What it does not touch

This recovers a **pose**. It says nothing about the body's *shape*, and nothing here needs SMPL,
DensePose, Sapiens or a canonical body mesh — which is the point.

The other routes were checked on terms the same day and the results are in the ledger row for that
document. In short: **SOMA-X is real** (`huggingface.co/nvidia/SOMA-X`, Apache 2.0) and its
`SMPL/base_body.obj` really is 6,890 v / 13,776 f — but it carries **zero texture coordinates**,
so the review's "bake the character into the SMPL UV map" opening step has no map to bake into.
The atlas lives in `SOMA_wrap.obj` and in **MHR**, which is in the same repository and is the
better candidate on both capability and terms. **Sapiens is CC-BY-NC 4.0**, which cannot be
combined into an AGPL-3.0 work at all — if it is ever used it must be a separate process on the
`potrace` pattern, not linked and not redistributed. **molesq is MIT**, and the MLS algorithm is
implementable from the paper in any case.
