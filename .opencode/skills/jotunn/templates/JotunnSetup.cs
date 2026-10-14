using System.Collections.Generic;
using BepInEx;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ModIA.Companion
{
    /// <summary>
    ///     Plugin de exemplo do ModIA. Mostra o esqueleto mínimo para:
    ///     1) declarar a dependência do Jotunn;
    ///     2) registrar uma localização em Awake;
    ///     3) registrar um item simples assim que os prefabs vanilla estiverem disponíveis.
    ///     Copie e adapte — não é código de produção.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(Main.ModGuid)] // "com.jotunn.jotunn": sem o Jotunn o mod não carrega.
    public class CompanionPlugin : BaseUnityPlugin
    {
        // Convenção do projeto: GUID de plugin no formato "modia.<feature>".
        public const string Guid = "modia.companion";
        public const string Name = "ModIA Companion";
        public const string Version = "0.1.0";

        private CustomLocalization _localization;

        private void Awake()
        {
            // (1) Localização pode ser adicionada JÁ no Awake: são apenas tokens.
            AddLocalizations();

            // (2) Itens que CLONAM um prefab vanilla (ex.: "SwordIron") ainda não existem em
            //     Awake — os prefabs só ficam disponíveis mais tarde. O Jotunn avisa por evento.
            PrefabManager.OnVanillaPrefabsAvailable += AddItems;

            Logger.LogInfo($"{Name} v{Version} inicializado (aguardando prefabs vanilla).");
        }

        /// <summary>
        ///     Registra as traduções do mod. Tokens SEM o "$" à frente.
        /// </summary>
        private void AddLocalizations()
        {
            _localization = LocalizationManager.Instance.GetLocalization();

            _localization.AddTranslation("English", new Dictionary<string, string>
            {
                { "item_modia_totem", "Companion Totem" },
                { "item_modia_totem_desc", "A totem used to call your companion." },
            });

            _localization.AddTranslation("Portuguese_Brazilian", new Dictionary<string, string>
            {
                { "item_modia_totem", "Totem do Companheiro" },
                { "item_modia_totem_desc", "Um totem usado para chamar o seu companheiro." },
            });
        }

        /// <summary>
        ///     Cria e registra um item simples, clonando um prefab vanilla já existente.
        ///     Executado quando <see cref="PrefabManager.OnVanillaPrefabsAvailable"/> dispara.
        /// </summary>
        private void AddItems()
        {
            // Config: nome/descrição tokenizados + estação de criação.
            ItemConfig config = new ItemConfig
            {
                Name = "$item_modia_totem",
                Description = "$item_modia_totem_desc",
                CraftingStation = CraftingStations.Workbench,
                MinStationLevel = 1,
                Weight = 1.5f,
                StackSize = 1,
            };

            // AddRequirement(item, quantidade, quantidadePorNível).
            config.AddRequirement("Wood", 5, 2);
            config.AddRequirement("Stone", 10, 5);

            // Clona "SwordIron" e aplica a config. A receita é criada junto (há Requirements).
            CustomItem totem = new CustomItem("ModIA_Totem", "SwordIron", config);
            ItemManager.Instance.AddItem(totem);

            Logger.LogInfo("Item 'ModIA_Totem' registrado.");

            // Roda só uma vez: o Jotunn já mantém o item em cache na sessão.
            PrefabManager.OnVanillaPrefabsAvailable -= AddItems;
        }
    }
}
