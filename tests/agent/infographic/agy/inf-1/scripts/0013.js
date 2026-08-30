Stage.begin('Composition');
Stage.note('Stage 3: Composition — Editorial Spread pattern (Manual 13 §4) with 63/37 asymmetric split.');
Stage.note('Reader position: Standing at a line printer, watching continuous fanfold paper feed through the platen.');
Stage.note('Tension rules: One dense cluster (1993-2007), one breathing zone (1969-1989 gap), hero element crosses column boundary, non-uniform ground with tractor feed perforations.');

const width = 1080;
const height = 1920;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

// Ground: Neo-brutalist pale fanfold paper with green-bar tractor margins
ctx.fillStyle = '#f6f5ee';
ctx.fillRect(0, 0, width, height);

// Page structure using Layout
const canvasRect = Layout.rect(0, 0, width, height);
const page = Layout.inset(canvasRect, 40, 36, 40, 36);

// Vertical layout: Header, Main Area, Footer
const [headerBand, mainBand, footerBand] = Layout.rows(page, [220, 1400, 220], 24);

// Main area asymmetric columns (63% Timeline / 37% Comparison & Stats)
const [timelineCol, statsCol] = Layout.columns(mainBand, [63, 37], 28);

// Blocking Wireframe Visuals
ctx.lineWidth = 4;
ctx.strokeStyle = '#111111';

// Header block
ctx.fillStyle = '#ffffff';
ctx.fillRect(headerBand.x, headerBand.y, headerBand.width, headerBand.height);
ctx.strokeRect(headerBand.x, headerBand.y, headerBand.width, headerBand.height);

// Timeline block
ctx.fillStyle = '#ffffff';
ctx.fillRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);
ctx.strokeRect(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height);

// Stats sub-panels using Layout.rows
const statsPanels = Layout.rows(statsCol, [260, 520, 280, 280], 20);
const panelColors = ['#ffdf00', '#ffffff', '#a6e3e9', '#ff6b6b'];

for (let i = 0; i < statsPanels.length; i++) {
  const p = statsPanels[i];
  ctx.fillStyle = panelColors[i % panelColors.length];
  ctx.fillRect(p.x, p.y, p.width, p.height);
  ctx.strokeRect(p.x, p.y, p.width, p.height);
}

// Footer block
ctx.fillStyle = '#ffffff';
ctx.fillRect(footerBand.x, footerBand.y, footerBand.width, footerBand.height);
ctx.strokeRect(footerBand.x, footerBand.y, footerBand.width, footerBand.height);

// Header text blocking
ctx.fillStyle = '#111111';
ctx.font = '900 48px sans-serif';
ctx.fillText('STAGE 3: COMPOSITION BLOCKING', headerBand.x + 30, headerBand.y + 70);

ctx.font = '700 24px monospace';
ctx.fillText('PATTERN: EDITORIAL SPREAD [63 / 37]', headerBand.x + 30, headerBand.y + 120);
ctx.fillText('VIEWPORT: 1080 x 1920 PORTRAIT', headerBand.x + 30, headerBand.y + 160);

// Annotations
ctx.font = '700 28px monospace';
ctx.fillText('ZONE A: PROPORTIONAL TIMELINE', timelineCol.x + 30, timelineCol.y + 60);
ctx.fillText('(Real 53-Year Scale 1969-2022)', timelineCol.x + 30, timelineCol.y + 100);

ctx.fillText('HERO: 53 YEARS', statsPanels[0].x + 20, statsPanels[0].y + 60);
ctx.fillText('GAP COMPARISON BARS', statsPanels[1].x + 20, statsPanels[1].y + 60);
ctx.fillText('ERA SHARES', statsPanels[2].x + 20, statsPanels[2].y + 60);
ctx.fillText('ADOPTION VELOCITY', statsPanels[3].x + 20, statsPanels[3].y + 60);

ctx.fillText('ZONE C: CORE CLAIM & VERIFICATION', footerBand.x + 30, footerBand.y + 60);

log('Composition blocking rendered to artifacts/03_composition.webp');
canvas;
