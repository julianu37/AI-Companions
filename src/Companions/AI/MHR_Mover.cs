using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace Companions
{
    // ---- 1.4.74: one movement layer. Trips, repairs and bed go through here (follow and jobs move over in later builds). ----
    // Follows the real path corner by corner, re-plans every second, walks/jogs/runs by distance, plans around water it
    // can't swim, jumps narrow streams, opens doors on the way (and closes them behind), and when stuck works through:
    // open a door -> step back on a diagonal -> hop toward the next corner -> detour -> give up (callers decide what next).
    public static class MHR_Mover
    {
        class S
        {
            public Vector3 Goal, Dest; public float Dist; public string Tag = "";
            public bool Quiet;   // 1.5.4: sneaking - no jumps it can do without
            public bool FireReplan;   // 1.5.70: stepped out of a fire - plan again from outside it
            public readonly List<Vector3> Path = new List<Vector3>();
            public int Corner; public float NextPlan; public bool PathOk, Partial;
            public float Best, ProgressTime, AnchorTime; public Vector3 Anchor;
            public int Ladder; public float StepUntil; public Vector3 StepDir; public bool StepRun;
            public bool HasDetour; public Vector3 Detour; public float DetourUntil;
            public bool GaveUp; public float GaveUpAt;
            public bool DoorBlocked;   // 1.5.65: a fight chase came to a closed door it won't open
            public float LastGo, NextLog, NextDoorSearch, NextDoorPeek, NextJump, NextDive, NextWaterPeek, NextLineLog, NextFireLog;
            public Door RouteDoor; public Vector3 RouteDoorSpot;
            public bool WaitStamina, TooTired; public float NeedStamina, WaitStart, NoWaitUntil;
            public float MainBest; public int Passes; public Vector3 Shore; public bool HasShore; public float NextShore;
            public bool Walking, NewLeg;
            public float NextStartSearch;
            public Door LastRouteDoor; public float LastRouteDoorAt;
            public float RouteSwim;
            // 1.5.2: a ladder it's heading for (a skeleton up a Black Forest tower: the way up is the ladder)
            public Vector3 LastWaterJumpDir; public float LastWaterJumpAt = -99f;
            // 1.5.2: climbing a built ladder (push into it and hop, like a player); NextLadderLog throttles the "nothing found" diagnostic
            public int HopUpTries;
            public bool DescendLadder; public float DescendUntil; public Vector3 DescendTop;
            public bool ClimbLadder, ClimbAuto, LadderPending; public Vector3 ClimbDir, ClimbBase; public float ClimbStartY, ClimbStartAt, ClimbBottomY, NextEdgeLog, ClimbViaUntil, ClimbFootBest, ClimbFootAt, ClimbFaceAt; public Vector3 ClimbFace;
            // 1.5.3: a whole chain of ladders planned at once and climbed in one go
            public List<LadderStep> Chain, PendingChain; public bool OnChain, ChainDown, PendingDown; public int ChainIdx, ChainJumps;
            public AutoJumpLedge[] ClimbLedges; public float ChainProgressAt, ChainBestY, ChainJumpAt, ChainPushAt, ChainRestUntil, ChainFootBest, ChainFootAt, ChainViaUntil; public Vector3 ChainVia; public Vector3 ClimbVia; public int ClimbJumps; public int ClimbLadderId; public float NextLadderClimbSearch, NextLadderLog; public readonly HashSet<int> LadderClimbsTried = new HashSet<int>(); public readonly Dictionary<int, Vector2> LadderUses = new Dictionary<int, Vector2>();
            public Ladder UseLadder; public float LadderUntil, NextLadderSearch; public readonly HashSet<Ladder> LaddersUsed = new HashSet<Ladder>();
            public bool MainPartial;
            public Vector3 GoodPos; public float GoodAt; public bool HasGood;
            public float ClimbUntil, ClimbBestY, ClimbProgressAt, NextClimbJump, ClimbRestUntil; public Vector3 ClimbTop;
            public int ClimbTries; public readonly List<Vector3> ClimbTried = new List<Vector3>();
            public bool Direct; public float NextHop, NoDirectUntil;
            public Door PassDoor; public float PassSide, NextDoorwayCheck;
        }
        class Opened { public Door Door; public float At; }

        static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();
        static readonly Dictionary<int, List<Opened>> s_opened = new Dictionary<int, List<Opened>>();
        static readonly List<Vector3> s_tmp = new List<Vector3>();
        static readonly List<Vector3> s_tmp2 = new List<Vector3>();
        static readonly List<Piece> s_pieces = new List<Piece>();
        static readonly Collider[] s_cols = new Collider[16];
        // 1.4.138: door searches need far more room - inside a house dozens of walls, floors and beams are within 2.5 m and
        // with 16 slots the door often wasn't among them (1.4.137 test: stuck by an open door that was 1.9 m away)
        static readonly Collider[] s_doorCols = new Collider[512];
        static int s_mask = -1;
        static float s_nextErr;

        public const float StillTime = 2.5f, NoProgressTime = 8f;

        static int Mask()
        {
            if (s_mask == -1) s_mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            return s_mask;
        }
        static S Get(Component c)
        {
            if (c == null) return null;
            S s;
            if (!s_s.TryGetValue(c.gameObject.GetInstanceID(), out s)) { s = new S(); s_s[c.gameObject.GetInstanceID()] = s; }
            return s;
        }
        static S Peek(Component c)
        {
            S s;
            return c != null && s_s.TryGetValue(c.gameObject.GetInstanceID(), out s) ? s : null;
        }
        static void Dbg(string msg) { if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug(msg); }
        public static string Fmt(Vector3 v) { return "(" + v.x.ToString("F1") + ", " + v.y.ToString("F1") + ", " + v.z.ToString("F1") + ")"; }
        static float Flat(Vector3 a, Vector3 b) { return new Vector2(a.x - b.x, a.z - b.z).magnitude; }

        // ---------- queries for the callers ----------
        public static bool OwnsMovement(Component c) { S s = Peek(c); return s != null && Time.time - s.LastGo < 0.3f; }
        public static bool GaveUp(CompanionAI ai) { S s = Peek(ai); return s != null && s.GaveUp; }
        public static bool DoorBlocked(CompanionAI ai) { S s = Peek(ai); return s != null && s.GaveUp && s.DoorBlocked; }

        // 1.5.65: a fight doesn't open doors on its own (user: they go for enemies behind closed doors) - only an order does
        public const string CombatTag = "Combat";
        // 1.4.79: the current route doesn't reach the goal (Follow switches to the player's path then)
        public static bool RoutePartial(CompanionAI ai) { S s = Peek(ai); return s != null && Time.time - s.LastGo < 0.5f && s.Partial && !s.HasDetour; }
        // 1.4.81: meters of swimming on the current route
        // 1.4.110: how much swimming a job accepts to reach something (General.JobSwim)
        public static float JobSwim() { return ModConfig.JobSwim != null ? ModConfig.JobSwim.Value : 20f; }
        public static float RouteSwim(CompanionAI ai) { S s = Peek(ai); return s != null && Time.time - s.LastGo < 0.5f ? s.RouteSwim : 0f; }
        // 1.5.83: waiting on the bank for its breath before a swim (MHR_Board doesn't count that as stuck)
        public static bool RestingForSwim(CompanionAI ai) { S s = Peek(ai); return s != null && s.WaitStamina && Time.time - s.LastGo < 0.5f; }
        // 1.4.82: length of the current route, for callers that compare it with the straight line (0 when not moving)
        public static float RouteLength(CompanionAI ai)
        {
            S s = Peek(ai);
            if (s == null || Time.time - s.LastGo >= 0.5f || !s.PathOk || s.Path.Count == 0) return 0f;
            return Remaining(s, ai.transform.position);
        }
        // 1.5.2: on a ladder, heading for one, going down one or mid-hop - a caller shouldn't stop it here
        public static bool OnTheWayUp(CompanionAI ai)
        {
            S s = Peek(ai); float now = Time.time;
            return s != null && now - s.LastGo < 1f && (now < s.ClimbUntil || s.LadderPending || s.DescendLadder || now < s.StepUntil || s.OnChain);
        }
        // 1.5.3: climbing a planned chain of ladders (Follow leaves it to finish rather than switching to the player's path)
        public static bool OnLadderChain(CompanionAI ai) { S s = Peek(ai); return s != null && s.OnChain && Time.time - s.LastGo < 1f; }
        public static bool Progressing(CompanionAI ai) { S s = Peek(ai); return s != null && !s.GaveUp && Time.time - s.ProgressTime < 10f; }
        public static float StuckSeconds(CompanionAI ai) { return GaveUp(ai) ? 11f : 0f; }
        // CompanionRest keeps its bed trip alive while we're still getting somewhere
        public static bool KeepAlive(Component c)
        {
            S s = Peek(c);
            return s != null && !s.GaveUp && Time.time - s.LastGo < 0.5f && Time.time - s.ProgressTime < 10f;
        }
        public static void Release(CompanionAI ai)
        {
            S s = Peek(ai);
            if (s == null) return;
            s.LastGo = 0f; s.GaveUp = false; s.Path.Clear(); s.HasDetour = false; s.RouteDoor = null;
            Character c = ai.GetComponent<Character>();
            if (c != null && s.Walking) { c.SetWalk(false); s.Walking = false; }
        }

        // ---------- main entry ----------
        // Returns true once within 'dist' (flat) and on the same level (within 1.5m up/down).
        public static bool Go(CompanionAI ai, float dt, Vector3 goal, float dist, bool allowRun, string tag)
        {
            return Go(ai, dt, goal, dist, allowRun, tag, SpeedAuto);
        }

        // 1.4.76: speed set by the caller (Follow matches the player's pace) instead of by distance.
        public const int SpeedAuto = 0, SpeedWalk = 1, SpeedJog = 2, SpeedRun = 3;
        public const string FollowTag = "Follow";
        public static bool Go(CompanionAI ai, float dt, Vector3 goal, float dist, bool allowRun, string tag, int speed)
        {
            if (ai == null) return false;
            try { return GoInner(ai, goal, dist, allowRun, tag ?? "", speed); }
            catch (Exception e)
            {
                if (CompanionsPlugin.Log != null && Time.time >= s_nextErr) { s_nextErr = Time.time + 30f; CompanionsPlugin.Log.LogWarning("[Move] " + e.Message); }
                return false;
            }
        }

        static bool GoInner(CompanionAI ai, Vector3 goal, float dist, bool allowRun, string tag, int speed)
        {
            float now = Time.time;
            S s = Get(ai);
            Character c = ai.GetComponent<Character>();
            Vector3 pos = ai.transform.position;
            float shift = Vector3.Distance(goal, s.Goal);
            bool fresh = shift > 4f || now - s.LastGo > 2f || s.Tag != tag;
            if (s.GaveUp && now - s.GaveUpAt > 15f) fresh = true;   // try again after a while
            if (fresh) Reset(s, goal, pos, now, tag);
            // 1.4.78: a new nearby target (the farmer's next crop 2 m on) or carrying on after a pause (harvesting,
            // swinging) starts the stuck clocks over. They used to keep running while he stood working, so the next
            // short step counted as "standing still 4s" and he started sidestepping and hopping around the field.
            // 1.4.82: a new goal also clears an old give-up (1.4.81 log: planting stopped because the give-up from the
            // last spot was still set). A mere pause on the same goal keeps the stuck ladder where it was: the harvest
            // swings reset it every few seconds, so in the moat he sat on "step 1" forever and never gave up.
            else if (now - s.LastGo > 0.5f || (shift > 1.5f && tag != FollowTag))
            {
                s.AnchorTime = now; s.Anchor = pos; s.ProgressTime = now; s.Best = float.MaxValue; s.StepUntil = 0f;
                if (shift > 1.5f) { s.MainBest = float.MaxValue; s.Ladder = 0; s.Passes = 0; s.GaveUp = false; }
            }
            else if (shift > 1.5f && !s.HasDetour && s.RouteDoor == null) s.NextPlan = 0f;
            // 1.4.76: a goal that keeps moving (Follow): re-plan once it has drifted from the planned destination
            else if (!s.HasDetour && s.RouteDoor == null && Vector3.Distance(goal, s.Dest) > 1.5f && s.NextPlan > now + 0.7f) s.NextPlan = Mathf.Min(s.NextPlan, now + 0.3f);
            s.LastGo = now; s.Goal = goal; s.Dist = dist;
            // 1.5.4: crouched (sneaking up on prey): wade shallow water and walk round instead of jumping - a jump is loud,
            // stands it up and costs stamina (user log: jumped the water while sneaking up on a deer, which then bolted)
            CompanionStamina cst = ai.GetComponent<CompanionStamina>();
            s.Quiet = cst != null && cst.IsCrouching;

            // arrived: within reach on the same level (not standing under it on the floor below)
            if (Flat(pos, goal) <= dist && Mathf.Abs(pos.y - goal.y) <= 1.5f) { Halt(ai, c, s); return true; }
            if (s.GaveUp) { Halt(ai, c, s); return false; }
            if (c == null || c.InAttack()) return false;

            // steering target: a detour point, the door we're routing through, or the path
            if (now >= s.NextPlan || s.Path.Count == 0) Plan(ai, c, s, pos, now);
            s_swimming = c.IsSwimming();
            AdvanceCorner(s, pos);
            float remain = Remaining(s, pos);
            if (s.NewLeg) { s.NewLeg = false; s.Best = remain; }
            if (remain < s.Best - 0.75f) { s.Best = remain; s.ProgressTime = now; }
            if (!s.HasDetour && s.RouteDoor == null && remain < s.MainBest - 1.5f) { s.MainBest = remain; s.Ladder = 0; s.Passes = 0; }
            if (Vector3.Distance(pos, s.Anchor) > 0.6f) { s.Anchor = pos; s.AnchorTime = now; }

            Log(s, pos, remain, now, c);

            // 1.4.82: climbing out of a pit
            // 1.5.3: a planned chain of ladders, climbed without stopping
            if (s.OnChain) { RunChain(ai, c, s, pos, now, tag); if (s.OnChain) return false; }
            if (now < s.ClimbUntil) { Climb(ai, c, s, pos, now, tag); return false; }
            // 1.5.2: going back down a ladder
            if (s.DescendLadder && !s.LadderPending)
            {
                if (now < s.DescendUntil) { Descend(ai, c, s, pos, now, tag); return false; }
                MarkBadLadder(s.ClimbLadderId, now, 300f);
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get down the ladder (got to " + Fmt(pos) + ")");
                s.DescendLadder = false;
            }

            // a sidestep / hop in progress
            if (now < s.StepUntil)
            {
                // 1.5.72: a walking step (a sidestep out of being stuck) stops at a drop, fire or spikes on the way - it was only
                // checked when it started (jumps are left alone - they're in the air)
                if (!s.StepRun && c.IsOnGround() && !c.IsSwimming() && (DropAhead(pos, s.StepDir, 0.8f, 1.2f) || FireArea(pos + s.StepDir * 0.8f, 0.3f) != null || SpikeNear(pos + s.StepDir * 0.8f, 0.6f) != null))
                {
                    s.StepUntil = 0f; Halt(ai, c, s);
                    if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] stopped a step - a drop, fire or spikes ahead");
                    return false;
                }
                Steer(ai, c, s, s.StepDir, s.StepRun, false); return false;
            }

            // 1.4.88: never stand in a fire
            // 1.5.70: plan again once out - the old route led into it (log 188ac60a: out, then straight back in, 7 times)
            if (EscapeFire(ai)) { s.FireReplan = true; return false; }
            if (EscapeLava(ai)) { s.FireReplan = true; return false; }
            if (s.FireReplan) { s.FireReplan = false; s.HasDetour = false; s.NextPlan = 0f; }

            // waiting on the bank until there's stamina for the swim
            if (s.WaitStamina)
            {
                if (Stamina(ai) >= s.NeedStamina) { s.WaitStamina = false; s.TooTired = false; s.NoWaitUntil = now + 30f; s.NextPlan = 0f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] rested enough to swim across"); }
                else if (now - s.WaitStart > 40f) { s.WaitStamina = false; s.TooTired = false; s.NoWaitUntil = now + 60f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] waited long enough - swimming across"); }
                else { Halt(ai, c, s); s.ProgressTime = now; s.AnchorTime = now; return false; }
            }

            // 1.5.2: ladders - use one we've reached; look for one when the goal is up out of reach
            if (s.UseLadder != null || (object)s.UseLadder != null)
            {
                if (UseLadderNow(ai, c, s, pos, now, tag)) return false;
                if (s.UseLadder == null) { s.HasDetour = false; Plan(ai, c, s, pos, now); }
            }
            else if (s.MainPartial && !s.HasDetour && s.RouteDoor == null && !c.IsSwimming() && now >= s.NextLadderSearch && s.Goal.y > pos.y + 2f)
            {
                if (!TryLadder(ai, s, pos, now, tag) && now >= s.NextLadderClimbSearch && TryStartLadderClimb(ai, c, s, pos, now, tag)) return false;
            }
            else if (s.MainPartial && !s.HasDetour && !s.DescendLadder && s.RouteDoor == null && !c.IsSwimming() && now >= s.NextLadderClimbSearch && s.Goal.y < pos.y - 2f)
            {
                if (TryLadderDown(ai, c, s, pos, now, tag)) return false;
            }
            if (s.LadderPending)
            {
                if (s.DescendLadder && Flat(pos, s.DescendTop) < 1.0f && Mathf.Abs(pos.y - s.DescendTop.y) < 1.5f)
                {
                    s.LadderPending = false; s.HasDetour = false; s.DescendUntil = now + 10f;
                    if (s.PendingChain != null && s.PendingDown && s.PendingChain.Count >= 2) { StartChain(s, s.PendingChain, 0, pos, now, tag, true); s.PendingChain = null; return false; }
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] at the top of the ladder - going down");
                    return false;
                }
                if (!s.DescendLadder && Flat(pos, s.ClimbBase) < 1.0f && Mathf.Abs(pos.y - s.ClimbBase.y) < 1.5f)
                {
                    s.LadderPending = false; s.HasDetour = false;
                    if (s.PendingChain != null && !s.PendingDown && s.PendingChain.Count >= 2) { StartChain(s, s.PendingChain, 0, pos, now, tag); s.PendingChain = null; return false; }
                    s.ClimbLadder = true; s.ClimbStartY = pos.y; s.ClimbStartAt = now; s.ClimbJumps = 0; s.ClimbViaUntil = 0f; s.ClimbFootBest = float.MaxValue; s.ClimbFootAt = now; s.ClimbUntil = now + 20f; s.ClimbBestY = pos.y; s.ClimbProgressAt = now; s.NextClimbJump = 0f;
                    NoteLadderClimb(s, now, tag);
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] at the foot of the ladder - climbing");
                    return false;
                }
                if (!s.DescendLadder && pos.y < s.ClimbBase.y - 1.5f && c.IsOnGround())
                {
                    s.LadderPending = false; s.HasDetour = false; s.NextLadderClimbSearch = 0f; s.NextPlan = 0f;
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] fell off on the way to the ladder (now at " + Fmt(pos) + ") - looking again");
                    Plan(ai, c, s, pos, now);
                }
                else if (now > s.DetourUntil || !s.HasDetour)
                {
                    s.LadderPending = false; s.HasDetour = false; MarkBadLadder(s.ClimbLadderId, now, 120f);
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get to the " + (s.DescendLadder ? "top of the ladder at " + Fmt(s.DescendTop) : "foot of the ladder at " + Fmt(s.ClimbBase)));
                    s.DescendLadder = false;
                    Plan(ai, c, s, pos, now);
                }
            }
            else if (s.HasDetour && s.UseLadder == null && (Flat(pos, s.Detour) < 1.0f || now > s.DetourUntil)) { s.HasDetour = false; Plan(ai, c, s, pos, now); }
            if (s.RouteDoor != null)
            {
                if (!DoorClosed(s.RouteDoor)) { s.RouteDoor = null; Plan(ai, c, s, pos, now); }
                else if (MHR_Nav.DoorDist(pos, s.RouteDoor.transform.position) <= 2.2f)
                {
                    // open it, then walk through to the far side before asking for a new route
                    Vector3 dp = s.RouteDoor.transform.position, across = dp - s.RouteDoorSpot; across.y = 0f;
                    OpenDoor(ai, c, s.RouteDoor, tag);
                    s.LastRouteDoor = s.RouteDoor; s.LastRouteDoorAt = now;
                    if (across.sqrMagnitude > 0.01f) { s.HasDetour = true; s.Detour = dp + across.normalized * 1.8f; s.DetourUntil = now + 5f; }
                    s.RouteDoor = null; s.NextPlan = 0f; Halt(ai, c, s);
                    return false;
                }
            }
            Vector3 target = NextCorner(s, pos);

            // swimming: head for the bank that's closest to where we're going, unless the path already knows the way
            if (c.IsSwimming() && (!s.PathOk || s.Partial))
            {
                if (now >= s.NextShore) { s.NextShore = now + 0.5f; Vector3 sh; s.HasShore = FindShore(c, pos, goal, out sh); s.Shore = sh; }
                if (s.HasShore) target = s.Shore;
            }

            // 1.4.140: line up with a doorway on the way and walk straight through it, like a player
            if (!c.IsSwimming()) target = DoorwayLine(s, pos, target, now, tag);

            // a closed door right ahead: open it before walking into it
            if (now >= s.NextDoorPeek)
            {
                s.NextDoorPeek = now + 0.4f;
                Door ahead = DoorAhead(pos, target);
                // 1.4.139: look again soon - if the door was still swinging shut it couldn't be opened yet (1.4.138 log: close,
                // "couldn't open", then open 0.4 s later - about a second stood at the door)
                if (ahead != null) { OpenDoor(ai, c, ahead, tag); if (DoorClosed(ahead)) s.NextDoorPeek = now + 0.15f; Halt(ai, c, s); return false; }
                // 1.4.136: an open door whose leaf is in the way (opened from the other side, so it swung towards us): close
                // it like a player would - if the way is through it, the closed door is then opened from this side and
                // swings away (user: kept getting stuck on a door for a moment)
                Door leaf = OpenLeafAhead(pos, target);
                // 1.4.138: held up for a second right by an open door - close it now rather than waiting for the 3 s stuck check
                if (leaf == null && now - s.AnchorTime > 1.0f) leaf = OpenDoorNear(pos, 2.0f);
                // 1.5.73: not in a fight - it can't open it again then (log 702b4b9c: Gunter shut the burial chamber door behind you
                // and stayed outside: "behind a closed door - not opening it in a fight")
                if (leaf != null && tag != CombatTag) { CloseDoor(ai, c, leaf, tag); s.NextDoorPeek = now + 0.15f; Halt(ai, c, s); return false; }
            }

            Vector3 dir = target - pos; dir.y = 0f;
            if (dir.sqrMagnitude < 0.04f && s.Corner < s.Path.Count - 1) { s.Corner++; target = NextCorner(s, pos); dir = target - pos; dir.y = 0f; }
            if (dir.sqrMagnitude < 0.0001f)
            {
                // right on top of where we're steering: let the stuck checks run instead of standing here forever
                Halt(ai, c, s);
                if (now - s.AnchorTime >= StillTime) Unstick(ai, c, s, pos, s.Goal, now, tag, now - s.AnchorTime, now - s.ProgressTime);
                return false;
            }
            dir.Normalize();

            // water on the next stretch: jump narrow streams, take a running leap into wide water
            if (!c.IsSwimming() && c.IsOnGround() && now >= s.NextJump && now >= s.NextWaterPeek)
            {
                s.NextWaterPeek = now + 0.2f;
                if (TryWaterJump(ai, c, s, pos, dir, now, tag)) return false;
            }

            // 1.4.97: going straight and there's a gap right ahead: leap it
            if (s.Direct && !s.Quiet && !c.IsSwimming() && c.IsOnGround() && now >= s.NextJump && GapAhead(c, pos, dir) && !LandingHazard(pos, dir))
            {
                Leap(ai, c, dir, 6.5f);
                s.NextJump = now + 1.2f; s.StepDir = dir; s.StepRun = true; s.StepUntil = now + 0.6f;
                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] jumping the gap at " + Fmt(pos + dir * 0.8f));
                return false;
            }

            // 1.4.88: a fire on the next step (and the goal isn't in it): jump over it like a player when it's small
            // (clear ground 2.5 m on), otherwise go round it
            // 1.5.46: at body height too (the hearth's flames sit above its stone), and only small fires are jumped -
            // a hearth is stepped round (1.5.45 test: it still ended up on the hearth at the very end)
            EffectArea aheadFire = FireArea(pos + dir * 0.8f, 0.3f);
            if (aheadFire != null && !InFire(goal))
            {
                Collider fc = aheadFire.GetComponent<Collider>();
                bool small = fc != null && Mathf.Max(fc.bounds.extents.x, fc.bounds.extents.z) < 0.9f;
                if (small && c.IsOnGround() && now >= s.NextHop && !NearFire(pos + dir * 2.5f, 0.3f) && !DropAhead(pos, dir, 2.5f, 1.2f))
                {
                    s.NextHop = now + 1.0f;
                    ai.transform.rotation = Quaternion.LookRotation(dir);
                    c.Jump(false);
                    Rigidbody rb = ai.GetComponent<Rigidbody>();
                    if (rb != null) { Vector3 v = rb.linearVelocity; v.x = dir.x * 6f; v.z = dir.z * 6f; rb.linearVelocity = v; }
                    s.StepDir = dir; s.StepRun = true; s.StepUntil = now + 0.6f;
                    if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] jumping over a fire at " + Fmt(pos + dir * 1.2f));
                    return false;
                }
                Vector3 round;
                if (FireExit(pos, dir, out round, true)) dir = round;
            }

            // 1.5.74: lava on the next step (and the goal isn't on it): turn along clear ground, or stop at its edge
            if (!c.IsSwimming() && WorldGenerator.IsAshlands(pos.x, pos.z) && (Lava(pos + dir * 0.9f) || Lava(pos + dir * 1.6f)) && !Lava(goal))
            {
                Vector3 round; float rd;
                if (LavaExit(pos + dir * 0.0f, dir, out round, out rd) && Vector3.Dot(round, dir) > -0.2f && LegClearOfLava(pos, pos + round * 1.5f, 0.3f)) dir = round;
                else
                {
                    Halt(ai, c, s);
                    if (now >= s.NextFireLog) { s.NextFireLog = now + 10f; if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stopping at the edge of the lava at " + Fmt(pos + dir * 0.9f)); }
                    if (now - s.AnchorTime >= StillTime) Unstick(ai, c, s, pos, s.Goal, now, tag, now - s.AnchorTime, now - s.ProgressTime);
                    return false;
                }
            }

            // 1.5.72: a spike wall (a built piece that hurts what touches it) on the next step: walk along it, a little away
            // from it, instead of into it - also when the goal is right at it (an enemy caught on the stakes is hit from a
            // step off) (user: "what about defenses like a spike wall?")
            // 1.5.73: further ahead and wider, and right where it stands - a stake wall's hurt zone reaches well out from the
            // stakes (log 93298113: hit 1.6-3.7 m from them following you past); already at its edge, turn away harder
            Collider spike = SpikeNear(pos, c.GetRadius() + 0.35f);
            bool spikeClose = spike != null;
            if (spike == null) spike = SpikeNear(pos + dir * 1.2f, 0.8f);
            if (spike != null)
            {
                Vector3 cp = spike.ClosestPoint(pos); Vector3 off = pos - cp; off.y = 0f;
                if (off.sqrMagnitude > 0.0001f)
                {
                    off.Normalize();
                    Vector3 along = Vector3.Cross(Vector3.up, off);
                    if (Vector3.Dot(along, dir) < 0f) along = -along;
                    dir = (along + off * (spikeClose ? 1.5f : 0.6f)).normalized;
                    if (now >= s.NextFireLog) { s.NextFireLog = now + 10f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] keeping off the spikes at " + Fmt(cp)); }
                }
            }

            // 1.5.2: never walk off a ledge that hurts (user log: a "full" route off the small 57.4 floor of a ladder tower -
            // "fell 7.6 m and took 22 fall damage", twice). Stop at the edge and let the stuck steps choose: a ladder down,
            // a safe drop, another way. Applies to everything that moves through here - following, combat chases, jobs.
            if (!c.IsSwimming() && c.IsOnGround())
            {
                float drop;
                if (LedgeAhead(pos, dir, 0.7f, out drop))
                {
                    Halt(ai, c, s);
                    if (now >= s.NextEdgeLog) { s.NextEdgeLog = now + 5f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] stopping at the edge at " + Fmt(pos + dir * 0.7f) + " (" + drop.ToString("F1") + "m down)"); }
                    if (now - s.AnchorTime >= StillTime) Unstick(ai, c, s, pos, s.Goal, now, tag, now - s.AnchorTime, now - s.ProgressTime);
                    return false;
                }
            }

            float runDist = ModConfig.WorkRunDistance != null ? ModConfig.WorkRunDistance.Value : 25f;
            bool run = allowRun && runDist > 0f && remain > runDist;
            bool walk = remain < 2.5f;
            if (speed == SpeedWalk) { run = false; walk = true; }
            else if (speed == SpeedJog) { run = false; walk = false; }
            else if (speed == SpeedRun) { run = allowRun; walk = false; }
            Steer(ai, c, s, dir, run, walk);
            // 1.4.83: going straight: hop the step in front, like a player
            // 1.4.84: and on a normal route, when something low has stopped us for a moment
            // 1.4.86: walk up stairs like a player; only hop when a step actually holds him up (1.4.85 hopped every step)
            if (now >= s.NextHop && c.IsOnGround() && !c.InAttack() && !c.IsSwimming() && (s.Quiet ? now - s.AnchorTime > 1.5f : (s.Direct || now - s.AnchorTime > 0.5f)) && StepAhead(pos, dir) && !NearFire(pos + dir * 0.8f, 0.4f) && !NearFire(pos + dir * 1.5f, 0.4f))
            {
                s.NextHop = now + 0.6f;
                ai.transform.rotation = Quaternion.LookRotation(dir);
                c.Jump(false);
                Rigidbody rb = ai.GetComponent<Rigidbody>();
                if (rb != null) { Vector3 v = rb.linearVelocity; v.x = dir.x * 4f; v.z = dir.z * 4f; rb.linearVelocity = v; }
            }

            // stuck?
            float still = now - s.AnchorTime, noCloser = now - s.ProgressTime;
            // 1.4.76: keeping pace with a player who's moving isn't getting closer, but isn't stuck either
            if (tag == FollowTag) noCloser = 0f;
            if (still >= StillTime || noCloser >= NoProgressTime) Unstick(ai, c, s, pos, target, now, tag, still, noCloser);
            return false;
        }

        static void Reset(S s, Vector3 goal, Vector3 pos, float now, string tag)
        {
            // 1.5.2: a target that moves about (a skeleton pacing on the tower) mustn't make us forget the ladder on the way to it
            bool keepLadder = s.UseLadder != null && s.Tag == tag && now <= s.LadderUntil && goal.y > pos.y + 1.5f;
            // 1.5.2: same for a ladder climb in progress (the player walking about on the platform above moves the goal)
            bool keepClimb = s.ClimbLadder && s.Tag == tag && now < s.ClimbUntil && goal.y > pos.y + 1.5f;
            bool keepPending = s.LadderPending && s.Tag == tag && now <= s.DetourUntil && (s.DescendLadder ? goal.y < pos.y - 1.5f : goal.y > pos.y + 1.5f);
            bool keepDescend = s.DescendLadder && s.Tag == tag && (keepPending || now < s.DescendUntil) && goal.y < pos.y - 1.0f;
            bool keepChain = s.OnChain && s.Tag == tag && (s.ChainDown ? goal.y < pos.y - 1.0f : goal.y > pos.y + 1.0f);
            float dsUntil = s.DescendUntil; Vector3 dsTop = s.DescendTop;
            float cUntil = s.ClimbUntil, cBest = s.ClimbBestY, cProg = s.ClimbProgressAt, cStart = s.ClimbStartY, cStartAt = s.ClimbStartAt, cBottom = s.ClimbBottomY, dUntil = s.DetourUntil; Vector3 cTop = s.ClimbTop, cDir = s.ClimbDir, cBase = s.ClimbBase;
            bool cAuto = s.ClimbAuto; int cId = s.ClimbLadderId;
            Ladder ladder = s.UseLadder; float ladderUntil = s.LadderUntil;
            s.Goal = goal; s.Dest = goal; s.Tag = tag; s.Path.Clear(); s.Corner = 0; s.NextPlan = 0f; s.PathOk = false; s.Partial = false;
            s.Best = float.MaxValue; s.ProgressTime = now; s.AnchorTime = now; s.Anchor = pos;
            s.Ladder = 0; s.StepUntil = 0f; s.HasDetour = false; s.GaveUp = false; s.DoorBlocked = false; s.RouteDoor = null; s.WaitStamina = false; s.PassDoor = null;
            s.TooTired = false; s.MainBest = float.MaxValue; s.Passes = 0; s.HasShore = false; s.NextShore = 0f;
            s.NextDoorSearch = 0f; s.NextLog = now + 1f; s.NextStartSearch = 0f;
            s.ClimbUntil = 0f; s.ClimbTries = 0; s.ClimbTried.Clear(); s.ClimbRestUntil = 0f; s.Direct = false;
            s.UseLadder = null; s.LadderUntil = 0f; s.NextLadderSearch = 0f;
            s.ClimbLadder = false; s.NextLadderClimbSearch = 0f; s.LadderClimbsTried.Clear(); s.HopUpTries = 0;
            s.LadderPending = false;
            if (!keepChain) s.OnChain = false;
            if (!keepPending) s.PendingChain = null;
            if (keepClimb || keepPending) { s.ClimbTop = cTop; s.ClimbDir = cDir; s.ClimbBase = cBase; s.ClimbAuto = cAuto; s.ClimbLadderId = cId; s.ClimbStartY = cStart; s.ClimbStartAt = cStartAt; s.ClimbBottomY = cBottom; }
            if (keepClimb) { s.ClimbLadder = true; s.ClimbUntil = cUntil; s.ClimbBestY = cBest; s.ClimbProgressAt = cProg; }
            s.DescendLadder = false; s.DescendUntil = 0f;
            if (keepDescend) { s.DescendLadder = true; s.DescendUntil = dsUntil; s.DescendTop = dsTop; s.ClimbBase = cBase; s.ClimbDir = cDir; s.ClimbLadderId = cId; }
            if (keepPending) { s.LadderPending = true; s.HasDetour = true; s.Detour = s.DescendLadder ? dsTop : cBase; s.DetourUntil = dUntil; }
            if (keepLadder) { s.UseLadder = ladder; s.LadderUntil = ladderUntil; s.HasDetour = true; s.Detour = ladder.transform.position; s.DetourUntil = ladderUntil; }
            else s.LaddersUsed.Clear();
        }

        // ---------- ladders ----------
        // 1.5.2: the game's ladders (Black Forest towers and the like) are used, not climbed: standing next to one and using
        // it puts you at its top spot. When the goal is up out of reach (no full route), look for a ladder near us whose
        // top is closer to the goal, walk to it and use it like a player would.
        static bool TryLadder(CompanionAI ai, S s, Vector3 pos, float now, string tag)
        {
            s.NextLadderSearch = now + 2f;
            if (s.UseLadder != null || s.Goal.y < pos.y + 1.5f) return false;
            Ladder best = null; float bestScore = float.MaxValue;
            float here = Vector3.Distance(pos, s.Goal);
            foreach (Ladder l in UnityEngine.Object.FindObjectsByType<Ladder>(FindObjectsSortMode.None))
            {
                if (l == null || l.m_targetPos == null || s.LaddersUsed.Contains(l)) continue;
                Vector3 bottom = l.transform.position, top = l.m_targetPos.position;
                if (Flat(pos, bottom) > 25f || Mathf.Abs(bottom.y - pos.y) > 3f) continue;   // near us, on our level
                if (top.y < pos.y + 1.5f) continue;                                           // it has to take us up
                float after = Vector3.Distance(top, s.Goal);
                if (after > here - 2f) continue;                                               // and closer to the goal
                float score = Vector3.Distance(pos, bottom) + after;
                if (score < bestScore) { bestScore = score; best = l; }
            }
            if (best == null) return false;
            s.UseLadder = best; s.LadderUntil = now + 20f;
            s.HasDetour = true; s.Detour = best.transform.position; s.DetourUntil = s.LadderUntil; s.NextPlan = 0f;
            s.StepUntil = 0f; s.ClimbUntil = 0f;
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + (s.Goal.y - pos.y).ToString("F1") + "m below where it needs to be - going up the ladder at " + Fmt(best.transform.position));
            return true;
        }

        // next to the ladder: use it (moves us to its top). True when it did.
        static bool UseLadderNow(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            Ladder l = s.UseLadder;
            if (l == null || now > s.LadderUntil) { if (s.UseLadder != null || (object)s.UseLadder != null) { if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] couldn't get to the ladder"); } s.UseLadder = null; return false; }
            if (Vector3.Distance(pos, l.transform.position) >= l.m_useDistance - 0.1f) return false;
            Humanoid h = c as Humanoid;
            if (h == null) { s.UseLadder = null; return false; }
            l.Interact(h, false, false);
            s.LaddersUsed.Add(l);
            s.UseLadder = null; s.HasDetour = false; s.NextPlan = 0f; s.Path.Clear();
            s.Ladder = 0; s.Passes = 0; s.MainBest = float.MaxValue; s.Best = float.MaxValue; s.ProgressTime = now; s.AnchorTime = now; s.Anchor = ai.transform.position;
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] went up the ladder to " + Fmt(ai.transform.position));
            return true;
        }

        static void Halt(CompanionAI ai, Character c, S s)
        {
            ai.StopMoving();
            if (c != null && s.Walking) { c.SetWalk(false); s.Walking = false; }
        }

        static void Steer(CompanionAI ai, Character c, S s, Vector3 dir, bool run, bool walk)
        {
            if (walk != s.Walking) { c.SetWalk(walk); s.Walking = walk; }
            ai.MoveTowards(dir, run && !walk);
        }

        // ---------- path ----------
        static void Plan(CompanionAI ai, Character c, S s, Vector3 pos, float now)
        {
            s.NextPlan = now + 1f;
            s.Path.Clear(); s.Corner = 0;
            Vector3 goal = s.HasDetour ? s.Detour : s.RouteDoor != null ? s.RouteDoorSpot : s.Goal;
            if (Vector3.Distance(goal, s.Dest) > 1.5f) { s.NewLeg = true; s.ProgressTime = now; }   // new leg: measure progress from here
            s.Dest = goal;
            s.RouteSwim = 0f;
            bool ok = GetPath(pos, goal, s.Path, MHR_CompanionNavMesh.AgentOrHumanoid, false);
            if (!ok || s.Path.Count == 0)
            {
                s.PathOk = false; s.Partial = true; s.Path.Clear(); s.Path.Add(goal);
                if (!s.HasDetour && s.RouteDoor == null) s.MainPartial = true;
                // 1.4.75: no route can start from where we stand (e.g. in a doorway): step to a spot a meter or two
                // away that a route does start from, right away instead of after the stuck ladder
                Vector3 start;
                if (!s.HasDetour && s.RouteDoor == null && !c.IsSwimming() && now >= s.NextStartSearch)
                {
                    s.NextStartSearch = now + 2f;
                    if (FindRouteStart(pos, goal, out start))
                    {
                        s.HasDetour = true; s.Detour = start; s.DetourUntil = now + 3f;
                        s.Path.Clear(); s.Path.Add(start);
                        if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] no route from " + Fmt(pos) + " - stepping to " + Fmt(start) + " to find one");
                    }
                }
            }
            else
            {
                s.PathOk = true;
                Vector3 end = s.Path[s.Path.Count - 1];
                s.Partial = Flat(end, goal) > 1.5f || Mathf.Abs(end.y - goal.y) > 2.2f;
                // 1.4.82: the last place a full route to the goal started from, for the trapped rescue below
                if (!s.HasDetour && s.RouteDoor == null)
                {
                    s.MainPartial = s.Partial;
                    if (!s.Partial && c.IsOnGround() && !c.IsSwimming()) { s.GoodPos = pos; s.GoodAt = now; s.HasGood = true; }
                }
                // tired: a swim longer than the stamina allows -> a dry route, or wait on the bank
                s.TooTired = false;
                if (!c.IsSwimming())
                {
                    float swim = MHR_Water.SwimMeters(c, pos, s.Path, 60f);
                    s.RouteSwim = swim;
                    // 1.4.110: prefer staying dry, like a player (user: "it can go in the water, it just should prefer not
                    // to if there is a way that is not a long run in another direction"): a dry route that isn't much
                    // longer wins; otherwise swim
                    if (swim > 0.5f)
                    {
                        s_tmp.Clear();
                        float wetLen = PathLength(pos, s.Path);
                        if (GetPath(pos, goal, s_tmp, Pathfinding.AgentType.HumanoidAvoidWater, true))
                        {
                            float dryLen = PathLength(pos, s_tmp);
                            float extra = ModConfig.DryDetour != null ? ModConfig.DryDetour.Value : 30f;
                            if (dryLen <= wetLen * 1.5f + extra)
                            {
                                s.Path.Clear(); s.Path.AddRange(s_tmp); s.Partial = false; swim = 0f; s.RouteSwim = 0f;
                                if (now >= s.NextLog) if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] going round the water (" + dryLen.ToString("F0") + "m) instead of swimming (" + wetLen.ToString("F0") + "m)");
                            }
                            else if (now >= s.NextLog) if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] swimming: the dry way is " + dryLen.ToString("F0") + "m against " + wetLen.ToString("F0") + "m");
                        }
                    }
                    if (swim > 0.5f)
                    {
                        float need = MHR_Water.SwimStamina(c, swim) * 1.25f + 5f;
                        float max = MaxStamina(ai);
                        if (max > 0f) need = Mathf.Min(need, max * 0.9f);
                        if (now >= s.NoWaitUntil && Stamina(ai) < need)
                        {
                            s_tmp.Clear();
                            if (GetPath(pos, goal, s_tmp, Pathfinding.AgentType.HumanoidAvoidWater, true))
                            {
                                s.Path.Clear(); s.Path.AddRange(s_tmp); s.Partial = false;
                                if (now >= s.NextLog) if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] too tired to swim " + swim.ToString("F0") + "m - going around the water");
                            }
                            else { s.TooTired = true; s.NeedStamina = need; }   // stops at the water's edge (see TryWaterJump)
                        }
                    }
                }
            }
            // 1.4.83: a long way round for something close (1.4.82 log: 85-120 m round the base for the steps in front of
            // it, 4 m away - the route can't use the steps): if the straight line only has steps a player would hop up,
            // go straight and jump them
            bool wasDirect = s.Direct;
            s.Direct = false;
            if (!s.HasDetour && s.RouteDoor == null && !c.IsSwimming() && now >= s.NoDirectUntil)
            {
                float straight = Vector3.Distance(pos, goal);
                float len = Remaining(s, pos);
                int hops = 0; s_hopWhy = null;
                // 1.4.84: also a short way round something low that's in the way (a log, a low wall): a player hops it
                bool longWay = s.Partial || len > straight * 1.6f + 6f;
                bool line = straight < 25f && (longWay || len > straight + 2f) && HopLine(pos, goal, out hops, c);
                if (!line && straight < 25f && longWay && s_hopWhy != null && now >= s.NextLineLog)
                {
                    s.NextLineLog = now + 15f;
                    if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] taking the " + len.ToString("F0") + "m way round, not straight (" + straight.ToString("F0") + "m): " + s_hopWhy);
                }
                if (line && (longWay || hops > 0))
                {
                    if (!wasDirect) if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] going straight to " + Fmt(goal) + " (" + hops + " step(s) to hop) instead of " + (s.Partial ? "no route" : "a " + len.ToString("F0") + "m way round"));
                    s.Path.Clear(); s.Path.Add(goal); s.Corner = 0; s.PathOk = true; s.Partial = false; s.MainPartial = false; s.Direct = true; s.RouteSwim = 0f;
                }
            }
            // no full route: maybe a closed door is in the way (closed doors block the route); go open it like a player would
            // 1.4.79: not while following - a door 20 m the other way isn't how you keep up with someone (1.4.78 log);
            // MHR_Follow walks the player's own path instead when the route doesn't reach its spot
            if (s.Partial && s.Tag != FollowTag && s.Tag != CombatTag && s.RouteDoor == null && !s.HasDetour && now >= s.NextDoorSearch)
            {
                s.NextDoorSearch = now + 4f;
                // 1.4.78: not the door we just came through - if the route still doesn't reach the goal from the other
                // side, that door isn't the answer (1.4.77 log: back and forth through the same gate six times)
                Door skip = now - s.LastRouteDoorAt < 30f ? s.LastRouteDoor : null;
                Vector3 spot; Door d = RouteDoor(pos, goal, out spot, skip);
                if (d != null) { s.RouteDoor = d; s.RouteDoorSpot = spot; s.NextPlan = 0f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] no way through - heading for the closed " + d.m_name + " at " + Fmt(d.transform.position)); }
                // 1.4.84: no door either: a spot the route does reach, from which the rest can be hopped (stairs)
                else if (!c.IsSwimming())
                {
                    Vector3 b;
                    if (FindHopBridge(pos, goal, out b) && Flat(pos, b) > 1.0f)
                    {
                        s.HasDetour = true; s.Detour = b; s.DetourUntil = now + 10f + Flat(pos, b) * 0.5f; s.NextPlan = 0f;
                        if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] no route to " + Fmt(goal) + " - going to " + Fmt(b) + " to hop the rest");
                    }
                }
            }
            AvoidFire(s, pos, goal, now);
            AvoidLava(s, pos, goal, now);
        }

        // 1.5.74: a route over lava goes round it through clear ground (a ring of spots round where it starts); none -> a job
        // leaves the errand, following you it keeps to the edge step by step
        static void AvoidLava(S s, Vector3 pos, Vector3 goal, float now)
        {
            if (s.Path.Count == 0 || !WorldGenerator.IsAshlands(pos.x, pos.z) || Lava(goal)) return;
            try
            {
                Vector3 prev = pos;
                for (int i = 0; i < s.Path.Count && i < 40; i++)
                {
                    Vector3 next = s.Path[i];
                    Vector3 leg = next - prev; leg.y = 0f;
                    float len = leg.magnitude;
                    if (len > 0.05f)
                    {
                        Vector3 dir = leg / len;
                        for (float t = 0.3f; t < len; t += 0.5f)
                        {
                            Vector3 p = prev + dir * t;
                            if (!Lava(p)) continue;
                            List<Vector3> way;
                            if (LavaRing(p, prev, next, i == 0, out way))
                            {
                                s.Path.InsertRange(i, way);
                                if (now >= s.NextFireLog) { s.NextFireLog = now + 10f; if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + s.Tag + "] going round the lava at " + Fmt(p) + " by " + way.Count + " spots"); }
                                return;
                            }
                            if (s.Tag != FollowTag)
                            {
                                s.GaveUp = true; s.GaveUpAt = now;
                                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + s.Tag + "] the only way to " + Fmt(goal) + " is over lava at " + Fmt(p) + " - not going");
                                return;
                            }
                            if (now >= s.NextFireLog) { s.NextFireLog = now + 10f; if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + s.Tag + "] the route crosses lava at " + Fmt(p) + " and there's no clear way round - keeping to its edge"); }
                            return;
                        }
                    }
                    prev = next;
                }
            }
            catch { }
        }

        static bool LavaRing(Vector3 c, Vector3 from, Vector3 next, bool fromIsHere, out List<Vector3> way)
        {
            way = null;
            const int N = 16;
            float[] rings = { 3f, 6f, 10f };
            int count = N * rings.Length;
            Vector3[] pt = new Vector3[count];
            bool[] ok = new bool[count];
            for (int ring = 0; ring < rings.Length; ring++)
                for (int k = 0; k < N; k++)
                {
                    float ang = k * Mathf.PI * 2f / N;
                    Vector3 p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rings[ring];
                    Vector3 g;
                    int idx = ring * N + k;
                    ok[idx] = GroundAt(p, from.y, out g) && !Lava(g) && MHR_Water.DepthAt(g) < 0.5f;
                    pt[idx] = g;
                }
            int S0 = count, T = count + 1;
            float[] dist = new float[count + 2];
            int[] back = new int[count + 2];
            bool[] done = new bool[count + 2];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; back[i] = -1; }
            dist[S0] = 0f;
            Vector3 P(int i) { return i == S0 ? from : i == T ? next : pt[i]; }
            for (int iter = 0; iter < count + 2; iter++)
            {
                int u = -1; float best = float.MaxValue;
                for (int i = 0; i < dist.Length; i++) if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
                if (u < 0 || u == T) break;
                done[u] = true;
                for (int v = 0; v < count + 2; v++)
                {
                    if (done[v] || v == S0 || (v < count && !ok[v])) continue;
                    if (u < count && v < count)
                    {
                        int du = Mathf.Abs((u % N) - (v % N)); du = Mathf.Min(du, N - du);
                        if (du > 1) continue;
                    }
                    if (u == S0 && v == T) continue;
                    Vector3 a = P(u), b = P(v);
                    float d = Vector3.Distance(a, b);
                    if (dist[u] + d >= dist[v]) continue;
                    if (!CanSee(a, b) || !LegClearOfLava(a, b, u == S0 && fromIsHere ? 0.6f : 0.3f)) continue;
                    dist[v] = dist[u] + d; back[v] = u;
                }
            }
            if (back[T] < 0) return false;
            way = new List<Vector3>();
            for (int i = back[T]; i >= 0 && i != S0; i = back[i]) way.Insert(0, pt[i]);
            return way.Count > 0;
        }

        // 1.5.44: the route goes round a hearth's or campfire's collider, but the flames (the Burning area) reach further, so
        // it walked through the edge of the fire and stood in it (1.5.42/1.5.43 logs: 10 fire damage a tick, the hearth
        // damaged too). Walk each leg in 0.4 m steps; where one enters a fire, put two corners round it on the side that's
        // clear (ground, out of the flames, in sight of the corner before). Not when the goal itself is in the fire.
        static void AvoidFire(S s, Vector3 pos, Vector3 goal, float now)
        {
            if (s.Path.Count == 0 || InFire(goal) || InFire(pos)) return;
            try
            {
                Vector3 prev = pos;
                for (int i = 0; i < s.Path.Count && i < 40; i++)
                {
                    Vector3 next = s.Path[i];
                    Vector3 leg = next - prev; leg.y = 0f;
                    float len = leg.magnitude;
                    if (len > 0.05f)
                    {
                        Vector3 dir = leg / len;
                        for (float t = 0.2f; t < len; t += 0.4f)
                        {
                            Vector3 p = prev + dir * t; p.y = Mathf.Lerp(prev.y, next.y, t / len);
                            EffectArea fire = FireArea(p, 0.5f);
                            if (fire == null) continue;
                            Vector3 a, b;
                            List<Vector3> way;
                            if (FireBypass(fire, prev, dir, next, out a, out b))
                            {
                                s.Path.Insert(i, b); s.Path.Insert(i, a);
                                if (now >= s.NextFireLog) { s.NextFireLog = now + 10f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] going round the fire at " + Fmt(fire.transform.position) + " via " + Fmt(a) + " and " + Fmt(b)); }
                                return;
                            }
                            // 1.5.70: a ring of spots round the fire, the shortest chain of clear stretches through them (log
                            // bced720b: the two-corner way round didn't fit in the room, it steered step by step onto the stone)
                            if (FireRing(fire, prev, next, i == 0, out way))
                            {
                                s.Path.InsertRange(i, way);
                                if (now >= s.NextFireLog) { s.NextFireLog = now + 10f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] going round the fire at " + Fmt(fire.transform.position) + " by " + way.Count + " spots round it"); }
                                return;
                            }
                            // no way round: a job leaves this errand (it skips that chest/target - Repair and Restock sorting
                            // between chests either side of the hearth); following you it steers round step by step
                            if (s.Tag != FollowTag)
                            {
                                s.GaveUp = true; s.GaveUpAt = now;
                                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] the only way to " + Fmt(goal) + " is through the fire at " + Fmt(fire.transform.position) + " - not going");
                                return;
                            }
                            if (now >= s.NextFireLog) { s.NextFireLog = now + 10f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + s.Tag + "] the route passes the fire at " + Fmt(fire.transform.position) + " and there's no clear way round it - steering round step by step"); }
                            return;
                        }
                    }
                    prev = next;
                }
            }
            catch { }
        }

        // 1.5.70: sized by the fire piece itself (the hearth stone, not just its flames), every leg of the way round checked
        // for flames (from -> a -> b -> the next corner), pushed out half a metre at a time until it's clear (log 188ac60a:
        // the two corners were clear but the walk between them cut across the hearth stone into the fire, 7 times)
        static bool FireBypass(EffectArea fire, Vector3 from, Vector3 dir, Vector3 next, out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            Vector3 c;
            float r = FireReach(fire, out c);
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);
            for (float extra = 0.8f; extra <= 3.3f; extra += 0.5f)
            {
                float rr = r + extra;
                float best = float.MaxValue; bool found = false;
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 pa = c + side * (k * rr) - dir * rr * 0.75f;
                    Vector3 pb = c + side * (k * rr) + dir * rr * 0.75f;
                    Vector3 ga, gb;
                    if (!GroundAt(pa, from.y, out ga) || !GroundAt(pb, from.y, out gb)) continue;
                    if (NearFire(ga, 0.5f) || NearFire(gb, 0.5f)) continue;
                    if (!CanSee(from, ga) || !CanSee(ga, gb)) continue;
                    if (!LegClearOfFire(from, ga) || !LegClearOfFire(ga, gb) || !LegClearOfFire(gb, next)) continue;
                    float d = Vector3.Distance(from, ga);
                    if (d < best) { best = d; a = ga; b = gb; found = true; }
                }
                if (found) return true;
            }
            return false;
        }

        // the fire's middle and how far its piece, flames and damage zone reach out from it
        static float FireReach(EffectArea fire, out Vector3 c)
        {
            Collider col = fire.GetComponent<Collider>();
            c = col != null ? col.bounds.center : fire.transform.position;
            float r = col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) : 2f;
            Piece piece = fire.GetComponentInParent<Piece>();
            if (piece != null)
            {
                foreach (Collider pc in piece.GetComponentsInChildren<Collider>())
                {
                    if (pc == null) continue;
                    Vector3 off = pc.bounds.center - c; off.y = 0f;
                    r = Mathf.Max(r, off.magnitude + Mathf.Max(pc.bounds.extents.x, pc.bounds.extents.z));
                }
            }
            return Mathf.Min(r, 4f);
        }

        static bool FireRing(EffectArea fire, Vector3 from, Vector3 next, bool fromIsHere, out List<Vector3> way)
        {
            way = null;
            Vector3 c;
            float r = FireReach(fire, out c);
            const int N = 12;
            float[] rings = { r + 0.9f, r + 1.7f };
            int count = N * rings.Length;
            Vector3[] pt = new Vector3[count];
            bool[] ok = new bool[count];
            for (int ring = 0; ring < rings.Length; ring++)
                for (int k = 0; k < N; k++)
                {
                    float ang = k * Mathf.PI * 2f / N;
                    Vector3 p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rings[ring];
                    Vector3 g;
                    int idx = ring * N + k;
                    ok[idx] = GroundAt(p, from.y, out g) && !NearFire(g, 0.5f);
                    pt[idx] = g;
                }
            // Dijkstra: node `count` is where it is, `count + 1` the next corner
            int S0 = count, T = count + 1;
            float[] dist = new float[count + 2];
            int[] back = new int[count + 2];
            bool[] done = new bool[count + 2];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; back[i] = -1; }
            dist[S0] = 0f;
            Vector3 P(int i) { return i == S0 ? from : i == T ? next : pt[i]; }
            for (int iter = 0; iter < count + 2; iter++)
            {
                int u = -1; float best = float.MaxValue;
                for (int i = 0; i < dist.Length; i++) if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
                if (u < 0 || u == T) break;
                done[u] = true;
                for (int v = 0; v < count + 2; v++)
                {
                    if (done[v] || v == S0 || (v < count && !ok[v])) continue;
                    if (u < count && v < count)
                    {
                        // round the ring: the next spot either way, or the same/next angle on the other ring
                        int du = Mathf.Abs((u % N) - (v % N)); du = Mathf.Min(du, N - du);
                        if (du > 1) continue;
                    }
                    if (u == S0 && v == T) continue;
                    Vector3 a = P(u), b = P(v);
                    float d = Vector3.Distance(a, b);
                    if (dist[u] + d >= dist[v]) continue;
                    if (!CanSee(a, b) || !LegClearOfFire(a, b, u == S0 && fromIsHere ? 0.6f : 0.2f)) continue;
                    dist[v] = dist[u] + d; back[v] = u;
                }
            }
            if (back[T] < 0) return false;
            way = new List<Vector3>();
            for (int i = back[T]; i >= 0 && i != S0; i = back[i]) way.Insert(0, pt[i]);
            return way.Count > 0;
        }

        static bool LegClearOfFire(Vector3 p0, Vector3 p1, float skipStart = 0.2f)
        {
            Vector3 leg = p1 - p0; leg.y = 0f;
            float len = leg.magnitude;
            if (len < 0.05f) return FireArea(p0, 0.5f) == null;
            Vector3 dir = leg / len;
            for (float t = skipStart; t < len; t += 0.4f)
            {
                Vector3 p = p0 + dir * t; p.y = Mathf.Lerp(p0.y, p1.y, t / len);
                if (FireArea(p, 0.5f) != null) return false;
            }
            return FireArea(p1, 0.5f) == null;
        }

        static bool GroundAt(Vector3 p, float nearY, out Vector3 ground)
        {
            ground = p;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(p.x, nearY + 1.5f, p.z), Vector3.down, out hit, 4f, Mask())) return false;
            if (Mathf.Abs(hit.point.y - nearY) > 1.2f) return false;
            ground = hit.point;
            return true;
        }

        static float PathLength(Vector3 from, List<Vector3> path)
        {
            float len = 0f; Vector3 prev = from;
            for (int i = 0; i < path.Count; i++) { len += Vector3.Distance(prev, path[i]); prev = path[i]; }
            return len;
        }

        // 1.5.74: a walking route that gets there, and how long it is (for "can a melee weapon get at it")
        static readonly List<Vector3> s_walkPath = new List<Vector3>();
        public static bool WalkRoute(Vector3 from, Vector3 to, out float len)
        {
            len = 0f;
            if (!GetPath(from, to, s_walkPath, MHR_CompanionNavMesh.AgentOrHumanoid, false)) return false;
            Vector3 end = s_walkPath[s_walkPath.Count - 1];
            if (Flat(end, to) > 1.5f || Mathf.Abs(end.y - to.y) > 2.2f) return false;
            len = PathLength(from, s_walkPath);
            return true;
        }

        static bool GetPath(Vector3 from, Vector3 to, List<Vector3> path, Pathfinding.AgentType agent, bool requireFull)
        {
            path.Clear();
            return Pathfinding.instance != null && Pathfinding.instance.GetPath(from, to, path, agent, requireFull, true, false) && path.Count > 0;
        }

        // 1.4.112: swimming, the route's corners are on the ground under the water (or on the surface) - only the flat
        // distance counts. 1.4.111 test: the first corner was right below him, never "reached" because of the height,
        // so he steered at a point under his feet, stood still in the water and never even ran the stuck checks.
        static bool s_swimming;
        static void AdvanceCorner(S s, Vector3 pos)
        {
            while (s.Corner < s.Path.Count - 1)
            {
                Vector3 p = s.Path[s.Corner];
                if (Flat(pos, p) < 0.6f && (s_swimming || Mathf.Abs(pos.y - p.y) < 1.6f)) s.Corner++;
                else break;
            }
        }
        static Vector3 NextCorner(S s, Vector3 pos)
        {
            if (s.Path.Count == 0) return s.Dest;
            int i = Mathf.Clamp(s.Corner, 0, s.Path.Count - 1);
            // last corner of a partial path reached: keep pressing toward where we're going
            if (i == s.Path.Count - 1 && Flat(pos, s.Path[i]) < 0.8f) return s.Dest;
            return s.Path[i];
        }
        static float Remaining(S s, Vector3 pos)
        {
            if (s.Path.Count == 0) return Vector3.Distance(pos, s.Dest);
            float d = 0f; Vector3 prev = pos;
            for (int i = Mathf.Clamp(s.Corner, 0, s.Path.Count - 1); i < s.Path.Count; i++) { d += Vector3.Distance(prev, s.Path[i]); prev = s.Path[i]; }
            if (s.Partial) d += Vector3.Distance(prev, s.Dest);
            return d;
        }

        // ---------- stuck ladder ----------
        static void Unstick(CompanionAI ai, Character c, S s, Vector3 pos, Vector3 target, float now, string tag, float still, float noCloser)
        {
            string why = still >= StillTime ? "standing still " + still.ToString("F0") + "s" : "no closer for " + noCloser.ToString("F0") + "s";
            s.AnchorTime = now; s.Anchor = pos;
            s.ProgressTime = now - (NoProgressTime - 4f);   // next check in ~4s unless we start getting somewhere
            // 1.4.83: stuck going straight: that way doesn't work after all, take the route for a while
            if (s.Direct) { s.Direct = false; s.NoDirectUntil = now + 20f; s.NextPlan = 0f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] couldn't get through going straight at " + Fmt(pos) + " - taking the route"); return; }
            int step = s.Ladder++;
            Vector3 dir = target - pos; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = ai.transform.forward;
            dir.Normalize();
            // 1.5.2: a ladder up to where it needs to be comes first
            if (step == 0 && TryLadder(ai, s, pos, now, tag)) { s.Ladder = 0; return; }
            if (step == 0 && TryStartLadderClimb(ai, c, s, pos, now, tag)) { s.Ladder = 0; return; }
            if (step == 0 && TryHopUp(ai, c, s, pos, now, tag)) { s.Ladder = 0; return; }
            if (step == 0 && TryLadderDown(ai, c, s, pos, now, tag)) { s.Ladder = 0; return; }
            if (step == 0 && TryDropDown(ai, c, s, pos, now, tag)) { s.Ladder = 0; return; }
            // 1.4.82: down in a pit with no route out: climb out like a player before anything else
            if (step == 0 && TryStartClimb(ai, c, s, pos, now, tag, why)) { s.Ladder = 0; return; }
            switch (step)
            {
                case 0:
                    {
                        Door d = DoorNear(pos, 2.6f);
                        if (d != null) { OpenDoor(ai, c, d, tag); s.NextPlan = now + 0.6f; return; }
                        // 1.4.137: any open door right next to us when we're stuck (1.4.136 test: the leaf wasn't on the line to
                        // the next corner, so the look-ahead missed it and he stood there, then hopped round it)
                        Door leaf = OpenLeafAhead(pos, target) ?? OpenDoorNear(pos, 2.5f);
                        if (leaf != null && tag != CombatTag) { CloseDoor(ai, c, leaf, tag); s.NextPlan = now + 0.6f; return; }
                        Sidestep(ai, c, s, pos, dir, now, tag, why, true);
                        return;
                    }
                case 1: Hop(ai, c, s, pos, dir, now, tag, why); return;
                case 2: Sidestep(ai, c, s, pos, dir, now, tag, why, false); return;
                case 3: Hop(ai, c, s, pos, dir, now, tag, why); return;
                case 4:
                    {
                        // after two full rounds of this without getting any closer, stop
                        if (++s.Passes > 2) { s.Ladder = 99; Unstick(ai, c, s, pos, target, now, tag, still, noCloser); return; }
                        Vector3 det;
                        if (FindDetour(pos, s.Goal, dir, out det)) { s.HasDetour = true; s.Detour = det; s.DetourUntil = now + 6f; s.NextPlan = 0f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] stuck (" + why + ") at " + Fmt(pos) + " - trying another way via " + Fmt(det)); return; }
                        Hop(ai, c, s, pos, dir, now, tag, why);
                        return;
                    }
                case 5: Hop(ai, c, s, pos, dir, now, tag, why); s.Ladder = s.Passes >= 2 ? 6 : 0; return;
                default:
                    s.GaveUp = true; s.GaveUpAt = now;
                    Halt(ai, c, s);
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get to " + Fmt(s.Goal) + " from " + Fmt(pos) + " - giving up");
                    // 1.5.42: say so when it's about you or your order (following, a move order, a fight, a hunt); for a job
                    // it's only the reason in its status (user, speech review: "I can't get up there")
                    {
                        bool forPlayer = tag == FollowTag || tag == "Command" || tag == "Combat" || tag == "Hunt";
                        CompanionStatus.Why(ai, s.Goal.y - pos.y > 2f ? "hc_why_cant_reach_up" : "hc_why_no_way", speak: forPlayer);
                    }
                    Rescue(ai, c, s, pos, now, tag);
                    return;
            }
        }

        static void Sidestep(CompanionAI ai, Character c, S s, Vector3 pos, Vector3 dir, float now, string tag, string why, bool leftFirst)
        {
            Vector3 back = -dir;
            Vector3 left = Quaternion.Euler(0f, -45f, 0f) * back, right = Quaternion.Euler(0f, 45f, 0f) * back;
            Vector3 a = leftFirst ? left : right, b = leftFirst ? right : left;
            Vector3 side = Quaternion.Euler(0f, 90f, 0f) * dir;
            // 1.5.69: already swimming - deep water round it isn't a drop to stay out of
            if (c.IsSwimming())
            {
                Vector3 wpick = Clear(pos, a) ? a : Clear(pos, b) ? b : Clear(pos, side) ? side : -side;
                s.StepDir = wpick; s.StepRun = false; s.StepUntil = now + 1.2f;
                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] stuck in the water (" + why + ") at " + Fmt(pos) + " - swimming to the side");
                return;
            }
            if (DropAhead(pos, side, 1.5f, 1.2f)) side = DropAhead(pos, -side, 1.5f, 1.2f) ? Vector3.zero : -side;   // 1.4.82: never off a ledge
            // 1.5.2: never step back off a ledge either (user log: stuck at the top of a ladder, it stepped back down the shaft)
            Vector3 pick = SafeClear(pos, a) ? a : SafeClear(pos, b) ? b : SafeClear(pos, back) ? back : side;
            s.StepDir = pick; s.StepRun = false; s.StepUntil = now + 1.2f;
            if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] stuck (" + why + ") at " + Fmt(pos) + " - stepping back to the side");
        }

        // ---------- climbing out ----------
        // 1.4.82: like a player in a dry moat: find the lowest / least steep way up within reach, sprint at it and keep
        // jumping. Non-player characters never slide (Character.GetSlideAngle is 90 for them), and anything flatter
        // than ~84 degrees counts as ground to jump from, so jumping up a steep face works for them as it does for a player.
        const int ClimbMaxTries = 3;
        const float ClimbMaxRise = 10f;
        static readonly int s_terrainLayer = LayerMask.NameToLayer("terrain");

        public static bool Climbing(CompanionAI ai) { S s = Peek(ai); return s != null && Time.time < s.ClimbUntil && Time.time - s.LastGo < 0.5f; }

        // 1.5.2: a built ladder (Valheim's wooden ladders are steep stairs a player hops up - user: "The player kind of hops
        // up them"). The route planner can't use them and climb-out won't try a built rise over 1 m, so a companion stood at
        // the foot of one (user log: following him up a Black Forest outpost ladder, 12 m up, sidestepping at the bottom).
        // Found by name ("ladder" in the object or piece name): walk at it and keep pushing into it, hopping, up to the top.
        // 1.5.2: ladders that didn't get a companion up (a short one, one that leads nowhere) are skipped for a few minutes
        const int LadderJumpTries = 4;
        static readonly List<Vector3> s_ladderPath = new List<Vector3>();

        // 1.5.2 (user: "Ignore ladders that cause it to get stuck"): the same ladder started a fourth time inside two minutes
        // isn't getting it anywhere (it climbs, can't go on, ends up back at the bottom) - leave it alone for a few minutes
        static void NoteLadderClimb(S s, float now, string tag)
        {
            Vector2 u;
            if (!s.LadderUses.TryGetValue(s.ClimbLadderId, out u) || now - u.y > 120f) u = new Vector2(0f, now);
            u.x += 1f;
            s.LadderUses[s.ClimbLadderId] = u;
            if (u.x >= 4f)
            {
                MarkBadLadder(s.ClimbLadderId, now, 180f); s.LadderUses.Remove(s.ClimbLadderId);
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] this ladder keeps ending up back where it started - leaving it for a few minutes");
            }
        }
        static readonly Dictionary<int, float> s_badLadders = new Dictionary<int, float>();
        static void MarkBadLadder(int id, float now, float secs) { if (id != 0) s_badLadders[id] = now + secs; }
        static bool BadLadder(int id, float now) { float until; return s_badLadders.TryGetValue(id, out until) && now < until; }

        // 1.5.3: the object a ladder collider belongs to. A built ladder is its Piece. A ladder that is part of the world (user:
        // "a spot with ladders and a mob at the top" - a ruined tower; the log listed no pieces at all, it used to take
        // the whole location as the "ladder") is the nearest object up from the collider that is named a ladder or holds
        // the ladder's auto-jump - and only if it's ladder-sized, never the building around it.
        static GameObject LadderRoot(Collider col, out Piece piece)
        {
            piece = col.GetComponentInParent<Piece>();
            if (piece != null) return piece.gameObject;
            Transform t = col.transform;
            for (int i = 0; i < 4 && t != null; i++, t = t.parent)
            {
                bool named = t.name.ToLowerInvariant().Contains("ladder");
                if (!named && t.GetComponentInChildren<AutoJumpLedge>(true) == null) continue;
                Bounds b;
                if (!OwnBounds(t.gameObject, out b) || b.size.y > 14f || b.size.x > 4f && b.size.z > 4f) return null;
                return t.gameObject;
            }
            return null;
        }

        static bool IsLadder(GameObject root, Piece piece, Collider col)
        {
            string n = (root.name + " " + (col != null ? col.gameObject.name : "") + " " + (piece != null ? piece.m_name : "")).ToLowerInvariant();
            return n.Contains("ladder") || root.GetComponentInChildren<AutoJumpLedge>(true) != null;
        }

        // one ladder piece: its solid parts plus the auto-jump trigger (where it actually lifts you)
        static bool OwnBounds(GameObject root, out Bounds b)
        {
            b = default(Bounds); bool any = false;
            foreach (Collider o in root.GetComponentsInChildren<Collider>())
            {
                if (o == null || !o.enabled) continue;
                if (o.isTrigger && o.GetComponentInParent<AutoJumpLedge>() == null) continue;
                if (!any) { b = o.bounds; any = true; } else b.Encapsulate(o.bounds);
            }
            return any;
        }

        // 1.5.2: a ladder's full height, counting ladder pieces stacked on top of it or under it (user's outpost: three
        // ladders one above the other read as three short ones, so he got off each one partway up). id = the bottom piece.
        static Bounds LadderBounds(GameObject root, out int id)
        {
            id = root.GetInstanceID();
            Bounds b;
            if (!OwnBounds(root, out b)) return b;
            var used = new HashSet<int> { id };
            for (int dirUp = 0; dirUp < 2; dirUp++)
            {
                Bounds end = b; GameObject endObj = root;
                for (int i = 0; i < 8; i++)
                {
                    float y = dirUp == 0 ? end.max.y : end.min.y;
                    GameObject next = null; Bounds nb = default(Bounds);
                    foreach (Collider o in Physics.OverlapSphere(new Vector3(end.center.x, y, end.center.z), 1.0f, Mask(), QueryTriggerInteraction.Collide))
                    {
                        if (o == null) continue;
                        Piece p;
                        GameObject r = LadderRoot(o, out p);
                        if (r == null || used.Contains(r.GetInstanceID()) || !IsLadder(r, p, o)) continue;
                        Bounds ob;
                        if (!OwnBounds(r, out ob) || Flat(ob.center, end.center) > 0.8f) continue;
                        bool joins = dirUp == 0 ? ob.min.y > end.max.y - 1.5f && ob.min.y < end.max.y + 0.8f
                                                : ob.max.y < end.min.y + 1.5f && ob.max.y > end.min.y - 0.8f;
                        if (joins) { next = r; nb = ob; break; }
                    }
                    if (next == null) break;
                    used.Add(next.GetInstanceID()); b.Encapsulate(nb); end = nb; endObj = next;
                }
                if (dirUp == 1) id = endObj.GetInstanceID();
            }
            return b;
        }

        // 1.5.2: where a ladder is climbed from, on the side its auto-jump faces. A floor at about the ladder's bottom just in
        // front of it means it's climbed from that floor; otherwise from the ground below, with a jump. room = nothing
        // overhead to bang into (user: "standing under the small floor section between the 2 ladders and just jumping in
        // place ... kept hitting the floor tile just above their head").
        static bool LadderFoot(Bounds b, Vector3 f, out Vector3 spot, out bool room)
        {
            float depth = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.z) * b.extents.z;
            Vector3 c0 = new Vector3(b.center.x, b.min.y, b.center.z);
            RaycastHit g;
            spot = c0 - f * (depth + 0.7f); room = true;
            bool found = false;
            foreach (float d in new[] { 0.7f, 0.4f, 1.0f, 1.3f })
            {
                if (found) break;
                Vector3 p = c0 - f * (depth + d);
                if (Physics.Raycast(new Vector3(p.x, b.min.y + 0.6f, p.z), Vector3.down, out g, 1.2f, Mask())) { spot = new Vector3(p.x, g.point.y, p.z); found = true; }
            }
            if (!found)
            {
                if (!Physics.Raycast(spot + Vector3.up * 0.6f, Vector3.down, out g, 5f, Mask())) return false;
                spot.y = g.point.y;
            }
            // head room where it stands and on the way in (a jump needs about 2 m clear)
            for (float d = 0f; d <= 0.31f && room; d += 0.3f)
                foreach (RaycastHit h in Physics.RaycastAll(spot + f * d + Vector3.up * 0.3f, Vector3.up, 2.0f, Mask(), QueryTriggerInteraction.Ignore))
                {
                    Piece hp;
                    GameObject hr = LadderRoot(h.collider, out hp);
                    if (hr != null && IsLadder(hr, hp, h.collider)) continue;   // the ladder itself
                    room = false; break;
                }
            return true;
        }

        // 1.5.3: floor (or a ladder) under every step of a straight walk, within a metre below - staying on the floor
        // sections, not just "not a deep drop"; two misses in a row (0.4 m, wider than a seam) is off the floor
        static bool FloorAlong(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.05f) return true;
            d /= len; int miss = 0;
            for (float x = 0.2f; x <= len + 0.01f; x += 0.2f)
            {
                Vector3 p = a + d * Mathf.Min(x, len);
                float top = Mathf.Max(a.y, b.y) + 0.6f;
                if (Physics.SphereCast(new Vector3(p.x, top, p.z), 0.25f, Vector3.down, out _, top - (Mathf.Min(a.y, b.y) - 1.0f), Mask())) miss = 0;
                else if (++miss >= 2) return false;
            }
            return true;
        }

        // a corner point within 1.5 m with floor from here to it and from it to the target, shortest way first
        static bool FindFloorVia(Vector3 pos, Vector3 target, out Vector3 via)
        {
            via = Vector3.zero; float best = float.MaxValue;
            for (float r = 0.6f; r <= 1.51f; r += 0.45f)
                for (int k = 0; k < 16; k++)
                {
                    Vector3 w = pos + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * r;
                    RaycastHit g;
                    if (!Physics.SphereCast(new Vector3(w.x, pos.y + 1.0f, w.z), 0.25f, Vector3.down, out g, 1.8f, Mask())) continue;
                    if (Mathf.Abs(g.point.y - pos.y) > 0.6f) continue;
                    w.y = g.point.y;
                    float len = Flat(pos, w) + Flat(w, target);
                    if (len >= best || !FloorAlong(pos, w) || !FloorAlong(w, target)) continue;
                    best = len; via = w;
                }
            return best < float.MaxValue;
        }

        // ---------- 1.5.3: ladder chains ----------
        // A tower of short ladders with small landings between (the user's Black Forest ruin) reads to a player as one
        // staircase. Climbing them one at a time - find, walk to, climb, stop, look around, find the next - paused on every
        // landing and gave a wrong choice (a wall top, a step off the edge) a chance at each one. Instead: plan the chain
        // once, climb it without stopping, and remember chains that worked so a fall or the next visit reuses them.
        internal class LadderStep { public Vector3 Foot, Dir, TopSpot; public float Bottom, Top, Run; public int Id; public bool LinedUp; }
        class KnownChain { public List<LadderStep> Steps; public float Until; }
        static readonly List<KnownChain> s_knownChains = new List<KnownChain>();

        static LadderStep MakeStep(Bounds b, Vector3 foot, Vector3 dir, int id, GameObject root)
        {
            // how far along its direction the ladder's top end is from the foot spot
            float depth = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
            Vector3 c0 = new Vector3(b.center.x, foot.y, b.center.z) + dir * depth;
            float run = Vector3.Dot(new Vector3(c0.x - foot.x, 0f, c0.z - foot.z), dir);
            AutoJumpLedge[] ledges = root != null ? root.GetComponentsInChildren<AutoJumpLedge>(true) : null;
            // 1.5.3: stand where the lift zone starts (user log: "nearest lift zone 1.24m / 2.03m away" from the old foot
            // spot) - just short of the trigger's nearest point, on the floor
            Vector3 zone;
            if (LiftZoneEdge(ledges, foot, dir, out zone)) { run -= Vector3.Dot(new Vector3(zone.x - foot.x, 0f, zone.z - foot.z), dir); foot = zone; }
            return new LadderStep { Foot = foot, Dir = dir, Bottom = b.min.y, Top = b.max.y, Run = Mathf.Max(run, 0.5f), Id = id };
        }

        // the next ladder up from a ladder's top: its foot on the landing (within 1 m of the top's height, 3.5 m of it) and
        // it goes higher; nearest foot wins
        static List<LadderStep> PlanChain(GameObject first, Bounds firstB, int firstId, Vector3 firstFoot, Vector3 firstDir, float needY)
        {
            var chain = new List<LadderStep> { MakeStep(firstB, firstFoot, firstDir, firstId, first) };
            var used = new HashSet<int> { firstId };
            Bounds cur = firstB; Vector3 curDir = firstDir;
            for (int n = 0; n < 12 && cur.max.y < needY - 0.8f; n++)
            {
                float depth = Mathf.Abs(curDir.x) * cur.extents.x + Mathf.Abs(curDir.z) * cur.extents.z;
                Vector3 top = new Vector3(cur.center.x, cur.max.y, cur.center.z) + curDir * depth;
                LadderStep next = null; Bounds nextB = default(Bounds); float bd = float.MaxValue;
                var seen = new HashSet<int>();
                foreach (Collider col in Physics.OverlapSphere(top, 5f, Mask()))
                {
                    if (col == null || col.isTrigger) continue;
                    Piece piece;
                    GameObject root = LadderRoot(col, out piece);
                    if (root == null || !seen.Add(root.GetInstanceID()) || !IsLadder(root, piece, col)) continue;
                    AutoJumpLedge lg = root.GetComponentInChildren<AutoJumpLedge>(true);
                    if (lg == null) continue;
                    int id;
                    Bounds b = LadderBounds(root, out id);
                    if (used.Contains(id) || b.max.y < cur.max.y + 1.0f) continue;
                    Vector3 f = lg.transform.forward; f.y = 0f;
                    if (f.sqrMagnitude < 0.01f) continue;
                    f.Normalize();
                    Vector3 spot; bool room;
                    // 1.5.3: 4.5 m (was 3.5 - the ruin's third ladder fell just outside and split the chain in two)
                    if (!LadderFoot(b, f, out spot, out room) || Mathf.Abs(spot.y - cur.max.y) > 1.0f || Flat(spot, top) > 4.5f) continue;
                    float d = Flat(spot, top);
                    if (d < bd) { bd = d; next = MakeStep(b, spot, f, id, root); nextB = b; }
                }
                if (next == null) break;
                chain.Add(next); used.Add(next.Id); cur = nextB; curDir = next.Dir;
            }
            return chain;
        }

        // 1.5.3: the ladder's own trigger (AutoJumpLedge) bumps a companion up it, as for a player - the mover doesn't bump.
        // CompanionLadderBumpPatch makes his walking input read right when he walks the ladder's way. (An own bump from the AI
        // was tried and dropped: the game's walking code capped it at 3 m/s while he was still on the floor, and it took the
        // 0.5 s slot so the ladder's real bump was refused.)
        // nothing but ladder within `height` above a spot
        static bool HeadRoom(Vector3 spot, float height)
        {
            foreach (RaycastHit h in Physics.RaycastAll(spot + Vector3.up * 0.3f, Vector3.up, height - 0.3f, Mask(), QueryTriggerInteraction.Ignore))
            {
                Piece hp; GameObject hr = LadderRoot(h.collider, out hp);
                if (hr != null && IsLadder(hr, hp, h.collider)) continue;
                return false;
            }
            return true;
        }

        static bool LiftZoneEdge(AutoJumpLedge[] ledges, Vector3 foot, Vector3 dir, out Vector3 zone)
        {
            zone = foot;
            if (ledges == null) return false;
            Vector3 p = foot + Vector3.up * 0.5f; float best = float.MaxValue; bool found = false;
            foreach (AutoJumpLedge lg in ledges)
            {
                if (lg == null) continue;
                foreach (Collider tc in lg.GetComponentsInChildren<Collider>(true))
                {
                    if (tc == null || !tc.isTrigger) continue;
                    Vector3 cp = tc.ClosestPoint(p);
                    float d = Vector3.Distance(cp, p);
                    if (d >= best || d > 3f) continue;
                    // 1.5.3: from just short of the lift zone, forward along the ladder to the first spot with floor (or a rung)
                    // under it and head room over it (user trace: on every landing ladder the bump shot him up at 6 m/s and he was
                    // back on the same spot 0.3 s later - his head hit the floor above, which overhangs the ladder's foot)
                    for (float step = -0.2f; step <= 1.21f; step += 0.2f)
                    {
                        Vector3 spot = new Vector3(cp.x, foot.y, cp.z) + dir * step;
                        RaycastHit g;
                        if (!Physics.SphereCast(spot + Vector3.up * 1.0f, 0.25f, Vector3.down, out g, 2.0f, Mask()) || Mathf.Abs(g.point.y - foot.y) > 0.6f) continue;
                        spot.y = g.point.y;
                        if (!HeadRoom(spot, 2.2f)) continue;
                        best = d; zone = spot; found = true;
                        break;
                    }
                }
            }
            return found;
        }

        static void StartChain(S s, List<LadderStep> chain, int idx, Vector3 pos, float now, string tag, bool down = false)
        {
            s.Chain = chain; s.ChainIdx = idx; s.OnChain = true; s.ChainDown = down;
            foreach (LadderStep cs in chain) cs.LinedUp = false;
            s.DescendLadder = false; s.DescendUntil = 0f;
            s.ClimbLadder = false; s.ClimbUntil = 0f; s.LadderPending = false; s.HasDetour = false; s.StepUntil = 0f;
            ChainStepReset(s, pos, now);
            if (down)
            {
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] going down a chain of " + (chain.Count - idx) + " ladders to " + chain[chain.Count - 1].Bottom.ToString("F1") + " (first top " + Fmt(chain[idx].TopSpot) + ")");
                return;
            }
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] climbing a chain of " + (chain.Count - idx) + " ladders up to " + chain[chain.Count - 1].Top.ToString("F1") + " (first foot " + Fmt(chain[idx].Foot) + ")");
        }

        static void ChainStepReset(S s, Vector3 pos, float now)
        {
            s.ChainProgressAt = now; s.ChainBestY = pos.y; s.ChainJumps = 0; s.ChainJumpAt = 0f; s.ChainPushAt = 0f;
            s.ChainFootBest = float.MaxValue; s.ChainFootAt = now; s.ChainViaUntil = 0f; s.ChainRestUntil = 0f;
        }

        static void EndChain(S s, float now)
        {
            s.LadderClimbsTried.Clear();   // 1.5.3: after a fall the same ladders are the way back up ("no ladder found" beside them)
            s.OnChain = false; s.NextPlan = 0f; s.Ladder = 0; s.Passes = 0; s.MainBest = float.MaxValue;
            s.ProgressTime = now; s.AnchorTime = now; s.NextLadderClimbSearch = 0f; s.NextLadderSearch = 0f;
        }

        static bool UseKnownChain(S s, Vector3 pos, float needY, float now, string tag)
        {
            s_knownChains.RemoveAll(k => now > k.Until);
            foreach (KnownChain k in s_knownChains)
                for (int i = 0; i < k.Steps.Count; i++)
                {
                    LadderStep st = k.Steps[i];
                    if (BadLadder(st.Id, now) || Mathf.Abs(st.Foot.y - pos.y) > 1.0f || Flat(pos, st.Foot) > 3f) continue;
                    if (k.Steps[k.Steps.Count - 1].Top < pos.y + 1.5f) continue;
                    StartChain(s, k.Steps, i, pos, now, tag);
                    return true;
                }
            return false;
        }

        static void RememberChain(List<LadderStep> chain, float now)
        {
            foreach (KnownChain k in s_knownChains) if (k.Steps == chain) { k.Until = now + 900f; return; }
            s_knownChains.Add(new KnownChain { Steps = chain, Until = now + 900f });
            if (s_knownChains.Count > 20) s_knownChains.RemoveAt(0);
        }

        static void RunChain(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            if (s.Chain == null || s.ChainIdx >= s.Chain.Count) { EndChain(s, now); return; }
            if (s.ChainDown) { RunChainDown(ai, c, s, pos, now, tag); return; }
            LadderStep st = s.Chain[s.ChainIdx];
            // up this one: straight on to the next foot - no stopping on the landing. Only once standing at its top, though:
            // turning for the next foot 0.6 m short, still on the slope, walked him off its side ("fell off the ladders -
            // back on at ladder 1/5" again and again); against a wall at the top, also when it stops lifting for 0.5 s
            // 1.5.3: and never walk on past its top end, or off the edge of the landing there (user log: twice he carried on
            // in the ladder's direction, out through an opening in the tower wall - "fell 9.7 m and took 36 fall damage")
            Vector3 fromFoot = pos - st.Foot; fromFoot.y = 0f;
            float edgeDrop;
            bool pastTop = s.ChainFootBest <= 0.6f && pos.y > st.Top - 1.2f
                && (Vector3.Dot(fromFoot, st.Dir) > st.Run + 0.3f || LedgeAhead(pos, st.Dir, 0.6f, out edgeDrop));
            if (pastTop && !c.IsOnGround()) { Halt(ai, c, s); return; }
            // (a pause in rising counts only past 1.2 s - the ladder's bumps come every 0.5 s; user log: switched at 36.7, short
            // of the 37.4 landing, and stood there)
            if (c.IsOnGround() && (pos.y >= st.Top - 0.3f || pastTop || (pos.y >= st.Top - 0.8f && now - s.ChainProgressAt > 1.2f)))
            {
                s.ChainIdx++;
                ChainStepReset(s, pos, now);
                if (s.ChainIdx >= s.Chain.Count)
                {
                    RememberChain(s.Chain, now);
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] up the ladders at " + Fmt(pos));
                    EndChain(s, now);
                    // walk on off the last one the way it faces - not over a harmful drop
                    float edge;
                    if (!LedgeAhead(pos, st.Dir, 0.7f, out edge)) { s.StepDir = st.Dir; s.StepRun = false; s.StepUntil = now + 0.5f; }
                    return;
                }
                st = s.Chain[s.ChainIdx];
                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] next ladder (" + (s.ChainIdx + 1) + "/" + s.Chain.Count + "), foot at " + Fmt(st.Foot));
            }
            // fell off: back onto the chain at the ladder whose foot is on this level, else give it up
            if (c.IsOnGround() && pos.y < st.Foot.y - 1.0f)
            {
                for (int i = 0; i < s.Chain.Count; i++)
                    if (Mathf.Abs(s.Chain[i].Foot.y - pos.y) < 1.0f && Flat(pos, s.Chain[i].Foot) < 6f)
                    {
                        s.ChainIdx = i; ChainStepReset(s, pos, now);
                        if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] fell off the ladders - back on at ladder " + (i + 1) + "/" + s.Chain.Count);
                        return;
                    }
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] fell off the ladders (now at " + Fmt(pos) + ") - looking again");
                EndChain(s, now);
                return;
            }
            // 1.5.3: a bump moves him up a rung or so (~0.25 m) - that is progress (0.3 m counted only hops, so every ladder
            // after the first looked "stuck" and got a hop)
            if (pos.y > s.ChainBestY + 0.1f) { s.ChainBestY = pos.y; s.ChainProgressAt = now; s.ChainJumps = 0; }
            float off = Flat(pos, st.Foot);
            if (pos.y < st.Foot.y + 1.0f && off < s.ChainFootBest - 0.15f) { s.ChainFootBest = off; s.ChainFootAt = now; s.ChainProgressAt = Mathf.Max(s.ChainProgressAt, now - 2f); }
            // on this ladder only once its foot has been reached (or got as close as it gets) - still bouncing at the top of
            // the last one is not "on" this one
            bool reached = s.ChainFootBest <= 0.6f || (now - s.ChainFootAt > 1.0f && off < 1.2f);
            bool lifted = reached && pos.y > st.Foot.y + 0.25f;
            Vector3 dir = st.Dir;
            // 1.5.3 (user: "The NPC should be keeping track of [stamina] ... and try to not run out"): low on a landing -
            // stand and get some back before the next ladder, like a player; never part way up one
            float maxSt = MaxStamina(ai);
            if (!lifted && maxSt > 0f && c.IsOnGround())
            {
                float st0 = Stamina(ai);
                if (now >= s.ChainRestUntil && st0 < maxSt * 0.15f) { s.ChainRestUntil = now + 8f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] getting my breath back on the landing (" + (st0 / maxSt * 100f).ToString("F0") + "% stamina)"); }
                if (now < s.ChainRestUntil)
                {
                    if (st0 >= maxSt * 0.4f) s.ChainRestUntil = 0f;
                    else { Halt(ai, c, s); s.ChainProgressAt = now; s.ChainFootAt = now; return; }
                }
            }
            if (!lifted)
            {
                bool blocked = now - s.ChainFootAt > 1.0f && off < 1.2f;
                if (off > 0.45f && !blocked)
                {
                    // 1.5.3 (like doorways - user: "Did the door fix not get implemented in this new planning?"): line up square
                    // in front of the ladder first, then walk to its foot, so he goes up it straight instead of arriving at an
                    // angle and drifting off its side. The line-up spot needs floor; without one, straight to the foot.
                    Vector3 target = st.Foot;
                    Vector3 lineUp = st.Foot - st.Dir * 0.8f;
                    Vector3 toFoot = st.Foot - pos; toFoot.y = 0f;
                    if (!st.LinedUp)
                    {
                        RaycastHit lg;
                        bool square = toFoot.sqrMagnitude < 0.01f || Vector3.Dot(toFoot.normalized, st.Dir) > 0.8f;
                        bool lineFloor = Physics.SphereCast(lineUp + Vector3.up * 1.0f, 0.25f, Vector3.down, out lg, 2.0f, Mask()) && Mathf.Abs(lg.point.y - st.Foot.y) < 0.6f;
                        if (lineFloor)
                        {
                            Piece lp; GameObject lr = LadderRoot(lg.collider, out lp);
                            if (lr != null && IsLadder(lr, lp, lg.collider)) lineFloor = false;   // that's the ladder below, not floor
                        }
                        if (square || !lineFloor || Flat(pos, lineUp) < 0.4f) st.LinedUp = true;
                        else target = lineUp;
                    }
                    if (now < s.ChainViaUntil && Flat(pos, s.ChainVia) > 0.35f) target = s.ChainVia;
                    else if (!FloorAlong(pos, target))
                    {
                        Vector3 via;
                        if (FindFloorVia(pos, target, out via)) { s.ChainVia = via; s.ChainViaUntil = now + 3f; target = via; }
                    }
                    dir = target - pos; dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) dir = st.Dir;
                }
            }
            dir.Normalize();
            if (now - s.ChainProgressAt > 6f)
            {
                MarkBadLadder(st.Id, now, 120f);
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stuck on ladder " + (s.ChainIdx + 1) + "/" + s.Chain.Count + " at " + Fmt(pos) + " - looking again");
                EndChain(s, now);
                return;
            }
            // 1.5.3: the same edge check as normal walking (user: "I thought there was an edge check?" - this runner bypassed
            // it, and on the open stair he walked off the side: "fell 9.3 m"). Floor ending ahead on a landing: don't step.
            float drop;
            if (!lifted && c.IsOnGround() && LedgeAhead(pos, dir, 0.5f, out drop))
            {
                Halt(ai, c, s);
                if (now >= s.NextEdgeLog) { s.NextEdgeLog = now + 5f; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] edge ahead on the ladders at " + Fmt(pos + dir * 0.5f) + " (" + drop.ToString("F1") + "m down) - not stepping off"); }
                return;
            }
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, Quaternion.LookRotation(dir), 540f * Time.deltaTime);
            // walking pace on ladders (user: "There is no reason to have the NPC sprint up the ladder. Save the stamina.")
            Steer(ai, c, s, dir, false, false);
            // at the foot or on it: walk into it and let the ladder bump us up - no jumping
            bool atFoot = !lifted && (off <= 0.45f || (now - s.ChainFootAt > 1.0f && off < 1.2f));
            if (atFoot || lifted)
            {
                if (s.ChainPushAt <= 0f) s.ChainPushAt = now;
                // stuck: a hop, like a player would (user: "Its ok to have the NPC jump if they are stuck") - after 2.5 s
                // without rising, not when short of stamina
                float since = now - Mathf.Max(s.ChainPushAt, s.ChainProgressAt);
                bool lowStamina = maxSt > 0f && Stamina(ai) < maxSt * 0.2f;
                float hopDrop;
                bool edgeNear = LedgeAhead(pos, dir, 0.8f, out hopDrop) || LedgeAhead(pos, dir, 1.6f, out hopDrop);
                // 1.5.3: at the foot, one or two bumps that don't get him onto it (pressed against the ladder's bottom, the bump's
                // ~0.9 m lift doesn't clear it - user: "bashing into the ladder then bouncing a bit then going up the ladder. Like
                // the logic took a few seconds") -> hop after 0.8 s; part way up, give the bumps 2.5 s
                float hopAfter = lifted ? 2.5f : 0.8f;
                if (since > hopAfter && !lowStamina && !edgeNear && c.IsOnGround() && now >= s.ChainJumpAt)
                {
                    if (++s.ChainJumps > 6)
                    {
                        MarkBadLadder(st.Id, now, 120f);
                        if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get up ladder " + (s.ChainIdx + 1) + "/" + s.Chain.Count + " - looking again");
                        EndChain(s, now);
                        return;
                    }
                    c.Jump(false);
                    Rigidbody rb = ai.GetComponent<Rigidbody>();
                    if (rb != null) { Vector3 bv = rb.linearVelocity; bv.x = dir.x * 2.5f; bv.z = dir.z * 2.5f; rb.linearVelocity = bv; }
                    s.ChainJumpAt = now + 1.0f;
                    if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] stuck at ladder " + (s.ChainIdx + 1) + "/" + s.Chain.Count + " - hopping (" + s.ChainJumps + ")");
                }
            }
        }

        // 1.5.3: going down the same way (user: "had issues getting down the ladder" - one ladder at a time, each descent
        // stopping a metre short of the bottom on these sloping ladders, then edges and a 4 m drop). A step down: the spot at
        // the top where the ladder is stepped onto (on its upper side), and its foot.
        static LadderStep DownStep(Bounds b, Vector3 f, int id)
        {
            float depth = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.z) * b.extents.z;
            Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z) + f * (depth + 0.5f);
            RaycastHit g;
            if (Physics.SphereCast(top + Vector3.up * 1.0f, 0.25f, Vector3.down, out g, 2.2f, Mask()) && Mathf.Abs(g.point.y - b.max.y) < 1.0f) top.y = g.point.y;
            Vector3 foot; bool room;
            if (!LadderFoot(b, f, out foot, out room)) foot = new Vector3(b.center.x, b.min.y, b.center.z) - f * (depth + 0.7f);
            return new LadderStep { Foot = foot, TopSpot = top, Dir = f, Bottom = b.min.y, Top = b.max.y, Id = id };
        }

        static List<LadderStep> PlanChainDown(Bounds firstB, int firstId, Vector3 firstF, float needY)
        {
            var chain = new List<LadderStep> { DownStep(firstB, firstF, firstId) };
            var used = new HashSet<int> { firstId };
            Bounds cur = firstB;
            for (int n = 0; n < 12 && cur.min.y > needY + 0.8f; n++)
            {
                Vector3 foot = chain[chain.Count - 1].Foot;
                LadderStep next = null; Bounds nextB = default(Bounds); float bd = float.MaxValue;
                var seen = new HashSet<int>();
                foreach (Collider col in Physics.OverlapSphere(foot, 5f, Mask()))
                {
                    if (col == null || col.isTrigger) continue;
                    Piece piece;
                    GameObject root = LadderRoot(col, out piece);
                    if (root == null || !seen.Add(root.GetInstanceID()) || !IsLadder(root, piece, col)) continue;
                    AutoJumpLedge lg = root.GetComponentInChildren<AutoJumpLedge>(true);
                    if (lg == null) continue;
                    int id;
                    Bounds b = LadderBounds(root, out id);
                    if (used.Contains(id) || b.min.y > cur.min.y - 1.0f || Mathf.Abs(b.max.y - cur.min.y) > 1.2f) continue;
                    Vector3 f = lg.transform.forward; f.y = 0f;
                    if (f.sqrMagnitude < 0.01f) continue;
                    LadderStep cand = DownStep(b, f.normalized, id);
                    float d = Flat(cand.TopSpot, foot);
                    if (d > 4.5f || d >= bd) continue;
                    bd = d; next = cand; nextB = b;
                }
                if (next == null) break;
                chain.Add(next); used.Add(next.Id); cur = nextB;
            }
            return chain;
        }

        static void RunChainDown(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            LadderStep st = s.Chain[s.ChainIdx];
            // down this one: straight on to the next one's top
            if (pos.y <= st.Bottom + 0.6f && c.IsOnGround())
            {
                s.ChainIdx++;
                ChainStepReset(s, pos, now);
                if (s.ChainIdx >= s.Chain.Count)
                {
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] down the ladders at " + Fmt(pos));
                    EndChain(s, now);
                    return;
                }
                st = s.Chain[s.ChainIdx];
                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] next ladder down (" + (s.ChainIdx + 1) + "/" + s.Chain.Count + "), top at " + Fmt(st.TopSpot));
            }
            // fell past it: carry on from the ladder whose top is on this level, else look again
            if (c.IsOnGround() && pos.y < st.Bottom - 1.0f)
            {
                for (int i = s.ChainIdx + 1; i < s.Chain.Count; i++)
                    if (Mathf.Abs(s.Chain[i].Top - pos.y) < 1.2f)
                    {
                        s.ChainIdx = i; ChainStepReset(s, pos, now);
                        if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] slipped down the ladders - carrying on from ladder " + (i + 1) + "/" + s.Chain.Count);
                        return;
                    }
                EndChain(s, now);
                return;
            }
            if (pos.y < s.ChainBestY - 0.3f) { s.ChainBestY = pos.y; s.ChainProgressAt = now; }
            Vector3 target = st.Foot;
            if (pos.y > st.Top - 0.5f)
            {
                // still on the landing above it: to the spot where it's stepped onto, by the floor, then down it
                float off = Flat(pos, st.TopSpot);
                if (off < s.ChainFootBest - 0.15f) { s.ChainFootBest = off; s.ChainFootAt = now; s.ChainProgressAt = Mathf.Max(s.ChainProgressAt, now - 2f); }
                bool there = s.ChainFootBest <= 0.45f || now - s.ChainFootAt > 1.0f;
                if (!there)
                {
                    target = st.TopSpot;
                    if (now < s.ChainViaUntil && Flat(pos, s.ChainVia) > 0.35f) target = s.ChainVia;
                    else if (!FloorAlong(pos, st.TopSpot))
                    {
                        Vector3 via;
                        if (FindFloorVia(pos, st.TopSpot, out via)) { s.ChainVia = via; s.ChainViaUntil = now + 3f; target = via; }
                    }
                }
            }
            Vector3 dir = target - pos; dir.y = 0f;
            if (dir.sqrMagnitude < 0.04f) dir = -st.Dir;
            dir.Normalize();
            if (now - s.ChainProgressAt > 6f)
            {
                MarkBadLadder(st.Id, now, 120f);
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stuck going down ladder " + (s.ChainIdx + 1) + "/" + s.Chain.Count + " at " + Fmt(pos) + " - looking again");
                EndChain(s, now);
                return;
            }
            ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, Quaternion.LookRotation(dir), 540f * Time.deltaTime);
            Steer(ai, c, s, dir, false, false);
        }

        // where to face at the top of a ladder: the foot of the nearest other ladder that starts on this level, else the goal
        static Vector3 NextFootDir(S s, Vector3 pos)
        {
            Vector3 best = s.Goal - pos; best.y = 0f; float bd = float.MaxValue;
            var seen = new HashSet<int>();
            foreach (Collider col in Physics.OverlapSphere(pos, 3.5f, Mask()))
            {
                if (col == null || col.isTrigger) continue;
                Piece piece;
                GameObject root = LadderRoot(col, out piece);
                if (root == null || !seen.Add(root.GetInstanceID()) || !IsLadder(root, piece, col)) continue;
                AutoJumpLedge lg = root.GetComponentInChildren<AutoJumpLedge>(true);
                if (lg == null) continue;
                int id;
                Bounds b = LadderBounds(root, out id);
                if (id == s.ClimbLadderId || b.max.y < pos.y + 1.0f) continue;
                Vector3 f = lg.transform.forward; f.y = 0f;
                Vector3 spot; bool room;
                if (f.sqrMagnitude < 0.01f || !LadderFoot(b, f.normalized, out spot, out room) || Mathf.Abs(spot.y - pos.y) > 1.0f) continue;
                float d = Flat(pos, spot);
                if (d < bd) { bd = d; best = spot - pos; best.y = 0f; if (best.sqrMagnitude < 0.04f) best = f; }
            }
            return best.sqrMagnitude > 0.01f ? best.normalized : Vector3.zero;
        }

        static bool TryStartLadderClimb(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            s.NextLadderClimbSearch = now + 2f;
            if (c.IsSwimming() || !c.IsOnGround() || now < s.ClimbUntil) return false;
            float needY = s.HasGood && s.GoodPos.y > pos.y + 1.5f && Vector3.Distance(pos, s.GoodPos) < 40f ? s.GoodPos.y : s.Goal.y;
            // 1.5.3: 1.5 m, not 2 - a skeleton exactly 2.0 m up on the next floor got no ladder search (user log: stepped off
            // the ladder's top instead and fell)
            if (needY < pos.y + 1.5f) return false;
            // 1.5.3: a chain of ladders that has worked before, starting by our feet: straight onto it
            if (UseKnownChain(s, pos, needY, now, tag)) return true;
            Collider[] cols = Physics.OverlapSphere(pos + Vector3.up * 2f, 14f, Mask());
            GameObject best = null; Bounds bestB = default(Bounds); float bestD = float.MaxValue; int bestId = 0;
            var seen = new HashSet<int>();
            foreach (Collider col in cols)
            {
                if (col == null || col.isTrigger) continue;
                Piece piece;
                GameObject root = LadderRoot(col, out piece);
                if (root == null || !seen.Add(root.GetInstanceID())) continue;
                // the game's ladders carry an AutoJumpLedge trigger that bounces anyone moving its way up the rungs
                if (!IsLadder(root, piece, col)) continue;
                int id;
                Bounds b = LadderBounds(root, out id);
                if (!seen.Add(id) && id != root.GetInstanceID()) continue;              // a piece of a stack already looked at
                if (s.LadderClimbsTried.Contains(id) || BadLadder(id, now)) continue;
                // 1.5.2: it goes up from here. Ladder pieces are ~2.2 m, so one starting at our feet only rises about that much
                // (user log: on the first floor the next ladder up was skipped as "not going up 2 m" - "no ladder found").
                if (b.max.y < pos.y + 1.0f) continue;
                // 1.5.2 (user: "look for a path to where it needs to go, or at least close. Picking a ladder near its feet that
                // it can either walk up or jump onto"): its foot is on our level and we can get there; best = short walk to it
                // and its top as near the goal as possible (height counts double)
                Vector3 topAt = new Vector3(b.center.x, b.max.y, b.center.z);
                float toGoal = Flat(topAt, s.Goal) + 2f * Mathf.Abs(needY - b.max.y);
                float d;
                AutoJumpLedge lg = root.GetComponentInChildren<AutoJumpLedge>(true);
                Vector3 lf = lg != null ? lg.transform.forward : Vector3.zero; lf.y = 0f;
                Vector3 spot; bool room;
                if (lf.sqrMagnitude > 0.01f && LadderFoot(b, lf.normalized, out spot, out room))
                {
                    if (Mathf.Abs(spot.y - pos.y) > 1.0f) continue;                        // its foot is on a floor above or below us
                    float up = b.min.y - spot.y;
                    if (up > 0.4f && (!room || up > 2.0f)) continue;                     // a jump on: within reach, room overhead
                    float walk = Flat(pos, spot);
                    if (walk > 3f)
                    {
                        if (!GetPath(pos, spot, s_ladderPath, MHR_CompanionNavMesh.AgentOrHumanoid, false)) continue;
                        Vector3 end = s_ladderPath[s_ladderPath.Count - 1];
                        if (Flat(end, spot) > 1.0f || Mathf.Abs(end.y - spot.y) > 1.0f) continue;   // no way to its foot
                        walk = PathLength(pos, s_ladderPath);
                    }
                    d = walk + toGoal + 2f * Mathf.Max(0f, up - 0.4f);
                }
                else
                {
                    if (b.min.y > pos.y + 2.2f) continue;
                    d = Flat(pos, b.center) + toGoal + 5f * Mathf.Max(0f, b.min.y - pos.y - 0.5f);
                }
                if (d < bestD) { bestD = d; best = root; bestB = b; bestId = id; }
            }
            if (best == null)
            {
                if (now >= s.NextLadderLog)
                {
                    s.NextLadderLog = now + 30f;
                    // diagnostic: what built things are around, so a ladder with another name can be recognised later
                    var names = new HashSet<string>();
                    foreach (Collider col in cols)
                    {
                        if (col == null) continue;
                        if (col.bounds.max.y <= pos.y + 2f || Flat(pos, col.bounds.center) >= 6f) continue;
                        Piece piece = col.GetComponentInParent<Piece>();
                        // world objects too (a ruin's ladders aren't pieces): the collider's object and the one above it
                        string nm = piece != null ? piece.gameObject.name : (col.transform.parent != null ? col.transform.parent.name + "/" : "") + col.gameObject.name;
                        names.Add(nm.Replace("(Clone)", ""));
                        if (names.Count >= 8) break;
                    }
                    if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] " + (needY - pos.y).ToString("F1") + "m below where it needs to be and no ladder found (tall things close by: " + string.Join(", ", names) + ")");
                }
                return false;
            }
            s.LadderClimbsTried.Add(bestId);
            Vector3 into = bestB.center - pos; into.y = 0f;
            if (into.sqrMagnitude < 0.04f) { into = s.Goal - pos; into.y = 0f; }
            if (into.sqrMagnitude < 0.04f) into = ai.transform.forward;
            // 1.5.2: the ladder's own auto-jump only lifts someone moving its way (within 60 degrees of its forward) -
            // the same as a player just walking into it. Use its direction and let it do the jumping.
            AutoJumpLedge ledge = best.GetComponentInChildren<AutoJumpLedge>(true);
            s.ClimbAuto = false;
            if (ledge != null)
            {
                Vector3 f = ledge.transform.forward; f.y = 0f;
                if (f.sqrMagnitude > 0.01f)
                {
                    f.Normalize();
                    into = f; s.ClimbAuto = true;
                    // the foot of the ladder, on the side it's climbed from: get there first, then walk into it
                    // stand on what's there: the ladder's bottom can sit above the ground (user log: foot 2 m up, never "at the foot")
                    Vector3 spot; bool room;
                    if (LadderFoot(bestB, f, out spot, out room)) s.ClimbBase = spot;
                    else { float depth = Mathf.Abs(f.x) * bestB.extents.x + Mathf.Abs(f.z) * bestB.extents.z; s.ClimbBase = new Vector3(bestB.center.x, bestB.min.y, bestB.center.z) - f * (depth + 0.7f); }
                }
            }
            s.ClimbDir = into.normalized;
            s.ClimbLedges = best.GetComponentsInChildren<AutoJumpLedge>(true);
            Vector3 zoneFoot;
            if (s.ClimbAuto && LiftZoneEdge(s.ClimbLedges, s.ClimbBase, s.ClimbDir, out zoneFoot)) s.ClimbBase = zoneFoot;   // 1.5.3: where the lift starts
            s.ClimbLadderId = bestId; s.ClimbBottomY = bestB.min.y;
            s.ClimbTop = new Vector3(bestB.center.x, Mathf.Min(bestB.max.y, needY + 0.5f), bestB.center.z);
            // 1.5.2: get to the foot of it by the normal route first (user: "running into a wall" walking straight at it)
            // 1.5.2: a foot close by on our level is walked to straight (with a look for edges); a route there across the
            // small floors between ladders took him off the edge (user log: "fell 4.1 m" on the way to the next ladder)
            // 1.5.3 (user: "Is there a better way ... less awkward for the NPC flow?"): plan the chain of ladders above this one
            // now - this one's top by the next one's foot, and so on - and climb it in one go
            List<LadderStep> chain = s.ClimbAuto ? PlanChain(best, bestB, bestId, s.ClimbBase, s.ClimbDir, needY) : null;
            if (chain != null && chain.Count >= 2 && !(Flat(pos, s.ClimbBase) > 3.0f || Mathf.Abs(pos.y - s.ClimbBase.y) > 1.0f))
            {
                StartChain(s, chain, 0, pos, now, tag);
                return true;
            }
            s.PendingChain = chain != null && chain.Count >= 2 ? chain : null; s.PendingDown = false;
            if (s.ClimbAuto && (Flat(pos, s.ClimbBase) > 3.0f || Mathf.Abs(pos.y - s.ClimbBase.y) > 1.0f))
            {
                s.LadderPending = true; s.HasDetour = true; s.Detour = s.ClimbBase; s.DetourUntil = now + 15f; s.NextPlan = 0f;
                s.StepUntil = 0f;
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + (needY - pos.y).ToString("F1") + "m below where it needs to be - heading for the ladder " + best.name.Replace("(Clone)", "") + ", foot at " + Fmt(s.ClimbBase) + ", top at " + bestB.max.y.ToString("F1"));
                return true;
            }
            s.ClimbLadder = true; s.ClimbStartY = pos.y; s.ClimbStartAt = now; s.ClimbJumps = 0; s.ClimbViaUntil = 0f; s.ClimbFootBest = float.MaxValue; s.ClimbFootAt = now;
            NoteLadderClimb(s, now, tag);
            s.ClimbTop = new Vector3(bestB.center.x, Mathf.Min(bestB.max.y, needY + 0.5f), bestB.center.z);
            s.ClimbUntil = now + 20f; s.ClimbBestY = pos.y; s.ClimbProgressAt = now; s.NextClimbJump = 0f;
            s.StepUntil = 0f; s.HasDetour = false;
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + (needY - pos.y).ToString("F1") + "m below where it needs to be - climbing the ladder " + best.name.Replace("(Clone)", "") + " at " + Fmt(bestB.center) + " (" + Flat(pos, bestB.center).ToString("F1") + "m away)");
            return true;
        }

        // 1.5.2: off the top of a ladder (user: "If a ladder does not fully connect can the NPC jump up onto something nearby
        // to finish getting to the destination?"). Look for footing towards where it's going - up to a jump's height above
        // and within a jump's reach - and leap onto it like a player would. Nothing there: step off the ladder's way.
        static void LadderTopHop(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            Vector3 goal = s.HasGood && s.GoodPos.y > pos.y - 1f && Vector3.Distance(pos, s.GoodPos) < 40f ? s.GoodPos : s.Goal;
            Vector3 toGoal = goal - pos; toGoal.y = 0f;
            // 1.5.3: the way the ladder faces first - that's where its top floor is; towards the goal only after
            // (user: "went up one flight then jumped off" - the goal's way led across a small floor and off it)
            Vector3[] dirs = toGoal.sqrMagnitude > 0.04f ? new[] { s.ClimbDir, toGoal.normalized } : new[] { s.ClimbDir };
            float up = c.m_jumpForce * c.m_jumpForce / (2f * Mathf.Max(1f, -Physics.gravity.y)) * 0.85f;   // how high a jump gets it, with a margin
            foreach (Vector3 dir in dirs)
            {
                for (float d = 0.8f; d <= 3.01f; d += 0.4f)
                {
                    Vector3 p = pos + dir * d;
                    RaycastHit hit;
                    if (!Physics.Raycast(new Vector3(p.x, pos.y + up + 1.5f, p.z), Vector3.down, out hit, up + 3f, Mask())) continue;
                    float dy = hit.point.y - pos.y;
                    if (dy < -0.5f || dy > up) continue;                                   // a floor at about our height or a jump up
                    // 1.5.3: up onto something only when the goal is up there too (user log: already at the player's height, it
                    // jumped 0.9 m up onto the tower's wall top and over the side - "fell 11.3 m and took 45 fall damage")
                    if (dy > 0.4f && goal.y < pos.y + 1.0f) continue;
                    if (Physics.Raycast(new Vector3(p.x, hit.point.y + 0.1f, p.z), Vector3.up, 1.6f, Mask())) continue;   // room to stand
                    // 1.5.3: a floor, not just an edge or a beam - there's footing a bit further on too
                    RaycastHit more;
                    Vector3 q = p + dir * 0.5f;
                    if (!Physics.Raycast(new Vector3(q.x, hit.point.y + 1.0f, q.z), Vector3.down, out more, 1.5f, Mask()) || Mathf.Abs(more.point.y - hit.point.y) > 0.4f) continue;
                    if (MHR_Jump.Reach(c, dir, dy) < d) continue;
                    if (dy < 0.4f && d <= 1.3f)
                    {
                        // level and close: just step across, like a player walking off the top of a ladder - a full leap
                        // carried it over a small floor and off the far side
                        s.StepDir = dir; s.StepRun = false; s.StepUntil = now + 0.5f;
                        if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stepping off the top of the ladder onto " + Fmt(hit.point) + " (" + d.ToString("F1") + "m on)");
                        return;
                    }
                    // a jump carries on past where it lands: not onto something with a harmful drop just beyond (a wall top)
                    float beyond;
                    if (LedgeAhead(hit.point, dir, 1.0f, out beyond)) continue;
                    MHR_Jump.Leap(ai, c, dir);
                    s.StepDir = dir; s.StepRun = true; s.StepUntil = now + 0.6f;
                    if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] jumping from the top of the ladder onto " + Fmt(hit.point) + " (" + dy.ToString("F1") + "m up, " + d.ToString("F1") + "m on)");
                    return;
                }
            }
            if (TryHopUp(ai, c, s, pos, now, tag)) return;   // something to jump up onto beside the ladder (a wall top)
            // step off the top - not over an edge that hurts (the stuck steps take it from here instead)
            float edge;
            if (LedgeAhead(pos, s.ClimbDir, 0.7f, out edge)) { Halt(ai, c, s); return; }
            s.StepDir = s.ClimbDir; s.StepRun = false; s.StepUntil = now + 0.5f;   // walk off the top, no leap
        }

        // 1.5.2: the way down is the ladder it came up (user: "The NPC should prefer to go back down the ladder"). A ladder
        // whose top is on our level and whose foot is nearer the goal's level: walk round to its top, on the side it's
        // climbed off, and step back down it like a player. Dropping off an edge only comes after this.
        static bool TryLadderDown(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            s.NextLadderClimbSearch = now + 2f;
            if (c.IsSwimming() || !c.IsOnGround() || s.DescendLadder || s.LadderPending) return false;
            float below = pos.y - s.Goal.y;
            if (below < 1.5f) return false;
            Collider[] cols = Physics.OverlapSphere(pos, 14f, Mask());
            var seen = new HashSet<int>();
            float bestScore = float.MaxValue; GameObject best = null; int bestId = 0; Vector3 bestTop = Vector3.zero, bestBase = Vector3.zero, bestF = Vector3.zero;
            Bounds bestB = default(Bounds); bool bestLedge = false;
            foreach (Collider col in cols)
            {
                if (col == null || col.isTrigger) continue;
                Piece piece;
                GameObject root = LadderRoot(col, out piece);
                if (root == null || !seen.Add(root.GetInstanceID()) || !IsLadder(root, piece, col)) continue;
                AutoJumpLedge ledge = root.GetComponentInChildren<AutoJumpLedge>(true);
                int id;
                Bounds b = LadderBounds(root, out id);
                if (!seen.Add(id) && id != root.GetInstanceID()) continue;
                if (BadLadder(id, now)) continue;
                if (b.max.y < pos.y - 1.5f || b.max.y > pos.y + 1.2f) continue;            // its top is on our level
                if (b.min.y > pos.y - 1.5f) continue;                                       // and it goes down
                if (Mathf.Abs(b.min.y - s.Goal.y) > below - 1f) continue;                  // to nearer the goal's level
                Vector3 f = ledge != null ? ledge.transform.forward : (b.center - pos);
                f.y = 0f;
                if (f.sqrMagnitude < 0.01f) continue;
                f.Normalize();
                float depth = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.z) * b.extents.z;
                Vector3 top = new Vector3(b.center.x, pos.y, b.center.z) + f * (depth + 0.6f);     // where you step off at the top
                Vector3 foot = new Vector3(b.center.x, b.min.y, b.center.z) - f * (depth + 0.7f);
                float score = Flat(pos, top) + Mathf.Abs(b.min.y - s.Goal.y);
                if (score < bestScore) { bestScore = score; best = root; bestId = id; bestTop = top; bestBase = foot; bestF = f; bestB = b; bestLedge = ledge != null; }
            }
            if (best == null) return false;
            // 1.5.3: the ladders below this one too, planned now and gone down in one go
            List<LadderStep> down = bestLedge ? PlanChainDown(bestB, bestId, bestF, s.Goal.y) : null;
            if (down != null && down.Count >= 2)
            {
                if (Flat(pos, down[0].TopSpot) <= 3f && Mathf.Abs(pos.y - down[0].TopSpot.y) < 1.0f) { StartChain(s, down, 0, pos, now, tag, true); return true; }
                s.PendingChain = down; s.PendingDown = true;
            }
            else { s.PendingChain = null; s.PendingDown = false; }
            s.DescendLadder = true; s.DescendTop = bestTop; s.ClimbBase = bestBase; s.ClimbDir = bestF; s.ClimbLadderId = bestId;
            s.StepUntil = 0f; s.ClimbUntil = 0f;
            if (Flat(pos, bestTop) > 1.0f)
            {
                s.LadderPending = true; s.HasDetour = true; s.Detour = bestTop; s.DetourUntil = now + 15f; s.NextPlan = 0f;
            }
            else s.DescendUntil = now + 10f;
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + below.ToString("F1") + "m above where it needs to be - going down the ladder " + best.name.Replace("(Clone)", "") + " (top at " + Fmt(bestTop) + ")");
            return true;
        }

        // stepping down the ladder: walk from its top back towards its foot and let it carry us down
        static void Descend(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            if (pos.y <= s.ClimbBase.y + 1.0f)
            {
                s.DescendLadder = false; s.DescendUntil = 0f; s.NextPlan = 0f; s.Ladder = 0; s.Passes = 0; s.MainBest = float.MaxValue;
                s.ProgressTime = now; s.AnchorTime = now; s.Anchor = pos;
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] went down the ladder to " + Fmt(pos));
                return;
            }
            Vector3 dir = s.ClimbBase - pos; dir.y = 0f;
            if (dir.sqrMagnitude < 0.09f) dir = -s.ClimbDir;
            dir.Normalize();
            ai.transform.rotation = Quaternion.LookRotation(dir);
            Steer(ai, c, s, dir, false, false);
        }

        static bool TryStartClimb(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag, string why)
        {
            if (!s.MainPartial || c.IsSwimming() || !c.IsOnGround() || s.ClimbTries >= ClimbMaxTries) return false;
            // how high we need to get: the last spot a full route started from, else the goal itself
            float needY = s.HasGood && s.GoodPos.y > pos.y + 1.5f && Vector3.Distance(pos, s.GoodPos) < 40f ? s.GoodPos.y : s.Goal.y;
            if (needY < pos.y + 1.5f) return false;
            needY = Mathf.Min(needY, pos.y + ClimbMaxRise);
            Vector3 top; float steep;
            if (!FindClimb(s, pos, needY, out top, out steep)) return false;
            s.ClimbTries++; s.ClimbTried.Add(top);
            s.ClimbTop = top; s.ClimbUntil = now + 15f; s.ClimbBestY = pos.y; s.ClimbProgressAt = now; s.NextClimbJump = 0f;
            s.StepUntil = 0f; s.HasDetour = false;
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stuck (" + why + ") " + (needY - pos.y).ToString("F1") + "m below where it needs to be at " + Fmt(pos) + " - climbing out at " + Fmt(top) + " (steepest step " + steep.ToString("F1") + "m, try " + s.ClimbTries + ")");
            return true;
        }

        // Ground heights along 16 directions, a meter at a time out to 16 m: the way up whose steepest one-meter step is
        // smallest (shorter ways win ties), skipping ways already tried and anything through deep water.
        static bool FindClimb(S s, Vector3 pos, float needY, out Vector3 top, out float steep)
        {
            top = Vector3.zero; steep = 0f;
            float bestScore = float.MaxValue;
            int mask = LayerMask.GetMask("terrain", "static_solid", "piece", "Default");
            for (int k = 0; k < 16; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                float prevY = pos.y, maxStep = 0f;
                for (int i = 1; i <= 16; i++)
                {
                    Vector3 p = pos + dir * i;
                    RaycastHit hit;
                    if (!Physics.Raycast(new Vector3(p.x, needY + 4f, p.z), Vector3.down, out hit, needY + 4f - (pos.y - 3f), mask)) break;
                    if (MHR_Water.DepthAt(hit.point) >= 1.0f) break;
                    float rise = hit.point.y - prevY;
                    if (rise < -1.5f) break;   // dips back down: not a way up
                    if (rise > 1.0f && hit.collider.gameObject.layer != s_terrainLayer) break;   // a built wall or a roof, not a slope
                    if (rise > 6f) break;
                    if (rise > maxStep) maxStep = rise;
                    prevY = hit.point.y;
                    if (hit.point.y >= needY - 0.5f)
                    {
                        Vector3 t = hit.point + dir * 0.75f;
                        bool tried = false;
                        foreach (Vector3 old in s.ClimbTried) if (Flat(old, t) < 3f) { tried = true; break; }
                        float score = maxStep * 3f + i * 0.1f;
                        if (!tried && score < bestScore) { bestScore = score; top = t; steep = maxStep; }
                        break;
                    }
                }
            }
            return bestScore < float.MaxValue;
        }

        static void Climb(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            Vector3 dir = s.ClimbTop - pos; dir.y = 0f;
            if (pos.y > s.ClimbBestY + 0.3f) { s.ClimbBestY = pos.y; s.ClimbProgressAt = now; s.ClimbJumps = 0; }
            bool atTop = pos.y >= s.ClimbTop.y - 0.6f;
            // 1.5.2: the ladder lifts in bounces every half second - only off the top once it has stopped lifting us
            // 1.5.3: and stand still while waiting - these ladders slope like stairs, and walking on for that moment took him
            // 2-3 m past the top end, off to the side of the next ladder (user: "keep going off to one side")
            if (atTop && s.ClimbLadder && s.ClimbAuto && pos.y < s.ClimbTop.y + 0.3f && now - s.ClimbProgressAt < 0.5f)
            {
                if (c.IsOnGround())
                {
                    Halt(ai, c, s);
                    // 1.5.3 (user: "can they start to turn in the direction they need to move next while they wait"): face the
                    // next ladder's foot, else the goal, turning at a walking pace
                    if (s.ClimbFaceAt < s.ClimbStartAt) { s.ClimbFaceAt = now; s.ClimbFace = NextFootDir(s, pos); }
                    if (s.ClimbFace.sqrMagnitude > 0.01f)
                        ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, Quaternion.LookRotation(s.ClimbFace), 360f * Time.deltaTime);
                    return;
                }
                atTop = false;
            }
            // 1.5.2: on a ladder keep pushing the same way into it - its top is right above, so "close enough" sideways means nothing
            bool toFoot = false;
            // 1.5.2: fell off on the way (a small floor between ladders) - not the ladder's fault; look again from down here
            // (user log: kept jumping at the ladder from the ground under the floor it starts from, then marked it bad)
            if (s.ClimbLadder && pos.y < s.ClimbStartY - 1.0f && c.IsOnGround())
            {
                s.ClimbUntil = 0f; s.ClimbLadder = false; s.NextPlan = 0f; s.Ladder = 0; s.NextLadderClimbSearch = 0f;
                s.ProgressTime = now; s.AnchorTime = now; s.Anchor = pos;
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] fell off on the way to the ladder (now at " + Fmt(pos) + ") - looking again");
                return;
            }
            if (s.ClimbLadder && !atTop)
            {
                dir = s.ClimbDir;
                // still on the ground short of the ladder's foot (or round the back of it): walk to the foot first
                // 1.5.2: only a few seconds of this close by - he used to shuffle round the foot for 25 s without pushing in
                float off = Flat(pos, s.ClimbBase);
                // 1.5.3: not getting any closer for 1.5 s (up against a wall beside the ladder - user: "Still stops at the wall
                // and sits there"): this is as near as it gets - push in / jump on from here
                if (off < s.ClimbFootBest - 0.15f) { s.ClimbFootBest = off; s.ClimbFootAt = now; }
                bool footBlocked = now - s.ClimbFootAt > 1.5f;
                if (s.ClimbAuto && !footBlocked && pos.y < s.ClimbBase.y + 0.8f && (off > 1.2f || (off > 0.6f && now - s.ClimbStartAt < 6f)))
                {
                    Vector3 toBase = s.ClimbBase - pos; toBase.y = 0f;
                    dir = toBase; toFoot = true;
                    // 1.5.3: keep to the floor getting there (user: "There is no shaft or gap up the ladder ... As long as the NPC
                    // stays directly on the ladder and floor sections there is no gap"). The straight line to the foot can cut
                    // across the open side of a narrow floor; then go round by a point that has floor all the way - from here to
                    // it and from it to the foot - like a player turning the corner. Only with no such way is the ladder dropped.
                    if (now < s.ClimbViaUntil)
                    {
                        Vector3 toVia = s.ClimbVia - pos; toVia.y = 0f;
                        if (toVia.magnitude < 0.35f) s.ClimbViaUntil = 0f;
                        else dir = toVia;
                    }
                    else if (!FloorAlong(pos, s.ClimbBase))
                    {
                        Vector3 via;
                        if (FindFloorVia(pos, s.ClimbBase, out via))
                        {
                            s.ClimbVia = via; s.ClimbViaUntil = now + 3f;
                            Vector3 toVia = via - pos; toVia.y = 0f; dir = toVia;
                            if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] going round by " + Fmt(via) + " to the foot of the ladder, keeping to the floor");
                        }
                        else
                        {
                            s.ClimbUntil = 0f; s.ClimbLadder = false; s.NextPlan = 0f; s.Ladder = 0;
                            Halt(ai, c, s); s.ProgressTime = now; s.AnchorTime = now; s.Anchor = pos;
                            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] no floor all the way to the foot of the ladder at " + Fmt(s.ClimbBase) + " - trying another");
                            return;
                        }
                    }
                    if (now - s.ClimbStartAt < 8f) s.ClimbProgressAt = Mathf.Max(s.ClimbProgressAt, now - 4f);   // walking there isn't "stuck" yet
                }
            }
            if (atTop || dir.sqrMagnitude < 0.25f)
            {
                s.ClimbUntil = 0f; s.NextPlan = 0f; s.Ladder = 0; s.Passes = 0; s.MainBest = float.MaxValue;
                s.ProgressTime = now; s.AnchorTime = now; s.Anchor = pos;
                bool wasLadder = s.ClimbLadder;
                if (wasLadder && atTop && pos.y - s.ClimbStartY < 1.0f) MarkBadLadder(s.ClimbLadderId, now, 300f);   // barely got us anywhere
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + (atTop ? (wasLadder ? "climbed the ladder" : "climbed out") : "reached the climb spot") + " at " + Fmt(pos));
                s.ClimbLadder = false;
                // 1.5.3: a ladder that worked isn't "tried" - after a fall it's the way back up (user log: "no ladder found"
                // beside the two ladders he had just climbed)
                if (wasLadder && atTop) s.LadderClimbsTried.Clear();
                if (wasLadder && atTop)
                {
                    // 1.5.3: still further up to go - the next ladder first, straight from here (user log: in the ruin the next
                    // ladder's foot is right by this one's top; stepping off first took him away from it, round a hole)
                    if (s.Goal.y > pos.y + 1.5f && c.IsOnGround() && TryStartLadderClimb(ai, c, s, pos, now, tag)) return;
                    LadderTopHop(ai, c, s, pos, now, tag);
                    // look for the next ladder up straight away (ladders with a small floor between them: walking on towards
                    // the goal for the 2 s until the next search took him off the edge)
                    s.NextLadderClimbSearch = 0f; s.NextLadderSearch = 0f;
                }
                return;
            }
            dir.Normalize();
            // out of breath: stand and get some back, like a player would, then go again
            float max = MaxStamina(ai);
            if (max > 0f && (now < s.ClimbRestUntil || Stamina(ai) < max * 0.15f) && c.IsOnGround())
            {
                if (now >= s.ClimbRestUntil) s.ClimbRestUntil = now + 5f;
                if (Stamina(ai) >= max * 0.6f) s.ClimbRestUntil = 0f;
                Halt(ai, c, s); s.ClimbProgressAt = now; s.ClimbUntil = Mathf.Max(s.ClimbUntil, now + 6f);
                return;
            }
            // no higher in 5 s: this way doesn't go; the next stuck check picks another or carries on with the ladder
            if (now - s.ClimbProgressAt > 5f)
            {
                s.ClimbUntil = 0f; s.AnchorTime = now - StillTime; s.Ladder = 0;
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't " + (s.ClimbLadder ? "get up the ladder" : "climb out") + " at " + Fmt(s.ClimbTop) + " (got to " + Fmt(pos) + ")");
                if (s.ClimbLadder) MarkBadLadder(s.ClimbLadderId, now, 300f);
                s.ClimbLadder = false;
                return;
            }
            ai.transform.rotation = Quaternion.LookRotation(dir);
            Steer(ai, c, s, dir, !s.ClimbLadder, false);   // 1.5.3: walk up ladders, save the stamina
            // jump whenever there's ground under us and the face ahead rises, or we've stopped moving forward.
            // 1.5.2: not on a ladder with its own auto-jump - walking into it is all a player does there; only help out
            // if that hasn't lifted us for a couple of seconds.
            // 1.5.2 (user: "The NPC should be able to jump onto the bottom of a ladder if they need. I do that all the time
            // rather than trying to run up from the bottom"): at the foot, jump on when its bottom rung is above us or a
            // second of pushing hasn't lifted us; otherwise let the ladder do it.
            if (s.ClimbLadder && s.ClimbAuto)
            {
                bool bottomUp = s.ClimbBottomY > pos.y + 0.4f;
                // 1.5.3: at the foot, jump on after 0.4 s like a player stepping up (every ladder in the user's ruin needed it, and
                // the full second each time made him "pause on each landing"); part way up, give the ladder a second first
                // 1.5.3: the ladder's bump first (as for a player); a hop only when its bottom is up off the floor, or after 2 s
                float liftWait = 2f;
                if (toFoot || (!bottomUp && now - s.ClimbProgressAt < liftWait)) return;
                if (c.IsOnGround() && now >= s.NextClimbJump)
                {
                    // still not on it after a few jumps: this one is out of reach - try another way up (user log: jumped at
                    // a ladder 1.9 m up until out of stamina, then stood resting at its foot)
                    bool onIt = pos.y > s.ClimbStartY + 0.8f;
                    // 1.5.2: also part way up - jumping on the spot there for 20 s (user log) gets nowhere either
                    if (s.ClimbJumps >= LadderJumpTries)
                    {
                        s.ClimbUntil = 0f; s.AnchorTime = now - StillTime; s.Ladder = 0; s.NextPlan = 0f;
                        MarkBadLadder(s.ClimbLadderId, now, 120f);
                        if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get " + (onIt ? "further up" : "onto") + " the ladder at " + Fmt(s.ClimbBase) + " after " + s.ClimbJumps + " jumps (its bottom is " + (s.ClimbBottomY - s.ClimbStartY).ToString("F1") + "m up, best " + (s.ClimbBestY - s.ClimbStartY).ToString("F1") + "m) - trying another way");
                        s.ClimbLadder = false;
                        return;
                    }
                    c.Jump(false);
                    Rigidbody rb = ai.GetComponent<Rigidbody>();
                    if (rb != null) { Vector3 bv = rb.linearVelocity; bv.x = dir.x * 3f; bv.z = dir.z * 3f; rb.linearVelocity = bv; }
                    s.NextClimbJump = now + 1.2f;
                    s.ClimbJumps++;
                    if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] jumping " + (onIt ? "up the ladder" : "onto the ladder") + " (try " + s.ClimbJumps + "/" + LadderJumpTries + (bottomUp ? ", its bottom is " + (s.ClimbBottomY - pos.y).ToString("F1") + "m up" : "") + ")");
                }
                return;
            }
            if (c.IsOnGround() && now >= s.NextClimbJump)
            {
                Vector3 v = c.GetVelocity(); v.y = 0f;
                RaycastHit hit;
                bool rises = Physics.Raycast(pos + dir * 0.9f + Vector3.up * 3f, Vector3.down, out hit, 4f, Mask()) && hit.point.y > pos.y + 0.4f;
                if (rises || v.magnitude < 1f)
                {
                    c.Jump(false);
                    Rigidbody rb = ai.GetComponent<Rigidbody>();
                    if (rb != null) { Vector3 bv = rb.linearVelocity; bv.x = dir.x * 5f; bv.z = dir.z * 5f; rb.linearVelocity = bv; }
                    s.NextClimbJump = now + 0.3f;
                }
            }
        }

        // 1.4.82: fell somewhere with no route out (1.4.81 log: down a dry moat with sheer walls, only a player's
        // sprint-jumping gets out). After a full stuck ladder, well below the last spot that had a full route and still
        // no route from here: put him back there.
        static void Rescue(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            if (!s.HasGood || !s.MainPartial || c.IsSwimming() || ai.IsInCombat) return;
            if (pos.y > s.GoodPos.y - 2f || Vector3.Distance(pos, s.GoodPos) > 40f || now - s.GoodAt > 300f) return;
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] trapped at " + Fmt(pos) + " (" + (s.GoodPos.y - pos.y).ToString("F1") + "m below the last good spot) - moving back to " + Fmt(s.GoodPos));
            ai.MHR_PlaceAt(s.GoodPos);
            s.HasGood = false;
            Reset(s, s.Goal, s.GoodPos, now, tag);
        }

        // 1.4.83: the straight line from -> to, every half meter: each step up no more than a hop (1.1 m), no big drop,
        // no deep water, and nothing in the way at chest height (walls, doors, fences taller than a hop).
        // 1.4.97: drops up to General.MaxDrop (3 m - a player takes those without damage) and gaps up to General.JumpGap
        // (jumped with a running leap) are fine too (user: jump across to the beehives instead of down the stairs and round)
        static string s_hopWhy;
        static bool HopLine(Vector3 from, Vector3 to, out int hops) { return HopLine(from, to, out hops, null); }
        static bool HopLine(Vector3 from, Vector3 to, out int hops, Character c)
        {
            hops = 0; s_hopWhy = null;
            float maxDrop = ModConfig.MaxDrop != null ? ModConfig.MaxDrop.Value : 3f;
            float maxGap = ModConfig.JumpGap != null ? ModConfig.JumpGap.Value : 1.5f;
            Vector3 d = to - from; d.y = 0f;
            float len = d.magnitude;
            if (len < 1.5f) return false;
            d /= len;
            Vector3 prev = from;
            float gap = 0f;
            for (float x = 0.5f; x <= len + 0.01f; x += 0.5f)
            {
                Vector3 p = from + d * Mathf.Min(x, len);
                RaycastHit hit;
                // 1.4.84: from just above where the feet are, like walking: a roof or an upper floor overhead isn't the ground
                bool ground = Physics.Raycast(new Vector3(p.x, prev.y + 1.5f, p.z), Vector3.down, out hit, 2f + maxDrop, Mask());
                float rise = ground ? hit.point.y - prev.y : -100f;
                if (!ground || rise < -maxDrop)
                {
                    gap += 0.5f;
                    if (gap > maxGap) { s_hopWhy = "a drop or gap at " + Fmt(p); return false; }
                    continue;
                }
                if (gap > 0f)
                {
                    // landing after a jump: not higher than we can jump up to while leaping across
                    if (rise > 0.3f) { s_hopWhy = "the far side of the gap at " + Fmt(p) + " is too high"; return false; }
                    // 1.4.98: and within what this companion can jump right now (speed, Jump skill, stamina)
                    if (c != null && (!MHR_Jump.CanAfford(c) || gap + 0.25f > MHR_Jump.Reach(c, d, rise))) { s_hopWhy = "the gap at " + Fmt(p) + " is too wide to jump" + (MHR_Jump.CanAfford(c) ? "" : " (out of stamina)"); return false; }
                    hops++; gap = 0f;
                }
                if (rise > 1.1f) { s_hopWhy = "a step too high at " + Fmt(p); return false; }
                // 1.5.44: not straight through a fire (the hearth)
                if (NearFire(hit.point, 0.4f) && !InFire(to)) { s_hopWhy = "a fire at " + Fmt(hit.point); return false; }
                // 1.4.99: with the companion known, only water it would have to swim in rules the line out (it wades or jumps the rest)
                if (c != null ? MHR_Water.WouldSwim(c, hit.point) : MHR_Water.DepthAt(hit.point) > 0.8f) { s_hopWhy = "deep water at " + Fmt(p); return false; }
                if (Physics.Linecast(prev + Vector3.up * 1.4f, hit.point + Vector3.up * 1.4f, Mask())) { s_hopWhy = "something in the way at " + Fmt(p); return false; }
                if (rise > 0.35f) hops++;
                prev = hit.point;
            }
            if (gap > 0f) { s_hopWhy = "it ends over a drop"; return false; }
            // 1.4.88: and it has to actually end up where the target is. Sampling from just above the feet can't see a
            // ledge higher than that, so a chest up on one looked like "flat all the way, 0 hops" (1.4.87 log: back and
            // forth under the ledge chest for a minute after the job was turned off)
            if (Mathf.Abs(prev.y - to.y) > 1.0f) { s_hopWhy = "it doesn't end up at the target's height"; return false; }
            return hops <= 10;
        }

        // 1.4.97: on a straight line, a gap right in front with ground on the other side: running leap across
        static bool GapAhead(Character c, Vector3 pos, Vector3 dir)
        {
            float maxDrop = ModConfig.MaxDrop != null ? ModConfig.MaxDrop.Value : 3f;
            float maxGap = ModConfig.JumpGap != null ? ModConfig.JumpGap.Value : 1.5f;
            RaycastHit hit;
            Vector3 p = pos + dir * 0.6f;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 1.5f + maxDrop, Mask())) return false;
            for (float x = 1.0f; x <= maxGap + 1.0f; x += 0.5f)
            {
                Vector3 q = pos + dir * x;
                if (Physics.Raycast(q + Vector3.up * 1.5f, Vector3.down, out hit, 1.5f + maxDrop, Mask()))
                {
                    float reach = MHR_Jump.Reach(c, dir, hit.point.y - pos.y);
                    // 1.5.2: over a drop that would hurt (a miss isn't just a stumble), only with room to spare, like a player
                    float under;
                    if (LedgeAhead(pos, dir, 0.6f, out under)) reach *= 0.8f;
                    return hit.point.y < pos.y + 0.3f && MHR_Water.DepthAt(hit.point) < 0.8f && MHR_Jump.CanAfford(c) && x <= reach;
                }
            }
            return false;
        }

        // something to hop up onto just ahead (a step, a low wall), or a knee-high block
        static bool StepAhead(Vector3 pos, Vector3 dir)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + dir * 0.7f + Vector3.up * 2f, Vector3.down, out hit, 2.5f, Mask()) && hit.point.y > pos.y + 0.35f && hit.point.y < pos.y + 1.3f) return true;
            return Physics.Raycast(pos + Vector3.up * 0.4f, dir, 0.7f, Mask()) && !Physics.Raycast(pos + Vector3.up * 1.4f, dir, 1.0f, Mask());
        }

        // ---------- fire ----------
        // 1.4.88: don't stand or walk in a fire (user: twice he stood in the fire pit and caught fire).
        // 1.4.90: the flames themselves - EffectArea type Burning (what cooking stations check, what sets you alight).
        // 1.4.88-89 used type Fire, the wide area fire-shy creatures keep away from: it covered the whole kitchen, so
        // almost every spot by a chest counted as "in a fire" and cooking couldn't reach anything (1.4.89 log).
        // 1.5.44: in the flames or within margin of them - for picking a spot to stand (idle spots, follow spots)
        // 1.5.72: a built piece that hurts whatever touches it - a spike wall (sharpened stakes, the Dvergr ones, modded
        // ones): a Piece with an Aoe that does damage and isn't a fire (fires are handled above, and a hearth or campfire
        // has an Aoe too). Found by what it does, not by name. Returns the collider nearest p, or null.
        static readonly Collider[] s_spikeBuf = new Collider[32];
        public static Collider SpikeNear(Vector3 p, float r)
        {
            try
            {
                int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.6f, r, s_spikeBuf, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                {
                    Collider col = s_spikeBuf[i];
                    if (col == null) continue;
                    Aoe aoe = col.GetComponentInParent<Aoe>();
                    if (aoe == null || aoe.GetComponentInParent<Piece>() == null || aoe.GetComponentInParent<Fireplace>() != null) continue;
                    if (aoe.m_damage.GetTotalDamage() <= 0f) continue;
                    return col;
                }
            }
            catch { }
            return null;
        }

        // 1.5.72: a short nudge (closing the last bit on a tree or rock) - a drop, fire or spikes that far ahead. Not a wall
        // check: the thing it's walking up to is the wall.
        public static bool HazardAhead(Vector3 pos, Vector3 dir, float dist)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return true;
            dir.Normalize();
            Vector3 p = pos + dir * dist;
            return DropAhead(pos, dir, dist, 1.2f) || FireArea(p, 0.3f) != null || SpikeNear(p, 0.6f) != null;
        }

        // 1.5.72: fire or spikes where a jump would land (2-4 m on)
        static bool LandingHazard(Vector3 pos, Vector3 dir)
        {
            for (float d = 2f; d <= 4f; d += 1f)
            {
                Vector3 p = pos + dir * d;
                if (FireArea(p, 0.5f) != null || SpikeNear(p, 0.7f) != null || Lava(p)) return true;
            }
            return false;
        }

        public static bool NearFire(Vector3 p, float margin)
        {
            return FireArea(p, margin) != null;
        }

        // 1.5.45: the fire at a spot, checked at knee, waist and head height. 1.5.44 test (log 31ec683a): the route check
        // looked 0.3 m above the floor and passed under the hearth's flames; Gunter stepped up onto the hearth stone
        // (0.8 m higher) into them, and the hearth's own fire damaged the hearth (Aoe m_damageSelf, 10 a time - the game
        // does the same when a player stands in it).
        // 1.5.74: Ashlands lava (user: "the NPC dies to silly things due to lava in the ashlands"). Lava is the ground's paint
        // (ZoneSystem.IsLava - the heightmap's lava mask), not an object; standing on lava builds heat that burns 10, then 100
        // a tick (Character.UpdateLava/UpdateHeatDamage). Like the game, standing on something built or a rock above it is safe
        // (a ray down that hits a collider that isn't the heightmap). The game's route planner doesn't know about it.
        public static bool Lava(Vector3 p)
        {
            try
            {
                if (ZoneSystem.instance == null || !WorldGenerator.IsAshlands(p.x, p.z)) return false;
                if (!ZoneSystem.instance.IsLava(p)) return false;
                RaycastHit hit;
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 4f, Mask()) && hit.collider.GetComponent<Heightmap>() == null) return false;
                return true;
            }
            catch { return false; }
        }

        static bool LegClearOfLava(Vector3 p0, Vector3 p1, float skipStart = 0.3f)
        {
            Vector3 leg = p1 - p0; leg.y = 0f;
            float len = leg.magnitude;
            if (len < 0.05f) return !Lava(p0);
            Vector3 dir = leg / len;
            for (float t = skipStart; t < len; t += 0.5f)
            {
                if (Lava(p0 + dir * t)) return false;
            }
            return !Lava(p1);
        }

        // the nearest way off it: a direction whose whole stretch out is clear of lava
        static bool LavaExit(Vector3 pos, Vector3 prefer, out Vector3 dir, out float dist)
        {
            dir = Vector3.zero; dist = 0f;
            prefer.y = 0f;
            if (prefer.sqrMagnitude < 0.01f) prefer = Vector3.forward;
            prefer.Normalize();
            for (float r = 1.5f; r <= 8f; r += 1.5f)
                for (int i = 0; i < 16; i++)
                {
                    float ang = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 22.5f;
                    Vector3 d = Quaternion.Euler(0f, ang, 0f) * prefer;
                    if (!Lava(pos + d * r) && !DropAhead(pos, d, 1.0f, 1.2f)) { dir = d; dist = r; return true; }
                }
            return false;
        }

        static readonly Dictionary<int, float> s_lavaSince = new Dictionary<int, float>();
        static readonly Dictionary<int, float> s_lavaLog = new Dictionary<int, float>();

        // on lava: get off it at once - walk to the nearest clear ground, jump if that doesn't do it within a second
        public static bool EscapeLava(CompanionAI ai)
        {
            if (ai == null) return false;
            Character c = ai.GetComponent<Character>();
            if (c == null || c.IsAttached() || c.IsDead() || c.IsSwimming()) return false;
            int id = ai.gameObject.GetInstanceID();
            Vector3 pos = ai.transform.position;
            if (!WorldGenerator.IsAshlands(pos.x, pos.z) || (!c.InLava() && !Lava(pos))) { s_lavaSince.Remove(id); return false; }
            if (!c.IsOnGround()) return true;   // mid-jump out
            float now = Time.time, since;
            if (!s_lavaSince.TryGetValue(id, out since)) { since = now; s_lavaSince[id] = now; }
            Vector3 dir; float dist;
            bool exit = LavaExit(pos, ai.transform.forward, out dir, out dist);
            float last;
            if (!s_lavaLog.TryGetValue(id, out last) || now - last > 5f)
            {
                s_lavaLog[id] = now;
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move] \"" + c.m_name + "\" standing in lava at " + Fmt(pos) + " - " + (exit ? "getting out toward " + Fmt(pos + dir * dist) : "no clear ground close by"));
            }
            if (!exit) return false;
            if (now - since > 1.0f && MHR_Jump.CanAfford(c)) { s_lavaSince[id] = now; MHR_Jump.Leap(ai, c, dir); return true; }
            ai.MoveTowards(dir, true);
            return true;
        }

        internal static EffectArea FireArea(Vector3 p, float radius)
        {
            try
            {
                for (float h = 0.3f; h < 2f; h += 0.6f)
                {
                    EffectArea a = EffectArea.IsPointInsideArea(p + Vector3.up * h, EffectArea.Type.Burning, radius);
                    if (a != null) return a;
                }
                return FireZoneAt(p, radius);
            }
            catch { }
            return null;
        }

        public static bool InFire(Vector3 p)
        {
            try { return EffectArea.IsPointInsideArea(p + Vector3.up * 0.3f, EffectArea.Type.Burning, 0.3f) != null || FireZoneAt(p, 0.2f) != null; }
            catch { return false; }
        }

        // 1.5.70: a lit fire's damage zone (its Aoe - the part that hurts, a box, a trigger or a sphere) isn't its Burning
        // effect area (what the checks above look at) - log bced720b: hurt on the hearth stone 0.6 m from its middle. Lit
        // fireplaces with a damaging Aoe, refreshed every 2 s.
        class FireZone { public Fireplace Fp; public EffectArea Area; public Aoe Aoe; public Collider[] Triggers; }
        static readonly List<FireZone> s_zones = new List<FireZone>();
        static float s_zonesAt = -10f;

        static EffectArea FireZoneAt(Vector3 p, float margin)
        {
            float now = Time.time;
            if (now - s_zonesAt > 2f)
            {
                s_zonesAt = now;
                s_zones.Clear();
                foreach (Fireplace fp in UnityEngine.Object.FindObjectsByType<Fireplace>(FindObjectsSortMode.None))
                {
                    if (fp == null || !fp.IsBurning()) continue;
                    EffectArea area = fp.GetComponentInChildren<EffectArea>();
                    foreach (Aoe aoe in fp.GetComponentsInChildren<Aoe>(false))
                    {
                        if (aoe == null || (aoe.m_damage.m_fire <= 0f && aoe.m_damageSelf <= 0f && aoe.m_damage.GetTotalDamage() <= 0f)) continue;
                        List<Collider> trig = new List<Collider>();
                        foreach (Collider tc in aoe.GetComponents<Collider>()) if (tc != null && tc.isTrigger) trig.Add(tc);
                        s_zones.Add(new FireZone { Fp = fp, Area = area, Aoe = aoe, Triggers = trig.ToArray() });
                    }
                }
            }
            for (int i = 0; i < s_zones.Count; i++)
            {
                FireZone z = s_zones[i];
                if (z.Aoe == null || !z.Aoe.isActiveAndEnabled) continue;
                Vector3 at = z.Aoe.transform.position;
                if ((at - p).sqrMagnitude > 64f) continue;
                for (float h = 0.3f; h < 2f; h += 0.6f)
                {
                    Vector3 q = p + Vector3.up * h;
                    bool inside;
                    if (z.Aoe.m_useTriggers && z.Triggers.Length > 0)
                    {
                        inside = false;
                        foreach (Collider tc in z.Triggers) if (tc != null && Vector3.Distance(tc.ClosestPoint(q), q) <= margin) { inside = true; break; }
                    }
                    else if (z.Aoe.m_useCollider != null)
                    {
                        Vector3 local = Quaternion.Inverse(z.Aoe.transform.rotation) * (q - (at + z.Aoe.m_useCollider.center));
                        Vector3 half = z.Aoe.m_useCollider.size * 0.5f + Vector3.one * margin;
                        inside = Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
                    }
                    else inside = Vector3.Distance(q, at) <= z.Aoe.m_radius + margin;
                    if (inside) return z.Area ?? z.Fp.GetComponentInChildren<EffectArea>();
                }
            }
            return null;
        }

        // the nearest way out: a direction 1.5 m away that isn't in a fire and has ground
        // 1.4.100: further out too - a hearth's flames reach past 1.5 m from its middle (1.4.99 test: got up from a chair
        // into the hearth and stood in it, every way out within 1.5 m still counted as fire)
        static bool FireExit(Vector3 pos, Vector3 prefer, out Vector3 dir, bool wholeLeg = false)
        {
            dir = Vector3.zero;
            prefer.y = 0f;
            if (prefer.sqrMagnitude < 0.01f) prefer = Vector3.forward;
            prefer.Normalize();
            for (float r = 1.5f; r <= 3.6f; r += 1f)
                for (int i = 0; i < 8; i++)
                {
                    float ang = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 45f;
                    Vector3 d = Quaternion.Euler(0f, ang, 0f) * prefer;
                    if (!NearFire(pos + d * r, 0.3f) && !DropAhead(pos, d, 1.0f, 1.2f) && (!wholeLeg || LegClearOfFire(pos, pos + d * r, 0.6f))) { dir = d; return true; }
                }
            return false;
        }

        // the nearest spot to stand on that isn't in a fire, not on furniture
        static bool SafeSpot(Vector3 pos, out Vector3 spot)
        {
            spot = pos;
            for (float r = 1.5f; r <= 5f; r += 0.75f)
                for (int i = 0; i < 12; i++)
                {
                    Vector3 p = pos + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * r;
                    RaycastHit hit;
                    if (!Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out hit, 4f, Mask())) continue;
                    if (Mathf.Abs(hit.point.y - pos.y) > 1.2f || InFire(hit.point) || Lava(hit.point) || MHR_Chests.IsFurniture(hit.collider)) continue;
                    if (MHR_Water.DepthAt(hit.point) > 0.5f) continue;
                    if (Physics.CheckCapsule(hit.point + Vector3.up * 0.5f, hit.point + Vector3.up * 1.6f, 0.3f, Mask())) continue;
                    spot = hit.point; return true;
                }
            return false;
        }

        static readonly Dictionary<int, float> s_fireLog = new Dictionary<int, float>();
        static readonly Dictionary<int, float> s_fireSince = new Dictionary<int, float>();
        static readonly Dictionary<int, float> s_fireJump = new Dictionary<int, float>();

        // called before anything else steers the companion: standing in a fire -> step out of it; jump out if that
        // doesn't work (like a player), and if it's still in it after a few seconds, put it on the nearest safe spot
        public static bool EscapeFire(CompanionAI ai)
        {
            if (ai == null) return false;
            Character c = ai.GetComponent<Character>();
            int id = ai.gameObject.GetInstanceID();
            if (c == null || c.IsAttached() || c.IsDead()) return false;
            Vector3 pos = ai.transform.position;
            if (!InFire(pos)) { s_fireSince.Remove(id); return false; }
            if (!c.IsOnGround()) return false;   // mid-jump over or out of one
            float now = Time.time, since;
            if (!s_fireSince.TryGetValue(id, out since)) { since = now; s_fireSince[id] = now; }
            Vector3 dir;
            bool exit = FireExit(pos, ai.transform.forward, out dir);
            float last;
            if (!s_fireLog.TryGetValue(id, out last) || now - last > 10f)
            {
                s_fireLog[id] = now;
                if (CompanionsPlugin.LogDetailed) Dbg("[Move] standing in a fire at " + Fmt(pos) + " - " + (exit ? "stepping out" : "no clear way out"));
            }
            if (now - since > 3f)
            {
                Vector3 spot;
                if (SafeSpot(pos, out spot))
                {
                    ai.MHR_PlaceAt(spot);
                    s_fireSince.Remove(id);
                    if (CompanionsPlugin.LogDetailed) Dbg("[Move] still in the fire after 3s (stuck in it?) - moved to " + Fmt(spot));
                    return true;
                }
            }
            float lastJump;
            if (exit && now - since > 1.0f && (!s_fireJump.TryGetValue(id, out lastJump) || now - lastJump > 1.2f))
            {
                s_fireJump[id] = now;
                MHR_Jump.Leap(ai, c, dir);
                if (CompanionsPlugin.LogDetailed) Dbg("[Move] jumping out of the fire");
                return true;
            }
            if (exit) ai.MoveTowards(dir, true);
            return true;
        }

        // 1.4.82: true when the ground a step ahead is missing or more than maxDrop below (a ledge, a pit)
        // a dry drop of more than a harmless fall (MaxDrop, 3 m) just ahead; deep water below is fine to land in
        static bool LedgeAhead(Vector3 pos, Vector3 dir, float dist, out float drop)
        {
            drop = 0f;
            float maxDrop = ModConfig.MaxDrop != null ? ModConfig.MaxDrop.Value : 3f;
            Vector3 p = pos + dir.normalized * dist;
            RaycastHit hit;
            // 1.5.3: a foot-sized probe, not a line - a stair-like ladder is separate rungs with gaps between (a line slipped
            // between two rungs and read as an edge: "stopping at the edge" in the middle of the user's ruin ladders)
            if (Physics.SphereCast(p + Vector3.up * 1.0f, 0.25f, Vector3.down, out hit, 1.0f + maxDrop, Mask())) return false;
            if (!Physics.Raycast(p + Vector3.up * 1.0f, Vector3.down, out hit, 60f, Mask())) { drop = 60f; return true; }
            if (MHR_Water.DepthAt(hit.point) >= 1.0f) return false;
            drop = pos.y - hit.point.y;
            return drop > maxDrop;
        }

        public static bool DropAhead(Vector3 pos, Vector3 dir, float dist, float maxDrop)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            Vector3 p = pos + dir.normalized * dist;
            RaycastHit hit;
            if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 1.5f + maxDrop, Mask())) return true;
            return MHR_Water.DepthAt(hit.point) >= 1.0f;
        }

        static void Hop(CompanionAI ai, Character c, S s, Vector3 pos, Vector3 dir, float now, string tag, string why)
        {
            // 1.5.69: swimming against a bank - jump out like a player (the game lets a swimmer that touches the world jump,
            // Character.Jump: InLiquidSwimDepth and m_hitWorldTime < 0.25). Log fc93f903: stuck 7 minutes in the pool by
            // the stake wall, every unstuck step cancelled by the deep water
            if (c.IsSwimming())
            {
                ai.transform.rotation = Quaternion.LookRotation(dir);
                c.Jump(false);
                Rigidbody wrb = ai.GetComponent<Rigidbody>();
                if (wrb != null) { Vector3 v = wrb.linearVelocity; v.x = dir.x * 4f; v.z = dir.z * 4f; wrb.linearVelocity = v; }
                s.StepDir = dir; s.StepRun = true; s.StepUntil = now + 0.7f;
                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] stuck in the water (" + why + ") at " + Fmt(pos) + " - jumping out toward " + Fmt(pos + dir * 2f));
                return;
            }
            if (!c.IsOnGround()) { Sidestep(ai, c, s, pos, dir, now, tag, why, true); return; }
            // 1.5.70: never hop toward a fire (the hearth stone is a step it would hop up onto)
            if (DropAhead(pos, dir, 1.5f, 1.2f) || NearFire(pos + dir * 1.0f, 0.4f) || NearFire(pos + dir * 2.0f, 0.4f) || Lava(pos + dir * 1.5f) || Lava(pos + dir * 3f)) { Sidestep(ai, c, s, pos, dir, now, tag, why, true); return; }
            ai.transform.rotation = Quaternion.LookRotation(dir);
            c.Jump(false);
            Rigidbody rb = ai.GetComponent<Rigidbody>();
            if (rb != null) { Vector3 v = rb.linearVelocity; v.x = dir.x * 4f; v.z = dir.z * 4f; rb.linearVelocity = v; }
            s.StepDir = dir; s.StepRun = true; s.StepUntil = now + 0.7f;
            if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] stuck (" + why + ") at " + Fmt(pos) + " - jumping toward " + Fmt(pos + dir * 2f));
        }

        static bool Clear(Vector3 pos, Vector3 dir) { return Clear(pos, dir, 1.5f); }
        static bool SafeClear(Vector3 pos, Vector3 dir) { return Clear(pos, dir) && !DropAhead(pos, dir, 1.5f, 1.2f); }

        // 1.5.2: stuck above where it needs to be (user: "what about going down a ladder? ... jump down to lower ledge? ...
        // a player will jump even if its not safe as long as its not a fall that will kill them"). Look for an edge in reach
        // with ground below no further down than [Movement] StuckDrop (no deep water), landing nearer the goal's height and
        // not further from it - the smallest drop that gets close to the goal's level wins - and jump off it. Only when stuck:
        // a walkable way down comes first, like a player who takes the stairs when they're not far.
        static bool TryDropDown(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            if (c.IsSwimming() || !c.IsOnGround()) return false;
            Vector3 goal = s.Goal;
            float below = pos.y - goal.y;
            if (below < 1.5f) return false;
            // 1.5.2: companions take fall damage now: never a drop that would cost more than 30% of its health right now
            float maxDrop = Mathf.Min(ModConfig.StuckDrop != null ? ModConfig.StuckDrop.Value : 8f, CompanionFallDamagePatch.SafeDrop(c, 0.3f));
            float here = Flat(pos, goal);
            float bestScore = float.MaxValue; Vector3 bestDir = Vector3.zero, bestLand = Vector3.zero; float bestEdge = 0f;
            for (int k = 0; k < 16; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                if (!Clear(pos, dir, 1.0f)) continue;                                    // a wall right there
                for (float d = 0.6f; d <= 3.01f; d += 0.3f)
                {
                    Vector3 p = pos + dir * d;
                    RaycastHit hit;
                    if (!Physics.Raycast(new Vector3(p.x, pos.y + 1.0f, p.z), Vector3.down, out hit, maxDrop + 1.5f, Mask())) { if (d > 0.6f) break; else continue; }
                    float drop = pos.y - hit.point.y;
                    if (drop < 1.2f) continue;                                             // still our floor
                    if (drop > maxDrop) break;                                             // too far down this way
                    if (MHR_Water.DepthAt(hit.point) > 1.0f) break;
                    float flatAfter = Flat(hit.point, goal);
                    if (flatAfter > here + 4f) break;                                      // takes us well away from it
                    float score = Mathf.Abs(hit.point.y - goal.y) * 2f + flatAfter * 0.3f + drop * 0.2f;
                    if (score < bestScore) { bestScore = score; bestDir = dir; bestLand = hit.point; bestEdge = d; }
                    break;
                }
            }
            if (bestDir == Vector3.zero || Mathf.Abs(bestLand.y - goal.y) > below - 1f) return false;   // must get us closer to its level
            ai.transform.rotation = Quaternion.LookRotation(bestDir);
            Rigidbody rb = ai.GetComponent<Rigidbody>();
            if (rb != null) { Vector3 v = rb.linearVelocity; v.x = bestDir.x * 3f; v.z = bestDir.z * 3f; rb.linearVelocity = v; }
            s.StepDir = bestDir; s.StepRun = false; s.StepUntil = now + 0.5f + bestEdge * 0.3f;
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + below.ToString("F1") + "m above where it needs to be - dropping down to " + Fmt(bestLand) + " (" + (pos.y - bestLand.y).ToString("F1") + "m)");
            return true;
        }

        // 1.5.2: stuck a little below where it needs to be with something to stand on in reach (the top of a wall beside the
        // ladder, a crate, a ledge): jump up onto it like a player, then again from there (user: "The top of this one ends in
        // a wall and you have to jump on the wall to really get up onto the floor").
        static bool TryHopUp(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
        {
            if (c.IsSwimming() || !c.IsOnGround() || s.HopUpTries >= 6) return false;
            Vector3 goal = s.HasGood && s.GoodPos.y > pos.y + 0.5f && Vector3.Distance(pos, s.GoodPos) < 40f ? s.GoodPos : s.Goal;
            float need = goal.y - pos.y;
            if (need < 0.5f || need > 6f || Flat(pos, goal) > 15f) return false;
            float up = c.m_jumpForce * c.m_jumpForce / (2f * Mathf.Max(1f, -Physics.gravity.y)) * 0.85f;
            Vector3 toGoal = goal - pos; toGoal.y = 0f;
            Vector3 fwd = toGoal.sqrMagnitude > 0.04f ? toGoal.normalized : ai.transform.forward;
            float bestY = float.MinValue, bestD = 0f; Vector3 bestDir = Vector3.zero, bestPt = Vector3.zero;
            for (int k = 0; k < 12; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 30f, 0f) * fwd;
                for (float d = 0.6f; d <= 2.61f; d += 0.4f)
                {
                    Vector3 p = pos + dir * d;
                    RaycastHit hit;
                    if (!Physics.Raycast(new Vector3(p.x, pos.y + up + 1.0f, p.z), Vector3.down, out hit, up + 1.5f, Mask())) continue;
                    float dy = hit.point.y - pos.y;
                    if (dy < 0.4f || dy > up) continue;                                       // a real step up, within a jump
                    if (hit.normal.y < 0.6f) continue;                                         // something flat enough to stand on
                    if (Physics.Raycast(new Vector3(p.x, hit.point.y + 0.1f, p.z), Vector3.up, 1.2f, Mask())) continue;   // room for it
                    if (MHR_Jump.Reach(c, dir, dy) < d) continue;
                    // the higher the better (closer to where it's going), then the one more towards the goal
                    float score = hit.point.y - Vector3.Angle(dir, fwd) * 0.004f;
                    if (score > bestY) { bestY = score; bestDir = dir; bestPt = hit.point; bestD = d; }
                    break;   // nearest footing this way
                }
            }
            if (bestDir == Vector3.zero) return false;
            s.HopUpTries++;
            MHR_Jump.Leap(ai, c, bestDir);
            s.StepDir = bestDir; s.StepRun = true; s.StepUntil = now + 0.6f;
            if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + need.ToString("F1") + "m below where it needs to be - jumping up onto " + Fmt(bestPt) + " (" + (bestPt.y - pos.y).ToString("F1") + "m up, " + bestD.ToString("F1") + "m on)");
            return true;
        }
        // 1.5.52: a short step in a fight (back, aside, out of a group) that's safe all the way: no wall, ground under every
        // half metre no more than 1 m below or 0.8 m above, no water to wade into (no deeper than where it stands), no fire
        public static bool SafeStep(Vector3 pos, Vector3 dir, float dist)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f || dist <= 0f) return false;
            dir.Normalize();
            if (Physics.Raycast(pos + Vector3.up * 0.5f, dir, dist + 0.3f, Mask())) return false;
            // 1.5.73: already standing in shallow water, a step through water as deep is fine - only deeper (to swimming
            // depth) is refused (log 93298113: stood its ground in a shallow pool, every step "into water")
            float wade = Mathf.Min(1.0f, Mathf.Max(0.4f, MHR_Water.DepthAt(pos) + 0.15f));
            for (float x = 0.5f; x < dist + 0.49f; x += 0.5f)
            {
                Vector3 p = pos + dir * Mathf.Min(x, dist);
                RaycastHit hit;
                if (!Physics.SphereCast(p + Vector3.up * 1.5f, 0.2f, Vector3.down, out hit, 2.7f, Mask())) return false;
                if (hit.point.y < pos.y - 1.0f || hit.point.y > pos.y + 0.8f) return false;
                if (MHR_Water.DepthAt(hit.point) > wade) return false;
                if (FireArea(hit.point, 0.5f) != null) return false;
                if (SpikeNear(hit.point, 0.7f) != null) return false;
                if (Lava(hit.point)) return false;
            }
            return true;
        }

        static bool Clear(Vector3 pos, Vector3 dir, float dist)
        {
            Vector3 from = pos + Vector3.up * 0.5f;
            if (Physics.Raycast(from, dir, dist, Mask())) return false;
            // something to stand on and no drop into deep water
            Vector3 p = pos + dir * dist;
            RaycastHit hit;
            if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 4f, Mask())) return false;
            if (hit.point.y < pos.y - 1.2f) return false;   // 1.4.82: not over a ledge
            if (SpikeNear(hit.point, 0.7f) != null || FireArea(hit.point, 0.4f) != null || Lava(hit.point)) return false;   // 1.5.72, 1.5.74 lava
            return MHR_Water.DepthAt(hit.point) < 1.0f;
        }

        // 1.4.75: a reachable spot 1.5-2.5 m away that a route to the goal starts from (full routes first, then the shortest).
        // Further than the 1 m that counts as reaching a detour, so the companion actually steps there.
        static bool FindRouteStart(Vector3 pos, Vector3 goal, out Vector3 start)
        {
            start = Vector3.zero;
            float best = float.MaxValue; bool bestFull = false;
            for (int ring = 1; ring <= 2; ring++)
            {
                float r = ring == 1 ? 1.5f : 2.5f;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 dir = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                    if (!Clear(pos, dir, r)) continue;
                    RaycastHit hit;
                    Vector3 p = pos + dir * r;
                    if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 4f, Mask())) continue;
                    p = hit.point;
                    s_tmp.Clear();
                    if (!GetPath(p, goal, s_tmp, MHR_CompanionNavMesh.AgentOrHumanoid, false)) continue;
                    Vector3 end = s_tmp[s_tmp.Count - 1];
                    bool full = Flat(end, goal) <= 1.5f && Mathf.Abs(end.y - goal.y) <= 2.2f;
                    float len = r; Vector3 prev = p;
                    for (int i = 0; i < s_tmp.Count; i++) { len += Vector3.Distance(prev, s_tmp[i]); prev = s_tmp[i]; }
                    if (!full) len += Vector3.Distance(prev, goal);
                    if ((full && !bestFull) || (full == bestFull && len < best)) { best = len; bestFull = full; start = p; }
                }
                if (bestFull) break;   // a full route from the inner ring: no need to look further out
            }
            return best < float.MaxValue;
        }

        static bool FindDetour(Vector3 pos, Vector3 goal, Vector3 dir, out Vector3 det)
        {
            det = Vector3.zero;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            for (int i = 0; i < 4; i++)
            {
                float off = (i < 2 ? 4f : 7f) * (i % 2 == 0 ? 1f : -1f);
                Vector3 p = pos + side * off - dir * 1.5f;
                s_tmp.Clear();
                if (!GetPath(pos, p, s_tmp, MHR_CompanionNavMesh.AgentOrHumanoid, true)) continue;
                det = s_tmp[s_tmp.Count - 1];
                if (Flat(det, pos) < 2f) continue;
                return true;
            }
            return false;
        }

        // ---------- water ----------
        static bool TryWaterJump(CompanionAI ai, Character c, S s, Vector3 pos, Vector3 dir, float now, string tag)
        {
            float lim = MHR_Water.SwimLimit(c);
            // 1.4.96: shallow water too (user: a player jumps a small river or pond rather than run through it)
            float wade = ModConfig.JumpWaterDepth != null ? ModConfig.JumpWaterDepth.Value : 0.3f;
            float maxGap = ModConfig.JumpWaterWidth != null ? ModConfig.JumpWaterWidth.Value : 3f;
            // 1.5.70: never inside a dungeon - there it took the gap beside a ledge in a Mistlands mine for "shallow water" and
            // jumped (log 6f49ec1d: "jumping the shallow water" at the top, then 10 m down on the level below, fighting a Seeker)
            if (c.InInterior()) return false;
            float waterAt = -1f, landAt = -1f; bool swim = false;
            for (float d = 0.5f; d <= 6.0f; d += 0.5f)
            {
                Vector3 p = pos + dir * d;
                float depth = MHR_Water.DepthAt(p);
                // 1.5.70: ground (or the water's surface) well below our feet is a drop, not a stream - no jumping over it
                if (MHR_Water.GroundAt(p) + depth < pos.y - 1.5f) return false;
                if (waterAt < 0f) { if (depth > wade) waterAt = d; else if (d > 1.5f) return false; }
                else if (depth < Mathf.Min(wade, 0.3f)) { landAt = d; break; }
                if (waterAt >= 0f && depth > lim) swim = true;
            }
            if (waterAt < 0f) return false;
            // 1.5.2: no hopping straight back over water it just jumped (user: following him to a portal it jumped a stream
            // and right back - the moving player changed the route). Wade it or go round instead.
            if (now - s.LastWaterJumpAt < 6f && Vector3.Dot(dir, s.LastWaterJumpDir) < -0.3f) { s.NextWaterPeek = now + 1.0f; return false; }
            float landY = landAt > 0f ? MHR_Water.GroundAt(pos + dir * landAt) : pos.y;
            // 1.5.46: only onto real dry land - not a tuft with more water right behind it (1.5.46 test: "The NPC is jumping
            // a lot in and out of the water in the swamp rather than wading through the water. They should be jumping out
            // of the water when it gets them to dry land.")
            bool dryBeyond = landAt > 0f && MHR_Water.DepthAt(pos + dir * landAt) < 0.05f && MHR_Water.DepthAt(pos + dir * (landAt + 1f)) < 0.05f && MHR_Water.DepthAt(pos + dir * (landAt + 2f)) < 0.05f;
            if (!s.Quiet && landAt > 0f && dryBeyond && landAt - waterAt <= maxGap && landY < pos.y + 1.0f && MHR_Jump.CanAfford(c) && landAt <= MHR_Jump.Reach(c, dir, landY - pos.y) && !LandingHazard(pos, dir))
            {
                // a stream or puddle we can clear: jump it and stay dry
                Leap(ai, c, dir, 6.5f);
                s.NextJump = now + 0.8f; s.StepDir = dir; s.StepRun = true; s.StepUntil = now + 0.6f;
                s.LastWaterJumpDir = dir; s.LastWaterJumpAt = now;
                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] jumping the " + (swim ? "stream" : "shallow water") + " at " + Fmt(pos + dir * waterAt));
                return true;
            }
            // 1.4.99: close enough is good enough (user: "if it can make it most of the way across the water with a jump
            // into shallow water on the other side its worth the jump. They can always jump again"). Landing somewhere it
            // can stand counts, as long as it keeps some stamina back for the rest of the way.
            // 1.5.46: only to save a swim - shallow water it wades (jumping into it just splashes it back in again)
            if (!s.Quiet && swim && waterAt <= 1.5f && MHR_Jump.HasSpare(c, 0.25f))
            {
                float reach = MHR_Jump.Reach(c, dir, 0f);
                if (reach >= 2f)
                {
                    Vector3 l = pos + dir * Mathf.Min(reach, 6f);
                    float ly = MHR_Water.GroundAt(l);
                    if (!MHR_Water.WouldSwim(c, l) && ly < pos.y + 0.5f && ly > pos.y - (ModConfig.MaxDrop != null ? ModConfig.MaxDrop.Value : 3f) && !LandingHazard(pos, dir))
                    {
                        Leap(ai, c, dir, 6.5f);
                        s.NextJump = now + 0.8f; s.StepDir = dir; s.StepRun = true; s.StepUntil = now + 0.6f;
                        s.LastWaterJumpDir = dir; s.LastWaterJumpAt = now;
                        if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] jumping as far across the water as it can (lands at " + Fmt(l) + ")");
                        return true;
                    }
                }
            }
            if (!swim) { s.NextWaterPeek = now + 1.0f; return false; }   // too wide to jump but shallow: wade through
            if (s.TooTired && waterAt <= 1.5f)
            {
                s.WaitStamina = true; s.WaitStart = now; Halt(ai, c, s);
                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] too tired to swim across - waiting on the bank");
                return true;
            }
            // 1.5.75: never for loot - drops it would have to swim for are left (MHR_Loot)
            if (landAt < 0f && swim && now >= s.NextDive && waterAt <= 1.0f && !s.WaitStamina && s.RouteSwim > 3f && tag != "Loot")
            {
                // wide water we're about to swim: running leap in for a head start. 1.4.111: only a real swim across
                // (swim-depth water right ahead, no land within 6 m, 3 m+ of swimming on the route) - not to reach
                // something just off the bank or round a fence end (1.4.108 test)
                Leap(ai, c, dir, 5f);
                s.NextJump = now + 1.5f; s.NextDive = now + 20f; s.StepDir = dir; s.StepRun = true; s.StepUntil = now + 0.6f;
                if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] diving in at " + Fmt(pos + dir * waterAt));
                return true;
            }
            return false;
        }

        // 1.4.98: its own run speed, Jump skill and stamina (MHR_Jump)
        static void Leap(CompanionAI ai, Character c, Vector3 dir, float speed) { MHR_Jump.Leap(ai, c, dir); }

        static bool FindShore(Character c, Vector3 pos, Vector3 goal, out Vector3 best)
        {
            best = Vector3.zero;
            float bestScore = float.MaxValue; bool found = false;
            float lim = MHR_Water.SwimLimit(c);
            for (int r = 0; r < 4; r++)
                for (int k = 0; k < 16; k++)
                {
                    float rad = r == 0 ? 3f : r == 1 ? 5f : r == 2 ? 8f : 12f;
                    float ang = k * 22.5f * Mathf.Deg2Rad;
                    Vector3 p = pos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rad;
                    float w = MHR_Water.WaterLevel(p);
                    if (w < -1000f) w = pos.y;
                    float g = MHR_Water.GroundAt(new Vector3(p.x, w + 0.5f, p.z));
                    if (w - g > lim - 0.3f) continue;     // still deep there
                    if (g - w > 1.0f) continue;           // a wall, not a bank you can climb out on
                    float score = rad + Vector3.Distance(new Vector3(p.x, g, p.z), goal) * 0.6f;
                    if (score < bestScore) { bestScore = score; best = new Vector3(p.x, g, p.z); found = true; }
                }
            return found;
        }

        static float MaxStamina(Component c)
        {
            CompanionStamina st = c.GetComponent<CompanionStamina>();
            return st != null ? st.MaxStamina : 0f;
        }
        static float Stamina(Component c)
        {
            CompanionStamina st = c.GetComponent<CompanionStamina>();
            return st != null && st.MaxStamina > 0f ? st.Stamina : 999f;
        }

        // ---------- doors ----------
        static bool DoorClosed(Door d)
        {
            if (d == null) return false;
            ZNetView nv = d.GetComponent<ZNetView>();
            return nv != null && nv.IsValid() && nv.GetZDO().GetInt("state", 0) == 0;
        }
        static bool Usable(Door d)
        {
            try { return d != null && DoorHandler.IsValidClosedDoor(d); } catch { return false; }
        }

        static Door DoorNear(Vector3 pos, float range)
        {
            int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up, range, s_doorCols, LayerMask.GetMask("piece", "piece_nonsolid", "Default"));
            Door best = null; float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Door d = s_doorCols[i] != null ? s_doorCols[i].GetComponentInParent<Door>() : null;
                if (!Usable(d)) continue;
                float dd = MHR_Nav.DoorDist(pos, d.transform.position);
                if (dd < bd) { bd = dd; best = d; }
            }
            return bd <= range + 0.5f ? best : null;
        }

        // closed door between us and the next corner, within reach
        static Door DoorAhead(Vector3 pos, Vector3 target)
        {
            Vector3 from = pos + Vector3.up * 1.0f;
            Vector3 to = new Vector3(target.x, Mathf.Max(target.y, pos.y) + 1.0f, target.z);
            Vector3 d = to - from; float len = Mathf.Min(d.magnitude, 2.5f);
            if (len < 0.2f) return null;
            RaycastHit hit;
            if (!Physics.Raycast(from, d.normalized, out hit, len, LayerMask.GetMask("piece", "Default"))) return null;
            Door door = hit.collider != null ? hit.collider.GetComponentInParent<Door>() : null;
            return Usable(door) ? door : null;
        }

        // ---------- doorways ----------
        // 1.4.140: the route map ignores doors, so a route can cut across a doorway at an angle and clip the leaf of an open
        // door (user: "path around the open door properly"). When the route ahead goes through a doorway, first get onto the
        // line straight out from the middle of the doorway, then walk through along it - the open leaf hangs off to one side
        // of that line. door.transform is the middle of the doorway; its forward axis points through it.
        static Vector3 DoorwayLine(S s, Vector3 pos, Vector3 target, float now, string tag)
        {
            if (s.PassDoor == null && now >= s.NextDoorwayCheck)
            {
                s.NextDoorwayCheck = now + 0.4f;
                float side;
                Door d = DoorwayOnRoute(s, pos, out side);
                if (d != null) { s.PassDoor = d; s.PassSide = side; if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] lining up with the " + d.m_name + " at " + Fmt(d.transform.position)); }
            }
            Door door = s.PassDoor;
            if (door == null) return target;
            if (!door || door.GetComponent<ZNetView>() == null || !door.GetComponent<ZNetView>().IsValid()) { s.PassDoor = null; return target; }
            Vector3 ctr = door.transform.position;
            Vector3 f = door.transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) { s.PassDoor = null; return target; }
            f.Normalize();
            Vector3 rel = pos - ctr; rel.y = 0f;
            float along = Vector3.Dot(rel, f) * s.PassSide;           // > 0: still on our side
            float across = Vector3.Dot(rel, new Vector3(f.z, 0f, -f.x));
            if (along < -0.9f || rel.magnitude > 5f || Mathf.Abs(pos.y - ctr.y) > 2.5f)
            {
                // through (or gone elsewhere): a fresh route from here, so it doesn't turn back for a corner it skipped
                s.PassDoor = null; s.NextDoorwayCheck = now + 1.0f; s.NextPlan = 0f;
                return target;
            }
            Vector3 outSide = f * s.PassSide;
            if (along > 0.35f && Mathf.Abs(across) > 0.25f)
            {
                // not lined up yet: go to the point on the centre line in front of the doorway
                Vector3 p = ctr + outSide * Mathf.Clamp(along, 0.8f, 1.4f);
                return new Vector3(p.x, pos.y, p.z);
            }
            // lined up (or in the doorway): straight through to the other side
            Vector3 q = ctr - outSide * 1.4f;
            return new Vector3(q.x, pos.y, q.z);
        }

        // a door whose doorway the route crosses within the next few meters; side = which side of it we're on
        static Door DoorwayOnRoute(S s, Vector3 pos, out float side)
        {
            side = 0f;
            if (s.Path.Count == 0) return null;
            int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up, 5f, s_doorCols, LayerMask.GetMask("piece", "piece_nonsolid", "Default"));
            Door best = null; float bestT = float.MaxValue;
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                Door d = s_doorCols[i] != null ? s_doorCols[i].GetComponentInParent<Door>() : null;
                if (d == null || !seen.Add(d.GetInstanceID())) continue;
                Vector3 ctr = d.transform.position;
                if (Mathf.Abs(ctr.y - pos.y) > 2.5f) continue;
                Vector3 f = d.transform.forward; f.y = 0f;
                if (f.sqrMagnitude < 0.01f) continue;
                f.Normalize();
                // walk the route from here through the next few corners (up to ~6 m) and look for the crossing
                Vector3 a = pos; float walked = 0f;
                for (int k = Mathf.Clamp(s.Corner, 0, s.Path.Count - 1); k < s.Path.Count && walked < 6f; k++)
                {
                    Vector3 b = s.Path[k];
                    float da = Vector3.Dot(new Vector3(a.x - ctr.x, 0f, a.z - ctr.z), f);
                    float db = Vector3.Dot(new Vector3(b.x - ctr.x, 0f, b.z - ctr.z), f);
                    if (da * db < 0f || Mathf.Abs(db) < 0.05f)
                    {
                        float t = Mathf.Abs(da) / Mathf.Max(0.001f, Mathf.Abs(da) + Mathf.Abs(db));
                        Vector3 x = Vector3.Lerp(a, b, t);
                        Vector3 lat = x - ctr; lat.y = 0f;
                        if (lat.magnitude <= 1.3f && Mathf.Abs(da) > 0.5f)
                        {
                            float at = walked + Vector3.Distance(a, x);
                            if (at < bestT) { bestT = at; best = d; side = Mathf.Sign(da); }
                        }
                        break;
                    }
                    walked += Vector3.Distance(a, b);
                    a = b;
                }
            }
            return best;
        }

        // 1.4.136: an open door's leaf right in front of us on the way to the next corner
        static readonly Dictionary<int, float> s_leafClosed = new Dictionary<int, float>();
        static Door OpenLeafAhead(Vector3 pos, Vector3 target)
        {
            Vector3 flat = target - pos; flat.y = 0f;
            float len = Mathf.Min(flat.magnitude, 1.6f);
            if (len < 0.2f) return null;
            Vector3 dir = flat.normalized;
            float[] heights = { 0.5f, 1.0f, 1.5f };
            for (int i = 0; i < heights.Length; i++)
            {
                RaycastHit hit;
                if (!Physics.Raycast(pos + Vector3.up * heights[i], dir, out hit, len, LayerMask.GetMask("piece", "Default", "piece_nonsolid"))) continue;
                Door d = hit.collider != null ? hit.collider.GetComponentInParent<Door>() : null;
                if (d == null || DoorClosed(d) || d.m_keyItem != null) continue;
                if (d.m_checkGuardStone && !PrivateArea.CheckAccess(d.transform.position, 0f, false)) continue;
                float until;
                if (s_leafClosed.TryGetValue(d.GetInstanceID(), out until) && Time.time < until) continue;   // just did this one
                return d;
            }
            return null;
        }

        // an open door (any part of it) within range, not one we closed in the last few seconds
        static Door OpenDoorNear(Vector3 pos, float range)
        {
            int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up, range, s_doorCols, LayerMask.GetMask("piece", "piece_nonsolid", "Default"));
            Door best = null; float bd = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider col = s_doorCols[i];
                Door d = col != null ? col.GetComponentInParent<Door>() : null;
                if (d == null || DoorClosed(d) || d.m_keyItem != null) continue;
                if (d.m_checkGuardStone && !PrivateArea.CheckAccess(d.transform.position, 0f, false)) continue;
                float until;
                if (s_leafClosed.TryGetValue(d.GetInstanceID(), out until) && Time.time < until) continue;
                float dd = Vector3.Distance(pos + Vector3.up, col.ClosestPoint(pos + Vector3.up));
                if (dd < bd) { bd = dd; best = d; }
            }
            return best;
        }

        static void CloseDoor(CompanionAI ai, Character c, Door d, string tag)
        {
            if (d == null || DoorClosed(d)) return;
            s_leafClosed[d.GetInstanceID()] = Time.time + 8f;
            if (s_leafClosed.Count > 200) s_leafClosed.Clear();
            Vector3 to = d.transform.position - ai.transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) ai.transform.rotation = Quaternion.LookRotation(to.normalized);
            Humanoid h = c as Humanoid;
            bool ok = false;
            try { ok = h != null && d.Interact(h, false, false); } catch { ok = false; }
            if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] " + (ok ? "closed" : "couldn't close") + " the " + d.m_name + " at " + Fmt(d.transform.position) + " - it was open towards us and in the way");
        }

        // the closed door that opens the shortest way to the goal, and where to stand to open it
        // 1.4.84: is there a closed door that, once opened, gets us from pos to goal?
        public static bool ReachableViaDoor(Vector3 pos, Vector3 goal)
        {
            return CanGetTo(pos, goal);
        }

        // 1.4.84: through up to three closed doors in a row (closed doors block routes): a breadth-first search over
        // door sides - from here to the near side of a door, from its far side to the near side of the next, and from
        // the last far side a route has to actually reach the goal (1.4.79: otherwise the door isn't the answer and he
        // goes round in circles). Returns the first door of the shortest chain; once through it, the next plan finds
        // the next one. Capped at 120 route queries.
        class DoorNode { public int Door, Side, Depth, First; public Vector3 FirstSpot; public float Len; }
        static readonly List<Door> s_doors = new List<Door>();
        static Door RouteDoor(Vector3 pos, Vector3 goal, out Vector3 spot, Door skip)
        {
            spot = Vector3.zero;
            s_doors.Clear();
            s_pieces.Clear();
            Piece.GetAllPiecesInRadius(goal, 35f, s_pieces);
            if (Flat(pos, goal) > 20f) Piece.GetAllPiecesInRadius(pos, 20f, s_pieces);
            for (int i = 0; i < s_pieces.Count; i++)
            {
                Piece p = s_pieces[i];
                Door d = p != null ? p.GetComponentInChildren<Door>() : null;
                if (!Usable(d) || d == skip || s_doors.Contains(d)) continue;
                s_doors.Add(d);
            }
            if (s_doors.Count == 0) return null;
            Vector3 mid = (pos + goal) * 0.5f;
            s_doors.Sort((a, b) => Vector3.Distance(a.transform.position, mid).CompareTo(Vector3.Distance(b.transform.position, mid)));
            if (s_doors.Count > 8) s_doors.RemoveRange(8, s_doors.Count - 8);

            int budget = 120;
            bool[] seen = new bool[s_doors.Count * 2];
            Queue<DoorNode> queue = new Queue<DoorNode>();
            for (int i = 0; i < s_doors.Count; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 end; float len;
                    if (budget-- <= 0) break;
                    if (!ToDoor(pos, s_doors[i], side, out end, out len)) continue;
                    seen[i * 2 + (side > 0 ? 1 : 0)] = true;
                    queue.Enqueue(new DoorNode { Door = i, Side = side, Depth = 1, First = i, FirstSpot = end, Len = len });
                }
            Door best = null; float bestScore = float.MaxValue; int bestDepth = 99;
            while (queue.Count > 0 && budget > 0)
            {
                DoorNode n = queue.Dequeue();
                if (n.Depth > bestDepth) break;
                Door d = s_doors[n.Door];
                Vector3 other = d.transform.position - d.transform.forward * (1.4f * n.Side);
                s_tmp2.Clear(); budget--;
                if (GetPath(other, goal, s_tmp2, Pathfinding.AgentType.Humanoid, false))
                {
                    Vector3 far = s_tmp2[s_tmp2.Count - 1];
                    if (Flat(far, goal) <= 2.0f && Mathf.Abs(far.y - goal.y) <= 2.2f)
                    {
                        float score = n.Len + Vector3.Distance(d.transform.position, goal);
                        if (score < bestScore) { bestScore = score; best = s_doors[n.First]; spot = n.FirstSpot; bestDepth = n.Depth; }
                        continue;
                    }
                }
                if (n.Depth >= 3) continue;
                for (int j = 0; j < s_doors.Count && budget > 0; j++)
                {
                    if (j == n.Door) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int key = j * 2 + (side > 0 ? 1 : 0);
                        if (seen[key] || budget-- <= 0) continue;
                        Vector3 end; float len;
                        if (!ToDoor(other, s_doors[j], side, out end, out len)) continue;
                        seen[key] = true;
                        queue.Enqueue(new DoorNode { Door = j, Side = side, Depth = n.Depth + 1, First = n.First, FirstSpot = n.FirstSpot, Len = n.Len + len });
                    }
                }
            }
            return best;
        }

        // a full route from 'from' to one side of the door, ending by the door
        static bool ToDoor(Vector3 from, Door d, int side, out Vector3 end, out float len)
        {
            end = Vector3.zero; len = 0f;
            Vector3 dp = d.transform.position;
            Vector3 sp = dp + d.transform.forward * (1.4f * side);
            s_tmp.Clear();
            if (!GetPath(from, sp, s_tmp, Pathfinding.AgentType.Humanoid, true)) return false;
            end = s_tmp[s_tmp.Count - 1];
            if (MHR_Nav.DoorDist(end, dp) > 2.2f) return false;
            Vector3 prev = from;
            for (int k = 0; k < s_tmp.Count; k++) { len += Vector3.Distance(prev, s_tmp[k]); prev = s_tmp[k]; }
            return true;
        }

        // 1.4.84: no route at all, but a spot a route does reach from which the rest can be hopped (the bottom of stairs
        // the routes don't use - 1.4.83 test: the farm's chests are up the stairs in the house). Rings round the goal.
        public static bool FindHopBridge(Vector3 pos, Vector3 goal, out Vector3 bridge)
        {
            bridge = Vector3.zero;
            float best = float.MaxValue; int queries = 0;
            float[] radii = { 3f, 6f, 10f, 15f, 20f };
            for (int r = 0; r < radii.Length; r++)
            {
                for (int k = 0; k < 12; k++)
                {
                    Vector3 dir = Quaternion.Euler(0f, k * 30f, 0f) * Vector3.forward;
                    Vector3 p = goal + dir * radii[r];
                    RaycastHit hit;
                    if (!Physics.Raycast(new Vector3(p.x, goal.y + 1.5f, p.z), Vector3.down, out hit, 12f, Mask())) continue;
                    if (MHR_Water.DepthAt(hit.point) > 0.8f) continue;
                    Vector3 b = hit.point;
                    int hops;
                    if (!HopLine(b, goal, out hops)) continue;
                    if (queries++ >= 24) break;
                    s_tmp.Clear();
                    if (!GetPath(pos, b, s_tmp, MHR_CompanionNavMesh.AgentOrHumanoid, true)) continue;
                    Vector3 end = s_tmp[s_tmp.Count - 1];
                    if (Flat(end, b) > 1.0f || Mathf.Abs(end.y - b.y) > 1.2f) continue;
                    float len = 0f; Vector3 prev = pos;
                    for (int i = 0; i < s_tmp.Count; i++) { len += Vector3.Distance(prev, s_tmp[i]); prev = s_tmp[i]; }
                    len += radii[r];
                    if (len < best) { best = len; bridge = b; }
                }
                if (best < float.MaxValue) break;   // nearest ring that works
            }
            return best < float.MaxValue;
        }

        // 1.5.42: can he walk from pos to within near of goal: a full route that really ends there (the route's end is
        // snapped to the nearest walkable ground, which can be on the other side of a wall), or one CanGetTo finds
        static readonly List<Vector3> s_reachPath = new List<Vector3>();
        public static bool Reachable(Vector3 pos, Vector3 goal, float near)
        {
            try
            {
                if (GetPath(pos, goal, s_reachPath, Pathfinding.AgentType.Humanoid, true))
                {
                    Vector3 end = s_reachPath[s_reachPath.Count - 1];
                    if (Vector3.Distance(end, goal) <= near) return true;
                }
                return CanGetTo(pos, goal);
            }
            catch { return false; }
        }

        // 1.4.84: can he get from pos to goal: a route, a route plus a hop, through closed doors, or straight hopping
        public static bool CanGetTo(Vector3 pos, Vector3 goal)
        {
            try
            {
                Vector3 v; int hops;
                if (Flat(pos, goal) < 25f && HopLine(pos, goal, out hops)) return true;
                if (FindHopBridge(pos, goal, out v)) return true;
                return RouteDoor(pos, goal, out v, null) != null;
            }
            catch { return false; }
        }

        static bool YouWentThrough(CompanionAI ai, Door d)
        {
            Player p = Player.m_localPlayer;
            if (p == null || d == null || Vector3.Distance(p.transform.position, d.transform.position) > 25f) return false;
            Vector3 n = d.transform.forward; n.y = 0f;
            if (n.sqrMagnitude < 0.01f) return false;
            float me = Vector3.Dot(ai.transform.position - d.transform.position, n);
            float you = Vector3.Dot(p.transform.position - d.transform.position, n);
            return Mathf.Abs(you) > 0.5f && Mathf.Sign(me) != Mathf.Sign(you);
        }

        static void OpenDoor(CompanionAI ai, Character c, Door d, string tag)
        {
            if (d == null || !DoorClosed(d)) return;
            // 1.5.73: a fight may open a door you've gone through (you're on the far side, close by) - that's following you in
            if (tag == CombatTag && !YouWentThrough(ai, d))
            {
                S st = Get(ai);
                if (!st.DoorBlocked) if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] closed " + d.m_name + " in the way at " + Fmt(d.transform.position) + " - not opening it in a fight");
                st.DoorBlocked = true; st.GaveUp = true; st.GaveUpAt = Time.time;
                return;
            }
            Vector3 to = d.transform.position - ai.transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) ai.transform.rotation = Quaternion.LookRotation(to.normalized);
            Humanoid h = c as Humanoid;
            bool ok = false;
            try { ok = h != null && d.Interact(h, false, false); } catch { ok = false; }
            if (!ok) { if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] couldn't open " + d.m_name); return; }
            int id = ai.gameObject.GetInstanceID();
            List<Opened> list;
            if (!s_opened.TryGetValue(id, out list)) { list = new List<Opened>(); s_opened[id] = list; }
            list.Add(new Opened { Door = d, At = Time.time });
            if (CompanionsPlugin.LogDetailed) Dbg("[Move|" + tag + "] opened " + d.m_name + " at " + Fmt(d.transform.position));
        }

        // ---------- per-frame housekeeping (called from MHR_ToolKeeper.Tick for every companion we own) ----------
        public static void Tick(Component cs, CompanionAI ai)
        {
            if (cs == null || ai == null) return;
            try
            {
                S s = Peek(ai);
                if (s != null && s.Walking && Time.time - s.LastGo > 0.5f)
                {
                    Character c = ai.GetComponent<Character>();
                    if (c != null) c.SetWalk(false);
                    s.Walking = false;
                }
                List<Opened> list;
                if (!s_opened.TryGetValue(ai.gameObject.GetInstanceID(), out list) || list.Count == 0) return;
                Vector3 pos = ai.transform.position;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Opened o = list[i];
                    if (o.Door == null || DoorClosed(o.Door) || Time.time - o.At > 60f) { list.RemoveAt(i); continue; }
                    if (Time.time - o.At < 1.5f) continue;
                    Vector3 dp = o.Door.transform.position;
                    if (Flat(pos, dp) < 3.5f && Mathf.Abs(pos.y - dp.y) < 3f) continue;
                    // 1.4.75: still stuck near it (working through the stuck ladder or a detour) - leave it open
                    if (s != null && Time.time - s.LastGo < 0.5f && (s.Ladder > 0 || s.HasDetour) && Flat(pos, dp) < 8f) continue;
                    if (Player.m_localPlayer != null && Vector3.Distance(Player.m_localPlayer.transform.position, dp) < 2.5f) continue;
                    if (AnyCharacterIn(dp, 1.2f)) continue;
                    Humanoid h = ai.GetComponent<Humanoid>();
                    try { if (h != null) o.Door.Interact(h, false, false); } catch { }
                    if (CompanionsPlugin.LogDetailed) Dbg("[Move] closed " + o.Door.m_name + " behind us");
                    list.RemoveAt(i);
                }
            }
            catch (Exception e)
            {
                if (CompanionsPlugin.Log != null && Time.time >= s_nextErr) { s_nextErr = Time.time + 30f; CompanionsPlugin.Log.LogWarning("[Move] " + e.Message); }
            }
        }
        static bool AnyCharacterIn(Vector3 p, float r)
        {
            int n = Physics.OverlapSphereNonAlloc(p, r, s_cols, LayerMask.GetMask("character", "character_net", "character_ghost"));
            for (int i = 0; i < n; i++) if (s_cols[i] != null && s_cols[i].GetComponentInParent<Character>() != null) return true;
            return false;
        }

        // ---------- follow: optional catch-up when badly stuck and out of sight ----------
        class F { public Vector3 Anchor; public float AnchorTime; }
        static readonly Dictionary<int, F> s_follow = new Dictionary<int, F>();
        public static void FollowWatch(CompanionAI ai, GameObject target)
        {
            try
            {
                if (ai == null || target == null || ModConfig.FollowStuckTeleport == null || !ModConfig.FollowStuckTeleport.Value) return;
                float now = Time.time;
                int id = ai.gameObject.GetInstanceID();
                F f;
                if (!s_follow.TryGetValue(id, out f)) { f = new F { Anchor = ai.transform.position, AnchorTime = now }; s_follow[id] = f; }
                Vector3 pos = ai.transform.position, tp = target.transform.position;
                if (Vector3.Distance(pos, tp) < 10f || Vector3.Distance(pos, f.Anchor) > 1.5f || ai.IsInCombat) { f.Anchor = pos; f.AnchorTime = now; return; }
                if (now - f.AnchorTime < 15f) return;
                f.Anchor = pos; f.AnchorTime = now;
                if (!Physics.Linecast(pos + Vector3.up * 1.6f, tp + Vector3.up * 1.6f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain"))) return;   // you can see each other
                if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Move|Follow] stuck for 15s out of sight at " + Fmt(pos) + " - catching up");
                ai.TeleportToFollowTarget();
            }
            catch { }
        }

        // 1.4.83: eye to eye, nothing solid in between
        public static bool CanSee(Vector3 a, Vector3 b)
        {
            return !Physics.Linecast(a + Vector3.up * 1.6f, b + Vector3.up * 1.6f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain"));
        }

        // ---------- log ----------
        static void Log(S s, Vector3 pos, float remain, float now, Character c)
        {
            if (now < s.NextLog || CompanionsPlugin.Log == null) return;
            s.NextLog = now + 3f;
            float still = now - s.AnchorTime, noCloser = now - s.ProgressTime;
            string route = !s.PathOk ? "none" : (s.Partial ? "partial" : "full") + " (" + s.Path.Count + " corners)";
            if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Move|" + s.Tag + "] at " + Fmt(pos) + " -> " + Fmt(s.Goal) + " " + remain.ToString("F1") + "m left, route=" + route +
                (s.RouteDoor != null ? ", via door" : "") + (s.HasDetour ? ", detour" : "") + (c.IsSwimming() ? ", swimming" : "") +
                (noCloser > 3f ? ", no closer for " + noCloser.ToString("F0") + "s" : "") + (still > 2f ? ", standing still " + still.ToString("F0") + "s" : "") +
                (s.Ladder > 0 ? ", unstuck step " + s.Ladder : ""));
        }
    }

    // ---- 1.4.74: water depth measured against the companion, not "is there water" ----
    public static class MHR_Water
    {
        static WaterVolume s_wv;
        static int s_mask = -1;
        static int Mask()
        {
            if (s_mask == -1) s_mask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain", "vehicle");
            return s_mask;
        }
        public static float WaterLevel(Vector3 p)
        {
            try { return Floating.GetWaterLevel(p, ref s_wv); } catch { return -10000f; }
        }
        public static float GroundAt(Vector3 p)
        {
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 60f, Mask())) return hit.point.y;
            float y;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p, out y)) return y;
            return p.y;
        }
        public static float DepthAt(Vector3 p)
        {
            float g = GroundAt(p);
            float w = WaterLevel(new Vector3(p.x, g + 0.3f, p.z));
            if (w < -1000f) w = WaterLevel(p);
            if (w < -1000f) return 0f;
            return Mathf.Max(0f, w - g);
        }
        // vanilla: a character swims once the water is deeper than swimDepth - 0.4 (and only then gets Wet)
        public static float SwimLimit(Character c) { return Mathf.Max(0.3f, (c != null ? c.m_swimDepth : 2f) - 0.4f); }
        public static bool WouldSwim(Character c, Vector3 p) { return DepthAt(p) > SwimLimit(c); }

        public static float SwimDrainSkill(Character c)
        {
            float skill = 0f;
            try { if (c != null) skill = c.GetSkillFactor(Skills.SkillType.Swim); } catch { }
            return Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(skill));
        }

        public static float SwimStamina(Character c, float meters)
        {
            float speed = c != null && c.m_swimSpeed > 0.1f ? c.m_swimSpeed : 2f;
            // 1.5.88: like the player (Player.OnSwimming: Lerp(5, 2, Swim skill) per second while moving)
            float drain = (ModConfig.StaminaSwimDrain != null ? ModConfig.StaminaSwimDrain.Value : 5f) * SwimDrainSkill(c);
            return meters / speed * drain;
        }

        // meters of swimming along pos -> path corners (sampled every meter, up to maxLen)
        public static float SwimMeters(Character c, Vector3 pos, List<Vector3> path, float maxLen)
        {
            float lim = SwimLimit(c), swim = 0f, walked = 0f;
            Vector3 prev = pos;
            for (int i = 0; i < path.Count && walked < maxLen; i++)
            {
                Vector3 seg = path[i] - prev; float len = new Vector2(seg.x, seg.z).magnitude;
                for (float d = 0.5f; d < len && walked < maxLen; d += 1f, walked += 1f)
                    if (DepthAt(prev + seg * (d / len)) > lim) swim += 1f;
                prev = path[i];
            }
            return swim;
        }
        public static float DistToWater(Character c, Vector3 pos, List<Vector3> path)
        {
            float lim = SwimLimit(c), walked = 0f;
            Vector3 prev = pos;
            for (int i = 0; i < path.Count && walked < 30f; i++)
            {
                Vector3 seg = path[i] - prev; float len = new Vector2(seg.x, seg.z).magnitude;
                for (float d = 0.5f; d < len && walked < 30f; d += 1f, walked += 1f)
                    if (DepthAt(prev + seg * (d / len)) > lim) return walked;
                prev = path[i];
            }
            return 999f;
        }
        public static bool SwimOnLine(Character c, Vector3 a, Vector3 b, float maxLen)
        {
            float lim = SwimLimit(c);
            Vector3 d = b - a; float len = Mathf.Min(new Vector2(d.x, d.z).magnitude, maxLen);
            if (len < 0.5f) return DepthAt(b) > lim;
            Vector3 n = new Vector3(d.x, 0f, d.z).normalized;
            for (float t = 0.5f; t <= len; t += 1.5f)
                if (DepthAt(a + n * t + Vector3.up * (d.y * t / len)) > lim) return true;
            return DepthAt(b) > lim;
        }

        // replaces "below half stamina -> avoid ALL water": only steer around water he'd actually have to swim
        static readonly Dictionary<int, KeyValuePair<float, bool>> s_agent = new Dictionary<int, KeyValuePair<float, bool>>();
        public static void ApplyAgent(CompanionAI ai, bool lowStamina)
        {
            bool avoid = false;
            try
            {
                Character c = ai.GetComponent<Character>();
                GameObject f = ai.GetFollowTarget();
                if (lowStamina && c != null && !c.IsSwimming() && f != null)
                {
                    int id = ai.gameObject.GetInstanceID();
                    KeyValuePair<float, bool> cached;
                    if (s_agent.TryGetValue(id, out cached) && Time.time - cached.Key < 1f) avoid = cached.Value;
                    else
                    {
                        avoid = SwimOnLine(c, ai.transform.position, f.transform.position, 40f);
                        s_agent[id] = new KeyValuePair<float, bool>(Time.time, avoid);
                    }
                }
            }
            catch { avoid = false; }
            ai.m_avoidWater = avoid;
            ai.m_pathAgentType = avoid ? Pathfinding.AgentType.HumanoidAvoidWater : Pathfinding.AgentType.Humanoid;
        }
    }

    // ---- 1.4.74: what to fight ----
    public static class MHR_Threat
    {
        static readonly Dictionary<int, float> s_ignore = new Dictionary<int, float>();
        static float s_nextLog;

        // animals that never attack (vanilla deer and the like, modded fish, dolphins...) and creatures we gave up chasing
        // 1.5.2: recently given up on because reaching it means swimming, and it's still in deep water - even if it's after
        // us (a crocodile watching from the river). Only for starting a fight on its own; an attack is still answered.
        public static bool IgnoredInWater(Character c)
        {
            if (c == null) return false;
            float until;
            return s_ignore.TryGetValue(c.GetInstanceID(), out until) && Time.time < until && MHR_Water.DepthAt(c.transform.position) > 1.0f;
        }

        public static bool IsPassive(Character c)
        {
            if (c == null) return false;
            try
            {
                string n = c.gameObject.name;
                if (n.StartsWith("Deer") || n.StartsWith("Hare") || n.StartsWith("Chicken")) return true;
                float until;
                BaseAI ai = c.GetBaseAI();
                if (s_ignore.TryGetValue(c.GetInstanceID(), out until))
                {
                    bool coming = ai != null && ai.GetTargetCreature() != null;
                    if (Time.time < until && !coming && MHR_Water.DepthAt(c.transform.position) > 1.0f) return true;
                    s_ignore.Remove(c.GetInstanceID());
                }
                if (ai is AnimalAI) return true;
                // 1.4.78: creatures that only fight back (passive-aggressive), sea creatures that keep off land, and ones
                // that flee unless alerted are left alone while they aren't after anyone. Modded crabs and dolphins
                // (RtDOcean) use MonsterAI, so the AnimalAI check above missed them.
                MonsterAI mai = ai as MonsterAI;
                if (ai != null && ai.GetTargetCreature() == null && !ai.IsAlerted() &&
                    (ai.m_passiveAggresive || (mai != null && (mai.m_avoidLand || mai.m_fleeIfNotAlerted)))) return true;
            }
            catch { }
            return false;
        }

        // 1.4.79: is it going after a player or a companion right now?
        public static bool IsAfterUs(Character c)
        {
            if (c == null) return false;
            try
            {
                BaseAI ai = c.GetBaseAI();
                if (ai == null) return true;   // not an AI creature: leave it to the normal rules
                Character t = ai.GetTargetCreature();
                if (t == null) return false;
                // 1.4.154: and actually coming for us (alerted) - a creature that has only noticed you isn't a fight yet
                // (user: "going after creatures that have not engaged us yet"; log: a Neck with target Human, alerted=False).
                // 1.4.155: a choice - [Combat] EngageOnNotice (on = as soon as it has noticed us, the earlier behaviour)
                bool onNotice = ModConfig.EngageOnNotice == null || ModConfig.EngageOnNotice.Value;
                if (!onNotice && !ai.IsAlerted()) return false;
                return t.IsPlayer() || t.GetComponent<CompanionSetup>() != null;
            }
            catch { return true; }
        }

        // don't chase into water he'd have to swim in; if it's attacking, a bow or crossbow with ammo still works
        public static bool AvoidChase(Character self, Character target)
        {
            if (self == null || target == null) return false;
            try
            {
                if (self.IsSwimming()) return false;
                if (!MHR_Water.WouldSwim(self, target.transform.position)) return false;
                // something swimming out there minding its own business: leave it. If it's coming for us, shoot it if we can.
                BaseAI tai = target.GetBaseAI();
                bool hostile = tai != null && (tai.IsAlerted() || tai.GetTargetCreature() != null);
                Humanoid h = self as Humanoid;
                if (hostile && h != null && MHR_Crossbow.HasReadyRanged(h)) return false;
                if (hostile && Vector3.Distance(self.transform.position, target.transform.position) < 3f) return false;
                if (s_ignore.Count > 200) s_ignore.Clear();
                s_ignore[target.GetInstanceID()] = Time.time + 20f;
                CompanionCombatAI cc = self.GetComponent<CompanionCombatAI>();
                if (cc != null) cc.Disengage();
                if (CompanionsPlugin.Log != null && Time.time >= s_nextLog)
                {
                    s_nextLog = Time.time + 5f;
                    if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Combat] " + target.m_name + " is in deep water - leaving it");
                }
                return true;
            }
            catch { return false; }
        }
    }

    // ---- 1.4.74: crossbows. Vanilla only lets the player load one (Humanoid.IsWeaponLoaded is always false), so a
    // companion with a crossbow could never fire. Companions now load it like a player: reload animation, reload time
    // (faster with skill), stamina. Ranged weapons only count as ready with matching ammo in the pack. ----
    public static class MHR_Crossbow
    {
        class S { public ItemDrop.ItemData Loaded, Reloading; public float Until; public string Anim; }
        static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();
        static readonly Dictionary<int, bool> s_isComp = new Dictionary<int, bool>();

        public static bool IsCompanion(Humanoid h)
        {
            if (h == null || h is Player) return false;
            int id = h.GetInstanceID();
            bool v;
            if (!s_isComp.TryGetValue(id, out v)) { v = h.GetComponent<CompanionSetup>() != null; if (s_isComp.Count > 2000) s_isComp.Clear(); s_isComp[id] = v; }
            return v;
        }
        static S Get(Humanoid h)
        {
            S s;
            if (!s_s.TryGetValue(h.GetInstanceID(), out s)) { s = new S(); s_s[h.GetInstanceID()] = s; }
            return s;
        }

        static bool IsAmmo(ItemDrop.ItemData it)
        {
            return it != null && it.m_shared != null && (it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable);
        }
        public static bool NeedsAmmo(ItemDrop.ItemData it)
        {
            return it != null && it.m_shared != null && !IsAmmo(it) && !string.IsNullOrEmpty(it.m_shared.m_ammoType);
        }
        public static bool HasAmmo(Humanoid h, ItemDrop.ItemData weapon)
        {
            if (h == null || weapon == null || h.GetInventory() == null) return false;
            string type = weapon.m_shared.m_ammoType;
            List<ItemDrop.ItemData> items = h.GetInventory().GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                if (IsAmmo(it) && it.m_shared.m_ammoType == type && it.m_stack > 0) return true;
            }
            return false;
        }
        // replaces WeaponLogic.Ready in FindReadyBow / BalancedPrefix
        public static bool ReadyFor(ItemDrop.ItemData it, Humanoid h)
        {
            if (it == null || it.m_shared == null) return false;
            if (it.m_shared.m_useDurability && it.m_durability <= 0f) return false;
            if (NeedsAmmo(it) && h != null && !HasAmmo(h, it)) return false;
            return true;
        }
        public static bool HasReadyRanged(Humanoid h)
        {
            if (h == null || h.GetInventory() == null) return false;
            List<ItemDrop.ItemData> items = h.GetInventory().GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                if (NeedsAmmo(it) && ReadyFor(it, h)) return true;
            }
            return false;
        }

        public static bool IsLoaded(Humanoid h)
        {
            S s;
            if (h == null || !s_s.TryGetValue(h.GetInstanceID(), out s)) return false;
            ItemDrop.ItemData w = h.GetCurrentWeapon();
            return w != null && s.Loaded == w;
        }
        public static void Unload(Humanoid h)
        {
            S s;
            if (h != null && s_s.TryGetValue(h.GetInstanceID(), out s)) s.Loaded = null;
        }

        public static void Tick(Humanoid h)
        {
            if (h == null) return;
            try
            {
                ItemDrop.ItemData w = h.GetCurrentWeapon();
                bool needs = w != null && w.m_shared != null && w.m_shared.m_attack != null && w.m_shared.m_attack.m_requiresReload && h.IsItemEquiped(w);
                S s;
                if (!needs)
                {
                    if (s_s.TryGetValue(h.GetInstanceID(), out s) && s.Reloading != null) Cancel(h, s);
                    return;
                }
                s = Get(h);
                if (s.Loaded == w) return;
                if (s.Reloading != null && s.Reloading != w) Cancel(h, s);
                ZSyncAnimation zanim = h.GetComponent<ZSyncAnimation>();
                if (s.Reloading == w)
                {
                    if (Time.time < s.Until) return;
                    if (zanim != null && !string.IsNullOrEmpty(s.Anim)) { zanim.SetBool(s.Anim, false); zanim.SetTrigger(s.Anim + "_done"); }
                    s.Loaded = w; s.Reloading = null;
                    if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Ranged] " + w.m_shared.m_name + " loaded");
                    return;
                }
                if (h.InAttack() || !HasAmmo(h, w)) return;
                Attack atk = w.m_shared.m_attack;
                CompanionStamina st = h.GetComponent<CompanionStamina>();
                if (st != null && atk.m_reloadStaminaDrain > 0f && !st.UseStamina(atk.m_reloadStaminaDrain)) return;
                float skill = 0f;
                try { skill = h.GetSkillFactor(w.m_shared.m_skillType); } catch { skill = 0f; }
                float time = Mathf.Lerp(atk.m_reloadTime, atk.m_reloadTime * 0.5f, skill);
                s.Reloading = w; s.Until = Time.time + Mathf.Max(0.3f, time); s.Anim = atk.m_reloadAnimation;
                if (zanim != null && !string.IsNullOrEmpty(s.Anim)) zanim.SetBool(s.Anim, true);
            }
            catch { }
        }
        static void Cancel(Humanoid h, S s)
        {
            ZSyncAnimation zanim = h.GetComponent<ZSyncAnimation>();
            if (zanim != null && !string.IsNullOrEmpty(s.Anim)) zanim.SetBool(s.Anim, false);
            s.Reloading = null;
        }
    }

    [HarmonyPatch(typeof(Humanoid), "IsWeaponLoaded")]
    public static class MHR_WeaponLoaded_Patch
    {
        static void Postfix(Humanoid __instance, ref bool __result)
        {
            if (!__result && MHR_Crossbow.IsCompanion(__instance)) __result = MHR_Crossbow.IsLoaded(__instance);
        }
    }

    [HarmonyPatch(typeof(Humanoid), "ResetLoadedWeapon")]
    public static class MHR_ResetLoaded_Patch
    {
        static void Postfix(Humanoid __instance)
        {
            if (MHR_Crossbow.IsCompanion(__instance)) MHR_Crossbow.Unload(__instance);
        }
    }
}
