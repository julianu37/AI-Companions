#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Relatorio de run de obra (build) a partir do BepInEx\\LogOutput.log.

Uso:
    python tools/build_run_report.py [log] [--last|--all] [--json] [--barrier-seconds 10]

    log      caminho do LogOutput.log (padrao: DEVRIDGE_LOG, senao o do Valheim desta maquina)
    --last   so a ultima ordem de obra do log (padrao)
    --all    todas as ordens de obra encontradas
    --json   imprime SO o JSON (schema 1), para comparar dois runs (A/B) sem `grep` linha a linha
    --barrier-seconds  teto de UMA retencao da barreira de corpo (padrao 10 = BodyBarrierSeconds)

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

Schema JSON (v1): log, log_lines, scope, order_count, orders[] com index/label/status/start/
window/end/ends/plan/plan_blueprint/workers/group_workers/lines/build_lines/lines_per_piece/duration_s/
duration_note/placed/skipped_counter/skipped_named_lines/skipped_named_pieces/watchdog_calls/
watchdog_dropped/retries{...}/source_totals/body_on_piece/emergency_posts/emergency_refused{...}/deaths/
unsupported/wait{lines,unparsed,workers[],total{...},holds_cap_s}/spread{...}/aside_lines{...}.

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
WAIT = re.compile(
    r"^dispersão: (?P<skipped>\d+) adiada\(s\), (?P<resumed>\d+) retomada\(s\) \u00b7 "
    r"step-aside: (?P<asked>\d+) pedido\(s\) \u00b7 espera: (?P<total>-?[\d.,]+) s "
    r"\(barreira (?P<barrier>-?[\d.,]+) s em (?P<holds>\d+) retenção\(ões\) "
    r"(?P<body>-?[\d.,]+) corpo/(?P<dep>-?[\d.,]+) dependência, "
    r"fila (?P<queue>-?[\d.,]+) s(?: \(fim (?P<queue_end>-?[\d.,]+) s, meio (?P<queue_ahead>-?[\d.,]+) s\))?, "
    r"passo (?P<aside>-?[\d.,]+) s, portão (?P<gate>\d+)(?: \((?P<gate_seconds>-?[\d.,]+) s\))?\)")
# detalhe da dispersao (so com LogDetailed) :1189
SPREAD_DETAIL = re.compile(r"^dispersão: a peça (?P<i>\d+)/(?P<n>\d+) fica para depois - pegando ")
# a linha do Finish com `espera:` que o WAIT acima nao entendeu (o formato mudou?): nao perde em
# silencio - vira aviso na saida
WAIT_UNPARSED = re.compile(r"^dispersão: .*espera: ")
# step-aside :6485/:6458/:6565
ASIDE_ASK = re.compile(r'^".+" está na peça \d+ - pedindo para sair da frente')
ASIDE_NO_ROOM = re.compile(r'^".+" está na peça \d+ - sem lugar livre para ele sair da frente')
ASIDE_DONE = re.compile(r"^saindo da frente \(peça em ")


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


def finalize(block, index, barrier_seconds):
    end = block.ends[-1] if block.ends else None
    end_line = end[0] if end else block.window_end
    plan = block.group_plan if block.group_plan is not None else block.plan_blueprint
    first_line = block.first_line
    start_line = block.group_line or block.blueprint_line or first_line
    start_text = block.group_text or block.blueprint_text

    lines = max(0, end_line - first_line + 1)
    build_lines = end[4] if end else block.build_count
    events = [e for e in block.events if first_line <= e[0] <= end_line]

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
                }
            row["total"] += num(data["total"])
            row["barrier"] += num(data["barrier"])
            row["body"] += num(data["body"])
            row["dependency"] += num(data["dep"])
            row["queue"] += num(data["queue"])
            row["aside"] += num(data["aside"])
            row["holds"] += int(data["holds"])
            row["gate"] += int(data["gate"])
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
    }

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
        print("   %-20s %8s %9s %7s %9s %6s %7s %10s %7s"
              % ("worker", "espera", "barreira", "corpo", "depend.", "fila", "passo",
                 "retenções", "portão"), file=out)
        for row in o["wait"]["workers"]:
            print("   %-20s %8s %9s %7s %9s %6s %7s %10d %7d"
                  % (row["worker"], f1(row["total"]), f1(row["barrier"]), f1(row["body"]),
                     f1(row["dependency"]), f1(row["queue"]), f1(row["aside"]),
                     row["holds"], row["gate"]), file=out)
        t = o["wait"]["total"]
        print("   %-20s %8s %9s %7s %9s %6s %7s %10d %7d"
              % ("TOTAL", f1(t["total"]), f1(t["barrier"]), f1(t["body"]), f1(t["dependency"]),
                 f1(t["queue"]), f1(t["aside"]), t["holds"], t["gate"]), file=out)
        print("   retenções: %d no total -> teto %s s cada = %s s (TETO da barreira, não tempo "
              "medido;" % (t["holds"], f1(o["wait"]["barrier_seconds"]),
                           f1(o["wait"]["holds_cap_s"])), file=out)
        print("   o tempo medido é a coluna \"espera\", somada por frame pelo próprio mod)", file=out)
        # 11/11/2026 (Run 19): the queue split and the gate's own seconds (fields of the run 19+ line)
        print("   fila por metade: fim de plano %s s | meio de plano %s s | portão %s s em %d retenção(ões)"
              % (f1(t["queue_end"]), f1(t["queue_ahead"]), f1(t["gate_seconds"]), t["gate"]), file=out)
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
    print("", file=out)
    print(" RESUMO: %s | %s peças | done %d | puladas %d | mortes %d | [Build] %d | "
          "linhas/peça %s | espera %s s (fila %s: fim %s/meio %s, portão %s s) | retenções %d | "
          "step-aside %d | %s"
          % (o["label"], o["plan"], o["placed"], o["skipped_counter"], o["deaths"],
             o["build_lines"],
             f2(o["lines_per_piece"]) if o["lines_per_piece"] is not None else "-",
             f1(o["wait"]["total"]["total"]), f1(o["wait"]["total"]["queue"]),
             f1(o["wait"]["total"]["queue_end"]), f1(o["wait"]["total"]["queue_ahead"]),
             f1(o["wait"]["total"]["gate_seconds"]), o["wait"]["total"]["holds"],
             o["spread"]["aside_asked"], status_text(o)), file=out)


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
    args = parser.parse_args(argv)

    if not os.path.isfile(args.log):
        print("arquivo nao encontrado: %s" % args.log, file=sys.stderr)
        return 2

    try:
        blocks, total_lines, outside = scan(args.log)
    except OSError as exc:
        print("nao foi possivel ler %s: %s" % (args.log, exc), file=sys.stderr)
        return 2
    all_reports = [finalize(b, i + 1, args.barrier_seconds)
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
                  % (o["index"], o["label"], o["plan"], o["placed"], o["skipped_counter"],
                     o["deaths"],
                     f2(o["lines_per_piece"]) if o["lines_per_piece"] is not None else "-",
                     status_text(o)), file=out)
        print("", file=out)

    for o in reports:
        print_order(o, len(all_reports), out)

    return 0


if __name__ == "__main__":
    sys.exit(main())
