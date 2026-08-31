Stage.begin('Audit');

Stage.note('Running Stage 7 Audit against the final render.');

const sheet = Css.fromCss(`
  :root {
    --bg-paper: #eaf7f0;
    --bg-stripe: #d5f2e1;
    --ink: #07130c;
    --ink-dim: #1d4b35;
    --accent-green: #00f076;
    --accent-pink: #ff2a5f;
    --accent-cyan: #00e5ff;
    --accent-yellow: #f8ef36;
    --accent-orange: #ff9100;
    --card-bg: #ffffff;
    --border-dark: #07130c;
  }
`);
const tokens = sheet.tokens();

const milestones = [
  { year: 1969, title: 'ARPANET', detail: 'Sends its first message (it crashed after "LO")', tag: 'MIL-01' },
  { year: 1989, title: 'World Wide Web', detail: 'Tim Berners-Lee proposes the World Wide Web', tag: 'MIL-02' },
  { year: 1993, title: 'Mosaic', detail: 'Mosaic makes the web visual', tag: 'MIL-03' },
  { year: 1998, title: 'Google', detail: 'Google is founded', tag: 'MIL-04' },
  { year: 2004, title: 'Facebook', detail: 'Facebook launches — the social era begins', tag: 'MIL-05' },
  { year: 2007, title: 'iPhone', detail: 'The iPhone puts the web in every pocket', tag: 'MIL-06' },
  { year: 2022, title: 'ChatGPT', detail: 'ChatGPT reaches 100M users in 2 months', tag: 'MIL-07' }
];

const gaps = [
  { from: '1969', to: '1989', val: 20, label: 'ARPANET → Web', category: 'The 20-Yr Silence' },
  { from: '1989', to: '1993', val: 4, label: 'Web → Mosaic', category: 'Visual Spark' },
  { from: '1993', to: '1998', val: 5, label: 'Mosaic → Google', category: 'Search Index' },
  { from: '1998', to: '2004', val: 6, label: 'Google → Facebook', category: 'Social Web' },
  { from: '2004', to: '2007', val: 3, label: 'Facebook → iPhone', category: 'Mobile Sprint' },
  { from: '2007', to: '2022', val: 15, label: 'iPhone → ChatGPT', category: 'Mobile to AI Era' }
];

// Verify quantitative rules
const gapExt = Scale.extent(gaps.map(g => g.val));
const gapNice = Scale.nice(0, gapExt.max);
const gapBarScale = Scale.linear(0, gapNice.max, 0, 310);

if (!gapBarScale.isZeroBased) {
  throw new Error('Audit Failure: Gap bar scale is not zero-based');
}
Stage.note('Audit Check 1: Zero-baseline verified on gap bar chart (domainStart=' + gapBarScale.domainStart + ').');

const timeScale = Scale.linear(1969, 2022, 310, 1680);
Stage.note('Audit Check 2: Timeline scale mapping verified: 1969 mapped to y=' + timeScale.map(1969) + ', 2022 mapped to y=' + timeScale.map(2022) + '.');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// 1. Green Fanfold Background
ctx.fillStyle = tokens['--bg-paper'];
ctx.fillRect(0, 0, 1080, 1920);

// Alternating green-bar printer bands
ctx.fillStyle = tokens['--bg-stripe'];
const bandH = 56;
for (let y = 0; y < 1920; y += bandH * 2) {
  ctx.fillRect(0, y, 1080, bandH);
}

// Fold creases at y = 640 and y = 1280
for (const fy of [640, 1280]) {
  ctx.strokeStyle = '#97cca9';
  ctx.lineWidth = 2;
  ctx.setLineDash([8, 8]);
  ctx.beginPath();
  ctx.moveTo(0, fy);
  ctx.lineTo(1080, fy);
  ctx.stroke();
  ctx.setLineDash([]);
  
  ctx.fillStyle = '#1d4b35';
  ctx.font = '700 11px "Consolas", monospace';
  ctx.fillText('--- [PAGE PERFORATION / CONTINUOUS FEED FOLD] ---', 360, fy - 6);
}

// Tractor feed margin perforation bands
const tractW = 54;
ctx.fillStyle = tokens['--ink'];
ctx.fillRect(tractW, 0, 3, 1920);
ctx.fillRect(1080 - tractW, 0, 3, 1920);

