Stage.begin('Critic');
Stage.note('My belt "fix" made it worse (36.9 -> 40.7), so I am discarding my reasoning about it and tracing the boundary the same way I traced every other shape. Belt red is bright (luminance > 88) and red-dominant; the cape behind it is dark (< 70) and the suit is blue, so the three separate cleanly.');

const ref = Skia.Image.load('reference_images/comic1.png');
function isBelt(x, y) {
    const h = ref.getPixel(x, y);
    if (parseInt(h.substr(7, 2), 16) < 240) return false;
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
    const L = 0.299 * r + 0.587 * g + 0.114 * b;
    return r > 170 && (r - g) > 90 && (r - b) > 90 && L > 88;
}
const rows = [];
for (let y = 468; y <= 560; y += 4) {
    let lo = -1, hi = -1;
    for (let x = 470; x <= 700; x += 2) if (isBelt(x, y)) { if (lo < 0) lo = x; hi = x; }
    rows.push('y=' + y + '  ' + (lo < 0 ? 'none' : lo + '..' + hi));
}
log('BELT extent per row:'); for (const r of rows) log('  ' + r);

const cols = [];
for (let x = 480; x <= 700; x += 10) {
    let lo = -1, hi = -1;
    for (let y = 460; y <= 570; y += 2) if (isBelt(x, y)) { if (lo < 0) lo = y; hi = y; }
    cols.push('x=' + x + '  ' + (lo < 0 ? 'none' : lo + '..' + hi));
}
log('BELT extent per column:'); for (const c of cols) log('  ' + c);

// Buckle
let bx0 = 9999, by0 = 9999, bx1 = -1, by1 = -1;
for (let y = 490; y <= 560; y += 2) {
    for (let x = 550; x <= 640; x += 2) {
        const h = ref.getPixel(x, y);
        if (parseInt(h.substr(7, 2), 16) < 240) continue;
        const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
        if (r > 220 && g > 190 && b < 110) { if (x < bx0) bx0 = x; if (y < by0) by0 = y; if (x > bx1) bx1 = x; if (y > by1) by1 = y; }
    }
}
log(`BUCKLE bbox x[${bx0}..${bx1}] y[${by0}..${by1}]  centre ${(bx0+bx1)/2},${(by0+by1)/2}`);
for (let y = by0; y <= by1; y += 4) {
    let lo = -1, hi = -1;
    for (let x = 550; x <= 640; x += 2) {
        const h = ref.getPixel(x, y);
        if (parseInt(h.substr(7, 2), 16) < 240) continue;
        const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
        if (r > 220 && g > 190 && b < 110) { if (lo < 0) lo = x; hi = x; }
    }
    if (lo > 0) log('  buckle y=' + y + '  ' + lo + '..' + hi);
}
exit('belt traced');
