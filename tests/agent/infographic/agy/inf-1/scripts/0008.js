Stage.begin('Encode');

// 1. Data Verification & Arithmetic
const milestones = [
    { year: 1969, label: 'ARPANET sends its first message (it crashed after "LO")', short: 'ARPANET ("LO")' },
    { year: 1989, label: 'Tim Berners-Lee proposes the World Wide Web', short: 'World Wide Web' },
    { year: 1993, label: 'Mosaic makes the web visual', short: 'Mosaic Browser' },
    { year: 1998, label: 'Google is founded', short: 'Google Founded' },
    { year: 2004, label: 'Facebook launches — the social era begins', short: 'Facebook / Social Era' },
    { year: 2007, label: 'The iPhone puts the web in every pocket', short: 'iPhone / Mobile Web' },
    { year: 2022, label: 'ChatGPT reaches 100M users in 2 months', short: 'ChatGPT (100M/2mo)' }
];

const gaps = [
    { name: 'ARPANET → Web', from: 1969, to: 1989, years: 1989 - 1969, desc: 'The 20-Year Incubation' },
    { name: 'Web → Mosaic', from: 1989, to: 1993, years: 1993 - 1989, desc: 'From Text to Images' },
    { name: 'Mosaic → Google', from: 1993, to: 1998, years: 1998 - 1993, desc: 'Organizing the Chaos' },
    { name: 'Google → Facebook', from: 1998, to: 2004, years: 2004 - 1998, desc: 'Entering the Social Era' },
    { name: 'Facebook → iPhone', from: 2004, to: 2007, years: 2007 - 2004, desc: 'The Mobile Transition' },
    { name: 'iPhone → ChatGPT', from: 2007, to: 2022, years: 2022 - 2007, desc: 'The 15-Year Maturation' }
];

const totalSpan = 2022 - 1969; // 53 years

Stage.note(`Timeline Extent: 1969–2022 (${totalSpan} years). Gaps: ${gaps.map(g => g.years + 'y').join(', ')}.`);

// 2. Canvas & Setup
const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#f6f1e8';
ctx.fillRect(0, 0, 1080, 1920);

// 3. Layout Setup
const page = Layout.inset(Layout.rect(0, 0, 1080, 1920), 40, 40, 60, 60);
const [headerBox, bodyBox, footerBox] = Layout.rows(page, [14, 70, 16], 30);
const [timelineArea, gapBarArea] = Layout.columns(bodyBox, [58, 42], 30);

// 4. Geometry Scale 1: Vertical Timeline (1969 -> 2022)
const tStart = timelineArea.y + 40;
const tEnd = timelineArea.y2 - 40;
const timeScale = Scale.linear(1969, 2022, tStart, tEnd);

// 5. Geometry Scale 2: Gap Duration Horizontal Bars (0 -> 20 years)
const gapExtent = Scale.extent(gaps.map(g => g.years));
const gapBounds = Scale.nice(0, gapExtent.max);
const gapXScale = Scale.linear(0, gapBounds.max, gapBarArea.x + 80, gapBarArea.x2 - 30);
if (!gapXScale.isZeroBased) throw new Error('Gap horizontal bars must have a zero baseline!');
const gapYBand = Scale.band(gaps.length, gapBarArea.y + 70, gapBarArea.y2 - 20, 0.35);

log(`Gap scale bounds: ${gapBounds.min} to ${gapBounds.max}, Zero-based: ${gapXScale.isZeroBased}`);
Stage.note(`Encoding verified: Zero-baseline asserted on gap bars. Linear time scale maps 53 years seamlessly.`);

// Draw Header
ctx.fillStyle = '#000000';
ctx.font = '900 36px "Arial Black", Impact, sans-serif';
ctx.fillText('STAGE 4: GEOMETRIC ENCODING', headerBox.x, headerBox.y + 40);
ctx.font = '700 18px "Courier New", monospace';
ctx.fillStyle = '#ff3366';
ctx.fillText('VERIFYING SCALES, BASELINES, TICKS & METRICS', headerBox.x, headerBox.y + 75);

// Draw Timeline Track
ctx.strokeStyle = '#000000';
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(timelineArea.x + 100, tStart);
ctx.lineTo(timelineArea.x + 100, tEnd);
ctx.stroke();

// Timeline Decadal Ticks
const decadalTicks = [1970, 1980, 1990, 2000, 2010, 2020];
ctx.font = '700 14px "Courier New", monospace';
ctx.fillStyle = '#888888';
ctx.textAlign = 'right';
for (const tick of decadalTicks) {
    const ty = timeScale.map(tick);
    ctx.strokeStyle = '#bbbbbb';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(timelineArea.x + 85, ty);
    ctx.lineTo(timelineArea.x + 100, ty);
    ctx.stroke();
    ctx.fillText(tick.toString(), timelineArea.x + 75, ty + 5);
}

