using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public static class EnemyHudPatches
{
	private struct CompanionBars
	{
		public GuiBar StaminaBar;

		public GuiBar WeightBar;

		public CompanionStamina StaminaComp;
	}

	[HarmonyPatch(typeof(EnemyHud), "ShowHud")]
	private static class ShowHud_Patch
	{
		private static void Postfix(EnemyHud __instance, Character c)
		{
			if (c == null || c.GetComponent<CompanionStamina>() == null)
			{
				return;
			}
			int instanceID = c.GetInstanceID();
			if (_bars.ContainsKey(instanceID))
			{
				return;
			}
			IDictionary huds = GetHuds(__instance);
			if (huds == null || !huds.Contains(c))
			{
				return;
			}
			object hudData = huds[c];
			GameObject hudGui = GetHudGui(hudData);
			if (hudGui == null)
			{
				return;
			}
			Transform transform = hudGui.transform.Find("Health");
			if (transform == null)
			{
				return;
			}
			float num = transform.GetComponent<RectTransform>().rect.height;
			if (num <= 0f)
			{
				num = 8f;
			}
			float num2 = num + 2f;
			GuiBar guiBar = CreateBar(transform, hudGui.transform, "CompanionStamina", StaminaYellow, num2);
			float yOffset = num2 + num + 2f;
			GuiBar guiBar2 = CreateBar(transform, hudGui.transform, "CompanionWeight", WeightBrown, yOffset);
			OverheadStats.HideForever(guiBar2);
			if (guiBar != null)
			{
				CompanionStamina component = c.GetComponent<CompanionStamina>();
				guiBar.SetValue(component.GetStaminaPercentage());
			}
			if (guiBar2 != null)
			{
				Humanoid component2 = c.GetComponent<Humanoid>();
				if (component2 != null)
				{
					Inventory inventory = component2.GetInventory();
					float value = CompanionCarry.Fraction(inventory);
					guiBar2.SetValue(value);
				}
			}
			_bars[instanceID] = new CompanionBars
			{
				StaminaBar = guiBar,
				WeightBar = guiBar2,
				StaminaComp = c.GetComponent<CompanionStamina>()
			};
			_barCharacters[instanceID] = c;
			CompanionsPlugin.Log.LogDebug($"[HUD] Created companion bars — stamina={guiBar != null} " + $"weight={guiBar2 != null} companion=\"{c.m_name}\"");
		}
	}

	[HarmonyPatch(typeof(EnemyHud), "OnDestroy")]
	private static class OnDestroy_Patch
	{
		private static void Postfix()
		{
			_bars.Clear();
			_barCharacters.Clear();
			_cachedHuds = null;
			_cachedHudInstance = null;
			_hudDataGuiField = null;
		}
	}

	[HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
	private static class UpdateHuds_Patch
	{
		private static void Postfix(EnemyHud __instance)
		{
			HideHudForRadialTarget(__instance);
			List<int> list = null;
			foreach (KeyValuePair<int, CompanionBars> bar in _bars)
			{
				_barCharacters.TryGetValue(bar.Key, out var value);
				if (value == null || (bar.Value.StaminaBar == null && bar.Value.WeightBar == null))
				{
					if (list == null)
					{
						list = new List<int>();
					}
					list.Add(bar.Key);
					continue;
				}
				CompanionBars value2 = bar.Value;
				if (value2.StaminaBar != null && value2.StaminaComp != null)
				{
					value2.StaminaBar.SetValue(value2.StaminaComp.GetStaminaPercentage());
				}
				if (value2.WeightBar != null)
				{
					Humanoid humanoid = value as Humanoid;
					if (humanoid != null)
					{
						Inventory inventory = humanoid.GetInventory();
						float value3 = CompanionCarry.Fraction(inventory);
						value2.WeightBar.SetValue(value3);
						OverheadStats.Update(value, value2.StaminaBar, value2.StaminaComp);
					}
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					CompanionsPlugin.Log.LogDebug("[HUD] Removing stale companion bars — character destroyed or null");
					_bars.Remove(list[i]);
					_barCharacters.Remove(list[i]);
				}
			}
		}
	}

	private static readonly Color StaminaYellow = new Color(1f, 0.8f, 0.1f, 1f);

	private static readonly Color WeightBrown = new Color(0.72f, 0.53f, 0.26f, 1f);

	private static readonly Dictionary<int, CompanionBars> _bars = new Dictionary<int, CompanionBars>();

	private static readonly Dictionary<int, Character> _barCharacters = new Dictionary<int, Character>();

	private static IDictionary _cachedHuds;

	private static EnemyHud _cachedHudInstance;

	private static FieldInfo _hudDataGuiField;

	private static IDictionary GetHuds(EnemyHud instance)
	{
		if (_cachedHudInstance != instance || _cachedHuds == null)
		{
			_cachedHuds = Traverse.Create(instance).Field("m_huds").GetValue() as IDictionary;
			_cachedHudInstance = instance;
		}
		return _cachedHuds;
	}

	private static GameObject GetHudGui(object hudData)
	{
		if (hudData == null)
		{
			return null;
		}
		if (_hudDataGuiField == null)
		{
			_hudDataGuiField = hudData.GetType().GetField("m_gui", BindingFlags.Instance | BindingFlags.Public);
		}
		return _hudDataGuiField?.GetValue(hudData) as GameObject;
	}

	private static GuiBar CreateBar(Transform healthTransform, Transform guiParent, string name, Color color, float yOffset)
	{
		GameObject gameObject = Object.Instantiate(healthTransform.gameObject, guiParent);
		gameObject.name = name;
		RectTransform component = healthTransform.GetComponent<RectTransform>();
		RectTransform component2 = gameObject.GetComponent<RectTransform>();
		component2.localPosition = component.localPosition - new Vector3(0f, yOffset, 0f);
		Transform transform = gameObject.transform.Find("health_slow");
		if (transform != null)
		{
			transform.gameObject.SetActive(value: false);
		}
		Transform transform2 = gameObject.transform.Find("health_fast_friendly");
		if (transform2 != null)
		{
			transform2.gameObject.SetActive(value: false);
		}
		Transform transform3 = gameObject.transform.Find("health_fast");
		if (transform3 == null)
		{
			return null;
		}
		GuiBar component3 = transform3.GetComponent<GuiBar>();
		if (component3 == null)
		{
			return null;
		}
		component3.SetColor(color);
		return component3;
	}

	private static void HideHudForRadialTarget(EnemyHud instance)
	{
		if (CompanionRadialMenu.Instance == null || !CompanionRadialMenu.Instance.IsVisible)
		{
			return;
		}
		CompanionSetup currentCompanion = CompanionRadialMenu.Instance.CurrentCompanion;
		if (currentCompanion == null)
		{
			return;
		}
		Character component = currentCompanion.GetComponent<Character>();
		if (component == null)
		{
			return;
		}
		IDictionary huds = GetHuds(instance);
		if (huds != null && huds.Contains(component))
		{
			object hudData = huds[component];
			GameObject hudGui = GetHudGui(hudData);
			if (hudGui != null)
			{
				hudGui.SetActive(value: false);
			}
		}
	}
}
