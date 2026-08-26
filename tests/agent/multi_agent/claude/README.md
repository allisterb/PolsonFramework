# Claude Multi-Agent Comic Studio Test Harness

This directory is an autonomous multi-agent co-creative studio test harness configured for **Claude Code** and **Antigravity Claude**.

## Launching with Claude Code

`ash
cd tests/agent/multi_agent/claude
claude
`

## Launching with Antigravity (Claude Model)

`ash
cd tests/agent/multi_agent/claude
agy -m claude-3-5-sonnet
`

## Directory Structure
- CLAUDE.md: Master system prompt and multi-agent pipeline protocol for Claude.
- oles/: Detailed specifications for Penciler, Colorist, Inker, and Critic roles.
- eference_images/comic1.png: Reference illustration ( \times 380$ px).
- .mcp.json / mcp_config.json: MCP server configuration.
- rtifacts/: Target directory for intermediate stage scripts and rendered WebP snapshots.
