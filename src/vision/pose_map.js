// Detected pose landmarks -> a posed Polson mannequin.
//
// `LM` (the landmark dictionary) and `SRC` (a project-relative image path) are
// PREPENDED by the caller -- `pose_to_mannequin.py` does it -- because the sandbox
// cannot read a JSON file from disk. Run it through that driver rather than
// directly; on its own `LM` is undefined and the script throws on line one.
//
// The mannequin's angle convention, read from ConstructiveDrawingToolkit rather
// than assumed: SegmentAngle is atan2(dy, dx) in SCREEN degrees, so **90 is
// straight down** -- the way gravity and a hanging arm both go. A limb's root
// angle is ABSOLUTE; its bend is RELATIVE to that root.
//
// Three traps, each recorded in docs/internal/pose-to-mannequin.md before this
// existed and each honoured here:
//
//  1. `shoulderTiltDeg`, `pelvicTiltDeg` and `spineOffset` are read from the
//     OPTIONS ROOT. Passed inside `pose` they are silently ignored and the figure
//     builds, renders and looks entirely plausible at the canon's default tilt.
//  2. MediaPipe's `left_*` is the SUBJECT's left; the toolkit's `leftArm` is
//     PAGE-left. Deciding facing once from the shoulders produced a 173 degree
//     pelvic error on a figure standing straight up, because on a featureless
//     silhouette the detector's own labelling comes out internally inconsistent --
//     shoulders one way, hips the other. Ordering each pair by its OWN x is not an
//     approximation of the right answer: given that Polson's left/right is
//     page-space and nothing else, it IS the definition.
//  3. A limb is taken whole or not at all. `PoseLimb` falls back to the canon for
//     any key it is not given, so half a limb comes back as a subtly wrong whole
//     one -- which renders perfectly.

const GATE = 0.5;                        // below this, fall back to the canon
const deg = (a, b) => Math.atan2(b.y - a.y, b.x - a.x) * 180 / Math.PI;

/** The two landmarks ordered by x, so index 0 is always the toolkit's `left`. */
function pair(a, b) {
    const A = LM[a], B = LM[b];
    return A.x <= B.x ? [A, B] : [B, A];
}

const [shL, shR] = pair('leftShoulder', 'rightShoulder');
const [hipL, hipR] = pair('leftHip', 'rightHip');
const shMid = { x: (shL.x + shR.x) / 2, y: (shL.y + shR.y) / 2 };
const hipMid = { x: (hipL.x + hipR.x) / 2, y: (hipL.y + hipR.y) / 2 };

// Scale from the canon: shoulders sit at 1.4H and ankles at 7.7H, so one head
// unit is the span between them over 6.3. Anchored on the PELVIS rather than the
// shoulder midpoint, because it is the pivot the spine lean does not move -- an
// earlier attempt anchored on the shoulders using the canon's constants and put
// the head half a head off the body on any leaning figure.
const ankles = [LM.leftAnkle, LM.rightAnkle].filter(p => p.v >= GATE);
const ankleY = ankles.length
    ? ankles.reduce((s, p) => s + p.y, 0) / ankles.length
    : hipMid.y + (hipMid.y - shMid.y) * 2.2;     // no ankles: estimate from the torso
const headUnit = (ankleY - shMid.y) / 6.3;
const totalHeight = headUnit * 8;
const originY = shMid.y - headUnit * 1.4;

const limbs = {};
const dropped = [];

function limb(name, rootKey, bendKey, anchor, mid, end) {
    const [A, B, C] = [LM[anchor], LM[mid], LM[end]];
    if (Math.min(A.v, B.v, C.v) < GATE) { dropped.push(name); return; }
    const root = deg(A, B);
    limbs[name] = { [rootKey]: root, [bendKey]: deg(B, C) - root };
}

//: Each limb is resolved against its OWN shoulder or hip, so a mirrored subject
//: does not cross its arms over.
const near = shL === LM.leftShoulder;
const nearLeg = hipL === LM.leftHip;
limb('leftArm', 'shoulderDeg', 'elbowDeg',
    near ? 'leftShoulder' : 'rightShoulder', near ? 'leftElbow' : 'rightElbow', near ? 'leftWrist' : 'rightWrist');
limb('rightArm', 'shoulderDeg', 'elbowDeg',
    near ? 'rightShoulder' : 'leftShoulder', near ? 'rightElbow' : 'leftElbow', near ? 'rightWrist' : 'leftWrist');
limb('leftLeg', 'hipDeg', 'kneeDeg',
    nearLeg ? 'leftHip' : 'rightHip', nearLeg ? 'leftKnee' : 'rightKnee', nearLeg ? 'leftAnkle' : 'rightAnkle');
limb('rightLeg', 'hipDeg', 'kneeDeg',
    nearLeg ? 'rightHip' : 'leftHip', nearLeg ? 'rightKnee' : 'leftKnee', nearLeg ? 'rightAnkle' : 'leftAnkle');

// Screen 90 is down, so straight up is -90 and the lean is the departure from it.
const pose = Object.assign({ spineDeg: deg(hipMid, shMid) + 90, neckDeg: 0 }, limbs);
const shoulderTiltDeg = deg(shL, shR);
const pelvicTiltDeg = deg(hipL, hipR);

const r1 = (n) => +n.toFixed(1);
log(`headUnit ${r1(headUnit)}px  totalHeight ${Math.round(totalHeight)}  ` +
    `spineDeg ${r1(pose.spineDeg)}  shoulderTiltDeg ${r1(shoulderTiltDeg)}  pelvicTiltDeg ${r1(pelvicTiltDeg)}`);
for (const k of ['leftArm', 'rightArm', 'leftLeg', 'rightLeg']) {
    log(limbs[k]
        ? `  ${k}: ${JSON.stringify(limbs[k], (_, v) => typeof v === 'number' ? r1(v) : v)}`
        : `  ${k}: DROPPED (a joint below ${GATE}) -- falls back to the canon`);
}
if (dropped.length) {
    log(`${dropped.length} limb(s) dropped: ${dropped.join(', ')}. ` +
        `They render as the canon's default, which looks deliberate. Say so to the caller.`);
}

// --- source, recovery, overlay ------------------------------------------------
const src = Skia.Image.load(SRC);
const canvas = createCanvas(src.width * 3, src.height);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe8';
ctx.fillRect(0, 0, canvas.width, canvas.height);

const figure = () => Drawing.createMannequinFigure(hipMid.x, originY, totalHeight, {
    pose: pose,
    shoulderTiltDeg: shoulderTiltDeg,       // options ROOT, not inside `pose`
    pelvicTiltDeg: pelvicTiltDeg
});

ctx.drawImage(src, 0, 0);

ctx.save();
ctx.translate(src.width, 0);
Drawing.drawMannequinSolid(ctx, figure(), { fillColor: '#cfc6b4', strokeColor: '#15151a', strokeWidth: 2 });
ctx.restore();

ctx.save();
ctx.translate(src.width * 2, 0);
ctx.drawImage(src, 0, 0);
ctx.globalAlpha = 0.55;
Drawing.drawMannequinSolid(ctx, figure(), { fillColor: '#2f8fd0', strokeColor: '#0d3c5c', strokeWidth: 2 });
ctx.restore();

canvas;
