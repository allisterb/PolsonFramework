Stage.begin('Encode');
Stage.note('Building full infographic. True-scale vertical timeline (left), gap bar chart + era bar + callout (right). All geometry from Scale/Layout. Zero-baseline asserted.');

// === CONSTANTS ===
const W = 1080, H = 1920;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// Palette
const INK    = '#0a0a0a';
const PAPER  = '#f0ead8';
const YELLOW = '#f7c31a';
const PINK   = '#e8175d';
const CYAN   = '#00b4c8';
const GREEN  = '#16a34a';
const ORANGE = '#ea580c';
const WHITE  = '#ffffff';

// Milestone colours (each gets a distinct flat)
const MCOLORS = [PINK, YELLOW, GREEN, CYAN, '#a855f7', ORANGE, PINK];

// Data
const MILESTONES = [
  { year: 1969, label: 'ARPANET', desc: 'First msg\u2014crashed after "LO"' },
  { year: 1989, label: 'WORLD WIDE WEB', desc: 'Berners-Lee proposes HTTP/HTML' },
  { year: 1993, label: 'MOSAIC', desc: 'Makes the web visual' },
  { year: 1998, label: 'GOOGLE', desc: 'PageRank indexes the web' },
  { year: 2004, label: 'FACEBOOK', desc: 'Social era begins' },
  { year: 2007, label: 'iPHONE', desc: 'Web in every pocket' },
  { year: 2022, label: 'CHATGPT', desc: '100M users in 2 months' },
];
const GAPS = [
  { label: 'ARPANET\u2192WEB',   from:'ARPANET', to:'WEB',      years: 1989-1969 },
  { label: 'iPHONE\u2192AI',    from:'iPHONE',  to:'CHATGPT',  years: 2022-2007 },
  { label: 'GOOGLE\u2192FB',    from:'GOOGLE',  to:'FACEBOOK', years: 2004-1998 },
  { label: 'MOSAIC\u2192GOOGLE',from:'MOSAIC',  to:'GOOGLE',   years: 1998-1993 },
  { label: 'WEB\u2192MOSAIC',   from:'WEB',     to:'MOSAIC',   years: 1993-1989 },
  { label: 'FB\u2192iPHONE',    from:'FB',      to:'iPHONE',   years: 2007-2004 },
];
const SPAN = 2022 - 1969; // 53
const WAIT = (1989-1969) + (2022-2007); // 35

// Assert zero-baseline for bars (checked before any bar is drawn)
const barScale = Scale.linear(0, 20, 0, 100);
if (!barScale.isZeroBased) throw new Error('bars need a zero baseline');

// === GROUND: FANFOLD PAPER ===
ctx.fillStyle = PAPER;
ctx.fillRect(0, 0, W, H);

// Subtle horizontal lines (fanfold feed lines, every 60px)
ctx.strokeStyle = 'rgba(180,165,140,0.35)';
ctx.lineWidth = 1;
for (let y = 0; y < H; y += 60) {
  ctx.beginPath();
  ctx.moveTo(0, y);
  ctx.lineTo(W, y);
  ctx.stroke();
}

// Tractor hole strips
const STRIP = 40;
ctx.fillStyle = '#ddd5bc';
ctx.fillRect(0, 0, STRIP, H);
ctx.fillRect(W - STRIP, 0, STRIP, H);
ctx.strokeStyle = INK;
ctx.lineWidth = 2;
ctx.beginPath(); ctx.moveTo(STRIP, 0); ctx.lineTo(STRIP, H); ctx.stroke();
ctx.beginPath(); ctx.moveTo(W-STRIP, 0); ctx.lineTo(W-STRIP, H); ctx.stroke();

// Holes
const HOLE_SP = 60, HOLE_R = 9;
for (let hy = 30; hy < H; hy += HOLE_SP) {
  for (const hx of [STRIP/2, W-STRIP/2]) {
    ctx.fillStyle = PAPER;
    ctx.strokeStyle = '#b0a08a';
    ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.arc(hx, hy, HOLE_R, 0, Math.PI*2); ctx.fill(); ctx.stroke();
  }
}

