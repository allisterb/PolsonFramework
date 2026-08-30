Stage.begin('Detail');
Stage.note('Stage 6: Layering diegetic scene detail, fanfold green-bar paper striping, printer registration marks, and craft flourishes.');

const width = 1080;
const height = 1920;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

// Helper: Brutalist Block with Solid Offset Shadow
const drawBrutalistBlock = (x, y, w, h, fill, shadowOffset = 6, strokeWidth = 4) => {
  if (shadowOffset > 0) {
    ctx.fillStyle = '#000000';
    ctx.fillRect(x + shadowOffset, y + shadowOffset, w, h);
  }
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  if (strokeWidth > 0) {
    ctx.lineWidth = strokeWidth;
    ctx.strokeStyle = '#000000';
    ctx.strokeRect(x, y, w, h);
  }
};

// Ground: Continuous fanfold printer paper with subtle alternating horizontal green-bar bands
ctx.fillStyle = '#f5f3e9';
ctx.fillRect(0, 0, width, height);

// Alternating faint green-bar scanline bands across background (diegetic line-printer paper)
ctx.fillStyle = '#eef3ea';
for (let y = 0; y < height; y += 72) {
  ctx.fillRect(0, y, width, 36);
}

// Subtle grid dots across paper
ctx.fillStyle = '#dcd6c8';
for (let gx = 45; gx < width; gx += 40) {
  for (let gy = 45; gy < height; gy += 40) {
    ctx.fillRect(gx, gy, 2, 2);
  }
}

// Tractor Feed Sprocket Margins (Left & Right)
const drawTractorFeed = () => {
  const marginW = 34;
  
  // Left strip
  ctx.fillStyle = '#ebe6d8';
  ctx.fillRect(0, 0, marginW, height);
  ctx.fillRect(width - marginW, 0, marginW, height);

  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.moveTo(marginW, 0);
  ctx.lineTo(marginW, height);
  ctx.moveTo(width - marginW, 0);
  ctx.lineTo(width - marginW, height);
  ctx.stroke();

  // Perforation dots along inner margin border
  ctx.strokeStyle = '#888888';
  ctx.lineWidth = 2;
  ctx.setLineDash([3, 6]);
  ctx.beginPath();
  ctx.moveTo(marginW + 4, 0);
  ctx.lineTo(marginW + 4, height);
  ctx.moveTo(width - marginW - 4, 0);
  ctx.lineTo(width - marginW - 4, height);
  ctx.stroke();
  ctx.setLineDash([]);

  // Sprocket feeder holes
  for (let y = 28; y < height; y += 44) {
    // Left sprocket hole
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(8, y, 18, 18);
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 3;
    ctx.strokeRect(8, y, 18, 18);

    // Inner hole core
    ctx.fillStyle = '#111111';
    ctx.fillRect(13, y + 5, 8, 8);

    // Right sprocket hole
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(width - 26, y, 18, 18);
    ctx.strokeRect(width - 26, y, 18, 18);
    ctx.fillStyle = '#111111';
    ctx.fillRect(width - 21, y + 5, 8, 8);
  }
};
drawTractorFeed();

// Layout Zones inside printable area
const page = Layout.inset(Layout.rect(34, 0, width - 68, height), 34, 22, 34, 22);
const [headerBand, mainBand, footerBand] = Layout.rows(page, [240, 1370, 220], 22);
const [timelineCol, statsCol] = Layout.columns(mainBand, [62, 38], 24);
const statsPanels = Layout.rows(statsCol, [250, 520, 280, 280], 20);

// Corner Registration Marks Helper
const drawRegistrationMarks = (x, y, size = 12) => {
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 2;
  ctx.beginPath();
  // Top-left
  ctx.moveTo(x - size, y); ctx.lineTo(x + size, y);
  ctx.moveTo(x, y - size); ctx.lineTo(x, y + size);
  ctx.stroke();
};

// --- 1. HEADER ZONE ---
drawBrutalistBlock(headerBand.x, headerBand.y, headerBand.width, headerBand.height, '#ffffff', 8, 4);

// Registration marks on header
drawRegistrationMarks(headerBand.x + 12, headerBand.y + 12);
drawRegistrationMarks(headerBand.x2 - 12, headerBand.y + 12);

// Title banner
drawBrutalistBlock(headerBand.x + 18, headerBand.y + 18, headerBand.width - 36, 80, '#ffe600', 5, 4);
ctx.fillStyle = '#000000';
ctx.font = '900 52px "Impact", "Arial Black", sans-serif';
ctx.save();
ctx.letterSpacing = LogoType.computeWordmarkTracking(52, true, 'wordmark') + 'em';
ctx.fillText('WEB HISTORY: NEVER ON TIME', headerBand.x + 32, headerBand.y + 75);
ctx.restore();

