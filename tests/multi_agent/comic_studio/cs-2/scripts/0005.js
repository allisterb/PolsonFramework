Stage.begin('Penciler');
Stage.note('Measurement pass 4: full-figure classification map at 8px resolution. This is the master reference chart — every anchor constant is read off it, so later stages can re-derive a coordinate instead of inventing one.');

const ref = Skia.Image.load('reference_images/comic1.png');
function cls(x, y) {
    const h = ref.getPixel(x, y);
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16),
          b = parseInt(h.substr(5, 2), 16), a = parseInt(h.substr(7, 2), 16);
    if (a < 100) return '.';
    if (r > 200 && g > 140 && b < 140) return 'Y';           // skin, emblem, buckle
    if (r > 140 && g < 130 && b < 130 && (r - g) > 60) return 'R';  // cape, boots, belt
    if (b > 130 && (b - r) > 40) return 'B';                 // suit blue
    if (Math.max(r, g, b) < 150 && (r - b) < 70) return 'H';  // hair, eyes, mouth
    return '?';                                              // dark red / dark blue transitions
}
log('cols: each char = 8px, x = 8*col. Ruler marks every 100px.');
let ruler = '';
for (let x = 0; x < 1024; x += 8) ruler += (x % 100 < 8) ? '|' : ' ';
log('     ' + ruler);
for (let y = 16; y < 1016; y += 8) {
    let row = '';
    for (let x = 0; x < 1024; x += 8) row += cls(x, y);
    log(String(y).padStart(4, ' ') + ' ' + row.replace(/\.+$/, ''));
}
exit('full map complete');
