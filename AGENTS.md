# AGENTS.md — AI Companions

Mod de companheiro NPC com IA para **Valheim 1.0** (Unity 6), **BepInEx 5**, **net472**, interface em **PT-BR**
(5 idiomas: PT-BR, EN, ES, FR, IT). Repo local: `C:\Users\revenda33\Desktop\ModAI-BR-Companions` (no GitHub:
`julianu37/AI-Companions`). Versão no código: `PluginVersion` em `src/Companions/Core/CompanionsPlugin.cs` (**1.9.6**).

**Histórico completo deste arquivo (1745 linhas, com as "Run N" e as tentativas revertidas): `docs/agents-historico-2026-11-06.md`.**
Não o leia inteiro; use `grep`. Este arquivo é o resumo operacional — o que está aqui é o que você precisa para não errar.

## Antes de mexer em qualquer coisa

- **Estado atual (07/11/2026):** `main` = **`f6c6ee8`**, com a linha `fix/pisos-nivel-2` mergeada (151 commits); a `main`
  **não** é só a 1.9.6 publicada — o código anda à frente da tag. Linha de trabalho: **`feat/refino-construcao`** (parte de
  `f6c6ee8`). Merge/release continuam dependendo do pedido do jogador.
- **Onde está o "onde eu parei":** `docs/checklist-blueprints.md` (feito, **tentado e desfeito com o número que matou**, fila).
  Medições de construção: `docs/re/building-notes.md` (§17.x; os últimos são §17.32 e §17.33). Artefatos das runs:
  `docs/runs/*.json` (o `LogOutput.log` é apagado a cada boot — o run vira arquivo no mesmo ciclo).
- **Não copie números deste arquivo para outro doc.** Eles envelhecem (ex.: este arquivo citava 778 asserções em 12
  lugares; o harness mede 825). Rode o comando e use a saída.

## Comandos (verificados em 06/11/2026)

| para quê | comando | o que esperar |
|---|---|---|
| compilar sem tocar no jogo | `dotnet build Companions.sln -c Release -p:SkipDeploy=true` | `0 Erro(s)` — **use este por padrão** |
| compilar + deploy no jogo | `dotnet build Companions.sln -c Release` | copia para `BepInEx\plugins\AI-Companions`; **só o arquiteto**, e termina em restart (regra 13) |
| testes da IA/construção (harness) | `dotnet run --project tools/ai-intents-check -c Release` | última linha `=== N PASS / 0 FAIL ===` (medido: **825**) |
| testes do MCP DevBridge (mock) | `python tools/mcp-devbridge/test_mcp_devbridge.py` | `=== 122 PASS / 0 FAIL ===` |
| doc de config (nunca editar à mão) | `python tools/gen_config_doc.py --check src/Companions/Core/ModConfig.cs package/CONFIGURATION.md` | `[ok] ... (368 settings, 25 sections)` |
| tradução ES/FR/IT | `python tools/audit_translations.py --lang Spanish` (idem French, Italian) | `lacunas: 0` |
| datas nos docs/código | `python tools/check_dates.py` | `[ok] nenhuma data no futuro` |
| gate de release | `powershell -ExecutionPolicy Bypass -File tools\release-check.ps1 -Version <v>` | 12 itens; **o item 1 faz deploy** (ver nota abaixo) |

- **Pré-requisito:** `Environment.props` na raiz (gitignored; copie de `Environment.props.example`). Sem ele o build usa
  o caminho padrão do Valheim.
- **Não há CI** (`.github/` não existe). A verificação é a que você roda.
- **Nota do gate:** o item 1 do `release-check.ps1` é `dotnet build` **com deploy**. Com o jogo aberto, prefira rodar o build
  com `-p:SkipDeploy=true` e os demais itens à mão (foi o que se fez em 06/11: todos PASS, incl. harness 825 e MCP 122).
  Deploy sem restart viola a regra 13.
- **Harness serializado:** nunca dois `dotnet run` ao mesmo tempo (disputam `obj/bin`).
- O harness **linka** os `.cs` de `src/Companions/AI/` (ver `tools/ai-intents-check/ai-intents-check.csproj`). Código puro
  novo em `AI/` só é testado se o `.csproj` do harness ganhar o link.

## Mapa do código (o que muda como você trabalha)

- `src/Companions/`: `Core/` (entrada, config, localização), `Companion/` (spawn, aparência, skills, morte),
  `AI/` (movimento, combate, **e a lógica pura da construção `Build*.cs`**), `Jobs/` (trabalhos e o executor da obra),
  `UI/`, `Patches/` (Harmony), `Diagnostics/`, `DevBridge/` (módulo de dev, **desligado** por `[Advanced] DevBridge`).
