Stage.begin('Penciler');
Stage.note('Looked at stage1 and found two defects. (1) Labels pile up on the head — eyeL/eyeR/eye-line/nose all overprint, so the sheet is unreadable exactly where the measurements are densest; moved to short tags plus a legend. (2) The suit had volumes (ribcage egg, pelvic basin, limb cylinders) but no blocked silhouette, so the Colorist would have had cylinders to fill and no measured torso edge — the taper from a 294px shoulder span to a 150px waist is the single proportion most likely to drift, and it was not on the sheet. Both fixed; anchors unchanged.');

const A = JSON.parse(Session['ANCHORS']);
const BLUE = '#4a90e2', GRAPHITE = '#444444';

const canvas = createCanvas(A.W, A.H);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#faf8f2';
ctx.fillRect(0, 0, A.W, A.H);
ctx.font = '14px Arial';

function smooth(pts, closePath) {
    ctx.beginPath();
    ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length - 1; i++) {
        const mx = (pts[i][0] + pts[i + 1][0]) / 2, my = (pts[i][1] + pts[i + 1][1]) / 2;
        ctx.quadraticCurveTo(pts[i][0], pts[i][1], mx, my);
    }
    ctx.lineTo(pts[pts.length - 1][0], pts[pts.length - 1][1]);
    if (closePath) ctx.closePath();
}
const legend = [];
function cross(x, y, tag, note) {
    const s = 7;
    ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 1.4;
    ctx.beginPath();
    ctx.moveTo(x - s, y); ctx.lineTo(x + s, y);
    ctx.moveTo(x, y - s); ctx.lineTo(x, y + s);
    ctx.stroke();
    if (tag) {
        ctx.fillStyle = '#7a3b12';
        ctx.font = 'bold 13px Arial';
        ctx.fillText(tag, x + 9, y - 4);
        ctx.font = '14px Arial';
        legend.push(`${tag}  ${Math.round(x)},${Math.round(y)}   ${note || ''}`);
    }
}
function joint(x, y, r) {
    ctx.strokeStyle = BLUE; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.ellipse(x, y, r, r, 0, 0, Math.PI * 2); ctx.stroke();
}
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
    ctx.strokeStyle = BLUE; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(q.x, q.y); ctx.stroke();
    joint(p.x, p.y, 7); joint(q.x, q.y, 7);
}

// --- 1. armature -------------------------------------------------------------
Drawing.drawCompositionGrid(ctx, Drawing.createCompositionGrid(A.W, A.H, 'ruleOfThirds'),
                            { lineColor: '#cfe0f0', lineWidth: 1.5, opacity: 1 });
Drawing.drawCompositionGrid(ctx, Drawing.createCompositionGrid(A.W, A.H, 'dynamicSymmetry'),
                            { lineColor: '#e8e1d4', lineWidth: 1, opacity: 1 });
ctx.setLineDash([14, 8]);
ctx.strokeStyle = '#b08050'; ctx.lineWidth = 2.5;
ctx.beginPath();
ctx.moveTo(A.armUp.fist.cx, A.armUp.fist.cy); ctx.lineTo(A.boot.toe.x, A.boot.toe.y); ctx.stroke();
ctx.setLineDash([]);

// --- 2. head ------------------------------------------------------------------
// originY is the head's vertical CENTRE, not the crown — (64+272)/2 = 168.
const loomis = Drawing.createLoomisHead(A.head.cx, (A.head.crownY + A.head.chinY) / 2,
                                        A.head.chinY - A.head.crownY, 0, 0);
Drawing.drawLoomisWireframe(ctx, loomis, { blueLineColor: BLUE, graphiteColor: '#c4bcb0' });
ctx.strokeStyle = BLUE; ctx.lineWidth = 2.2;
ctx.beginPath(); ctx.ellipse(A.head.cx, 168, 92, 104, 0, 0, Math.PI * 2); ctx.stroke();

// Measured feature lines vs the canon. Labels stack to the LEFT, clear of the figure.
ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 1.5;
ctx.setLineDash([9, 5]);
const featureLines = [
    [A.head.crownY, 'crown 64'],
    [A.head.hairlineY, 'hairline 126   canon 110'],
    [A.eyeL.cy, 'EYE LINE 195   canon 172  << 23px low'],
    [A.nose.cy, 'nose 218   canon 210'],
    [244, 'mouth 244   canon 237'],
    [A.head.chinY, 'chin 272']
];
for (const [y, label] of featureLines) {
    ctx.beginPath(); ctx.moveTo(300, y); ctx.lineTo(730, y); ctx.stroke();
    ctx.fillStyle = GRAPHITE;
    ctx.textAlign = 'right';
    ctx.fillText(label, 294, y + 5);
    ctx.textAlign = 'left';
}
ctx.setLineDash([]);
ctx.strokeStyle = BLUE; ctx.lineWidth = 1.6;
ctx.beginPath(); ctx.moveTo(A.head.cx, A.head.crownY - 10); ctx.lineTo(A.head.cx, A.head.neckTipY); ctx.stroke();

