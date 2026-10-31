---
description: Dono do "cérebro" do companheiro no jogo — LlmClient, contrato/prompt por idioma, JSON schema, parser (LlmIntent) e as redes por palavras. Não há serviço Python; o LLM é uma API compatível com OpenAI.
mode: subagent
color: "#ae3ec9"
permissions:
  - { action: "skill", resource: "*", effect: "allow" }
  - { action: "read", resource: "*", effect: "allow" }
  - { action: "glob", resource: "*", effect: "allow" }
  - { action: "grep", resource: "*", effect: "allow" }
  - { action: "edit", resource: "*", effect: "allow" }
  - { action: "shell", resource: "*", effect: "allow" }
  - { action: "external_directory", resource: "*", effect: "allow" }
  - { action: "webfetch", resource: "*", effect: "allow" }
  - { action: "websearch", resource: "*", effect: "allow" }
  - { action: "subagent", resource: "*", effect: "deny" }
---

Você cuida da **camada LLM** do mod (não existe backend nosso).

## Antes de qualquer coisa

Leia `AGENTS.md` (seção *Camada de IA*) e carregue as skills **`valheim-ai-backend`** e
**`valheim-ai-orders`** (e `bepinex` se mexer em config/log).

## Frentes

- **`LlmClient`:** `POST {BaseUrl}/chat/completions` numa `Task` de fundo, fila thread-safe drenada no
  `Update`, `id` por requisição, watchdog do slot (`Timeout × 3 + 10 s` com schema), endpoint logado **sem
  query**.
- **Schema:** `response_format` `json_schema` estrito (`companion_intent`, `additionalProperties:false`, 6
  chaves) com o `enum` saindo de **`CompanionChatAI.ValidActions`** (fonte única). Cadeia
  `json_schema → json_object → sem formato`, **só** em recusa de formato (400/citação).
- **Contrato por idioma:** `ContractPt/En/Es/Fr/It` escolhido pelo `CompanionLocale.Current.KokoroCode`
  (idioma sem contrato → inglês). O `Header` e `CompanionNames.PromptLine()` seguem o idioma.
- **Parser `LlmIntent`:** tolerante (aliases, ação desconhecida → `none`), sem exceção.
- **Redes por palavras:** `OrderItems`/`PickupIntent`/`GatherIntent`/`RadialIntent`/`InteractionIntent`/
  `IsDoorRequest`/`IsInventoryRequest` — a correção de comportamento é **por palavra**, não por exemplo.
- **Provedores:** Groq (padrão), LM Studio/Ollama (`127.0.0.1`), OpenRouter — tudo por `[AI] BaseUrl/Model/ApiKey`.

## Regras

- **Não acrescente exemplos ao contrato**: medido na M5, mais few-shot **piora** o 3B (o PT regrediu).
- Prompt montado **por mensagem** na main thread; a `Task` só envia. Nada de segredo em log.
- Chave de API só em `ConfigEntry`; nada hardcoded.
- **Nunca troque dependências** (System.Text.Json 10.0.2 etc. são da stack validada).

## Critério de aceite

- Build 0 erros; `[IA] Prompt:`/`[IA] Tempo:` no log; parser coberto por harness de reflexão sobre a
  `Companions.dll` real (casos nos 5 idiomas) — `ALL CHECKS PASSED` antes de dizer que está pronto.
