Stage.begin('Critic');
Stage.note('The class diff only asks "is it the right colour family". It would pass a cape that is uniformly 40 units too dark. Mapping RGB distance per cell to find tone errors the class diff cannot see, and reporting the worst regions by name.');

const ref = Skia.Image.load('reference_images/comic1.png');
const mine = Skia.Image.load('artifacts/stage3_inker.webp');
log('colour error map, 16px cells. digit = mean RGB distance / 10 (9 = 90+). "." = under 10, " " = both empty');
const worst = [];
for (let y = 16; y < 1016; y += 16) {
    let row = '';
    for (let x = 0; x < 1024; x += 16) {
        let s = 0, n = 0, empty = 0;
        for (let dy = 0; dy < 16; dy += 8) {
            for (let dx = 0; dx < 16; dx += 8) {
                const ha = ref.getPixel(x + dx, y + dy), hb = mine.getPixel(x + dx, y + dy);
                const aa = parseInt(ha.substr(7, 2), 16), ab = parseInt(hb.substr(7, 2), 16);
                if (aa < 128 && ab < 128) { empty++; continue; }
                if (aa < 128 || ab < 128) { s += 200; n++; continue; }
                const dr = parseInt(ha.substr(1, 2), 16) - parseInt(hb.substr(1, 2), 16);
                const dg = parseInt(ha.substr(3, 2), 16) - parseInt(hb.substr(3, 2), 16);
                const db = parseInt(ha.substr(5, 2), 16) - parseInt(hb.substr(5, 2), 16);
                s += Math.sqrt(dr * dr + dg * dg + db * db); n++;
            }
        }
        if (n === 0) { row += ' '; continue; }
        const d = s / n;
        if (d >= 45) worst.push({ x: x, y: y, dist: Math.round(d) });
        row += d < 10 ? '.' : String(Math.min(9, Math.floor(d / 10)));
    }
    log(String(y).padStart(4, ' ') + ' ' + row.replace(/\s+$/, ''));
}
worst.sort((a, b) => b.dist - a.dist);
log('--- worst 30 cells ---');
table(worst.slice(0, 30));
exit('colour error mapped');
