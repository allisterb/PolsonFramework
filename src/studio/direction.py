"""*Why it looks like this*: a run's `direction.md`, read as choices and the evidence behind them.

The poster workflow writes one file, `direction.md`, in which every visual choice sits beside its evidence and
the kind of that evidence — Qloo, research, the brief, or an answer the agent assumed. That file is what a judge
of a taste-grounded poster needs, and as Markdown it is a wall of pipes. This reads its tables into rows the run
page can lay out, and links each `t5`-style reference to the Taste query the run actually made, so a Qloo line
shows the request behind it rather than asking to be taken on trust (design doc §4.6).

**The file is agent-written and therefore untrusted.** Nothing here interprets it beyond splitting tables: cells
come back as plain strings, and the page escapes them before it formats anything.
"""

from __future__ import annotations

import re
import unicodedata
from collections.abc import Iterable
from typing import Any
from urllib.parse import parse_qs, urlsplit

from . import catalogue

#: A direction file is a page of tables, not a book. Anything longer is truncated, said so, and still served.
MAX_DIRECTION = 200_000

#: Kinds the page has a badge for, by the stem that names each, in the order a reader weighs them. A stem
#: rather than a word, because the agent writes "Assumed", "assumption", "Judgement on the render", "judgment".
KINDS = (("qloo", "qloo"), ("research", "research"), ("brief", "brief"), ("document", "document"),
         ("comput", "computed"), ("rule", "rule"), ("assum", "assumed"),
         ("check", "check"), ("judg", "judgement"), ("workaround", "workaround"))

#: A Taste result's handle as the agent cites it: `t5`, `t12`. Bounded so `t2026` in prose is not one.
TASTE_REF = re.compile(r"(?<![\w-])t(\d{1,3})(?![\w-])")

#: A section heading, `##` to `####`; the document's own `#` title is not a section.
_HEADING = re.compile(r"^(#{2,4})\s+(.*\S)\s*$")

#: The row under a table's header: dashes and optional alignment colons between pipes.
_SEPARATOR = re.compile(r"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$")


def cells(line: str) -> list[str]:
    """A Markdown table row's cells, honouring `\\|` and pipes inside backticks."""
    out, cell, code, i = [], [], False, 0
    text = line.strip()
    if text.startswith("|"):
        text = text[1:]
    if text.endswith("|") and not text.endswith("\\|"):
        text = text[:-1]
    while i < len(text):
        ch = text[i]
        if ch == "\\" and i + 1 < len(text) and text[i + 1] == "|":
            cell.append("|")
            i += 2
            continue
        if ch == "`":
            code = not code
        if ch == "|" and not code:
            out.append("".join(cell).strip())
            cell = []
        else:
            cell.append(ch)
        i += 1
    out.append("".join(cell).strip())
    return out


def kinds(cell: str) -> list[dict[str, str]]:
    """The kinds a row's last cell names — `Qloo / Brief` is two — each as a badge key and its words."""
    found = []
    for part in re.split(r"\s*[/,;+]\s*", cell):
        words = part.strip()
        if not words:
            continue
        low = words.lower()
        key = next((key for stem, key in KINDS if stem in low), "other")
        found.append({"key": key, "label": words})
    return found


