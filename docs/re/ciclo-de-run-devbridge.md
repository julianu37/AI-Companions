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

## Depois do run

- **O aceite é do jogador**, não do log sozinho: número verde com peça faltando na captura de tela é run reprovado.
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
