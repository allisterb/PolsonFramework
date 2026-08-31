Stage.begin('Palette & Type');

Stage.note('Design Language: Neo-Brutalist Line-Printer / Fanfold Terminal.');

const css = `
:root {
  --bg-paper: #f4efe4;
  --bg-paper-alt: #ece5d4;
  --ink: #0d0d0d;
  --ink-muted: #555555;
  --accent-yellow: #ffe600;
  --accent-cyan: #00f0ff;
  --accent-coral: #ff4742;
  --accent-green: #00e676;
  --accent-purple: #b388ff;
  --card-bg: #ffffff;
  --border-thick: 4px;
  --font-display: Impact, Arial, sans-serif;
  --font-mono: Consolas, 'Courier New', monospace;
  --font-body: Arial, Helvetica, sans-serif;
}
.masthead-title {
  font: 900 64px var(--font-display);
  letter-spacing: -0.02em;
  color: var(--ink);
  text-transform: uppercase;
}
.kicker {
  font: 700 15px var(--font-mono);
  letter-spacing: 0.15em;
  color: var(--ink);
  text-transform: uppercase;
}
.card-year {
  font: 900 32px var(--font-display);
  color: var(--ink);
}
.card-title {
  font: 800 22px var(--font-body);
  color: var(--ink);
  text-transform: uppercase;
}
.card-body {
  font: 600 15px var(--font-mono);
  line-height: 20px;
  color: var(--ink);
}
.badge-mono {
  font: 700 13px var(--font-mono);
  letter-spacing: 0.08em;
  color: var(--ink);
  text-transform: uppercase;
}
`;

const sheet = Css.fromCss(css);
const tokens = sheet.tokens();
Stage.note('CSS tokens loaded: ' + Object.keys(tokens).length + ' variables.');

// Verify font pairing
const pairing = LogoType.evaluateFontPairing('sans', 'slab');
Stage.note('Typographic evaluation: ' + pairing.relationship + ' (' + pairing.score + '/100)');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

ctx.fillStyle = tokens['--bg-paper'];
ctx.fillRect(0, 0, 1080, 1920);

// Specimen test
const drawBrutalistBox = (x, y, w, h, fill, shadowOffset = 6) => {
  ctx.fillStyle = tokens['--ink'];
  ctx.fillRect(x + shadowOffset, y + shadowOffset, w, h);
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  ctx.strokeStyle = tokens['--ink'];
  ctx.lineWidth = 4;
  ctx.strokeRect(x, y, w, h);
};

// Header specimen
drawBrutalistBox(70, 50, 940, 180, tokens['--accent-yellow'], 8);
ctx.fillStyle = tokens['--ink'];
ctx.font = sheet.rule('.kicker').font;
ctx.letterSpacing = '0.15em';
ctx.fillText('/// TELETYPE FANFOLD LOG // SYSTEM.DATA.CHRONO ///', 100, 90);

ctx.letterSpacing = '0px';
ctx.font = '900 62px ' + tokens['--font-display'];
ctx.fillText("WEB MILESTONES: UNPLANNED", 100, 160);

ctx.font = '700 18px ' + tokens['--font-mono'];
ctx.fillText("PROGRESS NEVER STOPPED COMING. IT NEVER CAME ON SCHEDULE.", 100, 200);

// Card specimen
drawBrutalistBox(70, 270, 940, 140, tokens['--card-bg'], 6);
drawBrutalistBox(90, 290, 120, 50, tokens['--accent-coral'], 0);
ctx.fillStyle = '#ffffff';
ctx.font = '900 32px ' + tokens['--font-display'];
ctx.textAlign = 'center';
ctx.fillText('1969', 150, 326);

ctx.textAlign = 'left';
ctx.fillStyle = tokens['--ink'];
ctx.font = sheet.rule('.card-title').font;
ctx.fillText('ARPANET TRANSMITS FIRST PACKET', 230, 318);

ctx.font = sheet.rule('.card-body').font;
ctx.fillText('Sends its first message (it crashed after typing "LO").', 230, 348);
ctx.fillText('-> DELTA WAIT TIME: BASELINE ZERO (ORIGIN OF PACKET SWITCHING)', 230, 372);

// Badge specimen
drawBrutalistBox(70, 440, 450, 90, tokens['--accent-cyan'], 6);
ctx.fillStyle = tokens['--ink'];
ctx.font = '900 40px ' + tokens['--font-display'];
ctx.fillText('53 YEARS', 90, 490);
ctx.font = '700 14px ' + tokens['--font-mono'];
ctx.fillText('TOTAL HISTORICAL TIMELINE SPAN', 90, 515);

drawBrutalistBox(560, 440, 450, 90, tokens['--accent-green'], 6);
ctx.fillStyle = tokens['--ink'];
ctx.font = '900 40px ' + tokens['--font-display'];
ctx.fillText('20 YR GAP', 580, 490);
ctx.font = '700 14px ' + tokens['--font-mono'];
ctx.fillText('MAX WAIT: ARPANET -> WORLD WIDE WEB', 580, 515);

canvas;
