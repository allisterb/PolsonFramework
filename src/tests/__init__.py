"""Tests for the Python side of the studio. Standard library only — see README.md."""

import importlib.util

#: Whether the Antigravity SDK is installed, and therefore whether the driver can be exercised.
#:
#: **Two audiences now install this tree, and only one of them has an agent runtime.** Serving the
#: studio and reading a run need no SDK — `studio/` has zero references to one and `runs._drive`
#: imports the driver lazily — so `src/studio/requirements.txt` deliberately omits it and
#: `requirements-driver.txt` is what adds it. That is 17 packages against 52.
#:
#: Tests that genuinely drive an agent, or that import `orchestrator.run` / `orchestrator.director`,
#: skip rather than error when it is absent. Erroring would mean an observe-only installer could not
#: run the suite at all to check their own install, which is exactly when a person most wants to.
HAS_DRIVER = importlib.util.find_spec("google.antigravity") is not None

#: What to say when one is skipped, so the reason names the remedy rather than the symptom.
NO_DRIVER = "needs the Antigravity SDK: install src/studio/requirements-driver.txt"
