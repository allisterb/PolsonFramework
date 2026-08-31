Stage.begin('Detail');
Stage.note('Scene construction: Line printer fanfold paper ground, tractor-feed margins, matrix ruler ticks, neo-brutalist card containers, zero-based gap scale, and area-encoded callouts.');

const W = 1080;
const H = 1920;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// -------------------------------------------------------------
// 1. GROUND & TEXTURE (Fanfold Greenbar Paper + Platen Environment)
// -------------------------------------------------------------
// Base paper tone
ctx.fillStyle = '#f8f5ed';
ctx.fillRect(0, 0, W, H);

// Greenbar alternating ledger bands (every 40px)
const stripeH = 38;
for (let y = 0; y < H; y += stripeH * 2) {
  ctx.fillStyle = '#edf6ec'; // vintage greenbar tint
  ctx.fillRect(0, y, W, stripeH);
}

// Tractor feed strips on left and right margins
const tractorW = 60;
ctx.fillStyle = '#eae4d5';
ctx.fillRect(0, 0, tractorW, H);
ctx.fillRect(W - tractorW, 0, tractorW, H);

// Perforations
ctx.strokeStyle = '#a8a090';
ctx.lineWidth = 1.5;
ctx.setLineDash([5, 5]);
ctx.beginPath();
ctx.moveTo(tractorW, 0);
ctx.lineTo(tractorW, H);
ctx.moveTo(W - tractorW, 0);
ctx.lineTo(W - tractorW, H);
ctx.stroke();

// Fanfold page fold perforation across the vertical middle
ctx.beginPath();
ctx.moveTo(0, H * 0.52);
ctx.lineTo(W, H * 0.52);
ctx.stroke();
ctx.setLineDash([]);

// Fold label
ctx.fillStyle = '#8c8474';
ctx.font = '700 10px monospace';
ctx.textAlign = 'center';
ctx.fillText('--- [ TEAR ALONG PERFORATION / PAGE FOLD 01-OF-02 ] ---', W / 2, H * 0.52 - 6);
ctx.textAlign = 'left';

