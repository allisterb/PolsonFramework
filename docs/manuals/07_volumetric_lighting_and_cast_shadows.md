# Studio Manual 07: Volumetric Lighting, Cast Shadows & Studio Setups

> **Source Reference**: Andrew Loomis, *Creative Illustration* (Viking Press, 1947) — §1 from
> pp. 82–86 and 102–103; Andrew Loomis, *Successful Drawing* (Viking Press, 1951) — §1's three laws
> and the "hump" from pp. 78–80, §2 from pp. 80–88, §4 from pp. 80–81 and 83; and Andrew Loomis,
> *The Eye of the Painter* (Viking Press, 1961) — §3 from pp. 31, 34 and 104–105. **All four sections
> are now cited.** Where a number is the studio’s calibration rather than Loomis’s, it says so
> on the spot.  
> **Purpose**: Translates volumetric lighting theory (the 6 tonal light zones, Lambertian diffuse falloff, cast shadow geometric projection, contact occlusion, and 3-point studio lighting) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Anatomy of Light on Form (The 6 Tonal Zones)

> **Implemented by**: `Drawing.renderVolumetricSphere(ctx, cx, cy, radius, lightDirection, options)` and `Drawing.renderVolumetricCylinder(ctx, x, y, width, height, lightDirection, options)` render all six zones in one pass; `Drawing.createVolumetricSphereShader(options)` returns the equivalent as an SkSL shader for filling arbitrary paths.

> **Source**: Andrew Loomis, *Creative Illustration* (Viking Press, 1947) — "Basic Intensities of
> Light versus Shadow" p. 82, and "The Four Properties of Tone" pp. 83, 86.

> [!IMPORTANT]
> **A light's quality *is* the size of the value gap it opens, and Loomis gives that as a number.**
> His first property of tone is the intensity of light *in relation to* shadow: the stronger the light,
> the darker the shadow appears and the greater the contrast; the weaker it is, the closer the shadow
> comes to the value of the light. In a diffused light both go diffuse together, and in a dim hazy
> light the two are very close in value.
>
> He scales it as **tone separation** — how many steps apart the lit and shadow sides sit:
>
> | Separation | Lighting |
> | ---: | :--- |
> | 1 tone | diffused light |
> | 2 tones | hazy sunlight |
> | 3 tones | full sunlight |
> | 4 tones | strong artificial light |
> | 5 tones | spotlight |
> | full | limit of intensity — practically black |
>
> This is the missing guidance for the colour pairs the renderers take. `baseColor` and `shadowColor`
> on `renderVolumetricSphere` are two ends of exactly this gap, and nothing anywhere said how far
> apart to put them. Overcast exterior: one or two steps. Noon sun: three. A single bare bulb or a
> hard key: four or five. **Picking the gap first, then the hues, is the order that produces a
> coherent scene** — the commonest failure is a shadow chosen for its colour and landing at the wrong
> distance from its own light.

> **Principle**:
> Whenever light strikes a curved 3D form, it creates 6 distinct tonal zones:
>
> 1. **Center Light / Highlight (Specular Peak)**: The point on the surface where the angle of incidence equals the angle of reflection directly toward the camera ($(\mathbf{N} \cdot \mathbf{H})^\alpha$).
> 2. **Midtone / Halftone (Diffuse Plane)**: The illuminated zone where light strikes at an angle. Intensity is governed by **Lambert's Cosine Law**: $I = I_{\text{light}} \max(0, \mathbf{N} \cdot \mathbf{L})$.
> 3. **The Terminator**: The geometric dividing boundary where light rays graze the surface tangentially ($\mathbf{N} \cdot \mathbf{L} = 0$).
> 4. **Core Shadow**: The darkest band of the form shadow, positioned immediately past the terminator. It receives neither direct light rays nor ambient bounce.
> 5. **Reflected Light (Ambient Bounce)**: Soft indirect illumination bouncing off the floor/environment back into the shadow side.
> 6. **Cast Shadow & Ambient Occlusion (Contact Crevice)**:
>    - **Umbra**: Darkest, sharp core of the cast shadow.
>    - **Penumbra**: Soft transitional boundary due to light source size.
>    - **Ambient Occlusion**: Pure dark crevice where two surfaces meet and block all ambient light.

