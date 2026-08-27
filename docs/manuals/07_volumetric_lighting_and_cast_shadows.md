# Studio Manual 07: Volumetric Lighting, Cast Shadows & Studio Setups

> **Source Reference**: *Imaginative Drawing*, Chapter 3: "Light" (`reference/books/chapter3_light.pdf`)  
> **Purpose**: Translates volumetric lighting theory (the 6 tonal light zones, Lambertian diffuse falloff, cast shadow geometric projection, contact occlusion, and 3-point studio lighting) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Anatomy of Light on Form (The 6 Tonal Zones)

> **Implemented by**: `Drawing.renderVolumetricSphere(ctx, cx, cy, radius, lightDirection, options)` and `Drawing.renderVolumetricCylinder(ctx, x, y, width, height, lightDirection, options)` render all six zones in one pass; `Drawing.createVolumetricSphereShader(options)` returns the equivalent as an SkSL shader for filling arbitrary paths.

> **Core Insight from the Book (Page 274 & 284)**:
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

> **Implemented by**: `Drawing.projectCastShadow(lightSource, groundY, verticesOrBounds, options)` → `{ shadowPolygon, groundY, groundDepth, shadowColor, opacity, penumbraBlur }`, then `Drawing.drawCastShadow(ctx, shadow, options)`.
>
> **Why `groundDepth` exists.** Follow the construction below literally and every projected vertex lands on the ground line — $L_{\text{ground}}$ and $B_i$ both sit at `groundY`, so that second ray *is* the ground line. That is not an error in the maths: a ground plane seen edge-on in a 2D elevation has no thickness, and its shadow really is a segment. The footprint only becomes a fillable shape once the ground is given an apparent depth, which no amount of 2D projection can supply. `options.groundDepth` is that missing dimension; it defaults to about a fifth of the contact footprint's width. The light ray still fixes the shadow's **length and direction** — `groundDepth` only adds the recession.
>
> `Drawing.drawCastShadow(...)` throws on a zero-area polygon rather than quietly drawing nothing, so a shadow that fails to appear reports itself.

> **Core Insight from the Book (Page 274 & 289)**:
> To project a realistic cast shadow on a ground plane $Y = \text{groundY}$:
>
> 1. Identify the 2D light source position $L = (L_x, L_y)$ and its ground projection $L_{\text{ground}} = (L_x, \text{groundY})$.
> 2. For each top vertex $V_i = (x_i, y_i)$ of the object:
>    - Project a ray from the light source $L$ through $V_i$.
>    - Project a ray from $L_{\text{ground}}$ through the corresponding base vertex $B_i = (x_i, \text{groundY})$.
>    - The intersection point $S_i$ is the shadow vertex on the ground plane!
> 3. Connect shadow vertices $S_i$ to base points $B_i$ to form the **Cast Shadow Footprint Polygon**.

---

## 3. Color Temperature & The Warm/Cool Rule

> **Implemented by**: the colour options rather than a call of its own — pass complementary temperatures as `baseColor` / `shadowColor` / `bounceColor` to `Drawing.renderVolumetricSphere(...)`, and as `keyColor` / `fillColor` to `Drawing.createThreePointLighting(...)`. The defaults already obey the rule: warm key `#fff3d6` against cool fill `#8cb5db`.

> **Core Insight from the Book (Page 300)**:
> - **Direct Key Light is Warm** (Golden Sun $\approx 5000\text{K}$, `#fff5e0`) $\implies$ **Ambient Shadows are Cool** (Sky Blue $\approx 8500\text{K}$, `#3a4d66`).
> - **Direct Key Light is Cool** (Moonlight / Fluorescent $\approx 6500\text{K}$, `#d6e8ff`) $\implies$ **Ambient Shadows are Warm** (Earth/Room Bounce $\approx 3200\text{K}$, `#4a382e`).
>
> Never shade with neutral gray! Always shift the hue of the shadow toward the complementary ambient temperature.

---

## 4. Three-Point Studio Lighting (Key, Fill, Rim/Kicker)

> **Implemented by**: `Drawing.createThreePointLighting(options)` → `{ keyLight, fillLight, rimLight }`, each `{ angleDeg, color, intensity }`. Feed `keyLight.angleDeg` to the volumetric renderers as the light direction, and `rimLight.angleDeg` to `Drawing.drawRimLight(ctx, bounds, angleDeg, color, thickness)`.

> **Core Insight from the Book (Page 323)**:
> The standard lighting setup used in portrait painting, cinematography, and comics:
>
> 1. **Key Light (~70% intensity, primary angle e.g. $-45^\circ$)**: Main illumination defining the primary planar structures and cast shadows.
> 2. **Fill Light (~30% intensity, opposing angle e.g. $+60^\circ$, cool temperature)**: Soft ambient illumination that opens up deep shadows so details remain readable.
> 3. **Rim / Kicker Light (~90% intensity, backlight silhouette edge)**: High-contrast grazing edge light that cuts the subject out from the background.

---

## 5. Symbol → SDK Parameter Map

| Book concept | SDK parameter or field | Notes |
| --- | --- | --- |
| $\mathbf{L}$ (light vector) | `lightDirection` as `{ x, y }` | Points *from* the surface *toward* the light. |
| Highlight (zone 1) | `options.highlightColor` | Take the key light's temperature. |
| Halftone (zone 2) | `options.baseColor` | The local colour of the form. |
| Core shadow (zone 4) | `options.shadowColor` | Complementary temperature to the key (§3). |
| Reflected light (zone 5) | `options.bounceColor` | The environment's colour, not black. |
| Umbra / penumbra (zone 6) | `options.opacity`, `options.penumbraBlur` | Larger blur = larger apparent light source. |
| $L = (L_x, L_y)$ | `lightSource` as `{ x, y }` | Argument 1 of `projectCastShadow`. |
| $Y = \text{groundY}$ | `groundY` | Argument 2; also returned on the shadow object. |
| $S_i$ footprint | `shadow.shadowPolygon` | The projected polygon of §2: contact edge, then the projected edge lowered by `groundDepth`. |
| (not in the book) | `options.groundDepth` | Apparent depth of the ground plane. Defaults to ~20% of the contact width. See §2. |
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
// kicker. Give it a silhouette point list, not a bounds rect: against a rect it can
// only stroke the rectangle's edge, which reads as a stray line on a curved form.

canvas;
```
