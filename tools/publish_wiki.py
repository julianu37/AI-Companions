#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Publica a fonte de docs/wiki/ no repositorio da GitHub Wiki.

Uso:
    python tools/publish_wiki.py --check
    python tools/publish_wiki.py --dry-run
    python tools/publish_wiki.py [--message "..."] [--branch master]

POR QUE ESTE SCRIPT EXISTE
-------------------------
A wiki desta projeto vive num repositorio git SEPARADO
(`https://github.com/<owner>/<repo>.wiki.git`, branch `master`). A fonte
versionada e `docs/wiki/` neste repositorio: editar la e publicar aqui.

A ARMADILHA (a razao de o `--check` existir)
-------------------------------------------
O GitHub **nao cria** o repositorio `.wiki` por API nem por `git push`: ele
so nasce quando alguem salva a PRIMEIRA pagina pela interface web (botao
"Create the first page"). Antes disso:
  - `git clone ...wiki.git`   -> "Repository not found"
  - `gh repo create x.wiki`   -> "Name cannot end in .wiki"
Por isso o `--check` falha com a instrucao exata do passo manual em vez de
um erro de git sem contexto. Depois da primeira pagina salva, este script
faz o resto (copiar, commitar, empurrar) em qualquer maquina.

O QUE ELE COPIA
---------------
Todo `docs/wiki/*.md` e a pasta `docs/wiki/images/` (referenciada pelos .md
como `images/<arquivo>`). Nada mais: a wiki nao tem layout proprio.
"""
import argparse
import glob
import os
import shutil
import subprocess
import sys
import tempfile

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "docs", "wiki")
OWNER_REPO = "julianu37/AI-Companions"
WIKI_URL = "https://github.com/%s.wiki.git" % OWNER_REPO


def run(args, cwd=None, check=True):
    r = subprocess.run(args, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and r.returncode != 0:
        raise SystemExit("falhou: %s\n%s" % (" ".join(args), (r.stderr or r.stdout).strip()))
    return r


def wiki_exists():
    r = run(["git", "ls-remote", WIKI_URL], check=False)
    return r.returncode == 0


def first_page_instructions():
    return (
        "\nO repositorio da wiki ainda NAO existe: %s\n"
        "\n"
        "O GitHub so o cria quando a primeira pagina e salva pela interface web\n"
        "(nao ha API nem `git push` que faca isso, e `gh repo create *.wiki` e recusado\n"
        "com \"Name cannot end in .wiki\").\n"
        "\n"
        "PASSO MANUAL (uma vez, ~30 segundos):\n"
        "  1. abra https://github.com/%s/wiki\n"
        "  2. clique em \"Create the first page\"\n"
        "  3. titulo: Home   |   corpo: qualquer coisa (vai ser sobrescrito)\n"
        "  4. \"Save Page\"\n"
        "  5. rode este script de novo (sem --check) para publicar o conteudo real\n"
        "\n"
        "Depois disso o script funciona sozinho, nesta e em qualquer outra maquina.\n"
    ) % (WIKI_URL, OWNER_REPO)


def check_source():
    """Valida a fonte antes de publicar: .md existem, imagens referenciadas existem."""
    problems = []

    md = sorted(glob.glob(os.path.join(SRC, "*.md")))
    if not md:
        problems.append("nenhum .md em docs/wiki/")
    for name in ("Home.md", "_Sidebar.md", "_Footer.md"):
        if not os.path.exists(os.path.join(SRC, name)):
            problems.append("falta %s (a Home e o _Sidebar sao obrigatorios na wiki)" % name)

    images = os.path.join(SRC, "images")
    nimg = len(glob.glob(os.path.join(images, "*"))) if os.path.isdir(images) else 0
    if nimg == 0:
        problems.append("docs/wiki/images/ vazia ou ausente")

    # toda imagem local referenciada tem de existir (senao a wiki mostra link quebrado)
    import re
    missing = set()
    for path in md:
        text = open(path, encoding="utf-8").read()
        for m in re.finditer(r"!\[[^\]]*\]\(([^)]+)\)", text):
            target = m.group(1).strip()
            if target.startswith(("http://", "https://", "//")):
                continue
            full = os.path.normpath(os.path.join(SRC, target))
            if not os.path.exists(full):
                missing.add(os.path.relpath(full, ROOT))
    if missing:
        problems.append("imagens referenciadas que nao existem: " + ", ".join(sorted(missing)))

    return md, nimg, problems


def land_of(rel):
    """Estados por idioma presentes na fonte - so para o resumo."""
    langs = {"pt": 0, "es": 0, "fr": 0, "it": 0, "en": 0}
    for name in rel:
        base = os.path.basename(name)
        hit = False
        for code in ("pt", "es", "fr", "it"):
            if base.endswith("-%s.md" % code):
                langs[code] += 1
                hit = True
                break
        if not hit and not base.startswith("_"):
            langs["en"] += 1
    return langs


def main():
    ap = argparse.ArgumentParser(description="Publica docs/wiki/ no repositorio da GitHub Wiki.")
    ap.add_argument("--check", action="store_true", help="so valida a fonte e se a wiki ja existe; nao publica")
    ap.add_argument("--dry-run", action="store_true", help="mostra o que seria copiado, sem commitar")
    ap.add_argument("--message", default="", help="mensagem do commit na wiki")
    ap.add_argument("--branch", default="master", help="branch do repo da wiki (padrao: master)")
    args = ap.parse_args()

    md, nimg, problems = check_source()
    langs = land_of(md)
    print("fonte: docs/wiki/")
    print("   paginas: %d  (en=%d pt=%d es=%d fr=%d it=%d)" % (
        len(md), langs["en"], langs["pt"], langs["es"], langs["fr"], langs["it"]))
    print("   imagens: %d" % nimg)
    for p in problems:
        print("   PROBLEMA: %s" % p)

    exists = wiki_exists()
    print("   repositorio da wiki (%s): %s" % (WIKI_URL, "existe" if exists else "NAO EXISTE"))

    if problems:
        print("\nFAIL - a fonte tem problema; corrija antes de publicar.")
        return 1

    if args.check:
        if not exists:
            print(first_page_instructions())
            return 1
        print("\nOK - fonte valida e wiki acessivel. Rode sem --check para publicar.")
        return 0

    if not exists:
        print(first_page_instructions())
        return 1

    tmp = tempfile.mkdtemp(prefix="opencode-wiki-")
    clone = os.path.join(tmp, "wiki")
    try:
        print("\nclonando %s (branch %s)..." % (WIKI_URL, args.branch))
        run(["git", "clone", "--branch", args.branch, WIKI_URL, clone])

        # copia as paginas, preservando o .git
        for path in md:
            shutil.copy2(path, os.path.join(clone, os.path.basename(path)))
        src_images = os.path.join(SRC, "images")
        dst_images = os.path.join(clone, "images")
        if os.path.isdir(src_images):
            os.makedirs(dst_images, exist_ok=True)
            for path in glob.glob(os.path.join(src_images, "*")):
                if os.path.isfile(path):
                    shutil.copy2(path, os.path.join(dst_images, os.path.basename(path)))

        if args.dry_run:
            print("\n--dry-run: %d paginas + %d imagens seriam publicadas em %s" % (len(md), nimg, clone))
            print("   (nada foi commitado)")
            return 0

        run(["git", "add", "-A"], cwd=clone)
        st = run(["git", "status", "--porcelain"], cwd=clone)
        if not st.stdout.strip():
            print("\nsem mudancas - a wiki ja esta igual a fonte (nada a publicar)")
            return 0

        msg = args.message or ("docs(wiki): publica a fonte de docs/wiki (%d paginas, %d imagens)" % (len(md), nimg))
        run(["git", "-c", "user.name=julianu37",
             "-c", "user.email=julianu37@users.noreply.github.com",
             "commit", "-m", msg], cwd=clone)
        run(["git", "push", "origin", args.branch], cwd=clone)
        print("\nOK - wiki publicada: https://github.com/%s/wiki" % OWNER_REPO)
        return 0
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
