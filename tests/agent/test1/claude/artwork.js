/* =====================================================================
   "Night Passage" — a wooden sailing ship on the open ocean, moonlit.
   Polson JS SDK, Canvas2D + Skia. 1440x860.

   Run with ExecuteScript { outFile: 'output.webp', quality: 92 }.
   Requires three requisitioned materials in the session scratchpad:
     Session.moonUri   cratered lunar regolith -> the moon's disc
     Session.clothUri  woven canvas            -> sailcloth weave
     Session.oakUri    weathered oak planking  -> hull planking
   They supply SURFACE only. Every form here is constructed in code.

   Deterministic: one seeded LCG drives every scatter, so re-running
   reproduces the identical picture.

   Layer order is the design:
     sky, stars, cirrus, halo, moon, haze
     sea, moonglade, chop, swell, horizon seam
     reflection, ship, bow wave + wake, vignette

   Two workarounds for SDK defects are marked below; see findings.md
   F5 (fillStyle alpha carry-over) and F4 (drawImage ignores colorFilter).
   ===================================================================== */

const W = 1440, H = 860, HORIZON = 470;
const MOON = { x: 912, y: 268, r: 84 };
const SHIP = { x: 730, y: 604, s: 0.78, heel: 0.024 };
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');
let _seed = 20260827;
const rnd = () => (_seed = (_seed * 1664525 + 1013904223) >>> 0) / 4294967296;

// F5 workaround: an rgba() fillStyle latches its alpha onto the NEXT
// gradient/shader assigned. An opaque colour clears it, so route every
// gradient and shader through these.
const setPaint  = p => { ctx.fillStyle   = '#000000'; ctx.fillStyle   = p; };
const setStroke = p => { ctx.strokeStyle = '#000000'; ctx.strokeStyle = p; };

// Skia's Perlin noise is per-channel, so the cloud shader is iridescent
// unless collapsed to luminance first.
const DESAT = Skia.ColorFilter.colorMatrix([
    0.33,0.34,0.33,0,0, 0.33,0.34,0.33,0,0, 0.33,0.34,0.33,0,0, 0,0,0,1,0 ]);

// Perspective row spacing: rows crowd at the horizon, open toward the viewer.
const rowY = t => HORIZON + (H - HORIZON) * Math.pow(t, 2.05);

/* ------------------------------- SKY ------------------------------- */
const sky = ctx.createLinearGradient(0, 0, 0, HORIZON);
sky.addColorStop(0.00,'#03050d'); sky.addColorStop(0.42,'#080f1e');
sky.addColorStop(0.78,'#121c31'); sky.addColorStop(1.00,'#1e2b43');
setPaint(sky); ctx.fillRect(0, 0, W, HORIZON);
ctx.save();
ctx.globalCompositeOperation='screen'; ctx.globalAlpha=0.13; ctx.colorFilter=DESAT;
setPaint(Skia.Shader.perlinNoiseFractal(0.0035,0.0022,4,7));
ctx.fillRect(0,0,W,HORIZON); ctx.restore();

// Stars: extinguished toward the horizon haze, washed out by moon glare.
for (let i=0;i<1100;i++){
  const x=rnd()*W, y=rnd()*(HORIZON-6), dM=Math.hypot(x-MOON.x,y-MOON.y);
  let a=Math.pow(rnd(),2.6)*0.95+0.05;
  a*=Math.min(1,Math.pow((HORIZON-y)/(HORIZON*0.72),1.1));
  a*=Math.min(1,Math.max(0.04,(dM-MOON.r)/320));
  if(a<=0.015) continue;
  const r=0.35+Math.pow(rnd(),3)*1.5, warm=rnd();
  ctx.fillStyle = warm>0.90?`rgba(255,224,196,${a})`:warm>0.78?`rgba(200,220,255,${a})`:`rgba(236,243,255,${a})`;
  ctx.beginPath(); ctx.arc(x,y,r,0,Math.PI*2); ctx.fill();
  if(r>1.5&&a>0.7){ ctx.strokeStyle=`rgba(226,236,255,${a*0.4})`; ctx.lineWidth=0.7;
    ctx.beginPath(); ctx.moveTo(x-r*5,y); ctx.lineTo(x+r*5,y);
    ctx.moveTo(x,y-r*5); ctx.lineTo(x,y+r*5); ctx.stroke(); }
}

