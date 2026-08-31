Stage.begin('Encode');

Stage.note('Stage 4: Mathematical Encoding of Timeline and Gap Intervals.');

const milestones = [
  { id: 1, year: 1969, title: 'ARPANET FIRST PACKET', detail: 'Sends its first message (it crashed after "LO")', gap: null },
  { id: 2, year: 1989, title: 'WORLD WIDE WEB', detail: 'Tim Berners-Lee proposes the World Wide Web', gap: 20 },
  { id: 3, year: 1993, title: 'MOSAIC BROWSER', detail: 'Mosaic makes the web visual', gap: 4 },
  { id: 4, year: 1998, title: 'GOOGLE FOUNDED', detail: 'Google search indexes the exploding web', gap: 5 },
  { id: 5, year: 2004, title: 'FACEBOOK LAUNCH', detail: 'Facebook launches — the social era begins', gap: 6 },
  { id: 6, year: 2007, title: 'IPHONE REVEALED', detail: 'The iPhone puts the web in every pocket', gap: 3 },
  { id: 7, year: 2022, title: 'CHATGPT LAUNCH', detail: 'ChatGPT reaches 100M users in 2 months', gap: 15 }
];

const gaps = milestones.filter(m => m.gap !== null).map(m => m.gap);
const gapExtent = Scale.extent(gaps);
log('Gaps: ' + gaps.join(', ') + ' -> extent: min=' + gapExtent.min + ', max=' + gapExtent.max);

const gapBounds = Scale.nice(0, gapExtent.max);
log('Nice gap bounds: [0, ' + gapBounds.max + ']');

const gapScale = Scale.linear(0, gapBounds.max, 0, 180);
if (!gapScale.isZeroBased) throw new Error('bars and columns need a zero baseline');
Stage.note('Zero-baseline check verified: gapScale isZeroBased = ' + gapScale.isZeroBased);

// Timeline vertical scale: 1969 to 2022
const timelineStartYear = 1969;
const timelineEndYear = 2022;
const totalSpan = timelineEndYear - timelineStartYear;
if (totalSpan !== 53) throw new Error('Timeline span mismatch');
Stage.note('Total timeline span verified: 53 years (1969 to 2022)');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#faf6ee';
ctx.fillRect(0, 0, 1080, 1920);

// Test timeline scale mapping
const plotTop = 320;
const plotBottom = 1620;
const timeScale = Scale.linear(timelineStartYear, timelineEndYear, plotTop, plotBottom);

ctx.strokeStyle = '#111111';
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(180, plotTop);
ctx.lineTo(180, plotBottom);
ctx.stroke();

// Draw ticks along timeline
for (let y = 1970; y <= 2020; y += 10) {
  const py = timeScale.map(y);
  ctx.beginPath();
  ctx.moveTo(170, py);
  ctx.lineTo(190, py);
  ctx.stroke();
  ctx.font = '700 16px Consolas, monospace';
  ctx.fillStyle = '#555555';
  ctx.fillText(y.toString(), 110, py + 5);
}

// Map each milestone
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const my = timeScale.map(m.year);
  
  // Year dot
  ctx.fillStyle = '#ff4b4b';
  ctx.beginPath();
  ctx.arc(180, my, 8, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 3;
  ctx.stroke();
  
  // Year text
  ctx.font = '900 24px Arial, sans-serif';
  ctx.fillStyle = '#111111';
  ctx.fillText(m.year.toString(), 210, my - 10);
  
  // Title
  ctx.font = '700 20px Arial, sans-serif';
  ctx.fillText(m.title, 300, my - 10);
  
  // Detail
  ctx.font = '400 16px Consolas, monospace';
  ctx.fillStyle = '#444444';
  ctx.fillText(m.detail, 300, my + 15);
  
  // If gap, draw gap bar
  if (m.gap !== null) {
    const prevM = milestones[i - 1];
    const prevY = timeScale.map(prevM.year);
    const midY = (prevY + my) / 2;
    const barW = gapScale.extent(0, m.gap);
    
    ctx.fillStyle = '#ffe600';
    ctx.fillRect(800, midY - 12, barW, 24);
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = 2;
    ctx.strokeRect(800, midY - 12, barW, 24);
    
    ctx.font = '700 14px Consolas, monospace';
    ctx.fillStyle = '#111111';
    ctx.fillText('+' + m.gap + ' yrs', 805 + barW, midY + 5);
  }
}

canvas;
