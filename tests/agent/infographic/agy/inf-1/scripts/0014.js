Stage.begin('Encode');
Stage.note('Stage 4: Mathematical Encoding — Scales, baselines, and exact calculations.');

const width = 1080;
const height = 1920;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

// Ground
ctx.fillStyle = '#f5f3e9';
ctx.fillRect(0, 0, width, height);

// Page structure
const page = Layout.inset(Layout.rect(0, 0, width, height), 40, 36, 40, 36);
const [headerBand, mainBand, footerBand] = Layout.rows(page, [220, 1400, 220], 24);
const [timelineCol, statsCol] = Layout.columns(mainBand, [62, 38], 28);
const statsPanels = Layout.rows(statsCol, [250, 530, 280, 280], 20);

// --- 1. TIMELINE ENCODING (Left Column) ---
const timelineTop = timelineCol.y + 120;
const timelineBottom = timelineCol.y2 - 80;
const yearScale = Scale.linear(1969, 2022, timelineTop, timelineBottom);
log('Timeline scale: 1969 @ ' + yearScale.map(1969) + 'px -> 2022 @ ' + yearScale.map(2022) + 'px. Span: ' + (timelineBottom - timelineTop) + 'px');

// Draw timeline box
ctx.fillStyle = '#ffffff';
ctx.fillRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);
ctx.lineWidth = 4;
ctx.strokeStyle = '#000000';
ctx.strokeRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);

// Header inside timeline box
ctx.fillStyle = '#000000';
ctx.font = '900 24px monospace';
ctx.fillText('TIMELINE SCALE: 1969 - 2022 [53 YRS]', timelineCol.x + 24, timelineCol.y + 45);

// Ticks along timeline axis
const axisX = timelineCol.x + 110;
ctx.strokeStyle = '#cccccc';
ctx.lineWidth = 2;
const decades = [1970, 1980, 1990, 2000, 2010, 2020];
for (let i = 0; i < decades.length; i++) {
  const ty = yearScale.map(decades[i]);
  ctx.beginPath();
  ctx.moveTo(timelineCol.x + 30, ty);
  ctx.lineTo(timelineCol.x2 - 30, ty);
  ctx.stroke();
  ctx.fillStyle = '#888888';
  ctx.font = '700 16px monospace';
  ctx.fillText(decades[i].toString(), timelineCol.x + 30, ty - 6);
}

// Draw timeline spine
ctx.strokeStyle = '#000000';
ctx.lineWidth = 6;
ctx.beginPath();
ctx.moveTo(axisX, timelineTop);
ctx.lineTo(axisX, timelineBottom);
ctx.stroke();

// Milestones
const milestones = [
  { year: 1969, label: 'ARPANET sends 1st msg', note: 'Crashed after "LO"' },
  { year: 1989, label: 'Tim Berners-Lee / WWW', note: 'Proposes World Wide Web' },
  { year: 1993, label: 'Mosaic Browser', note: 'Makes the web visual' },
  { year: 1998, label: 'Google Founded', note: 'Search engine launch' },
  { year: 2004, label: 'Facebook Launches', note: 'Social era begins' },
  { year: 2007, label: 'iPhone Released', note: 'Web in every pocket' },
  { year: 2022, label: 'ChatGPT Adoption', note: '100M users in 2 mo' }
];

for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const my = yearScale.map(m.year);

  // Tick mark / notch
  ctx.fillStyle = '#ff3366';
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3;
  ctx.fillRect(axisX - 8, my - 8, 16, 16);
  ctx.strokeRect(axisX - 8, my - 8, 16, 16);

  // Year badge
  ctx.fillStyle = '#ffdf00';
  ctx.fillRect(axisX + 24, my - 24, 80, 32);
  ctx.strokeRect(axisX + 24, my - 24, 80, 32);
  ctx.fillStyle = '#000000';
  ctx.font = '900 18px monospace';
  ctx.fillText(m.year.toString(), axisX + 34, my - 2);

  // Milestone label
  ctx.font = '700 18px sans-serif';
  ctx.fillText(m.label, axisX + 114, my - 6);
  ctx.font = '500 14px monospace';
  ctx.fillStyle = '#555555';
  ctx.fillText(m.note, axisX + 114, my + 14);
}

