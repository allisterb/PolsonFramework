# Studio Manual 16: Requisitioning Material

> **Credits & Theoretical Foundation**: No external source — unlike the other manuals, this one documents a Polson subsystem rather than distilling a book. The division it enforces — **the model supplies substance, the toolkit supplies form** — is the studio's own, and follows from the Extended Mind position in the project brief: offload to the environment what you cannot synthesise, and keep what you must control. Signatures at `polson://sdk/core/Assets`; returned models at `polson://sdk/schema/Assets`.
> **Purpose**: How to requisition raw material from the cloud image model — what it will and will not produce, how to vet a descriptor before spending, how to read a result that reports its own failure, and how to turn a swatch into a drawn surface.

---

## 1. What This Is For

> **Implemented by**: `Assets.material(...)`, `Assets.backdrop(...)`, `Assets.matte(...)`.

`Assets` is the one **metered** surface in the SDK. Every other call in this studio is free and instant; these three reach a cloud service, take seconds, and cost money.

They return **raw material**, never finished pictures. There is no call that produces a logo, a character, or a scene, and that is a design decision rather than a limitation:

- The model supplies what is genuinely hard to synthesise from code — the look of weathered oak, the grain of hand-laid paper, the mottling of oxidised copper.
- The drawing toolkits supply form, structure, lighting and composition, because those are the things that must be *correct* and *editable*, and a generated picture is neither.

The three calls are:

| Call | Returns | For |
| :--- | :--- | :--- |
| `Assets.material(descriptor, options)` | A flat, seamlessly tiling swatch | Surfaces: what a shape is *made of*. The safest and most reusable — independent of geometry, so it survives any amount of redrawing |
| `Assets.backdrop(descriptor, options)` | A background plate | What sits *behind* the scene |
| `Assets.matte(descriptor, options)` | A greyscale mask or height field | A shader input: masks, displacement, tonal fields |

**Reach for `material` first.** A swatch is geometry-independent: it can be clipped into any shape, tiled at any scale, tinted, and reused across every stage of a run. A backdrop is welded to a composition and a matte to a specific effect.

---

## 2. Vet the Descriptor First — `classify`

> **Implemented by**: `Assets.classify(descriptor)` → `{ className, class, reason, triggers, allowed }`.

`Assets.classify` is **free, offline and instant**. It answers the only question that reliably wastes a requisition: *is this a material, or is it a thing?*

```js
Assets.classify('weathered ship hull planking, tarred caulking between planks');
// className: 'Substance', allowed: true
// reason: 'Head noun is a material; the object named is an attributive modifier.'

Assets.classify('a wooden pirate ship at sea');
// className: 'Form', allowed: false, triggers: ['ship']
// reason: "'ship' names a thing with an outline. Draw the form with the drawing
//          toolkit and requisition its surface instead — e.g. the planking, not the ship."
```

Note that the two descriptors contain the same words. What separates them is the **head noun**: *planking* is a material modified by "ship hull"; *ship* is an object modified by "wooden". The classifier reads for that, so rewording is usually a matter of naming the surface instead of the object.

`material()` applies the classifier automatically and refuses before touching the network. Calling it yourself is how you check a descriptor without committing to it — and how you rewrite a whole shopping list before spending anything.

### Reading a refusal

`triggers` names the words that caused it, so a refusal tells you what to change:

| Descriptor | Verdict | `triggers` |
| :--- | :--- | :--- |
| `rusted corrugated iron sheeting` | Substance | `[]` |
| `human skin texture, fine pores` | Substance | `[]` |
| `crumpled shipping paper` | Substance | `[]` |
| `a sailboat at anchor` | Form | `['boat']` |
| `a portrait of a woman smiling` | Form | `['portrait', 'woman']` |
| `a logo for a coffee shop` | Form | `['logo']` |

Matching is on whole words plus a **suffix rule**, so a compound ending in a form noun is caught — *sailboat* reports `boat`. It is deliberately not a substring test: *human* is not *man*, and *shipping* is not *ship*.

