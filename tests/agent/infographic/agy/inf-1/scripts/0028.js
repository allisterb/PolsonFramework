Stage.begin('Composition');

Stage.note('Named Composition Pattern: Editorial Spread (Manual 13 §4) with Line Printer Fanfold Armature.');
Stage.note('Armature: Asymmetric split with 280px left spine (calibrated tractor feed & timeline ruler) and 720px right column (milestone data cards & big typography).');
Stage.note('Tension Rules (Manual 13 §5):');
Stage.note('  - Dense vs Empty: Dense 1989-2007 burst vs spacious 1969-1989 (20y) & 2007-2022 (15y) lull zones.');
Stage.note('  - 3 Sizes: 84px display title / big numerals down to 14px monospace terminal details (>6x to 8x scale).');
Stage.note('  - Boundary crossing: Badge callouts and timeline connectors cross across the gutter between columns.');
Stage.note('  - Ground: Fanfold paper tone with perforation holes, tractor margins, and subtle printer rule bands.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Neo-brutalist palette & ground
const bgPaper = '#f4f0e6';
ctx.fillStyle = bgPaper;
ctx.fillRect(0, 0, 1080, 1920);

// Tractor feed margins (Left and Right)
const leftMargin = 70;
const rightMargin = 1080 - 70;

// Draw sprocket holes
ctx.fillStyle = '#111111';
for (let y = 40; y < 1920; y += 60) {
  ctx.beginPath();
  ctx.arc(35, y, 12, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(1080 - 35, y, 12, 0, Math.PI * 2);
  ctx.fill();
}

// Inner printable canvas
const printable = Layout.rect(70, 40, 1080 - 140, 1920 - 80);

// Layout zones: Header (260px), Timeline Body (1360px), Footer Summary (220px)
const [headerZone, bodyZone, footerZone] = Layout.rows(printable, [240, 1380, 220], 20);

// Header blocking
ctx.strokeStyle = '#111111';
ctx.lineWidth = 4;
ctx.strokeRect(headerZone.x, headerZone.y, headerZone.width, headerZone.height);

// Body split: Left ruler spine (260px) + Right cards (640px)
const [spineZone, cardsZone] = Layout.columns(bodyZone, [280, 640], 20);

ctx.fillStyle = 'rgba(0,0,0,0.04)';
ctx.fillRect(spineZone.x, spineZone.y, spineZone.width, spineZone.height);
ctx.strokeRect(spineZone.x, spineZone.y, spineZone.width, spineZone.height);
ctx.strokeRect(cardsZone.x, cardsZone.y, cardsZone.width, cardsZone.height);
ctx.strokeRect(footerZone.x, footerZone.y, footerZone.width, footerZone.height);

ctx.fillStyle = '#111111';
ctx.font = '700 24px Consolas, monospace';
ctx.fillText('STAGE 3: COMPOSITION & ZONING BLOCKING', headerZone.x + 20, headerZone.y + 40);

canvas;
