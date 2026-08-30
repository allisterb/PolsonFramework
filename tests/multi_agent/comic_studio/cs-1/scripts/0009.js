Stage.begin('Penciler');
const img = Skia.Image.load('reference_images/panel1.jpg');
const RW = img.width, RH = img.height;
const src = createCanvas(RW, RH); const sc = src.getContext('2d');
sc.drawImage(img, 0, 0);
const d = sc.getImageData(0, 0, RW, RH).data;
function px(x, y) { const i = (y * RW + x) * 4; return [d[i], d[i + 1], d[i + 2]]; }
function lum(p) { return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]; }

// Bounding box of near-black inside a window, plus the brightest pixel in it (the catchlight).
function eye(name, x0, y0, x1, y1, th) {
    let ax0 = 1e9, ay0 = 1e9, ax1 = -1, ay1 = -1, n = 0;
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
        if (lum(px(x, y)) >= th) continue;
        n++; if (x < ax0) ax0 = x; if (x > ax1) ax1 = x; if (y < ay0) ay0 = y; if (y > ay1) ay1 = y;
    }
    if (n === 0) { log(name + ': nothing under L<' + th); return; }
    let bx = 0, by = 0, bl = -1;
    for (let y = ay0; y <= ay1; y++) for (let x = ax0; x <= ax1; x++) {
        const L = lum(px(x, y)); if (L > bl) { bl = L; bx = x; by = y; }
    }
    log(name + ': pupil bbox x ' + ax0 + '-' + ax1 + ' y ' + ay0 + '-' + ay1 +
        ' centre (' + ((ax0 + ax1) / 2).toFixed(0) + ',' + ((ay0 + ay1) / 2).toFixed(0) + ')' +
        ' w=' + (ax1 - ax0 + 1) + ' h=' + (ay1 - ay0 + 1) + ' area=' + n +
        ' | catchlight (' + bx + ',' + by + ') L=' + bl.toFixed(0));
}
eye('L rabbit eye', 150, 170, 220, 230, 40);
eye('R rabbit near eye', 412, 88, 478, 152, 40);
eye('R rabbit far eye', 352, 92, 402, 145, 40);

// Lower body: figure runs over a wider window, to catch both chests.
const green = p => p[1] > p[0] + 2 && p[1] > p[2] + 8;
const isFig = (x, y) => { const p = px(x, y); return lum(p) < 172 && !green(p); };
function runs(y, x0, x1, min) {
    const r = []; let s = -1;
    for (let x = x0; x <= x1; x++) {
        const on = isFig(x, y);
        if (on && s < 0) s = x; else if (!on && s >= 0) { if (x - s >= min) r.push(s + '-' + (x - 1)); s = -1; }
    }
    if (s >= 0 && x1 + 1 - s >= min) r.push(s + '-' + x1);
    return r.join(' ');
}
let o = ['lower body, figure runs over x 200-560'];
for (let y = 200; y < RH; y += 6) o.push(('' + y).padStart(3) + ': ' + runs(y, 200, 560, 5));
o.push('', 'trunk region, figure runs over x 40-200 (rows 0-200)');
for (let y = 0; y <= 200; y += 8) o.push(('' + y).padStart(3) + ': ' + runs(y, 40, 200, 4));
log(o.join('\n'));
src;