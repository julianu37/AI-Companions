using System.Runtime.CompilerServices;
using UnityEngine;

namespace Companions;

public class CompanionTalk : MonoBehaviour
{
	private ZNetView _nview;

	private Character _character;

	private Humanoid _humanoid;

	private CompanionFood _food;

	private CompanionSetup _setup;

	private CompanionRest _rest;

	private float _talkTimer;

	private int _lastActionMode = -1;

	private const float RepairThreshold = 0.25f;

	private float _lastSayTime = float.NegativeInfinity;

	private static float _globalLastSpeechTime = float.NegativeInfinity;

	private const float SpeechOffset = 2f;

	private static float MinInterval => ModConfig.SpeechMinInterval.Value;

	private static float MaxInterval => ModConfig.SpeechMaxInterval.Value;

	private static float CullDistance => ModConfig.SpeechCullDistance.Value;

	private static float SpeechTTL => ModConfig.SpeechDuration.Value;

	private static float SayCooldown => ModConfig.SpeechSayCooldown.Value;

	// Fase 3.3c: [Speech] Chatter. Off (the default) = no line on the timer at all - the periodic chatter goes away.
	// Everything that happens on purpose still speaks: Say(text, force: true) (orders, the AI's answers) and the
	// mode-change line below.
	private static bool Chatter => ModConfig.SpeechChatter?.Value ?? false;

	private static string[] ActionLines => SpeechConfig.Instance.Action;

	private static string[] GatherLines => SpeechConfig.Instance.Gather;

	private static string[] ForageLines => SpeechConfig.Instance.Forage;

	private static string[] CombatLines => SpeechConfig.Instance.Combat;

	private static string[] FollowLines => SpeechConfig.Instance.Follow;

	private static string[] HungryLines => SpeechConfig.Instance.Hungry;

	private static string[] RepairLines => SpeechConfig.Instance.Repair;

	private static string[] OverweightLines => SpeechConfig.Instance.Overweight;

	private static string[] SmeltLines => SpeechConfig.Instance.Smelt;

	private static string[] IdleLines => SpeechConfig.Instance.Idle;

	private static string[] HuntLines => SpeechConfig.Instance.Hunt;

	private static string[] FarmLines => SpeechConfig.Instance.Farm;

	private static string[] FishLines => SpeechConfig.Instance.Fish;

	private static string[] CookLines => SpeechConfig.Instance.Cook;

	public void Say(string text, bool force = false)
	{
		Say(text, force, withVoice: true);
	}

	/// <summary>
	/// Fase 3.17: the line that is shown as a bubble is also <b>spoken</b> (with this companion's own voice), unless the
	/// caller already sent the very same line to the TTS (<paramref name="withVoice"/> false - that is the AI path:
	/// <see cref="CompanionChatAI"/> queues the model's reply and the mod's order line itself and only asks for the
	/// bubble here, so nothing is said twice). The cooldown below is the existing one: a line that is not shown is not
	/// spoken either.
	/// </summary>
	internal void Say(string text, bool force, bool withVoice)
	{
		if (!string.IsNullOrEmpty(text) && !(Chat.instance == null))
		{
			float time = Time.time;
			if ((force || !(time - _lastSayTime < SayCooldown)) && (force || !(time - _globalLastSpeechTime < SayCooldown)))
			{
				_lastSayTime = time;
				_globalLastSpeechTime = time;
				Chat.instance.SetNpcText(base.gameObject, Vector3.up * SpeechOffset, CullDistance, SpeechTTL, "", text, large: false);
				if (withVoice)
				{
					SpeakLine(text);
				}
			}
		}
	}

	/// <summary>
	/// Fase 3.17: the voice of the bubble. Every mod line that reaches the player's screen is spoken too - the job and
	/// event lines ("Preciso de uma picareta para isso.", "hc_speech_*"), the mode-change line, the idle lines and the
	/// forced ones - with <b>this</b> companion's voice (Fase 3.10: <see cref="CompanionTts.Speak(CompanionSetup, string, bool)"/>
	/// resolves it from the ZDO and the voice is part of the cache key).
	///
	/// <para>These are fixed lines, word for word, so they are <b>cacheable</b>. Nothing here blocks: when the TTS is off
	/// (<c>[IA] TtsLigado</c>), when <c>[IA] TtsFalarFalasDoMod</c> is off or when the model isn't ready, the bubble is
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

