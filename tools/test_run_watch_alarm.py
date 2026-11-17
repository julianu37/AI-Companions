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
        # the 16/11 case: the same -1 held for 20 samples. It speaks on the third one in a row, not the first.
        self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 984}, None))
        self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 984}, -1))
        line = W.gap_alarm({"done": 985, "in_scene": 984}, -2)
        self.assertIn("ALARME", line)
        self.assertIn("-1", line)

    def test_alarm_keeps_speaking_while_the_gap_holds(self):
        # a hole that stays open is worth one line per sample after the third: it is not noise any more
        self.assertIsNotNone(W.gap_alarm({"done": 985, "in_scene": 984}, -3))

    def test_gap_changing_sign_starts_over(self):
        # +4 then -4 are not the same story: the streak resets, so a flapping pair never becomes an alarm
        self.assertIsNone(W.gap_alarm({"done": 758, "in_scene": 762}, -2))
        self.assertIsNone(W.gap_alarm({"done": 762, "in_scene": 758}, 1))

    def test_missing_counters_produce_no_alarm(self):
        self.assertIsNone(W.gap_alarm({"done": None, "in_scene": None}, None))

    def test_one_sighting_is_a_race_not_an_alarm(self):
        # measured 17/11/2026: a healthy run showed +3, +4, +7 once each (the piece exists before its `done:` line);
        # the longest the same gap ever held was 2 samples. The backlog case of 16/11 held for 20.
        # previous_gap is the counter the caller keeps: the streak with its sign (3 = third sample of +gap).
        self.assertIsNone(W.gap_alarm({"done": 758, "in_scene": 762}, None))   # 1st sighting -> streak 1, silent
        self.assertIsNone(W.gap_alarm({"done": 758, "in_scene": 762}, 1))      # 2nd -> streak 2, silent
        self.assertIsNotNone(W.gap_alarm({"done": 758, "in_scene": 762}, 2))   # 3rd -> streak 3, speaks


if __name__ == "__main__":
    unittest.main()
