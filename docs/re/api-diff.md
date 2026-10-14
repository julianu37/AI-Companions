# API Diff — era do Thrall × Valheim 1.0.17

## Como este diff foi produzido (metodologia)

Não temos o `assembly_valheim.dll` da versão 0.218.20 para comparar lado a lado. O que temos é
evidência **forte e direta** do que aquela versão expunha: o **código consumidor do Thrall 1.0.11**
(que compilava e rodava contra 0.218.20). Se o Thrall chama `visEq.SetLeftItem(string, int)` é
porque esse era o contrato da época. Portanto:

* **Lado "era do Thrall"** = assinatura/visibilidade *usada* pelo Thrall (decompilado de
  `ValheimAIModLoader.dll` + `ValheimAIModLivePatch.dll`).
* **Lado "1.0.17"** = verificado por `ilspycmd 9.1.0` sobre o `assembly_valheim.dll` instalado
  (`C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed`), com números de
  linha citados para rastreabilidade.
* Onde a assinatura atual é compatível, marcamos `OK`. Onde difere, descrevemos **o que quebrou e
  por quê**.

| Componente | Era do Thrall | Alvo atual |
|---|---|---|
| Valheim | 0.218.20 | **1.0.17** (build 25730771) |
| Unity | 2021.3 / 2022.3 | **6000.0.75f1 (Unity 6)** |
| BepInEx | 5.4.22 | **5.4.23.5** |
| Jotunn | 2.21.3 | **2.30.2** |
| Compilado com | net472 | net472 (mantido) |

Severidade: 🔴 quebra compilação/execução · 🟠 quebra de comportamento (silenciosa) · 🟡 aviso/deprecação.

---

## A. Quebras de nível de linguagem (não compila mais) 🔴

Estas são as que impedem `dotnet build` do código "como o Thrall era". Resolvem-se trocando a
assinatura, mas **não** são apenas cosméticas: mudam o comportamento.

