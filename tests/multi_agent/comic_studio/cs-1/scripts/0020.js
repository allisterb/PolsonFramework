// ============================================================================
// cs-1 · Stage 2 · CRITIC — refinement pass 1
// Resolves the construction sheet into a graphite drawing: masses knocked back
// to the ground and hatched back-to-front so occlusion is deterministic, then
// contour, then the solid accents. Construction and labels dropped.
// ============================================================================
Stage.begin('Critic');
Stage.note('Refinement pass 1 against the ten defects. The structural move is back-to-front: each mass fills itself with the paper ground first and is then hatched, so a nearer mass occludes a farther one by construction rather than by an even-odd clip over self-intersecting contours. Value order is the reference Notan: trunk near-black, right rabbit mid-dark, left rabbit light, wash untouched. Light reads as coming from the upper front-left, so the shadow accents all go on the viewer-right of each mass.');

const RW = 620, RH = 336, S = 2;
const G = { ground: '#faf8f2', dark: '#2f2f2f', graphite: '#444444', soft: '#6b6b6b', pale: '#8a8a8a' };

const A = {
    L: {
        farEar: { c: [58, 134], rx: 52, ry: 67, rot: -24 },
        nearEarL: [[136, 45], [122, 62], [117, 90], [114, 120], [116, 150], [121, 176]],
        nearEarR: [[136, 45], [154, 56], [168, 76], [176, 102], [174, 132], [166, 160], [157, 179]],
        nearEarRidge: [[138, 72], [143, 112], [148, 156]],
        crown: [[157, 179], [180, 177], [205, 182], [228, 194], [246, 208], [258, 222], [266, 236], [274, 248], [281, 258]],
        jaw: [[281, 258], [277, 272], [270, 285], [255, 302], [228, 314], [196, 320], [160, 323], [130, 331], [122, 336]],
        back: [[74, 190], [84, 200], [96, 215], [100, 232], [103, 250], [108, 265], [113, 282], [118, 300], [119, 320], [117, 336]],
        skullTop: [[100, 190], [120, 183], [140, 179], [157, 179]],
        eyeAlmond: [[160, 199], [172, 187], [190, 181], [208, 185], [219, 197], [210, 215], [192, 228], [173, 221]],
        pupil: { c: [190, 206], rx: 23, ry: 17, rot: -10 }, catch: [184, 201],
        brow: [[150, 188], [176, 175], [205, 171], [232, 182]],
        muzzleTop: [[228, 248], [246, 246], [262, 252], [272, 262]],
        chinLobe: [[209, 272], [216, 288], [232, 300], [252, 303]],
        mouthLine: [[231, 266], [243, 279], [257, 292]],
        teeth: [[251, 279], [268, 281], [264, 297], [253, 295]],           // dropped 8px, defect 10
        whiskerRoot: [231, 262], whiskerEnds: [[152, 240], [148, 260], [156, 280]]
    },
    R: {
        earFarL: [[398, 0], [395, 26], [392, 48], [389, 62], [382, 76]], earFarR: [[444, 0], [443, 26], [441, 46], [446, 60], [452, 68]],
        earNearL: [[459, 0], [456, 20], [452, 40], [450, 53], [452, 66]], earNearR: [[525, 0], [512, 24], [501, 41], [494, 53], [489, 68]],
        earNearRidge: [[473, 6], [466, 30], [462, 51]], earFarRidge: [[418, 12], [414, 40], [412, 58]],
        skullTop: [[382, 76], [400, 62], [430, 56], [462, 60], [489, 68]],
        faceLeft: [[382, 76], [374, 84], [370, 90], [367, 112], [361, 136], [362, 156], [367, 172], [361, 182], [350, 192], [346, 200]],
        faceRight: [[489, 68], [487, 80], [490, 104], [494, 128], [496, 148], [491, 164], [484, 178], [473, 192], [470, 206]],
        chestLeft: [[346, 200], [335, 224], [320, 248], [303, 272], [289, 284], [274, 296], [261, 308], [249, 320], [241, 336]],
        chestRight: [[470, 206], [477, 236], [480, 260], [480, 284], [475, 296], [471, 308], [461, 320], [455, 336]],
        lidE: { c: [441, 118], rx: 14, ry: 26, rot: -14 }, pupil: { c: [441, 118], rx: 11, ry: 22, rot: -16 }, catch: [443, 121],
        farPupil: { c: [369, 114], rx: 8, ry: 15, rot: -8 }, farCatch: [371, 117],
        noseBridge: [[378, 168], [390, 163], [404, 168], [410, 178]],
        lowerMuzzle: [[366, 196], [372, 212], [386, 222], [404, 222], [418, 214]],
        mouthLine: [[379, 195], [391, 205], [406, 211]],
        teeth: [[387, 201], [405, 204], [402, 216], [389, 214]],
        whiskerRoot: [382, 186], whiskerEnds: [[262, 132], [254, 158], [262, 182], [284, 204]],
        whiskerEndsR: [[498, 200], [502, 218], [494, 234]]
    },
    stems: [[[203, 0], [262, 105], [300, 165], [345, 252]], [[70, 0], [64, 50], [58, 105], [54, 142]],
    [[468, 212], [575, 98]], [[575, 98], [620, 150]], [[500, 224], [620, 252]]],
    grass: [{ x0: 0, x1: 118, base: 340, lo: 55, hi: 150, n: 30 }, { x0: 0, x1: 74, base: 300, lo: 40, hi: 96, n: 14 },
    { x0: 226, x1: 350, base: 340, lo: 26, hi: 66, n: 17 }, { x0: 340, x1: 470, base: 340, lo: 24, hi: 58, n: 15 },
    { x0: 448, x1: 622, base: 340, lo: 38, hi: 98, n: 24 }]
};

