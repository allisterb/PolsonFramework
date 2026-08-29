// =============================================================================
//  Sailboat Tours — primary identity
//  Produces:  output.svg   the primary mark as vector (master artwork)
//             output.webp  the brand presentation sheet (1600 x 1000)
//
//  Concept — the mark is a sun/moon disc at sea with a two-sailed sloop cut out
//  of it as negative space. "Romantic" is carried by the pairing (two sails,
//  unequal, sharing one hull), by the wind-filled convex leeches, and by the
//  disc itself (a sunset, a moonrise) — not by a heart.
//
//  Armature — the mast sits on the GOLDEN SECTION of the boat's beam, so the
//  two sails are Phi-unequal while their combined visual mass still lands on
//  the centreline. Phi is read off Logo.createGoldenCircles, never retyped.
//  Mast width is bone-corrected via Logo.correctBoneEffect.
//
//  The mark is ONE closed counter inside ONE disc, filled evenodd. The negative
//  space is therefore genuine geometry: it closes up honestly as the mark
//  shrinks rather than being faked with a background-coloured shape on top.
// =============================================================================

// ---------------------------------------------------------------- geometry --
const PHI = Logo.createGoldenCircles(0, 0, 100, 2).phi;   // never retype the constant

const BEAM0 = 16, BEAM1 = 84;          // the boat's beam, inside a 0..100 box
const FOOT = 76, HEAD_M = 10, HEAD_J = 22;
const MAST_HALF = 7;
const HULL_SPAN = 34, HULL_DIP = 90;
const DISC_R = 46;

const MAST_C = BEAM0 + (BEAM1 - BEAM0) * (1 - 1 / PHI);   // the golden section of the beam
const ML = MAST_C - MAST_HALF, MR = MAST_C + MAST_HALF;

// Manual 10 s4A — a dark bar between two larger shapes reads pinched at the
// waist. Ask the SDK how far to bulge it rather than guessing.
const BONE = Logo.correctBoneEffect({ x: MAST_C, y: HEAD_M }, { x: MAST_C, y: FOOT }, 2 * MAST_HALF);
const CTRL = 2 * (ML - BONE[1].x);     // quadratic control offset giving that bulge

// Emit the mark's path data at an arbitrary origin and scale. Written
// parametrically rather than via a transform: on this engine a Snap element's
// transform only accepts Snap's own shorthand grammar, and both
// matrix(a,b,c,d,e,f) and the documented SnapMatrix overload are silently
// reduced to identity. See findings.md s1.5.
const markPath = (ox, oy, S) => {
    const u = v => v * S / 100;
    const X = v => (ox + u(v)).toFixed(4);
    const Y = v => (oy + u(v)).toFixed(4);
    const R = u(DISC_R).toFixed(4);
    return [
        // the disc — the sun, or the moon, on the water
        `M ${X(50 - DISC_R)},${Y(50)} A ${R},${R} 0 1 0 ${X(50 + DISC_R)},${Y(50)}`,
        `A ${R},${R} 0 1 0 ${X(50 - DISC_R)},${Y(50)} Z`,
        // the boat — one closed counter: hull and both sails are a single
        // connected light shape, notched from above by the dark mast
        `M ${X(50 + HULL_SPAN)},${Y(FOOT)}`,
        `Q ${X(50)},${Y(HULL_DIP)} ${X(50 - HULL_SPAN)},${Y(FOOT)}`,   // hull underside
        `Q ${X(BEAM0 + 3)},${Y((HEAD_J + FOOT) / 2 - 4)} ${X(ML)},${Y(HEAD_J)}`, // jib luff
        `Q ${X(ML - CTRL)},${Y((HEAD_J + FOOT) / 2)} ${X(ML)},${Y(FOOT)}`,       // mast, left edge
        `L ${X(MR)},${Y(FOOT)}`,
        `Q ${X(MR + CTRL)},${Y((HEAD_M + FOOT) / 2)} ${X(MR)},${Y(HEAD_M)}`,     // mast, right edge
        `Q ${X(BEAM1 - 7)},${Y(HEAD_M + (FOOT - HEAD_M) * 0.45)} ${X(BEAM1)},${Y(FOOT)}`, // mainsail leech
        `L ${X(50 + HULL_SPAN)},${Y(FOOT)} Z`
    ].join(' ');
};

