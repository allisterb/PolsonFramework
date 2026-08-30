Stage.begin('Detail');

Stage.note('Constructing high-fidelity Neo-Brutalist Infographic:');
Stage.note('- Ground: Continuous fanfold line-printer paper with green-bar zebra striping, punched tractor margins, and sprocket tracking.');
Stage.note('- Visual Language: 4-5px solid black rules, 6-8px hard offset geometric shadows, high-chroma flats, tightly tracked grotesque display type.');
Stage.note('- Hero Spine: 1020px vertical scale (1969 to 2022) with exact spatial gaps, milestone badges, and drought brackets.');
Stage.note('- Comparative Analysis: Zero-based horizontal gap bar chart, 18-year explosion callout, rotated status stamp.');
Stage.note('- Macro Breakdown: 53-unit discrete waffle grid (1 unit = 1 year) and big-number summary callouts.');

log('=== STAGE 6: FULL DETAIL RENDER ===');

const width = 1080;
const height = 1920;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

// --- 1. GROUND & TRACTOR MARGINS ---
ctx.fillStyle = '#F5F2EB';
ctx.fillRect(0, 0, width, height);

// Alternating green-bar stripes (32px tall, period 64px)
ctx.fillStyle = '#EAF2E7';
for (let y = 0; y < height; y += 64) {
  ctx.fillRect(0, y, width, 32);
}

// Tractor feed margins (54px each side)
const tmW = 54;
ctx.fillStyle = '#E2DDD1';
ctx.fillRect(0, 0, tmW, height);
ctx.fillRect(width - tmW, 0, tmW, height);

// Divider black rules
ctx.strokeStyle = '#000000';
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(tmW, 0);
ctx.lineTo(tmW, height);
ctx.moveTo(width - tmW, 0);
ctx.lineTo(width - tmW, height);
ctx.stroke();

