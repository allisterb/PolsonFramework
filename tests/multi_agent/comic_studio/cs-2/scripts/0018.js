Stage.begin('Colorist');
Stage.note('Statement cap again — I was scanning each row four times per image, once per colour class. Collecting all four classes in a single pass per row cuts it 4x. Worth noting as a pattern: the cap bites on the naive loop shape, not on the work itself.');

const ref = Skia.Image.load('reference_images/comic1.png');
const mine = Skia.Image.load('artifacts/stage2_colorist.webp');
// One pass per row per image, collecting min/max x for every class at once.
function scan(img, y) {
    const e = { R: [-1, -1], B: [-1, -1], Y: [-1, -1], H: [-1, -1] };
    for (let x = 0; x < 1024; x += 2) {
        const h = img.getPixel(x, y);
        if (parseInt(h.substr(7, 2), 16) < 128) continue;
        const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
        let c;
        if (r > 200 && g > 140 && b < 140) c = 'Y';
        else if (r > 140 && g < 130 && b < 130 && (r - g) > 60) c = 'R';
        else if (b > 130 && (b - r) > 40) c = 'B';
        else if (r < 150 && g < 150 && b < 150 && (r - b) < 70) c = 'H';
        else c = 'R';                              // dark-red transitions count as cape
        const t = e[c];
        if (t[0] < 0) t[0] = x;
        t[1] = x;
    }
    return e;
}
log('edge deltas, mine minus reference. + means my edge sits further RIGHT than it should.');
for (let y = 32; y <= 1000; y += 16) {
    const a = scan(ref, y), b = scan(mine, y), parts = [];
    for (const k of ['R', 'B', 'Y', 'H']) {
        const A = a[k], B = b[k];
        if (A[0] < 0 && B[0] < 0) continue;
        if (A[0] < 0) { parts.push(k + ': spurious ' + B[0] + '-' + B[1]); continue; }
        if (B[0] < 0) { parts.push(k + ': MISSING ref ' + A[0] + '-' + A[1]); continue; }
        const dl = B[0] - A[0], dr = B[1] - A[1];
        if (Math.abs(dl) > 8 || Math.abs(dr) > 8)
            parts.push(k + ' L' + (dl > 0 ? '+' : '') + dl + ' R' + (dr > 0 ? '+' : '') + dr + ' [ref ' + A[0] + '-' + A[1] + ']');
    }
    if (parts.length) log(String(y).padStart(4, ' ') + ' | ' + parts.join('  '));
}
exit('edge report complete');