// Every vertex must stay inside the disc, or the hull bursts the silhouette and
// leaves fragile spurs that alias away at favicon sizes.
const containment = [
    ['jib tack', BEAM0, FOOT], ['main clew', BEAM1, FOOT], ['masthead', MR, HEAD_M],
    ['jib head', ML, HEAD_J], ['hull left', 50 - HULL_SPAN, FOOT], ['hull right', 50 + HULL_SPAN, FOOT],
    ['hull bottom', 50, 0.25 * FOOT + 0.5 * HULL_DIP + 0.25 * FOOT]
];
for (const [name, x, y] of containment) {
    const d = Math.hypot(x - 50, y - 50);
    if (d > DISC_R - 0.5) error(`containment FAILED: ${name} at ${d.toFixed(2)} > ${DISC_R - 0.5}`);
}
log(`Phi = ${PHI}`);
log(`mast on the golden section of the beam: x = ${MAST_C.toFixed(3)}`);
log(`bone correction: ${(ML - BONE[1].x).toFixed(3)}u on a ${2 * MAST_HALF}u mast ` +
    `(${(100 * (ML - BONE[1].x) / (2 * MAST_HALF)).toFixed(1)}%)`);

// ----------------------------------------------------------------- palette --
const P = {
    primary: '#22304a', accent: '#de8b63', dark: '#101a2b', light: '#f7f3ea',
    names: [['Dusk Indigo', '#22304a'], ['Sunset Coral', '#de8b63'],
            ['Midnight', '#101a2b'], ['Sailcloth', '#f7f3ea']]
};
// Manual 11 s1D — radical contrast, never near-identical. Old-style serif
// against a neutral grotesque.
const PAIR = LogoType.evaluateFontPairing('serif', 'sans-serif');
log(`font pairing serif + sans-serif -> ${PAIR.relationship}, score ${PAIR.score}`);
const SERIF = 'Garamond';   // the only romantic old-style serif this engine resolves
const SANS = 'Arial';

// =========================================================== 1. output.svg ==
// The master artwork: the mark alone, as vector.
const SVG_SIZE = 512;
const paper = Snap(SVG_SIZE, SVG_SIZE);
paper.path(markPath(0, 0, SVG_SIZE)).attr({ fill: P.primary, 'fill-rule': 'evenodd' });

// =========================================================== 2. the board ===
const W = 1600, H = 1060;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// -- ground. Texture belongs to the PRESENTATION, never to the mark: the mark
// -- must survive one flat tin of ink, so nothing raster may enter it. The
// -- board, though, is a document, and a document is printed on a stock.
ctx.fillStyle = P.light;
ctx.fillRect(0, 0, W, H);
if (Session.clothUri) {
    const cloth = Skia.Image.fromDataUrl(Session.clothUri);
    ctx.save();
    ctx.globalAlpha = 0.13;
    ctx.fillStyle = Skia.Shader.bitmap(cloth, 'repeat', 'repeat');
    ctx.fillRect(0, 0, W, H);
    ctx.restore();
    log('presentation ground: requisitioned sailcloth at 13% — board only, never the mark');
} else {
    log('no requisitioned material in session — board printed plain');
}

const markInk = (c, ox, oy, S, ink) => {
    c.save(); c.fillStyle = ink;
    c.fill(new CanvasPath(markPath(ox, oy, S)), 'evenodd'); c.restore();
};

