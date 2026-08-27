# Studio Manual 04: Cel-Shading & Facial Planes

> **Purpose**: Provides lighting vectors, facial shadow plane geometries, color tiering formulas, and atmospheric gradients for high-impact comic book coloring.

---

## 1. Directional Light Vectors & Shadow Terminators

> **Implemented by**: `Drawing.createThreePointLighting(options)` for the rig, and `Drawing.renderVolumetricSphere(...)` / `Drawing.renderVolumetricCylinder(...)` where a form can be rendered volumetrically rather than cel-shaded. Manual 07 covers the full six-zone model this section summarises.

In comic illustration, lighting is typically established by a strong **Key Light** coming from an angle (e.g. upper-left, $\approx 45^\circ$).

```
       ☀️ Key Light (Sun / Sky)
         \
          \   Top-Left Angle
           ▼
        ┌────────────────────────────────────────────────────────┐
        │  Illuminated Planes:                                   │
        │  • Forehead dome, nose bridge, near cheek apex, chin   │
        └────────────────────────────────────────────────────────┘
                                 │
           ┌─────────────────────┴─────────────────────┐
           ▼                                           ▼
┌─────────────────────────┐                 ┌─────────────────────────┐
│     Form Shadows        │                 │       Cast Shadows      │
│ • Side of nose          │                 │ • Under-chin onto neck  │
│ • Temple flat plane     │                 │ • Hair fringe on brow   │
│ • Cheek hollow          │                 │ • Bandana onto forehead │
└─────────────────────────┘                 └─────────────────────────┘
```

---

## 2. The 5 Essential Cel-Shading Planes of the 3/4 Face

> **Implemented by**: no dedicated call — the planes are polygons you fill, but their corners come from the `LoomisHead` landmarks (`head.jaw.cheekApex`, `head.temporalOval`, `head.noseWedge`), not from eyeballed coordinates.

When coloring the face, construct these discrete shadow polygon planes:

1. **The Under-Brow Shadow**:
   - A soft triangular shadow cast into the upper eye socket below the brow line.
2. **The Nose Cast Shadow**:
   - An angled triangle extending from the nose apex downward across the far cheek or upper lip.
3. **The Cheek Hollow / Mandible Plane**:
   - An angled diagonal band extending from the bottom of the cheekbone down toward the jaw angle.
4. **The Lower Lip & Chin Shadow**:
   - A dark crescent immediately below the lower lip line.
5. **The Major Neck Cast Shadow**:
   - A bold, dark shadow cast by the entire lower jaw contour across the cylindrical neck surface.

---

## 3. Tiered Color Palette Formulations

> **Implemented by**: nothing — these are data. Declare them as constants, as below. For value structure rather than hue, `Drawing.createNotanPalette(type)` returns curated tonal sets (Manual 09 §3).

Comic colorists never use a single flat color. Every surface has a **4-tier color palette**:

### A. Skin Tone Tier (Golden Mediterranean / Pirate Tone)
```js
const SKIN_TIER = {
    highlight: '#faecd8', // Sunlit forehead, nose tip, cheekbone apex
    base:      '#e8b894', // Main facial skin tone
    shadow:    '#a66847', // Warm amber-brown cel-shadow planes
    deep:      '#753c24', // Ambient occlusion under jaw & in ear cavity
    lipRose:   '#b84848'  // Upper lip & cheek blush
};
```

### B. Red Hair Tone Tier (Fiery Copper-Auburn)
```js
const HAIR_TIER = {
    sunlit:    '#f5a458', // Blazing gold rim highlights on windward edges
    highlight: '#e88b48', // Warm orange-amber lock surfaces
    base:      '#c85a2b', // Rich saturated copper-red body
    shadow:    '#7d2d14', // Deep chestnut shadow crevices
    inkDeep:   '#380f05'  // Deepest root undercuts
};
```

### C. Bandana & Coat Tier (Weathered Navy / Charcoal)
```js
const CLOTHING_TIER = {
    bandanaHighlight: '#4e6b8a',
    bandanaBase:      '#2f4255',
    bandanaShadow:    '#1a2633',
    
    shirtBase:        '#f4ecd8', // Creamy parchment white
    shirtShadow:      '#c5baa4', // Warm gray-tan fold shadows
    
    coatBase:         '#1c242c', // Dark slate charcoal
    coatShadow:       '#0c1015'  // Near black
};
```

---

## 4. Atmospheric Sky & Background Rigging