// Top badge tag on header
drawBrutalistBlock(headerBand.x2 - 210, headerBand.y + 24, 180, 30, '#ff2a6d', 3, 2);
ctx.fillStyle = '#ffffff';
ctx.font = '900 13px "Consolas", monospace';
ctx.textAlign = 'center';
ctx.fillText('EDITION 01 \u2022 PL-53', headerBand.x2 - 120, headerBand.y + 44);
ctx.textAlign = 'left';

// Subtitle & Status Badges
ctx.font = '900 24px "Segoe UI", Arial, sans-serif';
ctx.fillStyle = '#ff2a6d';
ctx.fillText('53 YEARS OF WAITING FOR THE NEXT BIG THING (1969 \u2014 2022)', headerBand.x + 22, headerBand.y + 138);

// Printer status row
const statusTags = [
  { text: 'PLATEN FEED: LINEAR 1:1', bg: '#00e676' },
  { text: 'VERIFIED FIGURES: 7', bg: '#05d9e8' },
  { text: 'ZERO-BASELINE ASSERTED', bg: '#ffe600' }
];
let tagX = headerBand.x + 22;
for (let i = 0; i < statusTags.length; i++) {
  const t = statusTags[i];
  ctx.font = '900 11px "Consolas", monospace';
  const tw = ctx.measureText(t.text).width + 16;
  drawBrutalistBlock(tagX, headerBand.y + 160, tw, 24, t.bg, 2, 2);
  ctx.fillStyle = '#000000';
  ctx.fillText(t.text, tagX + 8, headerBand.y + 176);
  tagX += tw + 12;
}

ctx.font = '600 12px "Consolas", monospace';
ctx.fillStyle = '#555555';
ctx.fillText('TELEMETRY: CONTINUOUS PRINTER SPOOL // 53 YEARS TIMELINE SPAN // TOTAL TIME: 1969-2022', headerBand.x + 22, headerBand.y + 215);

// Perforated line under header
ctx.strokeStyle = '#000000';
ctx.lineWidth = 3;
ctx.setLineDash([10, 8]);
ctx.beginPath();
ctx.moveTo(headerBand.x, headerBand.y2 + 11);
ctx.lineTo(headerBand.x2, headerBand.y2 + 11);
ctx.stroke();
ctx.setLineDash([]);


// --- 2. TIMELINE ZONE (ZONE A - 62% Width) ---
drawBrutalistBlock(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height, '#ffffff', 8, 4);

// Header bar inside timeline
drawBrutalistBlock(timelineCol.x + 18, timelineCol.y + 18, timelineCol.width - 36, 52, '#05d9e8', 4, 3);
ctx.fillStyle = '#000000';
ctx.font = '900 21px "Segoe UI", Arial, sans-serif';
ctx.fillText('CONTINUOUS PLATEN: REAL TIME SCALE (1969\u20132022)', timelineCol.x + 32, timelineCol.y + 51);

// Timeline linear scale
const timelineTop = timelineCol.y + 125;
const timelineBottom = timelineCol.y2 - 65;
const yearScale = Scale.linear(1969, 2022, timelineTop, timelineBottom);

// Decade grid rules across timeline card
const decades = [1970, 1980, 1990, 2000, 2010, 2020];
ctx.strokeStyle = '#e2dfd5';
ctx.lineWidth = 2;
for (let i = 0; i < decades.length; i++) {
  const dy = yearScale.map(decades[i]);
  ctx.beginPath();
  ctx.moveTo(timelineCol.x + 20, dy);
  ctx.lineTo(timelineCol.x2 - 20, dy);
  ctx.stroke();

  // Decade watermark label
  ctx.fillStyle = '#a8a294';
  ctx.font = '900 14px "Consolas", monospace';
  ctx.fillText('\u2500\u2500 ' + decades[i].toString() + ' \u2500\u2500', timelineCol.x + 24, dy - 6);
}

// Timeline heavy central spine
const axisX = timelineCol.x + 105;
ctx.fillStyle = '#000000';
ctx.fillRect(axisX - 5, timelineTop, 10, timelineBottom - timelineTop);