// -- type helpers. The SDK's lockup call exposes sizes and colours but no
// -- typeface, so the wordmark is set by hand using its kerning + tracking data.
const trackedWidth = (c, text, size, family, extra) => {
    c.font = `${size}px "${family}"`;
    let w = 0;
    for (let i = 0; i < text.length; i++) w += c.measureText(text[i]).width + (i < text.length - 1 ? extra : 0);
    return w;
};
const drawTracked = (c, text, x, y, size, family, extra, color, kern) => {
    c.save(); c.fillStyle = color; c.font = `${size}px "${family}"`;
    let cx = x;
    for (let i = 0; i < text.length; i++) {
        c.fillText(text[i], cx, y);
        cx += c.measureText(text[i]).width;
        if (i < text.length - 1) {
            cx += extra;
            // Manual 11 s6 — optical kerning by glyph-pair silhouette. Damped:
            // the raw figure is tuned for display sizes far larger than this.
            if (kern && text[i] !== ' ' && text[i + 1] !== ' ')
                cx += LogoType.computeOpticalKerning(text[i], text[i + 1], size, 'serif') * 0.3;
        }
    }
    c.restore();
    return cx - x;
};

const NAME = 'Sailboat Tours', TAG = 'SUNSET & MOONLIGHT SAILING';

// A lockup, drawn once and reused at both scales and both layouts.
const lockup = (c, x, y, markS, nameS, vertical) => {
    const track = LogoType.computeWordmarkTracking(nameS, false, 'wordmark') * nameS;  // em fraction -> px
    const tagS = Math.max(11, Math.round(nameS * 0.26));
    if (vertical) {
        markInk(c, x + (260 - markS) / 2, y, markS, P.primary);
        const wmW = trackedWidth(c, NAME, nameS, SERIF, track);
        drawTracked(c, NAME, x + (260 - wmW) / 2, y + markS + nameS + 6, nameS, SERIF, track, P.primary, true);
        // Manual 11 sB — set the tagline to span the wordmark exactly.
        const nat = trackedWidth(c, TAG, tagS, SANS, 0);
        const ex = (wmW - nat) / (TAG.length - 1);
        drawTracked(c, TAG, x + (260 - wmW) / 2, y + markS + nameS + tagS + 20, tagS, SANS, ex, P.accent, false);
        return wmW;
    }
    markInk(c, x, y, markS, P.primary);
    // Manual 10 s6C — clear space X = mark height / 4 sets the gap to the type.
    const X = markS / 4;
    const tx = x + markS + X;
    const wmW = drawTracked(c, NAME, tx, y + markS * 0.62, nameS, SERIF, track, P.primary, true);
    const tagS2 = Math.max(11, Math.round(nameS * 0.26));
    const nat = trackedWidth(c, TAG, tagS2, SANS, 0);
    const ex = (wmW - nat) / (TAG.length - 1);
    drawTracked(c, TAG, tx, y + markS * 0.62 + tagS2 + 14, tagS2, SANS, ex, P.accent, false);
    return wmW;
};

const M = 60;
const label = (t, x, y, col, tr) => drawTracked(ctx, t, x, y, 11, SANS, tr === undefined ? 1.9 : tr, col || '#8a9099', false);
const rule = y => { ctx.save(); ctx.strokeStyle = '#d9d2c4'; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(M, y); ctx.lineTo(W - M, y); ctx.stroke(); ctx.restore(); };

// ---- header
lockup(ctx, M, 46, 92, 44, false);
label('BRAND IDENTITY / PRIMARY LOGO', W - M - 220, 70);
label('SAILBOAT TOURS', W - M - 220, 92, P.primary);
rule(178);

// ---- band 1: the mark, the knockout, the app icon
const B1 = 214, PANEL_H = 300, PW = 470;
ctx.save(); ctx.strokeStyle = '#ddd6c8'; ctx.lineWidth = 1; ctx.strokeRect(M, B1, PW, PANEL_H); ctx.restore();
markInk(ctx, M + (PW - 240) / 2, B1 + 24, 240, P.primary);
label('PRIMARY MARK', M + 14, B1 + PANEL_H - 16);

