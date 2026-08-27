# Studio Manual 11: Typography, Font Pairing & Logotypes

> **Credits & Theoretical Foundation**: Synthesized from Robin Williams' *The Non-Designer's Design Book* (CRAP principles, type categories, and contrast rules) and Doyald Young's *Fonts and Logos* (letterform optical mechanics, kerning matrices, stroke weight rules, and logotype lockups).
> **Purpose**: Provides rigorous principles, mathematical ratios, and algorithmic rules for logotype design, font pairing, typographic hierarchy, and brand lockups.

---

## 1. The 4 Fundamental Design Principles (CRAP)

> **Implemented by**: no single call — these are layout judgements. Repetition and contrast are served by `LogoType.calculateTypographicScale(...)` (one ratio, reused) and `LogoType.evaluateFontPairing(...)`; alignment by the armatures in Manual 09.

In brand and identity design, layout and typography must strictly adhere to four foundational laws:

### A. Proximity (Grouping & Spatial Hierarchy)
- **Rule**: Elements that are intellectually related must be physically grouped together into a cohesive visual unit.
- **Application in Brand Lockups**:
  - The Primary Wordmark and Tagline belong in close proximity (vertical spacing $\Delta Y \approx 0.25\text{--}0.4 \times \text{Wordmark Height}$).
  - Unrelated elements or metadata must be separated by at least $1.5\text{--}2.0 \times$ the internal group spacing.
  - White space must be **deliberate and unified**, never trapped in awkward leftover gaps.

### B. Alignment (Visual Connection & Anchor Lines)
- **Rule**: Every single typographic element must share an explicit visual alignment with another element.
- **Application in Brand Lockups**:
  - Horizontal Lockups: The mark center or baseline must align strictly with the wordmark cap-height/baseline.
  - Taglines should align flush-left with the wordmark initial stem or span the exact width of the primary wordmark using adjusted letter tracking.
  - Avoid ungrounded centered alignment unless the entire composition is mathematically symmetrical.

### C. Repetition (Visual Unity & Rhythm)
- **Rule**: Repeat visual attributes (stroke thicknesses, corner radii, color accents, geometric angles) across both the symbol and the logotype typography.
- **Application in Brand Lockups**:
  - If the mark uses a $3\text{px}$ stroke or $45^\circ$ angles, the logotype font or accents should echo those proportional dimensions.

### D. Contrast (Radical Visual Difference)
- **Rule**: If two typographic elements are not identical, make them **radically and unmistakably different**.
- **Application in Brand Lockups**:
  - Never pair a 16pt bold sans with an 18pt semi-bold sans (causes visual *Conflict*).
  - Pair a Heavy Bold Primary Wordmark with a Light, widely tracked Tagline (dramatic *Weight & Size Contrast*).

---

## 2. Typographic Harmony: Concord, Conflict & Contrast

> **Implemented by**: `LogoType.evaluateFontPairing(primaryCategory, secondaryCategory)` → `{ relationship, score, description, recommendations }`. Categories are `'sans'`, `'sansSerif'`, `'serif'`, `'modern'`, `'modernSerif'`, `'slab'`. Run it before committing to a pair — a `conflicting` verdict is the one to act on.

```
┌─────────────────┬──────────────────────────────────┬────────────────────────┐
│  Relationship   │           Description            │     Design Verdict     │
├─────────────────┼──────────────────────────────────┼────────────────────────┤
│ **Concordant**  │ Single typeface family; varies   │ Formal, elegant, safe, │
│                 │ only size, weight, or case.      │ highly unified.        │
├─────────────────┼──────────────────────────────────┼────────────────────────┤
│ **Conflicting** │ Two typefaces from same/similar  │ Visual dissonance;     │
│                 │ category with timid differences. │ looks like a mistake.  │
├─────────────────┼──────────────────────────────────┼────────────────────────┤
│ **Contrasting** │ Two or more distinctly different │ Dynamic, compelling,   │
│                 │ categories (e.g. Sans + Serif).  │ established hierarchy. │
└─────────────────┴──────────────────────────────────┴────────────────────────┘
```

---

## 3. The 6 Major Typeface Categories

> **Implemented by**: these category names are the arguments `LogoType.evaluateFontPairing(...)` accepts. Pass them verbatim.

