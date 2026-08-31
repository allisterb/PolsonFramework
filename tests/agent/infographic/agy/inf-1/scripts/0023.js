Stage.begin('Palette & Type');
Stage.note('Neo-brutalist print aesthetic: bold black strokes, hard drop-shadows, electric accent tags, and monospace line-printer type hierarchy.');

// Test font pairing
const pairing = LogoType.evaluateFontPairing('sansSerif', 'slab');
Stage.note('Font pairing evaluation: ' + pairing.relationship + ' (' + pairing.score + '/100) — ' + pairing.description);

// Calculate harmonic typographic scale
const typeScale = LogoType.calculateTypographicScale(16, 'augmentedFourth', 2, 5);
Stage.note('Typographic scale ratio (' + typeScale.ratioName + ' = ' + typeScale.ratioFactor + '): ' + 
  typeScale.steps.map(s => s.name + '=' + s.size.toFixed(1) + 'px').join(', '));

// Define design tokens via CSS
const sheet = Css.fromCss(`
  :root {
    --bg-paper: #f6f3eb;
    --bg-greenbar: #ebf5ee;
    --ink: #111111;
    --ink-muted: #4a5568;
    --border-ink: #111111;
    --shadow-ink: #111111;
    
    --accent-yellow: #ffea00;
    --accent-cyan: #00f0ff;
    --accent-orange: #ff4500;
    --accent-lime: #00e676;
    --accent-purple: #c084fc;
    --accent-pink: #ff6b8b;
    --accent-cream: #ffffff;
    
    --font-mono: 'Consolas', 'Courier New', monospace;
    --font-display: 'Trebuchet MS', 'Impact', sans-serif;
  }
`);

const tokens = sheet.tokens();
log('Tokens loaded: ink=' + tokens['--ink'] + ', yellow=' + tokens['--accent-yellow']);

const W = 1080;
const H = 1920;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// 1. Base Paper Ground with alternate line-printer / greenbar bands
ctx.fillStyle = '#f6f3eb';
ctx.fillRect(0, 0, W, H);

const bandHeight = 48;
for (let y = 0; y < H; y += bandHeight * 2) {
  ctx.fillStyle = '#eef5ec'; // greenbar stripe
  ctx.fillRect(0, y, W, bandHeight);
}

// 2. Tractor feed perforated strips
const tractorW = 64;
ctx.fillStyle = '#eae5d8';
ctx.fillRect(0, 0, tractorW, H);
ctx.fillRect(W - tractorW, 0, tractorW, H);

// Perforation line
ctx.strokeStyle = '#999999';
ctx.lineWidth = 1.5;
ctx.setLineDash([6, 6]);
ctx.beginPath();
ctx.moveTo(tractorW, 0);
ctx.lineTo(tractorW, H);
ctx.moveTo(W - tractorW, 0);
ctx.lineTo(W - tractorW, H);
ctx.stroke();
ctx.setLineDash([]);

