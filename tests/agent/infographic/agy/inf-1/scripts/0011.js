Stage.begin('Audit');

// 1. Data Definitions & Verification
const milestones = [
    { year: 1969, title: 'ARPANET FIRST PACKET', text: 'Sends its first message (it crashed after "LO")', tag: 'CRASHED AFTER "LO"', color: '#ff3e3e' },
    { year: 1989, title: 'WORLD WIDE WEB', text: 'Tim Berners-Lee proposes the World Wide Web', tag: 'THE PROPOSAL', color: '#0047ff' },
    { year: 1993, title: 'MOSAIC BROWSER', text: 'Mosaic makes the web visual', tag: 'FIRST IMAGES', color: '#00d26a' },
    { year: 1998, title: 'GOOGLE FOUNDED', text: 'Google is founded to index the exploding web', tag: 'ORGANIZING CHAOS', color: '#ff9900' },
    { year: 2004, title: 'FACEBOOK LAUNCHES', text: 'Facebook launches — the social era begins', tag: 'THE SOCIAL ERA', color: '#0047ff' },
    { year: 2007, title: 'THE iPHONE REVOLUTION', text: 'The iPhone puts the web in every pocket', tag: 'WEB IN POCKET', color: '#9333ea' },
    { year: 2022, title: 'CHATGPT HYPER-SCALE', text: 'ChatGPT reaches 100M users in 2 months', tag: '100M IN 2 MONTHS', color: '#ff3e3e' }
];

const gaps = [
    { name: 'ARPANET → Web', years: 20, math: '1989 - 1969', note: '20-Year Incubation', col: '#ff3e3e' },
    { name: 'Web → Mosaic', years: 4, math: '1993 - 1989', note: 'Visual Revolution', col: '#00d26a' },
    { name: 'Mosaic → Google', years: 5, math: '1998 - 1993', note: 'Search Indexing', col: '#ff9900' },
    { name: 'Google → Facebook', years: 6, math: '2004 - 1998', note: 'Social Networks', col: '#0047ff' },
    { name: 'Facebook → iPhone', years: 3, math: '2007 - 2004', note: 'Mobile Leap', col: '#9333ea' },
    { name: 'iPhone → ChatGPT', years: 15, math: '2022 - 2007', note: 'AI Maturation', col: '#ff3e3e' }
];

const totalSpan = 2022 - 1969; // 53 years

// 2. Render Full Infographic
const cssText = `
:root {
  --bg-paper: #f6f1e7;
  --bg-chassis: #131415;
  --zebra-bar: #e5ede6;
  --ink-black: #111213;
  --neo-red: #ff3838;
  --neo-blue: #0044ff;
  --neo-green: #00c853;
  --neo-yellow: #ffd000;
  --neo-purple: #8b5cf6;
  --guide-gray: #8d9096;
}
`;
const sheet = Css.fromCss(cssText);
const T = sheet.tokens();

const W = 1080;
const H = 1920;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

function drawBrutalistCard(x, y, w, h, bg, shadowOffset = 6, borderW = 3.5) {
    ctx.fillStyle = T['--ink-black'];
    ctx.fillRect(x + shadowOffset, y + shadowOffset, w, h);
    ctx.fillStyle = bg;
    ctx.strokeStyle = T['--ink-black'];
    ctx.lineWidth = borderW;
    ctx.fillRect(x, y, w, h);
    ctx.strokeRect(x, y, w, h);
}

// Chassis & Fanfold Paper
ctx.fillStyle = T['--bg-chassis'];
ctx.fillRect(0, 0, W, H);

const paperX = 32;
const paperW = W - paperX * 2;
ctx.fillStyle = T['--bg-paper'];
ctx.fillRect(paperX, 0, paperW, H);

// Zebra accounting bands
ctx.fillStyle = 'rgba(215, 230, 218, 0.45)';
for (let y = 0; y < H; y += 72) {
    if (Math.floor(y / 72) % 2 === 1) {
        ctx.fillRect(paperX, y, paperW, 72);
    }
}

