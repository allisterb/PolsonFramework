/**
 * Artwork: Seagull Riding a Bicycle on a Seaside Promenade
 * Target Engine: Polson Graphics MCP Server (ECMAScript 2025 / Sandboxed Canvas2D & Skia)
 */

const W = 1200;
const H = 900;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext("2d");

// =========================================================================
// 1. SKY, GLOWING SUN & ATMOSPHERE
// =========================================================================
const skyGrad = ctx.createLinearGradient(0, 0, 0, 560);
skyGrad.addColorStop(0.0, '#1565c0');
skyGrad.addColorStop(0.35, '#3895e8');
skyGrad.addColorStop(0.65, '#80c4f8');
skyGrad.addColorStop(0.88, '#ffe8b6');
skyGrad.addColorStop(1.0, '#ffd899');
ctx.fillStyle = skyGrad;
ctx.fillRect(0, 0, W, H);

// Warm Sun with Multi-layered Radial Flare
const sunGrad = ctx.createRadialGradient(980, 140, 15, 980, 140, 210);
sunGrad.addColorStop(0.0, 'rgba(255, 255, 250, 1.0)');
sunGrad.addColorStop(0.2, 'rgba(255, 245, 175, 0.9)');
sunGrad.addColorStop(0.5, 'rgba(255, 220, 120, 0.35)');
sunGrad.addColorStop(1.0, 'rgba(255, 200, 90, 0.0)');
ctx.fillStyle = sunGrad;
ctx.beginPath();
ctx.arc(980, 140, 210, 0, Math.PI * 2);
ctx.fill();

// =========================================================================
// 2. PROCEDURAL PUFFY CLOUDS & SKY SILHOUETTES
// =========================================================================
function drawCloud(cx, cy, scale, alpha) {
  ctx.save();
  ctx.translate(cx, cy);
  ctx.scale(scale, scale);

  const puffs = [
    { x: -75, y: 15, r: 38 },
    { x: -38, y: 5, r: 48 },
    { x: 12, y: -16, r: 56 },
    { x: 60, y: 0, r: 45 },
    { x: 98, y: 18, r: 32 },
    { x: 18, y: 22, r: 40 },
    { x: -32, y: 24, r: 36 }
  ];

  // Soft underside cloud shadow
  ctx.fillStyle = 'rgba(195, 215, 240, ' + (alpha * 0.5) + ')';
  for (const p of puffs) {
    ctx.beginPath();
    ctx.arc(p.x, p.y + 10, p.r, 0, Math.PI * 2);
    ctx.fill();
  }

  // Bright white cloud body
  ctx.fillStyle = 'rgba(255, 255, 255, ' + alpha + ')';
  for (const p of puffs) {
    ctx.beginPath();
    ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
    ctx.fill();
  }

  ctx.restore();
}

drawCloud(170, 115, 1.3, 0.95);
drawCloud(560, 90, 1.0, 0.88);
drawCloud(830, 185, 0.75, 0.72);
drawCloud(370, 205, 0.65, 0.62);

// Distant gliding gulls in sky
function drawSkyGull(gx, gy, scale, rotDeg) {
  ctx.save();
  ctx.translate(gx, gy);
  ctx.rotate(rotDeg * Math.PI / 180);
  ctx.strokeStyle = 'rgba(35, 55, 80, 0.8)';
  ctx.lineWidth = Math.max(1.8, scale * 2.2);
  ctx.lineCap = 'round';
  ctx.beginPath();
  ctx.moveTo(-18 * scale, 0);
  ctx.quadraticCurveTo(-9 * scale, -10 * scale, 0, 0);
  ctx.quadraticCurveTo(9 * scale, -10 * scale, 18 * scale, 0);
  ctx.stroke();
  ctx.restore();
}

drawSkyGull(300, 125, 0.95, -6);
drawSkyGull(345, 110, 0.7, -3);
drawSkyGull(710, 130, 0.8, 8);

// =========================================================================
// 3. OCEAN & COASTAL LIGHTHOUSE HEADLAND
// =========================================================================
// Distant Headland
const headlandGrad = ctx.createLinearGradient(0, 420, 0, 520);
headlandGrad.addColorStop(0, '#436d75');
headlandGrad.addColorStop(1, '#25444a');
ctx.fillStyle = headlandGrad;
ctx.beginPath();
ctx.moveTo(0, 470);
ctx.quadraticCurveTo(110, 445, 240, 475);
ctx.quadraticCurveTo(320, 492, 380, 515);
ctx.lineTo(0, 515);
ctx.closePath();
ctx.fill();

// Lighthouse on Headland
ctx.save();
ctx.translate(130, 452);
ctx.fillStyle = '#172b2f';
ctx.beginPath();
ctx.arc(0, 6, 22, Math.PI, 0);
ctx.fill();

// Tower
ctx.fillStyle = '#f8f9fa';
ctx.beginPath();
ctx.moveTo(-6, 0);
ctx.lineTo(-3.5, -38);
ctx.lineTo(3.5, -38);
ctx.lineTo(6, 0);
ctx.closePath();
ctx.fill();

// Red stripes
ctx.fillStyle = '#d32f2f';
ctx.fillRect(-5.5, -28, 11, 8);
ctx.fillRect(-4.5, -12, 9, 7);

// Lantern room & cupola
ctx.fillStyle = '#263238';
ctx.fillRect(-5, -42, 10, 4);
ctx.beginPath();
ctx.moveTo(-6, -42);
ctx.lineTo(0, -50);
ctx.lineTo(6, -42);
ctx.closePath();
ctx.fillStyle = '#d32f2f';
ctx.fill();

