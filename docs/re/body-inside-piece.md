# Corpo atravessado por peça: a física diz que é impossível parado, e por que não é

> **Por que existe.** Medido em jogo (**06/11/2026**): companheiro **em pé no chão** (`y` 38,78), cápsula
> 38,78–40,58, `wood_floor` a 39,78 e **1,56 m em XZ** do centro; visual: **só as pernas aparecem**.
> Responde se o jogo colide corpo × peça, se o collider é trigger, o que prende o corpo, se o jogador
> sofre do mesmo e como o jogo resolve. **Fonte:** `assembly_valheim.dll` decompilado (1.0.20,
> `%TEMP%\opencode\vh-decomp\valheim\`). Continua `placement-self-collision.md` e `deck-clearance.md`.

## 1. `Character` × `piece` colide? **Sim** — a peça é chão, não fantasma

A matriz de layers em si vive no asset `PhysicsManager` do `globalgamemanagers` (não lida nesta rodada), mas o
**código prova a colisão por comportamento**: `Character.s_groundRayMask` inclui `"piece"` (`Character.cs:683`) e
`OnCollisionStay` só carimba contato de chão para collider dessa máscara (`Character.cs:2713-2719`) — é assim que
ele anda, pula e recebe dano de queda em cima de assoalho. `FootStep` usa o mesmo: `layer == "piece"` tem material de
passo (`FootStep.cs:368`). **Conclusão: um corpo em pé não pode "atravessar" uma peça sólida — se atravessa, é
porque a sobreposição nasceu com o corpo já lá dentro** (o solver não expulsa rápido o bastante; §3).

## 2. `wood_floor` tem collider sólido. Não é trigger

`WearNTear.SetupColliders` monta a lista de caixas de apoio **ignorando `isTrigger` e collider com `attachedRigidbody`**
(`WearNTear.cs:484-514`, o filtro em `:491`), lê `BoxCollider.size * lossyScale` + `TransformVector(center)` (`:495-499`)
e cresce `+0,3` no total (`:507-510`). Só essas caixas contam apoio (`:871` `OverlapBoxNonAlloc`). Se o piso fosse
trigger ele **não apoiaria nada** e nenhuma casa ficaria de pé. O que pode **enganar a medição** é o **pivô**:
`wood_floor` de 2,27 m de lado com yaw girado tem **canto a 1,60 m** do centro — por isso "1,56 m em XZ" **não**
prova ausência de sobreposição; é a **caixa girada** que decide (é o que `BuildProbe.BoxOf` já faz).

## 3. O que prende: o corpo reescreve a própria velocidade todo fixed step

`UpdateWalking` monta `vector5 = currentVel − linearVelocity`, **só aplica `AddForce` se `> 0,01`**
(`Character.cs:1775-1798`) e depois **grava `m_body.linearVelocity = vel`** (`:1806-1808`), com `useGravity = true`
(`:1826`). Com o companheiro **parado** (`m_moveDir` zero) isso escreve **XZ ≈ 0** no Rigidbody a cada passo e
**anula o empurrão de depenetração** — que é limitado por `m_body.maxDepenetrationVelocity = 2f`
(`Character.cs:675`): **2 m/s × 0,02 s = 4 cm por passo**, e a direção é a **saída mais curta**. Empurrado para
baixo contra o terreno, ele fica **prensado e parado**, sem erro, sem queda, sem log. Não é bug de IA: é o
`Character` cumprindo "não ande".

## 4. O jogador fica preso igual — mas **nunca chega** nesse estado pelo caminho normal

O jogo **recusa** colocar a peça no corpo de quem coloca: `CheckPlacementGhostVSPlayers` (`Player.cs:4195-4225`)
testa o fantasma contra **todos** os `Character` a 30 m — **incluindo o próprio jogador** — com
`Physics.ComputePenetration` e **sem limiar** (`:4218`), veredito `BlockedbyPlayer` (`:4092` → `$msg_blocked`,
`:3049-3051`). **O que não existe:** `PlacePiece` é `Instantiate` (`Player.cs:3103`) e **não re-testa nada**; e
**nenhum tick** procura "corpo dentro de peça". O único resgate é `UnderWorldCheck` (`Character.cs:882-902`), que
compara com **`GetGroundHeight` — só a layer `terrain`** (`ZoneSystem.cs:2742/2752`), a cada **5 s** no NPC (`:889`)
e com o limite de **1,00 m** (`:893`). Peça não entra nessa conta, em nenhum caso. `IsUnderRoof`
(`Character.cs:1153-1156`) é o único raio para cima e tem 20 m: não é clearance. Agachar **não encurta** o collider
(`Player.cs:6279-6282`, animação pura).

## 5. Como o JOGO resolve: **não resolve — o jogador se desfaz**

Greps no assembly: `CheckCapsule`/`OverlapCapsule` existem **uma vez** (`Fireplace.cs:574`), `CapsuleCast` **zero**;
não há empurrão de personagem por peça, nem "peça que cai e cospe o corpo". `StaticPhysics.PushUp`
(`StaticPhysics.cs:111-125`) move **a peça**, nunca o `Character`. As saídas reais são **do jogador**: sair andando
(a depenetração de 2 m/s ajuda quando há lado livre) ou **tirar a peça** com o martelo — `RemovePiece`
(`Player.cs:2961-2963`) → `WearNTear.Remove` (`WearNTear.cs:1274`) → `Destroy` (`:1290`), que **dropa o material**
(`:1302-1305`). UX esperada: *"eu me virei sozinho"*, e o jogo **não** pune por ficar preso. Ou seja: a expectativa
do jogo é **evitar** (recusa) e **terceirizar a saída** — nunca mover ninguém.

## O que o mod deveria fazer

**(a) Não colocar a peça sobre o próprio corpo: copiar a recusa, não inventar regra.** O `Self(ai)` sai do `ignore`
incondicional (`MHR_Build.cs:9438-9441`) e o colocador entra no **mesmo laço** de `BuildProbe` com a **caixa girada
real** da peça × a **cápsula realizada** (`CapsuleOf`), respondendo "bloqueia" exatamente como `ComputePenetration`
sem limiar — mantendo a **tangência livre** (`BuildBody.cs:45`), que é o que o jogo permite e o que faz o "piso nos
pés" funcionar; o caso "peça baixa no chão dos pés" não se resolve com exceção, resolve-se com o passo do item (b).
**(b) Tirar o corpo se já estiver preso: fazer o que o jogador faz — andar, e só depois remover.** O caminho barato
que **já existe** é `OwnBodyOn` → `WalkSpot` → `StepTo`, e ele falha hoje por dois furos conhecidos (o `SelfStep`
terminal que **pula** o bloco na 3ª tentativa e o `Goto` que **descarta** o destino quando a peça está a
exatamente 1,00 m), não por falta de raio; o resgate de emergência tem de usar a régua do mover
`MHR_Clearance.MayEnter` (`fitsDest || !fitsHere`) e **nunca** o `UnderWorldCheck` (ele falha por 0,88 m contra
1,00 m e é cego a peça). Remover a peça — `WearNTear.Remove` — é o **último** recurso e **custa material**:
o jogo aceita esse custo porque o jogador escolhe; o companheiro tem de preferir **sair**, e só remover a peça
**que ele mesmo acabou de colocar** na mesma ordem, com log em PT-BR e o motivo nomeado.

## O que NÃO deu para verificar

- **A matriz de colisão de layers** (`PhysicsManager.m_LayerCollisionMatrix` em `globalgamemanagers`): deduzida por
  comportamento (`Character.cs:683/2719`, `FootStep.cs:368`), **não lida do asset** — a sessão não tinha shell/Python.
- **O collider real do `wood_floor`** (BoxCollider exato, se há mais de um, se algum é trigger): o decompilado só
  prova o **filtro** de `SetupColliders`; o número do prefab exigiria abrir o bundle. `support-rule-wearntear.md` §7
  já registrava isso.
- **A direção real do empurrão**: a aritmética de §3 casa com "prensado para baixo", mas eu **não tracei o solver**
  do PhysX — só o campo (`:675`) e o efeito observável (parado, 0,02 m de deslocamento).
- Se o `wood_floor` a 1,56 m **do pivô** é o tile que o visual mostra atravessado (§2: canto girado).
