Stage.begin('Forms');
Stage.note('Stage 2: Form selection based on Manual 13 §1. Selecting at least 3 distinct truthful forms.');

const forms = [
  {
    question: 'Over time? (How did the 7 milestones unfold across 53 years?)',
    form: 'Proportional Vertical Timeline / Fanfold Platen Track with Milestone Badges',
    rationale: 'Encodes real time intervals vertically on a true linear year scale along a continuous perforated printer ribbon. Answers the core brief requirement of seeing gaps drawn to scale.',
    rejected: 'Equal-spaced list cards (hides true gap variations), horizontal timeline (poor fit for 9:16 portrait canvas).'
  },
  {
    question: 'Compared to what? (How do the waiting gaps between revolutions rank?)',
    form: 'Zero-Baseline Horizontal Comparison Blocks (Gap Comparison Bars)',
    rationale: 'Neo-brutalist solid blocks with 4px borders comparing the 6 intervals (20yr, 15yr, 6yr, 5yr, 4yr, 3yr) with zero baseline.',
    rejected: 'Donut / Pie (gaps are individual durations, not fixed percentages of a pie), Radar chart (Manual 13 anti-pattern).'
  },
  {
    question: 'How big? (What is the total duration and speed of recent adoption?)',
    form: 'Big-Number Hero Callout Badges ("53 YEARS", "100M IN 2 MO")',
    rationale: 'High-contrast neo-brutalist offset-shadow callouts for the overarching magnitude and extreme contrast in adoption speeds.',
    rejected: 'Gauge dials (anti-pattern), 3D shapes (anti-pattern).'
  },
  {
    question: 'What share? (How was the 53-year timeline distributed across eras?)',
    form: 'Segmented Era Bar / Strip with Heavy Dividers',
    rationale: 'Proportional 1D segmented bar breaking down the 53 years into foundational eras (Arpanet era: 20y / 37.7%, Web 1.0: 15y / 28.3%, Social/Mobile/AI: 18y / 34.0%).',
    rejected: 'Proportional area circles (circles contradict neo-brutalist sharp geometry and read poorly).'
  }
];

Session.forms = forms;

for (let i = 0; i < forms.length; i++) {
  Stage.note('Q: ' + forms[i].question + ' -> FORM: ' + forms[i].form + ' (Rejected: ' + forms[i].rejected + ')');
  log('• ' + forms[i].question + '\n  Selected: ' + forms[i].form + '\n  Rationale: ' + forms[i].rationale + '\n  Rejected: ' + forms[i].rejected + '\n');
}
