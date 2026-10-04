#!/usr/bin/env python
"""Convert the Synfig user manual (reStructuredText, Sphinx) into one Markdown file.

The manual is ~255 .rst files in a Sphinx tree. This follows the toctree from
`index.rst` in reading order and writes every page into a single document, so a
layer, a converter or a parameter can be found with one search. Like
`clean-blender-docs.py` it is stdlib only, and it is a reading aid: Synfig's
source is the authority on what a layer computes, and the manual is not always
right about it.

Usage
  py -3.13 tools/synfig-docs-to-md.py reference/docs/synfig-docs-master/source reference/docs/synfig-docs.md

What it handles, because the manual uses it:
  - headings by underline (and overline) character, levels assigned per file in
    order of first appearance, then shifted by the page's depth in the toctree;
  - `.. |Name| replace::` substitutions, resolved; `.. |Name| image::` dropped,
    except the type icons and the colour/checkbox swatches that carry meaning
    in a parameter table;
  - `:ref:` (to the target's title when the text is omitted), `:download:`,
    hyperlinks, inline literals;
  - grid tables, as Markdown pipe tables;
  - figures and images dropped (their captions kept), notes and warnings as
    quotes, code blocks and `::` literal blocks fenced, raw HTML dropped.

The manual is CC BY-SA 4.0 (Synfig contributors); the output carries that notice.
"""
import argparse
import glob
import os
import re
import sys

HEADING_CHARS = set("=-~^#*+\"'`:._")
DIRECTIVE = re.compile(r"^(\s*)\.\.\s+([a-zA-Z-]+)::\s*(.*)$")
SUBST_DEF = re.compile(r"^\.\.\s+\|([^|]+)\|\s+(replace|image)::\s*(.*)$")
LABEL = re.compile(r"^\.\.\s+_([^:]+):\s*(\S.*)?$")
COMMENT = re.compile(r"^\.\.(\s|$)")

# Type icons and swatches that mean something in a parameter table.
ICON_TEXT = {
    "p_checkbox_off.png": "off",
    "p_checkbox_on.png": "on",
}


def read(path):
    with open(path, encoding="utf-8-sig") as f:
        return f.read().replace("\r\n", "\n").replace("\t", "    ").split("\n")


def is_underline(line):
    s = line.rstrip()
    return len(s) >= 3 and s[0] in HEADING_CHARS and s == s[0] * len(s)


# ------------------------------------------------------------------ toctree


def toctree_entries(path):
    """The pages a file's toctrees name, in order, as absolute paths."""
    lines = read(path)
    base = os.path.dirname(path)
    out = []
    i = 0
    while i < len(lines):
        m = DIRECTIVE.match(lines[i])
        if m and m.group(2) == "toctree":
            i += 1
            while i < len(lines) and (not lines[i].strip() or lines[i].startswith(" ")):
                entry = lines[i].strip()
                i += 1
                if not entry or entry.startswith(":"):
                    continue
                entry = re.sub(r"^.*<(.+)>$", r"\1", entry)  # "Title <page>"
                pattern = os.path.join(base, entry)
                matches = sorted(glob.glob(pattern if pattern.endswith(".rst") or "*" in pattern else pattern + ".rst"))
                if "*" in entry:
                    matches = [m for m in matches if m.endswith(".rst")]
                elif not matches and os.path.exists(pattern + ".rst"):
                    matches = [pattern + ".rst"]
                out.extend(os.path.normpath(m) for m in matches)
            continue
        i += 1
    return out


def walk(root_file):
    """Every page in reading order, with its depth in the toctree."""
    order, seen = [], set()

    def visit(path, depth):
        path = os.path.normpath(path)
        if path in seen or not os.path.exists(path):
            return
        seen.add(path)
        order.append((path, depth))
        for child in toctree_entries(path):
            visit(child, depth + 1)

    visit(root_file, 0)
    return order, seen


# ------------------------------------------------------------------ labels


def first_title(lines, start):
    """The first heading at or after a line."""
    for j in range(start, len(lines) - 1):
        a, b = lines[j], lines[j + 1]
        if is_underline(a) and j + 2 < len(lines) and is_underline(lines[j + 2]) and lines[j + 1].strip():
            return lines[j + 1].strip()
        if a.strip() and not is_underline(a) and is_underline(b) and len(b.rstrip()) >= len(a.strip()) - 2:
            return a.strip()
    return None


def collect_labels(pages):
    """label -> title, for :ref:`label` with no text of its own."""
    labels = {}
    for path, _ in pages:
        lines = read(path)
        for j, line in enumerate(lines):
            m = LABEL.match(line)
            if m:
                key = m.group(1).strip().lower()
                title = first_title(lines, j + 1)
                if title and key not in labels:
                    labels[key] = title
    return labels


# ------------------------------------------------------------------ inline