> [!TIP]
> **A refusal is a rewording, not a retry.** `retryable` is `false`, and repeating the identical descriptor produces the identical refusal. Name the surface: not *a wooden ship* but *weathered ship hull planking*; not *a tree* but *oak bark, deep fissured*; not *a portrait* but *aged photographic paper, silver-gelatin grain*.

---

## 3. Check the Budget — `Assets.budget`

> **Implemented by**: `budget.total`, `budget.spent`, `budget.remaining`, `budget.canAfford(n)`, `budget.cacheHits`, `budget.tokensSpent`.

```js
if (!Assets.budget.canAfford(3)) exit('not enough allowance for the three surfaces this needs');
log(Assets.budget.spent + ' of ' + Assets.budget.total + ' spent, ' +
    Assets.budget.remaining + ' left; ' + Assets.budget.cacheHits + ' served from cache');
```

- `total` / `spent` / `remaining` are requisition counts, and `canAfford(n)` is the check to make **before** planning work that needs `n` of them.
- `cacheHits` counts requisitions served from the content-addressed cache. Those cost nothing and return instantly — re-running an identical requisition is free, which is what makes a two-script workflow (§5) cheap to repeat.
- `tokensSpent` is what the service actually billed, as reported by the service.

**`total` of `0` means requisition is unavailable on this project** — either no credentials are configured, or the workflow denied it. The `drawing` workflow denies `Assets` by design: a drawing has no materials, and every mark on that surface is one you made. Check the budget rather than discovering this from a failure three scripts later.

---

## 4. `await`, or Get Nothing

> [!WARNING]
> **All three requisition calls are asynchronous. You must `await` them.** This is the single most expensive mistake available in this API.
>
> ```js
> const oak = Assets.material('weathered oak planking');   // WRONG — no await
> log(oak.success);        // undefined
> log(oak.failureName);    // undefined
> if (!oak.success) { … }  // taken, because !undefined is true
> ```
>
> Without `await` you hold a `Promise`, on which every documented property is `undefined`. That reads as a failure — and the requisition **still completes in the background and still spends budget**. You pay for a material you then report as unavailable.
>
> **If a requisition appears to fail with `undefined: undefined`, you forgot the `await`.** Top-level `await` is supported, so there is no wrapper function to write.

---

## 5. Two Scripts, Not One

Generation takes several seconds per call, against a bounded script execution limit. A script that requisitions three assets can exceed it — and a timeout loses the drawing work in the same script, not just the requisition.

**Requisition in one short script; draw in the next.** Results are cached by content, so re-running the first script is free and instant.

```js
// Script 1 — requisition only.
const oak = await Assets.material('weathered ship hull planking, tarred caulking between planks');
if (!oak.success) { error(oak.remedy); exit(oak.failureName); }
Session.oakUri = oak.toDataUri();
log('oak ' + oak.size + 'px, wraps=' + oak.tiling.wraps + ', ' + Assets.budget.remaining + ' left');
```

```js
// Script 2 — draw. The material is already in the session scratchpad.
const plank = Skia.Image.fromDataUrl(Session.oakUri);
```

`Session` persists across executions within a session, which is what makes the split free. Stash the **data URI**, not the asset object.

---

## 6. Every Result Reports Its Own Failure

> **Implemented by**: `material.success`, `material.failureName`, `material.remedy`, `material.retryable`, `material.error`. The same five are on `plate` and `matte`.

**Nothing here throws.** A requisition is a metered network call and can fail in a dozen ways, so failure is data:

```js
const oak = await Assets.material('weathered oak planking');
if (!oak.success) {
    error(oak.remedy);          // what to do next, in words — read this before retrying anything
    exit(oak.failureName);      // and stop, rather than drawing onto a material you do not have
}
```

- **`success`** — the only thing to branch on.
- **`failureName`** — a readable name: `'None'`, `'NotConfigured'`, `'BudgetExhausted'`, `'RefusedFormRequest'`, `'SafetyBlocked'`, `'Recitation'`, `'RateLimited'`, `'Quota'`, `'ConstraintNotMet'`, `'Network'`, `'Timeout'`, `'Auth'`, `'ModelNotFound'`, `'NoImageReturned'`, `'ServiceError'`, `'InvalidRequest'`, `'Cancelled'`. (`failure` is the same value as a number — prefer the name.)
- **`remedy`** — what to do about it. Written to be read.
- **`retryable`** — whether repeating the *identical* request could succeed. `false` means reword or give up; retrying anyway spends again for the same answer.
- **`error`** — the underlying message, usually more specific than the remedy.

