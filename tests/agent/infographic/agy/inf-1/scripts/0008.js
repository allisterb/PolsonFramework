Stage.begin('Detail');

Stage.note('Finalizing all typography contrast, padding, leader lines, and neo-brutalist marks for Stage 7 Audit.');

const W = 1080;
const H = 1920;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// Neo-brutalist Palette & Tokens
const P = {
  paper: '#FAF6EE',
  paperTractor: '#EDE6D6',
  paperHole: '#2B2925',
  ink: '#0D0D0D',
  yellow: '#FFE600',
  cyan: '#00F0FF',
  coral: '#FF3366',
  coralDark: '#B80033',
  green: '#00E676',
  orange: '#FF8800',
  purple: '#B366FF',
  purpleDark: '#6200B3',
  white: '#FFFFFF',
  gridLine: 'rgba(0,0,0,0.055)'
};

// 1. BASE GROUND: Continuous Fanfold Paper
ctx.fillStyle = P.paper;
ctx.fillRect(0, 0, W, H);

// Continuous tractor feed grid lines (every 24px)
ctx.strokeStyle = P.gridLine;
ctx.lineWidth = 1;
for (let y = 0; y < H; y += 24) {
  ctx.beginPath();
  ctx.moveTo(0, y);
  ctx.lineTo(W, y);
  ctx.stroke();
}

// Side Tractor Margins (46px on left and right)
const marginW = 46;
ctx.fillStyle = P.paperTractor;
ctx.fillRect(0, 0, marginW, H);
ctx.fillRect(W - marginW, 0, marginW, H);

ctx.strokeStyle = P.ink;
ctx.lineWidth = 3.5;
ctx.beginPath();
ctx.moveTo(marginW, 0); ctx.lineTo(marginW, H);
ctx.moveTo(W - marginW, 0); ctx.lineTo(W - marginW, H);
ctx.stroke();

