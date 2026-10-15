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

### 2.1 Quem carrega a pedra (apurado na Fase 3.3d)

Lido direto dos assets (bundle `c4210710` do `StreamingAssets/SoftRef`, via UnityPy — os prefabs do Valheim 1.0
**não** estão no `resources.assets`):

| Prefab | Componentes | Drop |
|---|---|---|
| `Rock_3` (e `Rock_3_deepnorth`) | `Destructible` com **`m_health = 1`** e **sem tabela de drop** | `m_spawnWhenDestroyed` = `Rock_3_frac` |
| `Rock_3_frac` | **`MineRock5`** (5 áreas, `m_health = 30`) | `m_dropItems` = **`Stone` 3–6 por área** |
| `Rock_4` / `Rock_4_plains` / `Rock_4_deepnorth` | `Destructible` (30 HP) + `DropOnDestroyed` | **`Stone` 3–6** |

**Armadilha:** a pedra do `Rock_3` **não está** em nenhum componente dele — está no **frac que ele spawna ao
quebrar**. Qualquer classificação que olhe só os componentes do alvo (como o `MHR_GatherRules.DropsOf` fazia antes da
3.3d) conclui que `Rock_3` "não dropa nada", e alvo sem drop conhecido **não é bloqueado** por ordem/limite nenhum.
Por isso o `DropsOf` segue `Destructible.m_spawnWhenDestroyed`/`m_spawnWhenDamaged` (profundidade ≤ 3, cacheado por
alvo). O mesmo padrão vale a pena para outros `Destructible` que se trocam por uma versão "quebrada" (é o
`m_spawnWhenDamaged` que a 1.4.79 já tratava ao mover o alvo para a cópia rachada).

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
