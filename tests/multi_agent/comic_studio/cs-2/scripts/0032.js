Stage.begin('Critic');
const ref = Skia.Image.load('reference_images/comic1.png');
function classOf(h) {
    if (parseInt(h.substr(7, 2), 16) < 128) return '.';
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
    if (r > 200 && g > 140 && b < 140) return 'Y';
    if (b > 130 && (b - r) > 40) return 'B';
    if (r < 150 && g < 150 && b < 150 && (r - b) < 70) return 'H';
    return 'R';
}
function score(file) {
    const m = Skia.Image.load(file);
    let same = 0, bad = 0, sumDE = 0, nDE = 0;
    for (let y = 6; y < 1024; y += 6) {
        for (let x = 6; x < 1024; x += 6) {
            const ha = ref.getPixel(x, y), hb = m.getPixel(x, y);
            const a = classOf(ha), b = classOf(hb);
            if (a === '.' && b === '.') continue;
            if (a === b) same++; else bad++;
            if (a !== '.' && b !== '.') {
                const dr = parseInt(ha.substr(1, 2), 16) - parseInt(hb.substr(1, 2), 16);
                const dg = parseInt(ha.substr(3, 2), 16) - parseInt(hb.substr(3, 2), 16);
                const db = parseInt(ha.substr(5, 2), 16) - parseInt(hb.substr(5, 2), 16);
                sumDE += Math.sqrt(dr * dr + dg * dg + db * db); nDE++;
            }
        }
    }
    return { stage: file.replace('artifacts/', '').replace('.webp', ''),
             coverageErrPct: (100 * bad / (same + bad)).toFixed(2), meanRGBdist: (sumDE / nDE).toFixed(1) };
}
table([score('artifacts/stage2_colorist.webp'), score('artifacts/stage3_inker.webp'), score('artifacts/stage4_critic.webp')]);
exit('scored');
