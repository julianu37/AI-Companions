using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Companions;

// 1.5.1: the companion roster - a saved copy of each of your companions, kept in your character file per world, so a
// companion that's gone missing (for example deleted by a server without the mod) can be brought back.
// Each copy has a "generation": a recovered companion gets the next one, and any older copy that turns up later
// (the original found in the wild, or its old tombstone) is removed or no longer respawns.
internal static class MHR_Roster
{
	internal static readonly int GenHash = "HC_Gen".GetStableHashCode();

	internal static readonly int RespawnGenHash = "HC_Respawn_Gen".GetStableHashCode();

	private const int Version = 1;

	private const float ConfirmSeconds = 60f;

	// a companion's items load from the world a moment after it does - don't save it before then (it'd look empty)
	private const float LoadSettleSeconds = 10f;

	private static bool s_saving;

	internal enum Status
	{
		Alive,
		Dead,
		Lost,
		Unknown,
		// 1.5.80: despawned this session - can be brought back (no gear) until the character next loads the world
		Unsummoned
	}

	internal class Entry
	{
		public string Id = "";
		public int Gen;
		public string Name = "";
		public string Prefab = "";
		public string Appearance = "";
		public string Owner = "";
		public int ActionMode;
		public int Schema;
		public bool Follow = true;
		public bool FollowPaused;
		public bool StayHome;
		public bool AutoPickup = true;
		public bool Wander;
		public int Commandable = 1;
		public int Formation = -1;
		public string Skills = "";
		public bool HasHome;
		public Vector3 HomePos;
		public int CombatStance;
		public bool HasBed;
		public Vector3 BedPos;
		public string Inventory = "";
		public Vector3 LastPos;
		public double LastSeen;
		public bool Dead;

		// 1.5.42: added at the end of the saved entry (older entries just don't have it)
		public string FarmZones;

		// 1.5.80: despawned (unsummoned) - kept for recovery without gear; also at the end
		public bool Unsummoned;
	}

	// 1.5.80: companions unsummoned in this game session (user: "the unsummoned recovery should only be until the player
	// logs in again. So that the list does not get full of unsummoned NPC's. Maybe have this as a toggle in the settings").
	// A new session (new ZNet - the world loaded again) drops the rest when the roster loads.
	private static readonly HashSet<string> s_sessionUnsummoned = new HashSet<string>();

	private static ZNet s_sessionNet;

	private static readonly List<Entry> s_entries = new List<Entry>();

	/// <summary>Fase 3.19 (auditoria): the same entries by id - <see cref="Find"/> was a linear walk used by the
	/// per-frame stale-copy check and by every snapshot; with the entries in a dictionary it is one lookup.</summary>
	private static readonly Dictionary<string, Entry> s_byId = new Dictionary<string, Entry>(StringComparer.Ordinal);

	private static string s_loadedKey;

	private static Player s_loadedFor;

	private static float s_nextSnapshot;

	/// <summary>Fase 3.19: when the stale-copy check runs again (see <see cref="Tick"/>).</summary>
	private static float s_nextStaleCheck;

	private static string s_confirmId;

	private static float s_confirmUntil;

	private static readonly FieldInfo s_objectsById = AccessTools.Field(typeof(ZDOMan), "m_objectsByID");

	private static string Key()
	{
		ZNet instance = ZNet.instance;
		if (instance == null)
		{
			return null;
		}
		return $"HC_Roster_{instance.GetWorldUID()}";
	}

	private static string MyId()
	{
		return Player.m_localPlayer?.GetPlayerID().ToString() ?? "";
	}

