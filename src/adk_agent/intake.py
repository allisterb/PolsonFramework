"""Taking a commission from a visitor: a brief, and the document it refers to.

**Why this is not the ADK console.** The console can attach a file — it has `onFileSelect` and sends
it as an `inlineData` part — but an attached file becomes a *part in the conversation*, never a file
on disk. `Documents.ask(path, question)` reads the project directory, so it cannot see one; the agent
would hold the PDF and be unable to hand it to the toolkit, with no provenance, no scan for text
addressed to whoever is processing it, and no budget accounting. A document has to land in
`documents/` to be usable, and that is what this does.

**Why it lives here rather than in `src/webapp`.** `main.py` returns an ordinary `FastAPI` precisely
so a UI can share its app and its port, and this is the first thing mounted on that seam. It is
deliberately the *narrow* half: a form, an upload, and a redirect into the console. The studio's
observe page — the tailer, the broker, the sense-making curve — is a separate mount that reuses
`src/webapp` as it stands, and it needs a transcript writer on the ADK side before it has anything
to show.

**Everything here treats the visitor as a stranger**, because on a hosted URL that is what they are
(Milestone 6 §3). The brief is passed to the CLI, which sanitises it into `brief.md` as data; the
upload is checked for size, for a type the studio can actually declare, and for a filename that
cannot escape the directory it is written to.
"""

from __future__ import annotations

import asyncio
import importlib
import os
import time
import logging
import re
import shutil
import sys
import tempfile
from collections import deque
from html import escape
from pathlib import Path
from xml.sax.saxutils import quoteattr

import httpx
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import HTMLResponse, RedirectResponse

import api_gate
import keepalive
from newproject import DOCUMENT_SUFFIXES, PROJECTS_DIR, VALID_APP_NAME, GenerateError, create

_logger = logging.getLogger("polson.intake")

#: Largest upload accepted, in bytes.
#:
#: Below `Documents.MaxInlineBytes` (18 MB) on purpose: a document larger than the reader will accept
#: is one a visitor waits to upload and then cannot use, and refusing at the door is the kinder of the
#: two failures. Enforced by counting bytes as they arrive rather than by trusting `content-length`,
#: which is a header a client writes.
MAX_UPLOAD_BYTES = 16 * 1024 * 1024

#: Read size for the streaming copy. Small enough that an oversized upload is abandoned early.
CHUNK = 64 * 1024

#: A length bound, and nothing else. **What a name may *contain* is `VALID_APP_NAME`'s business** —
#: it is an ADK constraint, so it belongs with the code that writes the app.
#:
#: This module used to keep its own copy, described as mirroring that rule. It did not: the copy
#: allowed a dash, ADK does not, and ADK refuses at **run** time rather than at load time — so the
#: form accepted `boxoffice-2`, staged the visitor's uploaded PDF into it, served it in
#: `/list-apps`, opened it in the console, and answered the first message with a 404. Importing the
#: rule rather than restating it is what stops the two drifting again.
MAX_NAME = 48

#: Whose sessions these are. One director per host; ADK scopes sessions by user id.
#:
#: **`user` rather than `director`, because that is what the dev UI looks under.** ADK's own console
#: requests `/apps/<app>/users/user/sessions` with no way to change it from the page, so a session
#: created under any other id is invisible there — the app appears with an empty session list while
#: the run is going, which reads as a run that never started. Measured on kubrick8: the run was five
#: turns in and the console showed nothing.
#:
#: The descriptive name cost more than it bought. Nothing keys off the value — `launch` writes it and
#: reads it back, and that is all — so the studio page, the record and the transcript are unaffected.
USER = "user"

#: Workflows offered on the form, each with the types it offers. Not the full set: these are the
#: ones whose instructions tell an agent to look for documents, and offering a workflow that ignores
#: an upload would be worse than not offering it.
#:
#: **A type is a `type.<name>.md` file in the workflow's template**, so this table mirrors a set that
#: lives somewhere else — and hardcoding it is deliberate, because no visitor string should ever
#: select a template by name. A mirror drifts silently, though: a type added to the template simply
#: never appears here and nobody finds out. `WorkflowCatalogueTests` compares the two and names the
#: difference. It cannot be read from disk at runtime — the templates are *embedded resources* in
#: `Polson.CLI.dll`, so a container has the DLL and no `ProjectTemplate/` tree at all.
WORKFLOWS: dict[str, tuple[str, ...]] = {
    "vector_infographic": ("blueprint", "brutalist", "editorial", "specimen", "swiss"),
    "infographic": ("blueprint", "brutalist", "editorial", "specimen", "swiss"),
    # **The collaborative one, and the only one here that stops and asks.** Its `type.review.md`
    # specifies the interaction `ask_director` implements — two to four options, answerable with one
    # click, always room to say something unanticipated — so this is the workflow that demonstrates
    # the studio as a partner rather than as a generator.
    "drawing": ("review",),
    # **The Qloo entry's two, without Qloo since 2026-11-14.** Evidence is the brief, the director's documents and
    # research on the subject; audiences are language and locale versions. As built for the hackathon, grounded in each
    # audience's measured taste, they are `poster_qloo` and `motion_qloo` below. A poster reads `documents/` from stage 1
    # (a brand guide, an earlier poster); a motion film builds on a poster found there. `film` is the CLI's default for a
    # poster; `product` makes either one a product ad, the dynamic-creative case (`docs/internal/product-ad-probe.md`).
    # Their deadlines are the craft defaults (55 and 70, since the brain dump): no entry in `DEADLINES` below, since the keepalive's
    # ceiling (218) holds both, where the old 38 held neither — and a set of six, at +20 or +25 per audience.
    "poster": ("film", "product"),
    # `product` is a product ad; `report` explains a long document the director attached (a 10-K) — the Nebius entry,
    # read by Nemotron through `Documents.ask` and needing no Qloo. **No type is a film trailer**, so it is in
    # `UNTYPED_IS_A_CHOICE` below.
    # `pitch` tells a startup's case from its own deck (and model) to investors, every projection marked as one; with
    # places, a film per investor audience in its conventions. `report.10k` and `report.s1` are reports on a Form 10-K and
    # a Form S-1, with the questions their readers take to them asked as well as the director's.
    "motion": ("pitch", "product", "report", "report.10k", "report.s1"),
    # **The Qloo twins**: the two above as the hackathon entry built them, each audience's design from its Qloo taste
    # profile through the `Taste` tool. Offered only while Qloo is on (`taste_available`): its API is contract-only.
    "poster_qloo": ("film", "product"),
    "motion_qloo": ("pitch", "product", "report", "report.10k", "report.s1"),
    # **The Nebius entry's.** A theorem or concept explained in the model's own argument, every formula and claim checked
    # before it is drawn (`Equation`, `Verify.derivation`, `Verify.claim`, whose code Nemotron writes) and the film read
    # back after. Untyped. Documents are optional material (lecture notes, a textbook page), never the subject.
    "math_explainer": (),
    # **Engine-only image workflows, offered from 2026-10-29** once each template read `documents/` (a brand guide or
    # a mark to evolve; a synopsis or the series' covers). Their deadlines are the CLI's craft defaults, 15 and 25.
    # A cover requisitions its parts (`Assets`), within the host's `POLSON_ASSETS_BUDGET`.
    "logo": ("antique", "geometric", "modern"),
    "cover": (),
}

#: Workflows whose lone type is **not** what an empty type means. The form selects a workflow's only type for
#: the visitor, on the reasoning that one type is not a choice — true when the CLI would default to it anyway
#: (`poster` to `film`), false for `motion`, where no type is the film trailer and `product` is something else.
UNTYPED_IS_A_CHOICE: frozenset[str] = frozenset({"motion", "motion_qloo"})

#: The suffix of a workflow grounded in Qloo's taste data, as the CLI's `ProjectGenerator.QlooSuffix`.
QLOO_SUFFIX = "_qloo"


def base_workflow(workflow: str) -> str:
    """The workflow a `_qloo` twin is built on (`poster_qloo` → `poster`), whose deadline and budgets it takes; any
    other, itself. As the CLI's `ProjectGenerator.BaseWorkflow`."""
    return workflow[:-len(QLOO_SUFFIX)] if workflow.endswith(QLOO_SUFFIX) else workflow


