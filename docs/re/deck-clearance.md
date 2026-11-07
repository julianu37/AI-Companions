# O vão do deck: a cápsula do corpo, o teto e a depenetração

> **Por que este documento existe.** Medido em jogo em **05/11/2026**: o companheiro fica **preso embaixo do piso
> térreo da própria obra**. Terreno (`terrain_check` min) **38,78 m**, piso do `wood_floor` **39,78 m** → vão de
> **exatamente 1,00 m**; o **Companheiro 3** foi encontrado em **y = 37,90** (**0,88 m DENTRO do terreno**) e só saiu
> quando o jogador **demoliu o piso** (`build.delete`). O `[Building] DeckLift` está no **padrão 1** e a config do
> jogador **grava 1**, então o vão existe em **toda** obra (`Core/ModConfig.cs:983`).
>
> **Decisão do jogador (05/11/2026):** a correção é **regra de teto/clearance no mover** — *"não ENTRAR onde a cápsula
> não cabe"*, **não** *"não passar por lugar apertado"* (andar sob beiral não pode virar estátua). As alternativas
> **`DeckLift = 0`** e **saia em volta** foram **recusadas**. Esta nota é o desenho geométrico dessa regra.
>
> **Fonte:** `assembly_valheim.dll` decompilado (`%TEMP%\opencode\vh-decomp\valheim\*`, Valheim 1.0.20) e o código do
> mod. Tudo abaixo tem `arquivo:linha`. **Confirmado no IL/decompilado**, não de memória.
>
> **Escopo:** este documento **não implementou nada** — é read-only sobre `src/`. O que ele entrega é a geometria e a
> recomendação do §6. Ligado à pendência **(21)** do `AGENTS.md`.

---

## 1. A geometria real de colisão de um `Character`

**Não existem `m_height` nem `m_radius` no `Character`.** A altura e o raio vivem **no `CapsuleCollider` do prefab**,
lido uma vez no `Awake`:

```csharp
// Character.cs:663
m_collider = GetComponent<CapsuleCollider>();
```

| membro | onde | o que é |
|---|---|---|
| `m_collider` | `Character.cs:340` (campo), `:663` (atribuído), `:3787-3790` (`GetCollider()`) | o `CapsuleCollider` **do prefab** — é a única fonte de altura/raio |
| `m_body` | `Character.cs:664` | `Rigidbody`; `:675` `m_body.maxDepenetrationVelocity = 2f` |
| `m_eye` | `Character.cs:243` | `public Transform m_eye` — é o **olho da câmera/mira**, **não** o topo do corpo |
| `m_head` | `Character.cs:245` (protected), `:674` | osso `HumanBodyBones.Head` do animator — **ponto de efeito/dano**, não de colisão |
| `GetHeight()` | `Character.cs:2182-2185` | `Mathf.Max(m_collider.height, m_collider.radius * 2f)` |
| `GetRadius()` | `Character.cs:2173-2180` | `m_collider.radius` para `Player`; nos outros, `* localScale.magnitude` |
| `GetTopPoint()` | `Character.cs:2168-2171` | `TransformPoint(m_collider.center) + m_visual.up * (m_collider.height * 0.5f)` |

```csharp
// Character.cs:2182-2185
public float GetHeight()
{
    return Mathf.Max(m_collider.height, m_collider.radius * 2f);
}
```

**Altura efetiva de um `Humanoid`/`Player`: 1,80 m, raio 0,30 m.** Três confirmações independentes:

1. **O log do próprio mod, em jogo:** `corpo na peça X - cápsula 39,90-41,70 m, r 0,30`
   (`docs/re/building-notes.md:2279`; emitido por `MHR_Build.CapsuleText`, `Jobs/MHR_Build.cs:7878-7885`, que
   formata `" - cápsula " + bottom + "-" + top + " m, r " + radius`) — **41,70 − 39,90 = 1,80 m** exatos, raio
   **0,30**. *(A `building-notes.md:2280` cita `:5074` para essa função; o sítio correto hoje é `:7878`.)*
