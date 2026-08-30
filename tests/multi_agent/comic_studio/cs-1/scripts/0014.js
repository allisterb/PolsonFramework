// ============================================================================
// cs-1 · Stage 1 · PENCILER — construction sheet for reference_images/panel1.jpg
// Blue non-repro construction + graphite contour. No colour fills anywhere.
// Every coordinate below is in REFERENCE pixels (620x336); the context is scaled
// x2 once, so the measurements go in exactly as they were measured.
// ============================================================================
Stage.begin('Penciler');
Stage.note('Construction pass 1. Armature = ruleOfThirds (mean 40px in reference units to nearest power point, against 84 / 87 / 105 for golden, dynamic symmetry, triangle). Building back-to-front: trunk and stems, then the two cranial volumes, then ears as secondary masses seated on them, then features. Nothing is fitted by eye except the two volumes marked FITTED.');

const RW = 620, RH = 336, S = 2;

const PENCIL = {
    ground: '#faf8f2',
    blue: '#4a90e2',        // primary construction
    blueSoft: '#5b8db8',    // secondary construction / armature
    graphite: '#444444',
    graphiteSoft: '#6b6b6b'
};

// --- ANCHORS -----------------------------------------------------------------
// MEASURED = read off threshold scans or 6x zooms of the reference.
// FITTED   = a constructive volume solved from measured landmarks.
const ANCHORS = {
    frame: { w: RW, h: RH },                                   // MEASURED

    trunk: {                                                   // MEASURED rows 0..96
        left: [[80, 0], [80, 60], [82, 100], [88, 140], [96, 186]],
        right: [[162, 0], [163, 40], [168, 62]],
        occludedBelow: 58                                      // near ear takes over
    },

    // ---- LEFT rabbit: lower, further back, near profile facing right --------
    L: {
        farEar: { c: [51, 133], rx: 38, ry: 65, rot: -24, tip: [25, 74], base: [78, 192] }, // FITTED to rows 78..190
        nearEar: {
            tip: [136, 45],                                    // MEASURED
            left: [[136, 45], [122, 62], [117, 90], [114, 120], [116, 150], [121, 176]],
            right: [[136, 45], [154, 56], [168, 76], [176, 102], [174, 132], [166, 160], [157, 179]],
            ridge: [[138, 72], [143, 112], [148, 156]]
        },
        cranium: { c: [172, 232], r: 62 },                     // FITTED: crown, eye line, back contour
        muzzle: { c: [248, 262], r: 34 },                      // FITTED
        noseTip: [281, 258],                                   // MEASURED (max right edge, y=260)
        eye: {
            almond: [[160, 199], [172, 187], [190, 181], [208, 185], [219, 197], [210, 215], [192, 228], [173, 221]],
            pupil: { c: [190, 206], rx: 23, ry: 17, rot: -10 }, // MEASURED, 6x zoom
            catch: [184, 201]                                   // MEASURED
        },
        brow: [[150, 188], [176, 175], [205, 171], [232, 182]],
        crown: [[157, 179], [180, 177], [205, 182], [228, 194], [246, 208], [258, 222], [266, 236], [274, 248], [281, 258]],
        back: [[84, 200], [96, 215], [100, 232], [103, 250], [108, 265], [113, 282], [118, 300], [119, 320], [117, 336]],
        jaw: [[281, 258], [277, 272], [270, 285], [255, 302], [228, 314], [196, 320], [160, 323], [130, 331]],
        mouthLine: [[231, 266], [243, 279], [257, 292]],
        teeth: [[249, 271], [267, 269], [269, 289], [252, 291]],
        whiskerRoot: [231, 262]
    },

    // ---- RIGHT rabbit: higher, nearer, turned toward the viewer -------------
    R: {
        earFar: {                                              // MEASURED, tip cropped by frame
            left: [[398, 0], [395, 26], [392, 48], [394, 59]],
            right: [[444, 0], [443, 26], [441, 46], [446, 59]]
        },
        earNear: {                                             // MEASURED, tip cropped by frame
            left: [[459, 0], [456, 20], [452, 40], [450, 53]],
            right: [[525, 0], [512, 24], [501, 41], [494, 53]],
            ridge: [[473, 6], [466, 30], [462, 51]]
        },
        cranium: { c: [430, 132], r: 70 },                     // FITTED to head rows 56..192
        muzzle: { c: [398, 188], r: 44 },                      // FITTED
        eyeNear: {
            ring: { c: [443, 118], rx: 26, ry: 40, rot: -6 },
            lid: { c: [441, 118], rx: 14, ry: 26, rot: -14 },
            pupil: { c: [441, 118], rx: 11, ry: 22, rot: -16 }, // MEASURED, 6x zoom
            catch: [443, 121]
        },
        eyeFar: { pupil: { c: [369, 114], rx: 8, ry: 15, rot: -8 }, catch: [371, 117] }, // MEASURED
        nose: [398, 176],
        mouthLine: [[379, 195], [391, 205], [406, 211]],
        teeth: [[386, 200], [406, 203], [404, 217], [388, 214]],
        faceLeft: [[370, 90], [367, 112], [361, 136], [362, 156], [367, 172], [361, 182], [350, 192], [346, 200]],
        faceRight: [[487, 80], [490, 104], [494, 128], [496, 148], [491, 164], [484, 178], [473, 192], [470, 206]],
        chestLeft: [[346, 200], [335, 224], [320, 248], [303, 272], [289, 284], [274, 296], [261, 308], [249, 320], [241, 336]],
        chestRight: [[470, 206], [477, 236], [480, 260], [480, 284], [475, 296], [471, 308], [461, 320], [455, 336]],
        whiskerRoot: [382, 186]
    },

    // Background: directions measured off the Notan; individual strokes blocked.
    stems: [
        [[203, 0], [262, 105], [300, 165], [345, 252]],
        [[70, 0], [64, 50], [58, 105], [54, 142]],
        [[468, 212], [575, 98]],
        [[575, 98], [620, 150]],
        [[500, 224], [620, 252]]
    ],
    leaves: [[218, 12], [246, 24], [268, 38], [292, 52], [318, 74], [338, 98]],
    grassTufts: [[40, 336], [252, 336], [545, 336]]
};