def taste_available() -> bool:
    """Whether the engine's `Taste` tool is on, read from the CLI's settings by the studio's own rule
    (`orchestrator.credentials.qloo_enabled`), so this form, the studio's composer and the engine agree."""
    shared = Path(__file__).resolve().parent.parent          # `src/`, or `/app` in the image
    if (shared / "orchestrator").is_dir() and str(shared) not in sys.path:
        sys.path.insert(0, str(shared))
    from orchestrator.credentials import qloo_enabled
    return qloo_enabled()


def needs_taste(workflow: str) -> bool:
    """Whether this workflow cannot run without Qloo: a `_qloo` twin. Audiences elsewhere are language versions."""
    return workflow.endswith(QLOO_SUFFIX)

#: Workflows by a former name, as the CLI's `RenamedWorkflows` has them: `trailer` became `motion` on 2026-10-25, since
#: its types make product ads, report explainers and pitches as well as film trailers.
RENAMED: dict[str, str] = {"trailer": "motion"}


def canonical(workflow: str) -> str:
    """The workflow's current name: a former one read as what it became, anything else as given."""
    return RENAMED.get(workflow, workflow)

#: What a workflow is **called** on the form, where its template name is not the useful name.
#:
#: A label rather than a rename: `drawing` appears as a workflow name in `ProjectGenerator`, in
#: `studio/projects.py` and in both test suites, and renaming it there would buy nothing this map
#: does not. The name is doing communication work — *this one needs you at the keyboard* — and that
#: work belongs on the form rather than in the template tree.
LABELS: dict[str, str] = {
    "drawing": "drawing_partner",
}

#: The host's request ceiling, in seconds. Cloud Run's `--timeout`, whose own maximum for a service
#: is 3600 — so this is not a number that can simply be raised when a longer run is wanted.
#:
#: **Its own variable, `POLSON_HOST_TIMEOUT_SECONDS`.** It read `POLSON_REQUEST_TIMEOUT_SECONDS`, which `studio` reads
#: as one model call's timeout (180 s): raising it to let a local run past 38 minutes (anthropic_s_1_narrated, 7800)
#: also let one hung Gemini call wait 130 minutes rather than be retried at three, and the run lost 13 minutes to one.
try:
    REQUEST_TIMEOUT_SECONDS = max(60.0, float(os.environ.get("POLSON_HOST_TIMEOUT_SECONDS", "3600")))
except ValueError:
    REQUEST_TIMEOUT_SECONDS = 3600.0

#: The shortest and longest commission this form will accept, in minutes.
#:
#: **The maximum is not the request timeout, and the gap is the whole subtlety.** Three things share
#: that hour: the deadline, the breaker's grace — the extra time an agent gets to write up after its
#: allowance runs out — and up to `studio.MAX_CREDIT_SECONDS` of director-waiting handed back. Only
#: what is left over may be offered, because past the ceiling the platform kills the run mid-flight
#: rather than the breaker halting it cleanly.
#:
#: **Derived rather than typed**, so that changing the grace or the credit moves this with them. A
#: hardcoded 40 would be right today and quietly wrong the first time either is tuned — which is the
#: same class of mistake as the workflow-deadline table this sits next to.
MIN_DEADLINE_MINUTES: int = 5

#: Slack left over after everything above is accounted for.
#:
#: **Without it the arithmetic lands exactly on the ceiling**, which is the same fault as `drawing`'s
#: 45-minute default: 40 + 15 grace + 5 credited comes to precisely 3600s, so the breaker's last
#: model call and the platform's cutoff are the same instant. The run still has to encode a final
#: render, write its deliverables and let the mirror take its closing sweep after that — none of
#: which is instant, and all of which is what a director actually keeps.
DEADLINE_MARGIN_SECONDS: float = 120.0


def _after_deadline_seconds() -> float:
    """What a run may still need once its deadline passes: credited waiting, the breaker's grace, the margin.

    **Read off `studio` rather than restated, and tolerantly.** Two modules in this tree are called
    `studio` — this package's agent factory and `src/studio`, the web layer — and which one a bare
    import finds depends on `sys.path` order. `main.py` guarantees the right one in the container,
    but a plain `from studio import ...` here raised `ImportError` under the other order and took
    **the whole intake form** down at import time. A form that cannot be built is a far worse outcome
    than one working from conservative numbers, so a wrong resolution degrades instead of crashing.

    The fallbacks match `studio`'s own defaults, and the log line says when they were used — a silent
    fallback would be the drift this is deriving the number to avoid.
    """
    grace, credit = 15.0, 300.0
    try:
        import studio                                            # noqa: PLC0415 - deliberately late

        grace = float(getattr(studio, "BREAKER_GRACE_MINUTES", grace))
        credit = float(getattr(studio, "MAX_CREDIT_SECONDS", credit))
    except Exception as exc:                                     # noqa: BLE001 - never fatal
        _logger.warning("polson intake: using default breaker figures for the deadline cap (%s)", exc)
    return credit + grace * 60 + DEADLINE_MARGIN_SECONDS


#: How long a run is held awake after its deadline (`keepalive.track`), and what the ceiling below leaves for it.
AFTER_DEADLINE_SECONDS: float = _after_deadline_seconds()

#: What ends a commission on this host. **With the keepalive on, it is the keepalive's own cap**: a commission is a
#: background task, held awake for its deadline and `AFTER_DEADLINE_SECONDS` but never past
#: `keepalive.MAX_HOLD_MINUTES`, and no single request's timeout reaches it. **With it off**, the request timeout, on
#: the old assumption that a run lives inside one request. That assumption is what made the ceiling 38 minutes,
#: and it was never true of a commission, only of a run typed into ADK's own console.
RUN_LIMIT_SECONDS: float = (keepalive.MAX_HOLD_MINUTES * 60.0 if keepalive.SELF_URL else REQUEST_TIMEOUT_SECONDS)


