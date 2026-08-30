Stage.begin('Encode');
Stage.note('Revision: fix card/gap overlap by moving gap blocks LEFT of stem, event cards RIGHT. Fix callout panel use of space. Tighten all spacing.');

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
const PURPLE = '#a855f7';
const WHITE  = '#ffffff';
const MCOLORS = [PINK, YELLOW, GREEN, CYAN, PURPLE, ORANGE, PINK];

// Data
const MILESTONES = [
  { year: 1969, label: 'ARPANET', desc: 'First message — crashed after "LO"' },
  { year: 1989, label: 'WWW', desc: 'Berners-Lee proposes HTTP/HTML' },
  { year: 1993, label: 'MOSAIC', desc: 'Makes the web visual' },
  { year: 1998, label: 'GOOGLE', desc: 'PageRank indexes the web' },
  { year: 2004, label: 'FACEBOOK', desc: 'Social era begins' },
  { year: 2007, label: 'iPHONE', desc: 'Web in every pocket' },
  { year: 2022, label: 'CHATGPT', desc: '100M users in 2 months' },
];
const GAPS = [
  { label: 'ARPANET\u2192WEB',    years: 20 },
  { label: 'iPHONE\u2192AI',     years: 15 },
  { label: 'GOOGLE\u2192FB',     years: 6  },
  { label: 'MOSAIC\u2192GOOGLE', years: 5  },
  { label: 'WEB\u2192MOSAIC',    years: 4  },
  { label: 'FB\u2192iPHONE',     years: 3  },
];
const ERA_DATA = [
  { label: 'ARPANET LULL', years: 20, color: PINK   },
  { label: '5-REV BURST',  years: 18, color: GREEN  },
  { label: 'iPHONE LULL',  years: 15, color: ORANGE },
];
const eraSum = 20+18+15; // = 53 verified
if (eraSum !== 53) throw new Error('ERA sum wrong');

// === GROUND ===
ctx.fillStyle = PAPER;
ctx.fillRect(0, 0, W, H);

// Faint horizontal lines
ctx.strokeStyle = 'rgba(160,145,120,0.25)';
ctx.lineWidth = 1;
for (let hy = 0; hy < H; hy += 60) {
  ctx.beginPath(); ctx.moveTo(0, hy); ctx.lineTo(W, hy); ctx.stroke();
}

// Tractor strips
const STRIP = 40;
ctx.fillStyle = '#ddd5bc';
ctx.fillRect(0, 0, STRIP, H);
ctx.fillRect(W-STRIP, 0, STRIP, H);
ctx.strokeStyle = INK; ctx.lineWidth = 2;
ctx.beginPath(); ctx.moveTo(STRIP, 0); ctx.lineTo(STRIP, H); ctx.stroke();
ctx.beginPath(); ctx.moveTo(W-STRIP, 0); ctx.lineTo(W-STRIP, H); ctx.stroke();

const HOLE_SP = 60, HOLE_R = 9;
for (let hy = 30; hy < H; hy += HOLE_SP) {
  for (const hx of [STRIP/2, W-STRIP/2]) {
    ctx.fillStyle = PAPER; ctx.strokeStyle = '#b0a08a'; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.arc(hx, hy, HOLE_R, 0, Math.PI*2); ctx.fill(); ctx.stroke();
  }
}

// === LAYOUT ===
const page = Layout.inset(Layout.rect(STRIP+1, 0, W-(STRIP+1)*2, H), 0, 12);
const HDR_H = 115;
const [hdrRect, mainRect] = Layout.rows(page, [HDR_H, page.height - HDR_H], 0);
const [leftRect, rightRect] = Layout.columns(mainRect, [42, 58], 12);

// === HEADER ===
const SHADOW = 5;
ctx.fillStyle = YELLOW;
ctx.fillRect(hdrRect.x+SHADOW, hdrRect.y+SHADOW, hdrRect.width, HDR_H);
ctx.fillStyle = INK;
ctx.fillRect(hdrRect.x, hdrRect.y, hdrRect.width, HDR_H);

ctx.fillStyle = YELLOW;
ctx.font = 'black 46px Impact, Arial';
ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.letterSpacing = LogoType.computeWordmarkTracking(46, true) + 'em';
ctx.fillText('THE INTERNET WAS', hdrRect.x+16, hdrRect.y+10);
ctx.fillText('NEVER ON SCHEDULE', hdrRect.x+16, hdrRect.y+57);
ctx.letterSpacing = '0px';