The three failures worth knowing by name:

| `failureName` | Means | Do |
| :--- | :--- | :--- |
| `RefusedFormRequest` | The descriptor named an object, not a material | Reword to name the surface (§2). Costs nothing — refused before the network |
| `NotConfigured` | No credentials on this studio | Draw it instead. Do not retry |
| `BudgetExhausted` | The allowance is spent | Reuse from `Assets.library` (§9) |

---

## 7. Applying a Material

> **Implemented by**: `material.toDataUri()`, `material.bytes`, `material.size`, `material.mimeType`, `material.tiling`.

A material is a **flat tiling swatch**. It becomes art by being clipped into a form you constructed and lit by lighting you controlled:

```js
const plank = Skia.Image.fromDataUrl(Session.oakUri);

const hull = new CanvasPath();          // the form is yours
hull.moveTo(230, 400); /* … */
ctx.save();
ctx.clip(hull);
ctx.fillStyle = Skia.Shader.bitmap(plank, 'repeat', 'repeat');
ctx.fillRect(0, 0, 1200, 760);
ctx.restore();
```

`material.tiling` reports whether the swatch actually wraps: `{ horizontalSeamStep, verticalSeamStep, neighbourMedian, neighbourMax, wraps, repaired }`. `wraps` is guaranteed `true` on success; `repaired` says whether this layer had to fix it, which is worth logging when a texture looks subtly banded.

`material.size` is the swatch's pixel dimension (`32`–`1024`, default `512`) and `mimeType` its encoding. Tile scale is a drawing decision, not a requisition one — the same swatch serves a plank at any size.

---

## 8. Backdrops and Mattes

> **Implemented by**: `plate.metrics`, `plate.boundTo`, `plate.isReusable`, `plate.width`, `plate.height`, `matte.bytes`, `matte.size`.

**A backdrop plate measures itself**, and that is what keeps the foreground from contradicting it:

```js
const sky = await Assets.backdrop('overcast dusk sky over open water', { keepQuiet: 'lowerThird' });
if (!sky.success) { error(sky.remedy); exit(sky.failureName); }

// The plate's own light, measured from the returned image — not assumed.
const key = { x: sky.metrics.keyLightX * canvas.width, y: sky.metrics.keyLightY * canvas.height };
Drawing.drawRimLight(ctx, subjectBounds, angleTo(key), '#ffd9a0', 3);
Drawing.projectCastShadow(key, groundY, subject);
```

`plate.metrics` gives `keyLightX` / `keyLightY` — the brightest mass, in normalised `[0,1]` coordinates — plus `bandLuminance` for the top, middle and lower thirds, `quietRegionHonoured` and `hasBakedMask`. **Feed the key light into `drawRimLight` and `projectCastShadow`** so the subject is lit by the sky it is standing under. A foreground lit from the left against a plate lit from the right is the commonest way a composited scene reads as fake, and it is entirely avoidable here.

`keepQuiet: 'lowerThird' | 'upperThird' | 'leftHalf' | 'rightHalf' | 'center' | 'none'` asks for a region left visually calm so the subject has somewhere to sit; `metrics.quietRegionHonoured` says whether it was.

`plate.boundTo` is set when the plate was conditioned on a blocking. **A non-null `boundTo` means the plate is welded to that silhouette** and must be re-requisitioned if the foreground changes; `isReusable` is the same fact stated positively. This is why a backdrop is a weaker asset than a material — it can be invalidated by work you do later.

`Assets.matte(descriptor, { size, invert })` returns a greyscale field for use as a shader input — a mask, a height field, a displacement source. Its `bytes` go through `Skia.Image.fromBytes(...)` or `Skia.Shader.bitmap(...)` like anything else.

---

## 9. Provenance and Reuse

