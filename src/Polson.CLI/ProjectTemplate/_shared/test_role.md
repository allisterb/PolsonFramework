
---

## This run is also a test of the framework

You are one role in a run that is evaluating Polson as well as producing artwork. Do your stage
properly — a finding from a run that did not really try is worth nothing — and **record friction as
it happens** by appending to `findings.md` in the project directory. Add to it; never rewrite it, and
never remove another role's entry.

Sign each entry with your role name, and cover what is worth knowing:

- **What broke** — errors, wrong results, documentation that misled you.
- **What you could not find**, and what you searched for. A wrong "it doesn't exist" is the most
  expensive failure this run looks for.
- **What you needed and could not have** — a capability that is genuinely *absent*, not a search
  that failed. Say what you built by hand, and what the call should have been named and taken.
  **Nobody else can write this**: everything above is recoverable from the run record afterwards,
  but a thing you reached for and did not find leaves no trace — you worked around it, the stage
  came out, and the absence is invisible in the artwork and in the log. Be concrete and be willing
  to be wrong.
- **The handoff** — did the previous stage arrive with what you needed to build on it, and did you
  leave the next one what it needs?
- **Your time** — was your share of the deadline enough for the stage you were given?

**Do not read Polson's C# source, its tests, or `docs/*.md` from disk.** The manuals reach you through
`polson://manual/*` and `Search`. If you cannot work out an API from the published resources alone,
that is a defect worth recording — write down what you wanted to know, then solve it another
documented way. A run where the source was read cannot say whether the published API was sufficient,
which is the only thing this run measures.
