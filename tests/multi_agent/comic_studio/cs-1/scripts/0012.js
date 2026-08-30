Stage.begin('Penciler');
// Canvas is the reference at 2x, so every measured reference coordinate doubles.
const CW = 1240, CH = 672;
const focal = {
    L_eye: [380, 412], R_near_eye: [882, 236], R_far_eye: [738, 228],
    L_nose: [556, 516], R_mouth: [792, 410]
};
const types = ['ruleOfThirds', 'goldenRatio', 'dynamicSymmetry', 'triangle'];
const out = [];
for (const t of types) {
    const g = Drawing.createCompositionGrid(CW, CH, t);
    const keys = [];
    for (const k in g.powerPoints) keys.push(k);
    let tot = 0, det = [];
    for (const f in focal) {
        const p = focal[f];
        let best = 1e9, bk = '';
        for (const k of keys) {
            const q = g.powerPoints[k];
            const dd = Math.hypot(q.x - p[0], q.y - p[1]);
            if (dd < best) { best = dd; bk = k; }
        }
        tot += best; det.push(f + '->' + bk + ' ' + best.toFixed(0));
    }
    out.push(t.padEnd(16) + ' points=' + keys.length + ' [' + keys.join(',') + ']  mean=' +
        (tot / 5).toFixed(1) + 'px\n    ' + det.join('  '));
}
log(out.join('\n'));

// The three axes I measured directly, as angles, for comparison with the armature.
function ang(a, b) { return (Math.atan2(b[1] - a[1], b[0] - a[0]) * 180 / Math.PI).toFixed(1); }
log('\nmeasured axes (canvas coords):');
log('  eye-to-eye, L pupil -> R near pupil : ' + ang(focal.L_eye, focal.R_near_eye) + ' deg');
log('  L far-ear long axis (50,148)->(156,384): ' + ang([50, 148], [156, 384]) + ' deg');
log('  R chest edge (692,400)->(482,664)      : ' + ang([692, 400], [482, 664]) + ' deg');
log('  trunk left edge (160,0)->(190,360)     : ' + ang([160, 0], [190, 360]) + ' deg');