def _max_deadline(limit: float | None = None) -> int:
    """The longest commission that still leaves room to be stopped in an orderly way within `limit`."""
    limit = RUN_LIMIT_SECONDS if limit is None else limit
    return max(MIN_DEADLINE_MINUTES, int((limit - AFTER_DEADLINE_SECONDS) // 60))


MAX_DEADLINE_MINUTES: int = _max_deadline()

#: Minutes this form gives a commission, per workflow. `None` — an absent entry — takes the CLI's own
#: default for that workflow.
#:
#: **Deliberately not a mirror of `ProjectGenerator.WorkflowDeadlines`.** Those answer *how long does
#: this kind of work take*, which is a property of the craft; this answers *what fits on this host*,
#: which is a property of the deployment — the same split as `POLSON_BUDGET_TOKENS`. Where they
#: disagree the smaller one wins, and that is the point rather than a conflict.
#:
#: **The host's number is Cloud Run's 3600s request timeout**, and three things have to fit inside
#: it: the deadline, the breaker's 15-minute grace, and up to `studio.MAX_CREDIT_SECONDS` of director
#: waiting given back. Past that the platform kills the run mid-flight instead of the breaker halting
#: it cleanly — the exact failure the breaker exists to replace. `drawing` is why this exists at all:
#: its craft default is 45, which with the grace *is* the ceiling exactly, before any waiting.
DEADLINES: dict[str, int] = {
    "vector_infographic": 30,
    "infographic": 30,
    # 35 + 15 grace + 5 of credited waiting = 55, leaving five minutes of margin. Shorter than the
    # craft default because a partner run is bounded by the director's attention anyway: this is a
    # workflow with no fixed endpoint, so something has to end it.
    "drawing": 35,
    # The craft defaults. They need the keepalive: without it the ceiling is 38 and `check` clamps them to it. Ten minutes
    # more each since 2026-10-24, for the brain dump before the concepts (Manual 29 §2c); the CLI's agree.
    "poster": 55,
    "motion": 70,
    # A type's own entry, `workflow.type`, wins over its workflow's: a pitch is two minutes of narrated film to a
    # trailer's fifteen seconds. The CLI's craft default agrees (`ProjectGenerator.WorkflowDeadlines`).
    "motion.pitch": 90,
    "math_explainer": 60,
    # The CLI's craft defaults (`ProjectGenerator.WorkflowDeadlines`): a mark and its sheet, one composed cover. Logo 20, not
    # 15: emberroast1 (2026-10-29) finished at 13 of 15 with a thin brand sheet and no findings.md.
    "logo": 20,
    "cover": 25,
}

#: Minutes added to a commission's deadline for each audience past the ones its allowance was sized for
#: (`AUDIENCES_IN_DEADLINE`: the second for a poster or a film, `Audience C:` onward).
#:
#: **A set is more work in two places, and only one of them scales.** Each audience is its own profile and its
#: own piece — a poster or a trailer — so that half is linear. The comparison is pairwise, but local
#: (`Taste.comparePaceAll`, no Qloo calls), so it costs minutes at most however many pairs there are. Only the
#: default is scaled: a deadline the director typed is theirs, and the ceiling still clamps either.
PER_AUDIENCE_MINUTES: dict[str, int] = {
    "poster": 20,
    "motion": 25,
    # An infographic for another audience is a variant of the first (same figures and layout; palette, numerals and
    # language move, `_shared/audience_design.md`), so less than a poster's fresh design.
    "vector_infographic": 10,
    "infographic": 10,
}

#: How many audiences a workflow's `DEADLINES` allowance already covers; two where absent. A poster's 55 and a film's 70
#: were set for the pair those workflows were built around. An infographic's 30 was set for one piece, before it took
#: audiences, so its second audience adds time too: all_in_2 (2026-10-30, two audiences) went out on a single piece's 30.
AUDIENCES_IN_DEADLINE: dict[str, int] = {"vector_infographic": 1, "infographic": 1}

#: How a brief names each audience: `Audience A:` to `Audience F:`. The composer writes one per line; the starters
#: and a hand-typed brief run them inline, so this does not anchor on a line start.
AUDIENCE_LINE = re.compile(r"\bAudience ([A-Z])\s*:")


def audiences_in(brief: str) -> int:
    """How many distinct `Audience X:` lines the brief carries; 0 when it names none that way."""
    return len(set(AUDIENCE_LINE.findall(brief)))


def _base_token_cap() -> int | None:
    """The deployment's per-run token cap, read off `studio` as tolerantly as `_after_deadline_seconds` reads it."""
    try:
        import studio                                            # noqa: PLC0415 - deliberately late

        return studio.resolve_token_cap(None)
    except Exception as exc:                                     # noqa: BLE001 - never fatal
        _logger.warning("polson intake: cannot read the token cap, so a set is not given more (%s)", exc)
        return None


#: Workflows whose runs need more than the host's cap, as a multiple of it, so they follow the host's when it changes.
#:
#: `math_explainer` at twice: every stage checks before it draws, and each check is a turn that resends the whole
#: context (55k tokens at the start, 172k by the end). ftcexplainer1 (3.7 Flash, 2026-10-23) had spent 1.8M of 2M by
#: Deliver and had not written its findings; ftcexplainer3 (3.5 Flash) hit 2M in its build stage after 57 turns.
#: `motion.pitch` at twice, keyed like `DEADLINES`: about nine scenes and their narration against a trailer's handful, and
#: no measured run behind it yet.
#: `motion.report` at twice too, its kinds (`report.s1`, `report.10k`) with it: reading a long filing is the cost.
#: anthropic_s_1 (3.8 Flash, 2026-10-28, three audiences, S-1) spent its 3M on the reading, three profiles and one design
#: board in 17 minutes — 66 calls, each resending ~180k tokens of context — and was halted before any film was encoded.
TOKEN_SCALE: dict[str, float] = {"math_explainer": 2.0, "motion.pitch": 2.0, "motion.report": 2.0}

#: A further multiple when the brief asks for a voice (`- Narration: Enabled`), keyed as `TOKEN_SCALE` is. Only where
#: the type's scale was set for a silent film: a pitch is narrated by default and its scale already counts it.
#: anthropic_s_1_narrated_2 (3.8 Flash, 2026-10-28, three audiences, S-1, narrated) had spent 4.5M of 6M at 38
#: minutes, with every line recorded and the boards drawn but no film encoded: by then each call resent ~245k tokens,
#: ~28k billable even cached. The Claude Code run of the silent brief took 284 turns.
NARRATION_SCALE: dict[str, float] = {"motion.report": 2.0}

#: How the composer writes a voice into the brief.
NARRATION_ON = re.compile(r"^- Narration: Enabled\s*$", re.MULTILINE)


def token_budget(workflow: str, brief: str, kind: str = "") -> int | None:
    """This commission's own token cap when its workflow or its audiences need more, else None for the host's.

    **Scaled, not added to.** The host's cap is sized for the two-audience pair the audience workflows were built
    for, so a set of n gets n/2 of it; a workflow in `TOKEN_SCALE` gets its multiple. None also means *leave it
    alone*: an uncapped host stays uncapped.
    """
    # A kind of a type (`report.s1`) takes its own entry, else its family's (`report`), else its workflow's.
    family = kind.split(".", 1)[0]
    workflow = base_workflow(workflow)
    keyed = lambda table: table.get(f"{workflow}.{kind}", table.get(f"{workflow}.{family}", table.get(workflow, 1.0)))
    scale = keyed(TOKEN_SCALE)
    if NARRATION_ON.search(brief):
        scale *= keyed(NARRATION_SCALE)
    n = audiences_in(brief)
    if workflow in PER_AUDIENCE_MINUTES and n > 2:
        scale *= n / 2
    if scale == 1.0 or (base := _base_token_cap()) is None:
        return None
    return int(base * scale)

#: Types a workflow ships that this form deliberately does **not** offer, and why.
#:
#: Recorded rather than simply omitted, because the guard in `test_intake` compares what is offered
#: against what each template ships — a type added upstream and never offered here is exactly the
#: silent drift that check exists to catch. An entry says "considered, declined, for this reason";
#: an absence still fails.
NOT_OFFERED: dict[tuple[str, str], str] = {
    ("drawing", "seed"): "reads the director's opening sketch from seed/, and this form stages an "
                         "upload into documents/ — a visitor choosing it would get an agent looking "
                         "at an empty directory",
}

#: Every type any offered workflow has, for building the form's one type control. Derived rather
#: than listed, so the control and the table above cannot disagree about what exists.
ALL_TYPES: tuple[str, ...] = tuple(sorted({t for types in WORKFLOWS.values() for t in types}))


#: Ready-made commissions, offered on the form as one-click starters.
#:
#: **Why they exist.** A visitor handed a URL and an empty textarea has to invent a brief before they
#: can see anything, and inventing one is the slowest part — the old standalone webapp got this right
#: by letting a director click through a workflow rather than type it. These restore that, and steer
#: the demo toward the industries it is aimed at, without narrowing what anyone may ask for: the
#: field stays free and nothing is refused.
#:
#: **Every one is a subject these two workflows can actually do.** That is the constraint that
#: matters and it is easy to get wrong — `vector_infographic` and `infographic` make *graphics that
#: encode quantities*, so a title card or a key-art comp would be a starter that reliably produces a
#: poor run. The industry is in the **subject**, not in the form.
#:
#: **None of them states a figure.** A brief carrying invented numbers is the one thing the whole
#: research and document apparatus exists to prevent — `polson://manual/13` and the `Research`
#: CAUTION both say so — and a starter that supplied them would be teaching the agent to draw a
#: sourced-looking graphic from nothing. Each asks for something the agent must research, or read out
#: of a document the director attaches.
STARTERS: tuple[dict[str, str], ...] = (
    {
        "label": "Box office, 2025",
        "name": "boxoffice2025",
        "workflow": "vector_infographic",
        "kind": "editorial",
        "brief": "The 2025 theatrical box office: how the year's ten highest-grossing films earned, "
                 "by studio and by release window. Show opening weekend against total domestic "
                 "gross, and make the difference between a wide opening and a platform release "
                 "legible at a glance.",
    },
    {
        "label": "Where a TV spot's budget goes",
        "name": "spotbudget",
        "workflow": "vector_infographic",
        "kind": "swiss",
        "brief": "Where the money goes in a national 30-second television commercial: production, "
                 "director and talent, post and grade, music licensing, and the media buy, as shares "
                 "of the total. Research typical splits and cite them. Call out the two line items "
                 "clients are most often surprised by.",
    },
    {
        "label": "How a film earns out",
        "name": "earnout",
        "workflow": "infographic",
        "kind": "blueprint",
        "brief": "How a mid-budget feature earns out over eighteen months across theatrical, "
                 "premium video on demand, streaming licence and television. One timeline, with the "
                 "break-even point marked and each window's contribution shown against it.",
    },
    {
        "label": "A launch campaign, week by week",
        "name": "campaign",
        "workflow": "vector_infographic",
        "kind": "brutalist",
        "brief": "An eight-week campaign for a streaming series launch: teaser, full trailer, "
                 "out-of-home, paid social, press junket and premiere. Show what runs when and how "
                 "the spend is weighted across channels.",
    },
    {
        "label": "Genre share, ten years apart",
        "name": "genreshare",
        "workflow": "infographic",
        "kind": "specimen",
        "brief": "Genre share of the hundred highest-grossing films, 2015 against 2025. One plate, "
                 "one cell per film, arranged so the shift between the two years reads without a "
                 "caption explaining it.",
    },
    {
        "label": "The shape of a shoot day",
        "name": "shootday",
        "workflow": "vector_infographic",
        "kind": "blueprint",
        "brief": "The shape of a commercial shoot day, call time to wrap: which departments are on "
                 "set when, where the waiting happens, and how the hours actually distribute across "
                 "camera, grip and electric, art, wardrobe and talent.",
    },
    # **Pencil and pen only.** `drawing`'s first non-negotiable rules out colour, flats and fills of
    # any hue — value comes from pressure, hatching and density — so a starter asking for a palette
    # or a graded sky would be commissioning something the workflow refuses to draw. Each of these is
    # a subject graphite and line are *for*, and each says outright that the director will steer,
    # because that is the difference between this workflow and the other two.
    {
        "label": "Storyboard: the reveal",
        "name": "storyboard",
        "workflow": "drawing",
        "kind": "review",
        "brief": "A single storyboard frame: the moment a character sees something we cannot see "
                 "yet. Their back to us, the room doing the work. Block it in and I will tell you "
                 "where to push it.",
    },
    {
        "label": "Wet street, night",
        "name": "nightstreet",
        "workflow": "drawing",
        "kind": "review",
        "brief": "A location sketch in graphite: a rain-slicked street corner at night, signage and "
                 "reflections, the kind of place a crew would block a chase through. Start with the "
                 "perspective and the darks; I will steer the mood.",
    },
    # **One film, two languages.** An original film keeps the work clear of any studio's key art; its comparables are
    # real, and research on them tells the run what kind of film it is (`type.film.md`). The audiences are language
    # versions of one design. The trailer is the same film, so a director can run the poster first and attach it.
    {
        "label": "Film poster, two languages",
        "name": "lanternkeeper",
        "workflow": "poster",
        "kind": "film",
        "brief": "An original film, The Lantern Keeper: a retired lighthouse keeper is asked to light the "
                 "lamp one last night, as a storm comes in and the town below loses power. Comparable "
                 "films: The Lighthouse (2019), The Banshees of Inisherin (2022), Aftersun (2022). "
                 "Audience A: Lisbon, in Portuguese. Audience B: Seoul, in Korean.",
    },
    {
        "label": "Film trailer, two languages",
        "name": "lanternkeepertrailer",
        "workflow": "motion",
        "kind": "",
        "brief": "A fifteen-second trailer for an original film, The Lantern Keeper: a retired lighthouse "
                 "keeper lights the lamp one last night as a storm takes the town's power. Comparable "
                 "films: The Lighthouse (2019), The Banshees of Inisherin (2022), Aftersun (2022). "
                 "Audience A: Lisbon, in Portuguese. Audience B: Seoul, in Korean. Silent. If a "
                 "poster for it is attached, carry its system over.",
    },
    {
        "label": "Product ad",
        "name": "nightshiftcoldbrew",
        "workflow": "motion",
        "kind": "product",
        "brief": "A fifteen-second ad for Nightshift, a fictitious canned cold brew coffee: slow-steeped for twenty "
                 "hours, sold chilled. Comparable brands: Blue Bottle Coffee, La Colombe, Stumptown Coffee "
                 "Roasters. Claim: \"Steeped for twenty hours.\" Call to action: \"Find it chilled.\" "
                 "Silent. No logo is supplied: set the name in type.",
    },
    # **The Qloo pair, as the hackathon entry ran it: one film, two audiences, each designed from its taste profile.**
    # Comparables are how Qloo knows what kind of film it is. Offered only while Qloo is on.
    {
        "label": "Film poster, two cities (Qloo)",
        "name": "lanternkeeperqloo",
        "workflow": "poster_qloo",
        "kind": "film",
        "brief": "An original film, The Lantern Keeper: a retired lighthouse keeper is asked to light the "
                 "lamp one last night, as a storm comes in and the town below loses power. Comparable "
                 "films: The Lighthouse (2019), The Banshees of Inisherin (2022), Aftersun (2022). "
                 "Audience A: Lisbon, aged 35 to 44. Audience B: Seoul, aged 25 to 34.",
    },
    {
        "label": "Film trailer, two cities (Qloo)",
        "name": "lanternkeepertrailerqloo",
        "workflow": "motion_qloo",
        "kind": "",
        "brief": "A fifteen-second trailer for an original film, The Lantern Keeper: a retired lighthouse "
                 "keeper lights the lamp one last night as a storm takes the town's power. Comparable "
                 "films: The Lighthouse (2019), The Banshees of Inisherin (2022), Aftersun (2022). "
                 "Audience A: Lisbon, aged 35 to 44. Audience B: Seoul, aged 25 to 34. Silent. If a "
                 "poster for it is attached, carry its system over.",
    },
    {
        # A fictitious product with real comparables: the dynamic-creative case. The comparables are the ones the
        # product-ad probe measured, so a run's evidence can be read against docs/internal/product-ad-probe.md.
        "label": "Product ad, three cities (Qloo)",
        "name": "nightshiftcoldbrewqloo",
        "workflow": "motion_qloo",
        "kind": "product",
        "brief": "A fifteen-second ad for Nightshift, a fictitious canned cold brew coffee: slow-steeped for twenty "
                 "hours, sold chilled. Comparable brands: Blue Bottle Coffee, La Colombe, Stumptown Coffee "
                 "Roasters. Claim: \"Steeped for twenty hours.\" Call to action: \"Find it chilled.\" "
                 "Audience A: Berlin, aged 30 to 34. Audience B: Lagos, aged 30 to 34. Audience C: Seoul, aged "
                 "30 to 34. Silent. No logo is supplied: set the name in type.",
    },
    {
        # The Nebius entry's. A theorem every first-year student meets, with a worked example whose every step the checks
        # can decide: the integral, its evaluation bracket and the area all go through `Verify.derivation`, and the claim
        # that the area grows at the curve's height through `Verify.claim`. States no figure: the worked values are the
        # agent's to compute and check.
        "label": "Math concept explainer: the fundamental theorem of calculus",
        "name": "ftcexplainer",
        "workflow": "math_explainer",
        "kind": "",
        "brief": "Explain the fundamental theorem of calculus to a first-year student who knows what a derivative is "
                 "and what the area under a curve means, but has not seen why the two are connected. Show that the "
                 "area under a curve grows at the rate of the curve's height, then use it to find an area exactly, "
                 "worked through with x squared. Sixty seconds, narrated.",
    },
    {
        "label": "Empty room, after",
        "name": "emptyroom",
        "workflow": "drawing",
        "kind": "review",
        "brief": "A room a moment after whoever was in it left: the chair pushed back, the lamp "
                 "still swinging, one door open onto dark. No people in frame. Tell the story with "
                 "the objects and the light, and I will steer what happened here.",
    },
    {
        "label": "Establishing: the approach",
        "name": "establishing",
        "workflow": "drawing",
        "kind": "review",
        "brief": "An establishing frame: a lone figure crossing open ground toward a building that "
                 "dwarfs them. Wide, low horizon, the architecture doing the intimidating. Block it "
                 "and I will tell you how much sky to give it.",
    },
    {
        "label": "Two figures, one lit",
        "name": "twofigures",
        "workflow": "drawing",
        "kind": "review",
        "brief": "Two figures in a doorway, one in the light and one a silhouette against it. We "
                 "read the situation from their shapes and the gap between them, not from their "
                 "faces. Stage it and I will tell you which of them we should be worried about.",
    },
    {
        "label": "Insert: the hand",
        "name": "insertshot",
        "workflow": "drawing",
        "kind": "review",
        "brief": "An insert frame, close on hands: one setting something down that the other is not "
                 "meant to see. Tight crop, the object reading instantly, everything else falling "
                 "away. Choose the object and tell me what you chose, or ask.",
    },
    {
        "label": "Car interior, night",
        "name": "carnight",
        "workflow": "drawing",
        "kind": "review",
        "brief": "Inside a car at night: the driver lit from below by the dash, headlights raking "
                 "across as another vehicle passes. Everything beyond the glass is suggestion "
                 "rather than detail. Start with where the light falls and I will steer the mood.",
    },
    # **The engine-only image pair.** Invented subjects, so no real company's mark or real book's cover is imitated.
    {
        "label": "Logo: a small-batch roastery",
        "name": "emberroast",
        "workflow": "logo",
        "kind": "geometric",
        "brief": "A logo mark and brand sheet for Ember & Ash Coffee Roasters, a small-batch roastery in a former "
                 "foundry. Warm and crafted, never rustic cliché: no coffee beans, no steam swirls. It has to read "
                 "on a bag stamp, a cup sleeve and a favicon.",
    },
    {
        "label": "Book cover: a coastal mystery",
        "name": "saltwatch",
        "workflow": "cover",
        "kind": "",
        "brief": "A cover for The Salt Watch, a mystery novel by Mara Quill: a lighthouse keeper on a northern island "
                 "finds the logbook's last week torn out. Cold and quiet rather than lurid; the title and author "
                 "must read at thumbnail size.",
    },
)


def archived_names() -> list[str]:
    """Projects already in the archive, or empty when there is no archive or it cannot be read.

    **Reached through `sys.modules` and never imported by a bare name.** The web layer is loaded by
    `main.py` under the alias `polson_studio`, because two modules in this tree are called `studio`;
    `import studio.archive` from here would find this package's agent factory and fail. Empty on any
    failure, deliberately: this guards against overwriting an archive, and a guard that cannot read
    the archive must not also stop a visitor creating a project.
    """
    module = sys.modules.get("polson_studio.archive")
    if module is None:
        try:
            module = importlib.import_module("polson_studio.archive")
        except Exception:                                        # noqa: BLE001 - no archive here
            return []
    try:
        return module.names()
    except Exception as exc:                                     # noqa: BLE001 - never fatal
        _logger.warning("polson intake: archive not listed (%s)", exc)
        return []


def safe_filename(raw: str | None) -> str:
    """The name to write, with every route out of the directory removed.

    `UploadFile.filename` is attacker-controlled: it arrives in a multipart header and nothing
    validates it. `..\\..\\etc\\passwd`, an absolute path, an NTFS alternate data stream and a name
    that is nothing but dots are all things a client can send, and `folder / name` would honour the
    first two. Taking the basename of *both* separator conventions — a POSIX server must still reject
    a Windows path, since the attacker chooses the separator, not the host — and then allowing only a
    conservative character set leaves nothing that can traverse.
    """
    name = (raw or "").replace("\\", "/").split("/")[-1].strip()

    # Windows drops a trailing dot or space, so "a.pdf." and "a.pdf " resolve to "a.pdf" *after* any
    # check that read them literally. Stripping them here means the name that is checked is the name
    # that lands.
    name = name.rstrip(". ")

    if not name or set(name) <= {"."}:
        raise HTTPException(400, "The uploaded file has no usable name.")

    # A conservative allowlist rather than a denylist of bad characters: a colon is an NTFS stream,
    # a null truncates a path in some layers, and the set of things worth excluding is not knowable.
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._ -]{0,127}", name):
        raise HTTPException(400,
            f"{name!r} is not a usable file name. Use letters, digits, spaces, dots, dashes and "
            "underscores.")

    if Path(name).suffix.lower() not in DOCUMENT_SUFFIXES:
        raise HTTPException(400,
            f"{Path(name).suffix or 'that'} is not a type the studio can read. Supported: "
            + ", ".join(sorted(DOCUMENT_SUFFIXES)))

    return name


async def stage_upload(upload: UploadFile, into: Path) -> Path:
    """Streams an upload into `into`, under its own sanitised name, refusing it past the cap.

    **Staged under the real name, in a directory of its own.** An earlier version wrote to a
    `NamedTemporaryFile` and handed back that path, so the document arrived in the project as
    `tmpi7tswuc1.csv` — `stage_documents` copies using the source's name, and the sanitised one had
    been computed, checked, and then not used. The name a visitor sees in `Documents.list()` is the
    name they uploaded.

    Streamed rather than `await upload.read()`: reading first and checking the length afterwards
    means a visitor can spend the server's memory before being refused, which on a public URL is the
    whole attack.
    """
    name = safe_filename(upload.filename)
    staged = into / name

    written = 0
    with staged.open("wb") as out:
        while chunk := await upload.read(CHUNK):
            written += len(chunk)
            if written > MAX_UPLOAD_BYTES:
                out.close()
                staged.unlink(missing_ok=True)
                raise HTTPException(413,
                    f"{name} is larger than the {MAX_UPLOAD_BYTES // (1024 * 1024)} MB limit.")
            out.write(chunk)

    if written == 0:
        staged.unlink(missing_ok=True)
        raise HTTPException(400, f"{name} is empty.")

    _logger.info("intake: staged %s (%d bytes)", name, written)
    return staged


#: What the agent is told first. The brief is already in `brief.md`; this only points at it, so the
#: director's words reach the agent by reference rather than by being restated here.
OPENING = "Begin. Read brief.md and work the project through to a finished deliverable."

#: Runs in flight, held so the event loop does not collect a task nobody is awaiting. A run outlives
#: the request that started it by many minutes, which is the whole reason it is a task at all.
_running: set[asyncio.Task] = set()

#: A run's task is named this plus its project, so the studio can ask whether a project is being worked.
RUN_TASK = "polson-run:"


def running(name: str) -> bool:
    """Whether a run of project `name` is in flight on this host."""
    return any(isinstance(t, asyncio.Task) and t.get_name() == RUN_TASK + name and not t.done() for t in _running)

#: How many runs this host will start in a rolling 24 hours, and how many at once.
#:
#: **The per-run budgets do not add up to a limit.** `POLSON_BUDGET_TOKENS`, `POLSON_BUDGET_MINUTES`
#: and the circuit breaker each bound *one* run; nothing bounded how many runs a stranger could
#: start. On a public URL that is an open door onto a metered image service and a metered model —
#: the cost surface `CLAUDE.md` Milestone 6 §4 names and nothing implemented.
#:
#: A spend brake, **not a security control**: it is global rather than per visitor, because behind
#: Cloud Run the only per-visitor key is an IP, and rate-limiting by IP is both easy to evade and a
#: kind of fingerprinting this project has no reason to do. One number, applied to everyone.
MAX_RUNS_PER_DAY = int(os.environ.get("POLSON_MAX_RUNS_PER_DAY", "25"))
MAX_CONCURRENT_RUNS = int(os.environ.get("POLSON_MAX_CONCURRENT_RUNS", "2"))

#: When each run was started, newest last. In memory on purpose — the deployment runs one instance
#: and its projects are ephemeral, so a counter that outlived them would be describing runs whose
#: record is already gone. A restart clears it, which is the honest failure mode for a brake whose
#: subject is the container's own work: it can be defeated by crashing the service, and anyone who
#: can do that has a better lever than this one anyway.
_started: deque[float] = deque(maxlen=1000)


def _too_many() -> str | None:
    """Why this run may not start, or None. Checked before anything is created or staged."""
    if MAX_CONCURRENT_RUNS > 0 and len(_running) >= MAX_CONCURRENT_RUNS:
        return (f"{len(_running)} run(s) already going, which is the limit for this host. "
                "Watch one of those, or come back when it has finished — a run takes a few minutes.")

    if MAX_RUNS_PER_DAY > 0:
        cutoff = time.time() - 86_400
        while _started and _started[0] < cutoff:
            _started.popleft()
        if len(_started) >= MAX_RUNS_PER_DAY:
            return (f"This host has started its {MAX_RUNS_PER_DAY} runs for today. The studio spends "
                    "real money on every run, so the daily allowance is deliberate rather than a "
                    "fault. Finished runs are still readable in the studio.")
    return None


class Refused(Exception):
    """Why a commission was not made, and the HTTP status that says so.

    Raised by `check`, `commission` and `free_name`. `/projects` turns it into an `HTTPException`; the
    studio's composer reads `status` and the message off it and shows them on its own form — which is
    why it is a plain exception carrying a status rather than FastAPI's, so the studio needs no import
    from this package to read one.
    """

    def __init__(self, status: int, message: str) -> None:
        super().__init__(message)
        self.status = status


def check(name: str, workflow: str, kind: str, brief: str, deadline: str = "",
          start: bool = True) -> int | None:
    """Every refusal a commission can meet before anything is staged or written. Returns its minutes.

    **One path for both forms.** `/projects` and the studio's composer both commission through here, so
    the name rule, the archive guard, the offered workflows and types, the deadline ceiling and the
    spend brake cannot drift apart — a second copy is how a form ends up accepting what the host will
    then refuse at run time.
    """
    if not VALID_APP_NAME.match(name) or len(name) > MAX_NAME:
        raise Refused(400,
            f"{name!r} cannot be a project name — start with a letter, then letters, digits or "
            f"underscores, up to {MAX_NAME} characters. No dashes: the name becomes an ADK app "
            "name, which must be a Python identifier.")

    # **A name free on this container is not necessarily a name free in the archive.**
    # The CLI refuses a non-empty project directory, which covers a collision with a project
    # still on disk. It cannot see the archive — and after a restart that is where every earlier
    # project lives. `mirror` keys its prefix on the project's directory name, so a second run
    # under an archived name sweeps into the first one's prefix and overwrites its event logs,
    # its brief and any artifact sharing a filename: the two runs interleave and neither is
    # readable afterwards. Easy to reach by accident, because the starters offer fixed names.
    if name in archived_names():
        raise Refused(409, (
            f"There is already an archived project called {name!r}, and a new run under that "
            f"name would be written over it. Choose another name — or open the archived one "
            f"from the studio, where its renders, scripts and documents are kept."))

    workflow = canonical(workflow)
    if workflow not in WORKFLOWS:
        raise Refused(400, f"{workflow!r} is not offered here. Choose: {', '.join(WORKFLOWS)}.")

    # Checked against *this* workflow's list rather than the union: every type here happens to be
    # offered by both, but a type the chosen workflow lacks would reach the generator as a
    # `--type` naming a `type.<name>.md` that is not in its template, and the agent would be sent
    # a direction nothing describes.
    if kind and kind not in WORKFLOWS[workflow]:
        offered = ", ".join(WORKFLOWS[workflow]) or "none"
        raise Refused(400, f"The {workflow} workflow does not offer a type {kind!r}. It offers: {offered}.")

    if not brief.strip():
        raise Refused(400, "A brief is required — say what you want made.")

    if needs_taste(workflow) and not taste_available():
        raise Refused(400, (
            f"The {workflow} workflow designs for each audience from Qloo's taste data, which is turned off here. "
            f"Choose {base_workflow(workflow)}, which makes the same piece from the brief and research, or turn Qloo "
            "on (Qloo:Enabled in bin/cli/appsettings.json)."))

    # **Checked here, not by the input's own min/max.** Those are a convenience for a browser;
    # this endpoint is reachable without one, and the ceiling is what keeps a run being halted
    # cleanly by the breaker rather than killed mid-flight by the platform.
    # The host's own allowance, clamped to what this host can stop cleanly. Where the two disagree the smaller
    # wins: a poster's 45 is offered whole with the keepalive on and as 38 without it, never past the run limit.
    # A `_qloo` twin takes its base workflow's allowances, as the CLI does.
    base = base_workflow(workflow)
    minutes = DEADLINES.get(f"{base}.{kind}", DEADLINES.get(base))
    if minutes is not None:
        if (extra := audiences_in(brief) - AUDIENCES_IN_DEADLINE.get(base, 2)) > 0:
            minutes += PER_AUDIENCE_MINUTES.get(base, 0) * extra
        minutes = min(minutes, MAX_DEADLINE_MINUTES)
    if (asked := deadline.strip()):
        try:
            minutes = int(asked)
        except ValueError:
            raise Refused(400, f"{asked!r} is not a number of minutes.") from None
        if not MIN_DEADLINE_MINUTES <= minutes <= MAX_DEADLINE_MINUTES:
            raise Refused(400, (
                f"A commission here runs between {MIN_DEADLINE_MINUTES} and "
                f"{MAX_DEADLINE_MINUTES} minutes. The ceiling is not arbitrary: this host stops a "
                f"run at {RUN_LIMIT_SECONDS / 60:.0f} minutes, and the agent needs the rest of that "
                f"to be stopped in an orderly way rather than cut off mid-drawing."))

    # Before the upload is staged and before the project is written, so a refused commission
    # leaves nothing behind and costs nothing. 429 rather than 400: the request is fine, the
    # host is not willing right now, and a client should read it as "later" rather than "wrong".
    if start and (full := _too_many()):
        raise Refused(429, full)
    return minutes


async def commission(app: FastAPI, name: str, workflow: str, brief: str, kind: str = "",
                     deadline: str = "", documents: list[Path] | tuple[Path, ...] = (),
                     start: bool = True, auto: bool = False,
                     request: list[str] | tuple[str, ...] = (), deny: list[str] | tuple[str, ...] = ()) -> str | None:
    """Makes the project and, with `start`, sets the agent working on it. Raises `Refused`.

    Returns the studio's page for the run when one was started and a studio is mounted to watch it,
    else None. `documents` are files already staged and checked by the caller; `create` copies them
    into the project's `documents/`, and is the only thing that writes into it. `request` and `deny` are design
    technique ids; the CLI refuses any it does not know or that is not for this workflow, and its reason is the refusal.
    """
    workflow = canonical(workflow)
    minutes = check(name, workflow, kind, brief, deadline, start)
    try:
        create(name, workflow=workflow, prompt=brief, type_=kind or None, deadline=minutes,
               documents=[str(p) for p in documents] or None, budget_tokens=token_budget(workflow, brief, kind),
               auto=auto, request=list(request), deny=list(deny))
    except GenerateError as exc:
        raise Refused(400, str(exc)) from exc

    if not start:
        return None

    # **Creating a project is not starting one, and that gap is what this closes.** The form used
    # to end at a redirect into ADK's console, where the visitor had to know to type a message
    # before anything happened. Two directors in a row read the empty studio as a failure — which
    # it was, of the flow rather than of the run.
    #
    # Fired as a task rather than awaited: a run takes minutes and the response has to come back
    # now. Nothing reads its result, so a failure is logged rather than raised.
    # Recorded at the moment of starting rather than of asking, so a commission refused above or
    # one that never reached here does not consume the day's allowance.
    _started.append(time.time())

    run = asyncio.create_task(launch(app, name), name=RUN_TASK + name)
    _running.add(run)
    run.add_done_callback(_running.discard)
    # The run outlives the request that started it; this keeps the instance's CPU while it works (keepalive.py).
    # Awaited before answering, so the hold's own setup runs while this request still has the CPU.
    # Held for the deadline and what the breaker may still need after it; a run hung past that is not. With no
    # deadline, as long as the keepalive holds anything.
    keepalive.track(run, minutes=minutes + AFTER_DEADLINE_SECONDS / 60 if minutes else None)
    await keepalive.held()
    run.add_done_callback(lambda t: t.cancelled() or t.exception() is None
                          or _logger.error("intake: %s failed to run: %r", name, t.exception()))

    # Registered after the start, so the record is watched from this run's first event.
    return await observe(app, name)


def free_name(title: str) -> str:
    """An app name for a commission titled `title`, free here and in the archive. Raises `Refused`.

    `Harbour festival trailer` becomes `harbour_festival_trailer`, then `_2`, `_3`… The studio's own
    ids are slugs with dashes, which an ADK app name cannot carry, so a commission made through the
    composer is named here rather than by it.
    """
    base = re.sub(r"[^a-z0-9]+", "_", title.lower()).strip("_")[:MAX_NAME - 3].strip("_")
    if not base[:1].isalpha():
        base = f"request_{base}".strip("_")[:MAX_NAME - 3].strip("_")

    taken = set(archived_names())
    for n in range(1, 100):
        name = base if n == 1 else f"{base}_{n}"
        if name not in taken and not (Path(PROJECTS_DIR) / name).exists():
            return name
    raise Refused(409, "There are too many commissions with that title; choose another.")


class Commissioner:
    """What the mounted studio's composer is given in place of a driver: this host's way to make a run.

    The studio offers what `types` says, names a request with `free_name`, and starts it with
    `commission` — the same checks, caps and launch as `/projects`. It never imports this package; it
    is handed one of these by `main.py`, and a studio without one stays a window onto the record.
    """

    def __init__(self, app: FastAPI) -> None:
        self.app = app

    def types(self, workflow: str) -> tuple[str, ...] | None:
        """The types this host offers for `workflow`, or None when it does not offer the workflow."""
        return WORKFLOWS.get(canonical(workflow))

    def free_name(self, title: str) -> str:
        return free_name(title)

    def running(self, name: str) -> bool:
        """Whether this host is working project `name` now: a model turn can be minutes with nothing written."""
        return running(name)

    async def commission(self, name: str, workflow: str, kind: str, brief: str,
                         documents: list[Path], auto: bool = False,
                         request: list[str] | tuple[str, ...] = (), deny: list[str] | tuple[str, ...] = ()) -> str | None:
        return await commission(self.app, name, workflow, brief, kind, documents=documents, start=True, auto=auto,
                                request=request, deny=deny)


#: `POST /requests` — the studio's composer, which commissions through `Commissioner` and so meets
#: every check `/projects` makes.
REQUESTS = re.compile(r"^/requests$")

#: `POST /runs/<id>/say` — the director interrupting a run that is already going.
SAY = re.compile(r"^/runs/[^/]+/say$")

#: `POST /runs/<id>/answer` — the director settling a question the agent stopped to ask.
ANSWER = re.compile(r"^/runs/[^/]+/answer$")

#: `POST /runs/<id>/comparison` — the audiences' finished films set side by side in one MP4 (`studio.compare`).
COMPARISON = re.compile(r"^/runs/[^/]+/comparison$")


def observing_only(method: str, path: str, root_path: str = "") -> bool:
    """Whether a request to the mounted studio is one an observer may make.

    Reading is every GET. Three mutations belong to it: registering a watch, **speaking to a run that
    is already going**, and **answering a question that run asked**.

    **Why these are allowed when they look like driving.** The blanket refusal was right when it was
    written — the studio's whole write side went through the Antigravity SDK, which this runtime does
    not ship — but it stopped being true one verb at a time. `adk_agent.interject` gives an ADK run a
    channel for words the director volunteered; `adk_agent.ask` gives it the other direction, holding
    the future the agent is awaiting. Both live in this process, because the studio is mounted on the
    ADK app, so neither needs a driver.

    `answer` was the last to change and this comment used to say why it could not: it settles a
    question the *host* asked, and with no host that could ask one, `ObservedRun.answer` returned
    False and refusing here merely saved the request a second refusal. Now the host is this process.

    **And one creation: the studio's composer, `POST /requests`.** It is allowed because it does not
    drive anything itself: on this runtime it commissions through `Commissioner`, which is `/projects`
    under another form, with the same checks, caps and launch. Starting or continuing a request from its
    page (`/requests/<name>/start`) and the studio's own `POST /projects` and `POST /runs` still need a
    driver, and are still refused.

    **And one view: `POST /runs/<id>/comparison`.** A POST because it makes a file, but what it makes is a reading
    of work already finished — the audiences' films side by side — written beside the run and made again only when
    the films change. It drives no agent and needs no driver.

    **The subtlety is the path.** Middleware on a mounted app sees the *whole* path with `root_path`
    beside it — `/studio/observe`, not `/observe`; only route matching strips the prefix afterwards.
    Comparing the raw path refuses the one mutation this exists to allow, which is what the first
    version did.
    """
    if method in ("GET", "HEAD"):
        return True

    if root_path and path.startswith(root_path):
        path = path[len(root_path):]

    path = path.rstrip("/")
    return (path == "/observe" or bool(SAY.match(path)) or bool(ANSWER.match(path))
            or bool(REQUESTS.match(path)) or bool(COMPARISON.match(path)))


async def launch(app: FastAPI, name: str, message: str = OPENING) -> None:
    """Starts the agent on `name`, by driving this very app.

    **In-process rather than over the network.** `ASGITransport` calls the app directly, so there is
    no port to discover, no second connection to the machine, and nothing to configure differently
    when this is deployed behind a proxy. It is how the tests drive it too.
    """
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://intake", timeout=None,
                                 headers=api_gate.internal()) as web:
        made = await web.post(f"/apps/{name}/users/{USER}/sessions", json={})
        made.raise_for_status()
        session = made.json()["id"]

        answer = await web.post("/run_sse", json={
            "appName": name, "userId": USER, "sessionId": session,
            "newMessage": {"role": "user", "parts": [{"text": message}]},
            "streaming": False,
        })
        answer.raise_for_status()
    _logger.info("intake: %s ran to completion", name)


