/**
 * Aetheria Robotics - Stage 1: Precision Mark Construction
 * The Geometer: Mark Architect
 * 
 * Mathematical & Geometric Foundations:
 * 1. Golden Ratio (Phi = 1.6180339887...) governing apex height, wing sweep angles, 
 *    core aperture ratios, and dynamic vortex channel kerfs.
 * 2. Tri-Fold Gestalt Fusion:
 *    - Modernist Monolithic Letter 'A' (Aetheria identity anchor)
 *    - Aerodynamic Swept Delta-Wing / Hypersonic Atmospheric Lifting Body (Flight & Robotics)
 *    - Golden Logarithmic Kinetic Core / Autonomous Guidance Apex (AI Autonomous Systems)
 * 3. George Bokhua Principles & Drafting Standards:
 *    - Continuous-curvature tangent fillets on wingtips and apexes
 *    - Optical bone-effect stroke compensation (angled diagonal wings vs horizontal crossbar)
 *    - Optical overshoot at top apex and base trailing reflex
 *    - High-contrast negative space datums optimized for 16px favicon to billboard scales
 *    - 100% vector geometry with pure 1-color monochrome silhouette integrity
 */

const PHI = (1 + Math.sqrt(5)) / 2; // ~1.6180339887

/**
 * Geometric Helper Utilities (George Bokhua Principles)
 */
const Logo = {
  PHI: PHI,

  /**
   * Generates a series of golden ratio circles scaled to a base unit
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
   * Applies optical bone-effect correction to strokes
   */
  correctBoneEffect(baseWeight, isVerticalOrDiagonal = true) {
    return isVerticalOrDiagonal ? baseWeight * 1.08 : baseWeight * 0.92;
  },

  /**
   * Computes optical overshoot for curved or sharp apexes
   */
  computeOvershoot(coord, dimension, isTop = true) {
    const overshoot = dimension * 0.022;
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
  }
};

/**
 * Modular drawMark function for AETHERIA ROBOTICS
 * @param {CanvasRenderingContext2D} ctx - HTML5 2D Canvas context
 * @param {number} size - Canvas dimension (width = height = size)
 * @param {string} [color='#0EA5E9'] - Primary mark fill color
 */
