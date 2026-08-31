Stage.begin('Palette & Type');

Stage.note('Settling Design Language: Neo-brutalist green-bar line printer aesthetic.');
Stage.note('Colors: Fanfold pale green background (#eef9f2 / #d8f3e5), Electric Mint Header (#00f076), Stamped Hot Pink (#ff2a5f), Hard Black Borders & Shadows (#0a0a0a).');
Stage.note('Typography: Consolas & Courier New monospace, rigorous scale ladder from 14px to 44px.');

const sheet = Css.fromCss(`
  :root {
    --bg-paper: #eef9f2;
    --bg-stripe: #d8f3e5;
    --ink: #0a0a0a;
    --ink-dim: #234d3b;
    --accent-green: #00f076;
    --accent-pink: #ff2a5f;
    --accent-cyan: #00e5ff;
    --accent-yellow: #f5ee38;
    --card-bg: #ffffff;
    --border-dark: #0a0a0a;
    --font-mono: 'Consolas', 'Courier New', monospace;
  }
  .display { font: 900 42px/1.1 'Consolas', monospace; color: #0a0a0a; text-transform: uppercase; }
  .h1      { font: 700 24px/1.2 'Consolas', monospace; color: #0a0a0a; text-transform: uppercase; }
  .h2      { font: 700 18px/1.2 'Consolas', monospace; color: #0a0a0a; }
  .body    { font: 400 16px/1.4 'Consolas', monospace; color: #0a0a0a; }
  .badge   { font: 900 14px/1 'Consolas', monospace; color: #ffffff; text-transform: uppercase; }
  .caption { font: 400 13px/1.3 'Consolas', monospace; color: #234d3b; }
`);

const tokens = sheet.tokens();
log('Tokens loaded: ' + JSON.stringify(tokens));

const typeScale = LogoType.calculateTypographicScale(16, 'majorThird', 1, 4);
log('Typographic Scale ratios: ' + JSON.stringify(typeScale));

const milestones = [
  { year: 1969, title: 'ARPANET', detail: 'Sends its first message (crashed after "LO")', note: 'First node-to-node link' },
  { year: 1989, title: 'World Wide Web', detail: 'Tim Berners-Lee proposes the Web at CERN', note: '20-year desert ends' },
  { year: 1993, title: 'Mosaic', detail: 'NCSA Mosaic makes the web visual', note: 'Images & GUI browser' },
  { year: 1998, title: 'Google', detail: 'Google is founded in Menlo Park', note: 'PageRank revolution' },
  { year: 2004, title: 'Facebook', detail: 'Facebook launches — the social era begins', note: 'Campus to global network' },
  { year: 2007, title: 'iPhone', detail: 'The iPhone puts the web in every pocket', note: 'Mobile omnipresence' },
  { year: 2022, title: 'ChatGPT', detail: 'Reaches 100M users in 2 months', note: 'Generative AI era begins' }
];

const gaps = [
  { from: '1969', to: '1989', val: 20, label: '20-YEAR DESERT', desc: 'Arpanet to WWW' },
  { from: '1989', to: '1993', val: 4, label: '4-YEAR GAP', desc: 'WWW to Mosaic' },
  { from: '1993', to: '1998', val: 5, label: '5-YEAR GAP', desc: 'Mosaic to Google' },
  { from: '1998', to: '2004', val: 6, label: '6-YEAR GAP', desc: 'Google to Facebook' },
  { from: '2004', to: '2007', val: 3, label: '3-YEAR SPRINT', desc: 'Facebook to iPhone' },
  { from: '2007', to: '2022', val: 15, label: '15-YEAR PLATEAU', desc: 'iPhone to ChatGPT' }
];

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Fanfold green background
ctx.fillStyle = tokens['--bg-paper'];
ctx.fillRect(0, 0, 1080, 1920);

// Alternating green-bar printer bands
ctx.fillStyle = tokens['--bg-stripe'];
for (let y = 0; y < 1920; y += 64) {
  if (Math.floor(y / 64) % 2 === 0) {
    ctx.fillRect(0, y, 1080, 64);
  }
}

// Perforation / Tractor feed margin
const tractW = 60;
ctx.fillStyle = tokens['--ink'];
ctx.fillRect(tractW, 0, 3, 1920);
ctx.fillRect(1080 - tractW, 0, 3, 1920);

