---
name: Valheim Mod Research
description: Como pesquisar mods de Valheim já existentes (Thunderstore, Nexus, GitHub), decidir o que a licença permite, clonar os repositórios públicos para consulta local e confirmar com REA se o mod faz o que promete. Use ao procurar solução/implementação de terceiros para um problema do projeto.
---

# Pesquisa de mods de Valheim

O objetivo é **não reinventar** o que a comunidade já resolveu — e não repetir o erro de confiar em README. O produto
é um `docs/re/mods/<owner>-<repo>.md` com **o que o mod promete × o que o código faz**, licença e recomendação.

Regra do projeto que manda aqui: `AGENTS.md` — **o upstream manda em comportamento de jogo; nós mandamos na camada de
IA, na localização, nas chaves de config e nas camadas de UI**. Fonte de API do **jogo** é o
`valheim-reverse-engineer`; esta skill é só para **mods**.

## Quando usar

- "Como outro mod faz apoio de peça / blueprint / navmesh em obra / posse de ZDO / jobs de coleta?"
- Antes de desenhar uma feature que parece nova (construção, comida, casa, portais, carroça, radial).
- Quando um bug nosso cheira a "problema conhecido" (peça caindo, ZDO sem dono, pathfinding dentro de obra).

## 1. Descobrir (metadata primeiro, sem baixar nada)

| fonte | rota | observação |
|---|---|---|
| Thunderstore (Valheim) | `https://thunderstore.io/c/valheim/api/v1/package/` | lista completa em JSON (grande); `.../api/experimental/package/<ns>/<name>/` para um mod |
| Thunderstore (detalhe de versão) | `https://thunderstore.io/api/experimental/package/<ns>/<name>/<version>/` | traz `description`, `dependencies`, `file_size`, `website_url` |
| Nexus | `https://api.nexusmods.com/v1/games/valheim/mods/search?keyword=<termo>` | exige header `apikey` (chave **pessoal**, via variável de ambiente) |
| Nexus (mod) | `https://api.nexusmods.com/v1/games/valheim/mods/<mod_id>.json` | `description`, `author`, `version` |
| GitHub (busca) | `gh search repos "<termo>" --limit 20 --json fullName,description,stargazersCount,updatedAt,license` | é onde se acha o **código** |
| GitHub (licença) | `gh api repos/<owner>/<repo>/license --jq .license.spdx_id` | portão da tabela abaixo |

Termos que funcionam melhor (inglês): `valheim companion`, `valheim build blueprint`, `valheim building`, `valheim
pathfinding`, `valheim navmesh`, `valheim zdo ownership`, `valheim dedicated server sync`, `valheim radial menu`,
`valheim farm job`, `valheim autocraft`.

**Filtro de candidato:** repo público + atualizado nos últimos 12 meses + licença da tabela + **não** Jotunn-only.

## 2. Licença — o portão

| licença | pode ler | pode adaptar | pode colar trecho |
|---|---|---|---|
| MIT / BSD / Apache-2.0 / ISC / Unlicense / WTFPL | sim | sim | sim, **com atribuição** e aviso preservado |
| GPL-2.0 / GPL-3.0 / LGPL / AGPL | sim | **não** (viral no nosso MIT) | não |
| CC-BY / CC-BY-SA | sim | sim (BY); SA contamina | com cuidado |
| sem `LICENSE`, "all rights reserved", DLL fechada | **só** README/metadata | não | não |
| desconhecida | trate como "sem licença" | não | não |

DLL fechada: decompilar **somente** para fato de interoperabilidade (nome de campo, assinatura, ordem de chamada) —
nunca para reimplementar a lógica de outro autor. Isso é o mesmo critério que usamos com o `assembly_valheim.dll`.

## 3. Clonar para consulta local

```
git clone --depth 1 https://github.com/<owner>/<repo> C:\Users\revenda33\.local\share\opencode\repos\github.com\<owner>\<repo>
```

