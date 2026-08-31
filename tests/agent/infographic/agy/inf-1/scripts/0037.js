Stage.begin('Encode');

Stage.note('Setting up rigorous encoding geometries for the timeline spine and the comparative gap bar chart.');

// 1. TIMELINE SCALE: 1969 to 2022
const timelineYStart = 280;
const timelineYEnd = 1580;
const timeScale = Scale.linear(1969, 2022, timelineYStart, timelineYEnd);

Stage.note('Timeline scale spans domain [1969, 2022] across pixel range [' + timelineYStart + ', ' + timelineYEnd + '].');

// 2. GAP BARS SCALE: Zero-based 0 to 20 years
const maxGap = 20;
const barMaxW = 280;
const gapScale = Scale.linear(0, maxGap, 0, barMaxW);
if (!gapScale.isZeroBased) {
  throw new Error('Bar chart scale must have a zero baseline');
}
Stage.note('Gap scale is zero-based [0, 20] years mapped to [0, 280] px. Baseline assertion PASSED.');

// Data items
const milestones = [
  { year: 1969, name: 'ARPANET 1st Message', detail: 'Sent "LO" then crashed', tag: 'CRASH', color: '#FF4338' },
  { year: 1989, name: 'World Wide Web', detail: 'Berners-Lee proposal', tag: 'WWW', color: '#FFE600' },
  { year: 1993, name: 'Mosaic Browser', detail: 'Makes the web visual', tag: 'GUI', color: '#00E5FF' },
  { year: 1998, name: 'Google Founded', detail: 'Indexing the whole web', tag: 'SEARCH', color: '#00FF66' },
  { year: 2004, name: 'Facebook Launch', detail: 'The social era begins', tag: 'SOCIAL', color: '#B388FF' },
  { year: 2007, name: 'iPhone Released', detail: 'Web in every pocket', tag: 'MOBILE', color: '#FF9100' },
  { year: 2022, name: 'ChatGPT Explosion', detail: '100M users in 2 months', tag: 'AI', color: '#FF0055' }
];

const gaps = [
  { label: 'ARPANET → Web', from: 1969, to: 1989, years: 20, color: '#FF4338' },
  { label: 'Web → Mosaic', from: 1989, to: 1993, years: 4, color: '#FFE600' },
  { label: 'Mosaic → Google', from: 1993, to: 1998, years: 5, color: '#00E5FF' },
  { label: 'Google → Facebook', from: 1998, to: 2004, years: 6, color: '#00FF66' },
  { label: 'Facebook → iPhone', from: 2004, to: 2007, years: 3, color: '#B388FF' },
  { label: 'iPhone → ChatGPT', from: 2007, to: 2022, years: 15, color: '#FF9100' }
];

// Canvas Setup
const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// Paper tone
ctx.fillStyle = '#F5F0EB';
ctx.fillRect(0, 0, 1080, 1920);