async def observe(app: FastAPI, name: str) -> str | None:
    """Registers a watch on `name` and returns the studio path, or None if there is no studio.

    Registered *after* the run is started rather than before: the ADK cut is the last `run.begin`,
    and a project with earlier work would otherwise replay it as though it were this run.
    """
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://intake") as web:
        try:
            seen = await web.post("/studio/observe", data={"project": name})
        except Exception as exc:                                # noqa: BLE001 - studio is optional
            _logger.info("intake: no studio to observe %s with (%s)", name, exc)
            return None

    location = seen.headers.get("location")
    return location if seen.status_code == 303 and location else None


FORM = """<!doctype html>
<meta charset="utf-8">
<title>Polson — new commission</title>
<style>
 body { font: 15px/1.5 system-ui, sans-serif; max-width: 40rem; margin: 4rem auto; padding: 0 1rem;
        color: #1c2733; background: #faf8f4; }
 h1 { font-weight: 600; letter-spacing: -.02em; }
 label { display: block; margin: 1.2rem 0 .3rem; font-weight: 600; }
 input, select, textarea { width: 100%; padding: .5rem; font: inherit;
        border: 1px solid #cdc7bb; border-radius: 3px; background: #fff; }
 textarea { min-height: 6rem; }
 small { color: #6b7280; font-weight: 400; }
 .note { margin-top: 1.2rem; padding: .7rem .9rem; font-size: .88rem; color: #4b5563;
        background: #f3f0ea; border-left: 3px solid #cdc7bb; border-radius: 3px; }
 button { margin-top: 1.5rem; padding: .6rem 1.4rem; font: inherit; font-weight: 600;
        background: #1f6f8b; color: #fff; border: 0; border-radius: 3px; cursor: pointer; }
 .starters { margin: 1.4rem 0 .4rem; }
 .starters p { margin: 0 0 .5rem; font-size: .88rem; color: #6b7280; }
 .chips { display: flex; flex-wrap: wrap; gap: .4rem; margin-bottom: .7rem; }
 .group { margin: 0 0 .25rem; font-size: .75rem; font-weight: 600; letter-spacing: .06em;
        text-transform: uppercase; color: #8a94a0; }
 /* Not the submit button: these fill the form, they do not send it. Stated in the styling as well
    as in `type="button"`, so the one that commissions a run never looks like the five that do not. */
 .chips button { margin: 0; padding: .35rem .7rem; font-size: .86rem; font-weight: 500;
        background: #fff; color: #1f6f8b; border: 1px solid #cdc7bb; border-radius: 999px; }
 .chips button:hover { background: #f3f0ea; }
 .chips button[aria-pressed="true"] { background: #1f6f8b; color: #fff; border-color: #1f6f8b; }
 .back { display: inline-block; margin-bottom: 1.2rem; font-size: .85rem; color: #1f6f8b;
        text-decoration: none; }
 .back:hover { text-decoration: underline; }
</style>
<!-- `/studio/`, not `/`. The root of this app is ADK's own dev UI, and a "back to the studio" link
     that lands a visitor there is the exact bug the run page's own link had — it reads as the studio
     having vanished. Absolute rather than relative, matching this form's own action="/projects". -->
<a class="back" href="/studio/">&larr; projects</a>
<h1>New commission</h1>
<p>Describe what you want made. Attach a document and the studio will read its figures
   rather than researching them.</p>

<div class="starters">
  <p>Or start from one of these &mdash; every field stays editable.</p>
  __STARTERS__
</div>

<form method="post" action="/projects" enctype="multipart/form-data">
  <label>Project name <small>letters, digits and underscores &mdash; no dashes</small></label>
  <input name="name" required pattern="[a-zA-Z][a-zA-Z0-9_]{0,47}" placeholder="boxoffice2025">

  <label>Workflow</label>
  <select id="workflow" name="workflow">__WORKFLOWS__</select>

  <label>Type <small>optional &mdash; the direction the workflow takes</small></label>
  <select id="kind" name="kind">__TYPES__</select>

  <!-- The bounds are stated, not only enforced. `max` stops the spinner and blocks the submit, but
       a visitor who cannot see the ceiling only finds it by being refused — and the interesting half
       is *why* it is less than the run limit: the breaker's grace and credited waiting come after it. -->
  <label>Deadline <small>minutes &mdash; __MIN_DEADLINE__ to __MAX_DEADLINE__, blank for the
         workflow's own. The rest of the hour is held back so the agent can be stopped in an orderly
         way rather than cut off mid-drawing.</small></label>
  <input id="deadline" name="deadline" type="number" inputmode="numeric"
         min="__MIN_DEADLINE__" max="__MAX_DEADLINE__" step="1" placeholder="">

  <label>Brief</label>
  <textarea name="brief" required
      placeholder="Chart the 2025 box office returns as a timeline."></textarea>

  <label>Document <small>optional — PDF, CSV, TXT, MD, JSON, or an image</small></label>
  <input type="file" name="document" accept="__ACCEPT__">

  <label style="font-weight:400; margin-top:1.4rem">
    <input type="checkbox" name="start" value="1" checked style="width:auto; margin-right:.4rem">
    Start the agent straight away, and open the studio on it
  </label>

  <label style="font-weight:400; margin-top:.4rem">
    <input type="checkbox" name="auto" value="1" style="width:auto; margin-right:.4rem">
    Decide everything &mdash; the agent asks nothing, and lists its decisions for you to review afterwards
  </label>

  <p class="note">If this host restarts while your project is running, the run stops and you will
     need to start it again. <b>The work it had already done is kept</b> &mdash; the project comes
     back under <b>Archived projects</b> in the studio, with its renders, its scripts and its
     documents.</p>

  <button type="submit">Create</button>
</form>
<script>
// The option list holds every type any offered workflow has; this hides the ones the selected
// workflow does not offer, and disables the control entirely when it offers none. Convenience only
// — the server refuses a bad pairing whether or not this ran. Same guard idiom as the studio form:
// a missing element returns rather than throwing, so one absent control cannot take the page down.
(function () {
  const workflow = document.getElementById('workflow');
  const kind = document.getElementById('kind');
  if (!workflow || !kind) return;

  const deadline = document.getElementById('deadline');

  function sync() {
    const chosen = workflow.selectedOptions[0];
    const types = (chosen.dataset.types || '').split(',').filter(Boolean);
    kind.disabled = types.length === 0;
    for (const option of kind.options) {
      option.hidden = option.value !== '' && !types.includes(option.value);
    }

    // **A type the new workflow does not offer cannot be kept.** Hiding an option does not clear a
    // value already on it, so choosing drawing_partner (review) and then switching to infographic
    // left `review` selected on a hidden option — submitted, and refused by the server for a type
    // the visitor could no longer even see. Only visible now that a workflow offers a type no other
    // one does.
    if (kind.disabled || !types.includes(kind.value)) kind.value = '';

    // One type is not a choice. Select it, so the control shows what is going to happen rather than
    // an em-dash the server will quietly turn into the same thing.
    if (types.length === 1 && !chosen.dataset.untyped) kind.value = types[0];
    // The placeholder, not the value: an empty field means "the workflow's own", and pre-filling it
    // would turn a default the studio chose into a number the visitor appears to have set.
    if (deadline) deadline.placeholder = chosen.dataset.deadline || '';
  }
  workflow.addEventListener('change', sync);
  sync();

  // The starters. Each chip carries its whole commission as data attributes, so filling the form is
  // four assignments and no round trip — and because they write into the ordinary controls, every
  // field stays editable afterwards and the server validates exactly what it would have anyway.
  //
  // **`sync()` runs between the workflow and the type**, not after both. The type control hides the
  // options the chosen workflow does not offer, so setting a type before its workflow has been
  // synced assigns a value to a hidden option — which the browser keeps, and the server then refuses
  // with a message about a type that is not offered. Order is load-bearing here.
  for (const chip of document.querySelectorAll('.chips button')) {
    chip.addEventListener('click', () => {
      const form = document.querySelector('form');
      if (!form) return;

      workflow.value = chip.dataset.workflow;
      sync();
      kind.value = chip.dataset.kind;
      form.name.value = chip.dataset.name;
      form.brief.value = chip.dataset.brief;

      for (const other of document.querySelectorAll('.chips button')) {
        other.setAttribute('aria-pressed', String(other === chip));
      }
      form.brief.focus();
      // The end, not the start: this is a draft to edit rather than a value to accept, and a cursor
      // sitting after the last word says so without a line of instruction.
      form.brief.setSelectionRange(form.brief.value.length, form.brief.value.length);
    });
  }
})();
</script>
"""


