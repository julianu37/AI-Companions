using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.61: companions draw a bow like a player holding attack (user, 1.5.60 test: "Does the NPC actually draw the bow?" -
// it didn't: the shot went off at once with m_bowDraw switched off, so every arrow was instant and fully drawn).
// Mirrors Player.UpdateAttackBowDraw: Attack.StartDraw (ammo), m_attackDrawTime counts up, the draw animation bool and
// "drawpercent", draw stamina drain (half once fully drawn); released with StartAttack, which reads
// Humanoid.GetAttackDrawPercentage - so the draw time (m_drawDurationMin, down to 20% with Bows skill), the spread and
// the arrow speed are the game's own.
// 1.5.62: the draw runs on the clock like the player's (FixedUpdate adds time every step while attack is held): Maintain
// advances it every frame, also while turning or stepping; Tick only says "still want to shoot" and lets go of the string
// once it's fully drawn, lined up and nothing is in the way (user, 1.5.61 test: 2.4 s draws took 6-8 s - the time only
// ran on the frames it was already lined up - and it shot into a hill).
internal static class MHR_BowDraw
{
	private class State
	{
		public float LastWant;

		public float Started;

		public float Held;

		// a little variation in when it lets go, so it isn't machine-exact (+-10%)
		public float Jitter = Random.Range(0.9f, 1.1f);

		// ran out of stamina with enough draw to shoot: loose instead of dropping the arrow
		public bool LooseNow;

		public string Why = "full draw";
	}

	private static readonly Dictionary<int, State> s_states = new Dictionary<int, State>();

	// 1.5.63: catching its breath - no new draw until stamina is back to [Combat] StaminaResume (log d40905e0: at 0 stamina it
	// started a draw, dropped it the next frame, started again... dozens of times, never letting stamina come back)
	private static readonly HashSet<int> s_resting = new HashSet<int>();

	internal static bool NeedsDraw(Humanoid h)
	{
		ItemDrop.ItemData w = h?.GetCurrentWeapon();
		return w?.m_shared?.m_attack != null && w.m_shared.m_attack.m_bowDraw;
	}

	internal static bool IsResting(Humanoid h)
	{
		return h != null && s_resting.Contains(h.GetInstanceID());
	}

	internal static bool IsDrawing(Humanoid h)
	{
		return h != null && s_states.ContainsKey(h.GetInstanceID());
	}

	// Call every frame the companion wants to shoot (target in range, roughly facing it). Starts the draw if it isn't
	// drawing; looses the arrow once fully drawn, aimed and clear. Returns true on the frame the arrow is loosed.
	internal static bool Tick(Humanoid h, Character target, bool aimed, bool clear)
	{
		ItemDrop.ItemData w = h?.GetCurrentWeapon();
		if (w == null || !NeedsDraw(h))
		{
			return false;
		}
		int id = h.GetInstanceID();
		if (!s_states.TryGetValue(id, out State st))
		{
			if (h.InAttack() || h.IsBlocking() || TooTiredToDraw(h, w, target))
			{
				return false;
			}
			if (!w.m_shared.m_attack.StartDraw(h, w))
			{
				return false;
			}
			st = new State { Started = Time.time };
			s_states[id] = st;
			Attack a = w.m_shared.m_attack;
			float full = Mathf.Lerp(a.m_drawDurationMin, a.m_drawDurationMin * 0.2f, h.GetSkillFactor(w.m_shared.m_skillType));
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[Bow] Drawing {w.m_shared.m_name} - full draw in {full:F1} s");
			ReflectionHelper.TrySetAttackDrawTime(h, 0.0001f);
			w.m_shared.m_holdStartEffect.Create(h.transform.position, Quaternion.identity, h.transform);
			LogBowOnce(h, "companion");
		}
		st.LastWant = Time.time;
		float pct = h.GetAttackDrawPercentage();
		if (pct < (st.LooseNow ? 0f : ReleaseAt(h, w, target, st)))
		{
			return false;
		}
		if (!aimed || !clear)
		{
			if (!clear && st.Held == 0f)
			{
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Bow] Fully drawn but something is in the way of the shot - holding");
			}
			st.Held += Time.deltaTime;
			return false;
		}
		bool shot = h.StartAttack(target, secondaryAttack: false);
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[Bow] {(shot ? "Loosed" : "Couldn't loose")} at {pct * 100f:F0}% draw ({(st.LooseNow ? "out of stamina" : st.Why)}) after {Time.time - st.Started:F1} s" + (st.Held > 0.05f ? $", held {st.Held:F1} s" : ""));
		Cancel(h);
		return shot;
	}

	// Lets go of the string without shooting (target gone, blocking, out of stamina).
	internal static void Cancel(Humanoid h)
	{
		if (h == null)
		{
			return;
		}
		s_states.Remove(h.GetInstanceID());
		h.m_turnSpeed = h.m_runTurnSpeed = ModConfig.TurnSpeed?.Value ?? 300f;
		ReflectionHelper.TrySetAttackDrawTime(h, 0f);
		string state = h.GetCurrentWeapon()?.m_shared?.m_attack?.m_drawAnimationState;
		ZSyncAnimation zanim = h.GetComponent<ZSyncAnimation>();
		if (zanim != null && !string.IsNullOrEmpty(state))
		{
			zanim.SetBool(state, value: false);
		}
	}

	// Call every frame (CompanionCombatAI.Update): the draw time, animation and stamina, like Player.UpdateAttackBowDraw.
	// Let go when nothing has wanted the shot for a second, the bow was put away, it blocks or runs out of stamina.
	internal static void Maintain(Humanoid h, float dt)
	{
		LogPlayerBowOnce();
		if (h == null || !s_states.TryGetValue(h.GetInstanceID(), out State st))
		{
			return;
		}
		ItemDrop.ItemData w = h.GetCurrentWeapon();
		if (w == null || !NeedsDraw(h) || h.IsBlocking() || Time.time - st.LastWant > 1f)
		{
			Cancel(h);
			return;
		}
		float pct = h.GetAttackDrawPercentage();
		float drain = DrawDrain(h, w) * (pct >= 1f ? 0.5f : 1f);
		// 1.5.68: turns towards the target faster while drawn, like you swinging the camera round
		h.m_turnSpeed = h.m_runTurnSpeed = Mathf.Max(ModConfig.TurnSpeed?.Value ?? 300f, ModConfig.CombatBowAimTurnSpeed?.Value ?? 600f);
		if (drain > 0f && !h.HaveStamina())
		{
			if (pct >= MinDraw())
			{
				st.LooseNow = true;
				return;
			}
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Bow] Out of stamina - letting go of the string, getting its breath back");
			s_resting.Add(h.GetInstanceID());
			Cancel(h);
			return;
		}
		ReflectionHelper.TrySetAttackDrawTime(h, ReflectionHelper.GetAttackDrawTime(h) + dt);
		pct = h.GetAttackDrawPercentage();
		ZSyncAnimation zanim = h.GetComponent<ZSyncAnimation>();
		string state = w.m_shared.m_attack.m_drawAnimationState;
		if (zanim != null && !string.IsNullOrEmpty(state))
		{
			zanim.SetBool(state, value: true);
			zanim.SetFloat("drawpercent", pct);
		}
		if (drain > 0f)
		{
			h.UseStamina(drain * dt);
		}
	}

	// Like a player: don't start a draw it can't finish. Needs the stamina to reach the draw it would shoot at (half for a
	// close target, full for a far one); short of that, or below StaminaReserve with a far target, it waits until stamina
	// is back to StaminaResume.
	private static bool TooTiredToDraw(Humanoid h, ItemDrop.ItemData w, Character target)
	{
		CompanionStamina stam = h.GetComponent<CompanionStamina>();
		if (stam == null || stam.MaxStamina <= 0f)
		{
			return false;
		}
		int id = h.GetInstanceID();
		float share = stam.Stamina / stam.MaxStamina;
		if (s_resting.Contains(id))
		{
			if (share < (ModConfig.CombatStaminaResume?.Value ?? 0.5f))
			{
				return true;
			}
			s_resting.Remove(id);
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[Bow] Got its breath back ({stam.Stamina:F0}/{stam.MaxStamina:F0}) - drawing again");
			return false;
		}
		bool far = target == null || Vector3.Distance(h.transform.position, target.transform.position) >= (ModConfig.CombatBowFullDrawRange?.Value ?? 15f);
		Attack a = w.m_shared.m_attack;
		float full = Mathf.Lerp(a.m_drawDurationMin, a.m_drawDurationMin * 0.2f, h.GetSkillFactor(w.m_shared.m_skillType));
		float need = DrawDrain(h, w) * full * (far ? 1f : MinDraw()) + 1f;
		if (stam.Stamina < need || (far && share < (ModConfig.CombatStaminaReserve?.Value ?? 0.3f)))
		{
			s_resting.Add(id);
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[Bow] Too tired to draw ({stam.Stamina:F0}/{stam.MaxStamina:F0} stamina) - waiting for its breath");
			return true;
		}
		return false;
	}

	// 1.5.68: stamina per second while drawing, from the companion's own Bows skill. The game's ItemData.GetDrawStaminaDrain
	// reads Player.m_localPlayer's skill (it only expects the local player to draw), so the companion paid your rate.
	private static float DrawDrain(Humanoid h, ItemDrop.ItemData w)
	{
		float drain = w.m_shared.m_attack.m_drawStaminaDrain;
		if (drain <= 0f)
		{
			return 0f;
		}
		return drain - drain * 0.33f * h.GetSkillFactor(w.m_shared.m_skillType);
	}

	private static float MinDraw()
	{
		return Mathf.Clamp01(ModConfig.CombatBowMinDraw?.Value ?? 0.5f);
	}

	// 1.5.62: how far to draw before letting go, like a player (user: "I dont always do a full draw of the bow. If its closer
	// or I have less stamina I sometimes use a not full draw."). The draw sets the shot (Attack.ProjectileAttackTriggered):
	// spread Lerp(undrawn, drawn accuracy, sqrt(draw)), damage and push x draw, speed along the bow's draw curve.
	// - close (under BowFullDrawRange): just enough draw that the spread still lands on the target
	// - low on stamina (under [Combat] StaminaReserve) or the target rushing in before a full draw: the minimum
	// - otherwise a full draw. Never under BowMinDraw.
	private static float ReleaseAt(Humanoid h, ItemDrop.ItemData w, Character target, State st)
	{
		float min = MinDraw();
		if (target == null)
		{
			st.Why = "full draw";
			return 1f;
		}
		Vector3 me = h.GetEyePoint();
		Vector3 to = target.GetCenterPoint();
		float dist = Vector3.Distance(me, to);
		float fullRange = ModConfig.CombatBowFullDrawRange?.Value ?? 15f;
		CompanionStamina stam = h.GetComponent<CompanionStamina>();
		// 1.5.63: only close - far off a weak arrow drops short (log d40905e0: 53% at a boar 15 m off, 4 m up, missed); far
		// off it waits for its breath instead (TooTiredToDraw)
		if (dist < fullRange && stam != null && stam.MaxStamina > 0f && stam.Stamina / stam.MaxStamina < (ModConfig.CombatStaminaReserve?.Value ?? 0.3f))
		{
			st.Why = "low on stamina";
			return min;
		}
		Attack a = w.m_shared.m_attack;
		Vector3 toMe = me - to;
		toMe.y = 0f;
		float closing = toMe.sqrMagnitude > 0.01f ? Vector3.Dot(target.GetVelocity(), toMe.normalized) : 0f;
		if (dist < 8f && closing > 1f)
		{
			float full = Mathf.Max(0.1f, Mathf.Lerp(a.m_drawDurationMin, a.m_drawDurationMin * 0.2f, h.GetSkillFactor(w.m_shared.m_skillType)));
			float left = (1f - h.GetAttackDrawPercentage()) * full;
			if ((dist - 2f) / closing < left)
			{
				st.Why = $"it's rushing in, {dist:F0} m";
				return min;
			}
		}
		if (dist >= fullRange)
		{
			st.Why = "full draw";
			return 1f;
		}
		ItemDrop.ItemData ammo = h.GetAmmoItem();
		float accDrawn = a.m_projectileAccuracy + (ammo?.m_shared?.m_attack?.m_projectileAccuracy ?? 0f);
		float accUndrawn = a.m_projectileAccuracyMin + (ammo?.m_shared?.m_attack?.m_projectileAccuracyMin ?? 0f);
		float allowed = Mathf.Atan2(Mathf.Max(0.3f, target.GetRadius()), Mathf.Max(0.5f, dist)) * Mathf.Rad2Deg;
		float need = 1f;
		if (accUndrawn > accDrawn + 0.01f && allowed > accDrawn)
		{
			float s = Mathf.Clamp01((accUndrawn - allowed) / (accUndrawn - accDrawn));
			need = s * s;
		}
		need = Mathf.Clamp(need * st.Jitter, min, 1f);
		st.Why = need < 0.99f ? $"close, {dist:F0} m" : "full draw";
		return need;
	}

	// A straight line from the eyes and from the bow hand to the middle of the target that no terrain, rock or building
	// crosses (a player doesn't shoot into the hill in front of them).
	internal static bool ClearShot(Character me, Character target)
	{
		if (me == null || target == null)
		{
			return true;
		}
		Vector3 to = target.GetCenterPoint();
		return ClearLine(me.GetEyePoint(), to, target) && ClearLine(me.transform.position + Vector3.up * 1.3f + me.transform.forward * 0.3f, to, target);
	}

	private static int s_blockMask;

	private static bool ClearLine(Vector3 from, Vector3 to, Character target)
	{
		if (s_blockMask == 0)
		{
			s_blockMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "vehicle");
		}
		Vector3 d = to - from;
		float len = d.magnitude;
		if (len < 0.5f)
		{
			return true;
		}
		RaycastHit[] hits = Physics.RaycastAll(from, d / len, len - 0.3f, s_blockMask, QueryTriggerInteraction.Ignore);
		foreach (RaycastHit hit in hits)
		{
			if (hit.collider != null && hit.collider.GetComponentInParent<Character>() == null)
			{
				return false;
			}
		}
		return true;
	}

	// 1.5.62: diagnostic - the user sees the draw pose but the bow string doesn't move. The game code never touches the
	// string, so it's in the bow model or the character's animation; log what's on the bow in the hand, once for a
	// companion and once for you, to compare.
	private static readonly AccessTools.FieldRef<VisEquipment, GameObject> s_leftInst = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_leftItemInstance");

	private static bool s_loggedCompanion;

	private static bool s_loggedPlayer;

	private static void LogPlayerBowOnce()
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		Player p = Player.m_localPlayer;
		if (!s_loggedPlayer && p != null && p.IsDrawingBow() && p.GetAttackDrawPercentage() > 0.5f)
		{
			LogBowOnce(p, "you");
		}
	}

	private static void LogBowOnce(Humanoid h, string who)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		bool player = who == "you";
		if (player ? s_loggedPlayer : s_loggedCompanion)
		{
			return;
		}
		if (player)
		{
			s_loggedPlayer = true;
		}
		else
		{
			s_loggedCompanion = true;
		}
		try
		{
			VisEquipment ve = h.GetComponent<VisEquipment>();
			GameObject bow = ve != null ? s_leftInst(ve) : null;
			if (bow == null)
			{
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogInfo($"[BowDiag] {who}: no bow model in the left hand");
				return;
			}
			string comps = string.Join(", ", bow.GetComponentsInChildren<Component>(includeInactive: true).Where((Component c) => c != null && !(c is Transform)).Select((Component c) => c.GetType().Name + "@" + c.gameObject.name).Distinct().Take(30));
			string anims = string.Join("; ", bow.GetComponentsInChildren<Animator>(includeInactive: true).Select((Animator a) => a.gameObject.name + " ctrl=" + (a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "none") + " enabled=" + a.enabled + " params=[" + string.Join(",", a.parameters.Select((AnimatorControllerParameter x) => x.name)) + "]"));
			string bones = string.Join("; ", bow.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true).Select((SkinnedMeshRenderer s) => s.gameObject.name + " bones=" + (s.bones?.Length ?? 0) + " root=" + (s.rootBone != null ? s.rootBone.name : "none")));
			Animator body = h.GetComponentInChildren<Animator>();
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogInfo($"[BowDiag] {who}: bow model \"{bow.name}\" under \"{(bow.transform.parent != null ? bow.transform.parent.name : "none")}\" | components: {comps} | animators: {(anims.Length > 0 ? anims : "none")} | skinned: {(bones.Length > 0 ? bones : "none")} | body animator: cull={body?.cullingMode} drawpercent={(body != null ? body.GetFloat("drawpercent").ToString("F2") : "?")}");
		}
		catch (System.Exception ex)
		{
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogInfo("[BowDiag] " + who + ": " + ex.Message);
		}
	}
}