> **Implemented by**: `Assets.library`, `material.provenance`, `material.id`.

`Assets.library` holds every material requisitioned this session, by any agent. **Look here before requisitioning a near-duplicate** — "weathered oak" and "weathered oak planking" are two charges for one surface.

```js
// It is array-LIKE, not an Array: length and [i] work, but forEach passes undefined
// as the index and the Array.prototype methods are absent.
const have = Array.from(Assets.library);
log('already requisitioned: ' + have.map(m => m.provenance.prompt).join(' | '));
```

`material.provenance` carries `{ model, hash, blockingHash, requester, generatedUtc, fromCache, prompt }`. **Record it.** A run that cannot say which surfaces were generated, by which model, from which prompt, cannot answer the first question anyone asks of a piece that used generation — and the run record does not currently capture requisitions on its own, so a `Stage.note` naming the descriptor and the model is what makes it answerable later.

`fromCache` distinguishes a fresh generation from a free cache hit, which is the honest way to report what a run actually cost.

---

## 10. Traps

| What happens | Why | What to do |
| :--- | :--- | :--- |
| Every property is `undefined` and it reads as a failure | The call was not `await`ed — **and it still spent** | `await` every `Assets.*` requisition |
| `Object.keys(result)` and `JSON.stringify(result)` show `Success`, `Bytes`, `FailureName` | The result is a typed object; the reference documents the camelCase spelling Jint resolves onto it | Read `result.success`; do not iterate the keys |
| The same descriptor is refused however often it is sent | `RefusedFormRequest` is deterministic and `retryable` is `false` | Reword to name a surface (§2) |
| The script times out with the drawing lost | Requisition and drawing in one script | Split them (§5); the re-run is cached |
| `budget.total` is `0` | Requisition is unavailable or denied on this project | Draw it; do not retry |
| A composited subject looks pasted onto its background | The plate's light was never read | `plate.metrics.keyLightX` / `keyLightY` into the lighting calls (§8) |
| A reused plate no longer fits the scene | `plate.boundTo` is non-null — it is welded to an old blocking | Check `isReusable`; re-requisition |
| A tiled surface shows faint banding | Check `material.tiling.repaired` | Log it; consider a fresh descriptor |

---

## 11. Symbol → SDK Map

| What you want | The call | Notes |
| :--- | :--- | :--- |
| Is this descriptor spendable? | `Assets.classify(d)` | Free, offline. `className`, `reason`, `triggers`, `allowed` |
| Can I afford this? | `Assets.budget.canAfford(n)` | Also `total`, `spent`, `remaining`, `cacheHits`, `tokensSpent` |
| A tiling surface | `await Assets.material(d, opts)` | `size` 32–1024, `tileable`, `format`, `quality`, `model` |
| A background | `await Assets.backdrop(d, opts)` | `width`, `height`, `keepQuiet`, `noHorizon`, `conditionOn` |
| A mask or height field | `await Assets.matte(d, opts)` | `size`, `invert` |
| Did it work? | `result.success` | Then `failureName`, `remedy`, `retryable`, `error` |
| Turn it into pixels | `material.toDataUri()` → `Skia.Image.fromDataUrl(...)` | Or `material.bytes` → `Skia.Image.fromBytes(...)` |
| Does it really tile? | `material.tiling.wraps`, `.repaired` | Guaranteed `true` on success |
| Where is the plate's light? | `plate.metrics.keyLightX` / `keyLightY` | Normalised `[0,1]`; feed the lighting calls |
| Is the plate still valid? | `plate.boundTo`, `plate.isReusable` | Non-null `boundTo` means welded to a blocking |
| What have I already got? | `Assets.library` | Array-*like*; `Array.from(...)` for a real array |
| Where did it come from? | `material.provenance` | `model`, `prompt`, `hash`, `fromCache`, `generatedUtc` |

---

## 12. Planning It: A Runnable Requisition Plate

This plate **spends nothing**, by construction. It vets a shopping list with the free offline classifier, reports the budget, and demonstrates the failure protocol using a descriptor that is refused *before* the network is touched.

