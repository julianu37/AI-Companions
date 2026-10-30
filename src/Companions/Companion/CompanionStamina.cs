using UnityEngine;

namespace Companions;

public class CompanionStamina : MonoBehaviour
{
	private const float SaveInterval = 5f;

	private ZNetView _nview;

	private CompanionFood _food;

	private Character _character;

	private float _saveTimer;

	private float _regenDelayTimer;

	private float _remoteSyncTimer;

	private bool _initialized;

	private CompanionRestedBuff _restedBuff;

	public float MaxStamina
	{
		get
		{
			float num = ((_food != null) ? _food.TotalStaminaBonus : 0f);
			return CompanionFood.BaseStamina + num;
		}
	}

	public float Stamina { get; private set; }

	public bool IsResting { get; set; }

	public bool IsRunning { get; set; }

	// 1.4.96: crouching counts like it does for a player (CompanionSneak): quiet footsteps, harder to spot
	private bool _crouching;

	public bool IsCrouching
	{
		get
		{
			return _crouching;
		}
		set
		{
			_crouching = value;
			Character character = (_character != null) ? _character : GetComponent<Character>();
			if (character != null)
			{
				if (value)
				{
					CompanionSneak.Crouching.Add(character);
				}
				else
				{
					CompanionSneak.Crouching.Remove(character);
				}
			}
		}
	}

	private static float RegenRate => ModConfig.StaminaRegenRate.Value;

	private static float RunDrainRate => ModConfig.StaminaRunDrain.Value;

	private static float SneakDrainRate => ModConfig.StaminaSneakDrain.Value;

	private static float SwimDrainRate => ModConfig.StaminaSwimDrain.Value;

	private const float EncumberedDrain = 10f;

	private static float RegenDelay => ModConfig.StaminaRegenDelay.Value;

	public float GetStaminaPercentage()
	{
		float maxStamina = MaxStamina;
		if (!(maxStamina > 0f))
		{
			return 0f;
		}
		return Stamina / maxStamina;
	}

	public void Drain(float amount)
	{
		if (!float.IsNaN(amount) && !(amount <= 0f))
		{
			float stamina = Stamina;
			Stamina = Mathf.Max(0f, Stamina - amount);
			_regenDelayTimer = RegenDelay;
			if (stamina > 0f && Stamina <= 0f)
			{
				CompanionsPlugin.Log.LogDebug($"[Stamina] DEPLETED — drained {amount:F1} (was {stamina:F1}) " + "companion=\"" + (_character?.m_name ?? "?") + "\"");
			}
		}
	}