	[SpecialName]
	private static string[] get_NoBedLines()
	{
		return SpeechConfig.Instance.NoBed;
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_character = GetComponent<Character>();
		_humanoid = GetComponent<Humanoid>();
		_food = GetComponent<CompanionFood>();
		_setup = GetComponent<CompanionSetup>();
		_rest = GetComponent<CompanionRest>();
		ResetTimer();
		_talkTimer += Random.Range(0f, MaxInterval * 0.5f);
	}

	private void ResetTimer()
	{
		_talkTimer = Random.Range(MinInterval, MaxInterval);
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f || Chat.instance == null || Player.m_localPlayer == null)
		{
			return;
		}
		float num = Vector3.Distance(base.transform.position, Player.m_localPlayer.transform.position);
		if (num > CullDistance || (_setup != null && CompanionRadialMenu.IsOpenFor(_setup)) || (_setup != null && CompanionInteractPanel.IsOpenFor(_setup)))
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO != null)
		{
			int num2 = zDO.GetInt(CompanionSetup.ActionModeHash);
			if (_lastActionMode >= 0 && num2 != _lastActionMode)
			{
				SayRandom(ActionLines);
			}
			_lastActionMode = num2;
		}
		// Fase 3.3c: [Speech] Chatter off (the default) = no line on the timer at all, so the random chatter is gone.
		// The mode-change line above and every Say(..., force: true) (orders, the AI's answers, job milestones) still
		// speak - the timer isn't even counted down.
		if (!Chatter)
		{
			return;
		}
		_talkTimer -= Time.deltaTime;
		if (!(_talkTimer > 0f))
		{
			ResetTimer();
			SayContextual();
		}
	}

	private void SayRandom(string[] pool)
	{
		if (pool != null && pool.Length != 0)
		{
			Say(pool[Random.Range(0, pool.Length)]);
		}
	}

	private void SayContextual()
	{
		if (HasEnemyNearby())
		{
			SayRandom(CombatLines);
			return;
		}
		if (IsOverweight())
		{
			SayRandom(OverweightLines);
			return;
		}
		if (IsHungry())
		{
			SayRandom(HungryLines);
			return;
		}
		if (NeedsRepair())
		{
			SayRandom(RepairLines);
			return;
		}
		if ((object)_rest != null && _rest.NeedsBed())
		{
			SayRandom(get_NoBedLines());
			return;
		}
		int num = _nview.GetZDO()?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
		if (num >= 1 && num <= 3)
		{
			SayRandom(GatherLines);
			return;
		}
		switch (num)
		{
		case 5:
			SayRandom(ForageLines);
			return;
		case 6:
			SayRandom(SmeltLines);
			return;
		case 7:
			SayRandom(HuntLines);
			return;
		case 8:
			SayRandom(FarmLines);
			return;
		case 9:
			SayRandom(FishLines);
			return;
		case 17:
			SayRandom(CookLines);
			return;
		case 4:
			SayRandom(IdleLines);
			return;
		}
		if (Random.value < 0.3f)
		{
			SayRandom(IdleLines);
		}
		else
		{
			SayRandom(FollowLines);
		}
	}

	private bool HasEnemyNearby()
	{
		if (_character == null)
		{
			return false;
		}
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (!(allCharacter == null) && !(allCharacter.GetHealth() <= 0f))
			{
				float num = Vector3.Distance(base.transform.position, allCharacter.transform.position);
				if (!(num > 10f) && BaseAI.IsEnemy(_character, allCharacter))
				{
					return true;
				}
			}
		}
		return false;
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
				if (maxDurability > 0f && allItem.m_durability / maxDurability < 0.25f)
				{
					return true;
				}
			}
		}
		return false;
	}
}
