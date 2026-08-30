Stage.begin('Inker');
Stage.note('Ingesting the Colorist. A problem with my brief that I want on the record before I act on it: the role asks for a three-tier line weight hierarchy, tapered strokes and solid blacks, but the reference has NO line art — it is a flat vector illustration with zero outlines. Drawing ink on it would move the work AWAY from the target. So before inventing strokes I am checking whether the reference separates adjacent same-hue masses some other way; the coarse map showed isolated dark pixels between the legs and along the cape/torso seam, which would be exactly that. Mapping every dark pixel in the body region to find out.');

const ref = Skia.Image.load('reference_images/comic1.png');
log('body region, dark pixels (luminance < 118). each char = 6px.  #=very dark  +=dark  .=normal paint');
for (let y = 320; y <= 830; y += 6) {
    let row = '';
    for (let x = 280; x <= 800; x += 6) {
        const h = ref.getPixel(x, y);
        if (parseInt(h.substr(7, 2), 16) < 128) { row += ' '; continue; }
        const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
        const L = 0.299 * r + 0.587 * g + 0.114 * b;
        row += L < 78 ? '#' : (L < 118 ? '+' : '.');
    }
    if (row.indexOf('#') >= 0 || row.indexOf('+') >= 0) log(String(y).padStart(4, ' ') + ' ' + row);
}
exit('dark-structure map complete');