| # | Feature do Thrall | Chamada no Thrall (era 0.218.20) | 1.0.17 | Por que quebrou |
|---|---|---|---|---|
| A1 | Equipamento visível do NPC | `VisEquipment.SetLeftItem(string, int)`, `SetRightItem(string)`, `SetChestItem(string)`, `SetLegItem/SetHelmetItem/SetShoulderItem/SetUtilityItem/SetBeardItem/SetHairItem(string)`, `SetLeftBackItem/SetRightBackItem(string, int)` | `Set*(int itemHash, [int variant,] [int quality])` (L20 513–20 708) | A 1.0 migrou o esquema visual para **hashes de prefab + quality**. Nomes de string foram removidos. Precisa de `nome.GetStableHashCode()`. **Afeta `HumanoidNPC.SetupVisEquipment` (override) e a cópia de aparência do player no spawn.** |
| A2 | Ler texto do chat | `((TMP_InputField)((Terminal)Chat.instance).m_input).text` | `Terminal.m_input` agora é **`GuiInputField`** (L43 303) | Tipo do campo trocado (TMP → `GuiInputField`, wrapper do jogo). Todo o fluxo chat→cérebro depende disso. |
| A3 | `player_id` (Steam ID) | `ZNet.instance.GetPlayerList()[0].m_host` | `PlayerInfo.m_host` **não existe**; struct só tem `m_name`, `m_characterID`, `m_userInfo` (`CrossNetworkUserInfo`), `m_publicPosition`, `m_position` (L79 109) | A 1.0 passou a usar identidade **CrossNetworkUser** (`PlatformUserID`) para suportar Xbox/PS/Steam. **Sem `player_id`, todo o contrato HTTP do backend cai.** |
| A4 | Matar o Thrall no dismiss | `Character.AddPoisonDamage(100000f)` | `AddPoisonDamage(float damage, short variant)` (L2 704) — idem `AddFireDamage/AddSpiritDamage/AddFrostDamage/AddLightningDamage` | Ganharam o parâmetro `variant`. |
| A5 | Overrides de `HumanoidNPC` | `public override void ApplyArmorDamageMods(ref DamageModifiers)`, `public override void DamageArmorDurability(HitData)`, `public override bool CheckRun(Vector3,float)`, `public override void SetCrouch(bool)`, `public override void SetupVisEquipment(VisEquipment,bool)`, `public override void UseStamina(float,bool isHomeUsage = false)` | Bases agora **`protected virtual`** (L2 866 / L2 668 / L3 792 / L3 649 / L8 194) e `UseStamina` é `UseStamina(float stamina)` (L3 855) | C# proíbe **mudar a acessibilidade** num `override` (CS0507) e exige **assinatura idêntica** (CS0115). O `public override` do Thrall não compila. |
| A6 | Override de `ThrallAI` | `public override void SetAlerted(bool alert)` | base `protected virtual void SetAlerted(bool)` (L27 263) | Mesmo motivo de A5. |
| A7 | Patch de arma | leitura/escrita de `Humanoid.optimalWeapons`, `Humanoid.outofRangeWeapons`, `Humanoid.allWeapons` | `private static List<ItemDrop.ItemData>` (L6 817/6 819/6 821) | Campos ficaram **privados**. Patch do `EquipBestWeapon` não compila sem publicizer/reflection. |
| A8 | Patch de comida | escrita/leitura de `Player.m_foodUpdateTimer`, `m_foods`, `m_foodRegenTimer`, `GetTotalFoodValue(out,out,out)` | todos **privados** (L9 737/9 741/11 871) | Idem A7. |
| A9 | Acesso ao inventário do NPC | `humanoidNPC.m_inventory` (de fora da classe) | `protected readonly Inventory m_inventory` (L6 864) | Visibilidade reduzida. |
| A10 | Pin do NPC no mapa | `Minimap.instance.AddPin(pos, PinType, name, false, false, 9990L, "NPC")` e `pin.m_author == "NPC"` | `AddPin(..., long ownerID = 0L, PlatformUserID author = default)` (L58 474); `PinData.m_author` é `PlatformUserID` (L56 212) | Autor de pin virou identidade de plataforma → não dá mais para marcar pins com a string `"NPC"`. |
| A11 | Detecção de "submerso" | `Fish.GetWaterLevel(Vector3)` | agora **`private`** (L126 748) | Precisa de heurística própria (`ZoneSystem`/raycast) ou publicizer. |
| A12 | Ler tamed | `character.m_tamed` | `private bool m_tamed` (L623) | Usar `IsTamed()`. |
| A13 | Nome de skill do player | `Player.m_localPlayer.m_skills.GetSkillFactor(...)` | `m_skills` é `private` (L9 695); exposto via `GetSkillFactor(Skills.SkillType)` (L4 367) | Usar o método público. |
| A14 | Saber se o inventário do NPC está sob o cursor | `Player.m_hovering` | `private GameObject m_hovering` (L9 819) | Publicizer ou reimplementar com raycast. |
| A15 | Patch de dano | `Character.m_localPlayerHasHit = true`, `m_backstabTime` | ambos `private` (L635 / L469) | Publicizer. |
| A16 | Fechar balões de chat | `Chat.instance.m_worldTexts.Remove(...)`, `Chat.instance.m_hideTimer = 0` | `m_worldTexts`/`m_hideTimer` **privados** (L41 627/41 631); há `WorldTexts` público (L41 639) e `m_chatWindow` privado | Remover via `WorldTexts`, e "mostrar janela" por outro caminho. |
| A17 | Linha no chat | `((Terminal)Chat.instance).AddString(string, string, (MessageType)1, false)` | `AddString(string title, string text, Talker.Type type, bool timestamp = false)` (L46 033) | O enum do parâmetro mudou de `MessageType` para `Talker.Type`. |
| A18 | Balão sobre o NPC | `Chat.instance.AddInworldText(go, id, pos, (MessageType)2, user, text)` | 4º parâmetro é **`Talker.Type`** (L41 899) e o método é `private` | Ver A17; `Talker.Type { Whisper, Normal, Shout, Ping }` (L18 926). |

## B. Quebras de comportamento (compila, mas quebra em runtime) 🟠

