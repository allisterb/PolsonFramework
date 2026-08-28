# Comics Harness — Gemini / Antigravity

An autonomous-agent test harness for the Polson MCP server. A Gemini (Antigravity) agent is given the MCP server and nothing else, and asked to reproduce a reference illustration.

This is the Gemini counterpart of `../claude`. Both harnesses run the **same prompt, the same task and the same restrictions**, so their `findings.md` reports are directly comparable.

## Purpose

Evaluate whether the **published** Polson API is discoverable and usable by an agent that has never seen the backend. The agent works only from MCP tools (`Search`, `ExecuteScript`, `RenderSvg`, `MeasureSvgPath`, `History`) and MCP resources (`polson://sdk/*`, `polson://manual/*`), then reports its developer experience.

The task is the *instrument*; `findings.md` is the *result*.

## Files

| Path | Role |
|---|---|
| `GEMINI.md` | The agent prompt: role, ground rules, task, deliverables. |
| `.mcp.json` | Points the `polson` MCP server at `bin/cli/Polson.CLI.dll`. |
| `.agents/settings.json` | Enforces the harness restrictions (see below). |
| `reference_images/` | Source images. The current task targets `comic2.png`. |
| `findings.md` | Report template the agent fills in. |
| `artwork.js` | Produced by the agent — the final drawing script. |
| `output.webp` | Produced by the agent — the final render. |
| `archive/` | Artifacts from earlier sessions, kept out of the way. |

## Enforced restrictions

`.agents/settings.json` makes the harness's ground rules real rather than advisory:

- **Shell denied** — no `node`, `python`, `dotnet`, or any local interpreter. All execution must go through `ExecuteScript` on the MCP server, which is the component under test.
- **The implementation is out of reach** — `src/`, `docs/`, `reference/`, `bin/`, the unit-test projects, the sibling harness and the repo-root prompts are denied, so the agent cannot infer the API from the code it is meant to be evaluating blind.
- **Web fetch, web search and task delegation denied** — no outside information, no subagents.

### Writing path deny rules (learned the hard way)

Deny beats allow, and two obvious-looking patterns both fail — verified in the Claude harness, and assumed to apply here until proven otherwise:

| Pattern | What happens | Why |
|---|---|---|
| `read:/**` | Denies **everything**, including this folder | A root-anchored glob matches every path on the machine; relative paths are resolved to absolute before matching, so nothing escapes it. |
| `read:../**` | Denies **nothing** | Patterns cannot express a location outside the project root, so the rule never matches. |

What works is the **absolute form naming a specific subtree** — `read:C:/Projects/Polson/src/**`. The rules are therefore machine-specific and must be updated if the repo moves. Because a deny list cannot carve an exception out of a broader deny, each sibling directory is listed individually rather than denying the repo root.

> Known gap: `ExecuteScript`'s `outFile` writes through the MCP server, not through the agent's file tools, so it is not covered by these rules. The prompt asks the agent to keep those paths relative to this folder.
>
> Antigravity's permission-rule syntax is less settled than Claude Code's. Confirm the deny rules actually bite on a throwaway run before trusting a comparison — the `.agents/settings.json` shape here follows the convention already used elsewhere in this repo.

## Prerequisites

Build the CLI to the central output directory that `.mcp.json` points at:

```bash
dotnet build src/Polson.CLI/Polson.CLI.csproj
```

## Launching

```bash
cd tests/agent/comics/gemini && agy
```

## After a run

Read `findings.md` first, then compare `output.webp` against `reference_images/comic2.png`. Move the run's artifacts into `archive/` before starting a fresh session so results stay separable.
