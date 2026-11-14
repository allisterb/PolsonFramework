"""What a visitor can commission, in their words rather than the studio's.

The developer page offers `projects.WORKFLOWS` as it is: template names, types, and a brief box. A
person commissioning work does not think in templates. They want a film trailer or a poster, in some
sizes and some formats, made from some files — so this is the mapping from that request to the
workflow, type and brief the studio actually runs.

**Every choice here ends as text in `brief.md` or as an argument `projects.check` already validates.**
Nothing in this module selects a template by a visitor's string: a card names its workflows, a route
picks one of them by index into that closed list, and the type is checked against
`projects.WORKFLOWS` by the code that spawns the CLI. Sizes and formats are *asked for* in the brief,
not enforced — the workflows read the brief as prose, so a size the agent ignores is a size the
agent ignores, and the page must not promise more than that.
"""

from __future__ import annotations

import functools
import json
import os
import re
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path

from orchestrator import credentials

from . import projects
from .runs import StudioError

#: A request's title is a sentence a person typed; its project id is derived from it.
MAX_TITLE = 80

#: A free-text extra (a headline, an author) is a line, not a brief.
MAX_EXTRA = 200

#: A long extra (an audience's questions) is a paragraph, drawn as a box under the brief; still far short of a brief.
MAX_LONG_EXTRA = 2000

#: Largest custom dimension, in pixels. Past this the engine's own canvas caps would refuse it later.
MAX_SIDE = 8192

#: Qloo's age bands, as `Taste` takes them (`QlooParameters` on the .NET side), with the words a person reads.
AGE_BANDS: tuple[tuple[str, str], ...] = (
    ("24_and_younger", "24 and younger"), ("25_to_29", "25 to 29"), ("30_to_34", "30 to 34"),
    ("35_to_44", "35 to 44"), ("45_to_54", "45 to 54"), ("55_and_older", "55 and older"))

#: The languages a poster or film can be made in for an audience, by code, English first and the default. Only those
#: whose script the engine draws, a separate model reads back, and a voice speaks (probe 2026-10-27): German, Japanese,
#: Chinese, Korean and Hindi measured, French, Spanish and Portuguese in German's Latin script, and Arabic, drawn right
#: since Motion text took the canvas's shaper and read back by Gemini (probe 2026-10-29).
LANGUAGES: tuple[tuple[str, str], ...] = (
    ("", "English"), ("de", "German"), ("fr", "French"), ("es", "Spanish"), ("pt", "Portuguese"),
    ("ja", "Japanese"), ("zh-Hans", "Chinese (Simplified)"), ("ko", "Korean"), ("hi", "Hindi"), ("ar", "Arabic"))

#: Which service reads the films back, as the engine's `Verify:Reader` falls back to it: `gemini` (the default) or
#: `nemotron`. Set it in the studio's environment, which the MCP server it starts inherits, so both agree.
VERIFY_READER_ENV = "POLSON_VERIFY_READER"

#: Languages Nemotron cannot be trusted to read back. With it reading, their lines are checked by translation back and by
#: the studio's own eye on the frame, and every place the language is named says so, so nobody takes an unchecked line for
#: a checked one. Arabic: it read correctly drawn words as others ("one cow carries the car", probe 2026-10-27). Hindi: on
#: becool3 (2026-10-28) it read a film's credits as unrelated sentences. Gemini read both: the same Hindi film almost
#: exactly, and six correct Arabic lines exactly and five of six deliberately wrong ones as drawn (probe 2026-10-29).
NEMOTRON_UNREADABLE: frozenset[str] = frozenset({"ar", "hi"})


def verify_reader() -> str:
    """`nemotron` when the environment names it, else `gemini`, as the engine decides."""
    return "nemotron" if os.environ.get(VERIFY_READER_ENV, "").strip().lower() == "nemotron" else "gemini"


def checked_by_eye() -> frozenset[str]:
    """The languages whose text the read-back cannot vouch for with this studio's reader: none with Gemini."""
    return NEMOTRON_UNREADABLE if verify_reader() == "nemotron" else frozenset()

#: What is said of such a language wherever it is named.
EYE_NOTE = "checked by eye, not read back"


def taste_available() -> bool:
    """Whether audiences can be measured here: the engine's `Taste` tool is on (`Qloo:Enabled` and `ApiKeys:Qloo` in
    the CLI's settings, read as the engine reads them). Off, the form offers no audiences and no card that needs one.
    Read per call, so the pages follow the file; a running engine reads it once, at start."""
    return credentials.qloo_enabled()


#: Why a card made for measured audiences is refused when Qloo is off.
TASTE_OFF = ("audiences are measured with Qloo's taste data, which is turned off on this studio. The card without "
             "\"for audiences\" makes the same piece from the brief and research, or turn Qloo on (Qloo:Enabled in "
             "bin/cli/appsettings.json).")

#: The poster and trailer briefs name audiences A, B…. Six lets a set span regions (a western, an African, a Middle
#: Eastern and an Asian audience, say); the run's time, tokens and Qloo budget grow with the count (`intake`).
MAX_AUDIENCES = 6

