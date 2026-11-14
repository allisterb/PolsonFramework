### The document is a Form S-1: the questions an investor brings to it

**An S-1 is read by people deciding whether to buy shares in a company that has never reported before**, and they come
to it with questions of their own. Its prospectus is the only place most of the answers exist (SEC staff, *Investor
Bulletin: Investing in an IPO*, 2022). The ten below are the questions that bulletin tells an investor to take to a
prospectus, in our words, each with the heading its answer is printed under. **The brief's own questions come first**;
these fill the film to its five scenes, and all ten are answered in `process.pdf`.

A prospectus is laid out by rule, so the headings are the same in every S-1: **find each question's pages with
`Documents.find` on its heading, then ask that question of those pages** (*A long document* above), with the Stage 1
`SHAPE` and the figure check after it. Ten small reads, each cached, never one read of the whole filing.

```javascript
// The S-1's ten, each with the headings its answer sits under. Sources: SEC staff, Investor Bulletin: Investing in an
// IPO (2022), "Highlighted below…" and "What else should I consider?"; Latham & Watkins, US IPO Guide (2026), pages as cited.
const S1_QUESTIONS = [
    { q: 'What does the company do, and how does it make money: its products or services, and who pays for them?',
      at: ['Prospectus Summary', 'Business'] },                                   // bulletin: Prospectus Summary, Business
    { q: 'How have revenue, operating income or loss, and net income or loss changed across the periods the financial '
        + 'statements cover, and what is the largest line between operating and net income or loss, as the statement titles it?',
      at: ['Consolidated Statements of Operations', 'Summary Consolidated Financial'] },   // L&W p. 28: the summary page
    { q: 'Which key metrics and non-GAAP measures does management use, how does the filing define each, and what is the '
        + 'most directly comparable GAAP figure beside it?',
      at: ['Key Metrics', 'Non-GAAP'] },                                          // L&W p. 6 (MD&A's KPIs), p. 58 (Reg G)
    { q: 'On which customers, suppliers or partners does the business depend, and what share of revenue comes from the largest?',
      at: ['concentration', 'significant customers'] },                          // bulletin: Business
    { q: 'How much does the offering raise, and what does the company say it will do with the money?',
      at: ['Use of Proceeds'] },                                                  // bulletin: Use of Proceeds
    { q: 'How much dilution do investors in the offering take on: the offering price against net tangible book value per '
        + 'share, and against what existing holders paid?',
      at: ['Dilution'] },                                                         // bulletin: Dilution
    { q: 'Who controls the company after the offering: the classes of stock and their votes, and the largest holders?',
      at: ['Description of Capital Stock', 'Principal and Selling Stockholders', 'controlled company'] }, // bulletin; L&W p. 66
    { q: 'Are existing holders selling shares in the offering, and when can insiders sell the rest: the lock-up, and the '
        + 'shares eligible for future sale?',
      at: ['Selling Stockholders', 'Shares Eligible for Future Sale', 'Lock-Up'] }, // bulletin: selling shareholders, overhang
    { q: 'Which risks does the filing name that are particular to this company, rather than to any company?',
      at: ['Risk Factors'] },                                                     // bulletin: Risk Factors; L&W p. 6
    { q: 'Does the filing report a material weakness in internal control over financial reporting, and is the company an '
        + 'emerging growth company with lighter disclosure?',
      at: ['material weakness', 'emerging growth company'] },                    // L&W p. 31; bulletin: EGCs
];
```

For each: `Documents.find` every heading, keep the pages whose `context` begins with it (the section itself, not the
contents page or a cross-reference), and ask the question of those pages and the next —
`Documents.ask(REPORT, q, { pages, schema: SHAPE })`, at the top of `motion.js` with the reading. A heading
found nowhere, with no `unsearched` pages left, means the filing has no such section: the answer is
*not in this filing*.

- **They choose the scenes.** The brief's questions take the first scenes; the best-answered of these fill the rest, up
  to five, in the order above, which is the order an investor reads a prospectus in. With no audience in the brief, say
  which you chose and why in Q1.
- **An unanswered question is a gap, and the film never fills it.** Nothing goes on screen the filing does not say.
  *"The filing does not break out revenue by customer"* is a scene's honest answer; a plausible figure is the failure.
- **Blank is an answer.** A first S-1 is filed before pricing, so the price, the share count and everything computed
  from them (proceeds, dilution per share) print as blanks, filled only in the final prospectus (Rule 430A; L&W p. 35).
  Say *not yet set*, in those words. Never estimate a price range the filing does not print.
- **A non-GAAP figure never appears without its GAAP counterpart.** A company that discloses one must present the most
  directly comparable GAAP measure with equal or greater prominence, and reconcile the two (Regulation G and S-K Item
  10(e); L&W p. 58). The film holds itself to the same rule: *"adjusted EBITDA $X; net loss $Y"*, the GAAP figure no
  smaller, and the filing's own name and definition for the metric in the source line.
- **A disclosure is stated as the filing states it, with no adjective.** A material weakness (disclosed even when since
  remedied; L&W p. 31), a controlled company (more than half the votes for directors held by one person, group or
  company; L&W p. 66), dual-class stock, revenue concentrated in a few customers: each is *"the filing reports…"*,
  never "a red flag", "risky" or "a concern". The viewer decides what it means.
- **Risk factors read as worse than the business, by design.** The SEC does not let a company soften a risk factor with
  mitigating language, so the section is written to sound negative (L&W p. 6). Choose risks particular to this company
  (a named dependency, a lawsuit, a structure), report each in the filing's words, and never rank them by your own
  sense of how serious they are.
- **A company's losses before its IPO are often mostly not spending.** Convertible notes, SAFEs and warrants are
  revalued each period until they convert, and a fast-rising valuation shows up as a large loss below the operating
  line. That is why *A net loss is not an operating loss* above matters most in an S-1: ask for both lines and the one
  between them, and when the brief's question is about losses, the scene shows the operating loss beside the net loss.
- **An S-1 has no history outside itself, so two questions an investor brings are outside it by nature**, and
  *Outside the filing* above is how they are answered, when the brief asks them:
  - *How does it compare with its competitors?* The competitors are the ones the S-1's **Competition** section names,
    found with `Documents.find(REPORT, 'Competition')`, never ones you pick. Each is compared on what its own filing
    reports for the same period: a listed one's 10-Q or 10-K, a segment named as a segment. One that files nothing is
    said to file nothing; its revenue in the press is not a reported figure.
  - *What has happened since it was filed?* An S-1 is filed before pricing, and an amended S-1/A sets the price range
    and the share count, then the final prospectus the price. Look on EDGAR for filings after this one's date. A range
    from an S-1/A is the filing's figure, from a later filing, and its source line names that filing and its date.
- **The offering price is not a value.** It is negotiated between the company and its underwriters, and the bulletin
  warns it may bear little relationship to where the shares trade. Never present it, or anything computed from it, as
  what the company is worth.
- **They go in `process.pdf`**, as *An investor's read of the S-1*: the ten questions, each with its pages and the
  filing's answer, or *not in this filing*.
