// Executive Typographic & Brand Identity Lockup Board
// Demonstrates LogoTypeToolkit, VectorLogoToolkit, and LogoDesignToolkit in Jint

const width = 1200;
const height = 850;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');

// 1. Deep Midnight Studio Background
ctx.fillStyle = '#090d16';
ctx.fillRect(0, 0, width, height);

// Background subtle grid
ctx.strokeStyle = '#1e293b';
ctx.lineWidth = 1;
ctx.globalAlpha = 0.35;
Logo.drawIsometricGrid(ctx, width, height, 50);
ctx.globalAlpha = 1.0;

// Header / Title Block
ctx.fillStyle = '#f8fafc';
ctx.font = '700 28px sans-serif';
ctx.fillText('NEXUS DYNAMICS — BRAND IDENTITY & TYPOGRAPHY', 50, 55);

ctx.fillStyle = '#64748b';
ctx.font = '500 13px sans-serif';
ctx.fillText('DESIGN PRINCIPLES SYNTHESIZED FROM DOYALD YOUNG & ROBIN WILLIAMS', 50, 80);

// Helper function to draw the Nexus geometric mark
function drawNexusMark(c, size) {
    const cx = size * 0.5;
    const cy = size * 0.5;
    const r = size * 0.44;

    // Gradient background badge squircle
    const grad = c.createLinearGradient(0, 0, size, size);
    grad.addColorStop(0, '#0284c7');
    grad.addColorStop(1, '#6366f1');
    c.fillStyle = grad;
    c.drawSquircle(0, 0, size, size, { exponent: 4.5, fill: true, stroke: false });

    // Inner geometric N-core
    c.strokeStyle = '#ffffff';
    c.lineWidth = size * 0.1;
    c.lineCap = 'round';
    c.lineJoin = 'round';

    const p = size * 0.28;
    c.beginPath();
    c.moveTo(p, size - p);
    c.lineTo(p, p);
    c.lineTo(size - p, size - p);
    c.lineTo(size - p, p);
    c.stroke();
}

// ----------------------------------------------------
// Panel 1: Primary Horizontal Lockup (Top Left)
// ----------------------------------------------------
ctx.fillStyle = '#0f172a';
ctx.strokeStyle = '#334155';
ctx.lineWidth = 1.5;
ctx.drawSquircle(50, 110, 530, 200, { exponent: 4.0, fill: true, stroke: true });

ctx.fillStyle = '#38bdf8';
ctx.font = '600 11px sans-serif';
ctx.fillText('PRIMARY HORIZONTAL LOCKUP (WEB / NAV)', 70, 138);

ctx.drawWordmarkLockup(
    drawNexusMark,
    'NEXUS',
    'AUTONOMOUS SYSTEMS',
    {
        layout: 'horizontal',
        x: 70,
        y: 175,
        markSize: 85,
        fontSize: 44,
        taglineSize: 11,
        primaryColor: '#ffffff',
        taglineColor: '#38bdf8'
    }
);

// ----------------------------------------------------
// Panel 2: Stacked / Vertical Lockup (Top Right)
// ----------------------------------------------------
ctx.fillStyle = '#0f172a';
ctx.strokeStyle = '#334155';
ctx.lineWidth = 1.5;
ctx.drawSquircle(620, 110, 530, 340, { exponent: 4.0, fill: true, stroke: true });

ctx.fillStyle = '#38bdf8';
ctx.font = '600 11px sans-serif';
ctx.fillText('VERTICAL / STACKED LOCKUP (SEAL & SPLASH)', 640, 138);

ctx.drawWordmarkLockup(
    drawNexusMark,
    'NEXUS',
    'ADVANCED AUTONOMOUS SYSTEMS',
    {
        layout: 'vertical',
        x: 835,
        y: 165,
        markSize: 100,
        fontSize: 38,
        taglineSize: 11,
        primaryColor: '#ffffff',
        taglineColor: '#94a3b8'
    }
);

// ----------------------------------------------------
// Panel 3: Optical Kerning & Spacing Mechanics (Bottom Left)
// ----------------------------------------------------
ctx.fillStyle = '#0f172a';
ctx.strokeStyle = '#334155';
ctx.lineWidth = 1.5;
ctx.drawSquircle(50, 330, 530, 470, { exponent: 4.0, fill: true, stroke: true });

