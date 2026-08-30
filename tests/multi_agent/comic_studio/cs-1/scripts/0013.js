Stage.begin('Penciler');
Stage.note('Reading the dry stems and grass as shapes: the panel through Skia.ColorFilter.highContrast turns it into a Notan, which makes the pale stems against the pale wash legible. They are straight, and a smoothing curve would falsify them.');
const img = Skia.Image.load('reference_images/panel1.jpg');
const S = 2, W = img.width * S, H = img.height * S;
const c = createCanvas(W, H); const ctx = c.getContext('2d');
ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, W, H);
ctx.colorFilter = Skia.ColorFilter.highContrast(true, 'none', 0.9);
ctx.drawImage(img, 0, 0, W, H);
ctx.colorFilter = null;
ctx.font = '11px sans-serif'; ctx.lineWidth = 1;
for (let x = 0; x <= img.width; x += 20) {
    const m = x % 100 === 0;
    ctx.strokeStyle = m ? 'rgba(255,0,0,0.8)' : 'rgba(0,120,255,0.25)';
    ctx.beginPath(); ctx.moveTo(x * S, 0); ctx.lineTo(x * S, H); ctx.stroke();
    if (m) { ctx.fillStyle = '#ff0000'; ctx.fillText('' + x, x * S + 3, 13); }
}
for (let y = 0; y <= img.height; y += 20) {
    const m = y % 100 === 0;
    ctx.strokeStyle = m ? 'rgba(255,0,0,0.8)' : 'rgba(0,120,255,0.25)';
    ctx.beginPath(); ctx.moveTo(0, y * S); ctx.lineTo(W, y * S); ctx.stroke();
    if (m) { ctx.fillStyle = '#ff0000'; ctx.fillText('' + y, 3, y * S - 3); }
}
c;