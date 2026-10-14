# API do jogo usada pelo Thrall — dependências a reimplementar

Levantamento feito sobre o decompilado do Thrall 1.0.11 (uso pessoal/estudo) cruzado com
`ilspycmd` sobre **`assembly_valheim.dll` do Valheim 1.0.17** (build 25730771, Unity 6000.0.75f1).

Coluna **Status 1.0**: `OK` = existe com a mesma forma de uso; `mudou` = existe mas com
assinatura/visibilidade diferente; `quebrado` = o uso do Thrall não compila/roda mais.

> Convenção de nomes: `Thrall` = código do mod; `jogo` = `assembly_valheim.dll`.
> Campos que o Thrall declarava **na própria classe** (`HumanoidNPC : Humanoid`,
> `ThrallAI : BaseAI`) foram marcados como `(mod)` e não contam como API do jogo.

---

## 1. `Character` (jogo)

| Membro | Uso no Thrall | Status 1.0 | Nota |
|---|---|---|---|
| `m_name` | ler/escrever nome | OK | `public string m_name` (jogo L250). |
| `m_health` | zerar para matar | OK | `public float m_health` (L442). |
| `m_walkSpeed`, `m_runSpeed` | copiar do player | OK | públicos (L302). |
| `m_damageModifiers` | ler mods do alvo | OK | `public HitData.DamageModifiers` (L456). |
| `m_onDeath` | `+= OnNPCDeath` | OK | `public Action m_onDeath` (L241). |
| `m_nview` | `IsValid/IsOwner/GetZDO/InvokeRPC` | mudou | agora `protected ZNetView m_nview` (L513) — não é acessível fora da hierarquia sem publicizer. |
| `m_zanim`, `m_animator`, `m_body`, `m_animEvent`, `m_seman`, `m_baseAI`, `m_lastHit`, `m_moveDir`, `m_wallRunning` | uso interno/override | mudou | todos `protected` (L515/517/509/519/617/521/637/477/683). Sobrescrevíveis por herança, não acessíveis de fora. |
| `m_tamed` | ler tamed | **quebrado** | agora `private bool m_tamed` (L623). Usar `IsTamed()`. |
| `m_localPlayerHasHit` | escrever no patch de `RPC_Damage` | **quebrado** | `private` (L635). |
| `m_backstabTime` | ler/escrever | **quebrado** | `private` (L469). |
| `CustomFixedUpdate(float)` | virtual base | OK | `public virtual` (L980). |
| `GetHealth/SetHealth/SetMaxHealth(float)` | HP do NPC | OK | `public ... SetMaxHealth(float)` (L3229); `Player` tem overload `(float,bool)`. |
| `GetHealthPercentage()` | fuga por HP baixo | OK | L3219. |
| `IsDead/IsPlayer/IsTamed/SetTamed` | checagens | OK | L3078/L4517/L4486 (região). |
| `StartAttack(Character,bool)` | atacar alvo/recurso | OK | `public virtual bool StartAttack(Character target, bool charge)` (L3544). |
| `InAttack/InDodge/InMinorAction/InIntro/IsTeleporting/IsSwimming/IsOnGround/IsFlying/IsDrawingBow/IsBlocking/IsCrouching/IsEncumbered/IsStaggering/IsKnockedBack` | gates de ação | OK | todos `public virtual` na faixa L3400–L4100. |
| `CanMove()` | checar movimento | OK | `public virtual bool CanMove()` (L3915). |
| `SetCrouch(bool)` | agachar | mudou | `protected virtual` (L3649) → o `public override` do mod **não compila**. |
| `SetWalk(bool)` | andar | OK | `public void SetWalk(bool walk)` (L3375). |
| `UseStamina(float)` | sobrescrito pelo mod | **quebrado** | base agora é `UseStamina(float stamina)` (L3855); o Thrall sobrescrevia `UseStamina(float, bool isHomeUsage)`. |
| `HaveStamina(float = 0)` | checagem | OK | `public virtual` (L3841). |
| `CheckRun(Vector3,float)` | sobrescrito | **quebrado** | base `protected virtual` (L3792) vs `public override` do mod. |
| `GetDamageModifiers(WeakSpot = null)` | resistências | OK | `public HitData.DamageModifiers GetDamageModifiers(...)` (L2580). |
| `GetBodyArmor()` | armadura | OK | `public virtual` (L2870). |
| `ApplyArmorDamageMods(ref DamageModifiers)` | sobrescrito | **quebrado** | base `protected virtual` (L2866). |
| `DamageArmorDurability(HitData)` | sobrescrito | **quebrado** | base `protected virtual` (L2668). |
| `SetupVisEquipment(VisEquipment,bool)` | sobrescrito | **quebrado** | base em `Humanoid` é `protected virtual` (L8194). |
| `OnDamaged(HitData)` | patch + override | OK | `protected virtual` (L2880); `Humanoid` sobrescreve (L7261) e **o patch do Thrall em `Humanoid.OnDamaged` continua válido**. |
| `OnDeath()` | patch | OK | `public virtual void OnDeath()` (L3078). |
| `RPC_Damage(long,HitData)` | patch reimplementado | OK | `private void RPC_Damage(long sender, HitData hit)` (L2412) — Harmony patcheia métodos privados. |
| `GetHoverText()` | patch (hover) | OK | `public virtual string GetHoverText()` (L4029). |
| `GetEyePoint()` | posição do balão de chat | OK | `public Vector3 GetEyePoint()` (L2363). |
| `GetRadius()/GetVelocity()/GetTopPoint()/GetStealthFactor()/AboveOrInLava()/InLava()` | IA | OK | L2344/…/L3704/L3699. |
| `AddFireDamage/AddSpiritDamage/AddPoisonDamage/AddFrostDamage/AddLightningDamage(float, short)` | dano elemental | **quebrado** | todos exigem `(float damage, short variant)` agora (L2672/2688/2704/2717/2730). O Thrall chamava com 1 argumento. |
| `BlockAttack/ApplyPushback/ApplyDamage` | patch de dano | OK | L2500–L2570. |
| `GetZDOID()` | identificar alvo | OK | L2511. |
| `IsDebugFlying/InGhostMode/InCutscene/InBed/InPlaceMode` | gates | OK | faixa L3600–L4200. |

