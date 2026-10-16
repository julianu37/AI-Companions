# -*- coding: utf-8 -*-
"""Auditoria de traducao PT-BR do mod (cobertura de UI, falas e literais em ingles).

Uso:
    python tools/audit_translations.py            # relatorio completo
    python tools/audit_translations.py --quiet    # so os totais

O que ele checa (e o que cada numero quer dizer):

1. UI       - os defaults em ingles do `src/Companions/Core/ModLocalization.cs` contra
              `Translations/Portuguese_Brazilian.json`: chaves ausentes, chaves que so existem de um lado e
              valores que continuam identicos ao ingles.
2. Codigo   - todas as chamadas `Loc(...)`/`LocFmt(...)`/`LocOr(...)` do `src/`: chave referenciada que nao
              existe em nenhum dos dois lados (o jogador veria o token cru) e chaves definidas e nunca usadas.
              Prefixos de concatenacao (`Loc("hc_cat_" + nome)`) sao reconhecidos como prefixo, nao como chave.
3. Falas    - os campos/arrays do `SpeechConfig.Defaults()` contra `Speech/Portuguese_Brazilian.json`
              (`SpeechConfig.Load` le `<pasta do plugin>/Speech/<Idioma>.json`).
4. Literais - literais de string "com cara de texto" nas linhas que mexem com UI (`.text =`, `MessageHud`,
              hover, botoes, labels, `Say(`...) e que NAO passam por `Loc`. Linhas de log/diagnostico
              (`Log*`, `Dbg`, `Diag`) e nomes de GameObject/componente sao descartados de proposito - o
              jogador nao os ve (so no LogOutput.log).

Exit code 1 quando ha lacuna (chave ausente, valor em ingles ou literal suspeito), 0 quando esta limpo.
"""
import io
import os
import re
import sys
import json

try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
except Exception:
    pass

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
QUIET = '--quiet' in sys.argv

ENTRY = re.compile(r'"key"\s*:\s*"((?:[^"\\]|\\.)*)"[^"]*"value"\s*:\s*"((?:[^"\\]|\\.)*)"')
DEFAULT_ENTRY = re.compile(r'\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*\}')
LOC_CALL = re.compile(r'\bLoc(?:Fmt|Or)?\s*\(\s*"([^"]+)"')
STRING_LIT = re.compile(r'"((?:[^"\\]|\\.)*)"')

# palavras que denunciam ingles num texto de UI
EN_WORDS = re.compile(
    r'\b(the|and|you|your|not|with|from|need|can|will|here|there|that|this|have|has|are|was|were|is|it|of|to|in|on|at|my|me|we|our|'
    r'already|nothing|everything|please|press|open|close|error|failed|missing|empty|full|ready|waiting|done|left|right|down|up|out|'
    r'only|too|use|used|setting|settings|option|options|mode|button|name|value|off|none|again|later|now|still|keep|give|take|make|'
    r'find|search|add|remove|new|old|one|two|all|any|some|each|every|which|what|when|where|how|why|who|hold|companion|warrior|mage|'
    r'support|inventory|commands|health|stamina|voice|speech|panel|memory|speed|volume|push|talk|hunt|farm|fish|cook|repair|pickup)\b',
    re.I)
