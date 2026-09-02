# Studio Manual 07: Volumetric Lighting, Cast Shadows & Studio Setups

> **Sources under revision.** This manual previously cited *Imaginative Drawing* (John Guy, 2025).
> That work's terms ask that it be shared only in its entirety, and withhold permission for it to be
> used for machine learning or AI. Distilling it into a manual that is served to agents is against
> both, so the citations have been withdrawn. The constructions below are standard studio practice
> and are being re-sourced; treat any unattributed claim here as pending a citation, not as verified.
> **Purpose**: Translates volumetric lighting theory (the 6 tonal light zones, Lambertian diffuse falloff, cast shadow geometric projection, contact occlusion, and 3-point studio lighting) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Anatomy of Light on Form (The 6 Tonal Zones)

> **Implemented by**: `Drawing.renderVolumetricSphere(ctx, cx, cy, radius, lightDirection, options)` and `Drawing.renderVolumetricCylinder(ctx, x, y, width, height, lightDirection, options)` render all six zones in one pass; `Drawing.createVolumetricSphereShader(options)` returns the equivalent as an SkSL shader for filling arbitrary paths.

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

## 2. Cast Shadow Geometric Projection

> **Implemented by**: `Drawing.projectCastShadow(lightSource, groundYOrGrid, verticesOrBounds, options)`, then `Drawing.drawCastShadow(ctx, shadow, options)`. Argument 2 is either a ground-line `Y` or a `PerspectiveGrid` — see the two models below. Signatures: `polson://sdk/core/Drawing`.
>
> **Two ground models — argument 2 chooses.** Pass a **number** for a ground *line*, or a **`PerspectiveGrid`** for a ground *plane*.
>
> `Drawing.drawCastShadow(...)` throws on a zero-area polygon rather than quietly drawing nothing, so a shadow that fails to appear reports itself.

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

---

> **Atmosphere is made of noise, not of low-alpha shapes.** The three general shader presets —
> `Drawing.createAtmosphericCloudShader(fx, fy, octaves, seed)` for air and vapour,
> `createRopeFiberShader(...)` for fibrous material, and `createHalftoneDotShader(...)` for Ben-Day
> tone — are documented in **Manual 04 §5**. Only the halftone one is really about comics; the other
> two are general, and the atmospheric preset is what an aerial-perspective or haze pass wants.
> Pair it with `Skia.MaskFilter.blur(sigma)`, which softens a shape's coverage rather than blurring
> the result, so a mist bank has no edge. Reach for both before hand-writing SkSL.

## 3. Color Temperature & The Warm/Cool Rule

> **Implemented by**: the colour options rather than a call of its own — pass complementary temperatures as `baseColor` / `shadowColor` / `bounceColor` to `Drawing.renderVolumetricSphere(...)`, and as `keyColor` / `fillColor` to `Drawing.createThreePointLighting(...)`. The defaults already obey the rule: warm key `#fff3d6` against cool fill `#8cb5db`.

> **Principle**:
> - **Direct Key Light is Warm** (Golden Sun $\approx 5000\text{K}$, `#fff5e0`) $\implies$ **Ambient Shadows are Cool** (Sky Blue $\approx 8500\text{K}$, `#3a4d66`).
> - **Direct Key Light is Cool** (Moonlight / Fluorescent $\approx 6500\text{K}$, `#d6e8ff`) $\implies$ **Ambient Shadows are Warm** (Earth/Room Bounce $\approx 3200\text{K}$, `#4a382e`).
>
> Never shade with neutral gray! Always shift the hue of the shadow toward the complementary ambient temperature.

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

> [!NOTE]
> **The numbers are the toolkit's defaults, not a cited rule.** `createThreePointLighting` ships
> `~70/30/90%` intensities and $-45^\circ$ / $+60^\circ$ angles; earlier versions of this manual
> presented them as sourced. Treat them as a starting position to tune.
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
