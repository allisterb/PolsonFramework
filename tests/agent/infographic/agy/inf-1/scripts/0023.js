Stage.begin('Composition');
Stage.note('Building layout skeleton. Editorial Spread pattern: left=timeline column (40%), right=panels (60%). Fanfold ground: tractor-feed holes on L and R margins.');

// Canvas: 1080x1920 story format
const W = 1080, H = 1920;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// --- PALETTE (neo-brutalist: saturated flats, ink, paper white) ---
const INK    = '#0a0a0a';
const PAPER  = '#f5f0e8'; // aged paper, not flat white
const YELLOW = '#f7c31a'; // hot yellow
const PINK   = '#e8175d'; // hot pink
const CYAN   = '#00c4cc'; // electric cyan
const GREEN  = '#22c55e'; // terminal green
const PURPLE = '#a855f7';
const ORANGE = '#f97316';

// --- GROUND: fanfold paper ---
// Paper fill
ctx.fillStyle = PAPER;
ctx.fillRect(0, 0, W, H);

// Tractor feed holes: left strip and right strip, 38px wide each
const HOLE_STRIP = 38;
const HOLE_R = 10; // circle radius
const HOLE_SPACING = 60; // center-to-center

// Left strip
ctx.fillStyle = '#e0dac8';
ctx.fillRect(0, 0, HOLE_STRIP, H);
// Right strip
ctx.fillRect(W - HOLE_STRIP, 0, HOLE_STRIP, H);

// Left strip border
ctx.strokeStyle = INK;
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(HOLE_STRIP, 0);
ctx.lineTo(HOLE_STRIP, H);
ctx.stroke();
ctx.beginPath();
ctx.moveTo(W - HOLE_STRIP, 0);
ctx.lineTo(W - HOLE_STRIP, H);
ctx.stroke();

// Draw holes
const drawHoles = (x) => {
  for (let y = 30; y < H; y += HOLE_SPACING) {
    ctx.fillStyle = PAPER;
    ctx.strokeStyle = INK;
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.arc(x, y, HOLE_R, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  }
};
drawHoles(HOLE_STRIP / 2);
drawHoles(W - HOLE_STRIP / 2);

// --- MAIN CONTENT AREA (inside the tractor strip borders) ---
const page = Layout.inset(Layout.rect(HOLE_STRIP + 1, 0, W - (HOLE_STRIP + 1) * 2, H), 12, 12);

// --- EDITORIAL SPLIT: left 40% = timeline, right 60% = panels ---
// Use weights [40, 60] with a gap
const [leftCol, rightCol] = Layout.columns(page, [40, 60], 16);

// --- HEADER: spans full width, top 80px ---
const headerH = 90;
const [header, body] = Layout.rows(page, [headerH, page.height - headerH], 0);
const [leftBody, rightBody] = Layout.columns(body, [40, 60], 16);

// Draw header background
ctx.fillStyle = INK;
ctx.fillRect(header.x, header.y, header.width, header.height);

// Header text
ctx.fillStyle = YELLOW;
ctx.font = 'black 42px Impact, Arial';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.letterSpacing = LogoType.computeWordmarkTracking(42, true) + 'em';
ctx.fillText('THE INTERNET WAS NEVER ON SCHEDULE', header.x + 16, header.y + header.height / 2);
ctx.letterSpacing = '0px';

// --- OUTLINE THE ZONES (skeleton) ---
// Left body (timeline zone)
ctx.strokeStyle = PINK;
ctx.lineWidth = 3;
ctx.setLineDash([8, 4]);
ctx.strokeRect(leftBody.x, leftBody.y, leftBody.width, leftBody.height);

// Right body (panels zone)
ctx.strokeStyle = CYAN;
ctx.strokeRect(rightBody.x, rightBody.y, rightBody.width, rightBody.height);
ctx.setLineDash([]);

// Labels for zones
ctx.fillStyle = PINK;
ctx.font = 'bold 14px "Courier New", monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'top';
ctx.fillText('LEFT: TRUE-SCALE TIMELINE', leftBody.x + leftBody.width/2, leftBody.y + 10);
ctx.fillStyle = CYAN;
ctx.fillText('RIGHT: PANELS', rightBody.x + rightBody.width/2, rightBody.y + 10);

// Right panel zones: 4 panels stacked
// [bignum, gapbars, erabar, footer]
const [rTop, rGapBar, rEra, rFooter] = Layout.rows(rightBody, [22, 35, 18, 25], 10);

// Draw panel outlines
const panelColors = [YELLOW, GREEN, ORANGE, PURPLE];
const panels = [rTop, rGapBar, rEra, rFooter];
const panelLabels = ['BIG-NUMBER CALLOUT', 'GAP BAR CHART', 'ERA SEGMENTED BAR', 'CLAIM FOOTER'];
for (let i = 0; i < panels.length; i++) {
  ctx.strokeStyle = panelColors[i];
  ctx.lineWidth = 2;
  ctx.setLineDash([6, 3]);
  ctx.strokeRect(panels[i].x, panels[i].y, panels[i].width, panels[i].height);
  ctx.fillStyle = panelColors[i];
  ctx.font = 'bold 11px "Courier New", monospace';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';
  ctx.fillText(panelLabels[i], panels[i].x + 6, panels[i].y + 6);
}
ctx.setLineDash([]);

log('W=' + W + ' H=' + H);
log('header: x=' + Math.round(header.x) + ' y=' + Math.round(header.y) + ' w=' + Math.round(header.width) + ' h=' + Math.round(header.height));
log('leftBody: x=' + Math.round(leftBody.x) + ' y=' + Math.round(leftBody.y) + ' w=' + Math.round(leftBody.width) + ' h=' + Math.round(leftBody.height));
log('rightBody: x=' + Math.round(rightBody.x) + ' y=' + Math.round(rightBody.y) + ' w=' + Math.round(rightBody.width) + ' h=' + Math.round(rightBody.height));
log('rTop panel: ' + Math.round(rTop.height) + 'px');
log('rGapBar panel: ' + Math.round(rGapBar.height) + 'px');
log('rEra panel: ' + Math.round(rEra.height) + 'px');
log('rFooter panel: ' + Math.round(rFooter.height) + 'px');

canvas;
