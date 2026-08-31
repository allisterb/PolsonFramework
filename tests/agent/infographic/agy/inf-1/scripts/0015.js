Stage.begin('Encode');

Stage.note('Encoding Stage: Strict geometric mappings with Scale.linear, Scale.nice, Scale.extent, and Scale.radiusFor.');

const milestones = [
  { year: 1969, title: 'ARPANET', detail: 'Sends its first message (crashed after "LO")' },
  { year: 1989, title: 'World Wide Web', detail: 'Tim Berners-Lee proposes the WWW' },
  { year: 1993, title: 'Mosaic', detail: 'Makes the web visual' },
  { year: 1998, title: 'Google', detail: 'Google is founded' },
  { year: 2004, title: 'Facebook', detail: 'Facebook launches — social era begins' },
  { year: 2007, title: 'iPhone', detail: 'Puts the web in every pocket' },
  { year: 2022, title: 'ChatGPT', detail: 'Reaches 100M users in 2 months' }
];

const gaps = [
  { from: '1969', to: '1989', val: 20, label: '20 yrs' },
  { from: '1989', to: '1993', val: 4, label: '4 yrs' },
  { from: '1993', to: '1998', val: 5, label: '5 yrs' },
  { from: '1998', to: '2004', val: 6, label: '6 yrs' },
  { from: '2004', to: '2007', val: 3, label: '3 yrs' },
  { from: '2007', to: '2022', val: 15, label: '15 yrs' }
];

// 1. Timeline Scale
const yTop = 320;
const yBottom = 1620;
const timeScale = Scale.linear(1969, 2022, yTop, yBottom);

// 2. Gap Bar Scale (Must be zero-based!)
const gapExt = Scale.extent(gaps.map(g => g.val));
const gapNice = Scale.nice(0, gapExt.max);
const gapScale = Scale.linear(0, gapNice.max, 0, 180);
if (!gapScale.isZeroBased) throw new Error('Gap bars must have a zero baseline');
Stage.note('Asserted zero baseline on gap duration scale: isZeroBased=' + gapScale.isZeroBased);

// 3. Radius encoding check
const maxNodeR = 24;
for (const g of gaps) {
  const r = Scale.radiusFor(g.val, gapNice.max, maxNodeR);
  log('Gap ' + g.label + ' -> area-proportional radius = ' + r.toFixed(2) + 'px');
}

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Neo-brutalist green fanfold paper background
ctx.fillStyle = '#eaf7ef';
ctx.fillRect(0, 0, 1080, 1920);

// Alternating green-bar printer bands
ctx.fillStyle = '#d7f0e0';
for (let y = 0; y < 1920; y += 64) {
  if (Math.floor(y / 64) % 2 === 0) {
    ctx.fillRect(0, y, 1080, 64);
  }
}

// Tractor feed strips
const tractW = 55;
ctx.fillStyle = '#111111';
ctx.fillRect(tractW, 0, 3, 1920);
ctx.fillRect(1080 - tractW, 0, 3, 1920);

