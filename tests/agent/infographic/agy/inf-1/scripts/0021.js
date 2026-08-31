Stage.begin('Composition');
Stage.note('Pattern: Editorial spread with continuous vertical tractor-feed armature.');
Stage.note('Place: At a line printer, watching fanfold paper feed through the platen.');
Stage.note('Tension plan: Dense cluster at 1989-2007 (4-6 yr hops); open breathing void at 1969-1989 (20y) and 2007-2022 (15y). Badge brackets break across spine boundary.');

const W = 1080;
const H = 1920;

const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// Base fanfold / greenbar paper ground
ctx.fillStyle = '#f6f3eb'; // warm off-white newsprint / fanfold paper
ctx.fillRect(0, 0, W, H);

// Subdivide into zones using Layout
const canvasRect = Layout.rect(0, 0, W, H);

// Margin for tractor feed strips (left 70px, right 70px)
const tractorWidth = 70;
const [leftTractor, contentArea, rightTractor] = Layout.columns(canvasRect, [tractorWidth, W - 2 * tractorWidth, tractorWidth]);

// Vertical zones: Header banner, Main infographic body, Footer summary
const [headerZone, bodyZone, footerZone] = Layout.rows(contentArea, [240, 1500, 180]);

// Within bodyZone, asymmetric Editorial Spread: Spine / Time rail (32%) vs Event canvas (68%)
const [spineZone, eventZone] = Layout.columns(bodyZone, [30, 70], 24);

log('Header: ' + headerZone.width + 'x' + headerZone.height);
log('Spine: ' + spineZone.x + '..' + spineZone.x2);
log('EventZone: ' + eventZone.x + '..' + eventZone.x2);

// Draw blocking wireframe
ctx.strokeStyle = '#000000';
ctx.lineWidth = 3;

// Draw tractor feed strips
ctx.fillStyle = '#eee9dc';
ctx.fillRect(leftTractor.x, leftTractor.y, leftTractor.width, leftTractor.height);
ctx.fillRect(rightTractor.x, rightTractor.y, rightTractor.width, rightTractor.height);

// Tractor holes
ctx.fillStyle = '#ded8c8';
for (let y = 30; y < H; y += 48) {
  ctx.beginPath();
  ctx.arc(leftTractor.x + tractorWidth / 2, y, 10, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();

  ctx.beginPath();
  ctx.arc(rightTractor.x + tractorWidth / 2, y, 10, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

// Draw boundary lines
ctx.strokeRect(headerZone.x, headerZone.y, headerZone.width, headerZone.height);
ctx.strokeRect(spineZone.x, spineZone.y, spineZone.width, spineZone.height);
ctx.strokeRect(eventZone.x, eventZone.y, eventZone.width, eventZone.height);
ctx.strokeRect(footerZone.x, footerZone.y, footerZone.width, footerZone.height);

// Header placeholder
ctx.fillStyle = '#000000';
ctx.font = '900 48px monospace';
ctx.fillText('COMPOSITION BLOCKING', headerZone.x + 20, headerZone.y + 60);

canvas;