- Mesmo padrão das referências do projeto (`~/.local/share/opencode/repos/<host>/<owner>/<repo>`).
- `--depth 1` por padrão; `git -C <dir> fetch --unshallow` só quando precisar de histórico (ex.: "quando mudou").
- Um clone por mod; **nunca** clone dentro do repo do projeto.
- O clone é **read-only** para você: não edite, não commite, não faça push nele.

## 4. Verificar a promessa (REA quando necessário)

1. Comece pelo **código**: procure a feature pelo nome que o README dá (`grep -ri "blueprint" --include=*.cs`).
2. Se só existir DLL, ou se o README descrever um mecanismo que você quer entender **sem** repo: use REA
   (`inspect_managed_members`, decompile) e registre o que **existe** (campos, chamadas, patches) — não copie corpo de
   método.
3. Confirme o que o mod **não** faz: `TODO`, `NotImplemented`, `throw new`, flag desligada por padrão, `if (false)`.
4. Para mod com Harmony: liste os **alvos** (`[HarmonyPatch(typeof(...))]`) — é o que mais quebra entre versões do jogo.

## 5. Avaliar contra as nossas restrições

- **Jotunn**: se o mod depende dele, o valor é a **ideia**; a implementação não porta (nosso runtime não usa Jotunn).
  Marque `JOTUNN-ONLY` na primeira linha.
- **Versão do jogo**: mod de 0.2xx (antes do 1.0 / Unity 6) pode ter API que não existe mais — confira no
  `docs/re/` se a assinatura citada ainda vale.
- **Rede/ZDO**: o que é nosso (`MHR_Owner`/`MHR_WriteOrQueue`, roster, lápide) tem prioridade; mod de terceiro pode
  ensinar o padrão, mas a posse é decisão nossa.
- **Performance**: patch de `Update` por frame é bomba — registre a frequência (`Update`/`FixedUpdate`/coroutine) e o
  custo declarado pelo autor.

## 6. Template do `docs/re/mods/<owner>-<repo>.md`

```markdown
# <mod> — <owner>/<repo> (<versão>, <data do commit>)

- **Fonte:** <url> · **Licença:** <SPDX> · **Última atualização:** <data> · **Jogo alvo:** <versão>
- **JOTUNN-ONLY?** sim/não · **Repo público?** sim/não · **DLL fechada?** sim/não
- **Nosso problema:** <o que o arquiteto pediu>

## O que o README promete
<2–4 linhas, citando o trecho>

## O que o código faz
- <mecanismo> — `path:linha`
- <mecanismo> — `path:linha`

## Lacunas (promete e não faz)
- <item> — `path:linha`

## Serve para nós?
| aspecto | veredito |
|---|---|
| ideia | adotar/adaptar/descartar |
| implementação | portável/não portável — por quê |
| custo | <peças a mudar, risco> |

## Recomendação
<2–3 linhas: o que fazer e o que NÃO fazer>
```

## 7. Armadilhas conhecidas (já pagas por nós)

- **README não é contrato**: o nosso próprio mod já teve fala prometendo checagem que era código morto
  ("carregando demais para construir"). Presuma o mesmo dos outros.
- **Mod abandonado com estrelas**: checar `updatedAt` no GitHub, não o número de downloads.
- **Fork com o mesmo nome**: confirme o `owner` no README do Thunderstore (`website_url`).
- **`gh` neste repo resolve o `upstream` por padrão**: para o nosso, `gh -R julianu37/AI-Companions`.
- **Nexus API key** nunca no repo: variável de ambiente / `ConfigEntry`.

## 8. Saída esperada para o arquiteto

Um resumo de **8 linhas** por mod (serve / não serve / custo) + os arquivos em `docs/re/mods/` + a lista de clones
feitos (para virarem *project references*), com `arquivo:linha` em tudo que afirmar.
