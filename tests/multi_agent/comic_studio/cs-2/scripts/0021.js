Stage.begin('Inker');
Stage.note('Confirmed: the reference does its line work tonally. There is a genuine dark crease between the legs (x538-548, y596-690) and a dark seam band along the cape/torso boundary. So the Inkers three-tier hierarchy maps onto TONE here, not stroke: primary = the deep seams that read as contour, secondary = the softer boundaries between adjacent reds, tertiary = none. Measuring the actual seam colours so I darken to the references values instead of adding black.');

const ref = Skia.Image.load('reference_images/comic1.png');
const probes = [
    ['leg crease top', 541, 600], ['leg crease mid', 541, 630], ['leg crease low', 528, 665],
    ['leg lit L of crease', 500, 630], ['leg lit R of crease', 580, 630],
    ['cape/torso seam', 710, 440], ['cape/torso seam2', 700, 480], ['cape right lobe outer', 755, 470],
    ['cape/leg left seam', 440, 592], ['cape left of seam', 420, 592], ['leg right of seam', 470, 592],
    ['boot/cape seam', 468, 800], ['boot/cape seam2', 462, 820],
    ['arm shadow core', 495, 455], ['arm shadow edge', 470, 480],
    ['gauntlet/arm seam', 800, 195], ['collar/torso seam', 470, 300],
    ['cape behind legs', 512, 660], ['torso/cape seam L', 436, 400]
];
table(probes.map(p => {
    const h = ref.getPixel(p[1], p[2]);
    const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
    return { name: p[0], at: p[1] + ',' + p[2], hex: h, lum: Math.round(0.299 * r + 0.587 * g + 0.114 * b) };
}));

// Exact extent of the leg crease per row.
log('leg crease extent per row (pixels darker than 105 luminance, x 480..620):');
for (let y = 580; y <= 700; y += 6) {
    let lo = -1, hi = -1;
    for (let x = 480; x <= 620; x += 2) {
        const h = ref.getPixel(x, y);
        if (parseInt(h.substr(7, 2), 16) < 200) continue;
        const r = parseInt(h.substr(1, 2), 16), g = parseInt(h.substr(3, 2), 16), b = parseInt(h.substr(5, 2), 16);
        if (0.299 * r + 0.587 * g + 0.114 * b < 105) { if (lo < 0) lo = x; hi = x; }
    }
    log('  y=' + y + '  ' + (lo < 0 ? 'none' : lo + '..' + hi));
}
exit('seam measurement complete');
