Stage.begin('Data');

log('CLAIM:   Progress is constant — it never stopped coming, but it never once came on schedule.');
log('READER:  General. Knows all seven events already; has never seen the gaps between them drawn to scale.');
log('PLACE:   At a line printer, watching fanfold paper feed through the platen');

Stage.note('Settling data, claim, reader, place, and inspecting environment/fonts.');

const milestones = [
  { year: 1969, title: 'ARPANET', detail: 'Sends its first message (crashed after "LO")' },
  { year: 1989, title: 'World Wide Web', detail: 'Tim Berners-Lee proposes the WWW' },
  { year: 1993, title: 'Mosaic', detail: 'Makes the web visual' },
  { year: 1998, title: 'Google', detail: 'Google is founded' },
  { year: 2004, title: 'Facebook', detail: 'Facebook launches — social era begins' },
  { year: 2007, title: 'iPhone', detail: 'Puts the web in every pocket' },
  { year: 2022, title: 'ChatGPT', detail: 'Reaches 100M users in 2 months' }
];

const gaps = [];
for (let i = 0; i < milestones.length - 1; i++) {
  const g = milestones[i+1].year - milestones[i].year;
  gaps.push({
    from: milestones[i].title,
    to: milestones[i+1].title,
    span: g,
    label: milestones[i].year + ' → ' + milestones[i+1].year + ' (' + g + ' yrs)'
  });
}

table(milestones);
table(gaps);

const fonts = Skia.Font.families();
log('Available font families: ' + fonts.slice(0, 25).join(', '));
log('Has Courier New: ' + Skia.Font.has('Courier New'));
log('Has Consolas: ' + Skia.Font.has('Consolas'));
log('Has monospace: ' + Skia.Font.has('monospace'));
log('Has Impact: ' + Skia.Font.has('Impact'));
log('Has Arial: ' + Skia.Font.has('Arial'));
log('Has Trebuchet MS: ' + Skia.Font.has('Trebuchet MS'));

const canvas = createCanvas(1080, 1920);
const ctx = canvas.getContext('2d');
ctx.fillStyle = '#f2efe9';
ctx.fillRect(0, 0, 1080, 1920);

// Basic proof of data stage
ctx.fillStyle = '#111111';
ctx.font = '700 42px monospace';
ctx.fillText('DATA & CLAIM INITIALIZED', 80, 140);

ctx.font = '400 24px monospace';
ctx.fillText('Claim: Progress is constant, but never on schedule.', 80, 220);
ctx.fillText('Canvas: 1080 x 1920 portrait (9:16 story)', 80, 270);
ctx.fillText('Place: At a line printer watching fanfold paper', 80, 320);

canvas;