> **Implemented by**: `ctx.createLinearGradient(...)` for the sky ramp, and `Drawing.createAtmosphericCloudShader(...)` for vapour. The cloud silhouettes themselves are hand-drawn Bézier masses — there is no cloud primitive.

### Sky Gradient
```js
function drawAtmosphericSky(ctx, width, height) {
    const skyGrad = ctx.createLinearGradient(0, 0, 0, height);
    skyGrad.addColorStop(0.0, '#4a7c9d'); // Deeper zenith blue
    skyGrad.addColorStop(0.5, '#6c9ebf');
    skyGrad.addColorStop(1.0, '#a2c8dc'); // Pale horizon blue

    ctx.fillStyle = skyGrad;
    ctx.fillRect(0, 0, width, height);
}
```

### Billowy Comic Cloud Masses
Clouds in comics are constructed using overlapping circular arcs with a **flat, shaded base plane**:
```js
function drawComicCloud(ctx, cx, cy, scale = 1.0) {
    ctx.save();
    ctx.translate(cx, cy);
    ctx.scale(scale, scale);

    // 1. Cloud Shadow Underside (Flat warm gray-blue base)
    ctx.beginPath();
    ctx.moveTo(-120, 20);
    ctx.lineTo(120, 20);
    ctx.quadraticCurveTo(0, 40, -120, 20);
    ctx.fillStyle = '#b6c4cf';
    ctx.fill();

    // 2. Billowy Cloud Puffs (White with crisp comic outlines)
    ctx.beginPath();
    ctx.arc(-80, 0, 45, 0, Math.PI * 2);
    ctx.arc(-20, -30, 60, 0, Math.PI * 2);
    ctx.arc(50, -20, 50, 0, Math.PI * 2);
    ctx.arc(90, 5, 35, 0, Math.PI * 2);
    ctx.closePath();

    ctx.fillStyle = '#f8f6f0';
    ctx.fill();
    ctx.strokeStyle = '#2b3d4d';
    ctx.lineWidth = 2.0;
    ctx.stroke();

    ctx.restore();
}
```

---

## 5. Polson SDK Shader & Material Recipes

> **Implemented by**: `Drawing.createHalftoneDotShader(options)`, `Drawing.createRopeFiberShader(fx, fy, octaves, seed)`, and `Drawing.createAtmosphericCloudShader(fx, fy, octaves, seed)` — three presets over `Skia.Shader`. Reach for these before hand-writing SkSL.

To achieve professional comic texture and lighting that elevates drawings far beyond flat vector shapes, use these native Polson SDK shader pipelines:

### A. Classic Comic Half-Tone / Ben-Day Dot Shading (SkSL)
In authentic comic printing, cel-shadows blend into skin and cloth via half-tone dots:
`Drawing.createHalftoneDotShader({ dotSpacing, shadowColor, resolution })` → `SKShader` returns this shader ready to use — reach for it before hand-writing SkSL:

```js
const halftone = Drawing.createHalftoneDotShader({ dotSpacing: 6.5, shadowColor: '#1c2733' });
ctx.save();
ctx.fillStyle = halftone;
ctx.fill(jawShadowPath);
ctx.restore();
```

Drop `dotSpacing` toward `4` for fine 1960s Ben-Day; raise it toward `10` for coarse pulp newsprint. Hand-write the SkSL below only when you need a dot profile the toolkit does not offer, such as elliptical or angled dots.

```js
const halfToneSkSL = `
    uniform float2 u_resolution;
    uniform float4 u_shadowColor;
    uniform float u_dotSpacing;

    half4 main(float2 coord) {
        float2 pos = mod(coord, u_dotSpacing) - (u_dotSpacing * 0.5);
        float dist = length(pos);
        float radius = u_dotSpacing * 0.38;
        float dot = smoothstep(radius + 0.4, radius - 0.4, dist);
        return u_shadowColor * dot;
    }
`;

const halfToneShader = Skia.Shader.sksl(halfToneSkSL, {
    u_resolution: [900, 750],
    u_shadowColor: [0.45, 0.22, 0.12, 0.65],
    u_dotSpacing: 6.5
});

// Fill shadow plane with Ben-Day dots
ctx.save();
ctx.fillStyle = halfToneShader;
ctx.fill(jawShadowPath);
ctx.restore();
```