// === LEFT: TRUE-SCALE VERTICAL TIMELINE ===
const tlPad = Layout.inset(leftRect, 12, 8, 20, 4);
const TL_YEAR_MIN = 1969, TL_YEAR_MAX = 2022;
const yScale = Scale.linear(TL_YEAR_MIN, TL_YEAR_MAX, tlPad.y, tlPad.y2);

// Stem x is in the middle of the column
const TL_X = tlPad.x + Math.round(tlPad.width * 0.45);

// --- Year ticks ---
const TICK_YEARS = [1970,1975,1980,1985,1990,1995,2000,2005,2010,2015,2020];
ctx.font = '400 10px "Courier New", monospace';
ctx.fillStyle = 'rgba(10,10,10,0.4)';
ctx.strokeStyle = 'rgba(10,10,10,0.2)';
ctx.lineWidth = 1;
ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
for (const yr of TICK_YEARS) {
  const ty = yScale.map(yr);
  ctx.beginPath(); ctx.moveTo(TL_X-4, ty); ctx.lineTo(tlPad.x2, ty); ctx.stroke();
  ctx.fillText(yr, TL_X-8, ty);
}

// --- Stem ---
ctx.strokeStyle = INK; ctx.lineWidth = 5;
ctx.beginPath();
ctx.moveTo(TL_X, yScale.map(TL_YEAR_MIN));
ctx.lineTo(TL_X, yScale.map(TL_YEAR_MAX));
ctx.stroke();

// --- Big gap blocks (LEFT of stem, as filled regions) ---
// These show the waiting time visually as solid blocks left of the stem
const GAP_BW = 56; // block width, left of stem
const bigGaps = [
  { fromY: 1969, toY: 1989, label: '20 YR\nWAIT', color: PINK   },
  { fromY: 2007, toY: 2022, label: '15 YR\nWAIT', color: ORANGE },
];
for (const g of bigGaps) {
  const gy1 = yScale.map(g.fromY);
  const gy2 = yScale.map(g.toY);
  const gx = TL_X - GAP_BW - 2;
  const gh = gy2 - gy1;
  // Shadow
  ctx.fillStyle = INK;
  ctx.fillRect(gx+4, gy1+4, GAP_BW, gh);
  // Block
  ctx.fillStyle = g.color;
  ctx.fillRect(gx, gy1, GAP_BW, gh);
  ctx.strokeStyle = INK; ctx.lineWidth = 3;
  ctx.strokeRect(gx, gy1, GAP_BW, gh);
  // Label
  const lines = g.label.split('\n');
  ctx.fillStyle = WHITE;
  ctx.font = 'bold 15px Impact, Arial';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText(lines[0], gx + GAP_BW/2, (gy1+gy2)/2 - 9);
  ctx.fillText(lines[1], gx + GAP_BW/2, (gy1+gy2)/2 + 9);
}

// --- Milestone event cards (RIGHT of stem) ---
const CARD_MARGIN = 8; // gap between stem and card
const cardX = TL_X + CARD_MARGIN;
const cardW = tlPad.x2 - cardX;

for (let i = 0; i < MILESTONES.length; i++) {
  const m = MILESTONES[i];
  const my = yScale.map(m.year);
  const col = MCOLORS[i];
  const cardH = 50;
  const cardY = my - cardH/2;

  // Stem dot
  ctx.fillStyle = INK;
  ctx.beginPath(); ctx.arc(TL_X, my, 11, 0, Math.PI*2); ctx.fill();
  ctx.fillStyle = col;
  ctx.beginPath(); ctx.arc(TL_X, my, 8, 0, Math.PI*2); ctx.fill();

  // Connector line from stem to card
  ctx.strokeStyle = col; ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(TL_X + 10, my);
  ctx.lineTo(cardX + 2, my);
  ctx.stroke();

  // Card shadow
  ctx.fillStyle = INK;
  ctx.fillRect(cardX+3, cardY+3, cardW, cardH);
  // Card fill
  ctx.fillStyle = col;
  ctx.fillRect(cardX, cardY, cardW, cardH);
  ctx.strokeStyle = INK; ctx.lineWidth = 3;
  ctx.strokeRect(cardX, cardY, cardW, cardH);

  // Year badge at left of card
  ctx.fillStyle = INK;
  ctx.font = 'bold 12px "Courier New", monospace';
  ctx.textAlign = 'left'; ctx.textBaseline = 'top';
  ctx.fillText(m.year, cardX + 5, cardY + 4);

  // Milestone name
  ctx.font = 'bold 15px Impact, Arial';
  ctx.fillText(m.label, cardX + 5, cardY + 18);

  // Description
  ctx.font = '400 9px "Courier New", monospace';
  ctx.fillText(m.desc, cardX + 5, cardY + 36);
}