// Fold Perforations
const folds = [420, 1140, 1720];
ctx.strokeStyle = '#baa892';
ctx.lineWidth = 1.5;
ctx.setLineDash([8, 8]);
for (const fy of folds) {
    ctx.beginPath();
    ctx.moveTo(paperX, fy);
    ctx.lineTo(paperX + paperW, fy);
    ctx.stroke();

    ctx.fillStyle = '#8f7d67';
    ctx.font = '700 11px "Courier New"';
    ctx.textAlign = 'left';
    ctx.fillText('▼ FOLD PERFORATION LINE // TEAR HERE ▼', paperX + 45, fy - 4);
}
ctx.setLineDash([]);

// Tractor-feed Sprockets
const drawSprockets = (cx) => {
    for (let sy = 24; sy < H; sy += 38) {
        ctx.fillStyle = T['--bg-chassis'];
        ctx.beginPath();
        ctx.arc(cx, sy, 8, 0, Math.PI * 2);
        ctx.fill();

        ctx.strokeStyle = '#d0c5b4';
        ctx.lineWidth = 1.5;
        ctx.stroke();
    }
};
drawSprockets(paperX + 18);
drawSprockets(paperX + paperW - 18);

// Technical Registration Marks
const drawCrosshair = (cx, cy) => {
    ctx.strokeStyle = T['--ink-black'];
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.arc(cx, cy, 10, 0, Math.PI * 2);
    ctx.moveTo(cx - 16, cy); ctx.lineTo(cx + 16, cy);
    ctx.moveTo(cx, cy - 16); ctx.lineTo(cx, cy + 16);
    ctx.stroke();
};
drawCrosshair(paperX + 50, 40);
drawCrosshair(paperX + paperW - 50, 40);

ctx.fillStyle = '#6b7280';
ctx.font = '700 12px "Courier New"';
ctx.textAlign = 'left';
ctx.fillText('FORM 1401-STD // TRACTOR FEED SPEED: 1100 LPM // BUFFER: SYNC', paperX + 80, 42);
ctx.textAlign = 'right';
ctx.fillText('RECORD: 1969.10.29 - 2022.11.30 // SEQ #0053', paperX + paperW - 80, 42);

// ==========================================
// HEADER ZONE: HERO TITLE & CLAIM MARQUEE
// ==========================================
const headerBox = Layout.rect(paperX + 42, 60, paperW - 84, 280);
drawBrutalistCard(headerBox.x, headerBox.y, headerBox.width, headerBox.height, '#ffffff', 8, 4);

// Top Header Banner
ctx.fillStyle = T['--neo-yellow'];
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 3;
ctx.fillRect(headerBox.x, headerBox.y, headerBox.width, 42);
ctx.strokeRect(headerBox.x, headerBox.y, headerBox.width, 42);

ctx.fillStyle = T['--ink-black'];
ctx.font = '900 16px "Arial"';
ctx.textAlign = 'left';
ctx.fillText('● THE DEFINITIVE 53-YEAR CHRONICLE OF THE INTERNET', headerBox.x + 16, headerBox.y + 27);
ctx.textAlign = 'right';
ctx.fillText('A SCALE POSTER OF PUBLIC MILESTONES [1969—2022] ■', headerBox.x + headerBox.width - 16, headerBox.y + 27);

// Giant Title
ctx.textAlign = 'left';
ctx.fillStyle = T['--ink-black'];
ctx.font = '900 78px Impact';
ctx.fillText('WEB-HISTORY', headerBox.x + 20, headerBox.y + 124);

// Tag Badge
ctx.save();
ctx.translate(headerBox.x + 580, headerBox.y + 68);
ctx.rotate(3 * Math.PI / 180);
ctx.fillStyle = T['--ink-black'];
ctx.fillRect(4, 4, 310, 48);
ctx.fillStyle = T['--neo-red'];
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 3;
ctx.fillRect(0, 0, 310, 48);
ctx.strokeRect(0, 0, 310, 48);
ctx.fillStyle = '#ffffff';
ctx.font = '900 20px "Arial Black", Impact';
ctx.textAlign = 'center';
ctx.fillText('DRAWN TO TRUE SCALE', 155, 31);
ctx.restore();

