Stage.begin('Encode');

// 1. Milestones Data from brief.md
const milestones = [
  { year: 1969, title: "ARPANET", desc: "First message sent (crashed after 'LO')", color: '#ff5252' },
  { year: 1989, title: "WORLD WIDE WEB", desc: "Tim Berners-Lee proposes the Web", color: '#ffde59' },
  { year: 1993, title: "MOSAIC", desc: "Mosaic makes the web visual", color: '#00f0ff' },
  { year: 1998, title: "GOOGLE", desc: "Google is founded", color: '#33ff77' },
  { year: 2004, title: "FACEBOOK", desc: "The social era begins", color: '#ff9900' },
  { year: 2007, title: "iPHONE", desc: "The web in every pocket", color: '#cc66ff' },
  { year: 2022, title: "CHATGPT", desc: "100M users in 2 months", color: '#00ffcc' }
];

// 2. Derived Gaps from brief.md
const gaps = [
  { label: 'ARPANET -> Web', from: 1969, to: 1989, result: 20, rank: 1 },
  { label: 'iPhone -> ChatGPT', from: 2007, to: 2022, result: 15, rank: 2 },
  { label: 'Google -> Facebook', from: 1998, to: 2004, result: 6, rank: 3 },
  { label: 'Mosaic -> Google', from: 1993, to: 1998, result: 5, rank: 4 },
  { label: 'Web -> Mosaic', from: 1989, to: 1993, result: 4, rank: 5 },
  { label: 'Facebook -> iPhone', from: 2004, to: 2007, result: 3, rank: 6 }
];

// Geometry setup
const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4efe6';
ctx.fillRect(0, 0, 1080, 1920);

// Assertions & Scales
// Timeline Scale: 1969 to 2022
const timelineY1 = 360;
const timelineY2 = 1420;
const timeScale = Scale.linear(1969, 2022, timelineY1, timelineY2);

Stage.note('Timeline Scale: Domain [1969, 2022], Pixel range [' + timelineY1 + ', ' + timelineY2 + ']. Span = 53 years.');
log('Timeline mapped 1969 -> ' + timeScale.map(1969) + 'px, 2022 -> ' + timeScale.map(2022) + 'px');

// Gap Bars Scale: Zero-based linear scale
const gapPlot = Layout.rect(580, 420, 440, 500);
const gapBounds = Scale.nice(0, 20, 4);
const gapX = Scale.linear(gapBounds.min, gapBounds.max, gapPlot.x, gapPlot.x2);

if (!gapX.isZeroBased) {
  throw new Error('Gap bars scale must have a zero baseline');
}
Stage.note('Gap bar chart linear scale: isZeroBased=' + gapX.isZeroBased + ', bounds=[' + gapBounds.min + ', ' + gapBounds.max + '], ticks=' + gapX.ticks(4).join(','));

const gapBand = Scale.band(gaps.length, gapPlot.y, gapPlot.y2, 0.3);

// Draw timeline spine and tick marks
ctx.strokeStyle = '#000000';
ctx.lineWidth = 6;
ctx.beginPath();
ctx.moveTo(220, timelineY1 - 20);
ctx.lineTo(220, timelineY2 + 20);
ctx.stroke();

// Year ticks every 10 years
const decadeTicks = [1970, 1980, 1990, 2000, 2010, 2020];
ctx.font = '700 16px monospace';
ctx.fillStyle = '#666666';
for (const yr of decadeTicks) {
  const y = timeScale.map(yr);
  ctx.fillRect(205, y - 2, 30, 4);
  ctx.fillText(yr.toString(), 145, y + 6);
}

// Draw Milestone markers
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const y = timeScale.map(m.year);
  
  // Tick from rail
  ctx.fillStyle = '#000000';
  ctx.fillRect(220, y - 4, 40, 8);
  
  // Event node
  ctx.fillStyle = m.color;
  ctx.fillRect(260, y - 24, 260, 48);
  ctx.strokeRect(260, y - 24, 260, 48);
  
  ctx.fillStyle = '#000000';
  ctx.font = '900 18px monospace';
  ctx.fillText(m.year + ' ' + m.title, 275, y + 6);
}

// Draw Gap Bars
ctx.fillStyle = '#000000';
ctx.font = '900 22px monospace';
ctx.fillText('GAP DURATION (YEARS)', gapPlot.x, gapPlot.y - 20);

// X-axis ticks
for (const tick of gapX.ticks(4)) {
  const tx = gapX.map(tick);
  ctx.strokeStyle = '#cccccc';
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(tx, gapPlot.y);
  ctx.lineTo(tx, gapPlot.y2);
  ctx.stroke();
  
  ctx.fillStyle = '#666666';
  ctx.font = '700 14px monospace';
  ctx.fillText(tick.toString(), tx - 5, gapPlot.y2 + 20);
}

for (let i = 0; i < gaps.length; i++) {
  const g = gaps[i];
  const barY = gapBand.map(i);
  const barH = gapBand.bandwidth;
  const barW = gapX.extent(gapBounds.min, g.result);
  
  // Bar shadow
  ctx.fillStyle = '#000000';
  ctx.fillRect(gapPlot.x + 4, barY + 4, barW, barH);
  
  // Bar fill
  ctx.fillStyle = '#ffde59';
  ctx.fillRect(gapPlot.x, barY, barW, barH);
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3;
  ctx.strokeRect(gapPlot.x, barY, barW, barH);
  
  // Value label
  ctx.fillStyle = '#000000';
  ctx.font = '900 16px monospace';
  ctx.fillText(g.result + ' yrs (' + g.label + ')', gapPlot.x + 10, barY + barH * 0.7);
}

canvas;