```
                  Light Rays (Key Light)
                    ╲   ╲   ╲   ╲
                     ╲   ╲   ╲   ╲
                      ▼   ▼   ▼   ▼
               ┌────────────────────────┐
               │    1. Highlight (Pure) │
               │ 2. Halftone (Diffuse)  │
               │────────────────────────│ ◄── 3. The Terminator (N · L = 0)
               │    4. Core Shadow      │ (Darkest Form Shadow)
               │ 5. Reflected Light     │ (Ambient Bounce from Floor)
               └────────────────────────┘
                 ▲                    ▲
                 │ 6. Contact Occl.   │ Cast Shadow (Umbra ➔ Penumbra)
                 ●────────────────────●───────────────────────►
```

---

### The "lost and found" of edges

> **Source**: Loomis, *Creative Illustration*, pp. 102–103 — "The Treatment of Edges".

The six zones above say what happens *across* a form. This says what happens at its **boundary**, and
Loomis calls it perhaps the most important element in getting a drawing to feel free rather than cut
out. Edges are **lost and found**: the same contour is sharp in some places and dissolved in others.

His example is the one to hold onto. A polished square table has four edges that are hard *to the
touch* — and if you draw them hard all the way round, you have drawn what you know rather than what
you see. Look at the real table and every edge differs along its length: in places it passes tones
that merge with it, in others it stands out in relief; a reflection running to the edge makes it
sharp, a dark reflection leaves it undefined.

Three reasons an edge goes soft, and they want different treatment:

1. **Deliberate subordination.** Bring the two tones **closer in value where they meet** — darken a
   light background as it approaches a dark edge, lighten it as it approaches a light one. Loomis
   describes it as *extending the one tone a little way into the other*. The edge is still held; it
   just stops asking for attention. `Skia.MaskFilter.blur(sigma, 'outer')` is the mechanical form of
   this, lifting a haze strictly outside one contour without moving either mass's own value.
2. **The material is soft.** A beard, a wisp of hair, fine twigs, lace, transparent fabric, mist,
   cloud, spray. Here the edge genuinely *is* a mixture of the form with what lies behind it, and
   `'normal'` blur on the coverage — an airbrushed edge — is what that looks like.
3. **The values have converged.** Where a contour arrives at a value equal to what it sits against,
   let it go entirely. Light against light, grey against grey.

> [!IMPORTANT]
> **This is the counterpoint to Manual 09 §3's "two adjacent masses have merged in value".** That
> section treats a merge as a defect with a fix, and often it is — a silhouette that stops reading is
> a real failure. But Loomis's third case is the same situation treated as an **opportunity**: when
> two values are close anyway, it is safe to lose the edge *further*, and spend the sharpness
> somewhere it does more good.
>
> The two are reconciled by asking what the edge is doing for the picture. A merged edge on the
> subject's silhouette against its background is a defect. A merged edge on a secondary mass —
> shoulder into shadow, a background plane into another — is a saving, because **every sharp edge
> spends attention, and there is a fixed amount to spend.** Decide which edges you are buying before
> reaching for either remedy.

### Three laws of light, and why the core shadow is conditional

> **Source**: Andrew Loomis, *Successful Drawing* (Viking Press, 1951) — pp. 78-80.

Loomis states three laws in sequence, and each decides something the six-zone model above otherwise
leaves to taste:

1. **Light from a single source travels in a straight line, so it cannot reach more than halfway
   around a round form.** The terminator is therefore not a judgement call: given a light direction,
   the shadow begins at the halfway mark around the form. That is what
   `Drawing.renderVolumetricSphere(...)` places from `lightDirection`, and it is why a sphere lit by
   one source can never be more than half in light.
2. **A surface is lit according to the angle it presents to the source.** Brightest where flat-on or
   perpendicular to the light, darkening with every increase in the curve away from that, to a
   maximum at and just past the shadow's edge.
3. **Only a flat plane can be evenly lit in one value.** Curved and rounded planes must be graduated.
   Loomis makes this the *identity* of the form rather than a rendering nicety: the way an area is
   treated is what tells the observer it is round or flat. A cube is rendered in flat tones, a sphere
   or egg only in graduated ones, and every subject is a combination of the two.

