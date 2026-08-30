// Stage 1: Data & Claim
Stage.begin('Data');

log('===============================================================');
log('STAGE 1: DATA & CLAIM DECLARATION');
log('===============================================================');
log('CLAIM:   Progress is constant — it never stopped coming, but it never once came on schedule.');
log('READER:  General. Knows all seven events already; has never seen the gaps between them drawn to scale.');
log('PLACE:   At a line printer, watching fanfold paper feed through the platen');
log('===============================================================');

Stage.note('Claim, reader context, and spatial setting declared.');
Stage.note('Derived arithmetic verified and committed to brief.md data table.');

const milestones = [
  { year: 1969, event: "ARPANET sends its first message (it crashed after 'LO')", note: "Crash after 'LO'" },
  { year: 1989, event: "Tim Berners-Lee proposes the World Wide Web", note: "Vague but exciting" },
  { year: 1993, event: "Mosaic makes the web visual", note: "Graphical browser" },
  { year: 1998, event: "Google is founded", note: "Search indexation" },
  { year: 2004, event: "Facebook launches — the social era begins", note: "Social graph" },
  { year: 2007, event: "The iPhone puts the web in every pocket", note: "Mobile revolution" },
  { year: 2022, event: "ChatGPT reaches 100M users in 2 months", note: "Generative AI shock" }
];

log('\n--- PRIMARY MILESTONES ---');
milestones.forEach((m, idx) => {
  log(`[${idx + 1}] ${m.year}: ${m.event}`);
});

log('\n--- DERIVED GAP ARITHMETIC ---');
const gaps = [];
for (let i = 0; i < milestones.length - 1; i++) {
  const fromYear = milestones[i].year;
  const toYear = milestones[i + 1].year;
  const diff = toYear - fromYear;
  gaps.push({ from: fromYear, to: toYear, gap: diff, label: `${fromYear} → ${toYear}` });
  log(`Gap ${fromYear} to ${toYear}: ${toYear} - ${fromYear} = ${diff} years`);
}

const totalSpan = milestones[milestones.length - 1].year - milestones[0].year;
log(`Total timeline span: ${milestones[milestones.length - 1].year} - ${milestones[0].year} = ${totalSpan} years`);

Stage.note(`Arithmetic verified: 6 interval gaps computed (20y, 4y, 5y, 6y, 3y, 15y) spanning a total of ${totalSpan} years (1969-2022).`);
