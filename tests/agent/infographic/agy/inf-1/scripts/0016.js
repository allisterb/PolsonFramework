Stage.begin('Palette & Type');
Stage.note('Stage 5: Applying Neo-Brutalist design language — 6px hard solid shadows, 4px black borders, saturated flats, tight display type.');

const width = 1080;
const height = 1920;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

// Helper: Draw Neo-Brutalist Block with Hard Offset Shadow
const drawBrutalistBlock = (x, y, w, h, fill, shadowOffset = 6, strokeWidth = 4) => {
  // Hard shadow (solid ink offset)
  ctx.fillStyle = '#000000';
  ctx.fillRect(x + shadowOffset, y + shadowOffset, w, h);
  // Main surface
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  // Border
  ctx.lineWidth = strokeWidth;
  ctx.strokeStyle = '#000000';
  ctx.strokeRect(x, y, w, h);
};

// Ground: Fanfold paper base tone
ctx.fillStyle = '#f3efe6';
ctx.fillRect(0, 0, width, height);

// Draw tractor feed margins (left & right margins with sprocket holes)
const drawTractorFeed = () => {
  const marginW = 32;
  // Left band
  ctx.fillStyle = '#eae4d5';
  ctx.fillRect(0, 0, marginW, height);
  ctx.fillRect(width - marginW, 0, marginW, height);

  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(marginW, 0);
  ctx.lineTo(marginW, height);
  ctx.moveTo(width - marginW, 0);
  ctx.lineTo(width - marginW, height);
  ctx.stroke();

  // Sprocket holes
  for (let y = 30; y < height; y += 45) {
    // Left hole
    ctx.fillStyle = '#d8d0be';
    ctx.fillRect(8, y, 16, 16);
    ctx.strokeRect(8, y, 16, 16);

    // Right hole
    ctx.fillRect(width - 24, y, 16, 16);
    ctx.strokeRect(width - 24, y, 16, 16);
  }
};
drawTractorFeed();

// Layout Zones inside printable area
const page = Layout.inset(Layout.rect(32, 0, width - 64, height), 36, 24, 36, 24);
const [headerBand, mainBand, footerBand] = Layout.rows(page, [230, 1380, 220], 22);
const [timelineCol, statsCol] = Layout.columns(mainBand, [62, 38], 24);
const statsPanels = Layout.rows(statsCol, [250, 520, 280, 280], 20);

// --- 1. HEADER ZONE ---
drawBrutalistBlock(headerBand.x, headerBand.y, headerBand.width, headerBand.height, '#ffffff');

// Header title banner
drawBrutalistBlock(headerBand.x + 20, headerBand.y + 20, headerBand.width - 40, 75, '#ffe600', 4);
ctx.fillStyle = '#000000';
ctx.font = '900 52px "Impact", "Arial Black", sans-serif';
ctx.save();
ctx.letterSpacing = LogoType.computeWordmarkTracking(52, true, 'wordmark') + 'em';
ctx.fillText('WEB HISTORY: NEVER ON TIME', headerBand.x + 35, headerBand.y + 75);
ctx.restore();

// Subheader & Printer Telemetry
ctx.font = '900 24px "Segoe UI", Arial, sans-serif';
ctx.fillStyle = '#ff2a6d';
ctx.fillText('53 YEARS OF WAITING FOR THE NEXT BIG THING (1969 \u2014 2022)', headerBand.x + 25, headerBand.y + 135);

ctx.font = '700 14px "Consolas", monospace';
ctx.fillStyle = '#444444';
ctx.fillText('TELEMETRY: TRACTOR FEED CONTINUOUS PLATEN // 7 KEY MILESTONES // SCALE: 1:1 LINEAR', headerBand.x + 25, headerBand.y + 170);
ctx.fillText('DIAGNOSTIC: ZERO-BASED ENCODINGS ASSERTED // ALL GAPS MEASURED TO TRUE DURATION', headerBand.x + 25, headerBand.y + 195);

// Perforation line below header
ctx.strokeStyle = '#000000';
ctx.lineWidth = 2;
ctx.setLineDash([8, 8]);
ctx.beginPath();
ctx.moveTo(headerBand.x, headerBand.y2 + 11);
ctx.lineTo(headerBand.x2, headerBand.y2 + 11);
ctx.stroke();
ctx.setLineDash([]);

