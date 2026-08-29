Stage.begin('Palette & Type');
Stage.note('Calibrating brand palette and typographic harmony: evaluating font pairing, optical kerning, and typographic scale.');

const pairing = LogoType.evaluateFontPairing('oldstyle', 'sans-serif');
console.log('Font Pairing Evaluation: ' + JSON.stringify(pairing));

const typeScale = LogoType.calculateTypographicScale(16, 'goldenRatio', 1, 4);
console.log('Typographic Scale (Golden Ratio): ' + JSON.stringify(typeScale));

const trackingWordmark = LogoType.computeWordmarkTracking(36, false, 'wordmark');
const trackingTagline = LogoType.computeWordmarkTracking(14, true, 'tagline');
console.log('Tracking - Wordmark: ' + trackingWordmark + ', Tagline: ' + trackingTagline);

const kernST = LogoType.computeOpticalKerning('S', 'a', 36, 'serif');
const kernai = LogoType.computeOpticalKerning('a', 'i', 36, 'serif');
const kernil = LogoType.computeOpticalKerning('i', 'l', 36, 'serif');
console.log(`Kerning sample - S-a: ${kernST}px, a-i: ${kernai}px, i-l: ${kernil}px`);

const W = 1200, H = 600;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#0F141D';
ctx.fillRect(0, 0, W, H);

// Title
ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 22px Georgia';
ctx.fillText('STAGE 5: PALETTE & TYPOGRAPHIC HARMONY', 50, 48);

ctx.fillStyle = '#8E9AA8';
ctx.font = '13px Georgia';
ctx.fillText('Robin Williams Contrast Matrix, Typographic Scale & Automated Brand Lockup', 50, 72);

// Draw Wordmark Lockups (Horizontal & Vertical)
function drawMarkAt(c, size) {
    c.save();
    const scale = size / 260; // mark width is 260
    c.scale(scale, scale);
    c.translate(-255, -258.5); // center of mark
    
    // Mainsail
    c.fillStyle = '#D97757';
    c.beginPath();
    const mainsailPath = "M 160 360 C 130 240 180 130 255 95 C 210 190 200 290 235 365 C 195 375 175 372 160 360 Z";
    // Foresail
    const foresailPath = "M 250 365 C 255 260 300 180 345 160 C 315 245 305 315 335 365 C 290 375 270 372 250 365 Z";
    // Hull
    const hullPath = "M 125 375 C 230 422 315 410 385 360 C 315 385 230 395 125 375 Z";
    
    // Fill paths directly using SVG path string logic or bezier calls
    // Using Canvas bezier equivalents
    c.beginPath();
    c.moveTo(160, 360);
    c.bezierCurveTo(130, 240, 180, 130, 255, 95);
    c.bezierCurveTo(210, 190, 200, 290, 235, 365);
    c.bezierCurveTo(195, 375, 175, 372, 160, 360);
    c.closePath();
    c.fill();
    
    c.fillStyle = '#E8C59A';
    c.beginPath();
    c.moveTo(250, 365);
    c.bezierCurveTo(255, 260, 300, 180, 345, 160);
    c.bezierCurveTo(315, 245, 305, 315, 335, 365);
    c.bezierCurveTo(290, 375, 270, 372, 250, 365);
    c.closePath();
    c.fill();
    
    c.fillStyle = '#FAF5EC';
    c.beginPath();
    c.moveTo(125, 375);
    c.bezierCurveTo(230, 422, 315, 410, 385, 360);
    c.bezierCurveTo(315, 385, 230, 395, 125, 375);
    c.closePath();
    c.fill();
    
    c.restore();
}

// Left Card: Horizontal Lockup
ctx.fillStyle = '#161C27';
ctx.beginPath();
ctx.roundRect(50, 110, 520, 220, 12);
ctx.fill();
ctx.strokeStyle = '#273244';
ctx.lineWidth = 1.5;
ctx.stroke();

ctx.fillStyle = '#7E8B9B';
ctx.font = '11px Georgia';
ctx.fillText('PRIMARY HORIZONTAL LOCKUP', 70, 135);

LogoType.drawWordmarkLockup(ctx, drawMarkAt, 'Sailboat Tours', 'ROMANTIC SUNSET CRUISES', {
    layout: 'horizontal',
    x: 70,
    y: 190,
    markSize: 100,
    fontSize: 34,
    taglineSize: 12,
    primaryColor: '#FFFFFF',
    taglineColor: '#D97757',
    fontFamily: 'Georgia',
    taglineFontFamily: 'Lato'
});

// Right Card: Stacked / Vertical Lockup
ctx.fillStyle = '#161C27';
ctx.beginPath();
ctx.roundRect(610, 110, 540, 220, 12);
ctx.fill();
ctx.strokeStyle = '#273244';
ctx.lineWidth = 1.5;
ctx.stroke();

ctx.fillStyle = '#7E8B9B';
ctx.font = '11px Georgia';
ctx.fillText('SECONDARY STACKED LOCKUP', 630, 135);

LogoType.drawWordmarkLockup(ctx, drawMarkAt, 'Sailboat Tours', 'ROMANTIC SUNSET CRUISES', {
    layout: 'vertical',
    x: 880,
    y: 160,
    markSize: 75,
    fontSize: 26,
    taglineSize: 10,
    primaryColor: '#FFFFFF',
    taglineColor: '#D97757',
    fontFamily: 'Georgia',
    taglineFontFamily: 'Lato'
});

// Bottom Card: Color Palette Specification
ctx.fillStyle = '#161C27';
ctx.beginPath();
ctx.roundRect(50, 360, 1100, 190, 12);
ctx.fill();
ctx.strokeStyle = '#273244';
ctx.lineWidth = 1.5;
ctx.stroke();

ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 14px Georgia';
ctx.fillText('BRAND COLOR SYSTEM — SUNSET ROMANCE', 70, 390);

const paletteSpecs = [
    { name: 'Midnight Abyssal', hex: '#0F141D', role: 'Primary Ground / Dark Field', dark: true },
    { name: 'Sunset Terracotta', hex: '#D97757', role: 'Hero Primary Accent / Mainsail', dark: false },
    { name: 'Champagne Gold', hex: '#E8C59A', role: 'Secondary Warm Glow / Foresail', dark: false },
    { name: 'Lustrous Pearl', hex: '#FAF5EC', role: 'Light Ground / Keel Wave Accent', dark: false },
    { name: 'Warm Charcoal', hex: '#273244', role: 'Structure / Border / Subtle Midtone', dark: true }
];

const pW = (1100 - 40 - 4 * 20) / 5;
paletteSpecs.forEach((spec, i) => {
    const px = 70 + i * (pW + 20);
    const py = 415;
    
    // Swatch box
    ctx.fillStyle = spec.hex;
    ctx.beginPath();
    ctx.roundRect(px, py, pW, 60, 6);
    ctx.fill();
    ctx.strokeStyle = '#3A475C';
    ctx.lineWidth = 1;
    ctx.stroke();
    
    // Labels
    ctx.fillStyle = '#FFFFFF';
    ctx.font = 'bold 12px Georgia';
    ctx.fillText(spec.name, px, py + 80);
    
    ctx.fillStyle = '#D97757';
    ctx.font = 'bold 11px monospace';
    ctx.fillText(spec.hex, px, py + 96);
    
    ctx.fillStyle = '#7E8B9B';
    ctx.font = '10px Georgia';
    ctx.fillText(spec.role, px, py + 112);
});

canvas;