## 2. `Humanoid : Character`

| Membro | Uso no Thrall | Status 1.0 | Nota |
|---|---|---|---|
| `m_inventory` | ler/escrever inventário | **quebrado** | `protected readonly Inventory m_inventory` (L6864) — o Thrall acessava direto de fora. |
| `m_leftItem/m_rightItem/m_chestItem/m_legItem/m_helmetItem/m_shoulderItem/m_utilityItem/m_hiddenLeftItem/m_hiddenRightItem` | vis equipment | mudou | `protected` (L6868+); há propriedades públicas `LeftItem`/`RightItem`/… |
| `m_visEquipment` | sobrescrever setup | mudou | `protected` (L6896). |
| `m_consumeItems` / `m_consumeItemEffects` | comer itens | mudou | `m_consumeItems` migrou para `MonsterAI` (L27624); `m_consumeItemEffects` segue em `Humanoid` (L6854). |
| `GetInventory()` | acesso | OK | — |
| `m_inventory.GetAllItems/AddItem/RemoveItem/RemoveAll/ContainsItem/MoveItemToThis/GetItemAt` | inventário | OK | `Inventory` L67822+: `AddItem(ItemData)` L67928, `RemoveItem(ItemData,int)` L68172, `ContainsItem` L68136, `GetItemAt(x,y)` L68457, `RemoveAll` L68945. |
| `EquipItem(ItemData, bool = true)` | equipar | OK | `public bool EquipItem(...)` (L7846). |
| `UnequipItem(ItemData, bool = true)` | desequipar | OK | L8065. |
| `IsItemEquiped(ItemData)` | checar arma | OK | L8432. |
| `GetCurrentWeapon()` | arma atual | OK | L7266. |
| `PickupPrefab(GameObject, int = 0, bool = true)` | dar itens | OK | L7367. |
| `HaveUniqueKey/AddUniqueKey` | quest keys | OK | L7385/L7390. |
| `EquipBestWeapon(Character, StaticTarget, Character, Character)` | patch reimplementado | OK | L7461. |
| `optimalWeapons` / `outofRangeWeapons` / `allWeapons` | patch de arma | **quebrado** | agora `private static List<ItemDrop.ItemData>` (L6817/6819/6821). |
| `SetHair(string)` | aparência | OK | L8421. |
| `DropItem(Inventory, ItemData, int)` | drag&drop | OK | `public bool DropItem(...)` (L7605). |
| `InAttack/IsBlocking/InDodge/InMinorAction/HaveQueuedChain` | patch de ataque | OK | overrides L7062–L8669. |
| `m_currentAttack/m_previousAttack/m_timeSinceLastAttack/m_lastCombatTimer/ClearActionQueue/StartAttackGroundCheck/m_currentAttackIsSecondary` | `StartAttack` reimplementado | OK | presentes em `Humanoid`. |
| `SetHair/SetModel/SetSkinColor/SetHairColor` (em `VisEquipment`) | ver §`VisEquipment`. | | |
| `m_foods` (comer) | já em `HumanoidNPC` (mod) | — | — |