### B. Fibrous Hemp Rope & Wood Mast Texture (`perlinNoiseTurbulence`)
Give ship rigging authentic twisted cordage fiber rather than flat plastic tubes:
`Drawing.createRopeFiberShader(frequencyX, frequencyY, octaves, seed)` → `SKShader` is exactly this turbulence preset — the stretched-Y noise that reads as twisted cordage. Its defaults (`0.08, 0.40, 3, 42`) are the rope values; raise `frequencyY` for tighter twist.

```js
const fiber = Drawing.createRopeFiberShader();
ctx.save();
ctx.strokeStyle = baseColor;          // solid base pass
ctx.lineWidth = width;
ctx.stroke(shroudPath);
ctx.globalCompositeOperation = 'overlay';   // fibre pass rides on top
ctx.strokeStyle = fiber;
ctx.lineWidth = width - 2;
ctx.stroke(shroudPath);
ctx.restore();
```

The two-pass structure matters: the base stroke carries the local colour, the overlay pass carries only texture. A single pass with the shader alone loses the rope's value.

### C. Organic Cloud Volume (`perlinNoiseFractal`)
Layer fractal turbulence over cloud puffs for atmospheric sea-air depth:
`Drawing.createAtmosphericCloudShader(frequencyX, frequencyY, octaves, seed)` → `SKShader` is the isotropic fractal preset for air and vapour. Its defaults (`0.015, 0.015, 4, 101`) are the sea-air values.

```js
const cloudNoise = Drawing.createAtmosphericCloudShader();
ctx.save();
ctx.globalCompositeOperation = 'soft-light';
ctx.fillStyle = cloudNoise;
ctx.fillRect(0, 0, width, height * 0.45);
ctx.restore();
```

`soft-light` is the operative choice — the noise must modulate the sky already painted underneath, not replace it.

### D. Layer Blending for High-Impact Comic Lighting
- **`ctx.globalCompositeOperation = 'multiply'`**: Apply dark amber shadow glazes over skin without obscuring black ink hatching.
- **`ctx.globalCompositeOperation = 'overlay'`**: Paint golden-orange sunlight rim highlights along the windward hair curls and nose ridge.
- **`ctx.globalCompositeOperation = 'screen'`**: Soften background rigging into atmospheric sky haze.


---

## 6. Constructing It: A Runnable Background Plate

Palette as data, sky as a ramp, and the three shader presets doing the work that raw SkSL was doing before.

```javascript
// Coastal background plate: sky ramp → vapour → rope → Ben-Day shadow plane.
const canvas = createCanvas(760, 560);
const ctx = canvas.getContext('2d');

// §3 — Palettes are data. Declare the tier, then reference it.
const SKIN = { highlight: '#faecd8', base: '#e8c9a0', shadow: '#b07d52', core: '#7d4f2e' };

// §4 — Sky ramp first.
const sky = ctx.createLinearGradient(0, 0, 0, 560);
sky.addColorStop(0, '#2f6fa8');
sky.addColorStop(1, '#bcd9ea');
ctx.fillStyle = sky;
ctx.fillRect(0, 0, 760, 560);

// §5C — Vapour modulates the sky beneath it; 'soft-light' is the operative choice.
const clouds = Drawing.createAtmosphericCloudShader();
ctx.save();
ctx.globalCompositeOperation = 'soft-light';
ctx.fillStyle = clouds;
ctx.fillRect(0, 0, 760, 250);
ctx.restore();

// §5B — Rope in two passes: solid base carries colour, overlay carries fibre.
const fiber = Drawing.createRopeFiberShader();
ctx.save();
ctx.strokeStyle = '#8a6a44';
ctx.lineWidth = 14;
ctx.beginPath(); ctx.moveTo(120, 560); ctx.lineTo(250, 40); ctx.stroke();
ctx.globalCompositeOperation = 'overlay';
ctx.strokeStyle = fiber;
ctx.lineWidth = 12;
ctx.beginPath(); ctx.moveTo(120, 560); ctx.lineTo(250, 40); ctx.stroke();
ctx.restore();

// §5A — Ben-Day dots break a shadow plane where a gradient would go muddy.
const halftone = Drawing.createHalftoneDotShader({ dotSpacing: 6.5, shadowColor: SKIN.core });
ctx.save();
ctx.fillStyle = halftone;
ctx.beginPath();
ctx.moveTo(430, 300);
ctx.lineTo(690, 260);
ctx.lineTo(660, 470);
ctx.lineTo(440, 440);
ctx.closePath();
ctx.fill();
ctx.restore();

canvas;
```
