Stage.begin('Composition');

Stage.note('Named Pattern: Editorial Spread with Overlap Stack (Manual 13 §4).');
Stage.note('Place: At a line printer, watching fanfold paper feed through the platen.');
Stage.note('Tension Rules (Manual 13 §5):');
Stage.note('- One dense zone (1989-2007 cluster of 5 milestones in 18 years) vs one empty zone (1969-1989 20-year lull and 2007-2022 15-year lull).');
Stage.note('- Three sizes minimum: 64px display header down to 11px printer metadata (scale ratio >8x).');
Stage.note('- Something crosses a boundary: Rotated neo-brutalist status stamp and connector brackets break container borders.');
Stage.note('- Ground participates: Authentic green-bar fanfold paper with tractor feed sprocket margins and registration marks.');
Stage.note('- One committed rotation: -4deg stamp for visual punch.');

log('=== STAGE 3: COMPOSITION BLOCKING ===');

const width = 1080;
const height = 1920;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

// 1. Background Ground - Fanfold paper
ctx.fillStyle = '#F5F2EB';
ctx.fillRect(0, 0, width, height);

// Draw alternating green-bar paper stripes (each 32px tall)
ctx.fillStyle = '#ECF3E8';
for (let y = 0; y < height; y += 64) {
  ctx.fillRect(0, y, width, 32);
}

// 2. Tractor feed sprocket margins (Left and Right 54px)
const marginW = 54;
ctx.fillStyle = '#EBE6DC';
ctx.fillRect(0, 0, marginW, height);
ctx.fillRect(width - marginW, 0, marginW, height);

// Marginal rules
ctx.strokeStyle = '#000000';
ctx.lineWidth = 3;
ctx.beginPath();
ctx.moveTo(marginW, 0);
ctx.lineTo(marginW, height);
ctx.moveTo(width - marginW, 0);
ctx.lineTo(width - marginW, height);
ctx.stroke();

// Punch holes in tractor margins
for (let y = 24; y < height; y += 48) {
  // Left hole
  ctx.fillStyle = '#D8D1C3';
  ctx.beginPath();
  ctx.arc(marginW / 2, y, 10, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
  
  // Right hole
  ctx.beginPath();
  ctx.arc(width - marginW / 2, y, 10, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

// 3. Layout Zones using Layout.*
const contentRect = Layout.rect(marginW + 24, 24, width - (marginW * 2) - 48, height - 48);

// Divide into Header, Main Body, Bottom Macro, Footer
const [headerRect, midRect, bottomRect] = Layout.rows(contentRect, [13, 62, 25], 24);

log('Header Zone: y=' + headerRect.y.toFixed(0) + ', h=' + headerRect.height.toFixed(0));
log('Mid Zone: y=' + midRect.y.toFixed(0) + ', h=' + midRect.height.toFixed(0));
log('Bottom Zone: y=' + bottomRect.y.toFixed(0) + ', h=' + bottomRect.height.toFixed(0));

// Draw blocking rectangles with brutalist border
const drawBlock = (r, fill, title) => {
  ctx.fillStyle = fill;
  ctx.fillRect(r.x, r.y, r.width, r.height);
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 4;
  ctx.strokeRect(r.x, r.y, r.width, r.height);
  
  ctx.fillStyle = '#000000';
  ctx.font = '700 20px Consolas, monospace';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';
  ctx.fillText('[BLOCK: ' + title + ']', r.x + 12, r.y + 12);
};

drawBlock(headerRect, '#FFE500', 'HEADER & CLAIM');

// Split midRect into Left (Timeline Spine 58%) and Right (Gap Analysis 42%)
const [timelineRect, sideRect] = Layout.columns(midRect, [58, 42], 24);
drawBlock(timelineRect, '#FFFFFF', 'TIMELINE SPINE (1969-2022)');
drawBlock(sideRect, '#00E5FF', 'GAP COMPARISON & BURST STATS');

drawBlock(bottomRect, '#FFFFFF', '53-YEAR MACRO BREAKDOWN & WAFFLE');

canvas;