#: What the composer's place fields suggest, one region each, in the order the rows appear. Each is a name in `PLACES`.
EXAMPLE_PLACES = ("Tokyo, Japan", "Lagos, Nigeria", "Dubai, United Arab Emirates", "Berlin, Germany", "São Paulo, Brazil",
                  "Mumbai, India")

#: The places an audience can be, as the picker offers them: "City, Country" (a region between where two share a name)
#: and every country. Built by `tools/build_places.py` from Natural Earth's populated places (public domain; ledgered).
#: **A place is chosen, not typed**: a typo or a bare "Lagos" became whatever Qloo's fuzzy match made of it, and the
#: country decides the figures' conventions too.
PLACES_FILE = Path(__file__).parent / "static" / "places.json"


def _key(text: str) -> str:
    """A place as compared: NFC, no invisible format characters, spaces collapsed, case folded."""
    text = "".join(ch for ch in unicodedata.normalize("NFC", text or "") if unicodedata.category(ch) != "Cf")
    return " ".join(text.split()).casefold()


@functools.lru_cache(maxsize=1)
def places() -> tuple[dict[str, str], dict[str, list[str]]]:
    """Every place's exact name by its key (and by its unaccented spelling), and every place by its first part alone,
    largest first: "lagos" → ["Lagos, Nigeria"]."""
    data = json.loads(PLACES_FILE.read_text(encoding="utf-8"))
    exact: dict[str, str] = {}
    by_name: dict[str, list[str]] = {}
    for label, ascii_name, _pop, _iso in data["places"]:
        exact.setdefault(_key(label), label)
        if ascii_name:
            exact.setdefault(_key(", ".join([ascii_name, *label.split(", ")[1:]])), label)
        for first in {label.split(", ")[0], ascii_name} - {""}:
            by_name.setdefault(_key(first), []).append(label)
    for country, _iso in data["countries"]:
        exact.setdefault(_key(country), country)
    return exact, by_name


def resolve_place(text: str) -> str:
    """The place `text` names, exactly as the list has it. A bare city name that is one place is that place ("Tokyo" →
    "Tokyo, Japan"). Raises `StudioError` for a name in no row, or for one that is several places, naming them."""
    exact, by_name = places()
    if (found := exact.get(_key(text))) is not None:
        return found
    matches = by_name.get(_key(text), [])
    if len(matches) == 1:
        return matches[0]
    if matches:
        raise StudioError(f"{text!r} is {len(matches)} places: {'; '.join(matches[:4])}{'…' if len(matches) > 4 else ''}. "
                          "Choose one from the list.")
    raise StudioError(f"{text[:60]!r} is not a place the studio knows. Choose one from the list, such as Mumbai, India.")


@dataclass(frozen=True)
class Size:
    label: str
    width: int
    height: int

    @property
    def key(self) -> str:
        return f"{self.width}x{self.height}"

    @property
    def detail(self) -> str:
        return f"{self.width} × {self.height} px"


@dataclass(frozen=True)
class Format:
    ext: str
    label: str


@dataclass(frozen=True)
class Route:
    """One workflow a card can run, as the visitor would choose between them."""

    workflow: str
    label: str
    detail: str = ""


@dataclass(frozen=True)
class Extra:
    """A workflow-specific field. `options` empty means free text."""

    key: str
    label: str
    options: tuple[str, ...] = ()
    default: str = ""
    placeholder: str = ""
    #: A paragraph rather than a line: drawn as a text box under the brief, line breaks kept.
    long: bool = False
    #: A long field's box height in lines: a claim is a sentence or two, a viewer a paragraph.
    rows: int = 5
    #: Drawn under the brief, with what the request is about (the company, its stage), rather than in the bar with how
    #: the piece is made (its duration, narration). A long field always is.
    about: bool = False
    #: What the request is about — the company, the film, the product: required, and drawn first, above the brief, so the
    #: brief is left for what it is for. Optional and below the brief, it was typed twice or not at all.
    subject: bool = False

    @property
    def under_brief(self) -> bool:
        return (self.long or self.about) and not self.subject