// Cirrus, not cumulus: long blurred streaks echo the horizontal of the sea
// and brighten as they approach the moon.
function cirrusBand(cy,x0,x1,count,thick,alpha,blur,tilt){
  ctx.save(); ctx.globalCompositeOperation='screen';
  ctx.filter=Skia.ImageFilter.blur(blur,blur*0.42);
  for(let i=0;i<count;i++){
    const t=i/count, cx=x0+t*(x1-x0)+(rnd()-0.5)*90, y=cy+(rnd()-0.5)*thick*3.4;
    const rx=(60+rnd()*210)*(0.7+rnd()*0.9), ry=thick*(0.35+rnd()*0.85);
    const near=Math.exp(-Math.pow(Math.hypot(cx-MOON.x,y-MOON.y)/430,1.7)), lum=92+near*130;
    ctx.globalAlpha=alpha*(0.35+rnd()*0.8);
    ctx.fillStyle=`rgb(${Math.round(lum*0.86)},${Math.round(lum*0.95)},${Math.round(Math.min(255,lum*1.14))})`;
    ctx.beginPath(); ctx.ellipse(cx,y,rx,ry,tilt+(rnd()-0.5)*0.05,0,Math.PI*2); ctx.fill();
  }
  ctx.restore();
}
cirrusBand(150,-120,700,26,13,0.20,15,-0.020);
cirrusBand(112,780,1560,22,11,0.17,16,0.016);
cirrusBand(238,620,1500,16,8,0.13,13,0.010);
cirrusBand(372,-100,1540,34,7,0.22,9,-0.006);
cirrusBand(412,200,1520,26,5,0.20,7,0.004);
cirrusBand(330,700,1300,12,6,0.26,8,0.008);

/* ------------------------------- MOON ------------------------------ */
for(const g of [[440,0.20],[255,0.22],[145,0.30],[102,0.34]]){
  const halo=ctx.createRadialGradient(MOON.x,MOON.y,MOON.r*0.5,MOON.x,MOON.y,g[0]);
  halo.addColorStop(0.0,`rgba(214,230,255,${g[1]})`);
  halo.addColorStop(0.4,`rgba(170,198,244,${g[1]*0.34})`);
  halo.addColorStop(1.0,'rgba(130,164,220,0)');
  setPaint(halo); ctx.beginPath(); ctx.arc(MOON.x,MOON.y,g[0],0,Math.PI*2); ctx.fill();
}
// F4 workaround: ctx.colorFilter is ignored by drawImage, so the regolith
// swatch is brightened on the bitmap itself before it is drawn.
const lunar = Skia.Image.fromDataUrl(Session.moonUri).applyColorFilter(
  Skia.ColorFilter.colorMatrix([1.85,0,0,0,0.22, 0,1.88,0,0,0.23, 0,0,1.92,0,0.27, 0,0,0,1,0]));
ctx.save();
ctx.beginPath(); ctx.arc(MOON.x,MOON.y,MOON.r,0,Math.PI*2); ctx.clip();
ctx.fillStyle='#eef4ff'; ctx.fillRect(MOON.x-MOON.r,MOON.y-MOON.r,MOON.r*2,MOON.r*2);
ctx.save(); ctx.globalAlpha=0.58;
ctx.drawImage(lunar,MOON.x-MOON.r,MOON.y-MOON.r,MOON.r*2,MOON.r*2); ctx.restore();
// a full moon is lit head-on, so it darkens only at the very limb
const limb=ctx.createRadialGradient(MOON.x,MOON.y,MOON.r*0.76,MOON.x,MOON.y,MOON.r);
limb.addColorStop(0,'rgba(0,0,0,0)'); limb.addColorStop(1,'rgba(52,74,116,0.38)');
setPaint(limb); ctx.fillRect(MOON.x-MOON.r,MOON.y-MOON.r,MOON.r*2,MOON.r*2);
ctx.restore();