// Sprocket holes
ctx.fillStyle = '#d8d0be';
ctx.strokeStyle = '#222222';
ctx.lineWidth = 2;
for (let y = 20; y < H; y += 40) {
  // Left sprocket
  ctx.beginPath();
  ctx.arc(tractorW / 2, y, 8.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();

  // Right sprocket
  ctx.beginPath();
  ctx.arc(W - tractorW / 2, y, 8.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

// Fine matrix ruler ticks along tractor edges
ctx.strokeStyle = '#666666';
ctx.lineWidth = 1;
for (let y = 10; y < H; y += 10) {
  const tickLen = (y % 50 === 0) ? 10 : 5;
  ctx.beginPath();
  ctx.moveTo(tractorW, y);
  ctx.lineTo(tractorW - tickLen, y);
  ctx.moveTo(W - tractorW, y);
  ctx.lineTo(W - tractorW + tickLen, y);
  ctx.stroke();
}

// -------------------------------------------------------------
// 2. NEO-BRUTALIST DRAWING PRIMITIVES
// -------------------------------------------------------------
function drawBrutalistCard(ctx, x, y, w, h, fill = '#ffffff', shadowX = 6, shadowY = 6, strokeW = 3) {
  // Hard offset shadow
  ctx.fillStyle = '#111111';
  ctx.fillRect(x + shadowX, y + shadowY, w, h);
  // Card fill
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  // Card border
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = strokeW;
  ctx.strokeRect(x, y, w, h);
}

function drawBadge(ctx, x, y, text, fill = '#ffea00', font = '700 12px Consolas, monospace', padH = 6, padW = 10) {
  ctx.font = font;
  const metrics = ctx.measureText(text);
  const w = metrics.width + padW * 2;
  const h = 22;
  // Shadow
  ctx.fillStyle = '#111111';
  ctx.fillRect(x + 3, y + 3, w, h);
  // Box
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 2;
  ctx.strokeRect(x, y, w, h);
  // Text
  ctx.fillStyle = '#111111';
  ctx.textBaseline = 'middle';
  ctx.textAlign = 'center';
  ctx.fillText(text, x + w / 2, y + h / 2);
  ctx.textAlign = 'left';
  return w;
}

// -------------------------------------------------------------
// 3. LAYOUT ZONES
// -------------------------------------------------------------
const innerArea = Layout.rect(tractorW + 18, 16, W - 2 * (tractorW + 18), H - 32);
const [headerBox, bodyBox, footerBox] = Layout.rows(innerArea, [240, 1460, 160], 16);

// -------------------------------------------------------------
// 4. HEADER: TELETYPE MAINFRAME BANNER
// -------------------------------------------------------------
drawBrutalistCard(ctx, headerBox.x, headerBox.y, headerBox.width, headerBox.height, '#ffea00', 8, 8, 3.5);

// Header badges & metadata
drawBadge(ctx, headerBox.x + 20, headerBox.y + 16, 'LP-3800 // TELETYPE HIGH-SPEED IMPACT PLATEN', '#00f0ff');
drawBadge(ctx, headerBox.x + 400, headerBox.y + 16, 'JOB #01969-2022', '#ff6b8b');
drawBadge(ctx, headerBox.x + 550, headerBox.y + 16, 'VERIFIED PUBLIC DATA', '#00e676');

// Header Title
ctx.fillStyle = '#111111';
ctx.font = '900 48px "Trebuchet MS", "Impact", sans-serif';
ctx.textBaseline = 'top';
ctx.fillText("THE WEB'S GREATEST HITS", headerBox.x + 20, headerBox.y + 50);

// Claim subtitle
ctx.font = '700 16px Consolas, monospace';
ctx.fillStyle = '#111111';
ctx.fillText("Progress is constant — it never stopped coming, but it never once came on schedule.", headerBox.x + 20, headerBox.y + 112);

// Header Diagnostic stats row
const statBoxY = headerBox.y + 152;
ctx.strokeStyle = '#111111';
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(headerBox.x + 20, statBoxY);
ctx.lineTo(headerBox.x2 - 20, statBoxY);
ctx.stroke();

ctx.font = '900 13px Consolas, monospace';
ctx.fillText("TOTAL TIMELINE: 53 YEARS (1969-2022)  |  7 PUBLIC MILESTONES  |  LONGEST GAP: 20 YEARS", headerBox.x + 20, statBoxY + 14);
ctx.font = '600 12px Consolas, monospace';
ctx.fillStyle = '#333333';
ctx.fillText("SCALE ENCODING: LINEAR TIME AXIS (Y) + ZERO-BASED DURATION BARS (X) + AREA-PROPORTIONAL STAMPS", headerBox.x + 20, statBoxY + 36);

// -------------------------------------------------------------
// 5. BODY: ASYMMETRIC EDITORIAL SPREAD (Spine vs Milestone Cards)
// -------------------------------------------------------------
const [spineBox, eventStreamBox] = Layout.columns(bodyBox, [27, 73], 22);

// Time scale: Linear mapping 1969 to 2022
const timeScale = Scale.linear(1969, 2022, spineBox.y + 55, spineBox.y2 - 35);

// Spine Card Container
drawBrutalistCard(ctx, spineBox.x, spineBox.y, spineBox.width, spineBox.height, '#ffffff', 6, 6, 3);

// Spine Header
ctx.fillStyle = '#111111';
ctx.font = '900 15px Consolas, monospace';
ctx.fillText("TIME AXIS", spineBox.x + 16, spineBox.y + 16);
ctx.font = '600 11px Consolas, monospace';
ctx.fillStyle = '#555555';
ctx.fillText("[1969 → 2022]", spineBox.x + 16, spineBox.y + 32);

// Time Axis Spine Line
const spineAxisX = spineBox.x + 46;
ctx.beginPath();
ctx.moveTo(spineAxisX, spineBox.y + 55);
ctx.lineTo(spineAxisX, spineBox.y2 - 25);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 3;
ctx.stroke();

// Decade tick marks along the spine
for (let y = 1970; y <= 2020; y += 10) {
  const py = timeScale.map(y);
  ctx.beginPath();
  ctx.moveTo(spineAxisX - 10, py);
  ctx.lineTo(spineAxisX + 10, py);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 2;
  ctx.stroke();

  ctx.fillStyle = '#666666';
  ctx.font = '700 12px Consolas, monospace';
  ctx.fillText(y.toString(), spineAxisX + 16, py + 4);
}

// -------------------------------------------------------------
// 6. DATA ENCODING: MILESTONES, ZERO-BASED GAPS & CARDS
// -------------------------------------------------------------
const milestones = [
  {
    year: 1969,
    title: 'ARPANET SENDS FIRST MESSAGE',
    desc: 'First node-to-node message transmitted between UCLA and Stanford.',
    note: 'It crashed after "LO" (intended "LOGIN"). The network was born with a crash.',
    gap: 20,
    tag: 'GENESIS',
    color: '#ff6b8b',
    targetCardY: spineBox.y + 20
  },
  {
    year: 1989,
    title: 'TIM BERNERS-LEE PROPOSES WWW',
    desc: 'Information Management: A Proposal submitted at CERN.',
    note: '20 years of military/academic silence, then hyperlinks changed humanity.',
    gap: 4,
    tag: 'INVENTION',
    color: '#00f0ff',
    targetCardY: spineBox.y + 370
  },
  {
    year: 1993,
    title: 'MOSAIC MAKES THE WEB VISUAL',
    desc: 'Marc Andreessen & NCSA release the first cross-platform graphical browser.',
    note: 'Added the <img> tag. Suddenly the internet was no longer a wall of ASCII.',
    gap: 5,
    tag: 'GRAPHICS',
    color: '#00e676',
    targetCardY: spineBox.y + 520
  },
  {
    year: 1998,
    title: 'GOOGLE IS FOUNDED',
    desc: 'Larry Page & Sergey Brin build PageRank in a Menlo Park garage.',
    note: 'Organizing the world’s knowledge into one famously empty search box.',
    gap: 6,
    tag: 'SEARCH',
    color: '#ffea00',
    targetCardY: spineBox.y + 680
  },
  {
    year: 2004,
    title: 'FACEBOOK LAUNCHES',
    desc: 'Mark Zuckerberg launches "Thefacebook" from Harvard dorm room.',
    note: 'The social era begins: poking friends, status updates, and digital vanity.',
    gap: 3,
    tag: 'SOCIAL',
    color: '#c084fc',
    targetCardY: spineBox.y + 850
  },
  {
    year: 2007,
    title: 'THE IPHONE IN EVERY POCKET',
    desc: 'Steve Jobs introduces iPhone: iPod, phone, and breakthrough internet communicator.',
    note: 'The web leaves the desk forever and attaches itself directly to human hands.',
    gap: 15,
    tag: 'MOBILE',
    color: '#ff9900',
    targetCardY: spineBox.y + 1020
  },
  {
    year: 2022,
    title: 'CHATGPT: MACHINES TALK BACK',
    desc: 'OpenAI releases ChatGPT, reaching 100M monthly active users in 2 months.',
    note: 'Fastest consumer tech adoption in history (100M users / 60 days).',
    gap: 0,
    tag: 'SYNTHESIS',
    color: '#ff3e00',
    targetCardY: spineBox.y + 1240
  }
];

// Truthful Gap Scale (0 to 20 years)
const gapScale = Scale.linear(0, 20, 0, spineBox.width - 55);
if (!gapScale.isZeroBased) throw new Error('Gap scale must be zero-based');

// Render Gap Bars & Connectors on the Spine
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const py = timeScale.map(m.year);

  // Milestone Pin on Axis
  ctx.fillStyle = m.color;
  ctx.beginPath();
  ctx.arc(spineAxisX, py, 9, 0, Math.PI * 2);
  ctx.fill();
  ctx.lineWidth = 2.5;
  ctx.strokeStyle = '#111111';
  ctx.stroke();

  // Gap visualization
  if (m.gap > 0) {
    const nextY = timeScale.map(milestones[i + 1].year);
    const midY = (py + nextY) / 2;
    const barW = gapScale.map(m.gap);

    // Span dashed line connecting the two milestone pins
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = 2;
    ctx.setLineDash([4, 4]);
    ctx.beginPath();
    ctx.moveTo(spineAxisX, py + 10);
    ctx.lineTo(spineAxisX, nextY - 10);
    ctx.stroke();
    ctx.setLineDash([]);

    // Gap Bar (Zero-based length encoding)
    ctx.fillStyle = '#ffea00';
    ctx.fillRect(spineAxisX + 12, midY - 12, barW, 24);
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = 2;
    ctx.strokeRect(spineAxisX + 12, midY - 12, barW, 24);

    ctx.fillStyle = '#111111';
    ctx.font = '900 12px Consolas, monospace';
    ctx.fillText('+' + m.gap + ' YRS', spineAxisX + 16, midY + 4);

    // If gap is huge (20y or 15y), add breathing void annotation
    if (m.gap >= 15) {
      ctx.fillStyle = '#888888';
      ctx.font = 'italic 700 10px Consolas, monospace';
      ctx.fillText('[QUIET ERA: ' + m.gap + ' YR HIATUS]', spineAxisX + 12, midY + 24);
    }
  }
}

// Render Milestone Cards in EventStreamBox
const cardW = eventStreamBox.width - 24;
const cardH = 125;

for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const py = timeScale.map(m.year);
  const cardY = m.targetCardY;
  const cardX = eventStreamBox.x + 12;

  // Connector from Spine dot to Card edge (Crossing boundary tension rule!)
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 2.5;
  ctx.beginPath();
  ctx.moveTo(spineAxisX, py);
  ctx.lineTo(cardX - 18, py);
  ctx.lineTo(cardX, cardY + cardH / 2);
  ctx.stroke();

  // Draw Card Container
  drawBrutalistCard(ctx, cardX, cardY, cardW, cardH, '#ffffff', 6, 6, 3);

  // Year Badge & Tag Badge
  drawBadge(ctx, cardX + 18, cardY + 14, m.year.toString(), m.color, '900 14px Consolas, monospace');
  drawBadge(ctx, cardX + 96, cardY + 14, m.tag, '#f0f0f0', '700 11px Consolas, monospace');

  if (m.gap > 0) {
    drawBadge(ctx, cardX + cardW - 130, cardY + 14, 'GAP: ' + m.gap + ' YRS', '#ffea00', '900 11px Consolas, monospace');
  } else {
    drawBadge(ctx, cardX + cardW - 150, cardY + 14, 'FINAL RECORD: 2022', '#ff3e00', '900 11px Consolas, monospace');
  }

  // Card Title
  ctx.fillStyle = '#111111';
  ctx.font = '900 19px "Trebuchet MS", sans-serif';
  ctx.fillText(m.title, cardX + 18, cardY + 54);

  // Description
  ctx.font = '600 13px Consolas, monospace';
  ctx.fillStyle = '#2d3748';
  ctx.fillText(m.desc, cardX + 18, cardY + 80, cardW - 36);

  // Humorous kicker / historical quote
  ctx.font = 'italic 700 11.5px Consolas, monospace';
  ctx.fillStyle = '#e53e3e';
  ctx.fillText('>> ' + m.note, cardX + 18, cardY + 102, cardW - 36);

  // Area-proportional callout badge on 2022 ChatGPT milestone
  if (m.year === 2022) {
    const stampX = cardX + cardW - 68;
    const stampY = cardY + cardH / 2 + 10;
    const r100M = Scale.radiusFor(100, 100, 36); // Area proportional to 100M
    ctx.fillStyle = '#ff3e00';
    ctx.beginPath();
    ctx.arc(stampX, stampY, r100M, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = 2.5;
    ctx.stroke();

    ctx.fillStyle = '#ffffff';
    ctx.font = '900 13px Consolas, monospace';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('100M', stampX, stampY - 8);
    ctx.font = '700 9px Consolas, monospace';
    ctx.fillText('USERS/2mo', stampX, stampY + 8);
    ctx.textAlign = 'left';
  }
}

// -------------------------------------------------------------
// 7. FOOTER: SYSTEM REPORT & RETROSPECTIVE
// -------------------------------------------------------------
drawBrutalistCard(ctx, footerBox.x, footerBox.y, footerBox.width, footerBox.height, '#ffffff', 8, 8, 3.5);

drawBadge(ctx, footerBox.x + 20, footerBox.y + 16, 'FINAL DIAGNOSTIC VERDICT', '#00e676');

ctx.fillStyle = '#111111';
ctx.font = '900 15px Consolas, monospace';
ctx.fillText("HISTORICAL TAKEAWAY: 53 YEARS OF RELENTLESS, IRREGULAR MOMENTUM", footerBox.x + 20, footerBox.y + 54);

ctx.font = '600 13px Consolas, monospace';
ctx.fillStyle = '#2d3748';
ctx.fillText("• The longest hiatus: 20 years between ARPANET (1969) and the World Wide Web (1989).", footerBox.x + 20, footerBox.y + 78);
ctx.fillText("• The hyperactive era: 1989 to 2007 packed Web, Mosaic, Google, Facebook & iPhone into 18 years.", footerBox.x + 20, footerBox.y + 98);
ctx.fillText("• The AI leap: 15 years after iPhone, ChatGPT reached 100M users in just 60 days.", footerBox.x + 20, footerBox.y + 118);

ctx.font = '700 10.5px Consolas, monospace';
ctx.fillStyle = '#718096';
ctx.fillText("RECORD LOGGED AT LINE PRINTER · ENGINE: POLSON SDK CORE · TRUTHFUL GEOMETRY AUDITED", footerBox.x + 20, footerBox.y + 140);

canvas;