@dataclass(frozen=True)
class Card:
    slug: str
    title: str
    kind: str                       # video | motion | image | document — sets the icon, sizes and formats
    meta: str
    routes: tuple[Route, ...]
    image: str = ""                 # under static/gallery/, the studio's own work in this workflow
    alt: str = ""
    extras: tuple[Extra, ...] = ()
    style_label: str = "Style"      # what the workflow's types are called on this card
    type_labels: dict[str, str] = field(default_factory=dict)
    audiences: bool = False         # asks who it is for: a place and an age band per audience, A, B…
    #: Audiences may be left out: a report explainer for "shareholders" has no place to measure, one for Mumbai and Tokyo
    #: does, and gets a film per audience in its conventions (pace from Qloo, numbers as the place writes them).
    audiences_optional: bool = False
    #: The workflow types this card offers, when not all of them: `()` for none, one for a card that *is* that type.
    #: A film trailer and a product ad are one workflow; without this each card would offer the other's type, and a
    #: lone type is chosen for the visitor (`front.commissioned`), so every film trailer would become an ad.
    types: tuple[str, ...] | None = None
    #: A file the request cannot run without, by extension: a report explainer with no report has nothing to explain.
    needs: str = ""
    #: The type is chosen in the form, above the brief, rather than in the header: it is what the request is about (the
    #: report's kind), not a setting every workflow has.
    type_in_form: bool = False
    #: The brief the form starts with, for the director to keep or rewrite. Only on a card whose subject is asked
    #: elsewhere (a `subject` field, or the document it `needs`): there a general brief is still about something. On
    #: the others the brief is the only place the subject is, and a prefilled one would be made as typed, about nothing.
    brief: str = ""

    @property
    def multi_route(self) -> bool:
        return len(self.routes) > 1

    @property
    def needs_taste(self) -> bool:
        """Whether this card cannot be made without Qloo: it runs a `_qloo` twin, whose audiences are taste profiles."""
        return any(r.workflow.endswith(projects.QLOO_SUFFIX) for r in self.routes)

    @property
    def fixed_type(self) -> str:
        """The type this card always runs, or ''. Not offered as a choice: the card is the choice."""
        return self.types[0] if self.types is not None and len(self.types) == 1 else ""

    @property
    def default_type(self) -> str:
        """
        The type a card runs when none is chosen, where its types are one family — a report, or a report on an S-1
        (`report.s1`). "The studio's choice" means nothing there, and an untyped motion project is a film trailer.
        """
        family = self.types[0] if self.types and len(self.types) > 1 else ""
        return family if family and all(t.startswith(family + ".") for t in self.types[1:]) else ""

    def check_files(self, names: list[str]) -> None:
        """Refuses a request missing the file this card needs. Raises `StudioError`."""
        if self.needs and not any(n.lower().endswith(self.needs) for n in names):
            raise StudioError(f"{self.title} needs its document: attach the {self.needs[1:].upper()}.")

    @property
    def piece(self) -> str:
        """What one audience gets, in the visitor's words."""
        return "ad" if self.fixed_type == "product" else "film" if self.kind == "video" else "piece"


#: Presets per kind. A tile draws the ratio, so the order is the order a person scans in.
SIZES: dict[str, tuple[Size, ...]] = {
    "video": (Size("Landscape", 1920, 1080), Size("Story", 1080, 1920), Size("Square", 1080, 1080),
              Size("Portrait", 1080, 1350)),
    "motion": (Size("Square", 1080, 1080), Size("Landscape", 1920, 1080), Size("Story", 1080, 1920),
               Size("Portrait", 1080, 1350)),
    "image": (Size("Landscape", 1920, 1080), Size("Portrait", 1080, 1350), Size("Story", 1080, 1920),
              Size("Square", 1080, 1080), Size("Poster", 1200, 1800), Size("A4 print", 2480, 3508),
              Size("Banner", 1500, 500), Size("Cinema", 2048, 858)),
    "document": (Size("A4 print", 2480, 3508), Size("Landscape", 1920, 1080), Size("Portrait", 1080, 1350),
                 Size("Square", 1080, 1080), Size("Tall", 1080, 2700)),
}

#: What each kind can be delivered as. Only formats the engine writes: outFile, outSvg, outPdf, saveVideo, .sif.
FORMATS: dict[str, tuple[Format, ...]] = {
    # The motion workflow writes the film and, when silent, a looping .webp of it. No .sif: the template
    # forbids writing one during a run, and it is exported afterwards with `polson eval --sif`.
    "video": (Format(".mp4", "Video"), Format(".webp", "Looping preview")),
    "motion": (Format(".webp", "Animation"), Format(".mp4", "Video"), Format(".sif", "Synfig file")),
    "image": (Format(".png", "Image"), Format(".webp", "Web image"), Format(".svg", "Vector"),
              Format(".pdf", "Print")),
    "document": (Format(".svg", "Vector"), Format(".png", "Image"), Format(".pdf", "Print")),
}

#: Whether a film is spoken as well as set on screen. Disabled, every word is on screen and the film is silent.
NARRATION = Extra("narration", "Narration", ("Enabled", "Disabled"), "Enabled")

#: What a video card's .webp is when the film is narrated: an animated WebP has no sound, so the film itself is the MP4
#: and the WebP a silent copy beside it (the motion template: "A film with sound stays a video").
SILENT_PREVIEW = "Silent preview"

#: Which film a film poster or trailer is for. A real film by title and year, which the run researches before it
#: designs (and, in a `_qloo` twin, resolves with Qloo); or a new film's title, with its logline and comparables in the brief.
FILM = Extra("film", "Film", placeholder="A real film's title and year, e.g. Be Cool (2005), or a new film's title",
             subject=True)

