// STAGE 4 - Encoding. Real geometry for every form: decade ruler on the time axis, ranked bars
// with the zero-baseline assertion, callouts. Palette is still the blocking scheme; Stage 5 owns it.
Stage.begin('Encode');

const W = 1080, H = 1920;
const Y0 = 1969, Y1 = 2022;
const MILESTONES = [
    { year: 1969, short: 'ARPANET',  hero: true  },
    { year: 1989, short: 'WWW',      hero: false },
    { year: 1993, short: 'MOSAIC',   hero: false },
    { year: 1998, short: 'GOOGLE',   hero: false },
    { year: 2004, short: 'FACEBOOK', hero: false },
    { year: 2007, short: 'IPHONE',   hero: false },
    { year: 2022, short: 'CHATGPT',  hero: true  }
];
const gaps = [];
for (let i = 1; i < MILESTONES.length; i++) {
    gaps.push({ from: MILESTONES[i - 1].year, to: MILESTONES[i].year,
                years: MILESTONES[i].year - MILESTONES[i - 1].year });
}

const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');
const PAPER = '#EFEDE3', BAR = '#DCE6D8', INK = '#141414', GREY = '#9C9C9C', HOT = '#E2402C';

ctx.fillStyle = PAPER; ctx.fillRect(0, 0, W, H);
for (let y = 0; y < H; y += 88) { ctx.fillStyle = BAR; ctx.fillRect(0, y, W, 44); }
const SPROCKET = 56;
ctx.strokeStyle = GREY; ctx.lineWidth = 2;
[SPROCKET, W - SPROCKET].forEach(x => { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, H); ctx.stroke(); });
for (let y = 22; y < H; y += 44) {
    const holes = [SPROCKET / 2, W - SPROCKET / 2];
    for (let h = 0; h < holes.length; h++) {
        ctx.fillStyle = PAPER; ctx.beginPath(); ctx.arc(holes[h], y, 11, 0, Math.PI * 2); ctx.fill();
        ctx.strokeStyle = GREY; ctx.lineWidth = 2; ctx.stroke();
    }
}
const sheet = Layout.rect(SPROCKET, 0, W - SPROCKET * 2, H);

// ---- HEADER. Kicker ABOVE the wordmark, fixing the clipped subhead from rev B.
ctx.fillStyle = INK; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.font = 'bold 15px Arial'; ctx.letterSpacing = '0.22em';
ctx.fillText('SEVEN MILESTONES / 53 YEARS / EVERY GAP DRAWN TO SCALE', sheet.x + 3, 18);

ctx.letterSpacing = '-0.03em';
ctx.font = '100px Impact';
const at100 = ctx.measureText('WEB-HISTORY').width;
const headSize = Math.ceil(((W - sheet.x) + 70) / at100 * 100);
ctx.font = headSize + 'px Impact';
const HEAD_Y = 46;
ctx.fillText('WEB-HISTORY', sheet.x, HEAD_Y);
// Measure what was actually drawn rather than assuming where its underside is.
const hm = ctx.measureText('WEB-HISTORY');
const headBottom = HEAD_Y + hm.actualBoundingBoxAscent + hm.actualBoundingBoxDescent;
ctx.letterSpacing = '0em';
log('wordmark ' + headSize + 'px, width ' + hm.width.toFixed(0) + ' (bleeds ' +
    (sheet.x + hm.width - W).toFixed(0) + 'px), drawn underside y=' + headBottom.toFixed(0));

// ---- AXIS. Top derived from the measured wordmark, not guessed.
const HERO_H = 92, STD_H = 58, MID_H = 62;
const AXIS_TOP = Math.ceil(headBottom + 26 + HERO_H / 2);
const AXIS_BOT = 1498, TEAR_Y = 1558;
const ty = Scale.linear(Y0, Y1, AXIS_TOP, AXIS_BOT);
if (Math.abs((ty.map(1980) - ty.map(1970)) - (ty.map(2010) - ty.map(2000))) > 0.001)
    throw new Error('time axis is not linear in years');
const pxYr = (AXIS_BOT - AXIS_TOP) / (Y1 - Y0);
const clusterClear = (ty.map(2007) - ty.map(2004)) - STD_H;
if (clusterClear < 5) throw new Error('2004/2007 blocks collide: ' + clusterClear.toFixed(1) + 'px');
log('AXIS_TOP=' + AXIS_TOP + '  px/year=' + pxYr.toFixed(2) + '  cluster clearance=' + clusterClear.toFixed(1) + 'px');

const railX = sheet.x + 306;

// ---- DECADE RULER. The evidence that the axis is linear: round numbers at even spacing (s3).
// Not strictly required - every value on the canvas is directly labelled - but without it the
// reader has to take "drawn to scale" on trust.
ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
for (let d = 1970; d <= 2020; d += 10) {
    const y = ty.map(d);
    ctx.strokeStyle = GREY; ctx.lineWidth = 2;
    ctx.beginPath(); ctx.moveTo(sheet.x + 4, y); ctx.lineTo(sheet.x + 34, y); ctx.stroke();
    ctx.fillStyle = GREY; ctx.font = 'bold 13px Arial';
    ctx.fillText(String(d), sheet.x + 40, y);
}

