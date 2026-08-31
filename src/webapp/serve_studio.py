"""Serves the studio. Run from the repository root:

    python src/webapp/serve_studio.py projects
    python src/webapp/serve_studio.py tests/agent/infographic/agy --port 8080

The directory argument holds project directories, one per run — the same layout `create-project`
writes into. Nothing starts an agent until someone presses the button.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))


def main() -> int:
    parser = argparse.ArgumentParser(prog="serve_studio", description="Serve the Polson studio.")
    parser.add_argument("root", help="directory holding the project directories")
    parser.add_argument("--host", default="127.0.0.1",
                        help="bind address (default: localhost only)")
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.is_dir():
        print(f"error: no such directory: {root}", file=sys.stderr)
        return 1

    import uvicorn
    from studio import create_app

    print(f"  projects in {root}")
    print(f"  http://{args.host}:{args.port}\n")
    uvicorn.run(create_app(root), host=args.host, port=args.port, log_level="warning")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
