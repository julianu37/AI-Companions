"""Testes do alarme de contador do tools/mcp-devbridge/run_watch.py (sem jogo).

Por que existe: no run de 16/11/2026 o sampler viu `done=985 in_scene=984` em 20 amostras
seguidas e o -1 foi lido como ruido - a peca orfa so apareceu no reconcile, no fim.

As fixtures sao os runs GRAVADOS, nao linhas inventadas: um alarme que fala no run saudavel
e tao inutil quanto um que fica calado no run com buraco, e so o arquivo real prova os dois.

Rodar: python tools/test_run_watch_alarm.py
"""

import importlib.util
import json
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


def load_rows(name):
    path = os.path.join(ADAPTER_DIR, name)
    with open(path, encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def alarms(rows):
    """Replay the samples through the alarm exactly the way the loop does."""
    state = {"streak": 0, "spoken": False}
    return [line for line in (W.gap_alarm(row, state) for row in rows) if line is not None]


W = load_watch()


class Rule(unittest.TestCase):
    def test_no_alarm_when_the_pair_agrees(self):
        self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 985}, {"streak": 0, "spoken": False}))

    def test_the_scene_ahead_is_a_race_not_a_hole(self):
        # measured 17/11/2026: a healthy run shows +1..+7 once each (the piece exists before its `done:` line)
        state = {"streak": 0, "spoken": False}
        for gap in (3, 2, 3, 4, 7, 1):
            self.assertIsNone(W.gap_alarm({"done": 100, "in_scene": 100 + gap}, state))

    def test_the_first_sighting_of_a_hole_speaks(self):
        # measured 17/11/2026 (final run): the hole showed as gap -1 in TWO samples and the 3-in-a-row threshold
        # missed it. A false alarm costs a log line; a silent hole costs a house with a piece missing.
        state = {"streak": 0, "spoken": False}
        line = W.gap_alarm({"done": 985, "in_scene": 984}, state)
        self.assertIn("ALARME", line)
        self.assertIn("-1", line)

    def test_it_speaks_once_per_episode_not_per_sample(self):
        state = {"streak": 0, "spoken": False}
        self.assertIsNotNone(W.gap_alarm({"done": 985, "in_scene": 984}, state))
        for _ in range(5):
            self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 984}, state))

    def test_it_speaks_again_after_the_pair_agrees_and_breaks_again(self):
        state = {"streak": 0, "spoken": False}
        self.assertIsNotNone(W.gap_alarm({"done": 985, "in_scene": 984}, state))
        self.assertIsNone(W.gap_alarm({"done": 985, "in_scene": 985}, state))
        self.assertIsNotNone(W.gap_alarm({"done": 985, "in_scene": 984}, state))

    def test_missing_counters_produce_no_alarm(self):
        self.assertIsNone(W.gap_alarm({"done": None, "in_scene": None}, {"streak": 0, "spoken": False}))


class RecordedRuns(unittest.TestCase):
    """The two runs of 17/11/2026, replayed from the files the sampler wrote."""

    def test_the_healthy_run_is_silent(self):
        rows = load_rows("run-resumecheck-slices.jsonl")
        self.assertGreater(len(rows), 50, "fixture missing or too small")
        self.assertEqual([], alarms(rows), "o run que fechou 985/985 nao pode alarmar")

    def test_the_run_that_had_the_defect_does_alarm(self):
        # this is the FIRST run of 17/11, the one that exposed the 3D-sphere bug: 22 pieces returned, all false,
        # and 87 samples with the scene short of the counter. The alarm has to speak on it.
        rows = load_rows("run-resumecheck-clean.jsonl")
        self.assertGreater(len(rows), 50, "fixture missing or too small")
        found = alarms(rows)
        self.assertEqual(1, len(found), "um episodio de buraco = uma linha, nao uma por amostra")
        self.assertIn("ALARME", found[0])

    def test_a_recorded_hole_does_alarm(self):
        # the 16/11 case rebuilt from its own numbers: the same -1 for 20 samples in a row
        rows = [{"t": "15:00:%02d" % i, "done": 985, "in_scene": 984} for i in range(20)]
        self.assertEqual(1, len(alarms(rows)), "o buraco de 16/11 tem de falar UMA vez")


if __name__ == "__main__":
    unittest.main()