2. **O `Pathfinding` do jogo** declara o agente `Humanoid` com **1,8 m** de altura e **0,4 m** de raio
   (`Pathfinding.cs:192-194`) — a altura casa; o **raio do navmesh é 0,4**, e isso é **outra coisa** que o collider
   (o navmesh é inflado para caber folgado; ver a nota no fim desta seção).
3. **`CapsuleOf`** (`AI/BuildProbe.cs:854-888`) devolve `bottom = bounds.min.y`, `top = bounds.max.y`,
   `radius = max(bounds.extents.x, bounds.extents.z)`; o fallback, quando o prefab não tem cápsula, é
   **1,8 m / 0,3 m** (`BuildProbe.cs:33-34`).

**O companheiro `HC_Companion` é o prefab `Player`** (com `PlayerController` e `Player` destruídos):
`Companion/CompanionTierData.cs:73-76` (`PrefabName = "HC_Companion"`, `SourcePrefab = "Player"`) +
`Companion/CompanionPrefabs.cs:66-69`. As variantes **Dverger** clonam `Dverger`/`DvergerMage`
(`CompanionTierData.cs:85-136`) e **podem ter outra cápsula** — é por isso que a sonda lê o collider real em vez de
assumir (e por isso a regra nova deve usar a cápsula **medida**, não a constante).

> **Cuidado com a armadilha do `AABB`:** `Collider.bounds` é o AABB **em mundo**. Para uma cápsula **vertical** (o caso
> de todo `Character`) `min.y`/`max.y` são as pontas reais e `extents.x/z` é o raio — **correto**. Para uma cápsula
> **deitada/inclinada** o AABB **superestima**. `BuildProbe.CapsuleOf` é seguro porque o corpo é vertical; uma regra
> nova que use `CheckCapsule` precisa dos **centros das esferas**, não do AABB
> (`Character.CapsuleCollider.center` é o centro da cápsula, `Character.cs:868`).

---

## 2. O jogo **não** tem teste de teto (headroom) em lugar nenhum do movimento

O caminho de movimento é `Character.UpdateMotion` → `UpdateWalking` → força no `Rigidbody`:

```csharp
// Character.cs:1364-1366 e :1602 (UpdateWalking), :1797 (o único empurrão)
private void UpdateMotion(float dt) { UpdateBodyFriction(); m_sliding = false; ...
if (vector5.magnitude > 0.01f) { m_body.AddForce(vector5, ForceMode.VelocityChange); }
```

**Varredura no assembly inteiro:**

| busca | resultado |
|---|---|
| `CheckCapsule` / `OverlapCapsule` | **1 ocorrência em todo o `assembly_valheim`**: `Fireplace.cs:574` (cápsula de **acender** fogueira) |
| `CapsuleCast` | **0 ocorrências** |
| `Character.cs` — testes físicos | `CheckSphere` (`:942`, fumaça), `Raycast` (`:975` lava, `:2111` chão), `OverlapSphereNonAlloc` (`:1723`, neve). **Nenhum** olha para cima |
| `Player.cs` — `ComputePenetration` | `:4186` e `:4218` — **colocação de peça** (`TestGhostClipping`, `CheckPlacementGhostVSPlayers`), **não** movimento |

```csharp
// Character.cs:1153-1156 - o único raycast "para cima" do Character, e nao e clearance:
private bool IsUnderRoof()
{
    return Physics.RaycastNonAlloc(base.transform.position + Vector3.up * 0.2f, Vector3.up, m_lavaRoofCheck, 20f, ...);
}
```

`IsUnderRoof` tem **20 m** de alcance (serve ao calor das Ashlands) e responde "tem telhado em algum lugar acima" —
**inútil** como teste de folga.

**Agachar não encurta o corpo.** `Player.IsCrouching()` é **só animação**:

```csharp
// Player.cs:6279-6282
public override bool IsCrouching()
{
    return GetCurrentAnimHash() == s_animatorTagCrouch;
}
```

