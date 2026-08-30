// ============================================================================
// cs-1 · Stage 1 · PENCILER — construction sheet, pass 2
// Fixes the ten defects found by overlaying pass 1 on the reference in multiply.
// All coordinates in REFERENCE pixels (620x336); context scaled x2 once.
// ============================================================================
Stage.begin('Penciler');
Stage.note('Pass 2. Pass 1 was in the right places and drawn badly, so this pass is almost all drawing: widen the far ear to its measured 107 right edge and seat it on the head, close every silhouette into one continuous chain, let the right ears run into the skull instead of being capped with a straight line, and replace the starburst grass, the bead-chain leaves and the ray-like whiskers with observed shapes. Also dropping Drawing.drawCompositionGrid and ruling the thirds by hand, because it paints pink power-point dots onto a sheet that is meant to be blue and graphite only.');

const RW = 620, RH = 336, S = 2;
const PENCIL = { ground: '#faf8f2', blue: '#4a90e2', blueSoft: '#5b8db8', graphite: '#444444', graphiteSoft: '#6b6b6b' };

// --- ANCHORS (MEASURED unless marked FITTED) --------------------------------
const ANCHORS = {
    frame: { w: RW, h: RH },
    trunk: {
        left: [[80, 0], [80, 60], [82, 100], [88, 140], [96, 186]],
        right: [[162, 0], [163, 40], [168, 62], [172, 110], [176, 160]],
        visibleRightTo: 62                      // near ear occludes below this
    },
    L: {
        farEar: { c: [56, 134], rx: 46, ry: 66, rot: -24 },   // FITTED to the 4x zoom: right edge 107 at y155
        nearEar: {
            left: [[136, 45], [122, 62], [117, 90], [114, 120], [116, 150], [121, 176]],
            right: [[136, 45], [154, 56], [168, 76], [176, 102], [174, 132], [166, 160], [157, 179]],
            ridge: [[138, 72], [143, 112], [148, 156]]
        },
        cranium: { c: [172, 232], r: 62 },       // FITTED
        muzzle: { c: [248, 262], r: 34 },        // FITTED
        noseTip: [281, 258],
        skullTop: [[100, 190], [120, 183], [140, 179], [157, 179]],
        crown: [[157, 179], [180, 177], [205, 182], [228, 194], [246, 208], [258, 222], [266, 236], [274, 248], [281, 258]],
        jaw: [[281, 258], [277, 272], [270, 285], [255, 302], [228, 314], [196, 320], [160, 323], [130, 331], [122, 336]],
        back: [[70, 186], [84, 200], [96, 215], [100, 232], [103, 250], [108, 265], [113, 282], [118, 300], [119, 320], [117, 336]],
        eye: {
            almond: [[160, 199], [172, 187], [190, 181], [208, 185], [219, 197], [210, 215], [192, 228], [173, 221]],
            pupil: { c: [190, 206], rx: 23, ry: 17, rot: -10 },
            catch: [184, 201]
        },
        brow: [[150, 188], [176, 175], [205, 171], [232, 182]],
        muzzleTop: [[228, 248], [246, 246], [262, 252], [272, 262]],
        chinLobe: [[209, 272], [216, 288], [232, 300], [252, 303]],
        mouthLine: [[231, 266], [243, 279], [257, 292]],
        teeth: [[250, 271], [268, 273], [264, 291], [252, 289]],
        whiskerRoot: [231, 262],
        whiskerEnds: [[152, 240], [148, 260], [156, 280]]
    },
    R: {
        earFar: { left: [[398, 0], [395, 26], [392, 48], [389, 62], [382, 76]], right: [[444, 0], [443, 26], [441, 46], [446, 60], [455, 70]] },
        earNear: { left: [[459, 0], [456, 20], [452, 40], [450, 53], [452, 64]], right: [[525, 0], [512, 24], [501, 41], [494, 53], [489, 68]], ridge: [[473, 6], [466, 30], [462, 51]] },
        cranium: { c: [430, 132], r: 70 },       // FITTED
        muzzle: { c: [398, 188], r: 44 },        // FITTED
        eyeNear: { ring: { c: [443, 118], rx: 26, ry: 40, rot: -6 }, lid: { c: [441, 118], rx: 14, ry: 26, rot: -14 }, pupil: { c: [441, 118], rx: 11, ry: 22, rot: -16 }, catch: [443, 121] },
        eyeFar: { pupil: { c: [369, 114], rx: 8, ry: 15, rot: -8 }, catch: [371, 117] },
        noseBridge: [[378, 168], [390, 163], [404, 168], [410, 178]],
        lowerMuzzle: [[366, 196], [372, 212], [386, 222], [404, 222], [418, 214]],
        mouthLine: [[379, 195], [391, 205], [406, 211]],
        teeth: [[387, 201], [405, 204], [402, 216], [389, 214]],
        faceLeft: [[382, 76], [374, 84], [370, 90], [367, 112], [361, 136], [362, 156], [367, 172], [361, 182], [350, 192], [346, 200]],
        faceRight: [[489, 68], [487, 80], [490, 104], [494, 128], [496, 148], [491, 164], [484, 178], [473, 192], [470, 206]],
        chestLeft: [[346, 200], [335, 224], [320, 248], [303, 272], [289, 284], [274, 296], [261, 308], [249, 320], [241, 336]],
        chestRight: [[470, 206], [477, 236], [480, 260], [480, 284], [475, 296], [471, 308], [461, 320], [455, 336]],
        whiskerRoot: [382, 186],
        whiskerEnds: [[262, 128], [252, 154], [258, 180], [278, 202]],
        whiskerEndsR: [[512, 198], [516, 218], [506, 236]]
    },
    // Straight: dry stems must not go through the smoothing helper.
    stems: [
        [[203, 0], [262, 105], [300, 165], [345, 252]],
        [[70, 0], [64, 50], [58, 105], [54, 142]],
        [[468, 212], [575, 98]],
        [[575, 98], [620, 150]],
        [[500, 224], [620, 252]]
    ],
    // [x, y, length, angle deg] — irregular, off the Notan, not a repeated stamp.
    leaves: [[218, 14, 17, -28], [243, 27, 13, 14], [262, 41, 18, -36], [288, 57, 12, 26],
    [311, 75, 19, -18], [331, 99, 14, 33], [357, 9, 15, -30], [381, 17, 11, 22]],
    grass: [
        { x0: 0, x1: 118, base: 340, lo: 55, hi: 150, n: 26 },
        { x0: 0, x1: 74, base: 300, lo: 40, hi: 96, n: 13 },
        { x0: 228, x1: 344, base: 340, lo: 26, hi: 64, n: 15 },
        { x0: 452, x1: 622, base: 340, lo: 38, hi: 98, n: 21 }
    ]
};

