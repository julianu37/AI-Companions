using System.Collections.Generic;
using UnityEngine;

namespace Companions;

// 1.5.41: speech overhaul, phase 1 (user: "make sure anything the NPC says is location and action appropriate. Also it
// will help players understand why / why not a NPC is doing something."). One speech manager per companion:
// - every line has a priority: Order (a reply to your order, said at once) > Problem (something stops it - queued, never
//   lost, and a problem it already told you about isn't repeated for a while) > Event (what it's doing - queued
//   briefly) > Ambient (small talk - only when it has been quiet for a while, dropped otherwise);
// - small talk only fits what it's really doing right now and where it is (CompanionTalk.Fits / the tags in speech.json);
// - a problem said while you're out of earshot shows top left with its name ([Speech] FarAwayMessages).
public class CompanionTalk : MonoBehaviour
{
	public enum Priority
	{
		Ambient,
		Event,
		Problem,
		Order
	}

	private struct Pending
	{
		public string Text;

		public string Topic;

		public Priority Prio;

		public bool Voice;

		public float Until;
	}

	private ZNetView _nview;

	private Character _character;

	private Humanoid _humanoid;

	private CompanionFood _food;

	private CompanionSetup _setup;

	private CompanionRest _rest;

	private CompanionAI _ai;

	private IdleController _idle;

	private float _talkTimer;

	private const float RepairThreshold = 0.25f;

	private float _lastSayTime = float.NegativeInfinity;

	private static float _globalLastSpeechTime = float.NegativeInfinity;

	private readonly List<Pending> _queue = new List<Pending>();

	// topic -> when it may be said again (problems and needs aren't nagged about)
	private readonly Dictionary<string, float> _topicUntil = new Dictionary<string, float>();

	// pool -> the line said last from it, so the same line doesn't come twice in a row
	private static readonly Dictionary<string[], int> s_lastPick = new Dictionary<string[], int>();

	private static float CullDistance => ModConfig.SpeechCullDistance.Value;

	private static float SpeechTTL => ModConfig.SpeechDuration.Value;

	private static float SayCooldown => ModConfig.SpeechSayCooldown.Value;

	// Fase 3.17: the bubble (and the 3D voice source of the TTS) sits this high above the companion's origin - the same
	// offset the player's own bubble uses in SayAsPlayer.
	private const float SpeechOffset = 2f;

	// Fase 3.3c: [Speech] Chatter (bool, off by default). Off = no line on the timer at all, so the periodic small talk
	// is gone. Everything that happens on purpose still speaks: the order replies (SayOrder / Say(..., force: true)),
	// the problems (SayProblem) and the job lines the controllers push as an Event.
	private static bool Chatter => ModConfig.SpeechChatter?.Value ?? false;

	private static bool Enabled => ModConfig.SpeechEnabled?.Value ?? true;

	// how long a problem it has mentioned stays unsaid, unless it changes
	private const float ProblemRepeat = 300f;

	// the same "what I'm doing" line (e.g. "Crops are ready!" before each crop)
	private const float EventRepeat = 120f;

	/// <summary>
	/// Fase 3.17: the voice of the bubble. Every mod line that reaches the player's screen is spoken too - the job and
	/// event lines ("Preciso de uma picareta para isso.", "hc_speech_*"), the order replies, the problems, the idle
	/// lines and the forced ones - with <b>this</b> companion's voice (Fase 3.10: <see cref="CompanionTts.Speak(CompanionSetup, string, bool)"/>
	/// resolves it from the ZDO and the voice is part of the cache key).
	///
	/// <para>These are fixed lines, word for word, so they are <b>cacheable</b>. Nothing here blocks: when the TTS is off
	/// (<c>[AI] TtsEnabled</c>), when <c>[AI] TtsSpeakModLines</c> is off or when the model isn't ready, the bubble is
	/// all there is - exactly as before. Called on the main thread, once per shown line, after the bubble.</para>
	/// </summary>
	private void SpeakLine(string text)
	{
		if (!CompanionTts.Enabled || !CompanionTts.SpeakModLines || !CompanionTts.Ready)
		{
			return;
		}
		try
		{
			if (_setup != null)
			{
				CompanionTts.Speak(_setup, text);
			}
			else
			{
				// defensive: a companion prefab always carries a CompanionSetup, so this is only a fallback
				CompanionTts.Speak(base.gameObject, text);
			}
		}
		catch (System.Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[Speech] The TTS of a bubble line failed: " + ex.Message);
		}
	}