`SetCrouch` (`Player.cs:6301-6304`) grava **um bool** e `UpdateCrouch` (`:6284-6299`) só escreve o bool do animator.
**O `CapsuleCollider.height` nunca é alterado** (grep por `collider.height =` no assembly: **0 resultados**). Ou seja:
não existe no jogo nenhum mecanismo de "caber embaixo" — nem para o jogador. O vão de 1,00 m é intransponível por
**projeto**.

**`UpdateLayer` não tem nada a ver com isso** (`Character.cs:854-880`): só troca a layer entre `character` e
`character_net` conforme a posse, e liga/desliga `isKinematic` ao dormir.

### 2b. O resgate que **deveria** ter libertado — e por que não libertou

`UnderWorldCheck` roda a cada **5 s** (ou todo frame no `Player`) e teleporta quem está fundo demais:

```csharp
// Character.cs:892-900
float groundHeight = ZoneSystem.instance.GetGroundHeight(base.transform.position);
if (base.transform.position.y < groundHeight - 1f)
{
    Vector3 position = base.transform.position;
    position.y = groundHeight + 0.5f;
```

O `GetGroundHeight` faz raycast **só na layer `terrain`**:

```csharp
// ZoneSystem.cs:676 e :2738-2742
m_terrainRayMask = LayerMask.GetMask("terrain");
... Physics.Raycast(origin, Vector3.down, out var hitInfo, 10000f, m_terrainRayMask);
```

**A conta que explica o incidente:** `37,90 < 38,78 − 1,00` → `37,90 < 37,78` é **FALSO**. O companheiro estava
**0,88 m** dentro do terreno, e o limite do resgate é **exatamente 1,00 m** — ele **não** disparou. É por isso que
ele **só saiu quando o piso foi demolido**, e não sozinho. O resgate do jogo não é a resposta para este defeito, e
**confiar nele seria um erro de 12 cm**.

---

## 3. O que acontece com uma cápsula de 1,80 m num corredor de 1,00 m

**O número existe e é do `Rigidbody`:**

```csharp
// Character.cs:675
m_body.maxDepenetrationVelocity = 2f;
```

É `Rigidbody.maxDepenetrationVelocity` do **Unity**: o **teto de velocidade (m/s)** com que o solver pode **empurrar o
corpo para fora de uma sobreposição**. `2 m/s` × `fixedDeltaTime` 0,02 = **4 cm por passo**. Não há escapatória
rápida: o corpo **não é cuspido** do vão, ele fica **prensado**.

**Onde o jogo usa o mesmo valor (é padrão da casa, não escolha do `Character`):** `Ship.cs:171` `2f`, `Vagon.cs:122`
`2f`, `Gibber.cs:106` `2f`; `ItemDrop.cs:1302`, `Smoke.cs:37`, `TombStone.cs:39`, `TreeLog.cs:61` usam **`1f`**.

### A direção do empurrão, pela aritmética da medição

A Unity resolve a sobreposição pela **saída mais curta**. Com o piso de **1,00 m** e o corpo de **1,80 m**, o corpo
**não cabe**: ele sobrepõe a laje em **0,80 m** por cima e o terreno por baixo. Como subir é impossível (laje) e ficar
parado está fora de questão, o corpo **desce** — para **dentro do heightmap**, que é um collider que ele não
consegue atravessar mas que também não o expulsa.

**A conta fecha com 2 cm:** pés em `39,78 − 0,10 (espessura da laje) − 1,80 =` **`37,88`**, e o **medido foi
`37,90`**. Ou seja: a cápsula foi empurrada para baixo **até o topo dela passar por baixo do fundo da laje** — o
corpo ficou **inteiro debaixo do piso e inteiro dentro do terreno**.

> **Ressalva honesta:** eu **não tracei o solver**. O que está provado é (a) o campo e o valor (`:675`), (b) o estado
> final medido (37,90 com terreno em 38,78) e (c) a aritmética que casa com "topo da cápsula = fundo da laje" dentro
> de 2 cm. A direção "para baixo" é **consistente com** os três, não uma observação direta do passo de física.