// Haze is painted over the whole sky rect, never a band, so no edge shows.
const haze=ctx.createLinearGradient(0,0,0,HORIZON);
haze.addColorStop(0.00,'rgba(150,180,224,0)'); haze.addColorStop(0.55,'rgba(150,180,224,0)');
haze.addColorStop(0.86,'rgba(150,180,224,0.055)'); haze.addColorStop(1.00,'rgba(158,188,230,0.19)');
setPaint(haze); ctx.fillRect(0,0,W,HORIZON);
ctx.save(); ctx.beginPath(); ctx.rect(0,0,W,HORIZON); ctx.clip();
const hazeM=ctx.createRadialGradient(MOON.x,HORIZON,10,MOON.x,HORIZON,430);
hazeM.addColorStop(0.00,'rgba(198,220,255,0.26)'); hazeM.addColorStop(0.45,'rgba(198,220,255,0.08)');
hazeM.addColorStop(1.00,'rgba(198,220,255,0)');
setPaint(hazeM); ctx.beginPath(); ctx.arc(MOON.x,HORIZON,430,0,Math.PI*2); ctx.fill();
ctx.restore();

/* ------------------------------- SEA ------------------------------- */
const sea=ctx.createLinearGradient(0,HORIZON,0,H);
sea.addColorStop(0.000,'#2c3c54'); sea.addColorStop(0.035,'#141d2d');
sea.addColorStop(0.280,'#080f1c'); sea.addColorStop(1.000,'#03060d');
setPaint(sea); ctx.fillRect(0,HORIZON,W,H-HORIZON);

// The moonglade. Brightness is the product of distance from the glade axis
// and depth down the frame; that product is what makes it read as a path on
// water rather than a painted cone. Built from separable specks.
const ROWS=118;
for(let i=1;i<=ROWS;i++){
  const t=i/ROWS, y=rowY(t), rowH=Math.max(1.0,rowY((i+1)/ROWS)-y);
  const gh=20+t*235, count=Math.round(12+t*52);
  for(let j=0;j<count;j++){
    const x=(MOON.x-gh*2.4)+rnd()*gh*4.8;
    if(x<-30||x>W+30) continue;
    const g=Math.exp(-Math.pow(Math.abs(x-MOON.x)/gh,2.0));
    if(g<0.05) continue;
    const a=Math.min(0.70,g*(0.24+0.46*Math.pow(t,0.7))*(0.30+rnd()));
    if(a<0.02) continue;
    ctx.fillStyle=`rgba(${198+Math.round(g*48)},${216+Math.round(g*34)},${238+Math.round(g*17)},${a})`;
    ctx.beginPath();
    ctx.ellipse(x,y+rnd()*rowH,(1.2+rnd()*6.5)*(0.30+t*1.5),Math.max(0.5,rowH*(0.10+rnd()*0.22)),0,0,Math.PI*2);
    ctx.fill();
  }
}
// Off-glade chop: short broken dashes at random angles. Long horizontals
// here read as corduroy, which is the failure mode this avoids.
for(let i=0;i<2600;i++){
  const t=Math.pow(rnd(),0.5), y=rowY(t)+(rnd()-0.5)*6, x=rnd()*W;
  const a=(0.025+rnd()*0.075)*(0.35+t)*(Math.abs(x-MOON.x)>260?1:0.5);
  ctx.fillStyle=`rgba(146,172,210,${a})`;
  ctx.beginPath();
  ctx.ellipse(x,y,(0.8+rnd()*3.2)*(0.35+t*1.1),0.45+t*0.85,(rnd()-0.5)*0.16,0,Math.PI*2); ctx.fill();
}
// Swell crests: few, short, staggered, never spanning the frame.
ctx.lineCap='round';
for(let i=0;i<70;i++){
  const t=0.24+Math.pow(rnd(),0.75)*0.80, y=rowY(Math.min(t,1));
  if(y>H+40) continue;
  const amp=1+t*11, len=(90+rnd()*320)*(0.4+t);
  const x0=-100+rnd()*(W+120), x1=x0+len, cx=(x0+x1)/2;
  const g=Math.exp(-Math.pow(Math.abs(cx-MOON.x)/(110+t*520),1.8));
  ctx.strokeStyle=`rgba(184,206,240,${(0.03+g*0.17)*(0.35+t)})`;
  ctx.lineWidth=0.6+t*2.0;
  ctx.beginPath(); ctx.moveTo(x0,y);
  ctx.bezierCurveTo(x0+len*0.3,y-amp,x0+len*0.7,y+amp,x1,y); ctx.stroke();
}
const seam=ctx.createLinearGradient(0,HORIZON-1,0,HORIZON+16);
seam.addColorStop(0,'rgba(14,20,32,0.55)'); seam.addColorStop(1,'rgba(14,20,32,0)');
setPaint(seam); ctx.fillRect(0,HORIZON-1,W,18);

