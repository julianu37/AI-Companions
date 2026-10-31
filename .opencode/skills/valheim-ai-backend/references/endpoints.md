# Referência rápida — corpo e respostas (1.9.2)

Documento histórico do Thrall (endpoints `/instruct_agent`, `/synthesize_audio`): `docs/re/backend-contract.md`.
**O nosso contrato é o abaixo.**

## Corpo enviado (`POST {BaseUrl}/chat/completions`)

```json
{
  "model": "openai/gpt-oss-20b",
  "temperature": 0.4,
  "messages": [
    { "role": "system", "content": "<Header + persona + contrato do idioma + memória>" },
    { "role": "user",   "content": "<mensagem do jogador>" }
  ],
  "response_format": {
    "type": "json_schema",
    "json_schema": {
      "name": "companion_intent",
      "strict": true,
      "schema": {
        "type": "object",
        "additionalProperties": false,
        "required": ["action", "reply", "companion", "target", "resource", "amount"],
        "properties": {
          "action":   { "type": "string", "enum": ["<35 ações de CompanionChatAI.ValidActions>"] },
          "reply":    { "type": "string" },
          "companion":{ "type": ["string", "null"] },
          "target":   { "type": ["string", "null"] },
          "resource": { "type": ["string", "null"] },
          "amount":   { "type": ["integer", "null"] }
        }
      }
    }
  }
}
```

Formato `json_object` (2ª tentativa) = o mesmo corpo sem `json_schema`, com `"response_format": {"type":"json_object"}`.
3ª tentativa = sem `response_format`.

## Resposta esperada (conteúdo do `choices[0].message.content`)

```json
{"action":"wood","reply":"Deixa comigo.","companion":null,"target":null,"resource":"wood","amount":10}
```

- `action`: **exatamente** um valor de `ValidActions`.
- `reply`: uma frase curta, no idioma do jogo (é o que o TTS fala).
- `target`: objeto da ordem (`attack`/`equip`/`drop`) — o modelo **deve** nomear.
- `resource`: só para coleta (`wood`/`stone`/`ore`/`forage`), coerente com `action`.
- `amount`: inteiro (alvo total na mochila, para coleta).

## Erros e o que fazer

| Situação | Comportamento |
|---|---|
| 400 citando `response_format`/`json_schema` | desce um degrau da cadeia (1 tentativa) |
| 401/403 (chave) | erro no chat; não repete |
| 429 (limite do Groq) | erro no chat; o jogador espera |
| timeout / socket / 5xx | erro no chat; **não** repete |
| JSON inválido na resposta | `LlmIntent` → `none` (conversa) + log |
| resposta atrasada (id antigo) | descartada |
