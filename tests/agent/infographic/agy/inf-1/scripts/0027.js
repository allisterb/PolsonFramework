Stage.begin('Forms');

Stage.note('Form Selection Analysis (Manual 13 §1):');
Stage.note('1. Over time? -> Calibrated Vertical Timeline track with linearly scaled physical vertical intervals (1969-2022). Rejected: equal-step card list (destroys temporal truth), horizontal timeline (awkward in 9:16 story format).');
Stage.note('2. Compared to what? -> Zero-based horizontal delta bar chart comparing inter-milestone wait times (20y, 4y, 5y, 6y, 3y, 15y). Rejected: pie/donut (deltas are sequential durations, not parts-of-whole), radar chart (poor comparability).');
Stage.note('3. How big? -> Neo-brutalist Big-Number stat badges (53 yr total span, 20 yr max gap, 3 yr min gap, 100M / 2 mo velocity). Rejected: gauge dials, 3D charts.');
Stage.note('4. How does it work? -> Milestone cards with high-contrast badge headers, year stamps, and monospace annotation blocks styled like continuous line printer output.');

log('Form selection complete: 4 distinct forms integrated (Vertical Timeline, Horizontal Delta Bar Meters, Big-Number Badges, Monospace Printout Cards).');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#fdfbf7';
ctx.fillRect(0, 0, 1080, 1920);
ctx.fillStyle = '#111111';
ctx.font = '700 24px Consolas, monospace';
ctx.fillText('STAGE 2: FORMS SELECTED & VALIDATED', 60, 100);

canvas;