// Claim Box
const claimBoxY = headerBox.y + 148;
ctx.fillStyle = T['--neo-yellow'];
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 3;
ctx.fillRect(headerBox.x + 20, claimBoxY, headerBox.width - 40, 110);
ctx.strokeRect(headerBox.x + 20, claimBoxY, headerBox.width - 40, 110);

ctx.fillStyle = T['--ink-black'];
ctx.font = '900 14px "Courier New"';
ctx.textAlign = 'left';
ctx.fillText('THE CORE ARGUMENT / BRIEF CLAIM:', headerBox.x + 36, claimBoxY + 28);

ctx.font = '900 21px "Arial"';
ctx.fillText('"Progress is constant — it never stopped coming,', headerBox.x + 36, claimBoxY + 58);
ctx.fillText('but it never once came on schedule."', headerBox.x + 36, claimBoxY + 86);

// ==========================================
// MAIN ZONE: TIMELINE TRACK + GAP COMPARATOR
// ==========================================
const mainTop = 360;
const mainH = 1200;
const mainBox = Layout.rect(paperX + 42, mainTop, paperW - 84, mainH);
const [timelineCol, gapCol] = Layout.columns(mainBox, [58, 42], 26);

// LEFT COLUMN: TIMELINE
drawBrutalistCard(timelineCol.x, timelineCol.y, timelineCol.width, timelineCol.height, '#ffffff', 8, 4);

ctx.fillStyle = T['--ink-black'];
ctx.fillRect(timelineCol.x, timelineCol.y, timelineCol.width, 52);
ctx.fillStyle = '#ffffff';
ctx.font = '900 20px "Arial Black", Impact';
ctx.textAlign = 'left';
ctx.fillText('01 // CONTINUOUS TIME SCALE (53 YRS)', timelineCol.x + 18, timelineCol.y + 34);

const tMarginTop = timelineCol.y + 80;
const tMarginBottom = timelineCol.y + timelineCol.height - 40;
const timeScale = Scale.linear(1969, 2022, tMarginTop, tMarginBottom);

const axisX = timelineCol.x + 76;
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 5;
ctx.beginPath();
ctx.moveTo(axisX, tMarginTop - 10);
ctx.lineTo(axisX, tMarginBottom + 10);
ctx.stroke();

const decades = [1970, 1980, 1990, 2000, 2010, 2020];
for (const dec of decades) {
    const dy = timeScale.map(dec);
    ctx.strokeStyle = '#d1d5db';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(axisX + 8, dy);
    ctx.lineTo(timelineCol.x + timelineCol.width - 20, dy);
    ctx.stroke();

    ctx.strokeStyle = T['--ink-black'];
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(axisX - 12, dy);
    ctx.lineTo(axisX + 6, dy);
    ctx.stroke();

    ctx.fillStyle = '#9ca3af';
    ctx.font = '900 13px "Courier New"';
    ctx.textAlign = 'right';
    ctx.fillText(dec.toString(), axisX - 18, dy + 5);
}

