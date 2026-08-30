Stage.begin('Colorist');
Stage.note('Face detail measurement. The scanline trace missed the fringe: the hair dips into the face as two locks over the brow (interior features, so min/max per row cannot see them). Tracing the hair/skin boundary per COLUMN instead, plus isolating the nose by its darker amber (#E59600 vs #FFCA28 skin) and the mouth by its brown.');

const ref = Skia.Image.load('reference_images/comic1.png');
function px(x, y) {
    const h = ref.getPixel(x, y);
    return { r: parseInt(h.substr(1, 2), 16), g: parseInt(h.substr(3, 2), 16),
             b: parseInt(h.substr(5, 2), 16), a: parseInt(h.substr(7, 2), 16) };
}
const isSkin = p => p.a > 200 && p.r > 200 && p.g > 140 && p.b < 140;

// --- fringe: topmost skin pixel per column ---------------------------------
const fringe = [];
for (let x = 500; x <= 700; x += 4) {
    for (let y = 120; y <= 260; y += 2) {
        if (isSkin(px(x, y))) { fringe.push([x, y]); break; }
    }
}
log('fringe (hair/skin boundary, top of face) = ' + JSON.stringify(fringe));

// --- nose: skin that is markedly darker amber than the flat #FFCA28 --------
let nx0 = 9999, ny0 = 9999, nx1 = -1, ny1 = -1;
for (let y = 195; y <= 240; y += 2) {
    for (let x = 560; x <= 645; x += 2) {
        const p = px(x, y);
        if (p.a > 200 && p.r > 190 && p.g < 185 && p.b < 90) {
            if (x < nx0) nx0 = x; if (y < ny0) ny0 = y;
            if (x > nx1) nx1 = x; if (y > ny1) ny1 = y;
        }
    }
}
log(`nose bbox x[${nx0}..${nx1}] y[${ny0}..${ny1}]  sample=${JSON.stringify(px(600, 216))}`);

// --- mouth: the brown region, per-row extent -------------------------------
const mouth = [];
for (let y = 230; y <= 262; y += 2) {
    let lo = -1, hi = -1;
    for (let x = 560; x <= 645; x += 2) {
        const p = px(x, y);
        if (p.a > 200 && p.r > 100 && p.r < 160 && p.g > 60 && p.g < 120 && p.b > 50 && p.b < 110) {
            if (lo < 0) lo = x; hi = x;
        }
    }
    if (lo > 0) mouth.push([y, lo, hi]);
}
log('mouth rows [y, x0, x1] = ' + JSON.stringify(mouth));

// --- eyes: the dark neutral blobs ------------------------------------------
for (const [name, xa, xb] of [['eyeL', 535, 600], ['eyeR', 605, 670]]) {
    let x0 = 9999, y0 = 9999, x1 = -1, y1 = -1;
    for (let y = 170; y <= 220; y++) {
        for (let x = xa; x <= xb; x++) {
            const p = px(x, y);
            if (p.a > 200 && Math.max(p.r, p.g, p.b) < 110 && Math.abs(p.r - p.b) < 26) {
                if (x < x0) x0 = x; if (y < y0) y0 = y;
                if (x > x1) x1 = x; if (y > y1) y1 = y;
            }
        }
    }
    log(`${name} bbox x[${x0}..${x1}] y[${y0}..${y1}] centre ${(x0+x1)/2},${(y0+y1)/2} rx ${(x1-x0)/2} ry ${(y1-y0)/2}`);
}

// --- neck V: skin below the chin, per row ----------------------------------
const neck = [];
for (let y = 266; y <= 316; y += 4) {
    let lo = -1, hi = -1;
    for (let x = 540; x <= 660; x += 2) if (isSkin(px(x, y))) { if (lo < 0) lo = x; hi = x; }
    if (lo > 0) neck.push([y, lo, hi]);
}
log('neck rows [y, x0, x1] = ' + JSON.stringify(neck));
exit('face detail complete');