| Category | Anatomical Characteristics | Best Used For | Brand Feeling |
|---|---|---|---|
| **Oldstyle** | Diagonal stress, slanted bracketed serifs, moderate thick/thin transition. | Editorial, body copy, heritage wordmarks. | Traditional, trustworthy, warm, intellectual. |
| **Modern** | Vertical stress, flat thin horizontal serifs, radical thick/thin contrast (Bodoni/Didot). | Luxury, high fashion, lifestyle, elegance. | Glamorous, upscale, sophisticated, crisp. |
| **Slab Serif** | Thick rectangular block serifs, vertical stress, low stroke contrast. | Tech, engineering, industrial, collegiate. | Sturdy, bold, authoritative, honest. |
| **Sans Serif** | Zero serifs, uniform monoweight or humanist stroke modulation. | Tech startups, UI, modern apps, signage. | Clean, modern, efficient, transparent. |
| **Script** | Handwritten, cursive, calligraphic loops, connected stems. | Boutiques, signatures, luxury food, artisanal. | Personal, creative, fluid, bespoke. |
| **Decorative** | Unique custom letterforms, extreme weights, stylized flourishes. | Hero display logos, album titles, gaming. | Expressive, idiosyncratic, memorable. |

---

## 4. The 6 Modes of Typographic Contrast

> **Implemented by**: `LogoType.calculateTypographicScale(baseSize, ratio, stepsDown, stepsUp)` → `{ baseSize, ratioName, ratioFactor, steps }` supplies the size mode; each step carries `{ name, size, lineHeight, tracking }`. The other five modes are choices you make against that ladder.

When creating typographic hierarchy in a brand identity, contrast must be applied across multiple axes:

1. **Size Contrast**: Dramatic scale jumps ($36\text{pt}$ vs. $10\text{pt}$). Use harmonic musical scales (Golden Ratio $1.618$, Perfect Fourth $1.333$).
2. **Weight Contrast**: Ultra-Light or Regular paired with Heavy Black/Extra-Bold.
3. **Structure Contrast**: Combining distinct categories (e.g. Geometric Sans + Classical Oldstyle Serif).
4. **Form Contrast**: Roman upright vs. True Italic; All-Caps with generous tracking vs. Lowercase.
5. **Direction Contrast**: Horizontal wordmark paired with vertical margin labels or arched text.
6. **Color & Tonal Density Contrast**: High-value saturated primary color paired with muted dark slate / neutral gray.

---

## 5. Doyald Young's Optical Logotype Mechanics

> **Implemented by**: `LogoType.createOgeeCurvePath(x1, y1, x2, y2, inflectionT, amplitude)` → SVG `d` string for the Ogee, also reachable as `ctx.drawOgeeCurve(...)` and `paper.ogeeCurve(...)`. Stroke thinning and junction traps are hand-drawn corrections — the manual gives the percentages.

Doyald Young's master rules for drawing and spacing custom logotypes:

### A. Horizontal Stroke Thinning ($10\%–20\%$ Rule)
- **The Optical Principle**: The human eye perceives horizontal lines as thicker than vertical lines of identical width.
- **The Rule**: In every logotype letterform, horizontal bars (e.g., crossbars in $H, E, F, A, T$) must be drawn **$10\%–20\%$ thinner** than the vertical stems to appear optically equal.

```
          Vertical Stem (100% Width)
          │  │
          │  │─────── Horizontal Crossbar (80%-85% Width)
          │  │
          │  │
```

### B. Diagonal Stroke Weighting & Calligraphic Tradition
- Downward-left to downward-right strokes ($\backslash$, as in $A, V, W, X$) carry the primary weight ($100\%$).
- Upward-left to downward-left strokes ($/$) carry the secondary, thinner weight ($60\%–75\%$).

### C. Acute Junction Thinning & Ink Traps
- Where strokes meet at sharp angles ($K, M, N, V, W$), the intersection creates an optical clump of black mass.
- **The Rule**: Thin the inner apex or insert an internal fillet/notch (ink trap) to relieve visual density.

### D. The Ogee Curve ($S$-Curve / Cyma Reversa)
- The double curvature curve used in classical Roman letterforms, swashes, and brand logotype crests.
- Formed by two smooth tangent arcs with an inflection point at parameter $t \approx 0.5$.
- **Usage in Code**: `LogoType.createOgeeCurvePath(x1, y1, x2, y2, inflectionT, amplitude)`.

---

## 6. Optical Kerning & Letter-Spacing Formulas