def parse(text: str) -> list[dict[str, Any]]:
    """Sections of rows from every table whose last column is *Kind*, under the heading it sits beneath.

    A section also carries its **lead** — the first paragraph under the heading, before the table — because
    that is where the agent says which profile a section rests on, and a row out of that context loses it.
    Tables without a *Kind* column, and bullet lists, are left to the document itself, which the page links.
    """
    sections: list[dict[str, Any]] = []
    title, parent, lead, lead_open = "", "", "", True
    # The headings above the current one, by level: a `### Choices` under `## Film A: Tokyo` is Film A's choices.
    above: dict[int, str] = {}
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        heading = _HEADING.match(line)
        if heading:
            level = len(heading.group(1))
            above = {k: v for k, v in above.items() if k < level}
            parent = above[max(above)] if above else ""
            above[level] = heading.group(2)
            title, lead, lead_open = heading.group(2), "", True
            i += 1
            continue

        if line.lstrip().startswith("|") and i + 1 < len(lines) and _SEPARATOR.match(lines[i + 1].strip()):
            header = [c.lower() for c in cells(line)]
            i += 2
            body = []
            while i < len(lines) and lines[i].lstrip().startswith("|"):
                body.append(cells(lines[i]))
                i += 1
            lead_open = False
            if len(header) >= 3 and header[-1] == "kind":
                rows = [_row(r) for r in body if len(r) >= 2 and any(r)]
                if rows:
                    sections.append({"title": title, "parent": parent if title else "", "lead": lead,
                                     "leadRefs": refs(lead), "columns": header[:-1], "rows": rows})
            continue

        stripped = line.strip()
        if lead_open and stripped and not stripped.startswith(("-", "*", ">", "|")):
            lead = f"{lead} {stripped}".strip() if lead else stripped
        elif lead_open and not stripped and lead:
            lead_open = False
        i += 1
    return sections


def _row(cells_: list[str]) -> dict[str, Any]:
    choice, kind = cells_[0], cells_[-1] if len(cells_) >= 3 else ""
    evidence = " — ".join(c for c in cells_[1:-1] if c) if len(cells_) >= 3 else cells_[1]
    # Handles from the kind cell too — "Qloo (t2, t6)" is how agents often cite — as the engine's check reads them.
    return {"choice": choice, "evidence": evidence, "kinds": kinds(kind), "refs": refs(f"{choice} {evidence} {kind}")}


def refs(text: str) -> list[str]:
    """The Taste handles a cell cites, in order of first mention: `t5`, `t9`."""
    seen: list[str] = []
    for match in TASTE_REF.finditer(text):
        handle = f"t{int(match.group(1))}"
        if handle not in seen:
            seen.append(handle)
    return seen


def queries(events: Iterable[dict[str, Any]], since: str | None = None) -> dict[str, dict[str, Any]]:
    """The run's Taste queries by handle, from the server's record: what each was for and what it asked.

    Scoped to the run as the curve is, since a project's record outlives any one run and a handle like `t5`
    is reused by the next session. The request is Qloo's endpoint and canonical query — never the key, which the
    server never records.
    """
    found: dict[str, dict[str, Any]] = {}
    for event in events:
        if event.get("type") != "taste.query" or not event.get("id"):
            continue
        if since and (event.get("ts") or "") < since:
            continue
        found[event["id"]] = {
            "op": event.get("op") or "",
            "label": event.get("label") or "",
            "request": event.get("request") or "",
            "requests": event.get("requests"),
            "fromCache": bool(event.get("fromCache")),
        }
    return found


#: `**comparePace (t1 vs t2):** *"…"*` — the summary is whatever sits in the quotes after the call's name.
_COMPARE = re.compile(r"comparePace[^\n]*?[\"“](.+?)[\"”]", re.S)

#: `t1 (audience A)` in a summary, which a client reads better as `Audience A`.
_HANDLE = re.compile(r"\bt\d{1,3}\s*\((audience\s+[A-Z])\)", re.I)

#: An italic title in a cell: `*The Farewell*`, never `**bold**`.
_ITALIC = re.compile(r"(?<!\*)\*([^*\n]{1,80})\*(?!\*)")


#: Where a run's list of fast or slow titles begins: `Fast titles:`, `the fast list is led by`, `slow-paced favourites`.
_PACE_LIST = {kind: re.compile(rf"\b{kind}(?:[- ]paced)?\s+(?:titles|list|favou?rites)\b", re.I) for kind in ("fast", "slow")}

#: `Audience A`, `Film B`: the letter that ties a direction heading to the profile fetched for it.
_LETTER = re.compile(r"\b(?i:audience|film|poster|piece)\s+([A-Z])\b")