> [!IMPORTANT]
> **Law 3 is a check on the SDK, not just advice.** A flat fill on a curved mass and a gradient on a
> flat plane both say the wrong thing about the form, and both are easy to reach for by accident — a
> solid `fillStyle` on a limb, or a gradient laid across a wall because it looked bare. Ask which of
> the two a shape is before filling it. `Drawing.renderVolumetricCylinder(...)` and
> `Drawing.renderVolumetricSphere(...)` graduate for you; a genuine plane wants `ctx.fillRect(...)`
> and a flat colour.

**The core shadow (zone 3) exists only when something bounces light back.** Illustrators call the
dark band at the shadow's edge *the hump*, and Loomis is explicit that it appears only once the
initial light has been reflected back onto the object — it is caused by neither the direct light nor
the reflected light being able to reach the surface at that angle. Where there is no reflected light
at all, his example being the half moon, the shadow is a **flat tone** with no hump and no bounce.

So the six zones are a consequence rather than a recipe. Pass a `bounceColor` and you have earned a
core shadow; where a scene genuinely has nothing to bounce — a form against void, a night exterior —
a flat shadow side is the correct drawing and a hump is invention.

> Loomis also gives the photographic recipe for the effect, and it reads directly as a lighting rig:
> point the fill straight back at the key, at no more than half its intensity. §4 uses that number.

## 2. Cast Shadow Geometric Projection

> **Implemented by**: `Drawing.projectCastShadow(lightSource, groundYOrGrid, verticesOrBounds, options)`, then `Drawing.drawCastShadow(ctx, shadow, options)`. Argument 2 is either a ground-line `Y` or a `PerspectiveGrid` — see the two models below. Signatures: `polson://sdk/core/Drawing`.
>
> **Two ground models — argument 2 chooses.** Pass a **number** for a ground *line*, or a **`PerspectiveGrid`** for a ground *plane*.
>
> `Drawing.drawCastShadow(...)` throws on a zero-area polygon rather than quietly drawing nothing, so a shadow that fails to appear reports itself.

> **Source**: Andrew Loomis, *Successful Drawing* (Viking Press, 1951) — pp. 80-88. Loomis names
> **three things** every cast shadow needs: the position of the light source, the angle of light, and
> **the vanishing point of the shadows on the horizon**. That vanishing point sits on the horizon
> directly beneath the source, and the construction is: lines from the light source down through the
> object's **top** corners, crossed with lines from the shadow vanishing point through its **bottom**
> corners. Where the two sets cross is the limit of the shadow on the ground plane.
>
> That is Model B below, and the correspondence is exact — worth stating plainly, because this manual
> previously carried the construction with no source at all. When the light is **behind the viewer**
> Loomis takes the angle of light from a point dropped *below* the horizon on the perpendicular
> through the shadow vanishing point, and brings the lines up to the object instead; in image terms
> that is a light source below the horizon, which is the case the last paragraph of Model B describes.

> **Principle**:
> To project a realistic cast shadow on a ground plane $Y = \text{groundY}$:
>
> 1. Identify the 2D light source position $L = (L_x, L_y)$ and its ground projection $L_{\text{ground}} = (L_x, \text{groundY})$.
> 2. For each top vertex $V_i = (x_i, y_i)$ of the object:
>    - Project a ray from the light source $L$ through $V_i$.
>    - Project a ray from $L_{\text{ground}}$ through the corresponding base vertex $B_i = (x_i, \text{groundY})$.
>    - The intersection point $S_i$ is the shadow vertex on the ground plane!
> 3. Connect shadow vertices $S_i$ to base points $B_i$ to form the **Cast Shadow Footprint Polygon**.

### Model A — the ground line (2D elevation)

Follow the construction below literally with a scalar `groundY` and every projected vertex lands on the ground line: $L_{\text{ground}}$ and $B_i$ both sit at `groundY`, so that second ray *is* the ground line. That is not an error in the maths — a ground plane seen edge-on has no thickness, and its shadow really is a segment. `options.groundDepth` supplies the recession that 2D inputs cannot, defaulting to about a fifth of the contact width. The light ray still fixes the shadow's **length and direction**; `groundDepth` only adds the missing dimension.

