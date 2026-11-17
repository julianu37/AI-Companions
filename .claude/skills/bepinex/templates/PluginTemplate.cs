// ---------------------------------------------------------------------------
// ModIA — template de plugin BepInEx 5.4.x para Valheim 1.0.x (Unity 6)
// Target: net472 · C#
//
// Como usar:
//   1. Copie para src/ModIA.<Feature>/ e renomeie a classe/arquivo.
//   2. Troque "modia.<feature>" pelo seu GUID real (ex.: "modia.companion").
//   3. Ajuste PluginName/PluginVersion.
//   4. Veja os references da skill: config.md, logging.md, harmony.md, build-deploy.md.
//
// Regras: sem segredos no código (use ConfigEntry/variavel de ambiente),
// logs pelo Logger, patches Harmony minimos e defensivos.
// ---------------------------------------------------------------------------

using System;
using BepInEx;                 // BaseUnityPlugin, BepInPlugin, BepInDependency, BepInProcess
using BepInEx.Configuration;   // ConfigEntry, ConfigDescription, AcceptableValueRange
using HarmonyLib;              // Harmony (HarmonyX)
using UnityEngine;             // MonoBehaviour / tipos do Unity

namespace ModIA.Feature
{
    // [BepInPlugin] e obrigatorio: GUID unico, nome legivel e versao.
    // GUID no formato "modia.<feature>" (permitido: letras, digitos, . _ -).
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]

    // Carrega o plugin apenas sob o processo do jogo.
    [BepInProcess("valheim.exe")]

    // Dependencia opcional do Jotunn. Se ele nao existir, o plugin ainda carrega.
    // (Flags reais no BepInEx 5: HardDependency e SoftDependency.)
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
    public class FeaturePlugin : BaseUnityPlugin
    {
        // ---- Metadados (devem casar com o [BepInPlugin] acima) -----------------
        public const string PluginGuid = "modia.<feature>";
        public const string PluginName = "ModIA <Feature>";
        public const string PluginVersion = "0.1.0";

        // Acesso global opcional (evite abusar; prefira injeção/parametros).
        internal static FeaturePlugin Instance { get; private set; }

        // ---- Configuracao (ver references/config.md) ---------------------------
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<int> _maxFollowers;
        private ConfigEntry<string> _backendUrl;

        // ---- Harmony -----------------------------------------------------------
        private Harmony _harmony;

        // Awake roda primeiro; aqui Config e Logger ja estao prontos.
        private void Awake()
        {
            Instance = this;

            // Config.Bind cria/le a opcao em BepInEx/config/modia.<feature>.cfg.
            _enabled = Config.Bind(
                "Geral", "Ativado", true,
                "Liga/desliga o companheiro.");

            _maxFollowers = Config.Bind(
                "Geral", "MaxSeguidores", 2,
                new ConfigDescription(
                    "Quantos NPCs podem seguir o jogador.",
                    new AcceptableValueRange<int>(0, 8)));

            // NAO coloque API keys como default. Aqui e so um endereco (nao-segredo).
            _backendUrl = Config.Bind(
                "Backend", "Url", "http://127.0.0.1:8080",
                "Endereco do cerebro de IA (LLM/STT/TTS).");

            // Reage a mudanca de uma opcao (ver config.md).
            _enabled.SettingChanged += (_, __) =>
                Logger.LogInfo($"[Config] Ativado = {_enabled.Value}");

            // Aplica os patches Harmony desta assembly.
            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
            }
            catch (Exception e)
            {
                // Patch quebrado nao deve derrubar o plugin silenciosamente.
                Logger.LogError($"[Harmony] Falha ao aplicar patches: {e}");
            }

            Logger.LogInfo($"{PluginName} v{PluginVersion} carregado (GUID={Info.Metadata.GUID}).");
        }

        // Start: todos os Awake (inclusive de outros plugins) ja rodaram.
        private void Start()
        {
            if (!_enabled.Value)
            {
                Logger.LogInfo("Companheiro desativado por configuracao.");
                return;
            }

            Logger.LogDebug($"Backend configurado em {_backendUrl.Value}.");
        }

        // Update roda a cada frame: NUNCA logue nem aloque aqui.
        private void Update()
        {
            if (!_enabled.Value) return;
            // ... logica por frame (leve) ...
        }

        // OnDestroy: limpeza. Sempre desfaca seus patches.
        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Instance = null;
            Logger.LogInfo($"{PluginName} descarregado.");
        }
    }

    // =========================================================================
    // Exemplo de patch Harmony (comentado). Descomente e ajuste ao alvo real.
    // Veja references/harmony.md.
    //
    // [HarmonyPatch(typeof(Player), nameof(Player.Jump))]
    // internal static class Player_Jump_Patch
    // {
    //     // Prefix roda ANTES do original. Retornar false pula o original.
    //     // Argumentos especiais: __instance, __result, __state, __args, ___campo.
    //     private static void Postfix(Player __instance, ref bool __result)
    //     {
    //         if (__instance == null) return; // defensivo
    //         // if (condicao) __result = true;
    //     }
    // }
    // =========================================================================
}