**Por que ele não sai sozinho:** (i) o vão é **uniforme** — não há lado livre, é o piso inteiro; (ii) `LedgeAhead`
(`AI/MHR_Mover.cs:2787`) e `DropAhead` (`:2802`) só olham **para baixo**, então o mover **aprova** o lugar; (iii) o
resgate do jogo tem limite de **1,00 m** e ele estava a **0,88 m** (§2b). As três portas fechadas ao mesmo tempo.

---

## 4. `CheckCapsule` vs `CapsuleCast`: qual usar, e com qual máscara

### O que a sonda atual faz

`BuildProbe.CapsuleOf` (`AI/BuildProbe.cs:854-888`) devolve `axis` (centro), `bottom`, `top`, `radius` — e
`BuildBody.Blocks` (`AI/BuildBody.cs:42-60`) cruza isso com a **caixa girada** da peça, com **toque = livre**:

```csharp
// AI/BuildBody.cs:45-48
if (pieceTop <= capsuleBottom || pieceBottom >= capsuleTop)
{
    return false;   // entirely above the head or under the feet: the body and the piece do not cross
}
```

Isso está **certo** e **serve** — para a pergunta dela: *"esta peça, nesta pose, prende um corpo?"*. Para o vão do
deck ela responderia **"sim, prende"** (laje 39,68–39,78 cai dentro da janela 38,78–40,58). **O problema não é a
geometria: é que ninguém pergunta isso no caminho do movimento.**

### A escolha

| chamada | pergunta que responde | serve? |
|---|---|---|
| **`Physics.CheckCapsule(p1, p2, r, mask)`** | *"este volume está livre?"* (booleano) | ✅ **é esta** |
| `Physics.OverlapCapsuleNonAlloc(...)` | *"quais colliders ocupam este volume?"* | ✅ quando se quer **nomear** o bloqueador (log / allow-list da peça que se vai colocar) |
| `Physics.CapsuleCast(...)` | *"o que o corpo encontra **viajando** de A até B"* | ❌ não resolve este caso |
| `Physics.Raycast(pos, Vector3.up, h)` | *"tem algo **exatamente acima** do eixo"* | ⚠️ mais barato, **estritamente pior** |

**Por quê `CheckCapsule`:** a pergunta é *"o corpo cabe **nesta posição**?"*, e a restrição aqui é **vertical** (uma
laje horizontal). O `CapsuleCast` varre **na direção do deslocamento** — um corpo andando na horizontal **nunca**
"atinge" uma laje paralela ao movimento: ele já está debaixo dela. Ao passo de **≤ 0,1 m por frame** (2–5 m/s ×
0,02 s) também não há túnel a evitar. O `CheckCapsule` é a **consulta de sobreposição**, que é literalmente o
critério.

O par `CheckCapsule` **já é o idioma do arquivo** — não é API nova:

```csharp
// AI/MHR_Mover.cs:2731 (SafeSpot) e :2884-2885 (a busca de degrau do Unstick)
if (Physics.CheckCapsule(hit.point + Vector3.up * 0.5f, hit.point + Vector3.up * 1.6f, 0.3f, Mask())) continue;
```

### A máscara: **`MHR_Mover.Mask()` já serve — nenhuma máscara nova**

```csharp
// AI/MHR_Mover.cs:75-79
static int Mask()
{
    if (s_mask == -1) s_mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
    return s_mask;
}
```

**Inclui** `piece` (toda peça construída — `wood_floor` inclusive), `terrain` (o heightmap) e `static_solid`
(rocha/árvore caída). **Exclui** de propósito: `piece_nonsolid` (decoração/plantas — o `Character` também a exclui
em `s_groundRayMask`, `Character.cs:683`), `Water`, `character*`, `item`, `smoke`, `ghost`, `hitbox`.