// Draw tractor margins
const marginW = 44;
ctx.fillStyle = '#E6DFD5';
ctx.fillRect(0, 0, marginW, 1920);
ctx.fillRect(1080 - marginW, 0, marginW, 1920);
ctx.fillStyle = '#222222';
for (let y = 30; y < 1920; y += 48) {
  ctx.beginPath();
  ctx.arc(marginW / 2, y, 6, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(1080 - marginW / 2, y, 6, 0, Math.PI * 2);
  ctx.fill();
}

// Green-bar continuous line-printer paper alternate shading (subtle diegetic texture)
ctx.fillStyle = 'rgba(0, 150, 80, 0.035)';
for (let y = 0; y < 1920; y += 72) {
  ctx.fillRect(marginW, y, 1080 - marginW * 2, 36);
}

// Top Title Platen Header
ctx.fillStyle = '#111111';
ctx.fillRect(60, 40, 960, 150);
ctx.fillStyle = '#00FF66';
ctx.font = '900 36px monospace';
ctx.fillText('STAGE 4: ENCODING GEOMETRY VERIFICATION', 85, 95);
ctx.fillStyle = '#FFFFFF';
ctx.font = '600 20px monospace';
ctx.fillText('PROPORTIONAL TIME SPINE (1969-2022) + ZERO-BASED GAP BARS', 85, 140);

// Timeline Spine Line
const spineX = 140;
ctx.strokeStyle = '#111111';
ctx.lineWidth = 6;
ctx.beginPath();
ctx.moveTo(spineX, timelineYStart - 20);
ctx.lineTo(spineX, timelineYEnd + 20);
ctx.stroke();

// Draw Year Ticks along Timeline Spine
for (let yr = 1970; yr <= 2020; yr += 10) {
  const ty = timeScale.map(yr);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(spineX - 16, ty);
  ctx.lineTo(spineX + 16, ty);
  ctx.stroke();

  ctx.fillStyle = '#777777';
  ctx.font = '700 16px monospace';
  ctx.textAlign = 'right';
  ctx.fillText(String(yr), spineX - 24, ty + 5);
}

// Plot Milestones on Timeline
ctx.textAlign = 'left';
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const my = timeScale.map(m.year);

  // Spine pin notch
  ctx.fillStyle = m.color;
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.arc(spineX, my, 12, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();

  // Connector
  ctx.beginPath();
  ctx.moveTo(spineX + 12, my);
  ctx.lineTo(spineX + 60, my);
  ctx.stroke();

  // Event Card
  const cardW = 440;
  const cardH = 70;
  const cardX = spineX + 60;
  const cardY = my - cardH / 2;

  ctx.fillStyle = '#FFFFFF';
  ctx.fillRect(cardX, cardY, cardW, cardH);
  ctx.strokeRect(cardX, cardY, cardW, cardH);
  
  // Neo-brutalist solid shadow
  ctx.fillStyle = '#111111';
  ctx.fillRect(cardX + 4, cardY + cardH, cardW, 4);
  ctx.fillRect(cardX + cardW, cardY + 4, 4, cardH);

  // Year Tag
  ctx.fillStyle = m.color;
  ctx.fillRect(cardX, cardY, 90, cardH);
  ctx.strokeStyle = '#111111';
  ctx.strokeRect(cardX, cardY, 90, cardH);

  ctx.fillStyle = '#111111';
  ctx.font = '900 24px monospace';
  ctx.fillText(String(m.year), cardX + 12, cardY + 42);

  // Event Details
  ctx.fillStyle = '#111111';
  ctx.font = '800 19px sans-serif';
  ctx.fillText(m.name, cardX + 105, cardY + 28);
  ctx.font = '500 15px sans-serif';
  ctx.fillStyle = '#555555';
  ctx.fillText(m.detail, cardX + 105, cardY + 54);
}

// Right Panel: Comparative Gap Bars
const gapPanelX = 660;
const gapPanelY = 240;
const gapPanelW = 360;
const gapPanelH = 680;

ctx.fillStyle = '#FFFFFF';
ctx.fillRect(gapPanelX, gapPanelY, gapPanelW, gapPanelH);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 5;
ctx.strokeRect(gapPanelX, gapPanelY, gapPanelW, gapPanelH);

// Gap Panel Header
ctx.fillStyle = '#FF5500';
ctx.fillRect(gapPanelX, gapPanelY, gapPanelW, 55);
ctx.strokeRect(gapPanelX, gapPanelY, gapPanelW, 55);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 20px monospace';
ctx.fillText('THE WAITING PERIODS', gapPanelX + 16, gapPanelY + 36);

// Axis / Ticks for Gap Bars
const barStartX = gapPanelX + 24;
const barStartY = gapPanelY + 90;
const barBaselineY = gapPanelY + 130;

// Axis ticks (0, 5, 10, 15, 20 years)
ctx.strokeStyle = '#DDDDDD';
ctx.lineWidth = 1;
for (let t = 0; t <= 20; t += 5) {
  const tx = barStartX + gapScale.map(t);
  ctx.beginPath();
  ctx.moveTo(tx, barStartY);
  ctx.lineTo(tx, gapPanelY + gapPanelH - 30);
  ctx.stroke();

  ctx.fillStyle = '#888888';
  ctx.font = '600 13px monospace';
  ctx.textAlign = 'center';
  ctx.fillText(t + 'y', tx, barStartY + 16);
}

// Draw Gap Bars
ctx.textAlign = 'left';
let by = barStartY + 45;
for (let i = 0; i < gaps.length; i++) {
  const g = gaps[i];
  const barW = gapScale.map(g.years);

  ctx.fillStyle = '#111111';
  ctx.font = '700 15px sans-serif';
  ctx.fillText(g.label, barStartX, by);

  // Bar
  ctx.fillStyle = g.color;
  ctx.fillRect(barStartX, by + 8, barW, 26);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 3;
  ctx.strokeRect(barStartX, by + 8, barW, 26);

  // Value text
  ctx.fillStyle = '#111111';
  ctx.font = '900 16px monospace';
  ctx.fillText(g.years + ' yrs (' + (g.to - g.from) + 'y)', barStartX + barW + 10, by + 27);

  by += 65;
}

// Bottom Analytics Callout Card (Hero Stat)
const statX = gapPanelX;
const statY = 960;
const statW = gapPanelW;
const statH = 600;

ctx.fillStyle = '#FFE600';
ctx.fillRect(statX, statY, statW, statH);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 5;
ctx.strokeRect(statX, statY, statW, statH);

ctx.fillStyle = '#111111';
ctx.font = '900 24px monospace';
ctx.fillText('HERO METRICS', statX + 20, statY + 45);

// Big Number 53
ctx.font = '900 96px sans-serif';
ctx.fillText('53', statX + 20, statY + 150);
ctx.font = '900 26px sans-serif';
ctx.fillText('YEARS TOTAL SPAN', statX + 20, statY + 195);
ctx.font = '500 16px sans-serif';
ctx.fillWrappedText('From first 2-letter crash packet to global ubiquitous foundation (1969-2022).', statX + 20, statY + 230, statW - 40, 24);

// Big Number 100M in 2 Mo
ctx.fillStyle = '#FF0055';
ctx.fillRect(statX + 16, statY + 320, statW - 32, 240);
ctx.strokeRect(statX + 16, statY + 320, statW - 32, 240);

ctx.fillStyle = '#FFFFFF';
ctx.font = '900 52px sans-serif';
ctx.fillText('100M', statX + 36, statY + 390);
ctx.font = '900 22px monospace';
ctx.fillText('USERS IN 60 DAYS', statX + 36, statY + 430);
ctx.font = '500 16px sans-serif';
ctx.fillWrappedText('ChatGPT fastest consumer adoption in computing history.', statX + 36, statY + 470, statW - 72, 24);

// Footer Platen Feed Summary
const footY = 1620;
ctx.fillStyle = '#111111';
ctx.fillRect(60, footY, 960, 240);
ctx.fillStyle = '#FFE600';
ctx.font = '900 26px monospace';
ctx.fillText('SYNTHESIS: UNEVEN TEMPO OF COMPUTING', 85, footY + 50);
ctx.fillStyle = '#FFFFFF';
ctx.font = '600 20px sans-serif';
ctx.fillWrappedText('"Progress is constant — it never stopped coming, but it never once came on schedule."', 85, footY + 95, 910, 32);
ctx.fillStyle = '#00E5FF';
ctx.font = '500 17px monospace';
ctx.fillText('FANFOLD LINE PRINTER OUTPUT · SYSTEM VERIFIED 100% TRUTHFUL GEOMETRY', 85, footY + 195);

canvas;
