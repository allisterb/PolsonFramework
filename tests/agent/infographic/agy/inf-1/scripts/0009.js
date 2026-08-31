Stage.begin('Palette & Type');

const cssText = `
:root {
  --bg-paper: #f4f0e6;
  --bg-chassis: #161718;
  --zebra-bar: #e3ece4;
  --ink-black: #121314;
  --neo-red: #ff3e3e;
  --neo-blue: #0047ff;
  --neo-green: #00d26a;
  --neo-yellow: #ffe600;
  --guide-gray: #8c8e94;
  --card-bg: #ffffff;
}

.hero-title {
  font-family: 'Impact';
  font-size: 88px;
  font-weight: 900;
  color: var(--ink-black);
}

.h1 {
  font-family: 'Arial';
  font-size: 40px;
  font-weight: 900;
  color: var(--ink-black);
}

.h2 {
  font-family: 'Courier New';
  font-size: 24px;
  font-weight: 700;
  color: var(--ink-black);
}

.body {
  font-family: 'Consolas';
  font-size: 16px;
  font-weight: 400;
  color: var(--ink-black);
  line-height: 24px;
}

.badge {
  font-family: 'Arial';
  font-size: 14px;
  font-weight: 900;
  color: var(--ink-black);
}
`;

const sheet = Css.fromCss(cssText);
const tokens = sheet.tokens();
log('Parsed tokens: ' + Object.keys(tokens).join(', '));

Stage.note('Palette tokens loaded: Neo-Brutalist Fanfold palette (Chassis, Paper, Zebra, Black Ink, Neo-Red, Neo-Blue, Neo-Green, Neo-Yellow).');
Stage.note('Type hierarchy: Display (Impact 88px), H1 (Arial 900 40px), H2 (Courier New 700 24px), Body (Consolas 16px). Scale ratio 88/16 = 5.5x for titles, and hero 53 numeral at 140px (8.75x ratio).');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

ctx.fillStyle = tokens['--bg-chassis'];
ctx.fillRect(0, 0, 1080, 1920);

// Paper
ctx.fillStyle = tokens['--bg-paper'];
ctx.fillRect(36, 0, 1008, 1920);

// Specimen Sheet Layout for Palette & Type
ctx.fillStyle = tokens['--ink-black'];
ctx.font = '900 48px Impact';
ctx.fillText('STAGE 5: PALETTE & TYPOGRAPHIC SPECIMEN', 70, 80);

ctx.font = '700 18px "Courier New"';
ctx.fillStyle = tokens['--neo-blue'];
ctx.fillText('STYLE GUIDE: NEO-BRUTALIST LINE-PRINTER CONTINUUM', 70, 120);

// Color Chips with Neo-Brutalist hard shadows
const chips = [
    { name: '--bg-paper', hex: tokens['--bg-paper'], desc: 'Fanfold Ledger' },
    { name: '--ink-black', hex: tokens['--ink-black'], desc: 'Carbon Ribbon' },
    { name: '--neo-red', hex: tokens['--neo-red'], desc: 'Alert / 20y Drought' },
    { name: '--neo-blue', hex: tokens['--neo-blue'], desc: 'Hyperlink Blue' },
    { name: '--neo-green', hex: tokens['--neo-green'], desc: 'Terminal CRT' },
    { name: '--neo-yellow', hex: tokens['--neo-yellow'], desc: 'Caution Marquee' },
    { name: '--zebra-bar', hex: tokens['--zebra-bar'], desc: '1401 Greenbar' }
];

let chipX = 70;
let chipY = 160;
for (let i = 0; i < chips.length; i++) {
    const c = chips[i];
    // hard black shadow
    ctx.fillStyle = '#000000';
    ctx.fillRect(chipX + 5, chipY + 5, 120, 90);
    // swatch
    ctx.fillStyle = c.hex;
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 3;
    ctx.fillRect(chipX, chipY, 120, 90);
    ctx.strokeRect(chipX, chipY, 120, 90);

    // text under chip
    ctx.fillStyle = '#000000';
    ctx.font = '700 13px "Courier New"';
    ctx.fillText(c.name, chipX, chipY + 112);
    ctx.font = '400 11px "Consolas"';
    ctx.fillStyle = '#555555';
    ctx.fillText(c.desc, chipX, chipY + 126);

    chipX += 135;
    if (chipX > 900) {
        chipX = 70;
        chipY += 150;
    }
}

// Typography specimen cards
const sampleCards = [
    { role: 'DISPLAY / HERO NUMERAL (140px)', text: '53 YEARS', font: '900 140px Impact', bg: tokens['--neo-yellow'] },
    { role: 'HERO MARQUEE TITLE (64px)', text: 'WEB HISTORY: THE GAPS', font: '900 64px Impact', bg: tokens['--card-bg'] },
    { role: 'SECTION HEADING (32px)', text: '01. THE 20-YEAR INCUBATION', font: '900 32px Arial', bg: tokens['--neo-green'] },
    { role: 'DATA LABELS & TIMESTAMPS (18px)', text: '1969-10-29 22:30 PST // LOG: "LO"', font: '700 18px "Courier New"', bg: tokens['--card-bg'] },
    { role: 'RUNNING BODY & ANNOTATION (16px)', text: 'ARPANET crashed after two letters. Progress arrived relentlessly, but on nobody\'s timeline.', font: '400 16px "Consolas"', bg: tokens['--zebra-bar'] }
];

let typeY = 480;
for (const card of sampleCards) {
    ctx.fillStyle = '#000000';
    ctx.fillRect(76, typeY + 6, 920, 160);

    ctx.fillStyle = card.bg;
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 3;
    ctx.fillRect(70, typeY, 920, 160);
    ctx.strokeRect(70, typeY, 920, 160);

    ctx.fillStyle = '#666666';
    ctx.font = '700 13px "Courier New"';
    ctx.fillText(card.role, 90, typeY + 28);

    ctx.fillStyle = tokens['--ink-black'];
    ctx.font = card.font;
    ctx.fillText(card.text, 90, typeY + 80);

    typeY += 190;
}

// Neo-Brutalist Decorative Stamp
ctx.save();
ctx.translate(820, 1720);
ctx.rotate(-4 * Math.PI / 180);
ctx.fillStyle = '#000000';
ctx.fillRect(6, 6, 180, 80);
ctx.fillStyle = tokens['--neo-red'];
ctx.strokeStyle = '#000000';
ctx.lineWidth = 4;
ctx.fillRect(0, 0, 180, 80);
ctx.strokeRect(0, 0, 180, 80);

ctx.fillStyle = '#ffffff';
ctx.font = '900 24px Impact';
ctx.textAlign = 'center';
ctx.fillText('PASSED', 90, 36);
ctx.font = '700 14px "Courier New"';
ctx.fillText('SPEC 1969-2022', 90, 60);
ctx.restore();

canvas;