> **Única ausência digna de nota:** o `Mask()` do mover **não** tem `blocker`, e o `s_groundRayMask` do jogo tem
> (`Character.cs:683`). Para o vão do deck é irrelevante (o `blocker` é a parede invisível que sela masmorra), mas
> fica registrado: quem copiar essa máscara para outro fim deve decidir sobre o `blocker` de propósito.

---

## 5. `DropAhead` e o `1.5f + Vector3.up`: por que **não** é "insuficiente", é **pior**

```csharp
// AI/MHR_Mover.cs:2802-2810
public static bool DropAhead(Vector3 pos, Vector3 dir, float dist, float maxDrop)
{
    Vector3 p = pos + dir.normalized * dist;                  // <- so no plano horizontal
    if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 1.5f + maxDrop, Mask())) return true;
    return MHR_Water.DepthAt(hit.point) >= 1.0f;
}
```

**A geometria:** o raio **nasce em `pos.y + 1,5`** e desce. Uma laje a **1,00 m** acima dos pés está **abaixo** da
origem do raio — o raio **começa acima da laje** e a **primeira** coisa que encontra descendo é **a própria laje**,
a **0,5 m** de distância. O `Raycast` **tem sucesso**, `hit.point` é o **topo do piso do deck**, e o retorno é
`DepthAt(...) >= 1.0f` → **`false`** = *"não tem queda, pode andar"*.

**Portanto:** o problema **não** é o `1.5f` ser curto demais — é que `DropAhead` **não distingue chão de teto**, por
construção: ele pergunta *"há **alguma** superfície sob este ponto?"* e uma laje **satisfaz**. Subir a origem não
conserta: em qualquer altura, ou o raio nasce acima da laje (e a toma por chão — o caso de hoje) ou nasce abaixo (e
aí ele **não vê** a laje, e continua aprovando). **São as duas respostas erradas.**

O próprio arquivo já conhece a armadilha — só não a trata:

```csharp
// AI/MHR_Mover.cs:2423-2424 (HopLine)
// 1.4.84: from just above where the feet are, like walking: a roof or an upper floor overhead isn't the ground
bool ground = Physics.Raycast(new Vector3(p.x, prev.y + 1.5f, p.z), Vector3.down, out hit, 2f + maxDrop, Mask());
```

O comentário **diz** que um piso de cima não é o chão, mas o código **não filtra** — o raio pega o primeiro hit
descendo, seja laje ou chão.

### 5b. Achado colateral: `StepAhead` chama a laje de "degrau"

```csharp
// AI/MHR_Mover.cs:2481-2486
if (Physics.Raycast(pos + dir * 0.7f + Vector3.up * 2f, Vector3.down, out hit, 2.5f, Mask())
    && hit.point.y > pos.y + 0.35f && hit.point.y < pos.y + 1.3f) return true;
```

Origem em `pos.y + 2,0`; a laje do deck a **+1,00 m** é atingida a 1,0 m de distância e cai **exatamente** na janela
`]pos+0,35 ; pos+1,3[` → **`true`**. No vão do deck o mover **acredita que há um degrau de 1 m** e, em
`GoInner:503-510`, **pula** contra ele (`c.Jump(false)` + velocidade de avanço). Ou seja: além de não haver teste de
teto, a heurística de degrau **empurra** o companheiro para dentro da laje. Quem implementar a regra deve
**consultar a clearance antes do pulo**, senão os dois conspiram.

---

## 6. Recomendação — **um** teste: `CheckCapsule` no destino, com a cápsula medida

**Qual:** `Physics.CheckCapsule` **do corpo real, na posição candidata** — **não** raycast para cima, **não** os dois.

**Geometria (a do §1).** Com `r` = raio medido (0,30 no `Player`) e `h` = altura medida (1,80), os **centros das
esferas** são `pé + up*r` e `pé + up*(h − r)` = **`pé+0,30` até `pé+1,50`**, raio **0,30**. Some **1 cm** de folga
interna (`r = 0,29`, base em `pé+0,31`) para o teste **não colidir com o chão dos próprios pés** — é o mesmo tipo de
margem que o `SafeStep` já usa (`:3021`, esfera de 0,20 dentro de um corpo de 0,30).

