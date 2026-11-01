#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Monta o staging do pacote de release do AI Companions por ALLOW-LIST.

O staging nunca e "copiar a pasta do plugin": a lista e fechada, e o que nao esta nela nao
entra no pacote. Foi assim que `speech.json`, `Translations\\English.json` (gerados em runtime)
e o marcador `__folder_managed_by_vortex` chegaram ao zip (o do 1.9.1 tem o English.json:
399 arquivos contra os 398 do 1.9.2/1.9.3).

Uso:
    python tools/stage_release.py --version 1.9.4 ^
        --from "<Valheim>\\BepInEx\\plugins\\AI-Companions" ^
        --to "pkg/1.9.4/stage"
    python tools/stage_release.py --version 1.9.4 --from ... --to "pkg/1.9.4/stage-sem-modelos" --no-models

Layout produzido (igual ao do pacote 1.9.1 do Nexus, com os MODELOS no lugar certo):

    <to>\\CHANGELOG.md, CONFIGURATION.md, GUIDE.md, icon.png, LEIA-ME.txt, README.md   <- package/
    <to>\\BepInEx\\plugins\\AI-Companions\\Companions.dll + 8 DLLs de dependencia
    <to>\\BepInEx\\plugins\\AI-Companions\\runtimes\\win-x64\\*.dll             (nativos do Whisper.net)
    <to>\\BepInEx\\plugins\\AI-Companions\\runtimes\\win-x64\\native\\*.dll     (nativos do sherpa-onnx)
    <to>\\BepInEx\\plugins\\AI-Companions\\LICENSES\\
    <to>\\BepInEx\\plugins\\AI-Companions\\Translations\\<4 idiomas>.json
    <to>\\BepInEx\\plugins\\AI-Companions\\Speech\\<4 idiomas>.json
    <to>\\BepInEx\\plugins\\AI-Companions\\tts\\kokoro\\LICENSE, tokens.txt, espeak-ng-data\\**   (sempre)
    <to>\\BepInEx\\plugins\\AI-Companions\\tts\\kokoro\\model.int8.onnx, voices.bin    [so no completo]
    <to>\\BepInEx\\plugins\\AI-Companions\\whisper\\*.bin                            [so no completo]

O --no-models tira SO os 3 arquivos grandes (model.int8.onnx, voices.bin e whisper\\*.bin):
`tokens.txt`, `LICENSE` e `espeak-ng-data\\` ficam, porque sao o FONEMIZADOR - sem eles a voz
nao funciona nem depois de o jogador por o modelo. (No 1.9.1: 399 -> 396, exatamente 3 a menos.)

Os modelos ficam DENTRO da pasta do plugin porque e ali que o mod os procura: `[AI] TtsModel`
e `[AI] SttModel` sao relativos a pasta do `Companions.dll` (WhisperStt.PluginDirectory /
CompanionTts) e o proprio LEIA-ME manda copiar `tts\\` e `whisper\\` para
`...\\BepInEx\\plugins\\AI-Companions\\`. O zip do 1.9.1 ja estava assim; os de 1.9.2/1.9.3
regrediram e traziam os modelos na RAIZ do zip - um pacote assim nao tem voz nem STT depois
de extraido.

Codigos de saida:
    0  staging montado e conferido
    1  falhou (arquivo proibido na origem/destino, item obrigatorio ausente, destino sujo)
    2  erro de uso (argparse)

