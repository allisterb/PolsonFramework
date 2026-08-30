Stage.begin('Colorist');
Stage.note('Tracing region contours off the reference: for each mass, the leftmost and rightmost pixel of its colour class per scanline, inside a box that isolates it from its neighbours. These become the fill outlines. Doing it by measurement rather than by drawing curves freehand, because in a flat vector illustration the silhouette IS the drawing — a Bezier that looks right in code and is 15px wide of the reference reads immediately as a different character.');

const ref = Skia.Image.load('reference_images/comic1.png');
function clsAt(x, y) {
    const h = ref.getPixel(x, y);
    if (parseInt(h.substr(7, 2), 16) < 200) return '.';
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
    if (r > 200 && g > 140 && b < 140) return 'Y';
    if (r > 140 && g < 130 && b < 130 && (r - g) > 60) return 'R';
    if (b > 130 && (b - r) > 40) return 'B';
    if (r < 150 && g < 150 && b < 150 && (r - b) < 70) return 'H';
    return '?';
}
// Trace left/right boundaries of `want` per scanline inside a box.
// `also` lets a second class count as part of the same mass (e.g. dark-red '?').
function trace(want, also, x0, y0, x1, y1, dy) {
    const L = [], R = [];
    for (let y = y0; y <= y1; y += dy) {
        let lo = -1, hi = -1;
        for (let x = x0; x <= x1; x += 2) {
            const c = clsAt(x, y);
            if (c === want || (also && c === also)) { if (lo < 0) lo = x; hi = x; }
        }
        if (lo >= 0 && hi - lo >= 6) { L.push([lo, y]); R.push([hi, y]); }
    }
    return { L: L, R: R };
}
const SHAPES = {};
SHAPES.capeLeft   = trace('R', '?', 180, 252, 520, 848, 8);
SHAPES.capeRight  = trace('R', '?', 600, 328, 800, 812, 8);
SHAPES.torso      = trace('B', null, 420, 300, 730, 572, 6);
SHAPES.legL       = trace('B', null, 400, 572, 552, 700, 6);
SHAPES.legR       = trace('B', null, 480, 572, 690, 812, 6);
SHAPES.upperArm   = trace('B', null, 676, 186, 844, 322, 6);
SHAPES.gauntlet   = trace('R', null, 630,  16, 832, 216, 6);
SHAPES.boot       = trace('R', null, 350, 802, 570, 1012, 6);
SHAPES.hair       = trace('H', null, 480,  56, 744, 278, 6);
SHAPES.face       = trace('Y', null, 500, 118, 700, 320, 6);
SHAPES.bentFist   = trace('R', null, 320, 286, 470, 380, 6);
SHAPES.emblem     = trace('Y', null, 548, 334, 696, 416, 4);
SHAPES.belt       = trace('R', null, 470, 488, 700, 556, 4);

for (const k of Object.keys(SHAPES)) {
    const s = SHAPES[k];
    log(k + '  L=' + JSON.stringify(s.L));
    log(k + '  R=' + JSON.stringify(s.R));
}
Session['SHAPES'] = JSON.stringify(SHAPES);
exit('contours traced');
