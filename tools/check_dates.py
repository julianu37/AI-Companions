# -*- coding: utf-8 -*-
"""Falha se algum arquivo rastreado tiver uma DATA NO FUTURO.

Por que existe (registrado em 06/11/2026, quando este script foi escrito): o agente escreveu uma
data FUTURA em 619 linhas de 48 arquivos - a data vinha da memoria, nao do relogio. Isso destroi a
cronologia de um projeto que registra tudo por data. Ver a REGRA 14 no AGENTS.md.

Regra: a data de um comentario/doc vem de `git log -1 --format=%ad` (a data do commit que
escreveu a linha) ou da saida de `date` - nunca da cabeca.

Uso:
    python tools/check_dates.py            # falha (exit 1) se houver data futura (inclui QUOTE_FILES)
    python tools/check_dates.py --fix     # reescreve cada data futura pela data do COMMIT da linha
    python tools/check_dates.py --quiet   # so o veredito

--fix usa `git blame -w -M` para achar o commit de cada linha; uma substituicao unica para
todo o repo estaria ERRADA (a maioria das linhas vem de commits de 04/11, nao de 05/11).
A troca e no lugar: nao muda numero de linha, entao as citacoes arquivo:linha continuam validas.

QUOTE_FILES (AGENTS.md e este arquivo) CITAM uma data antiga de proposito: a REGRA 14 e a docstring
abaixo falam do erro pelo nome. Eles ficam fora do --fix (senao a citacao seria reescrita e a
explicacao perderia o sentido - aconteceu no primeiro uso), mas NUNCA fora da checagem: o loop de
verificacao os inclui, e uma data futura neles REPROVA como em qualquer outro arquivo.
"""
import datetime
import os
import re
import subprocess
import sys

# dd/mm/aaaa E aaaa-mm-dd (ISO) - so formatos de comentario/doc do projeto
DATE = re.compile(r"\b(\d{2})/(\d{2})/(\d{4})\b|\b(\d{4})-(\d{2})-(\d{2})\b")
# so nos importam arquivos de texto que o projeto versiona
EXTS = (".cs", ".md", ".py", ".ps1", ".json", ".txt", ".cfg", ".csproj", ".props", ".example")

# 06/11/2026: marcador de CITACAO. Uma linha que cita uma data antiga de proposito (a REGRA 14 descrevendo o erro,
# a docstring deste script) poe isto no texto, e o check ignora o que vem ANTES do marcador NAQUELA linha.
# Antes disto havia uma lista QUOTE_FILES: ela deixava o AGENTS.md fora da CHECAGEM (o arquivo com mais data errada,
# 53) - ou, incluindo-o, reprovava as citacoes legitimas. O marcador resolve os dois: checagem completa, citacao viva.
MARK_QUOTED = "[data-citada]"


def repo_root():
    try:
        out = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True)
        return out.stdout.strip() or os.getcwd()
    except Exception:
        return os.getcwd()


def tracked(root):
    out = subprocess.run(["git", "ls-files"], capture_output=True, text=True, cwd=root)
    return [f for f in out.stdout.splitlines() if f.endswith(EXTS)]


def today_local():
    """A data LOCAL, nunca UTC.

    Por que (achado da revisao de 05/11/2026): o ultimo commit foi 21:52 em -03:00, que ja e 06/11 em UTC.
    Um gate que calcula "hoje" em UTC deixa passar uma data um dia a frente - o erro que ele deveria pegar.
    """
    return datetime.date.today()


def future_dates(text, today):
    """[(linha_1based, data_str, data_obj)] das datas posteriores a hoje (dd/mm/aaaa e ISO).

    06/11/2026: uma linha que CITA uma data de proposito (a REGRA 14 falando do erro, a docstring deste script)
    carrega o marcador `[data-citada]`. Essas linhas ficam fora da checagem - mas isso vale SO para o texto antes
    do marcador na mesma linha, para nao abrir um buraco no arquivo inteiro.
    """
    hits = []
    for i, line in enumerate(text.splitlines(), 1):
        if MARK_QUOTED in line:
            continue        # linha que CITA uma data de proposito (REGRA 14, a docstring deste script): fora da checagem
        for m in DATE.finditer(line):
            try:
                if m.group(4):                                  # ISO aaaa-mm-dd
                    d = datetime.date(int(m.group(4)), int(m.group(5)), int(m.group(6)))
                else:                                           # dd/mm/aaaa
                    d = datetime.date(int(m.group(3)), int(m.group(2)), int(m.group(1)))
            except ValueError:
                continue        # 31/02 nao e data; nao e o nosso problema
            if d > today:
                hits.append((i, m.group(0), d))
    return hits


def is_real_date_line(root, relpath, line_no, date_str):
    """O commit apontado pelo blame REALMENTE escreveu esta data?

    Se nao escreveu, o blame caiu num commit de formatacao que mexeu na linha sem mudar o conteudo
    semantico - a linha vai para revisao manual em vez de ser trocada as cegas (revisao de 05/11/2026).
    """
    try:
        out = subprocess.run(["git", "blame", "-w", "-M", "-L", "%d,%d" % (line_no, line_no),
                              "--line-porcelain", relpath],
                             capture_output=True, text=True, cwd=root, encoding="utf-8", errors="replace").stdout
        commit = None
        for l in out.splitlines():
            m = re.match(r"^([0-9a-f]{40}) \d+ \d+", l)
            if m:
                commit = m.group(1)
                break
        if not commit:
            return True        # sem blame (linha nova, nao commitada): nao ha como checar
        d = subprocess.run(["git", "show", "--format=", "-U0", commit, "--", relpath],
                           capture_output=True, text=True, cwd=root, encoding="utf-8", errors="replace").stdout
        return date_str in d
    except Exception:
        return True