Use this when the scene has no perspective construction — a character on a notional floor, a product on a plain ground.

### Model B — the ground plane (perspective)

Pass the `PerspectiveGrid` from Manual 06 instead and the construction becomes exact, because the horizon defines a real plane:

1. The light is read as the **vanishing point of the light rays**. Their ground projections therefore converge on $VP_{\text{shadow}} = (L_x, HL_y)$ — on the horizon, directly beneath the light.
2. Each shadow vertex is the intersection of the ray $L \rightarrow V_i$ with the ground ray $VP_{\text{shadow}} \rightarrow B_i$.
3. Contact points at different depths sit at different image heights, so the footprint comes out as a genuine receding quad — **narrowing with distance, no `groundDepth` needed**.

This is the classical architectural method, and it is why a `PerspectiveBox` is the ideal caster: it already carries all eight vertices, so each top vertex has its true contact point. A bare point list has tops only and is rejected rather than guessed at.

Two configurations are refused with a message instead of nonsense coordinates:

- **Shadow at or beyond the horizon.** A light too near the horizon throws a shadow of infinite length. Raise it, or move it further from the horizon.
- **Rays parallel to the ground rays.** The shadow never lands. Move the light off the horizon line or out from directly above the object.

> Where is the light? Below the horizon in image terms means **behind the viewer**, and shadows recede away from camera — the lower it sits (the closer to the horizon from below), the longer they stretch. Above the horizon means the light is **in front**, and shadows come toward the viewer instead.

### Round casters: the sphere and the cone

Neither has corners to project, so Loomis gives each its own construction (*Successful Drawing*,
pp. 80-84). Both are worth having, because `Drawing.projectCastShadow(...)` takes vertices or a box
and can help with neither — you build these and fill the result yourself.

**Sphere.** The central ray is the line from the light source through the **centre** of the sphere.
Where it meets the ground plane is the **centre of the cast shadow**, and that shadow is always seen
as an ellipse. So it is one ray and one ellipse, not a polygon:

```javascript
// A sphere and its cast shadow: one ray, one ellipse.
const canvas = createCanvas(900, 700);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#cfc6b8';
ctx.fillRect(0, 0, 900, 700);

const light = { x: 210, y: 90 };
const centre = { x: 470, y: 330 };
const radius = 96;
const groundY = 520;

// The central ray, extended to the ground, locates the centre of the shadow.
const t = (groundY - light.y) / (centre.y - light.y);
const sx = light.x + (centre.x - light.x) * t;

ctx.save();
ctx.fillStyle = 'rgba(20,18,26,0.45)';
ctx.beginPath();
ctx.ellipse(sx, groundY, radius * 2.1, radius * 0.62, 0, 0, Math.PI * 2);   // foreshortened cap
ctx.fill();
ctx.restore();

// The form itself, lit from the same source the shadow was projected from.
Drawing.renderVolumetricSphere(ctx, centre.x, centre.y, radius,
    { x: light.x - centre.x, y: light.y - centre.y },
    { baseColor: '#b8794a', drawGroundShadow: false });

log('shadow centre x = ' + sx.toFixed(1));
canvas;
```

The ellipse's flatness is foreshortening, and the same rule governs it as governs a cylinder's cap in
Manual 06: a contact point nearer the horizon gives a flatter ellipse.

**Cone.** Draw the direction of the light through the centre of the base, and divide the base ellipse
at the two points where that line crosses it. From the apex, draw the angle of light down to the
ground plane; where it meets the direction line is the **point of the shadow**. Connect that point to
the two halfway points on the base ellipse, and the shadow closes — two straight sides and the far
half of the base.

> [!TIP]
> The two differ in an instructive way. A sphere's shadow is found from the ray through its
> **centre**, because a sphere has no contact geometry to speak of; a cone's needs the ray through
> its **apex** plus its base ellipse, because it has both. Any caster built by hand reduces to the
> same question — which points touch the ground, and which cast from above it.

---

