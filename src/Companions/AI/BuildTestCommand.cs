using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase E1 da construção autônoma: the <c>modia_build_test [prefab] [auto|direct]</c> console command. It places
/// <b>one</b> piece 1,5 m in front of the player, paying from the player's bag, and verifies it - the whole executor
/// without any AI, so the foundation is proven before the job exists.
///
/// <para><c>auto</c> uses the route <see cref="BuildExecutor"/> would use for a companion (the owner's
/// <c>Player.PlacePiece</c> when it is the local player); <c>direct</c> forces the hand-written sequence, which is
/// the route used when the owner's <c>Player</c> is not loaded on this machine. Comparing the two in the game is
/// how the E1 acceptance list is checked.</para>
/// </summary>
internal static class BuildTestCommand
{
	internal const string CommandName = "modia_build_test";

	private const float Distance = 1.5f;

	private static int _registered;

	[HarmonyPatch(typeof(Console), "Awake")]
	private static class ConsoleAwake_Patch
	{
		private static void Postfix(Console __instance)
		{
			Register(__instance);
		}
	}

	private static void Register(Console console)
	{
		if (_registered != 0)
		{
			return;
		}
		try
		{
			new Terminal.ConsoleCommand(
				CommandName,
				"ModIA: teste do executor de construcao - coloca UMA peca 1,5 m a frente do jogador, pagando da mochila, e confere. Uso: modia_build_test [prefab] [auto|direct]. Padrao: piece_workbench auto.",
				(Terminal.ConsoleEvent)delegate(Terminal.ConsoleEventArgs args)
				{
					Run(args);
				},
				false,
				false,
				false,
				false,
				false,
				false,
				null,
				false,
				false,
				false);
			AccessTools.Method(typeof(Terminal), "updateCommandList")?.Invoke(console, null);
			Interlocked.Exchange(ref _registered, 1);
			CompanionsPlugin.Log?.LogInfo($"[Build] Comando de console \"{CommandName} [prefab] [auto|direct]\" registrado (F5).");
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning($"[Build] Nao consegui registrar o comando \"{CommandName}\": " + ex);
		}
	}