## 3. `Player : Humanoid`

| Membro | Uso no Thrall | Status 1.0 | Nota |
|---|---|---|---|
| `m_localPlayer` | referência global | OK | — |
| `Awake()` / `Update()` | patches de init/loop | OK | — |
| `UpdateFood(float,bool)` | patch reimplementado | OK (patch) / **quebrado** (chamadas) | agora `private void UpdateFood(float dt, bool forceUpdate)` (L11816); dentro dele o mod chama membros privados. |
| `m_foodUpdateTimer`, `m_foods`, `m_foodRegenTimer` | patch de comida | **quebrado** | `private` (L9737/9741). |
| `GetTotalFoodValue(out,out,out)` | patch de comida | **quebrado** | `private` (L11871). |
| `SetMaxStamina(float,bool)` / `SetMaxEitr(float,bool)` / `SetMaxHealth(float,bool)` | sincronizar stats | OK | L15505/15515/15525. |
| `SetCrouch(bool)` | patch | mudou | `protected override` (L15664) — patch por nome segue válido. |
| `SetWalk(bool)` | patch em `Character` | OK | — |
| `GetPlayerName()` | nome no chat | OK | — |
| `GetClosestPlayer(Vector3,float)` | despertar por proximidade | OK | L14829. |
| `GetPlayerNoiseRange(Vector3,float=100)` | despertar por ruído | OK | L14920. |
| `IsPlayerInRange(Vector3,float)` | alcance | OK | overload L14892 (há versões com `playerID` e `minNoise`). |
| `m_skills.GetSkillFactor(SkillType)` | drain de stamina | mudou | `m_skills` agora `private` (L9695); usar `GetSkillFactor(...)` público (L4367). |
| `IsCrouching()` / `m_walk` / `m_crouch` | follow/sneak | OK/mudou | `IsCrouching()` público; campos de movimento são `protected`. |
| `m_hovering` | abrir inventário do NPC sob o cursor | **quebrado** | `private GameObject m_hovering` (L9819). |
| `TakeInput()`, `ShowTutorial()`, `GetPlayerSteamID`→ver `ZNet` | — | OK | — |
| `m_visEquipment.m_emptyBodyTexture/m_emptyLegsTexture` | copiar para o NPC | mudou | ver `VisEquipment`. |

## 4. `BaseAI` / `MonsterAI`

