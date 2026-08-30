// STAGE 3 - Composition blocking. Zones and proportions only; no palette, no type detail,
// no final styling. The question this render answers is whether the 20-year silence is actually
// large enough to read as the subject, and whether the 2004/2007 pair fits at true position.
Stage.begin('Composition');

const W = 1080, H = 1920;
const Y0 = 1969, Y1 = 2022;
const MILESTONES = [
    { year: 1969, short: 'ARPANET' },
    { year: 1989, short: 'WWW' },
    { year: 1993, short: 'MOSAIC' },
    { year: 1998, short: 'GOOGLE' },
    { year: 2004, short: 'FACEBOOK' },
    { year: 2007, short: 'IPHONE' },
    { year: 2022, short: 'CHATGPT' }
];

const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

const PAPER = '#EFEDE3', BAR = '#DCE6D8', INK = '#141414', GREY = '#9C9C9C', HOT = '#E2402C';

// ---- GROUND. Green-bar fanfold: alternating printed bands, sprocket margins both edges.
// s5 rule 4 - the ground is never uniform. Schematic here; texture is Stage 6.
ctx.fillStyle = PAPER;
ctx.fillRect(0, 0, W, H);
const BAND = 44;
for (let y = 0; y < H; y += BAND * 2) {
    ctx.fillStyle = BAR;
    ctx.fillRect(0, y, W, BAND);
}

const SPROCKET = 56;
ctx.strokeStyle = GREY;
ctx.lineWidth = 2;
[SPROCKET, W - SPROCKET].forEach(x => {
    ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, H); ctx.stroke();
});
for (let y = 22; y < H; y += 44) {
    [SPROCKET / 2, W - SPROCKET / 2].forEach(cx => {
        ctx.fillStyle = PAPER; ctx.beginPath(); ctx.arc(cx, y, 11, 0, Math.PI * 2); ctx.fill();
        ctx.strokeStyle = GREY; ctx.lineWidth = 2; ctx.stroke();
    });
});

const sheet = Layout.rect(SPROCKET, 0, W - SPROCKET * 2, H);

// ---- ZONE BLOCKING. Weighted, not equal (s5 preamble).
const HEADER_H = 190, TEAR_Y = 1596, FOOTER_TOP = TEAR_Y + 22;
const AXIS_TOP = 200, AXIS_BOT = 1540;

// The time axis. Linear in years - this is the whole argument, so it is asserted, not intended.
const ty = Scale.linear(Y0, Y1, AXIS_TOP, AXIS_BOT);
const pxPerYear = (AXIS_BOT - AXIS_TOP) / (Y1 - Y0);
// A timeline encodes position, so isZeroBased is correctly false here (s2). What must hold is
// LINEARITY: equal year counts must map to equal pixel counts anywhere on the rail.
const probeA = ty.map(1980) - ty.map(1970), probeB = ty.map(2010) - ty.map(2000);
if (Math.abs(probeA - probeB) > 0.001) throw new Error('time axis is not linear in years');
log('px per year = ' + pxPerYear.toFixed(2) + '   linearity probe ' + probeA.toFixed(3) + ' vs ' + probeB.toFixed(3));

// ---- HEADER. Headline bleeds off the RIGHT edge (s5 rule 3, and the language invites it).
ctx.fillStyle = INK;
ctx.font = '150px Impact';
ctx.letterSpacing = '-0.03em';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillText('WEB-HISTORY', sheet.x + 10, 34);          // deliberately overruns the sheet
ctx.letterSpacing = '0em';
ctx.font = 'bold 19px Arial';
ctx.fillText('SEVEN MILESTONES / 53 YEARS / DRAWN TO SCALE', sheet.x + 14, 196 - 34);

