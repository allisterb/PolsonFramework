// STAGE 5 - Palette & type. Neo-brutalist language applied: saturated flats, one hard offset
// shadow vector drawn as geometry, uniform heavy borders, oversized tracked display type.
// Contrast is measured off the rendered pixels at the end, not assumed from the swatches.
Stage.begin('Palette & Type');

const W = 1080, H = 1920;
const Y0 = 1969, Y1 = 2022;

// --- PALETTE. Three saturated flats + ink + paper + the green-bar ground. No tints, no gradients.
const INK = '#0D0D0D', PAPER = '#F3F0E3', GBAR = '#C8DFBB', WHITE = '#FFFFFF';
const RED = '#F5301B', BLUE = '#1B34DE', YELLOW = '#FFC400';
const SHX = 9, SHY = 9;               // ONE offset vector for the whole piece
const BORDER = 6;

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
for (let i = 1; i < MILESTONES.length; i++)
    gaps.push({ from: MILESTONES[i-1].year, to: MILESTONES[i].year,
                years: MILESTONES[i].year - MILESTONES[i-1].year });
// The colour split is the MEAN INTERVAL from brief.md, not a number chosen by eye.
const MEAN = (Y1 - Y0) / gaps.length;
log('mean interval = ' + (Y1-Y0) + '/' + gaps.length + ' = ' + MEAN.toFixed(2) + ' yr');

const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

const block = (x, y, w, h, fill) => {          // shadow -> flat fill -> heavy rule. Never blurred.
    ctx.fillStyle = INK; ctx.fillRect(x + SHX, y + SHY, w, h);
    ctx.fillStyle = fill; ctx.fillRect(x, y, w, h);
    ctx.strokeStyle = INK; ctx.lineWidth = BORDER;
    ctx.strokeRect(x + BORDER/2, y + BORDER/2, w - BORDER, h - BORDER);
};

// ---- GROUND
ctx.fillStyle = PAPER; ctx.fillRect(0, 0, W, H);
for (let y = 0; y < H; y += 88) { ctx.fillStyle = GBAR; ctx.fillRect(0, y, W, 44); }
const SPROCKET = 56;
ctx.strokeStyle = INK; ctx.lineWidth = 3;
[SPROCKET, W - SPROCKET].forEach(x => { ctx.beginPath(); ctx.moveTo(x,0); ctx.lineTo(x,H); ctx.stroke(); });
for (let y = 22; y < H; y += 44) {
    const holes = [SPROCKET/2, W - SPROCKET/2];
    for (let h = 0; h < holes.length; h++) {
        ctx.fillStyle = PAPER; ctx.beginPath(); ctx.arc(holes[h], y, 11, 0, Math.PI*2); ctx.fill();
        ctx.strokeStyle = INK; ctx.lineWidth = 3; ctx.stroke();
    }
}
const sheet = Layout.rect(SPROCKET, 0, W - SPROCKET*2, H);

// ---- HEADER. Display type tracked with the SDK's optical figure, not a guessed value.
ctx.fillStyle = INK; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.font = 'bold 15px Arial';
ctx.letterSpacing = LogoType.computeWordmarkTracking(15, true, 'tagline') + 'em';
ctx.fillText('SEVEN MILESTONES / 53 YEARS / EVERY GAP DRAWN TO SCALE', sheet.x + 3, 18);

ctx.font = '100px Impact';
const trackHead = LogoType.computeWordmarkTracking(200, true, 'wordmark');
ctx.letterSpacing = trackHead + 'em';
const at100 = ctx.measureText('WEB-HISTORY').width;
const headSize = Math.ceil(((W - sheet.x) + 70) / at100 * 100);
ctx.font = headSize + 'px Impact';
const HEAD_Y = 44;
ctx.fillText('WEB-HISTORY', sheet.x, HEAD_Y);
const hm = ctx.measureText('WEB-HISTORY');
const headBottom = HEAD_Y + hm.actualBoundingBoxAscent + hm.actualBoundingBoxDescent;
ctx.letterSpacing = '0em';
log('wordmark ' + headSize + 'px Impact, tracking ' + trackHead.toFixed(3) +
    'em, bleeds ' + (sheet.x + hm.width - W).toFixed(0) + 'px past the right edge');

// ---- AXIS
const HERO_H = 88, MID_H = 58, STD_H = 52;
const AXIS_TOP = Math.ceil(headBottom + 26 + HERO_H/2);
const AXIS_BOT = 1498, TEAR_Y = 1558;
const ty = Scale.linear(Y0, Y1, AXIS_TOP, AXIS_BOT);
if (Math.abs((ty.map(1980)-ty.map(1970)) - (ty.map(2010)-ty.map(2000))) > 0.001)
    throw new Error('time axis is not linear in years');
