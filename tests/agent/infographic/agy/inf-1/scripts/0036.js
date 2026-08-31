Stage.begin('Composition');

Stage.note('NAMED PATTERN: Editorial Spread with Overlap Stack accents. Asymmetric split (62% left timeline column, 38% right analytical column) framed as continuous fanfold computer paper rolling through a line printer.');
Stage.note('TENSION RULES APPLIED:');
Stage.note('1. Dense vs Empty: Dense cluster during the dot-com / social explosion (1993-2007) contrasted with the open 20-year desert (1969-1989) breathing room (>15% canvas).');
Stage.note('2. Scale Ladder: Hero headline at 72px / 53y hero stat at 88px vs 14px body text (>6x-8x size jump).');
Stage.note('3. Boundary Crossing: Pinned timeline badges and platen feed perforation lines break across the column dividers and extend to the edge.');
Stage.note('4. Non-uniform Ground: Continuous tractor-feed perforation margins with feed holes, line-printer subtle horizontal ruling bands (green-bar alternate lines), and paper tone.');
Stage.note('5. Single Rotation System: Neo-brutalist alert stamps ("[CRASHED]", "[BOOM]") rotated at -6deg, with all structural boxes rectilinear.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Paper tone background
ctx.fillStyle = '#F4EFEA';
ctx.fillRect(0, 0, 1080, 1920);

// Tractor-feed pinhole strips on left and right margins
const marginW = 44;
ctx.fillStyle = '#EAE3D9';
ctx.fillRect(0, 0, marginW, 1920);
ctx.fillRect(1080 - marginW, 0, marginW, 1920);

// Draw tractor feed holes
ctx.fillStyle = '#222222';
for (let y = 30; y < 1920; y += 48) {
  ctx.beginPath();
  ctx.arc(marginW / 2, y, 7, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(1080 - marginW / 2, y, 7, 0, Math.PI * 2);
  ctx.fill();
}

// Inset page area
const printable = Layout.rect(marginW + 16, 30, 1080 - (marginW + 16) * 2, 1860);

// Vertical zones: Header, Main Body (Split 60/40), Footer
const [headerZone, mainZone, footerZone] = Layout.rows(printable, [180, 1420, 260], 24);

// Header wireframe
ctx.fillStyle = '#111111';
ctx.fillRect(headerZone.x, headerZone.y, headerZone.width, headerZone.height);
ctx.fillStyle = '#FFE600';
ctx.font = '900 42px monospace';
ctx.fillText('HEADER ZONE: HERO TITLE & CLAIM', headerZone.x + 24, headerZone.y + 60);
ctx.fillStyle = '#FFFFFF';
ctx.font = '500 22px monospace';
ctx.fillText('Platen feed banner + 53-Year timeline scope', headerZone.x + 24, headerZone.y + 110);

// Split mainZone into Left (Timeline Spine 62%) and Right (Comparative Analytics 38%)
const [timelineCol, analyticsCol] = Layout.columns(mainZone, [62, 38], 24);

// Left Timeline Column
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 4;
ctx.strokeRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);

ctx.fillStyle = '#00E5FF';
ctx.fillRect(timelineCol.x, timelineCol.y, timelineCol.width, 50);
ctx.fillStyle = '#111111';
ctx.font = '900 22px monospace';
ctx.fillText('ZONE A: CONTINUOUS TIMELINE SPINE (62%)', timelineCol.x + 16, timelineCol.y + 34);

// Right Analytics Column split into 2 cards: Gap Bars & Big Stats
const [gapCard, statCard] = Layout.rows(analyticsCol, [65, 35], 20);

// Gap Card
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(gapCard.x, gapCard.y, gapCard.width, gapCard.height);
ctx.strokeRect(gapCard.x, gapCard.y, gapCard.width, gapCard.height);
ctx.fillStyle = '#FF5500';
ctx.fillRect(gapCard.x, gapCard.y, gapCard.width, 50);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 20px monospace';
ctx.fillText('ZONE B: GAP BARS (ZERO BASE)', gapCard.x + 16, gapCard.y + 34);

// Stat Card
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(statCard.x, statCard.y, statCard.width, statCard.height);
ctx.strokeRect(statCard.x, statCard.y, statCard.width, statCard.height);
ctx.fillStyle = '#00FF66';
ctx.fillRect(statCard.x, statCard.y, statCard.width, 50);
ctx.fillStyle = '#111111';
ctx.font = '900 20px monospace';
ctx.fillText('ZONE C: VELOCITY & METRICS', statCard.x + 16, statCard.y + 34);

// Footer Zone
ctx.fillStyle = '#FFE600';
ctx.fillRect(footerZone.x, footerZone.y, footerZone.width, footerZone.height);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 4;
ctx.strokeRect(footerZone.x, footerZone.y, footerZone.width, footerZone.height);

ctx.fillStyle = '#111111';
ctx.font = '900 24px monospace';
ctx.fillText('ZONE D: TAKEAWAY SYNTHESIS & PRINTER OUTPUT LOG', footerZone.x + 24, footerZone.y + 50);

canvas;