// Milestone Nodes on Timeline
ctx.textAlign = 'left';
for (let i = 0; i < milestones.length; i++) {
    const m = milestones[i];
    const my = timeScale.map(m.year);

    // Node mark
    ctx.fillStyle = '#ff4400';
    ctx.beginPath();
    ctx.arc(timelineArea.x + 100, my, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 3;
    ctx.stroke();

    // Card
    ctx.fillStyle = '#ffffff';
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 2;
    ctx.fillRect(timelineArea.x + 125, my - 20, 340, 48);
    ctx.strokeRect(timelineArea.x + 125, my - 20, 340, 48);

    ctx.fillStyle = '#000000';
    ctx.font = '900 16px "Arial Black", sans-serif';
    ctx.fillText(`${m.year}`, timelineArea.x + 135, my + 4);
    ctx.font = '600 13px "Courier New", monospace';
    ctx.fillText(m.short, timelineArea.x + 185, my + 4);
}

// Draw Gap Bars
ctx.fillStyle = '#000000';
ctx.font = '900 22px "Arial Black", Impact, sans-serif';
ctx.fillText('EPOCH GAPS (YEARS)', gapBarArea.x + 10, gapBarArea.y + 35);

// Ticks on Gap Bar Axis
const xTicks = gapXScale.ticks(4);
ctx.font = '600 12px "Courier New", monospace';
ctx.textAlign = 'center';
for (const xt of xTicks) {
    const px = gapXScale.map(xt);
    ctx.strokeStyle = '#dddddd';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(px, gapBarArea.y + 50);
    ctx.lineTo(px, gapBarArea.y2 - 20);
    ctx.stroke();

    ctx.fillStyle = '#666666';
    ctx.fillText(xt + 'y', px, gapBarArea.y + 62);
}

// Render Bars
ctx.textAlign = 'left';
for (let i = 0; i < gaps.length; i++) {
    const g = gaps[i];
    const by = gapYBand.map(i);
    const bh = gapYBand.bandwidth;
    const barW = gapXScale.extent(0, g.years);

    // Bar fill with solid brutalist shadow
    ctx.fillStyle = '#000000';
    ctx.fillRect(gapBarArea.x + 83, by + 3, barW, bh);

    ctx.fillStyle = g.years >= 15 ? '#ff3366' : '#2563eb';
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 2;
    ctx.fillRect(gapXScale.map(0), by, barW, bh);
    ctx.strokeRect(gapXScale.map(0), by, barW, bh);

    // Label
    ctx.fillStyle = '#000000';
    ctx.font = '700 13px "Courier New", monospace';
    ctx.fillText(g.name, gapBarArea.x - 20, by - 4);

    // Value inside/outside
    ctx.fillStyle = '#ffffff';
    ctx.font = '900 16px "Arial Black", sans-serif';
    ctx.fillText(`${g.years}y`, gapXScale.map(0) + 8, by + bh - 6);
}

// Area Encoding Demo / Stat in Footer
const maxRad = 45;
const r53 = Scale.radiusFor(53, 53, maxRad);
const r20 = Scale.radiusFor(20, 53, maxRad);
const r15 = Scale.radiusFor(15, 53, maxRad);

ctx.fillStyle = '#000000';
ctx.font = '700 18px "Courier New", monospace';
ctx.fillText('AREA ENCODING PROOF: 53y Total vs 20y Drought vs 15y Mobile-AI Gap', footerBox.x, footerBox.y + 25);

const drawAreaCircle = (cx, cy, r, val, label) => {
    ctx.fillStyle = '#000000';
    ctx.beginPath();
    ctx.arc(cx + 3, cy + 3, r, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#10b981';
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = '#ffffff';
    ctx.font = '900 18px "Arial Black", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(val, cx, cy + 6);
    ctx.fillStyle = '#000000';
    ctx.font = '700 13px "Courier New", monospace';
    ctx.fillText(label, cx, cy + r + 20);
};

drawAreaCircle(footerBox.x + 200, footerBox.y + 80, r53, '53y', 'TOTAL SPAN');
drawAreaCircle(footerBox.x + 500, footerBox.y + 80, r20, '20y', 'EARLY DROUGHT');
drawAreaCircle(footerBox.x + 800, footerBox.y + 80, r15, '15y', 'MOBILE-AI GAP');

canvas;