| Membro | Uso no Thrall | Status 1.0 | Nota |
|---|---|---|---|
| **Base para derivar** | `ThrallAI : BaseAI` | mudou | Em 1.0 os campos de combate/follow (`m_targetCreature`, `m_follow`, `m_updateTargetTimer`, `m_unableToAttackTargetTimer`, `m_consumeItems`, `m_fleeIf*`) estão em **`MonsterAI`** — recomenda-se `: MonsterAI`. |
| `m_follow` | seguir/perseguir | **quebrado** | não existe em `BaseAI`; é `private GameObject m_follow` em `MonsterAI` (L27668). O Thrall declarava o próprio campo. |
| `m_targetCreature` | alvo | mudou | `private` em `MonsterAI` (L27642). |
| `m_updateTargetTimer`, `m_unableToAttackTargetTimer` | congelar troca de alvo | mudou | `private` em `MonsterAI` (L25633/27666). |
| `m_alerted`, `m_aggravated` | modos | **quebrado** | `private bool` em `BaseAI` (L25876/25880). |
| `m_aggravatable`, `m_viewRange`, `m_alertRange`, `m_afraidOfFire`, `m_avoidFire`, `m_randomMoveRange`, `m_skipLavaTargets`, `m_maxChaseDistance`, `m_enableHuntPlayer` | IA | OK | públicos (L25823/25749/27554/25813/25811/25787/25819/27590/27580). |
| `m_spawnPoint`, `m_timeSinceHurt`, `m_character`, `m_nview`, `m_animator`, `m_tamable` | IA | mudou | `protected` (L25884/25896/25852/…/L25854) — ok se o código estiver em classe derivada. |
| `UpdateAI(float)` | override + patch | OK | `public virtual bool UpdateAI(float dt)` (L26014). |
| `MonsterAI.UpdateAI(float)` | referência de comportamento | OK | override em L27875 (L27533 é a classe). |
| `SetAlerted(bool)` | modos | **quebrado** | `protected virtual` (L27263) vs `public override` do mod. |
| `IsAlerted()` | checagens | OK | L27361. |
| `SetAggravated(bool, AggravatedReason)`, `IsAggravatable()`, `IsAggravated()`, `AggravateAllInArea(Vector3,float,AggravatedReason)` | agressividade | OK | L26853/26874/26879/27498. |
| `SetHuntPlayer(bool)`, `HuntPlayer()` | caçar player | OK | L27192/27204 (virtual). |
| `IsSleeping()`, `RPC_Wakeup` | sono | OK | `public virtual bool IsSleeping()` (L27421). |
| `SetTargetInfo(ZDOID)`, `GetTargetCreature()` | alvo | OK | L27366 / virtual. |
| `FindEnemy()` | achar inimigo | OK | `protected Character FindEnemy()` (L27104). |
| `HavePath(Vector3)` | pathfinding | OK | `protected bool HavePath(Vector3 target)` (L26646); há também `HavePath(Vector3,Vector3,AgentType)` em `Pathfinding`/`BaseAI`. |
| `CanSeeTarget(Character)`, `CanHearTarget(Character)` | percepção | OK | L26484/26453 (+ overloads estáticos). |
| `IsEnemy(Character)`, `IsEnemy(Character,Character)` | filtro | OK | L26897/26902. |
| `MoveTo(float,Vector3,float,bool)`, `MoveToWater`, `MoveAwayAndDespawn`, `Flee(float,Vector3)`, `AvoidFire`, `IdleMovement`, `RandomMovement`, `RandomMovementArroundPoint`, `StopMoving`, `LookAt`, `IsLookingAt`, `CanMove(Vector3,float,float)` | movimentação | OK | L26678/26148/26181/26265/26293/26195/26205/26331/26641/26613/26628/26817. |
| `GetPatrolPoint(out Vector3)` | patrulha | OK | `protected bool` (L25999). |
| `HaveFriendsInRange/HaveHurtFriendInRange/HaveFriendInRange` | `EquipBestWeapon` | OK | L27051/27101/27070. |
| `CanUseAttack(ItemData)`, `ChargeStart/ChargeStop/IsCharging`, `IsTakingOff` | combate | OK | L27435/27510/27519/27528/26054. |
| `m_pathAgentType` (novo) | pathfinding | novo | `BaseAI` usa `Pathfinding.instance.HavePath(..., m_pathAgentType)` — conceito de agent type é novidade da 1.0. |
| `FindRandomStaticTarget(float)`, `FindClosestStaticPriorityTarget()` | patches (desligar) | OK | `protected` (L27001/27025). |

## 5. Inventário / itens

