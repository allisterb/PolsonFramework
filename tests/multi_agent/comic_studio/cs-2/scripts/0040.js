// artwork.js — cs-2, comic_studio. Executed verbatim to produce output.webp.
Stage.begin('Critic');
Stage.note('Running artwork.js verbatim to produce output.webp. Executing the consolidated file rather than re-deriving the image proves the deliverable is actually the thing that made the picture, not a description of it.');

const WIDTH = 1024, HEIGHT = 1024;

const ANCHORS = {
    head:   { cx: 600, crownY: 62, chinY: 272, hairL: 498, hairR: 696,
              hairlineY: 126, faceWidestY: 210, faceL: 514, faceR: 682,
              neckTip: { x: 601, y: 308 } },
    eyeL:   { cx: 561, cy: 195, r: 10 },
    eyeR:   { cx: 635, cy: 193, r: 10 },
    browL:  { cx: 560, cy: 172, w: 32 },
    browR:  { cx: 635, cy: 171, w: 30 },
    nose:   { cx: 597, cy: 220, w: 18 },
    mouth:  { l: 574, r: 622, topY: 236, baseY: 253 },
    torso:  { shoulderL: { x: 478, y: 302 }, shoulderR: { x: 758, y: 314 },
              waistY: 500, crotch: { x: 550, y: 574 } },
    emblem: { cx: 617, topY: 336, botY: 404, halfW: 66 },
    buckle: { cx: 584, cy: 526, halfW: 22, halfH: 23 },
    belt:   { peakY: 474, flatY: 510, leftTip: { x: 494, y: 504 }, rightX: 674 },
    armUp:  { fist: { cx: 686, cy: 45 }, elbow: { x: 786, y: 200 }, shoulder: { x: 716, y: 314 } },
    armBent:{ fist: { cx: 362, cy: 334 }, elbow: { x: 440, y: 470 }, shoulder: { x: 478, y: 302 } },
    legNear:{ hip: { x: 600, y: 566 }, knee: { x: 580, y: 690 }, ankle: { x: 537, y: 806 } },
    legFar: { hip: { x: 500, y: 566 }, knee: { x: 462, y: 664 }, lostBehindCapeY: 690 },
    boot:   { cuffY: 810, toe: { x: 396, y: 1004 } },
    cape:   { leftTip: { x: 194, y: 520 }, rightMax: { x: 764, y: 456 }, tailTip: { x: 388, y: 832 } }
};
const PALETTE = {
    capeUpperLeftDark: '#8f1c07', capeUpperLeftLit: '#e74a3e',
    capeWingDark:      '#b6382c', capeWingLit:      '#ff4536',
    capeLowerDark:     '#a03023', capeLowerLit:     '#fd4231',
    capeFarDark:       '#662820', capeFarLit:       '#dc2e15',
    capeMidRightDark:  '#ac382c', capeMidRightLit:  '#fb4434',
    collarLit: '#b03a2c', collarDeep: '#8a2c22',
    suitLit: '#48a9f7', suitDeep: '#1d86e3', suitLeg: '#1782e1',
    armDeep: '#3f96de', armLit: '#44afff',
    gauntletLit: '#e74a3e', gauntletDeep: '#a32613',
    forearm: '#c93a2b', fistLit: '#e04537', fistDeep: '#c1372a',
    bootLit: '#f54739', bootDeep: '#a12b1a',
    beltEnd: '#d02e2e', beltMid: '#f44336', beltEndR: '#d33030',
    skin: '#ffca28', skinShade: '#e59600',
    hairDeep: '#543930', hairLit: '#5d433a',
    gold: '#fdd835', emblemRed: '#d03b3f',
    eye: '#404040', brow: '#6d4c41', mouth: '#795548'
};
const SHAPES = {
    cape: [
        [440,296],[424,310],[412,332],[400,356],[386,382],[364,404],[350,420],[342,428],[332,436],
        [320,444],[308,452],[296,460],[282,468],[266,476],[250,484],[232,492],[216,500],[202,508],
        [194,516],[194,524],[206,532],[224,540],[240,548],[254,556],[266,564],[276,572],[284,580],
        [290,588],[296,596],[302,604],[306,612],[310,620],[312,628],[314,640],[314,656],[314,672],
        [313,688],[312,700],[310,708],[308,716],[306,724],[304,740],[304,756],[306,764],[308,772],
        [312,780],[316,788],[322,796],[328,804],[336,812],[348,820],[364,828],[388,832],
        [424,822],[466,814],[506,806],[536,798],[552,790],[572,780],[596,788],[624,800],[650,812],[666,816],
        [676,800],[684,784],[690,768],[696,752],[702,736],[708,720],[714,704],[720,688],[724,672],
        [730,656],[734,640],[740,624],[744,608],[748,592],[752,576],[756,552],[760,528],[762,504],
        [764,480],[764,456],[764,432],[762,400],[760,376],[756,360],[752,344],[748,332],[736,316],
        [716,300],[700,258],[660,244],[600,238],[540,244],[500,256],[468,274],[446,290]
    ],
    torso: [
        [478,302],[488,292],[510,288],[526,292],[542,300],[560,304],[600,310],[620,306],[660,300],
        [700,296],[740,302],[758,314],[752,324],[740,338],[728,350],[718,362],[712,372],[709,384],
        [706,396],[703,410],[700,420],[696,432],[692,440],[688,450],[684,462],[680,472],[676,482],
        [672,492],[668,504],[620,502],[580,496],[556,490],[528,482],[520,472],[520,456],[516,444],
        [512,432],[512,416],[510,406],[486,400],[456,396],[434,392],[430,380],[436,366],[442,354],
        [450,342],[456,326],[466,312]
    ],
    bentUpperArm: [
        [444,396],[444,410],[450,424],[452,438],[446,452],[436,462],[424,472],[428,480],
        [448,478],[456,468],[462,456],[470,444],[478,432],[486,420],[494,408],[496,400],[478,394],[460,393]
    ],
    bentForearm: [[352,348],[392,340],[414,380],[430,414],[440,444],[442,466],[418,470],[396,444],[376,414],[360,380]],
    bentFist: [
        [362,290],[380,294],[390,304],[394,318],[394,334],[392,350],[388,362],[380,372],
        [366,378],[352,376],[342,368],[336,356],[332,342],[330,328],[332,314],[338,302],[348,293]
    ],
    legs: [
        [516,498],[496,512],[484,524],[474,536],[466,552],[458,566],[452,578],[446,588],[438,598],
        [432,610],[426,620],[422,632],[420,644],[418,652],[420,662],[424,672],[432,682],[446,690],
        [468,678],[486,670],[506,650],[524,626],[540,602],[548,584],[550,574],
        [550,600],[542,624],[532,648],[524,672],[514,696],[506,720],[496,744],[486,768],
        [494,784],[516,796],[534,806],
        [548,800],[554,788],[560,776],[568,764],[576,752],[582,740],[590,728],[598,716],[604,704],
        [612,692],[618,680],[626,668],[632,656],[640,644],[646,632],[650,620],[654,608],[658,596],
        [662,584],[664,572],[666,552],[668,520],[668,498]
    ],
    belt: [
        [512,474],[530,484],[550,492],[570,500],[590,506],[615,509],[645,510],[670,510],
        [674,514],[673,546],[668,549],[640,550],[610,549],[588,546],[566,540],[544,533],
        [522,523],[504,514],[494,504],[496,490],[502,480]
    ],
    buckle: [[587,503],[596,510],[603,518],[606,528],[602,538],[594,544],[584,549],[576,545],[568,538],[563,528],[564,518],[571,510],[578,505]],
    emblem: [[616,404],[590,395],[574,380],[562,366],[556,356],[570,346],[588,340],[606,337],[620,336],[636,339],[652,344],[668,351],[680,357],[672,368],[658,382],[642,395]],
    raisedUpperArm: [
        [744,212],[770,206],[796,198],[816,182],[828,186],[830,202],[830,216],[830,222],
        [828,228],[824,234],[820,240],[816,246],[812,252],[806,258],[802,264],[796,270],
        [792,276],[786,282],[780,288],[776,294],[770,300],[764,306],[758,312],[744,318],
        [712,318],[684,312],[676,302],[682,292],[690,284],[698,276],[706,268],[714,258],[722,248],[730,238],[736,226]
    ],
    gauntlet: [
        [662,22],[676,17],[690,22],[700,28],[710,34],[718,40],[726,46],[732,52],[740,58],
        [746,64],[752,70],[758,76],[762,82],[768,88],[772,94],[778,100],[782,106],[786,112],
        [790,118],[794,124],[798,130],[802,136],[804,142],[808,148],[812,154],[814,160],
        [818,166],[820,172],[822,180],[812,188],[796,198],[770,206],[744,212],[740,196],
        [740,178],[738,166],[736,154],[732,142],[730,130],[726,118],[722,106],[718,94],
        [712,82],[708,76],[692,72],[672,70],[654,64],[650,56],[650,46],[654,36],[658,28]
    ],
    collar: [
        [424,308],[436,284],[456,266],[484,254],[524,244],[576,240],[628,240],[672,242],
        [702,250],[714,272],[704,294],[676,308],[660,308],[640,306],[620,306],[600,310],
        [580,306],[560,304],[536,296],[510,286],[490,280],[470,288],[452,300]
    ],
    hair: [
        [609,62],[624,64],[640,70],[652,80],[664,92],[676,102],[684,112],[690,124],[692,140],
        [694,158],[696,176],[696,192],[692,204],[686,214],[678,224],[676,232],[684,238],
        [688,242],[682,248],[668,252],[660,258],[650,264],[640,268],[600,270],[560,268],
        [544,264],[532,258],[518,252],[514,246],[518,240],[522,234],[520,228],[514,222],
        [508,214],[504,206],[500,198],[498,186],[498,170],[498,154],[500,142],[504,130],
        [506,118],[510,106],[518,98],[526,88],[528,78],[546,74],[562,77],[576,70],[592,65]
    ],
    neck: [[566,262],[630,262],[624,274],[620,286],[612,298],[601,308],[590,298],[580,286],[576,274]],
    face: [
        [532,178],[540,166],[548,160],[556,156],[564,152],[572,148],[580,142],[588,136],
        [596,132],[604,128],[612,126],[620,126],[628,130],[636,138],[644,138],[652,142],
        [660,152],[666,166],[666,176],[668,184],[682,192],[682,200],[680,208],[676,214],
        [668,220],[664,226],[662,232],[660,238],[656,244],[650,250],[644,256],[636,262],
        [626,268],[624,274],[621,282],[618,290],[612,298],[604,304],[601,306],[590,298],
        [582,290],[578,282],[576,274],[570,268],[560,262],[552,256],[546,250],[540,244],
        [536,238],[534,232],[530,226],[522,220],[518,214],[514,208],[514,198],[516,190],[532,182],[534,174]
    ],
    browL: [[544,177],[548,170],[558,166],[568,167],[576,172],[576,179],[566,174],[554,174]],
    browR: [[620,176],[622,168],[632,164],[644,165],[650,171],[650,178],[638,172],[628,172]],
    nose: [[588,215],[606,215],[606,220],[602,224],[597,227],[592,224],[588,220]],
    boot: [
        [456,810],[476,808],[510,806],[540,812],[546,824],[544,834],[534,848],[520,860],
        [506,876],[490,894],[476,912],[464,930],[452,948],[440,970],[430,988],[414,1002],
        [386,1004],[368,988],[372,970],[382,952],[396,934],[412,916],[426,898],[436,874],[446,850],[452,828]
    ]
};
const rgb01 = h => [parseInt(h.substr(1, 2), 16) / 255, parseInt(h.substr(3, 2), 16) / 255, parseInt(h.substr(5, 2), 16) / 255];
function tracePath(ctx, pts) {
    ctx.beginPath();
    ctx.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length - 1; i++)
        ctx.quadraticCurveTo(pts[i][0], pts[i][1], (pts[i][0] + pts[i + 1][0]) / 2, (pts[i][1] + pts[i + 1][1]) / 2);
    ctx.lineTo(pts[pts.length - 1][0], pts[pts.length - 1][1]);
    ctx.closePath();
}
function fillShape(ctx, pts, style) { tracePath(ctx, pts); ctx.fillStyle = style; ctx.fill(); }
function lin(ctx, x0, y0, x1, y1, c0, c1) {
    const g = ctx.createLinearGradient(x0, y0, x1, y1);
    g.addColorStop(0, c0); g.addColorStop(1, c1);
    return g;
}
function makePlaneBlendShader(regions) {
    let decl = '', body = 'float wsum = 0.0; float3 acc = float3(0.0);\n';
    const uniforms = {};
    for (let i = 0; i < regions.length; i++) {
        const R = regions[i];
        decl += `uniform float2 d${i}; uniform float2 l${i}; uniform float4 cd${i}; uniform float4 cl${i};\n`;
        uniforms['d' + i] = R.d;
        uniforms['l' + i] = R.l;
        uniforms['cd' + i] = [R.cd[0], R.cd[1], R.cd[2], 1.0];
        uniforms['cl' + i] = [R.cl[0], R.cl[1], R.cl[2], 1.0];
        body += `{ float2 ax = l${i} - d${i};
                   float t = clamp(dot(coord - d${i}, ax) / dot(ax, ax), -0.15, 1.15);
                   float3 c = mix(cd${i}.rgb, cl${i}.rgb, t);
                   float2 ctr = (d${i} + l${i}) * 0.5;
                   float2 dv = coord - ctr;
                   float dd = dot(dv, dv) + 3600.0;
                   float w = 1.0 / (dd * dd);
                   acc += c * w; wsum += w; }\n`;
    }
    return Skia.Shader.sksl(decl + `half4 main(float2 coord) {\n${body}\n float4 o = float4(acc / wsum, 1.0); return half4(o); }`, uniforms);
}
function capeShader() {
    const P = PALETTE;
    return makePlaneBlendShader([
        { d: [488, 414], cd: rgb01(P.capeUpperLeftDark), l: [312, 356], cl: rgb01(P.capeUpperLeftLit) },
        { d: [441, 558], cd: rgb01(P.capeWingDark),      l: [189, 512], cl: rgb01(P.capeWingLit)      },
        { d: [517, 637], cd: rgb01(P.capeLowerDark),     l: [303, 823], cl: rgb01(P.capeLowerLit)     },
        { d: [643, 416], cd: rgb01(P.capeFarDark),       l: [822, 564], cl: rgb01(P.capeFarLit)       },
        { d: [632, 617], cd: rgb01(P.capeMidRightDark),  l: [713, 813], cl: rgb01(P.capeMidRightLit)  }
    ]);
}
function drawCape(ctx)  { fillShape(ctx, SHAPES.cape, capeShader()); }
function drawTorso(ctx) { fillShape(ctx, SHAPES.torso, lin(ctx, 0, 300, 0, 510, PALETTE.suitLit, PALETTE.suitDeep)); }
function drawBentArm(ctx) {
    fillShape(ctx, SHAPES.bentUpperArm, lin(ctx, 430, 396, 500, 470, PALETTE.armDeep, PALETTE.suitDeep));
    ctx.save();
    ctx.globalAlpha = 0.92;
    fillShape(ctx, SHAPES.bentForearm, PALETTE.forearm);
    ctx.restore();
    fillShape(ctx, SHAPES.bentFist, lin(ctx, 340, 300, 396, 372, PALETTE.fistLit, PALETTE.fistDeep));
}
function drawLegs(ctx) { fillShape(ctx, SHAPES.legs, lin(ctx, 634, 540, 520, 812, PALETTE.suitLit, PALETTE.suitLeg)); }
function drawBelt(ctx) {
    const g = ctx.createLinearGradient(495, 0, 685, 0);
    g.addColorStop(0.00, PALETTE.beltEnd);
    g.addColorStop(0.30, PALETTE.beltMid);
    g.addColorStop(0.72, PALETTE.beltMid);
    g.addColorStop(1.00, PALETTE.beltEndR);
    fillShape(ctx, SHAPES.belt, g);
    fillShape(ctx, SHAPES.buckle, PALETTE.gold);
}
function drawEmblem(ctx) {
    fillShape(ctx, SHAPES.emblem, PALETTE.gold);
    ctx.beginPath();
    ctx.moveTo(582, 357); ctx.lineTo(650, 357); ctx.lineTo(616, 404);
    ctx.closePath();
    ctx.fillStyle = PALETTE.emblemRed;
    ctx.fill();
}
function drawRaisedArm(ctx) {
    fillShape(ctx, SHAPES.raisedUpperArm, lin(ctx, 681, 222, 839, 288, PALETTE.armDeep, PALETTE.armLit));
    fillShape(ctx, SHAPES.gauntlet, lin(ctx, 706, 4, 769, 221, PALETTE.gauntletLit, PALETTE.gauntletDeep));
}
function drawCollar(ctx) {
    const g = ctx.createLinearGradient(430, 260, 700, 300);
    g.addColorStop(0, PALETTE.collarLit);
    g.addColorStop(0.5, PALETTE.collarDeep);
    g.addColorStop(1, PALETTE.collarLit);
    fillShape(ctx, SHAPES.collar, g);
}
function drawHead(ctx) {
    fillShape(ctx, SHAPES.hair, lin(ctx, 583, 44, 637, 270, PALETTE.hairDeep, PALETTE.hairLit));
    fillShape(ctx, SHAPES.neck, lin(ctx, 0, 262, 0, 308, PALETTE.skin, PALETTE.skinShade));
    fillShape(ctx, SHAPES.face, PALETTE.skin);
}
function drawFeatures(ctx) {
    fillShape(ctx, SHAPES.browL, PALETTE.brow);
    fillShape(ctx, SHAPES.browR, PALETTE.brow);
    ctx.fillStyle = PALETTE.eye;
    ctx.beginPath(); ctx.ellipse(ANCHORS.eyeL.cx, ANCHORS.eyeL.cy, 10, 11, 0, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(ANCHORS.eyeR.cx, ANCHORS.eyeR.cy, 10, 11, 0, 0, Math.PI * 2); ctx.fill();
    fillShape(ctx, SHAPES.nose, PALETTE.skinShade);
    ctx.fillStyle = PALETTE.mouth;
    ctx.beginPath();
    ctx.moveTo(574, 236);
    ctx.quadraticCurveTo(598, 241, 622, 236);
    ctx.quadraticCurveTo(612, 254, 598, 253);
    ctx.quadraticCurveTo(584, 254, 574, 236);
    ctx.closePath();
    ctx.fill();
}
function drawBoot(ctx) { fillShape(ctx, SHAPES.boot, lin(ctx, 555, 895, 360, 920, PALETTE.bootDeep, PALETTE.bootLit)); }

const canvas = createCanvas(WIDTH, HEIGHT);
const ctx = canvas.getContext('2d');
drawCape(ctx);
drawTorso(ctx);
drawBentArm(ctx);
drawLegs(ctx);
drawBelt(ctx);
drawEmblem(ctx);
drawRaisedArm(ctx);
drawCollar(ctx);
drawHead(ctx);
drawFeatures(ctx);
drawBoot(ctx);
canvas;
