# Studio Manual 07: Volumetric Lighting, Cast Shadows & Studio Setups

> **Source Reference**: *Imaginative Drawing*, Chapter 3: "Light" (`reference/books/chapter3_light.pdf`)  
> **Purpose**: Translates volumetric lighting theory (the 6 tonal light zones, Lambertian diffuse falloff, cast shadow geometric projection, contact occlusion, and 3-point studio lighting) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Anatomy of Light on Form (The 6 Tonal Zones)

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

> **Core Insight from the Book (Page 300)**:
> - **Direct Key Light is Warm** (Golden Sun $\approx 5000\text{K}$, `#fff5e0`) $\implies$ **Ambient Shadows are Cool** (Sky Blue $\approx 8500\text{K}$, `#3a4d66`).
> - **Direct Key Light is Cool** (Moonlight / Fluorescent $\approx 6500\text{K}$, `#d6e8ff`) $\implies$ **Ambient Shadows are Warm** (Earth/Room Bounce $\approx 3200\text{K}$, `#4a382e`).
>
> Never shade with neutral gray! Always shift the hue of the shadow toward the complementary ambient temperature.

---

## 4. Three-Point Studio Lighting (Key, Fill, Rim/Kicker)

> **Core Insight from the Book (Page 323)**:
> The standard lighting setup used in portrait painting, cinematography, and comics:
>
> 1. **Key Light (~70% intensity, primary angle e.g. $-45^\circ$)**: Main illumination defining the primary planar structures and cast shadows.
> 2. **Fill Light (~30% intensity, opposing angle e.g. $+60^\circ$, cool temperature)**: Soft ambient illumination that opens up deep shadows so details remain readable.
> 3. **Rim / Kicker Light (~90% intensity, backlight silhouette edge)**: High-contrast grazing edge light that cuts the subject out from the background.
