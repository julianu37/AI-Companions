# A peça contra o próprio corpo: o que o Valheim faz quando o JOGADOR se auto-bloqueia

> **Por que este documento existe.** O nosso executor coloca peça **por código** (`Instantiate` + `OnPlaced`,
> `AI/BuildExecutor.cs:144-151`) e passa `ignore = <o próprio companheiro>` para o validador
> (`AI/BuildProbe.cs:591` com `ignore` em `:768`/`:793`/`:816`; o `Self(ai)` em `Jobs/MHR_Build.cs:9438-9441`).
> Com isso o **colocador não é obstáculo para a própria peça** e ele consegue pôr um `wood_floor` **1,00 m acima dos
> próprios pés** (corpo de 1,80 m). Medido em jogo em **05/11/2026**: terreno **38,78**, piso **39,78**, corpo em
> **37,90** (0,88 m dentro do terreno) — ver a pendência **(21)** do `AGENTS.md` e `docs/re/deck-clearance.md`.
>
> **A pergunta:** o que o **jogo** faz quando é o **jogador** que tenta colocar a peça sobre si mesmo? A resposta
> decide se devemos **recusar**, **mover** ou **nada**.
>
> **Fonte:** `assembly_valheim.dll` decompilado (**Valheim 1.0.20**, `%TEMP%\opencode\vh-decomp\valheim\`); as linhas
> citadas são desse arquivo. **Tudo abaixo foi lido no decompilado**, com o trecho transcrito. O que é **medido em
> jogo** está marcado como tal. Nada aqui foi re-medido nesta rodada (read-only, sem jogo).
>
> **Continua `docs/re/colocacao-corpo-e-altura.md`** (que já fixou: `ComputePenetration` sem limiar, `m_noClipping` só
> com 0,2 m e sem layer de personagem, "o jogo não move ninguém"). O que **esta** nota acrescenta: a pergunta do
> **auto-bloqueio** (o colocador contra a própria peça), o `PieceRayTest`, o que o jogador tem de escape, e a
> **recomendação de imitar**.

---

## Resposta curta

**O jogo RECUSA.** `Player.UpdatePlacementGhost` → `CheckPlacementGhostVSPlayers` roda **sempre** (sem nenhuma flag de
peça), pega **todos** os `Character` a **30 m do jogador que coloca** — **incluindo o próprio `Player.m_localPlayer`** —
e recusa com `PlacementStatus.BlockedbyPlayer` (`$msg_blocked`) se **qualquer** collider do fantasma penetrar **qualquer**
cápsula. **O colocador não é privilegiado.** `m_noClipping` e `m_spaceRequirement` **não** mudam isso.

E, principalmente: **o jogo não permite** ao jogador colocar um piso que atravesse o próprio corpo — mas **permite** um
piso **exatamente nos pés** (toque, não sobreposição). Essa distinção é a chave da recomendação do §5: o que falta ao
companheiro **não** é permissão, é **saber recuar um passo** — que é o que o jogador faz sem pensar quando o fantasma fica
vermelho.

---

## 1. `Player.UpdatePlacementGhost` — existe teste do fantasma contra o **próprio** jogador?

**Sim, um só, e a pergunta é feita fora do método: quem responde é o `CheckPlacementGhostVSPlayers`, chamado em
`:4092`.**

O corpo do método (`Player.cs:3793-4114`) tem **quatro consultas inline**, e uma quinta indireta — o `Raycast` do
`PieceRayTest` chamado em `:3809`:

| linha | consulta | é o próprio corpo? |
|---|---|---|
| `:3809` → `:4234` | `PieceRayTest` → `Physics.Raycast(câmera, frente, 50f, m_placeRayMask)` | não — rejeita collider com `Rigidbody` (`:4245`), e todo `Character` tem (`Character.cs:664`) |
| `:3845` | `Physics.OverlapSphere(point, m_blockRadius, GetMask("piece"))` | não — só `piece`, distância entre peças (`m_blockingPieces`) |
| `:3866` | `Physics.OverlapSphere(component.transform.position, m_connectRadius)` | não — `m_mustConnectTo` |
| `:4092` | **`CheckPlacementGhostVSPlayers()`** | ✅ **sim** — inclui o colocador |
| `:4100` | `TestGhostClipping(m_placementGhost, 0.2f)` | não — máscara sem `character*` (§3) |

**Não existe `Physics.OverlapBox`, `Physics.CheckBox` nem `CapsuleCast` em nenhum lugar do caminho de colocação.** O
grep de `Player.cs` inteiro dá **6** ocorrências de consulta (`:1752` auto-pickup, `:3845`, `:3866`, `:4179`, `:4186`,
`:4218`), mais o `Raycast` do `:4234` — nenhuma delas é caixa nem varredura de cápsula. **O teste de corpo é penetração
de collider, não de volume agregado.**

```csharp
// Player.cs:4092-4095  (a recusa por corpo, a última antes do veredito)
if (CheckPlacementGhostVSPlayers())
{
    m_placementStatus = PlacementStatus.BlockedbyPlayer;
}
...
// Player.cs:4113  (o status vira a cor do fantasma; nada depois o reescreve)
SetPlacementGhostValid(m_placementStatus == PlacementStatus.Valid);
```

E o status é imperativo na hora de colocar:

```csharp
// Player.cs:3041-3051  (TryPlacePiece chama o UpdatePlacementGhost DE NOVO antes do switch)
public bool TryPlacePiece(Piece piece)
{
    UpdatePlacementGhost(flashGuardStone: true);
    switch (m_placementStatus)
    {
    ...
    case PlacementStatus.BlockedbyPlayer:
        Message(MessageHud.MessageType.Center, "$msg_blocked");
        return false;
```

`UpdatePlacementGhost` roda também **todo frame**, no `LateUpdate` (`Player.cs:1656-1667`: `if (m_nview.IsOwner())
UpdatePlacementGhost(flashGuardStone: false);`) — é o que pinta o fantasma de vermelho enquanto você mira.

### O teste lateral que quase parece: `PieceRayTest` **não** pousa em corpo

```csharp
// Player.cs:4234-4245
if (Physics.Raycast(GameCamera.instance.transform.position, GameCamera.instance.transform.forward,
                    out var hitInfo, 50f, layerMask))
{
    ...
    if ((bool)hitInfo.collider && !hitInfo.collider.attachedRigidbody
        && Vector3.Distance(m_eye.position, hitInfo.point) < num)
```

Um `Character` **tem** `Rigidbody` (`Character.cs:664` `m_body = GetComponent<Rigidbody>()`), então **o raio da mira
nunca pousa num corpo** — nem no seu, nem no de outro. O corpo bloqueia pelo **fantasma** (§2), não pela superfície de
apoio. (É por isso que "colocar em cima do amigo" não existe no jogo: você não consegue nem apontar para ele.)

---

## 2. `CheckPlacementGhostVSPlayers` inclui o jogador local? Qual máscara, qual lista?

```csharp
// Player.cs:4195-4225
private bool CheckPlacementGhostVSPlayers()
{
    if (m_placementGhost == null)
    {
        return false;
    }
    List<Character> list = new List<Character>();
    Character.GetCharactersInRange(base.transform.position, 30f, list);      // <- :4202
    Collider[] componentsInChildren = m_placementGhost.GetComponentsInChildren<Collider>();
    foreach (Collider collider in componentsInChildren)
    {
        if (collider.isTrigger || !collider.enabled || collider.gameObject == m_placementGhost)   // <- :4206
        {
            continue;
        }
        MeshCollider meshCollider = collider as MeshCollider;
        if (meshCollider != null && !meshCollider.convex)                    // <- :4211-4214
        {
            continue;
        }
        foreach (Character item in list)
        {
            CapsuleCollider collider2 = item.GetCollider();                   // <- :4217
            if (Physics.ComputePenetration(collider, collider.transform.position, collider.transform.rotation,
                                           collider2, collider2.transform.position, collider2.transform.rotation,
                                           out var _, out var _))             // <- :4218
            {
                return true;
            }
        }
    }
    return false;
}
```

**A lista inclui o jogador local. Não é inferência de leitura, é a estrutura do registro:**

```csharp
// Character.cs:4013-4023
public static void GetCharactersInRange(Vector3 point, float radius, List<Character> characters)
{
    float num = radius * radius;
    foreach (Character s_character in s_characters)
    {
        if (Utils.DistanceSqr(s_character.transform.position, point) < num)
        {
            characters.Add(s_character);
        }
    }
}
```

| pergunta | resposta | prova |
|---|---|---|
| quem entra em `s_characters`? | **todo** `Character` no `Awake`, sem exceção | `Character.cs:662` `s_characters.Add(this);` |
| o `Player` local passa pelo mesmo `Awake`? | **sim** — `Player.Awake` chama `base.Awake()` | `Player.cs:647-650` `protected override void Awake() { base.Awake(); s_players.Add(this); ... }` |
| há filtro de "só jogadores"? | **não** — é `List<Character>`, criatura **também** bloqueia | assinatura `:4013` |
| há filtro de morto (`IsDead`)? | **não** no laço; só sai em `OnDestroy` | `Character.cs:757-760` `s_characters.Remove(this)` |
| o `ignore` do colocador existe no jogo? | **não existe** parâmetro; **nenhum** `Character` é excluído | `:4215` `foreach (Character item in list)` sem `continue` de identidade |
| qual é a "máscara"? | **não há layer mask na decisão** — a decisão é `ComputePenetration` contra a **cápsula** (`GetCollider()`, `Character.cs:3787-3790`) | `:4217-4218` |
| qual o raio da lista? | **30 m da posição de quem coloca** (não da peça) | `:4202` |
| qual o limiar de profundidade? | **nenhum** — `out var _, out var _`, descarta a distância | `:4218` |

**As únicas filtragens são do lado do FANTASMA** (`:4206-4214`), quatro condições: collider `isTrigger`, collider
desligado, `MeshCollider` **não convexo** e **o collider que está no root do fantasma**
(`collider.gameObject == m_placementGhost`). *(A coleta é `GetComponentsInChildren<Collider>()` sem `includeInactive`, e
o `!collider.enabled` já cobre o resto.)*

> **Ressalva honesta sobre o root.** O jogo **pula o collider do root** do prefab. Se um prefab tiver **só** um collider
> no root, ele não participa do teste de corpo. Não verifiquei, prefab a prefab, quais têm collider no root; o que sei é
> que o **alarme já tocou uma vez no nosso lado** com essa mesma armadilha (`docs/re/building-notes.md:260-269`: a
> bancada tem collider no root e o `Transform.IsChildOf` era `false` para ele mesmo, na `BuildSite.Clips`). Ou seja: a
> regra do jogo é *"os colliders do fantasma, menos o do root"*, e isso é um furo **conhecido e não medido** por peça.
> Para o `wood_floor` o efeito prático existe e está medido do nosso lado (§4.2).

**Segunda ressalva: o fantasma tem colliders desligados antes.** Em `SetupPlacementGhost` (chamado no
`SetSelectedPiece`), todo collider cujo **layer** não está em `m_placeRayMask` é **desativado**:

```csharp
// Player.cs:3542-3550
Collider[] componentsInChildren5 = m_placementGhost.GetComponentsInChildren<Collider>();
foreach (Collider collider in componentsInChildren5)
{
    if (((1 << collider.gameObject.layer) & m_placeRayMask) == 0)
    {
        ZLog.Log("Disabling " + collider.gameObject.name + "  " + LayerMask.LayerToName(collider.gameObject.layer));
        collider.enabled = false;
    }
}
```

`m_placeRayMask` (`Player.cs:670`) = `Default, static_solid, Default_small, piece, piece_nonsolid, terrain, vehicle` —
**`piece` está dentro**, então o collider de uma peça sobrevive. (Depois disso todas as transformes do fantasma vão para
a layer `ghost`, `:3552-3557` — mas `ComputePenetration` **não usa layer mask**, então a troca não desliga o teste.)

---

## 3. `m_noClipping` e `m_spaceRequirement` mudam esse teste?

**Nenhum dos dois.** São três regras independentes; só uma fala de corpo.

| flag | onde é lida | o que faz | fala de corpo? |
|---|---|---|---|
| — | `:4092` `CheckPlacementGhostVSPlayers()` | `BlockedbyPlayer` | ✅ **é a única** |
| `m_noClipping` (`Piece.cs:165`) | `:4100` `if (component.m_noClipping && TestGhostClipping(m_placementGhost, 0.2f))` | **acrescenta** um teste de **clipping com o cenário** → `PlacementStatus.Invalid` | ❌ **nunca** |
| `m_spaceRequirement` (`Piece.cs:175`) | `:3838` `component2.OtherExtensionInRange(component.m_spaceRequirement)` | distância entre **`StationExtension`** (melhorias de bancada) → `PlacementStatus.MoreSpace` | ❌ **nunca** |

```csharp
// Player.cs:4176-4193  (o TestGhostClipping: OverlapSphere com m_placeRayMask e limiar de 0,2 m)
private bool TestGhostClipping(GameObject ghost, float maxPenetration)
{
    Collider[] componentsInChildren = ghost.GetComponentsInChildren<Collider>();
    Collider[] array = Physics.OverlapSphere(ghost.transform.position, 10f, m_placeRayMask);
    ...
            if (Physics.ComputePenetration(collider, ..., collider2, ..., out var _, out var distance)
                && distance > maxPenetration)
            {
                return true;
            }
```

`m_placeRayMask` (`:670`) **não tem** `character`, `character_net` nem `character_noenv` — então `TestGhostClipping` é
**estruturalmente incapaz** de ver um corpo. E `m_spaceRequirement` só chega em `StationExtension.OtherExtensionInRange`
(`StationExtension.cs:91-101`), que compara `Vector3.Distance` entre extensões — nada de corpo.

**Consequência para nós:** a ordem dos testes no jogo é **corpo → clipping**, e a flag que o nosso `BuildSite` usa
para "espaço" (`AI/BuildSite.cs:86`, `m_noClipping`) é a **segunda** regra. **A nossa `BlockedByCharacter` está no
lugar certo** (`BuildProbe.cs:591`, antes do teste de apoio) — o que está errado não é o lugar, é o `ignore`.

---

## 4. O jogo permite ao jogador pôr um piso por cima da própria cabeça? Ele fica preso?

### 4.1 Não permite: é `$msg_blocked`

Um `wood_floor` a **1,00 m** acima dos pés de um corpo de **1,80 m** atravessa a cápsula em **0,70–0,80 m** (a faixa vem
da espessura do collider da laje, não medida — `support-rule-wearntear.md` §5/§8) → `:4218` devolve
`true` → `BlockedbyPlayer` → `TryPlacePiece` devolve `false` e mostra **"$msg_blocked"** (`:3049-3051`). **Não há
caminho na colocação normal em que o jogador se enterre** — porque ele **é** um dos `Character` que a lista devolve
(a ordem é a de `s_characters`, e ele está nela).

### 4.2 Mas permite um piso **nos pés** — e é aí que está a lição

O teste é **penetração**, não "estou perto". Um piso cujo **topo** cai exatamente no nível dos pés **toca** a cápsula
sem sobrepor nada (`ComputePenetration` responde `false` na tangência) → **é aceito**. Isso é o que deixa o jogador
subir num deck colocando os tiles em que já está pisando — o gesto normal de construção.

> **O que o nosso código já sabe disso, e onde ele fura:** o `BuildBody.Blocks` trata **toque como livre** e o comentário
> diz o porquê — *"touching exactly does not block either, which is what lets a floor be placed at the feet of somebody
> standing on it"* (`AI/BuildBody.cs:38-40`, teste em `:45`). Ou seja: a nossa janela **concorda** com o jogo no
> "piso nos pés". O fantasma é posicionado para que o **ponto de collider mais próximo** coincida com o ponto mirado
> (`Player.cs:4033-4047`: `collider.ClosestPoint(point)` → `position = point + (position - vector)`), então a peça fica
> **encostada** na superfície que o jogador mirou. **A diferença é o deslocamento vertical:** no jogo o piso fica **com o
> topo no ponto mirado**, e se o jogador mira o chão dos próprios pés a peça é **tangente**, não sobreposta; o **nosso**
> plano manda a peça para o **y do arquivo** (39,78) com o companheiro **em 38,78** — 1,00 m de diferença que **não é
> toque, é sobreposição de 0,70–0,80 m**. O jogo recusaria essa pose; nós a aceitamos porque **ignoramos o colocador**.
>
> **A ressalva que quase me escapou:** o `ignore` **não** compra só o bug do deck. Ele compra **qualquer** peça cuja caixa
> cruze a cápsula **no ponto onde o companheiro está** — inclusive uma peça **baixa** (viga, poste, escada) no chão em
> que ele pisa: `wood_beam_1` tem bounds **−0,200 .. +0,200** (`support-rule-wearntear.md` §5), então com o pivô no nível
> dos pés a caixa cruza a cápsula em **0,20 m** para cima e o `BuildBody.Blocks` **bloqueia**. Nesse caso o jogo também
> recusaria — e o **jogador recua um passo** e coloca. É a metade que o companheiro **não** sabe fazer sozinho, e é
> exatamente o que a camada 2 do §5 tem de dar a ele.

### 4.3 Existe teste de "headroom" no `Player` ou no `Character`? Não.

Já está provado em `docs/re/deck-clearance.md` §2 e confirmo o essencial: `CheckCapsule` existe **uma vez** em todo o
assembly (`Fireplace.cs:574`, fogueira) e `CapsuleCast` **zero** vezes; o `Character` não olha para cima (o único raio
para cima é `IsUnderRoof`, `Character.cs:1153-1156`, 20 m, para o calor das Ashlands); agachar **não** encurta o
collider (`Player.IsCrouching`, `Player.cs:6279-6282`, e zero atribuições a `collider.height`). **O jogo não tem como
"caber embaixo" — nem para o jogador.**

### 4.4 As três saídas que o JOGADOR tem e o companheiro não

| saída | o que dá ao jogador | prova | o companheiro tem? |
|---|---|---|---|
| **a recusa** | nunca cria a pose que prende | `:4092` → `:3049-3051` | ❌ nós colocamos por `Instantiate`, sem validação (`BuildExecutor.cs:144-151`) |
| **`UnderWorldCheck` a cada frame** | sai de baixo do terreno **no mesmo frame** | `Character.cs:882-902`, `:889` `if (m_underWorldCheckTimer > 5f \|\| IsPlayer())` | ❌ o companheiro cai no ramo de **5 s** |
| **`RemovePiece`** | tira a peça ofensora **de dentro** do buraco, até 5 m do olho | `Player.cs:2961-2963` (`m_removeRayMask` + `m_maxPlaceDistance`) | ❌ o "hammer" dele é o nosso `build.delete` |

```csharp
// Character.cs:882-901  (o resgate é o MESMO para os dois; só a CADÊNCIA muda)
m_underWorldCheckTimer += dt;
if (m_underWorldCheckTimer > 5f || IsPlayer())
{
    m_underWorldCheckTimer = 0f;
    float groundHeight = ZoneSystem.instance.GetGroundHeight(base.transform.position);
    if (base.transform.position.y < groundHeight - 1f)       // <- o limite de 1,00 m que perdeu por 0,88
```

**A leitura correta do incidente:** o jogador **nunca chega** no estado preso (a recusa acontece antes); o companheiro
chegou porque **nós pulamos a recusa**. Não é que o jogo seja leniente com o jogador e duro com o companheiro — é que o
companheiro **não passa pelo funil** (`TryPlacePiece` → `CheckPlacementGhostVSPlayers`), e ele também **não tem** o
`RemovePiece` para se desfazer do erro.

---

## 5. O que a gente deveria imitar

**Imitar o jogo é (a)** — recusar — **e o (a) do jogo é mais estreito do que "ignorar o colocador".**

O jogo testa o fantasma contra **todos** os corpos, **incluindo o de quem coloca**, com a **caixa real da peça** e a
**cápsula real do corpo**, sem limiar de profundidade — e **nunca move ninguém**. É exatamente o que a nossa
`BuildBody.Blocks` já faz; o ponto em que **nós** divergimos do jogo é o `ignore` do colocador.

E o medo que criou o `ignore` — *"sem ele o companheiro não consegue colocar peça nenhuma, ele está sempre perto"*
(`MHR_Build.cs:9435-9437`) — **está só parcialmente certo**. Ele acerta o **fato** (o companheiro está sempre perto,
frequentemente **dentro** do footprint) e erra o **tamanho**: a geometria do teste já libera uma boa parte das poses,
e o que sobra não é "nada" — é "esta pose, daqui":

| pose | `BuildBody.Blocks` com o colocador **contando** | o jogo aceita? |
|---|---|---|
| piso com o **topo nos pés** | `pieceTop <= capsuleBottom` → `return false` em `:45` → **livre** | ✅ sim (tangência) |
| parede/viga **ao lado** do corpo (XZ além de `halfX + r`) | `dx² + dz² >= r²` → **livre** (`:59`) | ✅ sim |
| peça **acima da cabeça** ou **sob os pés** | a janela vertical (`:45`) fecha → **livre** | ✅ sim |
| **peça baixa no chão dos pés** (viga `−0,2..+0,2`, poste, escada) | **bloqueia** (a caixa cruza `capsuleBottom`) | ❌ não — o jogador **recua um passo** |
| piso **atravessando o corpo** (o caso do deck) | **bloqueia** | ❌ não (é o bug) |

As duas últimas linhas são o preço real do `ignore`, e são **coisas diferentes**: a penúltima é **normal** e o jogador
resolve **andando**; a última é o **defeito**. O `ignore` cobre as duas com o mesmo `continue` (`BuildProbe.cs:793`/`:816`)
— é por isso que a correção **não** pode ser "tirar o `ignore` e ver no que dá": ela tem de vir com a camada 2.

*(O custo real de tirá-lo **não foi medido**: exigiria um run com a sonda do colocador ligada e o `blocked_by_player`
separado por "eu" × "outro". É a única parte desta recomendação que ainda é hipótese — ver §7 item 2.)*

**O que **não** imitar:** a **passividade** do jogo. O jogador se recupera sozinho porque tem recusa + resgate por frame
+ `RemovePiece`; o companheiro **não tem nenhum dos três**. Copiar só a recusa deixaria a peça presa na fila para sempre
(o `blocked_by_player` gasta tentativa e o corpo do próprio colocador não sai do lugar sozinho), e copiar só o "mover"
seria inventar um comportamento que o jogo não tem em lugar nenhum.

**Forma recomendada (3 camadas, do mais barato ao mais caro):**

1. **Não criar a pose que prende** (a camada do jogo): o `ignore` do colocador deixa de ser **blanket** e passa a ser
   **consciente da geometria** — o colocador entra no laço e é **liberado apenas** quando a pose é de **toque** ou o
   corpo está **fora da caixa** (as três primeiras linhas da tabela acima). Ou seja: o `Self(ai)` sai do `ignore`
   incondicional e vira uma pergunta a mais na mesma janela `BuildBody.Blocks` — **nada de constante nova**, e a
   **exceção do toque já existe** (`:45`). O caso que **precisa** continuar livre (peça baixa no chão dos pés) é
   resolvido pela camada 2, não por um `continue`.
2. **Fazer o colocador sair da pose** (o que o jogo terceiriza para o jogador): a peça recusada pelo **próprio** corpo
   não é "pulada" — ela é **re-tentada de outro ponto**. É o `WalkSpot`/`Stay` que já existe (`AI/BuildWalkSpot.cs`,
   `MHR_Build.cs` P0 "fica e coloca"): o ponto de onde se coloca passa a ser um ponto de onde **a cápsula não cruza a
   peça**. Isso é a regra do §6 de `deck-clearance.md` aplicada na **colocação** em vez do movimento, e é a única forma
   de a peça do **plano** (pose fixa, decidida pelo arquivo) subir sem mentir sobre a geometria. **Sem esta camada a
   camada 1 é regressão** — é ela que paga pelo `ignore` que se está tirando.
3. **Resgate para quem já está preso** (o que o jogo **não** tem e nós precisamos): a regra de teto do
   `deck-clearance.md` §6 (`!Fits(dest) && Fits(pos)`) mais um resgate próprio, porque o `UnderWorldCheck` do jogo
   **falha por 0,88 m contra 1,00 m** (`:893`) e o companheiro só é consultado a cada **5 s** (`:889`).

**Por que não "só mover" (b):** seria o único ponto em que o mod inventaria um comportamento inexistente no jogo, e ele
não resolve a causa — a peça continuaria nascendo **atravessada** no corpo; mover quem coloca depois é tratar o sintoma
(a depenetração é limitada a **2 m/s**, `Character.cs:675`, e o vão de 1,00 m **não é** vencível).

**Por que não "não recusar" (o que fazemos hoje):** o jogo **recusa**, e o custo que ele paga por recusar é **zero**
para o jogador (ele recua um passo e mira de novo). O nosso custo por recusar é **uma re-tentativa de pose** — que é o
que a camada 2 resolve. Trocar uma recusa por um corpo enterrado é trocar um custo pequeno e **reversível** por um
defeito grande e **irreversível** (o resgate do jogo **não** o salva, e o jogador teve de **demolir 41 peças** para
libertar o companheiro — `AGENTS.md` pendência 21 / §17.13).

---

## 6. Tabela final `classe → método → assinatura → observação`

| classe | método | assinatura | observação |
|---|---|---|---|
| `Player` | `UpdatePlacementGhost` | `private void UpdatePlacementGhost(bool flashGuardStone)` | `Player.cs:3793-4114`. As únicas consultas de física: `OverlapSphere` `:3845`/`:3866` (peças), `:4092` **corpos**, `:4100` clipping. **Sem `OverlapBox`/`CheckBox`/`CapsuleCast`** em todo o método. Roda por frame no `LateUpdate` (`:1656-1667`) e de novo no `TryPlacePiece` (`:3043`). |
| `Player` | `CheckPlacementGhostVSPlayers` | `private bool CheckPlacementGhostVSPlayers()` | `:4195-4225`. `GetCharactersInRange(base.transform.position, **30f**)` `:4202`; pula trigger/desligado/`MeshCollider` não convexo/**root do ghost** `:4206-4214`; `ComputePenetration` fantasma × `GetCollider()` **sem limiar** `:4218`. **Inclui o colocador** — não há parâmetro `ignore`. |
| `Player` | `TryPlacePiece` | `public bool TryPlacePiece(Piece piece)` | `:3041-3097`. `BlockedbyPlayer` → `$msg_blocked` + `return false` (`:3049-3051`). Daqui ao `PlacePiece` (`:3093`) **nada** toca em `Character`. |
| `Player` | `PlacePiece` | `public void PlacePiece(Piece piece, Vector3 pos, Quaternion rot, bool doAttack = true, bool cheated = false)` | `:3099-3166`. **Não valida nada** — `Instantiate` e segue. É o que a nossa rota A chama (`BuildExecutor.cs:120-125`) e o que o ataque que gera peça (`m_spawnOnTrigger`) usa em `Attack.cs:1509`. **A recusa mora no `TryPlacePiece`, não aqui** — e é por isso que a nossa `PlaceDirect`/rota A também não a têm. |
| `Player` | `PieceRayTest` | `private bool PieceRayTest(out Vector3 point, out Vector3 normal, out Piece piece, out Heightmap heightmap, out Collider waterSurface, bool water)` | `:4227-4268`. `Raycast` 50 m (`:4234`) com `m_placeRayMask`; **rejeita** collider com `attachedRigidbody` (`:4245`) → **nenhum corpo é superfície de apoio**; alcance `m_maxPlaceDistance` (**5 m**, `:171`) + `m_extraPlacementDistance`. |
| `Player` | `TestGhostClipping` | `private bool TestGhostClipping(GameObject ghost, float maxPenetration)` | `:4176-4193`. Só com `m_noClipping` (`:4100`), `OverlapSphere(..., 10f, m_placeRayMask)` + `distance > 0.2f` → `Invalid`. Máscara (`:670`) **sem `character*`**: não vê corpo. |
| `Character` | `GetCharactersInRange` | `public static void GetCharactersInRange(Vector3 point, float radius, List<Character> characters)` | `Character.cs:4013-4023`. Percorre `s_characters` (todo `Character` desde o `Awake`, `:662`; sai no `OnDestroy`, `:760`) por `DistanceSqr < r²`. **Sem filtro de jogador e sem filtro de morto.** |
| `Character` | `GetCollider` | `public CapsuleCollider GetCollider()` | `:3787-3790` = `m_collider` (`:663`). É a cápsula **do prefab** (1,80 m / r 0,30 no `Player`, medido: `building-notes.md` §17.16). |
| `Character` | `UnderWorldCheck` | `private void UnderWorldCheck(float dt)` | `:882-902`. `position.y < GetGroundHeight - **1f**` (`:893`). Cadência: **5 s** ou **todo frame** se `IsPlayer()` (`:889`). |
| `Piece` | `m_noClipping` | `public bool m_noClipping;` | `Piece.cs:165`. Consumida em `Player.cs:4100` **só** para o `TestGhostClipping`. **Não** altera o teste de corpo. |
| `Piece` | `m_spaceRequirement` | `public float m_spaceRequirement;` | `Piece.cs:175`. Consumida **uma vez** no assembly: `Player.cs:3838` → `StationExtension.OtherExtensionInRange` (`StationExtension.cs:91-101`, distância entre extensões). **Nada de corpo.** |
| `Player` | `RemovePiece` | `private bool RemovePiece()` | `:2961-2963`. `Raycast` com `m_removeRayMask` + `Distance < m_maxPlaceDistance` (5 m). É a saída do jogador preso — o companheiro **não** tem equivalente automático. |

---

## 7. O que NÃO deu para verificar

1. **Quais prefabs têm o collider no root.** O jogo pula o collider do root do fantasma (`:4206`); a consequência por
   peça (`wood_floor`, `wood_beam`, `piece_workbench`) **não** foi levantada. A bancada tem collider no root — está
   registrado do nosso lado (`building-notes.md:260-269`) — e isso significa que **ela talvez não bloqueie por corpo**
   nem no jogo. Não abri os assets.
2. **O custo de tirar o `ignore` do colocador, medido em jogo.** Esta é a única parte da recomendação que é **hipótese**:
   a geometria diz **quais** poses mudam de veredito (§5), mas **quantas por plano** caem nelas — e quanto tempo custa
   re-tentar de outro ponto — **não foi medido**. Exigiria um run com a sonda ligada e o `blocked_by_player` separado por
   *"eu"* × *"outro"*.
3. **A tangência exata no PhysX.** `ComputePenetration` na borda (`distance == 0`) é física nativa, não gerenciada. O
   que o CIL prova é *"não há limiar"* no teste de corpo (`out var _, out var _`) — o mesmo já registrado em
   `colocacao-corpo-e-altura.md` §3.
4. **Se o corpo do jogador pode ser deslocado por uma peça "por fora"** (peça colocada por nós/por outro cliente no
   corpo dele). A recusa do jogo é do **cliente que coloca**; o nosso `Instantiate` não pergunta nada, e o multiplayer
   está declarado fora de escopo (pendências 5/16/17 do `AGENTS.md`).
5. **A espessura do collider do `wood_floor`** continua não medida (0,10 m ou 0,25 m — `support-rule-wearntear.md`
   §5/§8): ela entra na conta do "0,80 m de sobreposição" como 0,70–0,80 m, sem mudar a conclusão.
