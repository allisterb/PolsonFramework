#!/usr/bin/env python
"""Strip the duplicated chrome out of a Sphinx-generated Blender Python reference,
and optionally convert it to Markdown.

A downloaded `blender_python_reference_*` tree is ~929 MB, and almost none of it is
reference content: Sphinx inlines the *entire* navigation tree into every page, so a
typical 358 KB page carries ~326 KB of sidebar that is the same tree on all 1,982
pages. Stripping that alone takes the tree to ~70 MB; `--text` takes it to ~9 MB of
Markdown.

Usage
  python tools/clean-blender-docs.py <src> --dry-run       # report, write nothing
  python tools/clean-blender-docs.py <src>                 # cleaned HTML beside src
  python tools/clean-blender-docs.py <src> --text          # one .md per page
  python tools/clean-blender-docs.py <src> --text --single reference.md

The HTML strip uses regex deliberately: the input is machine-generated Sphinx markup
and that keeps the script dependency-free. The Markdown conversion uses stdlib
`html.parser` instead, because a definition list nests and regex should not be asked
to track that.
"""
import argparse
import os
import re
import shutil
import sys
from html.parser import HTMLParser

# --------------------------------------------------------------------------- strip

STRIP = [
    ("sidebar nav", re.compile(r"<aside\b[^>]*>.*?</aside>", re.S | re.I)),
    ("script", re.compile(r"<script\b[^>]*>.*?</script>", re.S | re.I)),
    ("noscript", re.compile(r"<noscript\b[^>]*>.*?</noscript>", re.S | re.I)),
    ("style", re.compile(r"<style\b[^>]*>.*?</style>", re.S | re.I)),
    ("footer", re.compile(r"<footer\b[^>]*>.*?</footer>", re.S | re.I)),
    ("icon sprites", re.compile(r"<svg\b[^>]*display:\s*none[^>]*>.*?</svg>", re.S | re.I)),
    ("related bar", re.compile(r'<div\b[^>]*class="[^"]*\brelated\b[^"]*"[^>]*>.*?</div>', re.S | re.I)),
    ("headerlinks", re.compile(r'<a\b[^>]*class="headerlink"[^>]*>.*?</a>', re.S | re.I)),
    ("link tags", re.compile(r"<link\b[^>]*>", re.I)),
]

DROP_FILES = re.compile(r"^(genindex.*|search|py-modindex)\.html$", re.I)
DROP_DIRS = {".doctrees", "_static", "css", "js", "_sources"}
BLANKS = re.compile(r"[ \t]*\n(?:[ \t]*\n)+")


def clean(html):
    """Return (cleaned_html, {block_name: bytes_removed})."""
    removed = {}
    for name, pattern in STRIP:
        before = len(html)
        html = pattern.sub("", html)
        if before != len(html):
            removed[name] = before - len(html)
    return BLANKS.sub("\n", html), removed


# ------------------------------------------------------------------------ markdown

BLOCK = {"p", "div", "dl", "dt", "dd", "ul", "ol", "li", "pre", "table", "tr",
         "h1", "h2", "h3", "h4", "h5", "h6", "section", "blockquote"}
SKIP = {"svg", "symbol", "path", "use", "title", "nav", "aside", "script", "style"}
# Sphinx marks each documented member with one of these on its <dl>.
PY_KINDS = ("py class", "py function", "py method", "py attribute", "py data",
            "py exception", "py property", "py classmethod", "py staticmethod")


