// STAGE 2 - Form selection. Unstyled on purpose: this tests which encodings carry the argument,
// not what they will look like. Palette and type are Stage 5. Judged from the render, not the plan.
Stage.begin('Forms');

const MILESTONES = [
    { year: 1969, short: 'ARPANET' },
    { year: 1989, short: 'WWW' },
    { year: 1993, short: 'MOSAIC' },
    { year: 1998, short: 'GOOGLE' },
    { year: 2004, short: 'FACEBOOK' },
    { year: 2007, short: 'IPHONE' },
    { year: 2022, short: 'CHATGPT' }
];
const Y0 = 1969, Y1 = 2022;

const gaps = [];
for (let i = 1; i < MILESTONES.length; i++) {
    gaps.push({
        label: MILESTONES[i - 1].year + '-' + MILESTONES[i].year,
        years: MILESTONES[i].year - MILESTONES[i - 1].year
    });
}

// Waffle needs a CELL COUNT, which is calendar years inclusive - not the 53-year span.
// 2022 - 1969 + 1 = 54 cells. Getting this wrong by one is exactly the silent kind of error.
const cellYears = Y1 - Y0 + 1;
const milestoneYears = MILESTONES.length;
const quietYears = cellYears - milestoneYears;
log('WAFFLE CELLS  = 2022 - 1969 + 1 = ' + cellYears + ' calendar years');
log('  milestone yr= ' + milestoneYears + '   quiet yr = ' + quietYears +
    '   quiet share = ' + quietYears + '/' + cellYears + ' = ' + (quietYears / cellYears * 100).toFixed(1) + '%');

const canvas = createCanvas(1000, 1400);
const ctx = canvas.getContext('2d');
const INK = '#111111', MID = '#8A8A8A', PAPER = '#F2F0EA', ACCENT = '#D8402F';
ctx.fillStyle = PAPER;
ctx.fillRect(0, 0, 1000, 1400);

const page = Layout.inset(Layout.rect(0, 0, 1000, 1400), 40);
const [header, body] = Layout.rows(page, [7, 93], 20);

ctx.fillStyle = INK;
ctx.font = 'bold 22px Arial';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillText('STAGE 2 FORM TEST - unstyled. Which encoding carries "never on schedule"?', header.x, header.y);

const [leftCol, rightCol] = Layout.columns(body, [42, 58], 46);

const panelTitle = (rect, t) => {
    ctx.fillStyle = MID;
    ctx.font = 'bold 15px Arial';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'top';
    ctx.fillText(t, rect.x, rect.y);
};

// ---- FORM A: vertical timeline on a LINEAR YEAR AXIS. Question: "over time / follow".
// A timeline encodes POSITION, not length, so Manual 13 s2 permits a non-zero origin here -
// the axis starts at 1969 rather than at year zero, which would be absurd. The rule it must
// obey instead is that the axis is linear in years, so the gaps are drawn to scale.
panelTitle(leftCol, 'A - TIMELINE, LINEAR YEAR AXIS  (follow)');
const tl = Layout.inset(leftCol, 40, 0, 10, 0);
const ty = Scale.linear(Y0, Y1, tl.y, tl.y2);
const railX = tl.x + 74;

ctx.strokeStyle = INK;
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(railX, tl.y);
ctx.lineTo(railX, tl.y2);
ctx.stroke();

// Gap ribbons first, so the SILENCE is a drawn object rather than leftover whitespace.
for (let i = 1; i < MILESTONES.length; i++) {
    const a = ty.map(MILESTONES[i - 1].year), b = ty.map(MILESTONES[i].year);
    const yrs = MILESTONES[i].year - MILESTONES[i - 1].year;
    ctx.fillStyle = (yrs >= 15) ? '#E3DFD4' : '#EAE7DE';
    ctx.fillRect(railX + 8, a, 26, b - a);
    if (b - a > 26) {
        ctx.fillStyle = (yrs >= 15) ? ACCENT : MID;
        ctx.font = 'bold 13px Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.save();
        ctx.translate(railX + 21, (a + b) / 2);
        ctx.rotate(-Math.PI / 2);
        ctx.fillText(yrs + ' YR', 0, 0);
        ctx.restore();
    }
}