// --- drawing helpers ---------------------------------------------------------
const canvas = createCanvas(RW * S, RH * S);
const ctx = canvas.getContext('2d');
ctx.fillStyle = PENCIL.ground;
ctx.fillRect(0, 0, RW * S, RH * S);
ctx.scale(S, S);
ctx.lineCap = 'round';
ctx.lineJoin = 'round';

// Ellipse as an explicit polyline: no dependence on a path-transform idiom.
function ellipsePts(cx, cy, rx, ry, rotDeg, n) {
    const a = rotDeg * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a), p = [];
    for (let i = 0; i < n; i++) {
        const t = i / n * Math.PI * 2, x = rx * Math.cos(t), y = ry * Math.sin(t);
        p.push([cx + x * ca - y * sa, cy + x * sa + y * ca]);
    }
    return p;
}
function line(pts, close) {
    ctx.beginPath();
    ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);
    if (close) ctx.closePath();
    ctx.stroke();
}
// Organic contours only. Dry stems are straight and must NOT go through this.
function smooth(pts, close) {
    if (pts.length < 3) return line(pts, close);
    ctx.beginPath();
    ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length - 1; i++) {
        const mx = (pts[i][0] + pts[i + 1][0]) / 2, my = (pts[i][1] + pts[i + 1][1]) / 2;
        ctx.quadraticCurveTo(pts[i][0], pts[i][1], mx, my);
    }
    const l = pts.length - 1;
    ctx.lineTo(pts[l][0], pts[l][1]);
    if (close) ctx.closePath();
    ctx.stroke();
}
function set(colour, width, alpha) {
    ctx.strokeStyle = colour; ctx.lineWidth = width; ctx.globalAlpha = alpha === undefined ? 1 : alpha;
}
function tick(p, r) {
    ctx.beginPath(); ctx.moveTo(p[0] - r, p[1]); ctx.lineTo(p[0] + r, p[1]);
    ctx.moveTo(p[0], p[1] - r); ctx.lineTo(p[0], p[1] + r); ctx.stroke();
}