class ToMarkdown(HTMLParser):
    """Sphinx Python-domain HTML to Markdown.

    Keeps the shape that matters for reading an API: one heading per documented
    member carrying its full signature (so a grep for a call name lands on it), then
    the prose, then Parameters / Returns as bullet lists.
    """

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.out = []
        self.stack = []          # (tag, class)
        self.skip_depth = 0
        self.pre_depth = 0
        self.sig_depth = 0       # inside a <dt class="sig">: collect, do not emit
        self.sig = []
        self.list_depth = 0
        self.pending_field = None
        self.just_bullet = False

    # -- helpers ----------------------------------------------------------------
    def _cls(self, attrs):
        return dict(attrs).get("class", "")

    def emit(self, s):
        if self.sig_depth:
            self.sig.append(s)
        else:
            self.out.append(s)

    def nl(self, n=1):
        if self.sig_depth:
            return
        while self.out and self.out[-1] == "\n":
            self.out.pop()
        self.out.append("\n" * n if self.out else "")

    # -- parser hooks -----------------------------------------------------------
    def handle_starttag(self, tag, attrs):
        cls = self._cls(attrs)
        if self.skip_depth or tag in SKIP:
            self.skip_depth += 1
            return
        self.stack.append((tag, cls))

        if tag == "pre":
            self.pre_depth += 1
            self.nl(2)
            self.emit("```python\n")
        elif tag == "dt" and "sig" in cls:
            self.sig_depth += 1
            self.sig = []
        elif tag == "dt" and cls.startswith("field"):
            self.pending_field = True
            self.nl(2)
            self.emit("**")
        elif tag in ("h1", "h2", "h3", "h4", "h5", "h6"):
            self.nl(2)
            self.emit("#" * int(tag[1]) + " ")
        elif tag == "li":
            self.nl()
            self.emit("  " * max(0, self.list_depth - 1) + "- ")
            self.just_bullet = True
        elif tag in ("ul", "ol"):
            self.list_depth += 1
            self.nl(2)
        elif tag in ("strong", "b"):
            self.emit("**")
        elif tag in ("em", "i") and "sig-param" not in cls:
            self.emit("*")
        elif tag == "code":
            self.emit("`")
        elif tag == "p":
            # A <p> that opens a list item must not break the line after the bullet.
            if not self.just_bullet:
                self.nl(2)

    def handle_endtag(self, tag):
        if self.skip_depth:
            self.skip_depth -= 1
            return
        cls = ""
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][0] == tag:
                cls = self.stack[i][1]
                del self.stack[i:]
                break

        if tag == "pre":
            self.pre_depth -= 1
            self.emit("\n```\n")
        elif tag == "dt" and "sig" in cls:
            self.sig_depth -= 1
            sig = re.sub(r"\s+", " ", "".join(self.sig)).strip()
            # Sphinx emits the leading keyword in its own span with no whitespace
            # after it, so collapsing runs "class" into the name: "classbpy.types.X".
            # Longest first: plain `class` would otherwise match the head of
            # `classmethod` and leave "methodbl_rna_get_subclass".
            sig = re.sub(r"^(classmethod|staticmethod|property|class)(?=[A-Za-z_])",
                         r"\1 ", sig)
            if sig:
                self.nl(2)
                self.out.append("### `%s`\n" % sig)
        elif tag == "dt" and cls.startswith("field"):
            self.emit("**")
            self.pending_field = False
            self.nl()
        elif tag in ("h1", "h2", "h3", "h4", "h5", "h6"):
            self.nl(2)
        elif tag in ("ul", "ol"):
            self.list_depth = max(0, self.list_depth - 1)
            self.nl(2)
        elif tag in ("strong", "b"):
            self.emit("**")
        elif tag in ("em", "i") and "sig-param" not in cls:
            self.emit("*")
        elif tag == "code":
            self.emit("`")
        elif tag in BLOCK:
            self.nl()

    def handle_data(self, data):
        if self.skip_depth:
            return
        if self.pre_depth:
            self.emit(data)
            return
        text = re.sub(r"\s+", " ", data)
        if not text.strip():
            if self.out and not self.out[-1].endswith((" ", "\n")):
                self.emit(" ")
            return
        # A field label already carries its colon from the markup.
        if self.pending_field:
            text = text.rstrip(": ")
        self.just_bullet = False
        self.emit(text)

    def result(self):
        md = "".join(self.out)
        md = re.sub(r"[ \t]+", " ", md)
        md = re.sub(r" *\n *", "\n", md)
        md = re.sub(r"\n{3,}", "\n\n", md)
        # Only truly empty emphasis, left by stripped links. Matching across a space
        # would splice two adjacent spans together — that produced "**Parametersface**".
        md = md.replace("****", "").replace("``", "")
        md = re.sub(r"\n- *\n", "\n", md)            # bullets whose content was dropped
        return md.strip() + "\n"


ARTICLE = re.compile(r"<article\b[^>]*>(.*?)</article>", re.S | re.I)


def to_markdown(html, title=None):
    p = ToMarkdown()
    # Furo wraps the real content in <article>. Taking it positively drops the header,
    # breadcrumb and "skip to content" links without having to name each one.
    m = ARTICLE.search(html)
    if m:
        body = m.group(1)
    else:
        body = html[html.lower().find("<body"):] if "<body" in html.lower() else html
    p.feed(body)
    md = p.result()
    if title and not md.lstrip().startswith("#"):
        md = "# %s\n\n%s" % (title, md)
    return md


# ---------------------------------------------------------------------------- run

