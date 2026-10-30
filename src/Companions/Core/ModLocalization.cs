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

	/// <summary>
	/// Counts the <b>real</b> language changes. The mod's windows are built once and every <c>TMP</c> label keeps the
	/// text it was built with, so changing the language with the game running left the F8 panel and the AI panel in the
	/// old language until a restart (the speech lines already followed the language - see <see cref="SpeechConfig"/>).
	/// Each panel compares this number on every <c>Show</c> - <b>never</b> per frame - and builds itself again when it
	/// moved.
	///
	/// <para>It is bumped in exactly one place, <see cref="InjectTranslations"/>, and by the same criterion that
	/// injection uses: the language is not the one injected last. The extra <c>SetupLanguage</c> calls the game makes
	/// at startup and on every world load do not count, and neither does a new <c>Localization</c> instance with the
	/// same language.</para>
	/// </summary>
	internal static int LanguageVersion { get; private set; }

	/// <summary>Fase 3.19 (auditoria): the instance the translations were injected into. Comparing only the language
	/// name skipped the injection when the game recreated its <see cref="Localization"/> (world reload) with the same
	/// language: the new dictionary came back with the game's own keys and none of the mod's.</summary>
	private static object _lastInjectedInstance;

	/// <summary>1.8.3 (review): one of our keys, written by every injection (it is in <see cref="_englishDefaults"/>).
	/// Finding it in the game's dictionary is what proves the injection is still there - the language name and the
	/// instance alone did not, because <c>Localization.SetupLanguage</c> rebuilds that dictionary.</summary>
	private const string SentinelKey = "hc_ui_settings_title";

	/// <summary>1.8.3 (hotfix): re-entrancy guard for the <see cref="Localization.SetupLanguage"/> postfix - the game
	/// calls it from the platform startup path, so anything that runs inside it can land here again.</summary>
	private static bool _inPostfix;

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
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Localization] Localization already initialized — injecting retroactively");
			InjectTranslations(loc);
		}
	}

	private static void BuildEnglishDefaults()
	{
		_englishDefaults = new Dictionary<string, string>
		{
			{ "hc_ui_title_companions", "Companions" },
			{ "hc_ui_title_choose", "Choose Your Companion" },
			{ "hc_ui_btn_spawn", "Summon Companion" },
			{ "hc_ui_title_hire", "Summon a Companion" },
			{ "hc_ui_btn_hire", "Summon Companion" },
			{ "hc_ui_desc_hire", "Odin will summon another companion if you make an offering of {0} coins. Customize their appearance on the left, then summon them.\n\nThey will follow you, fight by your side, and carry supplies. Equip them with gear and keep them fed to stay battle-ready." },
			{ "hc_shrine_use", "Your companions" },
			{ "hc_shrine_title", "Odin's Shrine" },
			{ "hc_shrine_flavor", "Here you can make an offering to Odin to receive a boon and check on those who are joining you on this adventure." },
			{ "hc_shrine_btn_recover", "Recover" },
			{ "hc_shrine_btn_recover_list", "Recover" },
			{ "hc_shrine_btn_summon", "Summon" },
			{ "hc_shrine_recover_title", "Recover companions" },
			{ "hc_shrine_offering", "Summoning a new companion asks an offering of {0} coins." },
			{ "hc_shrine_unsummoned", "Unsummoned - can be brought back without gear" },
			{ "hc_shrine_back", "Back" },
			{ "hc_shrine_none", "No companions on your roster in this world yet. A companion joins the roster once it has been near you for a few seconds." },
			{ "hc_shrine_alive", "Alive" },
			{ "hc_shrine_dead", "Dead - its tombstone will bring it back" },
			{ "hc_shrine_lost", "Lost - can be recovered" },
			{ "hc_shrine_unknown", "Unknown - not nearby, may still be alive" },
			{ "hc_msg_hire_need", "Odin asks for an offering of {0} coins to summon a companion - you have {1}, {2} short." },
			{ "hc_msg_hired", "Odin has summoned a new companion for you!" },
			{ "hc_ui_btn_buy", "Buy {0} ({1})" },
			{ "hc_ui_btn_need", "Need {0}" },
			{ "hc_ui_bank", "Bank: {0}" },
			{ "hc_ui_label_type", "Type" },
			// Fase 3.13: the name field of the starter panel (the placeholder reuses hc_ui_placeholder_name)
			{ "hc_ui_name", "Name" },
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
			// Fase 3.20: the appearance editor of a companion that already exists (the starter's column and preview, opened
			// from a companion's tab in the AI panel). The title, the line naming whose look is being edited, the two
			// buttons and the two messages (the change went through / the companion is dead).
			{ "hc_ui_title_appearance", "Appearance" },
			{ "hc_ui_editing_appearance", "Editing the appearance of {0}" },
			{ "hc_ui_btn_apply", "Apply" },
			{ "hc_ui_btn_cancel", "Cancel" },
			{ "hc_ui_appearance_applied", "Appearance of {0} updated." },
			{ "hc_ui_appearance_dead", "I can't change the appearance of a dead companion - bring it back first." },
			{ "hc_ui_desc_starter", "A loyal companion will join you on your journey. Customise their appearance on the left, pick their voice and personality in the AI section, then spawn them into the world.\n\nThey will follow you, fight by your side, and carry supplies. Equip them with gear and keep them fed to stay battle-ready.\n\nPress Escape to skip — the panel will appear again next session." },
			{ "hc_ui_desc_trader", "Hire a loyal companion to join your journey. They will follow you across the world and fight by your side against any threat.\n\nEach companion has their own health, stamina and inventory. You will need to equip them with gear and keep them fed to stay battle-ready.\n\nInteract with your companion to open their panel, where you can manage their equipment and issue commands.\n\nCustomise their appearance on the left, then confirm your purchase." },
			{ "hc_ui_placeholder_name", "Enter name..." },
			{ "hc_ui_label_food", "Food" },
			// Traducao PT-BR - auditoria de cobertura: the tier names the player reads (the trader panel's Sub-Type
			// buttons, the buy button and the "your new X has arrived" line). CompanionTierDef.DisplayName stays in
			// English for the logs; CompanionTierData.LocalName resolves these keys (with the English name as the
			// fallback, the same pattern MHR_Shrine uses).
			{ "hc_tier_companion", "Companion" },
			{ "hc_tier_warrior", "Warrior" },
			{ "hc_tier_mage", "Mage" },
			{ "hc_tier_fire_mage", "Fire Mage" },
			{ "hc_tier_ice_mage", "Ice Mage" },
			{ "hc_tier_support", "Support" },
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
			{ "hc_despawn_popup_recover", "Changed your mind? Until you next load this world, Odin's Shrine can bring them back - without their gear, which stays in the tombstone." },
			{ "hc_radial_active", "ACTIVE" },
			{ "hc_radial_on", "ON" },
			{ "hc_radial_off", "OFF" },
			{ "hc_hover_inventory", "Inventory" },
			{ "hc_hover_commands", "Commands" },
			// the "hold" word of the companion's hover line ([Hold E] Commands) - it was hardcoded in GetHoverText
			{ "hc_hover_hold", "Hold" },
			{ "hc_tomb_hold_resurrect", "Hold to resurrect" },
			{ "hc_tomb_resurrecting", "Resurrecting... {0}%" },
			{ "hc_speech_found_1", "I've been looking for you!" },
			{ "hc_speech_found_2", "There you are!" },
			{ "hc_speech_found_3", "Finally found you!" },
			{ "hc_msg_not_yours", "This is not your companion." },
			{ "hc_msg_need_coins", "Not enough coins in bank! Need {0}" },
			{ "hc_msg_companion_limit", "You already have {0} companion(s) (the limit is {1}). Dismiss one, or recover a tombstone." },
			{ "hc_msg_arrived", "Your new {0} has arrived!" },
			{ "hc_msg_returned", "{0} has returned at {1}!" },
			{ "hc_msg_location_home", "home" },
			{ "hc_msg_location_spawn", "the world spawn" },
			{ "hc_msg_starter_joined", "A companion has joined you on your journey!" },
			{ "hc_msg_name_default", "Companion" },
			// Fase 3.9: the default name of a newly spawned companion when one already answers to the tier's name
			// ("Guerreiro" -> "Guerreiro 2"). Visible text, so it lives here like every other name.
			{ "hc_msg_name_numbered", "{0} {1}" },
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
			// the tombstone's "X has been resurrected!" - it was hardcoded in CompanionTombstoneResurrect
			{ "hc_msg_resurrected", "{0} has been resurrected!" },
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
			{ "hc_status_sleeping", "Sleeping" },
			{ "hc_status_resting", "Resting" },
			{ "hc_status_hunting", "Hunting a {0}" },
			{ "hc_status_fighting", "Fighting a {0}" },
			{ "hc_status_spear", "Fetching its spear" },
			{ "hc_status_order", "Carrying out your order" },
			{ "hc_status_nothing", "nothing to do" },
			{ "hc_status_following_look", "following you, looking out for work" },
			{ "hc_status_following", "Following you" },
			{ "hc_status_idle", "Idling" },
			{ "hc_status_staying", "Staying put" },
			{ "hc_status_waiting", "Waiting" },
			{ "hc_status_fetching", "Fetching {0}" },
			{ "hc_status_putting_back", "Putting back {0}" },
			{ "hc_status_unloading", "Emptying its bags" },
			{ "hc_status_repairing_gear", "Repairing its gear" },
			{ "hc_status_needs_home", "Needs {0} - waiting till we're home" },
			{ "hc_status_job_wood", "Wood" },
			{ "hc_status_job_stone", "Stone" },
			{ "hc_status_job_ore", "Ore" },
			{ "hc_status_job_forage", "Forage" },
			{ "hc_status_job_smelt", "Smelt" },
			{ "hc_status_job_hunt", "Hunt" },
			{ "hc_status_job_farm", "Farm" },
			{ "hc_status_job_fish", "Fish" },
			{ "hc_status_job_upkeep", "Repair and Restock" },
			{ "hc_status_job_cook", "Cook" },
			{ "hc_status_job_mead", "Mead" },
			{ "hc_doing_chop", "chopping {0}" },
			{ "hc_doing_mine", "mining {0}" },
			{ "hc_doing_pick", "picking up {0}" },
			{ "hc_doing_forage", "picking {0}" },
			{ "hc_doing_hunt", "hunting {0}" },
			{ "hc_doing_encumbered", "carrying too much - walking slowly" },
			{ "hc_doing_fish", "fishing for {0}" },
			{ "hc_doing_smelt_load", "loading the {0} with {1}" },
			{ "hc_doing_smelt_fuel", "fuelling the {0}" },
			{ "hc_doing_smelt_collect", "emptying the {0}" },
			{ "hc_doing_take_pile", "taking down the {0}" },
			{ "hc_doing_upkeep_repair", "repairing the {0}" },
			{ "hc_doing_upkeep_fuel", "fuelling the {0}" },
			{ "hc_doing_upkeep_sort", "sorting {0}" },
			{ "hc_doing_upkeep_tidy", "stacking up {0}" },
			{ "hc_doing_upkeep_stow", "putting back {0}" },
			{ "hc_doing_upkeep_pile", "piling up {0}" },
			{ "hc_doing_upkeep_honey", "collecting honey" },
			{ "hc_doing_home", "heading home" },
			{ "hc_doing_cook_make", "making {0}" },
			{ "hc_doing_cook_rescue", "taking food off the {0}" },
			{ "hc_doing_cook_store", "putting away {0}" },
			{ "hc_doing_mead_fill", "filling a fermenter with {0}" },
			{ "hc_doing_mead_tap", "tapping a fermenter" },
			{ "hc_why_gather_limit", "{0} {1}/{2} - at the limit." },
			{ "hc_why_gather_tier", "{0} needs a better {1}." },
			{ "hc_tool_axe", "axe" },
			{ "hc_tool_pickaxe", "pickaxe" },
			{ "hc_why_gather_near_base", "Leaving {0} trees within {1} m of buildings." },
			{ "hc_why_hunt_at_target", "{0} {1}/{2} - enough." },
			{ "hc_why_hunt_none_range", "No game within {0} m." },
			{ "hc_why_fish_none", "No fish within {0} m." },
			{ "hc_why_fish_at_target", "{0} {1}/{2} - enough." },
			{ "hc_why_fish_bait", "No bait for {0} ({1})." },
			{ "hc_why_fish_no_bank", "No spot to fish for {0} from." },
			{ "hc_why_smelt_no_ore", "No ore for the {0}." },
			{ "hc_why_smelt_no_wood", "No wood for the {0}." },
			{ "hc_why_smelt_no_fuel", "No {0} for the {1}." },
			{ "hc_why_smelt_pile_hammer", "Need a hammer to take down the {0}." },
			{ "hc_why_upkeep_no_fuel", "No {0} for the fires." },
			{ "hc_why_upkeep_no_station", "{0} damaged pieces have no station in range." },
			{ "hc_why_farm_seed_min", "Seeds at the minimum ({0}) - keeping them." },
			{ "hc_why_farm_zone_full", "Every spot is planted." },
			{ "hc_why_farm_no_seed", "No {0} to plant." },
			{ "hc_act_fetch_tool", "Fetching {0}." },
			{ "hc_act_return_tool", "Putting {0} back." },
			{ "hc_act_bed", "Off to bed." },
			{ "hc_status_farm_harvesting", "harvesting" },
			{ "hc_status_farm_pickup", "picking up the harvest" },
			{ "hc_status_farm_seeds", "fetching seeds" },
			{ "hc_status_farm_planting", "planting {0}" },
			{ "hc_status_farm_storing", "storing the harvest" },
			{ "hc_cmd_refused_off", "{0}: Command is off on the wheel." },
			{ "hc_cmd_refused_far", "{0} is {1} m away. Hold {2} s to call all." },
			{ "hc_why_spear_lost", "I lost my {0}." },
			{ "hc_msg_companion_died", "{0} has died ({1}, {2} m from you)" },
			{ "hc_where", "{0}, {1} m from you" },
			{ "hc_status_recover", "Getting its gear back" },
			{ "hc_status_recover_wait", "Getting its gear back - waiting for it to be safe" },
			{ "hc_status_recover_far", "Its gear is out of reach" },
			{ "hc_status_recover_equip", "Putting its gear back on" },
			{ "hc_speech_recover_wait", "Too many of them. I'll wait for my gear." },
			{ "hc_speech_recover_far", "My gear's out of reach. I'll stay out of fights." },
			{ "hc_speech_recover_done", "Got my gear back." },
			{ "hc_msg_stance_set", "{0}: {1}" },
			{ "hc_msg_stance_none", "No companion in range for {0}." },
			{ "hc_msg_back_bed", "{0} is back - at its bed ({1})" },
			{ "hc_msg_back_spawn", "{0} is back - at the world spawn ({1})" },
			{ "hc_msg_back_here", "{0} is back - next to you" },
			{ "hc_why_hunt_none", "No game around." },
			{ "hc_why_hunt_enough", "We have enough meat." },
			{ "hc_why_hunt_reach", "The game around here is out of reach." },
			{ "hc_why_hunt_passive", "I won't hunt in the Passive stance." },
			{ "hc_why_hunt_spear", "Let me get my spear back first." },
			{ "hc_why_hunt_got_away", "It got away." },
			{ "hc_why_pack_full", "My pack is full." },
			{ "hc_why_pack_full_far", "My pack is full and home is too far." },
			{ "hc_why_gather_none", "Nothing left to gather around here." },
			{ "hc_why_no_way", "I can't find a way there." },
			{ "hc_why_cant_reach_up", "I can't get up there." },
			{ "hc_why_chests_full", "The chests near home are full." },
			{ "hc_why_chest_unreachable", "I can't get to that chest." },
			{ "hc_why_farm_no_zones", "Nothing ripe, and nowhere to plant." },
			{ "hc_why_farm_waiting", "Waiting for the crops to grow." },
			{ "hc_why_farm_no_chest", "No chest has room for the harvest." },
			{ "hc_why_smelt_no_chests", "There are no chests near the smelters." },
			{ "hc_why_smelt_nothing", "Nothing to smelt in the chests." },
			{ "hc_why_cook_nothing", "Nothing to cook right now." },
			{ "hc_why_mead_short", "{0} {1}/{2} for {3}." },
			{ "hc_why_cook_no_food", "No food ingredients in reach." },
			{ "hc_why_cook_stocked", "Food is stocked up." },
			{ "hc_why_cook_no_station", "Can't use the {0} (level, fire, roof)." },
			{ "hc_why_repair_no_station", "No station near home can fix my {0}." },
			{ "hc_why_tool_taken", "You took my {0} - I'll do without." },
			{ "hc_why_need_tool", "I need {0} for this job." },
			{ "hc_why_fish_no_rod", "I need a fishing rod." },
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
			// Traducao PT-BR - auditoria de cobertura: the greetings ReturnLogic speaks when it brings a companion
			// that was left behind back to you (they were hardcoded English in ReturnLogic)
			{ "hc_cmd_return_1", "I've been looking for you!" },
			{ "hc_cmd_return_2", "There you are!" },
			{ "hc_cmd_return_3", "Finally found you!" },
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
			{ "hc_cmd_doorclose_1", "Closing it." },
			{ "hc_cmd_doorclose_2", "I'll shut it." },
			{ "hc_cmd_doorclose_3", "Shutting the door." },
			{ "hc_cmd_door_locked", "It's locked." },
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
			{ "hc_cmd_hive", "I'll fetch the honey." },
			{ "hc_cmd_hive_empty", "No honey in there yet." },
			{ "hc_say_encumbered", "This is too heavy - I can barely move." },
			{ "hc_say_no_torches", "My last torch burned out." },
			{ "hc_say_portal_stay", "I can't take {0} through the portal - I'll wait here." },
			{ "hc_say_portal_left", "Left {0} in the chest by the portal." },
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
			// 1.8.3 (i18n): the tabs. The key is lower(section), and the AI section is "AI" now - so "hc_cat_ai".
			{ "hc_cat_ai", "AI" },
			{ "hc_ai_thinking", "Thinking..." },
			{ "hc_ai_not_understood", "I didn't understand that order." },
			{ "hc_ai_no_companion", "None of your companions is close enough to take that order." },
			// Fase 3.11: orders addressed to one companion by name ("Gruncha, colete 10 pedras")
			{ "hc_ai_unknown_companion", "I don't know any companion called \"{0}\"." },
			{ "hc_ai_companion_not_available", "{0} isn't within reach (or its orders are switched off)." },
			{ "hc_ai_disabled", "Companion AI is off ([AI] Enabled = false)." },
			{ "hc_ai_error", "The AI didn't answer: {0}" },
			{ "hc_ai_error_timeout", "timed out" },
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
			{ "hc_ai_order_stop", "stop following" },
			// Fase 3.22: the "stop" answer names the work the companion was doing ("stop: gather wood") - the job label
			// comes from hc_ai_order_wood/stone/ore/...; when there is no job to name, "stop" alone.
			{ "hc_ai_order_stop_job", "stop ({0})" },
			{ "hc_ai_order_stop_other", "stop" },
			// fix/dverger-combate: a work order sent to a combat-only type (the dverger ones: Mage, Fire Mage, Ice
			// Mage, Support) - the job is not started and the companion answers with this instead.
			{ "hc_ai_order_combat_only", "I fight, I don't do that work." },
			{ "hc_ai_order_come", "come here" },
			{ "hc_ai_order_sethome", "set home here" },
			{ "hc_ai_order_wander", "wander around" },
			{ "hc_ai_order_nowander", "stop wandering" },
			{ "hc_ai_order_pickup", "pick up items" },
			{ "hc_ai_order_nopickup", "stop picking up items" },
			// Fase 3.1: the "recolhe os itens do chão" order (MHR_Collect) - what it says when it comes back with
			// nothing (before this the order answered "Ok" and stayed silent while nothing happened)
			{ "hc_ai_collect_nothing", "There's nothing on the ground nearby." },
			{ "hc_ai_collect_full", "My pack is full." },
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
			// Fase 3.8: the companion opens its own inventory (voice/chat). These 9 keys only existed in the PT-BR
			// JSON, so another language showed the raw token - same bug the audit found in hc_ai_voice_key_conflict.
			{ "hc_ai_order_inventory", "open the inventory" },
			{ "hc_ai_order_inventory_close", "close the inventory" },
			// Fase 2.0: the five interaction orders (chat and voice) - each one reuses what the command key does
			{ "hc_ai_order_hive", "empty the beehive" },
			{ "hc_ai_order_door", "close the door" },
			{ "hc_ai_order_board", "board the ship" },
			{ "hc_ai_order_sit", "sit down" },
			{ "hc_ai_order_deposit", "put it in the chest" },
			{ "hc_ai_inventory_open", "Opening the inventory." },
			{ "hc_ai_inventory_requesting", "Asking {0} for access to the inventory - the window opens in a moment." },
			{ "hc_ai_inventory_timeout", "I couldn't open the companion's inventory. Ask me again." },
			{ "hc_ai_inventory_no_gui", "I can't open the inventory right now." },
			{ "hc_ai_inventory_closed", "I closed the inventory." },
			{ "hc_ai_inventory_none_open", "No inventory is open." },
			{ "hc_ai_no_container", "I couldn't find {0}'s inventory." },
			// Fase 2.0: what the interaction orders answer. The target of each one is the object the player is looking
			// at, or the nearest one around the player (see CompanionInteract.OrderHive and the other Order* wrappers).
			{ "hc_ai_hive_none", "I don't see a beehive nearby." },
			{ "hc_ai_hive_done", "Going for the honey." },
			{ "hc_ai_door_none", "I don't see a door nearby." },
			{ "hc_ai_door_closed", "Closing the door." },
			{ "hc_ai_door_opened", "Opening the door." },
			{ "hc_ai_board_none", "I don't see a ship nearby." },
			{ "hc_ai_board_done", "On board!" },
			{ "hc_ai_sit_none", "I don't see a lit fire nearby." },
			{ "hc_ai_sit_done", "Sitting by the fire." },
			{ "hc_ai_deposit_none", "I don't see a chest nearby." },
			{ "hc_ai_deposit_full", "There's no room in that chest." },
			{ "hc_ai_deposit_done", "Putting {0} item(s) in the chest." },
			// Fase 3.5: personality presets (shown in the log), free conversation and the memory
			{ "hc_ai_default_name", "Companion" },
			{ "hc_ai_preset_default", "Default" },
			// Fase 3.12: the "default personality" entry of a COMPANION's list, which also has "follow the [AI]
			// default" right above it - two different choices, so they must not read the same
			{ "hc_ai_preset_neutral", "Neutral (the default)" },
			{ "hc_ai_preset_custom", "Free text" },
			{ "hc_ai_preset_viking", "Loyal Viking" },
			{ "hc_ai_preset_mago", "Sarcastic Mage" },
			{ "hc_ai_preset_sarcastico", "Sarcastic" },
			{ "hc_ai_preset_guia", "Patient Guide" },
			// Fase 3.3: voice orders (push-to-talk + local STT) - what the pipeline itself says
			{ "hc_ai_voice_listening", "Listening..." },
			{ "hc_ai_voice_heard", "Got it: \"{0}\"" },
			{ "hc_ai_voice_nothing", "I didn't hear anything." },
			{ "hc_ai_voice_short", "That was too short - hold the key and speak." },
			{ "hc_ai_voice_busy", "Still working on the last thing you said." },
			{ "hc_ai_voice_timeout", "Maximum recording time reached ({0}s)." },
			{ "hc_ai_voice_error", "I couldn't make out the speech: {0}" },
			{ "hc_ai_voice_no_mic", "No microphone found (voice orders are off)." },
			{ "hc_ai_voice_unavailable", "Speech recognition isn't ready (the Whisper model is missing?)." },
			{ "hc_ai_voice_ui_open", "Close the window before speaking." },
			{ "hc_ai_voice_cancelled", "Recording cancelled." },
			{ "hc_ai_voice_disabled", "Voice orders are off ([AI] VoiceEnabled = false)." },
			{ "hc_ai_voice_key_conflict", "The {0} key is now push-to-talk: the game's auto-pickup no longer toggles with it. Rebind auto-pickup in the game's Controls settings if you want to use it." },
			{ "hc_ai_tts_unavailable", "The companion's voice isn't ready (the Kokoro model is missing?)." },
			// Fase 3.7: the in-game AI panel (personality, voice, push-to-talk). See UI/CompanionAiPanel.cs.
			{ "hc_ai_panel_title", "AI Panel" },
			{ "hc_ai_panel_title_named", "AI Panel — {0}" },
			{ "hc_ai_panel_hint_fmt", "Esc closes  •  {0} opens this panel  •  everything is saved right away  ·  advanced options: F8" },
			{ "hc_ai_panel_personality", "Personality" },
			{ "hc_ai_panel_free_text", "In your own words" },
			{ "hc_ai_panel_free_placeholder", "e.g. talks like a loyal viking" },
			{ "hc_ai_panel_apply", "Apply" },
			{ "hc_ai_panel_apply_ok", "Personality applied: {0}" },
			{ "hc_ai_panel_preset_shadowed", "Preset saved, but your own text is the one in use: {0}" },
			{ "hc_ai_panel_voice", "Voice" },
			{ "hc_ai_panel_listen", "Listen" },
			{ "hc_ai_panel_volume", "Volume" },
			{ "hc_ai_panel_memory", "Memory" },
			{ "hc_ai_panel_memory_fmt", "{0} exchanges" },
			{ "hc_ai_panel_push_to_talk", "Push-to-talk" },
			{ "hc_ai_panel_change", "Change" },
			{ "hc_ai_panel_on", "On" },
			{ "hc_ai_panel_off", "Off" },
			{ "hc_ai_panel_tts_on", "Companion speaks" },
			{ "hc_ai_panel_tts_replies", "Speak AI replies" },
			{ "hc_ai_panel_tts_modlines", "Speak mod lines" },
			{ "hc_ai_panel_conversation", "Free conversation" },
			{ "hc_ai_panel_voice_enabled", "Voice orders" },
			{ "hc_ai_panel_tts_sample", "Hello! This is how I will talk to you." },
			{ "hc_ai_panel_tts_off", "The voice is off ([AI] TtsEnabled = false)." },
			{ "hc_ai_panel_tts_missing", "The voice isn't ready (is the Kokoro model missing?)." },
			{ "hc_ai_panel_no_companion", "No companion nearby to talk." },
			// the footer summary is built from these two lines (the block is two lines tall) - see RefreshStatus
			{ "hc_ai_panel_status_personality", "Personality: {0}" },
			{ "hc_ai_panel_status_voice", "Voice: {0}  \u2022  Push-to-talk: {1}" },
			// Fase 3.9: the tabs and the summoner of the AI panel (see UI/CompanionAiPanel.cs). The tab of every
			// companion carries the companion's own name, which is not a key - only the AI tab has one.
			{ "hc_ai_tab_main", "AI" },
			{ "hc_ai_spawn_title", "Summon a new companion" },
			{ "hc_ai_spawn_tier", "Tier" },
			{ "hc_ai_spawn_button", "Summon" },
			{ "hc_ai_spawn_count", "Companions: {0}" },
			{ "hc_ai_spawn_failed", "I couldn't summon a companion right now (see the log)." },
			// Fase 3.20: the LLM settings section of the AI tab (and the "Testar conexão" of the panel)
			{ "hc_ai_llm_title", "LLM settings" },
			{ "hc_ai_llm_enabled", "On" },
			{ "hc_ai_llm_structured", "Structured output" },
			{ "hc_ai_llm_show_raw", "Show the reply in chat" },
			// Fase 3.21: the provider dropdown of the LLM section ("Selecione seu Provedor LLM"). The labels are short
			// on purpose - they are the caption of a field in the left column of the grid - and the table that pairs
			// each one with its URL and model is AI/LlmProviders.cs.
			{ "hc_ai_llm_provider", "Pick your LLM provider" },
			{ "hc_ai_llm_provider_groq", "Groq (free, fast)" },
			{ "hc_ai_llm_provider_lmstudio", "LM Studio (local)" },
			{ "hc_ai_llm_provider_openrouter", "OpenRouter (free, slower)" },
			{ "hc_ai_llm_provider_custom", "Custom (manual URL)" },
			{ "hc_ai_llm_base_url", "Server URL" },
			{ "hc_ai_llm_model", "Model" },
			{ "hc_ai_llm_api_key", "API key" },
			// Fase 3.21c: the link on the right half of the key row that opens the provider's key page in the browser
			{ "hc_ai_llm_key_link", "Create your free API key HERE" },
			{ "hc_ai_llm_prefix", "Prefix" },
			{ "hc_ai_llm_temperature", "Temperature" },
			{ "hc_ai_llm_timeout", "Timeout (s)" },
			{ "hc_ai_llm_cooldown", "Cooldown (s)" },
			{ "hc_ai_llm_target_range", "Target range (m)" },
			{ "hc_ai_llm_apply", "Apply" },
			{ "hc_ai_llm_test", "Test connection" },
			{ "hc_ai_llm_apply_ok", "LLM settings saved. They apply to the next message." },
			{ "hc_ai_llm_need_url", "The server URL can't be empty." },
			{ "hc_ai_llm_need_model", "The model name can't be empty." },
			{ "hc_ai_llm_bad_url", "The server URL has to start with http:// or https://." },
			{ "hc_ai_llm_test_busy", "A connection test is already running - wait for the answer." },
			{ "hc_ai_llm_test_ok_count", "Connection OK: {0} model(s) on the server." },
			{ "hc_ai_llm_test_ok_models", "Connection OK: {0} model(s) on the server, and the configured model is in the list." },
			{ "hc_ai_llm_test_ok_nomodel", "Connection OK: {0} model(s) on the server, but \"{1}\" isn't in the list." },
			{ "hc_ai_llm_test_ok_chat", "Connection OK: the server answered a minimal request (it doesn't list its models)." },
			{ "hc_ai_llm_test_fail", "Connection failed: {0}" },
			{ "hc_ai_comp_name", "Name" },
			{ "hc_ai_comp_tier", "Tier" },
			{ "hc_ai_comp_tier_unknown", "Unknown" },
			{ "hc_ai_comp_status", "Status" },
			{ "hc_ai_comp_alive", "Alive" },
			{ "hc_ai_comp_dead", "Dead" },
			{ "hc_ai_comp_recall", "Call" },
			// Fase 3.20: the button on a companion's tab that opens the appearance editor of that companion
			{ "hc_ai_comp_appearance", "Appearance" },
			{ "hc_ai_comp_recall_ok", "{0} is on the way." },
			{ "hc_ai_comp_recall_failed", "I couldn't call {0} (see the log)." },
			{ "hc_ai_comp_gone", "That companion isn't here anymore." },
			{ "hc_ai_comp_hint", "Call brings the companion over to you: it walks in, and if it is too far away it comes back through the game's own recall." },
			// Fase 3.15: the name and the tier are edited on the companion's tab
			{ "hc_ai_comp_name_required", "Type a name before applying." },
			{ "hc_ai_comp_rename_ok", "Renamed to \"{0}\"." },
			{ "hc_ai_comp_rename_failed", "I couldn't rename it (the companion isn't under my control right now)." },
			{ "hc_ai_comp_rename_dead", "I can't rename a dead companion - bring it back first." },
			{ "hc_ai_comp_tier_change", "Change tier (respawns)" },
			{ "hc_ai_comp_tier_popup_title", "Change the companion's tier?" },
			{ "hc_ai_comp_tier_popup_text", "{0} will be reborn as {1} (it is {2} now).\n\nKept: name, inventory, equipment, appearance, voice, personality, home, bed, position, skills and the companion's history. The job it is doing starts over, and a dverger tier cannot wear armor (the pieces stay in its pack)." },
			{ "hc_ai_comp_tier_ok", "{0} is now {1}." },
			{ "hc_ai_comp_tier_dead", "I can't change the type of a dead companion - bring it back first." },
			{ "hc_ai_comp_tier_failed", "I couldn't change the tier (see the log)." },
			{ "hc_ai_voice_42", "pf_dora (feminine)" },
			{ "hc_ai_voice_43", "pm_alex (masculine)" },
			{ "hc_ai_voice_44", "pm_santa (masculine)" },
			{ "hc_ai_voice_other", "voice {0} (another language)" },
			// Multilanguage M1: the neutral labels. The three pt-BR voices above keep their hand-written labels (they
			// are read better than the generic form); every other voice of the active language is "{name} ({gender})".
			{ "hc_ai_voice_label", "{0} ({1})" },
			{ "hc_ai_voice_female", "feminine" },
			{ "hc_ai_voice_male", "masculine" },
			// Multilanguage M2: "Automatic" is now an explicit option (the [AI] language and voice settings follow the
			// game language), and a companion's own voice from ANOTHER language is announced instead of being used.
			{ "hc_cfg_auto", "Automatic" },
			{ "hc_cfg_auto_value", "Automatic ({0})" },
			{ "hc_ai_voice_out_of_language", "{0} ({1}) - another language, using {2}" },
			// Multilanguage M2 fix: the short form for the narrow field of the F8 rows (the long one above is the AI
			// panel's footer, where "outro idioma" fits). "pf_dora is the pick; af_heart is what is spoken".
			{ "hc_cfg_voice_out_of_language", "{0} ({1}) - using {2}" },
			// Fase 3.10: voice and personality of ONE companion (its own ZDO), with the [AI] values as the default.
			// Used by the companion tab of the AI panel and by the starter panel's "AI" section.
			{ "hc_ai_profile_title", "Companion AI" },
			{ "hc_ai_profile_inherit", "Follow the [AI] default" },
			{ "hc_ai_profile_inherited", "{0} (default)" },
			{ "hc_ai_profile_hint", "Voice and personality belong to this companion: left unset, it follows the [AI] settings (AI tab)." },
			{ "hc_ai_comp_profile_title", "Voice and personality of this companion" },
			{ "hc_ai_profile_save_failed", "I couldn't save the companion's profile (its data is not under my control right now). Try again in a moment." }
		};
	}

	/// <summary>
	/// 1.8.3 (i18n): the keys the loaded language file has. <see cref="LocOr"/> needs this because some UI text exists
	/// only in a translation - the settings labels, for instance: in English the config key itself is the label (the
	/// mod's native language), so only a language file names them. The value ends up in <c>m_translations</c>, but
	/// nothing in <see cref="_englishDefaults"/> knows such a key.
	/// </summary>
	private static readonly HashSet<string> TranslatedKeys = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>Translation for a key that is registered (in the English defaults or in the loaded language file),
	/// otherwise the caller's own fallback. Used for text that has to show something sensible (a category name, a
	/// setting name) even when a key is missing.</summary>
	public static string LocOr(string key, string fallback)
	{
		if (_englishDefaults != null && _englishDefaults.ContainsKey(key))
		{
			return Loc(key);
		}
		if (TranslatedKeys.Contains(key))
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
		if (!(TranslationsField?.GetValue(loc) is Dictionary<string, string> dictionary))
		{
			return;
		}
		string selectedLanguage = loc.GetSelectedLanguage();
		// Multilanguage M1: the language is already in hand here, on the main thread - remember it for
		// CompanionLocale (the STT/TTS codes, the voices and the Whisper vocabulary follow it).
		CompanionLocale.NotifyLanguage(selectedLanguage);
		// 1.8.3 (review): the language name plus the instance were the whole criterion, and they skipped the injection
		// when the game called SetupLanguage again on the SAME Localization - and SetupLanguage rebuilds the
		// dictionary, so the mod's keys were gone and the UI went back to showing "$hc_...". The check is the
		// dictionary itself now: our sentinel still in it means the injection is there (or was redone, in which case
		// writing it again changes nothing). See SentinelKey.
		bool sameTarget = selectedLanguage == _lastInjectedLanguage && ReferenceEquals(_lastInjectedInstance, loc);
		bool reInjected = sameTarget;
		if (sameTarget && dictionary.ContainsKey(SentinelKey))
		{
			return;
		}
		// Multilanguage M2 fix: a language that is not the one injected last is a REAL change (the same criterion the
		// injection uses above). The panels read this number on their next Show and build their texts again - see
		// LanguageVersion. A new Localization with the same language does not count.
		if (!string.Equals(selectedLanguage, _lastInjectedLanguage, StringComparison.Ordinal))
		{
			LanguageVersion++;
		}
		_lastInjectedLanguage = selectedLanguage;
		_lastInjectedInstance = loc;
		TranslatedKeys.Clear();
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
				// 1.8.3: remembered so LocOr can translate a key that exists only in the language file
				TranslatedKeys.Add(item.Key);
				num++;
			}
		}
		CompanionsPlugin.Log.LogInfo(reInjected
			? $"[Localization] Re-injected {_englishDefaults?.Count ?? 0} defaults + {num} \"{selectedLanguage}\" overrides (the game had rebuilt the dictionary)"
			: $"[Localization] Injected {_englishDefaults?.Count ?? 0} defaults + {num} \"{selectedLanguage}\" overrides");
		if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo($"[Localization] Injected {_englishDefaults?.Count ?? 0} defaults + {num} \"{selectedLanguage}\" overrides");
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
				if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo($"[Localization] English.json is stale ({dictionary?.Count ?? 0} vs {_englishDefaults.Count} keys) — regenerating.");
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
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo("[Localization] Created default " + text);
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
		// 1.8.3 (hotfix): this postfix must do ONE thing - inject the strings. Anything else that touched Unity's
		// platform layer here (SpeechConfig.Load calls Localization.instance.GetSelectedLanguage, which initializes
		// PlatformPrefs, which runs SetStartupLanguage -> SetupLanguage -> this postfix -> ...) recursed until the game
		// froze on the loading screen. The speech lines follow the language lazily, in SpeechConfig.Instance.
		if (_inPostfix)
		{
			return;
		}
		_inPostfix = true;
		try
		{
			// Multilanguage M1: the language the STT and the TTS resolve (CompanionLocale) changed with this call.
			// Only the cache is dropped here - reading GetSelectedLanguage() would touch the platform layer, which is
			// what recursed (see the note above); the real language arrives with the injection below, which already
			// has it in hand.
			CompanionLocale.Invalidate();
			if (_initialized)
			{
				InjectTranslations(__instance);
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Localization] Couldn't inject the strings: " + ex.Message);
		}
		finally
		{
			_inPostfix = false;
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
			if (CompanionsPlugin.LogNormal) CompanionsPlugin.Log.LogInfo($"[Localization] Loaded {dictionary.Count} keys from {text}");
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