def learned(text: str, events: Iterable[dict[str, Any]], since: str | None = None) -> dict[str, Any]:
    """What a run learned from its audiences, for a client-facing page.

    Each audience profile fetched — who it is, how many distinctive titles it had, and its pace counts — comes from the
    run's `taste.query` events, so it shows as soon as the query is made, whatever the direction's wording. The titles
    each pace rests on, and the `comparePace` summary from "Between the audiences", are the agent's own, as it wrote them
    into `direction.md`. Plain words for a client, so no request line and no raw payload. (Qloo's terms forbid *storing*
    responses in a public repository; showing a run's results as evidence, with their source and limits, is what the
    hackathon kit asks for — `docs/SAFE_USE.md`.)
    """
    text = text[:MAX_DIRECTION]
    profiles: dict[str, dict[str, Any]] = {}
    for e in events:
        if e.get("type") == "taste.query" and e.get("op") == "profile" and not (since and (e.get("ts") or "") < since):
            profiles[e.get("label") or e.get("id") or ""] = e  # a profile fetched again replaces the earlier one

    compare = ""
    section = _section(text, "between the audiences")
    if section and (m := _COMPARE.search(section)):
        compare = _HANDLE.sub(lambda h: h.group(1)[0].upper() + h.group(1)[1:], " ".join(m.group(1).split()))

    titles = _pace_titles(text)
    audiences = []
    for label, e in profiles.items():
        letter = (m.group(1) if (m := _LETTER.search(label)) else "")
        heading = next((h for h in titles if letter and (m := _LETTER.search(h)) and m.group(1) == letter), None)
        if heading is None and len(profiles) == 1 and len(titles) == 1:
            heading = next(iter(titles))
        pace = e.get("pace") if isinstance(e.get("pace"), dict) else None
        audiences.append({"name": label[:1].upper() + label[1:], "who": _who(e.get("audience")),
                          "distinctive": e.get("distinctive"), "pace": _pace(pace),
                          **(titles.pop(heading) if heading is not None else {"fast": [], "slow": []})})
    # Titles under a heading no profile matched are still the run's words; they are shown under that heading.
    audiences += [{"name": h, "who": "", "distinctive": None, "pace": None, **t} for h, t in titles.items()]
    return {"compare": compare, "audiences": audiences}


def _who(audience: Any) -> str:
    """`Berlin, 30 to 34`: an audience as a profile event records it, in a reader's words."""
    if not isinstance(audience, dict):
        return ""
    ages = dict(catalogue.AGE_BANDS)
    parts = [audience.get("place"), ages.get(audience.get("age") or "", audience.get("age")), audience.get("gender")]
    return ", ".join(str(p) for p in parts if p)


def _pace(pace: dict[str, Any] | None) -> dict[str, Any] | None:
    """A profile's pace counts, with the fast share as a whole percentage; None when the profile carried none."""
    if not pace or not isinstance(pace.get("n"), int) or not pace["n"]:
        return None
    fast, slow = pace.get("fast") or 0, pace.get("slow") or 0
    return {"fast": fast, "slow": slow, "n": pace["n"],
            "share": round(100 * fast / (fast + slow)) if fast + slow else None}


def _pace_titles(text: str) -> dict[str, dict[str, list[str]]]:
    """The fast and slow titles a direction names, by the `##` heading they sit under.

    Two forms are read: a table row whose first cell is **Pace**, and the body under a `###` or `####` heading
    naming pace. nightshift5 wrote the second, in prose ("The fast list is led by *Uncut Gems*, …"), and a reader of
    the first alone showed nothing; neither form is set by the templates, so both are taken.
    """
    places: list[tuple[str, str]] = []
    heading, under = "", None
    for line in text.splitlines():
        if h := _HEADING.match(line):
            if len(h.group(1)) == 2:
                heading = h.group(2).strip("*_ ")
            under = len(places) if len(h.group(1)) > 2 and re.search(r"\bpace\b", h.group(2), re.I) else None
            if under is not None:
                places.append((heading, ""))
            continue
        if under is not None:
            places[under] = (heading, places[under][1] + "\n" + line)
        elif line.lstrip().startswith("|") and len(row := cells(line)) >= 2 and row[0].strip("*_ ").lower().startswith("pace"):
            places.append((heading, row[1]))

    found: dict[str, dict[str, list[str]]] = {}
    for heading, body in places:
        entry = found.setdefault(heading, {"fast": [], "slow": []})
        for kind in ("fast", "slow"):
            entry[kind] = entry[kind] or _titles_after(body, kind)
    return {h: t for h, t in found.items() if t["fast"] or t["slow"]}


