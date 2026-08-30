Stage.begin('Inker');
Stage.note('Inking here means contour precision, because in a flat vector illustration the silhouette edge IS the line. Re-running the edge-delta report against the refined Colorist render to find every contour still off by more than 8px, plus a fine trace of the hair outline — the hair is the most characterful contour in the piece and mine is smoothed where the reference has structure.');

const ref = Skia.Image.load('reference_images/comic1.png');
const mine = Skia.Image.load('artifacts/stage2_colorist.webp');
function scan(img, y) {
    const e = { R: [-1, -1], B: [-1, -1], Y: [-1, -1] };
    for (let x = 0; x < 1024; x += 2) {
        const h = img.getPixel(x, y);
        if (parseInt(h.substr(7, 2), 16) < 128) continue;
        const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
        let c;
        if (r > 200 && g > 140 && b < 140) c = 'Y';
        else if (b > 130 && (b - r) > 40) c = 'B';
        else if (r < 150 && g < 150 && b < 150 && (r - b) < 70) continue;   // hair/eyes/seams: skip
        else c = 'R';
        const t = e[c];
        if (t[0] < 0) t[0] = x;
        t[1] = x;
    }
    return e;
}
log('remaining contour deltas (mine minus reference), threshold 8px:');
for (let y = 32; y <= 1004; y += 12) {
    const a = scan(ref, y), b = scan(mine, y), parts = [];
    for (const k of ['R', 'B', 'Y']) {
        const A = a[k], B = b[k];
        if (A[0] < 0 && B[0] < 0) continue;
        if (A[0] < 0) { parts.push(k + ' spurious ' + B[0] + '-' + B[1]); continue; }
        if (B[0] < 0) { parts.push(k + ' MISSING ref ' + A[0] + '-' + A[1]); continue; }
        const dl = B[0] - A[0], dr = B[1] - A[1];
        if (Math.abs(dl) > 8 || Math.abs(dr) > 8)
            parts.push(k + ' L' + (dl > 0 ? '+' : '') + dl + ' R' + (dr > 0 ? '+' : '') + dr + ' [ref ' + A[0] + '-' + A[1] + ']');
    }
    if (parts.length) log(String(y).padStart(4, ' ') + ' | ' + parts.join('  '));
}

// Fine hair contour — exclude the mouth band (y 232-256) which is the same brown.
log('--- hair outline, 4px rows: leftmost..rightmost hair pixel ---');
const hl = [], hr = [];
for (let y = 60; y <= 280; y += 4) {
    let lo = -1, hi = -1;
    for (let x = 470; x <= 750; x += 2) {
        const h = ref.getPixel(x, y);
        if (parseInt(h.substr(7, 2), 16) < 200) continue;
        const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
        if (r < 150 && g < 150 && b < 150 && (r - b) < 70 && (r - b) > 12) { if (lo < 0) lo = x; hi = x; }
    }
    if (lo > 0) { hl.push([lo, y]); hr.push([hi, y]); }
}
log('hairL=' + JSON.stringify(hl));
log('hairR=' + JSON.stringify(hr));
exit('inker survey complete');
