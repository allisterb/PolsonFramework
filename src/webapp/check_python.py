"""Refuses a virtual environment older than the lock was resolved for.

pip catches this too, but much later and in the wrong vocabulary: part-way through the install, as a
pinned package rejecting the interpreter — which reads as a problem with the lock rather than with
the environment it is being installed into. On Ubuntu 22.04 the specific trap is that `python3` is
3.10, so `python3 -m venv` silently builds an environment the lock cannot be installed into, and
nothing says so until pip is several packages deep.

**The floor is read from the header uv writes into the lock, not repeated here.** A version copied
into this file is one that can disagree with the file it is guarding, and the disagreement would
surface as exactly the confusing pip failure this exists to prevent.

Run by `install.sh` and `install.cmd` with the venv's *own* interpreter, which is the one being
judged — so the check reports what pip will actually run under. Kept to syntax old interpreters can
still parse, because the environment it has to report on is by definition one that is too old.
"""

import re
import sys

#: The `--python-version` uv records in the lock's `uv pip compile ...` header line.
FLOOR = re.compile(r"--python-version\s+(\d+)\.(\d+)")

#: Enough to reach the header without reading a megabyte of hashes.
HEAD_BYTES = 4096


def main(lock):
    try:
        handle = open(lock, encoding="utf-8")
    except OSError as error:
        # Not fatal: the caller has already established the lock exists, so this is something
        # stranger than absence, and blocking an install over an unreadable *comment* would be a
        # worse failure than the one being guarded against.
        sys.stderr.write("warning: could not read %s (%s); skipping the version check\n" % (lock, error))
        return 0

    try:
        head = handle.read(HEAD_BYTES)
    finally:
        handle.close()

    match = FLOOR.search(head)
    if match is None:
        # A lock compiled without --python-version has no floor to check against, and inventing one
        # here is the duplicated constant the header is read to avoid.
        return 0

    want = (int(match.group(1)), int(match.group(2)))
    have = sys.version_info[:2]
    if have >= want:
        return 0

    sys.stderr.write(
        "error: this virtual environment runs Python %d.%d, and the lock was resolved for %d.%d or newer.\n"
        "       %s\n"
        "       pip would refuse a pinned package part-way through the install rather than here.\n"
        "       rebuild it with:  python%d.%d -m venv --clear %s\n"
        "       name the version -- plain python3 may not be the one you mean.\n"
        % (have[0], have[1], want[0], want[1], sys.executable, want[0], want[1], sys.prefix))
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
