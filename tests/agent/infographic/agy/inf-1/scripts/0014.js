Stage.begin('Composition');

Stage.note('Named Composition Pattern: Overlap Stack on Fanfold Feed.');
Stage.note('Reader Position: Standing at a line printer, watching fanfold continuous paper feed through the platen.');
Stage.note('Tension Rules:');
Stage.note('1. Dense vs Empty: 1993-2007 cluster packs 4 major milestones tightly; 1969-1989 and 2007-2022 voids breathe with large gap markers.');
Stage.note('2. Scale ladder: 96px hero numerals, 36px section headers, 24px milestone titles, 14-16px detail labels (>6x ratio).');
Stage.note('3. Boundary breaking: Badges and milestone cards overlap the continuous timeline spine and cross column boundaries.');
Stage.note('4. Non-uniform ground: Green-bar fanfold paper with tractor-feed perforations, horizontal fold lines, and dot-matrix grain.');
Stage.note('5. Single rotated system: Stamped alert/milestone highlight badges rotated by -2.5 deg.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Green background base
const bgBase = '#10b981'; // Vibrant neo-brutalist emerald green base
ctx.fillStyle = '#eafaf1'; // Pale green-bar paper background
ctx.fillRect(0, 0, 1080, 1920);

// Draw green-bar fanfold alternating bands
ctx.fillStyle = '#d4f4e2';
for (let y = 0; y < 1920; y += 72) {
  if (Math.floor(y / 72) % 2 === 0) {
    ctx.fillRect(0, y, 1080, 72);
  }
}

// Tractor feed strips on left and right margins
const tractW = 60;
ctx.fillStyle = '#000000';
ctx.fillRect(tractW, 0, 3, 1920);
ctx.fillRect(1080 - tractW, 0, 3, 1920);

// Tractor pin feed holes
for (let y = 36; y < 1920; y += 48) {
  ctx.fillStyle = '#0f172a';
  ctx.beginPath();
  ctx.arc(30, y, 9, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(1080 - 30, y, 9, 0, Math.PI * 2);
  ctx.fill();
}

// Header Zone
const headerRect = Layout.rect(80, 50, 920, 200);
// Drop shadow
ctx.fillStyle = '#000000';
ctx.fillRect(headerRect.x + 8, headerRect.y + 8, headerRect.width, headerRect.height);
// Card fill
ctx.fillStyle = '#00f076';
ctx.fillRect(headerRect.x, headerRect.y, headerRect.width, headerRect.height);
ctx.strokeStyle = '#000000';
ctx.lineWidth = 4;
ctx.strokeRect(headerRect.x, headerRect.y, headerRect.width, headerRect.height);

ctx.fillStyle = '#000000';
ctx.font = '900 44px "Consolas", monospace';
ctx.fillText('WEB-HISTORY // LINE FEED', headerRect.x + 30, headerRect.y + 60);

ctx.font = '700 22px "Consolas", monospace';
ctx.fillText('THE INTERNET’S GREATEST MILESTONES (1969 — 2022)', headerRect.x + 30, headerRect.y + 110);

ctx.font = '400 18px "Consolas", monospace';
ctx.fillText('CLAIM: Progress is constant — it never came on schedule.', headerRect.x + 30, headerRect.y + 155);

// Timeline spine zone
const spineX = 260;
ctx.strokeStyle = '#000000';
ctx.lineWidth = 8;
ctx.beginPath();
ctx.moveTo(spineX, 290);
ctx.lineTo(spineX, 1720);
ctx.stroke();

// Footer Zone
const footerRect = Layout.rect(80, 1750, 920, 120);
ctx.fillStyle = '#000000';
ctx.fillRect(footerRect.x + 6, footerRect.y + 6, footerRect.width, footerRect.height);
ctx.fillStyle = '#ffffff';
ctx.fillRect(footerRect.x, footerRect.y, footerRect.width, footerRect.height);
ctx.strokeRect(footerRect.x, footerRect.y, footerRect.width, footerRect.height);

ctx.fillStyle = '#000000';
ctx.font = '700 20px "Consolas", monospace';
ctx.fillText('SUMMARY METRICS: 53-YEAR TOTAL SPAN · 6 GAPS (3Y MIN, 20Y MAX)', footerRect.x + 25, footerRect.y + 50);
ctx.fillText('STATUS: FANFOLD PAPER CONTINUOUS FEED · LINE PRINTER SPOOL', footerRect.x + 25, footerRect.y + 85);

canvas;