/* -------------------------- SHIP GEOMETRY --------------------------
   Local frame: x along the keel (bow negative), y up-negative, waterline 0.
   Placement is the four numbers in SHIP.                              */
const MASTS=[
 {x:-190,deck:-76,top:-410,cap:7.5,sails:[[-180,-86,112],[-286,-188,102],[-376,-294,78]]},
 {x:-10, deck:-52,top:-478,cap:8.5,sails:[[-200,-90,130],[-320,-208,120],[-420,-328,92]]},
 {x:150, deck:-64,top:-392,cap:6.5,sails:[[-320,-244,68]]}];
const WATERLINE_D='M272,-26 C250,-8 170,2 60,4 C-70,6 -196,0 -258,-16';
function hullPath(c){c.beginPath();c.moveTo(-296,-104);
 c.bezierCurveTo(-262,-110,-228,-88,-196,-76);   // forward sheer
 c.bezierCurveTo(-110,-58,-30,-52,56,-56);       // midship, lowest
 c.bezierCurveTo(132,-62,196,-80,240,-100);      // rising to the poop
 c.lineTo(266,-112); c.lineTo(272,-26);          // taffrail, transom
 c.bezierCurveTo(250,-8,170,2,60,4);             // wetted edge
 c.bezierCurveTo(-70,6,-196,0,-258,-16);
 c.bezierCurveTo(-282,-26,-296,-62,-296,-104); c.closePath();}
function sheerPath(c){c.beginPath();c.moveTo(-296,-104);
 c.bezierCurveTo(-262,-110,-228,-88,-196,-76);
 c.bezierCurveTo(-110,-58,-30,-52,56,-56);
 c.bezierCurveTo(132,-62,196,-80,240,-100); c.lineTo(266,-112);}
function waterlinePath(c){c.beginPath();c.moveTo(272,-26);
 c.bezierCurveTo(250,-8,170,2,60,4); c.bezierCurveTo(-70,6,-196,0,-258,-16);}
// Square sail: straight yard on top, leeches bowed slightly to leeward, foot
// in a shallow catenary. Square sails are wider than they are tall.
function sailPath(c,mx,yTop,yBot,hw){const h=yBot-yTop,b=hw*0.055;
 c.beginPath(); c.moveTo(mx-hw,yTop); c.lineTo(mx+hw,yTop);
 c.bezierCurveTo(mx+hw+b,yTop+h*0.50,mx+hw+b,yBot-h*0.18,mx+hw*1.02,yBot);
 c.bezierCurveTo(mx+hw*0.45,yBot+h*0.100,mx-hw*0.45,yBot+h*0.115,mx-hw*1.02,yBot);
 c.bezierCurveTo(mx-hw-b*0.9,yBot-h*0.18,mx-hw-b*1.2,yTop+h*0.50,mx-hw,yTop); c.closePath();}
