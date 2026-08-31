Stage.begin('Detail');

Stage.note('Stage 6: Rendering complete neo-brutalist fanfold line-printer infographic.');
Stage.note('Scene elements: Tractor feed sprockets, green-bar accounting bands, diegetic line printer stamps, calibrated timeline track, zero-based gap delta meters, 7 milestone cards, and hero analytics dashboard.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// --- 1. THE DATA & DERIVED FIGURES ---
const rawMilestones = [
  { year: 1969, event: 'ARPANET sends its first message (it crashed after "LO")', title: 'ARPANET SENDS FIRST MESSAGE', tag: 'CRASHED AFTER "LO"', color: '#ff4742', gap: null },
  { year: 1989, event: 'Tim Berners-Lee proposes the World Wide Web', title: 'TIM BERNERS-LEE PROPOSES WWW', tag: 'HYPERTEXT PROPOSAL', color: '#00e676', gap: 20 },
  { year: 1993, event: 'Mosaic makes the web visual', title: 'MOSAIC MAKES THE WEB VISUAL', tag: 'FIRST GRAPHICAL BROWSER', color: '#00f0ff', gap: 4 },
  { year: 1998, event: 'Google is founded', title: 'GOOGLE SEARCH IS FOUNDED', tag: 'ORGANIZING WORLD INFO', color: '#ffe600', gap: 5 },
  { year: 2004, event: 'Facebook launches — the social era begins', title: 'FACEBOOK LAUNCHES SOCIAL ERA', tag: 'THE SOCIAL ERA BEGINS', color: '#b388ff', gap: 6 },
  { year: 2007, event: 'The iPhone puts the web in every pocket', title: 'IPHONE PUTS WEB IN POCKETS', tag: 'POCKET INTERNET REVOLUTION', color: '#ff80ab', gap: 3 },
  { year: 2022, event: 'ChatGPT reaches 100M users in 2 months', title: 'CHATGPT REACHES 100M USERS', tag: '100M IN 2 MONTHS', color: '#00e676', gap: 15 }
];

const totalSpan = 2022 - 1969; // 53 years

// Derived gaps verification
const gaps = [
  { from: '1969', to: '1989', gap: 1989 - 1969, label: 'ARPANET → Web gap' },
  { from: '1989', to: '1993', gap: 1993 - 1989, label: 'Web → Mosaic gap' },
  { from: '1993', to: '1998', gap: 1998 - 1993, label: 'Mosaic → Google gap' },
  { from: '1998', to: '2004', gap: 2004 - 1998, label: 'Google → Facebook gap' },
  { from: '2004', to: '2007', gap: 2007 - 2004, label: 'Facebook → iPhone gap' },
  { from: '2007', to: '2022', gap: 2022 - 2007, label: 'iPhone → ChatGPT gap' }
];

// --- 2. PALETTE & TOKENS ---
const C = {
  paper: '#f5f0e4',
  paperZebra: 'rgba(20, 160, 90, 0.04)',
  ink: '#0e0e0e',
  inkMuted: '#585858',
  border: '#0e0e0e',
  yellow: '#ffe817',
  cyan: '#00e5ff',
  coral: '#ff4d42',
  green: '#00e676',
  purple: '#c084fc',
  cardBg: '#ffffff',
  trackBg: '#ebe4d2'
};

// --- 3. GROUND & TRACTOR FEED MARGINS ---
ctx.fillStyle = C.paper;
ctx.fillRect(0, 0, 1080, 1920);

// Alternating continuous green-bar printer bands (every 40px)
ctx.fillStyle = C.paperZebra;
for (let y = 0; y < 1920; y += 80) {
  ctx.fillRect(0, y, 1080, 40);
}

// Tractor feed margin perforation guide lines
ctx.strokeStyle = '#d4ccb8';
ctx.lineWidth = 1;
ctx.setLineDash([4, 4]);
ctx.beginPath();
ctx.moveTo(70, 0); ctx.lineTo(70, 1920);
ctx.moveTo(1080 - 70, 0); ctx.lineTo(1080 - 70, 1920);
ctx.stroke();
ctx.setLineDash([]);

// Sprocket holes
const drawSprockets = (x) => {
  for (let y = 30; y < 1920; y += 45) {
    ctx.fillStyle = '#1c1a16';
    ctx.beginPath();
    ctx.arc(x, y, 9, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#857c6a';
    ctx.lineWidth = 1.5;
    ctx.stroke();
  }
};
drawSprockets(35);
drawSprockets(1080 - 35);

// Helper for Neo-Brutalist Box with solid offset shadow
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

// Helper for rotated badge
const drawStickerBadge = (text, x, y, bgCol, textCol = C.ink, deg = 0) => {
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate((deg * Math.PI) / 180);
  ctx.font = '800 13px Consolas, monospace';
  const tw = ctx.measureText(text).width + 20;
  brutalBox(-tw/2, -14, tw, 28, bgCol, 3, 3, 2.5);
  ctx.fillStyle = textCol;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(text, 0, 1);
  ctx.restore();
};

// --- 4. HEADER ZONE (TOP MASTHEAD) ---
const headerBox = Layout.rect(88, 28, 904, 185);
brutalBox(headerBox.x, headerBox.y, headerBox.width, headerBox.height, C.yellow, 8, 8, 4);

// Top ticker tape in header
brutalBox(headerBox.x + 16, headerBox.y + 14, headerBox.width - 32, 28, C.ink, 0, 0, 0);
ctx.fillStyle = C.yellow;
ctx.font = '800 12px Consolas, monospace';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.fillText('>>> TELETYPE CONTINUOUS FEED // JOB #1969-2022 // LOG: MILESTONES_SCALE.DAT', headerBox.x + 28, headerBox.y + 28);
ctx.textAlign = 'right';
ctx.fillText('STATUS: VERIFIED [OK]', headerBox.x + headerBox.width - 28, headerBox.y + 28);

// Main title
ctx.fillStyle = C.ink;
ctx.textAlign = 'left';
ctx.textBaseline = 'alphabetic';
ctx.font = '900 48px Impact, Arial Black, sans-serif';
ctx.letterSpacing = '-0.01em';
ctx.fillText("THE INTERNET'S GREATEST MILESTONES", headerBox.x + 20, headerBox.y + 92);

// Subhead & claim
ctx.letterSpacing = '0px';
ctx.font = '700 17px Arial, sans-serif';
ctx.fillStyle = C.ink;
ctx.fillText("Progress is constant — it never stopped coming, but it never once came on schedule.", headerBox.x + 22, headerBox.y + 128);

// Meta stripe
ctx.font = '600 13px Consolas, monospace';
ctx.fillStyle = C.inkMuted;
ctx.fillText("53-YEAR HISTORICAL AUDIT [1969-2022] // 7 CRITICAL LEAPS // DRAWN TO TRUE TEMPORAL SCALE", headerBox.x + 22, headerBox.y + 158);

// Decorative stamp badge on header
drawStickerBadge('FANFOLD LINE PRINTER', headerBox.x + headerBox.width - 110, headerBox.y + 82, C.coral, '#ffffff', 4);

// --- 5. TIMELINE & CARD LAYOUT ARMATURE ---
// Left spine (Timeline track): x = 88 to 330 (width = 242)
// Right column (Milestone cards): x = 345 to 992 (width = 647)
const spineX = 88;
const spineW = 236;
const cardX = 340;
const cardW = 652;

const timelineTopY = 240;
const timelineHeight = 1380;
const timelineBottomY = timelineTopY + timelineHeight;

// Background container for timeline spine
brutalBox(spineX, timelineTopY, spineW, timelineHeight, C.trackBg, 6, 6, 3.5);

// Spine header
brutalBox(spineX + 10, timelineTopY + 10, spineW - 20, 36, C.ink, 0, 0, 0);
ctx.fillStyle = '#ffffff';
ctx.font = '800 13px Consolas, monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
ctx.fillText('TEMPORAL RULER', spineX + spineW/2, timelineTopY + 28);

// True linear time scale mapping
const rulerTopY = timelineTopY + 65;
const rulerBottomY = timelineBottomY - 45;
const timeScale = Scale.linear(1969, 2022, rulerTopY, rulerBottomY);

// Zero-based gap scale for horizontal duration meters
const gapBounds = Scale.nice(0, 20);
const maxGapBarWidth = 100;
const gapScale = Scale.linear(0, gapBounds.max, 0, maxGapBarWidth);
if (!gapScale.isZeroBased) throw new Error('bars and columns need a zero baseline');

// Draw timeline spine vertical axis line
const axisX = spineX + 54;
ctx.strokeStyle = C.ink;
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(axisX, rulerTopY);
ctx.lineTo(axisX, rulerBottomY);
ctx.stroke();

// Draw 5-year tick marks and 10-year grid labels on the spine
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
    ctx.font = '800 14px Consolas, monospace';
    ctx.fillStyle = C.inkMuted;
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';
    ctx.fillText(yr.toString(), axisX - 18, ty);
  }
}

