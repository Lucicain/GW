"""Read the BOM-bearing files produced by the diagnostic logger and rollover."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location(
    "firesworn_logs", Path(__file__).resolve().parents[1] / "Analyze-Firesworn.py")
analyzer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(analyzer)


class FireswornLogTests(unittest.TestCase):
    def test_bom_in_current_log_archive_and_launch_marker(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "GreyWarden-Faults.log").write_text(
                "2020-01-02T00:00:00.0000000+11:00 | FAULT_WATCH_ARMED | build=test\n",
                encoding="utf-8-sig")
            archive = path / "GreyWarden-Diagnostics-Archive"
            archive.mkdir()
            (archive / "GreyWarden-AI-Diagnostics.log.1").write_text(
                "2020-01-01T00:00:00.0000000+11:00 | campaignHour=1 | FIRESWORN | stage=PATROL_DISPATCHED | home=v; men=10\n"
                "2020-01-02T00:00:00.0000000+11:00 | campaignHour=2 | FIRESWORN | stage=PATROL_DISPATCHED | home=v; men=0\n",
                encoding="utf-8-sig")
            (path / "GreyWarden-AI-Diagnostics.log").write_text(
                "2020-01-02T01:00:00.0000000+11:00 | campaignHour=3 | FIRESWORN | stage=PATROL_DISPATCHED | home=v; men=0\n",
                encoding="utf-8-sig")
            with patch.object(analyzer, "DOCS", directory):
                self.assertEqual([2, 3], [hour for hour, _, _ in analyzer.read_events(False)])
                self.assertEqual([1, 2, 3], [hour for hour, _, _ in analyzer.read_events(True)])


if __name__ == "__main__":
    unittest.main()
