# -*- coding: utf-8 -*-
"""Auditoria de traducao do mod (cobertura de UI, falas e literais em ingles).

Uso:
    python tools/audit_translations.py                      # relatorio completo do PT-BR (a fonte)
    python tools/audit_translations.py --quiet              # so os totais
    python tools/audit_translations.py --lang Spanish       # audita UMA traducao contra o PT-BR
    python tools/audit_translations.py --lang French --quiet

O que ele checa no modo PT-BR (e o que cada numero quer dizer):

1. UI       - os defaults em ingles do `src/Companions/Core/ModLocalization.cs` contra
              `Translations/Portuguese_Brazilian.json`: chaves ausentes, chaves que so existem de um lado e
              valores que continuam identicos ao ingles. As chaves que so existem no arquivo de idioma por
              desenho (`hc_cfg_*` de cada opcao e as abas `hc_cat_*` das secoes novas - em ingles o rotulo e a
              PROPRIA chave, ver `ConfigPanel.EntryLabel`) continuam na conta de "sobrando", e uma linha diz
              quantas sao dessa classe: uma opcao nova no `ModConfig` move o numero em 1, e so isso.
2. Codigo   - todas as chamadas `Loc(...)`/`LocFmt(...)`/`LocOr(...)` do `src/`: chave referenciada que nao
              existe em nenhum dos dois lados (o jogador veria o token cru) e chaves definidas e nunca usadas.
              Prefixos de concatenacao (`Loc("hc_cat_" + nome)`) sao reconhecidos como prefixo, nao como chave.
3. Falas    - os campos/arrays do `SpeechConfig.Defaults()` contra `Speech/Portuguese_Brazilian.json`
              (`SpeechConfig.Load` le `<pasta do plugin>/Speech/<Idioma>.json`).
4. Literais - literais de string "com cara de texto" nas linhas que mexem com UI (`.text =`, `MessageHud`,
              hover, botoes, labels, `Say(`...) e que NAO passam por `Loc`. Linhas de log/diagnostico
              (`Log*`, `Dbg`, `Diag`) e nomes de GameObject/componente sao descartados de proposito - o
              jogador nao os ve (so no LogOutput.log).

O modo `--lang <NomeDoArquivoSemExtensao>` (Multilanguage M3) valida `Translations/<Nome>.json` e
`Speech/<Nome>.json` contra o PT-BR:
    - toda chave do PT existe, na MESMA ordem, e nao esta vazia;
    - nenhum placeholder (`{0}`, `{1}`...) diverge do PT;
    - nenhum valor igual ao PT, exceto os que sao legitimamente iguais (nome de voz, string so com
      placeholders/codigos, palavra que o idioma escreve igual - a lista esta em IDENTICAL_OK, com o porque);
    - nenhuma linha com "cara de ingles" (as palavras de EN_HINTS que o PT nao tem);
    - as falas tem as mesmas categorias e a mesma quantidade de linhas, e nenhuma linha igual ao PT.

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


def language_argument():
    """`--lang Spanish` / `--lang=French`: a traducao a auditar (o nome do arquivo, sem extensao)."""
    for i, arg in enumerate(sys.argv):
        if arg == '--lang' and i + 1 < len(sys.argv):
            return sys.argv[i + 1]
        if arg.startswith('--lang='):
            return arg.split('=', 1)[1]
    return None


LANG = language_argument()

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
    # P2 (2.0.0): hc_cfg_* e as abas hc_cat_* das secoes novas existem SO nos arquivos de idioma - em ingles o
    # rotulo e a propria chave (ConfigPanel.EntryLabel -> LocOr(..., chave)), entao elas nao tem default em
    # ModLocalization. Continuam contadas acima (a conta nao mudou); esta linha so diz quantas sao desta classe
    # para o numero ser explicavel quando uma opcao nova entra no ModConfig.
    by_design = [k for k in extra if k.startswith('hc_cfg_') or k.startswith('hc_cat_')]
    print('   destas, so-de-JSON por desenho (hc_cfg_*/hc_cat_*, o ingles mostra a chave): %d' % len(by_design))
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


# ----------------------------------------------------------------------------------------------------------------
# Modo --lang: uma traducao (ES/FR...) contra o PT-BR
# ----------------------------------------------------------------------------------------------------------------

PLACEHOLDER = re.compile(r'\{(\d+)\}')

# nome de voz do Kokoro (pf_dora, af_heart...): igual em todo idioma
VOICE_NAME = re.compile(r'^(pf|pm|af|am|ef|em|ff|bf|bm|hf|hm|if|im|jf|jm|zf|zm)_')

# strings sem letra nenhuma depois de tirar os placeholders: '{0} ({1})', '{0} {1}', '  {0}  '...
TOKEN_ONLY = re.compile(r'^[\s{}0-9/:.,()\-%°·—]*$')

# palavras que denunciam ingles numa traducao. Menor que a EN_WORDS de proposito: nada aqui e palavra de
# espanhol nem de frances ('a', 'on', 'me', 'no', 'se', 'son', 'panel', 'option', 'file' ficaram de fora por isso).
EN_HINTS = re.compile(
    r'\b(the|and|you|your|with|from|this|that|these|those|not|can|will|would|should|could|there|here|what|when|'
    r'where|which|how|why|any|some|each|every|more|most|than|then|also|because|about|into|over|under|'
    r'between|again|still|just|only|own|same|other|new|old|make|made|take|give|keep|left|right|down|out|off|up|'
    r'settings|setting|voice|language|button|window|press|hold|wait|ready|missing|please|'
    r'thanks|sorry|disabled|enabled)\b',
    re.I)

# Valores que sao legitimamente IGUAIS ao PT, agrupados pelo motivo. Sem isso a auditoria teria 84 falsos
# positivos no espanhol: palavras que os dois idiomas escrevem igual, tokens tecnicos e nomes proprios.
IDENTICAL_OK = {
    'Spanish': {
        # Fase 2.0 (ordens de interacao): cognatos legitimos PT/ES e placeholders puros
        'Recuperar', 'Descansando', 'Esperando', 'Buscando {0}', 'Buscando {0}.',
        'pescando {0}', 'guardando {0}', 'plantando {0}', '{0} {1}/{2} para {3}.',
        # P2 (rotulos do F8 da 2.0.0): a tecla da postura equilibrada
        'Tecla equilibrada',
        # palavras que o espanhol e o portugues escrevem igual (ou quase: só o acento muda)
        'Aplicar', 'Automático', 'Automático ({0})', 'Aviso:', 'Banco: {0}', 'Cama de {0}', 'Cancelar', 'Comando',
        'Comandos', 'Combate', 'Comida', 'Comidas', 'Comprar {0} ({1})', 'Controles', 'Cura por segundo',
        'Defensivo', 'Descansado', 'Descanso', 'Dispositivo', 'Equilibrado', 'Estado', 'Fundir',
        'Hora de descansar.', 'Hora de fundir.', 'Invocar', 'Mago', 'Mago sarcástico', 'Masculino', 'Modelo',
        'Mostrar texto', 'Nunca descartar', 'Pensando...', 'Perdido', 'Pesca', 'Pescar', 'Peso máximo',
        'Plantar', 'Presas', 'Proteger base (m)', 'Próxima planta (m)', 'Reabastecer', 'Recuperar perdido',
        'Regen. por segundo', 'Repetir oferta inicial', 'Reserva de ingrediente', 'Sarcástico', 'Seguir',
        'Soltando.', 'Stamina', 'Stamina  {0} / {1}', 'Stamina base', 'Tecla de comando', 'Tecla modificadora',
        'Temperatura', 'Tipo', 'Usar alambique', 'Usar fermentador', 'Vagar', 'Vida  {0} / {1}', 'Vida base',
        'Vida p/ defensiva (%)', 'Viking leal', 'Vivo', 'Voz', 'casa', 'ignorar comandos', 'masculina', 'parar',
        'pescar', 'postura defensiva', 'postura equilibrada',
        # token tecnico que fica em ingles em todo idioma (ou nome de modelo)
        'Cooldown (s)', 'Cooldown de ataque (s)', 'IA', 'IA: {0}', 'Intervalo máximo (s)', 'Intervalo mínimo (s)',
        'LM Studio (local)', 'Personalizado (URL manual)', 'Prompt inicial', 'Timeout (s)',
        # nome proprio
        'Dverger',
        # so placeholders (o resto e pontuacao)
        '{0} ({1})', '{0} {1}', '{0} ({1}) - usando {2}',
    },
    'French': {
        'Volume',                                   # mesma palavra nos dois idiomas
        'Cache', 'Cooldown (s)', 'IA', 'LM Studio (local)', 'Timeout (s)',   # tokens tecnicos
        'Dverger',                                  # nome proprio
        '{0} ({1})', '{0} {1}',                     # so placeholders
    },
    'Italian': {
        # Fase 2.0: pescando e igual em italiano
        'pescando {0}',
        # palavras que o italiano e o portugues escrevem igual
        'Tipo', 'Nome', 'Mago', 'Passivo', 'casa', 'Vivo', 'Morto', 'Movimento', 'Stamina', 'Morte', 'Pesca',
        'Stamina  {0} / {1}', 'Stamina base', 'IA: {0}', 'Ok: {0}', 'Ok: {0} ({1})', 'Volume',
        # tokens tecnicos e nome de modelo
        'Cache', 'Cooldown (s)', 'IA', 'Temperatura', 'Timeout (s)', 'Preset', 'Dispositivo',
        # nome proprio
        'Dverger',
        # so placeholders
        '{0} ({1})', '{0} {1}',
    },
}

# Falas que sao iguais ao PT por nao serem traducao nenhuma (a interjeicao viking), em qualquer idioma.
SPEECH_IDENTICAL_OK = { 'Skål!' }


def placeholders(value):
    return sorted(PLACEHOLDER.findall(value))


def identical_allowed(lang, value):
    if TOKEN_ONLY.match(value) or VOICE_NAME.match(value):
        return True
    return value in IDENTICAL_OK.get(lang, ())


def looks_english(value, pt_value):
    """Palavra de EN_HINTS que a traducao tem e o PT nao - uma copia nao traduzida cai aqui."""
    for match in EN_HINTS.finditer(value):
        word = match.group(1).lower()
        if not re.search(r'\b' + re.escape(word) + r'\b', pt_value, re.I):
            return match.group(1)
    return None


def speech_file(rel):
    path = os.path.join(ROOT, rel)
    try:
        return json.load(open(path, encoding='utf-8'))
    except Exception as ex:
        print('   nao consegui ler %s: %s' % (rel, ex))
        return None


def report_language(lang):
    """Audita UMA traducao (UI + falas) contra o PT-BR, que e a fonte."""
    gaps = 0
    pt = translation_file('Translations/Portuguese_Brazilian.json')
    tr = translation_file('Translations/%s.json' % lang)
    print('== UI (%s) ==' % lang)
    print('   PT-BR: %d chaves, %s: %d chaves' % (len(pt), lang, len(tr)))
    missing = [k for k in pt if k not in tr]
    extra = [k for k in tr if k not in pt]
    empty = [k for k, v in tr.items() if not v.strip()]
    print('   ausentes: %d%s' % (len(missing), (' -> ' + ', '.join(missing)) if missing else ''))
    print('   sobrando (nao existem no PT): %d%s' % (len(extra), (' -> ' + ', '.join(extra)) if extra else ''))
    print('   vazias: %d%s' % (len(empty), (' -> ' + ', '.join(empty)) if empty else ''))
    bad_ph = [(k, placeholders(pt[k]), placeholders(tr[k])) for k in pt if k in tr and placeholders(pt[k]) != placeholders(tr[k])]
    print('   placeholders divergentes: %d' % len(bad_ph))
    if not QUIET:
        for key, a, b in bad_ph:
            print('      %s | PT %s -> %s %s' % (key, a, lang, b))
    same = [k for k in pt if k in tr and tr[k] == pt[k] and not identical_allowed(lang, pt[k])]
    print('   iguais ao PT (nao permitidos): %d%s' % (len(same), (' -> ' + ', '.join(same)) if same else ''))
    english = [(k, word) for k in tr if k in pt for word in [looks_english(tr[k], pt[k])] if word]
    print('   com cara de ingles: %d' % len(english))
    if not QUIET:
        for key, word in english:
            print('      %s | "%s" (%s)' % (key, tr[key], word))
    gaps += len(missing) + len(extra) + len(empty) + len(bad_ph) + len(same) + len(english)

    print('== falas (%s) ==' % lang)
    pt_speech = speech_file('Speech/Portuguese_Brazilian.json') or {}
    tr_speech = speech_file('Speech/%s.json' % lang) or {}
    pt_lines = sum(len(v) for v in pt_speech.values() if isinstance(v, list))
    tr_lines = sum(len(v) for v in tr_speech.values() if isinstance(v, list))
    print('   PT-BR: %d categorias / %d linhas; %s: %d categorias / %d linhas'
          % (len(pt_speech), pt_lines, lang, len(tr_speech), tr_lines))
    miss_cat = [c for c in pt_speech if c not in tr_speech]
    extra_cat = [c for c in tr_speech if c not in pt_speech]
    count = [(c, len(pt_speech[c]), len(tr_speech[c])) for c in pt_speech if c in tr_speech and len(pt_speech[c]) != len(tr_speech[c])]
    same_lines = [(c, line) for c in pt_speech if c in tr_speech for line in tr_speech[c]
                  if line in pt_speech[c] and line not in SPEECH_IDENTICAL_OK]
    empty_lines = [c for c in tr_speech if not tr_speech[c]]
    print('   categorias ausentes: %d%s' % (len(miss_cat), (' -> ' + ', '.join(miss_cat)) if miss_cat else ''))
    print('   categorias sobrando: %d%s' % (len(extra_cat), (' -> ' + ', '.join(extra_cat)) if extra_cat else ''))
    print('   categorias vazias: %d%s' % (len(empty_lines), (' -> ' + ', '.join(empty_lines)) if empty_lines else ''))
    print('   categorias com contagem diferente: %d%s' % (len(count), (' -> ' + ', '.join('%s %d/%d' % c for c in count)) if count else ''))
    print('   linhas iguais ao PT: %d' % len(same_lines))
    if not QUIET:
        for cat, line in same_lines:
            print('      %s | "%s"' % (cat, line))
    gaps += len(miss_cat) + len(extra_cat) + len(empty_lines) + len(count) + len(same_lines)

    print('== resumo (%s) ==' % lang)
    print('   lacunas: %d' % gaps)
    return 0 if gaps == 0 else 1


if __name__ == '__main__':
    sys.exit(report_language(LANG) if LANG else report())
