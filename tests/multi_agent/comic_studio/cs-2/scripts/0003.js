Stage.begin('Penciler');
Stage.note('Measurement pass 2: a 3px-resolution map of the head region to locate eyes, nose, mouth and the hair/face boundary, plus run-length scanlines across the whole figure to fix silhouette edges. These become the ANCHORS constants every later stage reads.');

const ref = Skia.Image.load('reference_images/comic1.png');
const px = (x, y) => {
    const h = ref.getPixel(x, y);
    return {
        r: parseInt(h.substr(1, 2), 16), g: parseInt(h.substr(3, 2), 16),
        b: parseInt(h.substr(5, 2), 16), a: parseInt(h.substr(7, 2), 16), hex: h
    };
};
const lum = p => 0.299 * p.r + 0.587 * p.g + 0.114 * p.b;

// --- head detail map --------------------------------------------------------
log('--- head region, 3px cells: E=dark neutral (eye) h=brown hair Y=skin m=mouth/dark-red ---');
log('     ' + '5'.padStart(1) + '00      520       540       560       580       600       620       640       660       680       700');
for (let y = 120; y < 290; y += 3) {
    let row = '';
    for (let x = 500; x < 720; x += 3) {
        const p = px(x, y);
        if (p.a < 128) { row += '.'; continue; }
        const L = lum(p);
        if (L < 100) row += (p.r - p.b) < 28 ? 'E' : 'h';
        else if (p.r > 190 && p.g > 130 && p.b < 150) row += 'Y';
        else if (p.r > 120 && (p.r - p.g) > 50) row += 'm';
        else row += '?';
    }
    log(String(y).padStart(4, ' ') + ' ' + row);
}

// sample the actual colours of the features so the palette is measured, not guessed
const named = [
    ['hair', 600, 100], ['face', 600, 200], ['eyeL', 566, 188], ['eyeR', 634, 188],
    ['mouth', 600, 242], ['nose', 600, 218], ['neck', 600, 268]
];
table(named.map(([n, x, y]) => ({ name: n, x, y, hex: px(x, y).hex })));
exit('head map complete');
