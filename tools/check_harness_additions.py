# -*- coding: utf-8 -*-
"""Trava de asserções do harness: o harness so pode GANHAR asserção (AGENTS.md, regra 4).

Por que existe: a regra 4 era só texto. Medido no histórico: 96f6c25 removeu 15 linhas de asserção e
adicionou 7; 35128fc removeu 2 e adicionou 3; 45ac792 trocou o valor esperado de 2 asserções. O gate
só via "0 FAIL", porque a asserção que sumia já não estava no código.

O que faz: compara `tools/ai-intents-check/*.cs` com uma base git e FALHA (exit 1) se alguma chamada
`Check(` (ou `KnownFail(`) foi REMOVIDA ou ALTERADA. Alterar conta como remover: a linha velha sai e a nova
entra, e a asserção velha deixa de existir. Adição pura passa (exit 0).

Uso:
    python tools/check_harness_additions.py                       # base HEAD: working tree + staged
    python tools/check_harness_additions.py --base HEAD~1         # revisar um commit já feito (árvore limpa)
    python tools/check_harness_additions.py --base 96f6c25^1 --to 96f6c25   # um commit exato (diff base..to)
    python tools/check_harness_additions.py --base HEAD --allow-removal "motivo"   # exceção explícita
    python tools/check_harness_additions.py --selftest            # casos sintéticos, sem git

--allow-removal "<motivo>": imprime o motivo e sai com 0 mesmo havendo remoção, para que a exceção fique
visível no log. Use só quando a regra 4 mudou DE PROPÓSITO; acrescente a asserção nova ao lado da antiga.

Limites conhecidos:
- Reindentar uma chamada Check( (mesmo texto, outro recuo) conta como alterada: remoção + adição.
- Chamadas multi-linha são inteiras: tirar qualquer linha delas é remover a asserção.
- Strings regulares (") não atravessam linha; strings verbatim @"..." multi-linha não são tratadas.

Exit: 0 = ok (ou exceção dada), 1 = asserção removida/alterada, 2 = erro (git, base inválida, motivo vazio).
"""
import argparse
import difflib
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HARNESS_DIR = "tools/ai-intents-check"
CALL_NAMES = ("Check", "KnownFail")
HUNK = re.compile(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")


def scan_calls(text):
    """Acha cada chamada Check( / KnownFail( em código (fora de string e de comentário).

    Devolve [(linha_inicial, linha_final)], 1-based e inclusivas. Chamada que ocupa varias linhas
    (`Check("...",` + condição na linha seguinte) conta inteira.
    """
    calls = []
    state = "code"  # code | str | chr | line_comment | block_comment
    line = 1
    depth = 0  # profundidade de parênteses dentro de uma chamada
    start = None
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        if c == "\n":
            line += 1
            if state in ("line_comment", "str", "chr"):
                state = "code"
            i += 1
            continue
        if state == "code":
            if text.startswith("//", i):
                state = "line_comment"
                i += 2
            elif text.startswith("/*", i):
                state = "block_comment"
                i += 2
            elif c == '"':
                state = "str"
                i += 1
            elif c == "'":
                state = "chr"
                i += 1
            elif c == "_" or c.isalpha():
                j = i
                while j < n and (text[j] == "_" or text[j].isalnum()):
                    j += 1
                word = text[i:j]
                i = j
                if depth == 0 and word in CALL_NAMES:
                    k = j
                    while k < n and text[k] in " \t":
                        k += 1
                    if k < n and text[k] == "(":
                        start = line
                        depth = 1
                        i = k + 1
            elif depth > 0 and c == "(":
                depth += 1
                i += 1
            elif depth > 0 and c == ")":
                depth -= 1
                i += 1
                if depth == 0:
                    calls.append((start, line))
                    start = None
            else:
                i += 1
        elif state in ("str", "chr"):
            quote = '"' if state == "str" else "'"
            if c == "\\":
                if i + 1 < n and text[i + 1] == "\n":
                    line += 1
                i += 2
            else:
                if c == quote:
                    state = "code"
                i += 1
        elif state == "block_comment":
            if text.startswith("*/", i):
                state = "code"
                i += 2
            else:
                i += 1
        else:  # line_comment
            i += 1
    return calls


def _line_index(calls):
    """linha (1-based) -> linha inicial da chamada que a contém."""
    idx = {}
    for s, e in calls:
        for ln in range(s, e + 1):
            idx[ln] = s
    return idx


def analyze(old_text, new_text, removed_old, added_new):
    """Núcleo da trava. removed_old: linhas removidas do antigo; added_new: linhas adicionadas no novo.

    Devolve (quantas chamadas novas foram adicionadas, [(linha_inicial_na_base, texto)] das removidas).
    Uma chamada alterada aparece nas duas contas: a velha sai (removida) e a nova entra (adicionada).
    """
    old_text = old_text.replace("\r\n", "\n")
    new_text = new_text.replace("\r\n", "\n")
    old_lines = old_text.split("\n")
    old_idx = _line_index(scan_calls(old_text))
    new_idx = _line_index(scan_calls(new_text))
    removed = {}
    for ln in sorted(removed_old):
        s = old_idx.get(ln)
        if s is not None and s not in removed:
            removed[s] = old_lines[s - 1].strip() if 0 < s <= len(old_lines) else ""
    added = set()
    for ln in added_new:
        s = new_idx.get(ln)
        if s is not None:
            added.add(s)
    return len(added), sorted(removed.items())


def parse_unified(diff_text):
    """Lê diff unificado (git diff -U0). Devolve (removidas do antigo, adicionadas no novo) como conjuntos."""
    removed, added = set(), set()
    old_no = new_no = 0
    in_hunk = False
    for raw in diff_text.replace("\r\n", "\n").split("\n"):
        m = HUNK.match(raw)
        if m:
            in_hunk = True
            old_no = int(m.group(1))
            new_no = int(m.group(3))
            continue
        if not in_hunk:
            continue
        if raw.startswith("-"):
            removed.add(old_no)
            old_no += 1
        elif raw.startswith("+"):
            added.add(new_no)
            new_no += 1
        elif raw.startswith(" "):
            old_no += 1
            new_no += 1
    return removed, added


def report(added, removed, allow):
    """Veredito. removed = [(arquivo, linha_na_base, texto)]. Devolve (exit, linhas)."""
    lines = []
    n_rem = len(removed)
    if allow is not None:
        lines.append(f"[aviso] harness: --allow-removal usado. motivo: {allow}")
    if n_rem == 0:
        lines.append(f"[ok] harness: +{added} asserção(ões), 0 removida(s)")
        return 0, lines
    if allow is not None:
        lines.append(f"[aviso] harness: -{n_rem} asserção(ões) removida(s), PERMITIDA(s) pela exceção acima "
                     f"(+{added} adicionada(s))")
        for f, ln, txt in removed:
            lines.append(f"    {f}:{ln}  {_short(txt)}")
        return 0, lines
    lines.append(f"[falha] harness: -{n_rem} asserção(ões) removida(s)  (+{added} adicionada(s) no mesmo diff)")
    for f, ln, txt in removed:
        lines.append(f"    {f}:{ln}  {_short(txt)}")
    lines.append("    regra 4 (AGENTS.md): o harness so ganha asserção. Se a mudança foi de propósito, acrescente a")
    lines.append("    asserção nova ao lado da antiga; só use --allow-removal \"motivo\" se for inevitável.")
    return 1, lines


def _short(txt):
    return txt if len(txt) <= 110 else txt[:107] + "..."


def git(*args):
    """Roda git na raiz do repo. Devolve (rc, stdout, stderr); erro de execução vira rc 2."""
    try:
        p = subprocess.run(["git", *args], cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    except OSError as e:
        return 2, "", str(e)
    return p.returncode, p.stdout.decode("utf-8", "replace"), p.stderr.decode("utf-8", "replace")


def read_disk(rel):
    p = os.path.join(ROOT, rel)
    if not os.path.isfile(p):
        return ""
    with open(p, "rb") as fh:
        return fh.read().decode("utf-8", "replace")


def run_check(base, to, allow):
    rev = [base] + ([to] if to else [])
    rc, names, err = git("diff", "--name-only", "--no-renames", *rev, "--", HARNESS_DIR)
    if rc != 0:
        return 2, [f"[erro] harness: git diff falhou ({' '.join(rev)}): {err.strip() or names.strip()}"]
    removed = []
    added_total = 0
    for rel in names.splitlines():
        rel = rel.strip()
        if not rel.lower().endswith(".cs"):
            continue
        _, old, _ = git("show", f"{base}:{rel}")
        new = git("show", f"{to}:{rel}")[1] if to else read_disk(rel)
        _, diff, _ = git("diff", "-U0", "--no-color", "--no-ext-diff", *rev, "--", rel)
        rem_lines, add_lines = parse_unified(diff)
        n_add, rem_calls = analyze(old, new, rem_lines, add_lines)
        added_total += n_add
        removed.extend((rel, ln, txt) for ln, txt in rem_calls)
    return report(added_total, removed, allow)


def _diff_sets(old_text, new_text):
    """Linhas removidas/adicionadas por difflib (só para o self-test, sem git)."""
    a = old_text.replace("\r\n", "\n").split("\n")
    b = new_text.replace("\r\n", "\n").split("\n")
    removed, added = set(), set()
    sm = difflib.SequenceMatcher(None, a, b, autojunk=False)
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag in ("delete", "replace"):
            removed.update(range(i1 + 1, i2 + 1))
        if tag in ("insert", "replace"):
            added.update(range(j1 + 1, j2 + 1))
    return removed, added


def selftest():
    passed = 0
    failed = 0

    def check(name, ok, detail=""):
        nonlocal passed, failed
        if ok:
            passed += 1
            print(f"[PASS] {name}")
        else:
            failed += 1
            print(f"[FAIL] {name}  {detail}")

    base_hdr = "void T() {\n"
    cases = [
        # (nome, antigo, novo, saída esperada, removidas esperadas, adicionadas esperadas)
        ("adição pura de Check passa",
         base_hdr + '\tCheck("a", x == 2);\n\tCheck("b", y);\n}\n',
         base_hdr + '\tCheck("a", x == 2);\n\tCheck("b", y);\n\tCheck("c", z);\n}\n', 0, 0, 1),
        ("remoção de Check falha",
         base_hdr + '\tCheck("a", x == 2);\n\tCheck("b", y);\n}\n',
         base_hdr + '\tCheck("a", x == 2);\n}\n', 1, 1, 0),
        ("troca de valor esperado falha (sai a velha, entra a nova)",
         base_hdr + '\tCheck("a", x == 2);\n\tCheck("b", y);\n}\n',
         base_hdr + '\tCheck("a", x == 3);\n\tCheck("b", y);\n}\n', 1, 1, 1),
        ("remoção de linha que não é Check passa",
         base_hdr + '\tint k = 0;\n\tCheck("b", y);\n}\n',
         base_hdr + '\tCheck("b", y);\n}\n', 0, 0, 0),
        ("remoção da linha de continuação de Check multi-linha falha",
         base_hdr + '\tCheck("a",\n\t\tx == 2);\n}\n',
         base_hdr + '\tCheck("a",\n}\n', 1, 1, 0),
        ("Check só em comentário removido passa",
         base_hdr + '\t// Check("velho", q);\n\tCheck("b", y);\n}\n',
         base_hdr + '\tCheck("b", y);\n}\n', 0, 0, 0),
        ("Check( dentro de string removido passa",
         base_hdr + '\tLog("use Check(x) aqui");\n\tCheck("b", y);\n}\n',
         base_hdr + '\tCheck("b", y);\n}\n', 0, 0, 0),
        ("parêntese desbalanceado dentro de string não quebra a contagem (remoção falha)",
         base_hdr + '\tCheck("fecha (a porta", f(1));\n\tCheck("b", y);\n}\n',
         base_hdr + '\tCheck("b", y);\n}\n', 1, 1, 0),
        ("KnownFail removido falha",
         base_hdr + '\tKnownFail("x", a, "d");\n\tCheck("b", y);\n}\n',
         base_hdr + '\tCheck("b", y);\n}\n', 1, 1, 0),
    ]
    for name, old, new, want_exit, want_rem, want_add in cases:
        rem, add = _diff_sets(old, new)
        n_add, rem_calls = analyze(old, new, rem, add)
        code, _ = report(n_add, [("x.cs", ln, t) for ln, t in rem_calls], None)
        got = (code, len(rem_calls), n_add)
        want = (want_exit, want_rem, want_add)
        check(name, got == want, f"esperado (saída, rem, add)={want}, obtido={got}")

    # --allow-removal: a remoção existe, mas sai com 0 e o motivo aparece no log.
    old = base_hdr + '\tCheck("a", x == 2);\n}\n'
    new = base_hdr + "}\n"
    rem, add = _diff_sets(old, new)
    n_add, rem_calls = analyze(old, new, rem, add)
    code, lines = report(n_add, [("x.cs", ln, t) for ln, t in rem_calls], "troca de regra de propósito")
    check("--allow-removal passa com aviso e motivo",
          code == 0 and any("troca de regra de propósito" in l for l in lines),
          f"exit={code}, linhas={lines}")

    # parser do diff unificado (o que o git devolve), com duas hunks e uma linha de contexto.
    diff_text = "\n".join([
        "diff --git a/tools/ai-intents-check/Program.cs b/tools/ai-intents-check/Program.cs",
        "index 1111111..2222222 100644",
        "--- a/tools/ai-intents-check/Program.cs",
        "+++ b/tools/ai-intents-check/Program.cs",
        "@@ -3,2 +3,1 @@ void T() {",
        '-\tCheck("a", x == 2);',
        "-\tint k = 0;",
        '+\tCheck("a", x == 3);',
        "@@ -10,1 +9,2 @@",
        " \tkeep();",
        '+\tCheck("n", 1);',
        "",
    ])
    p_rem, p_add = parse_unified(diff_text)
    check("parse_unified lê as linhas removidas e adicionadas (duas hunks)",
          p_rem == {3, 4} and p_add == {3, 10},
          f"removidas={sorted(p_rem)}, adicionadas={sorted(p_add)}")

    print(f"=== {passed} PASS / {failed} FAIL ===")
    return 0 if failed == 0 else 1


def main(argv):
    ap = argparse.ArgumentParser(description="Trava: o harness só ganha asserção (regra 4).")
    ap.add_argument("--base", default="HEAD", help="ref git de comparação (padrão: HEAD, com working tree + staged)")
    ap.add_argument("--to", default=None, help="ref final; sem ele, compara a base com o working tree")
    ap.add_argument("--allow-removal", dest="allow", default=None, metavar="MOTIVO",
                    help="exceção explícita: imprime o motivo e sai com 0 mesmo havendo remoção")
    ap.add_argument("--selftest", action="store_true", help="casos sintéticos, sem git")
    args = ap.parse_args(argv)
    if args.selftest:
        return selftest()
    if args.allow is not None and not args.allow.strip():
        print("[erro] harness: --allow-removal exige um motivo não vazio")
        return 2
    code, lines = run_check(args.base, args.to, args.allow)
    for ln in lines:
        print(ln)
    return code


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.exit(main(sys.argv[1:]))
