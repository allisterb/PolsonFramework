/**
 * AETHERIA ROBOTICS - Executive Brand Presentation Board & Visual Identity System
 * The Stylist: Brand Identity Director & Visual Systems Designer
 * 
 * Standards & Methodologies:
 * - Robin Williams (The Non-Designer's Design Book): Contrast, Repetition, Alignment, Proximity
 * - Doyald Young (Fonts and Logos): Optical letter-spacing, curve continuity, weight balancing
 * - Tubik Studio (Logo Design Creative Stages): Multi-scale stress testing, monochrome matrix, brand presentation
 * 
 * Client Specifications:
 * - Brand Name: AETHERIA ROBOTICS
 * - Tagline: AUTONOMOUS FLIGHT SYSTEMS
 * - Core Symbol: Swept Delta-Wing Monolithic 'A' + Autonomous Kinetic Core
 * - Color Palette: Deep Electric Cyan (#0EA5E9), Sapphire Blue (#3B82F6), 
 *                  Radiant Plasma Amber (#F59E0B), Titanium Slate (#0F172A),
 *                  Deep Background (#070B14), Crisp Signal White (#FFFFFF)
 */

const PHI = (1 + Math.sqrt(5)) / 2; // Golden Ratio ~ 1.6180339887

/**
 * Brand Identity Toolkit & Geometric Systems
 */