- **`src/Companions/Jobs/MHR_Build.cs` (~9,7 mil linhas)** é o executor da obra de blueprint. Quase toda regressão recente
  saiu daqui. Os números de linha citados em docs envelhecem em horas — cite por **nome de método**.
- **Lógica pura, medível pelo harness:** `AI/BuildFailure.cs` (classifica recusas), `AI/BuildBody.cs` (corpo × peça),
  `AI/BuildWalkSpot.cs`, `AI/BuildAsideSpot.cs`, `AI/BuildSupportBelow.cs`, `AI/BuildSpread.cs`, `AI/BuildPieceKind.cs`.
- `tools/ai-intents-check/Program.cs` (~3,1 mil linhas, seções `(a)`…`(ad)`). `Stubs.cs` são cópias de tipos do jogo;
  uma seção do harness confere que elas não derivaram da `Companions.dll`.
- **Jotunn não é usado** (só aparece em comentários; verificado). Não o adicione como dependência.
- **Pacotes congelados:** Whisper.net 1.9.1, sherpa-onnx 1.13.8, System.Text.Json 10.0.2. Não atualize sem pedido.

## Ciclo da obra de blueprint (o que quebra quando se mexe)

- O companheiro coloca peça a peça: `Place` → `BuildSite.Check` (o jogo decide apoio/estação) → se recusa, `BuildFailure.Classify`
  decide: **espera** (dependência, corpo), **fila de retry** (gasta tentativa) ou **pulada**.
- `blocked_by_player` cobre **qualquer** corpo, inclusive o do próprio companheiro. O corpo do jogador bloqueia a peça de
  propósito (é esperado).
- **Furo aberto (06/11/2026, §17.31):** o companheiro pode terminar **sob o piso** (vão de ~1 m entre terreno e deck,
  cápsula de 1,80 m). `fits: false` em `companions_list` é o sinal; `enterrado: N` no `Finish` não basta.
- **Limitação conhecida, não corrigir sem desenho:** o `WalkSpot` de uma peça no chão devolve a **própria coluna da peça**
  (sample 0), então "sair de cima" vira um passo de 0 m. O caminho que de fato sai do corpo é `SelfAside`.

## Regras que impedem regressão (o que a revisão do histórico mostrou)

Estas regras existem porque **cada uma já foi violada e custou uma run inteira**:

1. **Um commit = uma hipótese.** `7f352e0` juntou folga de cabeça, footprint e um guarda novo; o guarda sozinho
   era a regressão (366 recusas, pausa de 204 s). `0501e5a` tem 51 arquivos. Se o commit tem "e" no assunto, separe.
2. **Antes de mexer numa constante, veja o histórico dela:** `git log --oneline -S 'NomeDaConstante' -- src/`.
   `BodyBarrierSeconds` mudou **3 vezes** (20 → 10 → 5), cada uma com um número diferente. `SelfStep` mudou 3 vezes.
   Se a constante já oscilou, a próxima mudança **exige A/B medido**, não uma justificativa nova.
3. **Fix de comportamento só entra com prova:** (a) um teste no harness que **falha antes e passa depois**, ou (b) um run
   com número antes/depois. **"Build 0/0" e "harness verde" não provam comportamento** — o harness só vê o que está
   nele (10 dos 15 últimos commits que tocaram `src/Companions/AI|Jobs` não mudaram o harness).
4. **Harness: só adição de asserção.** Trocar um valor esperado para o código novo passar é afrouxar o teste. Violações no
   histórico: `96f6c25` (−15 / +7 asserções), `35128fc` (−2 / +3), `45ac792` (2 valores trocados — o último commit
   desta rodada). Se uma regra mudou de propósito, **acrescente** a asserção nova ao lado da antiga, com comentário, e
   deixe a antiga viva enquanto o comportamento antigo for válido.
   **A trava existe:** `python tools/check_harness_additions.py` (compara com HEAD; `--base <ref> --to <ref>` audita um
   commit; `--allow-removal "motivo"` é exceção visível). Está no `release-check.ps1` (itens 11–12). Testada em 06/11:
   pega os três commits acima com exit 1 e passa no working tree. Ela só vê o que está **não commitado** por padrão: contra
   `--base main` a branch dá `+93, 0 removida(s)` (as remoções do histórico foram repostas depois).
5. **Procure o caminho que já existe antes de desenhar.** `grep` pelo sintoma (o texto do log, o estado). Ex.: `SelfAside`
   já existia; o "conserto" do `WalkSpot` com anel de 4,5 m foi revertido (52 s → 72 s, sem ganho). Regra completa na
   pendência do AGENTS histórico (regra 15).