> **Implemented by**: `LogoType.computeOpticalKerning(charLeft, charRight, fontSize, fontCategory)` → pixel offset for that specific pair, and `LogoType.computeWordmarkTracking(fontSize, isAllCaps, role)` → tracking for the run as a whole. Use the pair function between glyphs, the tracking function for the whole word.

Letter spacing is an area-balancing task between character silhouettes:

```
┌─────────────────────────┬───────────────────┬────────────────────────────────┐
│   Glyph Pair Geometry   │  Spacing Factor   │            Examples            │
├─────────────────────────┼───────────────────┼────────────────────────────────┤
│ **Straight to Straight**│ $1.00 \times S$   │ $H|H, I|L, M|N, B|D$           │
├─────────────────────────┼───────────────────┼────────────────────────────────┤
│ **Straight to Round**   │ $0.75 \times S$   │ $H|O, D|C, N|G, I|Q$           │
├─────────────────────────┼───────────────────┼────────────────────────────────┤
│ **Round to Round**      │ $0.50 \times S$   │ $O|O, C|O, e|o, c|d$           │
├─────────────────────────┼───────────────────┼────────────────────────────────┤
│ **Diagonal to Any**     │ $-0.25\text{--}0$ │ $T|A, A|V, L|T, W|O, Y|a$       │
│ *(Tucking into Whitespace)*                  │ (Negative optical kerning)     │
└─────────────────────────┴───────────────────┴────────────────────────────────┘
```

### Wordmark vs. Tagline Tracking Rules:
1. **Large Display Wordmarks ($\ge 32\text{px}$)**: Use **tight optical tracking** ($-20\text{‰}$ to $-50\text{‰}$) so letterforms bind together into a unified wordmark silhouette.
2. **Small All-Caps Taglines ($\le 14\text{px}$)**: Use **wide generous tracking** ($+150\text{‰}$ to $+300\text{‰}$) to ensure maximum air and legibility across small digital screens.

---

## 7. Standard Brand Lockup Taxonomies

> **Implemented by**: `LogoType.drawWordmarkLockup(ctx, drawMarkFn, brandName, tagline, options)`, also on the context as `ctx.drawWordmarkLockup(...)`. `options.layout` selects `'horizontal'` or `'vertical'`. Raster only — there is no Snap.svg equivalent.

```
1. Horizontal Lockup (Default for Web Headers & Navigation)
   ┌────────────┐   ┌──────────────────────────────────────────────┐
   │            │   │  N E X U S                                   │
   │    MARK    │   │  ADVANCED AUTONOMOUS SYSTEMS                 │
   │            │   └──────────────────────────────────────────────┘
   └────────────┘

2. Vertical / Stacked Lockup (Default for Badges & App Splash)
                    ┌────────────┐
                    │    MARK    │
                    └────────────┘
               ┌──────────────────────┐
               │      N E X U S       │
               │  ADVANCED AUTONOMOUS │
               └──────────────────────┘

3. Enclosed Badge / Emblem Lockup (App Icons & Seals)
                ╭────────────────────────╮
                │          MARK          │
                │        ────────        │
                │         NEXUS          │
                ╰────────────────────────╯
```

- **Usage in Code**:
  - Canvas2D: `LogoType.drawWordmarkLockup(ctx, markFn, 'NEXUS', 'ADVANCED SYSTEMS', { layout: 'horizontal' })`, also reachable as `ctx.drawWordmarkLockup(markFn, ...)`.
  - Snap.svg: **no vector equivalent yet.** Lockup composition is raster-only; build the mark in Snap, render it, and compose the lockup on a Canvas2D context. There is no `wordmarkLockup` method on `paper`; earlier drafts of this manual claimed one.

---

## 8. Symbol → SDK Parameter Map

| Book concept | SDK parameter or field | Notes |
| --- | --- | --- |
| Concord / conflict / contrast | `pairing.relationship` | From `evaluateFontPairing`; `'conflicting'` is the verdict to act on. |
| Pairing quality | `pairing.score`, `pairing.recommendations` | Score is 0–100; the recommendations say what to change. |
| Category names | `'sans'`, `'sansSerif'`, `'serif'`, `'modern'`, `'modernSerif'`, `'slab'` | Arguments to `evaluateFontPairing`. |
| Harmonic ratio | `ratio` | `'goldenRatio'`, `'perfectFifth'`, `'augmentedFourth'`, `'perfectFourth'`, `'majorThird'`, `'minorThird'`. |
| Size ladder | `scale.steps[i]` | Each `{ name, size, lineHeight, tracking }`. |
| Ratio factor | `scale.ratioFactor` | The multiplier actually applied. |
| Pair kerning | `LogoType.computeOpticalKerning(l, r, fontSize, category)` | Pixels for that **specific pair**. |
| Run tracking | `LogoType.computeWordmarkTracking(fontSize, isAllCaps, role)` | For the **whole word**; `role` is `'wordmark'` or `'tagline'`. |
| Ogee amplitude / inflection | `amplitude`, `inflectionT` | Arguments 6 and 5 of `createOgeeCurvePath`. |
| Lockup layout | `options.layout` | `'horizontal'` or `'vertical'`. |