def _section(text: str, name: str) -> str:
    """The body under the first `##`–`####` heading containing `name`, up to the next heading of any level."""
    out, inside = [], False
    for line in text.splitlines():
        if _HEADING.match(line) or line.startswith("# "):
            if inside:
                break
            inside = name in line.lower()
            continue
        if inside:
            out.append(line)
    return "\n".join(out)


def _titles_after(cell: str, kind: str) -> list[str]:
    """The italic titles in the first `kind` ("fast" or "slow") list that names any: 'Fast titles: *A*, *B*.' and
    'the fast list is led by *A*, *B*; …' both give [A, B]."""
    for start in _PACE_LIST[kind].finditer(cell):
        rest = cell[start.end():]
        # The list ends at a full stop or semicolon *between* titles, never at one inside a title: *Dr. Strangelove*
        # would otherwise end it at "Dr." and lose itself and every title after it.
        titles, pos = [], 0
        for m in _ITALIC.finditer(rest):
            if re.search(r"[.;](\s|$)", rest[pos:m.start()]):
                break
            titles.append(m.group(1))
            pos = m.end()
        if titles:
            return titles[:8]
    return []


def explain(text: str, events: Iterable[dict[str, Any]], since: str | None = None) -> dict[str, Any]:
    """The panel's whole payload: sections, the queries their rows cite, the reference check, and whether the file
    was cut short."""
    events = list(events)
    truncated = len(text) > MAX_DIRECTION
    sections = parse(text[:MAX_DIRECTION])
    cited = {h for s in sections for r in s["rows"] for h in r["refs"]} | {h for s in sections for h in s["leadRefs"]}
    known = queries(events, since)
    check = attach_check(sections, events, since)
    return {
        "sections": sections,
        "queries": {h: q for h, q in known.items() if h in cited},
        "referenceCheck": check,
        "map": evidence_map(sections, known),
        "truncated": truncated,
    }


#: A tag as the workflows write it: `**"Glitzy"**`, or `"Glitzy" (item 10)` / `(10)` / `(t6 item 10)`, bold or not, and a
#: list sharing one item, `"Raw", "Matte" and "Glitzy" (item 10)`, whose quoted words are group 2 together, with the
#: entity between as an italic title if it is there: `"Cold drip espresso" at *DoubleEye* (t8, item 8)`.
#: The same pattern as the engine's `TasteReferences.TagRef`, so the map and the check see the same tags.
_TAG_REF = re.compile(r'\*\*["“]([^"”\n]{1,40})["”]\*\*|((?:["“][^"”\n]{1,40}["”]\**\s*(?:,\s*(?:and\s+|&\s*)?|and\s+|&\s*)){0,8}'
                      r'["“][^"”\n]{1,40}["”])\**\s*(?:(?:at|on|from|in|of)\s+\*[^*\n]{1,80}\*\s*)?'
                      r'\((?:`?t\d{1,3}`?,?\s*)?(?:item\s*)?\d+\)')

#: One quoted tag of a `_TAG_REF` list.
_QUOTED = re.compile(r'["“]([^"”\n]{1,40})["”]')

_TRAILING_YEAR = re.compile(r"\s*\((?:19|20)\d{2}\)\s*$")


def ref_key(name: Any) -> str:
    """A title or tag as compared: no accents, case, punctuation, leading article or trailing year — the engine's
    `TasteReferences.Key`, so a verdict recorded there finds its reference here."""
    text = _TRAILING_YEAR.sub("", str(name or "").strip())
    text = "".join(c for c in unicodedata.normalize("NFD", text) if unicodedata.category(c) != "Mn")
    key = " ".join("".join(c.lower() if c.isalnum() else " " for c in text).split())
    return re.sub(r"^(?:the|a|an)\s+", "", key)


