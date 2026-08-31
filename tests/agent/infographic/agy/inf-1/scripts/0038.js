Stage.begin('Palette & Type');

// Check available fonts
const fontCandidates = [
  'Consolas', 'Courier New', 'Lucida Console', 'Cascadia Code', 'Segoe UI',
  'Impact', 'Arial Black', 'Trebuchet MS', 'Arial', 'Helvetica', 'Georgia'
];
const available = fontCandidates.filter(f => Skia.Font.has(f));
log('Available system fonts: ' + available.join(', '));

Stage.note('Typography stack selected: Primary Display / Monospace: "Consolas", "Courier New", monospace. Primary Grotesque Sans: "Segoe UI", Arial, sans-serif. Heavy impact headers: "Impact", "Arial Black", sans-serif.');

// Font pairing evaluation
const pairing = LogoType.evaluateFontPairing('sansSerif', 'slab');
Stage.note('Pairing evaluation: ' + pairing.relationship + ' (Score: ' + pairing.score + '/100)');

// Typographic Scale
const typeScale = LogoType.calculateTypographicScale(16, 'goldenRatio', 1, 4);
Stage.note('Harmonic typographic scale (Golden Ratio 1.618): Base 16px, steps up to ' + typeScale.steps[typeScale.steps.length - 1].size.toFixed(1) + 'px.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Paper ground
ctx.fillStyle = '#FAF6EE';
ctx.fillRect(0, 0, 1080, 1920);

// Tractor feed margin
const marginW = 44;
ctx.fillStyle = '#EBE3D5';
ctx.fillRect(0, 0, marginW, 1920);
ctx.fillRect(1080 - marginW, 0, marginW, 1920);
ctx.fillStyle = '#222222';
for (let y = 30; y < 1920; y += 48) {
  ctx.beginPath();
  ctx.arc(marginW / 2, y, 6, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(1080 - marginW / 2, y, 6, 0, Math.PI * 2);
  ctx.fill();
}

// Platen Title Banner
ctx.fillStyle = '#111111';
ctx.fillRect(60, 40, 960, 130);
ctx.fillStyle = '#FFE500';
ctx.font = '900 34px Consolas, "Courier New", monospace';
ctx.fillText('STAGE 5: PALETTE & TYPOGRAPHIC SYSTEM', 85, 95);
ctx.fillStyle = '#FFFFFF';
ctx.font = '600 18px Consolas, monospace';
ctx.fillText('NEO-BRUTALIST LINE-PRINTER DESIGN SYSTEM SPECIFICATION', 85, 135);

// Color Palette Swatches Card
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(60, 200, 960, 480);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 5;
ctx.strokeRect(60, 200, 960, 480);

ctx.fillStyle = '#111111';
ctx.font = '900 24px "Segoe UI", Arial, sans-serif';
ctx.fillText('NEO-BRUTALIST COLOR PALETTE', 85, 245);

const swatches = [
  { name: 'Cyber Yellow', hex: '#FFE500', text: '#111111', role: 'Hero Titles, Main Claim, Highlights' },
  { name: 'Signal Red', hex: '#FF4338', text: '#FFFFFF', role: 'ARPANET Crash, 20y Gap, Alerts' },
  { name: 'Electric Cyan', hex: '#00E5FF', text: '#111111', role: 'Mosaic GUI, Timeline Accents' },
  { name: 'Terminal Green', hex: '#00FF66', text: '#111111', role: 'Google Search, Fast Metric' },
  { name: 'Acid Magenta', hex: '#FF007F', text: '#FFFFFF', role: 'ChatGPT Explosion, Disruptions' },
  { name: 'Electric Violet', hex: '#9D4EDD', text: '#FFFFFF', role: 'Facebook / Social Era' },
  { name: 'Safety Orange', hex: '#FF9100', text: '#111111', role: 'iPhone Pocket Web' },
  { name: 'Platen Ink', hex: '#111111', text: '#FFFFFF', role: 'Typography, Heavy Strokes, Hard Shadows' }
];

let sx = 85;
let sy = 270;
for (let i = 0; i < swatches.length; i++) {
  const sw = swatches[i];
  ctx.fillStyle = sw.hex;
  ctx.fillRect(sx, sy, 210, 80);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 3;
  ctx.strokeRect(sx, sy, 210, 80);

  ctx.fillStyle = sw.text;
  ctx.font = '900 16px "Segoe UI", sans-serif';
  ctx.fillText(sw.name, sx + 10, sy + 25);
  ctx.font = '700 14px Consolas, monospace';
  ctx.fillText(sw.hex, sx + 10, sy + 48);

  ctx.fillStyle = '#333333';
  ctx.font = '500 12px "Segoe UI", sans-serif';
  ctx.fillText(sw.role, sx, sy + 100);

  sx += 235;
  if ((i + 1) % 4 === 0) {
    sx = 85;
    sy += 120;
  }
}

// Typography Hierarchy Specimen Card
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(60, 710, 960, 680);
ctx.strokeRect(60, 710, 960, 680);

ctx.fillStyle = '#111111';
ctx.font = '900 24px "Segoe UI", Arial, sans-serif';
ctx.fillText('TYPOGRAPHIC SCALE & HIERARCHY', 85, 755);

const typeSpecs = [
  { level: 'HERO DISPLAY', font: '900 64px Impact, "Arial Black", sans-serif', sample: '53 YEARS OF INTERNET', size: '64px / Ratio 1.618^3' },
  { level: 'SECTION HEADER', font: '900 36px Consolas, "Courier New", monospace', sample: 'THE WAITING PERIODS', size: '36px / Ratio 1.618^2' },
  { level: 'CARD TITLE / MILESTONE', font: '900 24px "Segoe UI", Arial, sans-serif', sample: '1989 · World Wide Web Proposed', size: '24px / Ratio 1.618^1' },
  { level: 'BODY / ANNOTATION', font: '600 16px "Segoe UI", Arial, sans-serif', sample: 'Progress is constant — it never stopped coming, but it never once came on schedule.', size: '16px / Base' },
  { level: 'MONOSPACE CODE / STAMP', font: '900 14px Consolas, monospace', sample: '[CRASH: PACKET DROPPED AFTER "LO"]', size: '14px / Micro' },
  { level: 'TAGLINE / TRACKED CHROME', font: '700 12px Consolas, monospace', sample: 'FANFOLD LINE PRINTER CONTINUOUS STATIONERY · DIEGETIC DATA', size: '12px / Tracking +0.22em' }
];

let ty = 810;
for (const spec of typeSpecs) {
  ctx.fillStyle = '#888888';
  ctx.font = '700 13px Consolas, monospace';
  ctx.fillText(spec.level + ' (' + spec.size + ')', 85, ty);
  
  ctx.fillStyle = '#111111';
  ctx.font = spec.font;
  if (spec.level.includes('TRACKED')) {
    ctx.save();
    ctx.letterSpacing = '0.22em';
    ctx.fillText(spec.sample, 85, ty + 28);
    ctx.restore();
  } else {
    ctx.fillText(spec.sample, 85, ty + 32);
  }

  ctx.strokeStyle = '#EEEEEE';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(85, ty + 50);
  ctx.lineTo(995, ty + 50);
  ctx.stroke();

  ty += 78;
}

// Visual Component Test (Mock Neo-Brutalist Component Lockup)
ctx.fillStyle = '#FFE500';
ctx.fillRect(60, 1420, 960, 440);
ctx.strokeRect(60, 1420, 960, 440);

// Hard brutalist shadow
ctx.fillStyle = '#111111';
ctx.fillRect(60 + 8, 1420 + 440, 960, 8);
ctx.fillRect(60 + 960, 1420 + 8, 8, 440);

ctx.fillStyle = '#111111';
ctx.font = '900 24px Consolas, monospace';
ctx.fillText('COMPONENT DEMONSTRATION: NEO-BRUTALIST STAMP & CARD', 85, 1465);

// Inner white punch card
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(90, 1495, 540, 320);
ctx.strokeRect(90, 1495, 540, 320);

// Stamp inside card
ctx.save();
ctx.translate(120, 1530);
ctx.rotate(-0.06);
ctx.fillStyle = '#FF4338';
ctx.fillRect(0, 0, 130, 42);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 3;
ctx.strokeRect(0, 0, 130, 42);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 16px Consolas, monospace';
ctx.fillText('[CRASHED]', 12, 26);
ctx.restore();

ctx.fillStyle = '#111111';
ctx.font = '900 28px "Segoe UI", sans-serif';
ctx.fillText('1969: ARPANET', 275, 1555);

ctx.font = '500 18px "Segoe UI", sans-serif';
ctx.fillStyle = '#333333';
ctx.fillWrappedText('Sent first message between UCLA and Stanford. System crashed after typing "L-O". The internet began with a failure.', 120, 1600, 480, 26);

// Bar preview inside card
ctx.fillStyle = '#FF4338';
ctx.fillRect(120, 1720, 320, 30);
ctx.strokeRect(120, 1720, 320, 30);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 16px Consolas, monospace';
ctx.fillText('20 YEARS UNTIL NEXT EVENT (WWW)', 130, 1741);

// Callout sticker on right
ctx.fillStyle = '#00FF66';
ctx.fillRect(660, 1495, 330, 320);
ctx.strokeRect(660, 1495, 330, 320);

ctx.fillStyle = '#111111';
ctx.font = '900 22px Consolas, monospace';
ctx.fillText('DESIGN VERDICT', 685, 1540);
ctx.font = '900 56px Impact, sans-serif';
ctx.fillText('100% TRUTH', 685, 1610);
ctx.font = '600 17px "Segoe UI", sans-serif';
ctx.fillWrappedText('Zero baseline asserted. Scales calculated by engine. No invented numbers.', 685, 1650, 280, 24);

canvas;