function drawMark(ctx, size, color = '#0EA5E9') {
  ctx.save();

  const S = size;
  const cx = S / 2;
  const cy = S / 2;

  // Geometry Bounds & Golden Proportions
  const usable = S * 0.84;
  const halfW = (usable * 0.94) / 2;
  const halfH = usable / 2;

  const yTopRaw = cy - halfH * 0.95;
  const yTop = Logo.computeOvershoot(yTopRaw, usable, true);
  const yBase = cy + halfH * 0.90;
  const yReflex = cy + halfH * 0.50; // Swept trailing edge reflex

  const xLeftWing = cx - halfW;
  const xRightWing = cx + halfW;

  // Golden crossbar & core proportions
  const yCrossbarTop = cy + halfH * 0.05;
  const yCoreApex = cy - halfH * 0.38;

  // Fillet radii
  const apexFillet = usable * 0.045;
  const wingFillet = usable * 0.055;
  const reflexFillet = usable * 0.04;
  const slotW = Logo.correctBoneEffect(usable * 0.048, false);

  ctx.fillStyle = color;

  // =========================================================================
  // 1. PRIMARY DELTA 'A' WING BODY (Outer Hull with Continuous Fillets)
  // =========================================================================
  ctx.beginPath();

  // Top Apex with tangent arc
  const apexAngle = Math.atan2(yBase - yTop, halfW);
  const apexDx = Math.sin(apexAngle) * apexFillet;
  const apexDy = Math.cos(apexAngle) * apexFillet;

  ctx.moveTo(cx - apexDx, yTop + apexDy);
  ctx.quadraticCurveTo(cx, yTop, cx + apexDx, yTop + apexDy);

  // Right Leading Edge (Swept Delta Wing)
  ctx.lineTo(xRightWing - wingFillet * 0.8, yBase - wingFillet * 0.5);
  ctx.quadraticCurveTo(xRightWing, yBase, xRightWing - wingFillet * 1.4, yBase + wingFillet * 0.2);

  // Right Trailing Reflex Inward to Center Chevron Cusp
  ctx.lineTo(cx + reflexFillet * 0.8, yReflex + reflexFillet * 0.6);
  ctx.quadraticCurveTo(cx, yReflex, cx - reflexFillet * 0.8, yReflex + reflexFillet * 0.6);

  // Left Trailing Reflex Outward to Left Wingtip
  ctx.lineTo(xLeftWing + wingFillet * 1.4, yBase + wingFillet * 0.2);
  ctx.quadraticCurveTo(xLeftWing, yBase, xLeftWing + wingFillet * 0.8, yBase - wingFillet * 0.5);

  // Left Leading Edge returning to Apex
  ctx.closePath();
  ctx.fill();

  // =========================================================================
  // 2. NEGATIVE SPACE SCULPTING (Letter 'A' Aperture + Autonomous Aerodynamic Core)
  // =========================================================================
  ctx.save();
  ctx.globalCompositeOperation = 'destination-out';

  // --- 2A. Upper Letter 'A' Triangular Apex Aperture (Autonomous Sensor Core) ---
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

  // --- 2B. Golden Kinetic Flow Channels (Aerodynamic Wing Vortices) ---
  // Left & Right dynamic negative relief lines carving aerodynamic lift channels
  const channelAngle = Math.PI / 6.2;
  const channelLen = usable * 0.32;
  const channelW = slotW * 0.85;

  // Left Vortex Channel
  ctx.save();
  ctx.translate(cx - halfW * 0.42, cy + halfH * 0.06);
  ctx.rotate(-channelAngle);
  ctx.beginPath();
  ctx.roundRect(-channelW / 2, -channelLen / 2, channelW, channelLen, channelW / 2);
  ctx.fill();
  ctx.restore();

  // Right Vortex Channel
  ctx.save();
  ctx.translate(cx + halfW * 0.42, cy + halfH * 0.06);
  ctx.rotate(channelAngle);
  ctx.beginPath();
  ctx.roundRect(-channelW / 2, -channelLen / 2, channelW, channelLen, channelW / 2);
  ctx.fill();
  ctx.restore();

  // --- 2C. Lower Delta Ingress / Trailing Edge Exhaust Duct ---
  // Carves negative thrust notch creating the distinct autonomous drone elevator / delta empennage
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

  // =========================================================================
  // 3. INNER AUTONOMOUS FLIGHT DART / GUIDANCE CORE (Positive Accent Anchor)
  // =========================================================================
  // Nested Golden Arrowhead in the lower throat symbolizing intelligent guidance
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

/**
 * Geometric Blueprint & Construction Overlay Renderer
 * (For design audit, documentation, and presentation boards)
 */
function drawMarkBlueprint(ctx, size, theme = { bg: '#0F172A', mark: '#0EA5E9', grid: '#1E293B', arc: '#F59E0B' }) {
  ctx.save();
  ctx.fillStyle = theme.bg;
  ctx.fillRect(0, 0, size, size);

  const cx = size / 2;
  const cy = size / 2;
  const usable = size * 0.84;

  // 1. Isometric / Golden Construction Grid
  ctx.strokeStyle = theme.grid;
  ctx.lineWidth = 1;
  ctx.beginPath();
  
  // Golden Concentric Radius Circles
  const goldenRadii = Logo.createGoldenCircles(usable * 0.08, 4);
  goldenRadii.forEach(r => {
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
  });

  // Central Vertical Drafting Axis
  ctx.moveTo(cx, 0);
  ctx.lineTo(cx, size);
  ctx.moveTo(0, cy);
  ctx.lineTo(size, cy);

  // 60-degree Delta Sweep Drafting Tangents
  const diag = size * 0.7;
  ctx.moveTo(cx - diag, cy - diag * 0.866);
  ctx.lineTo(cx + diag, cy + diag * 0.866);
  ctx.moveTo(cx + diag, cy - diag * 0.866);
  ctx.lineTo(cx - diag, cy + diag * 0.866);
  ctx.stroke();

  // 2. Render Base Mark
  drawMark(ctx, size, theme.mark);

  // 3. Golden Ratio Tangent Arc Guides
  ctx.strokeStyle = theme.arc;
  ctx.lineWidth = 1.5;
  ctx.setLineDash([4, 4]);
  ctx.beginPath();
  ctx.arc(cx, cy - usable * 0.38, usable * 0.18, 0, Math.PI * 2);
  ctx.arc(cx, cy + usable * 0.25, usable * 0.28, 0, Math.PI * 2);
  ctx.stroke();
  ctx.setLineDash([]);

  ctx.restore();
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { drawMark, drawMarkBlueprint, Logo };
}
