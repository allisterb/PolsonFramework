Stage.begin('Forms');

Stage.note('Form 1: Vertical Timeline (Over time / Follow) — answers where the 7 milestones fall across 1969-2022 to true linear scale on fanfold continuous feed.');
Stage.note('Form 2: Horizontal Interval Bars (Compared to what) — answers how the gaps compare (20y, 4y, 5y, 6y, 3y, 15y) with strict zero-baseline Scale.linear.');
Stage.note('Form 3: Big Number Callouts & Neo-brutalist Badges (How big / Count) — answers the scale of the endpoints (53 years total, 100M in 2 months, 2 chars "LO").');
Stage.note('Rejected Form: Donut/Pie chart — sequential chronological milestones do not represent part-to-whole shares.');
Stage.note('Rejected Form: Radar chart — arbitrary multidimensional mapping distorts time-series gaps.');
Stage.note('Rejected Form: Standard uniform card-grid — violates non-dashboard rule; linear flow on tractor-feed paper is required.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f4f0ea';
ctx.fillRect(0, 0, 01080, 1920);

ctx.fillStyle = '#111111';
ctx.font = '700 32px monospace';
ctx.fillText('STAGE 2: FORM SELECTION', 60, 100);

ctx.font = '600 22px monospace';
ctx.fillText('SELECTED FORMS (>=3 Genuine Forms):', 60, 160);
ctx.font = '400 18px monospace';
ctx.fillText('1. True-Scale Vertical Timeline (Over time / Follow)', 80, 210);
ctx.fillText('2. Interval Delta Horizontal Bars [isZeroBased] (Compared to what)', 80, 250);
ctx.fillText('3. Neo-Brutalist Big Number & Ticket Badges (How big / Key metrics)', 80, 290);
ctx.fillText('4. Line-Printer Tractor Tally Track (Rhythm / Progression)', 80, 330);

ctx.font = '600 22px monospace';
ctx.fillText('REJECTED FORMS:', 60, 410);
ctx.font = '400 18px monospace';
ctx.fillText('• Donut / Pie: Temporal gaps are not parts of a single closed whole', 80, 460);
ctx.fillText('• Radar / Spider: Arbitrary polygon distortions on 1D time sequences', 80, 500);
ctx.fillText('• Uniform Card Grid: Dashboard anti-pattern; fails fanfold platen narrative', 80, 540);

canvas;
