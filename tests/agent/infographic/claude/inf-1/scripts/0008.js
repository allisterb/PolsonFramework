// STAGE 3 rev B - fixing the zone faults found in 03: header collision, no real bleed,
// undifferentiated blocks, illegible small-gap labels. Still no palette or shadows (Stage 5).
Stage.begin('Composition');

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

// ---- HEADER. Size the wordmark so it PROVABLY overruns the canvas rather than hoping it does.
ctx.letterSpacing = '-0.03em';
ctx.font = '100px Impact';
const at100 = ctx.measureText('WEB-HISTORY').width;
const targetW = (W - sheet.x) + 90;                       // 90px past the right canvas edge
const headSize = Math.ceil(targetW / at100 * 100);
ctx.font = headSize + 'px Impact';
const headW = ctx.measureText('WEB-HISTORY').width;
log('headline: ' + headSize + 'px Impact, width ' + headW.toFixed(0) +
    'px, right edge at x=' + (sheet.x + headW).toFixed(0) + ' (canvas is ' + W + ') -> bleeds by ' +
    (sheet.x + headW - W).toFixed(0) + 'px');
ctx.fillStyle = INK; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.fillText('WEB-HISTORY', sheet.x, 40);
ctx.letterSpacing = '0em';
ctx.font = 'bold 20px Arial';
ctx.fillText('SEVEN MILESTONES / 53 YEARS / EVERY GAP DRAWN TO SCALE', sheet.x + 4, 40 + headSize + 12);

// ---- AXIS. Re-balanced to clear the taller header and leave the footer real room.
const AXIS_TOP = 300, AXIS_BOT = 1500, TEAR_Y = 1560;
const ty = Scale.linear(Y0, Y1, AXIS_TOP, AXIS_BOT);
const probeA = ty.map(1980) - ty.map(1970), probeB = ty.map(2010) - ty.map(2000);
if (Math.abs(probeA - probeB) > 0.001) throw new Error('time axis is not linear in years');
const pxYr = (AXIS_BOT - AXIS_TOP) / (Y1 - Y0);

const railX = sheet.x + 250;

// ---- GAP RIBBONS. Alternating tone so consecutive short gaps do not merge into one band.
for (let i = 1; i < MILESTONES.length; i++) {
    const a = ty.map(MILESTONES[i - 1].year), b = ty.map(MILESTONES[i].year);
    const yrs = MILESTONES[i].year - MILESTONES[i - 1].year;
    const big = yrs >= 15;
    ctx.fillStyle = big ? '#F6D9D4' : (i % 2 ? '#E6E3D8' : '#DBD8CC');
    ctx.fillRect(railX - 44, a, 88, b - a);

    // Labels move OUT of the ribbon, into the clear left margin, set horizontally in INK.
    // The washed-out rotated labels of the previous two renders were unreadable below ~6 years.
    ctx.fillStyle = big ? HOT : INK;
    ctx.font = big ? 'bold 40px Arial' : 'bold 20px Arial';
    ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
    ctx.fillText(yrs + ' YR', railX - 58, (a + b) / 2);
}

ctx.strokeStyle = INK; ctx.lineWidth = 6;
ctx.beginPath(); ctx.moveTo(railX, AXIS_TOP); ctx.lineTo(railX, AXIS_BOT); ctx.stroke();

// ---- MILESTONE BLOCKS. Three sizes, and the two heroes BLEED off the right canvas edge.
// Tick stays at true time position; only x and height vary, neither of which encodes anything.
const stagger = { 1969: 0, 1989: 0, 1993: 54, 1998: 0, 2004: 100, 2007: 0, 2022: 0 };
const heights  = { 1969: 92, 1989: 62, 1993: 58, 1998: 62, 2004: 58, 2007: 58, 2022: 92 };
MILESTONES.forEach(m => {
    const y = ty.map(m.year), bh = heights[m.year];
    const bx = railX + 76 + stagger[m.year];
    const bw = (m.hero ? W + 40 : sheet.x2 - 30) - bx;      // heroes overrun the canvas edge
    ctx.fillStyle = INK; ctx.fillRect(railX - 28, y - 4, 56, 8);
    ctx.strokeStyle = INK; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(railX + 28, y); ctx.lineTo(bx, y); ctx.stroke();
    ctx.fillStyle = '#FFFFFF'; ctx.fillRect(bx, y - bh / 2, bw, bh);
    ctx.strokeStyle = INK; ctx.lineWidth = 6; ctx.strokeRect(bx, y - bh / 2, bw, bh);
    ctx.fillStyle = INK; ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
    ctx.font = 'bold ' + (m.hero ? 46 : 32) + 'px Arial';
    ctx.fillText(String(m.year), bx + 16, y);
    ctx.font = 'bold ' + (m.hero ? 30 : 22) + 'px Arial';
    ctx.fillText(m.short, bx + (m.hero ? 150 : 112), y);
});

// ---- TEAR PERFORATION and footer, bleeding off the bottom.
ctx.strokeStyle = INK; ctx.lineWidth = 3; ctx.setLineDash([14, 12]);
ctx.beginPath(); ctx.moveTo(0, TEAR_Y); ctx.lineTo(W, TEAR_Y); ctx.stroke();
ctx.setLineDash([]);
const footer = Layout.rect(sheet.x, TEAR_Y + 26, sheet.width, H - TEAR_Y - 26);
const [barsZone, numZone] = Layout.columns(footer, [58, 42], 28);
ctx.fillStyle = GREY; ctx.font = 'bold 15px Arial'; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.fillText('C - RANKED INTERVALS  (Stage 4)', barsZone.x, barsZone.y);
ctx.fillText('D - BIG NUMBERS  (Stage 4)', numZone.x, numZone.y);
ctx.strokeStyle = GREY; ctx.lineWidth = 2; ctx.setLineDash([8, 8]);
ctx.strokeRect(barsZone.x, barsZone.y + 24, barsZone.width, 270);
ctx.strokeRect(numZone.x, numZone.y + 24, numZone.width, 270);
ctx.setLineDash([]);

// ---- s5 checks, measured rather than asserted by eye.
const silencePx = ty.map(1989) - ty.map(1969);
const clusterClear = (ty.map(2007) - ty.map(2004)) - heights[2004] / 2 - heights[2007] / 2;
log('px/year = ' + pxYr.toFixed(2));
log('s5.1 empty zone : 20-yr silence = ' + silencePx.toFixed(0) + 'px = ' +
    (silencePx / H * 100).toFixed(1) + '% of canvas (needs >~15%)');
log('s5.2 size ladder: headline ' + headSize + ' / hero year 46 / label 22 / body 15 -> largest is ' +
    (headSize / 15).toFixed(1) + 'x body (needs >=8x)');
log('cluster 2004-2007 clearance = ' + clusterClear.toFixed(1) + 'px');
canvas;
