Stage.begin('Stress test');
Stage.note('Running multi-scale favicon legibility ladder (16px to 256px) and 4-way monochrome contrast testing.');

function drawVectorMark(c, size) {
    c.save();
    const scale = size / 260; // mark width is 260
    c.scale(scale, scale);
    c.translate(-255, -258.5);
    
    // Mainsail
    c.fillStyle = '#D97757';
    c.beginPath();
    c.moveTo(160, 360);
    c.bezierCurveTo(130, 240, 180, 130, 255, 95);
    c.bezierCurveTo(210, 190, 200, 290, 235, 365);
    c.bezierCurveTo(195, 375, 175, 372, 160, 360);
    c.closePath();
    c.fill();
    
    // Foresail
    c.fillStyle = '#E8C59A';
    c.beginPath();
    c.moveTo(250, 365);
    c.bezierCurveTo(255, 260, 300, 180, 345, 160);
    c.bezierCurveTo(315, 245, 305, 315, 335, 365);
    c.bezierCurveTo(290, 375, 270, 372, 250, 365);
    c.closePath();
    c.fill();
    
    // Hull
    c.fillStyle = '#FAF5EC';
    c.beginPath();
    c.moveTo(125, 375);
    c.bezierCurveTo(230, 422, 315, 410, 385, 360);
    c.bezierCurveTo(315, 385, 230, 395, 125, 375);
    c.closePath();
    c.fill();
    
    c.restore();
}

const W = 1200, H = 500;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#0F141D';
ctx.fillRect(0, 0, W, H);

// Generate Favicon Scale Test Ladder
Logo.generateFaviconScaleTest(ctx, drawVectorMark, {
    width: W,
    height: H,
    backgroundColor: '#0F141D',
    textColor: '#FFFFFF',
    accentColor: '#D97757'
});

canvas;
