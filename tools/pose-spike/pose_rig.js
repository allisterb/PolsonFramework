// SPIKE: joints -> createMannequinFigure pose.
//
// Two layers, deliberately separate, because they fail for different reasons and conflating them
// would make a bad number impossible to attribute:
//
//   poseFromJoints(j, ref)  - pure geometry. Page-space joints in, Polson pose out. Testable with
//                             no detector at all, which is what `verify` below does.
//   jointsFromMediaPipe(r)  - the adapter. Mirror, visibility, and the naming difference.
//
// SCREEN DEGREES throughout: 90 is straight down, because that is what the toolkit uses and what
// an author reasoning about a drawing reasons in.

function ang(from, to) { return Math.atan2(to.y - from.y, to.x - from.x) * 180 / Math.PI; }
function wrap(d) { while (d > 180) d -= 360; while (d <= -180) d += 360; return d; }
function mid(a, b) { return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }; }

// The canon's own angles, measured off an unposed figure rather than derived from the constants
// that produced it. Same trick `PoseLimb` uses for its fallbacks, and it means spineOffset and the
// default tilts never have to be modelled here - if the canon changes, this follows it.
function reference() {
    const f = Drawing.createMannequinFigure(0, 0, 800);
    return {
        spine: ang(f.pelvis.center, f.sternum),
        neck: ang(f.sternum, f.head.center),
        shoulderLine: ang(f.clavicles.left, f.clavicles.right),
        pelvisLine: ang(f.pelvis.leftHip, f.pelvis.rightHip),
        figure: f
    };
}

function poseFromJoints(j, ref) {
    ref = ref || reference();
    const shoulderMid = mid(j.leftShoulder, j.rightShoulder);
    const hipMid = mid(j.leftHip, j.rightHip);

    // The spine lean rotates the whole upper body about the pelvis, so it is read from the trunk
    // vector and everything above it then has that lean subtracted back out.
    const spineDeg = wrap(ang(hipMid, shoulderMid) - ref.spine);

    // `shoulderTiltDeg` and `pelvicTiltDeg` are ABSOLUTE parameters, not deltas from the canon:
    // the construction lays the shoulder line at exactly `radShoulder`, so the measured line angle
    // IS the parameter. Subtracting the reference angle here double-counted the canon's own -6/+6
    // defaults, which is what the first run of this test caught.
    //
    // The shoulders are then carried by the spine lean (rotating a line by t moves its angle by
    // exactly t), so that much and only that much comes back off. The pelvis is the pivot and is
    // not carried, so it needs no correction at all.
    const shoulderTiltDeg = wrap(ang(j.leftShoulder, j.rightShoulder) - spineDeg);
    const pelvicTiltDeg = wrap(ang(j.leftHip, j.rightHip));

    const pose = { spineDeg: spineDeg, shoulderTiltDeg: shoulderTiltDeg, pelvicTiltDeg: pelvicTiltDeg };

    if (j.head) pose.neckDeg = wrap(ang(shoulderMid, j.head) - ref.neck - spineDeg);

    // Root angle absolute, bend angle relative to the segment above it - which is what a joint is.
    const limb = (root, mid_, tip) => {
        const r = ang(root, mid_);
        return { root: r, bend: wrap(ang(mid_, tip) - r) };
    };

    // A joint the detector could not see is a GUESS, and a guess drawn at full confidence is worse
    // than no pose at all. Omitting the limb is not a loss: `PoseLimb` falls back to the canon's
    // own angle for any key it is not given, so a dropped arm comes out standing rather than
    // broken. Reported in `dropped` so the caller knows which limbs it is not being told about.
    const minV = pose_minVisibility === undefined ? 0.5 : pose_minVisibility;
    const seen = p => p.v === undefined || p.v >= minV;
    pose.dropped = [];

    for (const side of ['left', 'right']) {
        const sh = j[side + 'Shoulder'], el = j[side + 'Elbow'], wr = j[side + 'Wrist'];
        if (seen(sh) && seen(el) && seen(wr)) {
            const a = limb(sh, el, wr);
            pose[side + 'Arm'] = { shoulderDeg: a.root, elbowDeg: a.bend };
        } else pose.dropped.push(side + 'Arm');

        const hp = j[side + 'Hip'], kn = j[side + 'Knee'], an = j[side + 'Ankle'];
        if (seen(hp) && seen(kn) && seen(an)) {
            const l = limb(hp, kn, an);
            pose[side + 'Leg'] = { hipDeg: l.root, kneeDeg: l.bend };
        } else pose.dropped.push(side + 'Leg');
    }
    return pose;
}

// Module-level so the signature stays `(joints, ref)` for the analytic test, which feeds in the
// figure's own joints and has no visibility to report.
let pose_minVisibility = 0.5;

