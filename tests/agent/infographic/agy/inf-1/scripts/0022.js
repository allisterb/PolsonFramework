Stage.begin('Encode');
Stage.note('Truthful geometry: Zero-based gap scales, linear continuous time mapping, and area-encoded callouts.');

const W = 1080;
const H = 1920;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#f6f3eb';
ctx.fillRect(0, 0, W, H);

// Raw and derived data strictly from brief.md
const milestones = [
  { year: 1969, title: 'ARPANET FIRST MESSAGE', desc: 'Sends first message (crashed after "LO")', gap: 20, punchline: 'The network began with a crash.' },
  { year: 1989, title: 'WORLD WIDE WEB', desc: 'Tim Berners-Lee proposes the World Wide Web', gap: 4, punchline: '20 years of silence, then hyperlinks.' },
  { year: 1993, title: 'MOSAIC BROWSER', desc: 'Mosaic makes the web visual', gap: 5, punchline: 'Suddenly text had pictures.' },
  { year: 1998, title: 'GOOGLE FOUNDED', desc: 'Google is founded', gap: 6, punchline: 'Index the entire planet in an input box.' },
  { year: 2004, title: 'FACEBOOK LAUNCH', desc: 'Facebook launches — the social era begins', gap: 3, punchline: 'Everyone got online to argue with high school friends.' },
  { year: 2007, title: 'IPHONE REVEALED', desc: 'The iPhone puts the web in every pocket', gap: 15, punchline: 'The internet is now attached to your hand.' },
  { year: 2022, title: 'CHATGPT LAUNCH', desc: 'ChatGPT reaches 100M users in 2 months', gap: 0, punchline: '100M users in 60 days. Machines talk back.' }
];

// Verify arithmetic
const gaps = [20, 4, 5, 6, 3, 15];
const totalSpan = 2022 - 1969;
if (totalSpan !== 53) throw new Error('Total span mismatch');
const sumGaps = gaps.reduce((a, b) => a + b, 0);
if (sumGaps !== 53) throw new Error('Sum of gaps mismatch');

// Geometry Layout
const tractorW = 70;
const page = Layout.inset(Layout.rect(0, 0, W, H), 0, tractorW, 0, tractorW);
const [headerRect, bodyRect, footerRect] = Layout.rows(page, [220, 1520, 180]);
const [spineRect, contentRect] = Layout.columns(bodyRect, [28, 72], 28);

// Time Scale (Linear vertical mapping 1969 to 2022)
const timePaddingTop = 40;
const timePaddingBottom = 40;
const timeScale = Scale.linear(1969, 2022, spineRect.y + timePaddingTop, spineRect.y2 - timePaddingBottom);

// Gap Bar Scale (Zero-based horizontal length mapping)
const maxGap = Math.max(...gaps);
const gapBounds = Scale.nice(0, maxGap);
const maxBarWidth = spineRect.width - 40;
const gapScale = Scale.linear(0, gapBounds.max, 0, maxBarWidth);

// Assertions from Manual 13 §2
if (!gapScale.isZeroBased) throw new Error('bars and columns need a zero baseline');
Stage.note('Assertion passed: gapScale is zero-based (0 to ' + gapBounds.max + ' years).');

// Verify area scaling for 100M callout
const maxRadius = 45;
const r100M = Scale.radiusFor(100, 100, maxRadius);
const r10M = Scale.radiusFor(10, 100, maxRadius);
Stage.note('Area scaling verified: 100M r=' + r100M.toFixed(1) + 'px, 10M r=' + r10M.toFixed(1) + 'px (ratio ' + (r100M / r10M).toFixed(2) + ' == sqrt(10)=' + Math.sqrt(10).toFixed(2) + ')');

// Draw Encoding Geometry
ctx.strokeStyle = '#111111';
ctx.lineWidth = 2;

// Draw time spine axis
const axisX = spineRect.x + 30;
ctx.beginPath();
ctx.moveTo(axisX, spineRect.y);
ctx.lineTo(axisX, spineRect.y2);
ctx.stroke();

// Year ticks along the spine
ctx.font = '600 14px monospace';
ctx.fillStyle = '#111111';
for (let y = 1970; y <= 2020; y += 10) {
  const py = timeScale.map(y);
  ctx.beginPath();
  ctx.moveTo(axisX - 8, py);
  ctx.lineTo(axisX + 8, py);
  ctx.stroke();
  ctx.fillText(y.toString(), axisX + 14, py + 4);
}

// Render each milestone and gap bar
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const py = timeScale.map(m.year);

  // Milestone marker dot on spine
  ctx.fillStyle = '#ff3300';
  ctx.beginPath();
  ctx.arc(axisX, py, 7, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();

  // Year label on spine
  ctx.fillStyle = '#000000';
  ctx.font = '900 18px monospace';
  ctx.textAlign = 'right';
  ctx.fillText(m.year.toString(), axisX - 14, py + 6);
  ctx.textAlign = 'left';

  // Gap bar (if gap > 0)
  if (m.gap > 0) {
    const nextY = timeScale.map(milestones[i + 1].year);
    const midY = (py + nextY) / 2;
    const barW = gapScale.map(m.gap);

    // Gap bar
    ctx.fillStyle = 'rgba(255, 180, 0, 0.4)';
    ctx.fillRect(axisX + 10, midY - 10, barW, 20);
    ctx.strokeRect(axisX + 10, midY - 10, barW, 20);

    ctx.fillStyle = '#000000';
    ctx.font = '700 12px monospace';
    ctx.fillText('+' + m.gap + 'y GAP', axisX + 16, midY + 4);
  }

  // Event callout box placeholder in contentRect
  const boxY = py - 30;
  ctx.strokeRect(contentRect.x, boxY, contentRect.width - 20, 70);
  ctx.fillStyle = '#111111';
  ctx.font = '700 18px monospace';
  ctx.fillText(m.title, contentRect.x + 15, boxY + 28);
  ctx.font = '400 13px sans-serif';
  ctx.fillText(m.desc, contentRect.x + 15, boxY + 52);
}

canvas;