```csharp
// desenho, nao implementado - a assinatura que a regra deve ter
static bool Fits(Character c, Vector3 feet)
{
    // BuildProbe.CapsuleOf(c, out axis, out bottom, out top, out r) -> h = top - bottom, r
    return !Physics.CheckCapsule(feet + Vector3.up * (r + 0.01f),
                                 feet + Vector3.up * (h - r), r - 0.01f, Mask(),
                                 QueryTriggerInteraction.Ignore);
}
```

**A regra é "não ENTRAR", não "não estar" — e isso é o ponto inteiro.** Se o teste for aplicado como *"só ande se
couber"*, o companheiro que **já está** no vão (o caso medido) vira **estátua** — ele nunca mais se move, e o
jogador de novo tem de demolir o piso. A condição é a **passagem**:

```csharp
// recusa o passo apenas quando o destino NAO cabe e o lugar atual CABE
if (!Fits(c, dest) && Fits(c, pos)) return false;   // nao entra
// ja dentro: qualquer direcao que saia (ou que nao piore) e permitida
```

Isso dá exatamente as duas metades que o jogador pediu: **não entrar** onde não cabe, e **não virar estátua** — e
andar **sob um beiral** (onde a cápsula cabe, com folga) continua livre, porque a consulta é do **volume inteiro**,
não de "tem algo acima".

**Onde chamar — três pontos, nesta ordem de valor:**

1. **`GoInner`** (`AI/MHR_Mover.cs:205`), imediatamente antes do `Steer` principal (`:499`). É o funil do caminho
   **normal** — a entrada única (`Go`, `:186`/`:194`) de Follow, Combat, jobs, build e comando. Todos chegam ao
   `:499` com o `dir` já final e o corpo ainda não comprometido; é o lugar onde "não entrar" é literal. **Cache de
   ~0,2 s por corpo** (ou por mudança de `dir` > 30°): um `CheckCapsule` por decisão de direção, não por frame de
   física.
   *(Ressalva: `Steer` tem **outros 4** sítios — `:1703`, `:1845`, `:2160`, `:2336` — todos nos caminhos de
   escada/chain/descida, que já têm `HeadRoom` próprio. A regra em `:499` **não** os cobre; eles não são o vão do
   deck.)*
2. **`Clear(pos, dir, dist)`** (`:3031`) e **`SafeStep`** (`:3008`). Os escolhedores de ponto já **funilam** por
   essas duas — `Sidestep` (`:1177`), `FindRouteStart` (`:3056`), e o `SafeStep` do build
   (`Jobs/MHR_Build.cs:8051`), além de 11 sítios em `CompanionCombatAI/Tactics/Safety`. Uma inserção aqui herda a
   regra em **todos** eles de graça, e evita que a saída de emergência jogue o corpo **para dentro** do vão.
3. **A consulta no `StepAhead`/pulo** (`:503-510` e `:2481`), para o §5b: **não pular** num "degrau" que é teto.

**Onde NÃO chamar:** `ApplyAgent` (`:3760-3782`). Ele só escolhe o `AgentType` do navmesh e a flag de evitar água, e
roda em outra cadência; é a camada errada para uma consulta de geometria por passo.

**Sobre o navmesh (por que ele não resolveu sozinho):** o mapa de rota dos companheiros é construído a partir dos
**colliders de física** (`Patches/MHR_CompanionNavMesh.cs:152`, `NavMeshCollectGeometry.PhysicsColliders`) com
`agentHeight` herdado do `Humanoid` (**1,8 m**, `:100-105` + `Pathfinding.cs:192`). Em teoria o vão de 1,00 m
**não** deveria gerar superfície de navmesh. Na prática ele **foi** para lá, e as explicações possíveis estão
**fora** do que eu medi (ver §7). A clearance no mover é o que **não depende** dessa resposta.

---

## 7. O que **não** deu para verificar

