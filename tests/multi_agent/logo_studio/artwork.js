/**
 * Nexus Dynamics - Brand Identity System & Presentation Board
 * Built using Polson LogoDesignToolkit & 2D Canvas Engine
 */

const canvas = createCanvas(1200, 800);
const ctx = canvas.getContext('2d');

// --- Modular Logo Mark Function (Scalable to any N x N dimension) ---
function drawNexusMark(c, size) {
    c.save();
    
    // Proportional dimensions
    const cx = size * 0.5;
    const cy = size * 0.5;
    const outerR = size * 0.40;
    const innerR = size * 0.22;
    const barW = size * 0.12;

    // Gradient Shader Accent
    const grad = c.createLinearGradient(0, 0, size, size);
    grad.addColorStop(0, '#4f46e5');
    grad.addColorStop(0.5, '#06b6d4');
    grad.addColorStop(1, '#3b82f6');

    // 1. Geometric Outer Ribbon Nodes
    c.fillStyle = grad;
    c.beginPath();
    
    // Top-Left to Bottom-Right sweeping loop
    c.moveTo(cx - outerR, cy - barW * 0.5);
    c.arc(cx - innerR, cy - barW * 0.5, outerR * 0.45, Math.PI, 0, false);
    c.lineTo(cx + outerR, cy + barW * 0.5);
    c.arc(cx + innerR, cy + barW * 0.5, outerR * 0.45, 0, Math.PI, false);
    c.closePath();
    c.fill();

    // 2. Interlocking Counter-Ribbon
    c.fillStyle = '#06b6d4';
    c.beginPath();
    c.moveTo(cx + barW * 0.5, cy - outerR);
    c.arc(cx + barW * 0.5, cy - innerR, outerR * 0.45, -Math.PI * 0.5, Math.PI * 0.5, false);
    c.lineTo(cx - barW * 0.5, cy + outerR);
    c.arc(cx - barW * 0.5, cy + innerR, outerR * 0.45, Math.PI * 0.5, -Math.PI * 0.5, false);
    c.closePath();
    c.fill();

    // 3. Negative Space Central Diamond Core
    c.fillStyle = '#0b0f19';
    c.beginPath();
    c.moveTo(cx, cy - innerR * 0.5);
    c.lineTo(cx + innerR * 0.5, cy);
    c.lineTo(cx, cy + innerR * 0.5);
    c.lineTo(cx - innerR * 0.5, cy);
    c.closePath();
    c.fill();

    c.restore();
}

// --- Generate Full Executive Brand Presentation Board ---
Logo.generateBrandPresentationSheet(ctx, {
    brandName: 'NEXUS DYNAMICS',
    tagline: 'Autonomous AI Cloud & Cognitive Systems',
    primaryColor: '#4F46E5',
    secondaryColor: '#06B6D4',
    darkColor: '#0B0F19',
    lightColor: '#F8FAFC',
    drawMark: drawNexusMark
});

canvas;
