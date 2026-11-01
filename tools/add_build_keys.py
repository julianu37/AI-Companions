# Adds the Trilha E (build) keys to the five Translations files, right after hc_ai_collect_full,
# keeping the file's formatting (4 spaces, one entry per line) and its line endings.
import io, json, os

KEYS = {
    "Portuguese_Brazilian": [
        ("hc_ai_order_build", "construir {0}"),
        ("hc_ai_build_disabled", "Não posso construir agora: a construção está desligada."),
        ("hc_ai_build_no_recipe", "Não sei construir isso."),
        ("hc_ai_build_not_allowed", "Ainda não sei construir isso; por enquanto sei fazer bancada, baú, fogueira e cama."),
        ("hc_ai_build_missing", "Faltam {0} para a {1}."),
        ("hc_ai_build_no_site", "Não achei lugar para a {0} aqui."),
        ("hc_ai_build_done", "{0} no lugar."),
        ("hc_ai_build_failed", "Não consegui construir a {0}."),
        ("hc_ai_build_needs_station", "Preciso de uma {0} perto."),
        ("hc_ai_build_collecting", "Vou buscar {0}."),
        ("hc_cfg_building_enabled", "Ligado"),
        ("hc_cfg_building_sitesearchradius", "Raio da busca (m)"),
        ("hc_cfg_building_maxdistancefromhome", "Distância máxima da casa (m)"),
    ],
    "Spanish": [
        ("hc_ai_order_build", "construir {0}"),
        ("hc_ai_build_disabled", "No puedo construir ahora: la construcción está desactivada."),
        ("hc_ai_build_no_recipe", "No sé construir eso."),
        ("hc_ai_build_not_allowed", "Todavía no sé construir eso; por ahora sé hacer mesa de trabajo, cofre, fogata y cama."),
        ("hc_ai_build_missing", "Faltan {0} para la {1}."),
        ("hc_ai_build_no_site", "No encontré un lugar para la {0} aquí."),
        ("hc_ai_build_done", "{0} en su sitio."),
        ("hc_ai_build_failed", "No pude construir la {0}."),
        ("hc_ai_build_needs_station", "Necesito una {0} cerca."),
        ("hc_ai_build_collecting", "Voy a buscar {0}."),
        ("hc_cfg_building_enabled", "Activado"),
        ("hc_cfg_building_sitesearchradius", "Radio de búsqueda (m)"),
        ("hc_cfg_building_maxdistancefromhome", "Distancia máxima de casa (m)"),
    ],
    "French": [
        ("hc_ai_order_build", "construire {0}"),
        ("hc_ai_build_disabled", "Je ne peux pas construire maintenant : la construction est désactivée."),
        ("hc_ai_build_no_recipe", "Je ne sais pas construire ça."),
        ("hc_ai_build_not_allowed", "Je ne sais pas encore construire ça ; pour l'instant je sais faire un établi, un coffre, un feu de camp et un lit."),
        ("hc_ai_build_missing", "Il manque {0} pour {1}."),
        ("hc_ai_build_no_site", "Je n'ai pas trouvé de place pour {0} ici."),
        ("hc_ai_build_done", "{0} est en place."),
        ("hc_ai_build_failed", "Je n'ai pas réussi à construire {0}."),
        ("hc_ai_build_needs_station", "J'ai besoin de {0} à côté."),
        ("hc_ai_build_collecting", "Je vais chercher {0}."),
        ("hc_cfg_building_enabled", "Activé"),
        ("hc_cfg_building_sitesearchradius", "Rayon de recherche (m)"),
        ("hc_cfg_building_maxdistancefromhome", "Distance max de la maison (m)"),
    ],
    "Italian": [
        ("hc_ai_order_build", "costruire {0}"),
        ("hc_ai_build_disabled", "Non posso costruire adesso: la costruzione è disattivata."),
        ("hc_ai_build_no_recipe", "Non so costruire questo."),
        ("hc_ai_build_not_allowed", "Non so ancora costruire questo; per ora so fare banco da lavoro, cassa, falò e letto."),
        ("hc_ai_build_missing", "Mancano {0} per {1}."),
        ("hc_ai_build_no_site", "Non ho trovato un posto per {0} qui."),
        ("hc_ai_build_done", "{0} al suo posto."),
        ("hc_ai_build_failed", "Non sono riuscito a costruire {0}."),
        ("hc_ai_build_needs_station", "Mi serve {0} qui vicino."),
        ("hc_ai_build_collecting", "Vado a prendere {0}."),
        ("hc_cfg_building_enabled", "Attivo"),
        ("hc_cfg_building_sitesearchradius", "Raggio di ricerca (m)"),
        ("hc_cfg_building_maxdistancefromhome", "Distanza massima da casa (m)"),
    ],
}

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
root = os.path.join(root, "..")
root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

for language, pairs in KEYS.items():
    path = os.path.join(root, "Translations", language + ".json")
    with io.open(path, encoding="utf-8", newline="") as handle:
        text = handle.read()
    nl = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(nl)
    anchor = None
    for i, line in enumerate(lines):
        if '"hc_ai_collect_full"' in line:
            anchor = i
            break
    if anchor is None:
        raise SystemExit("anchor not found in " + path)
    # already added? do not duplicate
    existing = {json.loads(line.strip().rstrip(","))["key"] for line in lines if line.strip().startswith('{ "key"')}
    added = []
    for key, value in pairs:
        if key in existing:
            continue
        added.append('        { "key": ' + json.dumps(key) + ', "value": ' + json.dumps(value, ensure_ascii=False) + ' },')
    lines[anchor + 1:anchor + 1] = added
    with io.open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(nl.join(lines))
    # validate
    data = json.loads(io.open(path, encoding="utf-8").read())
    keys = [entry["key"] for entry in data["entries"]]
    print("%-22s +%d -> %d chaves (duplicadas: %d)" % (language, len(added), len(keys), len(keys) - len(set(keys))))
