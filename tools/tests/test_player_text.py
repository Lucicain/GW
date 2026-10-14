"""Regressions for the player-text editing workflow and missing-variable rejection."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


spec = importlib.util.spec_from_file_location("player_text", Path(__file__).resolve().parents[1] / "Generate-PlayerText.py")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class PlayerTextTests(unittest.TestCase):
    def fixture(self, directory):
        root = Path(directory)
        module = root / "GreyWardenPolicePurity"
        text = module / "PlayerText"
        text.mkdir(parents=True)
        cn = module / "_Module/ModuleData/Languages/CNs/strings.xml"
        cn.parent.mkdir(parents=True)
        cn.write_text('<base><strings><string id="gwp_greeting" text="旧文" /></strings></base>', encoding="utf-8")
        source = module / "Greeting.cs"
        source.write_text('var line = GwpTextCatalog.gwp_greeting;', encoding="utf-8")
        (text / "strings.json").write_text(json.dumps({"strings": {
            "gwp_greeting": {"en": "Hello, {NAME}.", "zh": "你好，{NAME}。"}}, "fragments": {}}), encoding="utf-8")
        return root, module, text, cn, source

    def run_fixture(self, fixture, check=False):
        root, module, text, cn, _ = fixture
        with patch.multiple(generator, ROOT=root, MODULE=module, TEXT=text, CN=cn,
                            CATALOG=module / "GwpTextCatalog.g.cs"), contextlib.redirect_stdout(io.StringIO()):
            return generator.generate(check)

    def test_edit_updates_both_native_outputs_and_stale_check_is_read_only(self):
        with tempfile.TemporaryDirectory() as directory:
            f = self.fixture(directory)
            self.assertEqual(0, self.run_fixture(f))
            self.assertIn('{=gwp_greeting}Hello, {NAME}.', (f[1] / 'GwpTextCatalog.g.cs').read_text(encoding='utf-8'))
            self.assertIn('你好，{NAME}。', f[3].read_text(encoding='utf-8'))
            data = json.loads((f[2] / 'strings.json').read_text(encoding='utf-8'))
            data['strings']['gwp_greeting']['en'] = 'Welcome, {NAME}.'
            (f[2] / 'strings.json').write_text(json.dumps(data), encoding='utf-8')
            previous = (f[1] / 'GwpTextCatalog.g.cs').read_bytes()
            self.assertEqual(1, self.run_fixture(f, True))
            self.assertEqual(previous, (f[1] / 'GwpTextCatalog.g.cs').read_bytes())
            self.assertEqual(0, self.run_fixture(f))
            self.assertEqual(0, self.run_fixture(f, True))

    def test_new_inline_text_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            f = self.fixture(directory)
            f[4].write_text('var line = "{=gwp_greeting}Hello";', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Inline player text'):
                self.run_fixture(f)

    def test_new_key_needs_only_the_master_and_crlf_checkout_stays_current(self):
        with tempfile.TemporaryDirectory() as directory:
            f = self.fixture(directory)
            path = f[2] / 'strings.json'
            data = json.loads(path.read_text(encoding='utf-8'))
            data['strings']['gwp_new'] = {'en': 'New line.', 'zh': '新文字。'}
            path.write_text(json.dumps(data), encoding='utf-8')
            self.assertEqual(0, self.run_fixture(f))
            self.assertIn('id="gwp_new"', f[3].read_text(encoding='utf-8'))
            for output in (f[1] / 'GwpTextCatalog.g.cs', f[2] / 'index.md', f[3]):
                output.write_bytes(output.read_bytes().replace(b'\r\n', b'\n').replace(b'\n', b'\r\n'))
            self.assertEqual(0, self.run_fixture(f, True))

    def test_lost_placeholder_is_rejected_but_repeating_a_name_is_valid(self):
        with tempfile.TemporaryDirectory() as directory:
            f = self.fixture(directory)
            path = f[2] / 'strings.json'
            data = json.loads(path.read_text(encoding='utf-8'))
            data['strings']['gwp_greeting']['zh'] = '你好。'
            path.write_text(json.dumps(data), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'placeholders differ'):
                self.run_fixture(f)
            data['strings']['gwp_greeting']['zh'] = '{NAME}，你好，{NAME}。'
            path.write_text(json.dumps(data), encoding='utf-8')
            self.assertEqual(0, self.run_fixture(f))


if __name__ == '__main__':
    unittest.main()