	// the roster belongs to the current character and world - reload it when either changes
	private static bool EnsureLoaded()
	{
		Player localPlayer = Player.m_localPlayer;
		string text = Key();
		if (localPlayer == null || text == null)
		{
			return false;
		}
		if ((object)s_loadedFor == localPlayer && s_loadedKey == text)
		{
			return true;
		}
		s_loadedFor = localPlayer;
		s_loadedKey = text;
		s_entries.Clear();
		s_byId.Clear();
		s_confirmId = null;
		s_nextSnapshot = Time.unscaledTime + LoadSettleSeconds + 2f;
		if (localPlayer.m_customData.TryGetValue(text, out var value) && !string.IsNullOrEmpty(value))
		{
			string[] array = value.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string data in array)
			{
				Entry entry = Read(data);
				if (entry != null)
				{
					s_entries.Add(entry);
					if (!string.IsNullOrEmpty(entry.Id))
					{
						s_byId[entry.Id] = entry;
					}
				}
			}
		}
		// 1.5.80: unsummoned companions are only kept for the session they were unsummoned in
		if ((object)s_sessionNet != ZNet.instance)
		{
			s_sessionNet = ZNet.instance;
			s_sessionUnsummoned.Clear();
		}
		int dropped = s_entries.RemoveAll((Entry e) => e.Unsummoned && !s_sessionUnsummoned.Contains(e.Id));
		if (dropped > 0)
		{
			Save(toDisk: false);
			CompanionsPlugin.Log.LogInfo($"[Roster] Dropped {dropped} companion(s) unsummoned in an earlier session");
		}
		return true;
	}

	private static void Save(bool toDisk)
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || s_loadedKey == null)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Entry s_entry in s_entries)
		{
			stringBuilder.Append(Write(s_entry)).Append('\n');
		}
		localPlayer.m_customData[s_loadedKey] = stringBuilder.ToString();
		if (toDisk && Game.instance != null)
		{
			s_saving = true;
			try
			{
				Game.instance.SavePlayerProfile(setLogoutPoint: false);
			}
			finally
			{
				s_saving = false;
			}
		}
	}

	private static string Write(Entry e)
	{
		ZPackage zPackage = new ZPackage();
		zPackage.Write(Version);
		zPackage.Write(e.Id);
		zPackage.Write(e.Gen);
		zPackage.Write(e.Name);
		zPackage.Write(e.Prefab);
		zPackage.Write(e.Appearance);
		zPackage.Write(e.Owner);
		zPackage.Write(e.ActionMode);
		zPackage.Write(e.Schema);
		zPackage.Write(e.Follow);
		zPackage.Write(e.FollowPaused);
		zPackage.Write(e.StayHome);
		zPackage.Write(e.AutoPickup);
		zPackage.Write(e.Wander);
		zPackage.Write(e.Commandable);
		zPackage.Write(e.Formation);
		zPackage.Write(e.Skills);
		zPackage.Write(e.HasHome);
		zPackage.Write(e.HomePos);
		zPackage.Write(e.CombatStance);
		zPackage.Write(e.HasBed);
		zPackage.Write(e.BedPos);
		zPackage.Write(e.Inventory);
		zPackage.Write(e.LastPos);
		zPackage.Write(e.LastSeen);
		zPackage.Write(e.Dead);
		zPackage.Write(e.FarmZones ?? "");
		zPackage.Write(e.Unsummoned);
		return zPackage.GetBase64();
	}

	private static Entry Read(string data)
	{
		try
		{
			ZPackage zPackage = new ZPackage(data);
			if (zPackage.ReadInt() != Version)
			{
				return null;
			}
			return new Entry
			{
				Id = zPackage.ReadString(),
				Gen = zPackage.ReadInt(),
				Name = zPackage.ReadString(),
				Prefab = zPackage.ReadString(),
				Appearance = zPackage.ReadString(),
				Owner = zPackage.ReadString(),
				ActionMode = zPackage.ReadInt(),
				Schema = zPackage.ReadInt(),
				Follow = zPackage.ReadBool(),
				FollowPaused = zPackage.ReadBool(),
				StayHome = zPackage.ReadBool(),
				AutoPickup = zPackage.ReadBool(),
				Wander = zPackage.ReadBool(),
				Commandable = zPackage.ReadInt(),
				Formation = zPackage.ReadInt(),
				Skills = zPackage.ReadString(),
				HasHome = zPackage.ReadBool(),
				HomePos = zPackage.ReadVector3(),
				CombatStance = zPackage.ReadInt(),
				HasBed = zPackage.ReadBool(),
				BedPos = zPackage.ReadVector3(),
				Inventory = zPackage.ReadString(),
				LastPos = zPackage.ReadVector3(),
				LastSeen = zPackage.ReadDouble(),
				Dead = zPackage.ReadBool(),
				FarmZones = ReadOptionalString(zPackage),
				Unsummoned = ReadOptionalBool(zPackage)
			};
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Roster] Skipped an unreadable roster entry: " + ex.Message);
			return null;
		}
	}

	private static bool ReadOptionalBool(ZPackage p)
	{
		try
		{
			return p.GetPos() < p.Size() && p.ReadBool();
		}
		catch
		{
			return false;
		}
	}

	private static string ReadOptionalString(ZPackage p)
	{
		try
		{
			return (p.GetPos() < p.Size()) ? p.ReadString() : "";
		}
		catch
		{
			return "";
		}
	}

	private static Entry Find(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		Entry entry;
		return s_byId.TryGetValue(id, out entry) ? entry : null;
	}

	internal static int GenOf(string id)
	{
		if (!EnsureLoaded())
		{
			return 0;
		}
		return Find(id)?.Gen ?? 0;
	}

	private static bool IsMine(CompanionSetup c, out ZNetView nview, out ZDO zdo)
	{
		nview = c?.GetComponent<ZNetView>();
		zdo = nview?.GetZDO();
		if (zdo == null || !nview.IsOwner())
		{
			return false;
		}
		string text = MyId();
		if (text.Length > 0)
		{
			return zdo.GetString(CompanionSetup.OwnerHash) == text;
		}
		return false;
	}

	internal static void Tick()
	{
		if (ZNet.instance == null || ZNetScene.instance == null || !EnsureLoaded())
		{
			return;
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer.IsDead() || localPlayer.InIntro())
		{
			return;
		}
		if (Time.unscaledTime >= s_nextSnapshot)
		{
			s_nextSnapshot = Time.unscaledTime + ModConfig.RosterSaveInterval.Value;
			SnapshotAll();
		}
		// Fase 3.19 (auditoria): the stale-copy walk is O(N) over every companion and used to run on EVERY frame. It
		// has its own gate (1 s): the roster only changes on a snapshot, and one second is plenty to notice a copy
		// that turned up.
		if (Time.unscaledTime >= s_nextStaleCheck)
		{
			s_nextStaleCheck = Time.unscaledTime + 1f;
			RemoveStaleCopies();
		}
		if (ModConfig.ShowCompanionRoster.Value)
		{
			ModConfig.ShowCompanionRoster.Value = false;
			ShowRoster();
		}
		if (ModConfig.RecoverLostCompanion.Value)
		{
			ModConfig.RecoverLostCompanion.Value = false;
			RecoverNext();
		}
	}

	/// <summary>
	/// Fase 3.19: how many companions the local player has in this world. It is the roster (alive, dead, lost and
	/// unknown - the entries saved with the character) plus every live companion the roster hasn't snapshotted yet
	/// (the first snapshot only happens a few seconds after entering the world). Used by the
	/// <c>[General] MaxCompanions</c> ceiling - dying does not free a place, because the tombstone brings the same
	/// companion back instead of creating one.
	/// </summary>
	internal static int CountMine()
	{
		if (!EnsureLoaded())
		{
			return 0;
		}
		string owner = MyId();
		HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
		int count = 0;
		for (int i = 0; i < s_entries.Count; i++)
		{
			Entry entry = s_entries[i];
			if (entry == null)
			{
				continue;
			}
			// the roster is stored in this character's own data, so it is already "mine"; the owner is only checked
			// when it is written (an entry from an old save has it empty)
			if (!string.IsNullOrEmpty(entry.Owner) && !string.IsNullOrEmpty(owner) && entry.Owner != owner)
			{
				continue;
			}
			if (!string.IsNullOrEmpty(entry.Id))
			{
				ids.Add(entry.Id);
			}
			count++;
		}
		foreach (CompanionSetup companion in CompanionSetup.AllCompanions)
		{
			if (companion == null || companion._mhrStale || !MHR_Owner.IsMine(companion))
			{
				continue;
			}
			ZDO zdo = companion.GetComponent<ZNetView>()?.GetZDO();
			if (zdo == null)
			{
				continue;
			}
			string id = zdo.GetString(CompanionSetup.MHR_CompanionIdHash, "");
			if (!string.IsNullOrEmpty(id) && ids.Contains(id))
			{
				continue;   // already counted from the roster
			}
			count++;
		}
		return count;
	}

	// an older copy of a recovered companion has turned up (found in the wild, or its old tombstone respawned it) - remove it
	private static void RemoveStaleCopies()
	{
		List<CompanionSetup> list = null;
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			if (!IsMine(allCompanion, out var _, out var zdo))
			{
				continue;
			}
			Entry entry = Find(zdo.GetString(CompanionSetup.MHR_CompanionIdHash));
			if (entry != null && entry.Gen > zdo.GetInt(GenHash))
			{
				Character component = allCompanion.GetComponent<Character>();
				if (component != null && component.GetHealth() > 0f && !allCompanion._mhrStale)
				{
					(list ?? (list = new List<CompanionSetup>())).Add(allCompanion);
				}
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (CompanionSetup item in list)
		{
			ZDO zDO = item.GetComponent<ZNetView>().GetZDO();
			string text = zDO.GetString(CompanionSetup.NameHash, ModLocalization.Loc("hc_msg_name_default"));
			CompanionsPlugin.Log.LogWarning($"[Roster] Found an old copy of \"{text}\" at {item.transform.position:F1} (copy {zDO.GetInt(GenHash)}, the recovered one is copy {Find(zDO.GetString(CompanionSetup.MHR_CompanionIdHash)).Gen}) - removing it");
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, ModLocalization.LocFmt("hc_roster_old_copy", text));
			// Fase 3.19 (auditoria): the old copy is destroyed, so its pack goes to the ground first - the copy is
			// removed, its items are not (an old copy that came back from the wild could be carrying everything the
			// player had, and deleting it silently lost the lot)
			DropInventory(item, text);
			item._mhrStale = true;
			// Fase 3.19: NOT PrepareForDespawn() - that one would forget the roster entry of the companion by id, and
			// the old copy shares the id with the recovered (live) one
			item.PrepareStaleCopyForDespawn();
			Humanoid component2 = item.GetComponent<Humanoid>();
			if (component2 != null)
			{
				component2.m_onDeath = null;
			}
			item.GetComponent<ZNetView>().Destroy();
		}
	}

	/// <summary>Fase 3.19: puts everything the old copy carries on the ground under it, the same way the game drops a
	/// container's contents. Called before the copy is destroyed - nothing is ever deleted.</summary>
	private static void DropInventory(CompanionSetup item, string name)
	{
		try
		{
			Humanoid humanoid = item.GetComponent<Humanoid>();
			Inventory inventory = humanoid != null ? humanoid.GetInventory() : null;
			if (inventory == null || inventory.NrOfItems() == 0)
			{
				return;
			}
			Vector3 position = item.transform.position + Vector3.up * 0.6f;
			List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
			int dropped = 0;
			int skipped = 0;
			for (int i = 0; i < items.Count; i++)
			{
				ItemDrop.ItemData data = items[i];
				if (data == null)
				{
					continue;
				}
				// the same order of the drop order (CompanionOrders.Drop): spawn the world item first, then take it out
				// of the pack - an item with no world prefab stays where it is instead of vanishing
				ItemDrop drop = ItemDrop.DropItem(data, data.m_stack, position, Quaternion.identity);
				if (drop == null)
				{
					skipped++;
					continue;
				}
				inventory.RemoveItem(data);
				dropped++;
			}
			CompanionsPlugin.Log.LogInfo($"[Roster] \"{name}\": {dropped} item(s) of the old copy were dropped on the ground"
				+ (skipped > 0 ? $" ({skipped} had no world prefab)" : ""));
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Roster] Couldn't drop the old copy's items: " + ex.Message);
		}
	}

	// the game is saving your character (autosave, logging out, quitting) - save the companions with it
	internal static void OnProfileSave()
	{
		if (s_saving)
		{
			return;
		}
		try
		{
			if (ZNet.instance != null && ZNetScene.instance != null && EnsureLoaded())
			{
				SnapshotAll();
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Roster] Couldn't save the roster with the character: " + ex.Message);
		}
	}

	private static void SnapshotAll()
	{
		bool flag = false;
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			if (allCompanion._mhrStale || allCompanion._mhrDespawning || allCompanion._mhrDying || Time.time - allCompanion._mhrAwakeTime < LoadSettleSeconds || !IsMine(allCompanion, out var _, out var zdo))
			{
				continue;
			}
			Character component = allCompanion.GetComponent<Character>();
			if (!(component == null) && !(component.GetHealth() <= 0f))
			{
				string text = allCompanion.MHR_EnsureCompanionId();
				if (!string.IsNullOrEmpty(text) && (Find(text)?.Gen ?? 0) <= zdo.GetInt(GenHash))
				{
					Snapshot(allCompanion, zdo, text);
					flag = true;
				}
			}
		}
		if (flag)
		{
			Save(toDisk: false);
		}
	}

	private static void Snapshot(CompanionSetup c, ZDO zdo, string id)
	{
		Entry entry = Find(id);
		if (entry == null)
		{
			entry = new Entry
			{
				Id = id
			};
			s_entries.Add(entry);
			s_byId[id] = entry;
			CompanionsPlugin.Log.LogInfo(string.Format("[Roster] Added \"{0}\" to your companion roster for this world ({1} item(s))", zdo.GetString(CompanionSetup.NameHash, "Companion"), c.GetComponent<Humanoid>()?.GetInventory()?.NrOfItems() ?? 0));
		}
		entry.Gen = zdo.GetInt(GenHash);
		entry.Name = zdo.GetString(CompanionSetup.NameHash, ModLocalization.Loc("hc_msg_name_default"));
		GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
		entry.Prefab = ((prefab != null) ? prefab.name : CompanionTierData.Companion.PrefabName);
		entry.Appearance = zdo.GetString(CompanionSetup.AppearanceHash);
		entry.Owner = zdo.GetString(CompanionSetup.OwnerHash);
		entry.ActionMode = zdo.GetInt(CompanionSetup.ActionModeHash);
		entry.Schema = zdo.GetInt(CompanionSetup.ActionModeSchemaHash);
		entry.Follow = zdo.GetBool(CompanionSetup.FollowHash, defaultValue: true);
		entry.FollowPaused = zdo.GetBool(CompanionTombstoneResurrect.FollowPausedByJobHash);
		entry.StayHome = zdo.GetBool(CompanionSetup.StayHomeHash);
		entry.AutoPickup = zdo.GetBool(CompanionSetup.AutoPickupHash, defaultValue: true);
		entry.Wander = zdo.GetBool(CompanionSetup.WanderHash);
		entry.Commandable = zdo.GetInt(CompanionSetup.IsCommandableHash, 1);
		entry.Formation = zdo.GetInt(CompanionSetup.FormationSlotHash, -1);
		CompanionSkills component = c.GetComponent<CompanionSkills>();
		entry.Skills = ((component != null) ? component.SerializeSkills() : zdo.GetString(CompanionSkills.SkillsHash));
		entry.HasHome = c.HasHomePosition();
		entry.HomePos = (entry.HasHome ? c.GetHomePosition() : Vector3.zero);
		entry.CombatStance = zdo.GetInt(CompanionSetup.CombatStanceHash);
		entry.HasBed = zdo.GetBool(CompanionSetup.HasHomeBedHash);
		entry.BedPos = zdo.GetVec3(CompanionSetup.HomeBedPosHash, Vector3.zero);
		entry.FarmZones = zdo.GetString(FarmZoneSerializer.FarmZonesHash);
		Inventory inventory = c.GetComponent<Humanoid>()?.GetInventory();
		if (inventory != null)
		{
			ZPackage zPackage = new ZPackage();
			inventory.Save(zPackage);
			entry.Inventory = zPackage.GetBase64();
		}
		entry.LastPos = c.transform.position;
		entry.LastSeen = ZNet.instance.GetTimeSeconds();
		entry.Dead = false;
	}

	// called as the companion dies (before its tombstone is made) - keep the last snapshot's gear, just note the death
	internal static void OnDied(CompanionSetup c)
	{
		if (!EnsureLoaded() || !IsMine(c, out var _, out var zdo))
		{
			return;
		}
		Entry entry = Find(zdo.GetString(CompanionSetup.MHR_CompanionIdHash));
		if (entry != null)
		{
			entry.Dead = true;
			entry.LastPos = c.transform.position;
			entry.LastSeen = ZNet.instance.GetTimeSeconds();
			Save(toDisk: false);
		}
	}

	// the Despawn button removes a companion - drop it from the roster so it can't be "recovered"; 1.5.80: or, with
	// [General] RecoverUnsummoned, keep it (no gear - that's in the tombstone) as Unsummoned until the next session
	internal static void Forget(CompanionSetup c)
	{
		if (c._mhrStale || !EnsureLoaded() || !IsMine(c, out var _, out var zdo))
		{
			return;
		}
		Entry entry = Find(zdo.GetString(CompanionSetup.MHR_CompanionIdHash));
		if (entry != null)
		{
			if (ModConfig.RecoverUnsummoned?.Value ?? true)
			{
				entry.Unsummoned = true;
				entry.Inventory = "";
				entry.Gen++;
				s_sessionUnsummoned.Add(entry.Id);
				Save(toDisk: true);
				CompanionsPlugin.Log.LogInfo("[Roster] \"" + entry.Name + "\" was unsummoned - Odin's Shrine can bring it back (without gear) until you next load this world");
				return;
			}
			s_entries.Remove(entry);
			s_byId.Remove(entry.Id);
			Save(toDisk: true);
			CompanionsPlugin.Log.LogInfo("[Roster] \"" + entry.Name + "\" was despawned - removed from your companion roster");
		}
	}

	private static bool IsLoaded(string id)
	{
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			if (allCompanion._mhrStale)
			{
				continue;
			}
			ZDO zDO = allCompanion.GetComponent<ZNetView>()?.GetZDO();
			if (zDO != null && zDO.GetString(CompanionSetup.MHR_CompanionIdHash) == id)
			{
				Character component = allCompanion.GetComponent<Character>();
				s_where[id] = allCompanion.transform.position;
				if (component != null && component.GetHealth() > 0f)
				{
					return true;
				}
			}
		}
		return false;
	}

	// works out each companion's status. Single player and the host hold every object in the world, so they can tell
	// Alive, Dead (a tombstone that will bring it back) and Lost (nothing left). A client of a dedicated server only
	// holds the area around it, so a companion that isn't nearby is Unknown there.
	// 1.5.44: where each alive companion is (issue #16: it came back at its bed far away and the roster only said
	// "Alive" - no map pin, no answer to the command key), filled by GetStatuses
	private static readonly Dictionary<string, Vector3> s_where = new Dictionary<string, Vector3>();

	// "Meadows, 850 m from you"
	internal static string WhereText(Vector3 pos)
	{
		string biome = "";
		try
		{
			if (WorldGenerator.instance != null)
			{
				Heightmap.Biome b = WorldGenerator.instance.GetBiome(pos);
				biome = Localization.instance?.Localize("$biome_" + b.ToString().ToLowerInvariant()) ?? b.ToString();
			}
		}
		catch
		{
		}
		float dist = (Player.m_localPlayer != null) ? Vector3.Distance(Player.m_localPlayer.transform.position, pos) : 0f;
		return ModLocalization.LocFmt("hc_where", biome, dist.ToString("F0"));
	}

	internal static Dictionary<string, Status> GetStatuses()
	{
		Dictionary<string, Status> dictionary = new Dictionary<string, Status>();
		s_where.Clear();
		foreach (Entry s_entry in s_entries)
		{
			dictionary[s_entry.Id] = s_entry.Unsummoned ? Status.Unsummoned : (IsLoaded(s_entry.Id) ? Status.Alive : Status.Unknown);
		}
		if (!ZNet.instance.IsServer() || ZDOMan.instance == null || !(s_objectsById?.GetValue(ZDOMan.instance) is IDictionary dictionary2))
		{
			return dictionary;
		}
		HashSet<string> hashSet = new HashSet<string>();
		HashSet<string> hashSet2 = new HashSet<string>();
		foreach (object value in dictionary2.Values)
		{
			if (!(value is ZDO zDO))
			{
				continue;
			}
			string text = zDO.GetString(CompanionSetup.MHR_CompanionIdHash);
			if (text.Length > 0 && dictionary.ContainsKey(text))
			{
				if (zDO.GetInt(GenHash) >= Find(text).Gen)
				{
					hashSet.Add(text);
					s_where[text] = zDO.GetPosition();
				}
				continue;
			}
			text = zDO.GetString(CompanionSetup.MHR_TombCompanionIdHash);
			if (text.Length > 0 && dictionary.ContainsKey(text) && zDO.GetString(CompanionTombstoneResurrect.RespawnOwnerHash).Length > 0 && zDO.GetInt(RespawnGenHash) >= Find(text).Gen)
			{
				hashSet2.Add(text);
			}
		}
		foreach (Entry s_entry2 in s_entries)
		{
			if (dictionary[s_entry2.Id] != Status.Alive && dictionary[s_entry2.Id] != Status.Unsummoned)
			{
				dictionary[s_entry2.Id] = (hashSet.Contains(s_entry2.Id) ? Status.Alive : (hashSet2.Contains(s_entry2.Id) ? Status.Dead : Status.Lost));
			}
		}
		return dictionary;
	}

	private static readonly Dictionary<string, KeyValuePair<bool, float>> s_goneCache = new Dictionary<string, KeyValuePair<bool, float>>();

	private static readonly HashSet<string> s_goneLogged = new HashSet<string>();

	// true only when this game holds the whole world (single player / host) and no companion or respawning tombstone
	// with this ID is in it. A client of a dedicated server can't tell, so it says false.
	internal static bool CompanionGone(string id)
	{
		if (string.IsNullOrEmpty(id) || ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
		{
			return false;
		}
		if (s_goneCache.TryGetValue(id, out var value) && Time.unscaledTime - value.Value < 120f)
		{
			return value.Key;
		}
		bool flag = true;
		if (s_objectsById?.GetValue(ZDOMan.instance) is IDictionary dictionary)
		{
			foreach (object value2 in dictionary.Values)
			{
				if (value2 is ZDO zDO && (zDO.GetString(CompanionSetup.MHR_CompanionIdHash) == id || (zDO.GetString(CompanionSetup.MHR_TombCompanionIdHash) == id && zDO.GetString(CompanionTombstoneResurrect.RespawnOwnerHash).Length > 0)))
				{
					flag = false;
					break;
				}
			}
		}
		else
		{
			flag = false;
		}
		s_goneCache[id] = new KeyValuePair<bool, float>(flag, Time.unscaledTime);
		if (flag && s_goneLogged.Add(id))
		{
			CompanionsPlugin.Log.LogInfo("[Rest] A bed is claimed by a companion that no longer exists (ID " + id + ") - the claim is ignored");
		}
		return flag;
	}

	// the status name and the notes are localized; a status with no key falls back to its own name
	private static string StatusName(Status s)
	{
		return ModLocalization.LocOr("hc_roster_" + s.ToString().ToLowerInvariant(), s.ToString());
	}

	internal static string Describe(Entry e, Status s)
	{
		string text = e.Name + ": " + StatusName(s);
		switch (s)
		{
		case Status.Alive:
			if (s_where.TryGetValue(e.Id, out var pos))
			{
				text += " (" + WhereText(pos) + ")";
			}
			break;
		case Status.Dead:
			text += ModLocalization.Loc("hc_roster_dead_note");
			break;
		case Status.Lost:
			text += ModLocalization.Loc("hc_roster_lost_note");
			break;
		case Status.Unknown:
			text += ModLocalization.LocFmt(e.Dead ? "hc_roster_unknown_died" : "hc_roster_unknown_seen", e.LastPos.x.ToString("F0"), e.LastPos.z.ToString("F0"));
			break;
		case Status.Unsummoned:
			text += " (unsummoned - Odin's Shrine can bring it back without its gear until you next load this world)";
			break;
		}
		return text;
	}

	private static void ShowRoster()
	{
		if (s_entries.Count == 0)
		{
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.Loc("hc_roster_empty"));
			CompanionsPlugin.Log.LogInfo("[Roster] No companions on the roster for this character in this world yet");
			return;
		}
		Dictionary<string, Status> statuses = GetStatuses();
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Entry s_entry in s_entries)
		{
			string text = Describe(s_entry, statuses[s_entry.Id]);
			stringBuilder.Append(text).Append('\n');
			CompanionsPlugin.Log.LogInfo("[Roster] " + text);
		}
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, stringBuilder.ToString().TrimEnd('\n'));
	}

	// "Unknown" only means "gone" where this game holds the whole world. A client of a dedicated server sees every
	// companion outside the loaded area as Unknown, and Recover creates a whole new body with the same
	// HC_CompanionId - the original can still be alive on the server, so recovering there duplicates the companion.
	private static bool UnknownMeansGone()
	{
		ZNet znet = ZNet.instance;
		return znet == null || znet.IsServer();
	}

	// brings back the first Lost companion; an Unknown one needs the button pressed a second time to confirm
	private static void RecoverNext()
	{
		if (s_entries.Count == 0)
		{
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.Loc("hc_roster_empty"));
			return;
		}
		Dictionary<string, Status> statuses = GetStatuses();
		Entry entry = null;
		foreach (Entry s_entry in s_entries)
		{
			if (statuses[s_entry.Id] == Status.Lost)
			{
				entry = s_entry;
				break;
			}
		}
		if (entry == null && !UnknownMeansGone())
		{
			// a client of a dedicated server only holds the area around it, so an Unknown companion there may be
			// perfectly alive somewhere else - instantiating a second body would duplicate it. The host (or a
			// single-player world) is the one that can tell, so this is refused here and the message below says
			// there is nothing lost to recover.
			foreach (Entry unknownEntry in s_entries)
			{
				if (statuses[unknownEntry.Id] == Status.Unknown)
				{
					CompanionsPlugin.Log.LogInfo("[Roster] recover skipped: \"" + unknownEntry.Name + "\" is Unknown on a dedicated client (ask the server)");
				}
			}
		}
		if (entry == null && UnknownMeansGone())
		{
			foreach (Entry s_entry2 in s_entries)
			{
				if (statuses[s_entry2.Id] == Status.Unknown)
				{
					if (s_confirmId != s_entry2.Id || Time.unscaledTime > s_confirmUntil)
					{
						s_confirmId = s_entry2.Id;
						s_confirmUntil = Time.unscaledTime + ConfirmSeconds;
						string text = ModLocalization.LocFmt("hc_roster_unknown_confirm", s_entry2.Name, ConfirmSeconds.ToString("F0"));
						MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, text);
						CompanionsPlugin.Log.LogInfo("[Roster] " + text);
						return;
					}
					entry = s_entry2;
					break;
				}
			}
		}
		s_confirmId = null;
		if (entry == null)
		{
			string text2 = ModLocalization.Loc("hc_roster_none_lost");
			foreach (Entry s_entry3 in s_entries)
			{
				text2 = text2 + "\n" + Describe(s_entry3, statuses[s_entry3.Id]);
			}
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, text2);
			CompanionsPlugin.Log.LogInfo("[Roster] Recover: nothing to recover");
			return;
		}
		Recover(entry, statuses[entry.Id]);
	}

	// the Companion Shrine's list: every companion on the roster with its status
	internal static List<KeyValuePair<Entry, Status>> GetRoster()
	{
		List<KeyValuePair<Entry, Status>> list = new List<KeyValuePair<Entry, Status>>();
		if (ZNet.instance == null || ZNetScene.instance == null || !EnsureLoaded())
		{
			return list;
		}
		Dictionary<string, Status> statuses = GetStatuses();
		foreach (Entry s_entry in s_entries)
		{
			list.Add(new KeyValuePair<Entry, Status>(s_entry, statuses[s_entry.Id]));
		}
		return list;
	}

	// the Companion Shrine's Recover button (after the player confirmed) - checks the status again first
	internal static bool RecoverAt(Entry e, Vector3 near)
	{
		if (e == null || ZNet.instance == null || ZNetScene.instance == null || !EnsureLoaded() || !s_entries.Contains(e))
		{
			return false;
		}
		Status status = GetStatuses()[e.Id];
		if (status == Status.Unknown && !UnknownMeansGone())
		{
			// dedicated client: the companion is only outside the loaded area here, the server may still have it
			// alive - creating another body would duplicate it (see UnknownMeansGone)
			CompanionsPlugin.Log.LogInfo("[Roster] recover skipped: \"" + e.Name + "\" is Unknown on a dedicated client (ask the server)");
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, Describe(e, status));
			return false;
		}
		if (status != Status.Lost && status != Status.Unknown && status != Status.Unsummoned)
		{
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, Describe(e, status));
			return false;
		}
		Recover(e, status, near);
		return true;
	}

	private static void Recover(Entry e, Status status, Vector3? near = null)
	{
		Player localPlayer = Player.m_localPlayer;
		GameObject prefab = ZNetScene.instance.GetPrefab(e.Prefab);
		if (prefab == null)
		{
			CompanionsPlugin.Log.LogError("[Roster] Recover: prefab not found: " + e.Prefab);
			return;
		}
		Vector3 vector = CompanionManager.FindSpawnPosition(near ?? localPlayer.transform.position, 3f);
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab, vector, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
		ZDO zDO = gameObject.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null)
		{
			CompanionsPlugin.Log.LogError("[Roster] Recover: spawn failed - no ZDO");
			UnityEngine.Object.Destroy(gameObject);
			return;
		}
		int num = e.Gen + 1;
		int num2 = 0;
		zDO.Set(CompanionSetup.AppearanceHash, e.Appearance);
		zDO.Set(CompanionSetup.OwnerHash, e.Owner);
		zDO.Set(CompanionSetup.NameHash, e.Name);
		zDO.Set(CompanionSetup.MHR_CompanionIdHash, e.Id);
		zDO.Set(GenHash, num);
		zDO.Set(CompanionSetup.FollowHash, e.Follow);
		zDO.Set(CompanionTombstoneResurrect.FollowPausedByJobHash, e.FollowPaused);
		zDO.Set(CompanionSetup.ActionModeHash, e.ActionMode);
		zDO.Set(CompanionSetup.ActionModeSchemaHash, e.Schema);
		zDO.Set(CompanionSetup.StayHomeHash, e.StayHome);
		zDO.Set(CompanionSetup.AutoPickupHash, e.AutoPickup);
		zDO.Set(CompanionSetup.WanderHash, e.Wander);
		zDO.Set(CompanionSetup.IsCommandableHash, e.Commandable);
		zDO.Set(CompanionSetup.FormationSlotHash, e.Formation);
		zDO.Set(CompanionSetup.CombatStanceHash, e.CombatStance);
		if (e.HasHome)
		{
			zDO.Set(CompanionSetup.HomePosHash, e.HomePos);
			zDO.Set(CompanionSetup.HomePosSetHash, value: true);
		}
		if (!string.IsNullOrEmpty(e.Skills))
		{
			zDO.Set(CompanionSkills.SkillsHash, e.Skills);
		}
		zDO.Set(CompanionSetup.HasHomeBedHash, e.HasBed);
		zDO.Set(CompanionSetup.HomeBedPosHash, e.BedPos);
		if (!string.IsNullOrEmpty(e.FarmZones))
		{
			zDO.Set(FarmZoneSerializer.FarmZonesHash, e.FarmZones);
		}
		zDO.Persistent = true;
		if (!string.IsNullOrEmpty(e.Inventory))
		{
			Inventory inventory = gameObject.GetComponent<Humanoid>()?.GetInventory();
			if (inventory != null)
			{
				inventory.Load(new ZPackage(e.Inventory));
				num2 = inventory.NrOfItems();
				foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
				{
					allItem.m_equipped = false;
				}
				inventory.m_onChanged?.Invoke();
			}
		}
		gameObject.GetComponent<CompanionSetup>()?.ApplyAppearance(CompanionAppearance.Deserialize(e.Appearance));
		Character component = gameObject.GetComponent<Character>();
		if (component != null)
		{
			component.m_name = e.Name;
		}
		CompanionAI component2 = gameObject.GetComponent<CompanionAI>();
		if (component2 != null)
		{
			if (e.HasHome)
			{
				component2.SetPatrolPointAt(e.HomePos);
			}
			if (e.StayHome && e.HasHome)
			{
				component2.SetFollowTarget(null);
			}
			else if (e.Follow)
			{
				component2.SetFollowTarget(localPlayer.gameObject);
			}
		}
		e.Gen = num;
		e.Dead = false;
		e.Unsummoned = false;
		s_sessionUnsummoned.Remove(e.Id);
		e.LastPos = vector;
		e.LastSeen = ZNet.instance.GetTimeSeconds();
		Save(toDisk: true);
		CompanionTombstoneResurrect.PlayResurrectEffects(vector);
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_roster_recovered", e.Name));
		CompanionsPlugin.Log.LogInfo($"[Roster] Recovered \"{e.Name}\" ({status}) at {vector:F1} as copy {num}, with {num2} item(s) from the last roster save");
	}
}
