Stage.begin('Penciler');
Stage.note('Constructing the sheet from the five measurement passes. Canvas 1024x1024 to match the reference exactly (1:1, no rescale). Subject is a flying figure, frontal, arms asymmetric: character-left arm raised overhead, character-right arm bent with fist at chest height. The armature is a standing 8-head canon deliberately NOT used for the limbs, because the pose is airborne and the legs trail; I block limbs from measured joint positions instead and keep the canon only for the head and torso proportion check.');
Stage.note('Divergence from the Loomis canon, measured not assumed: with crown y=64 and chin y=272 the canon puts the eye line at y=172, but the reference has it at y=195 — 11% of head height lower. Nose 210 vs 218, mouth 237 vs 244, hairline 110 vs 126. This is a big-cranium stylisation; every feature sits low in the mask. Downstream stages must use the MEASURED anchors, not the canonical head object, or the face will read as an adult when the reference reads as a stylised one.');

// ===========================================================================
// ANCHORS — every number here was read off the reference, not invented.
// Coordinates are canvas pixels at 1024x1024. Downstream stages read these
// by name; a coordinate buried in a call argument cannot be referred to.
// ===========================================================================
const A = {
    W: 1024, H: 1024,

    head: {
        cx: 600,
        crownY: 64, chinY: 272,          // hair crown -> chin, total 208
        hairL: 496, hairR: 720, hairBotY: 268,
        hairlineY: 126,                   // where skin first appears at centre
        faceWidestY: 210, faceL: 518, faceR: 682,
        neckL: 578, neckR: 622, neckTipY: 310   // yellow collar V bottoms out here
    },
    eyeL:  { cx: 561, cy: 195, r: 11 },
    eyeR:  { cx: 635, cy: 195, r: 11 },
    nose:  { cx: 600, cy: 218, rx: 14, ry: 8 },
    mouth: { cx: 600, cy: 238, halfW: 25, sag: 14 },   // smile arc, corners y=238, base y=252

    torso: {
        shoulderL: { x: 484, y: 322 }, shoulderR: { x: 716, y: 314 },
        sternum: { x: 600, y: 372 },
        waistY: 500, waistL: 490, waistR: 680,
        beltTopY: 494, beltBotY: 548,
        crotch: { x: 540, y: 572 }
    },
    emblem: { cx: 620, topY: 342, botY: 406, halfW: 56, innerHalfW: 30, innerTopY: 356, innerBotY: 398 },
    buckle: { cx: 584, cy: 525, halfW: 17, halfH: 24 },

    // character's LEFT arm — raised overhead (viewer right)
    armUp: {
        fist:  { cx: 688, cy: 56, rx: 40, ry: 36 },
        wrist: { x: 726, y: 96 },
        elbow: { x: 781, y: 202 },
        shoulder: { x: 716, y: 314 },
        gauntletW: 72, upperArmW: 92
    },
    // character's RIGHT arm — bent, fist at chest height (viewer left)
    armBent: {
        fist:  { cx: 362, cy: 334, rx: 32, ry: 40 },
        wrist: { x: 388, y: 372 },
        elbow: { x: 452, y: 466 },
        shoulder: { x: 484, y: 322 },
        forearmW: 62, upperArmW: 54
    },

    // legs: viewer-right leg carries down to the boot; viewer-left leg is
    // occluded by the cape below y=690 and has no visible foot.
    legMain: { hip: { x: 600, y: 566 }, knee: { x: 580, y: 690 }, ankle: { x: 537, y: 806 } },
    legOcc:  { hip: { x: 500, y: 566 }, knee: { x: 462, y: 664 }, lostY: 690 },
    boot: { cuffL: 456, cuffR: 542, cuffY: 810, toe: { x: 396, y: 1004 }, heel: { x: 368, y: 984 } },

    // cape silhouette, left edge top->bottom then right edge bottom->top,
    // sampled every 24px from the reference and kept as measured points.
    capeL: [[440,300],[420,312],[398,360],[354,408],[302,456],[258,480],[208,504],
            [196,528],[246,552],[280,576],[300,600],[310,624],[314,660],[308,720],
            [304,744],[306,768],[318,792],[342,816],[390,840]],
    capeR: [[690,795],[692,768],[700,744],[708,720],[718,696],[726,672],[732,648],
            [740,624],[746,600],[752,576],[756,552],[760,528],[764,504],[766,456],
            [764,432],[760,384],[756,360],[740,332],[700,296]],
    capeTailTip: { x: 390, y: 840 }, capeLeftTip: { x: 196, y: 528 }, capeRightMax: { x: 766, y: 456 }
};

