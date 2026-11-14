### The document is a Form 10-K: what a professional reads it for

**A 10-K is the company's annual report to the SEC, and its readers know how to read one**: investors, analysts,
lawyers, a general counsel. They do not need the form explained; they need what this year's filing says, quickly and
exactly, with the page it says it on. SEC rules fix its order of topics, so every 10-K has the same numbered Items
(SEC staff, *How to Read a 10-K/10-Q*, 2021). The ten questions below are what that guide tells an investor each Item is
for, in our words, each with the Item it is answered under. **The brief's own questions come first**; these fill the
film, and all ten are answered in `process.pdf`.

Find each question's pages with `Documents.find` on its Item heading ("Item 7.", "Item 9A.") or a statement's title, keep
the pages whose `context` begins with it — not the contents page or a cross-reference — and ask the question of those
pages and the next, with the Stage 1 `SHAPE` and the figure check after it (*A long document* above). One read a
question, each cached, never one read of the whole filing.

```javascript
// The 10-K's ten, each with where its answer is printed. Source: SEC staff, How to Read a 10-K/10-Q (2021), Items 1-15;
// Item 1C is the filing's own (added after that guide was written).
const TENK_QUESTIONS = [
    { q: 'What does the company do, and how is its revenue split by segment, product line and geography?',
      at: ['Item 1.', 'Segment Information'] },                                        // Item 1; Item 8's segment note
    { q: 'How did revenue, gross margin, operating income and net income change from the prior year, and what does '
        + 'management say drove each change?',
      at: ['Consolidated Statements of Income', 'Consolidated Statements of Operations', 'Results of Operations'] }, // Item 8; Item 7
    { q: 'How much cash did operations generate, and where did it go: capital expenditure, share repurchases, dividends, '
        + 'acquisitions?',
      at: ['Consolidated Statements of Cash Flows', 'Item 5.', 'repurchase'] },       // Item 8; Item 5
    { q: 'What is the company\'s liquidity: cash and investments, debt, and the commitments it reports?',
      at: ['Liquidity and Capital Resources'] },                                       // Item 7
    { q: 'How concentrated is the business: which customers, suppliers or regions account for a large share?',
      at: ['concentration', 'customer'] },                                             // Item 1, Item 1A, Item 8 notes
    { q: 'Which risk factors does the filing list first, and which are particular to this company?',
      at: ['Item 1A.'] },                                                              // Item 1A
    { q: 'What significant legal proceedings or contingencies does the filing report?',
      at: ['Item 3.', 'Legal Proceedings', 'Contingencies'] },                         // Item 3; Item 8 notes
    { q: 'What did the auditor conclude: its opinion on the financial statements, any critical audit matters, and its '
        + 'opinion on internal control? Does the filing report a material weakness?',
      at: ['Report of Independent Registered Public Accounting Firm', 'critical audit matter', 'Item 9A.'] }, // Item 8; Item 9A
    { q: 'Which accounting estimates does management call critical, and did any change from the prior year?',
      at: ['Critical Accounting Estimates', 'Critical Accounting Policies'] },         // Item 7
    { q: 'Do Items 1B, 1C and 9 report anything: comments from the SEC staff left unresolved, how cybersecurity risk is '
        + 'governed and any material incident, or a change of accountants or disagreement with them?',
      at: ['Item 1B.', 'Item 1C.', 'Item 9.'] },                                       // Items 1B, 1C, 9
];
```

- **They choose the scenes.** The brief's questions take the first scenes and the best-answered of these fill the rest,
  in the order above, which is the order a professional reads a 10-K in: the business, the year's results, the cash, the
  balance sheet, then what could go wrong. Say which you chose and why in Q1.
- **A professional wants the year at a glance first.** After the title, one scorecard scene: revenue, operating income,
  net income, cash from operations, cash and investments — **this year beside the prior year**, each found on its page,
  each change computed in the script. Then the scenes. A 10-K film of 60 seconds holds the scorecard and four or five
  scenes; say so if the brief asks for less.
- **The prior year is the comparison, and the filing prints it.** A 10-K's statements show the prior years beside the
  current one: every change on screen is computed from two found figures, its working in `direction.md`. Never an
  analyst's estimate, a consensus or a share price: none of them is in the filing.
- **A peer is compared only when the brief asks, and only filing to filing.** The peers are the ones the brief names or
  the 10-K's competition section does; each peer's figure comes from that peer's own 10-K or 10-Q for the nearest period,
  found through *Outside the filing* above, with its own source line. Fiscal years differ: name each company's year,
  and put two figures on one axis only when their periods match.
- **"None" is an answer.** Items 1B and 9 usually say *none* or *not applicable*, and an unqualified opinion with no
  material weakness is the usual finding. For this audience that is worth one line on screen (*"Auditor: no material
  weakness; two critical audit matters"*), not a scene of its own. **Anything else is a scene**: a qualified opinion, a
  disclaimer, a material weakness, a going-concern paragraph, a change of auditor with a disagreement. The SEC's guide
  says each deserves a careful look; the film shows it in the filing's own words and with its page, and adds no
  adjective.
- **Name the auditor's findings as the report does.** The opinion says the statements *"present fairly, in all material
  respects"*; a critical audit matter is named by its heading in the auditor's report. Quote the heading, never a
  summary of your own, and give the page.
- **Risk factors are the company's ranking.** The SEC's guide notes they are generally listed in order of importance, so
  *"the filing lists first…"* is a fact about the filing; which is *most serious* is not yours to say. Prefer the risks
  particular to this company to those any company carries.
- **A non-GAAP figure never appears without its GAAP counterpart**, at least as prominent, as the filing itself must show
  it (SEC staff guide; Regulation G, L&W *US IPO Guide* p. 58). The source line names the filing's own term for it.
- **Part III is usually somewhere else.** Directors, executive pay, ownership, related-party transactions and the
  auditor's fees (Items 10–14) are mostly *incorporated by reference* to the proxy statement, filed a month or two later.
  A question that lands there is answered with where the answer is — *"Executive pay: in the 2026 proxy statement,
  incorporated by reference (Item 11)"* — never with a figure from memory.
- **Segments from the note, not the narrative.** Where MD&A and the audited segment note give the same figure, use the
  note; where they differ, show neither until Q1 settles it.
- **They go in `process.pdf`**, as *A professional's read of the 10-K*: the ten questions, each with its Item, its pages
  and the filing's answer, or *not in this filing* / *incorporated by reference to …*.
