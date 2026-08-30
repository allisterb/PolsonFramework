Stage.begin('Critic');
Stage.note('The cape is the single largest source of remaining tone error: a radial gradient cannot model a surface that is dark behind the torso, bright at four separate outer edges, and asymmetric between the near and far lobes. Fitting a quadratic surface (1,u,v,u2,v2,uv per channel, coordinates normalised to [-1,1] for conditioning) over the capes measured pixels, and doing the same for the suit. If the fit holds I will drive both with SkSL shaders instead of gradients.');

const ref = Skia.Image.load('reference_images/comic1.png');
function solveN(M, n) {                       // Gaussian elimination, augmented n x (n+1)
    for (let i = 0; i < n; i++) {
        let p = i;
        for (let k = i + 1; k < n; k++) if (Math.abs(M[k][i]) > Math.abs(M[p][i])) p = k;
        const t = M[i]; M[i] = M[p]; M[p] = t;
        if (Math.abs(M[i][i]) < 1e-12) return null;
        for (let k = 0; k < n; k++) {
            if (k === i) continue;
            const f = M[k][i] / M[i][i];
            for (let j = i; j <= n; j++) M[k][j] -= f * M[i][j];
        }
    }
    const out = [];
    for (let i = 0; i < n; i++) out.push(M[i][n] / M[i][i]);
    return out;
}
function fitQuad(want, x0, y0, x1, y1, reject) {
    const N = 6;
    const A = [], rhs = [[], [], []];
    for (let i = 0; i < N; i++) { A.push([0, 0, 0, 0, 0, 0]); rhs[0].push(0); rhs[1].push(0); rhs[2].push(0); }
    let n = 0;
    for (let y = y0; y <= y1; y += 6) {
        for (let x = x0; x <= x1; x += 6) {
            if (reject && reject(x, y)) continue;
            const h = ref.getPixel(x, y);
            if (parseInt(h.substr(7, 2), 16) < 240) continue;
            const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
            let c;
            if (r > 200 && g > 140 && b < 140) c = 'Y';
            else if (b > 130 && (b - r) > 40) c = 'B';
            else if (r < 150 && g < 150 && b < 150 && (r - b) < 70) c = 'H';
            else c = 'R';
            if (c !== want) continue;
            const u = (x - 512) / 512, v = (y - 512) / 512;
            const B = [1, u, v, u * u, v * v, u * v];
            const ch = [r, g, b];
            for (let i = 0; i < N; i++) {
                for (let j = 0; j < N; j++) A[i][j] += B[i] * B[j];
                for (let k = 0; k < 3; k++) rhs[k][i] += B[i] * ch[k];
            }
            n++;
        }
    }
    const coef = [];
    for (let k = 0; k < 3; k++) {
        const M = [];
        for (let i = 0; i < N; i++) { const row = A[i].slice(); row.push(rhs[k][i]); M.push(row); }
        coef.push(solveN(M, N));
    }
    // residual check
    let se = 0, m = 0;
    for (let y = y0; y <= y1; y += 12) {
        for (let x = x0; x <= x1; x += 12) {
            if (reject && reject(x, y)) continue;
            const h = ref.getPixel(x, y);
            if (parseInt(h.substr(7, 2), 16) < 240) continue;
            const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
            let c;
            if (r > 200 && g > 140 && b < 140) c = 'Y';
            else if (b > 130 && (b - r) > 40) c = 'B';
            else if (r < 150 && g < 150 && b < 150 && (r - b) < 70) c = 'H';
            else c = 'R';
            if (c !== want) continue;
            const u = (x - 512) / 512, v = (y - 512) / 512;
            const B = [1, u, v, u * u, v * v, u * v];
            const pr = [0, 0, 0];
            for (let k = 0; k < 3; k++) for (let i = 0; i < 6; i++) pr[k] += coef[k][i] * B[i];
            const d = Math.sqrt((pr[0] - r) ** 2 + (pr[1] - g) ** 2 + (pr[2] - b) ** 2);
            se += d; m++;
        }
    }
    return { n: n, coef: coef, rms: (se / m).toFixed(1) };
}

// Cape: exclude belt, bent fist, gauntlet and boot so their tones do not bias it.
const capeReject = (x, y) =>
    (y > 470 && y < 560 && x > 480 && x < 690) ||     // belt
    (x > 316 && x < 400 && y < 390) ||                 // bent fist
    (y > 795) ||                                       // boot
    (y < 316);                                         // collar
const cape = fitQuad('R', 190, 240, 790, 800, capeReject);
const suit = fitQuad('B', 410, 300, 840, 812, null);
log('CAPE  samples=' + cape.n + '  mean residual=' + cape.rms);
log('  r: ' + cape.coef[0].map(v => v.toFixed(3)).join(', '));
log('  g: ' + cape.coef[1].map(v => v.toFixed(3)).join(', '));
log('  b: ' + cape.coef[2].map(v => v.toFixed(3)).join(', '));
log('SUIT  samples=' + suit.n + '  mean residual=' + suit.rms);
log('  r: ' + suit.coef[0].map(v => v.toFixed(3)).join(', '));
log('  g: ' + suit.coef[1].map(v => v.toFixed(3)).join(', '));
log('  b: ' + suit.coef[2].map(v => v.toFixed(3)).join(', '));
Session['CAPE_FIT'] = JSON.stringify(cape.coef);
Session['SUIT_FIT'] = JSON.stringify(suit.coef);
exit('quadratic fits complete');
