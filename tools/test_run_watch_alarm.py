"""Testes do alarme de contador do tools/mcp-devbridge/run_watch.py (sem jogo).

Por que existe: no run de 16/11/2026 o sampler viu `done=985 in_scene=984` em 20 amostras
seguidas e o -1 foi lido como ruido - a peca orfa so apareceu no reconcile, no fim, como
formalidade de aceite. O alarme e a rede que faltava: enquanto o run acontece, o contador
do mod e a cena tem de concordar.

Rodar: python tools/test_run_watch_alarm.py
"""

import importlib.util
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ADAPTER_DIR = os.path.join(HERE, "mcp-devbridge")
WATCH = os.path.join(ADAPTER_DIR, "run_watch.py")


def load_watch():
    # run_watch imports its sibling mcp_devbridge, so the adapter directory has to be on the path
    if ADAPTER_DIR not in sys.path:
        sys.path.insert(0, ADAPTER_DIR)
    spec = importlib.util.spec_from_file_location("run_watch", WATCH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


W = load_watch()


class Alarm(unittest.TestCase):
    def test_no_alarm_when_the_pair_agrees(self):
        self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 985}, None))

    def test_alarm_on_the_measured_gap(self):
        line = W.gap_alarm({"done": 985, "in_scene": 984}, None)
        self.assertIn("ALARME", line)
        self.assertIn("-1", line)

    def test_alarm_fires_once_while_the_gap_holds(self):
        # first sighting: nothing was reported before, so it speaks
        self.assertIsNotNone(W.gap_alarm({"done": 985, "in_scene": 984}, None))
        # the same gap on the next samples: silent (one line, not one per sample)
        self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 984}, -1))
        self.assertIsNone(W.gap_alarm({"done": 986, "in_scene": 985}, -1))

    def test_alarm_fires_again_when_the_gap_worsens(self):
        self.assertIsNotNone(W.gap_alarm({"done": 985, "in_scene": 982}, -1))

    def test_missing_counters_produce_no_alarm(self):
        self.assertIsNone(W.gap_alarm({"done": None, "in_scene": None}, None))


if __name__ == "__main__":
    unittest.main()