// === LAYOUT ===
const page = Layout.inset(Layout.rect(STRIP+1, 0, W-(STRIP+1)*2, H), 0, 12);

// Header: ink banner, 110px
const HDR_H = 110;
const [hdrRect, mainRect] = Layout.rows(page, [HDR_H, page.height - HDR_H], 0);
const [leftRect, rightRect] = Layout.columns(mainRect, [41, 59], 14);

// === HEADER ===
// Hard shadow
const SHADOW = 5;
ctx.fillStyle = YELLOW;
ctx.fillRect(hdrRect.x+SHADOW, hdrRect.y+SHADOW, hdrRect.width, HDR_H);
ctx.fillStyle = INK;
ctx.fillRect(hdrRect.x, hdrRect.y, hdrRect.width, HDR_H);

ctx.fillStyle = YELLOW;
ctx.font = 'black 44px Impact, Arial';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.letterSpacing = LogoType.computeWordmarkTracking(44, true) + 'em';
ctx.fillText('THE INTERNET WAS NEVER ON SCHEDULE', hdrRect.x+18, hdrRect.y + HDR_H/2 - 8);

ctx.font = '400 14px "Courier New", monospace';
ctx.fillStyle = 'rgba(247,195,26,0.7)';
ctx.letterSpacing = '0.12em';
ctx.fillText('53-YEAR SPAN  //  7 MILESTONES  //  1969–2022', hdrRect.x+18, hdrRect.y + HDR_H - 20);
ctx.letterSpacing = '0px';

// === LEFT: TRUE-SCALE VERTICAL TIMELINE ===
// The timeline spans 53 years. We map year → y pixel.
const TL_YEAR_MIN = 1969, TL_YEAR_MAX = 2022;
const tlPad = Layout.inset(leftRect, 16, 8, 24, 8);
// Scale: year 1969 = top, 2022 = bottom
const yScale = Scale.linear(TL_YEAR_MIN, TL_YEAR_MAX, tlPad.y, tlPad.y2);
// (timeline encodes position, not length — axis crop is OK per Manual 13 §2)

const TL_X = tlPad.x + 36; // x of the central stem

// Year tick marks every 5 years, Courier New labels
const TICK_YEARS = [1970,1975,1980,1985,1990,1995,2000,2005,2010,2015,2020];
ctx.strokeStyle = 'rgba(10,10,10,0.25)';
ctx.lineWidth = 1;
ctx.font = '400 10px "Courier New", monospace';
ctx.fillStyle = 'rgba(10,10,10,0.45)';
ctx.textAlign = 'right';
ctx.textBaseline = 'middle';
for (const yr of TICK_YEARS) {
  const ty = yScale.map(yr);
  ctx.beginPath();
  ctx.moveTo(TL_X - 6, ty);
  ctx.lineTo(tlPad.x2, ty);
  ctx.stroke();
  ctx.fillText(yr, TL_X - 10, ty);
}

// Main timeline stem
ctx.strokeStyle = INK;
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(TL_X, yScale.map(TL_YEAR_MIN));
ctx.lineTo(TL_X, yScale.map(TL_YEAR_MAX));
ctx.stroke();

// Gap annotations: the two big lulls (ARPANET->Web, iPhone->ChatGPT)
// These are the structural argument of the piece — they get bracketed zones
const bigGaps = [
  { fromY: 1969, toY: 1989, label: '20 YR\nWAIT', color: PINK },
  { fromY: 2007, toY: 2022, label: '15 YR\nWAIT', color: ORANGE },
];
for (const g of bigGaps) {
  const y1 = yScale.map(g.fromY);
  const y2 = yScale.map(g.toY);
  const bx = TL_X + 8;
  const bw = 62;
  // Shadow
  ctx.fillStyle = INK;
  ctx.fillRect(bx+3, y1+3, bw, y2-y1);
  // Block
  ctx.fillStyle = g.color;
  ctx.fillRect(bx, y1, bw, y2-y1);
  // Border
  ctx.strokeStyle = INK;
  ctx.lineWidth = 3;
  ctx.strokeRect(bx, y1, bw, y2-y1);
  // Label
  const lines = g.label.split('\n');
  ctx.fillStyle = WHITE;
  ctx.font = 'bold 14px Impact, Arial';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(lines[0], bx + bw/2, (y1+y2)/2 - 8);
  ctx.fillText(lines[1], bx + bw/2, (y1+y2)/2 + 8);
}

