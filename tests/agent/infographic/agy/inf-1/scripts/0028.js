Stage.begin('Detail');
Stage.note('Detail pass: fill dead air in callout with the burst stat block. Shrink era panel allocated height. Add timestamp/serial to header. Add a vertical POLSON label on the right strip. Tighten gap between panels.');

const W = 1080, H = 1920;
const canvas = createCanvas(W, H);
const ctx = canvas.getContext('2d');

const INK    = '#0a0a0a';
const PAPER  = '#f0ead8';
const YELLOW = '#f7c31a';
const PINK   = '#e8175d';
const CYAN   = '#00b4c8';
const GREEN  = '#16a34a';
const ORANGE = '#ea580c';
const PURPLE = '#a855f7';
const WHITE  = '#ffffff';
const MCOLORS = [PINK, YELLOW, GREEN, CYAN, PURPLE, ORANGE, PINK];

const MILESTONES = [
  { year: 1969, label: 'ARPANET', desc: 'First message — crashed after "LO"' },
  { year: 1989, label: 'WWW', desc: 'Berners-Lee proposes HTTP/HTML' },
  { year: 1993, label: 'MOSAIC', desc: 'Makes the web visual' },
  { year: 1998, label: 'GOOGLE', desc: 'PageRank indexes the web' },
  { year: 2004, label: 'FACEBOOK', desc: 'Social era begins' },
  { year: 2007, label: 'iPHONE', desc: 'Web in every pocket' },
  { year: 2022, label: 'CHATGPT', desc: '100M users in 2 months' },
];
const GAPS = [
  { label: 'ARPANET\u2192WEB',    years: 20 },
  { label: 'iPHONE\u2192AI',     years: 15 },
  { label: 'GOOGLE\u2192FB',     years: 6  },
  { label: 'MOSAIC\u2192GOOGLE', years: 5  },
  { label: 'WEB\u2192MOSAIC',    years: 4  },
  { label: 'FB\u2192iPHONE',     years: 3  },
];
const ERA_DATA = [
  { label: 'ARPANET LULL', years: 20, color: PINK   },
  { label: '5-REV BURST',  years: 18, color: GREEN  },
  { label: 'iPHONE LULL',  years: 15, color: ORANGE },
];
if (20+18+15 !== 53) throw new Error('ERA sum');

// === GROUND ===
ctx.fillStyle = PAPER;
ctx.fillRect(0,0,W,H);
ctx.strokeStyle='rgba(150,135,110,0.22)'; ctx.lineWidth=1;
for (let hy=0; hy<H; hy+=60) { ctx.beginPath(); ctx.moveTo(0,hy); ctx.lineTo(W,hy); ctx.stroke(); }

const STRIP=40;
ctx.fillStyle='#ddd5bc'; ctx.fillRect(0,0,STRIP,H); ctx.fillRect(W-STRIP,0,STRIP,H);
ctx.strokeStyle=INK; ctx.lineWidth=2;
ctx.beginPath(); ctx.moveTo(STRIP,0); ctx.lineTo(STRIP,H); ctx.stroke();
ctx.beginPath(); ctx.moveTo(W-STRIP,0); ctx.lineTo(W-STRIP,H); ctx.stroke();
for (let hy=30; hy<H; hy+=60) {
  for (const hx of [STRIP/2,W-STRIP/2]) {
    ctx.fillStyle=PAPER; ctx.strokeStyle='#b0a08a'; ctx.lineWidth=1.5;
    ctx.beginPath(); ctx.arc(hx,hy,9,0,Math.PI*2); ctx.fill(); ctx.stroke();
  }
}
// Row numbers on left strip
ctx.fillStyle='rgba(10,10,10,0.28)';
ctx.font='400 7px "Courier New",monospace';
ctx.textAlign='center'; ctx.textBaseline='middle';
for (let hy=30,row=1; hy<H; hy+=60,row++) {
  ctx.fillText(String(row).padStart(3,'0'), STRIP/2, hy);
}