const Logo = {
  PHI: PHI,

  // Commercial 6-Color Harmonized Brand Palette
  palette: {
    electricCyan: '#0EA5E9',
    sapphireBlue: '#3B82F6',
    plasmaAmber: '#F59E0B',
    titaniumSlate: '#0F172A',
    deepBackground: '#070B14',
    signalWhite: '#FFFFFF',
    gridBlueprint: '#1E293B',
    mutedSlate: '#94A3B8',
    cardBackground: 'rgba(15, 23, 42, 0.85)',
    borderCyan: 'rgba(14, 165, 233, 0.35)',
    borderAmber: 'rgba(245, 158, 11, 0.4)'
  },

  /**
   * Generates golden ratio scaling series
   */
  createGoldenCircles(baseRadius, count = 5) {
    const circles = [];
    let r = baseRadius;
    for (let i = 0; i < count; i++) {
      circles.push(r);
      r *= PHI;
    }
    return circles;
  },

  /**
   * Optical bone-effect stroke compensation
   */
  correctBoneEffect(baseWeight, isVertical = true) {
    return isVertical ? baseWeight * 1.08 : baseWeight * 0.94;
  },

  /**
   * Computes optical overshoot for curved or sharp apexes
   */
  computeOvershoot(coord, radius, isTop = true) {
    const overshoot = radius * 0.025;
    return isTop ? coord - overshoot : coord + overshoot;
  },

  /**
   * Creates a continuous squircle / superellipse path (Lamé curve, n = 4)
   */
  createSquirclePath(ctx, x, y, width, height, n = 4) {
    const halfW = width / 2;
    const halfH = height / 2;
    const cx = x + halfW;
    const cy = y + halfH;
    const steps = 120;
    ctx.beginPath();
    for (let i = 0; i <= steps; i++) {
      const theta = (i / steps) * 2 * Math.PI;
      const cosT = Math.cos(theta);
      const sinT = Math.sin(theta);
      const px = cx + Math.sign(cosT) * Math.pow(Math.abs(cosT), 2 / n) * halfW;
      const py = cy + Math.sign(sinT) * Math.pow(Math.abs(sinT), 2 / n) * halfH;
      if (i === 0) ctx.moveTo(px, py);
      else ctx.lineTo(px, py);
    }
    ctx.closePath();
  },

  /**
   * 7-Tier Favicon Scale Stress Test (16px to 256px)
   */
  generateFaviconScaleTest(ctx, drawMarkFunc, width = 1400, height = 650) {
    ctx.save();
    ctx.fillStyle = this.palette.deepBackground;
    ctx.fillRect(0, 0, width, height);

    // Title Block
    ctx.fillStyle = this.palette.signalWhite;
    ctx.font = 'bold 24px "Segoe UI", Inter, sans-serif';
    ctx.fillText('AETHERIA ROBOTICS - 7-TIER FAVICON STRESS LADDER', 50, 55);

    ctx.fillStyle = this.palette.electricCyan;
    ctx.font = '600 13px "SF Mono", monospace';
    ctx.fillText('OPTICAL RESOLUTION VALIDATION // 16px MICRO TO 256px RETINA MACRO', 50, 82);

    const tiers = [
      { size: 256, label: '256px (Retina / Store)', sub: 'Full geometric fidelity', x: 50, y: 130 },
      { size: 128, label: '128px (Launcher / Dock)', sub: 'Aero channels crisp', x: 340, y: 130 },
      { size: 64, label: '64px (OS Taskbar)', sub: 'Kinetic core resolved', x: 500, y: 130 },
      { size: 48, label: '48px (Large Favicon)', sub: 'Silhouette sharp', x: 595, y: 130 },
      { size: 32, label: '32px (Browser Tab)', sub: 'Apertures distinct', x: 675, y: 130 },
      { size: 24, label: '24px (Toolbar / Nav)', sub: 'High contrast', x: 740, y: 130 },
      { size: 16, label: '16px (Micro Favicon)', sub: '100% identifiable', x: 795, y: 130 }
    ];

    tiers.forEach(item => {
      // Tile container
      ctx.fillStyle = '#0B1528';
      ctx.fillRect(item.x, item.y, item.size, item.size);
      ctx.strokeStyle = this.palette.borderCyan;
      ctx.lineWidth = 1;
      ctx.strokeRect(item.x, item.y, item.size, item.size);

      // Render mark
      ctx.save();
      ctx.translate(item.x, item.y);
      drawMarkFunc(ctx, item.size, this.palette.electricCyan);
      ctx.restore();

      // Labels below
      ctx.fillStyle = this.palette.signalWhite;
      ctx.font = 'bold 12px "SF Mono", monospace';
      ctx.fillText(`${item.size}px`, item.x, item.y + item.size + 24);

      ctx.fillStyle = this.palette.mutedSlate;
      ctx.font = '10.5px "Segoe UI", sans-serif';
      ctx.fillText(item.label, item.x, item.y + item.size + 40);
    });

    ctx.restore();
  },

  /**
   * 4-Tier Monochrome Contrast Validation Matrix
   */
  generateMonochromeTest(ctx, drawMarkFunc, width = 1400, height = 450) {
    ctx.save();
    const cellW = width / 4;
    const cellH = height;

    const modes = [
      { name: '01 // POSITIVE PRINT (LIGHT)', bg: '#FFFFFF', fg: '#0F172A', sub: 'High-contrast black/slate on white' },
      { name: '02 // NEGATIVE KNOCKOUT (DARK)', bg: '#070B14', fg: '#FFFFFF', sub: 'Pure signal white on deep void' },
      { name: '03 // ELECTRIC SILHOUETTE', bg: '#0F172A', fg: '#0EA5E9', sub: 'Single-color cyan brand accent' },
      { name: '04 // PLASMA CORE ACCENT', bg: '#070B14', fg: '#F59E0B', sub: 'Single-color amber kinetic state' }
    ];

    modes.forEach((mode, i) => {
      const startX = i * cellW;
      ctx.fillStyle = mode.bg;
      ctx.fillRect(startX, 0, cellW, cellH);

      ctx.strokeStyle = 'rgba(14, 165, 233, 0.2)';
      ctx.lineWidth = 1;
      ctx.strokeRect(startX, 0, cellW, cellH);

      // Label
      ctx.fillStyle = mode.fg;
      ctx.font = 'bold 12px "SF Mono", monospace';
      ctx.fillText(mode.name, startX + 24, 42);

      ctx.font = '11px "Segoe UI", sans-serif';
      ctx.fillStyle = mode.fg === '#FFFFFF' ? '#94A3B8' : (mode.fg === '#0F172A' ? '#64748B' : mode.fg);
      ctx.fillText(mode.sub, startX + 24, 62);

      // Render Mark
      const markSize = 180;
      ctx.save();
      ctx.translate(startX + (cellW - markSize) / 2, 110);
      drawMarkFunc(ctx, markSize, mode.fg);
      ctx.restore();
    });

    ctx.restore();
  },

  /**
   * Executive Brand Presentation Board (1920x1080)
   */
  generateBrandPresentationSheet(ctx, options = {}) {
    const {
      brandName = 'AETHERIA ROBOTICS',
      tagline = 'AUTONOMOUS FLIGHT SYSTEMS',
      primaryColor = '#0EA5E9',
      secondaryColor = '#3B82F6',
      amberColor = '#F59E0B',
      darkColor = '#070B14',
      slateColor = '#0F172A',
      lightColor = '#FFFFFF',
      drawMark = drawMark
    } = options;

    const W = 1920;
    const H = 1080;

    // =========================================================================
    // 1. MASTER ENVIRONMENT & ARCHITECTURAL BLUEPRINT GRID
    // =========================================================================
    ctx.save();
    ctx.fillStyle = darkColor;
    ctx.fillRect(0, 0, W, H);

    // Subtle Aerospace Blueprint Coordinate Grid
    ctx.lineWidth = 1;
    ctx.strokeStyle = 'rgba(14, 165, 233, 0.07)';
    const gridSize = 40;

    for (let x = 0; x < W; x += gridSize) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, H);
      ctx.stroke();
    }
    for (let y = 0; y < H; y += gridSize) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(W, y);
      ctx.stroke();
    }

    // Outer Precision Blueprint Border Frame
    ctx.strokeStyle = 'rgba(14, 165, 233, 0.3)';
    ctx.lineWidth = 1.5;
    ctx.strokeRect(35, 35, W - 70, H - 70);

    // Coordinate Registration Crosshairs
    const drawCrosshair = (cx, cy, s = 14) => {
      ctx.beginPath();
      ctx.moveTo(cx - s, cy); ctx.lineTo(cx + s, cy);
      ctx.moveTo(cx, cy - s); ctx.lineTo(cx, cy + s);
      ctx.stroke();
    };
    ctx.strokeStyle = primaryColor;
    ctx.lineWidth = 1.5;
    drawCrosshair(35, 35);
    drawCrosshair(W - 35, 35);
    drawCrosshair(35, H - 35);
    drawCrosshair(W - 35, H - 35);

    // =========================================================================
    // 2. EXECUTIVE HEADER BLOCK & SYSTEM STATUS
    // =========================================================================
    const headX = 60;
    const headY = 75;

    ctx.fillStyle = lightColor;
    ctx.font = '900 28px "Segoe UI", Inter, -apple-system, sans-serif';
    ctx.fillText('AETHERIA ROBOTICS', headX, headY);

    ctx.fillStyle = primaryColor;
    ctx.font = 'bold 12.5px "SF Mono", Consolas, monospace';
    ctx.fillText('EXECUTIVE BRAND IDENTITY SYSTEM // STAGE 2 SPECIFICATION // GOLDEN RATIO GEOMETRY (Φ = 1.618034)', headX, headY + 26);

    // System Status Badge (Top Right)
    const badgeW = 340;
    const badgeH = 50;
    const badgeX = W - 60 - badgeW;
    const badgeY = 52;

    ctx.fillStyle = 'rgba(15, 23, 42, 0.85)';
    ctx.fillRect(badgeX, badgeY, badgeW, badgeH);
    ctx.strokeStyle = 'rgba(14, 165, 233, 0.5)';
    ctx.strokeRect(badgeX, badgeY, badgeW, badgeH);

    ctx.fillStyle = amberColor;
    ctx.beginPath();
    ctx.arc(badgeX + 22, badgeY + 25, 5.5, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = lightColor;
    ctx.font = 'bold 11.5px "SF Mono", monospace';
    ctx.fillText('STATUS: COMMERCIAL IDENTITY READY', badgeX + 38, badgeY + 22);
    ctx.fillStyle = this.palette.mutedSlate;
    ctx.font = '10px "SF Mono", monospace';
    ctx.fillText('PIPELINE: GEOMETER ➔ STYLIST ➔ HARNESS', badgeX + 38, badgeY + 38);

    // Header Divider Line
    ctx.strokeStyle = 'rgba(14, 165, 233, 0.25)';
    ctx.beginPath();
    ctx.moveTo(headX, headY + 45);
    ctx.lineTo(W - 60, headY + 45);
    ctx.stroke();

    // =========================================================================
    // SECTION 01: PRIMARY COMBINATION MARK LOCKUP (HORIZONTAL) - Top Left
    // =========================================================================
    const row1Y = 150;
    const sec1X = 60;
    const sec1W = 680;
    const sec1H = 265;

    ctx.fillStyle = primaryColor;
    ctx.font = 'bold 12px "SF Mono", monospace';
    ctx.fillText('01 // PRIMARY COMBINATION MARK (HORIZONTAL)', sec1X, row1Y);

    ctx.fillStyle = this.palette.cardBackground;
    ctx.fillRect(sec1X, row1Y + 12, sec1W, sec1H);
    ctx.strokeStyle = this.palette.borderCyan;
    ctx.strokeRect(sec1X, row1Y + 12, sec1W, sec1H);

    // Hero Mark in Lockup
    const heroMarkSize = 175;
    const heroMarkX = sec1X + 35;
    const heroMarkY = row1Y + 55;

    ctx.save();
    ctx.translate(heroMarkX, heroMarkY);
    drawMark(ctx, heroMarkSize, primaryColor);
    ctx.restore();

    // Typography: "AETHERIA ROBOTICS"
    const textStartX = heroMarkX + heroMarkSize + 30;
    ctx.fillStyle = lightColor;
    ctx.font = '900 42px "Segoe UI", Inter, -apple-system, sans-serif';
    ctx.fillText('AETHERIA', textStartX, heroMarkY + 70);

    ctx.fillStyle = primaryColor;
    ctx.font = '700 24px "Segoe UI", Inter, sans-serif';
    ctx.fillText('ROBOTICS', textStartX + 2, heroMarkY + 104);

    // Tagline: "AUTONOMOUS FLIGHT SYSTEMS"
    ctx.fillStyle = amberColor;
    ctx.font = 'bold 11px "SF Mono", monospace';
    ctx.fillText('AUTONOMOUS FLIGHT SYSTEMS', textStartX + 3, heroMarkY + 132);

    // Optical Baseline Datum Line
    ctx.strokeStyle = 'rgba(14, 165, 233, 0.4)';
    ctx.setLineDash([3, 3]);
    ctx.beginPath();
    ctx.moveTo(textStartX, heroMarkY + 112);
    ctx.lineTo(textStartX + 265, heroMarkY + 112);
    ctx.stroke();
    ctx.setLineDash([]);

    // Clearspace dimension callout
    ctx.fillStyle = this.palette.mutedSlate;
    ctx.font = '10px "SF Mono", monospace';
    ctx.fillText('CLEARSPACE: 1X MARK HEIGHT OPTICAL MARGIN', sec1X + 25, row1Y + sec1H - 12);

    // =========================================================================
    // SECTION 02: SECONDARY VERTICAL STACKED LOCKUP - Top Middle
    // =========================================================================
    const sec2X = sec1X + sec1W + 20; // 760
    const sec2W = 390;
    const sec2H = 265;

    ctx.fillStyle = primaryColor;
    ctx.font = 'bold 12px "SF Mono", monospace';
    ctx.fillText('02 // SECONDARY STACKED LOCKUP', sec2X, row1Y);

    ctx.fillStyle = this.palette.cardBackground;
    ctx.fillRect(sec2X, row1Y + 12, sec2W, sec2H);
    ctx.strokeStyle = this.palette.borderCyan;
    ctx.strokeRect(sec2X, row1Y + 12, sec2W, sec2H);

    // Render Stacked Mark Centered
    const stackMarkSize = 110;
    const stackMarkX = sec2X + (sec2W - stackMarkSize) / 2;
    const stackMarkY = row1Y + 30;

    ctx.save();
    ctx.translate(stackMarkX, stackMarkY);
    drawMark(ctx, stackMarkSize, lightColor);
    ctx.restore();

    // Stacked Typography
    ctx.fillStyle = lightColor;
    ctx.font = '900 21px "Segoe UI", Inter, sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('AETHERIA ROBOTICS', sec2X + sec2W / 2, row1Y + 175);

    ctx.fillStyle = primaryColor;
    ctx.font = 'bold 10px "SF Mono", monospace';
    ctx.fillText('AUTONOMOUS FLIGHT SYSTEMS', sec2X + sec2W / 2, row1Y + 198);
    ctx.textAlign = 'left';

    // Clearspace frame dashed line
    ctx.strokeStyle = 'rgba(245, 158, 11, 0.4)';
    ctx.setLineDash([3, 3]);
    ctx.strokeRect(sec2X + 35, row1Y + 22, sec2W - 70, 210);
    ctx.setLineDash([]);

    ctx.fillStyle = amberColor;
    ctx.font = '9.5px "SF Mono", monospace';
    ctx.fillText('ISOLATION ZONE: 1.5Φ', sec2X + 45, row1Y + sec2H - 12);

    // =========================================================================
    // SECTION 03: GEOMETRIC BLUEPRINT & MATHEMATICAL ANATOMY - Top Right
    // =========================================================================
    const sec3X = sec2X + sec2W + 20; // 1170
    const sec3W = W - 60 - sec3X; // 690
    const sec3H = 265;

    ctx.fillStyle = primaryColor;
    ctx.font = 'bold 12px "SF Mono", monospace';
    ctx.fillText('03 // GEOMETRIC BLUEPRINT & DRAFTING ANATOMY', sec3X, row1Y);

    ctx.fillStyle = this.palette.cardBackground;
    ctx.fillRect(sec3X, row1Y + 12, sec3W, sec3H);
    ctx.strokeStyle = this.palette.borderCyan;
    ctx.strokeRect(sec3X, row1Y + 12, sec3W, sec3H);

    // Blueprint Diagram Mark (Left inside box)
    const blueprintMarkSize = 180;
    const blueprintMarkX = sec3X + 30;
    const blueprintMarkY = row1Y + 50;

    // Draw base Mark
    ctx.save();
    ctx.translate(blueprintMarkX, blueprintMarkY);
    drawMark(ctx, blueprintMarkSize, 'rgba(14, 165, 233, 0.9)');

    // Construction Overlays
    const bS = blueprintMarkSize;
    const bCx = bS / 2;
    const bCy = bS / 2;
    const bUsable = bS * 0.84;

    // Golden Ratio Concentric Circles
    ctx.strokeStyle = 'rgba(245, 158, 11, 0.6)';
    ctx.lineWidth = 1;
    ctx.setLineDash([3, 3]);
    const gRadii = Logo.createGoldenCircles(bUsable * 0.08, 4);
    gRadii.forEach(r => {
      ctx.beginPath();
      ctx.arc(bCx, bCy, r, 0, Math.PI * 2);
      ctx.stroke();
    });

    // Swept Delta Tangent Lines (60 deg)
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.5)';
    ctx.lineWidth = 1;
    const diag = bS * 0.55;
    ctx.beginPath();
    ctx.moveTo(bCx, bCy - bUsable * 0.45);
    ctx.lineTo(bCx + diag, bCy + diag * 0.7);
    ctx.moveTo(bCx, bCy - bUsable * 0.45);
    ctx.lineTo(bCx - diag, bCy + diag * 0.7);
    ctx.stroke();

    // Central Drafting Axis
    ctx.strokeStyle = 'rgba(14, 165, 233, 0.5)';
    ctx.beginPath();
    ctx.moveTo(bCx, 0); ctx.lineTo(bCx, bS);
    ctx.moveTo(0, bCy); ctx.lineTo(bS, bCy);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();

    // Blueprint Callout Explanations (Right inside box)
    const notesX = blueprintMarkX + blueprintMarkSize + 30;
    ctx.fillStyle = lightColor;
    ctx.font = 'bold 13px "Segoe UI", sans-serif';
    ctx.fillText('AERODYNAMIC MATHEMATICAL ANATOMY', notesX, row1Y + 45);

    const callouts = [
      '• Swept Delta-Wing Lifting Body: Hypersonic aerodynamic profile',
      '• Golden Kinetic Flow Channels: 60° vortex decompression kerfs',
      '• Autonomous Guidance Core: Nested Golden Arrowhead dart',
      '• Continuous Tangent Fillets: George Bokhua smooth apex radii',
      '• Optical Bone Compensation: +8% diagonal stroke weighting'
    ];

    ctx.fillStyle = this.palette.mutedSlate;
    ctx.font = '11.5px "Segoe UI", Inter, sans-serif';
    callouts.forEach((text, i) => {
      ctx.fillText(text, notesX, row1Y + 75 + (i * 26));
    });

    // =========================================================================
    // SECTION 04: HARMONIZED BRAND COLOR SYSTEM - Middle Left
    // =========================================================================
    const row2Y = 445;
    const sec4X = 60;
    const sec4W = 680;
    const sec4H = 265;

    ctx.fillStyle = primaryColor;
    ctx.font = 'bold 12px "SF Mono", monospace';
    ctx.fillText('04 // HARMONIZED 5-COLOR PALETTE SYSTEM', sec4X, row2Y);

    ctx.fillStyle = this.palette.cardBackground;
    ctx.fillRect(sec4X, row2Y + 12, sec4W, sec4H);
    ctx.strokeStyle = this.palette.borderCyan;
    ctx.strokeRect(sec4X, row2Y + 12, sec4W, sec4H);

    const colorSwatches = [
      { name: 'DEEP ELECTRIC CYAN', hex: '#0EA5E9', rgb: '14, 165, 233', role: 'Primary Accent / Guidance Glow', bg: '#0EA5E9', text: '#070B14' },
      { name: 'SAPPHIRE BLUE', hex: '#3B82F6', rgb: '59, 130, 246', role: 'Aerodynamic Vector Tint', bg: '#3B82F6', text: '#FFFFFF' },
      { name: 'PLASMA AMBER', hex: '#F59E0B', rgb: '245, 158, 11', role: 'Kinetic Autonomous Core', bg: '#F59E0B', text: '#070B14' },
      { name: 'TITANIUM SLATE', hex: '#0F172A', rgb: '15, 23, 42', role: 'Structural Carbon Body', bg: '#0F172A', text: '#FFFFFF' },
      { name: 'SIGNAL WHITE', hex: '#FFFFFF', rgb: '255, 255, 255', role: 'Knockout / Typography', bg: '#FFFFFF', text: '#070B14' }
    ];

    const swatchW = (sec4W - 40) / 5 - 8;
    const swatchH = 135;

    colorSwatches.forEach((sw, i) => {
      const sx = sec4X + 18 + i * (swatchW + 8);
      const sy = row2Y + 35;

      // Color Fill Box
      ctx.fillStyle = sw.bg;
      ctx.fillRect(sx, sy, swatchW, swatchH);
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
      ctx.strokeRect(sx, sy, swatchW, swatchH);

      // Hex code inside box
      ctx.fillStyle = sw.text;
      ctx.font = 'bold 11px "SF Mono", monospace';
      ctx.fillText(sw.hex, sx + 8, sy + swatchH - 12);

      // Details below
      ctx.fillStyle = lightColor;
      ctx.font = 'bold 10px "Segoe UI", sans-serif';
      ctx.fillText(sw.name, sx, sy + swatchH + 20);

      ctx.fillStyle = this.palette.mutedSlate;
      ctx.font = '9.5px "SF Mono", monospace';
      ctx.fillText(`RGB: ${sw.rgb}`, sx, sy + swatchH + 34);

      ctx.font = '9px "Segoe UI", sans-serif';
      ctx.fillText(sw.role, sx, sy + swatchH + 48);
    });

    // =========================================================================
    // SECTION 05: MONOCHROME CONTRAST & SQUIRCLE APP ICONS - Middle Right
    // =========================================================================
    const sec5X = sec4X + sec4W + 20; // 760
    const sec5W = W - 60 - sec5X; // 1100
    const sec5H = 265;

    ctx.fillStyle = primaryColor;
    ctx.font = 'bold 12px "SF Mono", monospace';
    ctx.fillText('05 // MONOCHROME CONTRAST MATRIX & APP ICON SQUIRCLES (n = 4)', sec5X, row2Y);

    ctx.fillStyle = this.palette.cardBackground;
    ctx.fillRect(sec5X, row2Y + 12, sec5W, sec5H);
    ctx.strokeStyle = this.palette.borderCyan;
    ctx.strokeRect(sec5X, row2Y + 12, sec5W, sec5H);

    // 4 Monochrome Tiles
    const monoModes = [
      { label: 'Positive Light', bg: '#FFFFFF', fg: '#0F172A' },
      { label: 'Negative Dark', bg: '#070B14', fg: '#FFFFFF' },
      { label: 'Electric Cyan', bg: '#0F172A', fg: '#0EA5E9' },
      { label: 'Plasma Amber', bg: '#070B14', fg: '#F59E0B' }
    ];

    const mTileW = 145;
    const mTileH = 185;
    const monoStartX = sec5X + 25;

    monoModes.forEach((m, idx) => {
      const mx = monoStartX + idx * (mTileW + 15);
      const my = row2Y + 40;

      ctx.fillStyle = m.bg;
      ctx.fillRect(mx, my, mTileW, mTileH);
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
      ctx.strokeRect(mx, my, mTileW, mTileH);

      // Render Mark
      const mSize = 100;
      ctx.save();
      ctx.translate(mx + (mTileW - mSize) / 2, my + 25);
      drawMark(ctx, mSize, m.fg);
      ctx.restore();

      // Label below mark inside tile
      ctx.fillStyle = m.fg;
      ctx.font = 'bold 11px "SF Mono", monospace';
      ctx.textAlign = 'center';
      ctx.fillText(m.label, mx + mTileW / 2, my + mTileH - 22);
      ctx.textAlign = 'left';
    });

    // Squircle App Icons (Right of mono tiles)
    const squircleStartX = monoStartX + 4 * (mTileW + 15) + 30;
    const sqSize = 140;
    const sqY = row2Y + 50;

    // Dark App Icon
    ctx.save();
    Logo.createSquirclePath(ctx, squircleStartX, sqY, sqSize, sqSize, 4);
    const sqGrad1 = ctx.createLinearGradient(squircleStartX, sqY, squircleStartX + sqSize, sqY + sqSize);
    sqGrad1.addColorStop(0, '#0F172A');
    sqGrad1.addColorStop(1, '#070B14');
    ctx.fillStyle = sqGrad1;
    ctx.fill();
    ctx.strokeStyle = 'rgba(14, 165, 233, 0.6)';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Mark inside dark icon
    ctx.save();
    ctx.translate(squircleStartX + 20, sqY + 20);
    drawMark(ctx, sqSize - 40, primaryColor);
    ctx.restore();
    ctx.restore();

    ctx.fillStyle = lightColor;
    ctx.font = 'bold 11px "SF Mono", monospace';
    ctx.fillText('AERO DARK ICON', squircleStartX + 10, sqY + sqSize + 22);

    // Light App Icon
    const sq2X = squircleStartX + sqSize + 40;
    ctx.save();
    Logo.createSquirclePath(ctx, sq2X, sqY, sqSize, sqSize, 4);
    ctx.fillStyle = '#FFFFFF';
    ctx.fill();
    ctx.strokeStyle = 'rgba(14, 165, 233, 0.8)';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Mark inside light icon
    ctx.save();
    ctx.translate(sq2X + 20, sqY + 20);
    drawMark(ctx, sqSize - 40, '#0F172A');
    ctx.restore();
    ctx.restore();

    ctx.fillStyle = lightColor;
    ctx.font = 'bold 11px "SF Mono", monospace';
    ctx.fillText('STUDIO LIGHT ICON', sq2X + 5, sqY + sqSize + 22);

    // =========================================================================
    // SECTION 06: 7-TIER MULTI-SCALE FAVICON STRESS LADDER - Bottom Strip
    // =========================================================================
    const row3Y = 740;
    const sec6X = 60;
    const sec6W = W - 120; // 1800
    const sec6H = 265;

    ctx.fillStyle = primaryColor;
    ctx.font = 'bold 12px "SF Mono", monospace';
    ctx.fillText('06 // 7-TIER MULTI-SCALE FAVICON STRESS LADDER (256px ➔ 128px ➔ 64px ➔ 48px ➔ 32px ➔ 24px ➔ 16px)', sec6X, row3Y);

    ctx.fillStyle = this.palette.cardBackground;
    ctx.fillRect(sec6X, row3Y + 12, sec6W, sec6H);
    ctx.strokeStyle = this.palette.borderCyan;
    ctx.strokeRect(sec6X, row3Y + 12, sec6W, sec6H);

    const ladderTiers = [
      { size: 180, displaySize: 256, label: '256px Retina', desc: 'Full geometric fidelity & contour' },
      { size: 120, displaySize: 128, label: '128px Launcher', desc: 'Flow channels & dart resolved' },
      { size: 64, displaySize: 64, label: '64px Taskbar', desc: 'Apex & reflex distinct' },
      { size: 48, displaySize: 48, label: '48px Large Favicon', desc: 'Negative space preserved' },
      { size: 32, displaySize: 32, label: '32px Browser Tab', desc: 'Silhouette 100% crisp' },
      { size: 24, displaySize: 24, label: '24px Toolbar', desc: 'Zero visual artifacting' },
      { size: 16, displaySize: 16, label: '16px Micro', desc: 'Max legibility achieved' }
    ];

    let currX = sec6X + 40;
    ladderTiers.forEach(tier => {
      const tileH = tier.size;
      const tileY = row3Y + 35 + (180 - tier.size) / 2;

      // Dark containment tile
      ctx.fillStyle = '#0B1528';
      ctx.fillRect(currX, tileY, tier.size, tileH);
      ctx.strokeStyle = 'rgba(14, 165, 233, 0.4)';
      ctx.strokeRect(currX, tileY, tier.size, tileH);

      // Render Mark at precise scale
      ctx.save();
      ctx.translate(currX, tileY);
      drawMark(ctx, tier.size, primaryColor);
      ctx.restore();

      // Text descriptions
      ctx.fillStyle = lightColor;
      ctx.font = 'bold 11px "SF Mono", monospace';
      ctx.fillText(`${tier.displaySize}px`, currX, row3Y + 235);

      ctx.fillStyle = this.palette.mutedSlate;
      ctx.font = '10px "Segoe UI", sans-serif';
      ctx.fillText(tier.label, currX, row3Y + 250);

      currX += tier.size + 45;
    });

    ctx.restore();
  }
};

