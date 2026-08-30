Stage.begin('Palette & Type');

Stage.note('Neo-Brutalist Design System Definition:');
Stage.note('- Flat saturated chromatic palette: Electric Yellow (#FFE500), Cyan (#00E5FF), Neon Lime (#00E676), Punch Red (#FF3B30), Hot Pink (#FF2A85), Safety Orange (#FF6D00), Ink Black (#000000), Fanfold Paper (#F4F0E8 / #EBF3E8).');
Stage.note('- Typography Hierarchy: Impact (Hero Display, tightly tracked), Arial (Data/Labels), Consolas (Line Printer Technical Metadata).');
Stage.note('- Structural Borders: 4px solid black ink rules.');
Stage.note('- Hard Offset Shadows: Vector geometry black rectangles offset by (+6px, +6px), zero blur.');

log('=== STAGE 5: PALETTE & TYPE SYSTEM ===');

const sheet = Css.fromCss(`
  :root {
    --ink: #000000;
    --paper-bg: #F4F0E8;
    --paper-stripe: #EBF3E8;
    --yellow: #FFE500;
    --cyan: #00E5FF;
    --green: #00E676;
    --red: #FF3B30;
    --pink: #FF2A85;
    --orange: #FF6D00;
    --purple: #7C4DFF;
    --white: #FFFFFF;
    --border-width: 4px;
    --shadow-offset: 6px;
  }
  .hero-title {
    font: 900 64px/1.0 Impact, Arial, sans-serif;
    letter-spacing: -0.02em;
    text-transform: uppercase;
    color: var(--ink);
  }
  .section-header {
    font: 700 28px/1.1 Impact, Arial, sans-serif;
    letter-spacing: 0.02em;
    text-transform: uppercase;
    color: var(--ink);
  }
  .badge-title {
    font: 700 20px/1.1 Arial, sans-serif;
    color: var(--ink);
  }
  .terminal-meta {
    font: 700 13px/1.3 Consolas, "Courier New", monospace;
    letter-spacing: 0.05em;
    text-transform: uppercase;
    color: var(--ink);
  }
`);

const tokens = sheet.tokens();
log('Tokens loaded:');
for (const [k, v] of Object.entries(tokens)) {
  log(k + ': ' + v);
}

// Typographic scale calculation
const typoScale = LogoType.calculateTypographicScale(16, 'augmentedFourth', 1, 4);
log('Typographic scale (' + typoScale.ratioName + ' = ' + typoScale.ratioFactor + '):');
table(typoScale.steps.map(s => ({ name: s.name, size: Number(s.size.toFixed(1)), lh: Number(s.lineHeight.toFixed(1)), tracking: Number(s.tracking.toFixed(2)) })));

const pairing = LogoType.evaluateFontPairing('sans', 'sansSerif');
log('Pairing evaluation: ' + pairing.relationship + ' (score: ' + pairing.score + '/100)');

// Render a visual type & palette specimen
const width = 1080;
const height = 1920;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

ctx.fillStyle = tokens['--paper-bg'];
ctx.fillRect(0, 0, width, height);

// Draw green stripes
ctx.fillStyle = tokens['--paper-stripe'];
for (let y = 0; y < height; y += 64) {
  ctx.fillRect(0, y, width, 32);
}

// Swatches demo
const swatches = [
  { name: 'YELLOW', hex: tokens['--yellow'] },
  { name: 'CYAN', hex: tokens['--cyan'] },
  { name: 'GREEN', hex: tokens['--green'] },
  { name: 'RED', hex: tokens['--red'] },
  { name: 'PINK', hex: tokens['--pink'] },
  { name: 'ORANGE', hex: tokens['--orange'] }
];

let sx = 80;
for (const sw of swatches) {
  // Shadow
  ctx.fillStyle = tokens['--ink'];
  ctx.fillRect(sx + 6, 100 + 6, 130, 80);
  
  // Fill
  ctx.fillStyle = sw.hex;
  ctx.fillRect(sx, 100, 130, 80);
  ctx.strokeStyle = tokens['--ink'];
  ctx.lineWidth = 4;
  ctx.strokeRect(sx, 100, 130, 80);
  
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '700 16px Impact, Arial, sans-serif';
  ctx.textAlign = 'center';
  ctx.fillText(sw.name, sx + 65, 145);
  
  sx += 150;
}

// Specimen type samples
ctx.textAlign = 'left';
ctx.font = sheet.rule('.hero-title').font;
ctx.letterSpacing = '-0.02em';
ctx.fillStyle = tokens['--ink'];
ctx.fillText('THE INTERNET WAS NEVER ON SCHEDULE', 80, 260);

ctx.font = '700 24px Arial, sans-serif';
ctx.fillText('Progress is constant — it never stopped coming, but it never once came on schedule.', 80, 310);

ctx.font = sheet.rule('.terminal-meta').font;
ctx.fillText('JOB: #1969-2022 // CONTINUOUS FANFOLD // 53-YEAR RUN // NEO-BRUTALIST', 80, 360);

canvas;