def row_references(row: dict[str, Any]) -> list[dict[str, str]]:
    """The titles (in italics, in the choice or the evidence) and tags (quoted with an item number, in the evidence)
    a row cites, in order, each once — read as `Taste.checkReferences()` reads them."""
    found: list[dict[str, str]] = []
    seen: set[str] = set()
    for match in _ITALIC.finditer(f"{row['choice']} {row['evidence']}"):
        title = match.group(1).strip()
        if title and title[0] not in "\"“'‘" and any(c.isalpha() for c in title):
            key = f"title:{ref_key(title)}"
            if key not in seen:
                seen.add(key)
                found.append({"kind": "title", "text": title, "key": key})
    # Backticks are formatting, never part of a tag, and the engine drops them before reading (poster6): read with
    # them, `"Raw Textures"` (t6, item 32) was a verified reference the map drew as no reference at all (nightshift5).
    for match in _TAG_REF.finditer(row["evidence"].replace("`", "")):
        for tag in [match.group(1)] if match.group(1) else _QUOTED.findall(match.group(2)):
            tag = tag.strip()
            key = f"tag:{ref_key(tag)}"
            if tag and key not in seen:
                seen.add(key)
                found.append({"kind": "tag", "text": tag, "key": key})
    return found


#: Worst first: an edge that two rows reach in different states shows the worse one.
_SEVERITY = {"unverified": 0, "unchecked": 1, "verified": 2}


def evidence_map(sections: list[dict[str, Any]], known: dict[str, dict[str, Any]]) -> dict[str, Any]:
    """How the run used Qloo, as a graph: query → reference → choice → the piece it is for.

    Built from the Qloo rows of `direction.md` (the references the agent cited, in its words), the `taste.query`
    events (each query's op and label) and the reference check (whether each reference was returned by the query
    it cites). Never a raw Qloo payload. Every edge carries a state — **verified**, **unverified** or
    **unchecked** — so the picture shows what the check showed. A reference found in another query than the row
    cites hangs from the query that returned it; one never found hangs from the queries the row cites.
    """
    queries: dict[str, dict[str, Any]] = {}
    refs: dict[str, dict[str, Any]] = {}
    choices: list[dict[str, Any]] = []
    pieces: list[dict[str, Any]] = []
    edges: dict[tuple[str, str], str] = {}

    def edge(a: str, b: str, state: str) -> None:
        old = edges.get((a, b))
        if old is None or _SEVERITY[state] < _SEVERITY[old]:
            edges[(a, b)] = state

    def query(handle: str) -> str:
        if handle not in queries:
            q = known.get(handle) or {}
            queries[handle] = {"id": handle, "op": q.get("op") or "", "label": q.get("label") or "",
                               "known": handle in known}
        return handle

    for si, section in enumerate(sections):
        piece_id = f"s{si}"
        rows = [r for r in section["rows"] if any(k["key"] == "qloo" for k in r["kinds"])]
        if not rows:
            continue
        pieces.append({"id": piece_id, "title": section["title"] or "Direction", "parent": section.get("parent") or ""})
        for row in rows:
            check = row.get("check") or {}
            state = check.get("state") or "unchecked"
            choice_id = f"c{len(choices)}"
            choices.append({"id": choice_id, "text": row["choice"], "piece": piece_id, "state": state})
            edge(choice_id, piece_id, state)
            handles = row["refs"] or section["leadRefs"]

            cited = row_references(row)
            if not cited:
                for h in handles:
                    edge(query(h), choice_id, state)
                continue
            for ref in cited:
                verdict = (check.get("refs") or {}).get(ref["key"])
                ref_state = ("verified" if verdict["found"] else "unverified") if verdict else \
                            ("unverified" if state == "unverified" and check.get("problems") else "unchecked")
                ref_id = refs.setdefault(ref["key"], {"id": f"r{len(refs)}", "kind": ref["kind"], "text": ref["text"]})["id"]
                edge(ref_id, choice_id, ref_state)
                sources = [verdict["in"]] if verdict and verdict.get("found") and verdict.get("in") else handles
                for h in sources:
                    edge(query(h), ref_id, ref_state)

    order = sorted(queries, key=lambda h: int(h[1:]))
    return {
        "queries": [queries[h] for h in order],
        "refs": list(refs.values()),
        "choices": choices,
        "pieces": pieces,
        "edges": [{"from": a, "to": b, "state": s} for (a, b), s in edges.items()],
    }


