# Polson Multi-Agent Comic Studio Test Harness

This directory is an autonomous **multi-agent co-creative studio harness** for evaluating the Polson MCP server with specialized artistic roles.

## Studio Architecture

```
                 ┌────────────────────────────────┐
                 │       Studio Director          │
                 │   (Orchestrator & Vision QA)   │
                 └───────────────┬────────────────┘
                                 │
         ┌───────────────────────┼───────────────────────┐
         ▼                       ▼                       ▼
┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐
│     Penciler     │   │     Colorist     │   │      Inker       │
│ (Composition &   │   │   (Palette &     │   │ (Contour & Line  │
│      Pose)       │   │    Lighting)     │   │      Art)        │
└──────────────────┘   └──────────────────┘   └──────────────────┘
```

## Directory Contents

- `GEMINI.md`: Master orchestrator system prompt and studio protocol.
- `roles/`: Detailed prompt specifications for each artistic role:
  - `roles/01_penciler.md` (Composition & Pose Agent)
  - `roles/02_inker.md` (Contour & Line Art Agent)
  - `roles/03_colorist.md` (Palette & Lighting Agent)
  - `roles/04_critic.md` (Vision Critic Agent)
- `reference_images/comic1.png`: Reference illustration.
- `.mcp.json` / `mcp_config.json`: Polson MCP server configuration.

## Launching

To run a multi-agent studio session:

```powershell
cd tests/agent/multi_agent/comic_studio
agy
```