// The adapter. Three things it must get right, and every one of them is silent when wrong.
function jointsFromMediaPipe(r, minVisibility) {
    const min = minVisibility === undefined ? 0.5 : minVisibility;
    const L = r.landmarks;

    // MIRROR. MediaPipe's `left_*` is the SUBJECT's left, which is page-RIGHT for a front-facing
    // figure; the toolkit's `leftArm` is page-left (its leftShoulder is laid at originX - span/2).
    //
    // Assuming the subject faces us is wrong the moment they turn away, so the side is decided
    // from the data - but it must be decided PER PAIR, not once for the whole body. Measured: on a
    // featureless dark silhouette the detector has no cue for which way the figure faces, and its
    // own left/right labelling comes out INTERNALLY INCONSISTENT - shoulders labelled one way and
    // hips the other. One flip taken from the shoulders and applied to the hips then reverses the
    // pelvis vector and reports a 173-degree tilt error on a figure standing straight up.
    //
    // Polson's left/right is page-space and nothing else, so ordering each pair by its own x is
    // not an approximation of the right answer - it IS the definition.
    const order = part => L['left' + part].x <= L['right' + part].x ? ['left', 'right'] : ['right', 'left'];
    const armOrder = order('Shoulder'), legOrder = order('Hip');

    const weak = [];
    const j = {};
    const take = (dstSide, srcSide, part) => {
        const p = L[srcSide + part];
        if (p.v < min) weak.push(dstSide + part + ' v=' + p.v.toFixed(2));
        j[dstSide + part] = { x: p.x, y: p.y, v: p.v };
    };
    for (let i = 0; i < 2; i++) {
        const dst = ['left', 'right'][i];
        // An elbow and wrist follow THEIR OWN shoulder, never the page order, or a raised arm gets
        // spliced onto the opposite shoulder.
        for (const part of ['Shoulder', 'Elbow', 'Wrist']) take(dst, armOrder[i], part);
        for (const part of ['Hip', 'Knee', 'Ankle']) take(dst, legOrder[i], part);
    }
    // The head is the ear midpoint rather than the nose: a nose swings with yaw and would read as
    // a neck tilt the figure never had.
    if (L.leftEar.v >= min && L.rightEar.v >= min) j.head = mid(L.leftEar, L.rightEar);

    return {
        joints: j, weak: weak,
        armsFlipped: armOrder[0] === 'right', legsFlipped: legOrder[0] === 'right',
        // The detector disagreeing with itself about which way the body faces. Worth surfacing:
        // it is the signal that the silhouette gave it nothing to go on.
        inconsistent: (armOrder[0] === 'right') !== (legOrder[0] === 'right')
    };
}

// ---------------------------------------------------------------------------------------------
// VERIFY: the mapping alone, with no detector in the loop.
//
// Build a figure at a KNOWN pose, read back the joints it actually placed, run them through the
// mapping, and compare. Any error here is the mapping's - which is exactly what a detector round
// trip could not tell you, because it folds detection error in with it.
// ---------------------------------------------------------------------------------------------
function jointsOfFigure(f) {
    return {
        leftShoulder: f.leftArm.shoulder, leftElbow: f.leftArm.elbow, leftWrist: f.leftArm.wrist,
        rightShoulder: f.rightArm.shoulder, rightElbow: f.rightArm.elbow, rightWrist: f.rightArm.wrist,
        leftHip: f.leftLeg.hip, leftKnee: f.leftLeg.knee, leftAnkle: f.leftLeg.ankle,
        rightHip: f.rightLeg.hip, rightKnee: f.rightLeg.knee, rightAnkle: f.rightLeg.ankle,
        head: f.head.center
    };
}

const ref = reference();

// NOTE the split, which cost this test a run to find: `spineDeg` and `neckDeg` are read from
// `options.pose`, but `shoulderTiltDeg`, `pelvicTiltDeg`, `spineOffset` and `shoulderSpanHeads`
// are read from the options ROOT. A tilt passed inside `pose` is ignored in silence - the figure
// builds, renders and looks plausible at the canon's default tilt. Asserted at the end.
const cases = [
    { name: 'standing (no pose)', pose: null },
    { name: 'lean + tilt', tilt: { shoulderTiltDeg: -14, pelvicTiltDeg: 9 }, pose: { spineDeg: 12 } },
    { name: 'arm thrown up', pose: { leftArm: { shoulderDeg: -60, elbowDeg: -35 } } },
    { name: 'lunge', pose: { spineDeg: 8, leftLeg: { hipDeg: 62, kneeDeg: 30 }, rightLeg: { hipDeg: 108, kneeDeg: -25 },
                             leftArm: { shoulderDeg: 140, elbowDeg: -40 }, rightArm: { shoulderDeg: 35, elbowDeg: 50 } } },
    { name: 'seated-ish', tilt: { pelvicTiltDeg: -4 },
      pose: { spineDeg: -6, leftLeg: { hipDeg: 20, kneeDeg: 70 }, rightLeg: { hipDeg: 18, kneeDeg: 72 } } }
];

// One options object per case, tilts at the root and the pose under `pose`.
function build(c, x, y, h) {
    const o = {};
    if (c.tilt) for (const k of Object.keys(c.tilt)) o[k] = c.tilt[k];
    if (c.pose) o.pose = c.pose;
    return Drawing.createMannequinFigure(x, y, h, Object.keys(o).length ? o : undefined);
}