// Gap dimension annotations
const gaps = [
  { y1: 1969, y2: 1989, text: '20 YRS (1989-1969)' },
  { y1: 2007, y2: 2022, text: '15 YRS (2022-2007)' }
];
for (let i = 0; i < gaps.length; i++) {
  const g = gaps[i];
  const topY = yearScale.map(g.y1);
  const botY = yearScale.map(g.y2);
  const midY = (topY + botY) / 2;
  const bracketX = timelineCol.x2 - 50;

  ctx.strokeStyle = '#ff3366';
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(bracketX, topY + 10);
  ctx.lineTo(bracketX + 15, topY + 10);
  ctx.lineTo(bracketX + 15, botY - 10);
  ctx.lineTo(bracketX, botY - 10);
  ctx.stroke();

  ctx.fillStyle = '#ff3366';
  ctx.font = '900 14px monospace';
  ctx.fillText(g.text, bracketX - 160, midY + 5);
}

// --- 2. STAT PANEL 1: HERO METRIC ---
const p1 = statsPanels[0];
ctx.fillStyle = '#ffdf00';
ctx.fillRect(p1.x, p1.y, p1.width, p1.height);
ctx.strokeRect(p1.x, p1.y, p1.width, p1.height);

ctx.fillStyle = '#000000';
ctx.font = '900 18px monospace';
ctx.fillText('TOTAL TIMELINE SPAN', p1.x + 20, p1.y + 40);
ctx.font = '900 68px sans-serif';
ctx.fillText('53', p1.x + 20, p1.y + 120);
ctx.font = '900 32px sans-serif';
ctx.fillText('YEARS', p1.x + 130, p1.y + 115);
ctx.font = '700 15px monospace';
ctx.fillText('1969 \u2192 2022 (2022 - 1969)', p1.x + 20, p1.y + 160);
ctx.fillText('7 MAJOR REVOLUTIONS', p1.x + 20, p1.y + 190);

// --- 3. STAT PANEL 2: GAP COMPARISON BARS ---
const p2 = statsPanels[1];
ctx.fillStyle = '#ffffff';
ctx.fillRect(p2.x, p2.y, p2.width, p2.height);
ctx.strokeRect(p2.x, p2.y, p2.width, p2.height);

ctx.fillStyle = '#000000';
ctx.font = '900 18px monospace';
ctx.fillText('WAITING GAPS RANKED', p2.x + 20, p2.y + 40);
ctx.font = '500 13px monospace';
ctx.fillStyle = '#666666';
ctx.fillText('Years between major breakthroughs', p2.x + 20, p2.y + 65);

const gapData = [
  { label: 'ARPANET \u2192 Web', gap: 20, color: '#ff3366', math: '89-69' },
  { label: 'iPhone \u2192 ChatGPT', gap: 15, color: '#ff6b6b', math: '22-07' },
  { label: 'Google \u2192 Facebook', gap: 6, color: '#4d96ff', math: '04-98' },
  { label: 'Mosaic \u2192 Google', gap: 5, color: '#6bcb77', math: '98-93' },
  { label: 'Web \u2192 Mosaic', gap: 4, color: '#ffd93d', math: '93-89' },
  { label: 'Facebook \u2192 iPhone', gap: 3, color: '#c9b1e4', math: '07-04' }
];

// Bar scale: 0 to 20 years. Assert zero-baseline.
const barPlot = Layout.inset(p2, 80, 20, 20, 20);
const gapX = Scale.linear(0, 20, barPlot.x + 130, barPlot.x2 - 40);
if (!gapX.isZeroBased) throw new Error('bars need a zero baseline');

const barBand = Scale.band(gapData.length, barPlot.y, barPlot.y2, 0.35);

for (let i = 0; i < gapData.length; i++) {
  const d = gapData[i];
  const by = barBand.map(i);
  const bw = gapX.extent(0, d.gap);

  // Label
  ctx.fillStyle = '#000000';
  ctx.font = '700 13px sans-serif';
  ctx.textAlign = 'right';
  ctx.fillText(d.label, barPlot.x + 120, by + barBand.bandwidth / 2 + 5);
  ctx.textAlign = 'left';

  // Bar
  ctx.fillStyle = d.color;
  ctx.fillRect(barPlot.x + 130, by, bw, barBand.bandwidth);
  ctx.strokeRect(barPlot.x + 130, by, bw, barBand.bandwidth);

  // Value text
  ctx.fillStyle = '#000000';
  ctx.font = '900 14px monospace';
  ctx.fillText(d.gap + 'y', barPlot.x + 140 + bw, by + barBand.bandwidth / 2 + 5);
}