// Milestone markers + cards
const CARD_X = TL_X - 4;
const CARD_W = tlPad.x2 - CARD_X;

for (let i = 0; i < MILESTONES.length; i++) {
  const m = MILESTONES[i];
  const my = yScale.map(m.year);
  const col = MCOLORS[i];

  // Dot on stem
  ctx.fillStyle = INK;
  ctx.beginPath(); ctx.arc(TL_X, my, 11, 0, Math.PI*2); ctx.fill();
  ctx.fillStyle = col;
  ctx.beginPath(); ctx.arc(TL_X, my, 8, 0, Math.PI*2); ctx.fill();

  // Year badge (left of stem)
  const badgeW = 42, badgeH = 22;
  ctx.fillStyle = INK;
  ctx.fillRect(tlPad.x - 2, my - badgeH/2 + 2, badgeW, badgeH);
  ctx.fillStyle = col;
  ctx.fillRect(tlPad.x - 4, my - badgeH/2, badgeW, badgeH);
  ctx.fillStyle = INK;
  ctx.font = 'bold 11px "Courier New", monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(m.year, tlPad.x - 4 + badgeW/2, my);

  // Event card (right of stem) — only for events not in a big gap block
  const cardY = my - 28;
  const cardH = 52;
  // Shadow
  ctx.fillStyle = INK;
  ctx.fillRect(CARD_X + 3 + 70, cardY + 3, CARD_W - 70, cardH);
  // Card fill
  ctx.fillStyle = col;
  ctx.fillRect(CARD_X + 70, cardY, CARD_W - 70, cardH);
  ctx.strokeStyle = INK;
  ctx.lineWidth = 3;
  ctx.strokeRect(CARD_X + 70, cardY, CARD_W - 70, cardH);
  // Label
  ctx.fillStyle = INK;
  ctx.font = 'bold 13px Impact, Arial';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';
  ctx.fillText(m.label, CARD_X + 78, cardY + 6);
  ctx.font = '400 10px "Courier New", monospace';
  ctx.fillText(m.desc, CARD_X + 78, cardY + 24);
}

// === RIGHT PANELS ===
const rPad = Layout.inset(rightRect, 8, 0, 8, 0);

// Divide right into 4 zones: [bignum:22%, gapbar:36%, erabar:16%, footer:26%]
const [rCallout, rGaps, rEra, rFooter] = Layout.rows(rPad, [22, 36, 16, 26], 10);

// Helper: draw a panel box with ink border + hard shadow + title bar
const drawPanel = (rect, titleText, titleBg, titleFg = INK) => {
  // Shadow
  ctx.fillStyle = INK;
  ctx.fillRect(rect.x+5, rect.y+5, rect.width, rect.height);
  // Body
  ctx.fillStyle = WHITE;
  ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
  ctx.strokeStyle = INK;
  ctx.lineWidth = 3;
  ctx.strokeRect(rect.x, rect.y, rect.width, rect.height);
  // Title bar
  ctx.fillStyle = titleBg;
  const titleH = 28;
  ctx.fillRect(rect.x, rect.y, rect.width, titleH);
  ctx.fillStyle = titleFg;
  ctx.font = 'bold 12px "Courier New", monospace';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.letterSpacing = '0.1em';
  ctx.fillText(titleText, rect.x + 10, rect.y + titleH/2);
  ctx.letterSpacing = '0px';
  return titleH;
};

// --- PANEL 1: BIG-NUMBER CALLOUT ---
// '20 YEARS -> 2 MONTHS'
// The punchline: the longest wait vs. the fastest adoption
const titleH1 = drawPanel(rCallout, '// FASTEST ADOPTION IN HISTORY', INK, YELLOW);
const inner1 = Layout.inset(rCallout, titleH1 + 8, 12, 12, 12);

