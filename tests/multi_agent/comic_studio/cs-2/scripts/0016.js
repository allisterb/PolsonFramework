Stage.begin('Colorist');
Stage.note('Second look at stage2, done as a pixel diff rather than by eye: classifying both images on the same grid and mapping where my class disagrees with the references. An impression tells me it "looks close"; the diff tells me which mass is 20px wide of where it belongs.');

const ref = Skia.Image.load('reference_images/comic1.png');
const mine = Skia.Image.load('artifacts/stage2_colorist.webp');
function cls(img, x, y) {
    const h = img.getPixel(x, y);
    const a = parseInt(h.substr(7, 2), 16);
    if (a < 128) return '.';
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
    if (r > 200 && g > 140 && b < 140) return 'Y';
    if (r > 140 && g < 130 && b < 130 && (r - g) > 60) return 'R';
    if (b > 130 && (b - r) > 40) return 'B';
    if (r < 150 && g < 150 && b < 150 && (r - b) < 70) return 'H';
    return '?';
}
let same = 0, diff = 0, mineOnly = 0, refOnly = 0;
const rows = [];
for (let y = 16; y < 1016; y += 8) {
    let row = '';
    for (let x = 0; x < 1024; x += 8) {
        const a = cls(ref, x, y), b = cls(mine, x, y);
        if (a === b) { row += (a === '.' ? ' ' : '.'); if (a !== '.') same++; }
        else if (a === '.') { row += 'x'; mineOnly++; }      // I painted where the reference is empty
        else if (b === '.') { row += 'o'; refOnly++; }       // I left empty where the reference has paint
        else { row += a; diff++; }                            // wrong colour: char = what it SHOULD be
    }
    rows.push([y, row.replace(/\s+$/, '')]);
}
log(`matched ${same}  wrong-colour ${diff}  overpaint(x) ${mineOnly}  missing(o) ${refOnly}`);
log(`coverage error = ${(100 * (diff + mineOnly + refOnly) / (same + diff + mineOnly + refOnly)).toFixed(1)}%`);
log('map: "." agree   "x" I painted, ref empty   "o" ref painted, I empty   letter = colour it SHOULD be');
for (const [y, r] of rows) if (/[xo YRBH?]/.test(r.replace(/\./g, ''))) log(String(y).padStart(4, ' ') + ' ' + r);
exit('diff complete');
