using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Companions;

internal static class OverheadStats
{
	private static FieldInfo _hudsField;

	private static bool _hudsFieldMissing;

	// 2.0.0 (patch audit): EnemyHud.UpdateHuds calls this per companion per frame. It used to reflect on
	// m_huds, call GetMethod("TryGetValue") and allocate an object[2] on every call; the dictionary and the
	// HudData type are cached now and the lookup goes through the non-generic IDictionary (no boxing, no alloc).
	private static IDictionary _huds;

	private static EnemyHud _hudsInstance;

	private static FieldInfo _healthFastField;

	private static FieldInfo _healthFastFriendlyField;

	private static Type _hudDataType;

	private static int _logs;

	// 2.0.0 (second review): the caches above mirror EnemyHud's m_huds, so they must be dropped together with the
	// HUD itself (called from the EnemyHud.OnDestroy postfix). If the HUD object outlives a world reload while
	// m_huds is rebuilt, a stale dictionary never contains the new companions and the bars stop fading silently.
	internal static void Reset()
	{
		_huds = null;
		_hudsInstance = null;
		_hudsFieldMissing = false;
		_hudDataType = null;
		// the bar fields were resolved against the HudData type, so they go with it
		_healthFastField = null;
		_healthFastFriendlyField = null;
	}

	internal static void HideForever(GuiBar bar)
	{
		try
		{
			GameObject gameObject = VisualRoot(bar);
			if (gameObject != null)
			{
				gameObject.SetActive(value: false);
			}
		}
		catch (Exception)
		{
		}
	}

	internal static void Update(Character companion, GuiBar staminaBar, CompanionStamina staminaComp)
	{
		try
		{
			if (!(companion == null))
			{
				object obj = FindHudData(companion);
				if (obj != null)
				{
					GuiBar barField = GetBarField(obj, "m_healthFast", ref _healthFastField);
					GuiBar barField2 = GetBarField(obj, "m_healthFastFriendly", ref _healthFastFriendlyField);
					GuiBar bar = ((!(barField2 != null) || !barField2.gameObject.activeSelf) ? barField : barField2);
					ApplyFade(bar, companion.GetHealth(), companion.GetMaxHealth());
				}
				if (staminaBar != null && staminaComp != null)
				{
					// Both are public properties of the mod's own companion component: read directly, no reflection.
					ApplyFade(staminaBar, staminaComp.Stamina, staminaComp.MaxStamina);
				}
			}
		}
		catch (Exception ex)
		{
			Log("[OverheadStats] update failed: " + ex.Message);
		}
	}

	private static void Log(string s)
	{
		if (_logs < 20)
		{
			_logs++;
			CompanionsPlugin.Log?.LogInfo(s);
		}
	}

	private static bool ShowBars()
	{
		try
		{
			return ModConfig.ShowOverheadBars == null || ModConfig.ShowOverheadBars.Value;
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static object FindHudData(Character companion)
	{
		EnemyHud instance = EnemyHud.instance;
		if (instance == null)
		{
			return null;
		}
		if (_hudsInstance != instance)
		{
			// world reload: the HUD (and its m_huds dictionary) is a new object
			_huds = null;
			_hudsFieldMissing = false;
			_hudsInstance = instance;
		}
		if (_huds == null)
		{
			if (_hudsFieldMissing)
			{
				return null;
			}
			if (_hudsField == null)
			{
				_hudsField = typeof(EnemyHud).GetField("m_huds", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			if (_hudsField == null)
			{
				_hudsFieldMissing = true;
				return null;
			}
			// assigned during Awake, so a null here is transient: just try again on the next frame
			_huds = _hudsField.GetValue(instance) as IDictionary;
			if (_huds == null)
			{
				return null;
			}
			if (_hudDataType == null)
			{
				Type[] arguments = _huds.GetType().GetGenericArguments();
				if (arguments.Length >= 2)
				{
					_hudDataType = arguments[1];
				}
			}
		}
		if (!_huds.Contains(companion))
		{
			return null;
		}
		return _huds[companion];
	}

	private static GuiBar GetBarField(object hudData, string name, ref FieldInfo cache)
	{
		if (cache == null)
		{
			if (_hudDataType == null)
			{
				return null;
			}
			cache = _hudDataType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		if (cache == null)
		{
			return null;
		}
		return (GuiBar)cache.GetValue(hudData);
	}

	internal static GameObject VisualRoot(GuiBar bar)
	{
		if (bar == null)
		{
			return null;
		}
		Transform parent = bar.transform.parent;
		return (!(parent != null)) ? bar.gameObject : parent.gameObject;
	}

	private static void ApplyFade(GuiBar bar, float current, float max)
	{
		bool flag = ShowBars();
		bool flag2 = max > 0f && current >= max - 0.05f;
		bool flag3 = flag && !flag2;
		GameObject gameObject = VisualRoot(bar);
		if (gameObject != null && gameObject.activeSelf != flag3)
		{
			gameObject.SetActive(flag3);
		}
	}
}