const clusterClear = (ty.map(2007) - ty.map(2004)) - STD_H;
if (clusterClear < 8) throw new Error('2004/2007 collide: ' + clusterClear.toFixed(1));
log('px/year = ' + ((AXIS_BOT-AXIS_TOP)/(Y1-Y0)).toFixed(2) + ', cluster clearance = ' + clusterClear.toFixed(1) + 'px');
const railX = sheet.x + 306;

// ---- DECADE RULER
ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
for (let d = 1970; d <= 2020; d += 10) {
    const y = ty.map(d);
    ctx.strokeStyle = INK; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(sheet.x + 4, y); ctx.lineTo(sheet.x + 30, y); ctx.stroke();
    ctx.fillStyle = INK; ctx.font = 'bold 13px Arial';
    ctx.fillText(String(d), sheet.x + 36, y);
}

// ---- GAP RIBBONS. Colour = above/below the mean wait. Size, not colour, carries emphasis in
// the labels, so no text ever sits on a full-chroma flat.
for (let i = 0; i < gaps.length; i++) {
    const g = gaps[i], a = ty.map(g.from), b = ty.map(g.to), long = g.years > MEAN;
    ctx.fillStyle = INK; ctx.fillRect(railX - 44 + SHX, a + (i ? 0 : SHY), 88, b - a);
    ctx.fillStyle = long ? RED : BLUE;
    ctx.fillRect(railX - 44, a, 88, b - a);
    ctx.strokeStyle = INK; ctx.lineWidth = BORDER;
    ctx.strokeRect(railX - 44 + BORDER/2, a, 88 - BORDER, b - a);
    ctx.fillStyle = INK;
    ctx.font = 'bold ' + (long ? 40 : 20) + 'px Arial';
    ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
    ctx.fillText(g.years + ' YR', railX - 62, (a + b) / 2);
}

// ---- MILESTONE BLOCKS
const stagger = { 1969:0, 1989:0, 1993:54, 1998:0, 2004:100, 2007:0, 2022:0 };
const heights = { 1969:HERO_H, 1989:MID_H, 1993:STD_H, 1998:MID_H, 2004:STD_H, 2007:STD_H, 2022:HERO_H };
MILESTONES.forEach(m => {
    const y = ty.map(m.year), bh = heights[m.year];
    const bx = railX + 70 + stagger[m.year];
    const bw = (m.hero ? W + 40 : sheet.x2 - 30) - bx;
    ctx.strokeStyle = INK; ctx.lineWidth = 5;
    ctx.beginPath(); ctx.moveTo(railX, y); ctx.lineTo(bx, y); ctx.stroke();
    ctx.fillStyle = INK; ctx.fillRect(railX - 30, y - 6, 60, 12);
    block(bx, y - bh/2, bw, bh, m.hero ? YELLOW : WHITE);
    ctx.fillStyle = INK; ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
    ctx.font = (m.hero ? 56 : 38) + 'px Impact';
    ctx.letterSpacing = LogoType.computeWordmarkTracking(m.hero ? 56 : 38, true, 'wordmark') + 'em';
    ctx.fillText(String(m.year), bx + 18, y + 2);
    ctx.letterSpacing = '0em';
    ctx.font = 'bold ' + (m.hero ? 27 : 20) + 'px Arial';
    ctx.fillText(m.short, bx + (m.hero ? 132 : 96), y + 1);
});

// ---- TEAR PERFORATION
ctx.strokeStyle = INK; ctx.lineWidth = 5; ctx.setLineDash([16, 13]);
ctx.beginPath(); ctx.moveTo(0, TEAR_Y); ctx.lineTo(W, TEAR_Y); ctx.stroke();
ctx.setLineDash([]);

// ---- FOOTER
const footer = Layout.rect(sheet.x, TEAR_Y + 20, sheet.width, H - TEAR_Y - 20);
ctx.fillStyle = INK; ctx.font = 'bold 14px Arial';
ctx.letterSpacing = LogoType.computeWordmarkTracking(14, true, 'tagline') + 'em';
ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.fillText('END OF JOB / SUMMARY', footer.x + 2, footer.y);
ctx.letterSpacing = '0em';

const bodyZone = Layout.rect(footer.x, footer.y + 28, footer.width, footer.height - 28);
const [barsZone, numZone] = Layout.columns(bodyZone, [57, 43], 30);

