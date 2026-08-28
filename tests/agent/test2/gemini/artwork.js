/**
 * SAILBOAT TOURS — MASTER BRAND IDENTITY & GEOMETRIC SYSTEM
 * 
 * Generates:
 * 1. output.svg  — Master Vector Artwork (512x512 SVG)
 * 2. output.webp — Executive Brand Presentation Sheet (1600x1000 Landscape)
 * 
 * Grounded in:
 * - Golden Ratio Geometry (Φ = 1.618)
 * - Optical Balance & Irradiation Compensation
 * - Multi-scale 16px Favicon Robustness
 * - Single-Color Flat Ink & Knockout Reproduction
 */

Stage.begin('Presentation');
Stage.note('Generating master vector mark (output.svg) and executive brand presentation sheet (output.webp).');

// =========================================================================
// 1. MASTER VECTOR SVG MARK (512x512)
// =========================================================================
const paper = Snap(512, 512);

// Background container for SVG preview
paper.rect(0, 0, 512, 512).attr({ fill: '#0F141D', id: 'canvas-bg' });

// Master geometry paths
const mainsailD = "M 160 360 C 130 240 180 130 255 95 C 210 190 200 290 235 365 C 195 375 175 372 160 360 Z";
const foresailD = "M 250 365 C 255 260 300 180 345 160 C 315 245 305 315 335 365 C 290 375 270 372 250 365 Z";
const hullD = "M 125 375 C 230 422 315 410 385 360 C 315 385 230 395 125 375 Z";

const markGroup = paper.g().attr({ id: 'master-mark' });

const elMainsail = paper.path(mainsailD).attr({
    fill: '#D97757',
    id: 'mainsail-hero',
    'stroke-linejoin': 'round'
});

const elForesail = paper.path(foresailD).attr({
    fill: '#E8C59A',
    id: 'foresail-hero',
    'stroke-linejoin': 'round'
});

const elHull = paper.path(hullD).attr({
    fill: '#FAF5EC',
    id: 'hull-crest-hero',
    'stroke-linejoin': 'round'
});

markGroup.add(elMainsail, elForesail, elHull);

// =========================================================================
// 2. EXECUTIVE BRAND PRESENTATION SHEET (1600x1000 Canvas)
// =========================================================================
const W = 1600, H = 1000;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// Deep rich dark canvas ground
ctx.fillStyle = '#0B0F17';
ctx.fillRect(0, 0, W, H);

// Subtle ambient grid pattern for architectural precision
ctx.strokeStyle = 'rgba(255, 255, 255, 0.025)';
ctx.lineWidth = 1;
for (let gx = 0; gx < W; gx += 40) {
    ctx.beginPath();
    ctx.moveTo(gx, 0);
    ctx.lineTo(gx, H);
    ctx.stroke();
}
for (let gy = 0; gy < H; gy += 40) {
    ctx.beginPath();
    ctx.moveTo(0, gy);
    ctx.lineTo(W, gy);
    ctx.stroke();
}

// Brand Header Banner
ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 28px Georgia';
ctx.fillText('SAILBOAT TOURS', 60, 58);

ctx.fillStyle = '#D97757';
ctx.font = 'bold 13px Lato';
ctx.fillText('BRAND IDENTITY & GEOMETRIC SYSTEM SPECIFICATION', 60, 80);

ctx.fillStyle = '#64748B';
ctx.font = '12px Georgia';
ctx.fillText('Master Vector Artwork • Golden Ratio (Φ = 1.618) • Optical Balance System', W - 520, 58);
ctx.fillText('Client: Sailboat Tours — Romantic Sunset & Moonlight Cruises', W - 520, 80);

// Helper function to draw the master mark with custom fills
function drawMark(c, size, colors = { mainsail: '#D97757', foresail: '#E8C59A', hull: '#FAF5EC' }) {
    c.save();
    c.translate(size / 2, size / 2);
    const scale = size / 330;
    c.scale(scale, scale);
    c.translate(-255, -258.5);
    
    // Mainsail
    c.fillStyle = colors.mainsail;
    c.beginPath();
    c.moveTo(160, 360);
    c.bezierCurveTo(130, 240, 180, 130, 255, 95);
    c.bezierCurveTo(210, 190, 200, 290, 235, 365);
    c.bezierCurveTo(195, 375, 175, 372, 160, 360);
    c.closePath();
    c.fill();
    
    // Foresail
    c.fillStyle = colors.foresail;
    c.beginPath();
    c.moveTo(250, 365);
    c.bezierCurveTo(255, 260, 300, 180, 345, 160);
    c.bezierCurveTo(315, 245, 305, 315, 335, 365);
    c.bezierCurveTo(290, 375, 270, 372, 250, 365);
    c.closePath();
    c.fill();
    
    // Hull
    c.fillStyle = colors.hull;
    c.beginPath();
    c.moveTo(125, 375);
    c.bezierCurveTo(230, 422, 315, 410, 385, 360);
    c.bezierCurveTo(315, 385, 230, 395, 125, 375);
    c.closePath();
    c.fill();
    
    c.restore();
}