# linhas que sao log/diagnostico (o jogador so ve no LogOutput.log)
LOG_LINE = re.compile(r'Log\w*\(|LogInfo|LogWarning|LogError|LogDebug|\bDbg\(|\bInfo\(|\bWarn\(|Diag')
# linhas que sao nome de objeto/componente/atributo, nao texto de UI
GO_NAME_LINE = re.compile(r'new GameObject\(|GetComponent|AccessTools|HarmonyPatch|typeof\(|\.Find\(|name\s*=\s*"')
# arquivos que sao a propria fonte do ingles (o dicionario de reserva) - nao sao texto "hardcoded"
SKIP_FILES = ('Core\\ModLocalization.cs',)
# falsos positivos conhecidos, com o motivo (rel, literal)
ALLOW = {
    # o reserva defensivo para quando a localizacao devolve um token nao resolvido; a chave existe nos dois arquivos
    ('src\\Companions\\Patches\\MenuPatches.cs', 'Mod Options'),
    # nomes internos de GameObject (o rotulo do dropdown vem de um array ja localizado)
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'Voice'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'Personality'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'Memory'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'Speed'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'Volume'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'FreeText'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'Close'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'Apply'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'Listen'),
    ('src\\Companions\\UI\\CompanionAiPanel.cs', 'ChangeKey'),
    ('src\\Companions\\UI\\CompanionInteractPanel.cs', 'Fill Area'),
    ('src\\Companions\\UI\\CompanionInteractPanel.cs', 'Handle Slide Area'),
    ('src\\Companions\\UI\\CompanionPanel.cs', 'Fill Area'),
    ('src\\Companions\\UI\\CompanionPanel.cs', 'Handle Slide Area'),
    ('src\\Companions\\UI\\CompanionPanel.cs', 'Sliding Area'),
    ('src\\Companions\\UI\\ConfigPanel.cs', 'Fill Area'),
    ('src\\Companions\\UI\\ConfigPanel.cs', 'Handle Slide Area'),
    ('src\\Companions\\UI\\ConfigPanel.cs', 'Sliding Area'),
}
# "linhas de UI" - onde um literal aparece para o jogador
UI_LINE = re.compile(
    r'MessageHud|\.Message\(|ShowMessage|SetNpcText|AddString|UnifiedPopup|MakeText|CreateLabel|CreateButton|CreateInputField|'
    r'CreateToggle|CreateSlider|CreateDropdown|Chat\.instance|Say\(|SayRandom|SayContextual|GetHover|placeholder|\.text\s*=|Label|Hint|Title')


def unescape(s):
    return (s.replace('\\n', '\n').replace('\\r', '\r').replace('\\t', '\t')
             .replace('\\"', '"').replace('\\\\', '\\'))


def english_defaults():
    """As chaves e os textos em ingles registrados no ModLocalization.cs."""
    path = os.path.join(ROOT, 'src', 'Companions', 'Core', 'ModLocalization.cs')
    cs = open(path, encoding='utf-8').read()
    block = cs[cs.index('_englishDefaults = new Dictionary<string, string>'):]
    block = block[:block.index('\n\t\t};')]
    return {unescape(k): unescape(v) for k, v in DEFAULT_ENTRY.findall(block)}


def translation_file(rel):
    path = os.path.join(ROOT, rel)
    txt = open(path, encoding='utf-8').read()
    return {unescape(k): unescape(v) for k, v in ENTRY.findall(txt)}


def code_keys():
    """(chaves literais usadas em Loc, prefixos de concatenacao, todos os literais 'hc_*' do codigo)."""
    used, prefixes, literals = set(), set(), set()
    src = os.path.join(ROOT, 'src', 'Companions')
    for dirpath, _dirs, files in os.walk(src):
        if 'obj' in dirpath or 'bin' in dirpath:
            continue
        for fn in files:
            if not fn.endswith('.cs'):
                continue
            for line in open(os.path.join(dirpath, fn), encoding='utf-8'):
                for lit in STRING_LIT.findall(line):
                    if lit.startswith('hc_'):
                        literals.add(lit)
                for key in LOC_CALL.findall(line):
                    if line.rstrip().endswith('+') or ('"' + key + '" +') in line:
                        prefixes.add(key)
                    else:
                        used.add(key)
    return used, prefixes, literals


def speech_defaults():
    """Os arrays de `SpeechConfig.Defaults()` (as falas em ingles do codigo)."""
    cs = open(os.path.join(ROOT, 'src', 'Companions', 'Core', 'SpeechConfig.cs'), encoding='utf-8').read()
    fields = re.findall(r'public string\[\] (\w+);', cs)
    defaults = {}
    for m in re.finditer(r'speechConfig\.(\w+) = new string\[(\d+)\]\s*\{(.*?)\};', cs, re.S):
        defaults[m.group(1)] = re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(3))
    return fields, defaults