// --- helpers -----------------------------------------------------------------
const canvas = createCanvas(RW * S, RH * S);
const ctx = canvas.getContext('2d');
ctx.fillStyle = PENCIL.ground; ctx.fillRect(0, 0, RW * S, RH * S);
ctx.scale(S, S);
ctx.lineCap = 'round'; ctx.lineJoin = 'round';

// Deterministic jitter: the sheet must render identically every time.
function rnd(i) { const s = Math.sin(i * 127.1 + 11.7) * 43758.5453; return s - Math.floor(s); }
function ellipsePts(cx, cy, rx, ry, rotDeg, n) {
    const a = rotDeg * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a), p = [];
    for (let i = 0; i < n; i++) { const t = i / n * Math.PI * 2, x = rx * Math.cos(t), y = ry * Math.sin(t); p.push([cx + x * ca - y * sa, cy + x * sa + y * ca]); }
    return p;
}
function line(pts, close) {
    ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);
    if (close) ctx.closePath(); ctx.stroke();
}
function smooth(pts, close) {
    if (pts.length < 3) return line(pts, close);
    ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length - 1; i++) {
        const mx = (pts[i][0] + pts[i + 1][0]) / 2, my = (pts[i][1] + pts[i + 1][1]) / 2;
        ctx.quadraticCurveTo(pts[i][0], pts[i][1], mx, my);
    }
    ctx.lineTo(pts[pts.length - 1][0], pts[pts.length - 1][1]);
    if (close) ctx.closePath(); ctx.stroke();
}
function set(col, w, a) { ctx.strokeStyle = col; ctx.lineWidth = w; ctx.globalAlpha = a === undefined ? 1 : a; }
function tick(p, r) { ctx.beginPath(); ctx.moveTo(p[0] - r, p[1]); ctx.lineTo(p[0] + r, p[1]); ctx.moveTo(p[0], p[1] - r); ctx.lineTo(p[0], p[1] + r); ctx.stroke(); }
function arcPts(pts, from, to) { return pts.slice(from, to); }