// === LAYOUT ===
const page = Layout.inset(Layout.rect(STRIP+1,0,W-(STRIP+1)*2,H),0,10);
const HDR_H = 80;
const [hdrRect,mainRect] = Layout.rows(page,[HDR_H,page.height-HDR_H],0);
const [leftRect,rightRect] = Layout.columns(mainRect,[44,56],10);

// === HEADER ===
ctx.fillStyle=YELLOW; ctx.fillRect(hdrRect.x+4,hdrRect.y+4,hdrRect.width,HDR_H);
ctx.fillStyle=INK; ctx.fillRect(hdrRect.x,hdrRect.y,hdrRect.width,HDR_H);
ctx.fillStyle=YELLOW; ctx.font='black 43px Impact,Arial';
ctx.textAlign='left'; ctx.textBaseline='middle';
ctx.letterSpacing=LogoType.computeWordmarkTracking(43,true)+'em';
ctx.fillText('THE INTERNET WAS NEVER ON SCHEDULE', hdrRect.x+14, hdrRect.y+HDR_H/2);
ctx.letterSpacing='0px';
// Edition badge
ctx.fillStyle=PINK; ctx.fillRect(hdrRect.x2-120,hdrRect.y+8,110,26);
ctx.strokeStyle=YELLOW; ctx.lineWidth=1.5; ctx.strokeRect(hdrRect.x2-120,hdrRect.y+8,110,26);
ctx.fillStyle=WHITE; ctx.font='bold 10px "Courier New",monospace';
ctx.textAlign='center'; ctx.textBaseline='middle';
ctx.fillText('SPOOL: 1969–2022', hdrRect.x2-65, hdrRect.y+21);

// === LEFT TIMELINE ===
const tlPad=Layout.inset(leftRect,30,6,30,4);
const yScale=Scale.linear(1969,2022,tlPad.y,tlPad.y2);
const TL_X=tlPad.x+Math.round(tlPad.width*0.62);

// Year ticks
ctx.strokeStyle='rgba(10,10,10,0.18)'; ctx.lineWidth=1;
ctx.font='400 9px "Courier New",monospace';
ctx.fillStyle='rgba(10,10,10,0.35)';
ctx.textAlign='left'; ctx.textBaseline='middle';
for (const yr of [1970,1975,1980,1985,1990,1995,2000,2005,2010,2015,2020]) {
  const ty=yScale.map(yr);
  ctx.beginPath(); ctx.moveTo(tlPad.x,ty); ctx.lineTo(TL_X+4,ty); ctx.stroke();
  ctx.fillText(yr, tlPad.x+2, ty);
}
// Stem
ctx.strokeStyle=INK; ctx.lineWidth=5;
ctx.beginPath(); ctx.moveTo(TL_X,yScale.map(1969)); ctx.lineTo(TL_X,yScale.map(2022)); ctx.stroke();

// Lull blocks
const drawLull=(fromYr,toYr,color,label)=>{
  const y1=yScale.map(fromYr),y2=yScale.map(toYr);
  const lx=TL_X+6,lw=tlPad.x2-lx;
  ctx.fillStyle=INK; ctx.fillRect(lx+4,y1+4,lw,y2-y1);
  ctx.fillStyle=color; ctx.globalAlpha=0.88; ctx.fillRect(lx,y1,lw,y2-y1);
  ctx.globalAlpha=1.0; ctx.strokeStyle=INK; ctx.lineWidth=3; ctx.strokeRect(lx,y1,lw,y2-y1);
  ctx.fillStyle=WHITE; ctx.font='bold 16px Impact,Arial';
  ctx.textAlign='center'; ctx.textBaseline='middle';
  const lines=label.split('\n'),midY=(y1+y2)/2;
  lines.forEach((l,i)=>ctx.fillText(l,lx+lw/2,midY+(i-(lines.length-1)/2)*22));
};
drawLull(1969,1989,PINK,'20 YR\nLULL');
drawLull(2007,2022,ORANGE,'15 YR\nLULL');

