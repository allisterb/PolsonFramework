Stage.begin('Encode');

Stage.note('Mathematical Encoding & Scale Assertions:');
Stage.note('- Vertical Timeline: Linearly mapped over 53 years (1969 to 2022). Every pixel difference corresponds to (Y_span / 53) pixels per year.');
Stage.note('- Horizontal Gap Bars: Strictly zero-based linear scale from 0 to 20 years. Asserted scale.isZeroBased === true.');
Stage.note('- 53-Year Macro Waffle: 1 square = 1 calendar year. Exactly 53 units partitioned into 20 (ARPANET-to-Web), 18 (Internet Boom), 15 (iPhone-to-AI).');

log('=== STAGE 4: ENCODING & SCALE VALIDATION ===');

const width = 1080;
const height = 1920;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#F5F2EB';
ctx.fillRect(0, 0, width, height);

// Define timeline bounds
const spineTop = 320;
const spineBottom = 1380;
const spineHeight = spineBottom - spineTop; // 1060 px

const timeScale = Scale.linear(1969, 2022, spineTop, spineBottom);
const pxPerYear = spineHeight / 53;
log('Time scale: 1969 -> ' + timeScale.map(1969) + 'px, 2022 -> ' + timeScale.map(2022) + 'px');
log('Resolution: ' + pxPerYear.toFixed(2) + ' px/year');

const milestones = [
  { year: 1969, label: '1969: ARPANET First Message', desc: 'Crashed after "LO"' },
  { year: 1989, label: '1989: Tim Berners-Lee / WWW', desc: 'Web Proposal' },
  { year: 1993, label: '1993: Mosaic Web Browser', desc: 'Visual Web' },
  { year: 1998, label: '1998: Google Founded', desc: 'Search Era' },
  { year: 2004, label: '2004: Facebook Launches', desc: 'Social Era' },
  { year: 2007, label: '2007: iPhone Announced', desc: 'Pocket Web' },
  { year: 2022, label: '2022: ChatGPT Launch', desc: '100M in 2 Mo' }
];

const computedPositions = milestones.map(m => {
  const y = timeScale.map(m.year);
  return { year: m.year, y: Number(y.toFixed(1)), label: m.label };
});
table(computedPositions);

// Verify Gaps
const gaps = [
  { pair: '1969 → 1989 (ARPANET to Web)', years: 20, pyGap: timeScale.map(1989) - timeScale.map(1969) },
  { pair: '1989 → 1993 (Web to Mosaic)', years: 4, pyGap: timeScale.map(1993) - timeScale.map(1989) },
  { pair: '1993 → 1998 (Mosaic to Google)', years: 5, pyGap: timeScale.map(1998) - timeScale.map(1993) },
  { pair: '1998 → 2004 (Google to Facebook)', years: 6, pyGap: timeScale.map(2004) - timeScale.map(1998) },
  { pair: '2004 → 2007 (Facebook to iPhone)', years: 3, pyGap: timeScale.map(2007) - timeScale.map(2004) },
  { pair: '2007 → 2022 (iPhone to ChatGPT)', years: 15, pyGap: timeScale.map(2022) - timeScale.map(2007) }
];
table(gaps.map(g => ({ pair: g.pair, years: g.years, pxHeight: Number(g.pyGap.toFixed(1)) })));

// Gap Bar Chart Scale Assertions
const gapChartBounds = Scale.nice(0, 20);
const barPlotX = 640;
const barPlotW = 340;
const gapScale = Scale.linear(gapChartBounds.min, gapChartBounds.max, barPlotX, barPlotX + barPlotW);

if (!gapScale.isZeroBased) {
  throw new Error('Assertion Failed: Gap bar chart must be zero-based!');
}
log('Zero baseline check passed: gapScale.isZeroBased = ' + gapScale.isZeroBased);
log('Gap ticks: ' + gapScale.ticks(4).join(', '));

// Draw test encoding diagram
// 1. Draw timeline spine line
ctx.strokeStyle = '#000000';
ctx.lineWidth = 6;
ctx.beginPath();
ctx.moveTo(220, spineTop);
ctx.lineTo(220, spineBottom);
ctx.stroke();

// Draw tick marks and labels on timeline
for (const m of milestones) {
  const my = timeScale.map(m.year);
  
  // Tick
  ctx.fillStyle = '#000000';
  ctx.fillRect(200, my - 3, 40, 6);
  
  // Badge box
  ctx.fillStyle = '#FFE500';
  ctx.fillRect(250, my - 24, 300, 48);
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3;
  ctx.strokeRect(250, my - 24, 300, 48);
  
  ctx.fillStyle = '#000000';
  ctx.font = '700 16px Arial, sans-serif';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillText(m.label, 260, my);
}

// 2. Draw Gap Bars
const sortedGaps = [
  { label: 'ARPANET → Web', years: 20, color: '#FF3B30' },
  { label: 'iPhone → ChatGPT', years: 15, color: '#FF3B30' },
  { label: 'Google → Facebook', years: 6, color: '#00E676' },
  { label: 'Mosaic → Google', years: 5, color: '#00E676' },
  { label: 'Web → Mosaic', years: 4, color: '#00E676' },
  { label: 'Facebook → iPhone', years: 3, color: '#00E676' }
];

const gapBand = Scale.band(sortedGaps.length, 360, 720, 0.28);
for (let i = 0; i < sortedGaps.length; i++) {
  const g = sortedGaps[i];
  const gy = gapBand.map(i);
  const barLen = gapScale.extent(0, g.years);
  
  ctx.fillStyle = '#000000';
  ctx.font = '700 13px Consolas, monospace';
  ctx.textAlign = 'left';
  ctx.fillText(g.label + ' (' + g.years + ' YRS)', barPlotX, gy - 6);
  
  // Shadow
  ctx.fillStyle = '#000000';
  ctx.fillRect(barPlotX + 4, gy + 4, barLen, gapBand.bandwidth);
  
  // Bar
  ctx.fillStyle = g.color;
  ctx.fillRect(barPlotX, gy, barLen, gapBand.bandwidth);
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3;
  ctx.strokeRect(barPlotX, gy, barLen, gapBand.bandwidth);
}

canvas;
