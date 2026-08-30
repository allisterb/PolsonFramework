Stage.begin('Data');
Stage.note('Stage 1: Establishing data table, verified figures, and framing statement.');

const claim = "Progress is constant — it never stopped coming, but it never once came on schedule.";
const reader = "General. Knows all seven events already; has never seen the gaps between them drawn to scale.";
const place = "At a line printer, watching fanfold paper feed through the platen";

log('CLAIM:   ' + claim);
log('READER:  ' + reader);
log('PLACE:   ' + place);

const rawData = [
  { year: 1969, event: 'ARPANET sends its first message (it crashed after "LO")' },
  { year: 1989, event: 'Tim Berners-Lee proposes the World Wide Web' },
  { year: 1993, event: 'Mosaic makes the web visual' },
  { year: 1998, event: 'Google is founded' },
  { year: 2004, event: 'Facebook launches — the social era begins' },
  { year: 2007, event: 'The iPhone puts the web in every pocket' },
  { year: 2022, event: 'ChatGPT reaches 100M users in 2 months' }
];

const derivedData = [
  { name: 'ARPANET → Web gap', from: '1969, 1989', arithmetic: '1989 - 1969', result: '20 years', gap: 20 },
  { name: 'Web → Mosaic gap', from: '1989, 1993', arithmetic: '1993 - 1989', result: '4 years', gap: 4 },
  { name: 'Mosaic → Google gap', from: '1993, 1998', arithmetic: '1998 - 1993', result: '5 years', gap: 5 },
  { name: 'Google → Facebook gap', from: '1998, 2004', arithmetic: '2004 - 1998', result: '6 years', gap: 6 },
  { name: 'Facebook → iPhone gap', from: '2004, 2007', arithmetic: '2007 - 2004', result: '3 years', gap: 3 },
  { name: 'iPhone → ChatGPT gap', from: '2007, 2022', arithmetic: '2022 - 2007', result: '15 years', gap: 15 },
  { name: 'Total timeline span', from: '1969, 2022', arithmetic: '2022 - 1969', result: '53 years', gap: 53 }
];

Session.rawData = rawData;
Session.derivedData = derivedData;
Session.claim = claim;
Session.reader = reader;
Session.place = place;

table(['Year', 'Event'], rawData.map(d => [d.year, d.event]));
table(['Derived figure', 'From', 'Arithmetic', 'Result'], derivedData.map(d => [d.name, d.from, d.arithmetic, d.result]));