// --- 2. TIMELINE ZONE (ZONE A - 62% Width) ---
drawBrutalistBlock(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height, '#ffffff');

// Timeline Subheader Badge
drawBrutalistBlock(timelineCol.x + 20, timelineCol.y + 20, timelineCol.width - 40, 48, '#05d9e8', 4);
ctx.fillStyle = '#000000';
ctx.font = '900 20px "Segoe UI", Arial, sans-serif';
ctx.fillText('CONTINUOUS PLATEN: REAL TIME SCALE (1969\u20132022)', timelineCol.x + 35, timelineCol.y + 52);

// Linear Year Scale
const timelineTop = timelineCol.y + 120;
const timelineBottom = timelineCol.y2 - 70;
const yearScale = Scale.linear(1969, 2022, timelineTop, timelineBottom);
if (!yearScale) throw new Error('yearScale failed');

// Gridlines for decades
const decades = [1970, 1980, 1990, 2000, 2010, 2020];
ctx.strokeStyle = '#e0ded6';
ctx.lineWidth = 2;
for (let i = 0; i < decades.length; i++) {
  const dy = yearScale.map(decades[i]);
  ctx.beginPath();
  ctx.moveTo(timelineCol.x + 25, dy);
  ctx.lineTo(timelineCol.x2 - 25, dy);
  ctx.stroke();
  ctx.fillStyle = '#999999';
  ctx.font = '700 15px "Consolas", monospace';
  ctx.fillText(decades[i].toString(), timelineCol.x + 28, dy - 6);
}

// Timeline Heavy Spine
const axisX = timelineCol.x + 105;
ctx.fillStyle = '#000000';
ctx.fillRect(axisX - 4, timelineTop, 8, timelineBottom - timelineTop);

// Milestones Data
const milestones = [
  { year: 1969, title: 'ARPANET First Message', desc: 'Sends first packet; crashed after "LO"', fill: '#ff2a6d', tag: 'BIRTH' },
  { year: 1989, title: 'World Wide Web Proposal', desc: 'Tim Berners-Lee invents HTTP / HTML', fill: '#ffe600', tag: 'FOUNDATION', crossBoundary: true },
  { year: 1993, title: 'NCSA Mosaic Browser', desc: 'Makes the Web visual & navigable', fill: '#00e676', tag: 'BROWSER' },
  { year: 1998, title: 'Google Founded', desc: 'PageRank organizes the chaos', fill: '#05d9e8', tag: 'SEARCH' },
  { year: 2004, title: 'Facebook Launches', desc: 'The social network era begins', fill: '#d600ff', tag: 'SOCIAL' },
  { year: 2007, title: 'Apple iPhone Released', desc: 'Puts the entire web in every pocket', fill: '#ff5e00', tag: 'MOBILE' },
  { year: 2022, title: 'ChatGPT AI Explosion', desc: '100M users adopted in 2 months', fill: '#ff2a6d', tag: 'INTELLIGENCE' }
];

for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const my = yearScale.map(m.year);

  // Axis notch / anchor mark
  drawBrutalistBlock(axisX - 12, my - 12, 24, 24, '#000000', 0, 0);

  // Connecting arm
  ctx.fillStyle = '#000000';
  ctx.fillRect(axisX + 12, my - 3, 20, 6);

  // Milestone Badge Box
  const badgeX = axisX + 32;
  const badgeW = m.crossBoundary ? timelineCol.width - 120 : timelineCol.width - 165;
  const badgeH = 68;
  const badgeY = my - badgeH / 2;

  drawBrutalistBlock(badgeX, badgeY, badgeW, badgeH, '#ffffff', 4, 3);

  // Year Tag inside badge
  drawBrutalistBlock(badgeX + 8, badgeY + 8, 70, badgeH - 16, m.fill, 0, 2);
  ctx.fillStyle = '#000000';
  ctx.font = '900 18px "Consolas", monospace';
  ctx.textAlign = 'center';
  ctx.fillText(m.year.toString(), badgeX + 43, badgeY + badgeH / 2 + 6);
  ctx.textAlign = 'left';

  // Text inside badge
  ctx.fillStyle = '#000000';
  ctx.font = '900 18px "Segoe UI", Arial, sans-serif';
  ctx.fillText(m.title, badgeX + 90, badgeY + 28);
  ctx.font = '600 13px "Segoe UI", Arial, sans-serif';
  ctx.fillStyle = '#333333';
  ctx.fillText(m.desc, badgeX + 90, badgeY + 52);
}