// Tractor feed sprocket pin holes (punched circles every 48px)
for (let y = 24; y < H; y += 48) {
  // Left hole
  ctx.fillStyle = P.paperHole;
  ctx.beginPath();
  ctx.arc(marginW / 2, y, 9.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = P.ink;
  ctx.lineWidth = 2.5;
  ctx.stroke();

  // Right hole
  ctx.fillStyle = P.paperHole;
  ctx.beginPath();
  ctx.arc(W - marginW / 2, y, 9.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

// Horizontal Fanfold Perforation line (page fold at y = 1312)
const foldY = 1312;
ctx.save();
ctx.setLineDash([8, 8]);
ctx.strokeStyle = '#777777';
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(0, foldY);
ctx.lineTo(W, foldY);
ctx.stroke();
ctx.restore();

ctx.fillStyle = '#555555';
ctx.font = '700 11px "Consolas", monospace';
ctx.fillText('--- [ FANFOLD PERFORATION // TEAR HERE ] --------------------------------------------------------------------------------------------------------------------------------------------------', marginW + 12, foldY - 5);

// Helper: Neo-Brutalist Box with Hard Offset Shadow
const drawBox = (x, y, w, h, fill, shadowOffset = 6, strokeW = 3.5, strokeColor = P.ink) => {
  ctx.fillStyle = P.ink;
  ctx.fillRect(x + shadowOffset, y + shadowOffset, w, h);
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  if (strokeW > 0) {
    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = strokeW;
    ctx.strokeRect(x, y, w, h);
  }
};

// 2. HEADER ZONE
const pageInner = Layout.rect(marginW + 16, 16, W - (marginW * 2) - 32, H - 32);

// Top Printer Metadata
ctx.fillStyle = P.ink;
ctx.font = '700 12px "Consolas", monospace';
ctx.fillText('FORM: FF-1080X1920 // LPI: 6 // FEED: CONTINUOUS CALIBRATED // CALIBRATION: EXACT SCALES', pageInner.x, 32);

// Giant Title Box
const titleBoxY = 44;
const titleBoxH = 156;
drawBox(pageInner.x, titleBoxY, pageInner.width, titleBoxH, P.yellow, 8, 4);

// Diagonal Hatching accent in title box corner
ctx.save();
ctx.beginPath();
ctx.rect(pageInner.x + pageInner.width - 130, titleBoxY, 130, titleBoxH);
ctx.clip();
ctx.strokeStyle = P.ink;
ctx.lineWidth = 3.5;
for (let d = -100; d < 260; d += 12) {
  ctx.beginPath();
  ctx.moveTo(pageInner.x + pageInner.width - 130 + d, titleBoxY);
  ctx.lineTo(pageInner.x + pageInner.width - 130 + d + 160, titleBoxY + titleBoxH);
  ctx.stroke();
}
ctx.restore();

// Title Text
ctx.fillStyle = P.ink;
ctx.font = '900 48px "Arial Black", Impact, sans-serif';
ctx.fillText('PROGRESS IS NOT ON SCHEDULE', pageInner.x + 24, titleBoxY + 52);

ctx.font = '900 21px "Consolas", monospace';
ctx.fillText('A SCALE POSTER OF THE INTERNET\'S GREATEST MILESTONES', pageInner.x + 24, titleBoxY + 88);

// Claim subtitle banner
drawBox(pageInner.x + 24, titleBoxY + 104, pageInner.width - 170, 36, P.coral, 0, 2.5);
ctx.fillStyle = P.white;
ctx.font = '900 13.5px "Consolas", monospace';
ctx.fillText('"Progress is constant — it never stopped coming, but it never once came on schedule."', pageInner.x + 36, titleBoxY + 127);

// Boundary-Breaking Hero Stamp: "53 YEARS" (Rotated badge overlapping title and timeline)
ctx.save();
ctx.translate(pageInner.x + pageInner.width - 130, titleBoxY + 132);
ctx.rotate(-0.08); // committed diagonal rotation (~ -4.5 deg)
drawBox(-112, -36, 224, 72, P.cyan, 7, 4);
ctx.fillStyle = P.ink;
ctx.textAlign = 'center';
ctx.font = '900 32px "Arial Black", Impact, sans-serif';
ctx.fillText('53 YEARS', 0, 0);
ctx.font = '900 13px "Consolas", monospace';
ctx.fillText('1969 -> 2022 SPAN', 0, 22);
ctx.restore();
ctx.textAlign = 'left';

// 3. MAIN SECTION: Two Column Editorial Layout
const contentY = 228;
const contentH = 1058;
const [timelineCol, gapCol] = Layout.columns(Layout.rect(pageInner.x, contentY, pageInner.width, contentH), [60, 40], 20);

// ==========================================
// TIMELINE COLUMN (FORM 1)
// ==========================================
drawBox(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height, P.white, 8, 4);

// Header within Timeline
drawBox(timelineCol.x, timelineCol.y, timelineCol.width, 42, P.ink, 0, 0);
ctx.fillStyle = P.white;
ctx.font = '900 15px "Consolas", monospace';
ctx.fillText('FORM 1: CONTINUOUS VERTICAL SCALE TIMELINE', timelineCol.x + 16, timelineCol.y + 27);

// Timeline Mathematical Linear Scale: 1969 to 2022
const timelinePlotY1 = timelineCol.y + 76;
const timelinePlotY2 = timelineCol.y + timelineCol.height - 48;
const timeScale = Scale.linear(1969, 2022, timelinePlotY1, timelinePlotY2);

// Timeline Central Spine Rail
const railX = timelineCol.x + 86;
ctx.strokeStyle = P.ink;
ctx.lineWidth = 6;
ctx.beginPath();
ctx.moveTo(railX, timelinePlotY1 - 12);
ctx.lineTo(railX, timelinePlotY2 + 18);
ctx.stroke();

// Decades Ticks on timeline rail
const decades = [1970, 1980, 1990, 2000, 2010, 2020];
for (const dec of decades) {
  const dy = timeScale.map(dec);
  ctx.fillStyle = P.ink;
  ctx.fillRect(railX - 12, dy - 2, 24, 4);
  ctx.font = '700 12px "Consolas", monospace';
  ctx.fillStyle = '#666666';
  ctx.textAlign = 'right';
  ctx.fillText(dec.toString(), railX - 18, dy + 4);
}
ctx.textAlign = 'left';

// Milestones Data from brief.md
const milestones = [
  { year: 1969, title: "ARPANET", desc: "First message sent (crashed after 'LO')", color: P.coral, note: "CRASH AFTER 'LO'" },
  { year: 1989, title: "WORLD WIDE WEB", desc: "Tim Berners-Lee proposes the Web", color: P.yellow, note: "CERN PROPOSAL" },
  { year: 1993, title: "MOSAIC", desc: "Mosaic makes the web visual", color: P.cyan, note: "FIRST GRAPHICAL BROWSER" },
  { year: 1998, title: "GOOGLE", desc: "Google is founded", color: P.green, note: "SEARCH & PAGERANK" },
  { year: 2004, title: "FACEBOOK", desc: "Social era begins", color: P.orange, note: "SOCIAL ERA BOOM" },
  { year: 2007, title: "iPHONE", desc: "The web in every pocket", color: P.purple, note: "MOBILE EXPLOSION" },
  { year: 2022, title: "CHATGPT", desc: "100M users in 2 months", color: P.green, note: "GEN-AI BREAKTHROUGH" }
];

// Draw 20-Year Incubation Void Callout (1969 -> 1989)
const y1969 = timeScale.map(1969);
const y1989 = timeScale.map(1989);
const midVoidY = (y1969 + y1989) / 2;

drawBox(railX + 28, midVoidY - 50, timelineCol.width - 126, 100, P.paperTractor, 4, 2.5);
ctx.fillStyle = P.coral;
ctx.fillRect(railX + 28, midVoidY - 50, timelineCol.width - 126, 24);
ctx.fillStyle = P.white;
ctx.font = '900 12px "Consolas", monospace';
ctx.fillText('// 20-YEAR QUIET INCUBATION GAP (1969-1989)', railX + 38, midVoidY - 34);

ctx.fillStyle = P.ink;
ctx.font = '700 13px "Segoe UI", Arial, sans-serif';
ctx.fillText('20 years of terminal silence while packet switching,', railX + 38, midVoidY - 12);
ctx.fillText('TCP/IP & email quietly built the foundation.', railX + 38, midVoidY + 8);
ctx.font = '900 12px "Consolas", monospace';
ctx.fillStyle = P.coralDark;
ctx.fillText('GAP: 20 YEARS (1989 - 1969 = 20y)', railX + 38, midVoidY + 32);

// Draw 15-Year Modern Mobile Optimization Void Callout (2007 -> 2022)
const y2007 = timeScale.map(2007);
const y2022 = timeScale.map(2022);
const midVoid2Y = (y2007 + y2022) / 2;

drawBox(railX + 28, midVoid2Y - 42, timelineCol.width - 126, 84, P.paperTractor, 4, 2.5);
ctx.fillStyle = P.purple;
ctx.fillRect(railX + 28, midVoid2Y - 42, timelineCol.width - 126, 22);
ctx.fillStyle = P.white;
ctx.font = '900 12px "Consolas", monospace';
ctx.fillText('// 15-YEAR POST-MOBILE INCUBATION GAP', railX + 38, midVoid2Y - 26);

ctx.fillStyle = P.ink;
ctx.font = '700 13px "Segoe UI", Arial, sans-serif';
ctx.fillText('15 years optimizing feeds, cloud infra & GPU compute', railX + 38, midVoid2Y - 4);
ctx.font = '900 12px "Consolas", monospace';
ctx.fillStyle = P.purpleDark;
ctx.fillText('GAP: 15 YEARS (2022 - 2007 = 15y)', railX + 38, midVoid2Y + 22);

// Draw Milestone Cards on Timeline
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const my = timeScale.map(m.year);

  // Large Year Node on the rail
  ctx.fillStyle = P.ink;
  ctx.fillRect(railX - 8, my - 8, 16, 16);

  // Connector arm to card
  ctx.strokeStyle = P.ink;
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.moveTo(railX + 8, my);
  ctx.lineTo(railX + 28, my);
  ctx.stroke();

  // Milestone Card Dimensions
  const cardX = railX + 28;
  const cardW = timelineCol.width - 126;
  const cardH = 54;
  let cardY = my - 27;

  // Stagger overlapping cards in the dense 1989-2007 cluster
  if (m.year === 1989) cardY = my - 34;
  if (m.year === 1993) cardY = my - 24;
  if (m.year === 1998) cardY = my - 20;
  if (m.year === 2004) cardY = my - 16;
  if (m.year === 2007) cardY = my - 12;
  if (m.year === 2022) cardY = my - 27;

  drawBox(cardX, cardY, cardW, cardH, m.color, 4, 3);

  // Year Badge inside card
  drawBox(cardX + 8, cardY + 8, 64, cardH - 16, P.ink, 0, 0);
  ctx.fillStyle = P.white;
  ctx.font = '900 16px "Consolas", monospace';
  ctx.textAlign = 'center';
  ctx.fillText(m.year.toString(), cardX + 40, cardY + 28);
  ctx.font = '700 9px "Consolas", monospace';
  ctx.fillText('YEAR', cardX + 40, cardY + 41);
  ctx.textAlign = 'left';

  // Event Title & Description
  ctx.fillStyle = P.ink;
  ctx.font = '900 16px "Arial Black", Impact, sans-serif';
  ctx.fillText(m.title, cardX + 82, cardY + 24);

  ctx.font = '700 12px "Segoe UI", Arial, sans-serif';
  ctx.fillText(m.desc, cardX + 82, cardY + 44);
}

// ==========================================
// RIGHT COLUMN: GAP COMPARISON & ANALYTICS (FORM 3)
// ==========================================
drawBox(gapCol.x, gapCol.y, gapCol.width, gapCol.height, P.white, 8, 4);

// Header within Gap Col
drawBox(gapCol.x, gapCol.y, gapCol.width, 42, P.ink, 0, 0);
ctx.fillStyle = P.white;
ctx.font = '900 15px "Consolas", monospace';
ctx.fillText('FORM 3: GAP DURATION LEADERBOARD', gapCol.x + 14, gapCol.y + 27);

// Subtitle
ctx.fillStyle = P.ink;
ctx.font = '700 12px "Consolas", monospace';
ctx.fillText('RANKED WAIT TIMES (YEARS BETWEEN BREAKTHROUGHS)', gapCol.x + 16, gapCol.y + 68);

// Ranked Gaps Data
const rankedGaps = [
  { label: 'ARPANET -> Web', from: 1969, to: 1989, math: '1989 - 1969', yrs: 20, color: P.coral, pct: '37.7%' },
  { label: 'iPhone -> ChatGPT', from: 2007, to: 2022, math: '2022 - 2007', yrs: 15, color: P.purple, pct: '28.3%' },
  { label: 'Google -> Facebook', from: 1998, to: 2004, math: '2004 - 1998', yrs: 6, color: P.orange, pct: '11.3%' },
  { label: 'Mosaic -> Google', from: 1993, to: 1998, math: '1998 - 1993', yrs: 5, color: P.green, pct: '9.4%' },
  { label: 'Web -> Mosaic', from: 1989, to: 1993, math: '1993 - 1989', yrs: 4, color: P.cyan, pct: '7.5%' },
  { label: 'Facebook -> iPhone', from: 2004, to: 2007, math: '2007 - 2004', yrs: 3, color: P.yellow, pct: '5.7%' }
];

// Scale for Gap Bars
const gapPlot = Layout.rect(gapCol.x + 16, gapCol.y + 88, gapCol.width - 32, 530);
const gapBounds = Scale.nice(0, 20, 4);
const gapX = Scale.linear(gapBounds.min, gapBounds.max, gapPlot.x + 8, gapPlot.x + gapPlot.width - 70);

if (!gapX.isZeroBased) throw new Error('Gap bars must have a zero baseline');

const gapBand = Scale.band(rankedGaps.length, gapPlot.y + 16, gapPlot.y + gapPlot.height - 30, 0.32);

// Axis gridlines for gap chart
for (const tick of gapX.ticks(4)) {
  const tx = gapX.map(tick);
  ctx.strokeStyle = '#ded9cd';
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.moveTo(tx, gapPlot.y + 10);
  ctx.lineTo(tx, gapPlot.y + gapPlot.height - 20);
  ctx.stroke();

  ctx.fillStyle = '#777777';
  ctx.font = '700 11px "Consolas", monospace';
  ctx.textAlign = 'center';
  ctx.fillText(tick + 'y', tx, gapPlot.y + gapPlot.height - 6);
}
ctx.textAlign = 'left';

// Draw ranked bars
for (let i = 0; i < rankedGaps.length; i++) {
  const g = rankedGaps[i];
  const by = gapBand.map(i);
  const bh = gapBand.bandwidth;
  const bw = gapX.extent(gapBounds.min, g.yrs);

  // Label row above bar
  ctx.fillStyle = P.ink;
  ctx.font = '900 13px "Consolas", monospace';
  ctx.fillText('#' + (i + 1) + ' ' + g.label, gapPlot.x + 8, by - 6);

  ctx.font = '700 11px "Consolas", monospace';
  ctx.fillStyle = '#666666';
  ctx.textAlign = 'right';
  ctx.fillText(g.math, gapPlot.x + gapPlot.width - 8, by - 6);
  ctx.textAlign = 'left';

  // Bar
  drawBox(gapPlot.x + 8, by, bw, bh, g.color, 4, 2.5);

  // Bar value text
  ctx.fillStyle = P.ink;
  ctx.font = '900 16px "Arial Black", sans-serif';
  ctx.fillText(g.yrs + ' YRS', gapPlot.x + bw + 18, by + bh * 0.72);
}

// Lower sub-panels in Right Col: Arithmetic Takeaways
const insightY = gapPlot.y + gapPlot.height + 10;
const insightH = 175;
drawBox(gapCol.x + 16, insightY, gapCol.width - 32, insightH, P.paperTractor, 4, 2.5);

ctx.fillStyle = P.coral;
ctx.fillRect(gapCol.x + 16, insightY, gapCol.width - 32, 24);
ctx.fillStyle = P.white;
ctx.font = '900 12px "Consolas", monospace';
ctx.fillText('// ARITHMETIC TAKEAWAYS & PATTERNS', gapCol.x + 26, insightY + 16);

ctx.fillStyle = P.ink;
ctx.font = '700 13px "Segoe UI", Arial, sans-serif';
ctx.fillText('• 2 gaps alone (20y + 15y = 35y) account', gapCol.x + 26, insightY + 48);
ctx.fillText('  for 66% of all time since 1969.', gapCol.x + 26, insightY + 68);
ctx.fillText('• 4 middle milestones arrived in a 18-year blitz', gapCol.x + 26, insightY + 92);
ctx.fillText('  averaging only 4.5 years between paradigm shifts.', gapCol.x + 26, insightY + 112);
ctx.font = '900 12px "Consolas", monospace';
ctx.fillStyle = P.coralDark;
ctx.fillText('ARITHMETIC: 35y / 53y = 66.0% QUIET TIME', gapCol.x + 26, insightY + 144);

// Mini Acceleration Callout Box
const accelY = insightY + insightH + 16;
const accelH = gapCol.height - (accelY - gapCol.y) - 16;
drawBox(gapCol.x + 16, accelY, gapCol.width - 32, accelH, P.yellow, 4, 2.5);

drawBox(gapCol.x + 26, accelY + 12, gapCol.width - 52, 24, P.ink, 0, 0);
ctx.fillStyle = P.yellow;
ctx.font = '900 12px "Consolas", monospace';
ctx.fillText('FASTEST ACCELERATION: 3 YRS', gapCol.x + 36, accelY + 28);

ctx.fillStyle = P.ink;
ctx.font = '700 12.5px "Segoe UI", Arial, sans-serif';
ctx.fillText('Facebook (2004) -> iPhone (2007) took just 36 months.', gapCol.x + 26, accelY + 56);
ctx.font = '900 11px "Consolas", monospace';
ctx.fillText('MATH: 2007 - 2004 = 3 YEARS (SHORTEST GAP)', gapCol.x + 26, accelY + 76);

// ==========================================
// 4. FOOTER ZONE: FORM 4 (SEGMENTED SHARE BAR & STAT BADGES)
// ==========================================
const footerY = 1320;
const footerH = H - footerY - 44;
const footerRect = Layout.rect(pageInner.x, footerY, pageInner.width, footerH);

drawBox(footerRect.x, footerRect.y, footerRect.width, footerRect.height, P.white, 8, 4);

// Footer Header
drawBox(footerRect.x, footerRect.y, footerRect.width, 38, P.ink, 0, 0);
ctx.fillStyle = P.white;
ctx.font = '900 14px "Consolas", monospace';
ctx.fillText('FORM 4: 53-YEAR TOTAL TIMELINE CHRONOLOGICAL PROPORTIONS (1969-2022)', footerRect.x + 16, footerRect.y + 25);

// Segmented Bar Geometry (53 Years Total)
const segBarY = footerRect.y + 52;
const segBarW = footerRect.width - 32;
const segBarH = 46;
const segBarX = footerRect.x + 16;

const totalYears = 53;
let currX = segBarX;

// Segmented Bar Shadow
ctx.fillStyle = P.ink;
ctx.fillRect(segBarX + 6, segBarY + 6, segBarW, segBarH);

const chronoSegments = [
  { label: "69-89", yrs: 20, color: P.coral, name: "ARPANET->Web" },
  { label: "89-93", yrs: 4, color: P.cyan, name: "Web->Mosaic" },
  { label: "93-98", yrs: 5, color: P.green, name: "Mosaic->Google" },
  { label: "98-04", yrs: 6, color: P.orange, name: "Google->FB" },
  { label: "04-07", yrs: 3, color: P.yellow, name: "FB->iPhone" },
  { label: "07-22", yrs: 15, color: P.purple, name: "iPhone->GPT" }
];

for (let i = 0; i < chronoSegments.length; i++) {
  const chrono = chronoSegments[i];
  const segW = (chrono.yrs / totalYears) * segBarW;
  
  ctx.fillStyle = chrono.color;
  ctx.fillRect(currX, segBarY, segW, segBarH);
  ctx.strokeStyle = P.ink;
  ctx.lineWidth = 3;
  ctx.strokeRect(currX, segBarY, segW, segBarH);

  // Label inside bar
  if (segW > 40) {
    ctx.fillStyle = P.ink;
    ctx.font = '900 13px "Consolas", monospace';
    ctx.textAlign = 'center';
    ctx.fillText(chrono.yrs + 'y', currX + segW / 2, segBarY + 28);
  }

  // Subtitle / legend under bar
  ctx.fillStyle = P.ink;
  ctx.font = '700 10.5px "Consolas", monospace';
  ctx.textAlign = 'center';
  const legendY = segBarY + segBarH + 15;
  ctx.fillText(chrono.label, currX + segW / 2, legendY);
  ctx.fillText(chrono.name, currX + segW / 2, legendY + 14);

  currX += segW;
}
ctx.textAlign = 'left';

// Bottom 3 Stat Callout Blocks
const statZoneY = segBarY + segBarH + 42;
const statZoneH = footerRect.height - (statZoneY - footerRect.y) - 18;
const [stat1, stat2, stat3] = Layout.columns(Layout.rect(footerRect.x + 16, statZoneY, footerRect.width - 32, statZoneH), [33, 34, 33], 14);

// Stat 1: Incubation Share
drawBox(stat1.x, stat1.y, stat1.width, stat1.height, P.coral, 5, 3.5);
drawBox(stat1.x + 10, stat1.y + 10, stat1.width - 20, 26, P.ink, 0, 0);
ctx.fillStyle = P.white;
ctx.font = '900 12px "Consolas", monospace';
ctx.textAlign = 'center';
ctx.fillText('INCUBATION SHARE', stat1.x + stat1.width / 2, stat1.y + 28);

ctx.font = '900 48px "Arial Black", Impact, sans-serif';
ctx.fillText('66.0%', stat1.x + stat1.width / 2, stat1.y + 88);

ctx.font = '700 12px "Consolas", monospace';
ctx.fillText('35 of 53 Years Total', stat1.x + stat1.width / 2, stat1.y + 120);
ctx.fillText('(20y + 15y Gestation)', stat1.x + stat1.width / 2, stat1.y + 138);

ctx.font = '900 11px "Consolas", monospace';
ctx.fillText('MATH: (20+15)/53 = 66%', stat1.x + stat1.width / 2, stat1.y + 164);

// Stat 2: Boom Share
drawBox(stat2.x, stat2.y, stat2.width, stat2.height, P.yellow, 5, 3.5);
drawBox(stat2.x + 10, stat2.y + 10, stat2.width - 20, 26, P.ink, 0, 0);
ctx.fillStyle = P.white;
ctx.font = '900 12px "Consolas", monospace';
ctx.textAlign = 'center';
ctx.fillText('RAPID BOOM ERA', stat2.x + stat2.width / 2, stat2.y + 28);

ctx.fillStyle = P.ink;
ctx.font = '900 48px "Arial Black", Impact, sans-serif';
ctx.fillText('34.0%', stat2.x + stat2.width / 2, stat2.y + 88);

ctx.font = '700 12px "Consolas", monospace';
ctx.fillText('18 of 53 Years Total', stat2.x + stat2.width / 2, stat2.y + 120);
ctx.fillText('(1989 to 2007 Cluster)', stat2.x + stat2.width / 2, stat2.y + 138);

ctx.font = '900 11px "Consolas", monospace';
ctx.fillText('MATH: 18/53 = 34.0%', stat2.x + stat2.width / 2, stat2.y + 164);

// Stat 3: Total Span Summary
drawBox(stat3.x, stat3.y, stat3.width, stat3.height, P.cyan, 5, 3.5);
drawBox(stat3.x + 10, stat3.y + 10, stat3.width - 20, 26, P.ink, 0, 0);
ctx.fillStyle = P.white;
ctx.font = '900 12px "Consolas", monospace';
ctx.textAlign = 'center';
ctx.fillText('TOTAL TIMELINE SPAN', stat3.x + stat3.width / 2, stat3.y + 28);

ctx.fillStyle = P.ink;
ctx.font = '900 48px "Arial Black", Impact, sans-serif';
ctx.fillText('53 YRS', stat3.x + stat3.width / 2, stat3.y + 88);

ctx.font = '700 12px "Consolas", monospace';
ctx.fillText('1969 to 2022', stat3.x + stat3.width / 2, stat3.y + 120);
ctx.fillText('53 Continuous Years', stat3.x + stat3.width / 2, stat3.y + 138);

ctx.font = '900 11px "Consolas", monospace';
ctx.fillText('MATH: 2022 - 1969 = 53', stat3.x + stat3.width / 2, stat3.y + 164);
ctx.textAlign = 'left';

// 5. Corroborating Registration marks & Printer Edge stamps
ctx.fillStyle = P.ink;
ctx.font = '700 11px "Consolas", monospace';
ctx.fillText('+ [REG 01-A] POLSON HIGH-DENSITY RENDER ENGINE // NO IMAGE GEN // STRICT GEOMETRY // DIEGETIC FANFOLD', marginW + 16, H - 12);

canvas;