function spankerPath(c){c.beginPath();c.moveTo(152,-244);
 c.bezierCurveTo(190,-252,222,-255,244,-251);    // gaff
 c.bezierCurveTo(264,-198,274,-128,272,-70);     // leech
 c.bezierCurveTo(232,-64,190,-66,152,-78); c.closePath();}   // boom
const cloth=Skia.Image.fromDataUrl(Session.clothUri);
const plank=Skia.Image.fromDataUrl(Session.oakUri).applyColorFilter(
  Skia.ColorFilter.colorMatrix([0.34,0,0,0,0, 0,0.37,0,0,0, 0,0,0.46,0,0.01, 0,0,0,1,0]));

/* ----- reflection, under the hull, before the ship ----- */
ctx.save();
ctx.translate(SHIP.x,SHIP.y+3); ctx.rotate(-SHIP.heel); ctx.scale(SHIP.s,-SHIP.s*0.46);
ctx.filter=Skia.ImageFilter.blur(3,7); ctx.globalAlpha=0.62; ctx.fillStyle='#03060c';
hullPath(ctx); ctx.fill(); ctx.restore();
ctx.save(); ctx.filter=Skia.ImageFilter.blur(14,22); ctx.globalAlpha=0.44; ctx.fillStyle='#03060c';
ctx.beginPath(); ctx.ellipse(SHIP.x-20,SHIP.y+64,215,68,0,0,Math.PI*2); ctx.fill(); ctx.restore();
ctx.save(); ctx.beginPath(); ctx.rect(360,SHIP.y-2,700,150); ctx.clip();
for(let i=0;i<1000;i++){
  const y=SHIP.y+Math.pow(rnd(),1.25)*145, x=370+rnd()*680;
  const g=Math.exp(-Math.pow(Math.abs(x-MOON.x)/300,2));
  ctx.fillStyle=`rgba(178,200,236,${(0.04+rnd()*0.16)*(0.35+g)})`;
  ctx.beginPath(); ctx.ellipse(x,y,1+rnd()*7,0.5+rnd()*1.3,0,0,Math.PI*2); ctx.fill();
}
ctx.restore();

/* ----------------------------- THE SHIP ---------------------------- */
ctx.save();
ctx.translate(SHIP.x,SHIP.y); ctx.rotate(SHIP.heel); ctx.scale(SHIP.s,SHIP.s);

// standing rigging, behind everything
ctx.strokeStyle='rgba(9,13,21,0.9)'; ctx.lineCap='round'; ctx.lineWidth=2.0;
for(const s of [[[-190,-410],[-452,-186]],[[-190,-292],[-412,-172]],[[-190,-238],[-346,-144]],
 [[-10,-478],[-190,-400]],[[-10,-320],[-190,-278]],[[-10,-200],[-190,-172]],
 [[150,-392],[-10,-312]],[[150,-250],[-10,-160]]])
{ctx.beginPath();ctx.moveTo(s[0][0],s[0][1]);ctx.lineTo(s[1][0],s[1][1]);ctx.stroke();}
ctx.lineWidth=1.6;
for(const b of [[[-190,-410],[-56,-54]],[[-10,-478],[144,-64]],[[150,-392],[264,-112]],[[150,-392],[268,-70]]])
{ctx.beginPath();ctx.moveTo(b[0][0],b[0][1]);ctx.lineTo(b[1][0],b[1][1]);ctx.stroke();}