// Visual Gap Brackets on Timeline (Tension: 20-year desert vs 15-year quiet period)
const drawGapIndicator = (y1, y2, label, math) => {
  const topY = yearScale.map(y1);
  const botY = yearScale.map(y2);
  const midY = (topY + botY) / 2;
  const bx = timelineCol.x2 - 35;

  ctx.strokeStyle = '#ff2a6d';
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(bx - 12, topY + 14);
  ctx.lineTo(bx, topY + 14);
  ctx.lineTo(bx, botY - 14);
  ctx.lineTo(bx - 12, botY - 14);
  ctx.stroke();

  drawBrutalistBlock(bx - 145, midY - 18, 135, 36, '#ff2a6d', 3, 2);
  ctx.fillStyle = '#ffffff';
  ctx.font = '900 14px "Consolas", monospace';
  ctx.textAlign = 'center';
  ctx.fillText(label, bx - 77, midY + 5);
  ctx.textAlign = 'left';
};
drawGapIndicator(1969, 1989, '20 YR GAP', '1989-1969');
drawGapIndicator(2007, 2022, '15 YR GAP', '2022-2007');

// --- 3. STAT PANELS (ZONE B - 38% Width) ---

// Panel 1: Hero Stat (53 Years)
const p1 = statsPanels[0];
drawBrutalistBlock(p1.x, p1.y, p1.width, p1.height, '#ffe600');
ctx.fillStyle = '#000000';
ctx.font = '900 18px "Consolas", monospace';
ctx.fillText('SPAN: 1969 \u2192 2022', p1.x + 20, p1.y + 36);

ctx.font = '900 76px "Impact", "Arial Black", sans-serif';
ctx.fillText('53', p1.x + 20, p1.y + 115);
ctx.font = '900 36px "Impact", sans-serif';
ctx.fillText('YEARS', p1.x + 125, p1.y + 110);

ctx.font = '700 15px "Segoe UI", Arial, sans-serif';
ctx.fillText('ARITHMETIC: 2022 \u2212 1969 = 53', p1.x + 20, p1.y + 160);
ctx.font = '600 14px "Consolas", monospace';
ctx.fillText('7 MILESTONES ACROSS 5 DECADES', p1.x + 20, p1.y + 190);

// Panel 2: Gap Comparison Ranked Bars
const p2 = statsPanels[1];
drawBrutalistBlock(p2.x, p2.y, p2.width, p2.height, '#ffffff');

ctx.fillStyle = '#000000';
ctx.font = '900 18px "Consolas", monospace';
ctx.fillText('WAITING GAPS RANKED', p2.x + 20, p2.y + 36);
ctx.font = '600 12px "Consolas", monospace';
ctx.fillStyle = '#666666';
ctx.fillText('Duration between revolutions (Zero-based)', p2.x + 20, p2.y + 60);

const gapData = [
  { label: 'ARPANET\u2192Web', gap: 20, color: '#ff2a6d' },
  { label: 'iPhone\u2192ChatGPT', gap: 15, color: '#ff5e00' },
  { label: 'Google\u2192FB', gap: 6, color: '#d600ff' },
  { label: 'Mosaic\u2192Google', gap: 5, color: '#05d9e8' },
  { label: 'Web\u2192Mosaic', gap: 4, color: '#00e676' },
  { label: 'FB\u2192iPhone', gap: 3, color: '#ffe600' }
];

const barPlot = Layout.inset(p2, 80, 20, 20, 20);
const gapX = Scale.linear(0, 20, barPlot.x + 130, barPlot.x2 - 50);
if (!gapX.isZeroBased) throw new Error('bars need zero baseline');

const barBand = Scale.band(gapData.length, barPlot.y, barPlot.y2, 0.32);

for (let i = 0; i < gapData.length; i++) {
  const d = gapData[i];
  const by = barBand.map(i);
  const bw = gapX.extent(0, d.gap);

  // Label
  ctx.fillStyle = '#000000';
  ctx.font = '700 13px "Segoe UI", Arial, sans-serif';
  ctx.textAlign = 'right';
  ctx.fillText(d.label, barPlot.x + 120, by + barBand.bandwidth / 2 + 5);
  ctx.textAlign = 'left';

  // Brutalist Bar with shadow
  drawBrutalistBlock(barPlot.x + 130, by, bw, barBand.bandwidth, d.color, 3, 2);

  // Value text
  ctx.fillStyle = '#000000';
  ctx.font = '900 14px "Consolas", monospace';
  ctx.fillText(d.gap + 'y', barPlot.x + 140 + bw, by + barBand.bandwidth / 2 + 5);
}

