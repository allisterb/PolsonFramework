Stage.begin('Colorist');
Stage.note('Ingesting the Penciler before painting: read artifacts/stage1_penciler.webp and the ANCHORS it left. The reference is a flat vector illustration lit by linear gradients, not by a rendered light, so "one light direction" here means recovering each regions actual gradient axis. Fitting each channel as a linear function of (x,y) per region gives me the axis and both endpoint colours directly, instead of eyeballing a ramp.');

const ref = Skia.Image.load('reference_images/comic1.png');
const parse = h => ({
    r: parseInt(h.substr(1, 2), 16), g: parseInt(h.substr(3, 2), 16),
    b: parseInt(h.substr(5, 2), 16), a: parseInt(h.substr(7, 2), 16)
});
function cls(p) {
    if (p.a < 200) return '.';
    if (p.r > 200 && p.g > 140 && p.b < 140) return 'Y';
    if (p.r > 140 && p.g < 130 && p.b < 130 && (p.r - p.g) > 60) return 'R';
    if (p.b > 130 && (p.b - p.r) > 40) return 'B';
    if (Math.max(p.r, p.g, p.b) < 150 && (p.r - p.b) < 70) return 'H';
    return '?';
}
// Solve the 3x3 normal equations for  v = a*x + b*y + c  by Gaussian elimination.
function solve3(M) {
    for (let i = 0; i < 3; i++) {
        let piv = i;
        for (let k = i + 1; k < 3; k++) if (Math.abs(M[k][i]) > Math.abs(M[piv][i])) piv = k;
        const t = M[i]; M[i] = M[piv]; M[piv] = t;
        if (Math.abs(M[i][i]) < 1e-9) return null;
        for (let k = 0; k < 3; k++) {
            if (k === i) continue;
            const f = M[k][i] / M[i][i];
            for (let j = i; j < 4; j++) M[k][j] -= f * M[i][j];
        }
    }
    return [M[0][3] / M[0][0], M[1][3] / M[1][1], M[2][3] / M[2][2]];
}
const hex = v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0');

// Regions to fit: name, class it must match, and the box to sample.
const REGIONS = [
    ['cape upper-left', 'R', 330, 300, 470, 470],
    ['cape left lobe',  'R', 200, 450, 430, 620],
    ['cape lower',      'R', 300, 640, 520, 820],
    ['cape right lobe', 'R', 690, 360, 775, 620],
    ['cape tail',       'R', 300, 700, 460, 840],
    ['suit torso',      'B', 460, 320, 700, 490],
    ['suit leg L',      'B', 415, 580, 545, 690],
    ['suit leg R',      'B', 500, 580, 665, 800],
    ['suit upper arm',  'B', 690, 200, 830, 310],
    ['skin face',       'Y', 520, 130, 680, 270],
    ['hair',            'H', 500,  64, 720, 250],
    ['gauntlet',        'R', 650,  20, 825, 205],
    ['boot',            'R', 370, 815, 545, 1000],
    ['belt',            'R', 495, 496, 680, 546]
];
const out = [];
for (const [name, want, x0, y0, x1, y1] of REGIONS) {
    // Normal-equation accumulators, shared across the three channels.
    let n = 0, Sx = 0, Sy = 0, Sxx = 0, Sxy = 0, Syy = 0;
    const Sv = [0, 0, 0], Sxv = [0, 0, 0], Syv = [0, 0, 0];
    for (let y = y0; y <= y1; y += 2) {
        for (let x = x0; x <= x1; x += 2) {
            const p = parse(ref.getPixel(x, y));
            if (cls(p) !== want) continue;
            n++; Sx += x; Sy += y; Sxx += x * x; Sxy += x * y; Syy += y * y;
            const ch = [p.r, p.g, p.b];
            for (let c = 0; c < 3; c++) { Sv[c] += ch[c]; Sxv[c] += x * ch[c]; Syv[c] += y * ch[c]; }
        }
    }
    if (n < 60) { out.push({ region: name, n, note: 'too few samples' }); continue; }
    const coef = [];
    for (let c = 0; c < 3; c++) {
        const s = solve3([[Sxx, Sxy, Sx, Sxv[c]], [Sxy, Syy, Sy, Syv[c]], [Sx, Sy, n, Sv[c]]]);
        coef.push(s || [0, 0, Sv[c] / n]);
    }
    // Luminance gradient direction = where the region gets darker/lighter fastest.
    const la = 0.299 * coef[0][0] + 0.587 * coef[1][0] + 0.114 * coef[2][0];
    const lb = 0.299 * coef[0][1] + 0.587 * coef[1][1] + 0.114 * coef[2][1];
    const ang = Math.atan2(lb, la) * 180 / Math.PI;      // direction of INCREASING brightness
    const mag = Math.hypot(la, lb);
    // Endpoint colours: project the box onto the gradient axis, evaluate at both ends.
    const ux = mag > 1e-9 ? la / mag : 1, uy = mag > 1e-9 ? lb / mag : 0;
    const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
    const half = (Math.abs((x1 - x0) * ux) + Math.abs((y1 - y0) * uy)) / 2;
    const at = t => {
        const X = cx + ux * t, Y = cy + uy * t;
        return '#' + hex(coef[0][0] * X + coef[0][1] * Y + coef[0][2])
                   + hex(coef[1][0] * X + coef[1][1] * Y + coef[1][2])
                   + hex(coef[2][0] * X + coef[2][1] * Y + coef[2][2]);
    };
    out.push({
        region: name, n,
        darkAt: `${Math.round(cx - ux * half)},${Math.round(cy - uy * half)}`, dark: at(-half),
        litAt:  `${Math.round(cx + ux * half)},${Math.round(cy + uy * half)}`, lit: at(half),
        brightDirDeg: Math.round(ang), rampPer100px: (mag * 100).toFixed(1)
    });
}
table(out);
exit('gradient fit complete');