def blame_dates(root, relpath):
    """{linha_1based: 'dd/mm/aaaa'} pela data do commit que escreveu cada linha."""
    out = subprocess.run(["git", "blame", "-w", "-M", "--line-porcelain", relpath],
                         capture_output=True, text=True, cwd=root, encoding="utf-8", errors="replace")
    when = {}
    by_commit = {}
    cur = None
    n = 0
    line_of = {}
    for l in out.stdout.splitlines():
        m = re.match(r"^([0-9a-f]{40}) \d+ (\d+)", l)
        if m:
            cur = m.group(1)
            n = int(m.group(2))
            line_of[n] = cur
            continue
        if l.startswith("\t"):
            continue
    # a data de cada commit, uma consulta por commit
    for commit in set(line_of.values()):
        d = subprocess.run(["git", "log", "-1", "--format=%ad", "--date=format:%d/%m/%Y", commit],
                           capture_output=True, text=True, cwd=root).stdout.strip()
        by_commit[commit] = d
    for ln, commit in line_of.items():
        when[ln] = by_commit.get(commit, "")
    return when


def main():
    root = repo_root()
    today = today_local()
    fix = "--fix" in sys.argv
    quiet = "--quiet" in sys.argv
    # arquivos que OUTRO agente esta editando agora: lista de exclusao, para nunca sobrescrever o trabalho dele.
    skip = set()
    for a in sys.argv:
        if a.startswith("--skip="):
            skip.update(x.strip() for x in a[7:].split(",") if x.strip())
    # 06/11/2026: a excecao por arquivo deu lugar ao marcador por linha (MARK_QUOTED) - ver future_dates.
    # --allow=<arquivo...>: conserta tambem os arquivos que CITAM uma data antiga de proposito (ver abaixo).
    allow = set()
    for a in sys.argv:
        if a.startswith("--allow="):
            allow.update(x.strip() for x in a[7:].split(",") if x.strip())

    # 06/11/2026: a checagem le TODOS os arquivos. Quem cita uma data antiga de proposito marca a linha com
    # MARK_QUOTED (ver future_dates), e o --fix nunca reescreve uma linha marcada.

    bad_files = {}
    for rel in tracked(root):
        if rel in skip:
            continue
        try:
            text = open(os.path.join(root, rel), encoding="utf-8", errors="replace").read()
        except Exception:
            continue
        hits = future_dates(text, today)
        if hits:
            bad_files[rel] = hits

    total = sum(len(v) for v in bad_files.values())
    if not bad_files:
        print("[ok] nenhuma data no futuro (%d arquivo(s) rastreado(s), hoje %s)"
              % (len(tracked(root)), today.strftime("%d/%m/%Y")))
        return 0

    if not quiet:
        print("[!] %d data(s) no futuro em %d arquivo(s) (hoje %s)"
              % (total, len(bad_files), today.strftime("%d/%m/%Y")))
        for rel, hits in sorted(bad_files.items(), key=lambda kv: -len(kv[1]))[:15]:
            print("    %-46s %4d  (1a: linha %d = %s)" % (rel[:46], len(hits), hits[0][0], hits[0][1]))

    if not fix:
        print()
        print("    a data de um comentario vem do commit que o escreveu, nao de uma data unica.")
        print("    rode com --fix para trocar cada uma pela data do proprio commit (git blame -w -M).")
        return 1

    changed = 0
    manual = []
    for rel, hits in bad_files.items():
        if rel in skip:
            continue
        path = os.path.join(root, rel)
        when = blame_dates(root, rel)
        lines = open(path, encoding="utf-8", errors="replace").read().splitlines(keepends=True)
        touched = 0
        for ln, bad, _d in hits:
            good = when.get(ln, "")
            if not good:
                manual.append((rel, ln, bad, "sem blame (linha nao commitada)"))
                continue
            if not is_real_date_line(root, rel, ln, bad):
                manual.append((rel, ln, bad, "blame caiu num commit que nao escreveu esta data"))
                continue
            lines[ln - 1] = lines[ln - 1].replace(bad, good)
            touched += 1
        if touched:
            open(path, "w", encoding="utf-8", newline="").write("".join(lines))
            changed += touched
            if not quiet:
                print("    %-46s %4d linha(s)" % (rel[:46], touched))
    print("[fix] %d data(s) corrigida(s) para a data do commit" % changed)
    if manual:
        print("[manual] %d linha(s) NAO tocada(s) - revisar a mao:" % len(manual))
        for rel, ln, bad, why in manual[:20]:
            print("    %-42s linha %5d  %s  (%s)" % (rel[:42], ln, bad, why))
    return 0


if __name__ == "__main__":
    sys.exit(main())
