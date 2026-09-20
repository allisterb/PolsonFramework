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