| # | Feature | Era do Thrall | 1.0.17 | Risco |
|---|---|---|---|---|
| B1 | **Seguir** | `BaseAI` tinha `m_follow` e `BaseAI.Follow(GameObject,float)` era usado pela IA base | `BaseAI` **não tem mais `m_follow`**; o campo vive em **`MonsterAI`** (`private GameObject m_follow`, L27 668) e `BaseAI.Follow` é `protected` (L26 134) | O Thrall declara o próprio `m_follow` e faz override de `UpdateAI`. Funciona "por sorte", mas a IA base (agressividade, chase, `m_alertRange`) que o Thrall herdava de `BaseAI`/`MonsterAI` **não vê** esse follow. **Recomendação: derivar de `MonsterAI`** e setar o follow por um acessor. |
| B2 | Modos de combate | `m_alerted`, `m_aggravated` escrevíveis de fora | `private` em `BaseAI` (L25 876/25 880) | Sem publicizer, os modos Passive/Defensive/Aggressive só podem usar a API pública: `SetAggravated(bool, AggravatedReason)` (L26 853), `IsAggravated()`, `SetHuntPlayer(bool)`, `SetAlerted` (protected → só de dentro da classe). Menos controle fino do que o Thrall tinha. |
| B3 | Congelar alvo de combate | `__instance.m_targetCreature`, `m_updateTargetTimer = 1000000f` | `private` em `MonsterAI` (L27 642 / L25 633) | O truque "travar o alvo" do Thrall não é mais possível sem publicizer. |
| B4 | Desligar ataque a construções | prefixos em `BaseAI.FindRandomStaticTarget` / `FindClosestStaticPriorityTarget` | continuam existindo (`protected`, L27 001/27 025) | `OK` — mas se passarmos a derivar de `MonsterAI`, continuam sendo esses os pontos de corte. |
| B5 | Recompensas de XP/dano por tipo | `Game.instance.IncrementPlayerStat((PlayerStatType)(__instance.IsPlayer() ? 8 : 5), 1f)` | `PlayerStatType` mudou de valores/ordem (hoje `PlayerHits`, `EnemyHits`, `HitsTakenPlayers`, …) | Usar os **nomes** do enum, nunca os casts numéricos que o Thrall usava (risco de creditar a estatística errada). |
| B6 | Global keys | `ZoneSystem.instance.GetGlobalKey((GlobalKeys)24)` | enum `GlobalKeys` (L101 205) — `PassiveMobs` etc. | Idem: usar nomes, não o número 24. |
| B7 | `EnvMan`/`PlayerStatType`/`MessageType` por cast | `MessageHud.ShowMessage((MessageType)2, ...)` | `MessageType { TopLeft = 1, Center = 2 }` (L55 802) | Coincide hoje, mas é frágil — trocar por nomes. |
| B8 | `ItemType`/`SkillType` por número | `IsClothing` comparava `(int)itemType == 7/11/6/17`; `IsRangedWeapon` 4/15; `IsMeleeWeapon` 3/14/22; `skills.GetSkillFactor((SkillType)102)`; arma de arco `(int)skillType == 8` | enums `ItemDrop.ItemData.ItemType` e `Skills.SkillType` | Se a 1.0 inseriu/reordenou valores, o Thrall classificaria item como "roupa"/"arco" errado → `EquipBestClothes` e seleção de arma silenciosamente erradas. |
| B9 | Estatísticas de stamina global | `Game.m_staminaRate`, `m_staminaRegenRate`, `m_moveStaminaRate`, `m_enemyDamageRate`, `m_worldLevel` | existem (L999 20 etc.) | `OK`, mas são campos estáticos globais; com world modifiers da 1.0 podem variar. |
| B10 | Desligar autosave | patch `Game.UpdateSaving` sem parâmetros | `Game.UpdateSaving(float dt)` (L100 403) | Harmony aceita prefixo sem parâmetros, mas a lógica do Thrall podia devolver `true` no momento errado; revisar. |
| B11 | Sincronizar HP do NPC com a comida do player | `Player.UpdateFood` patch chamando `SetMaxHealth/SetMaxStamina` do NPC | `UpdateFood(float,bool)` agora `private`, com `GetTotalFoodValue` privado | O patch continua, mas o "espelhamento" de stats precisa ser reimplementado com API pública (`SetMaxHealth`/`SetMaxStamina`/`SetMaxEitr`). |
| B12 | `SimpleJson` | `using SimpleJson;` | Existe — mas **dentro do `Jotunn.dll`** | Não é API do jogo; se um dia removerem do Jotunn, o mod para de compilar. Considerar `Newtonsoft.Json` (já presente no jogo). |
| B13 | Varreduras de cena | `Object.FindObjectsOfType<GameObject>(true/false)` a cada 1–3 s em 300 m | continua existindo, mas é `[Obsolete]` e mais caro que `FindObjectsByType` | 1.0 com Unity 6: risco de stutter; migrar. |

## C. Deprecações / avisos 🟡

| Item | Detalhe |
|---|---|
| `Object.FindObjectsOfType<T>()` e `FindObjectsOfType<T>(bool)` | `[Obsolete(..., false)]` no Unity 6 → **warning**, não erro. Substituir por `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)`. |
| `WebClient` + `Task.WhenAny` para timeout | funciona em net472, mas é frágil (sem cancelamento real, sem retry). Preferir `HttpClient`/`UnityWebRequest`. |
| `AssetBundle.LoadFromStream` + `PrefabManager.AddPrefab` | funciona, mas a 1.0 introduziu **SoftReferenceableAssets** (`SoftReferenceableAssets.dll`) — o jeito recomendado hoje para assets próprios (ver skill `valheim-assets`). |
| `Jotunn.Managers.AssetBundleManager` | **removido** do Jotunn (não está em 2.30.2). O Thrall não usava, mas receitas antigas de mods usam. |

