// SPIKE step 2: render one posed mannequin for the detector to read back.
// The case literal (pose, tilt, style) is spliced in by the driver; everything else is fixed so
// the only differences between runs are the ones being tested.
const CASE = __CASE__;

const W = 700, H = 900;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');
ctx.fillStyle = CASE.ground || '#e8e4dc';
ctx.fillRect(0, 0, W, H);

const opt = {};
if (CASE.tilt) for (const k of Object.keys(CASE.tilt)) opt[k] = CASE.tilt[k];
if (CASE.pose) opt.pose = CASE.pose;
const mk = (x, y, h) => Drawing.createMannequinFigure(x, y, h, Object.keys(opt).length ? opt : undefined);

// One trial at a nominal height to get the posed extent, then scale to the frame. A posed figure's
// extent is not its height - a thrown arm reaches far wider than the canon ever does.
const trial = mk(0, 0, 1000);
const e = trial.bounds;
const fill = CASE.fill === undefined ? 0.62 : CASE.fill;
const s = Math.min((W * fill) / e.width, (H * (fill + 0.18)) / e.height);
const fig = mk(W / 2 - (e.x + e.width / 2) * s, (H - e.height * s) / 2 - e.y * s, 1000 * s);

const style = CASE.style || 'solid';
const skin = CASE.skin || '#b9ad99';

if (style === 'wireframe') {
    Drawing.drawMannequinWireframe(ctx, fig, { lineWidth: 3 });
} else if (style === 'solid') {
    // Detached masses: each limb segment is its own box with its own outline.
    Drawing.drawMannequinSolid(ctx, fig, {
        fillColor: skin, shadowColor: '#8d8271', strokeColor: '#3a352c', strokeWidth: 2 });
} else {
    // One continuous contour. `createFigureGeometry` unions every mass, so the gaps between the
    // segments close and the figure gets an actual silhouette rather than a scatter of parts.
    const geo = Drawing.createFigureGeometry(fig, CASE.padding ? { padding: CASE.padding } : undefined);
    ctx.fillStyle = skin;
    ctx.fill(geo.silhouette);
    if (style === 'silhouetteHead' || style === 'silhouetteFace') {
        // The canon's head is a bare egg with no neck join; a real head and neck is what a person
        // detector is looking for at the top of a body.
        const head = Drawing.createHeadForFigure(fig);
        const hg = Drawing.createHeadGeometry(head);
        ctx.fill(hg.silhouette);
        ctx.fill(geo.silhouette.subtract(geo.groups.head));

        if (style === 'silhouetteFace') {
            // Testing one hypothesis: a featureless silhouette gives the detector no cue for which
            // way the figure faces, which is what the arms/legs facing conflict looks like. If
            // that is the cause, features should settle it.
            ctx.save();
            ctx.clip(hg.mass);
            const ink = { inkColor: CASE.ground || '#ffffff' };
            Drawing.drawComicBrow(ctx, head.farBrow, true, ink);
            Drawing.drawComicBrow(ctx, head.nearBrow, false, ink);
            Drawing.drawComicEye(ctx, head.farEye, true, ink);
            Drawing.drawComicEye(ctx, head.nearEye, false, ink);
            Drawing.drawComicNose(ctx, head.noseWedge, ink);
            Drawing.drawComicMouth(ctx, head.mouthGuides, ink);
            ctx.restore();
        }
    }
    if (CASE.outline) {
        ctx.strokeStyle = '#3a352c';
        ctx.lineWidth = 2;
        ctx.stroke(geo.silhouette);
    }
}

log('CASE ' + CASE.name + ' / ' + style);
canvas;
