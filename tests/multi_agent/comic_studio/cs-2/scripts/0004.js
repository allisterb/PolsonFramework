Stage.begin('Penciler');
Stage.note('Measurement pass 3: run-length scanlines every 24px down the whole figure, reporting the x-extent of each colour region per row. This fixes the silhouette edges — cape boundary, limb axes, torso taper — as numbers rather than impressions.');

const ref = Skia.Image.load('reference_images/comic1.png');
function cls(x, y) {
    const h = ref.getPixel(x, y);
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16),
          b = parseInt(h.substr(5, 2), 16), a = parseInt(h.substr(7, 2), 16);
    if (a < 128) return '.';
    if (r > 200 && g > 140 && b < 140) return 'Y';
    if (r > 140 && g < 130 && b < 130 && (r - g) > 60) return 'R';
    if (b > 130 && (b - r) > 40) return 'B';
    if (Math.max(r, g, b) < 150 && (r - b) < 70) return 'H';
    return '?';
}

// Run-length encode a scanline at 2px resolution, keeping runs of at least 10px.
function rle(y) {
    const runs = [];
    let cur = null;
    for (let x = 0; x < 1024; x += 2) {
        const c = cls(x, y);
        if (cur && cur.c === c) { cur.x1 = x; }
        else { if (cur) runs.push(cur); cur = { c, x0: x, x1: x }; }
    }
    if (cur) runs.push(cur);
    return runs.filter(r => r.c !== '.' && (r.x1 - r.x0) >= 10)
               .map(r => `${r.c}${r.x0}-${r.x1}`).join(' ');
}
for (let y = 24; y <= 1000; y += 24) log(String(y).padStart(4, ' ') + ' | ' + rle(y));

log('--- vertical scans (columns), same encoding ---');
function rleV(x) {
    const runs = [];
    let cur = null;
    for (let y = 0; y < 1024; y += 2) {
        const c = cls(x, y);
        if (cur && cur.c === c) { cur.y1 = y; }
        else { if (cur) runs.push(cur); cur = { c, y0: y, y1: y }; }
    }
    if (cur) runs.push(cur);
    return runs.filter(r => r.c !== '.' && (r.y1 - r.y0) >= 10)
               .map(r => `${r.c}${r.y0}-${r.y1}`).join(' ');
}
for (const x of [260, 320, 380, 420, 460, 500, 540, 600, 660, 700, 740, 780, 810]) {
    log(String(x).padStart(4, ' ') + ' : ' + rleV(x));
}
exit('scanline pass complete');
