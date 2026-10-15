# Writes package/CONFIGURATION.md (PT-BR) from the settings in src/Companions/Core/ModConfig.cs.
# Usage: python3 tools/gen_config_doc.py src/Companions/Core/ModConfig.cs package/CONFIGURATION.md
# The setting DESCRIPTIONS come from ModConfig.cs, so they are translated there (this file only
# holds the page's own text). Section/key names are kept as they are, because they are what the
# player sees in the .cfg file and in the F8 panel tabs.
import io,re,sys
src=io.open(sys.argv[1],encoding='utf-8').read()
out=sys.argv[2]
init=src[src.index('internal static void Init(ConfigFile cfg)'):]
init=init[:init.index('MigrateAndClean(cfg);')]
rows=[]
for m in re.finditer(r'^\t\t\w+ = Bind\(cfg, "([^"]+)", "([^"]+)", (.*)\);$',init,re.M):
    sec,key,rest=m.groups()
    dm=re.match(r'(.*?), "((?:[^"\\]|\\.)*)"(?:, (.*))?$',rest)
    d,desc,rng=dm.group(1),dm.group(2).replace('\\"','"').replace('\\\\','\\'),dm.group(3)
    rows.append((sec,key,d,desc,rng))
def fmt_num(x):
    x=x.strip().rstrip('f')
    try:
        v=float(x)
        return ('%g'%v)
    except: return x
def fmt_default(d):
    d=d.replace('defaultValue:','').strip()
    if d=='true': return 'Ligado'
    if d=='false': return 'Desligado'
    if d.startswith('KeyCode.'): return '`'+d[8:]+'`'
    if d.startswith('@"'):
        v=d[2:-1].replace('""','"').replace('\\\\','\\')
        if len(v)>60: return None
        return '`'+v+'`'
    if d.startswith('"'):
        v=d[1:-1]
        if len(v)>60: return None
        return '`'+v+'`'
    return fmt_num(d)
def fmt_range(r):
    if not r: return ''
    m=re.search(r'\(([^,]+), ([^)]+)\)',r)
    return '%s – %s'%(fmt_num(m.group(1)),fmt_num(m.group(2))) if m else ''
NAMES={'GearRepair':'Gear Repair','RepairAndRestock':'Repair and Restock'}
INTRO={
'General':'O básico: quanto custa um companheiro, a vida, a stamina e os limites de carga dele, e até onde vai a "casa".',
'Controls':'As teclas e até onde os seus comandos alcançam.',
'Follow':'Como os companheiros seguem você.',
'Movement':'Como os companheiros se movem: velocidades, até onde enxergam e como contornam obstáculos (portas, degraus, água, pulos).',
'Combat':'Quando os companheiros lutam e como escolhem arma e escudo.',
'Food':'Quando os companheiros bebem hidroméis.',
'Stamina':'Com que rapidez os companheiros gastam e recuperam stamina.',
'Rest':'Dormir em camas, sentar perto do fogo e o efeito Descansado.',
'Death':'O que acontece quando um companheiro morre e como ele volta.',
'Looting':'Recolher o que um abate deixou no chão.',
'GearRepair':'Consertar as próprias armas, armaduras e ferramentas do companheiro numa bancada.',
'Gathering':'Os trabalhos de Madeira, Pedra, Minério e Forrageio.',
'Hunting':'O trabalho de Caça.',
'Fishing':'O trabalho de Pesca.',
'Farming':'O trabalho de Fazenda.',
'Smelting':'O trabalho de Fundição.',
'Cooking':'O trabalho de Cozinha.',
'RepairAndRestock':'O trabalho de Reparar e Reabastecer (cuidar da sua base).',
'Idle':'O que um companheiro faz quando o trabalho dele não tem nada para fazer.',
'Speech':'Com que frequência os companheiros falam e quanto tempo as falas ficam na tela.',
'Advanced':'Ajustes finos de tempos e distâncias. A maioria dos jogadores nunca precisa mexer nisso.',
'IA':'A camada de IA do fork: ordens por texto e por voz, personalidade, memória e o reconhecimento de voz local.',
}
order=[]
for r in rows:
    if r[0] not in order: order.append(r[0])
L=[]
L.append('# ModAI-BR-Companions — Referência de configurações\n')
L.append('Todas as configurações do mod: o que cada uma faz e com que valor ela começa.\n')
L.append('**Onde mudar:** no jogo, aperte `F8` para abrir o painel de configurações. Você também pode editar `BepInEx/config/Maddhattr.OfflineCompanionsReborn.cfg` com qualquer editor de texto, com o jogo fechado.\n')
L.append('**Dicas**\n')
L.append('- Uma opção mostrada como fração (como `0.5`) significa 50%. `0.25` é 25%, `1` é 100%.')
L.append('- As distâncias estão em metros. Para comparar: um jogador tem cerca de 2 m de altura e o alcance de construção de uma bancada é de 20 m.')
L.append('- **Opções de lista** usam todas o mesmo formato, separadas por vírgulas:')
L.append('  - `*` = tudo')
L.append('  - `Nome` = só esse')
L.append('  - `-Nome` = nunca esse')
L.append('  - `Nome:Número` = esse, com a sua própria quantidade (`0` = sem limite)')
L.append('  - Os nomes podem ser o nome do item ou da criatura no jogo ou o nome interno (prefab). Os nomes que você pode usar em cozinha, caça e pesca são escritos em `OfflineCompanionsReborn.Cooking.txt`, `.Hunting.txt` e `.Fishing.txt` na pasta de configuração a cada vez que o jogo inicia.')
L.append('- Atualizando de uma versão antiga? As opções que você mudou são movidas para os nomes novos automaticamente na primeira vez que você entra no mundo.')
L.append('- Os nomes das seções abaixo são os mesmos do arquivo `.cfg` (e das abas do painel `F8`, que as mostram em português): `General` = **Geral**, `Controls` = **Controles**, `Follow` = **Seguir**, `Movement` = **Movimento**, `Combat` = **Combate**, `Food` = **Comida**, `Stamina` = **Stamina**, `Rest` = **Descanso**, `Death` = **Morte**, `Looting` = **Saque**, `GearRepair` = **Reparo de Equip.**, `Gathering` = **Coleta**, `Hunting` = **Caça**, `Fishing` = **Pesca**, `Farming` = **Plantio**, `Smelting` = **Fundição**, `Cooking` = **Cozinha**, `RepairAndRestock` = **Reparar e Reabastecer**, `Idle` = **Ócio**, `Speech` = **Fala**, `Advanced` = **Avançado**, `IA` = **IA**.\n')
L.append('## Seções\n')
for s in order:
    L.append('- [%s](#%s)'%(NAMES.get(s,s), NAMES.get(s,s).lower().replace(' ','-')))
L.append('')
for s in order:
    L.append('## %s\n'%NAMES.get(s,s))
    L.append(INTRO.get(s,'')+'\n')
    for sec,key,d,desc,rng in rows:
        if sec!=s: continue
        dv=fmt_default(d)
        meta=[]
        meta.append('Padrão: '+(dv if dv is not None else 'veja abaixo'))
        rr=fmt_range(rng)
        if rr: meta.append('Faixa: '+rr)
        L.append('**%s** — %s  '%(key,' · '.join(meta)))
        L.append(desc)
        if dv is None:
            v=d.strip()[1:-1]
            if key=='MeadFocus': v=v.replace('; ',';\n')
            L.append('\nValor padrão:\n\n```\n%s\n```'%v)
        L.append('')
io.open(out,'w',encoding='utf-8').write('\n'.join(L).rstrip()+'\n')
print(len(rows),'settings',len(order),'sections')