> [!CAUTION]
> The one `await Assets.material(...)` below is free **only because it is deliberately refused** — `material()` classifies first and returns `RefusedFormRequest` without reaching the service. A well-formed descriptor in that position would spend. Do not paste this plate with the descriptor changed.

```javascript
// A requisition plan: vet every descriptor before committing to any of them.
const wanted = [
    'weathered ship hull planking, tarred caulking between planks',
    'a wooden pirate ship at sea',
    'rusted corrugated iron sheeting',
    'a portrait of a woman smiling',
    'human skin texture, fine pores',
    'a logo for a coffee shop'
];

// §2 — classify is free and offline, so the whole list can be vetted for nothing.
const plan = [];
for (const descriptor of wanted) {
    const v = Assets.classify(descriptor);
    plan.push({ descriptor: descriptor, ok: v.allowed, verdict: v.className, triggers: v.triggers });
    log((v.allowed ? 'SPEND ' : 'REWORD') + '  ' + v.className +
        (v.triggers.length ? '  triggers=' + Array.from(v.triggers).join(', ') : '') + '  | ' + descriptor);
}
const spendable = plan.filter(p => p.ok);
log(spendable.length + ' of ' + plan.length + ' descriptors are materials');

// §3 — decide affordability against the real list, not against a guess.
log('budget: ' + Assets.budget.spent + '/' + Assets.budget.total + ' spent, ' +
    Assets.budget.remaining + ' remaining, ' + Assets.budget.cacheHits + ' cache hits, ' +
    Assets.budget.tokensSpent + ' tokens billed');
log('can afford the plan: ' + Assets.budget.canAfford(spendable.length));
log('already in the library: ' + Array.from(Assets.library).length);

// §6 — the failure protocol, on a call that cannot spend because it is refused first.
const refused = await Assets.material('a wooden pirate ship at sea');
log('success=' + refused.success + '  failureName=' + refused.failureName +
    '  retryable=' + refused.retryable);
log('remedy: ' + refused.remedy);
log('spent nothing: ' + (Assets.budget.spent === 0));

// §4 — the same call without await, to show what the mistake looks like.
// (Still free here, for the same reason; with a real descriptor it would charge.)
const notAwaited = Assets.material('a wooden pirate ship at sea');
log('un-awaited reads as: success=' + notAwaited.success + ', failureName=' + notAwaited.failureName);
await notAwaited;

// The plan as a board, so a director can see what was vetted and what was rejected.
const canvas = createCanvas(880, 60 + plan.length * 44);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f4';
ctx.fillRect(0, 0, canvas.width, canvas.height);
ctx.textBaseline = 'top';

const page = Layout.inset(Layout.rect(0, 0, canvas.width, canvas.height), 20);
ctx.font = '700 19px sans-serif';
ctx.fillStyle = '#1c2733';
ctx.fillText('Requisition plan — ' + spendable.length + ' of ' + plan.length + ' spendable', page.x, page.y);

const rows = Layout.stack(Layout.rect(page.x, page.y + 34, page.width, plan.length * 44), 
    plan.map(() => 38), 6);
for (let i = 0; i < plan.length; i++) {
    const row = rows[i];
    ctx.fillStyle = plan[i].ok ? '#e6efe8' : '#f6e4e0';
    ctx.fillRect(row.x, row.y, row.width, row.height);
    ctx.fillStyle = plan[i].ok ? '#1f6f3b' : '#a33526';
    ctx.fillRect(row.x, row.y, 5, row.height);

    ctx.font = '600 12px sans-serif';
    ctx.fillText(plan[i].ok ? 'SPEND' : 'REWORD', row.x + 16, row.y + 6);
    ctx.font = '400 13px sans-serif';
    ctx.fillStyle = '#2b3742';
    const note = plan[i].triggers.length
        ? plan[i].descriptor + '   — names ' + Array.from(plan[i].triggers).join(', ')
        : plan[i].descriptor;
    ctx.fillText(note, row.x + 86, row.y + 6, row.width - 100);
    ctx.fillStyle = '#7a8794';
    ctx.font = '400 11px sans-serif';
    ctx.fillText(plan[i].verdict, row.x + 86, row.y + 22);
}

canvas;
```
