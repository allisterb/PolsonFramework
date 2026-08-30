Stage.begin('Penciler');
const img = Skia.Image.load('reference_images/panel1.jpg');
const W = img.width, H = img.height;
const c = createCanvas(W, H); const ctx = c.getContext('2d');
ctx.drawImage(img, 0, 0);
const d = ctx.getImageData(0, 0, W, H).data;
function px(x, y) { const i = (y * W + x) * 4; return [d[i], d[i + 1], d[i + 2]]; }
function lum(p) { return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]; }
const green = p => p[1] > p[0] + 2 && p[1] > p[2] + 8;
// Figure/ground: anything appreciably darker than the wash and not grass.
const isFig = (x, y) => { const p = px(x, y); return lum(p) < 170 && !green(p); };

function runs(pred, y, x0, x1, min) {
    const res = []; let s = -1;
    for (let x = x0; x <= x1; x++) {
        const on = pred(x, y);
        if (on && s < 0) s = x; else if (!on && s >= 0) { if (x - s >= min) res.push(s + '-' + (x - 1)); s = -1; }
    }
    if (s >= 0 && x1 + 1 - s >= min) res.push(s + '-' + x1);
    return res.join(' ');
}

let o = ['RIGHT rabbit + right field, figure runs (x 330-619)'];
for (let y = 0; y < H; y += 8) o.push(('' + y).padStart(3) + ': ' + runs(isFig, y, 330, 619, 5));
o.push('', 'LEFT field (x 0-329), figure runs');
for (let y = 0; y < H; y += 8) o.push(('' + y).padStart(3) + ': ' + runs(isFig, y, 0, 329, 5));
log(o.join('\n'));

// Connected components of near-black, to locate pupils without guessing.
const seen = new Uint8Array(W * H); const blobs = [];
const dark = (x, y) => lum(px(x, y)) < 25;
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (seen[y * W + x] || !dark(x, y)) continue;
    let st = [x, y], n = 0, x0 = x, x1 = x, y0 = y, y1 = y; seen[y * W + x] = 1;
    while (st.length) {
        const cy = st.pop(), cx = st.pop(); n++;
        if (cx < x0) x0 = cx; if (cx > x1) x1 = cx; if (cy < y0) y0 = cy; if (cy > y1) y1 = cy;
        const nb = [cx + 1, cy, cx - 1, cy, cx, cy + 1, cx, cy - 1];
        for (let k = 0; k < 8; k += 2) {
            const nx = nb[k], ny = nb[k + 1];
            if (nx < 0 || ny < 0 || nx >= W || ny >= H || seen[ny * W + nx] || !dark(nx, ny)) continue;
            seen[ny * W + nx] = 1; st.push(nx, ny);
        }
    }
    if (n > 120) blobs.push({ n: n, x0: x0, x1: x1, y0: y0, y1: y1 });
}
blobs.sort((a, b) => b.n - a.n);
log('near-black blobs (area > 120px):\n' + blobs.slice(0, 8).map(b =>
    'area=' + b.n + ' bbox x ' + b.x0 + '-' + b.x1 + ' y ' + b.y0 + '-' + b.y1 +
    ' centre (' + ((b.x0 + b.x1) / 2).toFixed(0) + ',' + ((b.y0 + b.y1) / 2).toFixed(0) + ')' +
    ' w=' + (b.x1 - b.x0 + 1) + ' h=' + (b.y1 - b.y0 + 1)).join('\n'));
c;