for (let y = 32; y < 1920; y += 48) {
  ctx.fillStyle = '#0a2318';
  ctx.beginPath();
  ctx.arc(27, y, 9, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(1080 - 27, y, 9, 0, Math.PI * 2);
  ctx.fill();
}

// Header
ctx.fillStyle = '#000000';
ctx.fillRect(85, 45, 910, 230);
ctx.fillStyle = '#00f57a'; // Neon green brutalist header
ctx.fillRect(80, 40, 910, 230);
ctx.strokeStyle = '#000000';
ctx.lineWidth = 4;
ctx.strokeRect(80, 40, 910, 230);

ctx.fillStyle = '#000000';
ctx.font = '900 40px "Consolas", monospace';
ctx.fillText('WEB-HISTORY // LINE-FEED TIMELINE', 110, 95);

ctx.font = '700 20px "Consolas", monospace';
ctx.fillText('A CONTINUOUS SCALE OF THE INTERNET’S GREATEST MILESTONES', 110, 135);

ctx.fillStyle = '#111111';
ctx.font = '700 17px "Consolas", monospace';
ctx.fillText('CLAIM: Progress is constant — it never came on schedule.', 110, 175);
ctx.fillText('SCALE: 1969 → 2022 (53 YEARS) · 1 YEAR ≈ 24.5 PX', 110, 205);
ctx.fillText('FORM: TRUE LINEAR VERTICAL SPINE + ZERO-BASELINE GAP METRICS', 110, 235);

// Timeline Axis Spine
const spineX = 220;

// Decade Ticks on timeline
const decadeTicks = [1970, 1980, 1990, 2000, 2010, 2020];
for (const t of decadeTicks) {
  const ty = timeScale.map(t);
  ctx.strokeStyle = '#88a892';
  ctx.lineWidth = 2;
  ctx.setLineDash([4, 4]);
  ctx.beginPath();
  ctx.moveTo(90, ty);
  ctx.lineTo(990, ty);
  ctx.stroke();
  ctx.setLineDash([]);
  
  // Tick label on left
  ctx.fillStyle = '#2d5a3f';
  ctx.font = '700 16px "Consolas", monospace';
  ctx.textAlign = 'right';
  ctx.fillText(t.toString(), spineX - 25, ty + 5);
}
ctx.textAlign = 'left';

// Main Spine Line
ctx.strokeStyle = '#000000';
ctx.lineWidth = 8;
ctx.beginPath();
ctx.moveTo(spineX, yTop - 20);
ctx.lineTo(spineX, yBottom + 30);
ctx.stroke();

// Draw Milestones & Gaps
// Distinct stagger/offsets for card placement to ensure no overlap in the dense 1993-2007 cluster
const cardHeights = [110, 110, 100, 100, 100, 100, 110];
const targetCardYs = [
  timeScale.map(1969) - 40,
  timeScale.map(1989) - 50,
  timeScale.map(1993) - 30,
  timeScale.map(1998) - 10,
  timeScale.map(2004) + 10,
  timeScale.map(2007) + 30,
  timeScale.map(2022) - 50
];

for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const py = timeScale.map(m.year);
  
  // Timeline node
  ctx.fillStyle = '#00f57a';
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 5;
  ctx.beginPath();
  ctx.arc(spineX, py, 14, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
  
  // Year callout on spine
  ctx.fillStyle = '#000000';
  ctx.font = '900 24px "Consolas", monospace';
  ctx.textAlign = 'right';
  ctx.fillText(m.year.toString(), spineX - 25, py + 8);
  ctx.textAlign = 'left';
  
  // Leader connector line to milestone card
  const cardY = targetCardYs[i];
  const cardX = 320;
  const cardW = 670;
  const cardH = cardHeights[i];
  
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(spineX + 14, py);
  ctx.lineTo(cardX, cardY + cardH / 2);
  ctx.stroke();
  
  // Milestone Card (Brutalist style)
  ctx.fillStyle = '#000000';
  ctx.fillRect(cardX + 6, cardY + 6, cardW, cardH);
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(cardX, cardY, cardW, cardH);
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3;
  ctx.strokeRect(cardX, cardY, cardW, cardH);
  
  // Card header strip
  ctx.fillStyle = '#00f57a';
  ctx.fillRect(cardX, cardY, cardW, 36);
  ctx.strokeRect(cardX, cardY, cardW, 36);
  
  ctx.fillStyle = '#000000';
  ctx.font = '900 20px "Consolas", monospace';
  ctx.fillText(m.year + ' // ' + m.title.toUpperCase(), cardX + 15, cardY + 25);
  
  ctx.fillStyle = '#111111';
  ctx.font = '700 16px "Consolas", monospace';
  ctx.fillText(m.detail, cardX + 15, cardY + 68);
  
  // If there's a following gap, draw gap encoding bar
  if (i < gaps.length) {
    const gap = gaps[i];
    const nextPy = timeScale.map(milestones[i+1].year);
    const midY = (py + nextPy) / 2;
    
    // Draw Gap badge & bar in the spine space or between cards
    const gapBarLen = gapScale.extent(0, gap.val);
    
    // Gap callout badge
    ctx.fillStyle = '#ff3366'; // Brutalist hot pink/magenta accent for gaps
    ctx.fillRect(spineX + 35, midY - 14, 12 + gapBarLen, 28);
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 2;
    ctx.strokeRect(spineX + 35, midY - 14, 12 + gapBarLen, 28);
    
    ctx.fillStyle = '#ffffff';
    ctx.font = '900 14px "Consolas", monospace';
    ctx.fillText('⏳ ' + gap.label, spineX + 42, midY + 5);
  }
}

// Footer
const footY = 1750;
ctx.fillStyle = '#000000';
ctx.fillRect(86, footY + 6, 910, 120);
ctx.fillStyle = '#ffffff';
ctx.fillRect(80, footY, 910, 120);
ctx.strokeStyle = '#000000';
ctx.lineWidth = 4;
ctx.strokeRect(80, footY, 910, 120);

ctx.fillStyle = '#000000';
ctx.font = '700 19px "Consolas", monospace';
ctx.fillText('DATA AUDIT: 7 MILESTONES · 6 QUANTIFIED GAPS · 53 YEARS TOTAL', 110, footY + 45);
ctx.font = '400 16px "Consolas", monospace';
ctx.fillText('MIN GAP: 3 YRS (FB→iPhone) · MAX GAP: 20 YRS (ARPANET→WWW) · MEDIAN: 5.5 YRS', 110, footY + 80);

canvas;