const canvas = createCanvas(RW * S, RH * S);
const ctx = canvas.getContext('2d');
ctx.fillStyle = G.ground; ctx.fillRect(0, 0, RW * S, RH * S);
ctx.scale(S, S); ctx.lineCap = 'round'; ctx.lineJoin = 'round';

function rnd(i) { const s = Math.sin(i * 127.1 + 11.7) * 43758.5453; return s - Math.floor(s); }
function ell(cx, cy, rx, ry, rot, n) {
    const a = rot * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a), p = [];
    for (let i = 0; i < n; i++) { const t = i / n * Math.PI * 2, x = rx * Math.cos(t), y = ry * Math.sin(t); p.push([cx + x * ca - y * sa, cy + x * sa + y * ca]); }
    return p;
}
function rev(a) { const r = []; for (let i = a.length - 1; i >= 0; i--) r.push(a[i]); return r; }
function cat() { let r = []; for (let i = 0; i < arguments.length; i++) r = r.concat(arguments[i]); return r; }
function build(pts, close, curve) {
    ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]);
    if (curve && pts.length > 2) {
        for (let i = 1; i < pts.length - 1; i++) {
            const mx = (pts[i][0] + pts[i + 1][0]) / 2, my = (pts[i][1] + pts[i + 1][1]) / 2;
            ctx.quadraticCurveTo(pts[i][0], pts[i][1], mx, my);
        }
        ctx.lineTo(pts[pts.length - 1][0], pts[pts.length - 1][1]);
    } else for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);
    if (close) ctx.closePath();
}
function set(c, w, a) { ctx.strokeStyle = c; ctx.lineWidth = w; ctx.globalAlpha = a === undefined ? 1 : a; }
function strokeS(pts, close) { build(pts, close, true); ctx.stroke(); }
function strokeL(pts, close) { build(pts, close, false); ctx.stroke(); }

// Knock the mass back to paper, so whatever was behind it stops showing through.
function knock(region) {
    build(region, true, true);
    ctx.globalAlpha = 1; ctx.fillStyle = G.ground;
    ctx.fill('nonzero');                       // rule always explicit — see findings
}
// Parallel hatch inside a clip. Extra regions intersect the clip, giving a crescent
// without needing a boolean path op, which the SDK does not expose.
function hatch(region, extra, angleDeg, spacing, width, alpha, colour) {
    ctx.save();
    build(region, true, true); ctx.clip('nonzero');
    if (extra) { build(extra, true, false); ctx.clip('nonzero'); }
    const a = angleDeg * Math.PI / 180, dx = Math.cos(a), dy = Math.sin(a), px = -dy, py = dx;
    const R = 400, Lh = 420;
    set(colour, width, alpha);
    for (let t = -R; t <= R; t += spacing) {
        const cx = RW / 2 + px * t, cy = RH / 2 + py * t;
        ctx.beginPath(); ctx.moveTo(cx - dx * Lh, cy - dy * Lh); ctx.lineTo(cx + dx * Lh, cy + dy * Lh); ctx.stroke();
    }
    ctx.restore(); ctx.globalAlpha = 1;
}