// PALETTE — measured by colour census, handed to the Colorist unaltered.
const P = {
    capeLit: '#e94132', capeMid: '#d63e31', capeDeep: '#a72816', capeShadow: '#6b2d27',
    capeCore: '#532823', capeCollar: '#9b342b',
    suitLit: '#42a5f5', suitMid: '#2c93eb', suitDeep: '#1e88e5', suitShadow: '#3570a0',
    skin: '#ffca28', skinShade: '#e59600',
    hair: '#543930', eye: '#404040', mouth: '#795548',
    gold: '#fdd835', emblemRed: '#d03b3f',
    ground: null   // transparent, as the reference is
};
Session['ANCHORS'] = JSON.stringify(A);
Session['PALETTE'] = JSON.stringify(P);

const BLUE = '#4a90e2', PALE = '#a9c8e8', GRAPHITE = '#444444', LIGHT = '#8a8a8a';

const canvas = createCanvas(A.W, A.H);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f2';
ctx.fillRect(0, 0, A.W, A.H);
ctx.font = '15px Arial';

// --- helpers ---------------------------------------------------------------
// Smooth polyline through measured points (quadratic midpoint interpolation),
// so a traced silhouette reads as a drawn curve rather than a staircase.
function smooth(pts, closePath) {
    ctx.beginPath();
    ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length - 1; i++) {
        const mx = (pts[i][0] + pts[i + 1][0]) / 2, my = (pts[i][1] + pts[i + 1][1]) / 2;
        ctx.quadraticCurveTo(pts[i][0], pts[i][1], mx, my);
    }
    const n = pts.length - 1;
    ctx.lineTo(pts[n][0], pts[n][1]);
    if (closePath) ctx.closePath();
}
function cross(x, y, label, size) {
    const s = size || 7;
    ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 1.4;
    ctx.beginPath();
    ctx.moveTo(x - s, y); ctx.lineTo(x + s, y);
    ctx.moveTo(x, y - s); ctx.lineTo(x, y + s);
    ctx.stroke();
    if (label) { ctx.fillStyle = GRAPHITE; ctx.fillText(label, x + s + 3, y - 3); }
}
function joint(x, y, r) {
    ctx.strokeStyle = BLUE; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.ellipse(x, y, r, r, 0, 0, Math.PI * 2); ctx.stroke();
}
function axis(p, q) {
    ctx.strokeStyle = BLUE; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(q.x, q.y); ctx.stroke();
}
// A limb blocked as a cylinder: axis plus the two contour walls at given width.
function limb(p, q, w0, w1) {
    const dx = q.x - p.x, dy = q.y - p.y, L = Math.hypot(dx, dy);
    const nx = -dy / L, ny = dx / L;
    ctx.strokeStyle = BLUE; ctx.lineWidth = 1.8;
    for (const s of [1, -1]) {
        ctx.beginPath();
        ctx.moveTo(p.x + nx * s * w0 / 2, p.y + ny * s * w0 / 2);
        ctx.lineTo(q.x + nx * s * w1 / 2, q.y + ny * s * w1 / 2);
        ctx.stroke();
    }
    axis(p, q);
    joint(p.x, p.y, 7); joint(q.x, q.y, 7);
}

