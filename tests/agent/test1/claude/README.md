# Comics Harness — Claude

An autonomous-agent test harness for the Polson MCP server. A Claude Code agent is given the MCP server and nothing else, and asked to reproduce a reference illustration.

## Purpose

Evaluate whether the **published** Polson API is discoverable and usable by an agent that has never seen the backend. The agent works only from MCP tools (`Search`, `ExecuteScript`, `RenderSvg`, `MeasureSvgPath`, `History`) and MCP resources (`polson://sdk/*`, `polson://manual/*`), then reports its developer experience.

The task is the *instrument*; `findings.md` is the *result*.

## Files

| Path | Role |
|---|---|
| `CLAUDE.md` | The agent prompt: role, ground rules, task, deliverables. |
| `.mcp.json` | Points the `polson` MCP server at `bin/cli/Polson.CLI.dll`. |
| `.claude/settings.local.json` | Enforces the harness restrictions (see below). |
| `reference_images/` | Source images. The current task targets `comic2.png`. |
| `findings.md` | Report template the agent fills in. |
| `artwork.js` | Produced by the agent — the final drawing script. |
| `output.webp` | Produced by the agent — the final render. |

## Enforced restrictions

`.claude/settings.local.json` makes the harness's ground rules real rather than advisory:

- **`Bash` denied** — no `node`, `python`, `dotnet`, or any local interpreter. All execution must go through `ExecuteScript` on the MCP server, which is the component under test.
- **The implementation is out of reach** — `src/`, `docs/`, `reference/`, `bin/`, the unit-test projects, the sibling harnesses and the repo-root `CLAUDE.md` are denied for `Read`/`Glob`/`Grep`/`Write`/`Edit`, so the agent cannot infer the API from the code it is meant to be evaluating blind.
- **`WebFetch`, `WebSearch`, `Task` denied** — no outside information, no subagents.
- **Bypass-permissions mode disabled** — the restrictions cannot be turned off from inside the session.

### Writing path deny rules (learned the hard way)

Claude Code permission patterns are rooted at the project directory, and **deny beats allow**. Two failure modes, both hit during setup:

| Pattern | What happens | Why |
|---|---|---|
| `Read(//**)` | Denies **everything**, including this folder | `//` means *absolute path*; `//**` is the root glob and matches every path on the machine. Relative paths are resolved to absolute before matching, so nothing escapes it. |
| `Read(../**)` | Denies **nothing** | Patterns cannot express a location outside the project root, so the rule never matches. Verified: the agent read `src/…/ConstructiveDrawingToolkit.cs` with this rule active. |

What works is the **absolute form naming a specific subtree** — `Read(//C:/Projects/Polson/src/**)`. That means the rules are machine-specific and must be updated if the repo moves. Because a deny list cannot carve an exception out of a broader deny, each sibling directory is listed individually rather than denying the repo root.

The prompt opens with a self-test that proves the rules are live, so a misconfiguration surfaces in the agent's first line instead of silently invalidating the run.

> Known gap: `ExecuteScript`'s `outFile` writes through the MCP server, not through the agent's file tools, so it is not covered by these rules. The prompt asks the agent to keep those paths relative to this folder.

## Prerequisites

Build the CLI to the central output directory that `.mcp.json` points at:

```bash
dotnet build src/Polson.CLI/Polson.CLI.csproj
```

## Launching

```bash
cd tests/agent/comics/claude && claude
```

## After a run

Read `findings.md` first, then compare `output.webp` against `reference_images/comic2.png`. Move the run's artifacts aside before starting a fresh session so results stay separable.