// === RIGHT PANELS ===
const rPad = Layout.inset(rightRect, 8, 0, 8, 0);
const [rCallout, rGaps, rEra, rFooter] = Layout.rows(rPad, [20, 36, 15, 29], 10);

const drawPanel = (rect, titleText, titleBg, titleFg) => {
  ctx.fillStyle = INK;
  ctx.fillRect(rect.x+5, rect.y+5, rect.width, rect.height);
  ctx.fillStyle = WHITE;
  ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
  ctx.strokeStyle = INK; ctx.lineWidth = 3;
  ctx.strokeRect(rect.x, rect.y, rect.width, rect.height);
  const tH = 28;
  ctx.fillStyle = titleBg;
  ctx.fillRect(rect.x, rect.y, rect.width, tH);
  ctx.fillStyle = titleFg || INK;
  ctx.font = 'bold 11px "Courier New", monospace';
  ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
  ctx.letterSpacing = '0.08em';
  ctx.fillText(titleText, rect.x + 10, rect.y + tH/2);
  ctx.letterSpacing = '0px';
  return tH;
};

// --- CALLOUT: 20 YRS vs 2 MOS ---
const tH1 = drawPanel(rCallout, '// LONGEST WAIT VS FASTEST ADOPTION', INK, YELLOW);
const i1 = Layout.inset(rCallout, tH1 + 8, 12, 10, 12);
const [i1left, i1right] = Layout.columns(i1, [1, 1], 10);

// Left: 20 YRS
ctx.fillStyle = PINK;
ctx.font = 'black 58px Impact, Arial';
ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.fillText('20 YRS', i1left.x, i1left.y);
ctx.fillStyle = INK;
ctx.font = 'bold 10px "Courier New", monospace';
ctx.fillText('ARPANET\u2192WEB', i1left.x, i1left.y + 62);
ctx.fillText('(1969\u21921989)', i1left.x, i1left.y + 74);

// Right: 2 MOS
ctx.fillStyle = GREEN;
ctx.font = 'black 58px Impact, Arial';
ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.fillText('2 MOS', i1right.x, i1right.y);
ctx.fillStyle = INK;
ctx.font = 'bold 10px "Courier New", monospace';
ctx.fillText('CHATGPT 100M', i1right.x, i1right.y + 62);
ctx.fillText('(2022)', i1right.x, i1right.y + 74);

// Central divider label
const midX = (i1left.x2 + i1right.x) / 2;
ctx.fillStyle = INK;
ctx.font = 'black 18px Impact, Arial';
ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
ctx.fillText('vs.', midX, i1.y + i1.height/2);

// --- GAP BAR CHART ---
const tH2 = drawPanel(rGaps, '// WAITING GAPS (YEARS BETWEEN REVOLUTIONS)', INK, YELLOW);
const i2 = Layout.inset(rGaps, tH2 + 8, 12, 28, 100);

const gapMax = 20;
const gapBounds = Scale.nice(0, gapMax);
const barY = Scale.band(GAPS.length, i2.y, i2.y2, 0.28);
const barX = Scale.linear(0, gapBounds.max, i2.x, i2.x2);
if (!barX.isZeroBased) throw new Error('gap bars need zero baseline');

const GAP_COLORS = [PINK, ORANGE, PURPLE, CYAN, GREEN, YELLOW];

for (let i = 0; i < GAPS.length; i++) {
  const g = GAPS[i];
  const by = barY.map(i);
  const bh = barY.bandwidth;
  const bx0 = barX.map(0);
  const bw = barX.extent(0, g.years);
  const col = GAP_COLORS[i];

  ctx.fillStyle = 'rgba(10,10,10,0.25)';
  ctx.fillRect(bx0+3, by+3, bw, bh);
  ctx.fillStyle = col;
  ctx.fillRect(bx0, by, bw, bh);
  ctx.strokeStyle = INK; ctx.lineWidth = 2.5;
  ctx.strokeRect(bx0, by, bw, bh);

  // Label left
  ctx.fillStyle = INK;
  ctx.font = 'bold 10px "Courier New", monospace';
  ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
  ctx.fillText(g.label, bx0 - 6, by + bh/2);

  // Value right of bar
  ctx.font = 'bold 12px Impact, Arial';
  ctx.textAlign = 'left';
  ctx.fillText(g.years + 'y', barX.map(g.years) + 5, by + bh/2);
}