	private static void Run(Terminal.ConsoleEventArgs args)
	{
		try
		{
			Player player = Player.m_localPlayer;
			if (ZNetScene.instance == null || player == null)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] O teste precisa de um mundo e de um jogador.");
				return;
			}
			string prefab = BuildProbeCommand.DefaultPrefab;
			bool forceDirect = false;
			if (args != null && args.Length > 1 && !string.IsNullOrEmpty(args[1]))
			{
				prefab = args[1].Trim();
			}
			if (args != null && args.Length > 2 && !string.IsNullOrEmpty(args[2]))
			{
				forceDirect = args[2].Trim().Equals("direct", StringComparison.OrdinalIgnoreCase);
			}
			Piece piece = BuildProbe.PieceOf(prefab);
			if (piece == null)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] Nao achei a peca \"" + prefab + "\" (ou ela nao tem Piece).");
				return;
			}

			Vector3 forward = player.transform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.01f)
			{
				forward = Vector3.forward;
			}
			forward.Normalize();
			Vector3 pos;
			Quaternion rot;
			Dictionary<string, object> check = FindSpot(piece, player, forward, out pos, out rot);
			string status = Convert.ToString(check["status"]);
			if (!(check["ok"] is bool ok && ok))
			{
				CompanionsPlugin.Log?.LogWarning("[Build] Nenhum ponto valido perto do jogador (ultimo: " + status + " " + Reasons(check) + ")");
				return;
			}

			Inventory bag = player.GetInventory();
			if (!BuildExecutor.Consume(bag, piece))
			{
				Dictionary<string, int> missing = BuildExecutor.Missing(bag, piece);
				CompanionsPlugin.Log?.LogWarning("[Build] Falta material para " + prefab + ": " + MissingText(missing) + " (custo " + BuildExecutor.CostText(piece) + ")");
				return;
			}

			long creator = BuildExecutor.CreatorOf(null, player);
			BuildResult result = BuildExecutor.Place(null, piece, pos, rot, forceDirect);
			if (!result.Ok)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] Executor falhou (" + result.Route + "): " + result.Error);
				return;
			}
			GameObject go = result.GameObject;
			// the station registry only exists after CraftingStation.Start (next frame), so the immediate check skips it
			string verify = BuildExecutor.Verify(go, piece, creator, checkStation: false);
			if (go == null)
			{
				verify = BuildExecutor.VerifyAt(piece, pos, creator, out go, checkStation: false);
			}
			if (verify != null)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] Colocou mas a verificacao falhou: " + verify + " (rota " + result.Route + ")");
				if (go != null && (verify == "no_zdo" || verify == "no_piece" || verify == "not_found"))
				{
					UnityEngine.Object.Destroy(go);
					CompanionsPlugin.Log?.LogWarning("[Build] Objeto removido (nao era uma peca valida).");
				}
				return;
			}
			CompanionsPlugin.Log?.LogInfo("[Build] Colocado: " + Localization.instance.Localize(piece.m_name) + " em (" + F(pos.x) + ", " + F(pos.y) + ", " + F(pos.z) + ")"
				+ " | rota " + result.Route
				+ " | criador " + creator
				+ " | custo " + BuildExecutor.CostText(piece)
				+ " | conferindo a estacao no proximo frame");
			VerifyLater.Schedule(go, piece, creator);
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] O teste falhou: " + ex);
		}
	}

	/// <summary>
	/// The deferred half of the verification: <c>CraftingStation.Start()</c> registers the station on the next frame,
	/// so the "the game sees the station" check runs two frames later and logs the verdict. It is a throwaway object
	/// (destroyed by itself, and after 10 s as a safety net), never a per-frame cost of the mod.
	/// </summary>
	private sealed class VerifyLater : MonoBehaviour
	{
		private GameObject _target;
		private Piece _piece;
		private long _creator;
		private int _frames = 2;

		internal static void Schedule(GameObject target, Piece piece, long creator)
		{
			GameObject host = new GameObject("modia_build_verify");
			VerifyLater later = host.AddComponent<VerifyLater>();
			later._target = target;
			later._piece = piece;
			later._creator = creator;
			UnityEngine.Object.Destroy(host, 10f);
		}

		private void Update()
		{
			if (--_frames > 0)
			{
				return;
			}
			try
			{
				string reason = BuildExecutor.Verify(_target, _piece, _creator);
				if (reason == null)
				{
					CompanionsPlugin.Log?.LogInfo("[Build] OK: a peca esta no mundo, e do jogador, tem ZDO e"
						+ (_piece != null && _piece.GetComponentInChildren<CraftingStation>() != null ? " a estacao entrou no registro do jogo" : " a vida cheia"));
				}
				else
				{
					CompanionsPlugin.Log?.LogWarning("[Build] Verificacao (frame seguinte) falhou: " + reason);
				}
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] Verificacao (frame seguinte) estourou: " + ex.Message);
			}
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	private static string F(float value)
	{
		return value.ToString("F1", CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// The spot for the test: 1,5 m in front of the player first (the natural "here"), then rings of 2..6 m at 45°
	/// steps. It returns the <see cref="BuildProbe.CheckPoint"/> of the chosen point (the last one when none is
	/// valid, so the log says why). The job (E3) will use <see cref="BuildSite"/>, which does the full search; this
	/// is the test's small version, and it also proves the scan works.
	/// </summary>
	private static Dictionary<string, object> FindSpot(Piece piece, Player player, Vector3 forward, out Vector3 pos, out Quaternion rot)
	{
		Vector3 origin = player.transform.position;
		Dictionary<string, object> last = null;
		for (int i = 0; i < 9; i++)
		{
			Vector3 direction = i == 0 ? forward : Quaternion.Euler(0f, 45f * (i - 1), 0f) * forward;
			float distance = i == 0 ? Distance : 2f + (i - 1) % 3;
			Vector3 flat = origin + direction * distance;
			float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(flat) : flat.y;
			pos = new Vector3(flat.x, y, flat.z);
			rot = Quaternion.LookRotation(direction, Vector3.up);
			last = BuildProbe.CheckPoint(piece, pos);
			if (last["ok"] is bool ok && ok)
			{
				return last;
			}
		}
		pos = origin;
		rot = Quaternion.LookRotation(forward, Vector3.up);
		return last;
	}

	private static string Reasons(Dictionary<string, object> check)
	{
		if (check.TryGetValue("reasons", out object value) && value is List<string> reasons && reasons.Count > 0)
		{
			return "[" + string.Join(", ", reasons.ToArray()) + "]";
		}
		return string.Empty;
	}

	private static string MissingText(Dictionary<string, int> missing)
	{
		if (missing == null || missing.Count == 0)
		{
			return "(nada)";
		}
		List<string> parts = new List<string>();
		foreach (KeyValuePair<string, int> kv in missing)
		{
			string name = Localization.instance != null ? Localization.instance.Localize(kv.Key) : kv.Key;
			parts.Add(name + " x" + kv.Value);
		}
		return string.Join(", ", parts.ToArray());
	}
}
