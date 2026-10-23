using System.Collections.Generic;

namespace Companions;

public static class CompanionTierData
{
	public static readonly CompanionTierDef Companion;

	public static readonly CompanionTierDef DvergerWarrior;

	public static readonly CompanionTierDef Dverger;

	public static readonly CompanionTierDef DvergerFire;

	public static readonly CompanionTierDef DvergerIce;

	public static readonly CompanionTierDef DvergerSupport;

	public static readonly CompanionTierDef[] DvergerVariants;

	/// <summary>
	/// Every tier the mod offers, in the order the AI panel lists it (Fase 3.14): the plain Companion first, then the
	/// dverger ones - the old "Dverger" mage included, because its prefab is registered too (Fase 3.16).
	/// <see cref="CompanionManager.ChangeTier"/> maps a prefab hash back to its tier with this list. The <b>starter</b>
	/// panel does not read it: its type buttons use <see cref="Companion"/>, <see cref="Dverger"/> and
	/// <see cref="DvergerVariants"/> (and that row is hidden in the starter anyway).
	/// </summary>
	public static readonly CompanionTierDef[] All;

	private static readonly HashSet<int> _dvergerHashes;

	public static int Price => ModConfig.CompanionPrice.Value;

	public static float MaxCarryWeight => ModConfig.MaxCarryWeight.Value;

	// Traducao PT-BR - auditoria de cobertura: the tier name as the player reads it. The localized text is written in
	// directly (the same thing MHR_Shrine does for the shrine's piece name), with the English data name as the
	// fallback for the (unlikely) case of the dictionary not being up yet - LocOr alone would return the raw key
	// while Localization.instance is null.
	public static string LocalName(CompanionTierDef def)
	{
		string key = ((def != null) ? def.NameKey : null);
		string fallback = ((def != null) ? def.DisplayName : null);
		if (string.IsNullOrEmpty(fallback))
		{
			fallback = "Companion";
		}
		if (Localization.instance == null || string.IsNullOrEmpty(key))
		{
			return fallback;
		}
		return ModLocalization.LocOr(key, fallback);
	}

	public static bool IsDvergerVariant(int prefabHash)
	{
		return _dvergerHashes.Contains(prefabHash);
	}

	static CompanionTierData()
	{
		Companion = new CompanionTierDef
		{
			PrefabName = "HC_Companion",
			DisplayName = "Companion",
			NameKey = "hc_tier_companion",
			SourcePrefab = "Player",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = true,
			GiveStarterGear = true
		};
		DvergerWarrior = new CompanionTierDef
		{
			PrefabName = "HC_DvergerWarrior",
			DisplayName = "Warrior",
			NameKey = "hc_tier_warrior",
			SourcePrefab = "Dverger",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		Dverger = new CompanionTierDef
		{
			PrefabName = "HC_Dverger",
			DisplayName = "Mage",
			NameKey = "hc_tier_mage",
			SourcePrefab = "DvergerMage",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		DvergerFire = new CompanionTierDef
		{
			PrefabName = "HC_DvergerFire",
			DisplayName = "Fire Mage",
			NameKey = "hc_tier_fire_mage",
			SourcePrefab = "DvergerMageFire",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		DvergerIce = new CompanionTierDef
		{
			PrefabName = "HC_DvergerIce",
			DisplayName = "Ice Mage",
			NameKey = "hc_tier_ice_mage",
			SourcePrefab = "DvergerMageIce",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		DvergerSupport = new CompanionTierDef
		{
			PrefabName = "HC_DvergerSupport",
			DisplayName = "Support",
			NameKey = "hc_tier_support",
			SourcePrefab = "DvergerMageSupport",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		DvergerVariants = new CompanionTierDef[4] { DvergerWarrior, DvergerFire, DvergerIce, DvergerSupport };
		// the full list, in the order the panels offer it (the starter's type picker and the AI panel's tier picker)
		All = new CompanionTierDef[6] { Companion, DvergerWarrior, Dverger, DvergerFire, DvergerIce, DvergerSupport };
		_dvergerHashes = new HashSet<int>();
		CompanionTierDef[] dvergerVariants = DvergerVariants;
		foreach (CompanionTierDef companionTierDef in dvergerVariants)
		{
			_dvergerHashes.Add(companionTierDef.PrefabName.GetStableHashCode());
		}
		_dvergerHashes.Add(Dverger.PrefabName.GetStableHashCode());
	}
}
