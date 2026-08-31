Stage.begin('Penciler');
Stage.note('Canvas 1024x1024, white ground. Figure tilted 30deg CW. Primary volumes mapped.');

const ANCHORS = {
  headCenter: { x: 512, y: 220 },
  headRadius: 100,
  torsoBBox: { x: 422, y: 310, w: 180, h: 260 },
  leftArm: [
    { x: 580, y: 310 }, // shoulder
    { x: 720, y: 220 }, // elbow
    { x: 750, y: 120 }  // fist
  ],
  rightArm: [
    { x: 440, y: 350 }, // shoulder
    { x: 380, y: 440 }, // elbow 
    { x: 350, y: 360 }  // hand behind cape
  ],
  leftLeg: [
    { x: 450, y: 550 }, // hip
    { x: 400, y: 680 }, // knee
    { x: 420, y: 750 }  // foot
  ],
  rightLeg: [
    { x: 520, y: 570 }, // hip
    { x: 460, y: 760 }, // knee
    { x: 400, y: 920 }  // foot
  ],
  capeControlPoints: [
    // Top left drape
    { cp1x: 200, cp1y: 400, cp2x: 250, cp2y: 700, x: 350, y: 800 },
    // Bottom flow
    { cp1x: 450, cp1y: 900, cp2x: 650, cp2y: 850, x: 700, y: 700 },
    // Right return
    { cp1x: 750, cp1y: 550, cp2x: 700, cp2y: 350, x: 600, y: 320 }
  ],
  crestPos: { x: 520, y: 390, w: 80, h: 60 },
  beltPos: { x: 485, y: 550, w: 160, h: 40 }
};

Session['ANCHORS'] = ANCHORS;

const canvas = createCanvas(1024, 1024);
const ctx = canvas.getContext('2d');

ctx.fillStyle = '#ffffff';
ctx.fillRect(0, 0, 1024, 1024);

const BLUE = '#4a90e2';
const GRAPHITE = '#444444';

ctx.lineWidth = 2;
ctx.strokeStyle = BLUE;

// Head
ctx.beginPath();
ctx.arc(ANCHORS.headCenter.x, ANCHORS.headCenter.y, ANCHORS.headRadius, 0, Math.PI * 2);
ctx.stroke();

// Face cross
ctx.save();
ctx.translate(ANCHORS.headCenter.x, ANCHORS.headCenter.y);
ctx.rotate(30 * Math.PI / 180);
ctx.beginPath();
ctx.moveTo(-ANCHORS.headRadius, 0);
ctx.lineTo(ANCHORS.headRadius, 0);
ctx.moveTo(0, -ANCHORS.headRadius);
ctx.lineTo(0, ANCHORS.headRadius);
ctx.stroke();
ctx.restore();

// Torso (egg/oval)
ctx.save();
ctx.translate(ANCHORS.torsoBBox.x + ANCHORS.torsoBBox.w/2, ANCHORS.torsoBBox.y + ANCHORS.torsoBBox.h/2);
ctx.rotate(30 * Math.PI / 180);
ctx.beginPath();
ctx.ellipse(0, 0, ANCHORS.torsoBBox.w/2, ANCHORS.torsoBBox.h/2, 0, 0, Math.PI * 2);
ctx.stroke();
ctx.restore();

// Limbs
function drawLimb(points) {
  ctx.beginPath();
  ctx.moveTo(points[0].x, points[0].y);
  for (let i = 1; i < points.length; i++) {
    ctx.lineTo(points[i].x, points[i].y);
  }
  ctx.stroke();
  for (let pt of points) {
    ctx.beginPath();
    ctx.arc(pt.x, pt.y, 8, 0, Math.PI * 2);
    ctx.stroke();
  }
}
drawLimb(ANCHORS.leftArm);
drawLimb(ANCHORS.rightArm);
drawLimb(ANCHORS.leftLeg);
drawLimb(ANCHORS.rightLeg);

// Cape (graphite)
ctx.strokeStyle = GRAPHITE;
ctx.beginPath();
ctx.moveTo(440, 350); 
for (let curve of ANCHORS.capeControlPoints) {
  ctx.bezierCurveTo(curve.cp1x, curve.cp1y, curve.cp2x, curve.cp2y, curve.x, curve.y);
}
ctx.stroke();

// Crest
ctx.strokeStyle = BLUE;
ctx.save();
ctx.translate(ANCHORS.crestPos.x, ANCHORS.crestPos.y);
ctx.rotate(30 * Math.PI / 180);
ctx.beginPath();
ctx.moveTo(0, -ANCHORS.crestPos.h/2);
ctx.lineTo(ANCHORS.crestPos.w/2, 0);
ctx.lineTo(0, ANCHORS.crestPos.h/2);
ctx.lineTo(-ANCHORS.crestPos.w/2, 0);
ctx.closePath();
ctx.stroke();
ctx.restore();

// Belt
ctx.save();
ctx.translate(ANCHORS.beltPos.x, ANCHORS.beltPos.y);
ctx.rotate(30 * Math.PI / 180);
ctx.strokeRect(-ANCHORS.beltPos.w/2, -ANCHORS.beltPos.h/2, ANCHORS.beltPos.w, ANCHORS.beltPos.h);
ctx.restore();