def taste_views(text: str, events: Iterable[dict[str, Any]], since: str | None = None) -> dict[str, Any]:
    """What the studio's Audiences, Contrast and Choices views draw, from the run record and `direction.md`.

    **audiences**: each profile the run fetched, as its `taste.query` event records it: label, who and where, the top
    of each domain's distinctive list with ranks and pace, and the pace counts. A run from before profiles recorded
    these has only the label and counts, and the view says so.
    **lists**: each `insights` query asked about an audience, with the tags the direction cites on its entities.
    **contrasts**: each `Taste.contrast` the run made, the latest per pair.
    **map** and **referenceCheck**: the evidence map and the reference check, as the run page has them.

    The titles here are results the run used as evidence, shown with their source, as Qloo's hackathon kit asks
    (`docs/SAFE_USE.md`); the run record lives in the project, never the repository. No request line, key or raw
    payload.
    """
    events = list(events)
    scoped = [e for e in events if not (since and (e.get("ts") or "") < since)]
    # The latest profile per label: a session that restarts fetches its audiences again (free, from the cache), and
    # each fetch is recorded — one audience, not two.
    latest: dict[str, dict[str, Any]] = {}
    for e in scoped:
        if e.get("type") == "taste.query" and e.get("op") == "profile":
            latest.pop(str(e.get("label") or e.get("id")), None)
            latest[str(e.get("label") or e.get("id"))] = {
                k: e.get(k) for k in ("id", "label", "audience", "domains", "pace", "distinctive", "fromCache")}
    audiences = list(latest.values())
    contrasts: dict[tuple[str, str], dict[str, Any]] = {}
    for e in scoped:
        if e.get("type") == "taste.contrast" and isinstance(e.get("a"), dict) and isinstance(e.get("b"), dict):
            contrasts[(str(e["a"].get("id")), str(e["b"].get("id")))] = {
                k: e.get(k) for k in ("a", "b", "domains", "pace", "summary")}
    explained = explain(text, events, since) if text else {"map": None, "referenceCheck": None}
    return {"audiences": audiences, "lists": audience_lists(scoped, explained["map"]), "contrasts": list(contrasts.values()),
            "map": explained["map"], "referenceCheck": explained["referenceCheck"],
            "verification": verification(scoped, explained["referenceCheck"])}


def audience_lists(events: list[dict[str, Any]], evidence: dict[str, Any] | None) -> list[dict[str, Any]]:
    """The run's `insights` queries asked about an audience — a place or an age in the request — the latest per label.

    Each with who and what was asked, and its entities by the item number a direction cites, each carrying only the tags
    the direction cites from that query (so `"Raw Textures"` (t6, item 32) shows on item 32, and on any other item of
    t6 that carries it). An entity's other tags are not sent: the page shows what the run used, not Qloo's answer. A
    run from before 2026-10-26 recorded no entities, and the list has `entities: None`.
    """
    cited: dict[str, set[str]] = {}
    if evidence:
        tags = {r["id"]: ref_key(r["text"]) for r in evidence.get("refs") or [] if r.get("kind") == "tag"}
        for edge in evidence.get("edges") or []:
            if edge["to"] in tags:
                cited.setdefault(edge["from"], set()).add(tags[edge["to"]])

    latest: dict[str, dict[str, Any]] = {}
    for e in events:
        if e.get("type") != "taste.query" or e.get("op") != "insights":
            continue
        q = {k: v[0] for k, v in parse_qs(urlsplit(str(e.get("request") or "")).query).items()}
        place = q.get("signal.location.query") or q.get("filter.location.query")
        who = {"place": place, "age": q.get("signal.demographics.age"), "gender": q.get("signal.demographics.gender")}
        if not any(who.values()):
            continue
        keys = cited.get(str(e.get("id")), set())
        entities = e.get("entities")
        label = str(e.get("label") or e.get("id"))
        latest.pop(label, None)
        latest[label] = {
            "id": e.get("id"), "label": e.get("label"), "audience": who, "n": e.get("results"),
            "type": (q.get("filter.type") or "").strip(),
            "entities": None if not isinstance(entities, list) else [
                {"rank": x.get("rank"), "name": x.get("name"), "cited": _cited_once(x.get("tags") or [], keys)}
                for x in entities if isinstance(x, dict)],
        }
    return list(latest.values())