// --- 1. armature, ruled by hand so the sheet stays blue + graphite ----------
function drawArmature() {
    set(PENCIL.blueSoft, 0.4, 0.45);
    for (let i = 1; i < 3; i++) { line([[RW * i / 3, 0], [RW * i / 3, RH]]); line([[0, RH * i / 3], [RW, RH * i / 3]]); }
    set(PENCIL.blue, 0.5, 0.55);
    for (const p of [[RW / 3, RH / 3], [2 * RW / 3, RH / 3], [RW / 3, 2 * RH / 3], [2 * RW / 3, 2 * RH / 3]]) tick(p, 5);
    set(PENCIL.blueSoft, 0.45, 0.5);
    line([[190, 206], [441, 118]]);                 // eye-to-eye  -19.3 deg
    line([[25, 74], [87, 194]]);                    // far-ear axis +65.8 deg
    line([[346, 200], [241, 336]]);                 // chest edge  +128.5 deg
    ctx.globalAlpha = 1;
}

// --- 2. background: trunk as a mass, straight stems, observed leaves/grass ---
function drawBackground() {
    const T = ANCHORS.trunk;
    set(PENCIL.blue, 0.5, 0.45);                    // full mass, including the occluded part
    smooth(T.left); smooth(T.right);
    set(PENCIL.graphite, 1.6, 1);                   // the part actually visible
    smooth([[80, 0], [80, 40], [80, 58]]);
    smooth([[162, 0], [163, 40], [168, 62]]);
    set(PENCIL.graphite, 1.1, 0.85);
    smooth([[96, 60], [98, 110], [104, 160], [110, 188]]);
    set(PENCIL.graphiteSoft, 0.6, 0.5);             // two bark axes, no value
    line([[104, 0], [106, 56]]); line([[132, 0], [134, 54]]);

    set(PENCIL.graphite, 1.0, 0.8);
    for (const s of ANCHORS.stems) line(s);
    for (const lf of ANCHORS.leaves) {              // pointed lens, individually angled
        const a = lf[3] * Math.PI / 180, L = lf[2], w = L * 0.30;
        const dx = Math.cos(a) * L, dy = Math.sin(a) * L, nx = -Math.sin(a) * w, ny = Math.cos(a) * w;
        ctx.beginPath();
        ctx.moveTo(lf[0], lf[1]);
        ctx.quadraticCurveTo(lf[0] + dx * 0.5 + nx, lf[1] + dy * 0.5 + ny, lf[0] + dx, lf[1] + dy);
        ctx.quadraticCurveTo(lf[0] + dx * 0.5 - nx, lf[1] + dy * 0.5 - ny, lf[0], lf[1]);
        ctx.stroke();
    }

    let k = 0;
    for (const g of ANCHORS.grass) {                // blades from a spread base, never a fan
        for (let i = 0; i < g.n; i++) {
            k++;
            const bx = g.x0 + (g.x1 - g.x0) * (i + rnd(k) * 0.8) / g.n;
            const h = g.lo + (g.hi - g.lo) * rnd(k + 300);
            const lean = (rnd(k + 700) - 0.5) * h * 0.75;
            const by = g.base - rnd(k + 900) * 14;
            set(PENCIL.graphiteSoft, 0.45 + rnd(k + 50) * 0.5, 0.45 + rnd(k + 90) * 0.35);
            ctx.beginPath();
            ctx.moveTo(bx, by);
            ctx.quadraticCurveTo(bx + lean * 0.2, by - h * 0.6, bx + lean, by - h);
            ctx.stroke();
        }
    }
    ctx.globalAlpha = 1;
}

