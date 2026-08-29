Stage.begin('Mood');
Stage.note('Developing 3 mood & stylistic directions: Sunset Romance (warm golden-rose), Moonlight Cruise (luminous midnight-teal), and Riviera Modern (luxury high-contrast).');

const W = 1200, H = 540;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

// Background
ctx.fillStyle = '#10141C';
ctx.fillRect(0, 0, W, H);

// Title & Header
ctx.fillStyle = '#FFFFFF';
ctx.font = 'bold 24px Georgia';
ctx.fillText('SAILBOAT TOURS — MOOD & STYLISTIC DIRECTIONS', 50, 48);

ctx.fillStyle = '#8E9AA8';
ctx.font = '14px Georgia';
ctx.fillText('Exploring three distinct aesthetic territories for romantic couple cruises', 50, 72);

const directions = [
    {
        title: 'DIRECTION A: SUNSET ROMANCE',
        subtitle: 'Warm, intimate, golden hour glow',
        palette: ['#141B2D', '#9E4738', '#D9824C', '#E8C59A', '#FAF5EC'],
        font: 'Georgia',
        specimen: 'Sailboat Tours',
        tagline: 'Sunset & Moonlight Cruises',
        shapeDesc: 'Entwined Golden Arcs'
    },
    {
        title: 'DIRECTION B: MOONLIGHT CRUISE',
        subtitle: 'Serene, luminous, poetic midnight sea',
        palette: ['#0A1420', '#1C3B57', '#3D7A94', '#96C0CE', '#F0F7F9'],
        font: 'Garamond',
        specimen: 'Sailboat Tours',
        tagline: 'Intimate Coastal Voyages',
        shapeDesc: 'Crescent Moon & Sail'
    },
    {
        title: 'DIRECTION C: RIVIERA LUXURY',
        subtitle: 'Sophisticated, high-contrast, editorial',
        palette: ['#0B192C', '#1E3E62', '#C69B7B', '#EBD3BE', '#FFFFFF'],
        font: 'Bodoni MT',
        specimen: 'Sailboat Tours',
        tagline: 'Private Romantic Charters',
        shapeDesc: 'Geometric Minimalist Blade'
    }
];

const colWidth = 340;
const gap = 35;
const startX = 50;
const startY = 110;

directions.forEach((dir, i) => {
    const x = startX + i * (colWidth + gap);
    
    // Panel card background
    ctx.fillStyle = '#181F2B';
    ctx.beginPath();
    ctx.roundRect(x, startY, colWidth, 380, 12);
    ctx.fill();
    ctx.strokeStyle = '#273244';
    ctx.lineWidth = 1.5;
    ctx.stroke();
    
    // Direction Title
    ctx.fillStyle = '#E2E8F0';
    ctx.font = 'bold 15px Georgia';
    ctx.fillText(dir.title, x + 20, startY + 34);
    
    ctx.fillStyle = '#7E8B9B';
    ctx.font = '12px Georgia';
    ctx.fillText(dir.subtitle, x + 20, startY + 54);
    
    // Color Swatches
    const swatchY = startY + 74;
    const swatchW = (colWidth - 40) / dir.palette.length;
    dir.palette.forEach((color, cIdx) => {
        ctx.fillStyle = color;
        ctx.beginPath();
        if (cIdx === 0) ctx.roundRect(x + 20 + cIdx * swatchW, swatchY, swatchW, 40, [6, 0, 0, 6]);
        else if (cIdx === dir.palette.length - 1) ctx.roundRect(x + 20 + cIdx * swatchW, swatchY, swatchW, 40, [0, 6, 6, 0]);
        else ctx.rect(x + 20 + cIdx * swatchW, swatchY, swatchW, 40);
        ctx.fill();
    });
    
    // Typography specimen box
    const typeBoxY = startY + 130;
    ctx.fillStyle = '#121722';
    ctx.beginPath();
    ctx.roundRect(x + 20, typeBoxY, colWidth - 40, 100, 8);
    ctx.fill();
    
    ctx.fillStyle = '#F8FAFC';
    ctx.font = `bold 22px ${dir.font}`;
    ctx.fillText(dir.specimen, x + 35, typeBoxY + 42);
    
    ctx.fillStyle = dir.palette[2];
    ctx.font = `italic 13px ${dir.font}`;
    ctx.fillText(dir.tagline, x + 35, typeBoxY + 70);
    
    // Shape language illustration
    const shapeBoxY = startY + 245;
    ctx.fillStyle = '#121722';
    ctx.beginPath();
    ctx.roundRect(x + 20, shapeBoxY, colWidth - 40, 115, 8);
    ctx.fill();
    
    ctx.fillStyle = '#64748B';
    ctx.font = '11px Georgia';
    ctx.fillText('SHAPE MOTIF: ' + dir.shapeDesc.toUpperCase(), x + 35, shapeBoxY + 22);
    
    // Draw representative geometric sketches in each box
    const cx = x + colWidth / 2;
    const cy = shapeBoxY + 68;
    
    ctx.save();
    if (i === 0) {
        // Entwined Golden Arcs
        ctx.strokeStyle = dir.palette[2];
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(cx - 15, cy + 10, 28, -Math.PI * 0.4, Math.PI * 0.35, false);
        ctx.stroke();
        
        ctx.strokeStyle = dir.palette[3];
        ctx.lineWidth = 3.5;
        ctx.beginPath();
        ctx.arc(cx + 8, cy + 5, 22, -Math.PI * 0.55, Math.PI * 0.25, false);
        ctx.stroke();
        
        ctx.strokeStyle = dir.palette[1];
        ctx.lineWidth = 2.5;
        ctx.beginPath();
        ctx.arc(cx, cy + 18, 36, Math.PI * 0.2, Math.PI * 0.8, false);
        ctx.stroke();
    } else if (i === 1) {
        // Crescent & Sail
        ctx.fillStyle = dir.palette[2];
        ctx.beginPath();
        ctx.arc(cx - 5, cy, 26, -Math.PI * 0.5, Math.PI * 0.5, false);
        ctx.arc(cx - 12, cy, 22, Math.PI * 0.5, -Math.PI * 0.5, true);
        ctx.fill();
        
        ctx.strokeStyle = dir.palette[3];
        ctx.lineWidth = 2.5;
        ctx.beginPath();
        ctx.arc(cx + 10, cy, 20, -Math.PI * 0.4, Math.PI * 0.3, false);
        ctx.stroke();
    } else {
        // Geometric Minimalist Blade
        ctx.fillStyle = dir.palette[2];
        ctx.beginPath();
        ctx.moveTo(cx, cy - 26);
        ctx.lineTo(cx + 18, cy + 16);
        ctx.lineTo(cx, cy + 12);
        ctx.closePath();
        ctx.fill();
        
        ctx.fillStyle = dir.palette[3];
        ctx.beginPath();
        ctx.moveTo(cx - 4, cy - 18);
        ctx.lineTo(cx - 18, cy + 16);
        ctx.lineTo(cx - 4, cy + 12);
        ctx.closePath();
        ctx.fill();
    }
    ctx.restore();
});

canvas;
