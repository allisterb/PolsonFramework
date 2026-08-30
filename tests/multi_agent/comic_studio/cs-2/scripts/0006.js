Stage.begin('Penciler');
Stage.note('Measurement pass 5: colour census of the reference (quantised to 4 bits/channel) to recover the actual palette, plus point probes at the ambiguous boundaries — glove vs cape, the dark band beside the bent arm, gradient endpoints on cape and suit. The Colorist inherits measured colours rather than eyeballed ones.');

const ref = Skia.Image.load('reference_images/comic1.png');
const parse = h => ({
    r: parseInt(h.substr(1, 2), 16), g: parseInt(h.substr(3, 2), 16),
    b: parseInt(h.substr(5, 2), 16), a: parseInt(h.substr(7, 2), 16)
});

// --- census: quantise to 4 bits per channel, count populations ---------------
const bins = new Map();
for (let y = 0; y < 1024; y += 4) {
    for (let x = 0; x < 1024; x += 4) {
        const p = parse(ref.getPixel(x, y));
        if (p.a < 200) continue;
        const key = ((p.r >> 4) << 8) | ((p.g >> 4) << 4) | (p.b >> 4);
        const e = bins.get(key);
        if (e) { e.n++; e.r += p.r; e.g += p.g; e.b += p.b; }
        else bins.set(key, { n: 1, r: p.r, g: p.g, b: p.b });
    }
}
const hex2 = v => Math.round(v).toString(16).padStart(2, '0');
const top = Array.from(bins.values())
    .sort((a, b) => b.n - a.n).slice(0, 22)
    .map(e => ({
        n: e.n,
        pct: (100 * e.n / 65536 * 16).toFixed(1),
        mean: '#' + hex2(e.r / e.n) + hex2(e.g / e.n) + hex2(e.b / e.n)
    }));
log('--- top colours by area (mean of each 4-bit bin) ---');
table(top);

// --- boundary probes --------------------------------------------------------
const probes = [
    ['glove-fist core', 364, 334], ['just below fist', 364, 380], ['cape near fist', 410, 400],
    ['dark band by arm', 480, 450], ['dark band 2', 470, 490], ['cape mid-left', 300, 520],
    ['cape far left tip', 210, 520], ['cape right lobe', 770, 480], ['cape lower', 400, 750],
    ['cape tail tip', 388, 830], ['boot mid', 460, 900], ['boot toe', 400, 990],
    ['suit chest lit', 560, 420], ['suit chest dark', 700, 430], ['suit leg lit', 470, 640],
    ['suit leg dark', 620, 640], ['upper arm blue', 760, 250], ['gauntlet upper', 690, 60],
    ['gauntlet lower', 780, 180], ['belt', 640, 520], ['buckle', 585, 526],
    ['emblem yellow', 570, 360], ['emblem red', 620, 375], ['hair lit', 560, 110],
    ['hair dark', 660, 110], ['neck', 600, 290], ['collar red', 500, 285]
];
table(probes.map(([n, x, y]) => ({ name: n, x, y, hex: ref.getPixel(x, y) })));
exit('palette census complete');