// Milestones Data with full details
const milestones = [
  { year: 1969, title: 'ARPANET First Message', desc: 'Sends first packet; crashed after "LO"', fill: '#ff2a6d', tag: 'BIRTH', math: 'START' },
  { year: 1989, title: 'World Wide Web Proposal', desc: 'Tim Berners-Lee invents HTTP / HTML', fill: '#ffe600', tag: 'FOUNDATION', crossBoundary: true, math: '+20 YRS' },
  { year: 1993, title: 'NCSA Mosaic Browser', desc: 'Makes the Web visual & navigable', fill: '#00e676', tag: 'BROWSER', math: '+4 YRS' },
  { year: 1998, title: 'Google Founded', desc: 'PageRank indexes the expanding web', fill: '#05d9e8', tag: 'SEARCH', math: '+5 YRS' },
  { year: 2004, title: 'Facebook Launches', desc: 'The social network era begins', fill: '#d600ff', tag: 'SOCIAL', math: '+6 YRS' },
  { year: 2007, title: 'Apple iPhone Released', desc: 'Puts the web in every pocket', fill: '#ff5e00', tag: 'MOBILE', math: '+3 YRS' },
  { year: 2022, title: 'ChatGPT AI Explosion', desc: '100M users adopted in 2 months', fill: '#ff2a6d', tag: 'INTELLIGENCE', math: '+15 YRS' }
];

for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const my = yearScale.map(m.year);

  // Axis anchor block
  drawBrutalistBlock(axisX - 14, my - 14, 28, 28, '#000000', 0, 0);
  ctx.fillStyle = m.fill;
  ctx.fillRect(axisX - 8, my - 8, 16, 16);

  // Connecting arm
  ctx.fillStyle = '#000000';
  ctx.fillRect(axisX + 14, my - 4, 18, 8);

  // Badge box
  const badgeX = axisX + 32;
  // Boundary breaker: 1989 WWW Proposal extends across the gutter!
  const badgeW = m.crossBoundary ? timelineCol.width - 110 : timelineCol.width - 165;
  const badgeH = 70;
  const badgeY = my - badgeH / 2;

  drawBrutalistBlock(badgeX, badgeY, badgeW, badgeH, '#ffffff', m.crossBoundary ? 7 : 4, 3);

  // Year Tag inside badge
  drawBrutalistBlock(badgeX + 8, badgeY + 8, 72, badgeH - 16, m.fill, 0, 2);
  ctx.fillStyle = '#000000';
  ctx.font = '900 19px "Consolas", monospace';
  ctx.textAlign = 'center';
  ctx.fillText(m.year.toString(), badgeX + 44, badgeY + badgeH / 2 + 6);
  ctx.textAlign = 'left';

  // Text details inside badge
  ctx.fillStyle = '#000000';
  ctx.font = '900 18px "Segoe UI", Arial, sans-serif';
  ctx.fillText(m.title, badgeX + 92, badgeY + 28);
  ctx.font = '600 13px "Segoe UI", Arial, sans-serif';
  ctx.fillStyle = '#333333';
  ctx.fillText(m.desc, badgeX + 92, badgeY + 52);

  // If boundary breaker, draw attention tag
  if (m.crossBoundary) {
    drawBrutalistBlock(badgeX + badgeW - 135, badgeY + 8, 125, 22, '#ff2a6d', 2, 2);
    ctx.fillStyle = '#ffffff';
    ctx.font = '900 10px "Consolas", monospace';
    ctx.textAlign = 'center';
    ctx.fillText('BREAKOUT EVENT', badgeX + badgeW - 72, badgeY + 23);
    ctx.textAlign = 'left';
  }
}

// Visual Gap Brackets on Timeline (Tension: 20-year desert vs 15-year quiet period)
const drawGapIndicator = (y1, y2, label, math) => {
  const topY = yearScale.map(y1);
  const botY = yearScale.map(y2);
  const midY = (topY + botY) / 2;
  const bx = timelineCol.x2 - 32;

  ctx.strokeStyle = '#ff2a6d';
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.moveTo(bx - 14, topY + 16);
  ctx.lineTo(bx, topY + 16);
  ctx.lineTo(bx, botY - 16);
  ctx.lineTo(bx - 14, botY - 16);
  ctx.stroke();

  // Gap callout badge
  drawBrutalistBlock(bx - 160, midY - 22, 150, 44, '#ff2a6d', 4, 2);
  ctx.fillStyle = '#ffffff';
  ctx.font = '900 15px "Consolas", monospace';
  ctx.textAlign = 'center';
  ctx.fillText(label, bx - 85, midY);
  ctx.font = '700 11px "Consolas", monospace';
  ctx.fillText(math, bx - 85, midY + 14);
  ctx.textAlign = 'left';
};
drawGapIndicator(1969, 1989, '20 YR WAITING GAP', '1989 \u2212 1969 = 20Y');
drawGapIndicator(2007, 2022, '15 YR WAITING GAP', '2022 \u2212 2007 = 15Y');


