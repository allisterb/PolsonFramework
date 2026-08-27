# Studio Manual 03: Comic Inking & Feathering

> **Purpose**: Provides line weight hierarchy standards, tapered stroke algorithms, directional cross-hatching recipes, and graphic black ink placement rules for authentic comic book illustration.

---

## 1. Line Weight Hierarchy (The Three Inking Tiers)

Comic inking derives its punch and depth from **stroke weight contrast**:

| Tier | Line Width | Purpose | Applied To |
|---|---|---|---|
| **Tier 1 (Outer Silhouette)** | **3.5px – 5.0px** | Defines the outer spatial boundary; grounds the figure against the background | Outer jawline, coat collar, outer hair mass contour, bandana underside |
| **Tier 2 (Internal Contours)** | **2.0px – 3.0px** | Defines structural forms and major overlapping planes | Eyelid creases, nose bridge, lip slit, hair lock boundaries, collar seams |
| **Tier 3 (Details & Hatching)** | **0.8px – 1.5px** | Adds volume, surface texture, and shadow transition | Eyelashes, iris fibers, neck tendon hatching, shirt fold wrinkles, rigging cord texture |

---

## 2. Inking Rules of Thumb

1. **The Light vs Gravity Rule**:
   - Lines facing the **light source** (top/left) should be **thin or broken** ($1.0\text{px} - 1.5\text{px}$).
   - Lines facing **away from light or affected by gravity** (underside of jaw, bottom of hair curls, coat hem) should be **heavy and thick** ($3.5\text{px} - 5.0\text{px}$).
2. **Line Tapering**:
   - Comic lines never end in flat, blunt cylindrical cutoffs. Every line starts at a point, swells in the middle, and tapers to a fine point.

---

## 3. Algorithmic Tapered Inking in Canvas2D

```javascript
/**
 * Strokes a curved line with smooth taper at both start and end.
 */
function drawTaperedStroke(ctx, start, cp1, cp2, end, maxThickness, inkColor = '#0a0a0c') {
    // Sample points along the cubic Bezier curve
    const steps = 24;
    const points = [];
    
    for (let i = 0; i <= steps; i++) {
        const t = i / steps;
        const it = 1 - t;
        
        // Cubic Bezier interpolation
        const x = it*it*it*start.x + 3*it*it*t*cp1.x + 3*it*t*t*cp2.x + t*t*t*end.x;
        const y = it*it*it*start.y + 3*it*it*t*cp1.y + 3*it*t*t*cp2.y + t*t*t*end.y;
        
        // Tangent derivative for normal vector
        const dx = 3*it*it*(cp1.x - start.x) + 6*it*t*(cp2.x - cp1.x) + 3*t*t*(end.x - cp2.x);
        const dy = 3*it*it*(cp1.y - start.y) + 6*it*t*(cp2.y - cp1.y) + 3*t*t*(end.y - cp2.y);
        const len = Math.sqrt(dx*dx + dy*dy) || 1;
        const nx = -dy / len;
        const ny = dx / len;
        
        // Sine envelope for thickness: 0 at start -> max in middle -> 0 at end
        const thickness = maxThickness * Math.sin(t * Math.PI);
        points.push({ x, y, nx, ny, thickness });
    }

    // Construct tapered polygon
    ctx.beginPath();
    // Forward pass along left side
    ctx.moveTo(points[0].x, points[0].y);
    for (let i = 0; i < points.length; i++) {
        const p = points[i];
        ctx.lineTo(p.x + p.nx * (p.thickness * 0.5), p.y + p.ny * (p.thickness * 0.5));
    }
    // Backward pass along right side
    for (let i = points.length - 1; i >= 0; i--) {
        const p = points[i];
        ctx.lineTo(p.x - p.nx * (p.thickness * 0.5), p.y - p.ny * (p.thickness * 0.5));
    }
    ctx.closePath();
    ctx.fillStyle = inkColor;
    ctx.fill();
}
```

---

## 4. Directional Cross-Hatching & Feathering Recipes

### Linear Feathering (Shadow Terminator Transitions)
Feathering uses parallel tapered lines extending from solid black shadow masses into the illuminated zones:

```javascript
/**
 * Draws directional hatching lines along a curved form (e.g. neck, throat, collar).
 */
function drawFeatheringHatch(ctx, origin, directionAngleDeg, count, length, spacing, strokeColor = '#0a0a0c') {
    const rad = (directionAngleDeg * Math.PI) / 180;
    const dx = Math.cos(rad);
    const dy = Math.sin(rad);
    
    // Perpendicular step direction
    const px = -dy;
    const py = dx;

    ctx.save();
    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = 1.2;
    ctx.lineCap = 'round';

    for (let i = 0; i < count; i++) {
        const sx = origin.x + px * (i * spacing);
        const sy = origin.y + py * (i * spacing);
        
        // Slight length variation for natural organic feel
        const lenVar = length * (0.8 + 0.4 * Math.sin(i * 1.5));
        const ex = sx + dx * lenVar;
        const ey = sy + dy * lenVar;

        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(ex, ey);
        ctx.stroke();
    }
    ctx.restore();
}
```

---

## 5. Solid Black Ink Placement (Graphic Chiaroscuro)

A hallmark of professional comic art is the confident use of **solid black ink shapes** (`#0a0a0c`):
- **Inside the mouth cavity** (behind teeth).
- **Under the jaw and chin** (cast shadow onto the throat).
- **In the deepest crevices between hair clumps**.
- **In the fold troughs of the coat and collar**.