def _cited_once(tags: list[Any], keys: set[str]) -> list[str]:
    """The cited tags among an entity's, in its order, once each: Qloo can give one tag under two types ("Nightlife")."""
    out, seen = [], set()
    for tag in tags:
        key = ref_key(tag)
        if key in keys and key not in seen:
            seen.add(key)
            out.append(str(tag))
    return out


def verification(events: list[dict[str, Any]], reference_check: dict[str, Any] | None) -> dict[str, Any]:
    """What the studio's Verification view draws: how the run checked what it put on screen.

    **films**: each film's latest `Verify.film` — every text it set, the seconds it was on screen, where it came from and
    whether the watcher read it — with its strays as the agent settled them after looking at their frames. **A stray not
    yet looked at is counted, never shown**: the watcher misreads, and a reading is not a finding until its frame has
    been seen. **images**: each still's latest `Verify.image`, the same with each text's box on the image in place of
    its seconds, and a crop in place of a frame. **derivations**: each derivation's latest `Verify.derivation`, step by
    step, with every pair's verdict and, where the watcher read the steps, the steps as read. **claims**: each claim's
    latest `Verify.claim`, with the code Nemotron wrote to check it and what each check found. **checks**: the run's
    `Stage.check`s, the latest per claim. **references**: the Qloo reference check. **documents**: the documents the run
    read, and with which model.
    """
    films = _read_backs(events, "film", "video", ("duration",))
    images = _read_backs(events, "image", "image", ("source", "width", "height"))
    derivations: dict[str, dict[str, Any]] = {}
    for e in events:
        if e.get("type") == "derivation.verify" and e.get("name") and isinstance(e.get("steps"), list):
            derivations.pop(e["name"], None)
            derivations[e["name"]] = {k: e.get(k) for k in ("name", "steps", "pairs", "counts", "passed", "ts")}
    claims: dict[str, dict[str, Any]] = {}
    for e in events:
        if e.get("type") == "claim.verify" and e.get("name") and e.get("claim"):
            claims.pop(e["name"], None)
            claims[e["name"]] = {k: e.get(k) for k in ("name", "claim", "context", "verdict", "passed", "checks", "code", "error",
                                                       "model", "ts")}

    checks: dict[str, dict[str, Any]] = {}
    for e in events:
        if e.get("type") == "check" and e.get("claim"):
            checks.pop(e["claim"], None)
            checks[e["claim"]] = {k: e.get(k) for k in ("claim", "passed", "detail", "judged", "accepted", "reason", "stage")}

    documents: dict[str, dict[str, Any]] = {}
    for e in events:
        if e.get("type") == "document.read" and e.get("success") and e.get("descriptor"):
            documents[e["descriptor"]] = {"document": e["descriptor"], "model": e.get("model"),
                                          "reads": documents.get(e["descriptor"], {}).get("reads", 0) + 1}

    return {"films": films, "images": images, "derivations": list(derivations.values()), "claims": list(claims.values()),
            "checks": list(checks.values()),
            "references": reference_check, "documents": list(documents.values())}