// --- 1. compositional armature ---------------------------------------------
// The figure is a diagonal: raised fist top-right to boot toe bottom-left.
// That is a dynamic-symmetry read, so both grids go down and get checked.
const thirds = Drawing.createCompositionGrid(A.W, A.H, 'ruleOfThirds');
Drawing.drawCompositionGrid(ctx, thirds, { lineColor: '#cfe0f0', lineWidth: 1.5, opacity: 1 });
const dyn = Drawing.createCompositionGrid(A.W, A.H, 'dynamicSymmetry');
Drawing.drawCompositionGrid(ctx, dyn, { lineColor: '#e6dfd2', lineWidth: 1, opacity: 1 });

// The actual gesture line of the pose, measured fist-to-toe.
ctx.setLineDash([14, 8]);
ctx.strokeStyle = '#b08050'; ctx.lineWidth = 2.5;
ctx.beginPath();
ctx.moveTo(A.armUp.fist.cx, A.armUp.fist.cy);
ctx.lineTo(A.boot.toe.x, A.boot.toe.y);
ctx.stroke();
ctx.setLineDash([]);
ctx.fillStyle = '#8a6030';
ctx.fillText('gesture axis  (688,56) -> (396,1004)   ~74 deg from horizontal', 120, 60);

// --- 2. head construction ---------------------------------------------------
// Canonical Loomis at the measured head box, drawn so the divergence is visible.
// originY is the head's vertical CENTRE, not the crown: centre = (64+272)/2 = 168.
const loomis = Drawing.createLoomisHead(A.head.cx, (A.head.crownY + A.head.chinY) / 2,
                                        A.head.chinY - A.head.crownY, 0, 0);
Drawing.drawLoomisWireframe(ctx, loomis, { blueLineColor: BLUE, graphiteColor: '#b8b0a4' });

// Cranial sphere and jaw mass as observed.
ctx.strokeStyle = BLUE; ctx.lineWidth = 2.2;
ctx.beginPath();
ctx.ellipse(A.head.cx, 168, 92, 104, 0, 0, Math.PI * 2);   // face mask, measured
ctx.stroke();

// Measured feature lines, in graphite, against the canon's blue ones.
ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 1.6;
ctx.setLineDash([9, 5]);
const featureLines = [
    [A.head.hairlineY, 'hairline 126  (canon 110)'],
    [A.eyeL.cy, 'EYE LINE 195  (canon 172 — 23px low)'],
    [A.nose.cy, 'nose 218  (canon 210)'],
    [A.mouth.cy + 6, 'mouth 244  (canon 237)'],
    [A.head.chinY, 'chin 272']
];
for (const [y, label] of featureLines) {
    ctx.beginPath(); ctx.moveTo(470, y); ctx.lineTo(750, y); ctx.stroke();
    ctx.fillStyle = GRAPHITE; ctx.fillText(label, 756, y + 5);
}
ctx.setLineDash([]);
// Vertical centre line of the head.
ctx.strokeStyle = BLUE; ctx.lineWidth = 1.6;
ctx.beginPath(); ctx.moveTo(A.head.cx, A.head.crownY - 10); ctx.lineTo(A.head.cx, A.head.neckTipY); ctx.stroke();

// Feature volumes as measured.
ctx.lineWidth = 2;
for (const e of [A.eyeL, A.eyeR]) {
    ctx.strokeStyle = BLUE;
    ctx.beginPath(); ctx.ellipse(e.cx, e.cy, e.r, e.r, 0, 0, Math.PI * 2); ctx.stroke();
}
ctx.strokeStyle = BLUE;
ctx.beginPath(); ctx.ellipse(A.nose.cx, A.nose.cy, A.nose.rx, A.nose.ry, 0, 0, Math.PI * 2); ctx.stroke();
// Mouth: the smile arc, corners up, base at y=252.
ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 2.4;
ctx.beginPath();
ctx.moveTo(A.mouth.cx - A.mouth.halfW, A.mouth.cy);
ctx.quadraticCurveTo(A.mouth.cx, A.mouth.cy + A.mouth.sag * 2, A.mouth.cx + A.mouth.halfW, A.mouth.cy);
ctx.stroke();

