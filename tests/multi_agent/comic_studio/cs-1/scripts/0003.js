Stage.begin('Penciler');
Stage.note('Measuring the reference before drawing anything. Reference is 620x336; I will construct at 2x (1240x672) so line weight has room, and keep every measured number in reference coordinates until the final scale-up. First probe: a coarse luminance Notan so I can see where the masses actually are rather than where I think they are.');

const img = Skia.Image.load('reference_images/panel1.jpg');
const W = img.width, H = img.height;
const c = createCanvas(W, H);
const ctx = c.getContext('2d');
ctx.drawImage(img, 0, 0);
const id = ctx.getImageData(0, 0, W, H);
const d = id.data;

// Luminance at a reference pixel.
function L(x, y) {
    const i = (y * W + x) * 4;
    return 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
}

log('reference: ' + W + ' x ' + H);

// Coarse Notan: one character per 10x10 cell, darkest-first ramp.
const ramp = '@%#*+=-:. ';
let out = [];
for (let y = 0; y < H; y += 10) {
    let row = '';
    for (let x = 0; x < W; x += 10) {
        let s = 0, n = 0;
        for (let yy = y; yy < Math.min(y + 10, H); yy += 2)
            for (let xx = x; xx < Math.min(x + 10, W); xx += 2) { s += L(xx, yy); n++; }
        const v = s / n;
        row += ramp[Math.min(9, Math.floor(v / 25.6))];
    }
    out.push(('' + y).padStart(3, ' ') + '|' + row);
}
log('col ruler: each char = 10px, so char index i covers x = 10i .. 10i+9');
log(out.join('\n'));
c;