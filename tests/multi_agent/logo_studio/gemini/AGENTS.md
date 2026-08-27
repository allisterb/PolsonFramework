# Logo Design Studio Agent Roles

| Agent | Name | Role Description | System Prompt |
| :--- | :--- | :--- | :--- |
| `geometer` | The Geometer | Precision Mark Architect (Golden circles, logarithmic spirals, tangent fillets, squircle containers) | `roles/01_geometer.md` |
| `stylist` | The Stylist | Brand Identity Director (Palette, lockups, favicon scale ladder, executive presentation sheet) | `roles/02_stylist.md` |

## Pipeline Flow
```
[Client Brief] ──▶ (1. Geometer: Mark & Geometry) ──▶ (2. Stylist: Brand & Presentation Board) ──▶ [output.webp]
```

## Mandatory Constraints
- All JavaScript must be executed EXCLUSIVELY via `polson:ExecuteScript` (or `ExecuteScript`).
- Do NOT run `node`, `python`, `npm`, or any terminal commands.
- Do NOT read local files on disk for manuals or documentation. All documentation MUST be queried via MCP resources (`polson://sdk/core/*`, `polson://sdk/schema/*`).