ctx.fillStyle = PINK;
ctx.font = 'black 72px Impact, Arial';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.letterSpacing = '-0.02em';
ctx.fillText('20 YRS', inner1.x, inner1.y);
ctx.letterSpacing = '0px';

ctx.fillStyle = INK;
ctx.font = 'bold 14px "Courier New", monospace';
ctx.fillText('ARPANET → WORLD WIDE WEB', inner1.x, inner1.y + 76);

// Arrow + contrast
ctx.fillStyle = INK;
ctx.font = 'black 28px Impact, Arial';
ctx.fillText('vs.', inner1.x, inner1.y + 100);

ctx.fillStyle = GREEN;
ctx.font = 'black 72px Impact, Arial';
ctx.letterSpacing = '-0.02em';
ctx.fillText('2 MOS', inner1.x, inner1.y + 128);
ctx.letterSpacing = '0px';

ctx.fillStyle = INK;
ctx.font = 'bold 14px "Courier New", monospace';
ctx.fillText('iPHONE → CHATGPT REACHES 100M', inner1.x, inner1.y + 204);

// --- PANEL 2: HORIZONTAL BAR CHART (ranked gaps) ---
const titleH2 = drawPanel(rGaps, '// THE WAITING GAPS (YEARS BETWEEN REVOLUTIONS)', INK, YELLOW);
const inner2 = Layout.inset(rGaps, titleH2 + 10, 14, 14, 14);

// Gaps already ranked longest-first
const gapValues = GAPS.map(g => g.years);
const gapMax = Scale.extent(gapValues).max; // 20
const gapBounds = Scale.nice(0, gapMax);
const barY = Scale.band(GAPS.length, inner2.y, inner2.y2, 0.25);
const barX = Scale.linear(0, gapBounds.max, inner2.x + 110, inner2.x2 - 10);
if (!barX.isZeroBased) throw new Error('gap bars need zero baseline');

const GAP_COLORS = [PINK, ORANGE, '#a855f7', CYAN, GREEN, YELLOW];

for (let i = 0; i < GAPS.length; i++) {
  const g = GAPS[i];
  const by = barY.map(i);
  const bh = barY.bandwidth;
  const bx0 = barX.map(0);
  const bx1 = barX.map(g.years);
  const bw = barX.extent(0, g.years);
  const col = GAP_COLORS[i];

  // Shadow
  ctx.fillStyle = 'rgba(10,10,10,0.3)';
  ctx.fillRect(bx0+3, by+3, bw, bh);
  // Bar
  ctx.fillStyle = col;
  ctx.fillRect(bx0, by, bw, bh);
  // Border
  ctx.strokeStyle = INK;
  ctx.lineWidth = 2.5;
  ctx.strokeRect(bx0, by, bw, bh);

  // Label (left of bar)
  ctx.fillStyle = INK;
  ctx.font = 'bold 11px "Courier New", monospace';
  ctx.textAlign = 'right';
  ctx.textBaseline = 'middle';
  ctx.fillText(g.label, bx0 - 6, by + bh/2);

  // Value (inside or right of bar)
  ctx.fillStyle = bw > 40 ? INK : INK;
  ctx.font = 'bold 12px Impact, Arial';
  ctx.textAlign = 'left';
  ctx.fillText(g.years + 'y', bx1 + 5, by + bh/2);
}

// X-axis line
ctx.strokeStyle = INK;
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(barX.map(0), inner2.y2);
ctx.lineTo(barX.map(gapBounds.max), inner2.y2);
ctx.stroke();
// Axis ticks
ctx.font = '400 10px "Courier New", monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'top';
ctx.fillStyle = INK;
for (const t of barX.ticks(4)) {
  const tx = barX.map(t);
  ctx.beginPath(); ctx.moveTo(tx, inner2.y2); ctx.lineTo(tx, inner2.y2+5); ctx.stroke();
  ctx.fillText(t, tx, inner2.y2 + 7);
}

