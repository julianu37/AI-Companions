using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Companions;

internal static class ReturnLogic
{
	private const float CloseRange = 4f;

	private const float ReturnDistance = 18f;

	private const float MaxChaseDistance = 60f;

	private const float TeleportGraceSeconds = 87f;

	private const float TeleportRadius = 30f;

	private const float PinHideSeconds = 4f;

	private const float PartyFollowRadius = 40f;

	private static readonly Hashtable _active = new Hashtable();

	private static readonly Hashtable _prevFollow = new Hashtable();

	private static readonly Hashtable _pendingSince = new Hashtable();

	private static readonly Hashtable _partyFollowActive = new Hashtable();

	internal static bool OwnerDead;

	private static MethodInfo _getFollowM;

	private static MethodInfo _setFollowM;

	private static MethodInfo _setFollowTargetM;

	private static MethodInfo _isEngagedM;

	private static MethodInfo _disengageM;

	private static PropertyInfo _targetCreatureProp;

	private static int _logs;

	private static Type _setupType;

	private static MethodInfo _allCompanionsM;

	private static Type _interactType;

	private static MethodInfo _isOwnedM;

	private static FieldInfo _nviewField;

	private static FieldInfo _actionModeHashField;

	private static MethodInfo _getZdoM;

	private static MethodInfo _zdoGetIntM;

	private static MethodInfo _isOwnerM;

	private static Type _managerType;

	private static MethodInfo _findSpawnPosM;

	private static MethodInfo _hidePinM;

	private static FieldInfo _talkField;

	private static MethodInfo _sayM;

	// Traducao PT-BR - auditoria de cobertura: the recall greeting used to be hardcoded English here. Built lazily
	// (like CompanionInteract's line pools) so the text is resolved when it is spoken, with the dictionary already up.
	private static string[] _greetings;

	private static string[] Greetings
	{
		get
		{
			if (_greetings == null)
			{
				_greetings = new string[3]
				{
					ModLocalization.Loc("hc_cmd_return_1"),
					ModLocalization.Loc("hc_cmd_return_2"),
					ModLocalization.Loc("hc_cmd_return_3")
				};
			}
			return _greetings;
		}
	}

	private static readonly System.Random _rng = new System.Random();

	internal static void OnPlayerRespawn()
	{
		OnPlayerRespawnFrom("OnSpawned");
	}

	internal static void OnPlayerDeath(Player instance)
	{
		try
		{
			if (!(instance == null) && !(instance != Player.m_localPlayer))
			{
				OwnerDead = true;
				Log("[Return] owner died - Follow-mode companions may fall in with a nearby ally until respawn");
			}
		}
		catch (Exception)
		{
		}
	}