// --- 3. primary volumes ------------------------------------------------------
function drawVolumes() {
    const L = ANCHORS.L, R = ANCHORS.R;
    set(PENCIL.blue, 0.8, 0.85);
    line(ellipsePts(L.cranium.c[0], L.cranium.c[1], L.cranium.r, L.cranium.r, 0, 48), true);
    line(ellipsePts(R.cranium.c[0], R.cranium.c[1], R.cranium.r, R.cranium.r, 0, 48), true);
    set(PENCIL.blue, 0.65, 0.7);
    line(ellipsePts(L.muzzle.c[0], L.muzzle.c[1], L.muzzle.r, L.muzzle.r, 0, 40), true);
    line(ellipsePts(R.muzzle.c[0], R.muzzle.c[1], R.muzzle.r, R.muzzle.r, 0, 40), true);
    line(ellipsePts(L.farEar.c[0], L.farEar.c[1], L.farEar.rx, L.farEar.ry, L.farEar.rot, 48), true);
    set(PENCIL.blueSoft, 0.45, 0.55);
    line([[112, 206], [234, 206]]);                 // L eye line
    line([[362, 118], [502, 118]]);                 // R eye line
    line([L.cranium.c, L.muzzle.c]); line([R.cranium.c, R.muzzle.c]);
    ctx.globalAlpha = 1;
}

// --- 4. LEFT rabbit: one continuous silhouette ------------------------------
function drawLeft() {
    const L = ANCHORS.L;
    const fe = ellipsePts(L.farEar.c[0], L.farEar.c[1], L.farEar.rx, L.farEar.ry, L.farEar.rot, 64);
    set(PENCIL.graphite, 1.6, 1);
    smooth(arcPts(fe, 4, 46));                      // visible arc only; base runs under the head
    smooth([[fe[46][0], fe[46][1]], [78, 192], [70, 186]]);
    smooth(L.back);
    smooth(L.jaw);
    smooth(L.crown);
    set(PENCIL.graphite, 1.2, 0.9);
    smooth(L.nearEar.left); smooth(L.nearEar.right);
    smooth([[121, 176], [138, 181], [157, 179]]);   // ear base seated on the crown
    set(PENCIL.blueSoft, 0.5, 0.6);
    smooth(L.skullTop);                             // hidden behind the far ear, but structural
    set(PENCIL.graphiteSoft, 0.7, 0.8);
    smooth(L.nearEar.ridge);
    smooth([[fe[10][0], fe[10][1]], [62, 128], [72, 172]]); // far-ear inner ridge
    ctx.globalAlpha = 1;
}

// --- 5. RIGHT rabbit: ears run into the skull, no cap line ------------------
function drawRight() {
    const R = ANCHORS.R;
    set(PENCIL.graphite, 1.6, 1);
    smooth(R.earFar.left); smooth(R.earFar.right);
    smooth(R.earNear.left); smooth(R.earNear.right);
    smooth([[455, 70], [452, 64]]);                 // the notch between the ears, not a cap
    smooth(R.faceLeft); smooth(R.faceRight);
    smooth(R.chestLeft); smooth(R.chestRight);
    set(PENCIL.graphiteSoft, 0.7, 0.8);
    smooth(R.earNear.ridge);
    smooth([[418, 12], [414, 40], [412, 58]]);      // far-ear inner ridge
    ctx.globalAlpha = 1;
}