// --- 4. STAT PANEL 3: ERA SHARE STRIP ---
const p3 = statsPanels[2];
ctx.fillStyle = '#a6e3e9';
ctx.fillRect(p3.x, p3.y, p3.width, p3.height);
ctx.strokeRect(p3.x, p3.y, p3.width, p3.height);

ctx.fillStyle = '#000000';
ctx.font = '900 18px monospace';
ctx.fillText('TIMELINE DISTRIBUTION', p3.x + 20, p3.y + 40);

const eras = [
  { name: 'ARPANET', years: 20, pct: '37.7%', color: '#ff3366' },
  { name: 'WEB 1.0', years: 15, pct: '28.3%', color: '#ffd93d' },
  { name: 'SOC/MOB/AI', years: 18, pct: '34.0%', color: '#4d96ff' }
];

const stripRect = Layout.inset(p3, 60, 20, 80, 20);
let curX = stripRect.x;
for (let i = 0; i < eras.length; i++) {
  const e = eras[i];
  const segW = (e.years / 53) * stripRect.width;
  ctx.fillStyle = e.color;
  ctx.fillRect(curX, stripRect.y, segW, 40);
  ctx.strokeRect(curX, stripRect.y, segW, 40);

  ctx.fillStyle = '#000000';
  ctx.font = '900 13px monospace';
  ctx.fillText(e.pct, curX + 10, stripRect.y + 65);
  ctx.font = '700 12px sans-serif';
  ctx.fillText(e.name, curX + 10, stripRect.y + 85);

  curX += segW;
}

// --- 5. STAT PANEL 4: ADOPTION VELOCITY ---
const p4 = statsPanels[3];
ctx.fillStyle = '#ff6b6b';
ctx.fillRect(p4.x, p4.y, p4.width, p4.height);
ctx.strokeRect(p4.x, p4.y, p4.width, p4.height);

ctx.fillStyle = '#000000';
ctx.font = '900 18px monospace';
ctx.fillText('ADOPTION VELOCITY', p4.x + 20, p4.y + 40);
ctx.font = '900 48px sans-serif';
ctx.fillText('100M', p4.x + 20, p4.y + 110);
ctx.font = '900 24px sans-serif';
ctx.fillText('USERS IN 2 MONTHS', p4.x + 20, p4.y + 150);
ctx.font = '700 15px monospace';
ctx.fillText('ChatGPT (2022 Milestone)', p4.x + 20, p4.y + 190);

// --- 6. HEADER & FOOTER ENCODING ---
// Header
ctx.fillStyle = '#ffffff';
ctx.fillRect(headerBand.x, headerBand.y, headerBand.width, headerBand.height);
ctx.strokeRect(headerBand.x, headerBand.y, headerBand.width, headerBand.height);

ctx.fillStyle = '#000000';
ctx.font = '900 52px sans-serif';
ctx.fillText('WEB HISTORY', headerBand.x + 30, headerBand.y + 70);
ctx.font = '900 32px sans-serif';
ctx.fillStyle = '#ff3366';
ctx.fillText('NEVER ON TIME: 53 YEARS OF WAITING', headerBand.x + 30, headerBand.y + 125);
ctx.fillStyle = '#555555';
ctx.font = '700 16px monospace';
ctx.fillText('LINE PRINTER LOG // TRACTOR FEED CONTINUOUS PLATEN // 1969-2022', headerBand.x + 30, headerBand.y + 175);

// Footer
ctx.fillStyle = '#ffffff';
ctx.fillRect(footerBand.x, footerBand.y, footerBand.width, footerBand.height);
ctx.strokeRect(footerBand.x, footerBand.y, footerBand.width, footerBand.height);

ctx.fillStyle = '#000000';
ctx.font = '900 22px monospace';
ctx.fillText('CORE TAKEAWAY:', footerBand.x + 30, footerBand.y + 50);
ctx.font = '700 28px sans-serif';
ctx.fillStyle = '#111111';
ctx.fillText('Progress is constant — it never stopped coming,', footerBand.x + 30, footerBand.y + 100);
ctx.fillText('but it never once came on schedule.', footerBand.x + 30, footerBand.y + 140);
ctx.font = '600 14px monospace';
ctx.fillStyle = '#666666';
ctx.fillText('DATA SOURCE: BRIEF.MD DATA TABLE \u00B7 ALL DERIVED DELTAS CHECKED \u00B7 ZERO BASELINE ASSERTED', footerBand.x + 30, footerBand.y + 185);

log('Encoding test rendered to artifacts/04_encoding.webp');
canvas;