// Sprocket holes with double rings
for (let y = 24; y < height; y += 48) {
  // Left hole
  ctx.fillStyle = '#CFC8BA';
  ctx.beginPath();
  ctx.arc(tmW / 2, y, 11, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 2.5;
  ctx.stroke();
  
  // Right hole
  ctx.beginPath();
  ctx.arc(width - tmW / 2, y, 11, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

// Sprocket track numbers (Consolas micro-text)
ctx.fillStyle = '#7A7365';
ctx.font = '700 10px Consolas, monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
for (let y = 48; y < height; y += 96) {
  const lineNum = String(Math.floor(y / 16)).padStart(4, '0');
  ctx.fillText(lineNum, tmW / 2, y);
  ctx.fillText(lineNum, width - tmW / 2, y);
}

// Perforation horizontal dash lines at y=250, y=1430
const drawPerforation = (py) => {
  ctx.strokeStyle = 'rgba(0,0,0,0.3)';
  ctx.lineWidth = 2;
  ctx.setLineDash([8, 8]);
  ctx.beginPath();
  ctx.moveTo(tmW, py);
  ctx.lineTo(width - tmW, py);
  ctx.stroke();
  ctx.setLineDash([]);
  
  ctx.fillStyle = '#8A8275';
  ctx.font = '700 10px Consolas, monospace';
  ctx.textAlign = 'left';
  ctx.fillText('--- [ TEAR ALONG PERFORATION ] -----------------------------------------------------------------------------------------', tmW + 12, py - 6);
};
drawPerforation(250);
drawPerforation(1430);

// Helper function: Neo-Brutalist Box with Hard Shadow
const drawBrutalistBox = (x, y, w, h, fill, shadowOffset = 6, borderWidth = 4) => {
  // Shadow
  ctx.fillStyle = '#000000';
  ctx.fillRect(x + shadowOffset, y + shadowOffset, w, h);
  // Box
  ctx.fillStyle = fill;
  ctx.fillRect(x, y, w, h);
  // Border
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = borderWidth;
  ctx.strokeRect(x, y, w, h);
};

// --- 2. HEADER SECTION (y: 26 to 240) ---
// Terminal metadata top bar
ctx.fillStyle = '#000000';
ctx.font = '700 12px Consolas, monospace';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillText('JOB: #INF-01 // SPOOL: 1969-2022 // TOTAL RUN: 53 YEARS // PAPER: CONTINUOUS FANFOLD', tmW + 16, 26);

// Main Hero Title Card
const headerX = tmW + 14;
const headerY = 48;
const headerW = width - (tmW * 2) - 28; // 944px
const headerH = 190;

drawBrutalistBox(headerX, headerY, headerW, headerH, '#FFE500', 8, 5);

// Title text inside header
ctx.fillStyle = '#000000';
ctx.font = '900 48px Impact, Arial Black, sans-serif';
ctx.letterSpacing = '-0.02em';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillText('THE INTERNET WAS NEVER ON SCHEDULE', headerX + 20, headerY + 16);

// Top-right Pill Tag
const pillW = 260;
const pillH = 34;
const pillX = headerX + headerW - pillW - 20;
const pillY = headerY + 22;
drawBrutalistBox(pillX, pillY, pillW, pillH, '#00E5FF', 4, 3);
ctx.fillStyle = '#000000';
ctx.font = '700 12px Consolas, monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
ctx.fillText('53-YEAR RUN // 7 MILESTONES', pillX + pillW / 2, pillY + pillH / 2);

// Lede / Claim Subtitle Box (White inset banner)
const claimBoxY = headerY + 82;
const claimBoxH = 88;
const claimBoxW = headerW - 40;
drawBrutalistBox(headerX + 20, claimBoxY, claimBoxW, claimBoxH, '#FFFFFF', 5, 3.5);

// Claim chip
drawBrutalistBox(headerX + 32, claimBoxY + 12, 70, 22, '#FF3B30', 2, 2);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 11px Arial, sans-serif';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
ctx.fillText('CLAIM', headerX + 32 + 35, claimBoxY + 12 + 11);

// Claim Sentence (from brief.md)
ctx.fillStyle = '#000000';
ctx.font = '700 20px Arial, sans-serif';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillText('Progress is constant — it never stopped coming,', headerX + 114, claimBoxY + 14);
ctx.fillText('but it never once came on schedule.', headerX + 114, claimBoxY + 40);

ctx.font = '700 12px Consolas, monospace';
ctx.fillStyle = '#555555';
ctx.fillText('AUDIENCE: GENERAL // SCALE: STRICT 1:1 LINEAR TIME // 1969 - 2022', headerX + 32, claimBoxY + 68);


// --- 3. MIDDLE SECTION (y: 266 to 1410) ---

// Split Mid Section into Left Timeline (550px) and Right Gap Analysis (370px)
const midTop = 270;
const leftX = tmW + 14;
const leftW = 546;
const rightX = leftX + leftW + 20; // 634
const rightW = width - tmW - 14 - rightX; // 378

// Left Container Box (Timeline Panel)
const timelineH = 1140;
drawBrutalistBox(leftX, midTop, leftW, timelineH, '#FFFFFF', 8, 4.5);

// Timeline Panel Header
drawBrutalistBox(leftX + 14, midTop + 14, leftW - 28, 38, '#000000', 0, 0);
ctx.fillStyle = '#FFE500';
ctx.font = '900 16px Impact, Arial, sans-serif';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.fillText('● THE TRUE-SCALE TIMELINE (1969 → 2022)', leftX + 26, midTop + 33);
ctx.fillStyle = '#FFFFFF';
ctx.font = '700 12px Consolas, monospace';
ctx.textAlign = 'right';
ctx.fillText('19.2 PX / YEAR', leftX + leftW - 26, midTop + 33);

// Scales for Timeline Spine
const spineYTop = midTop + 90;
const spineYBottom = midTop + timelineH - 70;
const spineSpan = spineYBottom - spineYTop; // 980 px
const timeScale = Scale.linear(1969, 2022, spineYTop, spineYBottom);

const spineX = leftX + 72;

// Draw continuous vertical spine rule
ctx.strokeStyle = '#000000';
ctx.lineWidth = 6;
ctx.beginPath();
ctx.moveTo(spineX, spineYTop - 10);
ctx.lineTo(spineX, spineYBottom + 10);
ctx.stroke();

// Draw 5-year tick marks on the spine
for (let yr = 1970; yr <= 2020; yr += 5) {
  const ty = timeScale.map(yr);
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(spineX - 12, ty);
  ctx.lineTo(spineX + 12, ty);
  ctx.stroke();
  
  ctx.fillStyle = '#777777';
  ctx.font = '700 10px Consolas, monospace';
  ctx.textAlign = 'right';
  ctx.textBaseline = 'middle';
  ctx.fillText(String(yr), spineX - 16, ty);
}

// 7 Historical Milestones (from brief.md)
const milestones = [
  {
    year: 1969,
    color: '#FF3B30',
    title: 'ARPANET FIRST MESSAGE',
    detail: 'Sends first message; crashed after "LO"',
    tag: 'FIRST PACKET SENT'
  },
  {
    year: 1989,
    color: '#FFE500',
    title: 'WORLD WIDE WEB PROPOSAL',
    detail: 'Tim Berners-Lee proposes the Web',
    tag: 'INFORMATION MANAGEMENT'
  },
  {
    year: 1993,
    color: '#00E676',
    title: 'MOSAIC WEB BROWSER',
    detail: 'Mosaic makes the web visual',
    tag: 'THE WEB GOES GRAPHICAL'
  },
  {
    year: 1998,
    color: '#00E5FF',
    title: 'GOOGLE IS FOUNDED',
    detail: 'PageRank organizes web search',
    tag: 'THE SEARCH REVOLUTION'
  },
  {
    year: 2004,
    color: '#FF2A85',
    title: 'FACEBOOK LAUNCHES',
    detail: 'The social era begins (3B+ humans)',
    tag: 'SOCIAL ERA BEGINS'
  },
  {
    year: 2007,
    color: '#FF6D00',
    title: 'THE IPHONE ANNOUNCED',
    detail: 'Puts the web in every pocket',
    tag: 'MOBILE BROADBAND'
  },
  {
    year: 2022,
    color: '#7C4DFF',
    textColor: '#FFFFFF',
    title: 'CHATGPT REACHES 100M',
    detail: '100M users reached in 2 months',
    tag: 'FASTEST 100M IN HISTORY'
  }
];

// Draw Lull Brackets behind milestones
// 1. 20-Year Lull (1969 to 1989)
const lull1Y1 = timeScale.map(1969) + 26;
const lull1Y2 = timeScale.map(1989) - 26;
const lull1H = lull1Y2 - lull1Y1;

// Saturated shaded lull corridor
ctx.fillStyle = 'rgba(255, 59, 48, 0.08)';
ctx.fillRect(spineX + 16, lull1Y1, leftW - 110, lull1H);

// Dimension bracket line
ctx.strokeStyle = '#FF3B30';
ctx.lineWidth = 3;
ctx.setLineDash([4, 4]);
ctx.strokeRect(spineX + 16, lull1Y1, leftW - 110, lull1H);
ctx.setLineDash([]);

// Lull badge chip
const lull1ChipY = lull1Y1 + lull1H / 2 - 20;
drawBrutalistBox(spineX + 40, lull1ChipY, 360, 42, '#FF3B30', 4, 3);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 15px Impact, Arial, sans-serif';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
ctx.fillText('▼ 20-YEAR DESERT: WAITING FOR THE WEB (1969-1989) ▼', spineX + 220, lull1ChipY + 14);
ctx.font = '700 11px Consolas, monospace';
ctx.fillText('37.7% OF ENTIRE TIMELINE IN A SINGLE GAP', spineX + 220, lull1ChipY + 30);


// 2. 15-Year Lull (2007 to 2022)
const lull2Y1 = timeScale.map(2007) + 26;
const lull2Y2 = timeScale.map(2022) - 26;
const lull2H = lull2Y2 - lull2Y1;

ctx.fillStyle = 'rgba(255, 109, 0, 0.08)';
ctx.fillRect(spineX + 16, lull2Y1, leftW - 110, lull2H);

ctx.strokeStyle = '#FF6D00';
ctx.lineWidth = 3;
ctx.setLineDash([4, 4]);
ctx.strokeRect(spineX + 16, lull2Y1, leftW - 110, lull2H);
ctx.setLineDash([]);

const lull2ChipY = lull2Y1 + lull2H / 2 - 20;
drawBrutalistBox(spineX + 40, lull2ChipY, 360, 42, '#FF6D00', 4, 3);
ctx.fillStyle = '#000000';
ctx.font = '900 15px Impact, Arial, sans-serif';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
ctx.fillText('▼ 15-YEAR INCUBATION: PHONE TO AI (2007-2022) ▼', spineX + 220, lull2ChipY + 14);
ctx.font = '700 11px Consolas, monospace';
ctx.fillText('28.3% OF TIMELINE BEFORE NEXT LEAP', spineX + 220, lull2ChipY + 30);


// Draw the Milestone Cards
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const my = timeScale.map(m.year);
  
  // Big anchor circle on spine
  ctx.fillStyle = m.color;
  ctx.beginPath();
  ctx.arc(spineX, my, 12, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3.5;
  ctx.stroke();
  
  // Year numeral tag on left
  drawBrutalistBox(spineX - 62, my - 14, 52, 28, '#000000', 2, 2);
  ctx.fillStyle = '#FFE500';
  ctx.font = '900 13px Consolas, monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(String(m.year), spineX - 36, my);
  
  // Horizontal connector line
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(spineX + 12, my);
  ctx.lineTo(spineX + 32, my);
  ctx.stroke();
  
  // Milestone Card Box
  const cardX = spineX + 32;
  const cardW = leftW - 124;
  const cardH = 50;
  const cardY = my - 25;
  
  drawBrutalistBox(cardX, cardY, cardW, cardH, m.color, 4, 3);
  
  // Title & Detail
  ctx.fillStyle = m.textColor || '#000000';
  ctx.font = '900 15px Impact, Arial, sans-serif';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';
  ctx.fillText(m.title, cardX + 10, cardY + 7);
  
  ctx.font = '700 11px Arial, sans-serif';
  ctx.fillText(m.detail, cardX + 10, cardY + 28);
}


// --- 4. RIGHT SECTION (y: 270 to 1410) ---

// Box 1: Gap Duration Bar Chart (y: 270 to 770)
const gapBoxH = 490;
drawBrutalistBox(rightX, midTop, rightW, gapBoxH, '#FFFFFF', 8, 4.5);

// Header bar
drawBrutalistBox(rightX + 14, midTop + 14, rightW - 28, 38, '#000000', 0, 0);
ctx.fillStyle = '#00E5FF';
ctx.font = '900 16px Impact, Arial, sans-serif';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.fillText('● THE 6 GAPS (WAITING PERIODS)', rightX + 24, midTop + 33);

// Bar Chart Scales
const barPlotX = rightX + 24;
const barPlotW = rightW - 48; // 330px
const barPlotTop = midTop + 72;
const barPlotH = 340;

const gapData = [
  { label: '1969→89: ARPANET → Web', years: 20, color: '#FF3B30', desc: '20 YEARS (Longest Drought)' },
  { label: '2007→22: iPhone → ChatGPT', years: 15, color: '#FF6D00', desc: '15 YEARS (Second Drought)' },
  { label: '1998→04: Google → Facebook', years: 6, color: '#00E676', desc: '6 YEARS (Search to Social)' },
  { label: '1993→98: Mosaic → Google', years: 5, color: '#00E676', desc: '5 YEARS (Browser to Search)' },
  { label: '1989→93: Web → Mosaic', years: 4, color: '#00E676', desc: '4 YEARS (Spec to Browser)' },
  { label: '2004→07: Facebook → iPhone', years: 3, color: '#00E676', desc: '3 YEARS (Social to Mobile)' }
];

const gapChartScale = Scale.linear(0, 20, barPlotX, barPlotX + barPlotW - 30);
if (!gapChartScale.isZeroBased) throw new Error('Gap bars must have zero baseline');

const gapBandScale = Scale.band(gapData.length, barPlotTop, barPlotTop + barPlotH, 0.28);

// Draw zero baseline vertical rule
ctx.strokeStyle = '#000000';
ctx.lineWidth = 3;
ctx.beginPath();
ctx.moveTo(barPlotX, barPlotTop);
ctx.lineTo(barPlotX, barPlotTop + barPlotH + 10);
ctx.stroke();

// Draw bars
for (let i = 0; i < gapData.length; i++) {
  const g = gapData[i];
  const by = gapBandScale.map(i);
  const bw = gapBandScale.bandwidth;
  const barLen = gapChartScale.extent(0, g.years);
  
  // Bar label
  ctx.fillStyle = '#000000';
  ctx.font = '700 12px Arial, sans-serif';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'bottom';
  ctx.fillText(g.label, barPlotX, by - 3);
  
  // Bar shadow
  ctx.fillStyle = '#000000';
  ctx.fillRect(barPlotX + 4, by + 4, barLen, bw);
  
  // Bar fill
  ctx.fillStyle = g.color;
  ctx.fillRect(barPlotX, by, barLen, bw);
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 2.5;
  ctx.strokeRect(barPlotX, by, barLen, bw);
  
  // Value label
  ctx.fillStyle = '#000000';
  ctx.font = '900 13px Impact, Arial, sans-serif';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillText(g.years + ' YRS', barPlotX + barLen + 8, by + bw / 2);
}

// X-axis ticks
const axisY = barPlotTop + barPlotH + 10;
ctx.strokeStyle = '#000000';
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(barPlotX, axisY);
ctx.lineTo(barPlotX + barPlotW - 30, axisY);
ctx.stroke();

for (const tick of [0, 5, 10, 15, 20]) {
  const tx = gapChartScale.map(tick);
  ctx.beginPath();
  ctx.moveTo(tx, axisY);
  ctx.lineTo(tx, axisY + 6);
  ctx.stroke();
  
  ctx.fillStyle = '#000000';
  ctx.font = '700 10px Consolas, monospace';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'top';
  ctx.fillText(tick + 'y', tx, axisY + 8);
}


// Box 2: The 18-Year Explosion Callout (y: 780 to 1100)
const burstY = midTop + gapBoxH + 20;
const burstH = 320;
drawBrutalistBox(rightX, burstY, rightW, burstH, '#FFE500', 8, 4.5);

// Burst Header Tag
drawBrutalistBox(rightX + 16, burstY + 16, 170, 26, '#000000', 2, 2);
ctx.fillStyle = '#FFE500';
ctx.font = '900 12px Arial, sans-serif';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
ctx.fillText('THE GOLDEN ERA', rightX + 16 + 85, burstY + 16 + 13);

ctx.fillStyle = '#000000';
ctx.font = '900 32px Impact, Arial Black, sans-serif';
ctx.textAlign = 'left';
ctx.textBaseline = 'top';
ctx.fillText('THE 18-YEAR BURST', rightX + 16, burstY + 50);

ctx.font = '700 14px Arial, sans-serif';
ctx.fillText('Between 1989 and 2007, five distinct', rightX + 16, burstY + 90);
ctx.fillText('world-changing revolutions landed:', rightX + 16, burstY + 110);

// Cluster list items
const burstItems = [
  '1989: Tim Berners-Lee proposes Web',
  '1993: Mosaic adds images (4y gap)',
  '1998: Google reinvents search (5y gap)',
  '2004: Facebook begins social era (6y gap)',
  '2007: iPhone puts web in pocket (3y gap)'
];

ctx.font = '700 12px Consolas, monospace';
let biy = burstY + 140;
for (const bi of burstItems) {
  ctx.fillStyle = '#000000';
  ctx.fillText('▶ ' + bi, rightX + 16, biy);
  biy += 22;
}

// Inset stat chip
drawBrutalistBox(rightX + 16, burstY + 256, rightW - 32, 48, '#FFFFFF', 3, 2.5);
ctx.fillStyle = '#000000';
ctx.font = '900 14px Impact, Arial, sans-serif';
ctx.textAlign = 'center';
ctx.textBaseline = 'middle';
ctx.fillText('5 REVOLUTIONS IN 18 YEARS = 3.6 YR AVG GAP!', rightX + rightW / 2, burstY + 280);


// Box 3: Rotated Neo-Brutalist Stamp (Crossing boundary / tension rule)
ctx.save();
const stampX = rightX + rightW / 2;
const stampY = midTop + timelineH - 120;
ctx.translate(stampX, stampY);
ctx.rotate(-4 * Math.PI / 180);

const stW = rightW + 20;
const stH = 120;
drawBrutalistBox(-stW / 2, -stH / 2, stW, stH, '#FF2A85', 6, 4);

ctx.fillStyle = '#FFFFFF';
ctx.font = '900 24px Impact, Arial, sans-serif';
ctx.textAlign = 'center';
ctx.textBaseline = 'top';
ctx.fillText('★ 66.0% OF TIME WAS WAITING ★', 0, -stH / 2 + 16);

ctx.font = '700 14px Arial, sans-serif';
ctx.fillText('35 of 53 years spent in just 2 lulls', 0, -stH / 2 + 50);

ctx.font = '700 12px Consolas, monospace';
ctx.fillText('1969-89 (20y) + 2007-22 (15y) = 35 YEARS', 0, -stH / 2 + 76);

ctx.restore();


// --- 5. BOTTOM SECTION: MACRO 53-YEAR BREAKDOWN (y: 1440 to 1860) ---
const botTop = 1446;
const botW = width - (tmW * 2) - 28;
const botH = 414;
const botX = tmW + 14;

drawBrutalistBox(botX, botTop, botW, botH, '#FFFFFF', 8, 4.5);

// Header banner
drawBrutalistBox(botX + 16, botTop + 16, botW - 32, 40, '#000000', 0, 0);
ctx.fillStyle = '#FFE500';
ctx.font = '900 18px Impact, Arial, sans-serif';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.fillText('● 53-YEAR TOTAL TIMELINE ALLOCATION (1969 - 2022)', botX + 28, botTop + 36);

ctx.fillStyle = '#FFFFFF';
ctx.font = '700 12px Consolas, monospace';
ctx.textAlign = 'right';
ctx.fillText('1 BLOCK = 1 CALENDAR YEAR (53 TOTAL)', botX + botW - 28, botTop + 36);

// 53-Year Segmented Waffle / Matrix
// 53 squares arranged in 2 horizontal rows of 27 columns (53 squares total)
const wColCount = 27;
const wRowCount = 2;
const wCellSize = 28;
const wGap = 5;
const waffleStartX = botX + 28;
const waffleStartY = botTop + 72;

let yearIndex = 0;
for (let r = 0; r < wRowCount; r++) {
  for (let c = 0; c < wColCount; c++) {
    if (yearIndex >= 53) break;
    const yr = 1969 + yearIndex;
    const cx = waffleStartX + c * (wCellSize + wGap);
    const cy = waffleStartY + r * (wCellSize + wGap);
    
    // Determine segment color
    let fill = '#00E676'; // Boom era (1989-2007)
    if (yr < 1989) fill = '#FF3B30'; // ARPANET 20y lull
    else if (yr >= 2007 && yr < 2022) fill = '#FF6D00'; // iPhone to AI 15y lull
    else if (yr === 2022) fill = '#7C4DFF'; // ChatGPT milestone
    
    // Draw waffle cell with hard shadow
    ctx.fillStyle = '#000000';
    ctx.fillRect(cx + 2, cy + 2, wCellSize, wCellSize);
    
    ctx.fillStyle = fill;
    ctx.fillRect(cx, cy, wCellSize, wCellSize);
    ctx.strokeStyle = '#000000';
    ctx.lineWidth = 2;
    ctx.strokeRect(cx, cy, wCellSize, wCellSize);
    
    yearIndex++;
  }
}

// Legend for Waffle Grid
const legY = waffleStartY + (wRowCount * (wCellSize + wGap)) + 8;
const legItems = [
  { color: '#FF3B30', label: '1969-1989: ARPANET TO WEB LULL (20 YRS / 37.7%)' },
  { color: '#00E676', label: '1989-2007: THE 5-REVOLUTION BURST (18 YRS / 34.0%)' },
  { color: '#FF6D00', label: '2007-2022: IPHONE TO AI LULL (15 YRS / 28.3%)' }
];

let legX = botX + 28;
for (const leg of legItems) {
  ctx.fillStyle = leg.color;
  ctx.fillRect(legX, legY, 14, 14);
  ctx.strokeStyle = '#000000';
  ctx.lineWidth = 1.5;
  ctx.strokeRect(legX, legY, 14, 14);
  
  ctx.fillStyle = '#000000';
  ctx.font = '700 11px Consolas, monospace';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillText(leg.label, legX + 20, legY + 7);
  
  legX += 300;
}


// Bottom 3 Hero Stat Callout Cards
const calloutY = botTop + 180;
const calloutH = 200;
const calloutW = (botW - 48 - 32) / 3; // ~288px each

// Callout 1: 35 YEARS
const c1X = botX + 24;
drawBrutalistBox(c1X, calloutY, calloutW, calloutH, '#FF3B30', 6, 3.5);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 14px Consolas, monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'top';
ctx.fillText('TOTAL WAITING TIME', c1X + calloutW / 2, calloutY + 16);

ctx.font = '900 56px Impact, Arial Black, sans-serif';
ctx.fillText('35 YRS', c1X + calloutW / 2, calloutY + 40);

ctx.font = '900 18px Arial, sans-serif';
ctx.fillText('66.0% OF ALL TIME', c1X + calloutW / 2, calloutY + 112);

ctx.font = '700 12px Arial, sans-serif';
ctx.fillText('1989-1969 (20y) + 2022-2007 (15y)', c1X + calloutW / 2, calloutY + 140);
ctx.fillText('= 35 years across 2 quiet lulls', c1X + calloutW / 2, calloutY + 160);


// Callout 2: 18 YEARS
const c2X = c1X + calloutW + 16;
drawBrutalistBox(c2X, calloutY, calloutW, calloutH, '#00E676', 6, 3.5);
ctx.fillStyle = '#000000';
ctx.font = '900 14px Consolas, monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'top';
ctx.fillText('FRENZY PERIOD', c2X + calloutW / 2, calloutY + 16);

ctx.font = '900 56px Impact, Arial Black, sans-serif';
ctx.fillText('18 YRS', c2X + calloutW / 2, calloutY + 40);

ctx.font = '900 18px Arial, sans-serif';
ctx.fillText('34.0% OF TIMELINE', c2X + calloutW / 2, calloutY + 112);

ctx.font = '700 12px Arial, sans-serif';
ctx.fillText('5 Major Paradigm Shifts:', c2X + calloutW / 2, calloutY + 140);
ctx.fillText('Web, Mosaic, Google, FB, iPhone', c2X + calloutW / 2, calloutY + 160);


// Callout 3: 2 MONTHS
const c3X = c2X + calloutW + 16;
drawBrutalistBox(c3X, calloutY, calloutW, calloutH, '#00E5FF', 6, 3.5);
ctx.fillStyle = '#000000';
ctx.font = '900 14px Consolas, monospace';
ctx.textAlign = 'center';
ctx.textBaseline = 'top';
ctx.fillText('FASTEST ADOPTION', c3X + calloutW / 2, calloutY + 16);

ctx.font = '900 56px Impact, Arial Black, sans-serif';
ctx.fillText('2 MOS', c3X + calloutW / 2, calloutY + 40);

ctx.font = '900 18px Arial, sans-serif';
ctx.fillText('CHATGPT TO 100M USERS', c3X + calloutW / 2, calloutY + 112);

ctx.font = '700 12px Arial, sans-serif';
ctx.fillText('From 20-year wait for Web', c3X + calloutW / 2, calloutY + 140);
ctx.fillText('to 60-day sprint for AI', c3X + calloutW / 2, calloutY + 160);


// --- 6. FOOTER LINE-PRINTER STRIP (y: 1880 to 1910) ---
ctx.fillStyle = '#000000';
ctx.font = '700 11px Consolas, monospace';
ctx.textAlign = 'left';
ctx.textBaseline = 'middle';
ctx.fillText('POLSON INFOGRAPHIC WORKFLOW // FORM 13-A // CONTINUOUS FANFOLD // ZERO-BASED ENCODING CONFIRMED', tmW + 16, 1895);

ctx.textAlign = 'right';
ctx.fillText('PAGE 001 OF 001 // VERIFIED ARITHMETIC', width - tmW - 16, 1895);

canvas;
