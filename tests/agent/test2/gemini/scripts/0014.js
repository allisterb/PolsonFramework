Stage.begin('Stress test');
Stage.note('Generating 4-way monochrome & contrast test board (Positive Black, Negative White Knockout, Grayscale, App Icon Squircle).');

function drawMonochromeMark(c, size) {
    c.save();
    c.translate(size / 2, size / 2);
    const scale = size / 330;
    c.scale(scale, scale);
    c.translate(-255, -258.5);
    
    // Mainsail
    c.beginPath();
    c.moveTo(160, 360);
    c.bezierCurveTo(130, 240, 180, 130, 255, 95);
    c.bezierCurveTo(210, 190, 200, 290, 235, 365);
    c.bezierCurveTo(195, 375, 175, 372, 160, 360);
    c.closePath();
    c.fill();
    
    // Foresail
    c.beginPath();
    c.moveTo(250, 365);
    c.bezierCurveTo(255, 260, 300, 180, 345, 160);
    c.bezierCurveTo(315, 245, 305, 315, 335, 365);
    c.bezierCurveTo(290, 375, 270, 372, 250, 365);
    c.closePath();
    c.fill();
    
    // Hull
    c.beginPath();
    c.moveTo(125, 375);
    c.bezierCurveTo(230, 422, 315, 410, 385, 360);
    c.bezierCurveTo(315, 385, 230, 395, 125, 375);
    c.closePath();
    c.fill();
    
    c.restore();
}

const W = 1100, H = 700;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

Logo.generateMonochromeTest(ctx, drawMonochromeMark, W, H);

canvas;
