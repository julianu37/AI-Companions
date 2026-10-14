# Notas de coleta (harvesting) — descobertas da Fase 2.4b

Anotações técnicas apuradas ao implementar a coleta de recursos do companheiro (ver `docs/roadmap.md`).

## 1. De onde vem a madeira de uma árvore (armadilha importante)

A madeira **não** está em `TreeBase.m_dropWhenDestroyed`. O fluxo real é:

- **`TreeBase`**
  - `SpawnLog()` instancia `TreeBase.m_logPrefab` (uma "tora" por árvore) quando ela é derrubada.
  - `m_dropWhenDestroyed` contém apenas drops secundários (sementes, penas, resina) — **sem madeira**.
- **`TreeLog`** (a tora)
  - `m_dropWhenDestroyed` contém a **madeira**;
  - ao ser destruída, parte em `m_subLogPrefab` nos pontos de `m_subLogPoints.Length`.

Consequência para qualquer "banco de recursos": para mirar em **madeira**, o nó inicial é a
**`TreeBase`** (que gera `TreeLog`), e o rendimento real vem da **cascata de logs**, não do `TreeBase`.

Exemplo (Beech1): ~**20 Wood** no total — ~10 por estágio de tora (2 estágios) + ~2 do coto.

> Implementação: `CompanionHarvestScanner.EstimateYield` segue `m_logPrefab`/`m_subLogPrefab` até 3
> níveis para estimar o rendimento do recurso pedido.

## 2. Rochas: drop por área

`MineRock`/`MineRock5` dropam por **área de golpe** (`m_dropItems` distribuídos nas partes). A estimativa
de rendimento multiplica pelas áreas de golpe do nó.

## 3. Fórmula de "facilidade" (do Thrall) e por que não basta

A fórmula original do Thrall:

```
score = (min+max)/2 * 10 * 0.3 + (100/vida) * 0.3 + (100/(1+dist)) * 0.4
```

- Favorece **nós com vida baixa** (`100/vida`) e com `min/max` conhecidos — o que **não** se aplica a
  árvores/pedras (sem `min/max`, vida alta).
- Resultado prático: galhos (`Pickable`) e itens no chão (`ItemDrop`) dominam, e árvores nunca ganham.

**Nossa abordagem (v0.2.7):** estimar o **rendimento real** do nó para o recurso pedido e pontuar por
`valor × esforço × proximidade`, com o `m_minToolTier` do nó respeitado.

## 4. Ferramenta certa por nó

O nó expõe resistências/tier esperado; o companheiro equipa a melhor ferramenta disponível (machado para
madeira, picareta para pedra). Sem ferramenta capaz, o rendimento cai e o log avisa.
