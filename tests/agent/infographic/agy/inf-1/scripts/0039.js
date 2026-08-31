Stage.begin('Detail');

Stage.note('Stage 6 Detail: Crafting full neo-brutalist line-printer fanfold infographic with rich tactile marks, continuous tractor-feed margins, green-bar shading, registration marks, and exact truthful geometric encoding.');

// Raw Data from brief.md
const milestones = [
  { 
    year: 1969, 
    title: 'ARPANET 1st Message', 
    body: 'Sent "LO" from UCLA to Stanford, then immediately crashed. The internet begins with a dropped packet.', 
    stamp: 'CRASHED', 
    stampColor: '#FF4338',
    cardColor: '#FFFFFF',
    tag: '1969'
  },
  { 
    year: 1989, 
    title: 'World Wide Web Proposed', 
    body: 'Tim Berners-Lee circulates CERN proposal. 20 quiet years of research suddenly ignite.', 
    stamp: 'HYPERTEXT', 
    stampColor: '#FFE500',
    cardColor: '#FFFFFF',
    tag: '1989'
  },
  { 
    year: 1993, 
    title: 'Mosaic Visual Web', 
    body: 'Marc Andreessen releases Mosaic browser. The web gains images, forms, and mass public curiosity.', 
    stamp: 'GUI BROWSER', 
    stampColor: '#00E5FF',
    cardColor: '#FFFFFF',
    tag: '1993'
  },
  { 
    year: 1998, 
    title: 'Google Founded', 
    body: 'PageRank algorithm organizes the expanding chaos of 25+ million web pages into searchable reality.', 
    stamp: 'INDEXED', 
    stampColor: '#00FF66',
    cardColor: '#FFFFFF',
    tag: '1998'
  },
  { 
    year: 2004, 
    title: 'Facebook Launches', 
    body: 'Social era begins from a Harvard dorm. The web shifts from documents to identity networks.', 
    stamp: 'SOCIAL ERA', 
    stampColor: '#9D4EDD',
    cardColor: '#FFFFFF',
    tag: '2004'
  },
  { 
    year: 2007, 
    title: 'The iPhone Launches', 
    body: 'Puts the web into every human pocket. Always-on mobile bandwidth transforms daily life worldwide.', 
    stamp: 'MOBILE REVOLUTION', 
    stampColor: '#FF9100',
    cardColor: '#FFFFFF',
    tag: '2007'
  },
  { 
    year: 2022, 
    title: 'ChatGPT Explosion', 
    body: 'Reaches 100M users in just 2 months. Generative AI becomes the fastest adopted consumer tech in history.', 
    stamp: 'AI DISRUPTION', 
    stampColor: '#FF007F',
    cardColor: '#FFFFFF',
    tag: '2022'
  }
];

// Derived Gaps from brief.md
const gaps = [
  { label: 'ARPANET → Web', from: 1969, to: 1989, years: 20, color: '#FF4338' },
  { label: 'Web → Mosaic', from: 1989, to: 1993, years: 4, color: '#FFE500' },
  { label: 'Mosaic → Google', from: 1993, to: 1998, years: 5, color: '#00E5FF' },
  { label: 'Google → Facebook', from: 1998, to: 2004, years: 6, color: '#00FF66' },
  { label: 'Facebook → iPhone', from: 2004, to: 2007, years: 3, color: '#9D4EDD' },
  { label: 'iPhone → ChatGPT', from: 2007, to: 2022, years: 15, color: '#FF9100' }
];

// 1. SCALES & ENCODINGS
const spineTop = 270;
const spineBottom = 1530;
const timeScale = Scale.linear(1969, 2022, spineTop, spineBottom);

const maxGapVal = 20;
const gapBarMaxW = 250;
const gapScale = Scale.linear(0, maxGapVal, 0, gapBarMaxW);
if (!gapScale.isZeroBased) throw new Error('Gap scale baseline must be zero');

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');

