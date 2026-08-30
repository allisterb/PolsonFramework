Stage.begin('Critic');
Stage.note('Verifying, first-hand, the one prior-run defect I already knew about: does ctx.clip(path, fillRule) mutate the fill rule stored on a reusable CanvasPath? A rect with an inner rect added: under nonzero the centre must be INSIDE (painted), under evenodd it must be knocked OUT.');

function mk() {
    const p = new CanvasPath(); p.rect(0, 0, 60, 40);
    const q = new CanvasPath(); q.rect(20, 10, 20, 20); p.addPath(q); return p;
}
// Returns the centre pixel after clipping to p and painting black. 0 = painted, 255 = knocked out.
function centre(p, rule) {
    const cv = createCanvas(60, 40), c2 = cv.getContext('2d');
    c2.fillStyle = '#ffffff'; c2.fillRect(0, 0, 60, 40);
    c2.save();
    if (rule === null) c2.clip(p); else c2.clip(p, rule);
    c2.fillStyle = '#000000'; c2.fillRect(0, 0, 60, 40);
    c2.restore();
    return c2.getImageData(30, 20, 1, 1).data[0];
}
const shared = mk();
const a = centre(shared, 'evenodd');
const b = centre(shared, null);
const c = centre(shared, 'nonzero');
const fresh = centre(mk(), null);
log('same path, evenodd  -> centre ' + a + '  (expect 255, knocked out)');
log('same path, default  -> centre ' + b + '  (expect 0 = nonzero default; 255 means the rule stuck)');
log('same path, nonzero  -> centre ' + c + '  (expect 0)');
log('FRESH path, default -> centre ' + fresh + '  (expect 0 = control)');
log(b === 255 && fresh === 0 ? 'REPRODUCED: clip stores the fill rule on the path.'
    : (b === 0 ? 'NOT reproduced: the default is per-call nonzero as HTML5 requires.' : 'inconclusive'));