for (let y = 32; y < 1920; y += 48) {
  ctx.fillStyle = '#1b4332';
  ctx.beginPath();
  ctx.arc(30, y, 9, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(1080 - 30, y, 9, 0, Math.PI * 2);
  ctx.fill();
}

// Top Header Card
const headRect = Layout.rect(80, 40, 920, 240);
// Shadow
ctx.fillStyle = tokens['--border-dark'];
ctx.fillRect(headRect.x + 8, headRect.y + 8, headRect.width, headRect.height);
// Body
ctx.fillStyle = tokens['--accent-green'];
ctx.fillRect(headRect.x, headRect.y, headRect.width, headRect.height);
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 4;
ctx.strokeRect(headRect.x, headRect.y, headRect.width, headRect.height);

// Header Decorative Top Bar
ctx.fillStyle = '#0a0a0a';
ctx.fillRect(headRect.x, headRect.y, headRect.width, 32);
ctx.fillStyle = '#00f076';
ctx.font = '700 14px "Consolas", monospace';
ctx.fillText('SYS_PRINTER // SPOOL: 1969-2022 // CONTINUOUS FEED', headRect.x + 16, headRect.y + 22);

ctx.fillStyle = tokens['--ink'];
const disp = sheet.rule('.display');
ctx.font = disp.font;
ctx.fillText('THE WEB ON NO SCHEDULE', headRect.x + 20, headRect.y + 78);

const h1 = sheet.rule('.h1');
ctx.font = h1.font;
ctx.fillText('53 YEARS OF UNPREDICTABLE LEAPS & DESERTS', headRect.x + 20, headRect.y + 120);

// Claim Callout Box inside Header
ctx.fillStyle = '#ffffff';
ctx.fillRect(headRect.x + 20, headRect.y + 145, headRect.width - 40, 75);
ctx.strokeRect(headRect.x + 20, headRect.y + 145, headRect.width - 40, 75);

ctx.fillStyle = tokens['--accent-pink'];
ctx.fillRect(headRect.x + 20, headRect.y + 145, 12, 75);

ctx.fillStyle = tokens['--ink'];
ctx.font = '700 16px "Consolas", monospace';
ctx.fillText('CLAIM: Progress is constant — it never stopped coming,', headRect.x + 45, headRect.y + 175);
ctx.fillText('       but it never once came on schedule.', headRect.x + 45, headRect.y + 202);

// Time Scale & Axis
const yStart = 330;
const yEnd = 1630;
const timeScale = Scale.linear(1969, 2022, yStart, yEnd);
const spineX = 230;

// Decade gridlines & ticks
const decades = [1970, 1980, 1990, 2000, 2010, 2020];
for (const d of decades) {
  const dy = timeScale.map(d);
  ctx.strokeStyle = '#95c4a7';
  ctx.lineWidth = 2;
  ctx.setLineDash([6, 6]);
  ctx.beginPath();
  ctx.moveTo(90, dy);
  ctx.lineTo(990, dy);
  ctx.stroke();
  ctx.setLineDash([]);
  
  // Decade label
  ctx.fillStyle = '#1c4a35';
  ctx.font = '700 15px "Consolas", monospace';
  ctx.textAlign = 'right';
  ctx.fillText(d.toString(), spineX - 25, dy + 5);
}
ctx.textAlign = 'left';

// Main Spine
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 6;
ctx.beginPath();
ctx.moveTo(spineX, yStart - 15);
ctx.lineTo(spineX, yEnd + 25);
ctx.stroke();

// Staggered vertical slots for milestone cards
const slotYs = [
  timeScale.map(1969) - 35,
  timeScale.map(1989) - 45,
  timeScale.map(1993) - 35,
  timeScale.map(1998) - 15,
  timeScale.map(2004) + 10,
  timeScale.map(2007) + 30,
  timeScale.map(2022) - 45
];

// Gap scale
const gapScale = Scale.linear(0, 20, 0, 150);
if (!gapScale.isZeroBased) throw new Error('Zero baseline violated');

// Draw Milestones & Gaps
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const py = timeScale.map(m.year);
  
  // Node on timeline spine
  ctx.fillStyle = tokens['--accent-green'];
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.arc(spineX, py, 13, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
  
  // Year stamped on spine
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '900 24px "Consolas", monospace';
  ctx.textAlign = 'right';
  ctx.fillText(m.year.toString(), spineX - 25, py + 8);
  ctx.textAlign = 'left';
  
  // Leader line
  const cardX = 330;
  const cardY = slotYs[i];
  const cardW = 660;
  const cardH = 96;
  
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(spineX + 13, py);
  ctx.lineTo(cardX, cardY + cardH / 2);
  ctx.stroke();
  
  // Card Shadow & Card
  ctx.fillStyle = tokens['--border-dark'];
  ctx.fillRect(cardX + 6, cardY + 6, cardW, cardH);
  ctx.fillStyle = tokens['--card-bg'];
  ctx.fillRect(cardX, cardY, cardW, cardH);
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = 3;
  ctx.strokeRect(cardX, cardY, cardW, cardH);
  
  // Card header bar
  ctx.fillStyle = (i === 0 || i === 6) ? tokens['--accent-yellow'] : tokens['--accent-cyan'];
  ctx.fillRect(cardX, cardY, cardW, 30);
  ctx.strokeRect(cardX, cardY, cardW, 30);
  
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '900 17px "Consolas", monospace';
  ctx.fillText('[' + m.year + '] ' + m.title.toUpperCase(), cardX + 12, cardY + 21);
  
  // Detail
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '700 15px "Consolas", monospace';
  ctx.fillText(m.detail, cardX + 12, cardY + 58);
  
  ctx.fillStyle = tokens['--ink-dim'];
  ctx.font = '400 13px "Consolas", monospace';
  ctx.fillText('→ ' + m.note, cardX + 12, cardY + 82);
  
  // Draw gap bar and badge in between
  if (i < gaps.length) {
    const gap = gaps[i];
    const nextPy = timeScale.map(milestones[i+1].year);
    const midY = (py + nextPy) / 2;
    
    // Gap badge
    const badgeW = 120 + gapScale.extent(0, gap.val);
    const badgeX = spineX + 25;
    const badgeY = midY - 14;
    
    // Draw horizontal bar indicator
    ctx.fillStyle = tokens['--border-dark'];
    ctx.fillRect(badgeX + 3, badgeY + 3, badgeW, 28);
    ctx.fillStyle = tokens['--accent-pink'];
    ctx.fillRect(badgeX, badgeY, badgeW, 28);
    ctx.strokeStyle = tokens['--border-dark'];
    ctx.lineWidth = 2;
    ctx.strokeRect(badgeX, badgeY, badgeW, 28);
    
    ctx.fillStyle = '#ffffff';
    ctx.font = '900 13px "Consolas", monospace';
    ctx.fillText('▲ GAP: ' + gap.val + ' YRS (' + gap.desc + ')', badgeX + 8, badgeY + 18);
  }
}

// Summary Footer Panel
const footRect = Layout.rect(80, 1720, 920, 160);
ctx.fillStyle = tokens['--border-dark'];
ctx.fillRect(footRect.x + 8, footRect.y + 8, footRect.width, footRect.height);
ctx.fillStyle = tokens['--accent-green'];
ctx.fillRect(footRect.x, footRect.y, footRect.width, footRect.height);
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 4;
ctx.strokeRect(footRect.x, footRect.y, footRect.width, footRect.height);

ctx.fillStyle = '#0a0a0a';
ctx.fillRect(footRect.x, footRect.y, footRect.width, 28);
ctx.fillStyle = '#00f076';
ctx.font = '700 13px "Consolas", monospace';
ctx.fillText('FANFOLD AUDIT LOG // VERIFIED GROUND TRUTH', footRect.x + 16, footRect.y + 19);

ctx.fillStyle = tokens['--ink'];
ctx.font = '900 18px "Consolas", monospace';
ctx.fillText('QUANTITATIVE SUMMARY OF THE 53-YEAR RUN:', footRect.x + 20, footRect.y + 60);

ctx.font = '700 14px "Consolas", monospace';
ctx.fillText('• TOTAL TIMELINE SPAN: 53 YEARS (1969 to 2022)', footRect.x + 20, footRect.y + 90);
ctx.fillText('• LONGEST VOID: 20 YEARS (ARPANET → Web) | SHORTEST SPRINT: 3 YEARS (FB → iPhone)', footRect.x + 20, footRect.y + 115);
ctx.fillText('• FRENZY CLUSTER: 5 MAJOR PLATFORMS BORN IN JUST 14 YEARS (1993–2007)', footRect.x + 20, footRect.y + 140);

canvas;
