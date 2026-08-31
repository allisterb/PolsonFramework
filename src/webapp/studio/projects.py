"""Making a project from a brief a stranger typed.

The web layer never writes a project directory itself. `create-project` does, and it is the .NET CLI,
so this is a subprocess — which is the right shape rather than a compromise: the generator owns the
directory contract, the tool policy, the instruction templates and the brief sanitiser, and a second
implementation in Python would be a second source of truth for all four.

**The visitor's text reaches the CLI through a file, never a command line.** `--brief <path>` instead
of `--prompt "<text>"`. A brief is paragraphs, and putting paragraphs on a command line means
quoting, which means every quirk of the platform's argument parsing — the exact class of problem that
cost three sessions on the Antigravity hook. A file has no quoting.

**What the visitor controls, and what they do not.** The id becomes a directory name and the workflow
selects which template becomes the agent's instructions, so both are checked here against a closed
set before anything is spawned — not because the CLI fails to check them (it does) but because a
value that never leaves this process cannot be mishandled by anything between here and there. The
brief itself is deliberately *not* checked: `BriefSanitizer` on the other side strips the machinery of
concealment and keeps visible content, so a hostile brief stays visible for a director to see.
"""

from __future__ import annotations

import asyncio
import re
import shutil
import tempfile
from pathlib import Path

from orchestrator.credentials import CLI_DLL

from .runs import StudioError

#: Same rule as the generator's own, applied before a value becomes a path segment here.
VALID_ID = re.compile(r"^[A-Za-z0-9._-]{1,64}$")

#: Workflow → the types it offers, mirroring `ProjectTemplate/`. A closed set: the form offers these
#: and this refuses anything else, so no visitor string ever selects a template by name.
WORKFLOWS: dict[str, tuple[str, ...]] = {
    "logo": ("antique", "geometric", "modern"),
    "infographic": ("blueprint", "brutalist", "editorial", "specimen", "swiss"),
    "drawing": ("review", "seed"),
    "comic": ("review", "seed"),
    "painting": (),
    "comic_studio": (),
    "harness": ("image", "infographic", "logo"),
}

#: Every type any workflow offers, for building the form's one type control. Derived rather than
#: listed, because the control and the closed set above cannot then disagree about what exists.
ALL_TYPES: tuple[str, ...] = tuple(sorted({t for types in WORKFLOWS.values() for t in types}))

#: A brief is prose, not a novel. Long enough for a real one, short enough not to be a payload.
MAX_BRIEF = 8000

#: Generating is local and fast; anything past this is a broken toolchain, not a slow one.
CREATE_TIMEOUT = 60.0


def launcher() -> list[str]:
    """How to invoke the CLI: the native apphost if it is there, else the muxer and the dll.

    The same preference the generated hook makes, for the same reason — one less resolution step
    that can fail while reporting a missing file without saying which one.
    """
    exe = CLI_DLL.with_suffix(".exe")
    return [str(exe)] if exe.is_file() else ["dotnet", str(CLI_DLL)]


def check(root: Path, project_id: str, workflow: str, kind: str) -> None:
    """Refuses anything the form should not have been able to send. Raises `StudioError`."""
    if not VALID_ID.match(project_id or "") or project_id.strip(".") == "":
        raise StudioError(
            "A project name may use letters, digits, dot, dash and underscore, up to 64 characters. "
            "It becomes a directory name.")

    if workflow not in WORKFLOWS:
        raise StudioError(f"Unknown workflow '{workflow}'. Choose one of: {', '.join(WORKFLOWS)}.")

    # Omitting a type is legal, exactly as it is for `create-project`: the generator either has a
    # default for that workflow or renders no type section at all. Only a type the workflow does not
    # offer is refused — requiring one here would make the form stricter than the thing it drives,
    # and a visitor cannot tell the difference between a rule and a bug.
    offered = WORKFLOWS[workflow]
    if kind and kind not in offered:
        raise StudioError(
            f"The {workflow} workflow does not offer a type '{kind}'."
            + (f" Choose one of: {', '.join(offered)}, or leave it unset."
               if offered else " It takes no type."))

    if (root / project_id).exists():
        raise StudioError(
            f"There is already a project called '{project_id}'. Choose another name — nothing here "
            f"overwrites a directory that already has work in it.")


async def create(root: Path, project_id: str, workflow: str, kind: str, brief: str) -> Path:
    """Generates a standalone project and returns its directory.

    Standalone always: the orchestrator refuses a managed project, because its host would own tool
    policy and running it here would apply none — including the denial of `generate_image`, which the
    whole studio's premise rests on.
    """
    check(root, project_id, workflow, kind)

    brief = (brief or "").strip()
    if not brief:
        raise StudioError("A brief is the one thing the agent cannot infer. Say what you want made.")
    if len(brief) > MAX_BRIEF:
        raise StudioError(f"That brief is {len(brief)} characters; the limit is {MAX_BRIEF}.")

    root.mkdir(parents=True, exist_ok=True)

    # A file, not an argument. See the module docstring.
    scratch = Path(tempfile.mkdtemp(prefix="polson-brief-"))
    try:
        brief_file = scratch / "brief.md"
        brief_file.write_text(brief, encoding="utf-8")

        argv = [
            *launcher(), "create-project", str(root), project_id, "agy",
            "--standalone", "--workflow", workflow, "--brief", str(brief_file),
        ]
        if kind:
            argv += ["--type", kind]

        await _spawn(argv)
    finally:
        shutil.rmtree(scratch, ignore_errors=True)

    made = root / project_id
    if not (made / "project.json").is_file():
        raise StudioError(
            f"create-project reported success but wrote no project.json into {made}. "
            f"Check that {CLI_DLL} is the build you expect.")

    return made


async def _spawn(argv: list[str]) -> None:
    """Runs the CLI, turning a non-zero exit into something a visitor can read.

    The CLI's own message is preferred over anything invented here: it is written for a person and it
    knows what it refused, which this does not.
    """
    try:
        process = await asyncio.create_subprocess_exec(
            *argv, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE)
    except FileNotFoundError as exc:
        raise StudioError(f"Could not run the Polson CLI ({argv[0]}): {exc}") from exc

    try:
        out, err = await asyncio.wait_for(process.communicate(), timeout=CREATE_TIMEOUT)
    except (asyncio.TimeoutError, TimeoutError) as exc:
        process.kill()
        raise StudioError(f"create-project did not finish within {CREATE_TIMEOUT:.0f}s.") from exc

    if process.returncode != 0:
        raise StudioError(_why(out, err) or f"create-project failed with exit {process.returncode}.")


def _why(out: bytes, err: bytes) -> str:
    """The CLI's reason, with its console formatting taken off.

    It writes colour and a banner; a visitor wants the sentence. The last non-empty line of stderr,
    falling back to stdout, is where the refusal is.
    """
    for stream in (err, out):
        text = stream.decode("utf-8", errors="replace")
        text = re.sub(r"\x1b\[[0-9;]*m", "", text)
        lines = [ln.strip() for ln in text.splitlines() if ln.strip()]
        lines = [ln for ln in lines if not set(ln) <= set("=-_ ") and "POLSON" not in ln]
        if lines:
            return lines[-1][:400]
    return ""