// Beacon Light Beam
const beamGrad = ctx.createLinearGradient(0, -40, 190, -25);
beamGrad.addColorStop(0, 'rgba(255, 245, 170, 0.75)');
beamGrad.addColorStop(1, 'rgba(255, 245, 170, 0.0)');
ctx.fillStyle = beamGrad;
ctx.beginPath();
ctx.moveTo(0, -40);
ctx.lineTo(210, -65);
ctx.lineTo(210, -15);
ctx.closePath();
ctx.fill();
ctx.restore();

// Deep Ocean Waves
const seaGrad = ctx.createLinearGradient(0, 500, 0, 630);
seaGrad.addColorStop(0.0, '#1565c0');
seaGrad.addColorStop(0.35, '#0d47a1');
seaGrad.addColorStop(0.75, '#072b61');
seaGrad.addColorStop(1.0, '#041b3d');
ctx.fillStyle = seaGrad;
ctx.fillRect(0, 500, W, 130);

function drawSeaWaves(y, amp, freq, color, lw) {
  ctx.strokeStyle = color;
  ctx.lineWidth = lw;
  ctx.beginPath();
  ctx.moveTo(0, y);
  for (let x = 0; x <= W; x += freq) {
    ctx.quadraticCurveTo(x + freq * 0.5, y - amp, x + freq, y);
  }
  ctx.stroke();
}
drawSeaWaves(512, 3, 30, 'rgba(255, 255, 255, 0.45)', 1.5);
drawSeaWaves(535, 4, 38, 'rgba(255, 255, 255, 0.55)', 2.0);
drawSeaWaves(565, 5, 46, 'rgba(255, 255, 255, 0.65)', 2.5);
drawSeaWaves(600, 6, 55, 'rgba(255, 255, 255, 0.8)', 3.0);

// =========================================================================
// 4. BOARDWALK PROMENADE & TIMBER RAILING
// =========================================================================
// Pier Railing Posts
for (let rx = 25; rx < W; rx += 165) {
  const postGrad = ctx.createLinearGradient(rx, 0, rx + 22, 0);
  postGrad.addColorStop(0, '#82522c');
  postGrad.addColorStop(0.4, '#aa7445');
  postGrad.addColorStop(1, '#5e381a');
  ctx.fillStyle = postGrad;
  ctx.fillRect(rx, 505, 22, 125);

  ctx.beginPath();
  ctx.moveTo(rx - 2, 505);
  ctx.lineTo(rx + 11, 495);
  ctx.lineTo(rx + 24, 505);
  ctx.closePath();
  ctx.fill();
}

// Swag Ropes
ctx.strokeStyle = '#d7b17a';
ctx.lineWidth = 8;
ctx.lineCap = 'round';
ctx.beginPath();
for (let rx = 36; rx < W; rx += 165) {
  ctx.moveTo(rx, 532);
  ctx.quadraticCurveTo(rx + 82, 568, rx + 165, 532);
}
ctx.stroke();
ctx.strokeStyle = '#b88d55';
ctx.lineWidth = 6;
ctx.beginPath();
for (let rx = 36; rx < W; rx += 165) {
  ctx.moveTo(rx, 575);
  ctx.quadraticCurveTo(rx + 82, 608, rx + 165, 575);
}
ctx.stroke();

// Wooden Boardwalk Floor
const boardGrad = ctx.createLinearGradient(0, 620, 0, H);
boardGrad.addColorStop(0.0, '#a36f3d');
boardGrad.addColorStop(0.4, '#875427');
boardGrad.addColorStop(1.0, '#5f3311');
ctx.fillStyle = boardGrad;
ctx.fillRect(0, 620, W, H - 620);

const plankYs = [620, 650, 685, 725, 770, 820, 870, 900];
for (let i = 0; i < plankYs.length; i++) {
  const py = plankYs[i];
  ctx.fillStyle = 'rgba(25, 12, 4, 0.82)';
  ctx.fillRect(0, py - 2, W, 4);
  ctx.fillStyle = 'rgba(255, 235, 195, 0.35)';
  ctx.fillRect(0, py + 2, W, 2);
}

