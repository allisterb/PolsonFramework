Stage.begin('Composition');

Stage.note('NAMED PATTERN: Editorial spread & Overlap stack — A strong vertical structural spine with asymmetrical offset milestone blocks, high-contrast visual tension, and stamp overlays breaking container boundaries.');
Stage.note('PLACE: At a line printer, watching fanfold tractor-feed paper emerge from the platen. The canvas features continuous tractor-feed sprocket tracks on the outer edges and dot-matrix registration marks.');
Stage.note('TENSION RULES: (1) Dense zone: 1989–2007 cluster (Web, Mosaic, Google, Facebook, iPhone tightly packed); Empty zone: 1969–1989 20-year gap breathing space (~20% canvas height). (2) Hierarchy: Hero display headline at 84px vs body copy at 14px (6x-8x ladder). (3) Boundary breaking: The "53 YEARS" hero badge tilts at -5 deg and overlaps the header rule into the timeline zone; the 1969 ARPANET card bleeds slightly past the timeline rail. (4) Ground: Fanfold paper tint (#f4f0e6) with subtle dot-matrix grid and side tractor perforation margins.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Base ground
ctx.fillStyle = '#f5f1e8';
ctx.fillRect(0, 0, 1080, 1920);

// Tractor margins
const marginW = 44;
ctx.fillStyle = '#ebe5d8';
ctx.fillRect(0, 0, marginW, 1920);
ctx.fillRect(1080 - marginW, 0, marginW, 1920);

// Inner page area
const page = Layout.rect(marginW, 0, 1080 - marginW * 2, 1920);
const [headerZone, contentZone, footerZone] = Layout.rows(page, [280, 1260, 380], 0);

// Draw Zone Blocking for visualization
ctx.strokeStyle = '#000000';
ctx.lineWidth = 4;

// Header box
ctx.fillStyle = '#ffde59';
ctx.fillRect(headerZone.x, headerZone.y + 20, headerZone.width, headerZone.height - 30);
ctx.strokeRect(headerZone.x, headerZone.y + 20, headerZone.width, headerZone.height - 30);

// Content zone: split into Timeline (left 62%) and Secondary/Gaps (right 38%) or Full width timeline
const [timelineCol, statsCol] = Layout.columns(contentZone, [62, 38], 20);

ctx.fillStyle = '#ffffff';
ctx.fillRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);
ctx.strokeRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);

ctx.fillStyle = '#00f0ff';
ctx.fillRect(statsCol.x, statsCol.y, statsCol.width, statsCol.height);
ctx.strokeRect(statsCol.x, statsCol.y, statsCol.width, statsCol.height);

// Footer zone
ctx.fillStyle = '#ff3366';
ctx.fillRect(footerZone.x, footerZone.y + 10, footerZone.width, footerZone.height - 20);
ctx.strokeRect(footerZone.x, footerZone.y + 10, footerZone.width, footerZone.height - 20);

// Labels
ctx.fillStyle = '#000000';
ctx.font = '900 32px monospace';
ctx.fillText('ZONE 1: HEADER & CLAIM LOCKUP', headerZone.x + 30, headerZone.y + 80);
ctx.fillText('ZONE 2A: SCALED TIMELINE', timelineCol.x + 20, timelineCol.y + 60);
ctx.fillText('ZONE 2B: GAP BARS', statsCol.x + 20, statsCol.y + 60);
ctx.fillText('ZONE 3: SUMMARY & FOOTER', footerZone.x + 30, footerZone.y + 80);

canvas;
