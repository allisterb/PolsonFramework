Stage.begin('Penciler');
const img = Skia.Image.load('reference_images/panel1.jpg');
const W = img.width, H = img.height;
const c = createCanvas(W, H);
const ctx = c.getContext('2d');
ctx.drawImage(img, 0, 0);
const d = ctx.getImageData(0, 0, W, H).data;

function px(x, y) { const i = (y * W + x) * 4; return [d[i], d[i + 1], d[i + 2]]; }
function lum(p) { return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]; }

// Classify a pixel into the panel's five material classes.
// K near-black (trunk core, pupils) | B warm brown fur | O pale ochre (lit ear, muzzle)
// G green grass | . pale wash background
function cls(x, y) {
    const p = px(x, y), L = lum(p), warm = p[0] - p[2];
    if (L < 62) return 'K';
    if (p[1] > p[0] + 4 && p[1] > p[2] + 10) return 'G';
    if (warm > 30 && L < 145) return 'B';
    if (warm > 22 && L < 205) return 'O';
    return '.';
}

// Majority class per 10x10 cell.
let out = [];
for (let y = 0; y < H; y += 10) {
    let row = '';
    for (let x = 0; x < W; x += 10) {
        const t = {};
        for (let yy = y; yy < Math.min(y + 10, H); yy += 2)
            for (let xx = x; xx < Math.min(x + 10, W); xx += 2) {
                const k = cls(xx, yy); t[k] = (t[k] || 0) + 1;
            }
        let best = '.', bn = -1;
        for (const k in t) if (t[k] > bn) { bn = t[k]; best = k; }
        row += best;
    }
    out.push(('' + y).padStart(3, ' ') + '|' + row);
}
log(out.join('\n'));

// Calibration samples, so the thresholds above are checkable rather than asserted.
const probes = [[100, 20, 'trunk top'], [60, 140, 'left far ear'], [150, 220, 'left head fur'],
[185, 200, 'left pupil'], [230, 265, 'left muzzle'], [430, 120, 'right head fur'],
[445, 115, 'right pupil'], [400, 30, 'right ear'], [560, 300, 'grass'], [330, 60, 'sky wash']];
log(probes.map(p => p[2] + ' (' + p[0] + ',' + p[1] + ') rgb=' + px(p[0], p[1]).join(',') +
    ' L=' + lum(px(p[0], p[1])).toFixed(0) + ' cls=' + cls(p[0], p[1])).join('\n'));
c;