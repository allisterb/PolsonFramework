Stage.begin('Palette & Type');

// Check available typefaces
const fontsToCheck = [
  'Impact', 'Arial Black', 'Courier New', 'Consolas', 'Trebuchet MS',
  'Helvetica', 'Arial', 'Segoe UI', 'Roboto', 'Inter', 'Menlo', 'Monaco'
];

const available = fontsToCheck.filter(f => Skia.Font.has(f));
log('Available typefaces: ' + available.join(', '));
Stage.note('Available typefaces checked: ' + available.join(', '));

// Check font pairing evaluation
const pairEval = LogoType.evaluateFontPairing('sansSerif', 'sansSerif');
log('Pairing eval: ' + pairEval.relationship + ' (score ' + pairEval.score + ')');

// Define Neo-Brutalist Color Palette & Typographic System
const NeoPalette = {
  paper: '#F6F2E9',
  paperDark: '#ECE6D8',
  ink: '#000000',
  yellow: '#FFE600',
  cyan: '#00F0FF',
  coral: '#FF3366',
  green: '#22EE77',
  orange: '#FF7700',
  purple: '#B366FF',
  white: '#FFFFFF',
  shadowOffset: 6,
  borderWidth: 4
};

// Test typographic scale
const typeScale = LogoType.calculateTypographicScale(16, 'goldenRatio', 1, 4);
log('Typographic scale ratio: ' + typeScale.ratioName + ' factor: ' + typeScale.ratioFactor);

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

ctx.fillStyle = NeoPalette.paper;
ctx.fillRect(0, 0, 1080, 1920);

// Helper for Neo-brutalist box with hard offset shadow and structural border
const drawNeoBox = (x, y, w, h, fill, border = NeoPalette.ink, shadow = NeoPalette.ink, offset = 6) => {
  ctx.fillStyle = shadow;
  ctx.fillRect(x + offset, y + offset, w, h);
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  ctx.strokeStyle = border;
  ctx.lineWidth = NeoPalette.borderWidth;
  ctx.strokeRect(x, y, w, h);
};

// Swatches test
const swatches = [
  { name: 'YELLOW #FFE600', fill: NeoPalette.yellow },
  { name: 'CYAN #00F0FF', fill: NeoPalette.cyan },
  { name: 'CORAL #FF3366', fill: NeoPalette.coral },
  { name: 'GREEN #22EE77', fill: NeoPalette.green },
  { name: 'ORANGE #FF7700', fill: NeoPalette.orange },
  { name: 'PURPLE #B366FF', fill: NeoPalette.purple }
];

let sy = 80;
for (const s of swatches) {
  drawNeoBox(80, sy, 400, 70, s.fill);
  ctx.fillStyle = NeoPalette.ink;
  ctx.font = '900 20px "Courier New", monospace';
  ctx.fillText(s.name, 100, sy + 42);
  sy += 95;
}

// Display Typography Test
ctx.save();
const heroSize = 56;
const tracking = LogoType.computeWordmarkTracking(heroSize, true, 'wordmark');
ctx.font = '900 ' + heroSize + 'px "Arial Black", Impact, sans-serif';
ctx.letterSpacing = tracking + 'em';
ctx.fillStyle = NeoPalette.ink;
ctx.fillText('NEO-BRUTALIST TYPE SPECIMEN', 80, 720);
ctx.restore();

canvas;