#: What a product ad or print ad asks for, shared by the mainline cards and their Qloo twins. What the director types goes
#: under the brief, where there is room to write it; the bar keeps the choices.
PRODUCT: tuple[Extra, ...] = (
    Extra("product", "Product", placeholder="Its name, and what it is", long=True, rows=2, subject=True),
    Extra("comparables", "Comparable brands", placeholder="Two or three real brands like it", long=True, rows=2),
    Extra("claim", "Claim", placeholder="The one claim, worded as it may appear", long=True, rows=2),
    Extra("cta", "Call to action", placeholder="What the viewer should do next", long=True, rows=2))
TRAILER_LENGTH = Extra("duration", "Duration", ("15 seconds", "30 seconds"), "15 seconds")

CARDS: tuple[Card, ...] = (
    # **Working workflows first, in rows of three as the pages lay them out** (2026-10-30): what this deployment runs,
    # then what it does not yet (motion graphic, storyboard, comic, painting), so a visitor's eye meets what they can start.
    # The workflow is built and measured around 15-second films; longer ones are more encode parts and more
    # clock, so 30 is the most the form offers. A director who wants longer says so in the brief.
    # **Without Qloo since 2026-11-14**: the trailer, the ad and the two posters are designed from the brief, the
    # director's documents and research on the subject, and an audience is a language version of the one design. As the
    # Qloo entry built them, each audience designed from its taste profile, they are the `-qloo` cards further down.
    Card("film-trailer", "Film trailer", "video", "Video · 15 seconds",
         (Route("motion", "Motion graphics trailer"),),
         image="film-trailer.webp", alt="A trailer's end card: a cracked vinyl record beside the title",
         extras=(FILM, TRAILER_LENGTH),
         audiences=True, audiences_optional=True, types=(),
         brief="A motion trailer for the film: true to the film's tone and story, with its real credits on the end "
               "card."),
    # One product, one ad: the motion workflow's `product` type.
    Card("product-ad", "Product ad", "video", "Video · 15 seconds",
         (Route("motion", "Motion graphics ad"),),
         image="product-ad.webp", alt="A matte black can of cold brew coffee beside its name and call to action",
         extras=(*PRODUCT, TRAILER_LENGTH),
         audiences=True, audiences_optional=True, types=("product",),
         brief="A motion ad for the product that makes the viewer want to try it, built on its one claim and ending "
               "on the call to action."),
    # An audience's questions answered from a long document the director attaches — an annual report, a 10-K. The
    # motion workflow's `report` type: Nemotron reads the PDF as page text through `Documents.ask`, and every figure
    # on screen is one the engine found on its page. Audiences are optional: with places, a film for each, paced from
    # its Qloo profile and with its figures written as that place writes them (Locale.number); without, one film.
    # **The document's kind** is the choice on the card: a kind the studio knows (`report.10k`, an annual report;
    # `report.s1`, an IPO prospectus) brings the questions its readers take to it, so the director's own become optional;
    # any other is answered as asked.
    Card("report-explainer", "Financial report explainer", "video", "Video · 45 seconds, questions answered from a filing",
         (Route("motion", "Motion graphics explainer"),),
         image="report-explainer.webp", alt="An IPO prospectus question answered on screen: who controls the company after the IPO",
         extras=(Extra("viewer", "Who is watching", placeholder="Who they are, and what they already know", long=True),
                 Extra("questions", "Their questions",
                       placeholder="One question a line, up to five. Optional for a Form 10-K or S-1: the studio already asks "
                                   "the questions investors bring to those filings, and yours are answered first.",
                       long=True),
                 Extra("duration", "Duration", ("45 seconds", "60 seconds"), "45 seconds"),
                 NARRATION),
         style_label="Document", type_labels={"report": "Any report", "report.10k": "Form 10-K (annual report)",
                                              "report.s1": "Form S-1 (IPO prospectus)"},
         audiences=True, audiences_optional=True, types=("report", "report.10k", "report.s1"), needs=".pdf",
         type_in_form=True,
         brief="A motion graphic explainer that answers the viewer's questions from the attached filing, clearly "
               "enough to follow without reading it, every figure taken from the document."),
    # A startup's case told to investors from its own pitch deck, and a financial model or metrics where it has them. The
    # motion workflow's `pitch` type: the documents read and checked as a report's are, every projection drawn and
    # labelled as one, the ask as stated. With places, a film per investor audience in its conventions; without, one.
    Card("startup-pitch", "Startup pitch", "video", "Video · 2 minutes from the deck, one film per investor audience",
         (Route("motion", "Motion graphics pitch"),),
         image="startup-pitch.webp", alt="A retold pitch deck's market: US$4.2B a year, the top four players holding 22%",
         extras=(Extra("company", "Company", placeholder="Its name, as the deck gives it", subject=True),
                 Extra("told_as", "Told as", ("The company's own pitch", "A retelling of a past deck"),
                       "The company's own pitch", about=True),
                 Extra("stage", "Stage", ("As the deck says", "Pre-seed", "Seed", "Series A", "Series B or later"),
                       "As the deck says", about=True),
                 Extra("raise", "The raise", placeholder="Amount and instrument, as the company states it; blank for the deck's words",
                       long=True, rows=2),
                 Extra("use_of_funds", "Use of funds", placeholder="What the money is for, if the deck does not say", long=True),
                 Extra("duration", "Duration", ("90 seconds", "2 minutes", "3 minutes"), "2 minutes"),
                 NARRATION),
         audiences=True, audiences_optional=True, types=("pitch",), needs=".pdf",
         brief="Create a motion graphic startup pitch with as much information as possible for potential investors."),
    # A theorem or concept explained in a film: equations, graphs and narration, every formula and claim checked before
    # it is drawn and the film read back after. The Nebius entry's; the `math_explainer` workflow.
    Card("maths-explainer", "Math concept explainer", "video", "Video · a theorem or concept, every step checked",
         (Route("math_explainer", "Math concept explainer"),),
         image="maths-explainer.webp", alt="The area under y = x² from 0 to 2 worked out as 8/3",
         extras=(Extra("topic", "Theorem or concept", placeholder="Named as a textbook would name it", subject=True),
                 Extra("viewer", "Who is watching", placeholder="Their level, and what they already know", long=True),
                 Extra("duration", "Duration", ("45 seconds", "60 seconds", "90 seconds"), "60 seconds"),
                 NARRATION),
         brief="A motion graphic explanation of the theorem or concept for the viewer: the idea built step by step, "
               "with a worked example, every formula and claim checked."),
    # The `poster` workflow's two types as two cards, as the video cards split `motion`: one card with a "Kind" inside hid
    # the film poster behind a choice a visitor had to open the card to see. `poster` was the one card before 2026-10-29;
    # a request recorded under it is placed by its type (`card_for`).
    Card("product-poster", "Product poster", "image", "Image · a print ad",
         (Route("poster", "Print ad"),),
         image="product-poster.webp", alt="Two Nightshift cold brew print ads: a dark bar shelf for Berlin, a pale arc for Seoul",
         extras=PRODUCT,
         audiences=True, audiences_optional=True, types=("product",),
         brief="A print ad for the product, built on its one claim and ending on the call to action."),
    Card("film-poster", "Film poster", "image", "Image · a one-sheet",
         (Route("poster", "One-sheet poster"),),
         image="film-poster.webp", alt="A Be Cool one-sheet: a gold record label behind crosshairs over a black vinyl",
         extras=(FILM, Extra("headline", "Headline", placeholder="The words that must appear", long=True, rows=2)),
         audiences=True, audiences_optional=True, types=("film",),
         brief="A one-sheet poster for the film: true to its tone, with the title and its real billing."),
    # Audiences optional, as the report explainer's: with places, an infographic per audience with the same figures and
    # design, its numerals and language the locale's (`_shared/audience_design.md`); without, one piece.
    Card("infographic", "Infographic", "document", "Document · checked figures",
         (Route("vector_infographic", "Vector", "Delivered as SVG, every figure checked"),
          Route("infographic", "Raster", "Delivered as an image")),
         image="infographic.webp", alt="A blueprint infographic of a lunar descent",
         type_labels={t: t.capitalize() for t in projects.WORKFLOWS["infographic"]},
         audiences=True, audiences_optional=True),
    Card("logo", "Logo and identity", "image", "Image · a mark and its sheet",
         (Route("logo", "Mark and brand sheet"),),
         image="logo.webp", alt="A brand presentation sheet for a P mark",
         type_labels={"antique": "Antique", "geometric": "Geometric", "modern": "Modern"},
         extras=(Extra("name", "Name", placeholder="The name the mark is for", subject=True),),
         brief="A logo mark and brand sheet for the name: simple, distinctive, and legible from a favicon to a sign."),
    Card("book-cover", "Book cover", "image", "Image · one composed cover",
         (Route("cover", "Composed cover"),),
         image="book-cover.webp", alt="The Salt Watch: a lighthouse keeper's silhouette under a sweeping beam, a torn logbook at his feet",
         extras=(Extra("book_title", "Book title", placeholder="As it should appear", subject=True),
                 Extra("author", "Author", placeholder="As it should appear")),
         brief="A cover for the book that tells a reader its genre and mood at thumbnail size, with the title and "
               "author clear."),
    Card("drawing", "Drawing", "image", "Image · pencil to finish",
         (Route("drawing", "Drawing", "Built up in passes"),
          Route("sketch_scene", "Sketched scene", "One scene from gesture")),
         image="drawing.webp", alt="A pencil drawing of an arcade in perspective",
         style_label="How to work", type_labels={"review": "Work with me", "seed": "Surprise me"}),
    Card("motion-graphic", "Motion graphic", "motion", "Animation · one action, keyed",
         (Route("animation", "One keyed action"),),
         image="motion-graphic.webp", alt="A jointed figure at a railing, one arm swept out toward a boat",
         extras=(Extra("duration", "Duration", ("2 seconds", "4 seconds", "8 seconds"), "4 seconds"),
                 Extra("loop", "Loop", ("Loops", "Plays once"), "Loops"))),
    Card("storyboard", "Storyboard", "image", "Image · 6 to 12 panels",
         (Route("storyboard_cast", "Built cast", "The same people in every panel, able to turn round"),
          Route("storyboard", "Bought cast", "One picture per expression; quicker when no one turns"),
          Route("storyboard_quick", "Quick board", "Fewer panels on a short clock")),
         image="storyboard.webp", alt="A six-panel storm-night storyboard",
         extras=(Extra("panels", "Panels", ("4 panels", "6 panels", "8 panels", "12 panels"), "6 panels"),)),
    Card("comic", "Comic", "image", "Image · a page of panels",
         (Route("comic", "Comic page"), Route("comic_studio", "Comic with a studio of agents", "Several agents, one page")),
         image="comic.webp", alt="An inked gull riding a bicycle on a boardwalk",
         style_label="How to work", type_labels={"review": "Work with me", "seed": "Surprise me"}),
    Card("painting", "Painting", "image", "Image · one scene",
         (Route("painting", "Painted scene"),),
         image="painting.webp", alt="A lit house behind bare trees at dusk"),
    # **The Qloo twins**: the four cards above as the Qloo entry built them, one piece per audience, each designed from
    # that audience's measured taste (`poster_qloo`, `motion_qloo`). Audiences are required: they are what is measured.
    # Shown only while Qloo is on (`taste_available`), which needs a Qloo contract.
    Card("film-trailer-qloo", "Film trailer for audiences", "video", "Video · 15 seconds, one film per audience (Qloo)",
         (Route("motion_qloo", "Taste-grounded trailer"),),
         image="film-trailer.webp", alt="A trailer's end card: a cracked vinyl record beside the title",
         extras=(FILM, TRAILER_LENGTH), audiences=True, types=(),
         brief="A motion trailer for the film that makes each audience want to see it: true to the film's tone and "
               "story, with its real credits on the end card."),
    # Dynamic creative: one product, an ad per audience, each from that audience's buyers. Its evidence method is
    # `docs/internal/product-ad-probe.md`.
    Card("product-ad-qloo", "Product ad for audiences", "video", "Video · 15 seconds, one ad per audience (Qloo)",
         (Route("motion_qloo", "Taste-grounded ad"),),
         image="product-ad.webp", alt="A matte black can of cold brew coffee beside its name and call to action",
         extras=(*PRODUCT, TRAILER_LENGTH), audiences=True, types=("product",),
         brief="A motion ad for the product that makes each audience want to try it, built on its one claim and "
               "ending on the call to action."),
    Card("product-poster-qloo", "Product poster for audiences", "image", "Image · one print ad per audience (Qloo)",
         (Route("poster_qloo", "Taste-grounded print ad"),),
         image="product-poster.webp", alt="Two Nightshift cold brew print ads: a dark bar shelf for Berlin, a pale arc for Seoul",
         extras=PRODUCT, audiences=True, types=("product",),
         brief="A print ad for the product for each audience, built on its one claim and ending on the call to "
               "action."),
    Card("film-poster-qloo", "Film poster for audiences", "image", "Image · one poster per audience (Qloo)",
         (Route("poster_qloo", "Taste-grounded poster"),),
         image="film-poster.webp", alt="A Be Cool one-sheet: a gold record label behind crosshairs over a black vinyl",
         extras=(FILM, Extra("headline", "Headline", placeholder="The words that must appear", long=True, rows=2)),
         audiences=True, types=("film",),
         brief="A one-sheet poster for the film for each audience: true to its tone, with the title and its real "
               "billing."),
)