> **Atmosphere is made of noise, not of low-alpha shapes.** The three general shader presets —
> `Drawing.createAtmosphericCloudShader(fx, fy, octaves, seed)` for air and vapour,
> `createRopeFiberShader(...)` for fibrous material, and `createHalftoneDotShader(...)` for Ben-Day
> tone — are documented in **Manual 04 §5**. Only the halftone one is really about comics; the other
> two are general, and the atmospheric preset is what an aerial-perspective or haze pass wants.
> Pair it with `Skia.MaskFilter.blur(sigma)`, which softens a shape's coverage rather than blurring
> the result, so a mist bank has no edge. Reach for both before hand-writing SkSL.

## 3. Color Temperature — Cause and Effect, Not a Complement Rule

> **Implemented by**: the colour options rather than a call of its own — pass temperatures as `baseColor` / `shadowColor` / `bounceColor` to `Drawing.renderVolumetricSphere(...)`, and as `keyColor` / `fillColor` to `Drawing.createThreePointLighting(...)`. For a shadow that changes temperature across its own depth, clip the shadow shape and fill it with `ctx.createLinearGradient(...)`. For the unity pass, assign `Skia.ColorFilter.blend(...)` to `ctx.colorFilter`.

> **Source**: Andrew Loomis, *The Eye of the Painter* (Viking Press, 1961) — pp. 31 and 34 for the cause-and-effect account of shadow colour, pp. 104-105 for warm/cool as a way of seeing neutrals and for the primaries rule.

> **Principle**:
> A shadow's colour is **the colour of whatever light reaches it**, and Loomis works it as cause and
> effect rather than as a rule about complements. His example is the shadow side of a barn with
> sunlit green grass in front of it and blue sky above: the grass bounces up into the lower part of
> the shadow, the sky cools the upper part, and the barn's own local colour tints all of it — so
> **that one shadow is warm at the bottom and cooler at the top.**
>
> Two consequences follow, and both are stronger than the rule this section used to state:
>
> 1. **A shadow is rarely one colour.** Filling it with a single shifted hue discards the information
>    that says what the form is standing on and near. A gradient across the shadow, running from the
>    ground bounce to the sky, is the sourced construction — and naming the source of each end of it
>    is the discipline. Loomis calls this looking for cause and effect in colour.
> 2. **Colour is relational.** Pictorially, colour is true only when its value is right and its
>    warmness or coolness is right *in relation to the neighbouring colour*. There is no correct
>    shadow colour to look up in isolation, which is why the temperatures below are a starting
>    position rather than an answer.

**Starting temperatures** — the studio's calibration, not Loomis's numbers:

| Key light | Ambient / sky term | Typical use |
| --- | --- | --- |
| Warm sun, `#fff5e0` | Cool sky, `#3a4d66` | Daylight exterior. |
| Cool moon or fluorescent, `#d6e8ff` | Warm bounce, `#4a382e` | Night, or an interior over a warm floor. |

Take the second column as the *sky* end of the shadow gradient, and take the other end from whatever
the form is actually standing on — grass, sand, a red rug — rather than from the key's complement.

### The unity pass — never all three primaries at full strength

Loomis reports it as a rule handed down by the finest painters: the three primaries should never
appear at full strength in the same picture, because being unrelated they fight one another. The
remedy is mechanical, and it is the most directly implementable colour instruction in any of these
books — **mix a little of one colour into all the others.** One stays dominant; the other two become
related even when only slightly reduced in brilliancy. Extended with a secondary or tertiary, it is
how a late-afternoon scene gets its warmth: a touch of the same orange into everything.

That is a global colour filter, applied once at the end:

```javascript
// The unity pass: a little of one colour into everything else.
const scene = createCanvas(900, 700);
const sctx = scene.getContext('2d');
const bands = ['#d81f26', '#1b4fd8', '#f2c400', '#0e8a3c'];   // primaries at full strength
for (let i = 0; i < bands.length; i++) {
    sctx.fillStyle = bands[i];
    sctx.fillRect(i * 225, 0, 225, 700);
}

const canvas = createCanvas(900, 700);
const ctx = canvas.getContext('2d');
ctx.colorFilter = Skia.ColorFilter.blend('#e8a13c', 'overlay');
ctx.drawImage(scene.toBitmap(), 0, 0);
ctx.colorFilter = null;

// The same measurement before and after, so the pass is checked rather than assumed.
const before = scene.bitmap.palette(4);
const after = canvas.bitmap.palette(4);
for (let i = 0; i < before.length; i++) log(before[i].color + '  ->  ' + after[i].color);

canvas;
```

