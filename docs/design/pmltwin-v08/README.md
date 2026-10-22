# PMLTWIN v08 — UI kit, light/dark themes and clickable prototype

Design source for the Yuzuha Toolkit workbench screens, drawn at 1440 × 1024 in
FigCraft. 19 frames, one shared shell, one variable-driven theme.

| File | What it is |
| --- | --- |
| `PMLTWIN-v08.figcraft` | Main design file, light mode by default |
| `PMLTWIN-v08-night.figcraft` | Same file with every page switched to the Dark mode |
| `preview-day.jpg` / `preview-night.jpg` | All 19 frames, light and dark |
| `before-after.jpg` | Three screens: v05 original → v08 light → v08 dark |
| `verification.json` | Type-scale, contrast and prototype-link checks |

Open a `.figcraft` with FigCraft 0.6+ (or `figcraft-cli render <file> -o out.png --page <name>`).

## Screens

Workbench: Overview · Launch · Sessions · Plugins · AI Runtime · Tool Logs · Knowledge ·
Settings · Review · Testing · Web Apps · Capabilities · Design rules.

CRUD / configuration screens added on top of the read-only dashboards:

- **Launch configurations** — a searchable library (8 sample configs, favourite / recent
  filters, product filter) with start / edit / duplicate / delete per row and a default badge.
- **Edit launch configuration** — basic info, program + paths, pre-flight checks, parameter
  preview, save / save-as-copy / delete.
- **Delete configuration** — impact list (shortcuts, default switch, running sessions),
  type-the-name confirmation.
- **New knowledge entry** — title, source, category, tags, body, visibility, publish checks.
- **Register web app** — entry path, version, target session, bridge capabilities, pre-checks.
- **Register function** — name, signature, source path, bound session, permission, validation.

## Prototype

456 hotspots use FigCraft's native `interactions`:

- sidebar: 11 nav items per page, brand → Overview, environment card / account → Settings;
- the Light / Dark segment fires `setVariableMode`, so the prototype re-themes itself;
- business flows: Overview → Launch / Sessions, Launch → Configuration library,
  library → Edit / Delete → back, Knowledge → new entry, Web Apps → register,
  Capabilities → register function;
- every page is a flow starting point, so the Prototype panel can start anywhere.

## Theme and type

26 colour variables with Light / Dark / High Contrast modes; every fill, stroke and text
colour in the 19 frames is bound to them (only the swatches on the design-rules page are
literal hex, on purpose). Contrast is AA or better on all body/label pairs.

Type scale: page title 28 · card title 16 · body 13–14 · table 13 · secondary 12 · micro 11.
No text below 11 px on the 19 frames (2,339 text layers, 0 below 10 px).

The last page of the source file —「设计疑问与圈注（保留原稿）」— is an untouched
annotation board from the previous round and is intentionally not part of this kit.