// --- BACKGROUND & DIEGETIC FANFOLD GROUND ---
ctx.fillStyle = '#FAF6EE';
ctx.fillRect(0, 0, 1080, 1920);

// Alternating green-bar continuous line printer shading bands
ctx.fillStyle = 'rgba(0, 140, 70, 0.032)';
for (let y = 0; y < 1920; y += 64) {
  ctx.fillRect(0, y, 1080, 32);
}

// Tractor Feed Margins
const marginW = 46;
ctx.fillStyle = '#EDE5D8';
ctx.fillRect(0, 0, marginW, 1920);
ctx.fillRect(1080 - marginW, 0, marginW, 1920);

// Perforation lines dividing tractor margins
ctx.strokeStyle = '#D5CCBD';
ctx.lineWidth = 2;
ctx.setLineDash([4, 4]);
ctx.beginPath();
ctx.moveTo(marginW, 0); ctx.lineTo(marginW, 1920);
ctx.moveTo(1080 - marginW, 0); ctx.lineTo(1080 - marginW, 1920);
ctx.stroke();
ctx.setLineDash([]);

// Tractor Pin Holes
for (let y = 24; y < 1920; y += 44) {
  // Left hole
  ctx.fillStyle = '#1A1A1A';
  ctx.beginPath();
  ctx.arc(marginW / 2, y, 7, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#D5CCBD';
  ctx.lineWidth = 1.5;
  ctx.stroke();

  // Right hole
  ctx.fillStyle = '#1A1A1A';
  ctx.beginPath();
  ctx.arc(1080 - marginW / 2, y, 7, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#D5CCBD';
  ctx.stroke();
}

// Continuous Fanfold Fold / Perforation across the page (Diegetic Detail)
const foldY = 1040;
ctx.strokeStyle = '#BDB3A1';
ctx.lineWidth = 2;
ctx.setLineDash([8, 8]);
ctx.beginPath();
ctx.moveTo(marginW, foldY);
ctx.lineTo(1080 - marginW, foldY);
ctx.stroke();
ctx.setLineDash([]);

ctx.fillStyle = '#A09684';
ctx.font = '700 11px Consolas, monospace';
ctx.fillText('>>> FOLD PERFORATION // CONTINUOUS FORM FEED · PAGE 01 / 02 >>>', marginW + 12, foldY - 6);

// Corner Registration Marks
const drawRegMark = (rx, ry) => {
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.arc(rx, ry, 8, 0, Math.PI * 2);
  ctx.moveTo(rx - 12, ry); ctx.lineTo(rx + 12, ry);
  ctx.moveTo(rx, ry - 12); ctx.lineTo(rx, ry + 12);
  ctx.stroke();
};
drawRegMark(65, 20);
drawRegMark(1015, 20);
drawRegMark(65, 1900);
drawRegMark(1015, 1900);

// --- HEADER ZONE (PLATEN FEED HEAD) ---
const headX = 64;
const headY = 40;
const headW = 952;
const headH = 175;

// Brutalist shadow
ctx.fillStyle = '#111111';
ctx.fillRect(headX + 6, headY + 6, headW, headH);

// Main Header Box
ctx.fillStyle = '#FFE500';
ctx.fillRect(headX, headY, headW, headH);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 5;
ctx.strokeRect(headX, headY, headW, headH);

// Header Meta strip
ctx.fillStyle = '#111111';
ctx.fillRect(headX, headY, headW, 36);
ctx.fillStyle = '#00FF66';
ctx.font = '900 15px Consolas, monospace';
ctx.fillText('SYSTEM://NET_HISTORY.LOG · LINE PRINTER CONTINUOUS FEED · 53 YEARS', headX + 16, headY + 24);

// Header Title
ctx.fillStyle = '#111111';
ctx.font = '900 48px Impact, "Arial Black", sans-serif';
ctx.fillText('53 YEARS OF THE INTERNET', headX + 16, headY + 86);

// Claim Subheading
ctx.fillStyle = '#111111';
ctx.font = '700 18px "Segoe UI", Arial, sans-serif';
ctx.fillWrappedText('"Progress is constant — it never stopped coming, but it never once came on schedule."', headX + 18, headY + 116, headW - 36, 24);

// Header Badge
ctx.fillStyle = '#FF4338';
ctx.fillRect(headX + headW - 220, headY + 48, 205, 52);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 3;
ctx.strokeRect(headX + headW - 220, headY + 48, 205, 52);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 18px Consolas, monospace';
ctx.fillText('1969 → 2022', headX + headW - 175, headY + 80);

// --- LEFT COLUMN: CONTINUOUS TIMELINE SPINE (60% WIDTH) ---
const spineX = 145;

// Spine vertical line
ctx.strokeStyle = '#111111';
ctx.lineWidth = 6;
ctx.beginPath();
ctx.moveTo(spineX, spineTop - 15);
ctx.lineTo(spineX, spineBottom + 15);
ctx.stroke();

// Timeline Decade Ticks
for (let yr = 1970; yr <= 2020; yr += 10) {
  const ty = timeScale.map(yr);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(spineX - 16, ty);
  ctx.lineTo(spineX + 16, ty);
  ctx.stroke();

  ctx.fillStyle = '#666666';
  ctx.font = '900 15px Consolas, monospace';
  ctx.textAlign = 'right';
  ctx.fillText(String(yr), spineX - 22, ty + 5);
}
ctx.textAlign = 'left';

// Render Milestone Cards on Timeline Spine
for (let i = 0; i < milestones.length; i++) {
  const m = milestones[i];
  const my = timeScale.map(m.year);

  // Spine pin circle
  ctx.fillStyle = m.stampColor;
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.arc(spineX, my, 12, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();

  // Pin inner dot
  ctx.fillStyle = '#111111';
  ctx.beginPath();
  ctx.arc(spineX, my, 4, 0, Math.PI * 2);
  ctx.fill();

  // Connector bracket to card
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(spineX + 12, my);
  ctx.lineTo(spineX + 45, my);
  ctx.stroke();

  // Milestone Card Dimensions
  const cardX = spineX + 45;
  const cardW = 430;
  // Dynamic card height based on text
  const cardH = 92;
  const cardY = my - cardH / 2;

  // Solid drop shadow
  ctx.fillStyle = '#111111';
  ctx.fillRect(cardX + 5, cardY + 5, cardW, cardH);

  // Card background
  ctx.fillStyle = m.cardColor;
  ctx.fillRect(cardX, cardY, cardW, cardH);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 4;
  ctx.strokeRect(cardX, cardY, cardW, cardH);

  // Year Tag Column
  ctx.fillStyle = m.stampColor;
  ctx.fillRect(cardX, cardY, 82, cardH);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 3;
  ctx.strokeRect(cardX, cardY, 82, cardH);

  ctx.fillStyle = '#111111';
  ctx.font = '900 22px Consolas, monospace';
  ctx.fillText(String(m.year), cardX + 10, cardY + 36);

  // Title
  ctx.fillStyle = '#111111';
  ctx.font = '900 18px "Segoe UI", Arial, sans-serif';
  ctx.fillText(m.title, cardX + 94, cardY + 26);

  // Body text
  ctx.fillStyle = '#333333';
  ctx.font = '500 13px "Segoe UI", Arial, sans-serif';
  ctx.fillWrappedText(m.body, cardX + 94, cardY + 44, cardW - 102, 17);

  // Punchy Status Stamp (Slanted)
  ctx.save();
  ctx.translate(cardX + cardW - 10, cardY + 12);
  ctx.rotate(-0.08);
  ctx.fillStyle = m.stampColor;
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 2;
  const stampText = '[' + m.stamp + ']';
  ctx.font = '900 11px Consolas, monospace';
  const stW = ctx.measureText(stampText).width + 12;
  ctx.fillRect(-stW, -14, stW, 20);
  ctx.strokeRect(-stW, -14, stW, 20);
  ctx.fillStyle = '#111111';
  ctx.fillText(stampText, -stW + 6, 0);
  ctx.restore();
}

// Gap Indicator bracket between 1969 and 1989 on the timeline (Tension / Desert annotation)
const g1Top = timeScale.map(1969) + 20;
const g1Bottom = timeScale.map(1989) - 20;
ctx.strokeStyle = '#FF4338';
ctx.lineWidth = 3;
ctx.setLineDash([4, 4]);
ctx.beginPath();
ctx.moveTo(spineX - 45, g1Top);
ctx.lineTo(spineX - 45, g1Bottom);
ctx.stroke();
ctx.setLineDash([]);

ctx.fillStyle = '#FF4338';
ctx.font = '900 13px Consolas, monospace';
ctx.save();
ctx.translate(spineX - 52, (g1Top + g1Bottom) / 2);
ctx.rotate(-Math.PI / 2);
ctx.textAlign = 'center';
ctx.fillText('◄ 20-YEAR SILENT DESERT ►', 0, 0);
ctx.restore();
ctx.textAlign = 'left';


// --- RIGHT COLUMN: COMPARATIVE ANALYTICS & HERO STATS (40% WIDTH) ---
const rightX = 645;
const rightW = 370;

// Panel 1: THE WAITING PERIODS (Zero-based Gap Bar Chart)
const barPanelY = 240;
const barPanelH = 670;

// Shadow
ctx.fillStyle = '#111111';
ctx.fillRect(rightX + 6, barPanelY + 6, rightW, barPanelH);

// Card
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(rightX, barPanelY, rightW, barPanelH);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 5;
ctx.strokeRect(rightX, barPanelY, rightW, barPanelH);

// Panel Header
ctx.fillStyle = '#FF4338';
ctx.fillRect(rightX, barPanelY, rightW, 52);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 3;
ctx.strokeRect(rightX, barPanelY, rightW, 52);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 20px Consolas, monospace';
ctx.fillText('THE WAITING PERIODS', rightX + 16, barPanelY + 34);

// Axis / Scale Grid
const barPlotX = rightX + 24;
const barPlotY = barPanelY + 80;
const barPlotH = 550;

// Zero-baseline vertical reference line
ctx.strokeStyle = '#111111';
ctx.lineWidth = 3;
ctx.beginPath();
ctx.moveTo(barPlotX, barPlotY);
ctx.lineTo(barPlotX, barPlotY + barPlotH);
ctx.stroke();

// Ticks (0, 5, 10, 15, 20 yrs)
for (let t = 0; t <= 20; t += 5) {
  const tx = barPlotX + gapScale.map(t);
  ctx.strokeStyle = '#E0DCD3';
  ctx.lineWidth = 1.5;
  ctx.setLineDash([2, 2]);
  ctx.beginPath();
  ctx.moveTo(tx, barPlotY);
  ctx.lineTo(tx, barPlotY + barPlotH);
  ctx.stroke();
  ctx.setLineDash([]);

  ctx.fillStyle = '#777777';
  ctx.font = '700 12px Consolas, monospace';
  ctx.textAlign = 'center';
  ctx.fillText(t + 'y', tx, barPlotY + 16);
}
ctx.textAlign = 'left';

// Draw the 6 Gap Bars
let curBarY = barPlotY + 40;
for (let i = 0; i < gaps.length; i++) {
  const g = gaps[i];
  const bw = gapScale.map(g.years);

  // Label & arithmetic
  ctx.fillStyle = '#111111';
  ctx.font = '800 14px "Segoe UI", Arial, sans-serif';
  ctx.fillText(g.label, barPlotX, curBarY);

  ctx.fillStyle = '#777777';
  ctx.font = '600 12px Consolas, monospace';
  ctx.fillText('(' + g.to + ' - ' + g.from + ' = ' + g.years + 'y)', barPlotX + 160, curBarY);

  // Bar rectangle with solid drop shadow
  ctx.fillStyle = '#111111';
  ctx.fillRect(barPlotX + 3, curBarY + 7 + 3, bw, 28);
  ctx.fillStyle = g.color;
  ctx.fillRect(barPlotX, curBarY + 7, bw, 28);
  ctx.strokeStyle = '#111111';
  ctx.lineWidth = 2.5;
  ctx.strokeRect(barPlotX, curBarY + 7, bw, 28);

  // Value text badge
  ctx.fillStyle = '#111111';
  ctx.font = '900 15px Consolas, monospace';
  ctx.fillText(g.years + ' YEARS', barPlotX + bw + 10, curBarY + 27);

  curBarY += 82;
}

// Annotation in bar chart
ctx.fillStyle = '#444444';
ctx.font = 'italic 500 12px "Segoe UI", Arial, sans-serif';
ctx.fillWrappedText('* Length strictly encodes time gap from zero baseline. Longest gap: 20y (ARPANET→Web). Shortest gap: 3y (Facebook→iPhone).', barPlotX, barPlotY + barPlotH - 25, rightW - 48, 16);


// Panel 2: HERO METRICS & ADOPTION VELOCITY
const heroY = 935;
const heroH = 635;

// Shadow
ctx.fillStyle = '#111111';
ctx.fillRect(rightX + 6, heroY + 6, rightW, heroH);

// Card
ctx.fillStyle = '#00E5FF';
ctx.fillRect(rightX, heroY, rightW, heroH);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 5;
ctx.strokeRect(rightX, heroY, rightW, heroH);

// Panel Title
ctx.fillStyle = '#111111';
ctx.fillRect(rightX, heroY, rightW, 46);
ctx.fillStyle = '#FFE500';
ctx.font = '900 18px Consolas, monospace';
ctx.fillText('MACRO VELOCITY METRICS', rightX + 16, heroY + 30);

// Inner Sub-Card 1: 53 Years Span
const sub1Y = heroY + 62;
ctx.fillStyle = '#FFFFFF';
ctx.fillRect(rightX + 16, sub1Y, rightW - 32, 235);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 4;
ctx.strokeRect(rightX + 16, sub1Y, rightW - 32, 235);

ctx.fillStyle = '#111111';
ctx.font = '900 84px Impact, "Arial Black", sans-serif';
ctx.fillText('53', rightX + 32, sub1Y + 82);

ctx.font = '900 24px Impact, sans-serif';
ctx.fillText('YEARS OF EVOLUTION', rightX + 130, sub1Y + 54);
ctx.fillStyle = '#FF4338';
ctx.font = '900 16px Consolas, monospace';
ctx.fillText('1969 TO 2022 SPAN', rightX + 130, sub1Y + 80);

ctx.fillStyle = '#222222';
ctx.font = '600 14px "Segoe UI", Arial, sans-serif';
ctx.fillWrappedText('From a two-letter crash message across two university labs to a ubiquitous planetary computing grid connecting over 5 billion humans.', rightX + 32, sub1Y + 115, rightW - 64, 20);

// Stamp inside Sub-Card 1
ctx.fillStyle = '#FFE500';
ctx.fillRect(rightX + 32, sub1Y + 185, rightW - 64, 34);
ctx.strokeRect(rightX + 32, sub1Y + 185, rightW - 64, 34);
ctx.fillStyle = '#111111';
ctx.font = '900 13px Consolas, monospace';
ctx.fillText('NET SPAN: 2022 - 1969 = 53 YEARS', rightX + 44, sub1Y + 207);


// Inner Sub-Card 2: 100M in 2 Mo (Adoption Speed)
const sub2Y = heroY + 315;
ctx.fillStyle = '#FF007F';
ctx.fillRect(rightX + 16, sub2Y, rightW - 32, 290);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 4;
ctx.strokeRect(rightX + 16, sub2Y, rightW - 32, 290);

ctx.fillStyle = '#FFFFFF';
ctx.font = '900 60px Impact, "Arial Black", sans-serif';
ctx.fillText('100M', rightX + 32, sub2Y + 68);

ctx.font = '900 20px Consolas, monospace';
ctx.fillText('USERS IN 2 MONTHS', rightX + 32, sub2Y + 105);

ctx.fillStyle = '#FFE500';
ctx.fillRect(rightX + 32, sub2Y + 120, rightW - 64, 28);
ctx.strokeRect(rightX + 32, sub2Y + 120, rightW - 64, 28);
ctx.fillStyle = '#111111';
ctx.font = '900 13px Consolas, monospace';
ctx.fillText('CHATGPT VELOCITY BREAKOUT', rightX + 44, sub2Y + 139);

ctx.fillStyle = '#FFFFFF';
ctx.font = '600 14px "Segoe UI", Arial, sans-serif';
ctx.fillWrappedText('Fastest consumer application adoption in recorded technology history — compressing historical decade-long adoption curves into 60 days.', rightX + 32, sub2Y + 172, rightW - 64, 20);

ctx.fillStyle = 'rgba(0,0,0,0.3)';
ctx.fillRect(rightX + 32, sub2Y + 238, rightW - 64, 32);
ctx.fillStyle = '#FFFFFF';
ctx.font = '700 12px Consolas, monospace';
ctx.fillText('COMPARE: WEB (7y) · FB (4.5y) · IPHONE (3.5y)', rightX + 38, sub2Y + 258);


// --- FOOTER ZONE: SYNTHESIS & DIEGETIC PRINTER AUDIT ---
const footX = 64;
const footY = 1595;
const footW = 952;
const footH = 265;

// Brutalist shadow
ctx.fillStyle = '#111111';
ctx.fillRect(footX + 6, footY + 6, footW, footH);

// Footer background
ctx.fillStyle = '#111111';
ctx.fillRect(footX, footY, footW, footH);
ctx.strokeStyle = '#111111';
ctx.lineWidth = 5;
ctx.strokeRect(footX, footY, footW, footH);

// Header bar
ctx.fillStyle = '#FFE500';
ctx.fillRect(footX, footY, footW, 40);
ctx.fillStyle = '#111111';
ctx.font = '900 16px Consolas, monospace';
ctx.fillText('EXECUTIVE SUMMARY & SYSTEM TAKEAWAY // LINE-PRINTER DISPATCH', footX + 16, footY + 26);

// Main Takeaway Quote
ctx.fillStyle = '#00FF66';
ctx.font = '900 24px "Segoe UI", Arial, sans-serif';
ctx.fillText('THE TAKEAWAY:', footX + 24, footY + 80);

ctx.fillStyle = '#FFFFFF';
ctx.font = '700 22px "Segoe UI", Arial, sans-serif';
ctx.fillWrappedText('"Progress is constant — it never stopped coming, but it never once came on schedule."', footX + 24, footY + 112, footW - 48, 30);

// 3 Core Insights
ctx.fillStyle = '#00E5FF';
ctx.font = '600 14px Consolas, monospace';
ctx.fillText('1. THE 20-YEAR DESERT (1969-1989): Foundational groundwork takes decades of invisible effort.', footX + 24, footY + 180);
ctx.fillText('2. THE 14-YEAR COMPRESSION (1993-2007): Visual browser → search → social → mobile in rapid succession.', footX + 24, footY + 205);
ctx.fillText('3. THE AI ACCELERATION (2022+): Adoption cycles have collapsed from decades into months.', footX + 24, footY + 230);

// Status Badge at bottom right
ctx.fillStyle = '#FF4338';
ctx.fillRect(footX + footW - 200, footY + 175, 180, 65);
ctx.strokeStyle = '#FFFFFF';
ctx.lineWidth = 2;
ctx.strokeRect(footX + footW - 200, footY + 175, 180, 65);
ctx.fillStyle = '#FFFFFF';
ctx.font = '900 15px Consolas, monospace';
ctx.fillText('[VERIFIED LOG]', footX + footW - 170, footY + 202);
ctx.font = '700 12px Consolas, monospace';
ctx.fillText('100% TRUTH INK', footX + footW - 170, footY + 224);

canvas;
