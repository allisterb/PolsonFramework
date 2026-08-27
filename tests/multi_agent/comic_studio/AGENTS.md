# Polson Multi-Agent Studio — Subagent Guidelines (AGENTS.md)

This file defines the subagent roles, responsibilities, and communication protocol for Claude Code and Antigravity.

## Role Definitions
- **Penciler (.agents/subagents/penciler.md)**: Establishes  \times 380$ compositional layout, anatomical proportions, and continuous Bézier boundary contours.
- **Colorist (.agents/subagents/colorist.md)**: Applies 4-tier harmonic color palettes, 5 facial cel-shadow planes, Ben-Day dot shaders, and golden rim lights.
- **Inker (.agents/subagents/inker.md)**: Applies variable-weight calligraphic inking strokes, hair strand tapers, directional feathering, and solid black masses.
- **Critic (.agents/subagents/critic.md)**: Conducts side-by-side gap analysis vs eference_images/comic1.png, fixes micro-defects, and signs off on final publication rtwork.js and output.webp.

## Dual-Representation Workflow (Code + Pixels)
1. Every subagent saves both the JavaScript code (rtifacts/stageX.js) and the rendered WebP image (rtifacts/stageX.webp).
2. Downstream subagents inspect both the previous code and the rendered image before making edits.
3. Use ExecuteScript(script, outFile: 'artifacts/stageX.webp') for fast, lightweight rendering.