// --- 6. features -------------------------------------------------------------
function wedgeTeeth(q) {                            // tapered, not a box
    ctx.beginPath();
    ctx.moveTo(q[0][0], q[0][1]); ctx.lineTo(q[1][0], q[1][1]);
    ctx.lineTo(q[2][0], q[2][1]); ctx.lineTo(q[3][0], q[3][1]); ctx.closePath(); ctx.stroke();
    ctx.beginPath();                                // the split between the two incisors
    ctx.moveTo((q[0][0] + q[1][0]) / 2, (q[0][1] + q[1][1]) / 2);
    ctx.lineTo((q[2][0] + q[3][0]) / 2, (q[2][1] + q[3][1]) / 2); ctx.stroke();
}
function fan(root, ends, lift) {
    for (const e of ends) {
        ctx.beginPath(); ctx.moveTo(root[0], root[1]);
        ctx.quadraticCurveTo((root[0] + e[0]) / 2, (root[1] + e[1]) / 2 - lift, e[0], e[1]); ctx.stroke();
    }
}
function drawFeatures() {
    const L = ANCHORS.L, R = ANCHORS.R;
    set(PENCIL.graphite, 1.15, 1);
    smooth(L.eye.almond, true);
    smooth(ellipsePts(L.eye.pupil.c[0], L.eye.pupil.c[1], L.eye.pupil.rx, L.eye.pupil.ry, L.eye.pupil.rot, 32), true);
    set(PENCIL.graphiteSoft, 0.75, 0.85); smooth(L.brow);
    set(PENCIL.blue, 0.7, 1); tick(L.eye.catch, 4);

    set(PENCIL.graphite, 1.15, 1);
    smooth(ellipsePts(R.eyeNear.lid.c[0], R.eyeNear.lid.c[1], R.eyeNear.lid.rx, R.eyeNear.lid.ry, R.eyeNear.lid.rot, 32), true);
    smooth(ellipsePts(R.eyeNear.pupil.c[0], R.eyeNear.pupil.c[1], R.eyeNear.pupil.rx, R.eyeNear.pupil.ry, R.eyeNear.pupil.rot, 32), true);
    set(PENCIL.blueSoft, 0.55, 0.65);
    smooth(ellipsePts(R.eyeNear.ring.c[0], R.eyeNear.ring.c[1], R.eyeNear.ring.rx, R.eyeNear.ring.ry, R.eyeNear.ring.rot, 40), true);
    set(PENCIL.blue, 0.7, 1); tick(R.eyeNear.catch, 4);
    set(PENCIL.graphite, 1.05, 1);
    smooth(ellipsePts(R.eyeFar.pupil.c[0], R.eyeFar.pupil.c[1], R.eyeFar.pupil.rx, R.eyeFar.pupil.ry, R.eyeFar.pupil.rot, 28), true);
    set(PENCIL.blue, 0.7, 1); tick(R.eyeFar.catch, 3);

    set(PENCIL.graphite, 1.15, 1);
    smooth(L.muzzleTop); smooth(L.mouthLine); smooth(L.chinLobe);
    wedgeTeeth(L.teeth);
    smooth(R.noseBridge); smooth(R.mouthLine); smooth(R.lowerMuzzle);
    wedgeTeeth(R.teeth);

    set(PENCIL.graphiteSoft, 0.5, 0.7);
    fan(L.whiskerRoot, L.whiskerEnds, 5);
    fan(R.whiskerRoot, R.whiskerEnds, 7);
    fan([440, 190], R.whiskerEndsR, 5);
    ctx.globalAlpha = 1;
}

// --- 7. labels ---------------------------------------------------------------
function drawLabels() {
    ctx.fillStyle = PENCIL.blue; ctx.font = '7px sans-serif'; ctx.globalAlpha = 0.9;
    const labs = [['L.cranium c(172,232) r62', 128, 306], ['L.muzzle c(248,262) r34', 252, 236],
    ['L.farEar rx46 ry66 -24deg', 4, 66], ['L.nearEar tip(136,45)', 96, 38],
    ['R.cranium c(430,132) r70', 366, 258], ['R.muzzle c(398,188) r44', 330, 156],
    ['both ears cropped by frame', 380, 88], ['noseTip(281,258)', 288, 252],
    ['R.eyeFar c(369,114)', 300, 108]];
    for (const l of labs) ctx.fillText(l[0], l[1], l[2]);
    ctx.globalAlpha = 1;
}

drawArmature(); drawBackground(); drawVolumes();
drawLeft(); drawRight(); drawFeatures(); drawLabels();
canvas;