def human(n):
    for unit in ("B", "KB", "MB", "GB"):
        if abs(n) < 1024 or unit == "GB":
            return "%.1f %s" % (n, unit)
        n /= 1024.0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src", help="the downloaded reference directory")
    ap.add_argument("--out", help="destination (default: <src>_clean, or _md with --text)")
    ap.add_argument("--in-place", action="store_true", help="rewrite the source tree")
    ap.add_argument("--text", action="store_true", help="convert to Markdown")
    ap.add_argument("--single", metavar="FILE",
                    help="with --text, also concatenate everything into one .md")
    ap.add_argument("--keep-index", action="store_true", help="keep genindex/search pages")
    ap.add_argument("--keep-assets", action="store_true", help="keep _static/css/js/.doctrees")
    ap.add_argument("--dry-run", action="store_true", help="report only, write nothing")
    a = ap.parse_args(argv)

    src = os.path.abspath(a.src)
    if not os.path.isdir(src):
        sys.exit("not a directory: %s" % src)
    if a.in_place and a.out:
        sys.exit("--in-place and --out are mutually exclusive")
    if a.in_place and a.text:
        sys.exit("--in-place makes no sense with --text; use --out")
    default = src.rstrip("\\/") + ("_md" if a.text else "_clean")
    out = src if a.in_place else os.path.abspath(a.out or default)

    t = {"pages": 0, "before": 0, "after": 0, "dropped": 0, "dropped_bytes": 0, "copied": 0}
    by_block = {}
    single = [] if (a.single and a.text) else None

    for dirpath, dirnames, filenames in os.walk(src):
        if not a.keep_assets:
            for d in list(dirnames):
                if d in DROP_DIRS:
                    t["dropped_bytes"] += sum(
                        os.path.getsize(os.path.join(dp, f))
                        for dp, _, fs in os.walk(os.path.join(dirpath, d)) for f in fs)
                    t["dropped"] += 1
                    dirnames.remove(d)

        rel = os.path.relpath(dirpath, src)
        dest_dir = out if rel == "." else os.path.join(out, rel)
        if not a.dry_run and not a.in_place:
            os.makedirs(dest_dir, exist_ok=True)

        for name in sorted(filenames):
            spath = os.path.join(dirpath, name)
            size = os.path.getsize(spath)

            if not a.keep_index and DROP_FILES.match(name):
                t["dropped"] += 1
                t["dropped_bytes"] += size
                continue
            if not name.lower().endswith((".html", ".htm")):
                if a.keep_assets and not a.text:
                    t["copied"] += 1
                    if not a.dry_run and not a.in_place:
                        shutil.copy2(spath, os.path.join(dest_dir, name))
                else:
                    t["dropped"] += 1
                    t["dropped_bytes"] += size
                continue

            with open(spath, encoding="utf-8", errors="replace") as fh:
                html = fh.read()
            cleaned, removed = clean(html)
            for k, v in removed.items():
                by_block[k] = by_block.get(k, 0) + v

            stem = os.path.splitext(name)[0]
            if a.text:
                body = to_markdown(cleaned, title=stem)
                dest = os.path.join(dest_dir, stem + ".md")
                if single is not None:
                    single.append("\n\n<!-- page: %s -->\n\n%s" % (name, body))
            else:
                body = cleaned
                dest = os.path.join(dest_dir, name)

            t["pages"] += 1
            t["before"] += len(html)
            t["after"] += len(body)
            if not a.dry_run:
                with open(dest, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(body)

    if single is not None and not a.dry_run:
        path = a.single if os.path.isabs(a.single) else os.path.join(out, a.single)
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write("# Blender Python API reference\n")
            fh.write("\n*Converted from the Sphinx HTML build by "
                     "`tools/clean-blender-docs.py`.*\n")
            fh.writelines(single)
        print("single file    : %s (%s)" % (path, human(os.path.getsize(path))))

    print("pages          : %d" % t["pages"])
    print("  before       : %s" % human(t["before"]))
    print("  after        : %s" % human(t["after"]))
    if t["before"]:
        print("  reduction    : %.1f%%" % (100.0 * (t["before"] - t["after"]) / t["before"]))
    print("dropped        : %d entries, %s" % (t["dropped"], human(t["dropped_bytes"])))
    if not a.text:
        print("\nremoved by block:")
        for k, v in sorted(by_block.items(), key=lambda kv: -kv[1]):
            print("  %-14s %10s" % (k, human(v)))
    print("\n(dry run - nothing written)" if a.dry_run else "\nwritten to: %s" % out)


if __name__ == "__main__":
    main()