// Hair mass — a secondary volume, so it is blocked as an outline attached to
// the cranium rather than floating: it wraps the skull from crown to jaw.
ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 2;
ctx.setLineDash([6, 4]);
smooth([[600,64],[652,72],[690,100],[712,140],[720,190],[712,232],[690,262],
        [660,268],[636,250],[618,240],[600,242],[576,240],[560,252],[536,266],
        [510,258],[498,224],[496,180],[506,132],[540,86],[600,64]], true);
ctx.stroke();
ctx.setLineDash([]);

// --- 3. figure armature ------------------------------------------------------
// Spine and shoulder/pelvis girdles from measured joints.
const T = A.torso;
ctx.strokeStyle = BLUE; ctx.lineWidth = 3;
ctx.beginPath();
ctx.moveTo(A.head.cx, A.head.neckTipY);
ctx.bezierCurveTo(600, 400, 578, 480, T.crotch.x, T.crotch.y);   // spine, gentle C-curve
ctx.stroke();
ctx.lineWidth = 2.5;
ctx.beginPath(); ctx.moveTo(T.shoulderL.x, T.shoulderL.y); ctx.lineTo(T.shoulderR.x, T.shoulderR.y); ctx.stroke();
ctx.beginPath(); ctx.moveTo(500, 552); ctx.lineTo(660, 548); ctx.stroke();   // pelvic girdle

// Ribcage egg and pelvic basin as primary volumes.
ctx.lineWidth = 2.2;
ctx.beginPath(); ctx.ellipse(600, 412, 96, 104, 0, 0, Math.PI * 2); ctx.stroke();
ctx.beginPath(); ctx.ellipse(586, 540, 92, 62, 0, 0, Math.PI * 2); ctx.stroke();

limb(A.armUp.elbow, { x: A.armUp.wrist.x, y: A.armUp.wrist.y }, A.armUp.gauntletW, A.armUp.gauntletW * 0.8);
limb(A.armUp.shoulder, A.armUp.elbow, A.armUp.upperArmW, A.armUp.upperArmW * 0.85);
limb(A.armBent.shoulder, A.armBent.elbow, A.armBent.upperArmW, A.armBent.upperArmW * 0.9);
limb(A.armBent.elbow, A.armBent.wrist, A.armBent.forearmW, A.armBent.forearmW);
limb(A.legMain.hip, A.legMain.knee, 104, 88);
limb(A.legMain.knee, A.legMain.ankle, 88, 40);
limb(A.legOcc.hip, A.legOcc.knee, 96, 70);

// Fists as spheres, anchored on the wrists — not floating.
for (const f of [A.armUp.fist, A.armBent.fist]) {
    ctx.strokeStyle = BLUE; ctx.lineWidth = 2.2;
    ctx.beginPath(); ctx.ellipse(f.cx, f.cy, f.rx, f.ry, 0, 0, Math.PI * 2); ctx.stroke();
}

// --- 4. blocked masses (graphite outline, no fill) ---------------------------
ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 2.4;
smooth(A.capeL.concat(A.capeR).concat([[440,300]]), true);   // cape silhouette
ctx.stroke();