BY_SLUG: dict[str, Card] = {c.slug: c for c in CARDS}

#: The home page's three rows of cards, in this order: the workflows a visitor can start, the entries' first.
HOME = ("film-trailer", "product-ad", "report-explainer", "startup-pitch", "maths-explainer", "product-poster",
        "film-poster", "infographic", "logo")

#: Cards by a former slug, for a link made before the change. A *request* recorded under one is placed by its workflow
#: and type instead (`card_for`), since `poster` covered both of the cards that replaced it.
RENAMED: dict[str, str] = {"poster": "film-poster"}

#: The type an untyped project of a workflow is, as the CLI defaults it: an untyped poster is a film poster. Named rather
#: than read off the card order, which named every untyped poster "Product poster" once that card moved first (2026-10-30).
UNTYPED_IS: dict[str, str] = {"poster": "film", "poster_qloo": "film"}


def card_for(workflow: str, type_: str = "") -> Card | None:
    """
    Which card a project belongs to, for one made on the developer page or by another host: a project made here records
    its card, so this is only the fallback. **The type decides between cards of one workflow** — a `motion` project
    typed `product` is a product ad, `report` a report explainer, untyped a film trailer — where taking the workflow's
    first card named every one of them "Film trailer". A former workflow name is read as its current one.
    """
    workflow = projects.canonical(workflow)
    type_ = type_ or UNTYPED_IS.get(workflow, "")
    cards = [c for c in CARDS if any(r.workflow == workflow for r in c.routes)]
    return (next((c for c in cards if type_ and c.fixed_type == type_), None)
            or next((c for c in cards if not type_ and c.types == ()), None)
            or next((c for c in cards if c.types is None or type_ in c.types), None)
            or next(iter(cards), None))


