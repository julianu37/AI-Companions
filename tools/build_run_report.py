#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Relatorio de run de obra (build) a partir do BepInEx\\LogOutput.log.

Uso:
    python tools/build_run_report.py [log] [--last|--all] [--json] [--barrier-seconds 10] [--order-seconds N]

    log      caminho do LogOutput.log (padrao: DEVRIDGE_LOG, senao o do Valheim desta maquina)
    --last   so a ultima ordem de obra do log (padrao)
    --all    todas as ordens de obra encontradas
    --json   imprime SO o JSON (schema 1), para comparar dois runs (A/B) sem `grep` linha a linha
    --barrier-seconds  teto de UMA retencao da barreira de corpo (padrao 10 = BodyBarrierSeconds)
    --order-seconds    duracao da ordem em segundos, medida FORA do log (relogio) - entra so no % do
                       BALANCO da secao REDESENHO (P0); sem ele o % sai `-`

Exit code: 0 = relatorio saiu; 1 = nenhuma ordem de obra no log; 2 = arquivo nao encontrado ou ilegivel.

O QUE ELE LE (e o que ele NAO faz)
----------------------------------
Cada linha do BepInEx tem o prefixo `[Info   :AI Companions] ` (ou `[Debug  :...] `) e o mod escreve
`[Build] <nome do companheiro>: <mensagem>`. O relatorio so olha linhas `[Build] ` (o `[Level] ` fica
de fora, menos o `order ended` dele, usado so para saber que a obra voltou depois do nivelamento).

Ordem de obra = bloco que comeca na primeira linha
    `blueprint <label>: <N> pieces near (...)`        (o lider; cada seguidor loga a sua)
ou
    `grupo de obra "<label>": <K> companheiro(s) no mesmo plano (<N> peças)`
e fecha no ultimo `order ended:` antes da proxima ordem (uma por companheiro: a ordem termina para
todos quando o plano fecha). Sem `order ended` ate o fim do arquivo = **em andamento** (ou log
interrompido), e o placar sai parcial.

⚠️ O log NAO TEM TIMESTAMP (medido) e a hora de modificacao do arquivo NAO e usada: o relatorio nao
inventa duracao. Ele conta LINHAS e deriva `linhas/peca` (o custo em log de uma peca), nada mais.
A duracao fica vazia com a nota "sem timestamp no log".

Blocos lidos (todos os numeros sao contagens de LINHA, nao tentativas deduplicadas):

  placar      done:, sem apoio: (watchdog, nomeadas), "N pulada(s)" (contador do SkipPiece),
              sem progresso (watchdog), it died, UNSUPPORTED, apoio de emergencia (+ recusas)
  retry       `retry no grupo:` / `retry later:` / `sem apoio ainda:` / `corpo no caminho:` /
              `desistindo de ... (motivo)`, agrupados por motivo
  espera      a linha do Finish `dispersao: ... · step-aside: ... · espera: ... s (barreira ... s em
              N retencao(oes) ... corpo/... dependencia, fila ... s, passo ... s, portao N)`, por
              worker e no total. O tempo da coluna "espera" e MEDIDO pelo mod (soma de dt por frame);
              o que e TETO e a barreira: N retencoes x BodyBarrierSeconds (padrao 10 s).
  dispersao   `dispersao: N adiada(s), M retomada(s)` + `step-aside: K pedido(s)` (a linha so sai
              quando algum contador e > 0), e as linhas `pedindo para sair da frente` / `saindo da
              frente` / `corpo na peca` (uma por peca, as do step-aside).

O que ele NAO cobre (declarado): ordens que nao sao blueprint (template/peca unica nao tem linha de
inicio); o `blueprint` de cada seguidor conta como continuacao da mesma ordem (mesmo label, sem
`order ended` no meio); duas ordens simultaneas de jogadores diferentes no mesmo log seriam somadas.

A FALA FINAL (`[TTS]`) e o segundo contador de puladas (11/2026)
--------------------------------------------------------------
O contador `N pulada(s)` so aparece nas linhas `desistindo de` (o `SkipPiece`). Quando o CANTEIRO da a
peca por vencida no watchdog (`sem progresso ha ... - N peca(s) sem apoio ficam de fora`), ninguem passa
pelo `SkipPiece`: o numero vai para o conjunto do canteiro e sai **na fala** do fim da obra
(`hc_ai_blueprint_done`, 5 idiomas), que o TTS escreve em ate 4 linhas `[TTS] ... "<frase>"`
(enfileirada / Frase / pronta / Tocando). O relatorio le essas linhas, deduplica pelo TEXTO e faz o
`skipped` do relatorio vir da FALA quando ela existe (`skipped_source` = `speech`); o `skipped_counter`
continua no JSON como estava. Foi o que separou, num run de 11/2026, as 3 puladas reais do 0 do
contador (o watchdog tinha dado 7 pecas por vencidas e 4 voltaram a subir).

A CAMINHADA (11/11/2026)
------------------------
O sufixo `· caminhada: X m` depois do `)` do `espera:` e o que CADA WORKER andou: a soma da distancia do
corpo ate a peca no instante em que a peca e tomada (`MHR_Build.EnterStep` -> `State.WalkMeters`). A
linha sai uma vez por worker, e no fim do plano de um grupo o `state.Group` ja e `null`
(`MHR_Build.cs:1358`), entao **cada linha e de um worker** - a soma das linhas e a caminhada da ORDEM e
nenhuma linha e um pedaco das outras. Por isso o relatorio soma e divide pelas pecas colocadas.

⚠️ MUDOU EM 11/11/2026 (o redesenho): a linha passou a ter DOIS numeros de caminhada -
`caminhada: 3196,6 m (3,2 m/peça) (este: 598,8 m)`. O **primeiro e do CANTEIRO** (a soma dos metros de
todos os workers, `MHR_BuildGroup.Walk`) e o `(este: W m)` e o DESTE worker. Somar o primeiro numero das
5 linhas daria **5x** o total; o relatorio passou a somar o `este:` (e cai no primeiro numero so quando a
linha e do formato antigo, que nao tem `(este:)`). O total do canteiro lido da linha fica em
`wait.total.walk_site` e a soma dos `este:` em `wait.total.walk`; os dois tem de bater (o relatorio avisa
quando divergem mais de 1%).

O REDESENHO (P0) - o par `next:` (11/11/2026)
--------------------------------------------
A peca que o worker vai pegar sai em DUAS variantes, e o par e o KPI do redesenho:

    next: wood_floor (300/985) a 3,4 m                             -> ele VAI ANDAR ate a peca
    next: wood_floor (300/985) a 3,4 m (ao alcance - sem caminhada) -> ele COLOCA DA ONDE ESTA

`next` (com caminhada) e `next ao alcance` (sem) sao contados SEPARADOS (`next.walk` / `next.stay`) e
aparecem na secao CUSTO POR PECA. O segundo e o numero que o redesenho quer subir.

⚠️ O `next:` NAO e uma linha por peca: ele sai so na pegada PRINCIPAL (`MHR_Build.cs:2178`); a peca
servida pelo retry do canteiro (`:2211`), pela barreira (`:2435`), a retomada (`:4751`), o retry pass
(`:4839`) e o apoio (`:6033`) chamam o `EnterStep` sem logar. Num run de 11/2026 foram **663** linhas de
`next:` para **985** pecas colocadas. Ou seja: `next ao alcance / next` e a taxa da PEGADA, e
`no alcance / pecas` (a secao REDESENHO) e a taxa das PECAS - **nao sao a mesma porcentagem** e o
relatorio imprime o aviso quando os dois denominadores diferem.

A mesma linha do Finish ganhou, DEPOIS do `)` do `espera:`, o que o redesenho mudou (tudo OPCIONAL - a
linha do Run 22 e anteriores termina no `)`, e ai o relatorio imprime `-`):

    · peças: 985/985, 0 pulada(s)          o placar do CANTEIRO (igual nas N linhas do grupo), com o
                                           mesmo `placed`/`skipped` da fala - e a fonte PREFERIDA do
                                           `skipped` desde 11/11/2026 (a fala fica como alternativa e o
                                           relatorio AVISA quando as fontes divergem)
    · caminhada: 3196,6 m (3,2 m/peça) (este: 598,8 m)   ver a secao acima
    · cpu: 105,9 µs/frame em 11572 frame(s), goto 0,5 s, place 0,5 s
                                           o custo do `Drive` por frame e o tempo das DUAS chamadas
                                           (`Goto`/`Place`) - ver a nota do BALANCO
    · posições: 164, peças/posição p50 1 p90 1, repeleu: 35
                                           quantas posicoes o worker usou, pecas por posicao (p90 pode
                                           vir `33+`, que e o balde de overflow) e quantas pecas foram
                                           tomadas LONGE da mais proxima por causa de outro corpo
    · no alcance: 146 peça(s)              pecas colocadas SEM andar (o `stay` do redesenho)

A secao REDESENHO (P0) da saida e o `redesign` do JSON leem esses campos por worker e no total; o
`balanco` compara `goto + place + espera` com o tempo da ORDEM - e o `goto`/`place` medem **a chamada**,
nao a permanencia na fase, entao estao subestimados por construcao (a nota sai impressa junto).

Schema JSON (v1): log, log_lines, scope, order_count, orders[] com index/label/status/start/
window/end/ends/plan/plan_blueprint/workers/group_workers/lines/build_lines/lines_per_piece/duration_s/
duration_note/placed/skipped/skipped_source/speech{label,lang,placed,skipped,sum,text}/speech_lines/
speech_distinct/placed_from_speech/skipped_counter/skipped_named_lines/skipped_named_pieces/watchdog_calls/
watchdog_dropped/retries{...}/source_totals/body_on_piece/emergency_posts/emergency_refused{...}/deaths/
unsupported/wait{lines,unparsed,workers[],total{...},holds_cap_s,walk,walk_per_piece,walk_site,
walk_este_sum,total_check}/spread{...}/aside_lines{...}/site_score{...}/next{lines,walk,stay,stay_pct}/
redesign{lines,workers[],total{...},balance{...}} - as tres ultimas sao de 11/11/2026 (o redesenho P0) e
saem com `-`/null quando o log e do formato antigo.

