Stage.begin('Penciler');
Stage.note('Measurement pass 1: classify the reference on an 8px grid into colour regions (skin/yellow, cape red, suit blue, hair brown) and report per-region bounding boxes, centroids and areas, plus a 64x64 ASCII map of the layout. Measuring numerically because eyeballed coordinates are exactly the drift the downstream stages inherit.');

const ref = Skia.Image.load('reference_images/comic1.png');
const W = ref.width, H = ref.height;
const STEP = 8;

// --- classifier -------------------------------------------------------------
// Returns a single-char class code for a #RRGGBBAA sample.
function classify(hex) {
    const r = parseInt(hex.substr(1, 2), 16);
    const g = parseInt(hex.substr(3, 2), 16);
    const b = parseInt(hex.substr(5, 2), 16);
    const a = parseInt(hex.substr(7, 2), 16);
    if (a < 128) return '.';                                   // transparent ground
    if (r > 200 && g > 140 && b < 140) return 'Y';             // skin / emblem yellow
    if (r > 140 && g < 130 && b < 130 && (r - g) > 60) return 'R';  // cape / boots / belt red
    if (b > 130 && (b - r) > 40) return 'B';                   // suit blue
    if (Math.max(r, g, b) < 150 && (r - b) < 70) return 'H';   // hair brown / darkest
    return '?';
}

const classes = ['Y', 'R', 'B', 'H', '?'];
const stats = {};
for (const c of classes) stats[c] = { n: 0, x0: 1e9, y0: 1e9, x1: -1, y1: -1, sx: 0, sy: 0 };
const opaque = { n: 0, x0: 1e9, y0: 1e9, x1: -1, y1: -1 };

const rows = [];
for (let y = 0; y < H; y += STEP) {
    let row = '';
    for (let x = 0; x < W; x += STEP) {
        const c = classify(ref.getPixel(x, y));
        if ((y % 16 === 0) && (x % 16 === 0)) row += c;
        if (c === '.') continue;
        opaque.n++;
        if (x < opaque.x0) opaque.x0 = x;
        if (y < opaque.y0) opaque.y0 = y;
        if (x > opaque.x1) opaque.x1 = x;
        if (y > opaque.y1) opaque.y1 = y;
        const s = stats[c];
        s.n++; s.sx += x; s.sy += y;
        if (x < s.x0) s.x0 = x;
        if (y < s.y0) s.y0 = y;
        if (x > s.x1) s.x1 = x;
        if (y > s.y1) s.y1 = y;
    }
    if (y % 16 === 0) rows.push(row);
}

log(`canvas ${W}x${H}  opaque bbox x[${opaque.x0}..${opaque.x1}] y[${opaque.y0}..${opaque.y1}] samples=${opaque.n}`);
const report = classes.map(c => {
    const s = stats[c];
    return {
        cls: c, n: s.n,
        x0: s.x0, y0: s.y0, x1: s.x1, y1: s.y1,
        w: s.x1 - s.x0, h: s.y1 - s.y0,
        cx: s.n ? Math.round(s.sx / s.n) : -1,
        cy: s.n ? Math.round(s.sy / s.n) : -1
    };
});
table(report);

log('--- 64x64 map (Y=skin R=red B=blue H=hair ?=other .=empty), each cell = 16px ---');
for (let i = 0; i < rows.length; i++) log(String(i * 16).padStart(4, ' ') + ' ' + rows[i]);
exit('measurement pass 1 complete');