ctx.lineWidth = 2;
for (const e of [A.eyeL, A.eyeR]) {
    ctx.strokeStyle = BLUE;
    ctx.beginPath(); ctx.ellipse(e.cx, e.cy, e.r, e.r, 0, 0, Math.PI * 2); ctx.stroke();
}
ctx.beginPath(); ctx.ellipse(A.nose.cx, A.nose.cy, A.nose.rx, A.nose.ry, 0, 0, Math.PI * 2); ctx.stroke();
ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 2.4;
ctx.beginPath();
ctx.moveTo(A.mouth.cx - A.mouth.halfW, A.mouth.cy);
ctx.quadraticCurveTo(A.mouth.cx, A.mouth.cy + A.mouth.sag * 2, A.mouth.cx + A.mouth.halfW, A.mouth.cy);
ctx.stroke();

// Hair mass — wraps the cranium crown-to-jaw, attached, not floating.
ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 2;
ctx.setLineDash([6, 4]);
smooth([[600,64],[652,72],[690,100],[712,140],[720,190],[712,232],[690,262],
        [660,268],[636,250],[618,240],[600,242],[576,240],[560,252],[536,266],
        [510,258],[498,224],[496,180],[506,132],[540,86],[600,64]], true);
ctx.stroke();
ctx.setLineDash([]);

// --- 3. figure armature --------------------------------------------------------
const T = A.torso;
ctx.strokeStyle = BLUE; ctx.lineWidth = 3;
ctx.beginPath();
ctx.moveTo(A.head.cx, A.head.neckTipY);
ctx.bezierCurveTo(600, 400, 578, 480, T.crotch.x, T.crotch.y);
ctx.stroke();
ctx.lineWidth = 2.5;
ctx.beginPath(); ctx.moveTo(T.shoulderL.x, T.shoulderL.y); ctx.lineTo(T.shoulderR.x, T.shoulderR.y); ctx.stroke();
ctx.beginPath(); ctx.moveTo(500, 552); ctx.lineTo(660, 548); ctx.stroke();
ctx.lineWidth = 2.2;
ctx.beginPath(); ctx.ellipse(600, 412, 96, 104, 0, 0, Math.PI * 2); ctx.stroke();
ctx.beginPath(); ctx.ellipse(586, 540, 92, 62, 0, 0, Math.PI * 2); ctx.stroke();

limb(A.armUp.elbow, A.armUp.wrist, A.armUp.gauntletW, A.armUp.gauntletW * 0.8);
limb(A.armUp.shoulder, A.armUp.elbow, A.armUp.upperArmW, A.armUp.upperArmW * 0.85);
limb(A.armBent.shoulder, A.armBent.elbow, A.armBent.upperArmW, A.armBent.upperArmW * 0.9);
limb(A.armBent.elbow, A.armBent.wrist, A.armBent.forearmW, A.armBent.forearmW);
limb(A.legMain.hip, A.legMain.knee, 104, 88);
limb(A.legMain.knee, A.legMain.ankle, 88, 40);
limb(A.legOcc.hip, A.legOcc.knee, 96, 70);
for (const f of [A.armUp.fist, A.armBent.fist]) {
    ctx.strokeStyle = BLUE; ctx.lineWidth = 2.2;
    ctx.beginPath(); ctx.ellipse(f.cx, f.cy, f.rx, f.ry, 0, 0, Math.PI * 2); ctx.stroke();
}

// --- 4. blocked masses ----------------------------------------------------------
ctx.strokeStyle = GRAPHITE; ctx.lineWidth = 2.4;
smooth(A.capeL.concat(A.capeR).concat([[440,300]]), true);
ctx.stroke();

// NEW: the suit silhouette, traced from the blue-region scanlines. This is the
// mass the Colorist fills; the shoulder-to-waist taper is measured, not guessed.
ctx.lineWidth = 2.2;
const suitR = [[716,314],[760,316],[738,336],[718,360],[710,384],[702,408],[696,432],
               [686,456],[678,480],[668,504]];
const suitL = [[466,312],[452,336],[442,360],[432,384],[442,408],[450,432],[442,456]];
smooth(suitR, false); ctx.stroke();
smooth(suitL, false); ctx.stroke();
// hips and both legs
smooth([[450,576],[436,600],[424,624],[418,648],[424,672],[440,690]], false); ctx.stroke();
smooth([[540,600],[524,624],[506,648],[486,672],[462,690]], false); ctx.stroke();
smooth([[550,600],[542,624],[532,648],[524,672],[514,696],[506,720],[496,744],[486,768],[522,792]], false); ctx.stroke();
smooth([[662,576],[656,600],[648,624],[638,648],[624,672],[610,696],[594,720],[580,744],[566,768],[552,792]], false); ctx.stroke();