	public void Restore(float amount)
	{
		if (!float.IsNaN(amount) && !(amount <= 0f))
		{
			float stamina = Stamina;
			Stamina = Mathf.Min(MaxStamina, Stamina + amount);
			CompanionsPlugin.Log.LogDebug($"[Stamina] Restored {amount:F1} — {stamina:F1} → {Stamina:F1}/{MaxStamina:F1} " + "companion=\"" + (_character?.m_name ?? "?") + "\"");
		}
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_food = GetComponent<CompanionFood>();
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
			if (_food == null)
			{
				_food = GetComponent<CompanionFood>();
			}
			if (_character == null)
			{
				_character = GetComponent<Character>();
			}
			float num = _nview.GetZDO().GetFloat(CompanionSetup.StaminaHash, MaxStamina);
			Stamina = Mathf.Clamp(num, 0f, MaxStamina);
			_initialized = true;
			CompanionsPlugin.Log.LogDebug($"[Stamina] Initialized — {Stamina:F1}/{MaxStamina:F1} " + string.Format("(saved={0:F1}) companion=\"{1}\"", num, _character?.m_name ?? "?"));
		}
	}

	private void Update()
	{
		if (!_initialized)
		{
			TryInit();
		}
		else
		{
			if (_nview == null || _nview.GetZDO() == null)
			{
				return;
			}
			if (!_nview.IsOwner())
			{
				_remoteSyncTimer -= Time.deltaTime;
				if (_remoteSyncTimer <= 0f)
				{
					_remoteSyncTimer = 0.5f;
					float num = _nview.GetZDO().GetFloat(CompanionSetup.StaminaHash, MaxStamina);
					Stamina = Mathf.Clamp(float.IsNaN(num) ? 0f : num, 0f, MaxStamina);
				}
				return;
			}
			if (_food == null)
			{
				_food = GetComponent<CompanionFood>();
			}
			if (_character == null)
			{
				_character = GetComponent<Character>();
			}
			float deltaTime = Time.deltaTime;
			float num2 = MaxStamina;
			if (float.IsNaN(num2) || num2 <= 0f)
			{
				num2 = CompanionFood.BaseStamina;
			}
			if (float.IsNaN(Stamina))
			{
				Stamina = 0f;
			}
			if (Stamina > num2)
			{
				Stamina = num2;
			}
			float num3 = ((_character != null) ? _character.GetVelocity().magnitude : 0f);
			bool flag = _character != null && _character.IsSwimming() && num3 > 0.25f;
			float num4 = ((_character != null) ? ((_character.m_speed + _character.m_runSpeed) * 0.5f) : 6f);
			bool flag2 = _character != null && !flag && num3 > 0.5f && (_character.IsRunning() || (num3 > num4 && _character.IsOnGround()));
			bool flag3 = IsCrouching && !flag2 && !flag && num3 > 0.1f;
			// 1.5.62: moving while encumbered costs 10 a second, like the player (Player.m_encumberedStaminaDrain)
			bool heavy = _character != null && !flag && num3 > 0.1f && _character.IsEncumbered();
			if ((flag2 || flag || flag3 || heavy) && Stamina > 0f)
			{
				// 1.5.88: swimming like the player: the skill-0 rate, down to 40% at Swim 100 (Player.OnSwimming)
				float num5 = (flag ? SwimDrainRate * MHR_Water.SwimDrainSkill(_character) : (flag3 ? SneakDrainRate : (heavy && !flag2) ? EncumberedDrain : RunDrainRate));
				// 1.5.4: sneaking costs less with Sneak skill, like the player (Player.OnSneaking: x Lerp(1, 0.25, sqrt(skill)))
				if (flag3 && !flag && _character != null)
				{
					num5 *= Mathf.Lerp(1f, 0.25f, Mathf.Sqrt(Mathf.Clamp01(_character.GetSkillFactor(Skills.SkillType.Sneak))));
				}
				Stamina = Mathf.Max(0f, Stamina - num5 * deltaTime);
				_regenDelayTimer = RegenDelay;
			}
			else
			{
				_regenDelayTimer = Mathf.Max(0f, _regenDelayTimer - deltaTime);
				if (_regenDelayTimer <= 0f && Stamina < num2)
				{
					float num6 = 1f;
					if (IsResting)
					{
						num6 += 1f;
					}
					if (_restedBuff != null && _restedBuff.IsRested)
					{
						num6 += CompanionRestedBuff.StaminaRegenAdditiveBonus;
					}
					float num7 = RegenRate * num6;
					// 1.4.95: wet, cold, meads and other status effects change regen like they do for a player
					if (_character != null && (ModConfig.StatusEffectsOnRegen?.Value ?? true))
					{
						float mult = 1f;
						_character.GetSEMan()?.ModifyStaminaRegen(ref mult);
						num7 *= mult;
					}
					Stamina = Mathf.Min(num2, Stamina + num7 * deltaTime);
				}
			}
			_saveTimer += deltaTime;
			if (_saveTimer >= 5f)
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
			float value = (float.IsNaN(Stamina) ? 0f : Stamina);
			_nview.GetZDO().Set(CompanionSetup.StaminaHash, value);
		}
	}

	private void OnDestroy()
	{
		if (_character != null)
		{
			CompanionSneak.Crouching.Remove(_character);
		}
		if (_initialized)
		{
			SaveToZDO();
		}
	}

	public bool UseStamina(float amount)
	{
		if (float.IsNaN(amount) || amount <= 0f)
		{
			return true;
		}
		if (Stamina < amount)
		{
			CompanionsPlugin.Log.LogDebug($"[Stamina] UseStamina FAILED — need {amount:F1} have {Stamina:F1} " + "companion=\"" + (_character?.m_name ?? "?") + "\"");
			return false;
		}
		Stamina -= amount;
		_regenDelayTimer = RegenDelay;
		return true;
	}
}