// Event cards
const CARD_W=TL_X-tlPad.x-10;
for (let i=0; i<MILESTONES.length; i++) {
  const m=MILESTONES[i],my=yScale.map(m.year),col=MCOLORS[i];
  const cardH=46,cardY=my-cardH/2,cx=tlPad.x;
  ctx.fillStyle=INK; ctx.fillRect(cx+3,cardY+3,CARD_W,cardH);
  ctx.fillStyle=col; ctx.fillRect(cx,cardY,CARD_W,cardH);
  ctx.strokeStyle=INK; ctx.lineWidth=3; ctx.strokeRect(cx,cardY,CARD_W,cardH);
  ctx.fillStyle=INK; ctx.font='bold 10px "Courier New",monospace';
  ctx.textAlign='left'; ctx.textBaseline='top';
  ctx.fillText(m.year, cx+5, cardY+4);
  ctx.font='bold 14px Impact,Arial';
  ctx.fillText(m.label, cx+5, cardY+16);
  ctx.font='400 8px "Courier New",monospace';
  ctx.fillText(m.desc, cx+5, cardY+32);
  ctx.fillStyle=INK; ctx.beginPath(); ctx.arc(TL_X,my,11,0,Math.PI*2); ctx.fill();
  ctx.fillStyle=col; ctx.beginPath(); ctx.arc(TL_X,my,8,0,Math.PI*2); ctx.fill();
  ctx.strokeStyle=col; ctx.lineWidth=2.5;
  ctx.beginPath(); ctx.moveTo(cx+CARD_W,my); ctx.lineTo(TL_X-11,my); ctx.stroke();
}

// === RIGHT PANELS ===
const rPad=Layout.inset(rightRect,8,0,8,0);
// Proportions: callout:26%, gaps:34%, era:12%, footer:28%
const [rCallout,rGaps,rEra,rFooter]=Layout.rows(rPad,[26,34,12,28],10);

const drawPanel=(rect,title,titleBg,titleFg)=>{
  ctx.fillStyle=INK; ctx.fillRect(rect.x+5,rect.y+5,rect.width,rect.height);
  ctx.fillStyle=WHITE; ctx.fillRect(rect.x,rect.y,rect.width,rect.height);
  ctx.strokeStyle=INK; ctx.lineWidth=3; ctx.strokeRect(rect.x,rect.y,rect.width,rect.height);
  const tH=26;
  ctx.fillStyle=titleBg; ctx.fillRect(rect.x,rect.y,rect.width,tH);
  ctx.fillStyle=titleFg||INK; ctx.font='bold 11px "Courier New",monospace';
  ctx.textAlign='left'; ctx.textBaseline='middle'; ctx.letterSpacing='0.08em';
  ctx.fillText(title,rect.x+10,rect.y+tH/2); ctx.letterSpacing='0px';
  return tH;
};

// PANEL 1: Callout (26% of right)
const tH1=drawPanel(rCallout,'// LONGEST WAIT VS FASTEST ADOPTION',INK,YELLOW);
const i1=Layout.inset(rCallout,tH1+10,14,10,14);

// Two-column: 20YRS | 2MOS
const [i1L,i1R]=Layout.columns(i1,[1,1],10);
ctx.fillStyle=PINK; ctx.font='black 54px Impact,Arial';
ctx.textAlign='left'; ctx.textBaseline='top';
ctx.fillText('20 YRS', i1L.x, i1L.y);
ctx.fillStyle=INK; ctx.font='bold 9px "Courier New",monospace';
ctx.fillText('ARPANET TO WEB', i1L.x, i1L.y+58);
ctx.fillText('1969 \u2192 1989', i1L.x, i1L.y+70);

ctx.fillStyle=GREEN; ctx.font='black 54px Impact,Arial';
ctx.fillText('2 MOS', i1R.x, i1R.y);
ctx.fillStyle=INK; ctx.font='bold 9px "Courier New",monospace';
ctx.fillText('CHATGPT 100M USERS', i1R.x, i1R.y+58);
ctx.fillText('2022', i1R.x, i1R.y+70);