// --- PANEL 3: ERA SEGMENTED BAR ---
// 3 eras: Lull (20y/37.7%), Burst (18y/34.0%), Lull (15y/28.3%)
const ERA_DATA = [
  { label: 'ARPANET LULL', years: 20, pct: (20/53*100).toFixed(1), color: PINK },
  { label: '5-REVOLUTION BURST', years: 18, pct: (18/53*100).toFixed(1), color: GREEN },
  { label: 'iPHONE LULL', years: 15, pct: (15/53*100).toFixed(1), color: ORANGE },
];
// Verify sum = 53 (20+18+15=53)
const eraSum = ERA_DATA.reduce((s,e) => s + e.years, 0);
if (eraSum !== 53) throw new Error('ERA years must sum to 53, got ' + eraSum);

const titleH3 = drawPanel(rEra, '// 53 YEARS: HOW THE TIME WAS SPENT', INK, YELLOW);
const inner3 = Layout.inset(rEra, titleH3 + 10, 12, 40, 12);

// Single horizontal segmented bar
const eraBarH = 48;
const eraBarY = inner3.y + (inner3.height - eraBarH) / 2 - 10;
const eraScale = Scale.linear(0, 53, inner3.x, inner3.x2);

let eraX = inner3.x;
for (let i = 0; i < ERA_DATA.length; i++) {
  const e = ERA_DATA[i];
  const ew = eraScale.extent(0, e.years);
  // Shadow
  ctx.fillStyle = INK;
  ctx.fillRect(eraX+3, eraBarY+3, ew, eraBarH);
  // Fill
  ctx.fillStyle = e.color;
  ctx.fillRect(eraX, eraBarY, ew, eraBarH);
  // Border
  ctx.strokeStyle = INK;
  ctx.lineWidth = 3;
  ctx.strokeRect(eraX, eraBarY, ew, eraBarH);
  // Label below
  ctx.fillStyle = INK;
  ctx.font = 'bold 10px "Courier New", monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'top';
  ctx.fillText(e.pct + '%', eraX + ew/2, eraBarY + eraBarH + 5);
  ctx.font = '400 9px "Courier New", monospace';
  ctx.fillText(e.years + 'y', eraX + ew/2, eraBarY + eraBarH + 17);
  eraX += ew;
}

// Legend below
const legY = eraBarY + eraBarH + 32;
let legX = inner3.x;
for (const e of ERA_DATA) {
  ctx.fillStyle = e.color;
  ctx.fillRect(legX, legY, 14, 10);
  ctx.strokeStyle = INK; ctx.lineWidth = 1;
  ctx.strokeRect(legX, legY, 14, 10);
  ctx.fillStyle = INK;
  ctx.font = '400 9px "Courier New", monospace';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';
  ctx.fillText(e.label, legX + 18, legY);
  legX += 18 + ctx.measureText(e.label).width + 12;
}

// --- PANEL 4: CLAIM FOOTER ---
const titleH4 = drawPanel(rFooter, '// CORE CLAIM', YELLOW, INK);
const inner4 = Layout.inset(rFooter, titleH4 + 12, 14, 14, 14);

ctx.fillStyle = INK;
ctx.font = 'black 22px Impact, Arial';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.letterSpacing = '0.01em';
const claimLines = [
  'PROGRESS IS CONSTANT —',
  'IT NEVER STOPPED COMING,',
  'BUT IT NEVER ONCE CAME',
  'ON SCHEDULE.',
];
let claimY = inner4.y;
for (const line of claimLines) {
  ctx.fillText(line, inner4.x, claimY);
  claimY += 28;
}
ctx.letterSpacing = '0px';

// Stats block below claim
const statsY = claimY + 14;
const statsData = [
  { v: '66%', label: 'OF 53 YRS SPENT WAITING' },
  { v: '35y', label: 'IN JUST 2 QUIET PERIODS' },
];
for (let i = 0; i < statsData.length; i++) {
  const sx = inner4.x + i * (inner4.width / 2);
  ctx.fillStyle = PINK;
  ctx.font = 'black 32px Impact, Arial';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';
  ctx.fillText(statsData[i].v, sx, statsY);
  ctx.fillStyle = INK;
  ctx.font = 'bold 10px "Courier New", monospace';
  ctx.fillText(statsData[i].label, sx, statsY + 36);
}

log('Encode complete. Zero-baseline asserted. Era sum=' + eraSum + '.');
canvas;
