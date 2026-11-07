# Quad's Companion — HardHeadHackerHead/valheim-mods (0.25.1, commit `643c7a9`, 23/10/2026)

- **Fonte:** <https://github.com/HardHeadHackerHead/valheim-mods/tree/main/mods/AICompanion> · **Licença:** MIT ·
  **Última atualização:** 23/10/2026 · **Jogo alvo:** Valheim 1.0.x (BepInEx 5)
- **JOTUNN-ONLY?** **não** (`.csproj` só referencia `UnityEngine.*`, `mods/AICompanion/AICompanion.csproj:8`) ·
  **Repo público?** sim · **DLL fechada?** não
- **Nosso problema:** (a)/(b)/(c) do **ponto de vista de quem consome** um plano pronto — é o único mod público que
  **manda um NPC construir o plano de outro mod**, que é exatamente o nosso caso. Também é concorrente direto.
- **Como li:** `shell` negado nesta sessão (sem clone); leitura por `raw.githubusercontent.com` no commit `643c7a9`.
  Caminhos relativos a `mods/AICompanion/`. **Clone pendente** para *project reference*.

## O que o README promete

> "With Quad's Build Orders: **it builds your planned ghosts near its home** (a home duty, *Build our plans*), paying
> from its own bag and your chests, never from you." — `README.md:85`

> "It picks the next piece that **will stand with what is built** (BuildOrders works that out, as for Build all),
> takes what the piece costs out of its bag or your chests near home … walks to it with a hammer … and puts it up."
> — `CHANGELOG.txt:33`

> "When pieces cannot be built at all **it says why**: pieces you have not unlocked, a station (a stonecutter, say)
> not built near them, ground you may not build on, or **nothing that will stand yet**." — `CHANGELOG.txt:31`

## O que o código faz

### A arquitetura: **zero lógica de construção própria** — tudo por `AppDomain`

- Quatro `Func<>` publicadas pelo BuildOrders são lidas por nome, sem referência de assembly:
  `DHack.BuildOrders.Count`, `.Next`, `.Place`, `.Blockers` — `Building.cs:22-26`; `Available` exige as três
  primeiras (`Building.cs:28`). Sem o BuildOrders, o dever **não existe** (`Building.cs:131`).
- Doc do próprio arquivo: *"BuildOrders publishes three functions through AppDomain data … without it this does
  nothing"* — `Building.cs:16`.

### (a) Ordem: **pergunta ao planejador, não ordena**

- `Building.Next` pede `NextFn(centro, alcance, 24)` e **recalcula a cada 10 s**
  (`Building.cs:133-137`); `alcance = Work.RadiusOf(me) + 10` (`Building.cs:37`). A ordem é a do `StandingOrder` do
  BuildOrders (profundidade de apoio → altura), **não** uma ordem por classe de peça.
- Pega a **primeira** da lista que ele consegue pagar com a **mochila** (`Building.cs:158-163`); se nenhuma, vai ao
  baú levar o custo de **até 10 peças seguintes** de uma vez, respeitando `Carry.Max * 0,6 - peso`
  (`Building.cs:165-190`); se sobra falta, vira **meta de coleta** (juntar/fazer/pedir) — `Building.cs:191-208`.
- Não constrói o que não pode pagar, mesmo que a lista diga que fica de pé: o portão é o material.

### (a) A peça que falha: **skip de 10 minutos**, sem re-tentativa

- `Place` → `PlaceFn(id)`; **falso** ⇒ `st.BuildSkip[id] = Time.time + 600f` e uma fala
  *"could not build <peça>"* (`Building.cs:327-329`). A peça sai do lote por **10 min**
  (`Building.cs:138` filtra `BuildSkip`).
- "Não conseguiu chegar": `Failed` aplica o **mesmo** skip de 600 s (`Building.cs:345-348`), chamado de
  `Work.cs:564`.
- **Não há contagem de tentativas nem fila de retry**: ou passa no `Place`, ou fica 10 min de castigo. É o oposto do
  nosso `MaxPasses = 3` + portão por motivo.