def types_for(card: Card, workflow: str) -> list[tuple[str, str]]:
    """The styles offered for this route, as (value, label), from the closed set in `projects` narrowed by the card."""
    return [(t, card.type_labels.get(t, t.capitalize())) for t in projects.WORKFLOWS.get(workflow, ())
            if card.types is None or t in card.types]


def slugify(title: str) -> str:
    """A project id from a title: what `projects.VALID_ID` accepts, readable, never empty."""
    slug = re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-")[:48].strip("-")
    return slug or "request"


@dataclass
class Commission:
    """A visitor's request, checked, ready to become a project."""

    card: Card
    title: str
    workflow: str
    kind: str
    sizes: list[Size]
    formats: list[Format]
    extras: list[tuple[str, str]]
    brief: str
    audiences: list[tuple[str, str, str]] = field(default_factory=list)   # (place, age band code or "", language code or "")
    #: Design techniques the visitor asked for and ruled out, by id (`techniques.parse`). Not in the brief: they are the
    #: director's settings, not the client's words, and reach the CLI as `--request` / `--deny`.
    request: list[str] = field(default_factory=list)
    deny: list[str] = field(default_factory=list)

    @property
    def narrated(self) -> bool:
        return (NARRATION.label, "Enabled") in self.extras

    def audience_lines(self) -> list[str]:
        """'Audience A: Tokyo, aged 45 to 54, in Japanese', as the poster and trailer briefs name them. English unsaid."""
        words, tongues = dict(AGE_BANDS), dict(LANGUAGES)
        return [f"Audience {chr(65 + i)}: {place}" + (f", aged {words[age]}" if age else "")
                + (f", in {tongues[lang]}" if lang else "")
                + (f" (its text {EYE_NOTE}: the read-back cannot read {tongues[lang]})" if lang in checked_by_eye() else "")
                for i, (place, age, lang) in enumerate(self.audiences)]

    def brief_text(self, files: list[str]) -> str:
        """The brief as the agent will read it: the visitor's words, then what to deliver.

        The delivery block is plain Markdown after a rule, so it reads as part of the commission and
        survives the sanitiser untouched. It asks; nothing downstream enforces it.
        """
        lines = [self.brief.strip(), "", "---", "", "## What to deliver", "",
                 f"- Request: {self.title}",
                 f"- Workflow: {self.card.title}"]
        lines += [f"- {line}" for line in self.audience_lines()]
        if len(self.audiences) > 1:
            lines.append(f"- One {'ad' if self.kind == 'product' else self.card.piece} per audience")
        lines += ["- Sizes: " + "; ".join(f"{s.label} {s.detail}" for s in self.sizes),
                  "- Formats: " + ", ".join(f.ext + (" (a silent preview beside the narrated .mp4)"
                                                     if f.ext == ".webp" and self.card.kind == "video" and self.narrated
                                                     else "") for f in self.formats)]
        for label, value in self.extras:
            asked = [q.strip() for q in re.split(r"[;\n]", value) if q.strip()] if label == "Their questions" else []
            lines += [f"- {label}:"] + [f"  {i}. {q}" for i, q in enumerate(asked, 1)] if asked else [f"- {label}: {' '.join(value.split())}"]
        clips = [f for f in files if f.lower().endswith(".mp4")]
        others = [f for f in files if f not in clips]
        if others:
            lines.append("- Files supplied, in documents/: " + ", ".join(others))
        if clips:
            lines.append("- Reference video, in documents/ (watch with Video.ask): " + ", ".join(clips))
        return "\n".join(lines) + "\n"

    def record(self) -> dict:
        """What the request page shows about the request, written beside the project as request.json."""
        return {"title": self.title, "card": self.card.slug, "workflow": self.workflow, "type": self.kind,
                "sizes": [{"label": s.label, "width": s.width, "height": s.height} for s in self.sizes],
                "formats": [f.ext for f in self.formats],
                "extras": [{"label": k, "value": v} for k, v in self.extras],
                "audiences": [{"letter": chr(65 + i), "place": p, "age": a, "ageLabel": dict(AGE_BANDS).get(a, ""),
                               "language": lang, "languageLabel": dict(LANGUAGES).get(lang, "") if lang else "",
                               "readBack": lang not in checked_by_eye()}
                              for i, (p, a, lang) in enumerate(self.audiences)],
                "techniques": {"request": list(self.request), "deny": list(self.deny)}}


