Stage.begin('Penciler');
const img = Skia.Image.load('reference_images/panel1.jpg');
const panels = [
    { t: 'L muzzle + mouth', sx: 195, sy: 240, sw: 100, sh: 80, z: 5, dx: 10, dy: 34 },
    { t: 'trunk + L near-ear tip', sx: 60, sy: 0, sw: 130, sh: 105, z: 5, dx: 540, dy: 34 },
    { t: 'R ear pair (cropped at top)', sx: 380, sy: 0, sw: 160, sh: 80, z: 5, dx: 10, dy: 490 },
    { t: 'L far ear', sx: 0, sy: 60, sw: 130, sh: 145, z: 4, dx: 830, dy: 490 }
];
const c = createCanvas(1360, 1090); const ctx = c.getContext('2d');
ctx.fillStyle = '#101014'; ctx.fillRect(0, 0, 1360, 1090);
ctx.font = '15px sans-serif';
for (const p of panels) {
    ctx.drawImage(img, p.sx, p.sy, p.sw, p.sh, p.dx, p.dy, p.sw * p.z, p.sh * p.z);
    ctx.fillStyle = '#ffd479';
    ctx.fillText(p.t + '  —  ' + p.z + 'x, src x ' + p.sx + '..' + (p.sx + p.sw) + ', y ' + p.sy + '..' + (p.sy + p.sh), p.dx, p.dy - 8);
    ctx.lineWidth = 1;
    for (let x = 0; x <= p.sw; x += 10) {
        ctx.strokeStyle = x % 20 === 0 ? 'rgba(255,60,60,0.8)' : 'rgba(0,140,255,0.3)';
        ctx.beginPath(); ctx.moveTo(p.dx + x * p.z, p.dy); ctx.lineTo(p.dx + x * p.z, p.dy + p.sh * p.z); ctx.stroke();
        if (x % 20 === 0) { ctx.fillStyle = '#ff4040'; ctx.fillText('' + (p.sx + x), p.dx + x * p.z + 2, p.dy + 14); }
    }
    for (let y = 0; y <= p.sh; y += 10) {
        ctx.strokeStyle = y % 20 === 0 ? 'rgba(255,60,60,0.8)' : 'rgba(0,140,255,0.3)';
        ctx.beginPath(); ctx.moveTo(p.dx, p.dy + y * p.z); ctx.lineTo(p.dx + p.sw * p.z, p.dy + y * p.z); ctx.stroke();
        if (y % 20 === 0) { ctx.fillStyle = '#ff4040'; ctx.fillText('' + (p.sy + y), p.dx + 3, p.dy + y * p.z - 3); }
    }
}
c;