> [!TIP]
> **Check it rather than trusting it.** `bitmap.palette(6)` returns the dominant colours and their
> shares, so a frame carrying three near-primaries at full strength says so — and after the pass the
> same call shows them pulled toward one another. Manual 15 covers reading the result.

---

## 4. Three-Point Studio Lighting (Key, Fill, Rim/Kicker)

> **Implemented by**: `Drawing.createThreePointLighting(options)` → `{ keyLight, fillLight, rimLight }`, each `{ angleDeg, color, intensity }`. Feed `keyLight.angleDeg` to the volumetric renderers as the light direction, and `rimLight.angleDeg` to `Drawing.drawRimLight(ctx, bounds, angleDeg, color, thickness, options)`.

> [!IMPORTANT]
> **The angle does the selecting — you do not trim the point list by hand.** Each stretch of contour is weighted by `max(0, n · L) ^ spread`, so the side facing away from the light is not drawn and the lit arc fades toward the terminator. That is the difference between a rim and an outline, and it is why `spread` (default `2`) is the knob worth reaching for: raise it for a tighter kicker, drop it to `1` for a broad falloff across the whole lit half.
>
> **The band is drawn just inside the contour**, because a rim is light on the form rather than a wire beside it. So the list you pass has to **be** the silhouette. A list that merely approximates it — say 10 px outboard of the figure — draws a pale wire floating clear of the form, which reads as a rim at 100% zoom and is obviously wrong at full size. Nothing in the call can know the form well enough to correct that for you.
>
> Both are recorded because the call did neither. It stroked the whole point list at full opacity whatever the angle, and offset every point two pixels *outward* along the light vector; a live run hand-trimmed its lists to the lit arc, then abandoned the call and built every rim as a gradient fill inside a clipped shape. That construction is still the sturdier one when a rim has to follow a form exactly — `ctx.clip(silhouette)` and a gradient running inward from the lit edge. Reach for `drawRimLight` when you already hold the contour as points.

> **Principle**:
> The standard three-light setup used in portraiture, cinematography and comics:
>
> 1. **Key light**: the dominant source. Its quality and position determine the overall look more than
>    anything else in the rig.
> 2. **Fill light**: opens the shadows the key leaves, so detail stays readable. Softer and weaker,
>    usually from the opposing side and cooler in temperature.
> 3. **Kicker / rim light**: a hard source behind the subject, producing a lit edge that separates it
>    from the background — visible only where it grazes that back edge.

> **Source**: Andrew Loomis, *Successful Drawing* (Viking Press, 1951) — pp. 80-81 for the fill light and
> the light/shadow proportions, p. 83 for the consistency rules.

**The fill light has a sourced number.** Loomis gives it as the recipe for photographic reference:
point the fill **directly back at the key light**, at **no more than half its intensity**. Aimed that
way it returns light along the key's own axis, which is what preserves the dark band at the shadow's
edge — the hump of §1 — instead of flattening it. A fill above half the key erases the core shadow
and, with it, the roundness.

**Prefer a quarter lighting to an even split.** With the form at right angles to both viewer and
source it reads half in light and half in shadow; at a quarter position it is three-quarters one and
one-quarter the other. Loomis: having either light or shadow dominate is more effective than an equal
division between them. Full-front lighting — the source behind the viewer — gives light and halftone
only, with the darkest darks at the contours, and is good for simple or postery effects.

> [!WARNING]
> **A second key breaks the form.** Loomis is unequivocal: two light sources tend to break down the
> solidity of the form, and crisscross lighting — sources to both the artist's right and left — is
> especially bad, because it cuts everything into small lights and shadows. The three-point rig is
> not an exception. Its fill and rim are *subordinate*, aimed and rated relative to the key, and the
> moment a second source is strong enough to open a shadow of its own, the form begins to come apart.
> If a scene needs two visible sources, decide which one owns the modelling.

