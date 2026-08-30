Stage.begin('Penciler');
const ref = Skia.Image.load('reference_images/panel1.jpg');
const sheet = Skia.Image.load('artifacts/stage1_penciler.webp');
const W = 1240, H = 672;
const c = createCanvas(W, H); const ctx = c.getContext('2d');
ctx.drawImage(ref, 0, 0, W, H);
ctx.globalCompositeOperation = 'multiply';
ctx.drawImage(sheet, 0, 0, W, H);
c;