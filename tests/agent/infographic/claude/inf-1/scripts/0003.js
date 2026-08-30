// STAGE 1 - Data & claim. No drawing here: this script exists to derive every figure the
// piece will use, show its arithmetic, and put the claim on the record before any form is chosen.
Stage.begin('Data');

// The seven milestones, exactly as given in brief.md. Nothing added, nothing rounded.
const MILESTONES = [
    { year: 1969, label: 'ARPANET SENDS ITS FIRST MESSAGE', note: 'it crashed after "LO"' },
    { year: 1989, label: 'TIM BERNERS-LEE PROPOSES THE WORLD WIDE WEB', note: '' },
    { year: 1993, label: 'MOSAIC MAKES THE WEB VISUAL', note: '' },
    { year: 1998, label: 'GOOGLE IS FOUNDED', note: '' },
    { year: 2004, label: 'FACEBOOK LAUNCHES', note: 'the social era begins' },
    { year: 2007, label: 'THE IPHONE PUTS THE WEB IN EVERY POCKET', note: '' },
    { year: 2022, label: 'CHATGPT REACHES 100M USERS IN 2 MONTHS', note: '' }
];

// --- Derived figures. Every one is subtraction or division over the years above, so a reader
// --- can check the step. None of these is a new fact; they are the same seven dates restated.
const gaps = [];
for (let i = 1; i < MILESTONES.length; i++) {
    gaps.push({
        from: MILESTONES[i - 1].year,
        to: MILESTONES[i].year,
        years: MILESTONES[i].year - MILESTONES[i - 1].year
    });
}

const span = MILESTONES[MILESTONES.length - 1].year - MILESTONES[0].year;   // 2022 - 1969
const gapYears = gaps.map(g => g.years);
const longest = Math.max.apply(null, gapYears);
const shortest = Math.min.apply(null, gapYears);
const meanGap = span / gaps.length;                                          // 53 / 6
const silenceShare = longest / span;                                         // 20 / 53

// The dense stretch: milestones 2..6 (1989-2007), which is where five of seven land.
const denseFrom = MILESTONES[1].year, denseTo = MILESTONES[5].year;
const denseCount = 5, denseSpan = denseTo - denseFrom;                       // 2007 - 1989

// The one non-year figure in the table, expressed as a rate.
const chatGptPerMonth = 100 / 2;                                             // 100M over 2 months

log('DERIVED FIGURES (arithmetic shown, all from the seven years in brief.md)');
gaps.forEach(g => log('  ' + g.from + ' -> ' + g.to + '  =  ' + g.to + ' - ' + g.from + '  =  ' + g.years + ' yr'));
log('  total span      = 2022 - 1969 = ' + span + ' yr');
log('  mean interval   = ' + span + ' / ' + gaps.length + ' = ' + meanGap.toFixed(2) + ' yr');
log('  longest silence = ' + longest + ' yr (1969-1989)');
log('  shortest gap    = ' + shortest + ' yr (2004-2007)');
log('  silence share   = ' + longest + ' / ' + span + ' = ' + (silenceShare * 100).toFixed(1) + '% of the whole span');
log('  dense stretch   = ' + denseCount + ' milestones in ' + denseSpan + ' yr (' + denseFrom + '-' + denseTo + ')');
log('  ChatGPT rate    = 100M / 2 months = ' + chatGptPerMonth + 'M users per month');
log('');
log('RATIO CHECK longest:shortest = ' + longest + ':' + shortest + ' = ' + (longest / shortest).toFixed(2) + ':1');
log('  -> intervals differ by more than 6x. A timeline spacing these evenly would be drawing');
log('     the claim instead of the data. The axis must be linear in years.');

log('');
log('CLAIM:   Progress is constant - it never stopped coming, but it never once came on schedule.');
log('READER:  A general audience who knows all seven of these events and has never seen the gaps');
log('         between them drawn to scale.');
log('PLACE:   At a line printer, watching fanfold paper feed through the platen.');

Stage.note('CLAIM: Progress is constant - it never stopped coming, but it never once came on schedule. ' +
    'The director gave "progress is constant". Two readings: constant-as-persistent (supported) and ' +
    'constant-as-evenly-paced (contradicted by the data - intervals run 20,4,5,6,3,15 years, a 6.67:1 ' +
    'spread). Built to the first reading; the geometry will show the irregularity rather than smooth it.');

Stage.note('READER: general audience, knows every one of these seven events already. So the piece cannot ' +
    'trade on novelty of fact - the only thing it can show them that they do not have is the SHAPE of the ' +
    'intervals. That is the whole argument, and it decides the form.');

Stage.note('PLACE (Manual 13 s4): at a line printer, watching fanfold paper feed. Chosen over three ' +
    'alternatives. A flyposted hoarding is the more idiomatic neo-brutalist ground but its overlapping ' +
    'bills destroy exact vertical position, and position IS the argument here. A brutalist tower reads ' +
    'as a pun on the style name and its concrete grey fights the brief\'s saturated flats. A phone screen ' +
    'is the sharpest joke but risks reading as a UI mockup, which fails s6\'s dashboard test. The printer ' +
    'gives a vertical rail (sprocket margins) for free, a participating ground (fanfold, tear perforations), ' +
    'and makes the 20-year silence into blank paper feeding through rather than a caption explaining a gap.');

Stage.note('CANVAS: 1080x1920 portrait (9:16 story), overriding the "1600x1200 landscape" row in ' +
    'brief.md\'s framing table, which contradicted the brief\'s own "Canvas: story" line. Director ' +
    'confirmed portrait. A vertical timeline over a 53-year linear axis needs the height.');

Stage.note('DATA SCOPE: the table gives seven years and exactly one non-year figure (100M users in ' +
    '2 months). Everything else on the canvas will be derived by subtraction from those years, with the ' +
    'arithmetic written into brief.md. Deliberately NOT inventing the figures a piece like this usually ' +
    'leans on - host counts, user curves, traffic volume, adoption percentages. None are in the brief.');

// Nothing to render at this stage.
null;
