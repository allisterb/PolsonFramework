// SPIKE step 3: the closed loop, with the detector in it.
//
// For each case: a figure was drawn at a known pose, the render was sent to BlazePose, and the
// landmarks come back embedded below. This maps them to a Polson pose, rebuilds the figure from
// that pose alone, and reports how far the recovery is from the truth.
//
// `DETECTED` is spliced in by the driver, which also prepends `pose_map.js` — so the geometry has
// one implementation, tested here and analytically in `pose_rig.js` rather than written twice.

const ref = reference();
const rows = [];
const rebuilt = [];

for (const d of DETECTED) {
    const truth = d.case;
    const a = jointsFromMediaPipe(d.result, 0.5);
    const got = poseFromJoints(a.joints, ref);

    // Compare only the angles the case actually asked for: an omitted angle falls back to the
    // canon, so demanding it come back as zero would be testing the wrong thing.
    const p = truth.pose || {}, t = truth.tilt || {};
    let maxErr = 0, which = '-', n = 0, sum = 0;
    const check = (label, want, have) => {
        const e = Math.abs(wrap(have - want));
        n++; sum += e;
        if (e > maxErr) { maxErr = e; which = label; }
    };
    if (p.spineDeg !== undefined) check('spineDeg', p.spineDeg, got.spineDeg);
    else check('spineDeg', 0, got.spineDeg);
    for (const k of ['shoulderTiltDeg', 'pelvicTiltDeg'])
        check(k, t[k] !== undefined ? t[k] : (k === 'shoulderTiltDeg' ? -6 : 6), got[k]);

    // A dropped limb is excluded from the error rather than scored against the canon it fell back
    // to: declining to answer and answering wrongly are different outcomes, and averaging them
    // together would hide exactly the distinction the visibility gate exists to make.
    for (const limbName of ['leftArm', 'rightArm', 'leftLeg', 'rightLeg'])
        if (p[limbName] && got[limbName])
            for (const k of Object.keys(p[limbName])) check(limbName + '.' + k, p[limbName][k], got[limbName][k]);

    rows.push({
        case: truth.name,
        'arms/legs flipped': a.armsFlipped + '/' + a.legsFlipped,
        'facing conflict': a.inconsistent,
        dropped: got.dropped.length ? got.dropped.join(' ') : '-',
        'mean err': +(sum / n).toFixed(2),
        'max err': +maxErr.toFixed(2),
        at: which
    });
    rebuilt.push({ truth: truth, got: got });
    if (a.weak.length) log(`  ${truth.name}: low-visibility -> ${a.weak.join(', ')}`);
}
table(rows);

// The picture: truth in graphite, recovered-through-the-detector in red.
const canvas = createCanvas(1000, 520);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe8';
ctx.fillRect(0, 0, 1000, 520);
ctx.textBaseline = 'top';

const cells = Layout.columns(Layout.inset(Layout.rect(0, 0, 1000, 520), 16), rebuilt.length, 8);
for (let i = 0; i < rebuilt.length; i++) {
    const { truth, got } = rebuilt[i], cell = cells[i];
    const opt = {};
    if (truth.tilt) for (const k of Object.keys(truth.tilt)) opt[k] = truth.tilt[k];
    if (truth.pose) opt.pose = truth.pose;

    const trial = Drawing.createMannequinFigure(0, 0, 1000, Object.keys(opt).length ? opt : undefined);
    const e = trial.bounds;
    const s = Math.min(cell.width / e.width, (cell.height - 40) / e.height) * 0.88;
    const ox = cell.x + (cell.width - e.width * s) / 2 - e.x * s, oy = cell.y + 34 - e.y * s;

    const A = Drawing.createMannequinFigure(ox, oy, 1000 * s, Object.keys(opt).length ? opt : undefined);
    const B = Drawing.createMannequinFigure(ox, oy, 1000 * s, {
        shoulderTiltDeg: got.shoulderTiltDeg, pelvicTiltDeg: got.pelvicTiltDeg, pose: got });
    ctx.drawMannequin(A, false, { blueLineColor: '#9aa4b0', graphiteColor: '#2b3038', lineWidth: 4 });
    ctx.drawMannequin(B, false, { blueLineColor: '#e08c84', graphiteColor: '#c9553d', lineWidth: 2 });

    ctx.fillStyle = '#1c2733';
    ctx.font = '600 13px sans-serif';
    ctx.fillText(truth.name, cell.x + 4, cell.y + 6);
    ctx.font = '400 11px sans-serif';
    ctx.fillStyle = '#7a7068';
    ctx.fillText('graphite = truth, red = via detector', cell.x + 4, cell.y + 20);
}
canvas;