const rows = [];
let worst = 0, worstWhat = '';
for (const c of cases) {
    const f = build(c, 400, 80, 800);
    const got = poseFromJoints(jointsOfFigure(f), ref);

    // Compare only what the case actually asked for. An omitted angle falls back to the canon, so
    // demanding it come back as zero would be testing the wrong thing.
    let maxErr = 0, which = '';
    const check = (label, want, have) => {
        const e = Math.abs(wrap(have - want));
        if (e > maxErr) { maxErr = e; which = label; }
    };
    const p = c.pose || {}, t = c.tilt || {};
    if (p.spineDeg !== undefined) check('spineDeg', p.spineDeg, got.spineDeg);
    for (const k of ['shoulderTiltDeg', 'pelvicTiltDeg'])
        if (t[k] !== undefined) check(k, t[k], got[k]);
    for (const limbName of ['leftArm', 'rightArm', 'leftLeg', 'rightLeg'])
        if (p[limbName])
            for (const k of Object.keys(p[limbName])) check(limbName + '.' + k, p[limbName][k], got[limbName][k]);

    if (c.pose === null) {
        // The unposed figure must round-trip to "no lean, canon tilts", which is the zero case.
        check('spineDeg', 0, got.spineDeg);
        check('shoulderTiltDeg', -6, got.shoulderTiltDeg);
        check('pelvicTiltDeg', 6, got.pelvicTiltDeg);
    }
    if (maxErr > worst) { worst = maxErr; worstWhat = c.name + ' / ' + which; }
    rows.push({
        case: c.name, 'max err (deg)': +maxErr.toFixed(4), at: which || '-',
        spine: +got.spineDeg.toFixed(2), shTilt: +got.shoulderTiltDeg.toFixed(2),
        pvTilt: +got.pelvicTiltDeg.toFixed(2)
    });
}
table(rows);
log('worst across all cases: ' + worst.toFixed(4) + ' deg at ' + worstWhat);

// The trap, asserted rather than described: a tilt inside `pose` changes nothing and says nothing.
const atRoot = Drawing.createMannequinFigure(0, 0, 800, { shoulderTiltDeg: -14 });
const inPose = Drawing.createMannequinFigure(0, 0, 800, { pose: { shoulderTiltDeg: -14 } });
const plain = Drawing.createMannequinFigure(0, 0, 800);
const lineOf = f => ang(f.clavicles.left, f.clavicles.right);
log('shoulderTiltDeg -14 at options root -> line ' + lineOf(atRoot).toFixed(2) + ' deg');
log('shoulderTiltDeg -14 inside pose     -> line ' + lineOf(inPose).toFixed(2) +
    ' deg (unposed figure reads ' + lineOf(plain).toFixed(2) + ') — silently ignored: ' +
    (Math.abs(lineOf(inPose) - lineOf(plain)) < 1e-4));

// ---------------------------------------------------------------------------------------------
// And the picture: original pose against the pose recovered from its own joints.
// ---------------------------------------------------------------------------------------------
const canvas = createCanvas(1100, 520);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe8';
ctx.fillRect(0, 0, 1100, 520);
ctx.textBaseline = 'top';

const shown = cases.slice(1);
const cells = Layout.columns(Layout.inset(Layout.rect(0, 0, 1100, 520), 16, 16, 16, 16), shown.length, 8);
for (let i = 0; i < shown.length; i++) {
    const c = shown[i], cell = cells[i];
    const f = build(c, 400, 80, 800);
    const back = poseFromJoints(jointsOfFigure(f), ref);

    const e = f.bounds;
    const s = Math.min(cell.width / e.width, (cell.height - 34) / e.height) * 0.9;
    const ox = cell.x + (cell.width - e.width * s) / 2 - e.x * s;
    const oy = cell.y + 30 - e.y * s;

    // Original in graphite, recovered in red over it. A visible red figure is a failed mapping.
    // The recovered one is rebuilt from `back` ALONE - tilts at the root, limbs under `pose` -
    // so it carries nothing over from the original but its size and placement.
    const a = build(c, 400 * s + ox, 80 * s + oy, 800 * s);
    const b = Drawing.createMannequinFigure(400 * s + ox, 80 * s + oy, 800 * s, {
        shoulderTiltDeg: back.shoulderTiltDeg, pelvicTiltDeg: back.pelvicTiltDeg, pose: back });
    ctx.drawMannequin(a, false, { blueLineColor: '#9aa4b0', graphiteColor: '#2b3038', lineWidth: 3 });
    ctx.drawMannequin(b, false, { blueLineColor: '#e08c84', graphiteColor: '#c9553d', lineWidth: 1 });

    ctx.fillStyle = '#1c2733';
    ctx.font = '600 12px sans-serif';
    ctx.fillText(c.name, cell.x + 4, cell.y + 6);
}
canvas;
