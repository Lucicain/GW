"""Read the BOM-bearing files produced by the diagnostic logger and rollover."""
import importlib.util
import contextlib
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location(
    "firesworn_logs", Path(__file__).resolve().parents[1] / "Analyze-Firesworn.py")
analyzer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(analyzer)


class FireswornLogTests(unittest.TestCase):
    def test_duplicate_rollover_lines_count_once(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "GreyWarden-Faults.log").write_text(
                "2020-01-02T00:00:00+11:00 | FAULT_WATCH_ARMED | build=test\n",
                encoding="utf-8-sig")
            archive = path / "GreyWarden-Diagnostics-Archive"
            archive.mkdir()
            row = "2020-01-02T01:00:00+11:00 | campaignHour=3 | FIRESWORN | stage=MARKET_HOME | goldToTreasury=50\n"
            (archive / "GreyWarden-AI-Diagnostics.log.1").write_text(row, encoding="utf-8-sig")
            (path / "GreyWarden-AI-Diagnostics.log").write_text(row, encoding="utf-8-sig")
            with patch.object(analyzer, "DOCS", directory):
                self.assertEqual(1, len(analyzer.read_events(False)))

    def test_current_market_cash_is_not_reported_as_zero_sales(self):
        events = [
            (1, "MARKET_DISPATCHED", {"cargoValue": "1000"}),
            (2, "MARKET_HOME", {"goldToTreasury": "250", "nativeTradeGold": "900", "unsoldReturned": "3"}),
        ]
        output = io.StringIO()
        with patch.object(analyzer, "read_events", return_value=events), patch("sys.argv", ["analyze"]), contextlib.redirect_stdout(output):
            analyzer.main()
        report = output.getvalue()
        self.assertIn("村民队灰袍货款入公库: 250", report)
        self.assertIn("灰袍余货归仓 3 件", report)
        self.assertNotIn("实卖 0", report)
        self.assertNotIn("只卖掉装车估值", report)
        self.assertNotIn("公库: 1150", report)

    def test_bom_in_current_log_archive_and_launch_marker(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "GreyWarden-Faults.log").write_text(
                "2020-01-02T00:00:00.0000000+11:00 | FAULT_WATCH_ARMED | build=test\n",
                encoding="utf-8-sig")
            archive = path / "GreyWarden-Diagnostics-Archive"
            archive.mkdir()
            (archive / "GreyWarden-AI-Diagnostics.log.1").write_text(
                "2020-01-01T00:00:00.0000000+11:00 | campaignHour=1 | FIRESWORN | stage=MISSION_DISPATCHED | home=v; men=10\n"
                "2020-01-02T00:00:00.0000000+11:00 | campaignHour=2 | FIRESWORN | stage=MISSION_DISPATCHED | home=v; men=10\n",
                encoding="utf-8-sig")
            (path / "GreyWarden-AI-Diagnostics.log").write_text(
                "2020-01-02T01:00:00.0000000+11:00 | campaignHour=3 | FIRESWORN | stage=MISSION_DISPATCHED | home=v; men=10\n",
                encoding="utf-8-sig")
            with patch.object(analyzer, "DOCS", directory):
                self.assertEqual([2, 3], [hour for hour, _, _ in analyzer.read_events(False)])
                self.assertEqual([1, 2, 3], [hour for hour, _, _ in analyzer.read_events(True)])


if __name__ == "__main__":
    unittest.main()