def inline(text, subs, labels):
    def sub_ref(m):
        body = m.group(1)
        mm = re.match(r"^(.*?)\s*<([^>]+)>$", body, re.S)
        if mm and mm.group(1).strip():
            return mm.group(1).strip()
        target = (mm.group(2) if mm else body).strip()
        return labels.get(target.lower(), target)

    # Substitutions first: their replacements may contain roles.
    for _ in range(3):
        new = re.sub(r"\|([^|\s][^|]*?)\|(?!\w)", lambda m: subs.get(m.group(1), m.group(0)), text)
        if new == text:
            break
        text = new

    # An image substitution a page uses but never defines (often spelled with escaped underscores): a type icon.
    text = re.sub(r"\|[^|\s]*?\.(?:png|jpg|jpeg|gif|svg)\|", "", text, flags=re.I)
    # A word substitution a page never defines (a bug in the source): its words.
    text = re.sub(r"\|([A-Za-z][A-Za-z_]*)\|", lambda m: m.group(1).replace("_", " "), text)

    text = re.sub(r":ref:`([^`]+)`", sub_ref, text)
    text = re.sub(r":doc:`([^`]+)`", lambda m: re.sub(r"\s*<[^>]+>$", "", m.group(1)), text)
    text = re.sub(r":download:`([^`<]+?)\s*<[^>]+>`", r"\1", text)
    text = re.sub(r":download:`([^`]+)`", r"\1", text)
    text = re.sub(r":(kbd|guilabel|menuselection|term|abbr|sup|sub|math):`([^`]+)`", r"\2", text)
    text = re.sub(r"``(.+?)``", r"`\1`", text)
    text = re.sub(r"`([^`<]+?)\s*<(https?://[^>]+)>`__?", r"[\1](\2)", text)
    text = re.sub(r"`<(https?://[^>]+)>`__?", r"\1", text)
    text = re.sub(r"`([^`]+)`__?", r"\1", text)
    return text


def substitutions(lines, labels):
    subs = {}
    i = 0
    while i < len(lines):
        m = SUBST_DEF.match(lines[i].strip())
        if m:
            name, kind, value = m.group(1), m.group(2), m.group(3).strip()
            if kind == "replace":
                subs[name] = value
            else:
                file = os.path.basename(value)
                if file in ICON_TEXT:
                    subs[name] = ICON_TEXT[file]
                elif file.startswith("p_color_"):
                    subs[name] = file[len("p_color_"):-4]
                else:
                    subs[name] = ""   # type icons, tool icons, pictures
        i += 1
    # Resolve the roles inside the replacements once, so a nested substitution resolves cleanly.
    return {k: inline(v, {}, labels) for k, v in subs.items()}


# ------------------------------------------------------------------ tables


def grid_table(rows, subs, labels):
    """A grid table's lines -> a Markdown pipe table."""
    border = rows[0]
    cuts = [i for i, ch in enumerate(border) if ch == "+"]
    records, current = [], None
    header_rows = 0
    for line in rows[1:]:
        if line.startswith("+"):
            if current is not None:
                records.append(current)
                current = None
            if "=" in line and header_rows == 0:
                header_rows = len(records)
            continue
        if current is None:
            current = [[] for _ in range(len(cuts) - 1)]
        for c in range(len(cuts) - 1):
            cell = line[cuts[c] + 1:cuts[c + 1]] if len(line) > cuts[c] else ""
            current[c].append(cell.strip())
    if current is not None:
        records.append(current)
    if not records:
        return []

    def render(rec):
        cells = [inline(" ".join(x for x in parts if x), subs, labels).replace("|", "\\|") for parts in rec]
        return "| " + " | ".join(re.sub(r"\s+", " ", c).strip() for c in cells) + " |"

    out = [render(records[0]), "|" + "|".join([" --- "] * (len(cuts) - 1)) + "|"]
    out += [render(r) for r in records[1:]]
    return out


# ------------------------------------------------------------------ blocks


def indented_block(lines, i, base_indent):
    """Lines after i that are blank or indented beyond base_indent."""
    block = []
    while i < len(lines) and (not lines[i].strip() or len(lines[i]) - len(lines[i].lstrip()) > base_indent):
        block.append(lines[i])
        i += 1
    while block and not block[-1].strip():
        block.pop()
    return block, i


def dedent(block):
    widths = [len(l) - len(l.lstrip()) for l in block if l.strip()]
    w = min(widths) if widths else 0
    return [l[w:] if l.strip() else "" for l in block]


