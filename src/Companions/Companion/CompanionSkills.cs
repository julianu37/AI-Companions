using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Companions;

public class CompanionSkills : MonoBehaviour
{
	private class SkillData
	{
		public Skills.SkillDef Def;

		public float Level;

		public float Accumulator;

		public SkillData(Skills.SkillDef def)
		{
			Def = def;
			Level = 0f;
			Accumulator = 0f;
		}
	}

	internal static readonly int SkillsHash = "HC_Skills".GetStableHashCode();

	private const float SaveInterval = 10f;

	private static List<Skills.SkillDef> _skillDefs;

	private readonly Dictionary<Skills.SkillType, SkillData> _skills = new Dictionary<Skills.SkillType, SkillData>();

	private ZNetView _nview;

	private Character _character;

	private CompanionRestedBuff _restedBuff;

	private float _saveTimer;

	private bool _dirty;

	private bool _initialized;

	private static float DeathLowerFactor => ModConfig.SkillDeathLossFactor.Value;

	public void RaiseSkill(Skills.SkillType type, float factor = 1f)
	{
		if (type == Skills.SkillType.None)
		{
			return;
		}
		if (!_initialized)
		{
			TryInit();
		}
		SkillData orCreateSkill = GetOrCreateSkill(type);
		if (orCreateSkill != null && !(orCreateSkill.Level >= 100f))
		{
			float num = orCreateSkill.Def?.m_increseStep ?? 1f;
			float num2 = num * factor * Game.m_skillGainRate;
			if (_restedBuff != null && _restedBuff.IsRested)
			{
				num2 += num2 * CompanionRestedBuff.SkillXPModifier;
			}
			orCreateSkill.Accumulator += num2;
			float num3 = Mathf.Pow(Mathf.Floor(orCreateSkill.Level + 1f), 1.5f) * 0.5f + 0.5f;
			string arg = _character?.m_name ?? "?";
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[CompanionSkills] \"{arg}\" raised {type} +{num2:F3} " + $"→ acc {orCreateSkill.Accumulator:F3}/{num3:F3} (lvl {(int)orCreateSkill.Level})");
			if (orCreateSkill.Accumulator >= num3)
			{
				orCreateSkill.Level += 1f;
				orCreateSkill.Level = Mathf.Clamp(orCreateSkill.Level, 0f, 100f);
				orCreateSkill.Accumulator = 0f;
				_dirty = true;
				ShowLevelUpMessage(type, orCreateSkill);
			}
			_dirty = true;
		}
	}

	public float GetSkillFactor(Skills.SkillType type)
	{
		if (type == Skills.SkillType.None)
		{
			return 0f;
		}
		if (!_initialized)
		{
			TryInit();
		}
		if (_skills.TryGetValue(type, out var value))
		{
			return Mathf.Clamp01(value.Level / 100f);
		}
		return 0f;
	}

	public float GetRandomSkillFactor(Skills.SkillType type)
	{
		float skillFactor = GetSkillFactor(type);
		float num = Mathf.Lerp(0.4f, 1f, skillFactor);
		float a = Mathf.Clamp01(num - 0.15f);
		float b = Mathf.Clamp01(num + 0.15f);
		return Mathf.Lerp(a, b, Random.value);
	}

	public static void InitSkillDefs(GameObject playerPrefab)
	{
		Skills skills = (Skills)playerPrefab.GetComponent(typeof(Skills));
		if (skills == null)
		{
			CompanionsPlugin.Log.LogError("[CompanionSkills] Player prefab has no Skills component!");
			return;
		}
		_skillDefs = new List<Skills.SkillDef>(skills.m_skills);
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo($"[CompanionSkills] Cached {_skillDefs.Count} SkillDefs from Player prefab.");
	}

	public void OnDeath()
	{
		foreach (KeyValuePair<Skills.SkillType, SkillData> skill in _skills)
		{
			float num = skill.Value.Level * DeathLowerFactor * Game.m_skillReductionRate;
			skill.Value.Level -= num;
			skill.Value.Accumulator = 0f;
		}
		_dirty = true;
		SaveToZDO();
		string text = ((_character != null) ? _character.m_name : "Companion");
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[CompanionSkills] Death penalty applied to \"" + text + "\" — " + $"all skills lowered by {DeathLowerFactor * Game.m_skillReductionRate * 100f:F0}%");
	}

	public string SerializeSkills()
	{
		if (_skills.Count == 0)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = true;
		foreach (KeyValuePair<Skills.SkillType, SkillData> skill in _skills)
		{
			if (!(skill.Value.Level <= 0f) || !(skill.Value.Accumulator <= 0f))
			{
				if (!flag)
				{
					stringBuilder.Append(';');
				}
				flag = false;
				stringBuilder.Append(((int)skill.Key).ToString(CultureInfo.InvariantCulture));
				stringBuilder.Append(':');
				stringBuilder.Append(skill.Value.Level.ToString("F2", CultureInfo.InvariantCulture));
				stringBuilder.Append(':');
				stringBuilder.Append(skill.Value.Accumulator.ToString("F2", CultureInfo.InvariantCulture));
			}
		}
		return stringBuilder.ToString();
	}

	private static Skills.SkillDef GetSkillDef(Skills.SkillType type)
	{
		if (_skillDefs == null)
		{
			return null;
		}
		for (int i = 0; i < _skillDefs.Count; i++)
		{
			if (_skillDefs[i].m_skill == type)
			{
				return _skillDefs[i];
			}
		}
		return null;
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_character = GetComponent<Character>();
		_restedBuff = GetComponent<CompanionRestedBuff>();
	}

	private void Start()
	{
		if (!_initialized)
		{
			TryInit();
		}
	}

	private void TryInit()
	{
		if (!_initialized && !(_nview == null) && _nview.GetZDO() != null)
		{
			string text = _nview.GetZDO().GetString(SkillsHash);
			if (text.Length > 0)
			{
				LoadFromString(text);
			}
			_initialized = true;
		}
	}

	public void LoadFromString(string data)
	{
		_skills.Clear();
		if (string.IsNullOrEmpty(data))
		{
			return;
		}
		string[] array = data.Split(new char[1] { ';' });
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { ':' });
			if (array2.Length >= 3 && int.TryParse(array2[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && float.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) && float.TryParse(array2[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
			{
				Skills.SkillType skillType = (Skills.SkillType)result;
				Skills.SkillDef skillDef = GetSkillDef(skillType);
				if (skillDef != null)
				{
					_skills[skillType] = new SkillData(skillDef)
					{
						Level = Mathf.Clamp(result2, 0f, 100f),
						Accumulator = Mathf.Max(0f, result3)
					};
				}
			}
		}
		if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[CompanionSkills] Loaded {_skills.Count} skills from string");
	}

	private void Update()
	{
		if (!_initialized)
		{
			TryInit();
		}
		else if (!(_nview == null) && _nview.GetZDO() != null && _nview.IsOwner() && _dirty)
		{
			_saveTimer += Time.deltaTime;
			if (_saveTimer >= 10f)
			{
				_saveTimer = 0f;
				SaveToZDO();
			}
		}
	}

	private void SaveToZDO()
	{
		if (!(_nview == null) && _nview.GetZDO() != null && _nview.IsOwner())
		{
			string value = SerializeSkills();
			_nview.GetZDO().Set(SkillsHash, value);
			_dirty = false;
		}
	}

	private void OnDestroy()
	{
		if (_initialized && _dirty)
		{
			SaveToZDO();
		}
	}

	private SkillData GetOrCreateSkill(Skills.SkillType type)
	{
		if (_skills.TryGetValue(type, out var value))
		{
			return value;
		}
		Skills.SkillDef skillDef = GetSkillDef(type);
		if (skillDef == null)
		{
			if (CompanionsPlugin.LogDetailed) CompanionsPlugin.Log.LogDebug($"[CompanionSkills] No SkillDef for {type} — skipping");
			return null;
		}
		SkillData skillData = new SkillData(skillDef);
		_skills[type] = skillData;
		return skillData;
	}

	private void ShowLevelUpMessage(Skills.SkillType type, SkillData skill)
	{
		Player localPlayer = MHR_Owner.IsMine(this) ? Player.m_localPlayer : null;
		if (localPlayer == null)
		{
			return;
		}
		string text = ((_character != null) ? _character.m_name : ModLocalization.Loc("hc_msg_name_default"));
		if (_nview?.GetZDO() != null)
		{
			string text2 = _nview.GetZDO().GetString(CompanionSetup.NameHash);
			if (!string.IsNullOrEmpty(text2))
			{
				text = text2;
			}
		}
		Sprite icon = skill.Def?.m_icon;
		string msg = text + " — $msg_skillup $skill_" + type.ToString().ToLower() + ": " + (int)skill.Level;
		localPlayer.Message(MessageHud.MessageType.TopLeft, msg, 0, icon);
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo($"[CompanionSkills] \"{text}\" leveled up {type} to {(int)skill.Level}");
	}
}