def parse_audiences(places: list[str], ages: list[str], languages: list[str] | None = None) -> list[tuple[str, str, str]]:
    """(place, age band, language) from the form's parallel lists, blank rows dropped. Raises `StudioError`."""
    bands, tongues = {code for code, _ in AGE_BANDS}, {code for code, _ in LANGUAGES}
    languages = languages or []
    # Too many is said first: it is the whole request's problem, and no single place's.
    if (given := sum(1 for p in places if (p or "").strip())) > MAX_AUDIENCES:
        raise StudioError(f"That is {given} audiences; a request takes up to {MAX_AUDIENCES}.")
    found = []
    for i, place in enumerate(places):
        place = " ".join((place or "").split())
        age = (ages[i] if i < len(ages) else "") or ""
        lang = (languages[i] if i < len(languages) else "") or ""
        if not place:
            if age or lang:
                raise StudioError("Each audience needs a place, such as a city or a country.")
            continue
        place = resolve_place(place)
        if age and age not in bands:
            raise StudioError("Choose an age band from the list.")
        if lang not in tongues:
            raise StudioError("Choose a language from the list.")
        found.append((place, age, lang))
    if len(found) > MAX_AUDIENCES:
        raise StudioError(f"That is {len(found)} audiences; a request takes up to {MAX_AUDIENCES}.")
    return found


