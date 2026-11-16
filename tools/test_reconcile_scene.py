"""Testes do tools/reconcile_scene.py (cena sintetica, sem jogo).

Por que existe: `reconcile_scene.py` e a PROVA de aceite de todo run de obra -- o relatorio conta
intencao e a cena conta peca. Um bug nele reprova um run bom ou, pior, aprova um run com buraco.

O caso que motivou o arquivo (16/11/2026): a reconciliacao do run de restauracao disse
`faltantes sem explicacao: 0` e exit 0 com uma peca fora do lugar de verdade. Duas causas, as duas
cobertas aqui:

1. `explain_missing` comparava a coordenada **local** do plano com a **mundial** da cena. As duas
   pontas ficam a centenas de metros uma da outra, entao nenhuma faltante tinha "extra por perto" --
   mas o teste nunca reprovava porque...
2. ...o dicionario gravava `best` em vez do `near` que acabara de calcular, entao `is None` no
   `main()` nunca era verdade e TODA faltante virava "explicada".

Rodar: python tools/test_reconcile_scene.py
"""

import importlib.util
import math
import os
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))


def load_module():
    spec = importlib.util.spec_from_file_location("reconcile_scene",
                                                  os.path.join(HERE, "reconcile_scene.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


R = load_module()


def build(plan_pieces, yaw_deg, tx, ty, tz):
    """A scene that is EXACTLY the plan under a known transform.

    The names carry `(Clone)` because Unity names instances that way in the real payload; they go
    through `normalise` exactly as `load_scene` does, which is the step the 15/11/2026 run was stuck
    on (raw comparison yields zero pairs at every yaw).
    """
    yaw = math.radians(yaw_deg)
    out = []
    for piece in plan_pieces:
        x, y, z = R.transform(piece, yaw, tx, ty, tz)
        out.append({"prefab": R.normalise(piece["prefab"] + "(Clone)"), "x": x, "y": y, "z": z})
    return out


def square(prefab="wood_floor", step=2.0, y=0.0, n=4):
    """An asymmetric L-shaped plan -- deliberately NOT a full grid.

    A symmetric grid is invariant under 90 degree rotations, so the seed-free sweep finds several
    candidate yaws with identical vote counts and the fixture (not the script) decides the winner.
    Real blueprints are asymmetric, so the fixture has to be too.
    """
    return [{"prefab": prefab, "x": i * step, "y": y, "z": j * step}
            for i in range(n) for j in range(n) if i + j < n + 1]


class ReconcileTests(unittest.TestCase):
    def test_exact_scene_matches_everything(self):
        plan = square()
        scene = build(plan, 344.0, -158.9, 38.8, -427.7)
        count, yaw, tx, ty, tz, pairs, _ = R.sweep(plan, scene, 0.45)
        count, yaw, tx, ty, tz = R.polish(plan, scene, yaw, tx, ty, tz, 0.45)
        pairs, _ = R.match(plan, scene, yaw, tx, ty, tz, 0.45)
        missing, _ = R.explain_missing(plan, scene, pairs, 0.45, yaw, tx, ty, tz)
        self.assertEqual(len(pairs), len(plan))
        self.assertEqual(missing, [])

    def test_missing_piece_away_from_any_extra_is_not_explained(self):
        """A piece displaced by a whole storey must FAIL the run.

        This is the run of 16/11/2026: the beam belongs at world y=45,0 and the scene has it at
        y=38,8 (`log ajustado ... - falls`). It is a real hole in the upper floor.
        """
        plan = square()
        scene = build(plan, 344.0, -158.9, 38.8, -427.7)
        # move the first beam down 6,2 m -- the measured displacement of the fatal fall
        scene[0]["y"] -= 6.2
        count, yaw, tx, ty, tz, pairs, _ = R.sweep(plan, scene, 0.45)
        count, yaw, tx, ty, tz = R.polish(plan, scene, yaw, tx, ty, tz, 0.45)
        pairs, _ = R.match(plan, scene, yaw, tx, ty, tz, 0.45)
        missing, _ = R.explain_missing(plan, scene, pairs, 0.45, yaw, tx, ty, tz)
        unexplained = [m for m in missing if m["extra_same_prefab_m"] is None]
        self.assertEqual(len(unexplained), 1, "a beam a storey below is a missing beam")
        # and the distance is reported in WORLD space, not local-vs-world
        self.assertLess(unexplained[0]["nearest_same_prefab_m"], 7.0)
        self.assertGreater(unexplained[0]["nearest_same_prefab_m"], 3.0 * 0.45)

    def test_extra_nearby_explains_the_buried_foundation(self):
        """The documented case: a planned piece that stood 0,7 m away is not a hole."""
        plan = square()
        scene = build(plan, 344.0, -158.9, 38.8, -427.7)
        scene[0]["y"] += 0.7    # the ground is higher here
        count, yaw, tx, ty, tz, pairs, _ = R.sweep(plan, scene, 0.45)
        count, yaw, tx, ty, tz = R.polish(plan, scene, yaw, tx, ty, tz, 0.45)
        pairs, _ = R.match(plan, scene, yaw, tx, ty, tz, 0.45)
        missing, extras = R.explain_missing(plan, scene, pairs, 0.45, yaw, tx, ty, tz)
        self.assertEqual(len(missing), 1)
        self.assertIsNotNone(missing[0]["extra_same_prefab_m"])
        self.assertLess(missing[0]["extra_same_prefab_m"], 1.0)

    def test_a_far_extra_does_not_explain(self):
        """`nearest_same_prefab_m` is always measured; only `extra_same_prefab_m` gates the run."""
        plan = square()
        scene = build(plan, 344.0, -158.9, 38.8, -427.7)
        scene[0]["y"] -= 6.2
        count, yaw, tx, ty, tz, pairs, _ = R.sweep(plan, scene, 0.45)
        count, yaw, tx, ty, tz = R.polish(plan, scene, yaw, tx, ty, tz, 0.45)
        pairs, _ = R.match(plan, scene, yaw, tx, ty, tz, 0.45)
        missing, _ = R.explain_missing(plan, scene, pairs, 0.45, yaw, tx, ty, tz)
        for item in missing:
            if item["nearest_same_prefab_m"] is None:
                continue
            if item["nearest_same_prefab_m"] > 3.0 * 0.45:
                self.assertIsNone(item["extra_same_prefab_m"])

    def test_prefab_clone_suffix_is_normalised(self):
        self.assertEqual(R.normalise("wood_floor(Clone)"), "wood_floor")
        self.assertEqual(R.normalise("wood_floor (Clone)"), "wood_floor")
        self.assertEqual(R.normalise("wood_floor"), "wood_floor")


if __name__ == "__main__":
    unittest.main(verbosity=2)