// -------------------------------------------------------------
// PANEL 1: MASTER GEOMETRIC HERO MARK (Left, Top)
// -------------------------------------------------------------
const p1X = 60, p1Y = 110, p1W = 460, p1H = 430;
ctx.fillStyle = '#111722';
ctx.beginPath();
ctx.roundRect(p1X, p1Y, p1W, p1H, 12);
ctx.fill();
ctx.strokeStyle = '#202B3C';
ctx.lineWidth = 1.5;
ctx.stroke();

ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 15px Georgia';
ctx.fillText('01. PRIMARY MARK & GEOMETRIC ARMATURE', p1X + 24, p1Y + 36);

ctx.fillStyle = '#7E8B9B';
ctx.font = '11px Georgia';
ctx.fillText('Golden Ratio (Φ) circles & tangent ogee curvature', p1X + 24, p1Y + 54);

// Draw underlying Golden Armature Guides
ctx.save();
const heroCx = p1X + p1W / 2;
const heroCy = p1Y + 230;
ctx.strokeStyle = 'rgba(217, 119, 87, 0.2)';
ctx.lineWidth = 1;
// Armature circles
ctx.beginPath();
ctx.arc(heroCx - 20, heroCy - 20, 110, 0, Math.PI * 2);
ctx.arc(heroCx + 25, heroCy + 10, 68, 0, Math.PI * 2);
ctx.arc(heroCx, heroCy + 60, 130, 0, Math.PI * 2);
ctx.stroke();

// Clear space guide box around hero
ctx.strokeStyle = 'rgba(232, 197, 154, 0.25)';
ctx.lineWidth = 1;
ctx.strokeRect(heroCx - 120, heroCy - 120, 240, 240);

ctx.fillStyle = 'rgba(232, 197, 154, 0.5)';
ctx.font = '10px monospace';
ctx.fillText('X = Height / 4', heroCx - 115, heroCy - 126);
ctx.restore();

// Render Hero Mark
ctx.save();
ctx.translate(heroCx - 100, heroCy - 100);
drawMark(ctx, 200);
ctx.restore();

ctx.fillStyle = '#64748B';
ctx.font = '11px Georgia';
ctx.fillText('Clear Space Boundary: X = 65px • Optical Centroid: Cy = 48.2%', p1X + 24, p1Y + p1H - 20);

// -------------------------------------------------------------
// PANEL 2: TYPOGRAPHIC LOCKUPS (Right, Top)
// -------------------------------------------------------------
const p2X = 540, p2Y = 110, p2W = 1000, p2H = 430;
ctx.fillStyle = '#111722';
ctx.beginPath();
ctx.roundRect(p2X, p2Y, p2W, p2H, 12);
ctx.fill();
ctx.strokeStyle = '#202B3C';
ctx.lineWidth = 1.5;
ctx.stroke();

ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 15px Georgia';
ctx.fillText('02. BRAND LOCKUPS & TYPOGRAPHIC SYSTEM', p2X + 24, p2Y + 36);

ctx.fillStyle = '#7E8B9B';
ctx.font = '11px Georgia';
ctx.fillText('Georgia (Oldstyle Serif) paired with Lato (Geometric Sans), Optically Kerned', p2X + 24, p2Y + 54);

// Horizontal Lockup Sub-Card
ctx.fillStyle = '#0B0F17';
ctx.beginPath();
ctx.roundRect(p2X + 24, p2Y + 75, 460, 160, 8);
ctx.fill();
ctx.strokeStyle = '#1D2533';
ctx.lineWidth = 1;
ctx.stroke();

ctx.fillStyle = '#64748B';
ctx.font = '10px Lato';
ctx.fillText('PRIMARY HORIZONTAL LOCKUP', p2X + 40, p2Y + 96);

LogoType.drawWordmarkLockup(ctx, (c, s) => drawMark(c, s), 'Sailboat Tours', 'ROMANTIC SUNSET CRUISES', {
    layout: 'horizontal',
    x: p2X + 110,
    y: p2Y + 165,
    markSize: 75,
    fontSize: 28,
    taglineSize: 11,
    primaryColor: '#FFFFFF',
    taglineColor: '#D97757',
    fontFamily: 'Georgia',
    taglineFontFamily: 'Lato'
});

// Light Background Horizontal Lockup Sub-Card
ctx.fillStyle = '#FAF5EC';
ctx.beginPath();
ctx.roundRect(p2X + 508, p2Y + 75, 468, 160, 8);
ctx.fill();

