Stage.begin('Detail');

Stage.note('Detail Refinement: Adjusting typography metrics, badge lockups, and spine geometry for crisp readability and zero collisions.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// --- 1. DATA DEFINITIONS ---
const milestones = [
  { year: 1969, event: 'ARPANET sends its first message (it crashed after "LO")', title: 'ARPANET SENDS FIRST MESSAGE', tag: 'CRASHED ON "LO"', color: '#ff4742', gap: null },
  { year: 1989, event: 'Tim Berners-Lee proposes the World Wide Web', title: 'TIM BERNERS-LEE PROPOSES WWW', tag: 'WWW PROPOSAL', color: '#00e676', gap: 20 },
  { year: 1993, event: 'Mosaic makes the web visual', title: 'MOSAIC MAKES THE WEB VISUAL', tag: 'GUI BROWSER', color: '#00f0ff', gap: 4 },
  { year: 1998, event: 'Google is founded', title: 'GOOGLE SEARCH IS FOUNDED', tag: 'INDEXING WEB', color: '#ffe600', gap: 5 },
  { year: 2004, event: 'Facebook launches — the social era begins', title: 'FACEBOOK LAUNCHES SOCIAL ERA', tag: 'SOCIAL ERA', color: '#b388ff', gap: 6 },
  { year: 2007, event: 'The iPhone puts the web in every pocket', title: 'IPHONE PUTS WEB IN POCKETS', tag: 'MOBILE WEB', color: '#ff80ab', gap: 3 },
  { year: 2022, event: 'ChatGPT reaches 100M users in 2 months', title: 'CHATGPT REACHES 100M USERS', tag: '100M IN 2 MO', color: '#00e676', gap: 15 }
];

// --- 2. PALETTE & THEME TOKENS ---
const C = {
  paper: '#f6f1e5',
  paperAlt: '#eee7d4',
  paperZebra: 'rgba(0, 180, 80, 0.04)',
  ink: '#0a0a0a',
  inkMuted: '#5a5a5a',
  border: '#0a0a0a',
  yellow: '#ffe500',
  cyan: '#00e5ff',
  coral: '#ff4742',
  green: '#00e676',
  purple: '#c084fc',
  pink: '#ff77a9',
  cardBg: '#ffffff',
  trackBg: '#ebe4d0'
};

// --- 3. BACKGROUND & TRACTOR MARGINS ---
ctx.fillStyle = C.paper;
ctx.fillRect(0, 0, 1080, 1920);

// Zebra bands (accounting paper style)
ctx.fillStyle = C.paperZebra;
for (let y = 0; y < 1920; y += 72) {
  ctx.fillRect(0, y, 1080, 36);
}

// Tractor feed margin perforation guide lines
ctx.strokeStyle = '#cfc6b0';
ctx.lineWidth = 1.5;
ctx.setLineDash([6, 6]);
ctx.beginPath();
ctx.moveTo(68, 0); ctx.lineTo(68, 1920);
ctx.moveTo(1080 - 68, 0); ctx.lineTo(1080 - 68, 1920);
ctx.stroke();
ctx.setLineDash([]);

// Sprocket holes
for (let y = 30; y < 1920; y += 45) {
  // Left sprocket
  ctx.fillStyle = '#181714';
  ctx.beginPath();
  ctx.arc(34, y, 9.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#9e947e';
  ctx.lineWidth = 1.5;
  ctx.stroke();
  
  // Right sprocket
  ctx.beginPath();
  ctx.arc(1080 - 34, y, 9.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

// Brutalist Box helper
const brutalBox = (x, y, w, h, fill = '#ffffff', shadowX = 6, shadowY = 6, strokeW = 3.5, strokeCol = C.border) => {
  if (shadowX !== 0 || shadowY !== 0) {
    ctx.fillStyle = C.ink;
    ctx.fillRect(x + shadowX, y + shadowY, w, h);
  }
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  if (strokeW > 0) {
    ctx.strokeStyle = strokeCol;
    ctx.lineWidth = strokeW;
    ctx.strokeRect(x, y, w, h);
  }
};

// Sticker Badge helper
const drawStickerBadge = (text, x, y, bgCol, textCol = C.ink, deg = 0) => {
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate((deg * Math.PI) / 180);
  ctx.font = '800 12px Consolas, monospace';
  const tw = ctx.measureText(text).width + 16;
  brutalBox(-tw/2, -13, tw, 26, bgCol, 3, 3, 2);
  ctx.fillStyle = textCol;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, 0, 1);
  ctx.restore();
};

// --- 4. HEADER ZONE ---
const header = Layout.rect(84, 26, 912, 190);
brutalBox(header.x, header.y, header.width, header.height, C.yellow, 8, 8, 4);

// Top ticker tape
brutalBox(header.x + 16, header.y + 12, header.width - 32, 26, C.ink, 0, 0, 0);
ctx.fillStyle = C.yellow;
ctx.font = '800 11px Consolas, monospace';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.fillText('>>> CONTINUOUS FANFOLD LOG // FORM #1080-PORTRAIT // TIMELINE AUDIT', header.x + 26, header.y + 25);
ctx.textAlign = 'right';
ctx.fillText('STATUS: ONLINE [BAUD 9600]', header.x + header.width - 26, header.y + 25);

// Title
ctx.fillStyle = C.ink;
ctx.textAlign = 'left';
ctx.textBaseline = 'alphabetic';
ctx.font = '900 44px Impact, Arial Black, sans-serif';
ctx.fillText("THE INTERNET'S GREATEST MILESTONES", header.x + 20, header.y + 86);

// Client-specified Core Claim
ctx.font = '700 17px Arial, sans-serif';
ctx.fillStyle = C.ink;
ctx.fillText("Progress is constant — it never stopped coming, but it never once came on schedule.", header.x + 22, header.y + 124);

// Metadata row
ctx.font = '600 12.5px Consolas, monospace';
ctx.fillStyle = C.inkMuted;
ctx.fillText("53-YEAR RECORD [1969-2022] // 7 CRITICAL BREAKTHROUGHS // CALIBRATED TO EXACT TEMPORAL SCALE", header.x + 22, header.y + 158);

// Top right decorative stamp
drawStickerBadge('LINE PRINTER FEED', header.x + header.width - 100, header.y + 84, C.coral, '#ffffff', 3);

// --- 5. TIMELINE & CARD ARMATURE ---
const spineX = 84;
const spineW = 270;
const cardX = 372;
const cardW = 624;

const timelineTopY = 244;
const timelineHeight = 1374;
const timelineBottomY = timelineTopY + timelineHeight;

// Spine box
brutalBox(spineX, timelineTopY, spineW, timelineHeight, C.trackBg, 6, 6, 3.5);

// Spine header banner
brutalBox(spineX + 10, timelineTopY + 10, spineW - 20, 36, C.ink, 0, 0, 0);
ctx.fillStyle = '#ffffff';
ctx.font = '800 12.5px Consolas, monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
ctx.fillText('TEMPORAL RULER [1969-2022]', spineX + spineW/2, timelineTopY + 28);

// Linear time scale mapping
const rulerTopY = timelineTopY + 68;
const rulerBottomY = timelineBottomY - 45;
const timeScale = Scale.linear(1969, 2022, rulerTopY, rulerBottomY);

// Zero-based gap scale for delta bars
const gapBounds = Scale.nice(0, 20);
const maxGapBarWidth = 105;
const gapScale = Scale.linear(0, gapBounds.max, 0, maxGapBarWidth);
if (!gapScale.isZeroBased) throw new Error('bars and columns need a zero baseline');

// Axis line
const axisX = spineX + 56;
ctx.strokeStyle = C.ink;
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(axisX, rulerTopY);
ctx.lineTo(axisX, rulerBottomY);
ctx.stroke();

// Decade and 5-year ticks
for (let yr = 1970; yr <= 2020; yr += 5) {
  const ty = timeScale.map(yr);
  const isDecade = (yr % 10 === 0);
  ctx.strokeStyle = C.ink;
  ctx.lineWidth = isDecade ? 3 : 1.5;
  ctx.beginPath();
  ctx.moveTo(axisX - (isDecade ? 14 : 7), ty);
  ctx.lineTo(axisX + (isDecade ? 14 : 7), ty);
  ctx.stroke();

  if (isDecade) {
    ctx.font = '800 13px Consolas, monospace';
    ctx.fillStyle = C.inkMuted;
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';
    ctx.fillText(yr.toString(), axisX - 18, ty);
  }
}

// Extent labels on axis
ctx.font = '900 14px Consolas, monospace';
ctx.fillStyle = C.ink;
ctx.textAlign = 'right';
ctx.textBaseline = 'middle';
ctx.fillText('1969', axisX - 18, rulerTopY);
ctx.fillText('2022', axisX - 18, rulerBottomY);

// Gap delta duration bars on spine
for (let i = 1; i < milestones.length; i++) {
  const m = milestones[i];
  const prevM = milestones[i - 1];
  const yPrev = timeScale.map(prevM.year);
  const yCurr = timeScale.map(m.year);
  const yMid = (yPrev + yCurr) / 2;
  
  // Interval dashed guide
  ctx.strokeStyle = 'rgba(0,0,0,0.3)';
  ctx.lineWidth = 1.5;
  ctx.setLineDash([3, 3]);
  ctx.beginPath();
  ctx.moveTo(axisX + 16, yPrev + 8);
  ctx.lineTo(axisX + 16, yCurr - 8);
  ctx.stroke();
  ctx.setLineDash([]);
  
  // Gap Bar
  const barLen = gapScale.extent(0, m.gap);
  const barX = axisX + 22;
  const barY = yMid - 10;
  
  const gapCol = m.gap >= 15 ? C.coral : (m.gap <= 4 ? C.green : C.yellow);
  brutalBox(barX, barY, barLen, 20, gapCol, 2, 2, 1.5);
  
  ctx.font = '800 11.5px Consolas, monospace';
  ctx.fillStyle = C.ink;
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillText('+' + m.gap + 'y', barX + barLen + 4, yMid + 1);
}

// --- 6. MILESTONE CARDS (RIGHT COLUMN) ---
const cardHeights = [154, 154, 154, 154, 154, 154, 154];
const cardGap = 20;
const cardStartY = timelineTopY;

const cardRects = [];
let currCY = cardStartY;
for (let i = 0; i < milestones.length; i++) {
  cardRects.push(Layout.rect(cardX, currCY, cardW, cardHeights[i]));
  currCY += cardHeights[i] + cardGap;
}

// Render cards
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const card = cardRects[i];
  const timeY = timeScale.map(m.year);
  
  // Connection line from Timeline dot (axisX, timeY) to Card left anchor
  ctx.strokeStyle = C.ink;
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(axisX, timeY);
  ctx.lineTo(cardX - 16, card.cy);
  ctx.lineTo(card.x, card.cy);
  ctx.stroke();

  // Timeline node dot on axis
  ctx.fillStyle = m.color;
  ctx.beginPath();
  ctx.arc(axisX, timeY, 8.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = C.ink;
  ctx.lineWidth = 3;
  ctx.stroke();

  // Center pupil
  ctx.fillStyle = C.ink;
  ctx.beginPath();
  ctx.arc(axisX, timeY, 2.5, 0, Math.PI * 2);
  ctx.fill();

  // Card Box
  brutalBox(card.x, card.y, card.width, card.height, C.cardBg, 6, 6, 3.5);

  // Year badge
  const yearBadgeW = 104;
  brutalBox(card.x + 12, card.y + 12, yearBadgeW, 44, m.color, 3, 3, 2.5);
  ctx.font = '900 30px Impact, Arial Black, sans-serif';
  ctx.fillStyle = C.ink;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(m.year.toString(), card.x + 12 + yearBadgeW/2, card.y + 12 + 22);

  // Card Title (fitted cleanly to avoid collision with badge)
  ctx.font = '900 18px Arial, sans-serif';
  ctx.fillStyle = C.ink;
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  const titleX = card.x + 128;
  const maxTitleWidth = card.width - 128 - 145; // room for tag badge
  ctx.fillText(m.title, titleX, card.y + 34, maxTitleWidth);

  // Sticker badge on top right
  drawStickerBadge(m.tag, card.x + card.width - 76, card.y + 34, C.paperAlt, C.ink, (i % 2 === 0 ? -1.5 : 1.5));

  // Divider
  ctx.strokeStyle = '#e4ded0';
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(card.x + 12, card.y + 68);
  ctx.lineTo(card.x + card.width - 12, card.y + 68);
  ctx.stroke();

  // Event narrative (from brief verbatim)
  ctx.font = '700 14px Consolas, monospace';
  ctx.fillStyle = C.ink;
  ctx.textBaseline = 'top';
  ctx.fillText('EVENT: ' + m.event, card.x + 14, card.y + 78, card.width - 28);

  // Delta description
  ctx.font = '600 12.5px Consolas, monospace';
  ctx.fillStyle = C.inkMuted;
  if (m.gap === null) {
    ctx.fillText('DELTA: [T-ZERO BASELINE] // ORIGIN OF PACKET SWITCHING', card.x + 14, card.y + 108);
  } else {
    const gapDetail = m.gap === 20 ? '20-YR GAP (THE 2-DECADE WAIT: LAB MESSAGE -> WORLD WIDE WEB)' :
                      (m.gap === 15 ? '15-YR GAP (SMARTPHONE INFLECTION -> CONSUMER AI EXPLOSION)' :
                      (m.gap === 3 ? '3-YR SPRINT (SHORTEST WAIT: SOCIAL ERA -> POCKET INTERNET)' :
                      '+' + m.gap + ' YRS ELAPSED ACROSS RAPID 1989-2007 INNOVATION CYCLE'));
    ctx.fillText('DELTA: +' + m.gap + ' YEARS // ' + gapDetail, card.x + 14, card.y + 108, card.width - 28);
  }

  // Card footer micro index
  ctx.font = '800 11px Consolas, monospace';
  ctx.fillStyle = C.ink;
  ctx.textAlign = 'right';
  ctx.fillText('MILESTONE ' + (i + 1) + ' OF 7', card.x + card.width - 16, card.y + 132);
}

// --- 7. FOOTER SUMMARY DASHBOARD ---
const footer = Layout.rect(84, 1640, 912, 244);
brutalBox(footer.x, footer.y, footer.width, footer.height, C.yellow, 8, 8, 4);

// Footer ticker header
brutalBox(footer.x + 16, footer.y + 12, footer.width - 32, 26, C.ink, 0, 0, 0);
ctx.fillStyle = C.yellow;
ctx.font = '800 11.5px Consolas, monospace';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.fillText('/// EXECUTIVE SUMMARY: TEMPORAL DISTRIBUTION & VELOCITY METRICS ///', footer.x + 26, footer.y + 25);
ctx.textAlign = 'right';
ctx.fillText('ALL ARITHMETIC VERIFIED FROM BRIEF DATA TABLE', footer.x + footer.width - 26, footer.y + 25);

// 4 stat boxes
const [stat1, stat2, stat3, stat4] = Layout.columns(Layout.rect(footer.x + 16, footer.y + 48, footer.width - 32, 176), 4, 14);

const drawStatCard = (box, val, unit, caption, fillCol, badgeTxt) => {
  brutalBox(box.x, box.y, box.width, box.height, fillCol, 4, 4, 3);
  ctx.textAlign = 'center';
  ctx.textBaseline = 'top';
  
  // Big Numeral
  ctx.font = '900 40px Impact, Arial Black, sans-serif';
  ctx.fillStyle = C.ink;
  ctx.fillText(val, box.cx, box.y + 10);
  
  // Unit
  ctx.font = '800 12.5px Consolas, monospace';
  ctx.fillStyle = C.ink;
  ctx.fillText(unit, box.cx, box.y + 54);
  
  // Divider
  ctx.strokeStyle = C.ink;
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.moveTo(box.x + 8, box.y + 74);
  ctx.lineTo(box.x + box.width - 8, box.y + 74);
  ctx.stroke();

  // Caption
  ctx.font = '700 11.5px Arial, sans-serif';
  ctx.fillStyle = C.ink;
  ctx.fillText(caption, box.cx, box.y + 84);
  
  // Badge
  drawStickerBadge(badgeTxt, box.cx, box.y + 140, C.paperAlt, C.ink, 0);
};

drawStatCard(stat1, '53', 'YEARS TOTAL', '1969 to 2022', '#ffffff', 'TOTAL SPAN');
drawStatCard(stat2, '20', 'YR MAX GAP', 'ARPANET → Web', C.coral, 'LONGEST LULL');
drawStatCard(stat3, '3', 'YR MIN GAP', 'FB → iPhone', C.green, 'FASTEST SPRINT');
drawStatCard(stat4, '100M', 'IN 2 MONTHS', 'ChatGPT Velocity', C.cyan, 'SPEED RECORD');

canvas;