> Kerning and tracking are different tools: the pair function fixes one junction, the tracking function sets the rhythm of the run. Applying tracking to fix a bad `AV` pair loosens every other pair with it.

---

## 9. Constructing It: A Runnable Type Specimen

Evaluate the pairing before committing to it, take sizes from one ratio rather than picking them, and let the kerning function judge each junction.

```javascript
// Type specimen: pairing verdict → harmonic scale → optical kerning → lockup.
const canvas = createCanvas(900, 620);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f4';
ctx.fillRect(0, 0, 900, 620);
ctx.fillStyle = '#1c2733';

// §2 — Check the pair first. A 'conflicting' verdict means change one of them.
const pairing = LogoType.evaluateFontPairing('sansSerif', 'modernSerif');
log(pairing.relationship.toUpperCase() + ' (' + pairing.score + '/100) — ' + pairing.description);
ctx.font = '600 15px sans-serif';
ctx.fillText('PAIRING: ' + pairing.relationship.toUpperCase() + '  ·  ' + pairing.score + '/100', 60, 60);

// §4 — One ratio generates the whole ladder, so every size is related.
const scale = LogoType.calculateTypographicScale(15, 'goldenRatio', 1, 4);
log('ratio ' + scale.ratioName + ' = ' + scale.ratioFactor);
let y = 120;
for (let i = 0; i < scale.steps.length; i++) {
    const step = scale.steps[i];
    ctx.font = '700 ' + Math.min(34, step.size) + 'px sans-serif';
    ctx.fillText(step.name.toUpperCase(), 60, y);
    ctx.font = '400 12px monospace';
    ctx.fillStyle = '#6b7684';
    // Sizes come back as 32-bit floats, so they widen to values like 9.300000190734863
    // in JS. Always format numbers you are about to draw.
    ctx.fillText(step.size.toFixed(1) + 'px / ' + step.lineHeight.toFixed(1) +
        'px  tracking ' + step.tracking.toFixed(2), 260, y);
    ctx.fillStyle = '#1c2733';
    y += 40;
}

// §6 — Kerning is per-junction. Round-to-round tucks tighter than straight-to-straight.
const pairs = [['H', 'H'], ['H', 'O'], ['O', 'O'], ['T', 'A']];
for (let i = 0; i < pairs.length; i++) {
    const offset = LogoType.computeOpticalKerning(pairs[i][0], pairs[i][1], 36);
    log(pairs[i][0] + pairs[i][1] + ' -> ' + offset.toFixed(2) + 'px');
}

// §6 — Tracking applies to the run, and a tagline wants far more of it than a wordmark.
log('wordmark tracking = ' + LogoType.computeWordmarkTracking(42, false, 'wordmark').toFixed(2) +
    ', tagline tracking = ' + LogoType.computeWordmarkTracking(13, true, 'tagline').toFixed(2));

// §7 — The lockup composes a mark with the name. The mark is a function so the
// lockup can size it: (ctx, size) => void.
const drawMark = (c, size) => {
    c.fillStyle = '#1f3b57';
    c.beginPath();
    c.arc(size / 2, size / 2, size / 2, 0, Math.PI * 2);
    c.fill();
    c.fillStyle = '#f0b429';
    c.beginPath();
    c.arc(size / 2, size / 2, size / 5, 0, Math.PI * 2);
    c.fill();
};

LogoType.drawWordmarkLockup(ctx, drawMark, 'NEXUS', 'ADVANCED SYSTEMS', {
    layout: 'horizontal',
    x: 520,
    y: 400,
    markSize: 96,
    fontSize: 46,
    taglineSize: 13,
    primaryColor: '#1c2733',
    taglineColor: '#6b7684'
});

canvas;
```