def _read_backs(events: list[dict[str, Any]], piece: str, key: str, fields: tuple[str, ...]) -> list[dict[str, Any]]:
    """The latest `<piece>.verify` per film or image, with its strays as settled by `<piece>.stray`: settled ones shown,
    pending ones only counted."""
    checks: dict[str, dict[str, Any]] = {}
    for e in events:
        if e.get("type") == f"{piece}.verify" and e.get(key):
            checks[e[key]] = {key: e[key], "model": e.get("model"), "items": e.get("items") or [], "ts": e.get("ts"),
                              **{f: e.get(f) for f in fields},
                              "strays": {s.get("n"): dict(s) for s in e.get("strays") or [] if isinstance(s, dict)}}
        elif e.get("type") == f"{piece}.stray" and e.get(key) in checks:
            stray = checks[e[key]]["strays"].get(e.get("n"))
            if stray is not None:
                stray.update(verdict=e.get("verdict"), note=e.get("note"))
    for check in checks.values():
        strays = list(check.pop("strays").values())
        check["strays"] = [s for s in strays if s.get("verdict") in ("misread", "real")]
        check["pending"] = sum(1 for s in strays if s.get("verdict") not in ("misread", "real"))
    return list(checks.values())


def _key(section: str, choice: str) -> tuple[str, str]:
    """A row as both sides name it: the engine strips `*` from the choice and the heading; this strips the rest of
    the inline marks too, from both, so the two always compare alike."""
    def norm(s: str) -> str:
        return " ".join(re.sub(r"[*_`]", "", s or "").split()).lower()
    return norm(section), norm(choice)


def attach_check(sections: list[dict[str, Any]], events: Iterable[dict[str, Any]],
                 since: str | None = None) -> dict[str, Any] | None:
    """The latest `Taste.checkReferences()` of `direction.md`, laid onto the rows it checked.

    Each Qloo row gets `check`: **verified** (every title and tag in it was returned by the query it cites),
    **unverified** (with what was not found and why), or **unchecked** — a row the check never saw, because it was
    written or reworded after the check ran, so an edit cannot inherit an earlier tick. The event holds only the
    agent's own words and the verdicts, never a raw Qloo payload: the run record is a file that could be committed,
    and Qloo's terms forbid storing responses in a public repository.
    Returns the check's totals for the panel's tally, or None when the run never ran one.
    """
    latest = None
    for event in events:
        if event.get("type") != "taste.check" or (event.get("path") or "direction.md") != "direction.md":
            continue
        if since and (event.get("ts") or "") < since:
            continue
        latest = event
    if latest is None:
        return None

    verdicts: dict[tuple[str, str], dict[str, Any]] = {}
    for row in latest.get("rows") or []:
        if not isinstance(row, dict):
            continue
        missing = [f"*{t.get('title')}*" + (f" (returned by {t['elsewhere']})" if t.get("elsewhere") else "")
                   for t in row.get("titles") or [] if isinstance(t, dict) and not t.get("found")]
        missing += [f"\"{t.get('tag')}\"" for t in row.get("tags") or [] if isinstance(t, dict) and not t.get("found")]
        problems = [str(p) for p in row.get("problems") or []]
        refs = {f"title:{ref_key(t.get('title'))}": {"found": bool(t.get("found")), "in": t.get("in")}
                for t in row.get("titles") or [] if isinstance(t, dict)}
        refs |= {f"tag:{ref_key(t.get('tag'))}": {"found": bool(t.get("found")), "in": t.get("in")}
                 for t in row.get("tags") or [] if isinstance(t, dict)}
        verdicts[_key(str(row.get("section") or ""), str(row.get("choice") or ""))] = {
            "state": "unverified" if missing or problems else "verified",
            "missing": missing,
            "problems": problems,
            "checked": len(row.get("titles") or []) + len(row.get("tags") or []),
            "refs": refs,
        }

    for section in sections:
        for row in section["rows"]:
            if any(k["key"] == "qloo" for k in row["kinds"]):
                row["check"] = verdicts.get(_key(section["title"], row["choice"]),
                                            {"state": "unchecked", "missing": [], "problems": [], "checked": 0,
                                             "refs": {}})

    return {"passed": bool(latest.get("passed")), "checked": latest.get("checked") or 0,
            "unverified": len(latest.get("unverified") or []), "ts": latest.get("ts")}
