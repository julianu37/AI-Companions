"""Testes do tools/build_run_report.py (log sintetico, sem jogo).

Por que existe: o relatorio e o instrumento com que TODO run de obra e lido, e ele nao tinha
nenhum teste. O caso que motivou o arquivo (11/11/2026) e o hand-over: o build devolve a mao
(`hc_ai_build_collecting`, `hc_ai_build_leveling`), volta com `- resuming at piece N`, e o
relatorio fechava a janela no `order ended` do hand-over - 255 pecas colocadas ficaram fora do
placar de um run real (114 lidas x 369 de verdade).

Rodar: python tools/test_build_run_report.py
"""

import importlib.util
import os
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))


def load_report():
    spec = importlib.util.spec_from_file_location("build_run_report",
                                                  os.path.join(HERE, "build_run_report.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


REPORT = load_report()

P = "[Info   :AI Companions] [Build] "

# Uma ordem que devolve a mao (coleta) e volta. O `order ended` da coleta NAO e o fim.
HANDOVER_LOG = [
    P + "Companion 2: blueprint BP: 10 pieces near (-180,0, 34,0, -320,0)",
    P + "Companion 2: done: wood_floor at (-180,0, 34,0, -320,0) yaw 0 supported",
    P + "Companion 2: done: wood_floor at (-178,0, 34,0, -320,0) yaw 0 supported",
    P + "Companion 2: order ended: hc_ai_build_collecting (arrived at the resource)",
    P + "Companion 2: blueprint BP: 10 pieces near (-180,0, 34,0, -320,0) - resuming at piece 3",
    P + "Companion 2: done: wood_wall at (-178,0, 35,0, -320,0) yaw 0 supported",
    P + "Companion 2: done: wood_wall at (-176,0, 35,0, -320,0) yaw 0 supported",
    P + "Companion 2: order ended: hc_ai_blueprint_done",
]

# A mesma obra, sem devolver a mao: o caso normal, que ja funcionava.
PLAIN_LOG = [
    P + "Companion 2: blueprint BP: 10 pieces near (-180,0, 34,0, -320,0)",
    P + "Companion 2: done: wood_floor at (-180,0, 34,0, -320,0) yaw 0 supported",
    P + "Companion 2: order ended: hc_ai_blueprint_done",
]

# Devolveu a mao e o log acaba ali: a ordem esta RODANDO, nao encerrada.
RUNNING_LOG = [
    P + "Companion 2: blueprint BP: 10 pieces near (-180,0, 34,0, -320,0)",
    P + "Companion 2: done: wood_floor at (-180,0, 34,0, -320,0) yaw 0 supported",
    P + "Companion 2: order ended: hc_ai_build_collecting (arrived at the resource)",
    P + "Companion 2: blueprint BP: 10 pieces near (-180,0, 34,0, -320,0) - resuming at piece 2",
    P + "Companion 2: done: wood_wall at (-178,0, 35,0, -320,0) yaw 0 supported",
]


class ReportCase(unittest.TestCase):

    def report(self, lines):
        with tempfile.NamedTemporaryFile("w", suffix=".log", delete=False,
                                         encoding="utf-8", newline="\n") as fh:
            fh.write("\n".join(lines) + "\n")
            path = fh.name
        try:
            blocks, _total, _outside = REPORT.scan(path)
            return [REPORT.finalize(b, i + 1, 10.0) for i, b in enumerate(blocks)]
        finally:
            os.unlink(path)

    def test_handover_does_not_end_the_order(self):
        """O `order ended` do hand-over nao fecha a janela; o fim e o da retomada."""
        reports = self.report(HANDOVER_LOG)
        self.assertEqual(len(reports), 1, "a retomada e a MESMA ordem, nao uma nova")
        order = reports[0]
        self.assertEqual(order["status"], "done")
        self.assertEqual(order["end"]["key"], "hc_ai_blueprint_done")
        self.assertEqual(order["placed"], 4,
                         "as pecas das duas metades contam (2 antes + 2 depois do hand-over)")

    def test_plain_order_is_unchanged(self):
        """Sem retomada nada muda: o caso normal nao pode regredir com a correcao."""
        order = self.report(PLAIN_LOG)[0]
        self.assertEqual(order["status"], "done")
        self.assertEqual(order["end"]["key"], "hc_ai_blueprint_done")
        self.assertEqual(order["placed"], 1)

    def test_handover_before_the_end_of_the_log_is_running(self):
        """Retomada e log cortado: a ordem esta em andamento, e o placar e parcial."""
        order = self.report(RUNNING_LOG)[0]
        self.assertIsNone(order["end"], "nenhum `order ended` DEPOIS da retomada")
        self.assertEqual(order["status"], "running")
        self.assertEqual(order["placed"], 2)

    def test_resume_lines_are_recorded(self):
        """A linha de retomada e guardada: e ela que distingue hand-over de fim de ordem."""
        with tempfile.NamedTemporaryFile("w", suffix=".log", delete=False,
                                         encoding="utf-8", newline="\n") as fh:
            fh.write("\n".join(HANDOVER_LOG) + "\n")
            path = fh.name
        try:
            blocks, _total, _outside = REPORT.scan(path)
        finally:
            os.unlink(path)
        self.assertEqual(blocks[0].resumes, [5])
        self.assertEqual([e[1] for e in blocks[0].ends],
                         ["hc_ai_build_collecting", "hc_ai_blueprint_done"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