`status`: `done` (o ultimo `order ended` e uma chave de obra concluida: blueprint_done / build_plan_done
/ build_done), `ended` (encerrou com outra chave - `..._too_heavy`, `..._needs_station`, `..._collecting`:
hand-over ou recusa) ou `running` (sem `order ended` ate o fim do log). As chaves de TODOS os
`order ended` ficam em `ends[]` - num grupo cada companheiro loga o seu.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from collections import Counter, OrderedDict

DEFAULT_LOG = r"C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"

BUILD_TAG = "[Build] "
LEVEL_TAG = "[Level] "
TTS_TAG = "[TTS] "

# --------------------------------------------------------------------------------------------------
# leitura das linhas
# --------------------------------------------------------------------------------------------------


def strip_prefix(line):
    """Tira o prefixo do BepInEx (`[Info   :AI Companions] `). Continuacao (stack trace) -> None."""
    if not line.startswith("["):
        return None
    end = line.find("] ")
    if end < 0:
        return None
    return line[end + 2:]


WORKER_RE = re.compile(r"^(?P<who>[^:]{1,80}): (?P<msg>.+)$")

# mensagens escritas por `Log(ai, ...)` (levam o nome do companheiro na frente). Mensagem que nao
# esta aqui e do plugin (sem nome) - ex.: "blueprint X: 2 line(s) skipped".
WORKER_MSGS = (
    "done: ",
    "retry no grupo: ",
    "retry later: ",
    "sem apoio ainda: ",
    "sem apoio: ",
    "desistindo de ",
    "corpo no caminho: ",
    "corpo na peça ",
    "apoio de emergência: ",
    "sem lugar para o apoio de emergência",
    "sem material para o apoio de emergência",
    "it died during the build",
    "UNSUPPORTED: ",
    "dispersão: ",
    "sem progresso há ",
    "o canteiro está pronto",
    "entrou no grupo da obra",
    "não entrou no grupo",
    "já existe um grupo de obra",
    "order ended: ",
    "saindo da frente",
    # 11/11/2026 (o redesenho): a peca que o worker vai pegar - duas variantes, contadas separadas
    "next: ",
    '"',
)


def split_worker(rest):
    """(who, msg) quando a mensagem e de um companheiro; ('', rest) quando e do plugin.

    O `blueprint ... pieces` e o `grupo de obra ...` tambem levam o nome na frente (`StartBlueprint`
    loga pelo `Log(ai, ...)`), mas a linha `blueprint X: 2 line(s) skipped` e do plugin - por isso o
    teste e na mensagem depois do primeiro `: `, nunca no texto inteiro."""
    m = WORKER_RE.match(rest)
    if m:
        msg = m.group("msg")
        if msg.startswith(WORKER_MSGS) or msg.startswith("blueprint ") or msg.startswith("grupo de obra "):
            return m.group("who"), msg
    return "", rest


def num(text):
    """Numero do log: o jogo escreve com a cultura da maquina (virgula no PT-BR)."""
    return float(str(text).replace(",", "."))


# --------------------------------------------------------------------------------------------------
# formatos (arquivo:linha de referencia em src/Companions/Jobs/MHR_Build.cs)
# --------------------------------------------------------------------------------------------------

# chaves que dizem "a obra terminou de verdade" (as outras sao hand-over/recusa: `..._collecting`,
# `..._leveling`, `..._too_heavy`, `..._needs_station`, `..._failed` ...)
DONE_KEYS = ("hc_ai_blueprint_done", "hc_ai_build_plan_done", "hc_ai_build_done")

START_BLUEPRINT = re.compile(
    r"^blueprint (?P<label>.+?): (?P<count>\d+) pieces? near \((?P<pos>[^)]*)\)(?P<tail>.*)$")
START_GROUP = re.compile(
    r'^grupo de obra "(?P<label>[^"]+)": (?P<workers>\d+) companheiro\(s\) no mesmo plano '
    r"\((?P<count>\d+) peças\)")
END_RE = re.compile(r"^order ended: (?P<rest>.+)$")

# `done:` :6822
DONE = re.compile(r"^done: (?P<prefab>\S+) at \((?P<pos>[^)]*)\)(?P<tail>.*)$")
# `QueueRetry` no grupo: `retry no grupo: <prefab> (<motivo>) em (...)` :752
RETRY_GROUP = re.compile(r"^retry no grupo: (?P<prefab>\S+) \((?P<reason>.+)\) em \(")
# `QueueRetry` de 1 companheiro (sem grupo): `retry later: <prefab> (<motivo>) at (...) - n/3` :806
RETRY_LATER = re.compile(r"^retry later: (?P<prefab>\S+) \((?P<reason>.+)\) at \(")
# dependencia (nao gasta tentativa) :785
DEPENDENCY = re.compile(r"^sem apoio ainda: (?P<prefab>\S+) \((?P<reason>.+)\) em \(")
# corpo no caminho (Blocked, nao gasta tentativa) :747/:794
BODY_PATH = re.compile(r"^corpo no caminho: (?P<prefab>\S+) em \(.*\) - volta em (?P<wait>[^ ]+) s")
# 1a recusa por corpo de cada peca (com a capsula) :6411
BODY_PIECE = re.compile(r"^corpo na peça (?P<i>\d+)/(?P<n>\d+) \(")
# peca abandonada (pulada) :907
GIVE_UP = re.compile(
    r"^desistindo de (?P<prefab>\S+) \((?P<reason>.+)\) em \(.*\) - (?P<total>\d+) pulada\(s\)")
# peca nomeada pelo watchdog :3674/:3710
SKIP_NAMED = re.compile(r"^sem apoio: (?P<prefab>\S+) em \((?P<pos>[^)]*)\)")
# watchdog :3680/:3693
STALL = re.compile(r"^sem progresso há (?P<secs>[^ ]+) s - (?P<count>\d+) peça\(s\) sem apoio ficam de fora")
# poste de emergencia :5048
POST = re.compile(r"^apoio de emergência: (?P<prefab>\S+) \((?P<len>[^)]*)\) sob (?P<under>\S+) em \(")
POST_NO_PLACE = re.compile(r"^sem lugar para o apoio de emergência sob ")
POST_NO_MATERIAL = re.compile(r"^sem material para o apoio de emergência sob ")
# morte :2592
DIED = re.compile(r"^it died during the build - the order resumes after the respawn \(piece (?P<i>\d+)/(?P<n>\d+)\)")
# veredito do jogo :6812
UNSUPPORTED = re.compile(r"^UNSUPPORTED: (?P<prefab>\S+) \(support (?P<detail>[^)]*)\)")
# a linha unica do Finish (:7601) - dispersao + step-aside + espera
# 11/11/2026 (o redesenho P0): a linha ganhou - DEPOIS do `)` do `espera:` - o placar do canteiro, a
# caminhada do canteiro x a deste worker (`este:`), o custo por frame com `goto`/`place`, as posicoes e o
# `no alcance:`. Cada bloco e OPCIONAL: a linha do Run 22 e anteriores termina no `)`, e ai os campos saem
# `None` (o relatorio imprime `-`, nunca estoura).
WAIT = re.compile(
    r"^dispersão: (?P<skipped>\d+) adiada\(s\), (?P<resumed>\d+) retomada\(s\) \u00b7 "
    r"step-aside: (?P<asked>\d+) pedido\(s\) \u00b7 espera: (?P<total>-?[\d.,]+) s "
    r"\(barreira (?P<barrier>-?[\d.,]+) s em (?P<holds>\d+) retenção\(ões\) "
    r"(?P<body>-?[\d.,]+) corpo/(?P<dep>-?[\d.,]+) dependência, "
    r"fila (?P<queue>-?[\d.,]+) s(?: \(fim (?P<queue_end>-?[\d.,]+) s, meio (?P<queue_ahead>-?[\d.,]+) s\))?, "
    r"passo (?P<aside>-?[\d.,]+) s, portão (?P<gate>\d+)(?: \((?P<gate_seconds>-?[\d.,]+) s\))?"
    # 11/11/2026: o `corpo-curto` (BodyRetry, 2,5 s por curta) DENTRO do parentese - estava fora de todos
    # os baldes da linha antiga
    r"(?:, corpo-curto (?P<wait_short>-?[\d.,]+) s)?\)"
    # 11/11/2026: o placar do CANTEIRO (`peças: P/N, S pulada(s)`), igual nas N linhas do grupo
    r"(?: \u00b7 peças: (?P<site_placed>\d+)/(?P<site_plan>\d+), (?P<site_skipped>\d+) pulada\(s\))?"
    # 11/11/2026: o primeiro numero e a caminhada do CANTEIRO; o `(este: W m)` e a deste worker
    r"(?: \u00b7 caminhada: (?P<walk_site>-?[\d.,]+) m"
    r"(?: \((?P<walk_avg>-?[\d.,]+) m/peça\))?(?: \(este: (?P<walk_este>-?[\d.,]+) m\))?)?"
    # 11/11/2026: o custo do Drive por frame e o tempo das duas chamadas do corpo
    r"(?: \u00b7 cpu: (?P<cpu_us>-?[\d.,]+) \u00b5s/frame em (?P<cpu_frames>\d+) frame\(s\), "
    r"goto (?P<goto_s>-?[\d.,]+) s, place (?P<place_s>-?[\d.,]+) s)?"
    # 11/11/2026: as posicoes (a unidade de trabalho do redesenho), os percentis (o p90 vem `33+` no balde
    # de overflow) e a repulsao (peca tomada longe da mais proxima por causa de outro corpo)
    r"(?: \u00b7 posições: (?P<positions>\d+), peças/posição p50 (?P<p50>\d+\+?) p90 (?P<p90>\d+\+?), "
    r"repeleu: (?P<repelled>\d+))?"
    r"(?: \u00b7 no alcance: (?P<reached>\d+) peça\(s\))?")
# 11/11/2026 (o redesenho): a peca que o worker vai pegar. DUAS variantes - com caminhada e ao alcance
# (`MHR_Build.cs:2104`); o par e o KPI do redesenho.
NEXT = re.compile(
    r"^next: (?P<prefab>\S+) \((?P<i>\d+)/(?P<n>\d+)\) a (?P<dist>[^ ]+) m(?P<tail>.*)$")
