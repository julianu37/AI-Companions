# Documentation i18n plan (English source, wiki in 5 languages)

Status: **planned** — no file moved yet. Decisions taken by the player on 25/10/2026.

## Decisions

1. **All documentation is written in English from now on**, including the internal docs (`AGENTS.md`,
   `docs/roadmap.md`, `docs/re/*`, the plans). Portuguese becomes a translation, not the source.
2. **The wiki is published in five languages**: **English is the default** (page names without a suffix) and
   Portuguese, Spanish, French and Italian are mirrors (`Home-pt`, `Home-es`, `Home-fr`, `Home-it`).
3. English screenshots are ready in `C:\Users\revenda33\Desktop\capturas_en` (10 PNG files).
4. `AGENTS.md` gets the new rule (docs in English; the mod's UI/speech stay in five languages, PT included).

## What exists today

| Layer | Files | Lines | Language |
|---|---|---|---|
| Public repo | `README.md`, `NOTICE.md`, `THIRD_PARTY_NOTICES.md` | 496 | PT (to translate) |
| Package (goes inside the zip) | `package/README.md`, `GUIDE.md`, `CHANGELOG.md`, `LEIA-ME.txt` | ~1.9k | PT (to translate, see below) |
| Package (generated) | `package/CONFIGURATION.md` | ~950 | **already EN** (script) |
| Internal | `AGENTS.md` (918), `docs/roadmap.md` (2,060), `docs/plan-*.md` (332), `docs/vision.md` (100), `docs/audit-*.md` (97), `docs/references.md` (81) | ~3.6k | PT (to translate) |
| Technical notes | `docs/re/*.md` (7 files) | ~1.6k | PT (to translate) |
| Release notes | `docs/release-notes-*.md` (9 files) | ~760 | PT (to translate) |
| Upstream notes | `docs/DEV_NOTES.md` (1,839), `docs/SESSION_HANDOFF.md` (98) | 1.9k | **already EN** (leave as is) |
| Wiki source | `docs/wiki/*.md` (12 pages) | ~1.9k | PT (EN source + 4 translations) |

Total to translate: **~7.8k lines in the repo** plus the wiki.

## Wiki architecture (GitHub wikis have no native i18n)

The wiki is a single git repository of markdown pages, so translation is done with a **page suffix**:

```
Home                      ← English (default: what a visitor sees)
Home-pt  Home-es  Home-fr  Home-it
Installation / Installation-pt / …
How-it-works / How-it-works-pt / …
Commands / Configuration / Voice / Requirements / Troubleshooting / LLM-(Groq…)
Credits-and-license
```

- **Language switcher** at the top of every page (one line, above the content):

  ```markdown
  > 🌍 **English** · [Português](Home-pt) · [Español](Home-es) · [Français](Home-fr) · [Italiano](Home-it)
  ```

  Each page links to its own mirror in the other languages (not to a different topic).
- **`_Sidebar.md`**: one sidebar, English links for the default pages plus an "Other languages" block with the
  five `Home-xx` pages (GitHub allows only one sidebar per wiki).
- **Anchors**: heading anchors are language-specific, so cross-page links inside a translated page must point to
  that language's page and anchor (`How-it-works-pt#companion-types`). No mixed-language links.
- **Images**: one folder `images/` shared by every language. The English pages use the new captures; the
  Portuguese pages keep the current ones; Spanish/French/Italian use the English captures (neutral) until
  per-language captures exist.
- **Publishing**: same flow as today (`docs/wiki/*.md` + `images/` → clone of `AI-Companions.wiki.git`), with
  more files (12 pages × 5 languages = 60).

### Screenshot mapping (English wiki)

| Capture (`capturas_en`) | Page |
|---|---|
| `first_run_companion_creation.png` | Home (first steps) |
| `Companions_Chat_Orders.png` | Home / Commands (chat orders) |
| `Multiple_Compainons1.png`, `Multiple_Compainons2.png` | Home / How-it-works (several companions) |
| `radial_menu.png` | Commands (radial menu) |
| `AI_Panel.png` | Configuration (AI panel) |
| `AI_Panel_Companions_Editor.png`, `AI_Panel_Companions_Editor2.png` | Configuration / How-it-works (companion tab) |
| `F8_Panel_Advanced_Configurations.png` | Configuration (F8) |
| `Companions_Inventory.png` | How-it-works (inventory) |

## Phases

| # | What | Files | Done when |
|---|---|---|---|
| **D0** | This plan + the new rule in `AGENTS.md` | 2 | committed |
| **D1** | Public docs to English | `README.md`, `NOTICE.md`, `THIRD_PARTY_NOTICES.md`, `package/README.md`, `package/GUIDE.md`, `package/CHANGELOG.md` (our sections) | no PT left; `LEIA-ME.txt` stays PT as the Brazilian quick read inside the zip |
| **D2** | Internal docs to English | `AGENTS.md`, `docs/roadmap.md`, `docs/plan-fases.md`, `docs/plan-multilinguagem.md`, `docs/vision.md`, `docs/audit-*.md`, `docs/references.md`, `docs/re/*.md`, `docs/release-notes-*.md` | no PT left in those files; technical terms/code identifiers unchanged |
| **D3** | Wiki: English source | the 12 pages renamed to the English titles above, content translated, new screenshots, switcher + sidebar | every page in English, links and anchors checked |
| **D4** | Wiki: PT/ES/FR/IT | 4 mirrors per page (48 files) | switcher works both ways; anchors valid in each language |
| **D5** | Verification | a script that scans for PT leftovers in the English files, checks every wiki link/anchor and that every page has the 5 mirrors | report with 0 problems |

Estimated effort: D1 ~½ day, D2 ~2–3 days (10k lines), D3 ~1 day, D4 ~2 days.

## Rules for the migration

- **English is the source**: a new doc is written in English first; the translations follow.
- Code identifiers, config keys, item/creature names and log tags are never translated.
- One commit per group of files (`docs(en): README and package`), so a bad batch is easy to revert.
- The **PT-BR translations of the player-facing texts are kept** where the player needs them: the wiki
  (`-pt`) and `package/LEIA-ME.txt` (the zip's Brazilian read-me). The rest of the repo becomes English-only.
- The mod itself is untouched: its UI/speech already follow the game language (five languages).
- `AGENTS.md` is the last internal doc to be translated, because the team works from it during the migration.

## Risks

- **Large diffs**: a 2,000-line file translated in one commit is hard to review; batches by section keep it
  reviewable.
- **Anchors and links**: the wiki has no automatic redirect; a renamed page breaks external links (the Nexus page
  links to `wiki/Configuracao`). Mitigation: keep a short stub page in the old name pointing to the new one
  (`Configuracao` → "This page moved to [Configuration](Configuration)").
- **Screenshots per language**: only English and Portuguese exist; ES/FR/IT reuse the English captures with a
  note until there are captures in those languages.
- **Players who read Portuguese**: the Nexus page stays in Portuguese and the wiki has `-pt` mirrors.
- **Search**: GitHub searches the whole wiki; it does not group results by language.
