Stage.begin('Colorist');
Stage.note('First fit attempt died on the sandbox statement cap (2,000,000) with 14 regions sampled every 2px. Re-running at 4px, which is ~4x fewer iterations and still hundreds of samples per region — plenty for a 3-parameter plane fit. Recording it because the cap is not in the SDK docs I read and it kills the whole script rather than truncating the loop.');

const ref = Skia.Image.load('reference_images/comic1.png');
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

const REGIONS = [
    ['cape upper-left', 'R', 330, 300, 470, 470],
    ['cape left lobe',  'R', 200, 450, 430, 620],
    ['cape lower',      'R', 300, 640, 520, 820],
    ['cape right lobe', 'R', 690, 360, 775, 620],
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
for (const R of REGIONS) {
    const want = R[1], x0 = R[2], y0 = R[3], x1 = R[4], y1 = R[5];
    let n = 0, Sx = 0, Sy = 0, Sxx = 0, Sxy = 0, Syy = 0;
    let Sr = 0, Sg = 0, Sb = 0, Sxr = 0, Sxg = 0, Sxb = 0, Syr = 0, Syg = 0, Syb = 0;
    for (let y = y0; y <= y1; y += 4) {
        for (let x = x0; x <= x1; x += 4) {
            const h = ref.getPixel(x, y);
            if (parseInt(h.substr(7, 2), 16) < 200) continue;
            const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
            let c;
            if (r > 200 && g > 140 && b < 140) c = 'Y';
            else if (r > 140 && g < 130 && b < 130 && (r - g) > 60) c = 'R';
            else if (b > 130 && (b - r) > 40) c = 'B';
            else if (r < 150 && g < 150 && b < 150 && (r - b) < 70) c = 'H';
            else c = '?';
            if (c !== want) continue;
            n++; Sx += x; Sy += y; Sxx += x * x; Sxy += x * y; Syy += y * y;
            Sr += r; Sg += g; Sb += b;
            Sxr += x * r; Sxg += x * g; Sxb += x * b;
            Syr += y * r; Syg += y * g; Syb += y * b;
        }
    }
    if (n < 40) { out.push({ region: R[0], n: n, note: 'too few' }); continue; }
    const cr = solve3([[Sxx, Sxy, Sx, Sxr], [Sxy, Syy, Sy, Syr], [Sx, Sy, n, Sr]]);
    const cg = solve3([[Sxx, Sxy, Sx, Sxg], [Sxy, Syy, Sy, Syg], [Sx, Sy, n, Sg]]);
    const cb = solve3([[Sxx, Sxy, Sx, Sxb], [Sxy, Syy, Sy, Syb], [Sx, Sy, n, Sb]]);
    const la = 0.299 * cr[0] + 0.587 * cg[0] + 0.114 * cb[0];
    const lb = 0.299 * cr[1] + 0.587 * cg[1] + 0.114 * cb[1];
    const mag = Math.hypot(la, lb);
    const ux = mag > 1e-9 ? la / mag : 1, uy = mag > 1e-9 ? lb / mag : 0;
    const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
    const half = (Math.abs((x1 - x0) * ux) + Math.abs((y1 - y0) * uy)) / 2;
    const at = t => {
        const X = cx + ux * t, Y = cy + uy * t;
        return '#' + hex(cr[0] * X + cr[1] * Y + cr[2]) + hex(cg[0] * X + cg[1] * Y + cg[2]) + hex(cb[0] * X + cb[1] * Y + cb[2]);
    };
    out.push({
        region: R[0], n: n,
        darkAt: Math.round(cx - ux * half) + ',' + Math.round(cy - uy * half), dark: at(-half),
        litAt: Math.round(cx + ux * half) + ',' + Math.round(cy + uy * half), lit: at(half),
        litDirDeg: Math.round(Math.atan2(lb, la) * 180 / Math.PI),
        per100px: (mag * 100).toFixed(1)
    });
}
table(out);
exit('gradient fit complete');
