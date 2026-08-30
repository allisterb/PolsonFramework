Stage.begin('Penciler');
Stage.note('Verifying the masks before trusting them: fur (warm>52) in blue, neutral-dark trunk in red, near-black in magenta, grass in green, over a desaturated reference. If a mask is wrong I will see it here rather than discover it in the construction.');
const img = Skia.Image.load('reference_images/panel1.jpg');
const RW = img.width, RH = img.height, S = 2;
const src = createCanvas(RW, RH); const sctx = src.getContext('2d');
sctx.drawImage(img, 0, 0);
const d = sctx.getImageData(0, 0, RW, RH).data;

const c = createCanvas(RW * S, RH * S); const ctx = c.getContext('2d');
ctx.colorFilter = Skia.ColorFilter.grayscale ? Skia.ColorFilter.grayscale() : null;
ctx.globalAlpha = 0.45;
ctx.drawImage(img, 0, 0, RW * S, RH * S);
ctx.globalAlpha = 1;
ctx.colorFilter = null;

for (let y = 0; y < RH; y += 2) {
    for (let x = 0; x < RW; x += 2) {
        const i = (y * RW + x) * 4, r = d[i], g = d[i + 1], b = d[i + 2];
        const L = 0.299 * r + 0.587 * g + 0.114 * b, w = r - b;
        let col = null;
        if (L < 25) col = '#ff00ff';
        else if (g > r + 2 && g > b + 8) col = '#00a000';
        else if (w > 52 && L < 180) col = '#1060ff';
        else if (L < 105) col = '#ff2020';
        if (col) { ctx.fillStyle = col; ctx.fillRect(x * S, y * S, S, S); }
    }
}
c;