## D. Diff de framework (BepInEx / Jotunn / Unity)

| Item | Era do Thrall | 1.0.17 | Impacto |
|---|---|---|---|
| BepInEx | 5.4.22 | 5.4.23.5 | Sem quebra de API para plugin/config/Harmony (`Config.Bind<KeyCode>`, `BaseUnityPlugin`, `Harmony.PatchAll` seguem iguais). |
| Jotunn | 2.21.3 | 2.30.2 | Todas as APIs usadas pelo Thrall **existem** (verificado): `PrefabManager.AddPrefab`, `CommandManager.AddConsoleCommand`, `GUIManager.{Instance,CustomGUIFront,BlockInput,CreateWoodpanel,CreateText,CreateButton,CreateDropDown,CreateInputField,CreateToggle,NorseBold,AveriaSerif,AveriaSerifBold,ValheimOrange,ValheimYellow}`, `ExposedGameObjectExtension.HasAnyComponent(...)`, `SimpleJson.*`. Única remoção relevante: `AssetBundleManager`. |
| Unity | 2021/2022 | 6000.0.75f1 | `FindObjectsOfType` obsoleto; novos módulos (`UnityEngine.HierarchyCoreModule`, `Unity.InputSystem`); no geral a API de `Transform`/`UI`/`Physics` usada continua válida. |
| Módulo `ZInput` | — | **mora em `assembly_utils.dll`**, não em `assembly_valheim.dll` | Quem procurar `ZInput` no `assembly_valheim.dll` não acha (falso alarme). Idem: `Terminal`/`Console` estão em `assembly_valheim.dll`, `SimpleJson` no `Jotunn.dll`. |

---

## E. Os 5 maiores riscos de quebra (resumo executivo)

1. **`VisEquipment.Set*Item` virou hash-based (A1).** É o bloco mais visível: aparência, armadura
   equipada e clonagem do corpo do player mudam de string para `GetStableHashCode()` + `quality`.
   Sem isso o NPC fica "pelado" ou com visual errado.
2. **`ZNet.PlayerInfo.m_host` não existe mais (A3).** Sem `player_id` o contrato com o backend
   inteiro (instruções, áudio, logs) fica sem identidade estável. Precisa de `m_userInfo.m_id`
   (`PlatformUserID`) ou de um ID próprio (ex.: hash de `m_name` + `m_characterID`).
3. **Redução maciça de visibilidade em `Character`/`Humanoid`/`BaseAI`/`Player` (A5–A16, B1–B3).**
   O Thrall era um "fork" do `MonsterAI` que acessava campos internos `public`. Em 1.0 quase tudo
   virou `private`/`protected`. Estratégia: compilar contra assemblies **publicized** (ver skill
   `valheim-reverse-engineering` / `AssemblyPublicizer`) **ou** reescrever usando só API pública
   (mais seguro, porém com menos controle).
4. **Assinaturas de override incompatíveis (A5/A6).** `UseStamina`, `CheckRun`, `SetCrouch`,
   `ApplyArmorDamageMods`, `DamageArmorDurability`, `SetupVisEquipment`, `SetAlerted` — os
   `public override` do Thrall viram erro de compilação. É trabalho mecânico, mas bloqueia o build.
5. **`m_follow`/alvo/nome saíram de `BaseAI` para `MonsterAI` (B1).** A arquitetura de IA do Thrall
   (`: BaseAI` + campos próprios copiados do `MonsterAI`) precisa ser repensada para
   `: MonsterAI`, porque a 1.0 moveu o estado de combate para lá — caso contrário o NPC segue o
   comando na "conta própria", mas o motor de IA do jogo não o enxerga.

### Mitigações recomendadas (ordem de ataque)

1. Publicizar `assembly_valheim.dll` (PreBuild do Jotunn ou `AssemblyPublicizer`) e **isolar todo
   acesso a membro privado num único arquivo de "adaptação"**, para poder trocar por API pública
   depois.
2. Criar uma camada nossa de **`ICompanionApi`** em cima do jogo (seguir, mirar, equipar, falar),
   de modo que a lógica do cérebro/comandos não dependa de nomes internos instáveis.
3. Reimplementar a aparência via hashes (`GetStableHashCode`) e a identidade do jogador via
   `PlatformUserID`, com fallback local (`player_id` configurável).
4. Derivar o NPC de `MonsterAI` em vez de `BaseAI`.
5. Trocar `WebClient`/`SimpleJson` por `HttpClient`/`Newtonsoft.Json` e adotar streaming de áudio.