def parse(card_slug: str, title: str, route: str, kind: str, sizes: list[str], custom: str,
          formats: list[str], extras: dict[str, str], brief: str,
          places: list[str] | None = None, ages: list[str] | None = None, languages: list[str] | None = None) -> Commission:
    """Checks a composed request against the card's closed sets. Raises `StudioError` with a reason."""
    card = BY_SLUG.get(card_slug)
    if card is None:
        raise StudioError("That workflow is not one the studio offers.")
    if card.needs_taste and not taste_available():
        raise StudioError(f"A {card.title.lower()} is designed from each audience's taste, and {TASTE_OFF}")

    title = " ".join((title or "").split())
    if not title:
        raise StudioError("Give the request a name, so you can find it again.")
    if len(title) > MAX_TITLE:
        raise StudioError(f"That name is {len(title)} characters; keep it under {MAX_TITLE}.")

    try:
        chosen = card.routes[int(route or 0)]
    except (ValueError, IndexError):
        raise StudioError("Choose one of the offered routes.") from None

    offered = {t for t, _ in types_for(card, chosen.workflow)}
    if kind and kind not in offered:
        raise StudioError(f"{card.title} does not offer that {card.style_label.lower()}.")
    kind = card.fixed_type or kind or card.default_type

    presets = {s.key: s for s in SIZES[card.kind]}
    picked = [presets[k] for k in dict.fromkeys(sizes) if k in presets]
    if len(picked) != len(set(sizes)):
        raise StudioError("One of the sizes is not a preset; use the custom size for anything else.")
    if custom.strip():
        m = re.fullmatch(r"\s*(\d{2,5})\s*[x×]\s*(\d{2,5})\s*", custom)
        if not m or not all(16 <= int(v) <= MAX_SIDE for v in m.groups()):
            raise StudioError(f"A custom size is width × height in pixels, each between 16 and {MAX_SIDE}.")
        picked.append(Size("Custom", int(m.group(1)), int(m.group(2))))
    if not picked:
        raise StudioError("Choose at least one size.")

    allowed = {f.ext: f for f in FORMATS[card.kind]}
    chosen_formats = [allowed[f] for f in dict.fromkeys(formats) if f in allowed]
    if len(chosen_formats) != len(set(formats)):
        raise StudioError(f"{card.title} cannot be delivered in one of those formats.")
    if not chosen_formats:
        raise StudioError("Choose at least one format.")

    pairs = []
    for extra in card.extras:
        raw = extras.get(extra.key) or extra.default
        # A long field keeps its lines (one question a line); each line, and a short field, is one run of words.
        value = ("\n".join(" ".join(line.split()) for line in raw.splitlines() if line.strip()) if extra.long
                 else " ".join(raw.split()))
        limit = MAX_LONG_EXTRA if extra.long else MAX_EXTRA
        if extra.options and value not in extra.options:
            raise StudioError(f"Choose a {extra.label.lower()} from the list.")
        if len(value) > limit:
            raise StudioError(f"{extra.label} is {len(value)} characters; keep it under {limit}.")
        if extra.subject and not value:
            raise StudioError(f"Give the {extra.label.lower()}: {extra.placeholder[:1].lower()}{extra.placeholder[1:]}.")
        if value:
            pairs.append((extra.label, value))
    # A narrated film is an MP4 whatever was ticked: a WebP alone would ask for the voice to be dropped.
    if card.kind == "video" and (NARRATION.label, "Enabled") in pairs and ".mp4" not in {f.ext for f in chosen_formats}:
        chosen_formats.insert(0, allowed[".mp4"])

    audiences = parse_audiences(places or [], ages or [], languages) if card.audiences else []
    if card.audiences and not audiences and not card.audiences_optional:
        raise StudioError(f"Say who the {card.title.lower()} is for: a place and, if it matters, an age band.")

    if not (brief or "").strip():
        raise StudioError("A brief is the one thing the studio cannot infer. Say what you want made.")

    return Commission(card, title, chosen.workflow, kind, picked, chosen_formats, pairs, brief, audiences)
