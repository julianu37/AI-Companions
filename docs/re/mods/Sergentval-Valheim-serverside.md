# Valheim-serverside (ServerAuthority) — Sergentval/Valheim-serverside (0.1.0 "TBD-2026", commit `8396f41`, 02/09/2026)

- **Fonte:** <https://github.com/Sergentval/Valheim-serverside> · **Licença:** **MIT** · **Última atualização:**
  02/09/2026 (commit `8396f41`) · **Jogo alvo:** Valheim 1.0.x (BepInEx 5, `net472`)
- **JOTUNN-ONLY?** **não** · **Repo público?** sim · **DLL fechada?** não · **Maturidade:** **alpha** — o CHANGELOG
  ainda marca `## [0.1.0] — TBD-2026` / "First public release" (`CHANGELOG.md:5-8`)
- **Nosso problema:** **posse de ZDO em servidor dedicado** (a nossa pendência (5), "posse do ZDO no spawn em
  multiplayer dedicado"): como um mod faz o **servidor** ser dono de ZDOs que não são de jogador.
- **Como li:** `shell` negado nesta sessão (sem clone); leitura por `raw.githubusercontent.com` no commit `8396f41`.
  Caminhos relativos à raiz do repo. **Clone pendente** para *project reference*.

## O que o README promete

> "Dedicated-server-side **ownership for all non-player ZDOs** in Valheim. Successor to ddormer/valheim-serverside."
> — descrição do repositório no GitHub

E o próprio CHANGELOG declara a promessa dura: *"Periodic sweep (configurable interval, default 5s) reclaims any
non-player ZDO whose owner is not the server"* — `CHANGELOG.md:9`; mais `ZDOMan.RemovePeer` postfix
(`CHANGELOG.md:10`).

## O que o código faz

### O mecanismo central: `m_sessionID` por reflexão + `SetOwner` + **`DataRevision++`**

- `OwnershipOps.ClaimForServer(ZDO, ClaimSource)` — **o único ponto de escrita**:
  - `m_sessionID` do `ZDOMan` lido **por reflexão** (não há getter público no 1.0) — `OwnershipOps.cs:7-14`;
  - idempotência: `if (zdo.GetOwner() == serverUid) return false;` — `OwnershipOps.cs:26`;
  - **`zdo.SetOwner(serverUid)`** (`:28`) e, na linha seguinte, **`zdo.DataRevision++`** (*"mark dirty for broadcast"*,
    `:29`) — é o passo que faz a mudança **chegar aos clientes**; sem ele a posse muda só no servidor.
  - log com `prev_owner`/`src` (`:31`) e contadores por `ClaimSource` (`Diagnostics.RecordClaim`, `:30`).
- Chamadores (`docs/architecture.md`): **A** `ZDOMan.RPC_ZDOData` postfix, **B** `ZDOMan.RemovePeer` postfix,
  **C** sweep periódico. Os três passam por `ClaimForServer`.

### O caminho A é **fachada vazia** (promete × faz, dentro do mesmo arquivo)

- `CreationPatches.RPC_ZDOData_Postfix` (`Patches/CreationPatches.cs:17-44`) **não tem corpo útil**: só comentários
  explicando que não dá para saber quais ZDOs o lote tocou sem transpiler, e que *"for v0.1.0 we rely on the periodic
  sweep + the disconnect patch"* (`:29-38`). O `docs/architecture.md` é honesto: *"Path A … currently a **no-op
  scaffold**; primary mechanism is the sweep"*.
- **Lição para nós:** "claim no RPC de criação" é o que todo mundo tenta e ninguém consegue sem transpile. Não
  desenhar a nossa posse em cima de `RPC_ZDOData`.

### O sweep: MonoBehaviour + reflexão em `m_objectsByID`

- `Sweeper` é um `MonoBehaviour` que roda `SweepLoop` (`Sweeper.cs:39-62`): espera `ZNet`/`ZDOMan` (`:42-43`), então
  a cada `SweepIntervalSeconds` (default **5 s**, `:49`) faz uma passada.
- `RunOneSweep` (`Sweeper.cs:64-111`): lê a tabela interna `ZDOMan.m_objectsByID` por reflexão (`:9-16`), itera, e
  para cada ZDO: pula se já é do servidor (`:87`), resolve o prefab por `ZNetScene.GetPrefab(hash)` (`:89-95`),
  **classifica** (`Categorizer.Classify`, `:97`), pula `Player` (`:98`) e consulta a política
  (`ClaimDecider.ShouldClaim`, `:99`) antes de reivindicar (`:101`). Teto por tique (`SweepMaxClaimsPerTick`, `:69-81`)
  e `try/catch` por ZDO (`:104-107`).
- **Religação:** `RemovePeer` postfix faz uma varredura única imediata (`Patches/DisconnectPatches.cs:31-59`), e o
  comentário registra um fato do jogo que vale para nós: *"vanilla RemovePeer transfers ownership of the peer's ZDOs
  to the server already **in some Valheim versions**"* (`:26-29`).

### A política: categoria **por nome de prefab**, `Player` nunca

- `ClaimDecider.ShouldClaim` (`ClaimDecider.cs:5-22`): `Player` **nunca** é reivindicado (`:8`); o resto depende de
  toggles por categoria (`Mob/Item/Structure/Vehicle/Tameable/Projectile/Terrain/Other`).
- `Categorizer.Classify` (`Categorizer.cs:129-144`): tabela **hardcoded** de nomes (`:13-60`) + prefixos de peça
  (`:125-127`) e, para qualquer prefab desconhecido, **`ZdoCategory.Other`** (`:143`) — governado por `ClaimOther`.

## Lacunas (promete e não faz)

- **"Ownership for all non-player ZDOs"** só vale para o que a tabela conhece ou para quem ligar `ClaimOther`; um
  prefab de **outro mod** (o nosso companheiro, por exemplo) cai em `Other` (`Categorizer.cs:143`).
- **Claim no evento, não na criação:** a criação em si é coberta só pelo sweep de até 5 s — na janela entre o spawn e
  a passada, o ZDO fica **sem dono** (é o nosso cenário exato: NPC spawnado pelo jogador num servidor dedicado).
- **Sem precedência publicada:** a própria pesquisa do autor aponta isso como o risco nº 1 (*"mod-vs-mod ownership
  conflicts"*, `docs/research/2026-04-26-prior-art-and-engine-limits.md:28-33`), e ela também confirma o mecanismo do
  jogo: `ZDO.GetOwner()` devolve peer-id `long`, `0` = sem dono, `ZDOMan.GetSessionID()` = id do servidor, e
  **`ZNetView.IsOwner()` é `GetOwner() == ZDOMan.GetMyID()`** (`:61`, `:78-84`).
- **Não é solução para nós, é concorrente:** se o jogador tiver este mod (ou o `Smoothbrain/DedicatedServer`), o
  companheiro é reivindicado **pelo servidor** — o que resolve a posse, mas **derruba o nosso `MHR_Owner.IsMine`** se
  ele assumir que o dono é o jogador.

## Serve para nós?

| aspecto | veredito |
|---|---|
| ideia | **adotar** o par `SetOwner(serverSessionId)` + **`DataRevision++`** e a **idempotência** por `GetOwner()` |
| implementação | **portável** (MIT, sem Jotunn, ~100 linhas no total) — mas o mod é alpha e mexe em tabela interna por reflexão |
| custo | baixo: é a receita de 3 linhas para a pendência (5); o risco é a **precedência** com outros mods de posse |

## Recomendação

**adotar ideia** (não o mod). Para a pendência (5): no spawn, se `ZNet.instance.IsServer()` (ou o `ZNetView` não tem
dono), pegar `m_sessionID` do `ZDOMan` **por reflexão uma vez e cachear**, chamar `GetZDO().SetOwner(serverId)` e
**`DataRevision++`** — e, do lado do jogador, `MHR_Owner.IsMine` passar a aceitar *"dono = servidor"* como dono válido
(senão o companheiro deixa de responder quando um mod de posse de servidor estiver instalado). **Não** desenhar a
posse em cima de `RPC_ZDOData` — o `CreationPatches.cs` deste repo é a prova de que esse caminho não fecha sem
transpiler. Ver também, como concorrentes sem licença/uso: `Smoothbrain/DedicatedServer` (blaxxun-boop) **sem
`LICENSE`** — não é fonte, só referência de comportamento; `Smoothbrain/CombatOwner` e
`Skarif/ValheimPerformanceOverhaul` (smart zone ownership) são os conflitos citados pelo próprio autor
(`docs/research/2026-04-26-prior-art-and-engine-limits.md:47-49`).