NEXT_STAY = "ao alcance - sem caminhada"
# detalhe da dispersao (so com LogDetailed) :1189
SPREAD_DETAIL = re.compile(r"^dispersão: a peça (?P<i>\d+)/(?P<n>\d+) fica para depois - pegando ")
# a linha do Finish com `espera:` que o WAIT acima nao entendeu (o formato mudou?): nao perde em
# silencio - vira aviso na saida
WAIT_UNPARSED = re.compile(r"^dispersão: .*espera: ")
# step-aside :6485/:6458/:6565
ASIDE_ASK = re.compile(r'^".+" está na peça \d+ - pedindo para sair da frente')
ASIDE_NO_ROOM = re.compile(r'^".+" está na peça \d+ - sem lugar livre para ele sair da frente')
ASIDE_DONE = re.compile(r"^saindo da frente \(peça em ")

# a fala do fim da obra (`Say(ai, "hc_ai_blueprint_done", ...)`), nos 5 idiomas do mod: o ingles e NATIVO
# (ModLocalization.cs:619) e os outros 4 vem de `Translations\<idioma>.json`. A linha do TTS nao leva o
# prefixo `[Build] ` - ela e `[TTS] ` e sai ate 4 vezes para a MESMA frase:
#   [TTS] Fala enfileirada: "A MACCommHomeBasicBP está de pé: 982 peças, 3 puladas." (voz 42, ...)
#   [TTS] Frase 1/1: 4677 ms, RTF 0,94 ... | "A MACCommHomeBasicBP está de pé: 982 peças, 3 puladas."
#   [TTS] Fala #4 pronta: 4679 ms no total, ... | "A ..."
#   [TTS] Tocando "A ..." (4,99 s, 24000 Hz) na posicao de "HC_Companion(Clone)".
SPEECH_DONE = re.compile(
    r"^(?P<label>.+?):\s*(?P<placed>\d+)\s+(?:peças|pieces|piezas|pièces|pezzi),\s*"
    r"(?P<skipped>\d+)\s+(?:puladas|skipped|omitidas|ignorées|saltati)\.$")
# a moldura de cada idioma, para o rotulo da fala virar so o nome do blueprint
SPEECH_WRAP = (
    (re.compile(r"^A (?P<label>.+) está de pé$"), "pt"),
    (re.compile(r"^The (?P<label>.+) is up$"), "en"),
    (re.compile(r"^La (?P<label>.+) está en pie$"), "es"),
    (re.compile(r"^La (?P<label>.+) est debout$"), "fr"),
    (re.compile(r"^La (?P<label>.+) è in piedi$"), "it"),
)
QUOTED = re.compile(r'"([^"]*)"')


def parse_speech(text):
    """(label, placed, skipped, sum) da fala de fim de obra; None quando a frase nao e essa.

    `sum` = placed + skipped e e o que fecha com o plano (num run de 11/2026: 982 + 3 = 985 pecas)."""
    m = SPEECH_DONE.match(text)
    if not m:
        return None
    head = text[:m.start("placed")].rstrip()
    if head.endswith(":"):
        head = head[:-1].rstrip()
    label, lang = head, ""
    for rx, code in SPEECH_WRAP:
        w = rx.match(head)
        if w:
            label, lang = w.group("label"), code
            break
    placed = int(m.group("placed"))
    skipped = int(m.group("skipped"))
    return {
        "label": label.strip(),
        "lang": lang,
        "placed": placed,
        "skipped": skipped,
        "sum": placed + skipped,
        "text": text,
    }


def parse_message(msg):
    """(kind, dados) de uma mensagem `[Build] ` (com ou sem nome na frente)."""
    for kind, rx in (
        ("start_blueprint", START_BLUEPRINT),
        ("start_group", START_GROUP),
        ("done", DONE),
        ("retry_group", RETRY_GROUP),
        ("retry_later", RETRY_LATER),
        ("dependency", DEPENDENCY),
        ("body_path", BODY_PATH),
        ("body_piece", BODY_PIECE),
        ("give_up", GIVE_UP),
        ("skip_named", SKIP_NAMED),
        ("stall", STALL),
        ("post", POST),
        ("post_no_place", POST_NO_PLACE),
        ("post_no_material", POST_NO_MATERIAL),
        ("died", DIED),
        ("unsupported", UNSUPPORTED),
        ("wait", WAIT),
        ("next", NEXT),
        ("spread_detail", SPREAD_DETAIL),
        ("wait_unknown", WAIT_UNPARSED),
        ("aside_ask", ASIDE_ASK),
        ("aside_no_room", ASIDE_NO_ROOM),
        ("aside_done", ASIDE_DONE),
        ("end", END_RE),
    ):
        m = rx.match(msg)
        if m:
            return kind, m.groupdict()
    return "other", {}


# --------------------------------------------------------------------------------------------------
# blocos (ordens)
# --------------------------------------------------------------------------------------------------


class Block:
    """Uma ordem de obra em construcao (linhas ja lidas)."""

    def __init__(self, first_line):
        self.first_line = first_line
        self.label = ""
        self.plan_blueprint = None
        self.blueprint_line = None
        self.blueprint_text = ""
        self.group_line = None
        self.group_text = ""
        self.group_workers = None
        self.group_plan = None
        self.events = []          # (line, kind, who, data)
        self.ends = []            # (line, key, why, texto, linhas [Build] ate aqui)
        self.speeches = []        # (line, {label, placed, skipped, sum, text}) - as linhas do TTS
        self.build_count = 0      # toda linha `[Build] ` na janela
        self.window_end = first_line


def normalize_reason(reason):
    """O mesmo motivo escrito de dois jeitos (o log usa `no_support` e `no support`)."""
    r = " ".join(str(reason).split())
    if r.lower() == "no support":
        return "no_support"
    if r.lower().startswith("verification:"):
        return "verification"
    return r


def scan(path):
    blocks = []
    cur = None
    outside = 0
    total = 0
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        for n, raw in enumerate(fh, 1):
            total = n
            text = strip_prefix(raw.rstrip("\r\n"))
            if text is None:
                continue
            if text.startswith(LEVEL_TAG):
                # o nivelamento que devolve a obra termina com `[Level] order ended:` - nao fecha o
                # bloco do build, mas explica por que a obra recomeca (o `blueprint ... pieces` sai
                # de novo depois do `the ground is ready`)
                continue
            if text.startswith(TTS_TAG):
                # 11/2026: a fala do fim da obra e do `Say` do `Finish` e sai DEPOIS do `order ended` (e
                # em ate 4 linhas, uma por estagio do TTS). Ela e do bloco que esta aberto agora: o `cur`
                # so muda no `blueprint ... pieces` da proxima ordem, e `window_end` fecha a janela.
                if cur is not None:
                    for phrase in QUOTED.findall(text[len(TTS_TAG):]):
                        speech = parse_speech(phrase)
                        if speech is not None:
                            cur.speeches.append((n, speech))
                continue
            if not text.startswith(BUILD_TAG):
                continue
            rest = text[len(BUILD_TAG):]
            who, msg = split_worker(rest)
            kind, data = parse_message(msg)
            if kind in ("start_blueprint", "start_group"):
                # um `blueprint ... pieces` por companheiro do grupo (todos com o mesmo label): so e
                # ordem nova quando a anterior ja terminou (`order ended`) e nao e retomada
                new = cur is None or (bool(cur.ends) and " - resuming at piece " not in msg)
                if new:
                    if cur is not None:
                        cur.window_end = n - 1
                        blocks.append(cur)
                    cur = Block(n)
                if kind == "start_blueprint":
                    if cur.blueprint_line is None:
                        cur.blueprint_line = n
                        cur.blueprint_text = rest
                        cur.label = data["label"]
                        cur.plan_blueprint = int(data["count"])
                else:
                    if cur.group_line is None:
                        cur.group_line = n
                        cur.group_text = rest
                        cur.group_workers = int(data["workers"])
                        cur.group_plan = int(data["count"])
                        cur.label = data["label"]
            if cur is None:
                outside += 1
                continue
            cur.build_count += 1
            if kind == "end":
                body = data["rest"].strip()
                key, why = body, None
                if body.endswith(")"):
                    cut = body.rfind(" (")
                    if cut > 0:
                        key, why = body[:cut].strip(), body[cut + 2:-1].strip()
                # o contador de linhas [Build] no instante do `order ended`: e o que conta quando ha
                # mais de um (um por companheiro do grupo) e a ordem fecha no ultimo
                cur.ends.append((n, key or "(silent)", why, rest, cur.build_count))
            else:
                cur.events.append((n, kind, who, data))
    if cur is not None:
        cur.window_end = total
        blocks.append(cur)
    return blocks, total, outside


# --------------------------------------------------------------------------------------------------
# agregacao
# --------------------------------------------------------------------------------------------------