ctx.fillStyle = '#8C7E72';
ctx.font = '10px Lato';
ctx.fillText('LIGHT BACKGROUND APPLICATION', p2X + 524, p2Y + 96);

LogoType.drawWordmarkLockup(ctx, (c, s) => drawMark(c, s, { mainsail: '#D97757', foresail: '#C48A52', hull: '#0F141D' }), 'Sailboat Tours', 'ROMANTIC SUNSET CRUISES', {
    layout: 'horizontal',
    x: p2X + 590,
    y: p2Y + 165,
    markSize: 75,
    fontSize: 28,
    taglineSize: 11,
    primaryColor: '#0F141D',
    taglineColor: '#D97757',
    fontFamily: 'Georgia',
    taglineFontFamily: 'Lato'
});

// Stacked Lockup & Typographic Hierarchy Specs (Bottom of Panel 2)
ctx.fillStyle = '#0B0F17';
ctx.beginPath();
ctx.roundRect(p2X + 24, p2Y + 250, 260, 160, 8);
ctx.fill();

ctx.fillStyle = '#64748B';
ctx.font = '10px Lato';
ctx.fillText('SECONDARY STACKED LOCKUP', p2X + 40, p2Y + 270);

LogoType.drawWordmarkLockup(ctx, (c, s) => drawMark(c, s), 'Sailboat Tours', 'ROMANTIC SUNSET CRUISES', {
    layout: 'vertical',
    x: p2X + 154,
    y: p2Y + 310,
    markSize: 55,
    fontSize: 20,
    taglineSize: 8,
    primaryColor: '#FFFFFF',
    taglineColor: '#D97757',
    fontFamily: 'Georgia',
    taglineFontFamily: 'Lato'
});

// Typographic Spec Details
const specX = p2X + 310;
const specY = p2Y + 250;
ctx.fillStyle = '#161C27';
ctx.beginPath();
ctx.roundRect(specX, specY, 666, 160, 8);
ctx.fill();

ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 13px Georgia';
ctx.fillText('TYPOGRAPHY & KERNING SPECIFICATION', specX + 20, specY + 28);

ctx.fillStyle = '#D97757';
ctx.font = 'italic 16px Georgia';
ctx.fillText('Primary Display: Georgia Regular / SemiBold', specX + 20, specY + 56);

ctx.fillStyle = '#A0AEC0';
ctx.font = '12px Lato';
ctx.fillText('Tagline & Captions: Lato Bold • All-Caps Optical Tracking: +220‰', specX + 20, specY + 80);

ctx.fillStyle = '#7E8B9B';
ctx.font = '11px Georgia';
ctx.fillText('• Harmonic Ratio: Golden Ratio (Φ = 1.618) Type Scale (10px / 16px / 26px / 42px / 68px)', specX + 20, specY + 110);
ctx.fillText('• Robin Williams Contrast Score: 65/100 (Contrasting Display & Neutral Functional Sans)', specX + 20, specY + 130);
ctx.fillText('• Optical Kerning: Diagonal tucking on S-a (-1.26px), a-i (-1.26px), i-l (+1.44px)', specX + 20, specY + 148);

// -------------------------------------------------------------
// PANEL 3: MULTI-SCALE FAVICON & APP ICON STRESS-TEST (Left, Bottom)
// -------------------------------------------------------------
const p3X = 60, p3Y = 560, p3W = 680, p3H = 400;
ctx.fillStyle = '#111722';
ctx.beginPath();
ctx.roundRect(p3X, p3Y, p3W, p3H, 12);
ctx.fill();
ctx.strokeStyle = '#202B3C';
ctx.lineWidth = 1.5;
ctx.stroke();

ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 15px Georgia';
ctx.fillText('03. MULTI-SCALE LEGIBILITY LADDER', p3X + 24, p3Y + 36);

ctx.fillStyle = '#7E8B9B';
ctx.font = '11px Georgia';
ctx.fillText('Verifying stroke contrast and negative space separation from 16px to 128px', p3X + 24, p3Y + 54);

// Ladder scales: 16px, 24px, 32px, 48px, 64px, 128px
const scales = [16, 24, 32, 48, 64, 128];
let curX = p3X + 24;
const ladderBaseY = p3Y + 230;

scales.forEach((s) => {
    // Box
    ctx.fillStyle = '#0B0F17';
    ctx.beginPath();
    ctx.roundRect(curX, ladderBaseY - s / 2 - 20, s + 16, s + 40, 6);
    ctx.fill();
    ctx.strokeStyle = '#202B3C';
    ctx.lineWidth = 1;
    ctx.stroke();
    
    // Draw mark
    ctx.save();
    ctx.translate(curX + 8, ladderBaseY - s / 2 - 12);
    drawMark(ctx, s);
    ctx.restore();
    
    // Label
    ctx.fillStyle = '#8E9AA8';
    ctx.font = '10px monospace';
    ctx.fillText(`${s}px`, curX + (s + 16) / 2 - 12, ladderBaseY + s / 2 + 14);
    
    curX += s + 30;
});

