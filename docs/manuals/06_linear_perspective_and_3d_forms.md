# Studio Manual 06: Linear Perspective & 3D Form Construction

> **Source Reference**: *Imaginative Drawing*, Chapter 2: "Perspective" (`reference/books/chapter2_perspective.pdf`)  
> **Purpose**: Translates linear perspective theory (horizon lines, vanishing points, 3D box projection, cylinder tangent ellipses, diagonal plane subdivision, and convergence testing) into algorithmic JavaScript Canvas2D / Skia code.

---

## 1. Linear Perspective Mathematics & Grid Setup

> **Core Insight from the Book**:
> In 2-point perspective, the **Station Point ($SP$)** is the camera position, $d$ is the focal distance, and $\theta$ is the camera rotation angle relative to the primary plane.
>
> The Left and Right Vanishing Points are mathematically fixed along the **Horizon Line ($HL_y$)**:
> $$VP_L = (CV_x - d \cot \theta,\; HL_y)$$
> $$VP_R = (CV_x + d \tan \theta,\; HL_y)$$
>
> - **Wide-Angle Lens ($d \approx 400\text{–}600\text{px}$)**: Vanishing points are close together, creating dynamic, exaggerated foreshortening.
> - **Telephoto Lens ($d \approx 1400\text{–}2000\text{px}$)**: Vanishing points are far off-canvas, compressing depth and flattening planes.
> - **Normal Human Vision ($d \approx 800\text{–}1000\text{px}$)**: $60^\circ$ standard cone of vision.

```
       VPL <─────────────────────── CV (Center of Vision) ───────────────────────> VPR
        │                                    │                                      │
   ─────●────────────────────────────────────┼──────────────────────────────────────●───── Horizon Line
         \                                   │                                     /
          \                                  │                                    /
           \                                 │                                   /
            \                                │                                  /
             \───────────────┐               │               ┌─────────────────/
              \    Left Face │               │               │   Right Face   /
               \             │               │               │               /
                \            ▼               ▼               ▼              /
                 \           ●───────────────●───────────────●             /
                  \          │          Front Corner         │            /
```

---

## 2. 3D Bounding Box Projection

A 3D perspective box is defined by:
1. **Front-Bottom Anchor Point** $(A_x, A_y)$.
2. **Height** $H$ (vertical upward along the $Y$-axis).
3. **Width** $W$ (receding along the Left Vanishing Point ray $VP_L$).
4. **Depth** $D$ (receding along the Right Vanishing Point ray $VP_R$).

### The 8 Projected 3D Vertices:
- **Bottom 4 Vertices**:
  - $V_0 = \text{Anchor}$ (Front-Bottom)
  - $V_1 = \text{Anchor} + \frac{W}{\|VP_L - \text{Anchor}\|} (VP_L - \text{Anchor})$ (Left-Bottom)
  - $V_2 = \text{Anchor} + \frac{D}{\|VP_R - \text{Anchor}\|} (VP_R - \text{Anchor})$ (Right-Bottom)
  - $V_3 = \text{Intersection of } (V_1 \rightarrow VP_R) \text{ and } (V_2 \rightarrow VP_L)$ (Back-Bottom)
- **Top 4 Vertices**:
  - $V_4 = V_0 - (0, H)$ (Front-Top)
  - $V_5 = \text{Intersection of } (V_4 \rightarrow VP_L) \text{ and vertical from } V_1$ (Left-Top)
  - $V_6 = \text{Intersection of } (V_4 \rightarrow VP_R) \text{ and vertical from } V_2$ (Right-Top)
  - $V_7 = \text{Intersection of } (V_5 \rightarrow VP_R) \text{ and } (V_6 \rightarrow VP_L)$ (Back-Top)

### The 3 Visible Lighting Planes:
1. **Top Plane ($V_4, V_5, V_7, V_6$)**: Sunlit Highlight.
2. **Left Plane ($V_0, V_1, V_5, V_4$)**: Midtone Flat.
3. **Right Plane ($V_0, V_2, V_6, V_4$)**: Core Shadow.

---

## 3. Perspective Cylinders & Tangent Ellipses

> **Core Insight from the Book (Exercise 2.6, Page 246)**:
> An accurate perspective cylinder is constructed by inscribing perspective circles (ellipses) inside the top and bottom square faces of a bounding perspective box.

1. **Top Ellipse**: Fits inside quad $(V_4, V_5, V_7, V_6)$.
2. **Bottom Ellipse**: Fits inside quad $(V_0, V_1, V_3, V_2)$.
3. **Contour Silhouettes**: Connect the outer tangent extremes of the top and bottom ellipses with straight vertical lines.

---

## 4. Perspective Division with Diagonals

> **Core Insight from the Book (Page 195)**:
> In perspective, equal spatial increments (e.g. windows along a building, floor tiles, staircase steps) appear progressively compressed.
> To find the exact perspective center of any 4-corner quad $[P_0, P_1, P_2, P_3]$:
> $$\text{Center} = \text{Intersection of diagonal } (P_0 \rightarrow P_2) \text{ and diagonal } (P_1 \rightarrow P_3)$$

---

## 5. Convergence Verification (Exercise 2.5)

To verify that drawn line segments $[(A_1, B_1), (A_2, B_2), \dots]$ correctly obey perspective:
1. Compute the angular slope of each line: $\theta_i = \text{atan2}(B_{iy} - A_{iy}, B_{ix} - A_{ix})$.
2. Compute the ideal angle from $A_i$ to the true vanishing point $VP$: $\hat{\theta}_i = \text{atan2}(VP_y - A_{iy}, VP_x - A_{ix})$.
3. Angular Error $= |\theta_i - \hat{\theta}_i|$. If $\text{Error} \le 5^\circ$, the line passes perspective QA.
