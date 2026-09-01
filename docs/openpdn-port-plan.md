# Porting OpenPDN's raster core — plan

**Status: proposed, nothing written.** Assessment done 2026-09-01 against
`reference/projects/openpdn-master/port`, which is scanned and ledgered but **not built and not
copied from**.

## What this is for

Polson has a low-level canvas API (`ctx`, paths, brushes, shaders) and a high-level toolkit layer
(`Drawing`, `Logo`, `LogoType`). It has nothing in between: no layers, no selections, no named
effects. An agent composites by hand with `save`/`restore`/`globalCompositeOperation` and cannot
re-order, hide or re-opacity a pass once drawn — which is why a critique that wants to lift the
values on one stage has to redraw it.

OpenPDN's port has exactly that middle layer, and it is unusually portable.

## Why it is portable

`OpenPDN.Core` has **zero package references** — no SkiaSharp, no Avalonia, pure BCL, 6,079 lines.
The effect contract is three lines:

```csharp
protected abstract void RenderCore(Surface src, Surface dst, PixelRect roi);
public void Apply(Surface surface, Selection? selection = null)
```

Everything worth having is written against four small types: `ColorBgra` (84 lines), `Surface` (49),
`PixelRect` (20), `Selection` (577).

**Licence: MIT, clean.** Upstream Paint.NET 3.36.7 is MIT with three exceptions — logo artwork,
"resource assets" (`.RESX`/`.PNG`/`.RESOURCES` by extension), and GPC. The port contains **none** of
the three: no image or resource files at all, and `PORT_STATUS.md` records GPC as deliberately not
ported, with selection booleans done on the raster mask instead. MIT into AGPL-3.0 is one-way
compatible. Attribution must be retained. See the ledger row in `reference/README.md`.

## What to take, and what not to

| Take | Why |
| :--- | :--- |
| `ColorBgra`, `Surface`, `PixelRect` | The interchange ABI every effect is written against. 153 lines. |
| `Effect` base | Selection-aware, `Parallel.For`, restores pixels outside the mask so effects can read neighbours. 73 lines. |
| `Selection` | `SetRectangle`, `SetEllipse`, `SetPolygon(antialias)`, `SetByColorFlood`, `Feather`, `Grow`, `Shrink`, `Invert`, `Combine`. **Polson has no mask primitive at all** — clipping is path-only, with no feather, grow or per-pixel combine. |
| `Effects/`, `Adjustments/` | Oil painting, ink sketch, pencil sketch, emboss, relief, edge detect, glow, soften portrait, radial and motion blur, frosted glass, twist, bulge, polar inversion, clouds, add/reduce noise; brightness/contrast, curves, hue/saturation, levels, RGB histogram. Skia gives none of these. |
| `Layer` / `Document` **model only** | Named layers with visibility, opacity and blend mode. |

| Leave | Why |
| :--- | :--- |
| `Compositor` | A managed per-pixel Porter-Duff loop. Skia does all eight blend modes natively and Polson already draws through `SKCanvas`; take the model, composite with `SaveLayer` + `SKBlendMode`. |
| Most of `Tools/` | `ITool` is `OnPointerDown/Move/Up`. Strip the mouse from `LassoSelectTool` and two lines survive — the rest is drag state, preview throttling and history. `MagicWandTool` has no algorithm in it at all; the flood is `Selection.SetByColorFlood`. `EyedropperTool` is `getPixel`. |
| `History/` | Polson's undo is the script; every execution is saved and re-runnable. |
| `OpenPDN.Formats`, `OpenPDN.App` | Skia encode/decode and Avalonia UI. Both already covered or irrelevant. |

### The rule that decides it

**Does the tool read the canvas, or only write to it?** Write-only tools — lasso, freeform shape,
pencil, brush, eraser, shapes, gradient — convert a drag into a path, and an agent *starts* with the
path. Read-then-write tools — flood fill, magic wand, clone stamp — compute a shape from pixel content
the agent has not got, and are the ones worth having.

### Tool *names* are still worth keeping

An agent has read everything ever written about digital painting. "Lasso the sky, feather 20px, adjust
levels on the selection" is a sentence it can decompose; "build a `CanvasPath`, `ctx.clip`, apply a
colour matrix" is a sentence only our documentation teaches. This session watched an agent reach for
`Skia.RuntimeEffect.make()` because CanvasKit's vocabulary was in its weights and ours was not.

So: **take the artist's nouns, reject the mouse-shaped implementations.** `Selection.lasso(path)`
should exist and should simply wrap geometry the agent already computed — the name is the value, not
the algorithm.

## The one real technical gotcha

`ColorBgra` is `[B, G, R, A]` in memory. Polson uses `SKColorType.Rgba8888` everywhere (five sites in
`Polson.Drawing.Skia`). The bridge is therefore **not** a memcpy — either swizzle on crossing, or build
the interop bitmap as `Bgra8888`. Settle this before copying anything; it decides whether the boundary
is free or a per-pixel pass in each direction.

Note also that effects run compiled and parallel behind one JS call, so the sandbox statement cap does
not apply — the same deal as `bitmap.diff`. This is not the per-pixel-loop-in-the-interpreter case the
SDK warns about.

## Slice 1 — prove the boundary

~800 lines, no dependencies, one vertical cut through every layer.

1. **Port** `ColorBgra`, `Surface`, `PixelRect`, `Effect`, `Selection` into a new
   `src/Polson.Drawing.Raster` (name deliberately not `OpenPDN`), retaining the MIT notice and a
   header naming the origin and the ledger row.
2. **Bridge** `SkiaBitmapWrapper` ↔ `Surface`, resolving the channel order above. One test asserting
   a round-trip is byte-identical.
3. **One effect** — `PencilSketchEffect` or `OilPaintingEffect`, both visibly unavailable today.
4. **One selection** — `Selection.SetByColorFlood`, because a flood fill cannot be written in JS at
   all and it proves the mask round-trip at the same time.
5. **JS surface**: `bitmap.applyEffect(effect, selection?)` and `Selection.byColor(bitmap, x, y, tol)`.
6. **Measure it** with what already exists: `bitmap.diff` before and after, and time the round-trip
   against a native Skia filter for comparison.

**The decision gate:** if the swizzle plus round-trip costs more than the effect itself on a
900 × 700 frame, the boundary is wrong and the whole approach needs rethinking before the remaining
twenty effects are worth porting.

## Slice 2 — if slice 1 holds

The rest of `Effects/` and `Adjustments/`, then the `Layer`/`Document` model over `SKCanvas`
compositing. A manual would follow — provisionally Manual 19, *Layers, Selections and Effects* — and
on the evidence of this session it will find its own defects while being written.

## Open questions

- Does a `Layer` model belong in the SDK, or is it a workflow convention over `outFile` naming? The
  painting workflow's stages are already conceptually layers.
- Should `Selection` replace path clipping in the JS API, or sit beside it? Feather and grow have no
  path equivalent, but a mask is resolution-bound and a path is not.
- Effects are CPU and single-resolution. Does an effect belong on a `SnapPaper` at all, or only after
  rasterisation? (Manual 14 §1: rasterising is a one-way door.)