// shrouds: lower section only, behind the sails, faint
ctx.strokeStyle='rgba(12,17,26,0.55)';
for(const m of MASTS){
  const topY=m.deck-150, spread=34;
  for(let side=-1;side<=1;side+=2){
    ctx.lineWidth=1.2;
    for(let k=0;k<4;k++){ctx.beginPath();ctx.moveTo(m.x+side*4,topY);
      ctx.lineTo(m.x+side*(11+k*(spread/3)),m.deck+2);ctx.stroke();}
    ctx.lineWidth=0.6;
    for(let r=1;r<=7;r++){const t=r/8, yy=topY+(m.deck+2-topY)*t;
      ctx.beginPath();ctx.moveTo(m.x+side*(4+7*t),yy);
      ctx.lineTo(m.x+side*(4+(7+spread)*t),yy);ctx.stroke();}
  }
}
// masts, tops and yards sit behind the canvas they carry
ctx.strokeStyle='#0d121b';
for(const m of MASTS){
  ctx.lineWidth=m.cap;
  ctx.beginPath();ctx.moveTo(m.x,m.deck+6);ctx.lineTo(m.x,m.top);ctx.stroke();
  ctx.lineWidth=m.cap*0.75;
  ctx.beginPath();ctx.moveTo(m.x-19,m.top+74);ctx.lineTo(m.x+19,m.top+74);ctx.stroke();
  for(const s of m.sails){ctx.lineWidth=4.6;ctx.beginPath();
    ctx.moveTo(m.x-s[2]-15,s[0]+3);
    ctx.quadraticCurveTo(m.x,s[0]-3,m.x+s[2]+15,s[0]+3);ctx.stroke();}
}
ctx.lineWidth=4.4;
ctx.beginPath();ctx.moveTo(150,-246);ctx.lineTo(248,-253);ctx.stroke();   // gaff
ctx.beginPath();ctx.moveTo(150,-76);ctx.lineTo(278,-66);ctx.stroke();     // boom
ctx.lineWidth=8.5;
ctx.beginPath();ctx.moveTo(-280,-98);ctx.lineTo(-462,-186);ctx.stroke();  // bowsprit

/* ----- sails ----- */
function sailDetail(pathFn,mx,yTop,yBot,hw){
  const h=yBot-yTop;
  ctx.save(); pathFn(ctx); ctx.clip();
  const belly=ctx.createRadialGradient(mx+hw*0.42,yTop+h*0.46,2,mx+hw*0.42,yTop+h*0.46,hw*1.35);
  belly.addColorStop(0,'rgba(196,216,246,0.15)'); belly.addColorStop(1,'rgba(196,216,246,0)');
  setPaint(belly); ctx.fillRect(mx-hw*1.4,yTop-8,hw*2.8,h+16);
  ctx.strokeStyle='rgba(18,24,34,0.11)'; ctx.lineWidth=0.7;      // cloth seams
  const n=Math.max(3,Math.round(hw/26));
  for(let i=1;i<n;i++){const x=mx-hw+2*hw*(i/n);
    ctx.beginPath();ctx.moveTo(x,yTop);
    ctx.quadraticCurveTo(x+(x-mx)*0.05,yTop+h*0.5,x,yBot+h*0.06);ctx.stroke();}
  ctx.strokeStyle='rgba(212,228,252,0.055)'; ctx.lineWidth=2.2;  // reef band
  const y1=yTop+h*0.40;
  ctx.beginPath();ctx.moveTo(mx-hw,y1);ctx.quadraticCurveTo(mx,y1+h*0.05,mx+hw,y1);ctx.stroke();
  ctx.restore();
}
function paintSail(pathFn,gx0,gy0,gx1,gy1,bbox,tone,rim,detail){
  pathFn(ctx);
  const g=ctx.createLinearGradient(gx0,gy0,gx1,gy1);
  g.addColorStop(0.00,tone[0]);g.addColorStop(0.34,tone[1]);g.addColorStop(1.00,tone[2]);
  setPaint(g); ctx.fill();
  ctx.save(); pathFn(ctx); ctx.clip();
  ctx.globalCompositeOperation='overlay'; ctx.globalAlpha=0.26;
  setPaint(Skia.Shader.bitmap(cloth,'repeat','repeat'));
  ctx.fillRect(bbox[0],bbox[1],bbox[2],bbox[3]);
  ctx.restore();
  if(detail) detail();
  // bolt rope: a gradient stroke, brightest on the moon side, fading away
  const rg=ctx.createLinearGradient(rim[0],rim[1],rim[2],rim[3]);
  rg.addColorStop(0.00,'rgba(226,240,255,0.85)');
  rg.addColorStop(0.35,'rgba(198,218,250,0.34)');
  rg.addColorStop(1.00,'rgba(150,175,215,0.12)');
  setStroke(rg); ctx.lineWidth=1.15;
  pathFn(ctx); ctx.stroke();
}
const TONE=['#65758d','#3d4859','#1c232f'];
for(const m of MASTS) for(const s of m.sails){
  const p=c=>sailPath(c,m.x,s[0],s[1],s[2]);
  paintSail(p,m.x+s[2],s[0],m.x-s[2]*0.75,s[1],
    [m.x-s[2]-20,s[0]-10,s[2]*2+40,s[1]-s[0]+40],TONE,
    [m.x+s[2]*1.05,s[0]-6,m.x-s[2]*1.05,s[1]],
    ()=>sailDetail(p,m.x,s[0],s[1],s[2]));
}
// the spanker is nearly edge-on to the moon, so it is the darkest sail
paintSail(spankerPath,272,-251,152,-78,[140,-262,150,200],
  ['#3c4759','#252d3b','#131922'],[276,-255,150,-80],
  ()=>sailDetail(spankerPath,212,-250,-70,62));