ctx.fillStyle = '#38bdf8';
ctx.font = '600 11px sans-serif';
ctx.fillText('OPTICAL KERNING MATRIX (DOYALD YOUNG GEOMETRY)', 70, 358);

// Kerning pairs demonstration
const pairs = [
    { label: 'Straight-to-Straight (H | H)', text: 'HH', desc: '1.00x Maximum Spacing', offset: LogoType.computeOpticalKerning('H', 'H', 36) },
    { label: 'Straight-to-Round (H | O)', text: 'HO', desc: '0.75x Medium Spacing', offset: LogoType.computeOpticalKerning('H', 'O', 36) },
    { label: 'Round-to-Round (O | O)', text: 'OO', desc: '0.50x Tight Spacing', offset: LogoType.computeOpticalKerning('O', 'O', 36) },
    { label: 'Diagonal Tucking (T | A)', text: 'TA', desc: 'Negative Optical Tuck', offset: LogoType.computeOpticalKerning('T', 'A', 36) }
];

let pairY = 395;
pairs.forEach((p) => {
    ctx.fillStyle = '#e2e8f0';
    ctx.font = '700 24px monospace';
    ctx.fillText(p.text, 75, pairY);

    ctx.fillStyle = '#38bdf8';
    ctx.font = '600 13px sans-serif';
    ctx.fillText(p.label, 130, pairY - 4);

    ctx.fillStyle = '#64748b';
    ctx.font = '400 12px sans-serif';
    ctx.fillText(p.desc + ` (offset: ${p.offset.toFixed(2)}px)`, 130, pairY + 12);

    pairY += 58;
});

// Doyald Young Ogee Swash
ctx.fillStyle = '#94a3b8';
ctx.font = '600 12px sans-serif';
ctx.fillText('DOYALD YOUNG OGEE CURVE (S-CURVE / SWASH):', 70, 660);

ctx.strokeStyle = '#f59e0b';
ctx.lineWidth = 3;
ctx.lineCap = 'round';
ctx.drawOgeeCurve(70, 720, 530, 720, 35, 0.5);

ctx.fillStyle = '#f59e0b';
ctx.font = '500 11px monospace';
ctx.fillText('Inflection Point (t = 0.50)', 245, 745);

// ----------------------------------------------------
// Panel 4: Typographic Scale & Contrast (Bottom Right)
// ----------------------------------------------------
ctx.fillStyle = '#0f172a';
ctx.strokeStyle = '#334155';
ctx.lineWidth = 1.5;
ctx.drawSquircle(620, 470, 530, 330, { exponent: 4.0, fill: true, stroke: true });

ctx.fillStyle = '#38bdf8';
ctx.font = '600 11px sans-serif';
ctx.fillText('HARMONIC TYPOGRAPHIC SCALE (GOLDEN RATIO Φ = 1.618)', 640, 498);

const scale = LogoType.calculateTypographicScale(14, 'goldenRatio', 1, 3);
const steps = scale.steps;

let scaleY = 535;
for (let i = 0; i < steps.length; i++) {
    const s = steps[i];
    ctx.fillStyle = '#f8fafc';
    ctx.font = `700 ${Math.min(28, s.size)}px sans-serif`;
    ctx.fillText(`${s.name.toUpperCase()}`, 645, scaleY);

    ctx.fillStyle = '#64748b';
    ctx.font = '400 12px monospace';
    ctx.fillText(`${s.size}px / ${s.lineHeight}px line-height (tracking: ${s.tracking})`, 760, scaleY);

    scaleY += 46;
}

// Pairing evaluation
const evalRes = LogoType.evaluateFontPairing('sansSerif', 'modernSerif');
ctx.fillStyle = '#10b981';
ctx.font = '600 12px sans-serif';
ctx.fillText(`FONT HARMONY: ${evalRes.relationship.toUpperCase()} (Score: ${evalRes.score}/100)`, 645, 765);

ctx.fillStyle = '#94a3b8';
ctx.font = '400 11px sans-serif';
ctx.fillText(evalRes.description, 645, 783);

canvas;