// Plank joints & nails
ctx.fillStyle = 'rgba(30, 15, 5, 0.68)';
const plankOffsets = [90, 270, 460, 650, 840, 1030];
for (let i = 0; i < plankYs.length - 1; i++) {
  const yTop = plankYs[i];
  const yBot = plankYs[i+1];
  const shift = (i % 2) * 95;
  for (const off of plankOffsets) {
    const px = (off + shift) % (W - 20) + 10;
    ctx.fillRect(px, yTop, 3, yBot - yTop);
    ctx.fillStyle = '#220f04';
    ctx.beginPath();
    ctx.arc(px - 9, yTop + 9, 2.5, 0, Math.PI * 2);
    ctx.arc(px + 12, yTop + 9, 2.5, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = 'rgba(30, 15, 5, 0.68)';
  }
}

// =========================================================================
// 5. CAST SHADOWS (Skia ImageFilter Drop Blur)
// =========================================================================
const rwx = 340, rwy = 675; // Rear wheel center
const fwx = 840, fwy = 675; // Front wheel center

ctx.save();
ctx.filter = Skia.ImageFilter.blur(12, 12);
ctx.fillStyle = 'rgba(18, 8, 4, 0.58)';
ctx.beginPath();
ctx.ellipse(rwx, 782, 95, 16, 0, 0, Math.PI * 2);
ctx.ellipse(fwx, 782, 95, 16, 0, 0, Math.PI * 2);
ctx.ellipse(580, 784, 210, 24, 0, 0, Math.PI * 2);
ctx.fill();
ctx.restore();

// =========================================================================
// 6. THE BICYCLE (VINTAGE BEACH CRUISER)
// =========================================================================
function drawCruiserWheel(cx, cy) {
  ctx.save();
  // Outer tire (charcoal rubber)
  ctx.strokeStyle = '#212529';
  ctx.lineWidth = 18;
  ctx.beginPath();
  ctx.arc(cx, cy, 96, 0, Math.PI * 2);
  ctx.stroke();

  // Vintage Cream / Tan Sidewall
  ctx.strokeStyle = '#f8f1e5';
  ctx.lineWidth = 10;
  ctx.beginPath();
  ctx.arc(cx, cy, 90, 0, Math.PI * 2);
  ctx.stroke();

  // Polished Chrome Rim
  ctx.strokeStyle = '#cfd8dc';
  ctx.lineWidth = 5;
  ctx.beginPath();
  ctx.arc(cx, cy, 84, 0, Math.PI * 2);
  ctx.stroke();

  // Stainless Steel Spokes (32 spokes with realistic crossing)
  const numSpokes = 32;
  ctx.strokeStyle = 'rgba(230, 235, 240, 0.85)';
  ctx.lineWidth = 1.5;
  for (let i = 0; i < numSpokes; i++) {
    const ang = (i * 360 / numSpokes) * Math.PI / 180;
    const angOffset = (i % 2 === 0 ? 0.12 : -0.12);
    ctx.beginPath();
    ctx.moveTo(cx + Math.cos(ang) * 14, cy + Math.sin(ang) * 14);
    ctx.lineTo(cx + Math.cos(ang + angOffset) * 84, cy + Math.sin(ang + angOffset) * 84);
    ctx.stroke();
  }

  // Chrome Hub
  const hubGrad = ctx.createRadialGradient(cx - 3, cy - 3, 2, cx, cy, 15);
  hubGrad.addColorStop(0, '#ffffff');
  hubGrad.addColorStop(0.5, '#b0bec5');
  hubGrad.addColorStop(1, '#37474f');
  ctx.fillStyle = hubGrad;
  ctx.beginPath();
  ctx.arc(cx, cy, 15, 0, Math.PI * 2);
  ctx.fill();
  ctx.strokeStyle = '#263238';
  ctx.lineWidth = 2;
  ctx.stroke();

  ctx.restore();
}

// Rear Wheel
drawCruiserWheel(rwx, rwy);

// Rear Chrome Fender
ctx.save();
ctx.strokeStyle = '#e0e0e0';
ctx.lineWidth = 15;
ctx.beginPath();
ctx.arc(rwx, rwy, 107, Math.PI * 0.92, Math.PI * 1.96);
ctx.stroke();
ctx.strokeStyle = '#ffffff';
ctx.lineWidth = 4;
ctx.beginPath();
ctx.arc(rwx, rwy, 107, Math.PI * 1.05, Math.PI * 1.85);
ctx.stroke();
ctx.strokeStyle = '#90a4ae';
ctx.lineWidth = 3.5;
ctx.beginPath();
ctx.moveTo(rwx, rwy);
ctx.lineTo(rwx - Math.cos(0.25) * 107, rwy - Math.sin(0.25) * 107);
ctx.moveTo(rwx, rwy);
ctx.lineTo(rwx + Math.cos(0.4) * 107, rwy - Math.sin(0.4) * 107);
ctx.stroke();
ctx.restore();

// Frame Geometry Points
const bbX = 570, bbY = 675;       // Bottom bracket
const stTopX = 490, stTopY = 490;  // Seat tube top
const htTopX = 760, htTopY = 465;  // Head tube top
const htBotX = 780, htBotY = 525;  // Head tube bottom

const frameColor = '#00897b';      // Seafoam Vintage Teal
const frameShadow = '#004d40';
const frameHighlight = '#4ebaaa';

function drawTube(x1, y1, x2, y2, lw) {
  ctx.save();
  ctx.lineCap = 'round';
  ctx.strokeStyle = frameShadow;
  ctx.lineWidth = lw + 3;
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
  ctx.stroke();
  ctx.strokeStyle = frameColor;
  ctx.lineWidth = lw;
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
  ctx.stroke();
  ctx.strokeStyle = frameHighlight;
  ctx.lineWidth = lw * 0.35;
  ctx.beginPath();
  ctx.moveTo(x1 - 1, y1 - 2);
  ctx.lineTo(x2 - 1, y2 - 2);
  ctx.stroke();
  ctx.restore();
}

// Rear Stays
drawTube(rwx, rwy, bbX, bbY, 12);       // Chainstay
drawTube(rwx, rwy, stTopX, stTopY, 12); // Seatstay

// Seat Tube
drawTube(bbX, bbY, stTopX - 10, stTopY - 25, 14);

// Down Tube (Swooping Cruiser Curve)
ctx.save();
ctx.lineCap = 'round';
ctx.strokeStyle = frameShadow;
ctx.lineWidth = 18;
ctx.beginPath();
ctx.moveTo(bbX, bbY);
ctx.quadraticCurveTo(670, 640, htBotX, htBotY);
ctx.stroke();
ctx.strokeStyle = frameColor;
ctx.lineWidth = 15;
ctx.beginPath();
ctx.moveTo(bbX, bbY);
ctx.quadraticCurveTo(670, 640, htBotX, htBotY);
ctx.stroke();
ctx.strokeStyle = frameHighlight;
ctx.lineWidth = 5;
ctx.beginPath();
ctx.moveTo(bbX, bbY - 3);
ctx.quadraticCurveTo(670, 637, htBotX, htBotY - 3);
ctx.stroke();
ctx.restore();

// Twin Top Tubes
ctx.save();
ctx.lineCap = 'round';
ctx.strokeStyle = frameColor;
ctx.lineWidth = 12;
ctx.beginPath();
ctx.moveTo(stTopX, stTopY);
ctx.quadraticCurveTo(620, 455, htTopX, htTopY);
ctx.stroke();
ctx.strokeStyle = frameHighlight;
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(stTopX, stTopY - 2);
ctx.quadraticCurveTo(620, 453, htTopX, htTopY - 2);
ctx.stroke();

ctx.strokeStyle = frameColor;
ctx.lineWidth = 12;
ctx.beginPath();
ctx.moveTo(stTopX, stTopY + 22);
ctx.quadraticCurveTo(630, 495, htBotX, htBotY);
ctx.stroke();
ctx.strokeStyle = frameHighlight;
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(stTopX, stTopY + 20);
ctx.quadraticCurveTo(630, 493, htBotX, htBotY - 2);
ctx.stroke();
ctx.restore();

// Head Tube & Front Fork
drawTube(htTopX, htTopY, htBotX, htBotY, 18);
drawTube(htBotX, htBotY, fwx, fwy, 14);

// Front Wheel
drawCruiserWheel(fwx, fwy);

// Front Chrome Fender
ctx.save();
ctx.strokeStyle = '#e0e0e0';
ctx.lineWidth = 15;
ctx.beginPath();
ctx.arc(fwx, fwy, 107, Math.PI * 1.08, Math.PI * 1.95);
ctx.stroke();
ctx.strokeStyle = '#ffffff';
ctx.lineWidth = 4;
ctx.beginPath();
ctx.arc(fwx, fwy, 107, Math.PI * 1.2, Math.PI * 1.85);
ctx.stroke();
ctx.strokeStyle = '#90a4ae';
ctx.lineWidth = 3.5;
ctx.beginPath();
ctx.moveTo(fwx, fwy);
ctx.lineTo(fwx + Math.cos(0.32) * 107, fwy - Math.sin(0.32) * 107);
ctx.stroke();
ctx.restore();

// Retro Bullet Headlight with Light Cone
ctx.save();
ctx.translate(htBotX + 15, htBotY - 15);
const lightCone = ctx.createLinearGradient(18, 0, 300, 80);
lightCone.addColorStop(0, 'rgba(255, 250, 180, 0.45)');
lightCone.addColorStop(0.5, 'rgba(255, 245, 150, 0.15)');
lightCone.addColorStop(1, 'rgba(255, 240, 120, 0.0)');
ctx.fillStyle = lightCone;
ctx.beginPath();
ctx.moveTo(18, -6);
ctx.lineTo(320, -50);
ctx.lineTo(340, 120);
ctx.lineTo(18, 6);
ctx.closePath();
ctx.fill();

const lampGrad = ctx.createLinearGradient(-15, 0, 20, 0);
lampGrad.addColorStop(0, '#546e7a');
lampGrad.addColorStop(0.5, '#eceff1');
lampGrad.addColorStop(1, '#ffffff');
ctx.fillStyle = lampGrad;
ctx.beginPath();
ctx.moveTo(-12, -8);
ctx.quadraticCurveTo(12, -10, 18, 0);
ctx.quadraticCurveTo(12, 10, -12, 8);
ctx.closePath();
ctx.fill();
ctx.strokeStyle = '#37474f';
ctx.lineWidth = 1.5;
ctx.stroke();
ctx.fillStyle = '#fff59d';
ctx.beginPath();
ctx.ellipse(18, 0, 3, 9, 0, 0, Math.PI * 2);
ctx.fill();
ctx.restore();

// Chain Guard
ctx.save();
ctx.fillStyle = frameColor;
ctx.strokeStyle = frameShadow;
ctx.lineWidth = 3;
ctx.beginPath();
ctx.moveTo(bbX - 28, bbY - 24);
ctx.arc(bbX, bbY, 34, Math.PI * 0.75, Math.PI * 1.55);
ctx.lineTo(rwx + 35, rwy - 12);
ctx.arc(rwx, rwy, 26, Math.PI * 1.55, Math.PI * 0.25);
ctx.lineTo(bbX + 18, bbY + 30);
ctx.closePath();
ctx.fill();
ctx.stroke();
ctx.strokeStyle = '#ffffff';
ctx.lineWidth = 3.5;
ctx.beginPath();
ctx.moveTo(bbX - 10, bbY - 6);
ctx.lineTo(rwx + 45, rwy - 6);
ctx.stroke();
ctx.restore();

// Bottom Bracket, Chrome Chainring & Pedals
ctx.save();
const crGrad = ctx.createRadialGradient(bbX - 4, bbY - 4, 3, bbX, bbY, 28);
crGrad.addColorStop(0, '#ffffff');
crGrad.addColorStop(0.6, '#cfd8dc');
crGrad.addColorStop(1, '#455a64');
ctx.fillStyle = crGrad;
ctx.beginPath();
ctx.arc(bbX, bbY, 28, 0, Math.PI * 2);
ctx.fill();
ctx.strokeStyle = '#263238';
ctx.lineWidth = 2;
ctx.stroke();

// Far Crank & Pedal
ctx.strokeStyle = '#90a4ae';
ctx.lineWidth = 7;
ctx.lineCap = 'round';
ctx.beginPath();
ctx.moveTo(bbX, bbY);
ctx.lineTo(bbX - 34, bbY - 34);
ctx.stroke();
ctx.fillStyle = '#37474f';
ctx.fillRect(bbX - 48, bbY - 40, 26, 10);

// Near Crank & Pedal
ctx.strokeStyle = '#eceff1';
ctx.lineWidth = 9;
ctx.beginPath();
ctx.moveTo(bbX, bbY);
ctx.lineTo(bbX + 38, bbY + 38);
ctx.stroke();
ctx.fillStyle = '#212529';
ctx.fillRect(bbX + 24, bbY + 33, 30, 12);
ctx.strokeStyle = '#ffa000';
ctx.lineWidth = 3.5;
ctx.beginPath();
ctx.moveTo(bbX + 28, bbY + 39);
ctx.lineTo(bbX + 50, bbY + 39);
ctx.stroke();
ctx.restore();

// Sprung Vintage Leather Saddle
ctx.save();
ctx.strokeStyle = '#cfd8dc';
ctx.lineWidth = 10;
ctx.lineCap = 'butt';
ctx.beginPath();
ctx.moveTo(stTopX - 10, stTopY - 25);
ctx.lineTo(stTopX - 22, stTopY - 65);
ctx.stroke();

ctx.strokeStyle = '#90a4ae';
ctx.lineWidth = 4;
ctx.beginPath();
ctx.arc(stTopX - 45, stTopY - 68, 12, 0, Math.PI * 2);
ctx.arc(stTopX - 10, stTopY - 68, 9, 0, Math.PI * 2);
ctx.stroke();

const saddleGrad = ctx.createLinearGradient(stTopX - 70, stTopY - 105, stTopX + 35, stTopY - 75);
saddleGrad.addColorStop(0.0, '#3e2010');
saddleGrad.addColorStop(0.4, '#6d3919');
saddleGrad.addColorStop(0.8, '#915024');
saddleGrad.addColorStop(1.0, '#3e2010');
ctx.fillStyle = saddleGrad;
ctx.beginPath();
ctx.moveTo(stTopX - 65, stTopY - 80);
ctx.quadraticCurveTo(stTopX - 70, stTopY - 102, stTopX - 40, stTopY - 102);
ctx.quadraticCurveTo(stTopX + 10, stTopY - 96, stTopX + 38, stTopY - 86);
ctx.quadraticCurveTo(stTopX + 40, stTopY - 76, stTopX + 28, stTopY - 76);
ctx.quadraticCurveTo(stTopX - 20, stTopY - 80, stTopX - 65, stTopY - 80);
ctx.closePath();
ctx.fill();
ctx.strokeStyle = '#2b1609';
ctx.lineWidth = 2.5;
ctx.stroke();
ctx.fillStyle = '#ffe082';
ctx.beginPath();
ctx.arc(stTopX - 55, stTopY - 88, 2.5, 0, Math.PI * 2);
ctx.arc(stTopX - 35, stTopY - 91, 2.5, 0, Math.PI * 2);
ctx.arc(stTopX - 15, stTopY - 88, 2.5, 0, Math.PI * 2);
ctx.fill();
ctx.restore();

// Handlebars & Stem & Basket
ctx.save();
ctx.strokeStyle = '#cfd8dc';
ctx.lineWidth = 12;
ctx.lineCap = 'round';
ctx.beginPath();
ctx.moveTo(htTopX, htTopY);
ctx.lineTo(htTopX - 10, htTopY - 70);
ctx.lineTo(htTopX - 30, htTopY - 85);
ctx.stroke();

// Handlebars
ctx.strokeStyle = '#ffffff';
ctx.lineWidth = 10;
ctx.lineCap = 'round';
ctx.beginPath();
ctx.moveTo(htTopX - 85, htTopY - 125);
ctx.quadraticCurveTo(htTopX - 55, htTopY - 95, htTopX - 30, htTopY - 85);
ctx.quadraticCurveTo(htTopX + 10, htTopY - 90, htTopX + 45, htTopY - 118);
ctx.stroke();

// Handgrips
ctx.strokeStyle = '#5d4037';
ctx.lineWidth = 13;
ctx.beginPath();
ctx.moveTo(htTopX - 88, htTopY - 128);
ctx.lineTo(htTopX - 70, htTopY - 110);
ctx.moveTo(htTopX + 48, htTopY - 121);
ctx.lineTo(htTopX + 32, htTopY - 106);
ctx.stroke();

// Bell
const bellGrad = ctx.createRadialGradient(htTopX - 48, htTopY - 106, 2, htTopX - 46, htTopY - 104, 11);
bellGrad.addColorStop(0, '#fff9c4');
bellGrad.addColorStop(0.5, '#fbc02d');
bellGrad.addColorStop(1, '#f57f17');
ctx.fillStyle = bellGrad;
ctx.beginPath();
ctx.arc(htTopX - 46, htTopY - 104, 10, 0, Math.PI * 2);
ctx.fill();
ctx.strokeStyle = '#b78103';
ctx.lineWidth = 1.5;
ctx.stroke();

// Wicker Basket
const bskX = htTopX + 15, bskY = htTopY - 75;
const bskW = 90, bskH = 70;
const bskGrad = ctx.createLinearGradient(bskX, bskY, bskX, bskY + bskH);
bskGrad.addColorStop(0, '#e5c290');
bskGrad.addColorStop(1, '#a6773d');
ctx.fillStyle = bskGrad;
ctx.beginPath();
ctx.roundRect(bskX, bskY, bskW, bskH, [4, 4, 14, 14]);
ctx.fill();
ctx.strokeStyle = '#6d4c1b';
ctx.lineWidth = 2.5;
ctx.stroke();

ctx.strokeStyle = '#855b24';
ctx.lineWidth = 1.8;
ctx.beginPath();
for (let x = bskX + 8; x < bskX + bskW; x += 12) {
  ctx.moveTo(x, bskY);
  ctx.lineTo(x + 10, bskY + bskH);
}
for (let y = bskY + 8; y < bskY + bskH; y += 12) {
  ctx.moveTo(bskX, y);
  ctx.lineTo(bskX + bskW, y + 6);
}
ctx.stroke();

// French Fries Bag & Golden Fries
ctx.fillStyle = '#ffffff';
ctx.fillRect(bskX + 20, bskY - 30, 48, 40);
ctx.fillStyle = '#e53935';
ctx.fillRect(bskX + 26, bskY - 30, 8, 40);
ctx.fillRect(bskX + 42, bskY - 30, 8, 40);
ctx.fillRect(bskX + 58, bskY - 30, 8, 40);

ctx.fillStyle = '#ffca28';
ctx.strokeStyle = '#ffa000';
ctx.lineWidth = 1.5;
const fryAngles = [-18, -8, 2, 14, 24, 6];
const fryX = [bskX + 24, bskX + 31, bskX + 39, bskX + 47, bskX + 54, bskX + 43];
for (let i = 0; i < fryAngles.length; i++) {
  ctx.save();
  ctx.translate(fryX[i], bskY - 24);
  ctx.rotate(fryAngles[i] * Math.PI / 180);
  ctx.fillRect(-3.5, -30, 7, 35);
  ctx.strokeRect(-3.5, -30, 7, 35);
  ctx.restore();
}
ctx.restore();

// =========================================================================
// 7. THE SEAGULL (HERO CHARACTER)
// =========================================================================
ctx.save();
const sgX = stTopX - 10;
const sgY = stTopY - 90;

// --- Legs & Webbed Feet ---
// Left Leg
ctx.strokeStyle = '#ff9100';
ctx.lineWidth = 8;
ctx.lineCap = 'round';
ctx.lineJoin = 'round';
ctx.beginPath();
ctx.moveTo(sgX + 15, sgY + 60);
ctx.lineTo(sgX + 45, sgY + 105);
ctx.lineTo(bbX + 35, bbY + 35);
ctx.stroke();

// Left Foot
ctx.fillStyle = '#ff9100';
ctx.strokeStyle = '#e65100';
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(bbX + 30, bbY + 34);
ctx.lineTo(bbX + 55, bbY + 30);
ctx.lineTo(bbX + 58, bbY + 42);
ctx.lineTo(bbX + 40, bbY + 46);
ctx.lineTo(bbX + 24, bbY + 42);
ctx.closePath();
ctx.fill();
ctx.stroke();

// Right Leg
ctx.strokeStyle = '#ff6d00';
ctx.lineWidth = 7;
ctx.beginPath();
ctx.moveTo(sgX - 15, sgY + 60);
ctx.lineTo(sgX - 5, sgY + 88);
ctx.lineTo(bbX - 34, bbY - 34);
ctx.stroke();

// Right Foot
ctx.fillStyle = '#ff6d00';
ctx.beginPath();
ctx.moveTo(bbX - 34, bbY - 34);
ctx.lineTo(bbX - 18, bbY - 40);
ctx.lineTo(bbX - 14, bbY - 32);
ctx.lineTo(bbX - 44, bbY - 30);
ctx.closePath();
ctx.fill();
ctx.stroke();

// --- Right Wing (Raised High in Salute / Ahoy! - Drawn behind body) ---
ctx.save();
const rightWingGrad = ctx.createLinearGradient(sgX + 10, sgY - 40, sgX + 100, sgY - 185);
rightWingGrad.addColorStop(0.0, '#90a4ae');
rightWingGrad.addColorStop(0.4, '#546e7a');
rightWingGrad.addColorStop(0.8, '#263238');
rightWingGrad.addColorStop(1.0, '#102027');
ctx.fillStyle = rightWingGrad;

ctx.beginPath();
ctx.moveTo(sgX + 15, sgY - 40);
ctx.quadraticCurveTo(sgX + 40, sgY - 110, sgX + 85, sgY - 185); // Wingtip 1
ctx.quadraticCurveTo(sgX + 102, sgY - 170, sgX + 96, sgY - 145); // Wingtip 2
ctx.quadraticCurveTo(sgX + 105, sgY - 130, sgX + 92, sgY - 110); // Wingtip 3
ctx.quadraticCurveTo(sgX + 75, sgY - 70, sgX + 45, sgY - 30);
ctx.closePath();
ctx.fill();
ctx.strokeStyle = '#263238';
ctx.lineWidth = 2;
ctx.stroke();

// White polka dots on raised wingtips
ctx.fillStyle = '#ffffff';
ctx.beginPath();
ctx.arc(sgX + 85, sgY - 175, 4.5, 0, Math.PI * 2);
ctx.arc(sgX + 94, sgY - 150, 4.5, 0, Math.PI * 2);
ctx.arc(sgX + 88, sgY - 125, 4.5, 0, Math.PI * 2);
ctx.fill();
ctx.restore();

// --- Tail Feathers ---
ctx.fillStyle = '#37474f';
ctx.beginPath();
ctx.moveTo(sgX - 40, sgY + 35);
ctx.lineTo(sgX - 120, sgY + 15);
ctx.lineTo(sgX - 135, sgY + 30);
ctx.lineTo(sgX - 110, sgY + 52);
ctx.lineTo(sgX - 30, sgY + 58);
ctx.closePath();
ctx.fill();
ctx.fillStyle = '#ffffff';
ctx.beginPath();
ctx.moveTo(sgX - 100, sgY + 20);
ctx.lineTo(sgX - 120, sgY + 15);
ctx.lineTo(sgX - 135, sgY + 30);
ctx.lineTo(sgX - 110, sgY + 52);
ctx.lineTo(sgX - 95, sgY + 46);
ctx.closePath();
ctx.fill();

// --- Seagull Plump Body (Torso & Belly) ---
const bodyGrad = ctx.createRadialGradient(sgX + 30, sgY + 10, 25, sgX + 15, sgY + 25, 95);
bodyGrad.addColorStop(0.0, '#ffffff');
bodyGrad.addColorStop(0.65, '#f5f7fa');
bodyGrad.addColorStop(0.88, '#e0e6ed');
bodyGrad.addColorStop(1.0, '#b0bec5');
ctx.fillStyle = bodyGrad;
ctx.beginPath();
ctx.moveTo(sgX - 40, sgY + 30);
ctx.quadraticCurveTo(sgX - 45, sgY - 30, sgX - 10, sgY - 65);
ctx.quadraticCurveTo(sgX + 35, sgY - 60, sgX + 68, sgY - 25);
ctx.quadraticCurveTo(sgX + 88, sgY + 25, sgX + 45, sgY + 70);
ctx.quadraticCurveTo(sgX - 10, sgY + 80, sgX - 40, sgY + 30);
ctx.closePath();
ctx.fill();
ctx.strokeStyle = '#90a4ae';
ctx.lineWidth = 2.5;
ctx.stroke();

// --- Left Wing (Gripping Handlebar with Natural Feathery Contours) ---
const leftWingGrad = ctx.createLinearGradient(sgX - 10, sgY - 20, htTopX - 70, htTopY - 110);
leftWingGrad.addColorStop(0.0, '#90a4ae');
leftWingGrad.addColorStop(0.35, '#607d8b');
leftWingGrad.addColorStop(0.7, '#37474f');
leftWingGrad.addColorStop(1.0, '#212121');
ctx.fillStyle = leftWingGrad;

ctx.beginPath();
ctx.moveTo(sgX - 10, sgY - 20); // Shoulder
ctx.quadraticCurveTo(sgX + 25, sgY + 25, sgX + 70, sgY + 5); // Lower elbow
ctx.lineTo(htTopX - 75, htTopY - 115); // Wrist at handlebar
ctx.quadraticCurveTo(htTopX - 60, htTopY - 135, sgX + 45, sgY - 45); // Upper wing edge
ctx.quadraticCurveTo(sgX + 15, sgY - 45, sgX - 10, sgY - 20);
ctx.closePath();
ctx.fill();
ctx.strokeStyle = '#263238';
ctx.lineWidth = 2.5;
ctx.stroke();

// Feathertips wrapped around handlebar grip
ctx.fillStyle = '#ffffff';
ctx.beginPath();
ctx.arc(htTopX - 76, htTopY - 118, 4.5, 0, Math.PI * 2);
ctx.arc(htTopX - 70, htTopY - 114, 4.5, 0, Math.PI * 2);
ctx.arc(htTopX - 64, htTopY - 110, 4.5, 0, Math.PI * 2);
ctx.fill();

// --- Seagull Neck & Head ---
const headGrad = ctx.createRadialGradient(sgX + 35, sgY - 100, 10, sgX + 25, sgY - 90, 50);
headGrad.addColorStop(0.0, '#ffffff');
headGrad.addColorStop(0.8, '#f5f7fa');
headGrad.addColorStop(1.0, '#cfd8dc');
ctx.fillStyle = headGrad;

// Neck
ctx.beginPath();
ctx.moveTo(sgX - 10, sgY - 65);
ctx.lineTo(sgX + 8, sgY - 95);
ctx.lineTo(sgX + 55, sgY - 85);
ctx.lineTo(sgX + 62, sgY - 30);
ctx.closePath();
ctx.fill();

// Head
ctx.beginPath();
ctx.arc(sgX + 32, sgY - 102, 36, 0, Math.PI * 2);
ctx.fill();
ctx.strokeStyle = '#90a4ae';
ctx.lineWidth = 2;
ctx.stroke();

// --- Red & White Striped Sailor Neckerchief (Bandana) ---
ctx.save();
// Knot
ctx.fillStyle = '#d32f2f';
ctx.beginPath();
ctx.arc(sgX + 18, sgY - 64, 11, 0, Math.PI * 2);
ctx.fill();
ctx.strokeStyle = '#b71c1c';
ctx.lineWidth = 2;
ctx.stroke();
// Fluttering tails
ctx.fillStyle = '#d32f2f';
ctx.beginPath();
ctx.moveTo(sgX + 18, sgY - 64);
ctx.quadraticCurveTo(sgX - 25, sgY - 55, sgX - 65, sgY - 45);
ctx.lineTo(sgX - 52, sgY - 32);
ctx.quadraticCurveTo(sgX - 15, sgY - 48, sgX + 18, sgY - 58);
ctx.closePath();
ctx.fill();
ctx.stroke();
// White stripe on scarf
ctx.fillStyle = '#ffffff';
ctx.beginPath();
ctx.moveTo(sgX - 32, sgY - 52);
ctx.lineTo(sgX - 42, sgY - 50);
ctx.lineTo(sgX - 36, sgY - 38);
ctx.lineTo(sgX - 28, sgY - 40);
ctx.closePath();
ctx.fill();
ctx.restore();

// --- Seagull Large Hooked Beak ---
const beakGrad = ctx.createLinearGradient(sgX + 54, sgY - 108, sgX + 130, sgY - 92);
beakGrad.addColorStop(0.0, '#ffb300');
beakGrad.addColorStop(0.65, '#ff9800');
beakGrad.addColorStop(1.0, '#f57c00');
ctx.fillStyle = beakGrad;
ctx.beginPath();
ctx.moveTo(sgX + 54, sgY - 114);
ctx.quadraticCurveTo(sgX + 96, sgY - 116, sgX + 132, -100 + sgY);
ctx.quadraticCurveTo(sgX + 136, sgY - 92, sgX + 120, sgY - 90);
ctx.lineTo(sgX + 60, sgY - 88);
ctx.closePath();
ctx.fill();
ctx.strokeStyle = '#e65100';
ctx.lineWidth = 2.5;
ctx.stroke();

// Mouth smile line
ctx.strokeStyle = '#bf360c';
ctx.lineWidth = 2;
ctx.beginPath();
ctx.moveTo(sgX + 62, sgY - 101);
ctx.lineTo(sgX + 124, sgY - 98);
ctx.stroke();

// Nostril
ctx.fillStyle = '#d84315';
ctx.beginPath();
ctx.ellipse(sgX + 78, sgY - 108, 4.5, 1.8, -0.15, 0, Math.PI * 2);
ctx.fill();

// Iconic Bright Red Spot on Lower Mandible!
ctx.fillStyle = '#e53935';
ctx.beginPath();
ctx.ellipse(sgX + 110, sgY - 92, 6.5, 4.5, 0, 0, Math.PI * 2);
ctx.fill();

// --- Seagull Eye & Cheerful Brow ---
ctx.fillStyle = '#ffeb3b';
ctx.beginPath();
ctx.arc(sgX + 40, sgY - 110, 9, 0, Math.PI * 2);
ctx.fill();
ctx.strokeStyle = '#f57f17';
ctx.lineWidth = 2;
ctx.stroke();

ctx.fillStyle = '#212121';
ctx.beginPath();
ctx.arc(sgX + 42, sgY - 110, 5.5, 0, Math.PI * 2);
ctx.fill();

ctx.fillStyle = '#ffffff';
ctx.beginPath();
ctx.arc(sgX + 40, sgY - 112.5, 2.5, 0, Math.PI * 2);
ctx.fill();

ctx.strokeStyle = '#78909c';
ctx.lineWidth = 2.5;
ctx.beginPath();
ctx.arc(sgX + 40, sgY - 114, 11, Math.PI * 1.15, Math.PI * 1.85);
ctx.stroke();

// --- Captain's Hat (Navy Peaked Cap with Visor & Gold Anchor) ---
ctx.save();
ctx.translate(sgX + 28, sgY - 132);
ctx.rotate(5 * Math.PI / 180);

const hatGrad = ctx.createLinearGradient(-35, -40, 35, 0);
hatGrad.addColorStop(0.0, '#ffffff');
hatGrad.addColorStop(0.25, '#f5f5f5');
hatGrad.addColorStop(0.65, '#1a237e');
hatGrad.addColorStop(1.0, '#0d47a1');
ctx.fillStyle = hatGrad;
ctx.beginPath();
ctx.moveTo(-32, 2);
ctx.quadraticCurveTo(-42, -36, 0, -42);
ctx.quadraticCurveTo(42, -36, 32, 2);
ctx.closePath();
ctx.fill();
ctx.strokeStyle = '#0a2168';
ctx.lineWidth = 2.5;
ctx.stroke();

ctx.fillStyle = '#0d1b2a';
ctx.fillRect(-30, -2, 60, 14);

ctx.strokeStyle = '#ffd54f';
ctx.lineWidth = 4;
ctx.beginPath();
ctx.moveTo(-30, 7);
ctx.lineTo(30, 7);
ctx.stroke();

ctx.fillStyle = '#1b1b1b';
ctx.beginPath();
ctx.moveTo(-35, 9);
ctx.quadraticCurveTo(0, 26, 42, 13);
ctx.quadraticCurveTo(12, 13, -35, 9);
ctx.closePath();
ctx.fill();
ctx.strokeStyle = '#000000';
ctx.lineWidth = 1.5;
ctx.stroke();

ctx.strokeStyle = 'rgba(255, 255, 255, 0.65)';
ctx.lineWidth = 2.5;
ctx.beginPath();
ctx.moveTo(-18, 13);
ctx.quadraticCurveTo(5, 19, 36, 13);
ctx.stroke();

// Golden Anchor Emblem
ctx.strokeStyle = '#ffc107';
ctx.lineWidth = 3;
ctx.lineCap = 'round';
ctx.beginPath();
ctx.arc(0, -20, 4, 0, Math.PI * 2);
ctx.stroke();
ctx.beginPath();
ctx.moveTo(0, -16);
ctx.lineTo(0, -5);
ctx.moveTo(-6, -12);
ctx.lineTo(6, -12);
ctx.moveTo(-8, -9);
ctx.quadraticCurveTo(0, -1, 8, -9);
ctx.stroke();

ctx.restore();
ctx.restore();

// =========================================================================
// 8. MOTION & ATMOSPHERIC FLOURISHES
// =========================================================================
ctx.save();
ctx.strokeStyle = 'rgba(255, 255, 255, 0.65)';
ctx.lineWidth = 3;
ctx.lineCap = 'round';
ctx.beginPath();
ctx.moveTo(180, 675);
ctx.lineTo(230, 675);
ctx.moveTo(150, 700);
ctx.lineTo(210, 700);
ctx.moveTo(170, 650);
ctx.lineTo(220, 650);
ctx.moveTo(300, 460);
ctx.lineTo(390, 460);
ctx.moveTo(270, 490);
ctx.lineTo(370, 490);
ctx.stroke();

// Floating white down feather in sea breeze
ctx.fillStyle = '#ffffff';
ctx.beginPath();
ctx.ellipse(240, 520, 18, 6, -0.35, 0, Math.PI * 2);
ctx.fill();
ctx.strokeStyle = '#e0e0e0';
ctx.lineWidth = 1;
ctx.stroke();
ctx.restore();

console.log('Seagull riding bicycle artwork rendering complete!');
canvas;