ctx.lineWidth = 2;
// Emblem: rounded shield, wide top, point at the bottom.
ctx.beginPath();
ctx.moveTo(A.emblem.cx, A.emblem.botY);
ctx.bezierCurveTo(A.emblem.cx - 56, A.emblem.botY - 40, A.emblem.cx - 56, A.emblem.topY + 4, A.emblem.cx - 22, A.emblem.topY);
ctx.bezierCurveTo(A.emblem.cx - 6, A.emblem.topY - 4, A.emblem.cx + 6, A.emblem.topY - 4, A.emblem.cx + 22, A.emblem.topY);
ctx.bezierCurveTo(A.emblem.cx + 56, A.emblem.topY + 4, A.emblem.cx + 56, A.emblem.botY - 40, A.emblem.cx, A.emblem.botY);
ctx.closePath(); ctx.stroke();
// Belt band and buckle diamond.
ctx.beginPath();
ctx.moveTo(488, 496); ctx.lineTo(682, 504); ctx.lineTo(676, 548); ctx.lineTo(470, 544);
ctx.closePath(); ctx.stroke();
const B = A.buckle;
ctx.beginPath();
ctx.moveTo(B.cx, B.cy - B.halfH); ctx.lineTo(B.cx + B.halfW, B.cy);
ctx.lineTo(B.cx, B.cy + B.halfH); ctx.lineTo(B.cx - B.halfW, B.cy);
ctx.closePath(); ctx.stroke();
// Boot: measured band from the cuff down to the pointed toe.
smooth([[456,810],[448,848],[432,890],[416,924],[396,960],[368,986],[384,1006],
        [412,996],[436,952],[462,906],[492,862],[542,812],[456,810]], true);
ctx.stroke();

// --- 5. anchor marks ---------------------------------------------------------
cross(A.eyeL.cx, A.eyeL.cy, 'eyeL 561,195');
cross(A.eyeR.cx, A.eyeR.cy, 'eyeR 635,195');
cross(A.nose.cx, A.nose.cy, null);
cross(A.head.cx, A.head.neckTipY, 'neckTip 600,310');
cross(T.shoulderL.x, T.shoulderL.y, 'shL 484,322');
cross(T.shoulderR.x, T.shoulderR.y, 'shR 716,314');
cross(A.armUp.elbow.x, A.armUp.elbow.y, 'elbowUp 781,202');
cross(A.armBent.elbow.x, A.armBent.elbow.y, 'elbowBent 452,466');
cross(A.armBent.fist.cx, A.armBent.fist.cy, 'fistBent 362,334');
cross(A.armUp.fist.cx, A.armUp.fist.cy, 'fistUp 688,56');
cross(B.cx, B.cy, 'buckle 584,525');
cross(A.emblem.cx, A.emblem.topY, 'emblem top 620,342');
cross(A.capeLeftTip.x, A.capeLeftTip.y, 'cape L tip 196,528');
cross(A.capeRightMax.x, A.capeRightMax.y, 'cape R max 766,456');
cross(A.capeTailTip.x, A.capeTailTip.y, 'cape tail 390,840');
cross(A.boot.toe.x, A.boot.toe.y, 'boot toe 396,1004');
cross(A.legMain.knee.x, A.legMain.knee.y, 'kneeMain');
cross(A.legOcc.knee.x, A.legOcc.knee.y, 'kneeOccluded');

// --- 6. verification with the toolkit's own checks ----------------------------
const plumb = Drawing.verifyPlumbAlignment({ x: A.head.cx, y: A.head.crownY },
                                           { x: T.crotch.x, y: T.crotch.y }, 24);
const relEye = Drawing.computeRelativeDistance(A.head.chinY - A.head.crownY,
                                               { x: A.eyeL.cx, y: A.eyeL.cy },
                                               { x: A.eyeR.cx, y: A.eyeR.cy });
log('plumb crown->crotch: ' + JSON.stringify(plumb));
log('eye separation in head-units: ' + JSON.stringify(relEye));

ctx.fillStyle = GRAPHITE;
ctx.font = '17px Arial';
ctx.fillText('STAGE 1 — PENCILER  |  construction only, no colour  |  1024x1024, transparent ground in the reference', 40, 1006);
canvas;
