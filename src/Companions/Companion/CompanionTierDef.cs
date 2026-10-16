namespace Companions;

public class CompanionTierDef
{
	public string PrefabName;

	public string DisplayName;

	// Traducao PT-BR - auditoria de cobertura: the localization key of the tier name the player reads (the Sub-Type
	// buttons, the buy button, the arrival message and the companion's own default name). DisplayName stays the
	// English name the logs use; CompanionTierData.LocalName resolves this key.
	public string NameKey;

	public string SourcePrefab;

	public float Health;

	public float WalkSpeed;

	public float RunSpeed;

	public bool CanWearArmor;

	public bool GiveStarterGear;
}