ctx.save(); ctx.fillStyle = P.dark; ctx.fillRect(M + 510, B1, PW, PANEL_H); ctx.restore();
markInk(ctx, M + 510 + (PW - 240) / 2, B1 + 24, 240, P.light);
label('ONE COLOUR — KNOCKOUT', M + 524, B1 + PANEL_H - 16, '#8e9aa8');

ctx.save(); ctx.strokeStyle = '#ddd6c8'; ctx.lineWidth = 1; ctx.strokeRect(M + 1020, B1, 460, PANEL_H); ctx.restore();
Logo.drawSquircle(ctx, M + 1130, B1 + 30, 240, 240, { fill: P.primary, exponent: 4.5 });
markInk(ctx, M + 1160, B1 + 60, 180, P.light);
label('APP ICON', M + 1034, B1 + PANEL_H - 16);
rule(B1 + PANEL_H + 40);

// ---- band 2: lockups
const B2 = B1 + PANEL_H + 82;
label('PRIMARY LOCKUP — HORIZONTAL', M, B2 - 14);
lockup(ctx, M, B2, 104, 46, false);
label('SECONDARY — STACKED', M + 780, B2 - 14);
lockup(ctx, M + 780, B2, 104, 40, true);
rule(B2 + 225);

// ---- band 3: scale ladder, palette, clear space, construction note
const B3 = B2 + 270;
label('SCALE — ACTUAL SIZE', M, B3 - 12);
let sx = M;
for (const px of [16, 24, 32, 48, 64]) {
    markInk(ctx, sx, B3 + (64 - px), px, P.primary);
    label(px + 'px', sx, B3 + 82, null, 0.6);
    sx += px + 30;
}

label('PALETTE', M + 380, B3 - 12);
let cx2 = M + 380;
for (const [nm, hex] of P.names) {
    ctx.save(); ctx.fillStyle = hex; ctx.fillRect(cx2, B3, 104, 62);
    ctx.strokeStyle = '#ddd6c8'; ctx.lineWidth = 1; ctx.strokeRect(cx2, B3, 104, 62); ctx.restore();
    label(nm.toUpperCase(), cx2, B3 + 78, '#5d6570', 0.3);
    label(hex.toUpperCase(), cx2, B3 + 94, null, 0.3);
    cx2 += 122;
}

label('CLEAR SPACE — X = HEIGHT / 4', M + 880, B3 - 12);
// Manual 10 s6 warning: drawClearSpaceGuide CLEARS the mark rectangle to
// transparent as it shades the margin, so it must run BEFORE the mark. It also
// leaves that rectangle transparent, so the textured ground is restored by hand.
const cs = { x: M + 928, y: B3 + 22, width: 96, height: 96 };
Logo.drawClearSpaceGuide(ctx, cs, cs.height / 4, { showLabels: true });
ctx.save(); ctx.fillStyle = P.light; ctx.fillRect(cs.x, cs.y, cs.width, cs.height); ctx.restore();
markInk(ctx, cs.x, cs.y, cs.width, P.primary);

const oshoot = Logo.computeOvershoot(96, 'circle');
label('CONSTRUCTION', M + 1160, B3 - 12);
ctx.save(); ctx.fillStyle = '#5d6570'; ctx.font = `12px "${SANS}"`;
ctx.fillWrappedText(
    `Mast on the golden section of the beam (Φ = ${PHI.toFixed(5)}). Mast bulge ` +
    `${(ML - BONE[1].x).toFixed(2)}u per Logo.correctBoneEffect. Circular overshoot ` +
    `${oshoot.toFixed(1)}px at this size. One closed counter, filled evenodd — the ` +
    `negative space is real geometry, not a shape laid on top.`,
    M + 1160, B3 + 14, 300, 17);
ctx.restore();

log('board complete');
canvas;