// --- 1. ARMATURE -------------------------------------------------------------
function drawArmature() {
    const grid = Drawing.createCompositionGrid(RW, RH, 'ruleOfThirds');
    Drawing.drawCompositionGrid(ctx, grid, { opacity: 0.28 });
    // The three axes the panel is actually built on, measured directly.
    set(PENCIL.blueSoft, 0.5, 0.5);
    line([[190, 206], [441, 118]]);                      // eye-to-eye, -19.3 deg
    line([ANCHORS.L.farEar.tip, ANCHORS.L.farEar.base]); // far-ear axis, +65.8 deg
    line([[346, 200], [241, 336]]);                      // right chest edge, +128.5 deg
    ctx.globalAlpha = 1;
}

// --- 2. BACKGROUND STRUCTURE -------------------------------------------------
function drawBackground() {
    set(PENCIL.blue, 0.5, 0.55);
    line(ANCHORS.trunk.left);
    line(ANCHORS.trunk.right);
    set(PENCIL.graphite, 1.1, 0.85);
    line([[80, 0], [80, 58]]);        // visible trunk edges, above the ear
    line([[162, 0], [163, 40], [168, 58]]);
    set(PENCIL.graphiteSoft, 0.8, 0.7);
    line([[96, 60], [98, 110], [104, 160], [110, 188]]);  // sliver still visible below

    set(PENCIL.graphite, 1.0, 0.8);
    for (const s of ANCHORS.stems) line(s);               // straight: dry stems
    for (const lf of ANCHORS.leaves) {                    // blocked leaf masses
        ctx.beginPath();
        ctx.moveTo(lf[0] - 7, lf[1] - 3);
        ctx.quadraticCurveTo(lf[0], lf[1] - 8, lf[0] + 8, lf[1] + 2);
        ctx.quadraticCurveTo(lf[0], lf[1] + 4, lf[0] - 7, lf[1] - 3);
        ctx.stroke();
    }
    set(PENCIL.graphiteSoft, 0.7, 0.6);                   // grass tufts, blocked
    for (const g of ANCHORS.grassTufts) {
        for (let i = -5; i <= 5; i++) {
            const spread = i * 11, h = 46 + (i % 3) * 22;
            ctx.beginPath();
            ctx.moveTo(g[0], g[1]);
            ctx.quadraticCurveTo(g[0] + spread * 0.5, g[1] - h * 0.6, g[0] + spread, g[1] - h);
            ctx.stroke();
        }
    }
    ctx.globalAlpha = 1;
}

// --- 3. PRIMARY VOLUMES ------------------------------------------------------
function drawPrimaryVolumes() {
    set(PENCIL.blue, 0.9, 0.9);
    line(ellipsePts(ANCHORS.L.cranium.c[0], ANCHORS.L.cranium.c[1], ANCHORS.L.cranium.r, ANCHORS.L.cranium.r, 0, 48), true);
    line(ellipsePts(ANCHORS.R.cranium.c[0], ANCHORS.R.cranium.c[1], ANCHORS.R.cranium.r, ANCHORS.R.cranium.r, 0, 48), true);
    set(PENCIL.blue, 0.7, 0.75);
    line(ellipsePts(ANCHORS.L.muzzle.c[0], ANCHORS.L.muzzle.c[1], ANCHORS.L.muzzle.r, ANCHORS.L.muzzle.r, 0, 40), true);
    line(ellipsePts(ANCHORS.R.muzzle.c[0], ANCHORS.R.muzzle.c[1], ANCHORS.R.muzzle.r, ANCHORS.R.muzzle.r, 0, 40), true);
    // Centre lines and eye lines, so features are placed on construction not floated.
    set(PENCIL.blueSoft, 0.5, 0.6);
    line([[110, 206], [232, 206]]);                     // L eye line
    line([[365, 118], [500, 118]]);                     // R eye line
    line([ANCHORS.L.cranium.c, ANCHORS.L.muzzle.c]);    // L cranium -> muzzle axis
    line([ANCHORS.R.cranium.c, ANCHORS.R.muzzle.c]);    // R cranium -> muzzle axis
    ctx.globalAlpha = 1;
}

