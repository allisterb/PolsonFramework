# Studio Manual 09: Compositional Armatures, Value Hierarchy & Visual Emphasis

> **Source Reference**: *Imaginative Drawing*, Chapter 5: "Composition" (`reference/books/chapter5_composition.pdf`)  
> **Purpose**: Translates visual design theory (classical geometric armatures, focal emphasis rules, Notan value structures, cinematic vignetting, and the 70-20-10 proportional design law) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Classical Geometric Armatures

> **Core Insight from the Book (Page 580 & 600)**:
> Great compositions are anchored on underlying geometric armatures rather than random placement:

### A. Rule of Thirds
Divides canvas into a $3 \times 3$ grid with 4 primary intersection **Power Points**:
- $P_1 = (W/3, H/3)$, $P_2 = (2W/3, H/3)$, $P_3 = (W/3, 2H/3)$, $P_4 = (2W/3, 2H/3)$.
- Place key focal landmarks (e.g. eyes, vanishing points, horizon lines, dominant character masses) along these axes.

### B. Golden Ratio & Golden Spiral ($\Phi \approx 1.618$)
- Divides canvas by the reciprocal golden ratio $\frac{1}{\Phi} \approx 0.618034$.
- Constructs logarithmic logarithmic spiral coils drawing the viewer's gaze toward the golden eye focal node.

### C. Dynamic Symmetry Harmonic Armature (14 Lines)
- Connects the 4 corner diagonals with perpendicular reciprocal diagonals and the central diamond.
- Any line or form aligned with these 14 harmonic angles immediately resonates with classical structural unity.

---

## 2. Visual Emphasis & The 4 Hierarchical Tools

> **Core Insight from the Book (Page 580–595)**:
> To guide the viewer's eye through a scene:
>
> 1. **Contrast of Value**: The point of highest local contrast (purest dark next to purest light) receives priority.
> 2. **Leading Lines**: Directional diagonals, architecture edges, or cast shadow rays that converge upon the hero subject.
> 3. **Framing & Vignetting**: Dark peripheral vignetting or foreground silhouettes that lock the viewer inside the visual container.
> 4. **Isolation / Negative Space**: Surrounding the focal hero with clean breathing room to elevate readability.

---

## 3. Notan Value Structures

> **Core Insight from the Book (Page 618)**:
> Before adding detailed color, establish a rock-solid **Notan value hierarchy**:
>
> - **2-Value Binary Notan**: 50% Black, 50% White. Tests whether the graphic silhouette reads at a glance.
> - **3-Value Classic Notan**:
>   - **70% Dominant** (Light or Midtone)
>   - **20% Secondary** (Opposing Tone)
>   - **10% Accent** (Deepest Dark or Blown Highlight)
> - **High-Key**: Scene dominated by values 1–4 (soft, ethereal, bright).
> - **Low-Key**: Scene dominated by values 7–10 (dramatic, moody, noir, mystery).

---

## 4. The 70-20-10 Proportional Law ("Big, Medium, Small")

> **Core Insight from the Book (Page 627)**:
> Visual appeal demands varied scale:
> - **70% Big / Dominant**: The major backdrop, environment mass, or sky.
> - **20% Medium / Secondary**: The character figure, vehicle, or architectural structure.
> - **10% Small / Detail**: Micro-flourishes, specular highlights, textures, and facial features.
