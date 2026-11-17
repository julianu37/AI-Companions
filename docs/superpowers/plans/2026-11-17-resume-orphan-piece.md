# Resume Orphan Piece Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A resumed build order must not trust the saved placed set blindly — it re-checks the pieces near the body against the live scene, puts the missing ones back in the queue, and the sampler raises an alarm during the run when the counter and the scene disagree.

**Architecture:** One new pure rule in `src/Companions/AI/BuildResumeCheck.cs` (`Suspects` + `Missing`, no Unity) linked into the harness; one call site inside `MHR_Build.RestoreProgress` (the single point both resume paths go through) that uses the game's existing `Piece.GetAllPiecesInRadius` sweep — no per-piece ghost test; and a counter comparison in `tools/mcp-devbridge/run_watch.py` with its own synthetic test.

**Tech Stack:** C# net472 / BepInEx 5 / Unity 5 (Valheim 1.0.20), Python 3 (stdlib only), the project harness `tools/ai-intents-check` (net472, links real sources by `<Compile Include>`).

**Spec:** `docs/superpowers/specs/2026-11-17-resume-orphan-piece-design.md`

## Global Constraints

- **The build deploys.** `dotnet build Companions.sln -c Release` copies the DLL into the game folder. Use `-p:SkipDeploy=true` for every build until the in-game task.
- **Harness and builds run serially** — two at once fight over `obj/bin`.
- **Pure rules live in `src/Companions/AI/`** and must be linked into `tools/ai-intents-check/ai-intents-check.csproj` via `<Compile Include>` with a `<Link>` under `src/AI/`.
- **The harness diff only ADDS assertions** — never relax or remove an existing one.
- **Code comments in English, log lines in PT-BR.**
- **Player free text is never translated**; UI/speech strings need keys in the 4 JSONs (this plan adds none).
- **Solo only.** A crew's progress lives in the site (`MHR_BuildGroup`); `RestoreProgress` already returns early for `pending.Group != null` and this plan does not change that.
- **Fail-open:** if the scene sweep finds nothing, remove nothing — the resume behaves exactly as today.
- **Verify radius is 6 m**, a named constant, not a literal at the call site.
- **Never `--force`**, and no merge to `main` without the player's authorization.
- No new NuGet package, no new Python dependency.

## Review Focus

- **A sweep that fails must not delete progress.** If `Piece.GetAllPiecesInRadius` returns an empty list because the world is not ready, every suspect would look missing and the order would re-walk the neighbourhood, paying material twice. Expected: nothing is removed when the sweep yields no pieces at all.
- **A piece that IS standing must never be queued.** A false "missing" verdict places a duplicate next to the original; expected: only a piece absent from the sweep is returned.
- **The metric is XZ, never 3D.** A floor one storey above is not the same piece; expected: a piece 3 m up but at the same XZ is matched, matching the reconcile's own rule.
- **The alarm must not fire on a healthy run.** `done` and `in_scene` legitimately differ by the emergency posts and by a chunk still loading; expected: no alarm on an equal pair and one alarm line (not one per sample) when they diverge.
- **Nothing changes when there was no interruption.** A run with no death and no gather must produce byte-identical behaviour and no new log line.

---

### Task 1: The pure rule `BuildResumeCheck`

**Files:**
- Create: `src/Companions/AI/BuildResumeCheck.cs`
- Modify: `tools/ai-intents-check/ai-intents-check.csproj` (next to the `BuildProgress.cs` link, line ~112)
- Modify: `tools/ai-intents-check/Program.cs` (new section definition next to `new SectionDef("at", ...)` at line ~149, and the section method next to `ResumeProgress`)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `internal const float MatchSlack = 0.45f;` — the XZ tolerance for "the same piece", the same value `tools/reconcile_scene.py` uses.
  - `internal static int Suspects(IEnumerable<int> placed, IList<UnityEngine.Vector3> poses, UnityEngine.Vector3 center, float radius, ISet<int> into) -> int` — fills `into` with the placed indices whose plan pose is within `radius` (XZ) of `center`; returns how many it added.
  - `internal static int Missing(IEnumerable<int> suspects, IList<UnityEngine.Vector3> poses, IList<UnityEngine.Vector3> standing, ISet<int> into) -> int` — fills `into` with the suspect indices that have **no** standing pose within `MatchSlack` (XZ); returns how many it added.