// ---- GAP RIBBONS drawn before the blocks: the silence is an object, not leftover space.
const railX = sheet.x + 244;
for (let i = 1; i < MILESTONES.length; i++) {
    const a = ty.map(MILESTONES[i - 1].year), b = ty.map(MILESTONES[i].year);
    const yrs = MILESTONES[i].year - MILESTONES[i - 1].year;
    const big = yrs >= 15;
    ctx.fillStyle = big ? '#F6D9D4' : '#E4E1D6';
    ctx.fillRect(railX - 46, a, 92, b - a);
    ctx.fillStyle = big ? HOT : GREY;
    ctx.font = big ? 'bold 34px Arial' : 'bold 17px Arial';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    if (b - a > 70) ctx.fillText(yrs + ' YR', railX, (a + b) / 2);
}

ctx.strokeStyle = INK; ctx.lineWidth = 5;
ctx.beginPath(); ctx.moveTo(railX, AXIS_TOP); ctx.lineTo(railX, AXIS_BOT); ctx.stroke();

// ---- MILESTONE BLOCKS. The TICK holds true time position; the BLOCK is offset only
// LATERALLY, never vertically, so the cluster can be de-collided without moving a date.
const BLOCK_H = 66;
const stagger = { 1969: 0, 1989: 0, 1993: 46, 1998: 0, 2004: 92, 2007: 0, 2022: 0 };
MILESTONES.forEach(m => {
    const y = ty.map(m.year);
    const bx = railX + 74 + stagger[m.year];
    const bw = sheet.x2 - bx - 6;
    ctx.fillStyle = INK; ctx.fillRect(railX - 30, y - 4, 60, 8);         // tick: true position
    ctx.strokeStyle = INK; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(railX + 30, y); ctx.lineTo(bx, y); ctx.stroke();   // leader
    ctx.fillStyle = '#FFFFFF';
    ctx.fillRect(bx, y - BLOCK_H / 2, bw, BLOCK_H);
    ctx.strokeStyle = INK; ctx.lineWidth = 5;
    ctx.strokeRect(bx, y - BLOCK_H / 2, bw, BLOCK_H);
    ctx.fillStyle = INK;
    ctx.font = 'bold 30px Arial'; ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
    ctx.fillText(String(m.year), bx + 14, y);
    ctx.font = 'bold 22px Arial';
    ctx.fillText(m.short, bx + 108, y);
});

// ---- TEAR PERFORATION: the boundary between the job and its summary.
ctx.strokeStyle = INK; ctx.lineWidth = 3;
ctx.setLineDash([14, 12]);
ctx.beginPath(); ctx.moveTo(0, TEAR_Y); ctx.lineTo(W, TEAR_Y); ctx.stroke();
ctx.setLineDash([]);

// ---- FOOTER SUMMARY. Off-axis by construction: below the perforation, so nothing here can be
// misread as sitting at a date. Bleeds off the bottom edge.
const footer = Layout.rect(sheet.x, FOOTER_TOP, sheet.width, H - FOOTER_TOP);
const [barsZone, numZone] = Layout.columns(footer, [56, 44], 26);
ctx.fillStyle = GREY; ctx.font = 'bold 15px Arial';
ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.fillText('C - RANKED INTERVALS  (placeholder)', barsZone.x, barsZone.y);
ctx.fillText('D - BIG NUMBER  (placeholder)', numZone.x, numZone.y);
ctx.strokeStyle = GREY; ctx.lineWidth = 2; ctx.setLineDash([8, 8]);
ctx.strokeRect(barsZone.x, barsZone.y + 26, barsZone.width, 240);
ctx.strokeRect(numZone.x, numZone.y + 26, numZone.width, 240);
ctx.setLineDash([]);

// ---- Measurements that decide whether this composition is viable.
const silencePx = ty.map(1989) - ty.map(1969);
const clusterGapPx = ty.map(2007) - ty.map(2004);
log('20-yr silence  = ' + silencePx.toFixed(0) + 'px = ' + (silencePx / H * 100).toFixed(1) + '% of canvas height');
log('2004->2007 gap = ' + clusterGapPx.toFixed(0) + 'px, block height ' + BLOCK_H +
    'px, clearance = ' + (clusterGapPx - BLOCK_H).toFixed(0) + 'px');

canvas;