// --- regions -----------------------------------------------------------------
const REG = {
    trunk: [[80, 0], [162, 0], [166, 50], [170, 100], [176, 160], [150, 178], [112, 186], [92, 182], [84, 120], [80, 60]],
    lFarEar: ell(58, 134, 52, 67, -24, 48),
    lNearEar: cat(A.L.nearEarR, rev(A.L.nearEarL)),
    lHead: cat(A.L.crown, A.L.jaw.slice(1), [[117, 336]], rev(A.L.back).slice(1), A.L.skullTop),
    rEarFar: cat(A.R.earFarL, rev(A.R.earFarR)),
    rEarNear: cat(A.R.earNearL, rev(A.R.earNearR)),
    rBody: cat(A.R.faceLeft, A.R.chestLeft.slice(1), [[455, 336]], rev(A.R.chestRight).slice(1), rev(A.R.faceRight), rev(A.R.skullTop).slice(1))
};
// Shadow sub-regions — intersected with their parent, never drawn alone.
const SHADE = {
    farEarRim: [[76, 78], [150, 120], [136, 200], [46, 214]],
    lHeadUnder: [[236, 228], [320, 236], [320, 344], [150, 344]],
    lEarRoot: [[138, 172], [196, 172], [190, 214], [132, 208]],
    rBodyRight: [[452, 40], [540, 40], [540, 344], [418, 344]],
    rMuzzleUnder: [[352, 196], [440, 196], [440, 250], [346, 250]]
};

// --- 1. distant grass --------------------------------------------------------
let k = 0;
function grass(lo, hi, alphaScale) {
    for (const g of A.grass) for (let i = 0; i < g.n; i++) {
        k++;
        const h = g.lo + (g.hi - g.lo) * rnd(k + 300);
        if (h < lo || h >= hi) continue;
        const bx = g.x0 + (g.x1 - g.x0) * (i + rnd(k) * 0.8) / g.n;
        const lean = (rnd(k + 700) - 0.5) * h * 0.75, by = g.base - rnd(k + 900) * 14;
        set(G.soft, 0.45 + rnd(k + 50) * 0.55, (0.35 + rnd(k + 90) * 0.4) * alphaScale);
        ctx.beginPath(); ctx.moveTo(bx, by);
        ctx.quadraticCurveTo(bx + lean * 0.2, by - h * 0.6, bx + lean, by - h); ctx.stroke();
    }
    ctx.globalAlpha = 1;
}
grass(0, 999, 0.85);

// --- 2. trunk: the darkest mass in the frame --------------------------------
knock(REG.trunk);
hatch(REG.trunk, null, 88, 1.9, 1.0, 0.42, G.dark);
hatch(REG.trunk, null, 58, 3.0, 0.8, 0.26, G.dark);
hatch(REG.trunk, [[80, 0], [130, 0], [138, 190], [88, 190]], 88, 2.6, 0.9, 0.30, G.dark);
set(G.graphite, 1.7, 1);
strokeS([[80, 0], [80, 60], [82, 100], [86, 130], [88, 140]]);
strokeS([[162, 0], [163, 40], [168, 62]]);

// --- 3. stems and leaves, hung ON the stem ----------------------------------
set(G.graphite, 1.1, 0.85);
for (const s of A.stems) strokeL(s);
// Each leaf roots on the main stem: parameter t along it, so nothing floats.
const main = A.stems[0];
function onStem(t) {
    const seg = t * (main.length - 1), i = Math.min(main.length - 2, Math.floor(seg)), f = seg - i;
    return [main[i][0] + (main[i + 1][0] - main[i][0]) * f, main[i][1] + (main[i + 1][1] - main[i][1]) * f];
}
const leafSpec = [[0.08, 17, -28], [0.20, 13, 14], [0.31, 18, -36], [0.45, 12, 26], [0.58, 19, -18], [0.72, 14, 33], [0.86, 15, -25]];
for (const ls of leafSpec) {
    const r = onStem(ls[0]), a = ls[2] * Math.PI / 180, Ln = ls[1], w = Ln * 0.3;
    const dx = Math.cos(a) * Ln, dy = Math.sin(a) * Ln, nx = -Math.sin(a) * w, ny = Math.cos(a) * w;
    ctx.beginPath(); ctx.moveTo(r[0], r[1]);
    ctx.quadraticCurveTo(r[0] + dx * 0.5 + nx, r[1] + dy * 0.5 + ny, r[0] + dx, r[1] + dy);
    ctx.quadraticCurveTo(r[0] + dx * 0.5 - nx, r[1] + dy * 0.5 - ny, r[0], r[1]); ctx.stroke();
}
ctx.globalAlpha = 1;

// --- 4. LEFT rabbit ----------------------------------------------------------
knock(REG.lFarEar);
hatch(REG.lFarEar, null, 62, 5.4, 0.55, 0.13, G.soft);
hatch(REG.lFarEar, SHADE.farEarRim, 62, 2.6, 0.7, 0.26, G.graphite);
knock(REG.lNearEar);
hatch(REG.lNearEar, null, 78, 3.8, 0.6, 0.20, G.soft);
knock(REG.lHead);
hatch(REG.lHead, null, 68, 4.0, 0.6, 0.17, G.soft);
hatch(REG.lHead, SHADE.lHeadUnder, 68, 2.4, 0.7, 0.22, G.graphite);
hatch(REG.lHead, SHADE.lEarRoot, 40, 2.2, 0.7, 0.26, G.graphite);