6. **Todo caminho que manda a peça para a fila ou desiste precisa de log com o motivo.** Três defeitos de 2026 eram
   fracassos silenciosos (`AbandonPiece`, `WalkSpot`, o retry): o código sumia com a causa.
7. **Medir a sequência inteira, não a peça.** A régua de uma run de obra é: `plano` (denominador deve ficar igual ao
   base), `placed`, `enterrado`, `fits` (rastro), `cruzaria o meu corpo`, `parado %`, `UNSUPPORTED`. "Done: N" sozinho
   mente (`64/64` com o corpo enterrado já aconteceu).
8. **Um revert também é um commit com hipótese.** Registre no commit o número que matou a mudança revertida.
9. **Não mude duas variáveis numa rodada.** A comparação `7f352e0` × `2b469d6` mudou **a barreira (10 → 5 s) e o guarda**
   ao mesmo tempo; a causa ficou sem atribuição. Isole antes.
10. **Pare no segundo fracasso do mesmo sintoma.** Se o mesmo sintoma volta depois de um fix, **não** faça o 3º patch:
    reverta o último commit ou faça o A/B. O custo de um patch a mais é mais regressão, não progresso.
11. **Nunca deployar no meio de uma medição** (um número = uma DLL). Matar a medição antes do deploy.

## Regras absolutas (do jogador — não negociar)

- **Entrar no mundo:** só `mcp_devbridge.world_enter` pelo bridge (sequência: Espaço em rajadas → Seta para baixo uma vez →
  Enter ×3 a 0,5 s). **Proibido** clique, `SendKeys`/`PostMessage`, `Tab`, ou reimplementar a sequência.
- **Todo deploy termina em restart.** Prova: `LastWriteTime` da DLL **anterior** ao `StartTime` do processo e `ping` com
  `inGame: true`. Não diga "está em jogo" antes disso.
- **Nunca** editar `BepInEx\config\AiCompanions.cfg` do jogador (o BepInEx reescreve a cada boot).
- **Nunca** teleportar nem mover o jogador. `[Building] TempScaffold` fica `false` (vetado).
- **Altura da obra (`DeckLift`) não se altera** sem pedido do jogador.
- **Multiplayer é o último item** e está parado por decisão do jogador.
- **A data vem do relógio ou do commit**, nunca da memória (`check_dates.py` é a trava).
- **Sem segredos** no código: chave de API é `ConfigEntry`.

## Convenções

- **Idioma:** interno (`AGENTS.md`, `docs/*`, `docs/re/*`) em **PT-BR**; público (`README.md`, `package/*`, wiki, release
  notes `docs/release-notes-*.md`) em **EN**; `package/LEIA-ME.txt` em PT. **Comentário de código em inglês; log em PT-BR;
  texto ao jogador por chave de localização** (`Translations/*.json` + `Speech/*.json`; o inglês é nativo no código).
- **Config:** nova chave em `Core/ModConfig.cs` exige descrição nos 5 idiomas e `gen_config_doc.py` regenerado.
- **Commit:** `tipo(escopo): resumo`. Agentes usam **caminhos explícitos** no `git add`, nunca `-A`. A árvore termina
  limpa (`git status --porcelain` vazio); nada fica editado sem commit (use `wip(...)` se estiver pela metade).
- **Doc no mesmo commit** que muda o comportamento (`building-notes`, `checklist-blueprints`, `CHANGELOG`,
  `CONFIGURATION.md` quando a config muda).
- **Subagentes:** despache com `background: true` e siga; o resultado chega por notificação. Não espere nem faça poll.
  `opencode.jsonc` tem `subagent_depth = 2` (arquiteto → especialista → executor). O orquestrador revisa o diff antes de commitar.

## Pendências ativas (resumo; detalhe no histórico e na checklist)

- **Teto de workers: `[Building] MaxWorkers`, padrão 3** (07/11/2026, pedido do jogador). Antes não havia teto. **Medido
  com 3** (§17.33): **612/612 em 165 s**, `UNSUPPORTED` 0. **A leitura de que "o colega bloqueia" estava ERRADA** — o
  motivo instrumentado (§17.34) é **`outro nível` 50**, o anel de 2,2 m caindo fora da laje. **Tentado e DESFEITO
  (`e8fa850`):** `PlanNear` com anéis de 2,2/1,5/1,0 m — o step-aside passou a acontecer (8 pedidos contra 0), mas **520 ×
  521 peças** e **387 × 237 bloqueios**: ping-pong entre companheiros na mesma peça (§17.35). **Fica só a instrumentação**
  (`StepRefusal`). A próxima tentativa exige: um pedido por peça e destino fora de **todas** as pendentes num raio maior.
  Quem mais bloqueia é o **jogador** (112 de 173 no run em que ele entrou no canteiro).