ctx.lineWidth = 2;
const E = A.emblem;
ctx.beginPath();
ctx.moveTo(E.cx, E.botY);
ctx.bezierCurveTo(E.cx - 56, E.botY - 40, E.cx - 56, E.topY + 4, E.cx - 22, E.topY);
ctx.bezierCurveTo(E.cx - 6, E.topY - 4, E.cx + 6, E.topY - 4, E.cx + 22, E.topY);
ctx.bezierCurveTo(E.cx + 56, E.topY + 4, E.cx + 56, E.botY - 40, E.cx, E.botY);
ctx.closePath(); ctx.stroke();
ctx.beginPath();
ctx.moveTo(488, 496); ctx.lineTo(682, 504); ctx.lineTo(676, 548); ctx.lineTo(470, 544);
ctx.closePath(); ctx.stroke();
const B = A.buckle;
ctx.beginPath();
ctx.moveTo(B.cx, B.cy - B.halfH); ctx.lineTo(B.cx + B.halfW, B.cy);
ctx.lineTo(B.cx, B.cy + B.halfH); ctx.lineTo(B.cx - B.halfW, B.cy);
ctx.closePath(); ctx.stroke();
smooth([[456,810],[448,848],[432,890],[416,924],[396,960],[368,986],[384,1006],
        [412,996],[436,952],[462,906],[492,862],[542,812],[456,810]], true);
ctx.stroke();
// gauntlet walls, wrist to elbow cuff
smooth([[700,90],[718,120],[732,150],[742,180],[746,204]], false); ctx.stroke();
smooth([[756,96],[774,126],[792,156],[808,186],[818,206]], false); ctx.stroke();

// --- 5. anchor tags --------------------------------------------------------------
cross(A.eyeL.cx, A.eyeL.cy, 'e1', 'eye near');
cross(A.eyeR.cx, A.eyeR.cy, 'e2', 'eye far');
cross(A.head.cx, A.head.neckTipY, 'nk', 'neck V tip');
cross(T.shoulderL.x, T.shoulderL.y, 'sL', 'shoulder, bent arm');
cross(T.shoulderR.x, T.shoulderR.y, 'sR', 'shoulder, raised arm');
cross(A.armUp.elbow.x, A.armUp.elbow.y, 'eU', 'elbow raised — gauntlet ends, blue begins');
cross(A.armUp.fist.cx, A.armUp.fist.cy, 'fU', 'raised fist');
cross(A.armBent.elbow.x, A.armBent.elbow.y, 'eB', 'elbow bent — lost in cape shadow below');
cross(A.armBent.fist.cx, A.armBent.fist.cy, 'fB', 'bent fist');
cross(B.cx, B.cy, 'bk', 'buckle diamond');
cross(E.cx, E.topY, 'em', 'emblem top');
cross(A.capeLeftTip.x, A.capeLeftTip.y, 'cL', 'cape left wing tip');
cross(A.capeRightMax.x, A.capeRightMax.y, 'cR', 'cape right max extent');
cross(A.capeTailTip.x, A.capeTailTip.y, 'cT', 'cape tail tip');
cross(A.boot.toe.x, A.boot.toe.y, 'bt', 'boot toe');
cross(A.legMain.knee.x, A.legMain.knee.y, 'k1', 'knee, weight leg');
cross(A.legOcc.knee.x, A.legOcc.knee.y, 'k2', 'knee, occluded leg');

// --- legend ---------------------------------------------------------------------
ctx.fillStyle = '#fdfbf6';
ctx.fillRect(24, 604, 250, 26 + legend.length * 17);
ctx.strokeStyle = '#ccc4b8'; ctx.lineWidth = 1;
ctx.strokeRect(24, 604, 250, 26 + legend.length * 17);
ctx.fillStyle = GRAPHITE;
ctx.font = 'bold 13px Arial';
ctx.fillText('ANCHORS', 34, 622);
ctx.font = '12px Arial';
for (let i = 0; i < legend.length; i++) ctx.fillText(legend[i], 34, 640 + i * 17);

ctx.fillStyle = GRAPHITE;
ctx.font = '16px Arial';
ctx.fillText('STAGE 1 — PENCILER   construction only, no colour   1024x1024, ground transparent in the reference', 30, 1010);
ctx.font = '13px Arial';
ctx.fillStyle = '#8a6030';
ctx.fillText('gesture axis  fist(688,56) -> toe(396,1004),  74 deg — the pose is one long diagonal', 300, 40);

// --- 6. verification ---------------------------------------------------------------
log('plumb crown->crotch: ' + JSON.stringify(
    Drawing.verifyPlumbAlignment({ x: A.head.cx, y: A.head.crownY }, A.torso.crotch, 24)));
log('shoulder span ' + (T.shoulderR.x - T.shoulderL.x) + 'px, waist span ' + (T.waistR - T.waistL) +
    'px, ratio ' + ((T.shoulderR.x - T.shoulderL.x) / (T.waistR - T.waistL)).toFixed(2));
log('head height ' + (A.head.chinY - A.head.crownY) + 'px; figure crown->toe ' +
    (A.boot.toe.y - A.head.crownY) + 'px = ' +
    ((A.boot.toe.y - A.head.crownY) / (A.head.chinY - A.head.crownY)).toFixed(2) + ' heads');
canvas;