	internal static void Tick(Component setupComp)
	{
		try
		{
			if (setupComp == null)
			{
				return;
			}
			int instanceID = setupComp.GetInstanceID();
			object obj = _active[instanceID];
			bool flag = obj != null && (bool)obj;
			if (OwnerDead && !flag && IsInFollowMode(setupComp))
			{
				TickPartyFollow(setupComp, instanceID);
			}
			else
			{
				object obj2 = _partyFollowActive[instanceID];
				if (obj2 != null && (bool)obj2)
				{
					_partyFollowActive[instanceID] = false;
				}
			}
			object obj3 = _active[instanceID];
			if (obj3 == null || !(bool)obj3)
			{
				return;
			}
			Player localPlayer = Player.m_localPlayer;
			if (localPlayer == null)
			{
				return;
			}
			float num = Vector3.Distance(setupComp.transform.position, localPlayer.transform.position);
			if (num <= 18f)
			{
				_active[instanceID] = false;
				_pendingSince.Remove(instanceID);
				bool flag2 = false;
				object obj4 = _prevFollow[instanceID];
				if (obj4 != null)
				{
					flag2 = (bool)obj4;
				}
				if (_setFollowM == null)
				{
					_setFollowM = setupComp.GetType().GetMethod("SetFollow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				try
				{
					if (_setFollowM != null)
					{
						_setFollowM.Invoke(setupComp, new object[1] { flag2 });
					}
				}
				catch (Exception)
				{
				}
				// 1.5.3: put its follow target back to match its setting too (SetFollow only stores the flag)
				CompanionSetup csBack = setupComp as CompanionSetup;
				ZDO zBack = csBack != null ? csBack.GetComponent<ZNetView>()?.GetZDO() : null;
				if (csBack != null && zBack != null) csBack.ApplyFollowMode(zBack.GetInt(CompanionSetup.ActionModeHash), true);
				Log("[Return] back in range (" + num.ToString("F1") + "m) for \"" + setupComp.name + "\" - resuming normal behavior.");
				return;
			}
			if (num <= 60f)
			{
				_pendingSince.Remove(instanceID);
				Component component = setupComp.GetComponent("CompanionAI");
				if (!(component != null))
				{
					return;
				}
				if (_setFollowTargetM == null)
				{
					_setFollowTargetM = component.GetType().GetMethod("SetFollowTarget", BindingFlags.Instance | BindingFlags.Public);
				}
				try
				{
					if (_setFollowTargetM != null)
					{
						_setFollowTargetM.Invoke(component, new object[1] { localPlayer.gameObject });
					}
					return;
				}
				catch (Exception)
				{
					return;
				}
			}
			object obj5 = _pendingSince[instanceID];
			if (obj5 == null)
			{
				_pendingSince[instanceID] = Time.time;
				return;
			}
			float num2 = (float)obj5;
			if (!(Time.time - num2 < 87f))
			{
				DoTeleportRecall(setupComp, localPlayer, instanceID);
			}
		}
		catch (Exception)
		{
		}
	}

	/// <summary>
	/// Starts the walk-in recall of one companion (the command key runs this for everyone in scope). Fase 3.9: it
	/// returns whether it <b>really</b> started - it refuses to touch a companion that isn't in Follow mode, has
	/// Follow switched off, or is beyond the safe chase range (there it only leaves its name on the waiting list),
	/// and the AI panel's "Chamar" button has to say so instead of claiming the companion is coming.
	/// </summary>
	internal static bool Trigger(Component setupComp, GameObject playerGO)
	{
		try
		{
			if (setupComp == null || playerGO == null)
			{
				return false;
			}
			if (!IsInFollowMode(setupComp))
			{
				Log("[Return] skipped for \"" + setupComp.name + "\" - not currently set to Follow, left alone");
				return false;
			}
			// 1.5.3: mode 0 with the Follow toggle off is "stay where it is", not following (user: "The NPC was following me
			// even though follow was off" - Return switched follow on at load and never put it back)
			CompanionSetup csFollow = setupComp as CompanionSetup;
			if (csFollow != null && !csFollow.GetFollow())
			{
				Log("[Return] skipped for \"" + setupComp.name + "\" - Follow is off, left alone");
				return false;
			}
			int instanceID = setupComp.GetInstanceID();
			_partyFollowActive[instanceID] = false;
			Type type = setupComp.GetType();
			if (_getFollowM == null)
			{
				_getFollowM = type.GetMethod("GetFollow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_setFollowM == null)
			{
				_setFollowM = type.GetMethod("SetFollow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			object obj = _active[instanceID];
			if (obj == null || !(bool)obj)
			{
				bool flag = false;
				try
				{
					if (_getFollowM != null)
					{
						flag = (bool)_getFollowM.Invoke(setupComp, null);
					}
				}
				catch (Exception)
				{
				}
				_prevFollow[instanceID] = flag;
			}
			_active[instanceID] = true;
			try
			{
				if (_setFollowM != null)
				{
					_setFollowM.Invoke(setupComp, new object[1] { true });
				}
			}
			catch (Exception)
			{
			}
			float num = float.MaxValue;
			try
			{
				Player localPlayer = Player.m_localPlayer;
				if (localPlayer != null)
				{
					num = Vector3.Distance(setupComp.transform.position, localPlayer.transform.position);
				}
			}
			catch (Exception)
			{
			}
			if (num > MaxChaseDistance)
			{
				if (_pendingSince[instanceID] == null)
				{
					_pendingSince[instanceID] = Time.time;
				}
				Log("[Return] \"" + setupComp.name + "\" is " + num.ToString("F0") + "m away - beyond safe chase range, waiting to walk in or be recalled.");
				return false;
			}
			_pendingSince.Remove(instanceID);
			Component component = setupComp.GetComponent("CompanionAI");
			if (component != null)
			{
				if (_setFollowTargetM == null)
				{
					_setFollowTargetM = component.GetType().GetMethod("SetFollowTarget", BindingFlags.Instance | BindingFlags.Public);
				}
				try
				{
					if (_setFollowTargetM != null)
					{
						_setFollowTargetM.Invoke(component, new object[1] { playerGO });
					}
				}
				catch (Exception)
				{
				}
			}
			Component component2 = setupComp.GetComponent("CompanionCombatAI");
			bool flag2 = false;
			if (component2 != null)
			{
				Type type2 = component2.GetType();
				if (_isEngagedM == null)
				{
					_isEngagedM = type2.GetMethod("get_IsEngaged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				bool flag3 = false;
				try
				{
					if (_isEngagedM != null)
					{
						flag3 = (bool)_isEngagedM.Invoke(component2, null);
					}
				}
				catch (Exception)
				{
				}
				if (flag3)
				{
					if (_targetCreatureProp == null)
					{
						_targetCreatureProp = type2.GetProperty("TargetCreature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					}
					Character character = null;
					try
					{
						if (_targetCreatureProp != null)
						{
							character = _targetCreatureProp.GetValue(component2, null) as Character;
						}
					}
					catch (Exception)
					{
					}
					bool flag4 = true;
					if (character != null)
					{
						float num2 = Vector3.Distance(setupComp.transform.position, character.transform.position);
						flag4 = num2 > 4f;
					}
					if (flag4)
					{
						if (_disengageM == null)
						{
							_disengageM = type2.GetMethod("Disengage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						}
						try
						{
							if (_disengageM != null)
							{
								_disengageM.Invoke(component2, null);
								flag2 = true;
							}
						}
						catch (Exception)
						{
						}
					}
				}
			}
			Log("[Return] triggered for \"" + setupComp.name + "\"" + ((!flag2) ? "" : " - disengaged current fight (safe)"));
			return true;
		}
		catch (Exception ex8)
		{
			Log("[Return] Trigger failed: " + ex8.Message);
		}
		return false;
	}

	/// <summary>
	/// Fase 3.9: the "Chamar" button of the AI panel - brings <b>one</b> companion over through the recall machinery
	/// the command key already uses, never through a new teleport of its own.
	///
	/// <para>Inside the safe chase range this is the normal walk-in (<see cref="Trigger"/>, which also keeps pulling
	/// the companion until it is back within 18 m); further away than that <see cref="Trigger"/> would only set the
	/// "waiting" timer, so the button runs the same <see cref="DoTeleportRecall"/> that timer runs after 87 s - with a
	/// tighter radius, because the player asked for the companion to come to them. The caller is expected to have
	/// taken control of the companion and set it to Follow first (that is what the come-to-me key does too, see
	/// <c>CompanionInteract.RecallOne</c>), or <see cref="Trigger"/> would leave it alone by design.</para>
	///
	/// <para>The result is the real one, not a hopeful <c>true</c>: a teleport that could not run (this game isn't the
	/// owner of the companion yet - the ownership request of a multiplayer session takes a moment) returns false, and
	/// so does a walk-in that <see cref="Trigger"/> refused to start. The button's line depends on it.</para>
	/// </summary>
	internal static bool RecallToPlayer(Component setupComp, Player player)
	{
		if (setupComp == null || player == null)
		{
			return false;
		}
		float distance;
		try
		{
			distance = Vector3.Distance(setupComp.transform.position, player.transform.position);
		}
		catch (Exception)
		{
			return false;
		}
		if (distance > MaxChaseDistance)
		{
			// the same teleport the 87 s timer runs, asked for now and ending closer (the companion walks the rest).
			// It only runs where this game owns the companion; when it can't (the ownership request of a multiplayer
			// session is still on its way, for instance) the walk-in below would only leave the companion on the
			// "waiting" list, so the button reports the failure instead of claiming the companion is coming.
			if (!DoTeleportRecall(setupComp, player, setupComp.GetInstanceID(), CloseRange * 2f,
				"was called from the AI panel at " + distance.ToString("F0") + "m"))
			{
				return false;
			}
		}
		// the walk-in is the success case of the near companion - and the bool is the truth: Trigger refuses a
		// companion that isn't following or that moved out of range in the meantime
		return Trigger(setupComp, player.gameObject);
	}

	private static bool IsInFollowMode(Component setupComp)
	{
		try
		{
			Type type = setupComp.GetType();
			if (_nviewField == null)
			{
				_nviewField = type.GetField("_nview", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_actionModeHashField == null)
			{
				_actionModeHashField = type.GetField("ActionModeHash", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_nviewField == null || _actionModeHashField == null)
			{
				return true;
			}
			object value = _nviewField.GetValue(setupComp);
			if (value == null)
			{
				return true;
			}
			if (_getZdoM == null)
			{
				_getZdoM = value.GetType().GetMethod("GetZDO", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_getZdoM == null)
			{
				return true;
			}
			object obj = _getZdoM.Invoke(value, null);
			if (obj == null)
			{
				return true;
			}
			int num = (int)_actionModeHashField.GetValue(null);
			if (_zdoGetIntM == null)
			{
				_zdoGetIntM = obj.GetType().GetMethod("GetInt", new Type[2]
				{
					typeof(int),
					typeof(int)
				});
			}
			if (_zdoGetIntM == null)
			{
				return true;
			}
			int num2 = (int)_zdoGetIntM.Invoke(obj, new object[2] { num, 0 });
			return num2 == 0;
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static void Log(string s)
	{
		if (_logs < 60)
		{
			_logs++;
			CompanionsPlugin.Log?.LogInfo(s);
		}
	}

	internal static void OnPlayerRespawnFrom(string source)
	{
		try
		{
			OwnerDead = false;
			Log("[Return] " + source + ": respawn hook fired");
			Player localPlayer = Player.m_localPlayer;
			if (localPlayer == null)
			{
				Log("[Return] " + source + ": skipped - Player.m_localPlayer is null");
				return;
			}
			GameObject gameObject = localPlayer.gameObject;
			string text = localPlayer.GetPlayerID().ToString();
			if (_setupType == null)
			{
				_setupType = Type.GetType("Companions.CompanionSetup");
			}
			if (_setupType == null)
			{
				Log("[Return] " + source + ": skipped - CompanionSetup type not found");
				return;
			}
			if (_allCompanionsM == null)
			{
				_allCompanionsM = _setupType.GetMethod("get_AllCompanions", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_allCompanionsM == null)
			{
				Log("[Return] " + source + ": skipped - get_AllCompanions not found");
				return;
			}
			if (!(_allCompanionsM.Invoke(null, null) is IEnumerable enumerable))
			{
				Log("[Return] " + source + ": skipped - AllCompanions returned null");
				return;
			}
			if (_interactType == null)
			{
				_interactType = Type.GetType("Companions.CompanionInteract");
			}
			if (_interactType != null && _isOwnedM == null)
			{
				_isOwnedM = _interactType.GetMethod("IsOwned", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			int num = 0;
			int num2 = 0;
			foreach (object item in enumerable)
			{
				num++;
				Component component = item as Component;
				if (component == null)
				{
					continue;
				}
				bool flag = true;
				try
				{
					if (_isOwnedM != null)
					{
						flag = (bool)_isOwnedM.Invoke(null, new object[2] { component, text });
					}
				}
				catch (Exception)
				{
				}
				if (flag)
				{
					num2++;
					Trigger(component, gameObject);
				}
			}
			if (num == 0)
			{
				Log("[Return] " + source + ": AllCompanions was empty (0 found)");
			}
			else if (num2 == 0)
			{
				Log("[Return] " + source + ": found " + num + " companion(s), none owned by this player (ownerId=" + text + ")");
			}
		}
		catch (Exception ex2)
		{
			Log("[Return] " + source + " failed: " + ex2.Message);
		}
	}

	private static void TickPartyFollow(Component setupComp, int id)
	{
		try
		{
			object obj = _partyFollowActive[id];
			if (obj != null && (bool)obj)
			{
				return;
			}
			Vector3 position = setupComp.transform.position;
			List<Player> allPlayers = Player.GetAllPlayers();
			if (allPlayers == null)
			{
				return;
			}
			Player player = null;
			float num = 40f;
			for (int i = 0; i < allPlayers.Count; i++)
			{
				Player player2 = allPlayers[i];
				if (!(player2 == null) && !(player2 == Player.m_localPlayer))
				{
					float num2 = Vector3.Distance(position, player2.transform.position);
					if (num2 <= num)
					{
						num = num2;
						player = player2;
					}
				}
			}
			if (player == null)
			{
				return;
			}
			_partyFollowActive[id] = true;
			Component component = setupComp.GetComponent("CompanionAI");
			if (component != null)
			{
				if (_setFollowTargetM == null)
				{
					_setFollowTargetM = component.GetType().GetMethod("SetFollowTarget", BindingFlags.Instance | BindingFlags.Public);
				}
				try
				{
					if (_setFollowTargetM != null)
					{
						_setFollowTargetM.Invoke(component, new object[1] { player.gameObject });
					}
				}
				catch (Exception)
				{
				}
			}
			Log("[Return] \"" + setupComp.name + "\" - owner is dead, tagging along with \"" + player.name + "\" until they respawn.");
		}
		catch (Exception)
		{
		}
	}

	private static bool DoTeleportRecall(Component setupComp, Player p, int id)
	{
		return DoTeleportRecall(setupComp, p, id, TeleportRadius,
			"was unreachable too long (" + TeleportGraceSeconds.ToString("F0") + "s)");
	}

	/// <summary>
	/// The teleport recall itself: puts the companion down next to the player (within <paramref name="radius"/>,
	/// snapped to the floor), moves its ZDO with it, hides its map pin for a moment and points it back at the player
	/// with one of the <see cref="Greetings"/>. It only runs where this game owns the companion
	/// (<c>ZNetView.IsOwner</c>) - the same condition the 87 s timer has always had.
	///
	/// <para>Fase 3.9: the radius and the reason of the log are parameters now, because the AI panel's "Chamar"
	/// button asks for the very same recall with a tighter radius (the player asked for the companion to come to
	/// them, not to 30 m away). The old 87 s timer keeps 30 m and its own line.</para>
	///
	/// <para>And it <b>returns</b> what happened: <c>true</c> only when the teleport really ran. The ownership check
	/// below is a real early exit (a companion run by another player's game, or an ownership request still on its
	/// way), so the button that asked for it must not treat that as a success.</para>
	/// </summary>
	private static bool DoTeleportRecall(Component setupComp, Player p, int id, float radius, string reason)
	{
		try
		{
			_pendingSince.Remove(id);
			Type type = setupComp.GetType();
			if (_nviewField == null)
			{
				_nviewField = type.GetField("_nview", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			object obj = ((!(_nviewField != null)) ? null : _nviewField.GetValue(setupComp));
			bool flag = true;
			if (obj != null)
			{
				if (_isOwnerM == null)
				{
					_isOwnerM = obj.GetType().GetMethod("IsOwner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				try
				{
					if (_isOwnerM != null)
					{
						flag = (bool)_isOwnerM.Invoke(obj, null);
					}
				}
				catch (Exception)
				{
				}
			}
			if (!flag)
			{
				Log("[Return] \"" + setupComp.name + "\" is run by another player's game - the teleport recall was not run here");
				return false;
			}
			if (_managerType == null)
			{
				_managerType = Type.GetType("Companions.CompanionManager");
			}
			Vector3 vector = p.transform.position;
			if (_managerType != null)
			{
				if (_findSpawnPosM == null)
				{
					_findSpawnPosM = _managerType.GetMethod("FindSpawnPosition", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				}
				try
				{
					if (_findSpawnPosM != null)
					{
						vector = (Vector3)_findSpawnPosM.Invoke(null, new object[2]
						{
							p.transform.position,
							radius
						});
					}
				}
				catch (Exception)
				{
				}
			}
			setupComp.transform.position = vector;
			if (obj != null)
			{
				if (_getZdoM == null)
				{
					_getZdoM = obj.GetType().GetMethod("GetZDO", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				try
				{
					object obj2 = ((!(_getZdoM != null)) ? null : _getZdoM.Invoke(obj, null));
					if (obj2 != null)
					{
						MethodInfo method = obj2.GetType().GetMethod("SetPosition", new Type[1] { typeof(Vector3) });
						if (method != null)
						{
							method.Invoke(obj2, new object[1] { vector });
						}
					}
				}
				catch (Exception)
				{
				}
			}
			if (_hidePinM == null)
			{
				_hidePinM = type.GetMethod("HidePinFor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			try
			{
				if (_hidePinM != null)
				{
					_hidePinM.Invoke(setupComp, new object[1] { 4f });
				}
			}
			catch (Exception)
			{
			}
			Component component = setupComp.GetComponent("CompanionAI");
			if (component != null)
			{
				if (_setFollowTargetM == null)
				{
					_setFollowTargetM = component.GetType().GetMethod("SetFollowTarget", BindingFlags.Instance | BindingFlags.Public);
				}
				try
				{
					if (_setFollowTargetM != null)
					{
						_setFollowTargetM.Invoke(component, new object[1] { p.gameObject });
					}
				}
				catch (Exception)
				{
				}
				if (_talkField == null)
				{
					_talkField = component.GetType().GetField("_talk", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				object obj3 = ((!(_talkField != null)) ? null : _talkField.GetValue(component));
				if (obj3 != null)
				{
					if (_sayM == null)
					{
						_sayM = obj3.GetType().GetMethod("Say", new Type[2]
						{
							typeof(string),
							typeof(bool)
						});
					}
					string text = Greetings[_rng.Next(Greetings.Length)];
					try
					{
						if (_sayM != null)
						{
							_sayM.Invoke(obj3, new object[2] { text, false });
						}
					}
					catch (Exception)
					{
					}
				}
			}
			Log("[Return] \"" + setupComp.name + "\" " + reason + " - recalled to near the player and walking in.");
			return true;
		}
		catch (Exception ex7)
		{
			Log("[Return] teleport recall failed: " + ex7.Message);
		}
		return false;
	}
}