- **Por que ele fica sob o piso (§17.31, revisão de 06/11 em `docs/revisao-executor-2026-11-06.md`):** o `Goto` apagava o
  destino de "saio para" quando a peça está à altura dos pés (A2) e o `WalkSpot` pode devolver a própria coluna da peça
  (A3, 2 de 11 tentativas com passo de 0 m). As duas tentativas viravam no-op e a peça era colocada sobre o corpo.
  **CORRIGIDO em `edc6d04`:** par A2 + A3 (`SelfWalk` independente de `ReachWalk` e sem recálculo no `Goto`; novo
  `BuildWalkSpot.ChooseAway` filtrando < 1,0 m dos pés e < 1,5 m da coluna da peça). Harness seção (af), **+7 casos**.
  `Choose` (caminho normal) não mudou, e o harness asserta isso.
- **Retry de corpo sem teto (M4):** `Kind.Blocked` não incrementava `Tries`, então nunca chegava a `MaxPasses`.
  **CORRIGIDO em `50b1cb3`:** `BuildFailure.BlockedExhausted` + `BlockedCap = 6` (~30 s de espera gratuita); depois, a
  recusa gasta tentativa e o `MaxPasses` limita. A magnitude **não foi medida**.
- **Fracassos silenciosos do executor (A4/M3/M6/B1): CORRIGIDO em `08611c9`** — todos logam o motivo; nenhuma decisão mudou.
- **A/B do A2+A3: MEDIDO em 07/11/2026, e o conserto FICA.** Mesma receita dos dois lados (1 worker, madzobuild,
  `DeckLift 0`, 600 s): **`fits:false` 88 % → 0** (1015/1150 → 0/1068), **parado 79 % → 17 %**, **colocadas 183 → 257**,
  plano estável em 612, `UNSUPPORTED`/`sem progresso` 0. As tentativas de sair agora **andam** (0,8–0,9 m; antes 0 m).
  §17.32 de `building-notes`. Ressalvas: **o M4 não foi exercitado** (`blocked_by_player` 2, cap não cruzado), a obra não
  terminou em 600 s (257/612) e é **1 worker** — nada diz sobre 5.
- **Peça colocada sobre o corpo na 3ª tentativa:** o conserto exige **impedir a re-inserção no plano**, não um guarda cego.
  A tentativa de re-inserir no mesmo andar (`RequeueSameStorey`) cresceu o plano de **612 para 1303** (691 cópias) e foi
  revertida.
- **Barreira de andar: 5 s ou 10 s?** Não decidido. Só um A/B (mesma árvore, 1 worker, `DeckLift 0`) decide.
- **Vão do deck × cápsula de 1,80 m:** decisão do jogador entre `DeckLift 0`, sair em volta ou regra de rota.
- **Rota até a cumeeira** (`no progress` em `wood_roof_45`): aberta, `TempScaffold` vetado.
- **Peso do companheiro** derruba a obra (`hc_ai_build_too_heavy` encerra a ordem do worker): aberta.
- **Blueprints excluídos por decisão do jogador:** 6 `stone_manor` (fundação de 1,50 m enterrada). Backups em `%TEMP%`.
- **Release** (1.9.6 já publicada: nova versão exige decisão do jogador sobre nome e tag) parada.

## Padrões de erro medidos no histórico (para quem vai mexer no executor)

Medido com `git log` e `git show` em 06/11/2026 — não é opinião:

- **Oscilação de constante:** `BodyBarrierSeconds` 20 → 10 → 5; `SelfStep` 3 mudanças; `MaxPlace` 14 commits.
- **Commit de vários fatos:** `0501e5a` (51 arquivos), `c6483b2` (partição por fase + repulsão + P0 num commit só),
  `7f352e0` (3 hipóteses).
- **Reverts sem teste de volta:** 5 em 150 commits; nenhum acrescentou asserção ao harness (+0 em todos), e `8ec9de7`
  removeu 18. Um revert que tira comportamento precisa de um teste que prove o comportamento que ficou.
- **Asserção removida para passar:** `96f6c25`, `35128fc`, `45ac792` (ver regra 4).
- **Número morto no doc:** 778 × 825 (este arquivo, até hoje); `docs/mcp-devbridge.md` dizia 46 ferramentas (são 62).
- **Doc que cita linha de código:** `MHR_Build.cs` se move a cada commit; 0 de 15 citações `arquivo:linha` batiam na
  varredura de 05/11 (pendência 26). Cite método.
- **Log que falta:** caminho que desiste sem falar (`AbandonPiece`) escondeu a causa de pulada anônima.