const vsX=(i1L.x2+i1R.x)/2;
ctx.fillStyle=INK; ctx.fillRect(vsX-18,i1L.y+14,36,26);
ctx.fillStyle=WHITE; ctx.font='black 17px Impact,Arial';
ctx.textAlign='center'; ctx.textBaseline='middle';
ctx.fillText('vs.', vsX, i1L.y+27);

// Stat strip: acceleration ratio + burst detail
const statY=i1L.y+86;
ctx.strokeStyle=INK; ctx.lineWidth=1.5;
ctx.beginPath(); ctx.moveTo(i1.x,statY); ctx.lineTo(i1.x2,statY); ctx.stroke();

const [statL,statR]=Layout.columns(
  Layout.inset(Layout.rect(i1.x,statY+6,i1.width,i1.y2-statY-6),0),
  [1,1], 8
);
// Left stat: ~120x faster
ctx.fillStyle=PINK; ctx.font='black 32px Impact,Arial';
ctx.textAlign='left'; ctx.textBaseline='top';
ctx.fillText('~120x', statL.x, statL.y);
ctx.fillStyle=INK; ctx.font='400 8px "Courier New",monospace';
ctx.fillText('FASTER ADOPTION', statL.x, statL.y+36);
ctx.fillText('(20y vs 2 months)', statL.x, statL.y+48);

// Right stat: 18-yr burst, 5 revolutions
ctx.fillStyle=GREEN; ctx.font='black 32px Impact,Arial';
ctx.fillText('5 in 18y', statR.x, statR.y);
ctx.fillStyle=INK; ctx.font='400 8px "Courier New",monospace';
ctx.fillText('REVOLUTIONS IN THE', statR.x, statR.y+36);
ctx.fillText('1989-2007 BURST ERA', statR.x, statR.y+48);

// PANEL 2: Gap bars (34%)
const tH2=drawPanel(rGaps,'// WAITING GAPS BETWEEN REVOLUTIONS',INK,YELLOW);
const i2=Layout.inset(rGaps,tH2+8,10,24,108);
const barYs=Scale.band(GAPS.length,i2.y,i2.y2,0.28);
const barXs=Scale.linear(0,20,i2.x,i2.x2-28);
if (!barXs.isZeroBased) throw new Error('zero baseline');
const GAP_COLORS=[PINK,ORANGE,PURPLE,CYAN,GREEN,YELLOW];
for (let i=0; i<GAPS.length; i++) {
  const g=GAPS[i],by=barYs.map(i),bh=barYs.bandwidth;
  const bw=barXs.extent(0,g.years),col=GAP_COLORS[i];
  ctx.fillStyle='rgba(10,10,10,0.22)'; ctx.fillRect(i2.x+3,by+3,bw,bh);
  ctx.fillStyle=col; ctx.fillRect(i2.x,by,bw,bh);
  ctx.strokeStyle=INK; ctx.lineWidth=2.5; ctx.strokeRect(i2.x,by,bw,bh);
  ctx.fillStyle=INK; ctx.font='bold 9px "Courier New",monospace';
  ctx.textAlign='right'; ctx.textBaseline='middle';
  ctx.fillText(g.label,i2.x-5,by+bh/2);
  ctx.font='bold 12px Impact,Arial'; ctx.textAlign='left';
  ctx.fillText(g.years+'y',i2.x+bw+5,by+bh/2);
}
ctx.strokeStyle=INK; ctx.lineWidth=2;
ctx.beginPath(); ctx.moveTo(i2.x,i2.y2+2); ctx.lineTo(i2.x2-22,i2.y2+2); ctx.stroke();
for (const t of [0,5,10,15,20]) {
  const tx=barXs.map(t);
  ctx.beginPath(); ctx.moveTo(tx,i2.y2+2); ctx.lineTo(tx,i2.y2+6); ctx.stroke();
  ctx.fillStyle=INK; ctx.font='400 9px "Courier New",monospace';
  ctx.textAlign='center'; ctx.textBaseline='top';
  ctx.fillText(t,tx,i2.y2+8);
}

