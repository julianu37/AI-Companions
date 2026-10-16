using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public class CompanionRestedBuff : MonoBehaviour
{
	internal class CompanionRestedSE : StatusEffect
	{
		internal float RemainingTime;

		public override string GetIconText()
		{
			if (RemainingTime <= 0f)
			{
				return "";
			}
			int num = (int)RemainingTime;
			int num2 = num / 60;
			int num3 = num % 60;
			if (num2 > 0)
			{
				return num2 + "m " + num3 + "s";
			}
			return num3 + "s";
		}
	}

	private static bool _bonusesCached;

	private static float _baseTTL = 300f;

	private static float _ttlPerComfortLevel = 60f;

	private static float _healthRegenMultiplier = 1.5f;

	private static float _staminaRegenMultiplier = 2f;

	private static float _eitrRegenMultiplier = 1f;

	private static float _raiseSkillModifier = 0.5f;

	private static readonly List<CompanionRestedBuff> _instances = new List<CompanionRestedBuff>();

	private static Sprite _restedIcon;

	private ZNetView _nview;

	private Character _character;

	private bool _isRested;

	private float _totalDuration;

	private float _elapsedTime;

	private int _comfortLevel;

	private bool _isAccumulatingRest;

	private float _restingAccumTimer;

	private float _passiveRestTimer;

	private float _passiveRestCheckTimer;

	private bool _passiveRestConditionsMet;

	public static float HealthRegenMultiplier => _healthRegenMultiplier;

	public static float StaminaRegenAdditiveBonus => _staminaRegenMultiplier - 1f;

	public static float EitrRegenMultiplier => _eitrRegenMultiplier;

	public static float SkillXPModifier => _raiseSkillModifier;

	internal static IReadOnlyList<CompanionRestedBuff> Instances => _instances;

	private static float RestingWarmupTime => ModConfig.RestWarmupTime.Value;

	internal CompanionRestedSE DisplaySE { get; private set; }

	public bool IsRested => _isRested;

	public int ComfortLevel => _comfortLevel;

	public float RemainingTime
	{
		get
		{
			if (!_isRested)
			{
				return 0f;
			}
			return Mathf.Max(0f, _totalDuration - _elapsedTime);
		}
	}

	internal static void ResetCache()
	{
		_bonusesCached = false;
		_restedIcon = null;
		_baseTTL = 300f;
		_ttlPerComfortLevel = 60f;
		_healthRegenMultiplier = 1.5f;
		_staminaRegenMultiplier = 2f;
		_eitrRegenMultiplier = 1f;
		_raiseSkillModifier = 0.5f;
	}

	public void ApplyRestedBuff()
	{
		if (!(_character == null) && !(_character.GetHealth() <= 0f))
		{
			CacheVanillaBonuses();
			int num = (_comfortLevel = CalculateComfort());
			float num2 = _baseTTL + (float)(num - 1) * _ttlPerComfortLevel;
			float remainingTime = RemainingTime;
			if (_isRested && num2 <= remainingTime)
			{
				CompanionsPlugin.Log.LogDebug("[Rested] Already active with longer remaining time " + $"({remainingTime:F0}s > {num2:F0}s) — skipping");
				return;
			}
			_isRested = true;
			_totalDuration = num2;
			_elapsedTime = 0f;
			DisplaySE.m_icon = _restedIcon;
			DisplaySE.RemainingTime = num2;
			string companionName = GetCompanionName();
			DisplaySE.m_name = companionName + " " + ModLocalization.Loc("hc_msg_rested_suffix");
			if (MHR_Owner.IsMine(this))
			{
				Player.m_localPlayer?.Message(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_rested", companionName, num.ToString()));
			}
			CompanionsPlugin.Log.LogInfo($"[Rested] Applied to \"{companionName}\" — comfort={num} duration={num2:F0}s " + $"hpRegen={_healthRegenMultiplier:F2} stamRegen={_staminaRegenMultiplier:F2} " + $"skillXP=+{_raiseSkillModifier:F2}");
		}
	}

	public void StartResting()
	{
		if (!_isAccumulatingRest)
		{
			_isAccumulatingRest = true;
			_restingAccumTimer = 0f;
			CacheVanillaBonuses();
			string companionName = GetCompanionName();
			CompanionsPlugin.Log.LogDebug($"[Rested] \"{companionName}\" started resting — warmup {RestingWarmupTime:F0}s");
		}
	}

	public void StopResting()
	{
		if (_isAccumulatingRest)
		{
			_isAccumulatingRest = false;
			_restingAccumTimer = 0f;
			CompanionsPlugin.Log.LogDebug("[Rested] \"" + GetCompanionName() + "\" stopped resting before warmup completed");
		}
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_character = GetComponent<Character>();
		DisplaySE = ScriptableObject.CreateInstance<CompanionRestedSE>();
		DisplaySE.m_name = "$se_rested";
		DisplaySE.m_tooltip = "$se_rested_tooltip";
	}

	private void OnEnable()
	{
		if (!_instances.Contains(this))
		{
			_instances.Add(this);
		}
	}

	private void OnDisable()
	{
		_instances.Remove(this);
	}

	private void OnDestroy()
	{
		_instances.Remove(this);
		if (DisplaySE != null)
		{
			Object.Destroy(DisplaySE);
		}
	}

	private static void CacheVanillaBonuses()
	{
		if (_bonusesCached || ObjectDB.instance == null)
		{
			return;
		}
		StatusEffect statusEffect = ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectRested);
		if (statusEffect == null)
		{
			CompanionsPlugin.Log.LogWarning("[Rested] Could not find vanilla SE_Rested — using fallback bonus values");
			_bonusesCached = true;
			return;
		}
		_restedIcon = statusEffect.m_icon;
		if (statusEffect is SE_Rested sE_Rested)
		{
			_baseTTL = sE_Rested.m_baseTTL;
			_ttlPerComfortLevel = sE_Rested.m_TTLPerComfortLevel;
			_healthRegenMultiplier = sE_Rested.m_healthRegenMultiplier;
			_staminaRegenMultiplier = sE_Rested.m_staminaRegenMultiplier;
			_eitrRegenMultiplier = sE_Rested.m_eitrRegenMultiplier;
			_raiseSkillModifier = sE_Rested.m_raiseSkillModifier;
			CompanionsPlugin.Log.LogInfo("[Rested] Cached vanilla SE_Rested values — " + $"baseTTL={_baseTTL:F0} ttlPerComfort={_ttlPerComfortLevel:F0} " + $"healthRegen={_healthRegenMultiplier:F2} " + $"staminaRegen={_staminaRegenMultiplier:F2} " + $"eitrRegen={_eitrRegenMultiplier:F2} " + $"skillXP=+{_raiseSkillModifier:F2} " + "icon=" + (_restedIcon?.name ?? "null"));
		}
		else if (statusEffect is SE_Stats sE_Stats)
		{
			_healthRegenMultiplier = sE_Stats.m_healthRegenMultiplier;
			_staminaRegenMultiplier = sE_Stats.m_staminaRegenMultiplier;
			_eitrRegenMultiplier = sE_Stats.m_eitrRegenMultiplier;
			_raiseSkillModifier = sE_Stats.m_raiseSkillModifier;
			CompanionsPlugin.Log.LogInfo("[Rested] Cached vanilla SE_Stats bonuses (not SE_Rested) — " + $"healthRegen={_healthRegenMultiplier:F2} " + $"staminaRegen={_staminaRegenMultiplier:F2} " + $"eitrRegen={_eitrRegenMultiplier:F2} " + $"skillXP=+{_raiseSkillModifier:F2} " + "icon=" + (_restedIcon?.name ?? "null"));
		}
		else
		{
			CompanionsPlugin.Log.LogWarning("[Rested] Vanilla SE_Rested is " + statusEffect.GetType().Name + ", not SE_Stats — using fallback bonus values");
		}
		_bonusesCached = true;
	}

	private string GetCompanionName()
	{
		string result = ((_character != null) ? _character.m_name : ModLocalization.Loc("hc_msg_name_default"));
		if (_nview?.GetZDO() != null)
		{
			string text = _nview.GetZDO().GetString(CompanionSetup.NameHash);
			if (!string.IsNullOrEmpty(text))
			{
				result = text;
			}
		}
		return result;
	}

	private int CalculateComfort()
	{
		Vector3 position = base.transform.position;
		Cover.GetCoverForPoint(position, out var coverPercentage, out var underRoof);
		bool flag = coverPercentage >= 0.8f && underRoof;
		int num = SE_Rested.CalculateComfortLevel(flag, position);
		CompanionsPlugin.Log.LogDebug($"[Rested] Comfort check — cover={coverPercentage:P0} underRoof={underRoof} " + $"inShelter={flag} comfort={num}");
		return num;
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || (_character != null && _character.GetHealth() <= 0f))
		{
			return;
		}
		if (_isAccumulatingRest)
		{
			_restingAccumTimer += Time.deltaTime;
			if (_restingAccumTimer >= RestingWarmupTime)
			{
				_isAccumulatingRest = false;
				_restingAccumTimer = 0f;
				ApplyRestedBuff();
			}
		}
		UpdatePassiveResting();
		if (!_isRested)
		{
			return;
		}
		_elapsedTime += Time.deltaTime;
		if (DisplaySE != null)
		{
			DisplaySE.RemainingTime = Mathf.Max(0f, _totalDuration - _elapsedTime);
		}
		if (_elapsedTime >= _totalDuration)
		{
			_isRested = false;
			_elapsedTime = 0f;
			_totalDuration = 0f;
			if (DisplaySE != null)
			{
				DisplaySE.RemainingTime = 0f;
			}
			CompanionsPlugin.Log.LogDebug("[Rested] Buff expired for \"" + GetCompanionName() + "\"");
		}
	}

	private void UpdatePassiveResting()
	{
		if (_isAccumulatingRest)
		{
			_passiveRestTimer = 0f;
			return;
		}
		_passiveRestCheckTimer -= Time.deltaTime;
		if (_passiveRestCheckTimer <= 0f)
		{
			_passiveRestCheckTimer = 2f;
			_passiveRestConditionsMet = false;
			Vector3 position = base.transform.position;
			if ((bool)EffectArea.IsPointInsideArea(position, EffectArea.Type.Heat))
			{
				Cover.GetCoverForPoint(position, out var coverPercentage, out var underRoof);
				_passiveRestConditionsMet = coverPercentage >= 0.8f && underRoof;
			}
		}
		if (_passiveRestConditionsMet)
		{
			_passiveRestTimer += Time.deltaTime;
			if (_passiveRestTimer >= RestingWarmupTime)
			{
				_passiveRestTimer = 0f;
				CacheVanillaBonuses();
				ApplyRestedBuff();
			}
		}
		else
		{
			_passiveRestTimer = 0f;
		}
	}
}
