Stage.begin('Colorist');
Stage.note('Isolating nose and brows: the coarse map showed dark pixels at y168-180 flanking the centre line that I first read as fringe locks, but the per-column fringe trace came back as one smooth arc — so those are EYEBROWS, a feature the earlier passes missed entirely. Mapping the whole face interior at 2px to settle brows, nose and mouth before I paint them.');

const ref = Skia.Image.load('reference_images/comic1.png');
log('cols x=540..664 step 2.  S=flat skin  n=darker amber (nose)  E=eye  b=brow/mouth brown  .=other');
for (let y = 160; y <= 258; y += 2) {
    let row = '';
    for (let x = 540; x <= 664; x += 2) {
        const h = ref.getPixel(x, y);
        const a = parseInt(h.substr(7, 2), 16);
        if (a < 200) { row += ' '; continue; }
        const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
        if (Math.max(r, g, b) < 110 && Math.abs(r - b) < 26) row += 'E';
        else if (r > 100 && r < 165 && g > 55 && g < 125 && b > 45 && b < 115) row += 'b';
        else if (r > 195 && g > 185 && b < 140) row += 'S';
        else if (r > 195 && g > 110 && g <= 185 && b < 110) row += 'n';
        else row += '.';
    }
    log(String(y).padStart(4, ' ') + ' ' + row);
}
exit('face interior mapped');