ctx.fillStyle = INK; ctx.font = 'bold 13px Arial'; ctx.textBaseline = 'top';
ctx.fillText('EVERY WAIT, LONGEST FIRST', barsZone.x, barsZone.y);
const plot = Layout.rect(barsZone.x + 116, barsZone.y + 22, barsZone.width - 116 - 60, barsZone.height - 30);
const gmax = Scale.nice(0, Math.max.apply(null, gaps.map(g => g.years)));
const bxs = Scale.linear(0, gmax.max, plot.x, plot.x2);
if (!bxs.isZeroBased) throw new Error('bars need a zero baseline');
const sorted = gaps.slice().sort((a, b) => b.years - a.years);
const band = Scale.band(sorted.length, plot.y, plot.y2, 0.26);
sorted.forEach((g, i) => {
    const len = bxs.extent(0, g.years);
    block(plot.x, band.map(i), len, band.bandwidth, g.years > MEAN ? RED : BLUE);
    ctx.fillStyle = INK; ctx.font = 'bold 14px Arial';
    ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
    ctx.fillText(g.from + '-' + g.to, plot.x - 14, band.center(i));
    ctx.textAlign = 'left'; ctx.font = '26px Impact';
    ctx.fillText(String(g.years), plot.x + len + 18, band.center(i));
});

const [c1, c2] = Layout.rows(numZone, 2, 14);
const callout = (r, big, small, sub) => {
    block(r.x, r.y, r.width, r.height, YELLOW);
    ctx.textAlign = 'left'; ctx.textBaseline = 'top';
    ctx.fillStyle = INK; ctx.font = '74px Impact';
    ctx.letterSpacing = LogoType.computeWordmarkTracking(74, true, 'wordmark') + 'em';
    ctx.fillText(big, r.x + 16, r.y + 8);
    ctx.letterSpacing = '0em'; ctx.font = 'bold 14px Arial';
    ctx.fillText(small, r.x + 18, r.y + 84);
    ctx.font = '12px Arial';
    ctx.fillText(sub, r.x + 18, r.y + 102);
};
callout(c1, '100M', 'CHATGPT USERS IN 2 MONTHS', '= 50M PER MONTH  (100 / 2)');
callout(c2, '47', 'OF 54 YEARS HAD NO MILESTONE HERE', '54 - 7 = 47  (87.0%)');

// ---- CONTRAST, MEASURED. Read the fill that actually landed, then compute WCAG ratio against
// the ink. "Looks bold" and "is legible" are different questions and only one of them is checkable.
const lin = c => { c /= 255; return c <= 0.03928 ? c/12.92 : Math.pow((c+0.055)/1.055, 2.4); };
const lum = hex => 0.2126*lin(parseInt(hex.substr(1,2),16)) +
                   0.7152*lin(parseInt(hex.substr(3,2),16)) +
                   0.0722*lin(parseInt(hex.substr(5,2),16));
const ratio = (a, b) => { const la = lum(a), lb = lum(b);
    return (Math.max(la,lb) + 0.05) / (Math.min(la,lb) + 0.05); };

const probes = [
    { name: 'ink on hero block (yellow)', at: [W - 120, ty.map(2022)] },
    { name: 'ink on standard block',      at: [sheet.x2 - 60, ty.map(1998)] },
    { name: 'ink on callout panel',       at: [c1.x + c1.width - 30, c1.y + c1.height - 20] },
    { name: 'ink on paper ground',        at: [sheet.x + 150, ty.map(1979)] },
    { name: 'ink on green bar',           at: [sheet.x + 8, 25] },
    { name: 'red ribbon vs paper',        at: [railX, ty.map(1979)] },
    { name: 'blue ribbon vs paper',       at: [railX, ty.map(1991)] }
];
const rows = [];
let worst = 99, worstName = '';
probes.forEach(p => {
    const px = canvas.bitmap.getPixel(Math.round(p.at[0]), Math.round(p.at[1]));
    const hex = px.substr(0, 7);
    const isMark = p.name.indexOf('vs paper') > -1;
    const r = isMark ? ratio(hex, PAPER) : ratio(INK, hex);
    rows.push({ probe: p.name, sampled: hex, ratio: r.toFixed(2) });
    if (!isMark && r < worst) { worst = r; worstName = p.name; }
});
table(rows);
log('WORST TEXT PAIRING: ' + worstName + ' at ' + worst.toFixed(2) + ':1 (WCAG AA body needs 4.5)');
if (worst < 4.5) throw new Error('legibility failure: ' + worstName + ' at ' + worst.toFixed(2));

canvas;