def convert(path, depth, labels, root):
    lines = read(path)
    subs = substitutions(lines, labels)
    levels = []          # heading styles in order of first appearance
    out = [f"<!-- {os.path.relpath(path, root).replace(os.sep, '/')} -->", ""]
    para = []

    def flush():
        if para:
            out.append(inline(" ".join(l.strip() for l in para), subs, labels))
            out.append("")
            para.clear()

    def heading(title, style):
        if style not in levels:
            levels.append(style)
        level = min(6, levels.index(style) + 1 + depth)
        flush()
        out.extend(["#" * level + " " + inline(title.strip(), subs, labels), ""])

    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        # Overline + title + underline.
        if is_underline(line) and i + 2 < len(lines) and lines[i + 1].strip() and is_underline(lines[i + 2]) \
                and lines[i + 2].strip()[0] == stripped[0]:
            heading(lines[i + 1], "over" + stripped[0])
            i += 3
            continue
        # Title + underline.
        if stripped and not is_underline(line) and i + 1 < len(lines) and is_underline(lines[i + 1]) \
                and len(lines[i + 1].rstrip()) >= len(stripped) - 2 and not line.startswith(" "):
            heading(line, lines[i + 1].strip()[0])
            i += 2
            continue

        if SUBST_DEF.match(stripped) or LABEL.match(line):
            flush()
            _, i = indented_block(lines, i + 1, 0)
            continue

        m = DIRECTIVE.match(line)
        if m:
            flush()
            indent, name, arg = len(m.group(1)), m.group(2), m.group(3)
            block, i = indented_block(lines, i + 1, indent)
            body = dedent([l for l in block if not re.match(r"^\s*:[\w-]+:", l)])
            while body and not body[0].strip():
                body.pop(0)
            if name in ("figure", "image"):
                if body:   # a figure's caption
                    out.extend(["*" + inline(" ".join(b.strip() for b in body if b.strip()), subs, labels) + "*", ""])
            elif name in ("note", "warning", "tip", "important", "caution", "admonition", "hint", "attention", "seealso"):
                title = arg if name == "admonition" and arg else name.capitalize()
                text = inline(" ".join(b.strip() for b in body if b.strip()), subs, labels)
                out.extend([f"> **{title}:** {text}", ""])
            elif name == "code-block":
                out.extend([f"```{arg}"] + body + ["```", ""])
            elif name in ("raw", "toctree", "only", "index", "contents"):
                pass
            else:
                out.extend([f"> *[{name}]* " + inline(" ".join(b.strip() for b in body if b.strip()), subs, labels), ""])
            continue

        if COMMENT.match(line):
            flush()
            _, i = indented_block(lines, i + 1, 0)
            continue

        if stripped.startswith("+-") and stripped.endswith("+"):
            flush()
            rows = []
            while i < len(lines) and lines[i].strip() and lines[i].strip()[0] in "+|":
                rows.append(lines[i].strip())
                i += 1
            out.extend(grid_table(rows, subs, labels) + [""])
            continue

        # Bullet and enumerated items keep their own line.
        if re.match(r"^\s*([-*+]|\d+\.|#\.)\s+", line):
            flush()
            item = [stripped]
            ind = len(line) - len(line.lstrip())
            i += 1
            while i < len(lines) and lines[i].strip() and (len(lines[i]) - len(lines[i].lstrip())) > ind \
                    and not re.match(r"^\s*([-*+]|\d+\.|#\.)\s+", lines[i]):
                item.append(lines[i].strip())
                i += 1
            text = re.sub(r"^#\.", "1.", " ".join(item))
            out.append("  " * (ind // 3) + inline(text, subs, labels))
            if i >= len(lines) or not lines[i].strip():
                out.append("")
            continue

        # A paragraph ending in "::" introduces a literal block.
        if stripped.endswith("::") and not stripped.startswith(".."):
            para.append(line[:-1] if stripped != "::" else "")
            flush()
            block, i = indented_block(lines, i + 1, len(line) - len(line.lstrip()))
            body = dedent(block)
            while body and not body[0].strip():
                body.pop(0)
            if body:
                out.extend(["```"] + body + ["```", ""])
            continue

        if not stripped:
            flush()
        elif line.startswith("   ") and not para:
            out.append("> " + inline(stripped, subs, labels))   # a block quote
        else:
            para.append(line)
        i += 1

    flush()
    return out


# ------------------------------------------------------------------ main


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("source", help="the manual's source/ folder (holds index.rst)")
    ap.add_argument("output", help="the Markdown file to write")
    args = ap.parse_args()

    root = os.path.abspath(args.source)
    pages, seen = walk(os.path.join(root, "index.rst"))
    orphans = sorted(p for p in glob.glob(os.path.join(root, "**", "*.rst"), recursive=True)
                     if os.path.normpath(p) not in seen)
    labels = collect_labels(pages + [(p, 1) for p in orphans])

    out = [
        "# Synfig User Manual (single file)",
        "",
        "Converted from the Synfig documentation (https://github.com/synfig/synfig-docs), "
        "CC BY-SA 4.0, Synfig contributors, by `tools/synfig-docs-to-md.py`. Figures are omitted; "
        "each page starts with a comment naming its source file. **Synfig's source is the authority on what "
        "a layer computes**; this manual is a guide to it.",
        "",
    ]
    for path, depth in pages:
        out.extend(convert(path, depth, labels, root))
    if orphans:
        out.extend(["# Pages outside the table of contents", ""])
        for path in orphans:
            out.extend(convert(path, 1, labels, root))

    text = re.sub(r"\n{3,}", "\n\n", "\n".join(out)).strip() + "\n"
    with open(args.output, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"{len(pages)} pages in order, {len(orphans)} outside the toctree, {len(labels)} labels; "
          f"wrote {len(text):,} characters to {args.output}")


if __name__ == "__main__":
    sys.exit(main())
