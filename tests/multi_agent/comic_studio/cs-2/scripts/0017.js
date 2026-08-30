Stage.begin('Colorist');
Stage.note('Turning the diff into actionable numbers: per scanline and per colour class, the left and right edge in the reference vs in my render, reported only where the gap exceeds 8px. "cape left edge is 14px short at y=520" is a fix; "the cape looks a bit small" is not.');

const ref = Skia.Image.load('reference_images/comic1.png');
const mine = Skia.Image.load('artifacts/stage2_colorist.webp');
function cls(img, x, y) {
    const h = img.getPixel(x, y);
    if (parseInt(h.substr(7, 2), 16) < 128) return '.';
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
    if (r > 200 && g > 140 && b < 140) return 'Y';
    if (r > 140 && g < 130 && b < 130 && (r - g) > 60) return 'R';
    if (b > 130 && (b - r) > 40) return 'B';
    if (r < 150 && g < 150 && b < 150 && (r - b) < 70) return 'H';
    return '?';
}
function edges(img, y, want) {
    let lo = -1, hi = -1;
    for (let x = 0; x < 1024; x += 2) {
        const c = cls(img, x, y);
        if (c === want || (want === 'R' && c === '?')) { if (lo < 0) lo = x; hi = x; }
    }
    return [lo, hi];
}
const rowsOut = [];
for (let y = 32; y <= 1000; y += 16) {
    const parts = [];
    for (const k of ['R', 'B', 'Y', 'H']) {
        const a = edges(ref, y, k), b = edges(mine, y, k);
        if (a[0] < 0 && b[0] < 0) continue;
        if (a[0] < 0) { parts.push(k + ': ref none, mine ' + b[0] + '-' + b[1]); continue; }
        if (b[0] < 0) { parts.push(k + ': MISSING (ref ' + a[0] + '-' + a[1] + ')'); continue; }
        const dl = b[0] - a[0], dr = b[1] - a[1];
        if (Math.abs(dl) > 8 || Math.abs(dr) > 8)
            parts.push(k + ': L' + (dl > 0 ? '+' : '') + dl + ' R' + (dr > 0 ? '+' : '') + dr +
                       '  (ref ' + a[0] + '-' + a[1] + ')');
    }
    if (parts.length) rowsOut.push(String(y).padStart(4, ' ') + ' | ' + parts.join('   '));
}
log('edge deltas, mine minus reference. + means my edge sits further RIGHT than it should.');
for (const r of rowsOut) log(r);
exit('edge report complete');