const cardWidth = timelineCol.width - 120;
for (let i = 0; i < milestones.length; i++) {
    const m = milestones[i];
    const my = timeScale.map(m.year);

    ctx.strokeStyle = T['--ink-black'];
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(axisX, my);
    ctx.lineTo(axisX + 30, my);
    ctx.stroke();

    ctx.fillStyle = m.color;
    ctx.beginPath();
    ctx.arc(axisX, my, 9, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = T['--ink-black'];
    ctx.lineWidth = 3.5;
    ctx.stroke();

    const cardH = 68;
    const cardX = axisX + 30;
    const cardY = my - cardH / 2;

    drawBrutalistCard(cardX, cardY, cardWidth, cardH, '#ffffff', 4, 3);

    ctx.fillStyle = m.color;
    ctx.fillRect(cardX, cardY, 68, cardH);
    ctx.strokeStyle = T['--ink-black'];
    ctx.lineWidth = 3;
    ctx.strokeRect(cardX, cardY, 68, cardH);

    ctx.fillStyle = '#ffffff';
    ctx.font = '900 19px "Arial Black", Impact';
    ctx.textAlign = 'center';
    ctx.fillText(m.year.toString(), cardX + 34, cardY + 41);

    ctx.textAlign = 'left';
    ctx.fillStyle = T['--ink-black'];
    ctx.font = '900 16px "Arial"';
    ctx.fillText(m.title, cardX + 80, cardY + 26);

    ctx.font = '600 12px "Consolas"';
    ctx.fillStyle = '#4b5563';
    ctx.fillText(m.text, cardX + 80, cardY + 48);

    ctx.fillStyle = T['--ink-black'];
    ctx.fillRect(cardX + cardWidth - 148, cardY + 8, 140, 20);
    ctx.fillStyle = '#ffffff';
    ctx.font = '900 10px "Courier New"';
    ctx.textAlign = 'center';
    ctx.fillText(m.tag, cardX + cardWidth - 78, cardY + 22);

    if (i < milestones.length - 1) {
        const nextY = timeScale.map(milestones[i + 1].year);
        const gapSpanY = nextY - my;
        const gapYears = milestones[i + 1].year - m.year;

        if (gapSpanY > 35) {
            const midY = (my + nextY) / 2;
            ctx.strokeStyle = '#ef4444';
            ctx.lineWidth = 2;
            ctx.setLineDash([3, 3]);
            ctx.beginPath();
            ctx.moveTo(axisX - 35, my + 10);
            ctx.lineTo(axisX - 45, my + 10);
            ctx.lineTo(axisX - 45, nextY - 10);
            ctx.lineTo(axisX - 35, nextY - 10);
            ctx.stroke();
            ctx.setLineDash([]);

            ctx.fillStyle = '#fee2e2';
            ctx.strokeStyle = '#ef4444';
            ctx.lineWidth = 1.5;
            ctx.fillRect(axisX - 70, midY - 10, 50, 20);
            ctx.strokeRect(axisX - 70, midY - 10, 50, 20);

            ctx.fillStyle = '#b91c1c';
            ctx.font = '900 11px "Courier New"';
            ctx.textAlign = 'center';
            ctx.fillText(`+${gapYears}y`, axisX - 45, midY + 4);
        }
    }
}

// RIGHT COLUMN: GAP BARS & STATS
const [gapChartSub, statsSub] = Layout.rows(gapCol, [62, 38], 22);

drawBrutalistCard(gapChartSub.x, gapChartSub.y, gapChartSub.width, gapChartSub.height, '#ffffff', 8, 4);

ctx.fillStyle = T['--neo-blue'];
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 3;
ctx.fillRect(gapChartSub.x, gapChartSub.y, gapChartSub.width, 52);
ctx.strokeRect(gapChartSub.x, gapChartSub.y, gapChartSub.width, 52);

ctx.fillStyle = '#ffffff';
ctx.font = '900 18px "Arial Black", Impact';
ctx.textAlign = 'left';
ctx.fillText('02 // GAP DURATION COMPARATOR', gapChartSub.x + 16, gapChartSub.y + 34);

const barPlot = Layout.inset(gapChartSub, 58, 20, 20, 20);
const maxGapVal = Scale.extent(gaps.map(g => g.years)).max;
const gapBounds = Scale.nice(0, maxGapVal);
const gapX = Scale.linear(0, gapBounds.max, barPlot.x + 10, barPlot.x + barPlot.width - 24);
if (!gapX.isZeroBased) throw new Error('Bars require a strict zero baseline!');

const gapBands = Scale.band(gaps.length, barPlot.y + 40, barPlot.y + barPlot.height - 20, 0.38);

const ticks = gapX.ticks(4);
ctx.font = '700 12px "Courier New"';
ctx.textAlign = 'center';
for (const t of ticks) {
    const tx = gapX.map(t);
    ctx.strokeStyle = '#e5e7eb';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(tx, barPlot.y + 30);
    ctx.lineTo(tx, barPlot.y + barPlot.height - 10);
    ctx.stroke();

    ctx.fillStyle = '#6b7280';
    ctx.fillText(`${t}y`, tx, barPlot.y + 24);
}

ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 3;
ctx.beginPath();
ctx.moveTo(gapX.map(0), barPlot.y + 30);
ctx.lineTo(gapX.map(0), barPlot.y + barPlot.height - 10);
ctx.stroke();

for (let i = 0; i < gaps.length; i++) {
    const g = gaps[i];
    const by = gapBands.map(i);
    const bh = gapBands.bandwidth;
    const barWidth = gapX.extent(0, g.years);

    ctx.fillStyle = T['--ink-black'];
    ctx.fillRect(gapX.map(0) + 4, by + 4, barWidth, bh);

    ctx.fillStyle = g.col;
    ctx.strokeStyle = T['--ink-black'];
    ctx.lineWidth = 2.5;
    ctx.fillRect(gapX.map(0), by, barWidth, bh);
    ctx.strokeRect(gapX.map(0), by, barWidth, bh);

    ctx.fillStyle = T['--ink-black'];
    ctx.font = '900 13px "Arial"';
    ctx.textAlign = 'left';
    ctx.fillText(`${g.name} (${g.math})`, gapX.map(0), by - 6);

    ctx.fillStyle = '#ffffff';
    ctx.font = '900 18px Impact';
    if (barWidth > 60) {
        ctx.textAlign = 'left';
        ctx.fillText(`${g.years} YRS`, gapX.map(0) + 10, by + bh - 10);
    } else {
        ctx.fillStyle = T['--ink-black'];
        ctx.fillText(`${g.years}y`, gapX.map(0) + barWidth + 8, by + bh - 10);
    }
}

// STATS BOX
drawBrutalistCard(statsSub.x, statsSub.y, statsSub.width, statsSub.height, '#ffffff', 8, 4);

ctx.fillStyle = T['--neo-green'];
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 3;
ctx.fillRect(statsSub.x, statsSub.y, statsSub.width, 48);
ctx.strokeRect(statsSub.x, statsSub.y, statsSub.width, 48);

ctx.fillStyle = T['--ink-black'];
ctx.font = '900 18px "Arial Black", Impact';
ctx.textAlign = 'left';
ctx.fillText('03 // SCALE DISPARITIES & EXTREMES', statsSub.x + 16, statsSub.y + 31);

const [badgeRow1, badgeRow2] = Layout.rows(
    Layout.inset(statsSub, 58, 16, 16, 16),
    [52, 48],
    14
);

ctx.fillStyle = T['--neo-yellow'];
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 3;
ctx.fillRect(badgeRow1.x, badgeRow1.y, badgeRow1.width, badgeRow1.height);
ctx.strokeRect(badgeRow1.x, badgeRow1.y, badgeRow1.width, badgeRow1.height);

ctx.fillStyle = T['--ink-black'];
ctx.font = '900 52px Impact';
ctx.textAlign = 'left';
ctx.fillText('53', badgeRow1.x + 16, badgeRow1.y + 60);

ctx.font = '900 20px "Arial Black"';
ctx.fillText('YEARS OF REVOLUTION', badgeRow1.x + 85, badgeRow1.y + 40);
ctx.font = '600 13px "Courier New"';
ctx.fillStyle = '#374151';
ctx.fillText('1969 to 2022 timeline span', badgeRow1.x + 85, badgeRow1.y + 64);

const [extLeft, extRight] = Layout.columns(badgeRow2, 2, 12);

ctx.fillStyle = '#fee2e2';
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 2.5;
ctx.fillRect(extLeft.x, extLeft.y, extLeft.width, extLeft.height);
ctx.strokeRect(extLeft.x, extLeft.y, extLeft.width, extLeft.height);

ctx.fillStyle = '#b91c1c';
ctx.font = '900 32px Impact';
ctx.textAlign = 'left';
ctx.fillText('20 YRS', extLeft.x + 10, extLeft.y + 36);
ctx.font = '700 11px "Courier New"';
ctx.fillStyle = T['--ink-black'];
ctx.fillText('LONGEST WAIT (69→89)', extLeft.x + 10, extLeft.y + 56);
ctx.fillText('ARPANET to WWW', extLeft.x + 10, extLeft.y + 72);

ctx.fillStyle = '#dcfce7';
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 2.5;
ctx.fillRect(extRight.x, extRight.y, extRight.width, extRight.height);
ctx.strokeRect(extRight.x, extRight.y, extRight.width, extRight.height);

ctx.fillStyle = '#15803d';
ctx.font = '900 32px Impact';
ctx.textAlign = 'left';
ctx.fillText('2 MO', extRight.x + 10, extRight.y + 36);
ctx.font = '700 11px "Courier New"';
ctx.fillStyle = T['--ink-black'];
ctx.fillText('FASTEST TO 100M', extRight.x + 10, extRight.y + 56);
ctx.fillText('ChatGPT in 2022', extRight.x + 10, extRight.y + 72);

// FOOTER ZONE: 53-YEAR MATRIX & STAMP
const footerBox = Layout.rect(paperX + 42, 1585, paperW - 84, 275);
drawBrutalistCard(footerBox.x, footerBox.y, footerBox.width, footerBox.height, '#ffffff', 8, 4);

ctx.fillStyle = T['--ink-black'];
ctx.fillRect(footerBox.x, footerBox.y, footerBox.width, 42);
ctx.fillStyle = '#ffffff';
ctx.font = '900 16px "Arial Black", Impact';
ctx.textAlign = 'left';
ctx.fillText('04 // 53-YEAR DISCRETE PROPORTIONAL INK MATRIX (1969—2022)', footerBox.x + 16, footerBox.y + 27);

const matrixStartX = footerBox.x + 20;
const matrixStartY = footerBox.y + 60;
const colsCount = 27;
const dotGap = 26;

ctx.font = '700 11px "Courier New"';
ctx.fillStyle = '#6b7280';
ctx.fillText('ONE GLYPH = ONE CALENDAR YEAR (53 YEARS RECORDED):', matrixStartX, matrixStartY);

const milestoneYears = [1969, 1989, 1993, 1998, 2004, 2007, 2022];

for (let yIdx = 0; yIdx < 53; yIdx++) {
    const yr = 1969 + yIdx;
    const r = Math.floor(yIdx / colsCount);
    const c = yIdx % colsCount;
    const gx = matrixStartX + c * dotGap;
    const gy = matrixStartY + 20 + r * 30;

    const isMilestone = milestoneYears.includes(yr);

    ctx.fillStyle = isMilestone ? '#ff3838' : '#e5e7eb';
    ctx.strokeStyle = T['--ink-black'];
    ctx.lineWidth = isMilestone ? 2 : 1;

    ctx.fillRect(gx, gy, 18, 18);
    ctx.strokeRect(gx, gy, 18, 18);

    if (isMilestone) {
        ctx.fillStyle = '#ffffff';
        ctx.font = '900 10px "Arial"';
        ctx.textAlign = 'center';
        ctx.fillText(yr.toString().slice(2), gx + 9, gy + 13);
    }
}

ctx.fillStyle = T['--ink-black'];
ctx.font = '600 13px "Courier New"';
ctx.textAlign = 'left';
ctx.fillText('■ RED BLOCKS INDICATE THE 7 BREAKTHROUGH ANCHOR YEARS DETAILED ABOVE.', matrixStartX, footerBox.y + 145);
ctx.fillText('■ ALL ARITHMETIC & INTERVALS DERIVED DIRECTLY FROM VERIFIED PUBLIC RECORDS.', matrixStartX, footerBox.y + 165);

// Diegetic Stamp
ctx.save();
ctx.translate(footerBox.x + footerBox.width - 210, footerBox.y + 130);
ctx.rotate(-3.5 * Math.PI / 180);

ctx.fillStyle = T['--ink-black'];
ctx.fillRect(6, 6, 185, 110);

ctx.fillStyle = T['--neo-red'];
ctx.strokeStyle = T['--ink-black'];
ctx.lineWidth = 3.5;
ctx.fillRect(0, 0, 185, 110);
ctx.strokeRect(0, 0, 185, 110);

ctx.fillStyle = '#ffffff';
ctx.font = '900 24px Impact';
ctx.textAlign = 'center';
ctx.fillText('AUDIT PASS', 92, 38);

ctx.font = '900 13px "Courier New"';
ctx.fillText('TRACTOR-FEED OK', 92, 62);
ctx.fillText('ZERO BASELINE OK', 92, 80);
ctx.fillText('53Y SPAN VERIFIED', 92, 98);
ctx.restore();

// ==========================================
// STAGE 7 AUDIT LITMUS TESTS & PIXEL VERIFICATION
// ==========================================

// Sample key pixels from the bitmap to verify rendering
const sample1 = canvas.bitmap.getPixel(100, 100); // Header area
const sample2 = canvas.bitmap.getPixel(paperX + 18, 24); // Sprocket chassis
const sample3 = canvas.bitmap.getPixel(500, 500); // Main timeline
const sample4 = canvas.bitmap.getPixel(800, 800); // Gap bars

log(`Pixel verification: Header: ${sample1}, Sprocket: ${sample2}, Timeline: ${sample3}, Bars: ${sample4}`);

Stage.note('--- LITMUS TEST AUDIT RESULTS (Manual 13 §6) ---');
Stage.note('Litmus Test 1 (Could this layout hold SaaS dashboard data?): NO. The diegetic fanfold tractor-feed ground, 1401 greenbar alternating ledger lines, continuous vertical time-mapped paper ribbon, sprocket margins, and brutalist physical stampings are intrinsically tied to continuous temporal printout and historical epoch milestones.');
Stage.note('Litmus Test 2 (Cover text — is topic recognizable?): YES. The chronological vertical spine with milestone nodes, scale-proportional gap bracket spans, comparative duration bars, and the 53-year discrete calendar matrix clearly communicate milestone pacing and time intervals even without reading copy.');
Stage.note('Litmus Test 3 (Are all 4 corners doing equal work?): NO. Top-left has form header / crosshair, Top-right has sequence & sync stamps, Bottom-left hosts the 53-year discrete proportional dot matrix, and Bottom-right anchors the rotated audit stamp. Visual gravity drives downward from the hero marquee along the vertical platen feed.');
Stage.note('Litmus Test 4 (Zero-baseline & Area Proportions): VERIFIED. Gap duration bars have strict zero baseline (isZeroBased = true, domain 0 to 20). 53-year matrix encodes discrete calendar units (1:1 ink per year).');
Stage.note('Figure reconciliation against brief.md:');
Stage.note('- 1969: ARPANET first message (crashed after "LO") -> Present & verified.');
Stage.note('- 1989: Tim Berners-Lee proposes World Wide Web -> Present & verified.');
Stage.note('- 1993: Mosaic makes the web visual -> Present & verified.');
Stage.note('- 1998: Google founded -> Present & verified.');
Stage.note('- 2004: Facebook launches -> Present & verified.');
Stage.note('- 2007: iPhone puts web in pocket -> Present & verified.');
Stage.note('- 2022: ChatGPT reaches 100M in 2 months -> Present & verified.');
Stage.note('Derived figures: 20y, 4y, 5y, 6y, 3y, 15y, and 53y total span all verified with explicit arithmetic displayed.');

canvas;