| Membro | Uso | Status | Nota |
|---|---|---|---|
| `Inventory(name, icon, w, h)` ctor | criar container | OK | `Inventory` L67822. |
| `ItemData.m_dropPrefab / m_shared / m_stack / m_durability / m_quality / m_equipped / m_gridPos / m_variant / m_lastAttackTime` | inventário | OK | `ItemDrop.ItemData` L69xxx. |
| `ItemData.m_shared.m_name / m_itemType / m_skillType / m_damages / m_aiAttackRange / m_aiTargetType / m_aiPrioritized / m_attack / m_secondaryAttack / m_icons / m_questItem` | lógica de arma | OK | `SharedData` (L69448 é `m_shared`). |
| `ItemData.IsWeapon/IsEquipable/GetArmor/HavePrimaryAttack/HaveSecondaryAttack/IsTwoHanded()` | seleção | OK | L69592… |
| `ItemDrop.DropItem(ItemData, int, Vector3, Quaternion)` | dropar | OK | estático em `ItemDrop` L69025. |
| `ItemDrop.m_itemData / CanPickup(bool) / RequestOwn() / Load() / RemoveOne() / InTar() / GetHoverName() / m_nview` | pickup | OK | L70661/70727/70745(dentro do jogo)/70596. |
| `ItemDrop.RPC_RequestOwn(long uid)` | patch | OK | `private` (L70745) — patchável. |
| `Container.m_inventory` | inventário do NPC | OK | `Container` L122016. |
| `InventoryGui.Show(Container,int) / Hide() / m_currentContainer / m_dragGo / m_dragInventory / m_containerGrid / m_dragItem / m_dragAmount / m_moveItemEffects / SetupDragItem / ShowSplitDialog / UpdateCraftingPanel / m_animator` | drag&drop | OK | L49847+. |
| `InventoryGrid.Modifier` (enum nested) + `OnSelectedItem(InventoryGrid, ItemData, Vector2i, InventoryGrid.Modifier)` | patch | mudou | `OnSelectedItem` agora é `private` (L50722); o enum `Modifier` é **nested em `InventoryGrid`**. |
| `Container.m_inventory` / `Inventory.MoveItemToThis(Inventory, ItemData, int, int, int)` | mover itens | OK | L49779 (uso) — a assinatura de 5 args é a canônica. |

## 6. Rede / persistência

| Membro | Uso | Status | Nota |
|---|---|---|---|
| `ZNetView.m_persistent` | persistir NPC | OK | `public bool m_persistent` (L82804). |
| `ZNetView.GetZDO/IsValid/IsOwner/ResetZDO/Destroy/Register(string,Action<long>)/InvokeRPC(long,string,params object[])/InvokeRPC(string,params object[])/Everybody` | RPCs e estado | OK | L83064/83117/83122; `ZNetView.Everywhere` existe. |
| `ZDO.GetBool/Set/GetFloat/IsOwner/SetOwner/GetOwner/Persistent/TempRemoveEarmark` | dados | OK | `ZDO` L73408: `Persistent` L73469, `TempRemoveEarmark` L73626. |
| `ZDOMan.GetSessionID()/instance/DestroyZDO(ZDO)` | rede | OK | `ZDOMan` L76115; `DestroyZDO` L77102. |
| `ZNet.instance.GetPlayerList()` → `List<ZNet.PlayerInfo>` | obter `player_id` | **quebrado** | `PlayerInfo.m_host` **não existe mais** (struct L79109 só tem `m_name`, `m_characterID`, `m_userInfo` (`CrossNetworkUserInfo`), `m_publicPosition`, `m_position`). Usar `m_userInfo.m_id` (`PlatformUserID`) ou `m_name`. |
| `ZNet.IsDedicated/IsServer/GetPeers` | detectar single-player | OK | — |
| `ZNetScene.instance/GetPrefab(string)/FindInstance(ZDOID)/Destroy(GameObject)/m_prefabs/m_instances/RemoveObjects(List<ZDO>,List<ZDO>)/m_tempRemoved` | spawn e varredura | OK | L82180+ (`Destroy` L82291, `RemoveObjects` L82463, `m_prefabs` L82188). |
| `Object.Instantiate(GameObject, Vector3, Quaternion)` | spawn | OK | Unity. |

## 7. UI/feedback do jogo