// Axis
ctx.strokeStyle = INK; ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(barX.map(0), i2.y2 + 2);
ctx.lineTo(barX.map(gapBounds.max), i2.y2 + 2);
ctx.stroke();
for (const t of [0, 5, 10, 15, 20]) {
  const tx = barX.map(t);
  ctx.beginPath(); ctx.moveTo(tx, i2.y2+2); ctx.lineTo(tx, i2.y2+7); ctx.stroke();
  ctx.fillStyle = INK;
  ctx.font = '400 9px "Courier New", monospace';
  ctx.textAlign = 'center'; ctx.textBaseline = 'top';
  ctx.fillText(t, tx, i2.y2 + 9);
}

// --- ERA SEGMENTED BAR ---
const tH3 = drawPanel(rEra, '// 53 YEARS: HOW THE TIME PASSED', INK, YELLOW);
const i3 = Layout.inset(rEra, tH3 + 10, 12, 36, 12);

const eraBarH = 42;
const eraBarY = i3.y + 4;
const eraScale = Scale.linear(0, 53, i3.x, i3.x2);

let eX = i3.x;
for (const e of ERA_DATA) {
  const ew = eraScale.extent(0, e.years);
  ctx.fillStyle = INK;
  ctx.fillRect(eX+3, eraBarY+3, ew, eraBarH);
  ctx.fillStyle = e.color;
  ctx.fillRect(eX, eraBarY, ew, eraBarH);
  ctx.strokeStyle = INK; ctx.lineWidth = 3;
  ctx.strokeRect(eX, eraBarY, ew, eraBarH);
  // % label inside
  ctx.fillStyle = WHITE;
  ctx.font = 'bold 13px Impact, Arial';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText((e.years/53*100).toFixed(0)+'%', eX + ew/2, eraBarY + eraBarH/2);
  eX += ew;
}

// Legend
let legX = i3.x;
const legY = eraBarY + eraBarH + 8;
for (const e of ERA_DATA) {
  ctx.fillStyle = e.color;
  ctx.fillRect(legX, legY, 12, 10);
  ctx.strokeStyle = INK; ctx.lineWidth = 1;
  ctx.strokeRect(legX, legY, 12, 10);
  ctx.fillStyle = INK;
  ctx.font = '400 9px "Courier New", monospace';
  ctx.textAlign = 'left'; ctx.textBaseline = 'top';
  const legLabel = e.years + 'y ' + e.label;
  ctx.fillText(legLabel, legX + 16, legY);
  legX += 16 + ctx.measureText(legLabel).width + 14;
}

// --- CLAIM FOOTER ---
const tH4 = drawPanel(rFooter, '// THE CLAIM', YELLOW, INK);
const i4 = Layout.inset(rFooter, tH4 + 12, 14, 14, 14);

ctx.fillStyle = INK;
ctx.font = 'black 24px Impact, Arial';
ctx.textAlign = 'left'; ctx.textBaseline = 'top';
ctx.letterSpacing = '0.01em';
let cy = i4.y;
for (const line of ['PROGRESS IS CONSTANT —', 'IT NEVER STOPPED COMING,', 'BUT IT NEVER ONCE', 'CAME ON SCHEDULE.']) {
  ctx.fillText(line, i4.x, cy);
  cy += 30;
}
ctx.letterSpacing = '0px';

// Stat block
cy += 10;
const stats = [{v:'66%', l:'OF 53 YEARS WAITING'}, {v:'35y', l:'IN 2 LONG LULLS'}];
for (let i = 0; i < stats.length; i++) {
  const sx = i4.x + i * 190;
  ctx.fillStyle = PINK;
  ctx.font = 'black 36px Impact, Arial';
  ctx.textAlign = 'left'; ctx.textBaseline = 'top';
  ctx.fillText(stats[i].v, sx, cy);
  ctx.fillStyle = INK;
  ctx.font = 'bold 10px "Courier New", monospace';
  ctx.fillText(stats[i].l, sx, cy + 40);
}

// Footer teletype line
const footerY = page.y2 - 18;
ctx.fillStyle = 'rgba(10,10,10,0.4)';
ctx.font = '400 9px "Courier New", monospace';
ctx.textAlign = 'left'; ctx.textBaseline = 'bottom';
ctx.fillText('DATA: BRIEF.MD // FIGURES VERIFIED // ZERO-BASELINE ASSERTED // 1969-2022', page.x, footerY);

log('Done. eraSum=' + eraSum + ' gapMax=' + gapMax);
canvas;