MILESTONES.forEach(m => {
    const y = ty.map(m.year);
    ctx.fillStyle = INK;
    ctx.fillRect(railX - 16, y - 3, 32, 6);
    ctx.font = 'bold 17px Arial';
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';
    ctx.fillText(String(m.year), railX - 24, y);
    ctx.textAlign = 'left';
    ctx.font = 'bold 15px Arial';
    ctx.fillText(m.short, railX + 44, y);
});

// ---- FORM B: waffle of calendar years. Question: "what share of this history was waiting?"
const [wafflePanel, barPanel, numPanel] = Layout.rows(rightCol, [34, 40, 26], 44);
panelTitle(wafflePanel, 'B - WAFFLE, 1 CELL = 1 YEAR  (count / share)');
const wf = Layout.inset(wafflePanel, 34, 0, 0, 0);
const COLS = 9, ROWS = 6;                      // 9 x 6 = 54 cells exactly
const cells = Layout.grid(wf, COLS, ROWS, 6, 6);
const milestoneSet = {};
MILESTONES.forEach(m => { milestoneSet[m.year] = true; });
for (let i = 0; i < cellYears; i++) {
    const yr = Y0 + i, c = cells[i];
    const hit = milestoneSet[yr] === true;
    ctx.fillStyle = hit ? ACCENT : '#DEDAD0';
    ctx.fillRect(c.x, c.y, c.width, c.height);
    if (hit) {
        ctx.strokeStyle = INK;
        ctx.lineWidth = 3;
        ctx.strokeRect(c.x, c.y, c.width, c.height);
    }
}
ctx.fillStyle = INK;
ctx.font = 'bold 14px Arial';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillText(quietYears + ' OF ' + cellYears + ' YEARS HAD NO MILESTONE ON THIS LIST',
    wf.x, cells[cells.length - 1].y2 + 12);

// ---- FORM C: ranked bars of the six intervals. Question: "compared to what - which wait was longest?"
// Re-sorting by length is what makes this NOT a restatement of A: it drops the time order to
// answer a ranking question the timeline cannot answer at a glance.
panelTitle(barPanel, 'C - RANKED BARS, INTERVALS SORTED  (compare)');
const bp = Layout.inset(barPanel, 34, 0, 0, 96);
const sorted = gaps.slice().sort((a, b) => b.years - a.years);
const gmax = Scale.nice(0, Math.max.apply(null, gaps.map(g => g.years)));
const bx = Scale.linear(0, gmax.max, bp.x, bp.x2);
if (!bx.isZeroBased) throw new Error('bars need a zero baseline');
const bband = Scale.band(sorted.length, bp.y, bp.y2, 0.3);
sorted.forEach((g, i) => {
    ctx.fillStyle = g.years >= 15 ? ACCENT : INK;
    ctx.fillRect(bp.x, bband.map(i), bx.extent(0, g.years), bband.bandwidth);
    ctx.fillStyle = INK;
    ctx.font = 'bold 13px Arial';
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';
    ctx.fillText(g.label, bp.x - 10, bband.center(i));
    ctx.textAlign = 'left';
    ctx.fillText(g.years + ' YR', bp.x + bx.extent(0, g.years) + 10, bband.center(i));
});

// ---- FORM D: big-number callout. Question: "how big?" - one value, so not a chart (s1).
panelTitle(numPanel, 'D - BIG NUMBER  (how big, one value)');
const np = Layout.inset(numPanel, 34, 0, 0, 0);
ctx.fillStyle = INK;
ctx.font = '112px Impact';
ctx.letterSpacing = '-0.02em';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillText('100M', np.x, np.y);
ctx.letterSpacing = '0em';
ctx.font = 'bold 16px Arial';
ctx.fillText('USERS IN 2 MONTHS', np.x + 6, np.y + 118);
ctx.fillStyle = MID;
ctx.font = '14px Arial';
ctx.fillText('= 50M PER MONTH  (100 / 2)', np.x + 6, np.y + 140);

log('form test rendered - judging next');
canvas;
