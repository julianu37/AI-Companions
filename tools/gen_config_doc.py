# Writes package/CONFIGURATION.md (PT-BR page, English settings) from the settings in src/Companions/Core/ModConfig.cs.
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
    if d=='true': return 'On'
    if d=='false': return 'Off'
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
    if 'AcceptableValueList' in r:
        return 'Options: '+', '.join('`%s`'%v for v in re.findall(r'"([^"]*)"',r))
    m=re.search(r'\(([^,]+), ([^)]+)\)',r)
    return '%s – %s'%(fmt_num(m.group(1)),fmt_num(m.group(2))) if m else ''
NAMES={'GearRepair':'Gear Repair','RepairAndRestock':'Repair and Restock'}
INTRO={
'General':'The basics: what a companion costs, its health, stamina and carry limits, and how far "home" reaches.',
'Controls':'The keys and how far your orders reach.',
'Follow':'How companions follow you.',
'Movement':'How companions move: speeds, how far they see and how they get around obstacles (doors, steps, water, jumps).',
'Combat':'When companions fight and how they pick weapon and shield.',
'Food':'When companions drink meads.',
'Stamina':'How fast companions spend and recover stamina.',
'Rest':'Sleeping in beds, sitting by the fire and the Rested effect.',
'Death':'What happens when a companion dies and how it comes back.',
'Looting':'Picking up what a kill left on the ground.',
'GearRepair':"Repairing the companion's own weapons, armour and tools at a bench.",
'Gathering':'The Wood, Stone, Ore and Foraging jobs.',
'Hunting':'The Hunting job.',
'Fishing':'The Fishing job.',
'Farming':'The Farming job.',
'Smelting':'The Smelting job.',
'Cooking':'The Cooking job.',
'RepairAndRestock':'The Repair and Restock job (taking care of your base).',
'Idle':'What a companion does when its job has nothing to do.',
'Speech':'How often companions speak and how long the lines stay on screen.',
'Advanced':'Fine tuning of timers and distances. Most players never need to touch it.',
'AI':"The AI layer of the project: text and voice orders, personality, memory, speech recognition and the companion's voice.",
}
order=[]
for r in rows:
    if r[0] not in order: order.append(r[0])
L=[]
L.append('# AI Companions — Configuration reference\n')
L.append('Every setting of the mod: what it does and the value it starts with.\n')
L.append('**Where to change it:** in game, press `F8` to open the settings panel. You can also edit `BepInEx/config/AiCompanions.cfg` with any text editor, with the game closed.\n')
L.append('**Tips**\n')
L.append('- An option shown as a fraction (like `0.5`) means 50%. `0.25` is 25%, `1` is 100%.')
L.append('- Distances are in meters. For scale: a player is about 2 m tall and a workbench build range is 20 m.')
L.append('- **List options** all use the same format, separated by commas:')
L.append('  - `*` = everything')
L.append('  - `Name` = only that one')
L.append('  - `-Name` = never that one')
L.append('  - `Name:Number` = that one, with its own amount (`0` = no limit)')
L.append('  - Names can be the item or creature name in game or the internal name (prefab). The names you can use in cooking, hunting and fishing are written to `OfflineCompanionsReborn.Cooking.txt`, `.Hunting.txt` and `.Fishing.txt` in the config folder every time the game starts.')
L.append('- Updating from an old version? The options you changed are moved to the new names automatically the first time you enter a world. **Since 1.8.3** the file is `AiCompanions.cfg` and the keys and descriptions are in English (the native language of the mod; Portuguese is a translation): the old `.cfg` (`Maddhattr.OfflineCompanionsReborn.cfg`) is ignored and the settings go back to their default values.')
L.append('- The section names below are the same as in the `.cfg` file (the `F8` panel shows them translated to the game language): `General` = **Geral**, `Controls` = **Controles**, `Follow` = **Seguir**, `Movement` = **Movimento**, `Combat` = **Combate**, `Food` = **Comida**, `Stamina` = **Stamina**, `Rest` = **Descanso**, `Death` = **Morte**, `Looting` = **Saque**, `GearRepair` = **Reparo de Equip.**, `Gathering` = **Coleta**, `Hunting` = **Caça**, `Fishing` = **Pesca**, `Farming` = **Plantio**, `Smelting` = **Fundição**, `Cooking` = **Cozinha**, `RepairAndRestock` = **Reparar e Reabastecer**, `Idle` = **Ócio**, `Speech` = **Fala**, `Advanced` = **Avançado**, `AI` = **IA**.\n')
L.append('## Sections\n')
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
        meta.append('Default: '+(dv if dv is not None else 'see below'))
        rr=fmt_range(rng)
        if rr: meta.append(rr if rr.startswith('Options') else 'Range: '+rr)
        L.append('**%s** — %s  '%(key,' · '.join(meta)))
        L.append(desc)
        if dv is None:
            v=d.strip()[1:-1]
            if key=='MeadFocus': v=v.replace('; ',';\n')
            L.append('\nDefault value:\n\n```\n%s\n```'%v)
        L.append('')
io.open(out,'w',encoding='utf-8').write('\n'.join(L).rstrip()+'\n')
print(len(rows),'settings',len(order),'sections')