// --- 4. SECONDARY MASSES: ears, seated on the crania --------------------------
function drawEars() {
    const L = ANCHORS.L, R = ANCHORS.R;
    set(PENCIL.blue, 0.7, 0.7);                          // construction ellipse of the far ear
    line(ellipsePts(L.farEar.c[0], L.farEar.c[1], L.farEar.rx, L.farEar.ry, L.farEar.rot, 48), true);
    set(PENCIL.graphite, 1.3, 0.95);                     // and its drawn contour
    smooth(ellipsePts(L.farEar.c[0], L.farEar.c[1], L.farEar.rx, L.farEar.ry, L.farEar.rot, 32), true);

    set(PENCIL.graphite, 1.3, 0.95);
    smooth(L.nearEar.left);
    smooth(L.nearEar.right);
    line([[121, 176], [157, 179]]);                      // ear base, closed onto the crown
    set(PENCIL.graphiteSoft, 0.8, 0.8);
    smooth(L.nearEar.ridge);

    set(PENCIL.graphite, 1.3, 0.95);
    smooth(R.earFar.left); smooth(R.earFar.right);
    line([[394, 59], [446, 59]]);
    smooth(R.earNear.left); smooth(R.earNear.right);
    line([[450, 53], [494, 53]]);
    set(PENCIL.graphiteSoft, 0.8, 0.8);
    smooth(R.earNear.ridge);
    ctx.globalAlpha = 1;
}

// --- 5. SILHOUETTES ----------------------------------------------------------
function drawSilhouettes() {
    const L = ANCHORS.L, R = ANCHORS.R;
    set(PENCIL.graphite, 1.5, 1);
    smooth(L.crown);
    smooth(L.jaw);
    smooth(L.back);
    line([[84, 200], [78, 192]]);                        // neck closed onto the far-ear base
    smooth(R.faceLeft);
    smooth(R.faceRight);
    smooth(R.chestLeft);
    smooth(R.chestRight);
    line([[346, 200], [350, 192]]);
    line([[470, 206], [473, 192]]);
}