	/// <summary>
	/// Fase 3.3d: the same bubble, but under the head of whoever is speaking - used for the PLAYER's own line (the
	/// text the microphone recognized, see CompanionChatAI.HeardByVoice). What the player said out loud belongs over
	/// the player, not over the companion it was said to. Same offset, cull distance and duration as the companion's
	/// own bubble, and no cooldown: the player pressed a key, the answer must show.
	/// </summary>
	internal static void SayAsPlayer(GameObject talker, string text)
	{
		if (talker == null || string.IsNullOrEmpty(text) || Chat.instance == null)
		{
			return;
		}
		try
		{
			Chat.instance.SetNpcText(talker, Vector3.up * SpeechOffset, CullDistance, SpeechTTL, "", text, large: false);
		}
		catch (System.Exception ex)
		{
			CompanionsPlugin.Log?.LogDebug("[Speech] Player bubble failed: " + ex.Message);
		}
	}

	// small talk about a need (hungry, worn gear, no bed, heavy pack)
	private const float NeedRepeat = 600f;

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_character = GetComponent<Character>();
		_humanoid = GetComponent<Humanoid>();
		_food = GetComponent<CompanionFood>();
		_setup = GetComponent<CompanionSetup>();
		_rest = GetComponent<CompanionRest>();
		_ai = GetComponent<CompanionAI>();
		_idle = GetComponent<IdleController>();
		ResetTimer();
		_talkTimer += Random.Range(0f, _talkTimer * 0.5f);
	}

	// ------------------------------------------------------------------ saying things

	// a line from a job or an order (the existing callers): an Event, or an Order when forced
	public void Say(string text, bool force = false)
	{
		Say(text, force, withVoice: true);
	}

	/// <summary>
	/// Fase 3.17: the line that is shown as a bubble is also <b>spoken</b> (with this companion's own voice), unless the
	/// caller already sent the very same line to the TTS (<paramref name="withVoice"/> false - that is the AI path:
	/// <see cref="CompanionChatAI"/> and <see cref="CompanionOrders"/> queue the model's reply and the mod's order line
	/// themselves and only ask for the bubble here, so nothing is said twice). Being an Order it is shown and spoken at
	/// once; the priority engine decides everything else, and a line that never reaches the screen is never spoken.
	/// </summary>
	internal void Say(string text, bool force, bool withVoice)
	{
		Speak(text, force ? Priority.Order : Priority.Event, null, -1f, withVoice);
	}

	// a reply to the player's order
	public void SayOrder(string text)
	{
		Speak(text, Priority.Order, null, -1f, withVoice: true);
	}

	// something stops it from doing its job; topic = what the problem is about (e.g. "tool:axe"), so it isn't repeated
	public void SayProblem(string topic, string text)
	{
		Speak(text, Priority.Problem, topic, -1f, withVoice: true);
	}

	public void Speak(string text, Priority prio, string topic)
	{
		Speak(text, prio, topic, -1f, withVoice: true);
	}

	// repeat: seconds before the same topic may be said again (-1 = the default for the priority)
	public void Speak(string text, Priority prio, string topic, float repeat)
	{
		Speak(text, prio, topic, repeat, withVoice: true);
	}

	// withVoice: Fase 3.17 - the line is also spoken when the bubble is actually shown (the caller that already sent
	// this very text to the TTS passes false, and the flag rides along in the queue so a delayed line doesn't speak).
	private void Speak(string text, Priority prio, string topic, float repeat, bool withVoice)
	{
		if (string.IsNullOrEmpty(text) || !Enabled || Chat.instance == null)
		{
			return;
		}
		float now = Time.time;
		if (topic != null)
		{
			if (_topicUntil.TryGetValue(topic, out var until) && now < until)
			{
				return;
			}
			if (repeat >= 0f)
			{
				_topicUntil[topic] = now + repeat;
			}
			else if (prio == Priority.Problem)
			{
				_topicUntil[topic] = now + ProblemRepeat;
			}
			else if (prio == Priority.Event)
			{
				_topicUntil[topic] = now + EventRepeat;
			}
		}
		if (TryShow(text, prio, withVoice))
		{
			return;
		}
		if (prio == Priority.Ambient)
		{
			return;
		}
		// someone just spoke: wait for a gap (an Event only briefly, a problem until it's said)
		for (int i = 0; i < _queue.Count; i++)
		{
			if (_queue[i].Text == text)
			{
				return;
			}
		}
		if (_queue.Count >= 4)
		{
			_queue.RemoveAt(0);
		}
		_queue.Add(new Pending
		{
			Text = text,
			Topic = topic,
			Prio = prio,
			Voice = withVoice,
			Until = now + ((prio == Priority.Event) ? 6f : 60f)
		});
	}

	private bool TryShow(string text, Priority prio, bool withVoice)
	{
		float now = Time.time;
		bool force = prio == Priority.Order;
		if (!force && (now - _lastSayTime < SayCooldown || now - _globalLastSpeechTime < SayCooldown))
		{
			return false;
		}
		Player player = Player.m_localPlayer;
		bool hearing = player != null && Vector3.Distance(base.transform.position, player.transform.position) <= CullDistance;
		if (!hearing)
		{
			// out of earshot: a problem shows top left, anything else isn't heard
			if (prio == Priority.Problem && (ModConfig.SpeechFarMessages?.Value ?? true) && MessageHud.instance != null)
			{
				MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, Name() + ": " + text);
				if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug("[Speech] (far away, top left) " + Name() + ": " + text);
			}
			return true;
		}
		_lastSayTime = now;
		_globalLastSpeechTime = now;
		Chat.instance.SetNpcText(base.gameObject, Vector3.up * SpeechOffset, CullDistance, SpeechTTL, "", text, large: false);
		// Fase 3.17: the bubble is on screen, so the line is spoken - once, and only when the caller hasn't already sent
		// this very text to the TTS. It goes after SetNpcText: a line that isn't shown isn't spoken.
		if (withVoice)
		{
			SpeakLine(text);
		}
		CompanionsPlugin.Log.LogDebug("[Speech] " + prio + " " + Name() + ": " + text);
		return true;
	}

	private string Name()
	{
		if (_character == null)
		{
			return "?";
		}
		string n = _character.m_name;
		return string.IsNullOrEmpty(n) ? "?" : Localization.instance?.Localize(n) ?? n;
	}

	private void ResetTimer()
	{
		// upstream 1.5.5: one small talk line every 1.5-3 minutes. Their Rare/Often variants are gone with the bool
		// [Speech] Chatter; with it off the timer is never counted down, so this is only ever rolled for nothing.
		_talkTimer = Random.Range(90f, 180f);
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f || Chat.instance == null || Player.m_localPlayer == null)
		{
			return;
		}
		if (_setup != null && (CompanionRadialMenu.IsOpenFor(_setup) || CompanionInteractPanel.IsOpenFor(_setup)))
		{
			return;
		}
		// waiting lines go out as soon as there's a gap
		if (_queue.Count > 0)
		{
			float now = Time.time;
			int best = -1;
			for (int i = _queue.Count - 1; i >= 0; i--)
			{
				if (now > _queue[i].Until)
				{
					_queue.RemoveAt(i);
				}
			}
			for (int j = 0; j < _queue.Count; j++)
			{
				if (best < 0 || _queue[j].Prio > _queue[best].Prio)
				{
					best = j;
				}
			}
			if (best >= 0 && TryShow(_queue[best].Text, _queue[best].Prio, _queue[best].Voice))
			{
				_queue.RemoveAt(best);
			}
		}
		// Fase 3.3c: [Speech] Chatter off (the default) = no line on the timer at all, so the random small talk is gone.
		// The queue above and everything said on purpose still speak - the timer isn't even counted down.
		if (!Chatter || !Enabled)
		{
			return;
		}
		if (Vector3.Distance(base.transform.position, Player.m_localPlayer.transform.position) > CullDistance)
		{
			return;
		}
		_talkTimer -= Time.deltaTime;
		if (_talkTimer <= 0f)
		{
			ResetTimer();
			SayContextual();
		}
	}

	// ------------------------------------------------------------------ small talk that fits

	private void SayAmbient(string[] pool, string topic = null, float repeat = 0f)
	{
		string line = Pick(pool);
		if (line == null)
		{
			return;
		}
		if (topic != null)
		{
			if (_topicUntil.TryGetValue(topic, out var until) && Time.time < until)
			{
				return;
			}
			_topicUntil[topic] = Time.time + repeat;
		}
		Speak(line, Priority.Ambient, null);
	}

	// a line from the pool that fits here and now, not the one said last time
	internal string Pick(string[] pool)
	{
		if (pool == null || pool.Length == 0)
		{
			return null;
		}
		List<int> fit = new List<int>();
		for (int i = 0; i < pool.Length; i++)
		{
			if (!string.IsNullOrEmpty(pool[i]) && Fits(pool[i], out _))
			{
				fit.Add(i);
			}
		}
		if (fit.Count == 0)
		{
			return null;
		}
		s_lastPick.TryGetValue(pool, out var last);
		if (fit.Count > 1)
		{
			fit.Remove(last);
		}
		int pick = fit[Random.Range(0, fit.Count)];
		s_lastPick[pool] = pick;
		Fits(pool[pick], out var text);
		return text;
	}

	// "[home,night] text": every tag must hold (a "!" in front: must not)
	private bool Fits(string line, out string text)
	{
		text = line;
		if (line.Length == 0 || line[0] != '[')
		{
			return true;
		}
		int end = line.IndexOf(']');
		if (end < 0)
		{
			return true;
		}
		text = line.Substring(end + 1).TrimStart();
		foreach (string raw in line.Substring(1, end - 1).Split(','))
		{
			string tag = raw.Trim().ToLowerInvariant();
			if (tag.Length == 0)
			{
				continue;
			}
			bool not = tag[0] == '!';
			if (not)
			{
				tag = tag.Substring(1);
			}
			if (Holds(tag) == not)
			{
				return false;
			}
		}
		return true;
	}

	private bool Holds(string tag)
	{
		Vector3 pos = base.transform.position;
		switch (tag)
		{
		case "home":
			return AtHome();
		case "away":
			return !AtHome();
		case "night":
			return EnvMan.IsNight();
		case "day":
			return !EnvMan.IsNight();
		case "rain":
			return EnvMan.IsWet();
		case "dry":
			return !EnvMan.IsWet();
		case "cold":
			return EnvMan.IsCold();
		case "indoors":
			return Indoors();
		case "outdoors":
			return !Indoors();
		}
		Heightmap.Biome biome = (WorldGenerator.instance != null) ? WorldGenerator.instance.GetBiome(pos) : Heightmap.Biome.None;
		switch (tag)
		{
		case "meadows":
			return biome == Heightmap.Biome.Meadows;
		case "blackforest":
			return biome == Heightmap.Biome.BlackForest;
		case "swamp":
			return biome == Heightmap.Biome.Swamp;
		case "mountain":
			return biome == Heightmap.Biome.Mountain;
		case "plains":
			return biome == Heightmap.Biome.Plains;
		case "mistlands":
			return biome == Heightmap.Biome.Mistlands;
		case "ashlands":
			return biome == Heightmap.Biome.AshLands;
		case "deepnorth":
			return biome == Heightmap.Biome.DeepNorth;
		case "ocean":
			return biome == Heightmap.Biome.Ocean;
		}
		return false;
	}

	private bool AtHome()
	{
		return _setup != null && _setup.HasHomePosition() && Vector3.Distance(base.transform.position, _setup.GetHomePosition()) <= MHR_ToolKeeper.AtHomeRange();
	}

	private bool Indoors()
	{
		if (_character != null && _character.InInterior())
		{
			return true;
		}
		// under a roof (a house, a shelter)
		return Physics.Raycast(base.transform.position + Vector3.up * 2f, Vector3.up, 30f, LayerMask.GetMask("piece", "static_solid", "Default"));
	}

	private void SayContextual()
	{
		SpeechConfig s = SpeechConfig.Instance;
		// nothing while asleep, sitting, swimming, on a ship, or carrying out an order
		if (_rest != null && (_rest.IsSleeping || _rest.IsResting || _rest.IsNavigating))
		{
			return;
		}
		if (_character.IsSwimming() || (_ai != null && (_ai.IsOnShip || _ai.IsBoardingShip)))
		{
			return;
		}
		CompanionCombatAI combat = GetComponent<CompanionCombatAI>();
		if (_ai != null && _ai.IsInCombat)
		{
			// a real fight only (hunting is the Hunt job's own lines)
			if (combat != null && !combat.HuntMode)
			{
				SayAmbient(s.Combat);
			}
			return;
		}
		if (MHR_JobRules.OtherOrder(_setup))
		{
			return;
		}
		if (IsOverweight())
		{
			SayAmbient(s.Overweight, "need:weight", NeedRepeat);
			return;
		}
		if (IsHungry())
		{
			SayAmbient(s.Hungry, "need:food", NeedRepeat);
			return;
		}
		if (NeedsRepair())
		{
			SayAmbient(s.Repair, "need:repair", NeedRepeat);
			return;
		}
		// no bed of its own: only worth saying at home, where a bed could be built for it
		if (_rest != null && _rest.NeedsBed() && EnvMan.IsNight() && AtHome())
		{
			SayAmbient(s.NoBed, "need:bed", NeedRepeat);
			return;
		}
		int mode = _nview.GetZDO()?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
		// the job's lines only while it's really at it
		string[] job = JobPool(s, mode);
		if (job != null)
		{
			SayAmbient(job);
			return;
		}
		if (_idle != null && _idle.IsActive)
		{
			SayAmbient(s.Idle);
			return;
		}
		if (_setup != null && _setup.GetFollow() && _ai != null && _ai.GetFollowTarget() != null)
		{
			SayAmbient((Random.value < 0.25f) ? s.Idle : s.Follow);
			return;
		}
		if (mode == CompanionSetup.ModeStay || mode == CompanionSetup.ModeFollow)
		{
			SayAmbient(s.Idle);
		}
	}

	// the job's pool when its job is busy right now, else null
	private string[] JobPool(SpeechConfig s, int mode)
	{
		switch (mode)
		{
		case CompanionSetup.ModeGatherWood:
			return Busy<HarvestController>() ? Or(s.Wood, s.Gather) : null;
		case CompanionSetup.ModeGatherStone:
			return Busy<HarvestController>() ? Or(s.Stone, s.Gather) : null;
		case CompanionSetup.ModeGatherOre:
			return Busy<HarvestController>() ? Or(s.Ore, s.Gather) : null;
		case CompanionSetup.ModeForage:
			return Busy<HarvestController>() ? s.Forage : null;
		case CompanionSetup.ModeSmelt:
			return Busy<SmeltController>() ? s.Smelt : null;
		case CompanionSetup.ModeHunt:
			return Busy<HuntController>() ? s.Hunt : null;
		case CompanionSetup.ModeFarm:
			return Busy<FarmController>() ? s.Farm : null;
		case CompanionSetup.ModeFish:
			return Busy<FishController>() ? s.Fish : null;
		case CompanionSetup.ModeCook:
			return Busy<CookController>() ? s.Cook : null;
		case CompanionSetup.ModeRepairBuildings:
			return Busy<UpkeepController>() ? s.Upkeep : null;
		default:
			return null;
		}
	}

	private static string[] Or(string[] a, string[] b)
	{
		return (a != null && a.Length != 0) ? a : b;
	}

	private bool Busy<T>() where T : MonoBehaviour
	{
		if (_idle != null && _idle.IsActive)
		{
			return false;
		}
		T c = GetComponent<T>();
		if (c == null)
		{
			return false;
		}
		switch (c)
		{
		case HarvestController h:
			return h.IsActive;
		case SmeltController sm:
			return sm.IsActive;
		case HuntController hu:
			return hu.IsActive;
		case FarmController f:
			return f.IsActive;
		case FishController fi:
			return fi.IsActive;
		case CookController co:
			return co.IsActive;
		case UpkeepController u:
			return u.IsActive;
		default:
			return false;
		}
	}

	private bool IsOverweight()
	{
		if (_humanoid == null)
		{
			return false;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory != null)
		{
			// "almost full" tracks the effective ceiling (a Megingjord in the pack raises it by 150): two kilos of
			// room left is what the old fixed 298 meant against the default 300
			return inventory.GetTotalWeight() >= CompanionCarry.Max(inventory) - 2f;
		}
		return false;
	}

	private bool IsHungry()
	{
		if (_setup != null && !_setup.CanWearArmor())
		{
			return false;
		}
		if (_food == null)
		{
			return false;
		}
		for (int i = 0; i < CompanionFood.MaxFoodSlots; i++)
		{
			if (_food.GetFood(i).IsActive)
			{
				return false;
			}
		}
		return true;
	}

	private bool NeedsRepair()
	{
		if (_humanoid == null)
		{
			return false;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem.m_shared.m_useDurability && _humanoid.IsItemEquiped(allItem))
			{
				float maxDurability = allItem.GetMaxDurability();
				if (maxDurability > 0f && allItem.m_durability / maxDurability < RepairThreshold)
				{
					return true;
				}
			}
		}
		return false;
	}
}
