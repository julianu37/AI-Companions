using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase E2 da construção autônoma: the <c>modia_build_find [prefab] [raio]</c> console command. It runs the real
/// placement search (<see cref="BuildSite.Find"/>) and prints the pose it chose and why the others were refused -
/// <b>without placing anything</b>, so a bad search never leaves a piece behind.
///
/// <para>The hint is the spot the player is looking at (the same camera ray and layers the command key uses), or
/// 3 m in front when nothing is under the crosshair. That is the "aqui" of the order, so what this command prints is
/// exactly where the companion would put the piece.</para>
/// </summary>
internal static class BuildFindCommand
{
	internal const string CommandName = "modia_build_find";

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
				"ModIA: teste da busca de local - mostra onde a peca caberia (e por que os outros pontos nao servem), sem colocar nada. Uso: modia_build_find [prefab] [raio]. Padrao: piece_workbench 8.",
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
			CompanionsPlugin.Log?.LogInfo($"[Build] Comando de console \"{CommandName} [prefab] [raio]\" registrado (F5).");
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
				CompanionsPlugin.Log?.LogWarning("[Build] A busca precisa de um mundo e de um jogador.");
				return;
			}
			string prefab = BuildProbeCommand.DefaultPrefab;
			float radius = 8f;
			if (args != null && args.Length > 1 && !string.IsNullOrEmpty(args[1]))
			{
				prefab = args[1].Trim();
			}
			if (args != null && args.Length > 2)
			{
				float parsed;
				if (float.TryParse((args[2] ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
				{
					radius = Mathf.Clamp(parsed, 1f, 30f);
				}
			}
			Piece piece = BuildProbe.PieceOf(prefab);
			if (piece == null)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] Nao achei a peca \"" + prefab + "\".");
				return;
			}
			Vector3 hint;
			Vector3 forward = player.transform.forward;
			if (!Hint(player, out hint))
			{
				Vector3 flat = player.transform.position + Flat(forward) * 3f;
				float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(flat) : flat.y;
				hint = new Vector3(flat.x, y, flat.z);
			}
			BuildSiteResult site = BuildSite.Find(piece, hint, forward, radius, 3, 8);
			if (site.Ok)
			{
				CompanionsPlugin.Log?.LogInfo("[Build] Local para " + Localization.instance.Localize(piece.m_name) + ": ("
					+ F(site.Pos.x) + ", " + F(site.Pos.y) + ", " + F(site.Pos.z) + ") yaw " + F(site.Rot.eulerAngles.y)
					+ " | " + F(site.Distance) + " m do ponto apontado | " + site.Tried + " tentativa(s)"
					+ (site.Tally.Count > 0 ? " | recusados: " + TallyText(site.Tally) : " | o proprio ponto apontado serve"));
				return;
			}
			CompanionsPlugin.Log?.LogWarning("[Build] Nenhum local para " + prefab + " em " + F(radius) + " m: "
				+ site.Tried + " tentativa(s), ultimo motivo " + site.Status
				+ (site.Tally.Count > 0 ? " | recusados: " + TallyText(site.Tally) : string.Empty));
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] A busca falhou: " + ex);
		}
	}

	/// <summary>The spot under the crosshair (the same ray and layers as the command key), or false when nothing is
	/// there.</summary>
	internal static bool Hint(Player player, out Vector3 hint)
	{
		hint = Vector3.zero;
		Camera camera = Utils.GetMainCamera();
		if (camera == null)
		{
			return false;
		}
		Ray ray = new Ray(camera.transform.position, camera.transform.forward);
		RaycastHit hit;
		if (!Physics.Raycast(ray, out hit, 50f, BuildProbe.PlaceMask(), QueryTriggerInteraction.Ignore))
		{
			return false;
		}
		if (hit.collider != null && hit.collider.GetComponentInParent<Player>() != null)
		{
			return false;
		}
		hint = hit.point;
		return true;
	}

	private static Vector3 Flat(Vector3 direction)
	{
		direction.y = 0f;
		return direction.sqrMagnitude < 0.01f ? Vector3.forward : direction.normalized;
	}

	private static string F(float value)
	{
		return value.ToString("F1", CultureInfo.InvariantCulture);
	}

	private static string TallyText(Dictionary<string, int> tally)
	{
		StringBuilder sb = new StringBuilder();
		foreach (KeyValuePair<string, int> kv in tally)
		{
			if (sb.Length > 0)
			{
				sb.Append(", ");
			}
			sb.Append(kv.Key).Append(" x").Append(kv.Value);
		}
		return sb.ToString();
	}
}