Sem dependencias externas: so a stdlib do Python 3.
"""

import argparse
import os
import re
import shutil
import sys
from pathlib import Path

# --- a allow-list -------------------------------------------------------------------------
# Onde o plugin vai dentro do staging (o zip e montado a partir do CONTEUDO de <to>).
PLUGIN_SUBDIR = Path("BepInEx") / "plugins" / "AI-Companions"

# Idiomas que o mod distribui. O ingles e nativo no codigo (ModLocalization/SpeechConfig),
# entao NAO existe Translations\\English.json no pacote.
LANGUAGES = ("Portuguese_Brazilian", "Spanish", "French", "Italian")

# Assemblies gerenciados que o plugin realmente carrega. Fonte: src/Companions/Companions.csproj,
# alvos DeployWhisperNet (Whisper.net + os shims do netstandard2.0 que ele usa) e
# DeploySherpaOnnx (sherpa-onnx). Microsoft.Extensions.AI.Abstractions e System.Text.Json ficam
# de fora de proposito (ExcludeAssets="all").
MANAGED_DLLS = (
    "Companions.dll",
    "Whisper.net.dll",
    "sherpa-onnx.dll",
    "Microsoft.Bcl.AsyncInterfaces.dll",
    "System.Buffers.dll",
    "System.Memory.dll",
    "System.Numerics.Vectors.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.Threading.Tasks.Extensions.dll",
)

# Nativos do Whisper.net: o pacote Whisper.net.Runtime usa o layout nao-padrao
# runtimes\\win-x64\\*.dll (NativeLibraryLoader.GetRuntimePaths -> <pasta>\runtimes\win-x64).
WHISPER_NATIVES = (
    "ggml-base-whisper.dll",
    "ggml-cpu-whisper.dll",
    "ggml-whisper.dll",
    "whisper.dll",
)

# Nativos do sherpa-onnx (CompanionTts.EnsureNativeLibraries pre-carrega os dois por caminho
# absoluto a partir de <plugin>\\runtimes\\win-x64\\native).
SHERPA_NATIVES = (
    "onnxruntime.dll",
    "sherpa-onnx-c-api.dll",
)

# Documentos do pacote (vem de package/, vao para a raiz do staging). O CONFIGURATION.md e
# GERADO por tools/gen_config_doc.py - nao edite a mao.
PACKAGE_DOCS = (
    "LEIA-ME.txt",
    "CONFIGURATION.md",
    "CHANGELOG.md",
    "GUIDE.md",
    "README.md",
    "icon.png",
)

# O tts\kokoro e fonemizador + modelo. O pacote "sem modelos" tira SO os arquivos grandes
# (KOKORO_MODEL_FILES e whisper\*.bin): tokens.txt, LICENSE e espeak-ng-data\ FICAM, porque sem
# eles a voz nao funciona nem depois de o jogador por o modelo. Foi assim no 1.9.1 do Nexus:
# 399 -> 396 arquivos, exatamente 3 a menos.
KOKORO_REQUIRED_FILES = ("tokens.txt",)
KOKORO_REQUIRED_DIRS = ("espeak-ng-data",)
KOKORO_MODEL_FILES = ("model.int8.onnx", "voices.bin")

# Modelo de STT padrao ([AI] SttModel). Outros .bin entram (a allow-list e whisper\\*.bin), mas
# sao avisados: o ggml-base-q5_1.bin saiu do pacote na 1.9.1 (57 MB).
DEFAULT_WHISPER_MODEL = "ggml-small-q5_1.bin"

# Arquivos que NUNCA podem ir para o pacote (gerados em runtime / metadado de gerenciador).
FORBIDDEN_NAMES = ("speech.json", "__folder_managed_by_vortex")

SECTION_LABELS = {
    "docs": "docs do pacote (raiz do stage)",
    "managed": "assemblies gerenciados (pasta do plugin)",
    "whisper-natives": "nativos do Whisper.net (runtimes\\win-x64)",
    "sherpa-natives": "nativos do sherpa-onnx (runtimes\\win-x64\\native)",
    "licenses": "licencas (LICENSES)",
    "translations": "traducoes (Translations)",
    "speech": "falas (Speech)",
    "tts": "modelo de voz (tts\\kokoro)",
    "whisper": "modelo de STT (whisper)",
}
SECTION_ORDER = tuple(SECTION_LABELS)


# --- utilidades ---------------------------------------------------------------------------
def fmt_bytes(n):
    if n >= 1024 * 1024:
        return "%.1f MiB" % (n / (1024.0 * 1024.0))
    if n >= 1024:
        return "%.1f KiB" % (n / 1024.0)
    return "%d B" % n


def fmt_count(n, one, many):
    return "%d %s" % (n, one if n == 1 else many)


def files_under(root):
    """Every file under root, sorted (deterministic manifest)."""
    out = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames.sort()
        for name in sorted(filenames):
            out.append(Path(dirpath) / name)
    return out


def dlls_in(folder):
    if not folder.is_dir():
        return []
    return sorted(p.name for p in folder.iterdir() if p.is_file() and p.suffix.lower() == ".dll")


# --- varredura de arquivos proibidos ------------------------------------------------------
def scan_forbidden_names(root):
    """speech.json / __folder_managed_by_vortex em QUALQUER nivel (case-insensitive)."""
    hits = []
    if not root.is_dir():
        return hits
    banned = {n.casefold() for n in FORBIDDEN_NAMES}
    for path in files_under(root):
        name = path.name.casefold()
        if name in banned:
            reason = "arquivo de runtime (fallback do SpeechConfig)" if name == "speech.json" \
                else "marcador de gerenciador de mods (Vortex)"
            hits.append((path.relative_to(root), reason))
    return hits


def scan_language_dirs(payload):
    """Translations\\ e Speech\\ so podem ter os 4 idiomas, no nivel de cima, e mais nada.

    Pega Translations\\English.json (ingles e nativo no codigo), Speech\\speech.json,
    Translations\\German.json e a pasta legada Translations\\speech\\.
    """
    hits = []
    allowed = {"%s.json" % lang.casefold() for lang in LANGUAGES}
    for folder in ("Translations", "Speech"):
        root = payload / folder
        if not root.is_dir():
            continue
        for path in files_under(root):
            rel = path.relative_to(payload)
            if len(rel.parts) != 2:
                hits.append((rel, "arquivo dentro de subpasta de %s\\ (o carregador de "
                                  "localizacao trata subpasta como idioma)" % folder))
            elif path.name.casefold() not in allowed:
                hits.append((rel, "nao e um dos %d idiomas do pacote (%s)"
                             % (len(LANGUAGES), ", ".join(LANGUAGES))))
    return hits


def scan_forbidden(root, payload=None):
    """Nome proibido + regra de idioma, sem repetir o mesmo arquivo.

    `root` is what gets walked for the banned names; `payload` is the plugin folder whose
    Translations\\/Speech\\ are checked (defaults to root). Every hit is reported relative to
    `root`, so the same file never shows up twice.
    """
    root = Path(root)
    payload = Path(payload) if payload else root
    hits = list(scan_forbidden_names(root))
    for rel, reason in scan_language_dirs(payload):
        hits.append(((payload / rel).relative_to(root), reason))
    seen = {}
    for rel, reason in hits:
        seen.setdefault(str(rel), (rel, reason))
    return [seen[key] for key in sorted(seen)]


# --- plano de copia -----------------------------------------------------------------------
def plan_stage(from_dir, to_dir, package_dir, include_models):
    """Return (items, missing, notes).

    items: [(src, rel_dest, section)]  rel_dest e relativo a <to>.
    missing: caminhos obrigatorios que nao existem.
    notes: avisos de drift (arquivos na origem que NAO entram no pacote).
    """
    items, missing, notes = [], [], []

    def add(src, rel, section):
        if src.is_file():
            items.append((src, rel, section))
        else:
            missing.append(str(src))

    # package/ -> raiz do staging
    for name in PACKAGE_DOCS:
        add(package_dir / name, Path(name), "docs")

    # gerenciados + nativos -> pasta do plugin
    for name in MANAGED_DLLS:
        add(from_dir / name, PLUGIN_SUBDIR / name, "managed")
    for name in WHISPER_NATIVES:
        rel = PLUGIN_SUBDIR / "runtimes" / "win-x64" / name
        add(from_dir / "runtimes" / "win-x64" / name, rel, "whisper-natives")
    for name in SHERPA_NATIVES:
        rel = PLUGIN_SUBDIR / "runtimes" / "win-x64" / "native" / name
        add(from_dir / "runtimes" / "win-x64" / "native" / name, rel, "sherpa-natives")

    # licencas (arvore inteira)
    lic = from_dir / "LICENSES"
    if lic.is_dir():
        for path in files_under(lic):
            add(path, PLUGIN_SUBDIR / "LICENSES" / path.relative_to(lic), "licenses")
    else:
        missing.append(str(lic))

    # traducoes e falas (so os 4 idiomas, nome por nome)
    for name in LANGUAGES:
        add(from_dir / "Translations" / (name + ".json"),
            PLUGIN_SUBDIR / "Translations" / (name + ".json"), "translations")
    for name in LANGUAGES:
        add(from_dir / "Speech" / (name + ".json"),
            PLUGIN_SUBDIR / "Speech" / (name + ".json"), "speech")

    # tts\kokoro: entra nos DOIS pacotes (e o fonemizador). O --no-models tira so os arquivos
    # grandes do modelo; tokens.txt, LICENSE e espeak-ng-data\ ficam.
    kokoro = from_dir / "tts" / "kokoro"
    model_names = {name.casefold() for name in KOKORO_MODEL_FILES}
    if not kokoro.is_dir():
        missing.append(str(kokoro))
    else:
        for name in KOKORO_REQUIRED_FILES:
            if not (kokoro / name).is_file():
                missing.append(str(kokoro / name))
        for name in KOKORO_REQUIRED_DIRS:
            if not (kokoro / name).is_dir():
                missing.append(str(kokoro / name) + os.sep)
        if include_models:
            for name in KOKORO_MODEL_FILES:
                if not (kokoro / name).is_file():
                    missing.append(str(kokoro / name))
        for path in files_under(kokoro):
            if not include_models and path.parent == kokoro \
                    and path.name.casefold() in model_names:
                continue
            add(path, PLUGIN_SUBDIR / "tts" / "kokoro" / path.relative_to(kokoro), "tts")

    # whisper: so o modelo, e so no pacote completo
    whisper = from_dir / "whisper"
    bins = sorted(whisper.glob("*.bin")) if whisper.is_dir() else []
    if include_models:
        if not bins:
            missing.append(str(whisper / "*.bin"))
        for path in bins:
            add(path, PLUGIN_SUBDIR / "whisper" / path.name, "whisper")
            if path.name.casefold() != DEFAULT_WHISPER_MODEL.casefold():
                notes.append("whisper\\%s entra no pacote mas nao e o modelo padrao (%s)"
                             % (path.name, DEFAULT_WHISPER_MODEL))

    # drift: o que existe na origem e NAO entra (visibilidade, nao bloqueia)
    extra_root = sorted(set(dlls_in(from_dir)) - set(MANAGED_DLLS))
    if extra_root:
        notes.append("DLL na raiz do plugin fora da allow-list (nao entra): " + ", ".join(extra_root))
    other_root = sorted(p.name for p in from_dir.iterdir()
                        if p.is_file() and p.suffix.lower() != ".dll")
    if other_root:
        notes.append("arquivo solto na raiz do plugin (nao entra): " + ", ".join(other_root))
    extra_native = sorted(set(dlls_in(from_dir / "runtimes" / "win-x64")) - set(WHISPER_NATIVES))
    if extra_native:
        notes.append("nativo extra em runtimes\\win-x64 (nao entra): " + ", ".join(extra_native))
    extra_sherpa = sorted(set(dlls_in(from_dir / "runtimes" / "win-x64" / "native"))
                          - set(SHERPA_NATIVES))
    if extra_sherpa:
        notes.append("nativo extra em runtimes\\win-x64\\native (nao entra): "
                     + ", ".join(extra_sherpa))

    return items, missing, notes


# --- checagem de versao -------------------------------------------------------------------
def version_warnings(version, from_dir):
    """Avisos (nunca falha): o staging pode ser montado de um build diferente do pedido."""
    out = []
    repo = Path(__file__).resolve().parent.parent
    plugin_cs = repo / "src" / "Companions" / "Core" / "CompanionsPlugin.cs"
    if plugin_cs.is_file():
        try:
            text = plugin_cs.read_text(encoding="utf-8", errors="replace")
        except OSError:
            text = ""
        m = re.search(r'PluginVersion\s*=\s*"([^"]+)"', text)
        if m and m.group(1) != version:
            out.append("--version %s != PluginVersion \"%s\" na fonte (%s): o DLL staged pode "
                       "ser de outro build." % (version, m.group(1), plugin_cs.name))
    dll = from_dir / "Companions.dll"
    if dll.is_file():
        try:
            blob = dll.read_bytes()
        except OSError:
            blob = b""
        if blob and version.encode("utf-16-le") not in blob:
            out.append("o Companions.dll de --from nao contem a string \"%s\": confira se ele e "
                       "o build dessa versao." % version)
    return out


# --- checagem do destino ------------------------------------------------------------------
def check_destination(payload, include_models):
    """O que o staging TEM de conter (e o que nao pode) para o modo pedido.

    Separado do main para poder ser exercitado direto (o caso de erro so apareceria se a
    copia falhasse). Devolve a lista de erros; vazia = ok.
    """
    errors = []
    kokoro = payload / "tts" / "kokoro"
    if include_models:
        if not (payload / "tts").is_dir():
            errors.append("destino sem tts\\ (pacote completo)")
        for name in KOKORO_MODEL_FILES:
            if not (kokoro / name).is_file():
                errors.append("destino sem tts\\kokoro\\%s (pacote completo)" % name)
        if not list((payload / "whisper").glob("*.bin")):
            errors.append("destino sem whisper\\*.bin (pacote completo)")
    else:
        # O --no-models tira SO os 3 arquivos grandes: o fonemizador tem de estar la.
        for name in KOKORO_REQUIRED_FILES:
            if not (kokoro / name).is_file():
                errors.append("destino sem tts\\kokoro\\%s (o fonemizador vai nos dois pacotes)"
                              % name)
        for name in KOKORO_REQUIRED_DIRS:
            if not (kokoro / name).is_dir():
                errors.append("destino sem tts\\kokoro\\%s\\ (o fonemizador vai nos dois pacotes)"
                              % name)
        for name in KOKORO_MODEL_FILES:
            if (kokoro / name).exists():
                errors.append("destino tem tts\\kokoro\\%s num pacote --no-models" % name)
        if (payload / "whisper").exists():
            errors.append("destino tem whisper\\ num pacote --no-models")
    return errors


# --- main ---------------------------------------------------------------------------------
def main(argv):
    ap = argparse.ArgumentParser(
        description="Monta o staging do pacote do AI Companions por allow-list.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="A allow-list, os arquivos proibidos e o layout estao no cabecalho de "
               "tools/stage_release.py.")
    ap.add_argument("--version", required=True, help="versao do pacote (ex.: 1.9.4)")
    ap.add_argument("--from", dest="from_dir", required=True,
                    help="pasta do plugin deployado (a que tem o Companions.dll)")
    ap.add_argument("--to", dest="to_dir", required=True,
                    help="raiz do staging (ex.: pkg/1.9.4/stage)")
    ap.add_argument("--no-models", action="store_true",
                    help="pacote sem modelos: omite SO os 3 arquivos grandes "
                         "(tts\\kokoro\\model.int8.onnx, voices.bin e whisper\\*.bin); "
                         "tokens.txt, LICENSE e espeak-ng-data\\ ficam (fonemizador)")
    ap.add_argument("--package-dir", default=None,
                    help="pasta com os docs do pacote (padrao: package/ do repositorio)")
    ap.add_argument("--force", action="store_true",
                    help="apaga um --to que ja existe e nao esta vazio")
    ap.add_argument("--allow-source-drift", action="store_true",
                    help="nao falha por arquivo proibido NA origem (pasta de deploy suja); "
                         "eles continuam sem ser copiados")
    ap.add_argument("--dry-run", action="store_true",
                    help="so valida e mostra o manifesto; nao escreve nada")
    args = ap.parse_args(argv)

    from_dir = Path(args.from_dir).expanduser().resolve()
    to_dir = Path(args.to_dir).expanduser().resolve()
    package_dir = Path(args.package_dir).expanduser().resolve() if args.package_dir \
        else Path(__file__).resolve().parent.parent / "package"
    include_models = not args.no_models
    labels = dict(SECTION_LABELS)
    if not include_models:
        # The --no-models package still ships the phonemizer, just not the model.
        labels["tts"] = "fonemizador (tts\\kokoro, sem o modelo)"

    print("== AI Companions %s - staging do pacote ==" % args.version)
    print("origem : %s" % from_dir)
    print("destino: %s%s" % (to_dir, "  (dry-run)" if args.dry_run else ""))
    print("modo   : %s" % ("completo (com o modelo de voz e o de STT)" if include_models
                           else "sem modelos (tira so model.int8.onnx, voices.bin e "
                                "whisper\\*.bin; o fonemizador fica)"))
    print("")

    errors = []

    # 1. caminhos
    if not from_dir.is_dir():
        print("[erro] --from nao e uma pasta: %s" % from_dir)
        return 1
    if to_dir == from_dir or from_dir in to_dir.parents or to_dir in from_dir.parents:
        print("[erro] --from e --to nao podem estar um dentro do outro")
        return 1
    if to_dir.exists() and not to_dir.is_dir():
        print("[erro] --to existe e nao e uma pasta: %s" % to_dir)
        return 1

    # 2. plano
    items, missing, notes = plan_stage(from_dir, to_dir, package_dir, include_models)
    for path in missing:
        errors.append("obrigatorio e ausente: %s" % path)

    # 3. arquivos proibidos na origem
    source_hits = scan_forbidden(from_dir)
    if source_hits:
        label = "ignorados (--allow-source-drift)" if args.allow_source_drift else "ERRO"
        print("[origem] %s: %s" % (label, fmt_count(len(source_hits), "arquivo proibido",
                                                    "arquivos proibidos")))
        for rel, reason in source_hits:
            print("   %-58s %s" % (rel, reason))
        if not args.allow_source_drift:
            errors.append("%d arquivo(s) proibido(s) na origem: use --allow-source-drift para "
                          "staging de uma pasta de deploy suja (eles nao sao copiados de "
                          "qualquer forma)" % len(source_hits))
    else:
        print("[origem] nenhum arquivo proibido (speech.json / Translations\\English.json / "
              "__folder_managed_by_vortex)")

    # 4. drift informativo
    for note in notes:
        print("[origem] aviso: %s" % note)
    for note in version_warnings(args.version, from_dir):
        print("[versao] aviso: %s" % note)

    # 5. destino
    if to_dir.is_dir() and any(to_dir.iterdir()):
        if not args.force:
            errors.append("--to ja existe e nao esta vazio (use --force para refazer): %s" % to_dir)
        elif not args.dry_run:
            print("[destino] apagando %s (--force)" % to_dir)
            shutil.rmtree(to_dir)

    if errors:
        print("")
        for err in errors:
            print("[erro] %s" % err)
        print("")
        print("[falhou] staging NAO montado.")
        return 1

    # 6. copia
    by_section = {key: [] for key in SECTION_ORDER}
    for src, rel, section in items:
        by_section[section].append((src, rel))

    if not args.dry_run:
        to_dir.mkdir(parents=True, exist_ok=True)
        for src, rel, _section in items:
            dst = to_dir / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
            if dst.stat().st_size != src.stat().st_size:
                errors.append("copia truncada: %s" % rel)

    # 7. manifesto
    totals = {}
    for section in SECTION_ORDER:
        rows = by_section[section]
        if not rows:
            continue
        size = sum(src.stat().st_size for src, _rel in rows)
        totals[section] = (len(rows), size)
        print("")
        print("-- %s: %s, %s --" % (labels[section], fmt_count(len(rows), "arquivo",
                                                                      "arquivos"), fmt_bytes(size)))
        width = min(74, max(len(str(rel)) for _src, rel in rows))
        for src, rel in rows:
            print("   %-*s %12s" % (width, rel, "{:,}".format(src.stat().st_size).replace(",", ".")))

    # 8. arquivos proibidos no destino (staging nunca sai sujo)
    if not args.dry_run:
        payload = to_dir / PLUGIN_SUBDIR
        dest_hits = scan_forbidden(to_dir, payload)
        if dest_hits:
            print("")
            for rel, reason in dest_hits:
                print("[erro] destino: %s (%s)" % (rel, reason))
            errors.append("%d arquivo(s) proibido(s) no destino" % len(dest_hits))

        errors.extend(check_destination(payload, include_models))

    # 9. resumo
    total_files = sum(n for n, _b in totals.values())
    total_bytes = sum(b for _n, b in totals.values())
    print("")
    print("== resumo ==")
    for section in SECTION_ORDER:
        if section in totals:
            n, size = totals[section]
            print("   %-50s %5d  %s" % (labels[section], n, fmt_bytes(size)))
    print("   %-50s %5d  %s" % ("TOTAL", total_files, fmt_bytes(total_bytes)))
    print("")
    print("   zip: compacte o CONTEUDO de %s" % to_dir)
    print("        (o plugin ja esta em %s e os modelos dentro dele)" % PLUGIN_SUBDIR)

    if errors:
        print("")
        for err in errors:
            print("[erro] %s" % err)
        print("[falhou] staging montado com erro.")
        return 1

    print("")
    print("[ok] staging %s: %s, %s" % ("validado" if args.dry_run else "montado",
                                       fmt_count(total_files, "arquivo", "arquivos"),
                                       fmt_bytes(total_bytes)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