- Enquanto a lista está vazia mas há fantasma (`Pending > 0`), ele **explica** pelo `Blockers`
  (`Building.cs:139-143`, `:244-264`) e cala por 600 s (`Talk.Tell(..., "planblock", 600f)`, `:262`).

### (b) Apoio: **não valida nada — confia no `Next`**

- Nenhuma checagem de apoio, `WearNTear`, collider ou altura neste arquivo (o `Building.cs` inteiro trata material,
  mochila, baú, meta e fala). O apoio é **terceirizado**: só entra na lista o que o `StandingOrder` do BuildOrders
  aprovou (`Plugin.cs:577-604` da ficha do BuildOrders).
- Ou seja: a decisão "pode subir?" é de quem publica `Next`; o NPC apenas paga, anda e coloca.

### (c) Estação: **só reporta — nunca planta bancada**

- Não há craft de bancada, nem `PlanStations`, nem `InsertBenchStep` neste mod. A estação vem de graça **se o plano
  dela for coberto**: peças cuja estação não está no raio **nem aparecem** no `Next` (BuildOrders filtra em
  `Plugin.Helpers.cs:52-58` e `Plugin.cs:633-642`).
- O que ele faz é **dizer o motivo**: `Blockers` tipo `station` → fala localizada
  `aicompanion_building_blocked_station` com nome e contagem (`Building.cs:250-256`), resumida em
  `hc`-equivalente *"nothing it can build: <porquê>"* (`Building.cs:260-263`).
- **Custo real para o jogador:** se o plano precisa de `stonecutter` e não há um no raio, o NPC **para de construir
  essas peças para sempre** e só avisa. Nós cobrimos esse caso melhor (`PlanStations` insere a bancada no plano).

## Lacunas (promete e não faz)

- **"Never from you"** (`README.md:85`) é verdade no material, mas `Place` chama o `PlacePiece` do BuildOrders, que
  **constrói como o jogador** (`Plugin.Helpers.cs:104`) → o crédito/estatística/efeito de "colocação" fica com o
  jogador local. É a mesma armadilha que a nossa E6 resolveu com executor próprio.
- **Sem ordem por andar/porta-por-último:** herda a ordem do planejador (`y*2` como desempate). Num plano com porta no
  térreo, nada impede a porta de subir antes das paredes de cima.
- **`Reach = RadiusOf + 10`** (`Building.cs:37`) é o raio de **busca do plano**, não o alcance de colocação — o
  alcance de verdade é o 5 m do olho do jogo, herdado do `PlacePiece`. Não há código tratando "peça alta fora de
  alcance" (nem escadinha, nem andaime).
- **CHANGELOG é a melhor fonte de defeitos reais** e vale ler antes de desenhar: 0.24.0 (falta de material virando
  meta de coleta), 0.24.x (não pegar do baú do jogador além do necessário), 0.25.0/0.25.1 (conflitos com
  StarLevelSystem/ExtraSlots).

## Serve para nós?

| aspecto | veredito |
|---|---|
| ideia | **adotar** a **separação de fala por motivo** (`unlocked`/`station`/`protected`/`nothing-that-will-stand`) — nós temos `BuildFailure` mas a fala final agrega pouco |
| implementação | **não portável no formato** (depende de um mod externo publicar `Next`); o que porta é o **desenho do contrato** (`id|prefab|x|y|z` + `kind|count|detail`) |
| custo | baixo — é uma fatia de fala/UI, não de lógica de obra |

## Recomendação

**adaptar**. Levar duas coisas baratas: (1) o **contrato de "por que não"** com 4 categorias fechadas e contagem,
alimentando a fala final e a nota de trabalho (hoje o nosso watchdog nomeia peças, mas não resume *por classe de
motivo*); (2) a **meta de coleta a partir do resto do plano** — que já temos (`PlanMissing`/`TemplateMissing`) e que
aqui confirma o desenho. **Descartar** o modelo de falha (skip único de 600 s) e a ausência de plantio de bancada:
nosso `retry` de 3 passadas + `PlanStations` são melhores e o CHANGELOG deles (1.14.7 do BuildOrders) mostra por quê.
