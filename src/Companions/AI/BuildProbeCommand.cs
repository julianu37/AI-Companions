using System;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace Companions;

/// <summary>
/// Fase E0 da construção autônoma: the <c>modia_build_probe [prefab] [raio]</c> console command. It prints the
/// piece's sheet (<see cref="BuildProbe.DescribeText"/>) and the verdict of a ring of points around the player
/// (<see cref="BuildProbe.ScanText"/>) - the same data the DevBridge's <c>build.inspect</c> / <c>build.sites</c>
/// return as JSON.
///
/// <para>Why a console command and not only the bridge: the flags of <c>Piece</c> are not published anywhere, so
/// the player can check a piece in the game without a bridge running, and the answer stays in
/// <c>LogOutput.log</c> for the record. The notes live in <c>docs/re/building-notes.md</c>.</para>
/// </summary>
internal static class BuildProbeCommand
{
	internal const string CommandName = "modia_build_probe";

	internal const string DefaultPrefab = "piece_workbench";

	private static int _registered;

	/// <summary>Registers the command when the game builds its console (the same hook the TTS test uses).</summary>
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
				"ModIA: sonda de construcao - mostra a receita, as flags de posicionamento e o veredito de pontos em volta do jogador para uma peca. Uso: modia_build_probe [prefab] [raio]. Padrao: piece_workbench 4.",
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
			CompanionsPlugin.Log?.LogWarning($"[Build] Nao consegui registrar o comando de console \"{CommandName}\": " + ex);
		}
	}

	private static void Run(Terminal.ConsoleEventArgs args)
	{
		try
		{
			if (ZNetScene.instance == null)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] A sonda precisa de um mundo carregado.");
				return;
			}
			string prefab = DefaultPrefab;
			float radius = 4f;
			if (args != null && args.Length > 1)
			{
				string first = (args[1] ?? string.Empty).Trim();
				if (first.Length > 0)
				{
					prefab = first;
				}
			}
			if (args != null && args.Length > 2)
			{
				float parsed;
				if (float.TryParse((args[2] ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
				{
					radius = Mathf.Clamp(parsed, 0.5f, 40f);
				}
			}
			CompanionsPlugin.Log?.LogInfo("[Build] Sonda: " + prefab + " (raio " + radius.ToString("F1", CultureInfo.InvariantCulture) + ")");
			string sheet = BuildProbe.DescribeText(prefab);
			CompanionsPlugin.Log?.LogInfo(sheet);
			Piece piece = BuildProbe.PieceOf(prefab);
			if (piece == null)
			{
				return;
			}
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				CompanionsPlugin.Log?.LogWarning("[Build] A sonda de pontos precisa do jogador no mundo.");
				return;
			}
			CompanionsPlugin.Log?.LogInfo(BuildProbe.ScanText(piece, player.transform.position, radius, 2, 8));
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Build] A sonda falhou: " + ex);
		}
	}
}