// Start year and End year labels on axis
ctx.font = '900 15px Consolas, monospace';
ctx.fillStyle = C.ink;
ctx.textAlign = 'right';
ctx.textBaseline = 'middle';
ctx.fillText('1969', axisX - 18, rulerTopY);
ctx.fillText('2022', axisX - 18, rulerBottomY);

// --- 6. DRAWING INTER-MILESTONE GAPS ON THE SPINE ---
// Draw delta duration bars and labels on the right side of the spine axis
for (let i = 1; i < rawMilestones.length; i++) {
  const m = rawMilestones[i];
  const prevM = rawMilestones[i - 1];
  const yPrev = timeScale.map(prevM.year);
  const yCurr = timeScale.map(m.year);
  const yMid = (yPrev + yCurr) / 2;
  
  // Interval line indicator along timeline track
  ctx.strokeStyle = 'rgba(0,0,0,0.25)';
  ctx.lineWidth = 2;
  ctx.setLineDash([2, 3]);
  ctx.beginPath();
  ctx.moveTo(axisX + 16, yPrev + 8);
  ctx.lineTo(axisX + 16, yCurr - 8);
  ctx.stroke();
  ctx.setLineDash([]);
  
  // Gap Bar (Zero-based length)
  const barLen = gapScale.extent(0, m.gap);
  const barX = axisX + 22;
  const barY = yMid - 11;
  
  // Color bar by wait time duration
  const gapCol = m.gap >= 15 ? C.coral : (m.gap <= 4 ? C.green : C.yellow);
  brutalBox(barX, barY, barLen, 22, gapCol, 2, 2, 1.5);
  
  ctx.font = '800 12px Consolas, monospace';
  ctx.fillStyle = C.ink;
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillText('+' + m.gap + 'y', barX + barLen + 5, yMid + 1);
}