function jib(head,tack,clew,bow){
  const p=c=>{c.beginPath();c.moveTo(head[0],head[1]);
    c.quadraticCurveTo((head[0]+tack[0])/2-bow*0.4,(head[1]+tack[1])/2+bow,tack[0],tack[1]);
    c.lineTo(clew[0],clew[1]);
    c.quadraticCurveTo((clew[0]+head[0])/2+bow,(clew[1]+head[1])/2,head[0],head[1]);
    c.closePath();};
  p(ctx);
  const g=ctx.createLinearGradient(clew[0],clew[1],tack[0],tack[1]);
  g.addColorStop(0,'#59687e'); g.addColorStop(1,'#232c3a');
  setPaint(g); ctx.fill();
  ctx.save(); p(ctx); ctx.clip();
  ctx.globalCompositeOperation='overlay'; ctx.globalAlpha=0.22;
  setPaint(Skia.Shader.bitmap(cloth,'repeat','repeat'));
  ctx.fillRect(-480,-330,320,260); ctx.restore();
  const rg=ctx.createLinearGradient(clew[0],clew[1],tack[0],tack[1]);
  rg.addColorStop(0,'rgba(216,232,255,0.6)'); rg.addColorStop(1,'rgba(150,175,215,0.14)');
  setStroke(rg); ctx.lineWidth=1.0; ctx.stroke();
}
jib([-196,-300],[-444,-180],[-250,-126],22);
jib([-192,-232],[-342,-142],[-216,-92],16);

/* ----- hull ----- */
hullPath(ctx);
const hullG=ctx.createLinearGradient(0,-116,0,4);
hullG.addColorStop(0.00,'#37455e'); hullG.addColorStop(0.42,'#1b2333'); hullG.addColorStop(1.00,'#05080f');
setPaint(hullG); ctx.fill();
ctx.save(); hullPath(ctx); ctx.clip();
ctx.globalCompositeOperation='overlay'; ctx.globalAlpha=0.50;
setPaint(Skia.Shader.bitmap(plank,'repeat','repeat'));
ctx.fillRect(-300,-120,580,132);
ctx.globalCompositeOperation='source-over'; ctx.globalAlpha=1;
ctx.strokeStyle='rgba(4,7,12,0.85)'; ctx.lineWidth=5;            // the wale
ctx.beginPath(); ctx.moveTo(-292,-70);
ctx.bezierCurveTo(-200,-44,-40,-24,90,-28);
ctx.bezierCurveTo(180,-32,240,-50,274,-68); ctx.stroke();
ctx.strokeStyle='rgba(150,175,214,0.20)'; ctx.lineWidth=1.1;     // its lit top edge
ctx.beginPath(); ctx.moveTo(-292,-74);
ctx.bezierCurveTo(-200,-48,-40,-28,90,-32);
ctx.bezierCurveTo(180,-36,240,-54,274,-72); ctx.stroke();
ctx.fillStyle='rgba(3,5,9,0.95)';                                 // gun ports give scale
for(let i=0;i<9;i++){const t=i/8, x=-250+t*480, y=-74+Math.sin(t*Math.PI)*26-8;
  ctx.beginPath(); ctx.rect(x-8,y-8,16,15); ctx.fill();
  ctx.fillStyle='rgba(154,178,216,0.22)';
  ctx.fillRect(x-8,y-8,16,1.6);
  ctx.fillStyle='rgba(3,5,9,0.95)';}