| Membro | Uso | Status | Nota |
|---|---|---|---|
| `Chat.instance.AddInworldText(GameObject,long,Vector3,?,UserInfo,string)` | balão sobre o NPC | mudou | 4º parâmetro agora é `Talker.Type` (L41899), método `private`; enum `Talker.Type { Whisper, Normal, Shout, Ping }` (L18926). O Thrall passava `(MessageType)2`. |
| `Chat.instance.FindExistingWorldText(long)` / `m_worldTexts` | limpar balões | mudou | `m_worldTexts` agora `private` (L41627); há a propriedade pública `WorldTexts` (L41639). |
| `Chat.WorldTextInstance.m_gui` / `m_text` / `m_type` | gerenciar balões | OK | `m_gui` L41549, `m_text` L41555. |
| `Chat.instance.m_hideTimer` / `m_chatWindow` | mostrar janela | **quebrado** | `private` (L41631 / L41695). |
| `Chat.instance.HasFocus()` | gate de input | OK | — |
| `Chat.InputText()` | patch (chat→cérebro) | OK | `protected override void InputText()` (L41973). |
| `Terminal.AddString(string,string,?,bool)` | linha no chat | mudou | agora `AddString(string title, string text, Talker.Type type, bool timestamp = false)` (L46033); também há `AddString(string)` (L46055). |
| `Terminal.m_input` | ler texto do chat | **quebrado** | agora é `public GuiInputField m_input` (L43303), não `TMP_InputField`. |
| `MessageHud.instance.ShowMessage(MessageType,string,int,Sprite,...)` | avisos | OK | `ShowMessage(MessageType type, string text, int amount = 0, Sprite icon = null, bool showDespiteHiddenHUD = false, bool log = true)` (L55950); `MessageType { TopLeft = 1, Center = 2 }` (L55802) — igual ao que o Thrall assumia. |
| `Minimap.instance.AddPin(Vector3,PinType,string,bool,bool,long,?)` / `RemovePin(PinData)` / `m_pins` | pin do NPC | **quebrado** | assinatura atual: `AddPin(Vector3 pos, PinType type, string name, bool save, bool isChecked, long ownerID = 0L, PlatformUserID author = default)` (L58474). |
| `Minimap.PinData.m_author` | identificar pins do NPC | **quebrado** | agora `PlatformUserID m_author` (L56212), não `string`. |
| `Console.IsVisible()` / `Console.instance.Print(string)` | console | OK | `Console : Terminal` (L42882). |
| `Terminal.ConsoleCommand` (classe base) | console command custom | OK | L42xxx. |
| `Menu.IsVisible()` | gate de teclas | OK | — |
| `UserInfo` (`new`, `.Name`) | chat | OK | `public class UserInfo` com `public string Name` (L72922). |
| `InventoryGui.instance.m_animator.GetBool("visible")` | saber se inventário aberto | OK | — |
| `ZInput.GetKey/GetKeyDown(KeyCode,bool)` | teclas | OK | `ZInput` mora em **`assembly_utils.dll`**, não em `assembly_valheim.dll`. |
| `EventSystem.current.currentSelectedGameObject.GetComponent<InputField>()` | não capturar input em campos | OK | `UnityEngine.UI`. |
| `HUD`/`Sprite.Create`/`Texture2D.whiteTexture` | ícones | OK | Unity. |

## 8. Mundo / ambiente

