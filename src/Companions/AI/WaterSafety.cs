using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Companions;

internal static class WaterSafety
{

	private static PropertyInfo _staminaProp;

	private static PropertyInfo _maxStaminaProp;

	private static Hashtable _shoreTarget = new Hashtable();

	private static Hashtable _shoreRefresh = new Hashtable();

	private static Hashtable _shoreStartTime = new Hashtable();

	private static int _logs;

	private const float MaxSeekSeconds = 25f;

	private static FieldInfo _characterField;

	// 1.4.150: heading for the shore to get out of the water - jobs wait (they pulled it back out and it could drown)
	internal static bool Active(Component c)
	{
		Character ch = c != null ? c.GetComponent<Character>() : null;
		return ch != null && _shoreTarget.ContainsKey(ch.GetInstanceID());
	}

	// 1.5.85: forgets the shore it was heading for (aboard a ship - log ce2af5bd: "stamina low while swimming" right after it sat)
	internal static void Clear(Character ch)
	{
		if (ch == null)
		{
			return;
		}
		int id = ch.GetInstanceID();
		_shoreTarget.Remove(id);
		_shoreRefresh.Remove(id);
		_shoreStartTime.Remove(id);
	}

	internal static bool Tick(BaseAI ai, float dt)
	{
		try
		{
			if (ai == null)
			{
				return false;
			}
			if (_characterField == null)
			{
				_characterField = typeof(BaseAI).GetField("m_character", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			Character character = ((!(_characterField == null)) ? ((Character)_characterField.GetValue(ai)) : null);
			if (character == null)
			{
				return false;
			}
			int instanceID = character.GetInstanceID();
			// 1.5.85: aboard a ship (seated, or standing on its deck) - nothing to swim for
			CompanionAI onShip = ai as CompanionAI;
			if ((onShip != null && onShip.IsOnShip) || MHR_Board.ShipUnder(character) != null)
			{
				Clear(character);
				return false;
			}
			// 1.5.84: boarding with a ship's ladder close by - keep on for the ladder (treading water costs no stamina)
			if (MHR_Board.KeepSwimming(character))
			{
				if (_shoreTarget.ContainsKey(instanceID))
				{
					_shoreTarget.Remove(instanceID);
					_shoreRefresh.Remove(instanceID);
					_shoreStartTime.Remove(instanceID);
				}
				return false;
			}
			bool flag = _shoreTarget.ContainsKey(instanceID);
			if (flag)
			{
				float num = ((!_shoreStartTime.ContainsKey(instanceID)) ? Time.time : ((float)_shoreStartTime[instanceID]));
				if (Time.time - num > 25f)
				{
					_shoreTarget.Remove(instanceID);
					_shoreRefresh.Remove(instanceID);
					_shoreStartTime.Remove(instanceID);
					if (CompanionsPlugin.LogNormal) Log("[WaterSafety] giving up on shore-seeking after " + 25f + "s (safety timeout) - resuming normal behavior.");
					return false;
				}
			}
			if (!character.IsSwimming())
			{
				if (flag)
				{
					_shoreTarget.Remove(instanceID);
					_shoreRefresh.Remove(instanceID);
					_shoreStartTime.Remove(instanceID);
				}
				return false;
			}
			bool flag2 = flag;
			if (!flag2)
			{
				Component component = character.GetComponent("CompanionStamina");
				if (component == null)
				{
					return false;
				}
				if (StaminaPct(component) > ThresholdPct())
				{
					return false;
				}
			}
			Vector3 position = character.transform.position;
			float num2 = ((!_shoreRefresh.ContainsKey(instanceID)) ? 999f : (Time.time - (float)_shoreRefresh[instanceID]));
			if (!flag2 || num2 > 3f)
			{
				float liquidLevel = character.GetLiquidLevel();
				if (TryFindShore(position, liquidLevel, out var shore))
				{
					_shoreTarget[instanceID] = shore;
					_shoreRefresh[instanceID] = Time.time;
					if (!flag2)
					{
						_shoreStartTime[instanceID] = Time.time;
						if (CompanionsPlugin.LogNormal) Log("[WaterSafety] stamina low while swimming - heading for the nearest shore.");
					}
				}
				else if (!flag2)
				{
					if (CompanionsPlugin.LogNormal) Log("[WaterSafety] stamina low while swimming, but no nearby shore found yet.");
					return false;
				}
			}
			if (!_shoreTarget.ContainsKey(instanceID))
			{
				return false;
			}
			Vector3 vector = (Vector3)_shoreTarget[instanceID];
			// 1.4.141 (movement phase 4 part 2): swims for the bank through MHR_Mover (was the old MoveToPoint by reflection)
			CompanionAI cai = ai as CompanionAI;
			if (cai == null)
			{
				return false;
			}
			MHR_Mover.Go(cai, dt, vector, 1f, true, "Shore");
			return true;
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogNormal) Log("[WaterSafety] error: " + ex.Message);
			return false;
		}
	}

	internal static bool ShouldAvoidChase(Character self, Character target)
	{
		//Discarded unreachable code: IL_0008, IL_004d
		return MHR_Threat.AvoidChase(self, target);
	}

	private static void Log(string s)
	{
		if (_logs < 60)
		{
			_logs++;
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log?.LogInfo(s);
		}
	}

	private static float ThresholdPct()
	{
		try
		{
			if (ModConfig.WaterStaminaFleeThreshold != null)
			{
				return ModConfig.WaterStaminaFleeThreshold.Value;
			}
		}
		catch (Exception)
		{
		}
		return 0.25f;
	}

	private static float StaminaPct(Component stamComp)
	{
		Type type = stamComp.GetType();
		if (_staminaProp == null)
		{
			_staminaProp = type.GetProperty("Stamina", BindingFlags.Instance | BindingFlags.Public);
		}
		if (_maxStaminaProp == null)
		{
			_maxStaminaProp = type.GetProperty("MaxStamina", BindingFlags.Instance | BindingFlags.Public);
		}
		if (_staminaProp == null || _maxStaminaProp == null)
		{
			return 1f;
		}
		float num = (float)_staminaProp.GetValue(stamComp, null);
		float num2 = (float)_maxStaminaProp.GetValue(stamComp, null);
		if (num2 <= 0f)
		{
			return 1f;
		}
		return num / num2;
	}

	private static bool TryFindShore(Vector3 from, float waterLevel, out Vector3 shore)
	{
		ZoneSystem instance = ZoneSystem.instance;
		shore = from;
		if (instance == null)
		{
			return false;
		}
		for (int i = 1; i <= 5; i++)
		{
			float num = (float)i * 10f;
			int num2 = 10;
			float num3 = float.MaxValue;
			Vector3 vector = from;
			bool flag = false;
			for (int j = 0; j < num2; j++)
			{
				float f = 360f / (float)num2 * (float)j * (Mathf.PI / 180f);
				Vector3 vector2 = from + new Vector3(Mathf.Sin(f) * num, 0f, Mathf.Cos(f) * num);
				if (instance.GetGroundHeight(vector2, out var height) && height >= waterLevel - 0.3f)
				{
					float num4 = Vector3.Distance(from, vector2);
					if (num4 < num3)
					{
						num3 = num4;
						vector = vector2;
						vector.y = height;
						flag = true;
					}
				}
			}
			if (flag)
			{
				shore = vector;
				return true;
			}
		}
		return false;
	}
}