// --- 3. STAT PANELS (ZONE B - 38% Width) ---

// PANEL 1: HERO METRIC (53 YEARS)
const p1 = statsPanels[0];
drawBrutalistBlock(p1.x, p1.y, p1.width, p1.height, '#ffe600', 8, 4);

drawBrutalistBlock(p1.x + 16, p1.y + 16, p1.width - 32, 28, '#000000', 0, 0);
ctx.fillStyle = '#ffffff';
ctx.font = '900 13px "Consolas", monospace';
ctx.fillText('SPAN: 1969 \u2192 2022', p1.x + 26, p1.y + 35);

ctx.fillStyle = '#000000';
ctx.font = '900 78px "Impact", "Arial Black", sans-serif';
ctx.fillText('53', p1.x + 18, p1.y + 118);
ctx.font = '900 36px "Impact", sans-serif';
ctx.fillText('YEARS', p1.x + 128, p1.y + 112);

ctx.font = '900 15px "Segoe UI", Arial, sans-serif';
ctx.fillText('ARITHMETIC: 2022 \u2212 1969 = 53', p1.x + 18, p1.y + 158);
ctx.font = '600 13px "Consolas", monospace';
ctx.fillStyle = '#222222';
ctx.fillText('7 MILESTONES ACROSS 5 DECADES', p1.x + 18, p1.y + 186);
ctx.fillText('CONSTANT BUT UNSCHEDULED', p1.x + 18, p1.y + 208);


// PANEL 2: GAP COMPARISON RANKED BARS
const p2 = statsPanels[1];
drawBrutalistBlock(p2.x, p2.y, p2.width, p2.height, '#ffffff', 8, 4);

drawBrutalistBlock(p2.x + 16, p2.y + 16, p2.width - 32, 34, '#ff2a6d', 3, 2);
ctx.fillStyle = '#ffffff';
ctx.font = '900 15px "Consolas", monospace';
ctx.fillText('WAITING GAPS RANKED', p2.x + 28, p2.y + 39);

ctx.fillStyle = '#444444';
ctx.font = '600 12px "Consolas", monospace';
ctx.fillText('Intervals between revolutions (0-20y)', p2.x + 18, p2.y + 72);

const gapData = [
  { label: 'ARPANET\u2192Web', gap: 20, color: '#ff2a6d', math: '89\u221269' },
  { label: 'iPhone\u2192ChatGPT', gap: 15, color: '#ff5e00', math: '22\u221207' },
  { label: 'Google\u2192FB', gap: 6, color: '#d600ff', math: '04\u221298' },
  { label: 'Mosaic\u2192Google', gap: 5, color: '#05d9e8', math: '98\u221293' },
  { label: 'Web\u2192Mosaic', gap: 4, color: '#00e676', math: '93\u221289' },
  { label: 'FB\u2192iPhone', gap: 3, color: '#ffe600', math: '07\u221204' }
];

const barPlot = Layout.inset(p2, 85, 18, 20, 18);
const gapX = Scale.linear(0, 20, barPlot.x + 115, barPlot.x2 - 50);
if (!gapX.isZeroBased) throw new Error('bars need zero baseline');

// Zero-baseline axis line
ctx.strokeStyle = '#000000';
ctx.lineWidth = 3;
ctx.beginPath();
ctx.moveTo(barPlot.x + 115, barPlot.y);
ctx.lineTo(barPlot.x + 115, barPlot.y2);
ctx.stroke();

const barBand = Scale.band(gapData.length, barPlot.y + 10, barPlot.y2 - 10, 0.32);

for (let i = 0; i < gapData.length; i++) {
  const d = gapData[i];
  const by = barBand.map(i);
  const bw = gapX.extent(0, d.gap);

  // Label
  ctx.fillStyle = '#000000';
  ctx.font = '900 12px "Segoe UI", Arial, sans-serif';
  ctx.textAlign = 'right';
  ctx.fillText(d.label, barPlot.x + 108, by + barBand.bandwidth / 2 + 4);
  ctx.textAlign = 'left';

  // Brutalist Bar
  drawBrutalistBlock(barPlot.x + 115, by, bw, barBand.bandwidth, d.color, 3, 2);

  // Value text
  ctx.fillStyle = '#000000';
  ctx.font = '900 13px "Consolas", monospace';
  ctx.fillText(d.gap + 'y', barPlot.x + 125 + bw, by + barBand.bandwidth / 2 + 4);
}


// PANEL 3: ERA SHARE STRIP
const p3 = statsPanels[2];
drawBrutalistBlock(p3.x, p3.y, p3.width, p3.height, '#05d9e8', 8, 4);

