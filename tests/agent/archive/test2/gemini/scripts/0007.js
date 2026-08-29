Stage.begin('Ideation');
Stage.note('Constructing 3 distinct vector mark candidates grounded in Golden Ratio circles, ogee sweeps, and tangent fillets.');

const W = 1350, H = 550;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#0F141D';
ctx.fillRect(0, 0, W, H);

// Title
ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 22px Georgia';
ctx.fillText('STAGE 3: GEOMETRIC IDEATION & VECTOR CANDIDATES', 50, 48);

ctx.fillStyle = '#8E9AA8';
ctx.font = '13px Georgia';
ctx.fillText('Constructed with Golden Ratio Circles (Φ = 1.618) and Geometric Armatures', 50, 72);

// Function to draw Candidate 1: Entwined Golden Sails (Two sails in harmonic embrace)
function drawCandidate1(c, cx, cy, s, showArmature = false) {
    c.save();
    c.translate(cx, cy);
    const scale = s / 100;
    c.scale(scale, scale);
    
    // Base geometry coordinates (normalized to 100x100 space, centered at 0,0)
    // Phi proportions: R0 = 42, R1 = 26, R2 = 16
    if (showArmature) {
        c.strokeStyle = 'rgba(217, 130, 76, 0.25)';
        c.lineWidth = 1;
        c.beginPath();
        c.arc(0, 5, 42, 0, Math.PI * 2);
        c.arc(10, -5, 26, 0, Math.PI * 2);
        c.arc(-12, 12, 16, 0, Math.PI * 2);
        c.stroke();
    }
    
    // Main Sail (Left/Back - larger golden sweep)
    c.fillStyle = '#D9824C';
    c.beginPath();
    c.moveTo(-28, 28);
    // Outer luff curve
    c.bezierCurveTo(-34, 0, -20, -32, 2, -44);
    // Leech inner curve
    c.bezierCurveTo(-10, -18, -12, 6, -6, 28);
    c.closePath();
    c.fill();
    
    // Fore Sail / Spinnaker (Right - embracing curve)
    c.fillStyle = '#E8C59A';
    c.beginPath();
    c.moveTo(-2, 28);
    c.bezierCurveTo(0, 8, 12, -22, 28, -26);
    c.bezierCurveTo(18, -4, 16, 14, 24, 28);
    c.closePath();
    c.fill();
    
    // Hull / Wave Base (Lyrical crescent base tying them together)
    c.fillStyle = '#FAF5EC';
    c.beginPath();
    c.moveTo(-36, 32);
    c.bezierCurveTo(-12, 42, 16, 40, 36, 28);
    c.bezierCurveTo(14, 34, -14, 35, -36, 32);
    c.closePath();
    c.fill();
    
    c.restore();
}

// Function to draw Candidate 2: Crescent Moon & Billow
function drawCandidate2(c, cx, cy, s, showArmature = false) {
    c.save();
    c.translate(cx, cy);
    const scale = s / 100;
    c.scale(scale, scale);
    
    if (showArmature) {
        c.strokeStyle = 'rgba(61, 122, 148, 0.25)';
        c.lineWidth = 1;
        c.beginPath();
        c.arc(0, 0, 44, 0, Math.PI * 2);
        c.arc(12, -4, 36, 0, Math.PI * 2);
        c.stroke();
    }
    
    // Crescent Outer Sail / Moon
    c.fillStyle = '#3D7A94';
    c.beginPath();
    c.moveTo(6, -42);
    c.bezierCurveTo(-28, -30, -38, 10, -16, 38);
    c.bezierCurveTo(-26, 12, -18, -18, 6, -42);
    c.closePath();
    c.fill();
    
    // Inner Billowed Sail
    c.fillStyle = '#96C0CE';
    c.beginPath();
    c.moveTo(8, -38);
    c.bezierCurveTo(28, -18, 32, 12, 14, 34);
    c.bezierCurveTo(12, 8, 4, -16, 8, -38);
    c.closePath();
    c.fill();
    
    // Gentle keel stroke
    c.fillStyle = '#F0F7F9';
    c.beginPath();
    c.moveTo(-24, 38);
    c.bezierCurveTo(0, 46, 18, 44, 30, 36);
    c.bezierCurveTo(16, 39, -4, 40, -24, 38);
    c.closePath();
    c.fill();
    
    c.restore();
}

// Function to draw Candidate 3: Continuous Monogrammatic "S" Sail
function drawCandidate3(c, cx, cy, s, showArmature = false) {
    c.save();
    c.translate(cx, cy);
    const scale = s / 100;
    c.scale(scale, scale);
    
    if (showArmature) {
        c.strokeStyle = 'rgba(198, 155, 123, 0.25)';
        c.lineWidth = 1;
        c.beginPath();
        c.arc(-4, -16, 24, 0, Math.PI * 2);
        c.arc(4, 16, 24, 0, Math.PI * 2);
        c.stroke();
    }
    
    // Upper "S" sweep / Top Sail
    c.fillStyle = '#C69B7B';
    c.beginPath();
    c.moveTo(18, -38);
    c.bezierCurveTo(-8, -44, -28, -24, -14, -4);
    c.bezierCurveTo(-4, -18, 12, -26, 18, -38);
    c.closePath();
    c.fill();
    
    // Lower "S" sweep / Hull & Sail Wave
    c.fillStyle = '#EBD3BE';
    c.beginPath();
    c.moveTo(-14, -2);
    c.bezierCurveTo(18, 14, 28, 38, -6, 42);
    c.bezierCurveTo(12, 28, 4, 10, -14, -2);
    c.closePath();
    c.fill();
    
    c.restore();
}

