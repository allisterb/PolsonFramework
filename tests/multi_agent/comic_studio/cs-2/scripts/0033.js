Stage.begin('Critic');
Stage.note('The plane-blend shader improved coverage (7.67 -> 7.57) but made mean tone error WORSE (24.3 -> 25.2). Not accepting it on the coverage number alone. Comparing stage3 and stage4 region by region to see whether the shader helped where I claimed it would (the cape) and lost elsewhere, or whether the whole idea is wrong.');

const ref = Skia.Image.load('reference_images/comic1.png');
const s3 = Skia.Image.load('artifacts/stage3_inker.webp');
const s4 = Skia.Image.load('artifacts/stage4_critic.webp');
const REGIONS = [
    ['cape left wing',  200, 460, 430, 640],
    ['cape upper-left', 330, 300, 470, 460],
    ['cape lower/tail', 300, 640, 520, 830],
    ['cape far lobe',   680, 360, 780, 640],
    ['cape mid-right',  600, 640, 740, 800],
    ['suit torso',      460, 320, 700, 490],
    ['suit legs',       420, 560, 670, 800],
    ['suit raised arm', 690, 200, 830, 315],
    ['gauntlet',        650,  20, 825, 205],
    ['head+hair',       500,  60, 700, 300],
    ['boot',            370, 815, 545, 1000],
    ['belt+buckle',     490, 480, 680, 552]
];
function meanDist(img, x0, y0, x1, y1) {
    let s = 0, n = 0;
    for (let y = y0; y <= y1; y += 4) {
        for (let x = x0; x <= x1; x += 4) {
            const ha = ref.getPixel(x, y), hb = img.getPixel(x, y);
            if (parseInt(ha.substr(7, 2), 16) < 240 || parseInt(hb.substr(7, 2), 16) < 240) continue;
            const dr = parseInt(ha.substr(1, 2), 16) - parseInt(hb.substr(1, 2), 16);
            const dg = parseInt(ha.substr(3, 2), 16) - parseInt(hb.substr(3, 2), 16);
            const db = parseInt(ha.substr(5, 2), 16) - parseInt(hb.substr(5, 2), 16);
            s += Math.sqrt(dr * dr + dg * dg + db * db); n++;
        }
    }
    return n ? s / n : -1;
}
table(REGIONS.map(R => {
    const a = meanDist(s3, R[1], R[2], R[3], R[4]);
    const b = meanDist(s4, R[1], R[2], R[3], R[4]);
    return { region: R[0], stage3: a.toFixed(1), stage4: b.toFixed(1),
             verdict: b < a - 1 ? 'BETTER' : (b > a + 1 ? 'WORSE' : 'same') };
}));
exit('regional comparison complete');
