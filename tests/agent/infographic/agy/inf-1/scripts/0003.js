Stage.begin('Composition');

Stage.note('Pattern: Editorial spread with Overlap stack (Manual 13 §4 #5 & #3).');
Stage.note('Place: At a line printer, watching fanfold paper feed through the platen.');
Stage.note('Asymmetric division: Left column (65%) for the continuous true-scale vertical timeline & milestone badges; Right column (35%) for interval comparisons, arithmetic breakdowns, and line-printer telemetry.');
Stage.note('Tension rules (§5): Dense cluster in the middle (1989-2007 rapid fire) vs open breathing room (1969-1989 20-yr gap and 2007-2022 15-yr gap).');
Stage.note('Boundary crossing (§5 #3): Bold neo-brutalist milestone badges span and overlap the vertical spine between columns; tractor-feed perforation tracks line the edges.');
Stage.note('Ground (§5 #4): Vintage line-printer continuous fanfold paper with green-bar / cream alternating bands, tractor pin feed holes, fold lines.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Check fonts
const families = Skia.Font.families();
log('Available font families sample: ' + families.slice(0, 15).join(', '));
const monoFonts = ['Consolas', 'Courier New', 'Lucida Console', 'Cascadia Code', 'monospace'].filter(f => Skia.Font.has(f));
const sansFonts = ['Impact', 'Arial Black', 'Trebuchet MS', 'Segoe UI', 'Arial', 'sans-serif'].filter(f => Skia.Font.has(f));
log('Usable Mono: ' + monoFonts.join(', '));
log('Usable Sans/Display: ' + sansFonts.join(', '));

// Draw Composition Blocking
// Paper ground
ctx.fillStyle = '#f8f5ee';
ctx.fillRect(0, 0, 1080, 1920);

// Page bounds
const page = Layout.inset(Layout.rect(0, 0, 1080, 1920), 40, 50, 40, 50);

// Perforated margin tracks (left: 0-70, right: 1010-1080)
ctx.fillStyle = '#eee8db';
ctx.fillRect(0, 0, 70, 1920);
ctx.fillRect(1010, 0, 70, 1920);

// Tractor pin holes
ctx.fillStyle = '#dcd4c0';
for (let y = 30; y < 1920; y += 45) {
    ctx.beginPath();
    ctx.arc(35, y, 9, 0, Math.PI * 2);
    ctx.arc(1045, y, 9, 0, Math.PI * 2);
    ctx.fill();
}

// Split page into Header zone, Body zone, Footer zone
const [headerRect, bodyRect, footerRect] = Layout.rows(page, [180, 1600, 140], 20);

// Header wireframe
ctx.strokeStyle = '#000000';
ctx.lineWidth = 4;
ctx.strokeRect(headerRect.x, headerRect.y, headerRect.width, headerRect.height);
ctx.fillStyle = '#000000';
ctx.font = '900 36px monospace';
ctx.fillText('HEADER: THE WEB TIMELINE [EDITORIAL SPREAD]', headerRect.x + 20, headerRect.y + 60);
ctx.font = '400 18px monospace';
ctx.fillText('Zone: High contrast brutalist banner + claim lockup', headerRect.x + 20, headerRect.y + 110);

// Body split: Left (Timeline 62%), Right (Delta Bars & Metrics 38%)
const [timelineCol, telemetryCol] = Layout.columns(bodyRect, [62, 38], 30);

// Timeline Column wireframe
ctx.fillStyle = '#ffffff';
ctx.fillRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);
ctx.strokeRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);
ctx.fillStyle = '#111111';
ctx.font = '700 22px monospace';
ctx.fillText('TIMELINE COLUMN (62%)', timelineCol.x + 20, timelineCol.y + 40);
ctx.font = '400 16px monospace';
ctx.fillText('• Linear year axis (1969 - 2022)', timelineCol.x + 20, timelineCol.y + 80);
ctx.fillText('• 7 Milestone Badges (Overlapping spine)', timelineCol.x + 20, timelineCol.y + 110);
ctx.fillText('• Dense cluster 1989-2007 vs sparse gaps', timelineCol.x + 20, timelineCol.y + 140);

// Telemetry Column wireframe
ctx.fillStyle = '#fff9d6';
ctx.fillRect(telemetryCol.x, telemetryCol.y, telemetryCol.width, telemetryCol.height);
ctx.strokeRect(telemetryCol.x, telemetryCol.y, telemetryCol.width, telemetryCol.height);
ctx.fillStyle = '#111111';
ctx.font = '700 22px monospace';
ctx.fillText('TELEMETRY & DELTAS (38%)', telemetryCol.x + 20, telemetryCol.y + 40);
ctx.font = '400 16px monospace';
ctx.fillText('• Horizontal Delta Bars (20, 4, 5, 6, 3, 15y)', telemetryCol.x + 20, telemetryCol.y + 80);
ctx.fillText('• Arithmetic breakdown callouts', telemetryCol.x + 20, telemetryCol.y + 110);
ctx.fillText('• Big Number Badges (53y, 100M)', telemetryCol.x + 20, telemetryCol.y + 140);

// Footer wireframe
ctx.strokeRect(footerRect.x, footerRect.y, footerRect.width, footerRect.height);
ctx.fillStyle = '#000000';
ctx.font = '700 20px monospace';
ctx.fillText('FOOTER / TRACTOR CUTOFF: Summary thesis & line-printer registration marks', footerRect.x + 20, footerRect.y + 60);

canvas;