/**
 * Ingested The Geometer's Precision Mark Construction
 * @param {CanvasRenderingContext2D} ctx - HTML5 2D Canvas context
 * @param {number} size - Canvas square dimension
 * @param {string} [color='#0EA5E9'] - Mark fill color
 */
function drawMark(ctx, size, color = '#0EA5E9') {
  ctx.save();
  const S = size;
  const cx = S / 2;
  const cy = S / 2;
  const usable = S * 0.84;
  const halfW = (usable * 0.94) / 2;
  const halfH = usable / 2;

  const yTopRaw = cy - halfH * 0.95;
  const yTop = Logo.computeOvershoot(yTopRaw, usable, true);
  const yBase = cy + halfH * 0.90;
  const yReflex = cy + halfH * 0.50;

  const xLeftWing = cx - halfW;
  const xRightWing = cx + halfW;
  const yCrossbarTop = cy + halfH * 0.05;
  const yCoreApex = cy - halfH * 0.38;

  const apexFillet = usable * 0.045;
  const wingFillet = usable * 0.055;
  const reflexFillet = usable * 0.04;
  const slotW = Logo.correctBoneEffect(usable * 0.048, false);

  ctx.fillStyle = color;

  // 1. Primary Swept Delta 'A' Wing Body
  ctx.beginPath();
  const apexAngle = Math.atan2(yBase - yTop, halfW);
  const apexDx = Math.sin(apexAngle) * apexFillet;
  const apexDy = Math.cos(apexAngle) * apexFillet;

  ctx.moveTo(cx - apexDx, yTop + apexDy);
  ctx.quadraticCurveTo(cx, yTop, cx + apexDx, yTop + apexDy);
  ctx.lineTo(xRightWing - wingFillet * 0.8, yBase - wingFillet * 0.5);
  ctx.quadraticCurveTo(xRightWing, yBase, xRightWing - wingFillet * 1.4, yBase + wingFillet * 0.2);
  ctx.lineTo(cx + reflexFillet * 0.8, yReflex + reflexFillet * 0.6);
  ctx.quadraticCurveTo(cx, yReflex, cx - reflexFillet * 0.8, yReflex + reflexFillet * 0.6);
  ctx.lineTo(xLeftWing + wingFillet * 1.4, yBase + wingFillet * 0.2);
  ctx.quadraticCurveTo(xLeftWing, yBase, xLeftWing + wingFillet * 0.8, yBase - wingFillet * 0.5);
  ctx.closePath();
  ctx.fill();

  // 2. Negative Space Sculpting
  ctx.save();
  ctx.globalCompositeOperation = 'destination-out';

  // 2A. Upper Triangular Guidance Aperture
  const upperCounterW = halfW * 0.36;
  const yUpperBase = yCrossbarTop - slotW * 0.2;
  const upperFillet = usable * 0.025;

  ctx.beginPath();
  ctx.moveTo(cx, yCoreApex + upperFillet);
  ctx.lineTo(cx + upperCounterW / 2 - upperFillet, yUpperBase - upperFillet);
  ctx.quadraticCurveTo(cx + upperCounterW / 2, yUpperBase, cx + upperCounterW / 2 - upperFillet * 1.5, yUpperBase);
  ctx.lineTo(cx - upperCounterW / 2 + upperFillet * 1.5, yUpperBase);
  ctx.quadraticCurveTo(cx - upperCounterW / 2, yUpperBase, cx - upperCounterW / 2 + upperFillet, yUpperBase - upperFillet);
  ctx.lineTo(cx - upperFillet * 0.5, yCoreApex + upperFillet);
  ctx.quadraticCurveTo(cx, yCoreApex, cx + upperFillet * 0.5, yCoreApex + upperFillet);
  ctx.closePath();
  ctx.fill();

  // 2B. Golden Kinetic Flow Channels (Left & Right)
  const channelAngle = Math.PI / 6.2;
  const channelLen = usable * 0.32;
  const channelW = slotW * 0.85;

  // Left Flow Channel
  ctx.save();
  ctx.translate(cx - halfW * 0.42, cy + halfH * 0.06);
  ctx.rotate(-channelAngle);
  ctx.beginPath();
  ctx.roundRect(-channelW / 2, -channelLen / 2, channelW, channelLen, channelW / 2);
  ctx.fill();
  ctx.restore();

  // Right Flow Channel
  ctx.save();
  ctx.translate(cx + halfW * 0.42, cy + halfH * 0.06);
  ctx.rotate(channelAngle);
  ctx.beginPath();
  ctx.roundRect(-channelW / 2, -channelLen / 2, channelW, channelLen, channelW / 2);
  ctx.fill();
  ctx.restore();

  // 2C. Lower Delta Exhaust Thrust Notch
  const notchW = halfW * 0.44;
  const notchTopY = yReflex - usable * 0.10;
  const notchBottomY = yReflex + usable * 0.18;

  ctx.beginPath();
  ctx.moveTo(cx, notchTopY);
  ctx.lineTo(cx + notchW / 2, notchBottomY);
  ctx.lineTo(cx - notchW / 2, notchBottomY);
  ctx.closePath();
  ctx.fill();

  ctx.restore(); // Exit destination-out

  // 3. Autonomous Guidance Flight Dart (Positive Accent Anchor)
  const dartW = halfW * 0.28;
  const dartApexY = notchTopY + usable * 0.035;
  const dartBaseY = yReflex + usable * 0.08;
  const dartReflexY = yReflex + usable * 0.04;

  ctx.beginPath();
  ctx.moveTo(cx, dartApexY);
  ctx.lineTo(cx + dartW / 2, dartBaseY);
  ctx.lineTo(cx, dartReflexY);
  ctx.lineTo(cx - dartW / 2, dartBaseY);
  ctx.closePath();
  ctx.fill();

  ctx.restore();
}

// Automatic harness execution if canvas exists
if (typeof canvas !== 'undefined' && canvas.getContext) {
  const ctx = canvas.getContext('2d');
  Logo.generateBrandPresentationSheet(ctx, {
    brandName: 'AETHERIA ROBOTICS',
    tagline: 'AUTONOMOUS FLIGHT SYSTEMS',
    primaryColor: '#0EA5E9',
    secondaryColor: '#3B82F6',
    amberColor: '#F59E0B',
    darkColor: '#070B14',
    slateColor: '#0F172A',
    lightColor: '#FFFFFF',
    drawMark: drawMark
  });
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { Logo, drawMark };
}
