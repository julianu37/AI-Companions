# Ciclo de uma run de construção (build novo → jogo → run → números)

O processo **inteiro** de medir uma mudança do mod em jogo, na ordem em que ele funciona, com os comandos exatos. É o
documento que o agente lê **antes** de começar um run — o objetivo é não gastar o ciclo sondando o MCP, o DevBridge ou
o `run_watch.py` aos pedaços (foi o que aconteceu em 13/11/2026 e o jogador cobrou: *"fica estudando/sondando como
faz a cada vez que precisa realizar esse processo e demora demais"*).

O protocolo do DevBridge (comandos, campos, armadilhas) está em [`docs/mcp-devbridge.md`](../mcp-devbridge.md) — este
arquivo é só o **procedimento**, e cita de lá as receitas em vez de repeti-las.

## Regras que valem em todo run

1. **BepInEx carrega a DLL no boot.** Um `dotnet build` que deploya **não** troca nada no jogo aberto — o processo
   continua rodando a `Companions.dll` antiga em memória. Todo run começa com o jogo **fechado e reaberto**. Sem isso a
   run mede o código anterior e o relatório mente sobre o fix.
2. **O build deploya.** `dotnet build Companions.sln -c Release` copia o DLL para `$(DeployDir)`. Use
   `-p:SkipDeploy=true` quando não for testar em jogo (harness, gates, commit).
3. **O mundo `tests` é descartável.** Antes de cada run, `build.delete` num raio grande em volta do terreno de teste,
   sem pedir autorização e sem conferir peça por peça. O que for apagado, foi apagado.
4. **Entrar no mundo é `world_enter`** — nunca mouse, nunca screenshot para navegar menu.
5. **Builds e harness rodam em série** (dois ao mesmo tempo brigam por `obj/bin`).
6. **Um run de cada vez.** `run_watch.py` de runs antigos ficam em background se não forem parados (`TaskStop`) e dois
   samplers escrevem no mesmo sítio.

## O ciclo, passo a passo

### 1. Fechar o jogo (antes do build)

Com o jogo aberto e a ponte respondendo:

```python
call_bridge("world.quit", save=True)     # {"quitting": true, "loggedOut": true}
```

`save: true` grava o mundo e o perfil; `save: false` só para mundo descartável e quebrado. **Não** matar o processo
por fora: descarta a sessão.

Se o jogo **não** estiver aberto (build a frio), pule para o passo 2.

### 2. Buildar e conferir o carimbo

```powershell
dotnet build Companions.sln -c Release          # sem SkipDeploy: tem de copiar para a pasta do jogo
(Get-Item "<DeployDir>\Companions.dll").LastWriteTime
```

O carimbo do DLL deployado tem de ser **mais novo** que a hora em que o build começou. Se não for, algo falhou (ou o
jogo estava com o arquivo travado) — não siga para o run.

### 3. Reabrir o jogo

```powershell
Start-Process "steam://rungameid/892970"
```

Espere a ponte responder: `call_bridge("ping")` até voltar (leva ~20–40 s). O `ping` já traz `mod: <versão>` — se a
versão não é a que você acabou de buildar, o deploy não chegou onde o jogo carrega (duas cópias instaladas com o mesmo
GUID é a causa comum; veja os *pitfalls* do `CLAUDE.md`).

### 4. Conferir o carregamento no log

`ping` prova que a ponte subiu, **não** que os patches entraram. `BepInEx\LogOutput.log` precisa ter as duas linhas:

```
Loading [AI Companions <versão>]
loaded successfully! (N methods patched)
```

### 5. Entrar no mundo

```python
call_bridge("world_enter")     # Space, DownArrow, Return x3, espera carregar e roda o world.setup
```

O `world.setup` liga `devcommands`, põe meio-dia (`tod 0.5`), `god on` + `heal` e stamina infinita. `ping` depois:
`inGame: true` e o mundo `tests`.

### 6. Limpar o sítio do run anterior

```python
call_bridge("build.pieces", x=-158.9, y=38.8, z=-427.7, radius=34)   # count + a lista
call_bridge("build.delete", x=-158.9, y=38.8, z=-427.7, radius=34)   # same centre, same radius
call_bridge("build.pieces", x=-158.9, y=38.8, z=-427.7, radius=34)   # tem de voltar 0
```

- **Os argumentos vão como keyword args** (`call_bridge(cmd, **args)` monta `{"cmd":…, "args":{…}}`). O erro de mandar
  um dicionário aninhado à mão foi o que fez um `build.delete` apagar com raio 10 no começo de uma run.
- Sem `x/y/z` o centro é o **player**; sem `radius` o padrão é **10 m**. Sempre passe os dois.
- `destroyed` tem de bater com o `count` lido antes, e o `build.pieces` seguinte tem de voltar `0`. Se sobrar peça que
  **não** é do run, use `filter` (substring do prefab).

### 7. Sampler em background, antes da ordem

```powershell
python tools/mcp-devbridge/run_watch.py --x -158.9 --z -427.7 --radius 32 --every 20 `
    --baseline <linhas atuais do LogOutput.log> --out tools/mcp-devbridge/<run>.jsonl
```

`--baseline` é o número de linhas do log **agora** — é o que impede os contadores do run anterior de entrarem na conta.
O sampler roda como tarefa de background; pare-o (`TaskStop`) quando a run fechar.

### 8. A ordem

```python
call_bridge("companions.build", blueprint="CommHomeBasicBP", name="Companion", x=-158.9, y=38.8, z=-427.7)
```

- `blueprint` **sem** a extensão `.blueprint`.
- **`name`** = o caminho **solo** (um companheiro, sem grupo). Sem `name` entram **todos** os companheiros do jogador no
  mesmo canteiro — é o caminho de grupo, que é outro conjunto de regras.
- O reply traz `started`, `pieces` (o plano inteiro), `workers`, `missing`, `missingStation`. `refused` só aparece quando
  a ordem **não** entrou.

### 9. Esperar e fechar

```python
call_bridge("companions.build_status")     # até {"site": false}
```

`site: false` = a ordem acabou (não é erro; também é o estado de "nunca começou"). Depois:

```powershell
python tools/build_run_report.py --last
```

O relatório sai do log e é o instrumento de aceite: `done`, `pulada(s)`, `desistindo de`, `no_support`, `no progress`,
apoios de emergência, queda, morte, a linha final (`order ended: hc_ai_blueprint_done`).

### 10. Reconciliar a cena contra o blueprint (a prova de "não falta peça")

O relatório sai do **log** e conta intenção. A prova de que a obra está completa é a **cena**, peça por peça:

```python
call_bridge("build.pieces", x=-158.9, z=-427.7, radius=40)   # salvar em tools/mcp-devbridge/scene-<run>.json
```

```powershell
python tools/reconcile_scene.py `
    --blueprint "<plugin folder>\blueprints\CommHomeBasicBP.blueprint" `
    --scene tools/mcp-devbridge/scene-<run>.json
#   exit 0 = toda peça do plano está na cena (ou tem um extra do mesmo prefab por perto)
#   exit 1 = falta peça de verdade (lista cada uma)   |   exit 2 = entrada ilegível
```

O ajuste cena↔blueprint é sempre o **mesmo problema** e é resolvido pelo script, **sem semente**:

1. Ler o `.blueprint` (`prefab;categoria;x;y;z;…`, coordenadas **locais**) e a cena (mundiais). O nome do
   prefab na cena vem como `<prefab>(Clone)` — o script normaliza, comparar cru dá **0 pares em todo yaw**.
2. Varrer o yaw de **0 a 360°** (passo 1°) e, em cada yaw, **votar na translação**: cada par
   (peça da cena × peça do plano girada) vota numa célula de 0,5 m em (x, y, z); a célula com mais votos
   é o deslocamento daquele yaw. Atribuição gulosa por prefab com tolerância **0,45 m**.
3. Polir: refinar o yaw (±1,2° em passo 0,1; depois ±0,12° em passo 0,02) e, a cada passo, deslocar a
   translação pela **mediana** do resíduo; parar quando o passo for < 1e-4.

**A translação por centróide da receita antiga não serve para uma cena parcial.** Ela ancora
`t = centróide_cena − R·centróide_plano`, o que só é verdade quando a cena tem o plano **inteiro**: numa cena
capturada no meio da obra (219 de 985 peças, run de 15/11/2026) o centróide é o do andar que existe e nenhum
yaw cai em lugar nenhum — medido: **4 casadas**. Com o voto 3D o mesmo par plano/cena casa **217 de 219**
(yaw 344°, resíduo máximo 349 mm, ~7 s).

**Por que "sem semente" não é preciosismo:** o yaw muda de run para run porque a posição do jogador muda —
344° nesta, 105° no run de 13/11, ~46° no run C. Um ajuste semeado pela solução anterior casa **78** peças e um ICP
que parte dele **diverge** para uma bacia espelhada (473 pares, yaw −45°). Um ajuste semeado não é evidência.

**Como ler o resultado:** `faltantes sem explicação` é a métrica de aceite. As peças que sobram geralmente
**não** são peças faltando: no run de 13/11 sobraram 2 — as **únicas duas** peças do plano em `y = -2.00`
(fundação enterrada), presentes na cena **0,97 m acima** do plano, assentadas no terreno que é mais alto ali. O
script procura esse caso sozinho (um "faltante" com um "extra" do **mesmo prefab** a até 3× a tolerância é
explicado) e só as sem explicação reprovam o run.

**A comparação do "extra" é no espaço do MUNDO.** O plano guarda coordenada **local**; a peça faltante tem de
passar pelo **mesmo transform** do casamento (yaw + translação) antes de ser medida contra a cena. Comparar a
coordenada local crua com a mundial dá centenas de metros e **explica tudo** — foi o defeito do run de 16/11/2026,
que deu `exit 0` com uma peça 6,2 m abaixo do plano. O `reconcile_scene.py` tem teste próprio
(`python tools/test_reconcile_scene.py`, 5 casos, incluindo a peça arrastada por uma queda) **e ele precisa rodar
sempre que a reconciliação reprovar ou aprovar um run** — o instrumento é a prova, e um instrumento sem teste já
aprovou um run com buraco.

## Depois do run

- **O aceite é do jogador**, não do log sozinho: número verde com peça faltando na captura de tela é run reprovado.
- **Nenhum run é declarado concluído pelo relatório — só pela cena reconciliada** (passo 10). O run C disse
  `done: 975` com **974** peças na cena, e o mod disse "pronto" com 10 faltando.
- Um aceite que falha **volta para a fila** (`docs/backlog-construcao.md`) **com o número que o matou**.
- O mesmo ciclo serve para medir uma mudança de AI, de movimento ou de job — o que muda é só o comando do passo 8.

## O que **não** fazer

| não | por quê |
|---|---|
| Rodar sem reiniciar o jogo depois do build | mede a DLL antiga e reporta "o fix não funcionou" sobre um fix que nunca rodou |
| `build.delete` sem `x/y/z/radius` | apaga em volta do player, com 10 m de raio |
| Matar o processo do Valheim por fora | descarta a sessão (use `world.quit`) |
| Dois `run_watch.py` ao mesmo tempo | dois samplers no mesmo sítio |
| Usar mouse/screenshot para entrar no mundo | `world_enter` faz a sequência e espera o carregamento |
| Concluir o aceite lendo só o log | o log é narrativa; `build_status`/`build.pieces` são o estado |
| Semear o ajuste cena↔blueprint na solução do run anterior | o yaw muda com a posição do jogador; o ajuste semeado casa 78 peças e o ICP diverge para uma bacia espelhada |