// --- 6. FEATURES -------------------------------------------------------------
function drawFeatures() {
    const L = ANCHORS.L, R = ANCHORS.R;

    set(PENCIL.graphite, 1.2, 1);
    smooth(L.eye.almond, true);
    smooth(ellipsePts(L.eye.pupil.c[0], L.eye.pupil.c[1], L.eye.pupil.rx, L.eye.pupil.ry, L.eye.pupil.rot, 32), true);
    set(PENCIL.graphiteSoft, 0.8, 0.9);
    smooth(L.brow);
    set(PENCIL.blue, 0.8, 1); tick(L.eye.catch, 4);

    set(PENCIL.graphite, 1.2, 1);
    smooth(ellipsePts(R.eyeNear.lid.c[0], R.eyeNear.lid.c[1], R.eyeNear.lid.rx, R.eyeNear.lid.ry, R.eyeNear.lid.rot, 32), true);
    smooth(ellipsePts(R.eyeNear.pupil.c[0], R.eyeNear.pupil.c[1], R.eyeNear.pupil.rx, R.eyeNear.pupil.ry, R.eyeNear.pupil.rot, 32), true);
    set(PENCIL.blueSoft, 0.6, 0.7);
    smooth(ellipsePts(R.eyeNear.ring.c[0], R.eyeNear.ring.c[1], R.eyeNear.ring.rx, R.eyeNear.ring.ry, R.eyeNear.ring.rot, 40), true);
    set(PENCIL.blue, 0.8, 1); tick(R.eyeNear.catch, 4);

    set(PENCIL.graphite, 1.1, 1);
    smooth(ellipsePts(R.eyeFar.pupil.c[0], R.eyeFar.pupil.c[1], R.eyeFar.pupil.rx, R.eyeFar.pupil.ry, R.eyeFar.pupil.rot, 28), true);
    set(PENCIL.blue, 0.8, 1); tick(R.eyeFar.catch, 3);

    // Muzzles, mouths, teeth.
    set(PENCIL.graphite, 1.2, 1);
    smooth([[228, 248], [246, 246], [262, 252], [272, 262]]);   // L muzzle top
    smooth(L.mouthLine);
    line(L.teeth, true);
    smooth([[209, 272], [216, 288], [232, 300], [252, 303]]);   // L chin lobe

    smooth([[378, 168], [390, 163], [404, 168], [410, 178]]);   // R nose bridge
    smooth(R.mouthLine);
    line(R.teeth, true);
    smooth([[366, 196], [372, 212], [386, 222], [404, 222], [418, 214]]); // R lower muzzle

    // Whiskers — direction measured off the Notan, individual strokes blocked.
    set(PENCIL.graphiteSoft, 0.6, 0.75);
    const lw = ANCHORS.L.whiskerRoot, rw = ANCHORS.R.whiskerRoot;
    const lends = [[140, 236], [136, 258], [143, 282], [158, 300]];
    for (const e of lends) { ctx.beginPath(); ctx.moveTo(lw[0], lw[1]); ctx.quadraticCurveTo((lw[0] + e[0]) / 2, (lw[1] + e[1]) / 2 - 6, e[0], e[1]); ctx.stroke(); }
    const rends = [[252, 120], [244, 150], [252, 178], [272, 205]];
    for (const e of rends) { ctx.beginPath(); ctx.moveTo(rw[0], rw[1]); ctx.quadraticCurveTo((rw[0] + e[0]) / 2, (rw[1] + e[1]) / 2 - 8, e[0], e[1]); ctx.stroke(); }
    const rends2 = [[520, 196], [524, 218], [512, 240]];
    for (const e of rends2) { ctx.beginPath(); ctx.moveTo(440, 190); ctx.quadraticCurveTo((440 + e[0]) / 2, (190 + e[1]) / 2 - 6, e[0], e[1]); ctx.stroke(); }
    ctx.globalAlpha = 1;
}

// --- 7. LABELS ---------------------------------------------------------------
function drawLabels() {
    ctx.fillStyle = PENCIL.blue;
    ctx.font = '7px sans-serif';
    ctx.globalAlpha = 0.9;
    const labs = [
        ['L.cranium c(172,232) r62', 116, 300], ['L.muzzle c(248,262) r34', 250, 240],
        ['L.farEar 24deg', 6, 70], ['L.nearEar tip(136,45)', 100, 40],
        ['R.cranium c(430,132) r70', 372, 250], ['R.muzzle c(398,188) r44', 340, 168],
        ['ears cropped by frame', 396, 70], ['noseTip(281,258)', 286, 254]
    ];
    for (const l of labs) ctx.fillText(l[0], l[1], l[2]);
    ctx.globalAlpha = 1;
}

drawArmature();
drawBackground();
drawPrimaryVolumes();
drawEars();
drawSilhouettes();
drawFeatures();
drawLabels();

// --- verification, not eyeballing -------------------------------------------
const checks = [
    ['R ear-pair base midpoint over cranium centre', { x: 445, y: 53 }, { x: 430, y: 132 }, 20],
    ['L far-ear base over back-of-neck', { x: 78, y: 192 }, { x: 103, y: 250 }, 20],
    ['trunk left edge, top over bottom', { x: 80, y: 0 }, { x: 96, y: 186 }, 12],
    ['R cranium centre over muzzle centre', { x: 430, y: 132 }, { x: 398, y: 188 }, 20]
];
for (const ch of checks) {
    const r = Drawing.verifyPlumbAlignment(ch[1], ch[2], ch[3]);
    log(ch[0] + ' -> ' + r.message);
}
log('perspective convergence: NOT APPLICABLE — a flat cel background, nothing recedes.');

canvas;