// --- 7. MILESTONE CARDS (RIGHT COLUMN) ---
// We stack 7 cards down the right column with measured leader lines anchored to exact timeScale.map(year)
// Card heights and positions calculated with generous layout
const cardHeights = [156, 156, 156, 156, 156, 156, 156];
const cardGap = 20;
const cardStartY = timelineTopY;

// Let's compute card bounding boxes
const cardRects = [];
let currCY = cardStartY;
for (let i = 0; i < rawMilestones.length; i++) {
  cardRects.push(Layout.rect(cardX, currCY, cardW, cardHeights[i]));
  currCY += cardHeights[i] + cardGap;
}

// Render each milestone card with connection to temporal ruler
for (let i = 0; i < rawMilestones.length; i++) {
  const m = rawMilestones[i];
  const card = cardRects[i];
  const timeY = timeScale.map(m.year);
  
  // 1. Connection line from Timeline dot on axis (axisX, timeY) to Card anchor (card.x, card.cy)
  ctx.strokeStyle = C.ink;
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(axisX, timeY);
  ctx.lineTo(cardX - 18, card.cy);
  ctx.lineTo(card.x, card.cy);
  ctx.stroke();

  // Timeline node marker dot on axis
  ctx.fillStyle = m.color;
  ctx.beginPath();
  ctx.arc(axisX, timeY, 9, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = C.ink;
  ctx.lineWidth = 3;
  ctx.stroke();

  // Inner center dot
  ctx.fillStyle = C.ink;
  ctx.beginPath();
  ctx.arc(axisX, timeY, 3, 0, Math.PI * 2);
  ctx.fill();

  // 2. Card Box
  brutalBox(card.x, card.y, card.width, card.height, C.cardBg, 6, 6, 3.5);

  // Card Header strip with Year badge
  const yearBadgeW = 110;
  brutalBox(card.x + 14, card.y + 14, yearBadgeW, 46, m.color, 3, 3, 2.5);
  ctx.font = '900 32px Impact, Arial Black, sans-serif';
  ctx.fillStyle = C.ink;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(m.year.toString(), card.x + 14 + yearBadgeW/2, card.y + 14 + 23);

  // Event Title
  ctx.font = '900 20px Arial, sans-serif';
  ctx.fillStyle = C.ink;
  ctx.textAlign = 'left';
  ctx.textBaseline = 'alphabetic';
  ctx.fillText(m.title, card.x + 138, card.y + 36);

  // Tag Badge beside title
  drawStickerBadge(m.tag, card.x + card.width - 120, card.y + 32, C.paper, C.ink, (i % 2 === 0 ? -1.5 : 1.5));

  // Divider inside card
  ctx.strokeStyle = '#e5dfcf';
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(card.x + 14, card.y + 70);
  ctx.lineTo(card.x + card.width - 14, card.y + 70);
  ctx.stroke();

  // Milestone narrative & context text
  ctx.font = '700 15px Consolas, monospace';
  ctx.fillStyle = C.ink;
  ctx.textBaseline = 'top';
  ctx.fillText('EVENT: ' + m.event, card.x + 16, card.y + 80);

  // Technical metric / wait time annotation
  ctx.font = '600 13px Consolas, monospace';
  ctx.fillStyle = C.inkMuted;
  if (m.gap === null) {
    ctx.fillText('DELTA: [T-ZERO BASELINE] // INCEPTION OF PACKET SWITCHING NETWORK', card.x + 16, card.y + 112);
  } else {
    const gapComment = m.gap === 20 ? '20-YEAR GAP (THE LONGEST WAIT — FROM LAB PACKET TO THE WEB)' :
                       (m.gap === 15 ? '15-YEAR GAP (SMARTPHONE ERA TO CONSUMER AI INFLECTION)' :
                       (m.gap === 3 ? '3-YEAR SPRINT (SHORTEST GAP — SOCIAL BOOM TO SMARTPHONE)' :
                       '+' + m.gap + ' YEARS INTERVAL ACROSS EXPLOSIVE MID-90s/00s CYCLE'));
    ctx.fillText('DELTA: +' + m.gap + ' YEARS ELAPSED // ' + gapComment, card.x + 16, card.y + 112);
  }

  // Micro status pill on bottom right
  ctx.font = '800 11px Consolas, monospace';
  ctx.fillStyle = C.ink;
  ctx.textAlign = 'right';
  ctx.fillText('INDEX #' + (i + 1) + ' / 7', card.x + card.width - 20, card.y + 134);
}

// --- 8. FOOTER SUMMARY DASHBOARD (HERO ANALYTICS) ---
const footerBox = Layout.rect(88, 1640, 904, 240);
brutalBox(footerBox.x, footerBox.y, footerBox.width, footerBox.height, C.yellow, 8, 8, 4);

// Footer banner header
brutalBox(footerBox.x + 16, footerBox.y + 14, footerBox.width - 32, 28, C.ink, 0, 0, 0);
ctx.fillStyle = C.yellow;
ctx.font = '800 12px Consolas, monospace';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.fillText('/// SUMMARY AUDIT: TEMPORAL DISTRIBUTION & VELOCITY METRICS ///', footerBox.x + 28, footerBox.y + 28);
ctx.textAlign = 'right';
ctx.fillText('DERIVED FROM BRIEF TABLE DATA', footerBox.x + footerBox.width - 28, footerBox.y + 28);

// 4 Stat callout cards inside footer
const [stat1, stat2, stat3, stat4] = Layout.columns(Layout.rect(footerBox.x + 16, footerBox.y + 52, footerBox.width - 32, 170), 4, 14);

const drawStatBox = (box, num, unit, desc, bgCol, alertBadge = null) => {
  brutalBox(box.x, box.y, box.width, box.height, bgCol, 4, 4, 3);
  ctx.textAlign = 'center';
  ctx.textBaseline = 'top';
  
  // Big number
  ctx.font = '900 42px Impact, Arial Black, sans-serif';
  ctx.fillStyle = C.ink;
  ctx.fillText(num, box.cx, box.y + 12);
  
  // Unit
  ctx.font = '800 13px Consolas, monospace';
  ctx.fillStyle = C.ink;
  ctx.fillText(unit, box.cx, box.y + 58);
  
  // Line separator
  ctx.strokeStyle = C.ink;
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.moveTo(box.x + 10, box.y + 78);
  ctx.lineTo(box.x + box.width - 10, box.y + 78);
  ctx.stroke();

  // Description text
  ctx.font = '600 11px Arial, sans-serif';
  ctx.fillStyle = C.ink;
  ctx.fillText(desc, box.cx, box.y + 88);
  
  if (alertBadge) {
    drawStickerBadge(alertBadge, box.cx, box.y + 135, C.paper, C.ink, 0);
  }
};

drawStatBox(stat1, '53', 'YEARS TOTAL', '1969 to 2022', '#ffffff', 'TOTAL SPAN');
drawStatBox(stat2, '20', 'YR MAX GAP', 'ARPANET → Web', C.coral, 'LONGEST LULL');
drawStatBox(stat3, '3', 'YR MIN GAP', 'FB → iPhone', C.green, 'FASTEST SPRINT');
drawStatBox(stat4, '100M', 'IN 2 MONTHS', 'ChatGPT Adoption', C.cyan, 'SPEED RECORD');

canvas;
