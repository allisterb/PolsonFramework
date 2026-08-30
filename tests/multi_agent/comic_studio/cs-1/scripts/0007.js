Stage.begin('Penciler');
Stage.note('Rendering the reference at 2x with a 20px reference-coordinate grid, so the silhouette numbers from the scans can be checked against what is actually visible. Grid labels are in REFERENCE pixels (620x336), not canvas pixels.');
const img = Skia.Image.load('reference_images/panel1.jpg');
const S = 2, W = img.width * S, H = img.height * S;
const c = createCanvas(W, H); const ctx = c.getContext('2d');
ctx.drawImage(img, 0, 0, W, H);

ctx.lineWidth = 1;
ctx.font = '11px sans-serif';
for (let x = 0; x <= img.width; x += 20) {
    const major = x % 100 === 0;
    ctx.strokeStyle = major ? 'rgba(255,0,0,0.75)' : 'rgba(0,120,255,0.35)';
    ctx.beginPath(); ctx.moveTo(x * S, 0); ctx.lineTo(x * S, H); ctx.stroke();
    if (major) { ctx.fillStyle = '#ff0000'; ctx.fillText('' + x, x * S + 3, 13); }
}
for (let y = 0; y <= img.height; y += 20) {
    const major = y % 100 === 0;
    ctx.strokeStyle = major ? 'rgba(255,0,0,0.75)' : 'rgba(0,120,255,0.35)';
    ctx.beginPath(); ctx.moveTo(0, y * S); ctx.lineTo(W, y * S); ctx.stroke();
    if (major) { ctx.fillStyle = '#ff0000'; ctx.fillText('' + y, 3, y * S - 3); }
}
c;