| Membro | Uso | Status | Nota |
|---|---|---|---|
| `EnvMan.IsDay()/IsNight()/IsWet()/IsCold()/IsFreezing()` | estado do clima | OK | estáticos; usados em L27901/150213/11619/11618. |
| `EnvMan.instance.GetDayFraction()` / `GetCurrentEnvironment().m_name` | JSON de estado | OK | L96158 / L93247. |
| `Heightmap.FindBiome(Vector3)` | JSON de estado | OK | estático (L120257 usa). |
| `ZoneSystem.instance.GetGlobalKey(GlobalKeys)` | `PassiveMobs`, etc. | OK | `GlobalKeys` enum L101205 (o Thrall usava o cast `(GlobalKeys)24`, hoje é `GlobalKeys.PassiveMobs`). |
| `RandEventSystem.HaveActiveEvent()/InEvent()` | despawn de criaturas de evento | OK | L106630+. |
| `Game.instance.IncrementPlayerStat(PlayerStatType,float)` / `GetDifficultyDamageScalePlayer(Vector3)` / `m_worldLevelEnemyBaseAC` / `m_worldLevel` / `m_enemyDamageRate` / `m_staminaRate` / `m_staminaRegenRate` / `m_moveStaminaRate` | patch de dano/stamina | OK | `Game` L99712 (`m_worldLevelEnemyBaseAC` L99866, `m_worldLevel` L99920). |
| `Game.UpdateSaving(float)` | patch (desligar autosave) | mudou | agora recebe `float dt` (L100403) — prefixo sem parâmetros ainda funciona. |
| `SEMan.s_statusEffect*` (`Encumbered`, `Burning`, `Wet`, `Shelter`, `CampFire`, `Resting`, `Cold`, `Freezing`) + `AddStatusEffect/RemoveStatusEffect/HaveStatusEffect/ModifyStaminaRegen/ModifyMaxCarryWeight/ModifyHealthRegen/ModifyHomeItemStaminaUsage/OnDamaged` | sobrevivência do NPC | OK | `SEMan` L28697 (`s_statusEffect*` a partir de L28713). |
| `EffectArea.Type` + `IsPointInsideArea(Vector3,Type,float=0)` / `IsPointInsideNoMonsterArea` / `IsPointCloseToNoMonsterArea` | efeitos de ambiente | OK | L124438/124414/124426. |
| `ShieldGenerator.IsInsideShield(Vector3)` | ambiente | OK | `ShieldGenerator` L139827. |
| `Fish.GetWaterLevel(Vector3)` | checar submerso | **quebrado** | agora `private` (L126748). |
| `HitData.GetAttacker/GetTotalDamage/GetTotalPhysicalDamage/GetTotalElementalDamage/ApplyModifier/ApplyResistance/ApplyArmor/m_damage/m_point/m_weakSpot/m_statusEffectHash/m_itemLevel/m_skillLevel/m_dodgeable/m_backstabBonus/m_ignorePVP/HaveAttacker` | patch de dano | OK | `HitData` L130116. |
| `HitData.DamageModifiers.GetModifier(DamageType)` / `Apply(...)` / `DamageModifier` (enum) | resistências | OK | L130xxx. |
| `StaticTarget.FindClosestPoint/GetCenter/transform` | alvo estático | OK | `StaticTarget` L143195. |
| `TreeBase.m_dropWhenDestroyed/m_health/m_damageModifiers/m_logPrefab` / `TreeLog.m_damages/m_dropWhenDestroyed/m_subLogPrefab` | banco de recursos | OK | L146295 / L146538. |
| `MineRock.m_damageModifiers` / `MineRock5.m_damageModifiers` / `Destructible.m_damages` / `DropOnDestroyed` / `Pickable.Interact` / `CharacterDrop.m_drops` | banco de recursos | OK | L134176 / L134459 / L123592 / L124203 / L70962 / L5217. |
| `CharacterDrop.DropData.m_item` | drops | OK | L5217+. |
| `ZSyncAnimation.GetHash(string)` | animações | OK | estático (L87128). |
| `Physics.SphereCast/Raycast/OverlapSphere/SyncTransforms` + `LayerMask.GetMask` | busca espacial | OK | Unity. |

## 9. `VisEquipment` (quebrou em bloco)

| Membro | Uso no Thrall | Status 1.0 | Nota |
|---|---|---|---|
| `SetLeftItem/SetRightItem/SetLeftBackItem/SetRightBackItem(…)` | equipamento visível | **quebrado** | agora `(int itemHash, int variant, int quality)` / `(int itemHash, int quality)` (L20567/20602/…). O Thrall passava **string** (`m_dropPrefab.name`). |
| `SetChestItem/SetLegItem/SetHelmetItem/SetUtilityItem/SetBeardItem/SetHairItem(int)` | equipamento visível | **quebrado** | agora recebem `int itemHash` (L20632/20684/20696/20708). |
| `SetShoulderItem(int, int, int)` | | **quebrado** | L20668. |
| Conversão de nome→hash | — | novo | usar `Utils.GetPrefabName(obj.name).GetStableHashCode()` / `string.GetStableHashCode()` (L8198). |
| `SetModel(int)` | corpo masc/fem | OK | L20488. |
| `SetSkinColor(Vector3)` / `SetHairColor(Vector3)` | cores | OK | L15398/15407 (em `Player`) e `VisEquipment` L20513. |
| `m_skinColor` / `m_hairColor` | salvar cores | mudou | o Thrall lia `Humanoid.m_visEquipment.m_skinColor` — hoje `Humanoid.m_visEquipment` é `protected` e `VisEquipment` expõe via getters; salvar/ler cores precisa de acesso ao componente `VisEquipment` (público via `GetComponent`). |
| `m_isPlayer` / `m_emptyBodyTexture` / `m_emptyLegsTexture` | clonar visual do player | mudou | continuam existindo, mas conferir visibilidade no 1.0 (o Thrall atribuía direto de outro objeto). |

## 10. Jotunn 2.30.2 (framework, confirmado no `Jotunn.dll` instalado)