ctx.restore();
sheerPath(ctx);                                                   // rim light, moon side
ctx.strokeStyle='rgba(212,230,255,0.72)'; ctx.lineWidth=1.7; ctx.stroke();
ctx.beginPath(); ctx.moveTo(266,-112); ctx.lineTo(272,-26);
ctx.strokeStyle='rgba(212,230,255,0.5)'; ctx.lineWidth=2.2; ctx.stroke();

// the stern lantern: the one warm note in an otherwise cold picture
const lamp=ctx.createRadialGradient(258,-120,1,258,-120,50);
lamp.addColorStop(0.0,'rgba(255,210,138,0.95)');
lamp.addColorStop(0.2,'rgba(255,178,86,0.42)');
lamp.addColorStop(1.0,'rgba(255,158,58,0)');
setPaint(lamp); ctx.beginPath(); ctx.arc(258,-120,50,0,Math.PI*2); ctx.fill();
ctx.fillStyle='rgba(255,232,186,0.97)';
ctx.beginPath(); ctx.ellipse(258,-120,3.0,4.2,0,0,Math.PI*2); ctx.fill();

// Seat the hull in the water. Snap.path measures the wetted edge so foam can
// be scattered along it rather than stroked as a clean curve.
waterlinePath(ctx);
ctx.strokeStyle='rgba(6,10,17,0.72)'; ctx.lineWidth=13; ctx.lineCap='round'; ctx.stroke();
const wlLen=Snap.path.getTotalLength(WATERLINE_D);
for(let i=0;i<300;i++){
  const u=rnd();
  const p=Snap.path.getPointAtLength(WATERLINE_D, u*wlLen);
  const ends=Math.pow(Math.abs(u-0.5)*2,1.4);   // foam gathers at bow and stern
  const a=(0.05+rnd()*0.26)*(0.30+ends*1.25);
  ctx.fillStyle=`rgba(224,238,255,${a})`;
  ctx.beginPath();
  ctx.ellipse(p.x+(rnd()-0.5)*9,p.y+1.5+(rnd()-0.4)*7,1.2+rnd()*6,0.6+rnd()*1.9,0,0,Math.PI*2);
  ctx.fill();
}
ctx.restore();

/* ------------------------ BOW WAVE AND WAKE ------------------------ */
const WL=SHIP.y+4;
ctx.save(); ctx.globalCompositeOperation='screen';
for(let i=0;i<380;i++){
  const x=SHIP.x-214+rnd()*76+(rnd()-0.5)*20, y=WL-14+Math.pow(rnd(),0.7)*26;
  ctx.fillStyle=`rgba(206,224,250,${0.04+rnd()*0.22})`;
  ctx.beginPath(); ctx.ellipse(x,y,1+rnd()*6,0.6+rnd()*1.8,0,0,Math.PI*2); ctx.fill();
}
for(let i=0;i<950;i++){
  const t=rnd(), x=SHIP.x+205+t*350, y=WL-10+(rnd()-0.5)*(18+t*88);
  ctx.fillStyle=`rgba(190,212,244,${(0.045+rnd()*0.24)*(1-t*0.7)})`;
  ctx.beginPath(); ctx.ellipse(x,y,1+rnd()*8,0.5+rnd()*1.5,0,0,Math.PI*2); ctx.fill();
}
ctx.restore();

/* ------------------------------ GRADE ------------------------------ */
Drawing.drawVignette(ctx, W, H, { vignetteColor:'#02040a', intensity:0.62, radius:0.78 });
canvas;
