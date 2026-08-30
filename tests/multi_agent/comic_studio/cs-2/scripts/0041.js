Stage.begin('Critic');
Stage.note('Final verification of output.webp against the reference. Also confirming the ground is transparent, which the reference is and a white fill would silently break.');
const ref = Skia.Image.load('reference_images/comic1.png');
const out = Skia.Image.load('output.webp');
log('corner alpha — ref ' + ref.getPixel(8, 8) + '  output ' + out.getPixel(8, 8));
log('dims ref ' + ref.width + 'x' + ref.height + '   output ' + out.width + 'x' + out.height);
function classOf(h) {
    if (parseInt(h.substr(7, 2), 16) < 128) return '.';
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
    if (r > 200 && g > 140 && b < 140) return 'Y';
    if (b > 130 && (b - r) > 40) return 'B';
    if (r < 150 && g < 150 && b < 150 && (r - b) < 70) return 'H';
    return 'R';
}
let same = 0, bad = 0, s = 0, n = 0;
for (let y = 4; y < 1024; y += 5) {
    for (let x = 4; x < 1024; x += 5) {
        const ha = ref.getPixel(x, y), hb = out.getPixel(x, y);
        const a = classOf(ha), b = classOf(hb);
        if (a === '.' && b === '.') continue;
        if (a === b) same++; else bad++;
        if (a !== '.' && b !== '.') {
            const dr = parseInt(ha.substr(1, 2), 16) - parseInt(hb.substr(1, 2), 16);
            const dg = parseInt(ha.substr(3, 2), 16) - parseInt(hb.substr(3, 2), 16);
            const db = parseInt(ha.substr(5, 2), 16) - parseInt(hb.substr(5, 2), 16);
            s += Math.sqrt(dr * dr + dg * dg + db * db); n++;
        }
    }
}
log('FINAL: silhouette+colour-family agreement ' + (100 * same / (same + bad)).toFixed(2) + '%  (error ' + (100 * bad / (same + bad)).toFixed(2) + '%)');
log('FINAL: mean RGB distance over shared pixels ' + (s / n).toFixed(1) + ' of 441 max');
exit('final verification complete');