def finalize(block, index, barrier_seconds, order_seconds=None):
    end = block.ends[-1] if block.ends else None
    end_line = end[0] if end else block.window_end
    plan = block.group_plan if block.group_plan is not None else block.plan_blueprint
    first_line = block.first_line
    start_line = block.group_line or block.blueprint_line or first_line
    start_text = block.group_text or block.blueprint_text

    lines = max(0, end_line - first_line + 1)
    build_lines = end[4] if end else block.build_count
    events = [e for e in block.events if first_line <= e[0] <= end_line]

    # 11/2026: a fala final da obra - o numero que o JOGADOR ouviu. O `skipped` do relatorio passa a vir
    # dela quando existe: o contador `N pulada(s)` so conta `desistindo de`, e a peca que o WATCHDOG do
    # canteiro da por vencida nao passa por la (Run de 11/2026: fala 3, contador 0).
    speeches = [s for line, s in block.speeches if first_line <= line <= block.window_end]
    speech = speeches[-1] if speeches else None

    placed = 0
    skipped_counter = 0
    named = []
    watchdog_calls = 0
    watchdog_dropped = 0
    posts = Counter()
    post_no_place = 0
    post_no_material = 0
    deaths = 0
    unsupported = 0
    body_piece = 0
    aside_ask = 0
    aside_no_room = 0
    aside_done = 0
    spread_detail = 0
    wait_unparsed = 0
    # 11/11/2026 (o redesenho): o par do `next:` (a peca e buscada andando x colocada de onde esta) e o
    # placar do canteiro que cada linha do Finish carrega
    next_walk = 0
    next_stay = 0
    site_scores = []

    retries = OrderedDict()
    source_totals = Counter()
    wait_by_worker = OrderedDict()   # uma linha `espera:` por worker (somadas se houver mais de uma)
    wait_lines = 0
    spread_workers = OrderedDict()
    workers = OrderedDict()

    def add_reason(source, reason):
        reason = normalize_reason(reason)
        row = retries.setdefault(reason, {
            "retry_grupo": 0, "retry_later": 0, "sem_apoio_ainda": 0,
            "corpo": 0, "desistindo": 0, "total": 0,
        })
        row[source] += 1
        row["total"] += 1
        source_totals[source] += 1

    for line, kind, who, data in events:
        if who:
            workers.setdefault(who, 0)
            workers[who] += 1
        if kind == "done":
            placed += 1
        elif kind == "skip_named":
            named.append((data["prefab"], data["pos"]))
        elif kind == "give_up":
            skipped_counter = max(skipped_counter, int(data["total"]))
            add_reason("desistindo", data["reason"])
        elif kind == "retry_group":
            add_reason("retry_grupo", data["reason"])
        elif kind == "retry_later":
            add_reason("retry_later", data["reason"])
        elif kind == "dependency":
            add_reason("sem_apoio_ainda", data["reason"])
        elif kind == "body_path":
            add_reason("corpo", "blocked_by_player")
        elif kind == "body_piece":
            body_piece += 1
        elif kind == "stall":
            watchdog_calls += 1
            watchdog_dropped += int(data["count"])
        elif kind == "post":
            posts[data["prefab"]] += 1
        elif kind == "post_no_place":
            post_no_place += 1
        elif kind == "post_no_material":
            post_no_material += 1
        elif kind == "died":
            deaths += 1
        elif kind == "unsupported":
            unsupported += 1
        elif kind == "wait":
            wait_lines += 1
            w = who or "(sem nome)"
            row = wait_by_worker.get(w)
            if row is None:
                row = wait_by_worker[w] = {
                    "worker": w, "total": 0.0, "barrier": 0.0, "body": 0.0,
                    "dependency": 0.0, "queue": 0.0, "aside": 0.0, "holds": 0, "gate": 0,
                    "queue_end": 0.0, "queue_ahead": 0.0, "gate_seconds": 0.0,
                    "walk": 0.0, "walk_lines": 0,
                    # 11/11/2026 (o redesenho P0): campos novos da mesma linha. Ficam em None/0 quando a
                    # linha e do formato antigo, e a saida imprime `-` por causa disso.
                    "wait_short": 0.0, "wait_short_lines": 0,
                    "walk_site": None, "walk_avg": None, "walk_este": 0.0, "walk_este_lines": 0,
                    "cpu_us": None, "cpu_frames": 0, "goto_s": 0.0, "place_s": 0.0, "cpu_lines": 0,
                    "positions": 0, "p50": None, "p90": None, "repelled": 0, "reached": 0,
                    "reached_lines": 0,
                }
            row["total"] += num(data["total"])
            row["barrier"] += num(data["barrier"])
            row["body"] += num(data["body"])
            row["dependency"] += num(data["dep"])
            row["queue"] += num(data["queue"])
            row["aside"] += num(data["aside"])
            row["holds"] += int(data["holds"])
            row["gate"] += int(data["gate"])
            # 11/11/2026: os metros que ESTE worker andou. No formato novo o primeiro numero e a caminhada
            # do CANTEIRO e o `(este: W m)` e a deste worker: somar o primeiro daria N x o total, entao a
            # soma usa o `este:` e so cai no primeiro numero quando ele e o unico (formato do Run 22).
            if data.get("walk_este") is not None:
                row["walk"] += num(data["walk_este"])
                row["walk_este"] += num(data["walk_este"])
                row["walk_este_lines"] += 1
                row["walk_lines"] += 1
            elif data.get("walk_site") is not None:
                row["walk"] += num(data["walk_site"])
                row["walk_lines"] += 1
            if data.get("walk_site") is not None:
                row["walk_site"] = num(data["walk_site"])
            if data.get("walk_avg") is not None:
                row["walk_avg"] = num(data["walk_avg"])
            # 11/11/2026: o corpo-curto (BodyRetry) e o custo por frame com as duas chamadas do corpo
            if data.get("wait_short") is not None:
                row["wait_short"] += num(data["wait_short"])
                row["wait_short_lines"] += 1
            if data.get("cpu_us") is not None:
                row["cpu_us"] = num(data["cpu_us"])
                row["cpu_lines"] += 1
            if data.get("cpu_frames") is not None:
                row["cpu_frames"] += int(data["cpu_frames"])
            row["goto_s"] += num(data.get("goto_s") or 0)
            row["place_s"] += num(data.get("place_s") or 0)
            # 11/11/2026: as posicoes, os percentis e a repulsao do redesenho. `p50`/`p90` sao a ULTIMA
            # linha do worker (o percentil e um retrato, nao um acumulado) e o p90 pode vir `33+`.
            if data.get("positions") is not None:
                row["positions"] += int(data["positions"])
            if data.get("p50") is not None:
                row["p50"] = data["p50"]
            if data.get("p90") is not None:
                row["p90"] = data["p90"]
            row["repelled"] += int(data.get("repelled") or 0)
            if data.get("reached") is not None:
                row["reached"] += int(data["reached"])
                row["reached_lines"] += 1
            # 11/11/2026: o placar do CANTEIRO de cada linha (as N linhas do grupo tem de concordar)
            if data.get("site_placed") is not None:
                site_scores.append((int(data["site_placed"]), int(data["site_plan"]),
                                    int(data["site_skipped"])))
            # 11/11/2026 (Run 19): the fields of the single-clock barrier - the two halves of the queue
            # (end of plan x middle of plan) and the seconds the gate itself held. Absent on the older
            # format (Run 18 and before), so they default to 0 instead of breaking the line.
            row["queue_end"] += num(data.get("queue_end") or 0)
            row["queue_ahead"] += num(data.get("queue_ahead") or 0)
            row["gate_seconds"] += num(data.get("gate_seconds") or 0)
            sp = spread_workers.setdefault(w, {
                "skipped": 0, "resumed": 0, "aside_asked": 0, "lines": 0,
            })
            sp["skipped"] += int(data["skipped"])
            sp["resumed"] += int(data["resumed"])
            sp["aside_asked"] += int(data["asked"])
            sp["lines"] += 1
        elif kind == "next":
            # 11/11/2026 (o KPI do redesenho): a peca e buscada ANDANDO (a linha diz a distancia) ou o
            # worker ja esta ao alcance e coloca de onde esta (` (ao alcance - sem caminhada)`).
            if NEXT_STAY in (data.get("tail") or ""):
                next_stay += 1
            else:
                next_walk += 1
        elif kind == "spread_detail":
            spread_detail += 1
        elif kind == "wait_unknown":
            wait_unparsed += 1
        elif kind == "aside_ask":
            aside_ask += 1
        elif kind == "aside_no_room":
            aside_no_room += 1
        elif kind == "aside_done":
            aside_done += 1

    named_unique = len(set(named))
    wait_rows = []
    for row in wait_by_worker.values():
        # 11/11/2026 (o redesenho): o balanco do worker. O `corpo-curto` NAO entra de novo - ele ja esta
        # DENTRO do `total` (total = barreira + fila + passo + portao + curto, `MHR_Build.cs:8869-8873`);
        # `balance_literal_s` guarda a soma do pedido (goto + place + espera + curto) para quem comparar.
        balance = row["goto_s"] + row["place_s"] + row["total"]
        wait_rows.append({
            "worker": row["worker"],
            "total": round(row["total"], 1),
            "barrier": round(row["barrier"], 1),
            "body": round(row["body"], 1),
            "dependency": round(row["dependency"], 1),
            "queue": round(row["queue"], 1),
            "aside": round(row["aside"], 1),
            "holds": row["holds"],
            "gate": row["gate"],
            "queue_end": round(row["queue_end"], 1),
            "queue_ahead": round(row["queue_ahead"], 1),
            "gate_seconds": round(row["gate_seconds"], 1),
            "walk": round(row["walk"], 1),
            "walk_lines": row["walk_lines"],
            # --- 11/11/2026 (o redesenho P0) - None quando a linha do Finish e do formato antigo ---
            "wait_short": round(row["wait_short"], 1) if row["wait_short_lines"] else None,
            "walk_site": round(row["walk_site"], 1) if row["walk_site"] is not None else None,
            "walk_avg": row["walk_avg"],
            "walk_este": round(row["walk_este"], 1) if row["walk_este_lines"] else None,
            "cpu_us": row["cpu_us"],
            "cpu_frames": row["cpu_frames"] if row["cpu_lines"] else None,
            "goto_s": round(row["goto_s"], 2) if row["cpu_lines"] else None,
            "place_s": round(row["place_s"], 2) if row["cpu_lines"] else None,
            "positions": row["positions"] if row["cpu_lines"] or row["positions"] else None,
            "p50": row["p50"],
            "p90": row["p90"],
            "repelled": row["repelled"] if row["p50"] is not None or row["repelled"] else None,
            "reached": row["reached"] if row["reached_lines"] else None,
            "balance_s": round(balance, 1) if (row["cpu_lines"] or row["wait_short_lines"]) else None,
            "balance_literal_s": (round(balance + row["wait_short"], 1)
                                  if (row["cpu_lines"] or row["wait_short_lines"]) else None),
        })
    wait_total = {
        "total": round(sum(r["total"] for r in wait_rows), 1),
        "barrier": round(sum(r["barrier"] for r in wait_rows), 1),
        "body": round(sum(r["body"] for r in wait_rows), 1),
        "dependency": round(sum(r["dependency"] for r in wait_rows), 1),
        "queue": round(sum(r["queue"] for r in wait_rows), 1),
        "aside": round(sum(r["aside"] for r in wait_rows), 1),
        "holds": sum(r["holds"] for r in wait_rows),
        "gate": sum(r["gate"] for r in wait_rows),
        "queue_end": round(sum(r["queue_end"] for r in wait_rows), 1),
        "queue_ahead": round(sum(r["queue_ahead"] for r in wait_rows), 1),
        "gate_seconds": round(sum(r["gate_seconds"] for r in wait_rows), 1),
        # a caminhada da ORDEM = a soma das linhas (uma por worker), nunca uma das linhas
        "walk": round(sum(r["walk"] for r in wait_rows), 1),
        "walk_lines": sum(r["walk_lines"] for r in wait_rows),
    }
    wait_total["walk_per_piece"] = (round(wait_total["walk"] / placed, 2) if placed else None)

    # ----------------------------------------------------------------------------------------------
    # 11/11/2026 (o redesenho P0) - o placar do CANTEIRO, o par do `next:` e o bloco do redesenho
    # ----------------------------------------------------------------------------------------------
    # o placar do canteiro sai igual em todas as linhas do grupo; `variants` guarda o que veio diferente
    # (worker que saiu antes do fim pode ter impresso o numero parcial - vale a linha MAIS completa)
    site_score = None
    if site_scores:
        best = max(site_scores, key=lambda s: (s[0] + s[2], s[0], -s[2]))
        site_score = {
            "lines": len(site_scores),
            "placed": best[0],
            "plan": best[1],
            "skipped": best[2],
            "consistent": len(set(site_scores)) == 1,
            "variants": [list(v) for v in sorted(set(site_scores))],
        }

    site_placed = site_score["placed"] if site_score else None
    pieces = site_placed if site_placed else placed

    red_workers = []
    for row in wait_rows:
        red_workers.append({
            "worker": row["worker"],
            "positions": row["positions"],
            "p50": row["p50"],
            "p90": row["p90"],
            "repelled": row["repelled"],
            "reached": row["reached"],
            "walk_este": row["walk_este"],
            "cpu_us": row["cpu_us"],
            "cpu_frames": row["cpu_frames"],
            "goto_s": row["goto_s"],
            "place_s": row["place_s"],
            "espera": row["total"],
            "wait_short": row["wait_short"],
            "balance_s": row["balance_s"],
            "balance_literal_s": row["balance_literal_s"],
        })

    def sum_or_none(key):
        vals = [w[key] for w in red_workers if w[key] is not None]
        return round(sum(vals), 1) if vals else None

    reached_total = sum_or_none("reached")
    short_total = sum_or_none("wait_short")
    goto_total = sum_or_none("goto_s")
    place_total = sum_or_none("place_s")
    repelled_total = sum_or_none("repelled")
    positions_total = sum_or_none("positions")
    cpu_us_total = sum_or_none("cpu_us")
    cpu_frames_max = max([w["cpu_frames"] for w in red_workers if w["cpu_frames"]] or [0])
    # a soma de µs/frame x frames e a CPU total gasta pelo Drive da equipe (o numero do §17.25)
    cpu_ms_total = None
    if cpu_us_total is not None and cpu_frames_max:
        cpu_ms_total = round(sum((w["cpu_us"] or 0) * (w["cpu_frames"] or 0)
                                 for w in red_workers) / 1000.0, 1)
    walk_sites = sorted({r["walk_site"] for r in wait_rows if r.get("walk_site") is not None})
    walk_site = walk_sites[-1] if walk_sites else None
    walk_avg_printed = next((r["walk_avg"] for r in wait_rows if r.get("walk_avg") is not None), None)
    walk_per_piece = round(walk_site / pieces, 2) if (walk_site is not None and pieces) else None
    walk_delta_pct = None
    if walk_site and wait_total["walk_lines"]:
        walk_delta_pct = round(100.0 * (wait_total["walk"] - walk_site) / walk_site, 1)

    balance_total = sum_or_none("balance_s")
    balance_literal = sum_or_none("balance_literal_s")
    balance_mean = (round(balance_total / len(red_workers), 1)
                    if (balance_total is not None and red_workers) else None)
    balance_share = None
    if balance_total is not None and order_seconds:
        balance_share = round(100.0 * balance_total / order_seconds, 1)
    # piso da duracao da ordem: a MAIOR espera de um worker aconteceu DENTRO da ordem
    espera_max = max([r["total"] for r in wait_rows] or [0])
    mix = {}
    if balance_total:
        for key, value in (("espera", wait_total["total"]), ("goto", goto_total or 0.0),
                           ("place", place_total or 0.0)):
            mix[key] = round(100.0 * value / balance_total, 1)

    redesign = {
        "lines": len(red_workers),
        "workers": red_workers,
        "total": {
            "positions": int(positions_total) if positions_total is not None else None,
            "p50": None,   # percentil nao se soma entre workers (a linha traz o de cada um)
            "p90": None,
            "repelled": int(repelled_total) if repelled_total is not None else None,
            "reached": int(reached_total) if reached_total is not None else None,
            "pieces": pieces,
            "reached_pct": (round(100.0 * reached_total / pieces, 1)
                            if (reached_total is not None and pieces) else None),
            "walk_este": wait_total["walk"] if wait_total["walk_lines"] else None,
            "walk_site": walk_site,
            "walk_per_piece": walk_per_piece,
            "walk_avg_printed": walk_avg_printed,
            "walk_delta_pct": walk_delta_pct,
            "cpu_us": cpu_us_total,
            "cpu_frames": cpu_frames_max or None,
            "cpu_ms": cpu_ms_total,
            "goto_s": goto_total,
            "place_s": place_total,
            "wait_short": short_total,
        },
        "balance": {
            "formula": "goto + place + espera (o corpo-curto ja esta DENTRO da espera)",
            "total_s": balance_total,
            "mean_per_worker_s": balance_mean,
            "literal_s": balance_literal,
            "literal_note": ("goto + place + espera + corpo-curto, como pedido no briefing: conta o "
                             "corpo-curto DUAS vezes - `total` do mod ja inclui o curto"),
            "mix_pct": mix,
            "order_seconds": order_seconds,
            "share_of_order_pct": balance_share,
            "espera_max_s": round(espera_max, 1),
            "duration_floor_note": ("a maior espera de um worker aconteceu dentro da ordem: a duracao e "
                                    ">= ela"),
            "goto_place_note": ("`goto`/`place` medem a CHAMADA (MHR_Build.Goto/Place), nao a "
                                "permanencia na fase - estao subestimados por construcao"),
        },
    }

    next_total = next_walk + next_stay
    next_block = {
        "lines": next_total,
        "walk": next_walk,
        "stay": next_stay,
        "stay_pct": (round(100.0 * next_stay / next_total, 1) if next_total else None),
    }

    # 11/11/2026: a linha carrega o total E as parcelas - conferir se fecham (o `portao` ja ficou fora de
    # todos os baldes uma vez e o total nao o incluia; `total_diff` e o quanto sobra)
    for row in wait_rows:
        parts = (row["barrier"] + row["queue"] + row["aside"] + row["gate_seconds"]
                 + (row["wait_short"] or 0.0))
        row["total_diff"] = round(row["total"] - parts, 1)
    wait_total["walk_site"] = walk_site
    wait_total["walk_este_sum"] = wait_total["walk"]
    wait_total["total_diff"] = round(wait_total["total"] - (
        wait_total["barrier"] + wait_total["queue"] + wait_total["aside"]
        + wait_total["gate_seconds"] + (short_total or 0.0)), 1)

    return {
        "index": index,
        "label": block.label,
        "status": ("running" if end is None
                   else ("done" if end[1] in DONE_KEYS else "ended")),
        "start": {
            "line": start_line,
            "kind": "group" if block.group_line else "blueprint",
            "text": start_text,
        },
        "window": {"first_line": first_line, "last_line": end_line},
        "end": ({"line": end[0], "key": end[1], "why": end[2], "text": end[3]} if end else None),
        "ends": [{"line": e[0], "key": e[1], "why": e[2]} for e in block.ends],
        "plan": plan,
        "plan_blueprint": block.plan_blueprint,
        "group_workers": block.group_workers,
        "workers": list(workers),
        "lines": lines,
        "build_lines": build_lines,
        "lines_per_piece": round(build_lines / plan, 2) if plan else None,
        "duration_s": None,
        "duration_note": "sem timestamp no log - nao estimada a partir de linhas",
        "placed": placed,
        # 11/11/2026 (o redesenho): o `skipped`/`placed` do CANTEIRO vem da linha do Finish (`peças: P/N,
        # S pulada(s)`, igual nas N linhas do grupo) quando ela existe - ela e o mesmo numero da fala sem
        # depender do TTS estar ligado. A fala continua como fonte alternativa e o contador das linhas
        # (`desistindo de`) como ultimo recurso; `skipped_sources` mostra as tres para a divergencia.
        "skipped": (site_score["skipped"] if site_score
                    else (speech["skipped"] if speech else skipped_counter)),
        "skipped_source": ("site" if site_score
                           else ("speech" if speech else ("counter" if skipped_counter else "none"))),
        "skipped_sources": {
            "site": site_score["skipped"] if site_score else None,
            "speech": speech["skipped"] if speech else None,
            "counter": skipped_counter,
        },
        "site_score": site_score,
        "speech": speech,
        "speech_lines": len(speeches),
        "speech_distinct": len({s["text"] for s in speeches}),
        "placed_from_speech": (speech["placed"] if speech else None),
        "skipped_counter": skipped_counter,
        "skipped_named_lines": len(named),
        "skipped_named_pieces": named_unique,
        "watchdog_calls": watchdog_calls,
        "watchdog_dropped": watchdog_dropped,
        "retries": retries,
        "source_totals": dict(source_totals),
        "body_on_piece": body_piece,
        "emergency_posts": sum(posts.values()),
        "emergency_posts_by_prefab": dict(posts),
        "emergency_refused": {"no_place": post_no_place, "no_material": post_no_material},
        "deaths": deaths,
        "unsupported": unsupported,
        "wait": {
            "lines": wait_lines,
            "unparsed": wait_unparsed,
            "workers": wait_rows,
            "total": wait_total,
            "holds_cap_s": round(wait_total["holds"] * barrier_seconds, 1),
            "holds_cap_note": "teto (retencoes x %.1f s), nao tempo medido" % barrier_seconds,
            "barrier_seconds": barrier_seconds,
        },
        "spread": {
            "workers": dict(spread_workers),
            "skipped": sum(v["skipped"] for v in spread_workers.values()),
            "resumed": sum(v["resumed"] for v in spread_workers.values()),
            "aside_asked": sum(v["aside_asked"] for v in spread_workers.values()),
            "lines": sum(v["lines"] for v in spread_workers.values()),
        },
        "aside_lines": {
            "pedindo": aside_ask,
            "sem_lugar": aside_no_room,
            "saindo": aside_done,
            "dispersao_detalhe": spread_detail,
        },
        # 11/11/2026 (o redesenho P0): o par do `next:` e o bloco do redesenho (posicoes, repulsao, no
        # alcance, cpu/goto/place, corpo-curto e o balanco) - tudo `None` no log do formato antigo
        "next": next_block,
        "redesign": redesign,
    }


