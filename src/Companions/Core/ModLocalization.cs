using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace Companions;

[HarmonyPatch]
public static class ModLocalization
{
	private static readonly FieldInfo TranslationsField = AccessTools.Field(typeof(Localization), "m_translations");

	private static Dictionary<string, string> _englishDefaults;

	private static string _translationsDir;

	private static bool _initialized;

	private static string _lastInjectedLanguage;

	private static readonly Regex EntryRegex = new Regex("\"key\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"[^\"]*\"value\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

	private static readonly FieldInfo InstanceField = AccessTools.Field(typeof(Localization), "m_instance");

	public static string Loc(string key)
	{
		if (Localization.instance == null)
		{
			return key;
		}
		return Localization.instance.Localize("$" + key);
	}

	public static string LocFmt(string key, params object[] args)
	{
		string text = Loc(key);
		try
		{
			return string.Format(text, args);
		}
		catch
		{
			return text;
		}
	}

	public static void Init()
	{
		string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		_translationsDir = Path.Combine(directoryName, "Translations");
		BuildEnglishDefaults();
		_initialized = true;
		if (InstanceField?.GetValue(null) is Localization loc)
		{
			CompanionsPlugin.Log.LogInfo("[Localization] Localization already initialized — injecting retroactively");
			InjectTranslations(loc);
		}
	}

	private static void BuildEnglishDefaults()
	{
		_englishDefaults = new Dictionary<string, string>
		{
			{ "hc_ui_title_companions", "Companions" },
			{ "hc_ui_title_choose", "Choose Your Companion" },
			{ "hc_ui_btn_spawn", "Spawn Companion" },
			{ "hc_ui_btn_buy", "Buy {0} ({1})" },
			{ "hc_ui_btn_need", "Need {0}" },
			{ "hc_ui_bank", "Bank: {0}" },
			{ "hc_ui_label_type", "Type" },
			{ "hc_ui_label_gender", "Gender" },
			{ "hc_ui_label_hair", "Hair Style" },
			{ "hc_ui_label_beard", "Beard Style" },
			{ "hc_ui_label_skin", "Skin Tone" },
			{ "hc_ui_label_hairtone", "Hair Tone" },
			{ "hc_ui_label_hairshade", "Hair Shade" },
			{ "hc_ui_label_subtype", "Sub-Type" },
			{ "hc_ui_btn_male", "Male" },
			{ "hc_ui_btn_female", "Female" },
			{ "hc_ui_btn_companion", "Companion" },
			{ "hc_ui_btn_dverger", "Dverger" },
			{ "hc_ui_beard_none", "None" },
			{ "hc_ui_desc_starter", "A loyal companion will join you on your journey. Customise their appearance on the left, then spawn them into the world.\n\nThey will follow you, fight by your side, and carry supplies. Equip them with gear and keep them fed to stay battle-ready.\n\nPress Escape to skip — the panel will appear again next session." },
			{ "hc_ui_desc_trader", "Hire a loyal companion to join your journey. They will follow you across the world and fight by your side against any threat.\n\nEach companion has their own health, stamina and inventory. You will need to equip them with gear and keep them fed to stay battle-ready.\n\nInteract with your companion to open their panel, where you can manage their equipment and issue commands.\n\nCustomise their appearance on the left, then confirm your purchase." },
			{ "hc_ui_placeholder_name", "Enter name..." },
			{ "hc_ui_label_food", "Food" },
			{ "hc_radial_follow", "Follow" },
			{ "hc_radial_wood", "Wood" },
			{ "hc_radial_stone", "Stone" },
			{ "hc_radial_ore", "Ore" },
			{ "hc_radial_forage", "Forage" },
			{ "hc_radial_smelt", "Smelt" },
			{ "hc_radial_pending", "Pending" },
			{ "hc_radial_sethome", "Set Home" },
			{ "hc_radial_wander", "Wander" },
			{ "hc_radial_pickup", "Pickup" },
			{ "hc_radial_command", "Command" },
			{ "hc_radial_balanced", "Balanced" },
			{ "hc_radial_aggressive", "Aggressive" },
			{ "hc_radial_defensive", "Defensive" },
			{ "hc_radial_passive", "Passive" },
			{ "hc_radial_melee", "Melee" },
			{ "hc_radial_ranged", "Ranged" },
			{ "hc_radial_hunt", "Hunt" },
			{ "hc_radial_farm", "Farm" },
			{ "hc_radial_fish", "Fish" },
			{ "hc_radial_cook", "Cook" },
			{ "hc_radial_repair", "Repair and Restock" },
			{ "hc_radial_despawn", "Despawn" },
			{ "hc_despawn_popup_title", "Despawn Companion" },
			{ "hc_despawn_popup_text", "Permanently remove {0}? Anything they're carrying will be left in a tombstone." },
			{ "hc_radial_active", "ACTIVE" },
			{ "hc_radial_on", "ON" },
			{ "hc_radial_off", "OFF" },
			{ "hc_hover_inventory", "Inventory" },
			{ "hc_hover_commands", "Commands" },
			{ "hc_msg_not_yours", "This is not your companion." },
			{ "hc_msg_need_coins", "Not enough coins in bank! Need {0}" },
			{ "hc_msg_arrived", "Your new {0} has arrived!" },
			{ "hc_msg_returned", "{0} has returned at {1}!" },
			{ "hc_msg_location_home", "home" },
			{ "hc_msg_location_spawn", "the world spawn" },
			{ "hc_msg_starter_joined", "A companion has joined you on your journey!" },
			{ "hc_msg_name_default", "Companion" },
			{ "hc_menu_mod_options", "Mod Options" },
			{ "hc_minimap_grave", "Grave" },
			{ "hc_msg_sleep_waiting", "Waiting for {0} to sleep..." },
			{ "hc_msg_sleep_waiting_generic", "Waiting for companions to sleep..." },
			{ "hc_msg_rested", "{0} is Rested (Comfort: {1})" },
			{ "hc_msg_rested_suffix", "Rested" },
			{ "hc_msg_tools_weak", "{0}'s tools are not strong enough" },
			{ "hc_msg_reposition_on", "UI Reposition Mode — drag to move, F7 to confirm" },
			{ "hc_msg_reposition_off", "UI position saved" },
			{ "hc_msg_commanded", "Moving to position." },
			{ "hc_speech_repair_start", "Time for repairs." },
			{ "hc_speech_repair_done", "All fixed up!" },
			{ "hc_speech_smelt_monitoring", "Everything's running. I'll keep watch." },
			{ "hc_speech_smelt_done", "All done smelting." },
			{ "hc_speech_smelt_fuel", "Fetching fuel." },
			{ "hc_speech_smelt_materials", "Fetching materials." },
			{ "hc_speech_homestead_refuel", "Time to refuel." },
			{ "hc_speech_homestead_stoked", "Fire's stoked." },
			{ "hc_speech_homestead_repair", "I'll patch that up." },
			{ "hc_speech_homestead_repaired", "Good as new." },
			{ "hc_speech_homestead_tidy", "Let me tidy up." },
			{ "hc_speech_homestead_tidied", "All tidied up." },
			{ "hc_speech_homestead_cooked", "Something tasty!" },
			{ "hc_speech_repair_buildings_done", "Everything's patched up." },
			{ "hc_speech_repair_nothing", "Nothing needs repair." },
			{ "hc_speech_repair_no_hammer", "I need a hammer for that." },
			{ "hc_speech_restock_done", "Fires are stoked." },
			{ "hc_speech_restock_nothing", "Nothing needs fuel." },
			{ "hc_speech_upkeep_nothing", "Everything's in order." },
			{ "hc_idle_why_0", "Nothing to do right now." },
			{ "hc_idle_why_1", "Nothing left to chop here." },
			{ "hc_idle_why_2", "Nothing left to mine around here." },
			{ "hc_idle_why_3", "No ore around here." },
			{ "hc_idle_why_5", "Nothing to pick right now." },
			{ "hc_idle_why_6", "The smelters are busy - I'll wait." },
			{ "hc_idle_why_7", "No game around." },
			{ "hc_idle_why_8", "Waiting for the crops to grow." },
			{ "hc_idle_why_9", "The fish aren't biting." },
			{ "hc_idle_why_15", "Everything's in order." },
			{ "hc_idle_why_17", "Nothing to cook right now." },
			{ "hc_idle_chat_1", "Fine day for it." },
			{ "hc_idle_chat_2", "Need anything?" },
			{ "hc_idle_chat_3", "I could use a break." },
			{ "hc_idle_chat_4", "Quiet around here." },
			{ "hc_idle_chat_5", "Smells like rain." },
			{ "hc_idle_chat_6", "Anything need doing?" },
			{ "hc_speech_farm_planting", "Time to plant some seeds." },
			{ "hc_speech_farm_harvesting", "Crops are ready!" },
			{ "hc_speech_farm_done", "All done farming." },
			{ "hc_speech_farm_no_seeds", "I need seeds to plant." },
			{ "hc_speech_farm_no_cultivator", "I need a cultivator to plant." },
			{ "hc_speech_no_chest", "No chest nearby to unload!" },
			{ "hc_speech_no_bow", "I need a bow and arrows to shoot!" },
			{ "hc_speech_overweight", "My back is hurting from all this weight!" },
			{ "hc_speech_cook_cooking", "Let me cook something." },
			{ "hc_speech_cook_brewing", "Brewing a batch." },
			{ "hc_speech_cook_fermenter", "Into the fermenter." },
			{ "hc_speech_cook_tapping", "This batch is ready!" },
			{ "hc_speech_cook_storing", "Putting these away." },
			{ "hc_speech_fish_need_rod", "I need a fishing rod." },
			{ "hc_speech_broke_axe", "My axe broke. I can fix it when we get home." },
			{ "hc_speech_broke_pickaxe", "My pickaxe broke. I can fix it when we get home." },
			{ "hc_speech_broke_tool", "My tool broke. I can fix it when we get home." },
			{ "hc_speech_unload", "I need to empty my bags first." },
			{ "hc_speech_need_axe", "I need an axe for that." },
			{ "hc_speech_need_pickaxe", "I need a pickaxe for that." },
			{ "hc_speech_need_arrows", "I need arrows." },
			{ "hc_speech_plenty_wood", "We've got plenty of wood." },
			{ "hc_speech_plenty_stone", "We've got plenty of stone." },
			{ "hc_speech_plenty_ore", "We've got plenty of ore." },
			{ "hc_speech_fish_need_bait", "I need bait to fish." },
			{ "hc_speech_fish_no_water", "I can't find water nearby..." },
			{ "hc_speech_fish_got_away", "It got away!" },
			{ "hc_speech_fish_caught", "I caught a {0}!" },
			{ "hc_speech_fish_full", "My bags are full!" },
			{ "hc_speech_tomb_found", "My belongings!" },
			{ "hc_speech_tomb_recovered", "Got everything back." },
			{ "hc_speech_tomb_empty", "Nothing left to take." },
			{ "hc_speech_deposit_done", "All stowed away." },
			{ "hc_speech_deposit_empty", "Nothing to deposit." },
			{ "hc_cmd_comehere_1", "Coming!" },
			{ "hc_cmd_comehere_2", "On my way back!" },
			{ "hc_cmd_comehere_3", "Right behind you." },
			{ "hc_cmd_attack_1", "On it!" },
			{ "hc_cmd_attack_2", "Going in!" },
			{ "hc_cmd_attack_3", "I'll take them down!" },
			{ "hc_cmd_attack_4", "For Odin!" },
			{ "hc_cmd_cart_pull_1", "I'll haul this." },
			{ "hc_cmd_cart_pull_2", "Got the cart!" },
			{ "hc_cmd_cart_pull_3", "Let me pull." },
			{ "hc_cmd_cart_release_1", "Letting go." },
			{ "hc_cmd_cart_release_2", "Cart's free." },
			{ "hc_cmd_cart_release_3", "Released!" },
			{ "hc_cmd_door_1", "Getting the door." },
			{ "hc_cmd_door_2", "I'll get it." },
			{ "hc_cmd_door_3", "Door's open!" },
			{ "hc_cmd_sit_1", "Nice and warm." },
			{ "hc_cmd_sit_2", "Good spot to rest." },
			{ "hc_cmd_sit_3", "I'll sit here." },
			{ "hc_cmd_sleep_1", "Time for some rest." },
			{ "hc_cmd_sleep_2", "I could use some sleep." },
			{ "hc_cmd_sleep_3", "Wake me if you need me." },
			{ "hc_cmd_wake_1", "I'm up!" },
			{ "hc_cmd_wake_2", "Already?" },
			{ "hc_cmd_wake_3", "Right, let's go." },
			{ "hc_cmd_notmybed_1", "That's not my bed." },
			{ "hc_cmd_notmybed_2", "That one's already taken." },
			{ "hc_cmd_notmybed_3", "That's not mine to use." },
			{ "hc_cmd_deposit_1", "Dropping off my haul." },
			{ "hc_cmd_deposit_2", "Storing the goods." },
			{ "hc_cmd_deposit_3", "Lightening my load." },
			{ "hc_cmd_deposit_empty_1", "I've got nothing to drop off." },
			{ "hc_cmd_deposit_empty_2", "Already empty." },
			{ "hc_cmd_harvest_1", "I'll get that." },
			{ "hc_cmd_harvest_2", "On it!" },
			{ "hc_cmd_harvest_3", "Looks like good stuff." },
			{ "hc_cmd_cancel_1", "Standing by." },
			{ "hc_cmd_cancel_2", "Awaiting orders." },
			{ "hc_cmd_cancel_3", "Ready when you are." },
			{ "hc_cmd_move_1", "Heading over." },
			{ "hc_cmd_move_2", "On my way." },
			{ "hc_cmd_move_3", "Moving out." },
			{ "hc_cmd_repair_1", "I'll fix my gear up." },
			{ "hc_cmd_repair_2", "Time for repairs." },
			{ "hc_cmd_repair_3", "This needs some work." },
			{ "hc_cmd_board_1", "Coming aboard!" },
			{ "hc_cmd_board_2", "All aboard!" },
			{ "hc_cmd_board_3", "I'll hop on." },
			{ "hc_cmd_repair_nothing_1", "Nothing to fix here." },
			{ "hc_cmd_repair_nothing_2", "My gear's fine." },
			{ "hc_cmd_repair_nothing_3", "No repairs needed." },
			{ "hc_cmd_tombstone_1", "I'll grab my things." },
			{ "hc_cmd_tombstone_2", "Let me get that." },
			{ "hc_cmd_tombstone_3", "On my way to pick that up." },
			{ "hc_cmd_smelt_1", "I'll keep the fires burning." },
			{ "hc_cmd_smelt_2", "Time to smelt." },
			{ "hc_cmd_smelt_3", "I'll tend the furnace." },
			{ "hc_radial_farmzones", "Farm Zones" },
			{ "hc_farmzone_title", "Select Crop" },
			{ "hc_farmzone_any", "Any Crop" },
			{ "hc_farmzone_hint", "[LMB] Place  [RMB] Remove  [Scroll] Resize  [RMB+Scroll] Rotate  [Esc] Exit" },
			{ "hc_farmzone_hint_gamepad", "[A] Place  [X] Remove  [Bumpers] Resize  [D-Pad] Rotate  [Start] Exit" },
			{ "hc_farmzone_full", "Maximum zones reached (4)" },
			{ "hc_ui_settings_title", "Companion Settings" },
			{ "hc_ui_press_key", "Press a key..." },
			{ "hc_ui_default", "Default" },
			{ "hc_ui_home_zone", "Home Zone" },
			{ "hc_ui_home_zone_suffix", " — Home Zone" },
			{ "hc_ui_home_radius", "Radius:" },
			{ "hc_ui_home_zone_fmt", "Home zone: {0}m" },
			{ "hc_ui_homezone_hint", "Drag slider  •  Esc to close" },
			{ "hc_ui_homezone_hint_gamepad", "[D-Pad] Adjust   [A] Confirm   [B] Close" },
			{ "hc_hud_health_fmt", "Health  {0} / {1}" },
			{ "hc_hud_stamina_fmt", "Stamina  {0} / {1}" },
			{ "hc_msg_recall_all", "Recalling all companions" },
			{ "hc_hover_bed", "{0}'s Bed" },
			{ "hc_hover_resurrect", "Hold to resurrect" },
			{ "hc_hover_resurrecting", "Resurrecting... {0}%" },
			{ "hc_shrine_name", "Odin's Shrine" },
			{ "hc_shrine_desc", "Shows your companions and whether they're alive, and brings back one that's been lost." },
			{ "hc_shrine_hover", "Your companions" },
			{ "hc_shrine_empty", "No companions on your roster in this world yet. A companion joins the roster once it has been near you for a few seconds." },
			{ "hc_shrine_none_to_recover", "None of your companions needs bringing back." },
			{ "hc_shrine_bring_back", "Bring {0} back here? It returns with its gear and settings from the last roster save. If the old {0} turns up later, it will be removed." },
			{ "hc_shrine_warning", "Warning:" },
			{ "hc_shrine_unknown_alive", "{0} may still be alive somewhere that isn't loaded." },
			{ "hc_shrine_unknown_dead", "{0} may still be alive somewhere that isn't loaded (last known: it died, and its tombstone keeps its items too)." },
			{ "hc_roster_alive", "Alive" },
			{ "hc_roster_dead", "Dead" },
			{ "hc_roster_lost", "Lost" },
			{ "hc_roster_unknown", "Unknown" },
			{ "hc_roster_unknown_note", " (not nearby - it may still be alive)" },
			{ "hc_roster_dead_note", " (its tombstone will bring it back)" },
			{ "hc_roster_lost_note", " (nothing left of it in the world - it can be recovered)" },
			{ "hc_roster_unknown_died", " (last known: died near {0}, {1})" },
			{ "hc_roster_unknown_seen", " (last seen near {0}, {1})" },
			{ "hc_roster_empty", "No companions on your roster in this world yet." },
			{ "hc_roster_none_lost", "No lost companions to recover." },
			{ "hc_roster_recovered", "{0} has been recovered!" },
			{ "hc_roster_old_copy", "An old copy of {0} was found and removed ({0} was recovered)." },
			{ "hc_roster_unknown_confirm", "{0}'s status is Unknown - it may still be alive somewhere that isn't loaded. Turn RecoverLostCompanion on again within {1} s to recover it anyway. If the old {0} turns up later, it will be removed." },
			{ "hc_cat_general", "General" },
			{ "hc_cat_controls", "Controls" },
			{ "hc_cat_follow", "Follow" },
			{ "hc_cat_movement", "Movement" },
			{ "hc_cat_combat", "Combat" },
			{ "hc_cat_food", "Food" },
			{ "hc_cat_stamina", "Stamina" },
			{ "hc_cat_rest", "Rest" },
			{ "hc_cat_death", "Death" },
			{ "hc_cat_looting", "Looting" },
			{ "hc_cat_gearrepair", "GearRepair" },
			{ "hc_cat_gathering", "Gathering" },
			{ "hc_cat_hunting", "Hunting" },
			{ "hc_cat_fishing", "Fishing" },
			{ "hc_cat_farming", "Farming" },
			{ "hc_cat_smelting", "Smelting" },
			{ "hc_cat_cooking", "Cooking" },
			{ "hc_cat_repairandrestock", "Repair and Restock" },
			{ "hc_cat_idle", "Idle" },
			{ "hc_cat_speech", "Speech" },
			{ "hc_cat_advanced", "Advanced" },
			// Fase 3.1: orders typed in the chat, understood by a local LLM
			{ "hc_cat_ia", "AI" },
			{ "hc_ai_thinking", "Thinking..." },
			{ "hc_ai_not_understood", "I didn't understand that order." },
			{ "hc_ai_no_companion", "None of your companions is close enough to take that order." },
			{ "hc_ai_disabled", "Companion AI is off ([IA] Ligado = false)." },
			{ "hc_ai_error", "The AI didn't answer: {0}" },
			{ "hc_ai_raw", "AI: {0}" },
			{ "hc_ai_ok", "Ok: {0}" },
			{ "hc_ai_ok_amount", "Ok: {0} ({1})" },
			// Fase 3.2c: the pack already has what the order asked for - the order is not started
			{ "hc_ai_already_have", "We already have enough." },
			{ "hc_ai_order_follow", "follow me" },
			{ "hc_ai_order_wood", "gather wood" },
			{ "hc_ai_order_stone", "gather stone" },
			{ "hc_ai_order_ore", "mine ore" },
			{ "hc_ai_order_forage", "gather plants" },
			{ "hc_ai_order_smelt", "smelt ore" },
			{ "hc_ai_order_hunt", "hunt" },
			{ "hc_ai_order_farm", "work the farm" },
			{ "hc_ai_order_fish", "fish" },
			{ "hc_ai_order_repair", "repair and restock" },
			{ "hc_ai_order_cook", "cook" },
			{ "hc_ai_order_stop", "stop and follow me" },
			{ "hc_ai_order_come", "come here" },
			{ "hc_ai_order_sethome", "set home here" },
			{ "hc_ai_order_wander", "wander around" },
			{ "hc_ai_order_nowander", "stop wandering" },
			{ "hc_ai_order_pickup", "pick up items" },
			{ "hc_ai_order_nopickup", "stop picking up items" },
			{ "hc_ai_order_command", "take orders" },
			{ "hc_ai_order_nocommand", "ignore orders" },
			{ "hc_ai_order_stance_balanced", "balanced stance" },
			{ "hc_ai_order_stance_passive", "defensive stance" },
			{ "hc_ai_order_stance_ranged", "ranged stance" },
			{ "hc_ai_order_stance_melee", "melee stance" },
			// Fase 3.6: orders that name a creature or an item (attack/equip/drop) have their own wording, so the
			// answer says which creature or which item it was about
			{ "hc_ai_order_attack", "attack the target" },
			{ "hc_ai_order_equip", "equip the item" },
			{ "hc_ai_order_drop", "drop the item" },
			{ "hc_ai_attacking", "Attacking {0}." },
			{ "hc_ai_no_target", "I don't see any {0} nearby." },
			{ "hc_ai_no_aim", "I don't see a creature in your sights." },
			{ "hc_ai_equipped", "Equipped {0}." },
			{ "hc_ai_no_item", "I don't have {0}." },
			{ "hc_ai_not_equipable", "I can't equip {0}." },
			{ "hc_ai_dropped", "Dropped {0} ({1})." },
			{ "hc_ai_need_item", "I didn't catch which item." },
			// Fase 3.5: personality presets (shown in the log), free conversation and the memory
			{ "hc_ai_default_name", "Companion" },
			{ "hc_ai_preset_default", "Default" },
			{ "hc_ai_preset_custom", "Free text" },
			{ "hc_ai_preset_viking", "Loyal Viking" },
			{ "hc_ai_preset_mago", "Sarcastic Mage" },
			{ "hc_ai_preset_sarcastico", "Sarcastic" },
			{ "hc_ai_preset_guia", "Patient Guide" }
		};
	}

	// Translation for a key that is registered, otherwise the caller's own fallback. Used for text that has to show
	// something sensible (a category name, a setting name) even when a key is missing.
	public static string LocOr(string key, string fallback)
	{
		if (_englishDefaults != null && _englishDefaults.ContainsKey(key))
		{
			return Loc(key);
		}
		return fallback;
	}

	private static void InjectTranslations(Localization loc)
	{
		if (loc == null)
		{
			return;
		}
		string selectedLanguage = loc.GetSelectedLanguage();
		if (selectedLanguage == _lastInjectedLanguage)
		{
			return;
		}
		_lastInjectedLanguage = selectedLanguage;
		if (!(TranslationsField?.GetValue(loc) is Dictionary<string, string> dictionary))
		{
			return;
		}
		if (_englishDefaults != null)
		{
			foreach (KeyValuePair<string, string> englishDefault in _englishDefaults)
			{
				dictionary[englishDefault.Key] = englishDefault.Value;
			}
		}
		Dictionary<string, string> dictionary2 = LoadTranslationFile((selectedLanguage != "English") ? selectedLanguage : null);
		if (dictionary2 == null && selectedLanguage != "English")
		{
			dictionary2 = LoadTranslationFile("English");
		}
		int num = 0;
		if (dictionary2 != null)
		{
			foreach (KeyValuePair<string, string> item in dictionary2)
			{
				dictionary[item.Key] = item.Value;
				num++;
			}
		}
		CompanionsPlugin.Log.LogInfo($"[Localization] Injected {_englishDefaults?.Count ?? 0} defaults + {num} \"{selectedLanguage}\" overrides");
	}

	public static void EnsureDefaultFile()
	{
		if (_englishDefaults == null)
		{
			return;
		}
		try
		{
			if (!Directory.Exists(_translationsDir))
			{
				Directory.CreateDirectory(_translationsDir);
			}
			string text = Path.Combine(_translationsDir, "English.json");
			if (File.Exists(text))
			{
				string json = File.ReadAllText(text);
				Dictionary<string, string> dictionary = ParseTranslationJson(json);
				if (dictionary != null && dictionary.Count >= _englishDefaults.Count)
				{
					return;
				}
				CompanionsPlugin.Log.LogInfo($"[Localization] English.json is stale ({dictionary?.Count ?? 0} vs {_englishDefaults.Count} keys) — regenerating.");
			}
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>(_englishDefaults);
			list.Sort((KeyValuePair<string, string> a, KeyValuePair<string, string> b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("{");
			stringBuilder.AppendLine("    \"entries\": [");
			for (int num = 0; num < list.Count; num++)
			{
				string text2 = ((num < list.Count - 1) ? "," : "");
				stringBuilder.AppendLine("        { \"key\": \"" + EscapeJson(list[num].Key) + "\", \"value\": \"" + EscapeJson(list[num].Value) + "\" }" + text2);
			}
			stringBuilder.AppendLine("    ]");
			stringBuilder.AppendLine("}");
			File.WriteAllText(text, stringBuilder.ToString(), Encoding.UTF8);
			CompanionsPlugin.Log.LogInfo("[Localization] Created default " + text);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Localization] Failed to write English.json: " + ex.Message);
		}
	}

	private static Dictionary<string, string> ParseTranslationJson(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return null;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		MatchCollection matchCollection = EntryRegex.Matches(json);
		for (int i = 0; i < matchCollection.Count; i++)
		{
			Match match = matchCollection[i];
			string text = UnescapeJson(match.Groups[1].Value);
			string value = UnescapeJson(match.Groups[2].Value);
			if (!string.IsNullOrEmpty(text))
			{
				dictionary[text] = value;
			}
		}
		return dictionary;
	}

	private static string EscapeJson(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")
			.Replace("\r", "\\r")
			.Replace("\t", "\\t");
	}

	[HarmonyPatch(typeof(Localization), "SetupLanguage")]
	[HarmonyPostfix]
	private static void SetupLanguage_Postfix(Localization __instance)
	{
		if (_initialized)
		{
			InjectTranslations(__instance);
		}
	}

	private static Dictionary<string, string> LoadTranslationFile(string language)
	{
		if (string.IsNullOrEmpty(language) || string.IsNullOrEmpty(_translationsDir))
		{
			return null;
		}
		string text = Path.Combine(_translationsDir, language + ".json");
		if (!File.Exists(text))
		{
			return null;
		}
		try
		{
			string json = File.ReadAllText(text);
			Dictionary<string, string> dictionary = ParseTranslationJson(json);
			if (dictionary == null || dictionary.Count == 0)
			{
				return null;
			}
			CompanionsPlugin.Log.LogInfo($"[Localization] Loaded {dictionary.Count} keys from {text}");
			return dictionary;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Localization] Failed to load " + text + ": " + ex.Message);
			return null;
		}
	}

	private static string UnescapeJson(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t")
			.Replace("\\\"", "\"")
			.Replace("\\\\", "\\");
	}
}