1. **Os números do prefab.** `1,80 m / 0,30 m` foram inferidos do **log do mod em jogo** (`cápsula 39,90-41,70, r
   0,30`) e do `agentHeight` do `Pathfinding` — **não** abri o asset do prefab `Player`. (`CapsuleOf` lê o real em
   runtime, então a regra não depende disso; o **desenho** depende.)
2. **A cápsula das variantes Dverger** (`Dverger`, `DvergerMage*`): **não medidas**. Podem ter altura/raio diferentes
   do `Player`, e é por isso que a regra deve usar `CapsuleOf`, nunca a constante.
3. **A direção que o solver escolhe.** Não tracei a física. Tenho o campo (`:675`), o estado final medido e a
   aritmética que casa em 2 cm (§3) — a conclusão é *consistente com*, não *observada no solver*.
4. **A espessura real do collider do `wood_floor`.** Já declarada como não verificada em
   `docs/re/building-notes.md:1973`: **0,41 m** pelo renderer, **~0,25 m** por uma tábua de 0,1 m. Usei **0,10 m** na
   conta do §3; com 0,25 m o número vira `37,73` (17 cm do medido) — a **conclusão não muda**, mas o "2 cm" é o
   cenário de laje fina.
4. **Por que o navmesh deu rota para baixo do piso.** O `CollectSources` usa colliders de física
   (`MHR_CompanionNavMesh.cs:152`) e o agente tem 1,8 m — a superfície **deveria** ter sido descartada. Não
   investiguei se o corpo entrou por uma **borda** descoberta do deck (o piso furado do §17.13 de
   `building-notes`, 41 peças apagadas), por `Pathfinding.GetPath` **snapar** o ponto (`Pathfinding.cs:458-500`,
   até 1,5 → 3 → 6 → 12 m), ou por o tile ter sido construído **antes** do piso e não re-bakeado. **É a próxima
   pergunta**, e ela é independente da correção.
5. **O comportamento em multiplayer.** Com a posse do corpo trocando, `CheckCapsule` roda no dono como qualquer
   física — mas **não testei** (e o multiplayer é o último item do projeto por decisão do jogador).

---

## 8. Resumo de uma linha por achado

| # | achado | prova |
|---|---|---|
| 1 | altura/raio **não** estão no `Character`; vêm do `CapsuleCollider` do prefab | `Character.cs:340`, `:663`, `:2182-2185` |
| 2 | corpo **1,80 m / r 0,30** | log `cápsula 39,90-41,70, r 0,30`; `Pathfinding.cs:192-194` |
| 3 | **nenhum** teste de teto no movimento; `CheckCapsule` só existe em `Fireplace.cs:574` | varredura do assembly |
| 4 | agachar **não** encurta o collider | `Player.cs:6279-6282`, `:6301-6304` |
| 5 | depenetração limitada a **2 m/s** | `Character.cs:675` |
| 6 | o resgate `UnderWorldCheck` **não** salvou: limite **1 m**, ele estava a **0,88 m** | `Character.cs:893` |
| 7 | `DropAhead` toma a laje por chão — o `1.5f` não é curto, é **o teste errado** | `MHR_Mover.cs:2802-2810` |
| 8 | `StepAhead` chama a laje de "degrau de 1 m" e manda **pular** nela | `MHR_Mover.cs:2481-2486`, `:503-510` |
| 9 | `Mask()` **já** tem `piece` + `terrain` + `static_solid` — máscara nova não é necessária | `MHR_Mover.cs:75-79` |

> **Próximo passo (não feito aqui — este documento é read-only sobre `src/`):** a regra do §6 entra em
> **`GoInner`** (`MHR_Mover.cs:205`, antes do `Steer` do `:499`) com a condição de **passagem**
> (`!Fits(dest) && Fits(pos)`), com `CheckCapsule` e a cápsula de `BuildProbe.CapsuleOf`, e ganha um caso no harness
> da seção **(s)** (`BuildBody`) provando as duas metades: **não entra** num vão de 1,00 m, e **não para** de se
> mover quando já está dentro.
