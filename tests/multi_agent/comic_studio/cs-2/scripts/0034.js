Stage.begin('Critic');
Stage.note('Keeping the shader where it earned its place and reverting it where it did not. Fitting the fifth cape plane for the mid-right region (600-740, 640-800) that had no centre near it and blew out to 44.8, and investigating the belt, which is the worst non-cape region in BOTH stages (36.9 / 38.4) — an error that predates the shader and that nobody has looked at.');

const ref = Skia.Image.load('reference_images/comic1.png');
function solve3(M) {
    for (let i = 0; i < 3; i++) {
        let p = i;
        for (let k = i + 1; k < 3; k++) if (Math.abs(M[k][i]) > Math.abs(M[p][i])) p = k;
        const t = M[i]; M[i] = M[p]; M[p] = t;
        if (Math.abs(M[i][i]) < 1e-9) return null;
        for (let k = 0; k < 3; k++) {
            if (k === i) continue;
            const f = M[k][i] / M[i][i];
            for (let j = i; j < 4; j++) M[k][j] -= f * M[i][j];
        }
    }
    return [M[0][3] / M[0][0], M[1][3] / M[1][1], M[2][3] / M[2][2]];
}
const hx = v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0');
function planeFit(want, x0, y0, x1, y1) {
    let n = 0, Sx = 0, Sy = 0, Sxx = 0, Sxy = 0, Syy = 0;
    let Sr = 0, Sg = 0, Sb = 0, Sxr = 0, Sxg = 0, Sxb = 0, Syr = 0, Syg = 0, Syb = 0;
    for (let y = y0; y <= y1; y += 3) {
        for (let x = x0; x <= x1; x += 3) {
            const h = ref.getPixel(x, y);
            if (parseInt(h.substr(7, 2), 16) < 240) continue;
            const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
            let c;
            if (r > 200 && g > 140 && b < 140) c = 'Y';
            else if (b > 130 && (b - r) > 40) c = 'B';
            else if (r < 150 && g < 150 && b < 150 && (r - b) < 70) c = 'H';
            else c = 'R';
            if (c !== want) continue;
            n++; Sx += x; Sy += y; Sxx += x * x; Sxy += x * y; Syy += y * y;
            Sr += r; Sg += g; Sb += b;
            Sxr += x * r; Sxg += x * g; Sxb += x * b;
            Syr += y * r; Syg += y * g; Syb += y * b;
        }
    }
    if (n < 40) return { n: n };
    const cr = solve3([[Sxx, Sxy, Sx, Sxr], [Sxy, Syy, Sy, Syr], [Sx, Sy, n, Sr]]);
    const cg = solve3([[Sxx, Sxy, Sx, Sxg], [Sxy, Syy, Sy, Syg], [Sx, Sy, n, Sg]]);
    const cb = solve3([[Sxx, Sxy, Sx, Sxb], [Sxy, Syy, Sy, Syb], [Sx, Sy, n, Sb]]);
    const la = 0.299 * cr[0] + 0.587 * cg[0] + 0.114 * cb[0];
    const lb = 0.299 * cr[1] + 0.587 * cg[1] + 0.114 * cb[1];
    const mag = Math.hypot(la, lb), ux = la / mag, uy = lb / mag;
    const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
    const half = (Math.abs((x1 - x0) * ux) + Math.abs((y1 - y0) * uy)) / 2;
    const at = t => {
        const X = cx + ux * t, Y = cy + uy * t;
        return '#' + hx(cr[0] * X + cr[1] * Y + cr[2]) + hx(cg[0] * X + cg[1] * Y + cg[2]) + hx(cb[0] * X + cb[1] * Y + cb[2]);
    };
    return { n: n, darkAt: [Math.round(cx - ux * half), Math.round(cy - uy * half)], dark: at(-half),
             litAt: [Math.round(cx + ux * half), Math.round(cy + uy * half)], lit: at(half) };
}
const midRight = planeFit('R', 600, 630, 745, 800);
log('cape mid-right plane: ' + JSON.stringify(midRight));
const capeTop = planeFit('R', 420, 300, 560, 400);
log('cape upper band plane: ' + JSON.stringify(capeTop));

// Belt: what is actually there?
log('--- belt scan, x 480..690 at four rows ---');
for (const y of [492, 508, 524, 540]) {
    let s = '';
    for (let x = 480; x <= 690; x += 10) s += ' ' + ref.getPixel(x, y).substr(0, 7);
    log('y=' + y + s);
}
exit('fits complete');