| Membro | Uso | Status | Nota |
|---|---|---|---|
| `PrefabManager.Instance.AddPrefab(CustomPrefab)` / `AddPrefab(GameObject)` | registrar `HumanoidNPC` | OK | confirmado. |
| `CommandManager.Instance.AddConsoleCommand(ConsoleCommand)` | `despawn_all` | OK | confirmado. |
| `Jotunn.Entities.ConsoleCommand` / `CustomPrefab` | bases | OK | confirmado. |
| `GUIManager.Instance` / `CustomGUIFront` / `BlockInput(bool)` | painéis | OK | confirmado. |
| `GUIManager.CreateWoodpanel(parent, min, max, pos, w, h, draggable)` | painel de madeira | OK | confirmado (sobrecarga com `draggable`). |
| `GUIManager.CreateText(...)` / `CreateButton(...)` / `CreateDropDown(...)` / `CreateInputField(...)` / `CreateToggle(...)` | widgets | OK | assinaturas confirmadas. |
| `GUIManager.NorseBold` / `AveriaSerif` / `AveriaSerifBold` / `ValheimOrange` / `ValheimYellow` | estilos | OK | confirmado. |
| `Jotunn.ExposedGameObjectExtension.HasAnyComponent(go, params string[]/Type[])` / `HasAnyComponentInChildren` | detecção de componentes | OK | confirmado. |
| `SimpleJson.JsonObject` / `SimpleJson.SimpleJson` / `JsonArray` | JSON | OK | **vêm do `Jotunn.dll`** (namespace `SimpleJson`), não do jogo. |
| `Jotunn.Managers.AssetBundleManager` | carregar bundle | **removido** | não existe mais em 2.30.2 (o Thrall já usava `AssetBundle.LoadFromStream` direto). |
| `LocalizationManager` / `ItemManager` / `PieceManager` | — | OK (não usado em runtime) | — |

## 11. Unity 6000.0.75f1

| API | Uso no Thrall | Nota |
|---|---|---|
| `Object.FindObjectsOfType<T>()` / `FindObjectsOfType<T>(bool)` | varreduras de cena | **`[Obsolete]` (warning, não erro)** no Unity 6. Migrar para `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)`. |
| `Microphone.Start/End/devices` | gravação | OK (`UnityEngine.AudioModule`). |
| `AudioClip.Create/GetData/SetData` | WAV | OK. |
| `AudioSource.PlayClipAtPoint` / `PlayOneShot` | reprodução | OK. |
| `Sprite.Create`, `Texture2D.whiteTexture`, `RectTransform`, `ScrollRect`, `VerticalLayoutGroup`, `ContentSizeFitter`, `Mask`, `Scrollbar`, `Dropdown`, `InputField`, `Slider`, `Toggle`, `Text` | UI | OK (`UnityEngine.UI` / `UnityEngine.UIModule`). |
| `TMP_InputField` | leitura do chat | ver `Terminal.m_input` (não é mais TMP). |
| `WebClient` (System.Net) / `Task` / `Aes` | rede/cripto | OK em net472, mas preferir `HttpClient`/`UnityWebRequest`. |
| `AssetBundle.LoadFromStream` | carregar prefab | OK, porém a 1.0 trouxe o pipeline **SoftReferenceableAssets** (`SoftReferenceableAssets.dll`) — ver skill `valheim-assets`. |

---

## 12. Resumo: as ~12 dependências mais críticas

1. `Humanoid` + `Character` (movimento, ataque, dano, inventário, `EquipBestWeapon`).
2. `BaseAI`/**`MonsterAI`** (`UpdateAI`, `SetFollowTarget`, `Flee`, `MoveTo`, agressividade).
3. `ZNetView` + `ZDO` + `ZDOMan` (estado replicado, persistência, RPCs).
4. `ZNetScene` (prefabs, `GetPrefab`, `Destroy`, `RemoveObjects`).
5. `Inventory` + `ItemDrop` + `Container` (itens, pickup, drop).
6. `Humanoid.EquipItem/EquipBestWeapon` + `Attack`/`HitData` (combate).
7. `VisEquipment` (aparência/equipamento visível — **quebrou**).
8. `Chat`/`Terminal` + `MessageHud` (feedback, chat→cérebro).
9. `SEMan` + `EnvMan` + `EffectArea` (sobrevivência e emissão de estado).
10. `InventoryGui` + `InventoryGrid` (drag&drop do inventário do NPC).
11. `ZInput` (em `assembly_utils.dll`) + `Minimap` + `Console`.
12. Jotunn (`PrefabManager`, `CommandManager`, `GUIManager`, `ExposedGameObjectExtension`)
    + `SimpleJson`.
