Stage.begin('Data');

Stage.note('CLAIM:   Progress is constant — it never stopped coming, but it never once came on schedule.');
Stage.note('READER:  General. Knows all seven events already; has never seen the gaps between them drawn to scale.');
Stage.note('PLACE:   At a line printer, watching fanfold paper feed through the platen');

log('=== STAGE 1: DATA & CLAIM ===');
log('CLAIM:   Progress is constant — it never stopped coming, but it never once came on schedule.');
log('READER:  General. Knows all seven events already; has never seen the gaps between them drawn to scale.');
log('PLACE:   At a line printer, watching fanfold paper feed through the platen');

const milestones = [
  { year: 1969, title: 'ARPANET sends its first message (it crashed after "LO")', short: 'ARPANET First Message', tag: 'CRASHED AFTER "LO"' },
  { year: 1989, title: 'Tim Berners-Lee proposes the World Wide Web', short: 'World Wide Web Proposed', tag: 'BERNERS-LEE PROPOSAL' },
  { year: 1993, title: 'Mosaic makes the web visual', short: 'Mosaic Web Browser', tag: 'THE WEB GOES VISUAL' },
  { year: 1998, title: 'Google is founded', short: 'Google Founded', tag: 'SEARCH REVOLUTION' },
  { year: 2004, title: 'Facebook launches — the social era begins', short: 'Facebook Launches', tag: 'SOCIAL ERA BEGINS' },
  { year: 2007, title: 'The iPhone puts the web in every pocket', short: 'iPhone Announced', tag: 'WEB IN EVERY POCKET' },
  { year: 2022, title: 'ChatGPT reaches 100M users in 2 months', short: 'ChatGPT 100M Users', tag: 'FASTEST 100M ADOPTION' }
];

const gaps = [
  { fromYear: 1969, toYear: 1989, arithmetic: '1989 - 1969', years: 20, desc: 'ARPANET → Web gap', label: '20 YR LULL' },
  { fromYear: 1989, toYear: 1993, arithmetic: '1993 - 1989', years: 4, desc: 'Web → Mosaic gap', label: '4 YR GAP' },
  { fromYear: 1993, toYear: 1998, arithmetic: '1998 - 1993', years: 5, desc: 'Mosaic → Google gap', label: '5 YR GAP' },
  { fromYear: 1998, toYear: 2004, arithmetic: '2004 - 1998', years: 6, desc: 'Google → Facebook gap', label: '6 YR GAP' },
  { fromYear: 2004, toYear: 2007, arithmetic: '2007 - 2004', years: 3, desc: 'Facebook → iPhone gap', label: '3 YR GAP' },
  { fromYear: 2007, toYear: 2022, arithmetic: '2022 - 2007', years: 15, desc: 'iPhone → ChatGPT gap', label: '15 YR GAP' }
];

const totalSpan = 2022 - 1969; // 53 years
const lullYears = 20 + 15; // 35 years
const clusterYears = 2007 - 1989; // 18 years
const lullPct = (lullYears / totalSpan) * 100; // 66.0377%
const clusterPct = (clusterYears / totalSpan) * 100; // 33.9623%

log('Milestones recorded: ' + milestones.length);
table(milestones);

log('Derived gaps:');
table(gaps);

log('Total Timeline Span: ' + totalSpan + ' years (1969 to 2022)');
log('Long Lulls (1969-1989: 20y + 2007-2022: 15y): ' + lullYears + ' years (' + lullPct.toFixed(1) + '%)');
log('Rapid Cluster (1989-2007): ' + clusterYears + ' years (' + clusterPct.toFixed(1) + '%)');

Session.data = {
  milestones,
  gaps,
  totalSpan,
  lullYears,
  clusterYears,
  lullPct,
  clusterPct
};