# --------------------------------------------------------------------------------------------------
# saida de texto
# --------------------------------------------------------------------------------------------------


def f1(value):
    return ("%.1f" % value).replace(".", ",")


def f2(value):
    """Duas casas: `linhas/peca` de um run curto (parcial) nao pode virar `0,0`."""
    return ("%.2f" % value).replace(".", ",")


def pct(part, whole):
    return "%d%%" % round(100.0 * part / whole) if whole else "-"


def status_text(o):
    if o["status"] == "done":
        return "concluída"
    if o["status"] == "ended":
        return "encerrada (%s)" % (o["end"]["key"] if o["end"] else "?")
    return "EM ANDAMENTO"


def print_order(o, total_orders, out):
    sep = "-" * 74
    print(sep, file=out)
    print(" ORDEM %d/%d - %s" % (o["index"], total_orders, o["label"]), file=out)
    print(sep, file=out)

    plan_txt = ("%d peça(s)" % o["plan"]) if o["plan"] is not None else "?"
    if o["plan_blueprint"] is not None and o["plan_blueprint"] != o["plan"]:
        plan_txt += " (a linha do blueprint dizia %d)" % o["plan_blueprint"]
    print(" plano:        %s" % plan_txt, file=out)
    print(" inicio:       linha %d (%s) - %s"
          % (o["start"]["line"],
             "grupo de obra" if o["start"]["kind"] == "group" else "blueprint",
             o["start"]["text"]), file=out)
    if o["end"]:
        print(" fim:          linha %d - %s" % (o["end"]["line"], o["end"]["text"]), file=out)
        if o["status"] == "done":
            print(" status:       concluída (order ended: %s)" % o["end"]["key"], file=out)
        else:
            print(" status:       ENCERRADA sem concluir a obra (order ended: %s%s)"
                  % (o["end"]["key"], (" - " + o["end"]["why"]) if o["end"]["why"] else ""), file=out)
    else:
        print(" fim:          -- (nenhum `order ended` até o fim do log)", file=out)
        print(" status:       EM ANDAMENTO (ou log interrompido) - placar PARCIAL", file=out)
    if len(o["ends"]) > 1:
        counts = Counter(e["key"] for e in o["ends"])
        print(" ends:         %d order ended (%s)"
              % (len(o["ends"]), ", ".join("%s x%d" % (k, v) for k, v in counts.items())), file=out)
    print(" duração:      -- (o log não tem timestamp; não é estimada a partir de linhas)", file=out)
    print(" linhas:       %d na janela da ordem (%d-%d) | %d [Build]"
          % (o["lines"], o["window"]["first_line"], o["window"]["last_line"], o["build_lines"]),
          file=out)
    if o["lines_per_piece"] is not None:
        print(" linhas/peça:  %s  ([Build] / plano)" % f2(o["lines_per_piece"]), file=out)
    workers = o["workers"]
    workers_txt = "%d no log" % len(workers)
    if o["group_workers"] is not None:
        workers_txt += " | o grupo anunciou %d" % o["group_workers"]
    print(" workers:      %s%s" % (workers_txt, (": " + ", ".join(workers)) if workers else ""), file=out)

    print("", file=out)
    print(" PLACAR", file=out)
    print("   done (colocadas): ........... %d" % o["placed"], file=out)
    print("   puladas (contador): ......... %d   (maior \"N pulada(s)\" do run)"
          % o["skipped_counter"], file=out)
    # 11/11/2026 (o redesenho): o placar do CANTEIRO, que a linha do Finish imprime igual em todas as N
    # linhas do grupo - e a fonte do `skipped` desde entao; as outras duas ficam para a divergencia.
    site = o.get("site_score")
    if site:
        print("   placar do canteiro (linha): . %d/%d, %d pulada(s)   (%d linha(s) do Finish, %s)"
              % (site["placed"], site["plan"], site["skipped"], site["lines"],
                 "todas iguais" if site["consistent"] else
                 "DIVERGEM: " + ", ".join("%d/%d/%d" % tuple(v) for v in site["variants"])), file=out)
        if site["plan"] != o["plan"]:
            print("        ATENCAO: o placar do canteiro diz plano %d e a linha de inicio diz %s"
                  % (site["plan"], o["plan"]), file=out)
        if site["placed"] != o["placed"]:
            print("        ATENCAO: o placar diz %d colocada(s) e o log tem %d linha(s) `done:`"
                  % (site["placed"], o["placed"]), file=out)
        if site["skipped"] != o["skipped_counter"]:
            print("        ATENCAO: o placar diz %d pulada(s) e o contador das linhas diz %d - o contador"
                  " so conta `desistindo de`" % (site["skipped"], o["skipped_counter"]), file=out)
        if o["speech"] and site["skipped"] != o["speech"]["skipped"]:
            print("        ATENCAO: o placar do canteiro diz %d pulada(s) e a FALA diz %d"
                  % (site["skipped"], o["speech"]["skipped"]), file=out)
    else:
        print("   placar do canteiro (linha): . -- (a linha do Finish desta ordem nao tem `pe\u00e7as: P/N`)",
              file=out)
    if o["speech"]:
        sp = o["speech"]
        print("   puladas (a FALA final): ..... %d   (%d colocada(s) + %d = %d do plano | %s)"
              % (sp["skipped"], sp["placed"], sp["skipped"], sp["sum"], sp["text"]), file=out)
        if o["speech_lines"] > 1:
            print("        (a mesma frase em %d linha(s) do TTS; o texto e a chave da deduplicacao)"
                  % o["speech_lines"], file=out)
        if sp["skipped"] != o["skipped_counter"]:
            print("        ATENCAO: a fala diz %d e o contador das linhas diz %d - o contador so conta"
                  " `desistindo de`;" % (sp["skipped"], o["skipped_counter"]), file=out)
            print("        a peca dada por vencida pelo watchdog do canteiro nao passa por ele", file=out)
        if sp["placed"] != o["placed"]:
            print("        ATENCAO: a fala diz %d colocada(s) e o log tem %d linha(s) `done:`"
                  % (sp["placed"], o["placed"]), file=out)
    else:
        print("   puladas (a FALA final): ..... -- (nenhuma fala de fim de obra nesta ordem)", file=out)
    print("   \"sem apoio:\" nomeadas: ...... %d linha(s) / %d peça(s) distinta(s)  (watchdog)"
          % (o["skipped_named_lines"], o["skipped_named_pieces"]), file=out)
    print("   watchdog: ................... %d chamada(s), %d peça(s) \"ficam de fora\" (soma)"
          % (o["watchdog_calls"], o["watchdog_dropped"]), file=out)
    print("   mortes (\"it died\"): ......... %d" % o["deaths"], file=out)
    print("   UNSUPPORTED: ................ %d" % o["unsupported"], file=out)
    print("   apoio de emergência: ........ %d colocado(s) | %d sem lugar | %d sem material"
          % (o["emergency_posts"], o["emergency_refused"]["no_place"],
             o["emergency_refused"]["no_material"]), file=out)
    print("   corpo na peça (1ª/peça): .... %d linha(s)" % o["body_on_piece"], file=out)
    print("   step-aside: ................. %d pedindo | %d sem lugar | %d saindo"
          % (o["aside_lines"]["pedindo"], o["aside_lines"]["sem_lugar"],
             o["aside_lines"]["saindo"]), file=out)

    print("", file=out)
    print(" RETRY / RECUSA (por motivo)", file=out)
    if o["retries"]:
        print("   %-28s %7s %7s %6s %6s %8s %7s"
              % ("motivo", "grupo", "later", "dep", "corpo", "desist.", "total"), file=out)
        for reason, row in sorted(o["retries"].items(), key=lambda kv: (-kv[1]["total"], kv[0])):
            print("   %-28s %7d %7d %6d %6d %8d %7d"
                  % (reason, row["retry_grupo"], row["retry_later"], row["sem_apoio_ainda"],
                     row["corpo"], row["desistindo"], row["total"]), file=out)
        print("   (grupo = \"retry no grupo\"; later = \"retry later\" (1 companheiro); dep = "
              "\"sem apoio ainda\";", file=out)
        print("    corpo = \"corpo no caminho\" (blocked_by_player); desist. = \"desistindo de\" - "
              "peça pulada)", file=out)
    else:
        print("   nenhuma recusa de peça registrada", file=out)

    print("", file=out)
    print(" ESPERA (a linha do Finish; a coluna \"espera\" é tempo MEDIDO pelo mod)", file=out)
    if o["wait"]["lines"]:
        print("   %-18s %8s %9s %7s %9s %6s %7s %9s %7s %10s %10s"
              % ("worker", "espera", "barreira", "corpo", "depend.", "fila", "passo",
                 "retenções", "portão", "c.curto", "caminhada"), file=out)
        for row in o["wait"]["workers"]:
            print("   %-18s %8s %9s %7s %9s %6s %7s %9d %7d %10s %10s"
                  % (row["worker"], f1(row["total"]), f1(row["barrier"]), f1(row["body"]),
                     f1(row["dependency"]), f1(row["queue"]), f1(row["aside"]),
                     row["holds"], row["gate"],
                     f1(row["wait_short"]) if row.get("wait_short") is not None else "-",
                     f1(row["walk"]) if row["walk_lines"] else "-"), file=out)
        t = o["wait"]["total"]
        short_total = o["redesign"]["total"].get("wait_short")
        print("   %-18s %8s %9s %7s %9s %6s %7s %9d %7d %10s %10s"
              % ("TOTAL", f1(t["total"]), f1(t["barrier"]), f1(t["body"]), f1(t["dependency"]),
                 f1(t["queue"]), f1(t["aside"]), t["holds"], t["gate"],
                 f1(short_total) if short_total is not None else "-",
                 f1(t["walk"]) if t["walk_lines"] else "-"), file=out)
        print("   retenções: %d no total -> teto %s s cada = %s s (TETO da barreira, não tempo "
              "medido;" % (t["holds"], f1(o["wait"]["barrier_seconds"]),
                           f1(o["wait"]["holds_cap_s"])), file=out)
        print("   o tempo medido é a coluna \"espera\", somada por frame pelo próprio mod)", file=out)
        # 11/11/2026 (Run 19): the queue split and the gate's own seconds (fields of the run 19+ line)
        print("   fila por metade: fim de plano %s s | meio de plano %s s | portão %s s em %d retenção(ões)"
              % (f1(t["queue_end"]), f1(t["queue_ahead"]), f1(t["gate_seconds"]), t["gate"]), file=out)
        # 11/11/2026 (o redesenho): o corpo-curto estava FORA de todos os baldes da linha antiga - agora ele
        # entra no `espera` e a conferencia abaixo diz se as parcelas fecham com o total
        if short_total is not None:
            print("   corpo-curto: %s s no total (BodyRetry, ~2,5 s por curta) - ja DENTRO da coluna "
                  "\"espera\"" % f1(short_total), file=out)
        if abs(t.get("total_diff") or 0) > 0.6:
            print("   ATENÇÃO: as parcelas não fecham com o \"espera\": sobra %s s (barreira+fila+passo+"
                  "portão+curto = %s s)"
                  % (f1(t["total_diff"]),
                     f1(t["total"] - t["total_diff"])), file=out)
        else:
            print("   conferência: barreira+fila+passo+portão+corpo-curto fecha com o \"espera\" (%s s de "
                  "diferença)" % f1(t["total_diff"]), file=out)
        # 11/11/2026: a caminhada - uma linha por worker, entao a soma e a da ORDEM
        if t["walk_lines"]:
            media = ("%s m/peça" % f2(t["walk_per_piece"])) if t["walk_per_piece"] else "-"
            print("   caminhada: %s m somando as %d linha(s) = %s - CADA LINHA E DE UM WORKER, nao um"
                  " pedaco das outras:" % (f1(t["walk"]), t["walk_lines"], media), file=out)
            if t.get("walk_site") is not None:
                print("   caminhada do CANTEIRO (o 1º número da linha): %s m = %s m/peça%s"
                      % (f1(t["walk_site"]),
                         f2(o["redesign"]["total"]["walk_per_piece"])
                         if o["redesign"]["total"]["walk_per_piece"] is not None else "-",
                         (" | a linha imprime %s" % f2(o["redesign"]["total"]["walk_avg_printed"]))
                         if o["redesign"]["total"].get("walk_avg_printed") is not None else ""),
                      file=out)
                delta = o["redesign"]["total"].get("walk_delta_pct")
                if delta is not None and abs(delta) > 1.0:
                    print("   ATENÇÃO: a soma dos `(este:)` difere do total do canteiro em %s%% - o "
                          "relatorio usa a SOMA dos workers" % f1(delta), file=out)
                else:
                    print("   as duas leituras batem (soma dos `(este:)` x total do canteiro, %s%%)"
                          % (f1(delta) if delta is not None else "-"), file=out)
            print("   na saida do grupo o `state.Group` ja e nulo (`MHR_Build.cs:1358`), entao nenhuma"
                  " linha e o total - o total e a SOMA", file=out)
        else:
            print("   caminhada: -- (as linhas desta ordem nao tem o sufixo `· caminhada: X m`)", file=out)
    else:
        if not o["wait"]["unparsed"]:
            print("   nenhuma linha \"espera:\" nesta ordem (a linha só sai quando algum contador é > 0)",
                  file=out)
    if o["wait"]["unparsed"]:
        print("   ATENÇÃO: %d linha(s) com `espera:` NÃO reconhecida(s) - o formato do Finish mudou?"
              % o["wait"]["unparsed"], file=out)

    print("", file=out)
    print(" DISPERSÃO / STEP-ASIDE", file=out)
    if o["spread"]["lines"]:
        print("   %-20s %9s %10s %12s"
              % ("worker", "adiadas", "retomadas", "step-aside"), file=out)
        for worker, row in o["spread"]["workers"].items():
            print("   %-20s %9d %10d %12d"
                  % (worker, row["skipped"], row["resumed"], row["aside_asked"]), file=out)
        print("   %-20s %9d %10d %12d"
              % ("TOTAL", o["spread"]["skipped"], o["spread"]["resumed"],
                 o["spread"]["aside_asked"]), file=out)
    else:
        print("   nenhuma linha \"dispersão:\" nesta ordem (só sai quando algum contador é > 0)",
              file=out)
    if o["aside_lines"]["dispersao_detalhe"]:
        print("   detalhe da dispersão (LogDetailed): %d linha(s)" % o["aside_lines"]["dispersao_detalhe"],
              file=out)

    # ----------------------------------------------------------------------------------------------
    # 11/11/2026 (o redesenho P0): a peca e buscada andando ou colocada de onde ele esta, quantas
    # posicoes cada worker usa, quanto ele foi repelido e quanto custa a chamada - por worker e no total
    # ----------------------------------------------------------------------------------------------
    red = o.get("redesign") or {}
    red_total = red.get("total") or {}
    print("", file=out)
    print(" REDESENHO (P0) - a peça é buscada andando ou colocada de onde ele está", file=out)
    if red.get("lines"):
        print("   %-18s %9s %5s %5s %8s %11s %11s %9s %6s %6s %9s"
              % ("worker", "posições", "p50", "p90", "repeleu", "no alcance", "cam.(este)",
                 "cpu µs/f", "goto", "place", "c.curto"), file=out)

        def dash(value, fmt=f1):
            return fmt(value) if value is not None else "-"

        for w in red["workers"]:
            print("   %-18s %9s %5s %5s %8s %11s %11s %9s %6s %6s %9s"
                  % (w["worker"],
                     dash(w["positions"], lambda v: "%d" % v),
                     w["p50"] if w["p50"] is not None else "-",
                     w["p90"] if w["p90"] is not None else "-",
                     dash(w["repelled"], lambda v: "%d" % v),
                     dash(w["reached"], lambda v: "%d" % v),
                     dash(w["walk_este"]),
                     dash(w["cpu_us"]),
                     dash(w["goto_s"], f2),
                     dash(w["place_s"], f2),
                     dash(w["wait_short"])), file=out)
        print("   %-18s %9s %5s %5s %8s %11s %11s %9s %6s %6s %9s"
              % ("TOTAL",
                 dash(red_total.get("positions"), lambda v: "%d" % v),
                 "-", "-",   # percentil de posicao nao se soma entre workers
                 dash(red_total.get("repelled"), lambda v: "%d" % v),
                 dash(red_total.get("reached"), lambda v: "%d" % v),
                 dash(red_total.get("walk_este")),
                 dash(red_total.get("cpu_us")),
                 dash(red_total.get("goto_s"), f2),
                 dash(red_total.get("place_s"), f2),
                 dash(red_total.get("wait_short"))), file=out)
        # --- os numeros que o redesenho quer mover ---
        if red_total.get("reached") is not None and red_total.get("pieces"):
            print("   no alcance / peças: %d/%d = %s colocadas SEM caminhada (denominador = o placar do "
                  "CANTEIRO)" % (red_total["reached"], red_total["pieces"],
                                  "%d%%" % round(red_total["reached_pct"] or 0)), file=out)
        else:
            print("   no alcance / peças: - (a linha do Finish nao trouxe `no alcance:`)", file=out)
        if red_total.get("walk_site") is not None:
            print("   caminhada do canteiro: %s m = %s m/peça"
                  % (f1(red_total["walk_site"]),
                     f2(red_total["walk_per_piece"]) if red_total.get("walk_per_piece") is not None else "-"),
                  file=out)
        else:
            print("   caminhada do canteiro: - (formato antigo: nao ha o total do canteiro na linha)",
                  file=out)
        print("   repeleu (soma): %s peça(s) | corpo-curto (soma): %s s | cpu: %s µs/frame somados em %s "
              "frame(s) = %s s de CPU"
              % (dash(red_total.get("repelled"), lambda v: "%d" % v),
                 dash(red_total.get("wait_short")),
                 dash(red_total.get("cpu_us")),
                 dash(red_total.get("cpu_frames"), lambda v: "%d" % v),
                 dash(red_total.get("cpu_ms"), lambda v: f1(v / 1000.0))), file=out)
        # --- o balanco ---
        bal = red.get("balance") or {}
        print("", file=out)
        print("   BALANÇO: goto + place + espera (o corpo-curto já está DENTRO da espera)", file=out)
        print("     %-18s %7s %7s %9s %10s" % ("worker", "goto", "place", "espera", "balanço"),
              file=out)
        for w in red["workers"]:
            print("     %-18s %7s %7s %9s %10s"
                  % (w["worker"],
                     dash(w["goto_s"], f2), dash(w["place_s"], f2),
                     dash(w.get("espera")),
                     dash(w["balance_s"])), file=out)
        if bal.get("total_s") is not None:
            print("     %-18s %7s %7s %9s %10s   (média %s s por worker)"
                  % ("TOTAL", dash(red_total.get("goto_s"), f2), dash(red_total.get("place_s"), f2),
                     f1(o["wait"]["total"]["total"]), f1(bal["total_s"]),
                     f1(bal["mean_per_worker_s"]) if bal.get("mean_per_worker_s") is not None else "-"),
                  file=out)
            mix = bal.get("mix_pct") or {}
            if mix:
                print("     do balanço: %s"
                      % " | ".join("%s %s%%" % (k, f1(v)) for k, v in mix.items()), file=out)
            if bal.get("literal_s") is not None:
                print("     (a soma literal do pedido `goto+place+espera+corpo-curto` daria %s s: o "
                      "corpo-curto entra 2x)" % f1(bal["literal_s"]), file=out)
        else:
            print("     %-18s %7s %7s %9s %10s" % ("TOTAL", "-", "-", "-", "-"), file=out)
        print("     nota: `goto`/`place` medem a CHAMADA (MHR_Build.Goto/Place), não a permanência na "
              "fase - estão subestimados por construção", file=out)
        print("     nota: o total é TIME-HOMEM (soma dos workers em paralelo), não o relógio da obra",
              file=out)
        if bal.get("order_seconds"):
            print("     tempo da ordem: %s s (informado em --order-seconds) -> o balanço é %s%% (time-homem "
                  "x relógio; por worker é a leitura justa)"
                  % (f1(bal["order_seconds"]), f1(bal["share_of_order_pct"] or 0)), file=out)
        else:
            print("     tempo da ordem: -- (o log não tem timestamp; use --order-seconds N para o %)",
                  file=out)
        if bal.get("espera_max_s"):
            print("     piso da duração: a maior espera de um worker (%s s) aconteceu DENTRO da ordem -> a "
                  "obra durou ao menos isso" % f1(bal["espera_max_s"]), file=out)
    else:
        print("   -- (nenhuma linha do Finish nesta ordem: não há posições/cpu/no alcance para ler)",
              file=out)

    print("", file=out)
    print(" CUSTO POR PEÇA", file=out)
    if o["lines_per_piece"] is not None:
        print("   linhas/peça: %s  (%d linha(s) [Build] / %d peça(s))"
              % (f2(o["lines_per_piece"]), o["build_lines"], o["plan"]), file=out)
    total_retry = sum(row["total"] for row in o["retries"].values())
    if total_retry:
        parts = ["%s %s" % (reason, pct(row["total"], total_retry))
                 for reason, row in sorted(o["retries"].items(), key=lambda kv: -kv[1]["total"])]
        print("   distribuição: %s (%d recusa(s))" % (", ".join(parts), total_retry), file=out)
    # 11/11/2026 (o KPI do redesenho): quantas pecas cada worker pegou ANDANDO x quantas ele colocou de
    # onde estava. O segundo numero e o que o redesenho quer subir.
    nxt = o.get("next") or {}
    if nxt.get("lines"):
        print("   next: %d peça(s) -> %d com caminhada (%s) | %d ao alcance (%s)   <-- o par do redesenho"
              % (nxt["lines"], nxt["walk"], pct(nxt["walk"], nxt["lines"]),
                 nxt["stay"], pct(nxt["stay"], nxt["lines"])), file=out)
        # 11/11/2026: o `next:` sai so na PEGADA principal (`MHR_Build.cs:2178`); a peca servida pelo
        # retry do canteiro (:2211), pela barreira (:2435), a retomada (:4751), o retry pass (:4839) e o
        # apoio (:6033) chamam o `EnterStep` SEM logar - por isso as linhas do `next:` sao MENOS que as
        # pecas colocadas, e a porcentagem NAO e a mesma do `no alcance / peças`.
        if o["placed"] and nxt["lines"] != o["placed"]:
            print("        (%d linha(s) de `next:` para %d peça(s) colocada(s): o `next:` só sai na pegada "
                  "principal - retry/fila/resume não logam; não compare esta %% com a de `no alcance`)"
                  % (nxt["lines"], o["placed"]), file=out)
    else:
        print("   next: - (nenhuma linha `next:` nesta ordem)", file=out)
    print("", file=out)
    print(" RESUMO: %s | %s peças | done %d | puladas %d%s | mortes %d | [Build] %d | "
          "linhas/peça %s | espera %s s (fila %s: fim %s/meio %s, portão %s s) | retenções %d | "
          "step-aside %d | caminhada %s | %s"
          % (o["label"], o["plan"], o["placed"], o["skipped"],
             (" (" + o["skipped_source"] + ")") if o["skipped_source"] != "none" else "",
             o["deaths"],
             o["build_lines"],
             f2(o["lines_per_piece"]) if o["lines_per_piece"] is not None else "-",
             f1(o["wait"]["total"]["total"]), f1(o["wait"]["total"]["queue"]),
             f1(o["wait"]["total"]["queue_end"]), f1(o["wait"]["total"]["queue_ahead"]),
             f1(o["wait"]["total"]["gate_seconds"]), o["wait"]["total"]["holds"],
             o["spread"]["aside_asked"],
             (f1(o["wait"]["total"]["walk"]) + " m") if o["wait"]["total"]["walk_lines"] else "-",
             status_text(o)), file=out)
    # 11/11/2026: a linha de baixo e o redesenho inteiro em uma frase (o que o A/B compara)
    red_total = (o.get("redesign") or {}).get("total") or {}
    if red_total.get("reached") is not None:
        print(" REDESENHO: no alcance %d/%s (%s) | caminhada %s m/peça | repeleu %s | corpo-curto %s s | "
              "balanço %s s | next %s ao alcance/%s"
              % (red_total["reached"],
                 ("%d" % red_total["pieces"]) if red_total.get("pieces") else "-",
                 ("%d%%" % round(red_total.get("reached_pct") or 0)),
                 ("%s" % f2(red_total["walk_per_piece"]))
                 if red_total.get("walk_per_piece") is not None else "-",
                 red_total.get("repelled") if red_total.get("repelled") is not None else "-",
                 f1(red_total["wait_short"]) if red_total.get("wait_short") is not None else "-",
                 f1((o.get("redesign") or {}).get("balance", {}).get("total_s") or 0)
                 if (o.get("redesign") or {}).get("balance", {}).get("total_s") is not None else "-",
                 nxt.get("stay") if nxt.get("lines") else "-",
                 nxt.get("lines") if nxt.get("lines") else "-"), file=out)


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Relatorio de run de obra (build) a partir do LogOutput.log.")
    parser.add_argument("log", nargs="?", default=os.environ.get("DEVRIDGE_LOG") or DEFAULT_LOG,
                        help="caminho do LogOutput.log (padrao: %s)" % DEFAULT_LOG)
    scope = parser.add_mutually_exclusive_group()
    scope.add_argument("--last", action="store_true", help="so a ultima ordem (padrao)")
    scope.add_argument("--all", action="store_true", help="todas as ordens do log")
    parser.add_argument("--json", action="store_true", help="imprime so o JSON (schema 1)")
    parser.add_argument("--barrier-seconds", type=float, default=10.0,
                        help="teto de UMA retencao da barreira (padrao 10 = BodyBarrierSeconds)")
    # 11/11/2026: a duracao da obra nao esta no log (sem timestamp) - quem mediu o relogio de fora (o caso
    # do run do §17.22) informa aqui e o BALANCO sai em % do tempo da ordem
    parser.add_argument("--order-seconds", type=float, default=None,
                        help="duracao da ordem em segundos, medida FORA do log (relogio); so alimenta o "
                             "%% do BALANCO da secao REDESENHO (P0)")
    args = parser.parse_args(argv)

    if not os.path.isfile(args.log):
        print("arquivo nao encontrado: %s" % args.log, file=sys.stderr)
        return 2

    try:
        blocks, total_lines, outside = scan(args.log)
    except OSError as exc:
        print("nao foi possivel ler %s: %s" % (args.log, exc), file=sys.stderr)
        return 2
    all_reports = [finalize(b, i + 1, args.barrier_seconds, args.order_seconds)
                   for i, b in enumerate(blocks)]
    reports = all_reports if args.all else all_reports[-1:]

    if args.json:
        payload = {
            "schema": 1,
            "tool": "tools/build_run_report.py",
            "log": os.path.abspath(args.log),
            "log_lines": total_lines,
            "scope": "all" if args.all else "last",
            "order_count": len(all_reports),
            "orders": reports,
        }
        print(json.dumps(payload, ensure_ascii=False, indent=2))
        return 0 if reports else 1

    out = sys.stdout
    try:
        out.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

    print("=" * 74, file=out)
    print(" Relatório de run de obra (build) - tools/build_run_report.py", file=out)
    print("=" * 74, file=out)
    print("log:      %s" % os.path.abspath(args.log), file=out)
    print("escopo:   %s" % ("todas as ordens (--all)" if args.all else "última ordem (--last)"), file=out)
    print("linhas:   %d linha(s) no arquivo | %d linha(s) [Build] fora de ordem" % (total_lines, outside),
          file=out)
    print("ordens:   %d no log | %s"
          % (len(all_reports), "mostrando todas" if args.all else "mostrando a última"), file=out)
    print("", file=out)

    if not reports:
        print(" NENHUMA ORDEM DE OBRA (blueprint) ENCONTRADA NO LOG.", file=out)
        print(" O relatório procura `blueprint <label>: N pieces near (...)` ou", file=out)
        print(' `grupo de obra "<label>": K companheiro(s) no mesmo plano (N peças)`.', file=out)
        print(" Se a ordem rodou nesta sessão, confira [Advanced] LogLevel (precisa ser Normal).", file=out)
        return 1

    if len(reports) > 1:
        print(" ORDENS NO LOG", file=out)
        print("   %-3s %-26s %7s %6s %8s %7s %10s  %s"
              % ("#", "label", "plano", "done", "puladas", "mortes", "l/peça", "status"), file=out)
        for o in reports:
            print("   %-3d %-26s %7s %6d %8d %7d %10s  %s"
                  % (o["index"], o["label"], o["plan"], o["placed"], o["skipped"],
                     o["deaths"],
                     f2(o["lines_per_piece"]) if o["lines_per_piece"] is not None else "-",
                     status_text(o)), file=out)
        print("", file=out)

    for o in reports:
        print_order(o, len(all_reports), out)

    return 0


if __name__ == "__main__":
    sys.exit(main())