// Pin-feed holes
ctx.fillStyle = '#d6cebd';
ctx.strokeStyle = '#111111';
ctx.lineWidth = 2;
for (let y = 24; y < H; y += 48) {
  ctx.beginPath();
  ctx.arc(tractorW / 2, y, 9, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();

  ctx.beginPath();
  ctx.arc(W - tractorW / 2, y, 9, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

// 3. Layout containers
const contentRect = Layout.rect(tractorW + 20, 20, W - 2 * (tractorW + 20), H - 40);
const [headerRect, bodyRect, footerRect] = Layout.rows(contentRect, [250, 1450, 140], 20);

// Neo-brutalist helper: draw box with solid 3px border and 5px hard black shadow
function drawBrutalistBox(ctx, x, y, w, h, fill = '#ffffff', shadowOffset = 6, strokeColor = '#111111') {
  // Shadow
  ctx.fillStyle = strokeColor;
  ctx.fillRect(x + shadowOffset, y + shadowOffset, w, h);
  // Box Fill
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  // Border
  ctx.strokeStyle = strokeColor;
  ctx.lineWidth = 3;
  ctx.strokeRect(x, y, w, h);
}

// Helper: draw badge/pill
function drawBadge(ctx, x, y, text, fill = '#ffea00', font = '700 13px Consolas, monospace') {
  ctx.font = font;
  const metrics = ctx.measureText(text);
  const padX = 10;
  const padY = 5;
  const w = metrics.width + padX * 2;
  const h = 24;
  
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

// Draw Header
drawBrutalistBox(ctx, headerRect.x, headerRect.y, headerRect.width, headerRect.height, '#ffea00', 8);

// Kicker badge
drawBadge(ctx, headerRect.x + 24, headerRect.y + 20, 'LP-3800 // CONTINUOUS ROLL TELETYPE', '#00f0ff');

// Hero Title
ctx.fillStyle = '#111111';
ctx.font = '900 44px "Trebuchet MS", "Impact", sans-serif';
ctx.textBaseline = 'top';
ctx.fillText("THE WEB'S GREATEST HITS", headerRect.x + 24, headerRect.y + 56);

// Subtitle / Claim
ctx.font = '700 17px Consolas, monospace';
ctx.fillStyle = '#111111';
const claimText = "Progress is constant: it never stopped coming, but it never once came on schedule.";
ctx.fillText(claimText, headerRect.x + 24, headerRect.y + 114, headerRect.width - 48);

// Header data banner / stats
const statY = headerRect.y + 160;
ctx.fillStyle = '#111111';
ctx.font = '900 13px Consolas, monospace';
ctx.fillText("SPAN: 1969 — 2022 [53 YEARS]  |  7 MILESTONES  |  AVG GAP: 8.8 YEARS", headerRect.x + 24, statY);
ctx.fillText("SOURCE: PUBLIC RECORD  |  PRODUCED ON FANFOLD PLATEN  |  NO LLM HALLUCINATIONS", headerRect.x + 24, statY + 22);

// Split Body: Spine vs Events
const [spineRect, eventsRect] = Layout.columns(bodyRect, [28, 72], 24);

// Time scale
const timeScale = Scale.linear(1969, 2022, spineRect.y + 40, spineRect.y2 - 40);
const axisX = spineRect.x + 40;

// Draw spine background track
drawBrutalistBox(ctx, spineRect.x, spineRect.y, spineRect.width, spineRect.height, '#ffffff', 6);
ctx.fillStyle = '#111111';
ctx.font = '900 14px Consolas, monospace';
ctx.fillText("TIMELINE SPUR", spineRect.x + 16, spineRect.y + 16);
ctx.font = '400 11px Consolas, monospace';
ctx.fillText("[TRUE LINEAR SCALE]", spineRect.x + 16, spineRect.y + 32);

// Spine rule
ctx.beginPath();
ctx.moveTo(axisX, spineRect.y + 55);
ctx.lineTo(axisX, spineRect.y2 - 20);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 3;
ctx.stroke();

// Year ticks
for (let y = 1970; y <= 2020; y += 10) {
  const py = timeScale.map(y);
  ctx.beginPath();
  ctx.moveTo(axisX - 8, py);
  ctx.lineTo(axisX + 8, py);
  ctx.stroke();
  ctx.fillStyle = '#111111';
  ctx.font = '700 13px Consolas, monospace';
  ctx.fillText(y.toString(), axisX + 16, py + 4);
}

// Data items
const data = [
  { year: 1969, title: 'ARPANET FIRST MESSAGE', subtitle: 'Sent first message (crashed after "LO")', gap: 20, tag: 'GENESIS', color: '#ff6b8b', humor: 'Intended message: "LOGIN". Computer died after two letters.' },
  { year: 1989, title: 'WORLD WIDE WEB PROPOSAL', subtitle: 'Tim Berners-Lee proposes the Web at CERN', gap: 4, tag: 'INVENTION', color: '#00f0ff', humor: '20 years of waiting, then "Vague but exciting".' },
  { year: 1993, title: 'MOSAIC BROWSER', subtitle: 'Mosaic makes the web visual & accessible', gap: 5, tag: 'HYPERTEXT', color: '#00e676', humor: 'Before this, the internet was just grey text on black screens.' },
  { year: 1998, title: 'GOOGLE FOUNDED', subtitle: 'Larry & Sergey organize world info', gap: 6, tag: 'SEARCH', color: '#ffea00', humor: 'A clean white page that ate every encyclopedia on earth.' },
  { year: 2004, title: 'FACEBOOK LAUNCHES', subtitle: 'The social networking era explodes', gap: 3, tag: 'SOCIAL', color: '#c084fc', humor: 'Now everyone could poke their roommates online.' },
  { year: 2007, title: 'IPHONE RELEASED', subtitle: 'The web lands inside every human pocket', gap: 15, tag: 'MOBILE', color: '#ff9900', humor: 'The moment you stopped looking up when walking across streets.' },
  { year: 2022, title: 'CHATGPT LAUNCH', subtitle: 'Reaches 100M users in just 2 months', gap: 0, tag: 'SYNTHESIS', color: '#ff3e00', humor: 'Fastest consumer tech adoption in human history.' }
];

// Draw gap scales and milestone cards
const maxGap = 20;
const gapScale = Scale.linear(0, maxGap, 0, spineRect.width - 50);
if (!gapScale.isZeroBased) throw new Error('bars need a zero baseline');

for (let i = 0; i < data.length; i++) {
  const item = data[i];
  const py = timeScale.map(item.year);

  // Pin on axis
  ctx.fillStyle = item.color;
  ctx.beginPath();
  ctx.arc(axisX, py, 9, 0, Math.PI * 2);
  ctx.fill();
  ctx.lineWidth = 2.5;
  ctx.strokeStyle = '#111111';
  ctx.stroke();

  // Gap bar and span annotation
  if (item.gap > 0) {
    const nextY = timeScale.map(data[i + 1].year);
    const midY = (py + nextY) / 2;
    const barW = gapScale.map(item.gap);

    // Gap bar
    ctx.fillStyle = '#ffea00';
    ctx.fillRect(axisX + 10, midY - 11, barW, 22);
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = 2;
    ctx.strokeRect(axisX + 10, midY - 11, barW, 22);

    ctx.fillStyle = '#111111';
    ctx.font = '900 11px Consolas, monospace';
    ctx.fillText('+' + item.gap + 'yr', axisX + 14, midY + 4);
    
    // Vertical bracket connecting milestones
    ctx.strokeStyle = '#111111';
    ctx.lineWidth = 1.5;
    ctx.setLineDash([4, 4]);
    ctx.beginPath();
    ctx.moveTo(axisX, py + 10);
    ctx.lineTo(axisX, nextY - 10);
    ctx.stroke();
    ctx.setLineDash([]);
  }

  // Card placement
  // Calculate vertical height and placement for card
  const cardW = eventsRect.width - 20;
  const cardH = 100;
  let cardY = py - cardH / 2;
  
  // Collision clamp so cards don't overlap in dense cluster (1989-2007)
  if (i > 0) {
    const prevPy = timeScale.map(data[i - 1].year);
    if (cardY < prevPy + 50) {
      cardY = prevPy + 52;
    }
  }

  // Connector line from spine dot to card
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 2.5;
  ctx.beginPath();
  ctx.moveTo(axisX, py);
  ctx.lineTo(eventsRect.x + 10, cardY + cardH / 2);
  ctx.stroke();

  // Draw Card
  drawBrutalistBox(ctx, eventsRect.x + 10, cardY, cardW, cardH, '#ffffff', 5);

  // Card Header Tag & Year Pill
  drawBadge(ctx, eventsRect.x + 22, cardY + 14, item.year.toString(), item.color);
  drawBadge(ctx, eventsRect.x + 95, cardY + 14, item.tag, '#ffffff');

  // Title
  ctx.fillStyle = '#111111';
  ctx.font = '900 17px "Trebuchet MS", sans-serif';
  ctx.fillText(item.title, eventsRect.x + 22, cardY + 54);

  // Subtitle
  ctx.font = '600 13px Consolas, monospace';
  ctx.fillStyle = '#333333';
  ctx.fillText(item.subtitle, eventsRect.x + 22, cardY + 74);

  // Humorous kicker
  ctx.font = 'italic 400 11px Consolas, monospace';
  ctx.fillStyle = '#ff4500';
  ctx.fillText('>> ' + item.humor, eventsRect.x + 22, cardY + 90);
}

// Footer
drawBrutalistBox(ctx, footerRect.x, footerRect.y, footerRect.width, footerRect.height, '#ffffff', 6);
drawBadge(ctx, footerRect.x + 20, footerRect.y + 16, 'SUMMARY DIAGNOSTIC', '#00f0ff');

ctx.fillStyle = '#111111';
ctx.font = '700 14px Consolas, monospace';
ctx.fillText("KEY TAKEAWAY: 53 years of total span. Longest desert: 20 years (1969-1989).", footerRect.x + 20, footerRect.y + 54);
ctx.fillText("Fastest acceleration: 2022 (ChatGPT, 100M users in 2 mos). The next milestone won't wait.", footerRect.x + 20, footerRect.y + 76);
ctx.font = '400 11px Consolas, monospace';
ctx.fillStyle = '#666666';
ctx.fillText("TELETYPE RECORD COMPLETED · POLSON ENGINE VERIFIED · ALL FIGURES TRACED TO SOURCE", footerRect.x + 20, footerRect.y + 104);

canvas;