def ui_literals():
    """Literais 'de texto' em linhas de UI que nao passam por Loc."""
    found = []
    src = os.path.join(ROOT, 'src', 'Companions')
    for dirpath, _dirs, files in os.walk(src):
        if 'obj' in dirpath or 'bin' in dirpath or 'Diagnostics' in dirpath:
            continue
        for fn in files:
            if not fn.endswith('.cs'):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, ROOT)
            if rel.endswith(SKIP_FILES):
                continue
            for i, line in enumerate(open(path, encoding='utf-8'), 1):
                s = line.strip()
                if s.startswith('//') or s.startswith('*') or s.startswith('///'):
                    continue
                if LOG_LINE.search(line) or GO_NAME_LINE.search(line):
                    continue
                if 'Loc(' in line or 'LocOr(' in line or 'LocFmt(' in line:
                    continue
                if not UI_LINE.search(line):
                    continue
                for lit in STRING_LIT.findall(line):
                    if not lit or lit.startswith('$') or lit.startswith('hc_') or lit.startswith('MHR_') or lit.startswith('ZDO'):
                        continue
                    if not re.search(r'[A-Za-z]{3}', lit):
                        continue
                    if not (re.search(r'[ .!?]', lit) or '.text' in line or 'laceholder' in line):
                        continue
                    if not EN_WORDS.search(lit):
                        continue
                    if (rel, lit) in ALLOW:
                        continue
                    found.append((rel, i, lit, s))
    return found


def report():
    gaps = 0
    en = english_defaults()
    pt = translation_file('Translations/Portuguese_Brazilian.json')
    print('== UI ==')
    print('   ModLocalization (ingles): %d chaves' % len(en))
    print('   Translations/Portuguese_Brazilian.json: %d chaves' % len(pt))
    missing = sorted(k for k in en if k not in pt)
    extra = sorted(k for k in pt if k not in en)
    same = sorted(k for k in en if k in pt and pt[k] == en[k])
    print('   ausentes no PT-BR: %d%s' % (len(missing), (' -> ' + ', '.join(missing)) if missing else ''))
    print('   sobrando (nao existem no ingles): %d%s' % (len(extra), (' -> ' + ', '.join(extra)) if extra else ''))
    print('   valor igual ao ingles: %d -> %s' % (len(same), ', '.join(same)))
    gaps += len(missing) + len(extra)

    print('== codigo ==')
    used, prefixes, literals = code_keys()
    orphans = sorted(k for k in used if k not in en and k not in pt)
    unused = sorted(k for k in en if k not in used and k not in literals and not any(k.startswith(p) for p in prefixes))
    print('   chaves literais em Loc/LocFmt/LocOr: %d' % len(used))
    print('   prefixos de concatenacao: %d -> %s' % (len(prefixes), ', '.join(sorted(prefixes))))
    print('   chaves orfas (nao existem nos dois lados): %d%s' % (len(orphans), (' -> ' + ', '.join(orphans)) if orphans else ''))
    print('   definidas e nunca citadas no codigo (nem por concatenacao): %d%s' % (len(unused), (' -> ' + ', '.join(unused)) if unused else ''))
    gaps += len(orphans)

    print('== falas ==')
    fields, defaults = speech_defaults()
    speech = json.load(open(os.path.join(ROOT, 'Speech', 'Portuguese_Brazilian.json'), encoding='utf-8'))
    lines = sum(len(v) for v in speech.values() if isinstance(v, list))
    print('   SpeechConfig: %d categorias / %d linhas em ingles no codigo' % (len(fields), sum(len(v) for v in defaults.values())))
    print('   Speech/Portuguese_Brazilian.json: %d categorias / %d linhas' % (len(speech), lines))
    miss = sorted(f for f in fields if f not in speech)
    empty = sorted(f for f in fields if isinstance(speech.get(f), list) and not speech[f])
    identical = sorted((f, [x for x in speech.get(f, []) if x in defaults.get(f, [])]) for f in fields if f in speech)
    identical = [x for x in identical if x[1]]
    print('   categorias ausentes: %d%s' % (len(miss), (' -> ' + ', '.join(miss)) if miss else ''))
    print('   categorias vazias: %d%s' % (len(empty), (' -> ' + ', '.join(empty)) if empty else ''))
    for f, dup in identical:
        print('   linhas iguais ao ingles em %s: %s' % (f, dup))
    gaps += len(miss) + len(empty)

    print('== literais em ingles em linhas de UI (sem Loc) ==')
    lits = ui_literals()
    print('   suspeitos: %d' % len(lits))
    if not QUIET:
        for rel, i, lit, s in lits:
            print('   %s:%d | %s' % (rel, i, lit))
            print('        %s' % s)
    gaps += len(lits)

    print('== resumo ==')
    print('   lacunas: %d' % gaps)
    return 0 if gaps == 0 else 1


if __name__ == '__main__':
    sys.exit(report())
