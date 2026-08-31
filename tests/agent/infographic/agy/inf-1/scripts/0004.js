Stage.begin('Encode');

// Verify all mathematical encodings
const milestones = [
    { year: 1969, title: "ARPANET First Message", detail: "Crashed after 'LO'", color: '#ff5c5c' },
    { year: 1989, title: "World Wide Web Proposed", detail: "Tim Berners-Lee / CERN", color: '#ffb703' },
    { year: 1993, title: "Mosaic Browser", detail: "Makes the web visual", color: '#06d6a0' },
    { year: 1998, title: "Google Founded", detail: "Search organises the web", color: '#118ab2' },
    { year: 2004, title: "Facebook Launches", detail: "The social era begins", color: '#8338ec' },
    { year: 2007, title: "The iPhone", detail: "The web in every pocket", color: '#ff006e' },
    { year: 2022, title: "ChatGPT", detail: "100M users in 2 months", color: '#fb5607' }
];

const deltas = [
    { from: 1969, to: 1989, years: 20, label: "1969 -> 1989: ARPANET to Web", math: "1989 - 1969 = 20 yrs" },
    { from: 1989, to: 1993, years: 4,  label: "1989 -> 1993: Web to Mosaic", math: "1993 - 1989 = 4 yrs" },
    { from: 1993, to: 1998, years: 5,  label: "1993 -> 1998: Mosaic to Google", math: "1998 - 1993 = 5 yrs" },
    { from: 1998, to: 2004, years: 6,  label: "1998 -> 2004: Google to FB", math: "2004 - 1998 = 6 yrs" },
    { from: 2004, to: 2007, years: 3,  label: "2004 -> 2007: FB to iPhone", math: "2007 - 2004 = 3 yrs" },
    { from: 2007, to: 2022, years: 15, label: "2007 -> 2022: iPhone to ChatGPT", math: "2022 - 2007 = 15 yrs" }
];

const totalSpan = 2022 - 1969; // 53
Stage.note('Asserting total span = ' + totalSpan + ' years (2022 - 1969).');

const sumDeltas = deltas.reduce((acc, d) => acc + d.years, 0);
if (sumDeltas !== totalSpan) {
    throw new Error('Delta sum mismatch: ' + sumDeltas + ' vs total ' + totalSpan);
}
Stage.note('Delta arithmetic check passed: 20 + 4 + 5 + 6 + 3 + 15 = 53.');

// Canvas setup
const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#f6f2e9';
ctx.fillRect(0, 0, 1080, 1920);

// Timeline vertical scale: 1969 to 2022
const timelineTop = 260;
const timelineBottom = 1680;
const timeScale = Scale.linear(1969, 2022, timelineTop, timelineBottom);

// Delta bar horizontal scale
const barPlotLeft = 680;
const barPlotRight = 990;
const maxDelta = Math.max(...deltas.map(d => d.years));
const deltaBounds = Scale.nice(0, maxDelta);
const deltaScale = Scale.linear(deltaBounds.min, deltaBounds.max, barPlotLeft, barPlotRight);

if (!deltaScale.isZeroBased) {
    throw new Error('Delta scale must have a zero baseline');
}
Stage.note('Delta horizontal bar scale verified zero-based (0 to ' + deltaBounds.max + ' years).');

// Draw basic encoding verification marks
ctx.fillStyle = '#111111';
ctx.font = '700 28px Consolas, monospace';
ctx.fillText('STAGE 4: ENCODING & SCALE VERIFICATION', 80, 80);

ctx.font = '400 16px Consolas, monospace';
ctx.fillText('Timeline range: Y=' + timelineTop + ' (1969) to Y=' + timelineBottom + ' (2022)', 80, 120);
ctx.fillText('Delta bar scale: X=' + barPlotLeft + ' (0y) to X=' + barPlotRight + ' (' + deltaBounds.max + 'y)', 80, 150);

// Draw Timeline Spine
const spineX = 220;
ctx.strokeStyle = '#111111';
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(spineX, timelineTop - 20);
ctx.lineTo(spineX, timelineBottom + 20);
ctx.stroke();

// Year ticks
for (let y = 1970; y <= 2020; y += 10) {
    const py = timeScale.map(y);
    ctx.strokeStyle = '#888888';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(spineX - 15, py);
    ctx.lineTo(spineX + 15, py);
    ctx.stroke();
    ctx.fillStyle = '#555555';
    ctx.font = '600 14px Consolas, monospace';
    ctx.fillText(y.toString(), spineX - 60, py + 5);
}

// Milestone points
for (const m of milestones) {
    const py = timeScale.map(m.year);
    ctx.fillStyle = m.color;
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = 3;
    ctx.fillRect(spineX - 10, py - 10, 20, 20);
    ctx.strokeRect(spineX - 10, py - 10, 20, 20);

    ctx.fillStyle = '#111111';
    ctx.font = '700 16px Consolas, monospace';
    ctx.fillText(m.year + ' : ' + m.title, spineX + 25, py + 5);
}

// Draw Delta Bars
const barBand = Scale.band(deltas.length, timelineTop + 40, timelineBottom - 40, 0.4);
for (let i = 0; i < deltas.length; i++) {
    const d = deltas[i];
    const by = barBand.map(i);
    const bh = barBand.bandwidth;
    const barWidth = deltaScale.extent(deltaBounds.min, d.years);

    ctx.fillStyle = '#ffb703';
    ctx.fillRect(barPlotLeft, by, barWidth, bh);
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = 2;
    ctx.strokeRect(barPlotLeft, by, barWidth, bh);

    ctx.fillStyle = '#111111';
    ctx.font = '700 14px Consolas, monospace';
    ctx.fillText(d.years + ' yrs', barPlotLeft + barWidth + 10, by + bh / 2 + 5);
    ctx.font = '400 12px Consolas, monospace';
    ctx.fillText(d.math, barPlotLeft, by - 6);
}

canvas;