### Two consistency rules, and both are checkable

These are the cheapest defects to catch and among the commonest to ship:

1. **If one thing casts a shadow, all things must cast shadows.**
2. **If one shadow is soft and diffused, every other shadow gets the same treatment.**

Loomis attaches a diagnosis worth repeating: when a drawing's effect is bad and the reason is not
obvious, this is often it. The difference between direct and diffused light — sharply defined and
positive, against gradual with no sharp definition — has to hold across the whole picture rather
than per object.

> [!TIP]
> Both survive as a check on the render rather than as an intention. Count the forms touching the
> ground, then count the shadow shapes; a caster with no `Drawing.drawCastShadow(...)` call is a
> defect the picture will show before a viewer names it. For softness, one `penumbraBlur` held in a
> variable and passed to every shadow is a stronger guarantee than remembering. Manual 18's
> `Stage.check` is where either verdict belongs.

> [!NOTE]
> **The numbers are the toolkit's defaults, not a cited rule.** `createThreePointLighting` ships
> `~70/30/90%` intensities and $-45^\circ$ / $+60^\circ$ angles; earlier versions of this manual
> presented them as sourced. Treat them as a starting position to tune.
>
> **One of them now does have a source.** Loomis's fill rule — aimed back at the key, at no more
> than half its intensity — is a ceiling, and the shipped `30 / 70` works out at about 43% of the
> key, comfortably under it. The angles remain the studio’s.
>
> The kicker's *visible only where it grazes the back edge* is worth holding onto when using
> `Drawing.drawRimLight`: that is the culling the call performs, and why a rim stroked along a whole
> contour reads as an outline rather than as light.

---

## 5. Symbol → SDK Parameter Map

| Concept | SDK parameter or field | Notes |
| --- | --- | --- |
| $\mathbf{L}$ (light vector) | `lightDirection` as `{ x, y }` | Points *from* the surface *toward* the light. |
| Highlight (zone 1) | `options.highlightColor` | Take the key light's temperature. |
| Halftone (zone 2) | `options.baseColor` | The local colour of the form. |
| Core shadow (zone 4) | `options.shadowColor` | Complementary temperature to the key (§3). |
| Reflected light (zone 5) | `options.bounceColor` | The environment's colour, not black. |
| Umbra / penumbra (zone 6) | `options.opacity`, `options.penumbraBlur` | Larger blur = larger apparent light source. |
| $L = (L_x, L_y)$ | `lightSource` as `{ x, y }` | Argument 1 of `projectCastShadow`. |
| $Y = \text{groundY}$ | `groundY` | Argument 2; also returned on the shadow object. |
| $S_i$ footprint | `shadow.shadowPolygon` | Contact edge first, then the projected edge walked back. |
| (no book symbol) | `options.groundDepth` | Apparent ground depth, **model A only**. Defaults to ~20% of the contact width. |
| $HL_y$ | `grid.horizonY` | Model B: pass the whole grid as argument 2. |
| $VP_{\text{shadow}}$ | `shadow.vpShadow` | Model B: on the horizon, directly beneath the light. |
| Which model ran | `shadow.model` | `'groundLine'` or `'perspective'`. |
| Key / Fill / Rim | `rig.keyLight`, `.fillLight`, `.rimLight` | Each `{ angleDeg, color, intensity }`. |

> Note the default intensities returned by `Drawing.createThreePointLighting(...)` — key `0.75`, fill `0.30`, rim `0.90` — match the ~70/30/90 ratios of §4.

---

## 6. Constructing It: A Runnable Scene

Set the rig first, so the key angle drives both the shading and the cast shadow rather than being guessed twice. Then shadow before form: the cast shadow is ground geometry the form overlaps at the contact crevice, so drawing it afterwards paints over the occlusion and the form floats.