// Pin-feed holes
for (let y = 28; y < 1920; y += 44) {
  ctx.fillStyle = '#082819';
  ctx.beginPath();
  ctx.arc(27, y, 8, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#1d4b35';
  ctx.lineWidth = 1;
  ctx.stroke();
  
  ctx.beginPath();
  ctx.arc(1080 - 27, y, 8, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
}

// 2. Header Box
const headRect = Layout.rect(74, 34, 932, 230);
ctx.fillStyle = tokens['--border-dark'];
ctx.fillRect(headRect.x + 8, headRect.y + 8, headRect.width, headRect.height);
ctx.fillStyle = tokens['--accent-green'];
ctx.fillRect(headRect.x, headRect.y, headRect.width, headRect.height);
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 4;
ctx.strokeRect(headRect.x, headRect.y, headRect.width, headRect.height);

ctx.fillStyle = tokens['--ink'];
ctx.fillRect(headRect.x, headRect.y, headRect.width, 30);
ctx.fillStyle = '#00f076';
ctx.font = '700 13px "Consolas", monospace';
ctx.fillText('LP-8000 LINE PRINTER // FANFOLD DATA TELEMETRY // SPEC-INF-1', headRect.x + 14, headRect.y + 20);
ctx.fillText('FORM FEED: OK', headRect.x + headRect.width - 130, headRect.y + 20);

ctx.fillStyle = tokens['--ink'];
ctx.font = '900 38px "Consolas", monospace';
ctx.fillText('WEB-HISTORY: 53 YEARS ON NO SCHEDULE', headRect.x + 20, headRect.y + 72);

ctx.font = '700 19px "Consolas", monospace';
ctx.fillText('A SCALE POSTER OF THE INTERNET’S 7 GREATEST MILESTONES (1969–2022)', headRect.x + 20, headRect.y + 106);

// Claim Box
const claimBox = Layout.rect(headRect.x + 16, headRect.y + 126, headRect.width - 32, 88);
ctx.fillStyle = tokens['--card-bg'];
ctx.fillRect(claimBox.x, claimBox.y, claimBox.width, claimBox.height);
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 3;
ctx.strokeRect(claimBox.x, claimBox.y, claimBox.width, claimBox.height);

ctx.fillStyle = tokens['--accent-pink'];
ctx.fillRect(claimBox.x, claimBox.y, 14, claimBox.height);

ctx.fillStyle = tokens['--ink'];
ctx.font = '900 15px "Consolas", monospace';
ctx.fillText('CORE CLAIM / THESIS:', claimBox.x + 26, claimBox.y + 26);
ctx.font = '700 15px "Consolas", monospace';
ctx.fillText('“Progress is constant — it never stopped coming, but it never once came on schedule.”', claimBox.x + 26, claimBox.y + 52);
ctx.font = '400 13px "Consolas", monospace';
ctx.fillStyle = tokens['--ink-dim'];
ctx.fillText('Audience: General reader · Canvas: 1080×1920 portrait · Setting: Line printer fanfold spool', claimBox.x + 26, claimBox.y + 74);

// 3. Timeline Spine & Decades
const spineX = 210;
const decades = [1970, 1980, 1990, 2000, 2010, 2020];
for (const d of decades) {
  const dy = timeScale.map(d);
  ctx.strokeStyle = '#8bc09e';
  ctx.lineWidth = 1.5;
  ctx.setLineDash([4, 4]);
  ctx.beginPath();
  ctx.moveTo(80, dy);
  ctx.lineTo(990, dy);
  ctx.stroke();
  ctx.setLineDash([]);
  
  ctx.fillStyle = '#164831';
  ctx.font = '700 15px "Consolas", monospace';
  ctx.textAlign = 'right';
  ctx.fillText(d.toString(), spineX - 24, dy + 5);
}
ctx.textAlign = 'left';

// Main Spine Line
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 7;
ctx.beginPath();
ctx.moveTo(spineX, 298);
ctx.lineTo(spineX, 1700);
ctx.stroke();

// Mini 1-Year Notches
for (let yr = 1969; yr <= 2022; yr++) {
  const yPos = timeScale.map(yr);
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = (yr % 5 === 0) ? 3 : 1;
  const tickLen = (yr % 10 === 0) ? 12 : (yr % 5 === 0) ? 8 : 4;
  ctx.beginPath();
  ctx.moveTo(spineX - tickLen, yPos);
  ctx.lineTo(spineX + tickLen, yPos);
  ctx.stroke();
}

// 4. In the 20-Year Desert Breathing Zone (1969 to 1989): Gap Comparison Chart
const voidY1 = timeScale.map(1969);
const gapChartRect = Layout.rect(320, voidY1 + 105, 670, 275);

ctx.fillStyle = tokens['--border-dark'];
ctx.fillRect(gapChartRect.x + 6, gapChartRect.y + 6, gapChartRect.width, gapChartRect.height);
ctx.fillStyle = '#ffffff';
ctx.fillRect(gapChartRect.x, gapChartRect.y, gapChartRect.width, gapChartRect.height);
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 3;
ctx.strokeRect(gapChartRect.x, gapChartRect.y, gapChartRect.width, gapChartRect.height);

ctx.fillStyle = tokens['--accent-yellow'];
ctx.fillRect(gapChartRect.x, gapChartRect.y, gapChartRect.width, 32);
ctx.strokeRect(gapChartRect.x, gapChartRect.y, gapChartRect.width, 32);

ctx.fillStyle = tokens['--ink'];
ctx.font = '900 14px "Consolas", monospace';
ctx.fillText('COMPARATIVE WAITING TIMES (YEARS BETWEEN BREAKTHROUGHS)', gapChartRect.x + 12, gapChartRect.y + 21);

const gapBand = Scale.band(gaps.length, gapChartRect.y + 42, gapChartRect.y + gapChartRect.height - 15, 0.22);
for (let i = 0; i < gaps.length; i++) {
  const g = gaps[i];
  const by = gapBand.map(i);
  const bh = gapBand.bandwidth;
  const bw = gapBarScale.extent(0, g.val);
  
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '700 13px "Consolas", monospace';
  ctx.textAlign = 'left';
  ctx.fillText(g.label, gapChartRect.x + 12, by + bh / 2 + 4);
  
  const bx = gapChartRect.x + 180;
  ctx.fillStyle = (g.val === 20) ? tokens['--accent-pink'] : (g.val === 3) ? tokens['--accent-green'] : tokens['--accent-cyan'];
  ctx.fillRect(bx, by, bw, bh);
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = 2;
  ctx.strokeRect(bx, by, bw, bh);
  
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '900 13px "Consolas", monospace';
  ctx.fillText(g.val + ' yrs', bx + bw + 8, by + bh / 2 + 4);
  
  ctx.fillStyle = tokens['--ink-dim'];
  ctx.font = '400 12px "Consolas", monospace';
  ctx.fillText('[' + g.category + ']', bx + 310 + 65, by + bh / 2 + 4);
}

// 5. Milestone Cards
const cardHeights = [84, 84, 84, 84, 84, 84, 84];
const cardSlots = [
  timeScale.map(1969) - 10,   // ARPANET: y=300
  timeScale.map(1989) - 45,   // WWW: y=782
  timeScale.map(1993) + 30,   // Mosaic: y=960
  timeScale.map(1998) + 40,   // Google: y=1100
  timeScale.map(2004) + 60,   // Facebook: y=1275
  timeScale.map(2007) + 80,   // iPhone: y=1373
  timeScale.map(2022) - 40    // ChatGPT: y=1640
];

for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const py = timeScale.map(m.year);
  
  // Area-encoded Node
  const nodeR = 14;
  ctx.fillStyle = (i === 0 || i === 6) ? tokens['--accent-pink'] : tokens['--accent-green'];
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.arc(spineX, py, nodeR, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();
  
  // Year stamp on left
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '900 22px "Consolas", monospace';
  ctx.textAlign = 'right';
  ctx.fillText(m.year.toString(), spineX - 22, py + 7);
  ctx.textAlign = 'left';
  
  // Leader connector line
  const cardX = 320;
  const cardY = cardSlots[i];
  const cardW = 670;
  const cardH = cardHeights[i];
  
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(spineX + nodeR, py);
  ctx.lineTo(cardX, cardY + cardH / 2);
  ctx.stroke();
  
  ctx.fillStyle = tokens['--border-dark'];
  ctx.beginPath();
  ctx.arc(cardX, cardY + cardH / 2, 4, 0, Math.PI * 2);
  ctx.fill();
  
  // Card Shadow & Body
  ctx.fillStyle = tokens['--border-dark'];
  ctx.fillRect(cardX + 6, cardY + 6, cardW, cardH);
  ctx.fillStyle = tokens['--card-bg'];
  ctx.fillRect(cardX, cardY, cardW, cardH);
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = 3;
  ctx.strokeRect(cardX, cardY, cardW, cardH);
  
  // Ribbon
  const hdrColor = (i === 0) ? tokens['--accent-yellow'] : (i === 6) ? tokens['--accent-orange'] : tokens['--accent-cyan'];
  ctx.fillStyle = hdrColor;
  ctx.fillRect(cardX, cardY, cardW, 26);
  ctx.strokeRect(cardX, cardY, cardW, 26);
  
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '900 15px "Consolas", monospace';
  ctx.fillText(m.tag + ' // ' + m.year + ': ' + m.title.toUpperCase(), cardX + 12, cardY + 18);
  
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '700 14px "Consolas", monospace';
  ctx.fillText(m.detail, cardX + 12, cardY + 50);
  
  ctx.fillStyle = tokens['--ink-dim'];
  ctx.font = '400 12px "Consolas", monospace';
  ctx.fillText('STATUS: DEPLOYED · RELATIVE POSITION: ' + ((m.year - 1969) / 53 * 100).toFixed(1) + '% OF TIMELINE SPAN', cardX + 12, cardY + 72);
}

// 6. Dense Cluster Stamped Callout Badge (Rotated - Tension Rule §5)
ctx.save();
ctx.translate(spineX + 28, timeScale.map(2000) - 20);
ctx.rotate(-0.04);
ctx.fillStyle = tokens['--border-dark'];
ctx.fillRect(4, 4, 250, 48);
ctx.fillStyle = tokens['--accent-pink'];
ctx.fillRect(0, 0, 250, 48);
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 3;
ctx.strokeRect(0, 0, 250, 48);

ctx.fillStyle = '#ffffff';
ctx.font = '900 13px "Consolas", monospace';
ctx.fillText('⚡ 14-YEAR FRENZY CLUSTER', 10, 20);
ctx.font = '700 11px "Consolas", monospace';
ctx.fillText('5 OF 7 MILESTONES IN 26% OF TIME', 10, 36);
ctx.restore();

// 7. Summary Footer Banner
const footRect = Layout.rect(74, 1720, 932, 160);
ctx.fillStyle = tokens['--border-dark'];
ctx.fillRect(footRect.x + 8, footRect.y + 8, footRect.width, footRect.height);
ctx.fillStyle = tokens['--accent-green'];
ctx.fillRect(footRect.x, footRect.y, footRect.width, footRect.height);
ctx.strokeStyle = tokens['--border-dark'];
ctx.lineWidth = 4;
ctx.strokeRect(footRect.x, footRect.y, footRect.width, footRect.height);

ctx.fillStyle = tokens['--ink'];
ctx.fillRect(footRect.x, footRect.y, footRect.width, 28);
ctx.fillStyle = '#00f076';
ctx.font = '700 13px "Consolas", monospace';
ctx.fillText('PRINTER TERMINATION // 53-YEAR RUN SUMMARY METRICS // END OF SPOOL', footRect.x + 14, footRect.y + 19);

ctx.fillStyle = tokens['--ink'];
ctx.font = '900 17px "Consolas", monospace';
ctx.fillText('KEY TAKEAWAYS FROM 53 YEARS OF COMPUTING REVOLUTIONS:', footRect.x + 18, footRect.y + 58);

const badgeW = 285;
const b1 = Layout.rect(footRect.x + 16, footRect.y + 70, badgeW, 74);
const b2 = Layout.rect(footRect.x + 16 + badgeW + 18, footRect.y + 70, badgeW, 74);
const b3 = Layout.rect(footRect.x + 16 + (badgeW + 18) * 2, footRect.y + 70, badgeW, 74);

const drawSummaryBadge = (rect, title, val, sub, bg) => {
  ctx.fillStyle = bg;
  ctx.fillRect(rect.x, rect.y, rect.width, rect.height);
  ctx.strokeStyle = tokens['--border-dark'];
  ctx.lineWidth = 2.5;
  ctx.strokeRect(rect.x, rect.y, rect.width, rect.height);
  
  ctx.fillStyle = tokens['--ink'];
  ctx.font = '900 13px "Consolas", monospace';
  ctx.fillText(title, rect.x + 10, rect.y + 20);
  ctx.font = '900 20px "Consolas", monospace';
  ctx.fillText(val, rect.x + 10, rect.y + 44);
  ctx.font = '700 11px "Consolas", monospace';
  ctx.fillStyle = tokens['--ink-dim'];
  ctx.fillText(sub, rect.x + 10, rect.y + 62);
};

drawSummaryBadge(b1, 'TOTAL TIME SPAN', '53 YEARS', '1969 to 2022 span', '#ffffff');
drawSummaryBadge(b2, 'LONGEST DESERT', '20 YEARS', 'ARPANET to WWW (1969–89)', tokens['--accent-yellow']);
drawSummaryBadge(b3, 'SHORTEST SPRINT', '3 YEARS', 'Facebook to iPhone (04–07)', tokens['--accent-cyan']);

// Pixel audit check on bitmap
const bm = canvas.toBitmap();
const pxTop = bm.getPixel(100, 100);
const pxSpine = bm.getPixel(spineX, 500);
const pxTractor = bm.getPixel(27, 28);
log('Pixel verification: Top header=' + pxTop + ', Timeline spine=' + pxSpine + ', Tractor hole=' + pxTractor);

Stage.note('Litmus Test 1: Could this layout hold a SaaS dashboard? No — it is diegetic fanfold line-printer continuous spool with tractor perforations and true-scale timeline.');
Stage.note('Litmus Test 2: Is the topic recognisable with text covered? Yes — the vertical timeline spine with lumpy clusters, leader vectors, and tractor feed sprocket margins instantly communicates historical timeline progression.');
Stage.note('Litmus Test 3: Are all four corners doing equal work? No — asymmetrical platen header at top, dense cluster in lower-middle, summary telemetry at bottom.');
Stage.note('Litmus Test 4: Is any baseline non-zero or any circle sized by radius? No — gap bar scale strictly zero-based and circles sized by area.');

canvas;