// ---- GAP RIBBONS + labels in the clear left margin.
for (let i = 0; i < gaps.length; i++) {
    const g = gaps[i];
    const a = ty.map(g.from), b = ty.map(g.to), big = g.years >= 15;
    ctx.fillStyle = big ? '#F6D9D4' : (i % 2 ? '#E6E3D8' : '#DBD8CC');
    ctx.fillRect(railX - 44, a, 88, b - a);
    ctx.fillStyle = big ? HOT : INK;
    ctx.font = 'bold ' + (big ? 36 : 20) + 'px Arial';
    ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
    ctx.fillText(g.years + ' YR', railX - 58, (a + b) / 2);
}
ctx.strokeStyle = INK; ctx.lineWidth = 6;
ctx.beginPath(); ctx.moveTo(railX, AXIS_TOP); ctx.lineTo(railX, AXIS_BOT); ctx.stroke();

// ---- MILESTONE BLOCKS.
const stagger = { 1969: 0, 1989: 0, 1993: 54, 1998: 0, 2004: 100, 2007: 0, 2022: 0 };
const heights = { 1969: HERO_H, 1989: MID_H, 1993: STD_H, 1998: MID_H, 2004: STD_H, 2007: STD_H, 2022: HERO_H };
MILESTONES.forEach(m => {
    const y = ty.map(m.year), bh = heights[m.year];
    const bx = railX + 70 + stagger[m.year];
    const bw = (m.hero ? W + 40 : sheet.x2 - 30) - bx;
    ctx.fillStyle = INK; ctx.fillRect(railX - 28, y - 4, 56, 8);
    ctx.strokeStyle = INK; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(railX + 28, y); ctx.lineTo(bx, y); ctx.stroke();
    ctx.fillStyle = '#FFFFFF'; ctx.fillRect(bx, y - bh / 2, bw, bh);
    ctx.strokeStyle = INK; ctx.lineWidth = 6; ctx.strokeRect(bx, y - bh / 2, bw, bh);
    ctx.fillStyle = INK; ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
    ctx.font = 'bold ' + (m.hero ? 44 : 30) + 'px Arial';
    ctx.fillText(String(m.year), bx + 16, y);
    ctx.font = 'bold ' + (m.hero ? 28 : 21) + 'px Arial';
    ctx.fillText(m.short, bx + (m.hero ? 142 : 106), y);
});

// ---- TEAR PERFORATION.
ctx.strokeStyle = INK; ctx.lineWidth = 3; ctx.setLineDash([14, 12]);
ctx.beginPath(); ctx.moveTo(0, TEAR_Y); ctx.lineTo(W, TEAR_Y); ctx.stroke();
ctx.setLineDash([]);

// ---- FOOTER. Off-axis by construction (below the perforation), so nothing here reads as dated.
const footer = Layout.rect(sheet.x, TEAR_Y + 20, sheet.width, H - TEAR_Y - 20);
ctx.fillStyle = INK; ctx.font = 'bold 14px Arial'; ctx.letterSpacing = '0.24em';
ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.fillText('END OF JOB / SUMMARY', footer.x + 2, footer.y);
ctx.letterSpacing = '0em';

const bodyZone = Layout.rect(footer.x, footer.y + 30, footer.width, footer.height - 30);
const [barsZone, numZone] = Layout.columns(bodyZone, [57, 43], 30);

// FORM C - ranked bars. Zero baseline asserted, not intended (s2).
ctx.fillStyle = GREY; ctx.font = 'bold 13px Arial'; ctx.textBaseline = 'top';
ctx.fillText('EVERY WAIT, LONGEST FIRST', barsZone.x, barsZone.y);
const plot = Layout.rect(barsZone.x + 118, barsZone.y + 24, barsZone.width - 118 - 66, barsZone.height - 34);
const gmax = Scale.nice(0, Math.max.apply(null, gaps.map(g => g.years)));
const bx = Scale.linear(0, gmax.max, plot.x, plot.x2);
if (!bx.isZeroBased) throw new Error('bars need a zero baseline');
const sorted = gaps.slice().sort((a, b) => b.years - a.years);
const band = Scale.band(sorted.length, plot.y, plot.y2, 0.34);
log('ranked bars: axis 0-' + gmax.max + ' yr, zeroBased=' + bx.isZeroBased +
    ', bandwidth=' + band.bandwidth.toFixed(1) + 'px');
sorted.forEach((g, i) => {
    const len = bx.extent(0, g.years);
    ctx.fillStyle = g.years >= 15 ? HOT : INK;
    ctx.fillRect(plot.x, band.map(i), len, band.bandwidth);
    ctx.fillStyle = INK; ctx.font = 'bold 15px Arial';
    ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
    ctx.fillText(g.from + '-' + g.to, plot.x - 12, band.center(i));
    ctx.textAlign = 'left'; ctx.font = 'bold 17px Arial';
    ctx.fillText(g.years, plot.x + len + 12, band.center(i));
});

// FORM D - two callouts. One value each, so not charts (s1).
const [c1, c2] = Layout.rows(numZone, 2, 16);
const callout = (r, big, small, sub) => {
    ctx.textAlign = 'left'; ctx.textBaseline = 'top';
    ctx.fillStyle = INK; ctx.font = '78px Impact'; ctx.letterSpacing = '-0.02em';
    ctx.fillText(big, r.x, r.y);
    ctx.letterSpacing = '0em'; ctx.font = 'bold 15px Arial';
    ctx.fillText(small, r.x + 3, r.y + 82);
    ctx.fillStyle = GREY; ctx.font = '13px Arial';
    ctx.fillText(sub, r.x + 3, r.y + 102);
};
callout(c1, '100M', 'CHATGPT USERS IN 2 MONTHS', '= 50M PER MONTH  (100 / 2)');
callout(c2, '47', 'OF 54 YEARS HAD NO MILESTONE HERE', '54 - 7 = 47  (87.0%)');

canvas;
