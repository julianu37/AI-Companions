# Ficha de publicação — Nexus Mods (ModAI-BR-Companions)

Esta ficha é para **quem vai fazer o upload** no site. Ela contém os textos prontos para copiar e colar,
as decisões já tomadas e o que ainda depende do dono do mod.

- A **descrição da página** (BBCode) está em [`descricao-bbcode.txt`](descricao-bbcode.txt), neste mesmo diretório.
- **Não cite nenhuma outra plataforma de distribuição de mods** em nenhum campo do site (regra do projeto:
  o Nexus é o único destino). Nada de alegações não comprovadas ("melhor IA", "ganho de FPS") — só o que está
  documentado no repositório.
- O Nexus **não aceita markdown** na descrição: só BBCode. O arquivo ao lado já está em BBCode.

---

## ✅ Versão publicada: 1.6.3 (decidida)

A versão que vai para o Nexus é a **1.6.3** — decisão tomada, e os pacotes já estão **montados** (tamanhos e
MD5 na seção 4). Os motivos:

- O `Companions.dll` que está **instalado e funcionando no jogo** já reporta **`1.6.3`**
  (`PluginVersion` em `src/Companions/Core/CompanionsPlugin.cs`) — e a DLL do pacote é **byte a byte igual**
  a essa (mesmo MD5).