- [ ] **Step 1: Write the failing test**

Add a new section to `Program.cs`. Definition in the section list (line ~149, after the `(at)` entry):

```csharp
new SectionDef("au", "BuildResumeCheck - a peca que sumiu volta para a fila na retomada", ResumeOrphan),
```

The section method:

```csharp
private static void ResumeOrphan()
{
    // the measured case of 16/11/2026: the slab #692 of the plan was placed, then left the world,
    // and the resume walked on as if it were up
    List<UnityEngine.Vector3> poses = EmbeddedPlan(new[] { 0f, 4f, 8f, 12f, 16f });
    var suspects = new HashSet<int>();

    // 1) the radius decides who is re-checked: only the pieces near the body
    int n = BuildResumeCheck.Suspects(new[] { 0, 1, 2, 3, 4 }, poses, new UnityEngine.Vector3(0f, 40f, 0f), 6f, suspects);
    Check("so os indices dentro de 6 m sao rechecados (2 de 5)",
        n == 2 && suspects.SetEquals(new[] { 0, 1 }), "suspeitos " + string.Join(",", suspects));

    // 2) a piece absent from the sweep is missing; a piece present is not
    var missing = new HashSet<int>();
    int m = BuildResumeCheck.Missing(new[] { 0, 1 }, poses, new[] { poses[1] }, missing);
    Check("a peca que nao esta na cena volta para a fila (so a #0)",
        m == 1 && missing.SetEquals(new[] { 0 }), "faltando " + string.Join(",", missing));

    // 3) the metric is FLAT: a piece one storey up at the same XZ is the same piece
    missing.Clear();
    BuildResumeCheck.Missing(new[] { 0 }, poses, new[] { poses[0] + new UnityEngine.Vector3(0f, 3f, 0f) }, missing);
    Check("a mesma peca 3 m acima no mesmo XZ casa (o teste e XZ, nao 3D)",
        missing.Count == 0, "faltando " + missing.Count);

    // 4) the slack: 0,45 m is the same piece, 0,60 m is not
    missing.Clear();
    BuildResumeCheck.Missing(new[] { 0 }, poses, new[] { poses[0] + new UnityEngine.Vector3(0.45f, 0f, 0f) }, missing);
    bool same = missing.Count == 0;
    missing.Clear();
    BuildResumeCheck.Missing(new[] { 0 }, poses, new[] { poses[0] + new UnityEngine.Vector3(0.60f, 0f, 0f) }, missing);
    Check("a tolerancia e 0,45 m: 0,45 casa e 0,60 nao", same && missing.Count == 1, "faltando " + missing.Count);

    // 5) FAIL-OPEN: an empty sweep removes nothing (the world was not ready)
    var kept = new HashSet<int>();
    int k = BuildResumeCheck.Missing(new[] { 0, 1 }, poses, new UnityEngine.Vector3[0], kept);
    Check("cena vazia nao apaga nada (fail-open)", k == 0 && kept.Count == 0, "removidas " + k);

    // 6) a broken pose is never a suspect and never a match
    suspects.Clear();
    var nan = new UnityEngine.Vector3(float.NaN, 40f, 0f);
    BuildResumeCheck.Suspects(new[] { 0 }, new List<UnityEngine.Vector3> { nan }, UnityEngine.Vector3.zero, 6f, suspects);
    Check("pose NaN do plano nunca e suspeita", suspects.Count == 0, "suspeitos " + suspects.Count);
}
```

`EmbeddedPlan` is a small local helper next to the existing point helpers:

```csharp
private static List<UnityEngine.Vector3> EmbeddedPlan(float[] xs)
{
    var list = new List<UnityEngine.Vector3>();
    foreach (float x in xs)
    {
        list.Add(new UnityEngine.Vector3(x, 40f, 0f));
    }
    return list;
}
```

- [ ] **Step 2: Run the harness to verify it fails**

Run: `dotnet run --project tools/ai-intents-check -c Release -- "--only=(au)"`
Expected: the build fails with `BuildResumeCheck could not be found` (the file does not exist yet).

- [ ] **Step 3: Create `src/Companions/AI/BuildResumeCheck.cs`**

Header comment in English following the `BuildProgress.cs` house style: name the measured run of 16/11/2026 (slab `#692` placed, then gone), state that the rule is the **cheap** check the player chose (a scene sweep near the body, never 985 ghost tests), and that it is pure so the harness pins it in section (au).

Implementation notes that the test does not determine:
- Both methods take `IList<Vector3>` and index it with the plan index; an out-of-range or non-finite pose is skipped (never a suspect, never a match).
- `Flat(a, b)` is the XZ distance with the same NaN/Infinity guard `BuildSpread.cs` uses (`float.MaxValue` when broken).
- `Missing` returns **immediately with 0** when `standing` is null or has no finite entry — that is the fail-open rule, and it is why an unready world cannot delete progress.
- `into` is cleared by the caller, not by these methods (the pattern `BuildProgress.Restore` already uses).

- [ ] **Step 4: Link the file into the harness csproj**

Add beside the existing `BuildProgress.cs` entry:

```xml
<Compile Include="..\..\src\Companions\AI\BuildResumeCheck.cs" Link="src/AI/BuildResumeCheck.cs" />
```

- [ ] **Step 5: Run the new section and the whole harness**

Run: `dotnet run --project tools/ai-intents-check -c Release -- "--only=(au)"` → Expected: all 6 checks PASS.
Run: `dotnet run --project tools/ai-intents-check -c Release` → Expected: `=== 1242 PASS / 0 FAIL ===` (1236 + 6).

- [ ] **Step 6: Commit**

```bash
git add src/Companions/AI/BuildResumeCheck.cs tools/ai-intents-check/ai-intents-check.csproj tools/ai-intents-check/Program.cs
git commit -m "test(build): a regra que acha a peca que sumiu na retomada, com prova propria"
```

---

### Task 2: Wire the rule into the resume

**Files:**
- Modify: `src/Companions/Jobs/MHR_Build.cs` (`RestoreProgress`, line ~8547; the miss list is `state.Retry`)

**Interfaces:**
- Consumes: `BuildResumeCheck.Suspects`, `BuildResumeCheck.Missing` (Task 1).
- Produces: nothing new for later tasks.

**Signature change:** `RestoreProgress` gains a `CompanionAI ai` first parameter (the log helper `MHR_Build.Log(CompanionAI, string)` needs it). Both call sites already have `ai` in scope and must be updated from `RestoreProgress(fresh, pending)` to `RestoreProgress(ai, fresh, pending)`.

- [ ] **Step 1: Read the two resume call sites before editing**

Run: `grep -n "RestoreProgress(fresh, pending)" src/Companions/Jobs/MHR_Build.cs`
Expected: two hits (line ~8935 for death, ~9099 for the gather handover). Both go through the same function, so the change lands once.

- [ ] **Step 2: Add the sweep and the removal inside `RestoreProgress`, after the existing `BuildPlanTotal.Of` line**

The constant goes near the top of `MHR_Build` with the other build constants:

```csharp
/// <summary>16/11/2026: how far around the body a resumed order re-checks the pieces it believes are standing.
/// It is two body windows (<see cref="BuildSpread.SpreadDistance"/>), the same distance the spread already uses for
/// "far enough not to step on each other" - the measured orphan slab fell 6,8 m from the body.</summary>
private const float ResumeCheckRadius = BuildSpread.SpreadDistance * 2f;
```