set(G.graphite, 1.7, 1);
strokeS(ell(58, 134, 52, 67, -24, 40), true);
strokeS(A.L.back); strokeS(A.L.jaw); strokeS(A.L.crown);
set(G.graphite, 1.2, 0.95);
strokeS(A.L.nearEarL); strokeS(A.L.nearEarR);
strokeS([[121, 176], [138, 181], [157, 179]]);
set(G.soft, 0.8, 0.8);
strokeS(A.L.nearEarRidge);
strokeS(ell(61, 138, 35, 49, -24, 32), true);
// ear-canal wedge at the far ear's base
set(G.graphite, 0.9, 0.7); strokeS([[86, 168], [98, 180], [92, 192]]);

// --- 5. RIGHT rabbit ---------------------------------------------------------
knock(REG.rEarFar); hatch(REG.rEarFar, null, 82, 3.2, 0.6, 0.26, G.soft);
knock(REG.rEarNear); hatch(REG.rEarNear, null, 82, 3.2, 0.6, 0.24, G.soft);
knock(REG.rBody);
hatch(REG.rBody, null, 72, 2.7, 0.65, 0.28, G.graphite);
hatch(REG.rBody, null, 18, 6.0, 0.55, 0.12, G.soft);
hatch(REG.rBody, SHADE.rBodyRight, 72, 2.0, 0.7, 0.22, G.dark);
hatch(REG.rBody, SHADE.rMuzzleUnder, 30, 2.4, 0.6, 0.18, G.graphite);

set(G.graphite, 1.7, 1);
strokeS(A.R.earFarL); strokeS(A.R.earFarR); strokeS(A.R.earNearL); strokeS(A.R.earNearR);
strokeS(A.R.faceLeft); strokeS(A.R.faceRight); strokeS(A.R.chestLeft); strokeS(A.R.chestRight);
set(G.soft, 0.8, 0.8);
strokeS(A.R.earNearRidge); strokeS(A.R.earFarRidge);

// --- 6. foreground grass over the chests ------------------------------------
grass(0, 60, 1.0);

// --- 7. features: the solid accents ------------------------------------------
function solidPupil(e, catchPt, r) {
    build(ell(e.c[0], e.c[1], e.rx, e.ry, e.rot, 40), true, true);
    ctx.globalAlpha = 1; ctx.fillStyle = '#1c1c1c'; ctx.fill('nonzero');
    ctx.beginPath(); ctx.arc(catchPt[0], catchPt[1], r, 0, Math.PI * 2);
    ctx.fillStyle = G.ground; ctx.fill('nonzero');
}
set(G.graphite, 1.2, 1); strokeS(A.L.eyeAlmond, true);
solidPupil(A.L.pupil, A.L.catch, 3.2);
set(G.soft, 0.8, 0.85); strokeS(A.L.brow);
set(G.graphite, 1.2, 1); strokeS(ell(A.R.lidE.c[0], A.R.lidE.c[1], A.R.lidE.rx, A.R.lidE.ry, A.R.lidE.rot, 32), true);
solidPupil(A.R.pupil, A.R.catch, 2.8);
solidPupil(A.R.farPupil, A.R.farCatch, 2.2);

set(G.graphite, 1.15, 1);
strokeS(A.L.muzzleTop); strokeS(A.L.mouthLine); strokeS(A.L.chinLobe);
strokeS(A.R.noseBridge); strokeS(A.R.mouthLine); strokeS(A.R.lowerMuzzle);
function teeth(q) {
    build(q, true, false); ctx.fillStyle = G.ground; ctx.globalAlpha = 1; ctx.fill('nonzero');
    set(G.graphite, 1.0, 1); build(q, true, false); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo((q[0][0] + q[1][0]) / 2, (q[0][1] + q[1][1]) / 2);
    ctx.lineTo((q[2][0] + q[3][0]) / 2, (q[2][1] + q[3][1]) / 2); ctx.stroke();
}
teeth(A.L.teeth); teeth(A.R.teeth);

// Whiskers: bowed, not straight rays. Lift scales with span.
function whiskers(root, ends) {
    for (const e of ends) {
        const span = Math.hypot(e[0] - root[0], e[1] - root[1]);
        ctx.beginPath(); ctx.moveTo(root[0], root[1]);
        ctx.quadraticCurveTo((root[0] + e[0]) / 2, (root[1] + e[1]) / 2 - span * 0.16, e[0], e[1]); ctx.stroke();
    }
}
set(G.soft, 0.5, 0.6);
whiskers(A.L.whiskerRoot, A.L.whiskerEnds);
whiskers(A.R.whiskerRoot, A.R.whiskerEnds);
whiskers([440, 192], A.R.whiskerEndsR);
ctx.globalAlpha = 1;
canvas;