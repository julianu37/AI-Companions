namespace Companions;

public class CompanionTierDef
{
	public string PrefabName;

	public string DisplayName;

	// 1.5.60: the name the player reads (panel buttons, "your new X has arrived"); DisplayName stays English for logs
	public string NameKey;

	public string LocalName => string.IsNullOrEmpty(NameKey) ? DisplayName : ModLocalization.Loc(NameKey);

	public string SourcePrefab;

	public float Health;

	public float WalkSpeed;

	public float RunSpeed;

	public bool CanWearArmor;

	public bool GiveStarterGear;
}