Then at the end of `RestoreProgress`:

```csharp
// 16/11/2026 (backlog 11g, the measured case): the saved set says a piece was placed; the scene is what proves it.
// One sweep of the game's own piece list around the body, no per-piece ghost test.
RecheckStanding(ai, fresh, pending);
```

And the helper, right below `RestoreProgress`:

```csharp
private static void RecheckStanding(CompanionAI ai, State fresh, Pending pending)
{
    // fail-open: no scene means no verdict, and the resume behaves as it did before this rule existed
    if (fresh == null || fresh.Plan == null || fresh.Plan.Count == 0 || fresh.PlacedIndices.Count == 0
        || ZNetScene.instance == null)
    {
        return;
    }
    s_resumePoses.Clear();
    for (int i = 0; i < fresh.Plan.Count; i++)
    {
        s_resumePoses.Add(fresh.Plan[i] != null ? fresh.Plan[i].Pos : new Vector3(float.NaN, 0f, 0f));
    }
    Vector3 center = fresh.Anchor;
    var suspects = new HashSet<int>();
    BuildResumeCheck.Suspects(fresh.PlacedIndices, s_resumePoses, center, ResumeCheckRadius, suspects);
    if (suspects.Count == 0)
    {
        return;
    }
    s_resumeStanding.Clear();
    Piece.GetAllPiecesInRadius(center, ResumeCheckRadius, s_resumePieces);
    foreach (Piece piece in s_resumePieces)
    {
        if (piece != null)
        {
            s_resumeStanding.Add(piece.transform.position);
        }
    }
    var missing = new HashSet<int>();
    BuildResumeCheck.Missing(suspects, s_resumePoses, s_resumeStanding, missing);
    int planned = BuildPlanTotal.Of(fresh.PlanTotal, fresh.Plan.Count);
    foreach (int index in missing)
    {
        fresh.PlacedIndices.Remove(index);
        if (fresh.Retry != null && index >= 0 && index < fresh.Plan.Count && fresh.Plan[index] != null)
        {
            fresh.Retry.Add(fresh.Plan[index]);
        }
        Log(ai, "retomada: a peça " + (index + 1) + "/" + planned + " não está de pé - volta para a fila");
    }
    if (missing.Count > 0)
    {
        fresh.PlacedTotal = Mathf.Max(0, fresh.PlacedTotal - missing.Count);
    }
}
```

The three lists are static scratch fields next to the other `s_` fields in `MHR_Build`:

```csharp
private static readonly List<Vector3> s_resumePoses = new List<Vector3>();
private static readonly List<Vector3> s_resumeStanding = new List<Vector3>();
private static readonly List<Piece> s_resumePieces = new List<Piece>();
```

- [ ] **Step 3: Build without deploying**

Run: `dotnet build Companions.sln -c Release -p:SkipDeploy=true`
Expected: `0 Aviso(s)`, `0 Erro(s)`.

- [ ] **Step 4: Run the whole harness**

Run: `dotnet run --project tools/ai-intents-check -c Release`
Expected: `=== 1242 PASS / 0 FAIL ===` (unchanged — this task adds no assertion; it wires the rule).

- [ ] **Step 5: Commit**

```bash
git add src/Companions/Jobs/MHR_Build.cs
git commit -m "fix(build): a retomada confere na cena as pecas que ela acha de pe"
```

---

### Task 3: The sampler alarm (item c)

**Files:**
- Modify: `tools/mcp-devbridge/run_watch.py` (`sample`, line ~60; the print loop, line ~110)
- Create: `tools/test_run_watch_alarm.py`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `gap_alarm(row, previous_gap) -> str | None` — the one line to print, or `None` when there is nothing to say. `row` is the dict `sample()` returns; `previous_gap` is the last gap it reported (`None` at the start).

- [ ] **Step 1: Write the failing test**

`tools/test_run_watch_alarm.py`, following the import-by-path style of `tools/test_build_run_report.py`:

```python
import importlib.util, os, unittest

HERE = os.path.dirname(os.path.abspath(__file__))
WATCH = os.path.join(HERE, "mcp-devbridge", "run_watch.py")


def load_watch():
    spec = importlib.util.spec_from_file_location("run_watch", WATCH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


W = load_watch()


class Alarm(unittest.TestCase):
    def test_no_alarm_when_the_pair_agrees(self):
        self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 985}, None))

    def test_alarm_on_the_measured_gap(self):
        line = W.gap_alarm({"done": 985, "in_scene": 984}, None)
        self.assertIn("ALARME", line)
        self.assertIn("-1", line)

    def test_alarm_fires_once_while_the_gap_holds(self):
        self.assertIsNotNone(W.gap_alarm({"done": 985, "in_scene": 984}, -1))
        self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 984}, -1))

    def test_alarm_fires_again_when_the_gap_worsens(self):
        self.assertIsNotNone(W.gap_alarm({"done": 985, "in_scene": 982}, -1))

    def test_missing_counters_produce_no_alarm(self):
        self.assertIsNone(W.gap_alarm({"done": None, "in_scene": None}, None))


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run it to verify it fails**

Run: `python tools/test_run_watch_alarm.py`
Expected: `AttributeError: module 'run_watch' has no attribute 'gap_alarm'`.

- [ ] **Step 3: Implement `gap_alarm` in `run_watch.py`, above `sample`**

Behaviour the tests pin: `done` and `in_scene` must both be integers or it returns `None`; `gap = done - in_scene`; returns `None` when `gap == 0` or when `gap == previous_gap` or when the new gap is **smaller in magnitude** than `previous_gap`; otherwise one line starting with `ALARME` that carries the gap, both counters and the time.

- [ ] **Step 4: Wire it into the print loop**

Keep the last reported gap in a local, so a healthy run prints nothing new:

```python
last_gap = None
# ... inside the loop, after the row is computed and before it is printed:
alarm = gap_alarm(row, last_gap)
if alarm is not None:
    print(alarm, flush=True)
    last_gap = row["done"] - row["in_scene"]