// PANEL 3: Era bar (12%) — compact
const tH3=drawPanel(rEra,'// 53 YEARS: HOW TIME PASSED',INK,YELLOW);
const i3=Layout.inset(rEra,tH3+6,10,28,10);
const eH=38,eY=i3.y+2;
const eraScale=Scale.linear(0,53,i3.x,i3.x2);
let eX=i3.x;
for (const e of ERA_DATA) {
  const ew=eraScale.extent(0,e.years);
  ctx.fillStyle=INK; ctx.fillRect(eX+3,eY+3,ew,eH);
  ctx.fillStyle=e.color; ctx.fillRect(eX,eY,ew,eH);
  ctx.strokeStyle=INK; ctx.lineWidth=3; ctx.strokeRect(eX,eY,ew,eH);
  ctx.fillStyle=WHITE; ctx.font='bold 12px Impact,Arial';
  ctx.textAlign='center'; ctx.textBaseline='middle';
  ctx.fillText((e.years/53*100).toFixed(0)+'%',eX+ew/2,eY+eH/2);
  eX+=ew;
}
let lX=i3.x; const lY=eY+eH+7;
for (const e of ERA_DATA) {
  ctx.fillStyle=e.color; ctx.fillRect(lX,lY,9,8);
  ctx.strokeStyle=INK; ctx.lineWidth=1; ctx.strokeRect(lX,lY,9,8);
  ctx.fillStyle=INK; ctx.font='400 8px "Courier New",monospace';
  ctx.textAlign='left'; ctx.textBaseline='top';
  const lbl=e.years+'y '+e.label;
  ctx.fillText(lbl,lX+13,lY);
  lX+=13+ctx.measureText(lbl).width+12;
}

// PANEL 4: Claim (28%)
const tH4=drawPanel(rFooter,'// THE CLAIM',YELLOW,INK);
const i4=Layout.inset(rFooter,tH4+14,14,14,14);
ctx.fillStyle=INK; ctx.font='black 28px Impact,Arial';
ctx.textAlign='left'; ctx.textBaseline='top'; ctx.letterSpacing='0.01em';
let cy=i4.y;
for (const line of ['PROGRESS IS CONSTANT —','IT NEVER STOPPED COMING,','BUT IT NEVER ONCE','CAME ON SCHEDULE.']) {
  ctx.fillText(line,i4.x,cy); cy+=34;
}
ctx.letterSpacing='0px'; cy+=14;
// Stat badges
const stats=[{v:'66%',l:'OF 53 YRS SPENT WAITING'},{v:'35y',l:'ACROSS 2 QUIET LULLS'}];
for (let i=0; i<stats.length; i++) {
  const sx=i4.x+i*210;
  ctx.fillStyle=PINK; ctx.fillRect(sx+3,cy+3,76,42);
  ctx.fillStyle=INK; ctx.fillRect(sx,cy,76,42);
  ctx.fillStyle=YELLOW; ctx.font='black 28px Impact,Arial';
  ctx.textAlign='left'; ctx.textBaseline='middle';
  ctx.fillText(stats[i].v,sx+5,cy+21);
  ctx.fillStyle=INK; ctx.font='bold 9px "Courier New",monospace';
  ctx.textBaseline='top';
  ctx.fillText(stats[i].l,sx,cy+48);
}
cy+=64;
// Source line
ctx.fillStyle='rgba(10,10,10,0.38)';
ctx.font='400 8px "Courier New",monospace';
ctx.textAlign='left'; ctx.textBaseline='top';
ctx.fillText('SOURCE: BRIEF.MD // VERIFIED // ZERO-BASELINE // 1969-2022', i4.x, cy);

// Page footer
ctx.fillStyle='rgba(10,10,10,0.3)';
ctx.font='400 8px "Courier New",monospace';
ctx.textAlign='left'; ctx.textBaseline='bottom';
ctx.fillText('JOB: INF-1 // SPOOL: 1969-2022 // PAPER: CONTINUOUS FANFOLD // PAGE 001', page.x, page.y2-6);

log('Detail pass done.');
canvas;