const candidates = [
    { name: 'CANDIDATE 1: ENTWINED GOLDEN SAILS', subtitle: 'Two harmonic sails in intimate couple embrace', draw: drawCandidate1 },
    { name: 'CANDIDATE 2: CRESCENT MOON & BILLOW', subtitle: 'Moonlight voyage with spinnaker silhouette', draw: drawCandidate2 },
    { name: 'CANDIDATE 3: MONOGRAMMATIC S-SAIL', subtitle: 'Fluid S-curve forming wind and sea', draw: drawCandidate3 }
];

const colW = 380;
const gapX = 30;

candidates.forEach((cand, idx) => {
    const x = 50 + idx * (colW + gapX);
    
    // Card container
    ctx.fillStyle = '#161C27';
    ctx.beginPath();
    ctx.roundRect(x, 110, colW, 400, 12);
    ctx.fill();
    ctx.strokeStyle = '#273244';
    ctx.lineWidth = 1.5;
    ctx.stroke();
    
    ctx.fillStyle = '#FFFFFF';
    ctx.font = 'bold 14px Georgia';
    ctx.fillText(cand.name, x + 20, 140);
    
    ctx.fillStyle = '#7E8B9B';
    ctx.font = '11px Georgia';
    ctx.fillText(cand.subtitle, x + 20, 160);
    
    // Top box: Geometric Armature + Mark
    ctx.fillStyle = '#10141D';
    ctx.beginPath();
    ctx.roundRect(x + 20, 180, (colW - 50) / 2, 150, 8);
    ctx.fill();
    
    ctx.fillStyle = '#5A6678';
    ctx.font = '10px Georgia';
    ctx.fillText('ARMATURE', x + 28, 200);
    cand.draw(ctx, x + 20 + (colW - 50) / 4, 265, 95, true);
    
    // Bottom box: Clean Mark
    ctx.fillStyle = '#10141D';
    ctx.beginPath();
    ctx.roundRect(x + 30 + (colW - 50) / 2, 180, (colW - 50) / 2, 150, 8);
    ctx.fill();
    
    ctx.fillStyle = '#5A6678';
    ctx.font = '10px Georgia';
    ctx.fillText('ISOLATED MARK', x + 38 + (colW - 50) / 2, 200);
    cand.draw(ctx, x + 30 + (colW - 50) * 0.75, 265, 95, false);
    
    // Contrast check row (White ground vs Dark ground vs 16px)
    const testY = 350;
    
    // White ground badge
    ctx.fillStyle = '#FFFFFF';
    ctx.beginPath();
    ctx.roundRect(x + 20, testY, 80, 80, 8);
    ctx.fill();
    
    // Draw monochrome black
    ctx.save();
    ctx.translate(x + 60, testY + 40);
    ctx.scale(0.55, 0.55);
    // Draw mark in solid black
    ctx.fillStyle = '#000000';
    // Simplified silhouette test
    cand.draw({
        save: () => {},
        restore: () => {},
        translate: () => {},
        scale: () => {},
        beginPath: () => ctx.beginPath(),
        moveTo: (px, py) => ctx.moveTo(px, py),
        lineTo: (px, py) => ctx.lineTo(px, py),
        bezierCurveTo: (c1, c2, c3, c4, px, py) => ctx.bezierCurveTo(c1, c2, c3, c4, px, py),
        closePath: () => ctx.closePath(),
        fill: () => { ctx.fillStyle = '#000000'; ctx.fill(); },
        stroke: () => {},
        arc: () => {}
    }, 0, 0, 100, false);
    ctx.restore();
    
    ctx.fillStyle = '#7E8B9B';
    ctx.font = '9px Georgia';
    ctx.fillText('MONO POSITIVE', x + 20, testY + 98);
    
    // Dark ground knockout
    ctx.fillStyle = '#0B0E14';
    ctx.beginPath();
    ctx.roundRect(x + 115, testY, 80, 80, 8);
    ctx.fill();
    ctx.strokeStyle = '#273244';
    ctx.lineWidth = 1;
    ctx.stroke();
    
    ctx.save();
    ctx.translate(x + 155, testY + 40);
    ctx.scale(0.55, 0.55);
    cand.draw({
        save: () => {},
        restore: () => {},
        translate: () => {},
        scale: () => {},
        beginPath: () => ctx.beginPath(),
        moveTo: (px, py) => ctx.moveTo(px, py),
        lineTo: (px, py) => ctx.lineTo(px, py),
        bezierCurveTo: (c1, c2, c3, c4, px, py) => ctx.bezierCurveTo(c1, c2, c3, c4, px, py),
        closePath: () => ctx.closePath(),
        fill: () => { ctx.fillStyle = '#FFFFFF'; ctx.fill(); },
        stroke: () => {},
        arc: () => {}
    }, 0, 0, 100, false);
    ctx.restore();
    
    ctx.fillStyle = '#7E8B9B';
    ctx.font = '9px Georgia';
    ctx.fillText('KNOCKOUT', x + 125, testY + 98);
    
    // 16px mini test container
    ctx.fillStyle = '#1E2635';
    ctx.beginPath();
    ctx.roundRect(x + 210, testY, 140, 80, 8);
    ctx.fill();
    
    ctx.fillStyle = '#A0AEC0';
    ctx.font = '10px Georgia';
    ctx.fillText('16px & 24px FAVICON', x + 220, testY + 22);
    
    // Draw 16px scale
    cand.draw(ctx, x + 240, testY + 50, 16, false);
    // Draw 24px scale
    cand.draw(ctx, x + 280, testY + 50, 24, false);
    // Draw 32px scale
    cand.draw(ctx, x + 325, testY + 50, 32, false);
});

canvas;