```

- [ ] **Step 5: Run the test and the existing suite**

Run: `python tools/test_run_watch_alarm.py` → Expected: `Ran 5 tests ... OK`.
Run: `python tools/test_build_run_report.py` → Expected: `OK` (unchanged).

- [ ] **Step 6: Commit**

```bash
git add tools/mcp-devbridge/run_watch.py tools/test_run_watch_alarm.py
git commit -m "feat(run): o sampler grita durante o run quando o contador e a cena discordam"
```

---

### Task 4: In-game verification (the player's run)

**Files:** none — this task measures.

**Interfaces:** Consumes Tasks 1–3 as built.

- [ ] **Step 1: Close the game, build with deploy, check the stamp**

Run: `python -c "import sys; sys.path.insert(0,'tools/mcp-devbridge'); import mcp_devbridge as m; print(m.call_bridge('world.quit', save=True))"` (or skip if the game is closed), then `dotnet build Companions.sln -c Release`, then confirm the DLL timestamp in `BepInEx\plugins\AI-Companions\` is newer than the build start.

- [ ] **Step 2: Reopen, enter the world, confirm the load lines**

`Start-Process "steam://rungameid/892970"`, poll `devbridge_ping`, then `world_enter`. In `BepInEx\LogOutput.log`: `Loading [AI Companions 1.9.9]` **and** `loaded successfully! (N methods patched)`.

- [ ] **Step 3: Clean the site and start the sampler with the alarm**

`build.pieces` at the site centre, `build.delete` with the **same centre and radius**, `build.pieces` back to `0`. Then `python run_watch.py --x -158.9 --z -427.7 --radius 32 --every 20 --baseline <current log lines> --out <run>.jsonl`, from inside `tools/mcp-devbridge/`.

- [ ] **Step 4: Order the build and let it run without any interruption**

`companions_build blueprint=CommHomeBasicBP name=Companion x=-158.9 y=38.8 z=-427.7` (the `y` is the **ground**, 38.8 — do not use 39.9). Expected: no new log line from this cycle, and **no ALARME** in the sampler — that is Review Focus line 5 and line 4.

- [ ] **Step 5: Force a death mid-build and watch the resume**

`companions_kill` between pieces 200 and 400. Expected in the log: `it died during the build`, `resuming at piece N`, and — only if a placed piece near the body vanished — `retomada: a peça N/M não está de pé - volta para a fila`. The order must continue and close with `hc_ai_blueprint_done`.

- [ ] **Step 6: Reconcile the scene — the acceptance that matters**

`build.pieces` at `radius=40` saved to `tools/mcp-devbridge/scene-<run>.json`, then
`python tools/reconcile_scene.py --blueprint "<plugin folder>\blueprints\CommHomeBasicBP.blueprint" --scene <that file>`.
Expected: **exit 0**, `faltantes no plano: ... sem explicacao: 0`.
Also run `python tools/test_reconcile_scene.py` → `OK` (the instrument is the proof).

- [ ] **Step 7: The short run (item d)**

Order the same blueprint, let it place ~150 pieces, then `companions_order action=stop`. Read `build_status` and the closing line, and compare `planned - placed` with the number of pieces the reconciliation says are missing. Expected: the same number.
If that run cannot be produced reproducibly, record item (d) as still open with the reason — do not mark it done.

---

### Task 5: Documentation, gate and merge

**Files:**
- Modify: `AGENTS.md` (state table: version, harness count, branch), `docs/roadmap.md` (a section for this cycle), `package/CHANGELOG.md` (new version section), `docs/backlog-construcao.md` (item 11g status)
- Modify: `src/Companions/Core/CompanionsPlugin.cs` (`PluginVersion`)

**Interfaces:** Consumes the measured numbers from Task 4.

- [ ] **Step 1: Write the measured numbers down, not the intended ones**

In `AGENTS.md`: harness count exactly as the last full run printed it, and the branch. In the backlog item 11g: which of (a)/(c)/(d) closed, with the number that proved it, and which stayed open with the reason.

- [ ] **Step 2: Bump the version and add the CHANGELOG section**

`PluginVersion` to `1.9.9`; a `## 1.9.9 - <date>` section in `package/CHANGELOG.md` in the house style (what was measured, what was fixed, what stayed open).

- [ ] **Step 3: Run the release gate**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\release-check.ps1 -Version 1.9.9`
Expected: **9/9 PASS**. The game must be closed (item 6 reads the published DLL).

- [ ] **Step 4: Commit and merge (only with the player's authorization)**

One commit for the docs+version, then `git switch main && git merge --no-ff <branch>`, then delete the branch. No `--force`.

---

## Self-Review

**Spec coverage:** the spec's three items map to Task 1+2 (a), Task 3 (c) and Task 4 Step 7 (d). The spec's "no ghost test per piece" is honoured in Task 2 (one `GetAllPiecesInRadius`). The spec's fail-open rule is Task 1 check 5 and Task 2's guard. The spec's "solo only" is untouched by design.

**Step scan:** every test step carries its assertions; every code step carries the signature and the values the spec pins; no step says "handle edge cases".

**Type consistency:** `Suspects`/`Missing` take `IEnumerable<int>` and `IList<Vector3>`, fill an `ISet<int>` and return `int` — used identically in Task 1's tests and Task 2's call site. `ResumeCheckRadius` is `BuildSpread.SpreadDistance * 2f` = 6 m, matching the spec.

**Review Focus coverage:** line 1 → Task 1 check 5 + Task 2 guard; line 2 → Task 1 check 2; line 3 → Task 1 check 3; line 4 → Task 3 tests + Task 4 Step 4; line 5 → Task 4 Step 4.

**Proportion:** the plan is shorter than the spec's implementation surface would be as code; code blocks are signatures, test bodies and one helper whose body the tests do not determine.