def mount(app: FastAPI) -> None:
    """Adds the intake form and its endpoint to an existing app."""
    request_app = app

    @app.get("/new", response_class=HTMLResponse)
    async def form() -> str:
        # `data-types` is what lets the type control narrow itself to the chosen workflow without a
        # round trip, and it is the same list the POST validates against, so the two cannot disagree.
        # The **value** stays the template name, which is what the POST validates and what the
        # generator is given; only the visible text is relabelled.
        # The `_qloo` twins only while Qloo is on: offered and then refused is a form that lies.
        taste = taste_available()
        options = "".join(
            f'<option value="{w}" data-types="{",".join(types)}"'
            f'{' data-untyped="1"' if w in UNTYPED_IS_A_CHOICE else ''}'
            f' data-deadline="{DEADLINES.get(base_workflow(w)) or ""}">{escape(LABELS.get(w, w))}</option>'
            for w, types in WORKFLOWS.items() if taste or not needs_taste(w))
        types_options = '<option value="">&mdash;</option>' + "".join(
            f'<option value="{t}">{t}</option>' for t in ALL_TYPES)
        accept = ",".join(sorted(DOCUMENT_SUFFIXES))

        # Escaped with `quoteattr` rather than by hand. These are our own strings, so nothing here is
        # untrusted — but a brief is prose, prose acquires apostrophes and quotation marks the moment
        # anyone edits one, and an unescaped quote in an attribute ends the attribute. The failure is
        # a chip that silently fills half a brief, which is worse than one that does not work at all.
        # Grouped by workflow, in the order the workflows are offered. Thirteen chips in one row
        # reads as a wall and hides the distinction that matters most — that one of these sets makes
        # a graphic on its own and the other needs somebody at the keyboard.
        def chip(entry: dict[str, str]) -> str:
            return (f'<button type="button" aria-pressed="false"'
                    f' data-name={quoteattr(entry["name"])}'
                    f' data-workflow={quoteattr(entry["workflow"])}'
                    f' data-kind={quoteattr(entry["kind"])}'
                    f' data-brief={quoteattr(entry["brief"])}>{escape(entry["label"])}</button>')

        chips = ""
        for workflow in WORKFLOWS:
            if needs_taste(workflow) and not taste:
                continue
            group = [e for e in STARTERS if e["workflow"] == workflow]
            if not group:
                continue
            chips += (f'<p class="group">{escape(LABELS.get(workflow, workflow))}</p>'
                      f'<div class="chips">{"".join(chip(e) for e in group)}</div>')

        return (FORM.replace("__WORKFLOWS__", options)
                    .replace("__TYPES__", types_options)
                    .replace("__STARTERS__", chips)
                    .replace("__MIN_DEADLINE__", str(MIN_DEADLINE_MINUTES))
                    .replace("__MAX_DEADLINE__", str(MAX_DEADLINE_MINUTES))
                    .replace("__ACCEPT__", accept))

    @app.post("/projects")
    async def make(
        name: str = Form(...),
        brief: str = Form(...),
        workflow: str = Form("vector_infographic"),
        kind: str = Form(""),
        deadline: str = Form(""),
        document: UploadFile | None = File(None),
        start: str = Form(""),
        auto: str = Form(""),
    ):
        """Creates a project, stages the upload into its `documents/`, opens the console on it."""
        name, kind = name.strip(), kind.strip()

        # Checked before the upload is staged, so a refused commission costs nothing; `commission`
        # checks again, which is cheap and keeps it safe to call without this.
        try:
            check(name, workflow, kind, brief, deadline, bool(start))
        except Refused as exc:
            raise HTTPException(exc.status, str(exc)) from exc

        # Staged into a scratch directory *before* the project exists, so a refused upload leaves
        # nothing behind and no half-made project. Its own directory rather than the shared temp
        # folder, so the file can keep its real name without colliding with a concurrent upload of
        # the same name. `create` copies it in and is the only thing that writes into the project.
        scratch: Path | None = None
        try:
            staged: list[Path] = []
            if document is not None and document.filename:
                scratch = Path(tempfile.mkdtemp(prefix="polson-intake-"))
                staged.append(await stage_upload(document, scratch))

            watching = await commission(request_app, name, workflow, brief, kind, deadline,
                                        documents=staged, start=bool(start), auto=bool(auto))
        except Refused as exc:
            raise HTTPException(exc.status, str(exc)) from exc
        finally:
            if scratch is not None:
                shutil.rmtree(scratch, ignore_errors=True)

        # The app appears without a restart — `list_agents` re-reads the directory on every call,
        # which `newproject` relies on too. Started, it goes straight to the record, which is the
        # thing worth looking at while it works; the console is the fallback when there is no studio
        # mounted, so this never strands a visitor.
        return RedirectResponse(watching or f"/dev-ui/?app={name}", status_code=303)