- O pacote usa o **layout que o Nexus e os gerenciadores de mods esperam**
  (`BepInEx\plugins\ModAI-BR-Companions\`): o gerenciador instala direto, sem o usuário mover pasta nenhuma.

Histórico: a **1.6.2** foi a **última release publicada no GitHub** (é a versão que o vídeo e a wiki citam
hoje). Publicar a 1.6.3 pede uma **release nova no GitHub** (`v1.6.3`) com esses mesmos dois zips, para os
links de download da descrição continuarem verdadeiros — ver a seção 9.1.

---

## 1. Identidade da página

**Nome (título)** — copie e cole:

```
ModAI-BR-Companions - Companheiros com IA (PT-BR)
```

**Resumo / summary** (até ~200 caracteres) — copie e cole:

```
Companheiros NPC com IA para Valheim 1.0, em português do Brasil: ordens por chat e por voz, personalidade, memória e voz sintetizada - tudo local. Fork PT-BR do Offline Companions Reborn (MIT).
```

> São **194 caracteres** (o site corta/avisa perto de 200). Se precisar de um resumo menor:
> `Companheiros NPC com IA para Valheim 1.0, em PT-BR: ordens por chat e voz, personalidade, memória e fala sintetizada. Tudo local.` (129)

**Versão:** `1.6.3` (decidida — ver o aviso no topo).

**Outros campos:**

| Campo | O que colocar |
|---|---|
| Autor / uploader | a sua conta (o crédito do upstream vai nos *Credits*, seção 5) |
| Nome no jogo / plugin | `ModAI-BR-Companions` (é o que aparece no log do BepInEx) |
| GUID do plugin / nome do `.cfg` | `Maddhattr.OfflineCompanionsReborn` → `BepInEx\config\Maddhattr.OfflineCompanionsReborn.cfg` (mantido do upstream de propósito: é o que faz as configurações antigas continuarem valendo) |
| Conteúdo adulto | **Não** |

---

## 2. Categoria e tags

**Categoria da página:** **Gameplay**

- É a mesma categoria do upstream ([Offline Companions Reborn, mod 4047](https://www.nexusmods.com/valheim/mods/4047)) —
  o companheiro é um NPC de gameplay, não uma utilidade.
- Alternativa aceitável: **Miscellaneous**. Não use "User Interface" (a UI é só uma parte) nem "Utilities".

**Tags — obrigatórias (regra do Nexus, sob pena de moderação):**

- [x] **AI-Generated Content** — cobre código, UI, **vozes**, **diálogo** e **traduções** gerados por IA **dentro do mod**.
- [x] **AI Media** — cobre **arte promocional, thumbnail, vídeo e a descrição da página** feitos com IA (o banner,
  o ícone e a miniatura do vídeo são arte de IA).
- [ ] ~~**AI Assisted**~~ — **não marcar**. Essa tag exige *evidência de desenvolvimento predominantemente humano*
  (histórico de projeto, conhecimento demonstrável do código) que nós **não temos como comprovar de forma honesta**.
  O uso de IA neste projeto é substancial; declarar "AI Assisted" pode ser revertido pela moderação.
  A justificativa completa está escrita na descrição (seção "🤖 Divulgação de IA").

**Tags sugeridas (as que existem na lista do Valheim):**

| Tag | Por quê |
|---|---|
| **Gameplay** | categoria do mod |
| **Quality of Life** | o companheiro trabalha sozinho, obedece por voz/texto e tem painéis em jogo |
| **Translation** | a tradução PT-BR completa é metade do que o fork entrega |
| **Language: Portuguese** (grupo *Language*) | idioma do mod |
| **Overhaul** *(opcional)* | só se você quiser sinalizar que é um fork com uma camada nova por cima |
| **Bug Fixes** *(opcional)* | o fork também corrige coisas do upstream (eco da voz, ordem de pedra, clique do painel F8) |

**Tags que eu NÃO marcaria:**

- **Voice Acting** — o mod **tem** voz, mas ela é **sintetizada por IA** (não há dubladores). Marcar "Voice Acting"
  ao lado de "AI-Generated Content" passa a mensagem errada para quem filtra por voz humana. Se você quiser marcar,
  a descrição já explica que a voz é de IA — mas eu deixaria de fora.
- **Cheating / Unbalanced / Performance Optimization** e afins: não se aplicam e são alegações que não temos como provar.

**Requisitos (aba de requirements):** adicione o **BepInExPack** (procure "BepInEx" no site do Valheim).
O **Jotunn não é requisito** — não adicione (o mod não usa nenhuma API dele; ver `AGENTS.md` e o `LEIA-ME`).

---

## 3. Imagens

### O campo **Header** (o banner do topo da página)

| Arquivo | Tamanho | O que é |
|---|---|---|
| `docs\images\nexus\header-1300x372.png` | 1300×372 · 883 KB | A arte do mod em **faixa larga**, com o **lado esquerdo escurecido** |

> **Por que o lado esquerdo é escuro:** o Nexus desenha o **título do mod** e o breadcrumb (`Games / Valheim / Mods`) **por cima do header**, na esquerda — foi o que a página do upstream mostrou (`nexusmods.com/valheim/mods/4047`). O gradiente escuro (e uma leve escurecida no topo) é o que deixa esse texto legível.
>
> Como foi feito: recorte da **faixa dos personagens** do `docs\images\banner.png` (`y 60..418`, escalado de 1254→1300, praticamente sem perda) + o gradiente. **Não é asset novo** — é a mesma arte, no formato que o Nexus pede.

### A galeria de imagens (o que subir e em que ordem)

| # | Arquivo (no repositório) | Tamanho | O que é |
|---|---|---|---|
| **1 (principal)** | `docs\images\banner.png` | 1254×1254 · 2,7 MB | Arte do mod: os três companheiros + "AI COMPANIONS PT-BR" |
| 2 | `docs\images\screenshots\1-ordem-no-chat.jpg` | 1913×1003 · 557 KB | **In-game:** a ordem no chat com a resposta e o balão dele |
| 3 | `docs\images\screenshots\2-painel-da-ia.jpg` | 1907×1002 · 384 KB | **In-game:** o painel da IA (tecla `I`) |
| 4 | `docs\images\screenshots\4-menu-radial.jpg` | 1896×991 · 462 KB | **In-game:** o menu radial (segure `E` olhando o companheiro) |
| 5 | `docs\images\screenshots\3-painel-f8.jpg` | 1887×964 · 309 KB | **In-game:** a seção `[IA]` no painel F8 |
| 6 | `docs\wiki\images\video-thumb.jpg` | 1280×720 · 73 KB | Miniatura do vídeo do canal SuaTV |
| 7 | `docs\wiki\images\lmstudio-servidor.png` | 1200×760 · 85 KB | LM Studio com o Local Server ligado e o modelo carregado |
| 8 | `docs\wiki\images\config-ia.png` | 1402×753 · 81 KB | A seção `[IA]` do `.cfg` (BaseUrl, Modelo, ApiKey) |
| 9 | `docs\wiki\images\pasta-do-mod.png` | 1139×641 · 78 KB | A pasta do mod no Explorer (onde os arquivos ficam) |
| 10 *(opcional)* | `package\icon.png` | 256×256 · 184 KB | O ícone do pacote (256×256) |

Observações:

- O `banner.png` é **quadrado** e pesa 2,7 MB. Serve como principal, mas a miniatura do site é exibida em
  formato largo — se quiser, use o **`video-thumb.jpg` (16:9, 73 KB)** como principal e o banner em seguida.
  Decisão do dono (ver seção 10).
- **`banner.png`, `video-thumb.jpg` e `icon.png` são arte de IA** → é isso que exige a tag **AI Media** (já prevista).

### Os screenshots in-game (capturados em 17/10/2026)

Os quatro já estão no repositório, em `docs\images\screenshots\` (1920×1080 → JPEG q92):

| Arquivo | O que mostra |
|---|---|
| `1-ordem-no-chat.jpg` | **A ordem no chat** — "colete 10 madeiras", a resposta do companheiro e o balão de fala |
| `2-painel-da-ia.jpg` | **Painel da IA** (tecla `I`) — personalidade nas suas palavras, voz `pf_dora`, volume, push-to-talk e os toggles |
| `3-painel-f8.jpg` | **Painel F8** aberto na seção `[IA]` |
| `4-menu-radial.jpg` | **Menu radial** (segurando `E`) — a base do upstream inteira |

> **Originais em PNG** (sem recompressão) em `C:\Users\revenda33\Pictures\Screenshots\`: `chat.png`,
> `janela ia.png`, `f8.png` e `menu .png`. Se preferir subir o PNG, use esses.

> Os quatro são **capturas de jogo** (não são arte de IA) — a tag **AI Media** vem do banner, da miniatura do
> vídeo e do ícone, que **são** arte de IA.

---

## 4. Arquivos

Os **dois pacotes já estão montados** (a partir da instalação que roda no jogo, com a pasta do mod em
`BepInEx\plugins\ModAI-BR-Companions\`, mais `README.md`, `CHANGELOG.md` e `icon.png` na raiz do zip, e
**sem** o `whisper\jfk.wav`). Eles estão em `C:\Users\revenda33\AppData\Local\Temp\opencode\nexus\`:

| Arquivo | Bytes | Tamanho | MD5 | Conteúdo | Categoria no site |
|---|---|---|---|---|---|
| `ModAI_BR_Companions-1.6.3.zip` | 385776351 | 367,9 MB | `b5e5cbd4e19aa0695bae2f36ac3bc649` | o mod **+** os modelos de voz (Whisper `ggml-small-q5_1.bin` + `ggml-base-q5_1.bin`; Kokoro `model.int8.onnx` + `voices.bin` + `tokens.txt` + `espeak-ng-data\`) | **Main Files** |
| `ModAI_BR_Companions-1.6.3-sem-modelos.zip` | 29756376 | 28,4 MB | `02c0240a1f4f221580ac5aaec097466b` | o mod, as traduções, o `tokens.txt` e a pasta `espeak-ng-data\` — **faltam só** o `ggml-small-q5_1.bin` (Whisper) e o `model.int8.onnx` + `voices.bin` (Kokoro); a pasta `whisper\` vai **vazia** | **Optional Files** |

> A DLL do pacote é **byte a byte igual** à que está instalada e funcionando (mesmo MD5).
> **Histórico:** a release **1.6.2** (dois zips, `completo` + `sem-modelos`) ainda é a última publicada no
> GitHub; os links diretos dela existem na página de releases, mas os links da **1.6.3** só passam a existir
> depois da release nova (seção 9.1).

**Qual marcar como *main*: o `ModAI_BR_Companions-1.6.3.zip` (o completo).** Motivo: é o que a própria wiki
recomenda ("se você quer só instalar e usar, pegue o completo") e é o único que **funciona sem passos extras** —
no `sem-modelos` o usuário precisa baixar os modelos de voz em sites de terceiros antes de a voz funcionar.
Com o `sem-modelos` como principal, a maioria dos usuários do Nexus (que não lê a descrição inteira) vai ficar
sem voz e abrir post de suporte.

- Marque o `ModAI_BR_Companions-1.6.3.zip` como **main file** e como o **download padrão do gerenciador de mods**.
- Deixe o `ModAI_BR_Companions-1.6.3-sem-modelos.zip` em **Optional Files**, com a descrição curta:
  `Só o mod e as traduções - os modelos de voz você baixa por fora (links na descrição da página).`
- Se o upload do completo falhar por tamanho/limite, o plano B é: **main = `sem-modelos`** e o completo
  em Optional (ou apontar o completo para a release do GitHub, depois que a `v1.6.3` existir).

**Layout dentro do zip (importante para quem usa gerenciador de mods):**

- **Os dois pacotes que vamos subir já trazem `BepInEx\plugins\ModAI-BR-Companions\`** → o gerenciador de mods
  instala direto, sem o usuário ter que mover pasta nenhuma. **Diga isso na descrição da página** — é um ponto
  a favor (é o layout que o Nexus e os gerenciadores esperam).
- *(Histórico: o zip da **1.6.2** trazia a pasta `ModAI-BR-Companions\` na **raiz** — quem baixar aquele pacote
  ainda precisa colocá-la dentro de `BepInEx\plugins\`.)*
- A pasta **`LICENSES\`** (textos de licença de terceiros) **tem** que viajar dentro do pacote — ela já vai.

---

## 5. Permissões, créditos e autoria

**Permissões / distribuição:** marque as opções **mais abertas** que o formulário oferecer, por exemplo:

- permitir que outros **subam este arquivo em outros sites** (com crédito);
- permitir que outros **modifiquem este arquivo**;
- permitir que outros **usem assets deste arquivo** nos mods deles;
- permitir **conversão** para outros jogos, se a opção existir.

> **Por quê:** o mod é **MIT**, e o upstream também é MIT. A licença já concede redistribuição e modificação
> com o aviso de copyright — marcar uma permissão **mais restritiva** no site seria contradizer a licença.
> O crédito obrigatório já está na descrição e no `NOTICE.md`.

**Créditos (seção *Credits* / *Authors* da página):**

| Nome | Papel | Link |
|---|---|---|
| **maddhattr** | autor do upstream (**Offline Companions Reborn**, MIT) — o companheiro, a IA de movimento/combate, a UI | https://www.nexusmods.com/valheim/mods/4047 |
| **ProfMags** | autor do **Offline Companions** original (MIT) — conceito e código original | (link do mod original, se você tiver) |
| Você | a tradução PT-BR, a camada de IA, os ajustes | seu perfil |

Os créditos de terceiros (Whisper.net/whisper.cpp, sherpa-onnx, Kokoro-82M, onnxruntime, espeak-ng-data,
LM Studio, Qwen2.5) já estão escritos na **descrição** (seção *Créditos*) e nos arquivos de licença do pacote.

**Fork / conteúdo de terceiros — o que o Nexus exige e o que esta ficha assume:**

- A regra do site é clara: *"a mera atribuição do autor não substitui a obtenção de permissão explícita"*, e
  **traduções** devem ser enviadas como um **complemento que funciona junto do original**, com **link para o
  arquivo original**.
- No nosso caso a licença do upstream (**MIT**) **é** a permissão explícita de redistribuir e modificar com
  atribuição — e é assim que a descrição se apresenta: **fork autorizado pela licença MIT**, com o link do
  original, os avisos de copyright mantidos e a frase *"se algum dos autores quiser que algo seja alterado,
  removido ou incorporado, é só pedir"*.
- Mesmo assim, **recomendação forte: avise/peça o aval do autor do upstream antes de publicar** (um post na
  página dele ou uma issue no GitHub). Custa pouco, evita moderação e é a diferença entre "fork autorizado pela
  licença" e "fork autorizado pelo autor". Ver seção 10.

---

## 6. Descrição

Cole o conteúdo de **[`descricao-bbcode.txt`](descricao-bbcode.txt)** no campo **Description** do formulário
(o campo aceita BBCode; a pré-visualização fica ao lado).

- O arquivo tem o banner em `[img]` e o vídeo em `[youtube]40FnVEIbXyo[/youtube]`.
  **Se a imagem não carregar** na pré-visualização (hotlink do `raw.githubusercontent.com` bloqueado),
  suba o banner na galeria e troque o `[img]` pela URL da imagem na galeria do Nexus.
- **Confira a pré-visualização** das listas numeradas: se o `[list=1]` aparecer literalmente, troque por `[list]`
  (as etapas continuam legíveis).
- O vídeo também pode (e deve) ser adicionado na aba **Videos** do formulário, colando
  `https://www.youtube.com/watch?v=40FnVEIbXyo`.

---

## 7. Passo a passo do assistente de upload

> Os nomes das etapas mudam um pouco entre versões do site — o caminho é sempre *menu do usuário → upload de mod*.

1. **Entre na sua conta** no Nexus Mods e abra o menu do usuário (canto superior direito) → **Upload a mod**
   (ou vá direto para a página do jogo, Valheim, e use o botão de upload).
2. **Jogo e categoria:** escolha **Valheim** e a categoria **Gameplay**. Conteúdo adulto: **Não**.
   Aceite os termos se for pedido.
3. **Arquivos:** arraste o **`ModAI_BR_Companions-1.6.3.zip`** e marque como **Main**; adicione o
   **`ModAI_BR_Companions-1.6.3-sem-modelos.zip`** como **Optional**. Para cada um, preencha **nome**,
   **versão** (`1.6.3`), a **descrição curta** (ver seção 4) e marque o **download do gerenciador de mods**
   no principal.
4. **Detalhes da página:** preencha **título** e **resumo** (seção 1), cole a **descrição** (seção 6),
   escolha as **tags** (seção 2), adicione o **BepInExPack** nos requisitos e confira a categoria.
5. **Créditos:** adicione **maddhattr** e **ProfMags** (seção 5) com os links.
6. **Permissões:** marque as opções abertas (seção 5).
7. **Imagens:** suba na ordem da seção 3, defina a **principal** e ajuste a ordem. Marque as de IA na sua cabeça
   (a tag **AI Media** já cobre a página inteira).
8. **Vídeos:** cole a URL do YouTube.
9. **Pré-visualize** tudo (descrição, imagens, arquivos) e corrija o que estiver torto.
10. **Publique** (ou salve como rascunho, se quiser revisar depois). Depois de publicado, guarde o
    **mod ID** e o **file ID** — são eles que a API usa (seção 9).

---

## 8. Checklist final (antes de apertar publicar)

- [ ] A versão é a **1.6.3** e ela é a mesma no título, no resumo, no campo *version* e nos arquivos.
- [ ] Título e resumo colados da seção 1 (resumo ≤ 200 caracteres).
- [ ] Descrição colada do `descricao-bbcode.txt` — **em BBCode**, sem markdown.
- [ ] A descrição **não** menciona nenhuma outra plataforma de mods e **não** tem alegação não comprovada.
- [ ] Tags **AI-Generated Content** e **AI Media** marcadas; **AI Assisted** deixada de fora.
- [ ] Categoria **Gameplay**; requisito **BepInExPack** adicionado; **Jotunn não** listado.
- [ ] Créditos de **maddhattr** e **ProfMags** com os links; link da página do upstream na descrição.
- [ ] Permissões marcadas como abertas (coerentes com a MIT).
- [ ] Imagem principal definida e galeria na ordem da seção 3.
- [ ] Os dois arquivos anexados, com `ModAI_BR_Companions-1.6.3.zip` como **main** e
      `ModAI_BR_Companions-1.6.3-sem-modelos.zip` como **optional**.
- [ ] A pasta `LICENSES\` está **dentro** do zip publicado (licenças de terceiros viajam no pacote).
- [ ] Link da wiki (`.../wiki/Instalacao`) e da release **`v1.6.3`** do GitHub funcionando na descrição.
- [ ] Vídeo do YouTube adicionado.
- [ ] **Aviso/aval do autor do upstream** enviado (ou decidido conscientemente não enviar — seção 10).
- [ ] Screenshots in-game capturados e adicionados (seção 3) — se não houver, publicar mesmo assim e adicionar depois.
- [ ] Mod ID e file ID anotados para a automação futura.

---

## 9. Depois de publicado

### 9.1 Atualizar as nossas docs

- Colocar o link da página do Nexus no `README.md`, na `docs\wiki\Home.md` (e republicar a wiki) e registrar a
  decisão no `docs\roadmap.md` / `AGENTS.md`.
- A partir daí o **Nexus** passa a ser o canal de download do usuário final, e o **GitHub** continua sendo o
  repositório, a wiki e a release espelho (com o pacote completo).

**Fica pendente depois de publicar (a ficha não faz isso por você):**

- **Criar a release `v1.6.3` no GitHub** com **esses mesmos dois zips** (`ModAI_BR_Companions-1.6.3.zip` e
  `ModAI_BR_Companions-1.6.3-sem-modelos.zip`) — é o que faz o **link de downloads da descrição** bater com o
  arquivo que o usuário recebe (hoje a última release é a `v1.6.2`, com outros nomes e outro layout).
- **Atualizar a wiki** (as páginas ainda citam `1.6.2`): `Home.md`, `_Footer.md` e a linha de log em
  `Instalacao.md` / `Problemas-comuns.md` (o `Loading [Offline Companions Reborn <versão>]`). Lembrando que a
  wiki publicada vive no repositório separado `ModAI-BR-Companions.wiki.git` — edite em `docs\wiki\` e publique.

### 9.2 Publicar versões futuras pela API (v3)

**Limite importante:** a API **só mexe em mod que já existe** — ela adiciona **versões a um arquivo existente**.
**Criar a página do mod e o primeiro arquivo ainda não é possível por API**: isso é feito à mão, no site
(seção 7), uma única vez.

**Chave de API:** gere em https://www.nexusmods.com/settings/api-keys.
Base da API: `https://api.nexusmods.com/v3` · autenticação no header `apikey: <sua-chave>`.

**Caminho A — GitHub Action oficial (recomendado):** [`Nexus-Mods/upload-action`](https://github.com/Nexus-Mods/upload-action)

Inputs (conforme o README da action):

| Input | Obrigatório | O que é |
|---|---|---|
| `api_key` | sim | a sua chave de API |
| `file_id` | sim | ID do **arquivo** (o "file" do mod) que vai receber a nova versão |
| `filename` | sim | nome do zip a subir |
| `version` | sim | versão (ex.: `1.6.3`) |
| `display_name` | não | nome da versão (padrão: o nome do arquivo) |
| `description` | não | descrição da versão |
| `category` | não | `main` (padrão), `optional`, `miscellaneous`… |
| `archive_existing_version` | não | arquiva a versão anterior |
| `mod_id` | não | ID do mod — **obrigatório** se você usar `changelog` |
| `changelog` | não | texto do changelog desta versão |

Exemplo de workflow (`.github/workflows/nexus.yml`):

```yaml
name: Publicar no Nexus Mods
on:
  release:
    types: [published]
  workflow_dispatch:

jobs:
  upload:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Montar o zip (ajuste para o seu processo de empacotamento)
        shell: pwsh
        run: Compress-Archive -Path ./build/ModAI-BR-Companions -DestinationPath ModAI_BR_Companions.zip

      - name: Enviar ao Nexus
        uses: Nexus-Mods/upload-action@<tag>   # fixe uma tag/commit específico
        with:
          api_key: ${{ secrets.NEXUSMODS_API_KEY }}
          file_id: "0000000"          # file ID (veja abaixo como descobrir)
          filename: ModAI_BR_Companions.zip
          version: "1.6.3"
          display_name: "ModAI-BR-Companions 1.6.3"
          description: "Pacote sem os modelos (veja a descrição para baixá-los)."
          category: main
          archive_existing_version: true
          mod_id: "0000000"           # necessário para o changelog
          changelog: "Reempacotado: pasta do mod em BepInEx/plugins e README na raiz."
```

- **Onde achar os IDs:** na aba **Files** da página do mod, em *Advanced* (mostra o file ID e o mod ID), ou no
  menu de edição da página *Manage Files*.
- **`NEXUSMODS_API_BASE`** é a variável de ambiente que sobrescreve a base da API (padrão
  `https://api.nexusmods.com/v3`) — útil para testar em um ambiente próprio; não precisa mexer nela no uso normal.
- ⚠️ **A chave de API nunca vai para o repositório.** Use **GitHub Actions Secret**
  (`NEXUSMODS_API_KEY`, em *Settings → Secrets and variables → Actions*) ou variável de ambiente na sua máquina.
  Nunca em `.yml`, `.cs`, `.cfg` versionado, print de tela ou issue. Se vazar, **revogue e gere outra** na página
  de API keys.
- ⚠️ Em revisões mais novas da action os inputs foram renomeados (`file_id` → `file_group_id`,
  `category` → `file_category`). **Confira o README da tag que você fixar** — se o upload reclamar de input
  desconhecido, é isso.

**Caminho B — chamar a API na mão** (para quem não usa GitHub Actions). O fluxo é:

1. `POST /uploads` com `{ "filename": "...zip", "size_bytes": N }` → devolve o **id** do upload e uma
   **presigned URL**.
2. **PUT** do zip na presigned URL (multipart, se o arquivo for grande).
3. `POST /uploads/{id}/finalise` → espera o processamento ficar `available`.
4. `POST /mod-file-update-groups/{group_id}/versions` (ou `POST /mod-files/{file_id}/versions`) com
   `{ "upload_id": ..., "name": ..., "version": ..., "file_category": "main", "description": ... }` →
   a versão nova entra no arquivo existente.

Também existe ferramenta de terceiros que faz esse fluxo inteiro (`bengosney/nexus-upload-cli`), útil para
publicar da sua máquina com `NEXUS_API_KEY` no ambiente.

### 9.3 Rotina de release (para não repetir erro)

1. Fechar o conteúdo e subir a versão em **um** lugar só (`PluginVersion` em `CompanionsPlugin.cs`).
2. Atualizar `package/CHANGELOG.md` e as notas de release.
3. Empacotar (lembrando da pasta `LICENSES\` e do layout `BepInEx\plugins\...`), no padrão de nome
   **`ModAI_BR_Companions-<versão>.zip`** — e, se houver, o `ModAI_BR_Companions-<versão>-sem-modelos.zip`.
4. Publicar a release no **GitHub** (é o espelho e a fonte dos links).
5. Publicar/atualizar no **Nexus** (site na 1ª vez; API nas seguintes).
6. Atualizar wiki + README com o que mudou.

---

## 10. Decisões que dependem do dono do mod

1. **Qual arquivo é o principal:** a recomendação é o **completo** (`ModAI_BR_Companions-1.6.3.zip`, 367,9 MB).
   Se você preferir aliviar o download do site (ou evitar redistribuir os modelos de terceiros no Nexus),
   inverta: main = `sem-modelos` e o completo como opcional apontando para o GitHub.
2. **Pedir o aval do autor do upstream** (maddhattr) antes de publicar? A licença MIT já autoriza o fork com
   atribuição, mas o Nexus valoriza permissão explícita do autor. Recomendo pedir — e deixar registrado o pedido.
3. **Screenshots in-game:** não existem no repositório. Vamos capturá-los antes de publicar (a lista está na
   seção 3) ou publicamos sem eles e adicionamos depois?
4. **Imagem principal:** o `banner.png` (quadrado, 2,7 MB) ou o `video-thumb.jpg` (16:9, 73 KB)?
5. **Tag "Voice Acting":** marcar ou não? (Recomendação: **não** — a voz é sintetizada por IA.)
6. **Automação:** vamos configurar a GitHub Action agora (criando o secret `NEXUSMODS_API_KEY`) ou publicar
   manualmente as primeiras versões?