// Panel 3: Era Share Strip
const p3 = statsPanels[2];
drawBrutalistBlock(p3.x, p3.y, p3.width, p3.height, '#05d9e8');
ctx.fillStyle = '#000000';
ctx.font = '900 18px "Consolas", monospace';
ctx.fillText('TIMELINE SHARE (53Y)', p3.x + 20, p3.y + 36);

const eras = [
  { name: 'ARPANET', years: 20, pct: '37.7%', color: '#ff2a6d' },
  { name: 'WEB 1.0', years: 15, pct: '28.3%', color: '#ffe600' },
  { name: 'SOC/MOB/AI', years: 18, pct: '34.0%', color: '#00e676' }
];

const stripRect = Layout.inset(p3, 56, 20, 70, 20);
let curX = stripRect.x;
for (let i = 0; i < eras.length; i++) {
  const e = eras[i];
  const segW = (e.years / 53) * stripRect.width;

  drawBrutalistBlock(curX, stripRect.y, segW, 46, e.color, 3, 2);

  ctx.fillStyle = '#000000';
  ctx.font = '900 14px "Consolas", monospace';
  ctx.fillText(e.pct, curX + 6, stripRect.y + 75);
  ctx.font = '700 11px "Segoe UI", Arial, sans-serif';
  ctx.fillText(e.name, curX + 6, stripRect.y + 94);

  curX += segW;
}

// Panel 4: Adoption Velocity Hero
const p4 = statsPanels[3];
drawBrutalistBlock(p4.x, p4.y, p4.width, p4.height, '#ff2a6d');
ctx.fillStyle = '#000000';
ctx.font = '900 18px "Consolas", monospace';
ctx.fillText('ADOPTION VELOCITY', p4.x + 20, p4.y + 36);

ctx.fillStyle = '#ffffff';
ctx.font = '900 58px "Impact", "Arial Black", sans-serif';
ctx.fillText('100M', p4.x + 20, p4.y + 105);

drawBrutalistBlock(p4.x + 20, p4.y + 125, p4.width - 40, 36, '#ffe600', 3, 2);
ctx.fillStyle = '#000000';
ctx.font = '900 16px "Consolas", monospace';
ctx.fillText('USERS IN 2 MONTHS', p4.x + 35, p4.y + 150);

ctx.fillStyle = '#ffffff';
ctx.font = '700 14px "Segoe UI", Arial, sans-serif';
ctx.fillText('ChatGPT (2022 Milestone)', p4.x + 20, p4.y + 190);

// --- 4. FOOTER ZONE ---
drawBrutalistBlock(footerBand.x, footerBand.y, footerBand.width, footerBand.height, '#ffffff');

drawBrutalistBlock(footerBand.x + 20, footerBand.y + 18, 220, 32, '#00e676', 3, 2);
ctx.fillStyle = '#000000';
ctx.font = '900 15px "Consolas", monospace';
ctx.fillText('CENTRAL TAKEAWAY', footerBand.x + 30, footerBand.y + 40);

ctx.font = '900 27px "Segoe UI", Arial, sans-serif';
ctx.fillStyle = '#000000';
ctx.fillText('Progress is constant \u2014 it never stopped coming,', footerBand.x + 20, footerBand.y + 90);
ctx.fillText('but it never once came on schedule.', footerBand.x + 20, footerBand.y + 130);

ctx.strokeStyle = '#cccccc';
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(footerBand.x + 20, footerBand.y + 152);
ctx.lineTo(footerBand.x2 - 20, footerBand.y + 152);
ctx.stroke();

ctx.font = '700 13px "Consolas", monospace';
ctx.fillStyle = '#666666';
ctx.fillText('SOURCE: BRIEF.MD DATA TABLE \u00B7 TRUTHFUL SCALING \u00B7 PROPORTIONAL GEOMETRY \u00B7 ZERO BASELINES', footerBand.x + 20, footerBand.y + 180);

log('Rendered Stage 5 Palette & Type to artifacts/05_palette_type.webp');
canvas;