```javascript
// One sphere, lit properly: rig → key direction → cast shadow → six tonal zones.
const canvas = createCanvas(900, 600);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#e8e4dc';
ctx.fillRect(0, 0, 900, 600);

const groundY = 470;   // the sphere below sits at cy 380, r 90 — its base
const bounds = { x: 360, y: 290, width: 180, height: 180 };

// §4 — Set the rig once. Warm key against cool fill satisfies §3.
const rig = Drawing.createThreePointLighting({
    keyAngleDeg: -45,
    fillAngleDeg: 60,
    rimAngleDeg: 135,
    keyColor: '#fff5e0',
    fillColor: '#3a4d66'
});

// Derive the light vector from the rig rather than hard-coding a second one —
// otherwise the shading and the rig drift apart the moment either is edited.
const keyRad = (rig.keyLight.angleDeg * Math.PI) / 180;
const key = { x: Math.cos(keyRad), y: Math.sin(keyRad) };
log('key ' + rig.keyLight.angleDeg + '° @ ' + rig.keyLight.color +
    ', fill ' + rig.fillLight.color + ' — complementary temperatures (§3)');

// §2 — Cast the footprint first. The light position sets its length and direction;
// groundDepth supplies the recession the 2D projection cannot know. Omit it and a
// sensible default is derived from the contact width.
const shadow = Drawing.projectCastShadow(
    { x: 250, y: 90 },
    groundY,
    bounds,
    { shadowColor: rig.fillLight.color, opacity: 0.45, penumbraBlur: 12, groundDepth: 34 }
);
log('footprint spans x ' + shadow.shadowPolygon[0].x.toFixed(0) +
    ' -> ' + shadow.shadowPolygon[2].x.toFixed(0) +
    ', depth ' + shadow.groundDepth);
Drawing.drawCastShadow(ctx, shadow);

// §1 — Then the form, overlapping its own contact crevice. All six tonal zones in
// one pass. Cool core shadow under a warm key (§3); the bounce carries the floor's
// colour, never neutral grey.
Drawing.renderVolumetricSphere(ctx, 450, 380, 90, key, {
    baseColor: '#c8b8a0',
    highlightColor: rig.keyLight.color,
    shadowColor: rig.fillLight.color,
    bounceColor: '#8a7f6d',
    drawGroundShadow: false
});

// §4 — Drawing.drawRimLight(ctx, boundsOrPts, angleDeg, color, thickness) adds the
// kicker. Give it a silhouette point list, not a bounds rect: a rect rims its own four
// edges correctly, which is still a rectangle's rim and not a curved form's.

canvas;
```

### Model B in practice: a shadow on a perspective ground plane

The same call, given Manual 06's grid instead of a scalar. Note what is *absent*: no `groundDepth`. The recession comes from the geometry, so the footprint narrows toward the horizon on its own.

```javascript
// A box on a perspective ground plane, with its true projective shadow.
const canvas = createCanvas(900, 600);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe8';
ctx.fillRect(0, 0, 900, 600);

// Manual 06 §1 — the ground plane. Its horizon is what makes the shadow exact.
const grid = Drawing.createPerspectiveGrid({
    type: '2point',
    horizonY: 200,
    centerOfVisionX: 450,
    focalLength: 900,
    cameraAngleDeg: 35
});
Drawing.drawPerspectiveGrid(ctx, grid, { lineCount: 14, lineWidth: 0.5 });

// Manual 06 §2 — the caster. A PerspectiveBox already carries all eight vertices,
// so every top vertex has its true contact point and nothing has to be guessed.
const box = Drawing.createPerspectiveBox(grid, 430, 470, 150, 150, 170);

// §2 model B — the light sits just below the horizon, so it is behind the viewer
// and low: shadows stretch away from camera toward the horizon. Move it further
// below the horizon to raise the sun and shorten them.
const shadow = Drawing.projectCastShadow({ x: 150, y: 250 }, grid, box, {
    shadowColor: '#3a4d66',
    opacity: 0.5,
    penumbraBlur: 6
});
log('model=' + shadow.model +
    '  vpShadow=' + shadow.vpShadow.x.toFixed(0) + ',' + shadow.vpShadow.y.toFixed(0));

// Shadow first, so the box overlaps its own contact edge (§1, zone 6).
Drawing.drawCastShadow(ctx, shadow);

Drawing.drawPerspectiveBox(ctx, box, {
    topFill: '#d9dee6',
    leftFill: '#9aa5b4',
    rightFill: '#5d6878',
    strokeColor: '#232a34',
    strokeWidth: 1.5
});

canvas;
```
