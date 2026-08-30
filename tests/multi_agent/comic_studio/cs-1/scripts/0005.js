Stage.begin('Penciler');
const img = Skia.Image.load('reference_images/panel1.jpg');
const W = img.width, H = img.height;
const c = createCanvas(W, H); const ctx = c.getContext('2d');
ctx.drawImage(img, 0, 0);
const d = ctx.getImageData(0, 0, W, H).data;
function px(x, y) { const i = (y * W + x) * 4; return [d[i], d[i + 1], d[i + 2]]; }
function lum(p) { return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]; }

// Warmth (r-b) separates fur from the neutral trunk: sampled trunk warm~44, left fur warm~86.
function warm(p) { return p[0] - p[2]; }
const isFur = (x, y) => { const p = px(x, y); return warm(p) > 52 && lum(p) < 180; };
const isTrunk = (x, y) => { const p = px(x, y); const L = lum(p); return L < 105 && warm(p) <= 52; };
const isPupil = (x, y) => lum(px(x, y)) < 32;

// Runs of a predicate along a scanline, ignoring specks.
function runs(pred, fixed, horiz, min) {
    const n = horiz ? W : H, res = []; let s = -1;
    for (let i = 0; i < n; i++) {
        const on = horiz ? pred(i, fixed) : pred(fixed, i);
        if (on && s < 0) s = i;
        else if (!on && s >= 0) { if (i - s >= min) res.push(s + '-' + (i - 1)); s = -1; }
    }
    if (s >= 0 && n - s >= min) res.push(s + '-' + (n - 1));
    return res.join(' ');
}

let o = ['FUR runs by row (y: x-intervals)'];
for (let y = 10; y < H; y += 10) o.push(('' + y).padStart(3) + ': ' + runs(isFur, y, true, 6));
o.push('', 'TRUNK runs by row');
for (let y = 10; y <= 320; y += 20) o.push(('' + y).padStart(3) + ': ' + runs(isTrunk, y, true, 6));
o.push('', 'FUR runs by column (x: y-intervals)');
for (let x = 20; x < W; x += 20) o.push(('' + x).padStart(3) + ': ' + runs(isFur, x, false, 6));
log(o.join('\n'));
c;