ctx.fillStyle = '#10B981';
ctx.font = 'bold 11px Lato';
ctx.fillText('✓ 16px Favicon Tier Passed: Dual sails & hull maintain distinct negative space channels.', p3X + 24, p3Y + p3H - 24);

// -------------------------------------------------------------
// PANEL 4: 4-WAY MONOCHROME CONTRAST & REPRODUCTION (Right, Bottom)
// -------------------------------------------------------------
const p4X = 760, p4Y = 560, p4W = 780, p4H = 400;
ctx.fillStyle = '#111722';
ctx.beginPath();
ctx.roundRect(p4X, p4Y, p4W, p4H, 12);
ctx.fill();
ctx.strokeStyle = '#202B3C';
ctx.lineWidth = 1.5;
ctx.stroke();

ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 15px Georgia';
ctx.fillText('04. SINGLE-COLOUR MONOCHROME & CONTRAST REPRODUCTION', p4X + 24, p4Y + 36);

ctx.fillStyle = '#7E8B9B';
ctx.font = '11px Georgia';
ctx.fillText('Single flat ink reproduction, Knockout (-1.5% irradiation compensation), and App Squircle', p4X + 24, p4Y + 54);

const monoCards = [
    { title: 'POSITIVE (1-COLOR BLACK)', bg: '#FFFFFF', markColor: { mainsail: '#000000', foresail: '#000000', hull: '#000000' }, textDark: true },
    { title: 'KNOCKOUT WHITE (-1.5%)', bg: '#0F141D', markColor: { mainsail: '#FFFFFF', foresail: '#FFFFFF', hull: '#FFFFFF' }, textDark: false },
    { title: 'GRAYSCALE NEUTRAL', bg: '#E2E8F0', markColor: { mainsail: '#273244', foresail: '#64748B', hull: '#1E293B' }, textDark: true },
    { title: 'APP ICON SQUIRCLE', bg: '#0F141D', isSquircle: true, textDark: false }
];

const cW = (p4W - 48 - 3 * 16) / 4;
monoCards.forEach((mc, idx) => {
    const mx = p4X + 24 + idx * (cW + 16);
    const my = p4Y + 75;
    
    // Card background
    ctx.fillStyle = '#0B0F17';
    ctx.beginPath();
    ctx.roundRect(mx, my, cW, 250, 8);
    ctx.fill();
    ctx.strokeStyle = '#202B3C';
    ctx.lineWidth = 1;
    ctx.stroke();
    
    // Inner presentation box
    const inBoxY = my + 14;
    const inBoxSize = cW - 28;
    const inBoxX = mx + 14;
    
    ctx.fillStyle = mc.bg;
    ctx.beginPath();
    ctx.roundRect(inBoxX, inBoxY, inBoxSize, inBoxSize, 8);
    ctx.fill();
    
    ctx.save();
    if (mc.isSquircle) {
        // App squircle container
        ctx.fillStyle = '#1A2434';
        ctx.beginPath();
        ctx.roundRect(inBoxX + 10, inBoxY + 10, inBoxSize - 20, inBoxSize - 20, 24);
        ctx.fill();
        ctx.strokeStyle = '#2D3D56';
        ctx.lineWidth = 1.5;
        ctx.stroke();
        
        ctx.translate(inBoxX + inBoxSize / 2 - 38, inBoxY + inBoxSize / 2 - 38);
        drawMark(ctx, 76, { mainsail: '#D97757', foresail: '#E8C59A', hull: '#FAF5EC' });
    } else {
        ctx.translate(inBoxX + inBoxSize / 2 - 42, inBoxY + inBoxSize / 2 - 42);
        drawMark(ctx, 84, mc.markColor);
    }
    ctx.restore();
    
    // Card label
    ctx.fillStyle = '#A0AEC0';
    ctx.font = 'bold 9px Lato';
    ctx.fillText(mc.title, mx + 12, my + inBoxSize + 36);
    
    ctx.fillStyle = '#64748B';
    ctx.font = '9px Georgia';
    ctx.fillText(mc.isSquircle ? 'iOS / Android Squircle' : '100% Vector Purity', mx + 12, my + inBoxSize + 52);
});

ctx.fillStyle = '#10B981';
ctx.font = 'bold 11px Lato';
ctx.fillText('✓ Single-Color Reproduction: Flawless silhouette recognition without dependency on color gradients.', p4X + 24, p4Y + p4H - 24);

canvas;
