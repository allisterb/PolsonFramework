Stage.begin('Penciler');
Stage.note('Threshold scans saturated on the eyes because the ink outlines are as dark as the pupils. Zooming 6x on the four feature clusters and reading them by eye against a 10px reference grid instead.');
const img = Skia.Image.load('reference_images/panel1.jpg');
const Z = 6;
const panels = [
    { t: 'L rabbit eye', sx: 145, sy: 165, sw: 90, sh: 70, dx: 10, dy: 34 },
    { t: 'R rabbit near eye', sx: 405, sy: 82, sw: 90, sh: 70, dx: 570, dy: 34 },
    { t: 'R far eye + brow', sx: 350, sy: 88, sw: 90, sh: 70, dx: 10, dy: 494 },
    { t: 'R muzzle + mouth', sx: 360, sy: 160, sw: 90, sh: 70, dx: 570, dy: 494 }
];
const c = createCanvas(1120, 924); const ctx = c.getContext('2d');
ctx.fillStyle = '#101014'; ctx.fillRect(0, 0, 1120, 924);
ctx.font = '15px sans-serif';
for (const p of panels) {
    ctx.drawImage(img, p.sx, p.sy, p.sw, p.sh, p.dx, p.dy, p.sw * Z, p.sh * Z);
    ctx.fillStyle = '#ffd479';
    ctx.fillText(p.t + '  —  src x ' + p.sx + '..' + (p.sx + p.sw) + ', y ' + p.sy + '..' + (p.sy + p.sh), p.dx, p.dy - 8);
    ctx.lineWidth = 1;
    for (let x = 0; x <= p.sw; x += 10) {
        ctx.strokeStyle = x % 20 === 0 ? 'rgba(255,60,60,0.8)' : 'rgba(0,140,255,0.35)';
        ctx.beginPath(); ctx.moveTo(p.dx + x * Z, p.dy); ctx.lineTo(p.dx + x * Z, p.dy + p.sh * Z); ctx.stroke();
        if (x % 20 === 0) { ctx.fillStyle = '#ff4040'; ctx.fillText('' + (p.sx + x), p.dx + x * Z + 2, p.dy + 14); }
    }
    for (let y = 0; y <= p.sh; y += 10) {
        ctx.strokeStyle = y % 20 === 0 ? 'rgba(255,60,60,0.8)' : 'rgba(0,140,255,0.35)';
        ctx.beginPath(); ctx.moveTo(p.dx, p.dy + y * Z); ctx.lineTo(p.dx + p.sw * Z, p.dy + y * Z); ctx.stroke();
        if (y % 20 === 0) { ctx.fillStyle = '#ff4040'; ctx.fillText('' + (p.sy + y), p.dx + 3, p.dy + y * Z - 3); }
    }
}
c;