drawBrutalistBlock(p3.x + 16, p3.y + 16, p3.width - 32, 30, '#000000', 0, 0);
ctx.fillStyle = '#ffffff';
ctx.font = '900 14px "Consolas", monospace';
ctx.fillText('TIMELINE SHARE (53 YEARS)', p3.x + 26, p3.y + 36);

const eras = [
  { name: 'ARPANET', years: 20, pct: '37.7%', color: '#ff2a6d', math: '20/53' },
  { name: 'WEB 1.0', years: 15, pct: '28.3%', color: '#ffe600', math: '15/53' },
  { name: 'MODERN', years: 18, pct: '34.0%', color: '#00e676', math: '18/53' }
];

const stripRect = Layout.inset(p3, 58, 16, 75, 16);
let curX = stripRect.x;
for (let i = 0; i < eras.length; i++) {
  const e = eras[i];
  const segW = (e.years / 53) * stripRect.width;

  drawBrutalistBlock(curX, stripRect.y, segW, 44, e.color, 3, 2);

  ctx.fillStyle = '#000000';
  ctx.font = '900 13px "Consolas", monospace';
  ctx.fillText(e.pct, curX + 6, stripRect.y + 70);
  ctx.font = '700 11px "Segoe UI", Arial, sans-serif';
  ctx.fillText(e.name, curX + 6, stripRect.y + 88);

  curX += segW;
}


// PANEL 4: ADOPTION VELOCITY HERO
const p4 = statsPanels[3];
drawBrutalistBlock(p4.x, p4.y, p4.width, p4.height, '#ff2a6d', 8, 4);

drawBrutalistBlock(p4.x + 16, p4.y + 16, p4.width - 32, 28, '#000000', 0, 0);
ctx.fillStyle = '#ffffff';
ctx.font = '900 13px "Consolas", monospace';
ctx.fillText('ADOPTION ACCELERATION', p4.x + 26, p4.y + 35);

ctx.fillStyle = '#ffffff';
ctx.font = '900 62px "Impact", "Arial Black", sans-serif';
ctx.fillText('100M', p4.x + 18, p4.y + 108);

drawBrutalistBlock(p4.x + 16, p4.y + 122, p4.width - 32, 38, '#ffe600', 3, 2);
ctx.fillStyle = '#000000';
ctx.font = '900 16px "Consolas", monospace';
ctx.fillText('USERS IN 2 MONTHS', p4.x + 28, p4.y + 147);

ctx.fillStyle = '#ffffff';
ctx.font = '900 13px "Segoe UI", Arial, sans-serif';
ctx.fillText('ChatGPT (2022 Milestone)', p4.x + 18, p4.y + 185);
ctx.font = '600 11px "Consolas", monospace';
ctx.fillText('FASTEST ADOPTION IN COMPUTING HISTORY', p4.x + 18, p4.y + 205);


// --- 4. FOOTER ZONE ---
drawBrutalistBlock(footerBand.x, footerBand.y, footerBand.width, footerBand.height, '#ffffff', 8, 4);

drawRegistrationMarks(footerBand.x + 12, footerBand.y + 12);
drawRegistrationMarks(footerBand.x2 - 12, footerBand.y + 12);

drawBrutalistBlock(footerBand.x + 18, footerBand.y + 18, 240, 32, '#00e676', 3, 2);
ctx.fillStyle = '#000000';
ctx.font = '900 15px "Consolas", monospace';
ctx.fillText('CORE TAKEAWAY', footerBand.x + 30, footerBand.y + 40);

ctx.font = '900 28px "Segoe UI", Arial, sans-serif';
ctx.fillStyle = '#000000';
ctx.fillText('Progress is constant \u2014 it never stopped coming,', footerBand.x + 20, footerBand.y + 88);
ctx.fillText('but it never once came on schedule.', footerBand.x + 20, footerBand.y + 128);

ctx.strokeStyle = '#000000';
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(footerBand.x + 20, footerBand.y + 150);
ctx.lineTo(footerBand.x2 - 20, footerBand.y + 150);
ctx.stroke();

ctx.font = '700 13px "Consolas", monospace';
ctx.fillStyle = '#555555';
ctx.fillText('DATA TABLE: BRIEF.MD \u00B7 TRUTHFUL GEOMETRY \u00B7 PROPORTIONAL INK \u00B7 ZERO BASELINES ASSERTED', footerBand.x + 20, footerBand.y + 178);

log('Rendered Stage 6 Final Artwork to artifacts/06_final.webp and artifacts/06_final.svg');
canvas;
