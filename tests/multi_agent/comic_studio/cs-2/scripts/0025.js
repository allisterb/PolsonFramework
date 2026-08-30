Stage.begin('Critic');
Stage.note('Adversarial audit of stage3 against the reference, side by side and numerically. Two measures: overall coverage error (what fraction of the image is the wrong colour) and per-region edge deltas. I am also checking the three places a pipeline like this usually hides its failures — the seam the Inker just rebuilt, the emblem the Colorist reshaped, and the hair silhouette nobody has verified since the Penciler traced it.');

const ref = Skia.Image.load('reference_images/comic1.png');
const mine = Skia.Image.load('artifacts/stage3_inker.webp');
function classOf(h) {
    if (parseInt(h.substr(7, 2), 16) < 128) return '.';
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
    if (r > 200 && g > 140 && b < 140) return 'Y';
    if (b > 130 && (b - r) > 40) return 'B';
    if (r < 150 && g < 150 && b < 150 && (r - b) < 70) return 'H';
    return 'R';
}
let same = 0, wrong = 0, over = 0, missing = 0;
let sumDE = 0, nDE = 0;
for (let y = 4; y < 1024; y += 4) {
    for (let x = 4; x < 1024; x += 4) {
        const ha = ref.getPixel(x, y), hb = mine.getPixel(x, y);
        const a = classOf(ha), b = classOf(hb);
        if (a === b) { if (a !== '.') same++; }
        else if (a === '.') over++;
        else if (b === '.') missing++;
        else wrong++;
        if (a !== '.' && b !== '.') {
            const dr = parseInt(ha.substr(1, 2), 16) - parseInt(hb.substr(1, 2), 16);
            const dg = parseInt(ha.substr(3, 2), 16) - parseInt(hb.substr(3, 2), 16);
            const db = parseInt(ha.substr(5, 2), 16) - parseInt(hb.substr(5, 2), 16);
            sumDE += Math.sqrt(dr * dr + dg * dg + db * db); nDE++;
        }
    }
}
const tot = same + wrong + over + missing;
log(`STAGE 3 SCORE  matched ${same}/${tot}  wrong-colour ${wrong}  overpaint ${over}  missing ${missing}`);
log(`coverage error ${(100 * (wrong + over + missing) / tot).toFixed(2)}%   mean RGB distance over shared pixels ${(sumDE / nDE).toFixed(1)}`);

function scan(img, y) {
    const e = { R: [-1, -1], B: [-1, -1], Y: [-1, -1] };
    for (let x = 0; x < 1024; x += 2) {
        const c = classOf(img.getPixel(x, y));
        if (c === '.' || c === 'H') continue;
        const t = e[c];
        if (t[0] < 0) t[0] = x;
        t[1] = x;
    }
    return e;
}
log('--- remaining edge deltas > 10px ---');
for (let y = 32; y <= 1004; y += 12) {
    const a = scan(ref, y), b = scan(mine, y), parts = [];
    for (const k of ['R', 'B', 'Y']) {
        const A = a[k], B = b[k];
        if (A[0] < 0 && B[0] < 0) continue;
        if (A[0] < 0) { parts.push(k + ' spurious ' + B[0] + '-' + B[1]); continue; }
        if (B[0] < 0) { parts.push(k + ' MISSING ref ' + A[0] + '-' + A[1]); continue; }
        const dl = B[0] - A[0], dr = B[1] - A[1];
        if (Math.abs(dl) > 10 || Math.abs(dr) > 10)
            parts.push(k + ' L' + (dl > 0 ? '+' : '') + dl + ' R' + (dr > 0 ? '+' : '') + dr + ' [ref ' + A[0] + '-' + A[1] + ']');
    }
    if (parts.length) log(String(y).padStart(4, ' ') + ' | ' + parts.join('  '));
}
exit('audit pass 1 complete');
