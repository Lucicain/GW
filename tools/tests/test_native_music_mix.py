"""Verify calibration against installed bank routing, not a guessed attenuation."""
import importlib.util
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'
GAME = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')


class NativeMusicMixTests(unittest.TestCase):
    def test_installed_music_fader_and_measured_reference(self):
        spec = importlib.util.spec_from_file_location('fmod_mix', ROOT / 'tools/native/inspect-fmod-mix.py')
        mix = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mix)
        sounds = GAME / 'Modules/Native/Sounds/PC'
        master = mix.inspect(sounds / 'MasterBank.bank', sounds / 'MasterBank.strings.bank')
        music = mix.inspect(sounds / 'music.bank', sounds / 'MasterBank.strings.bank')
        event = next(v for v in music['events'].values() if v['path'] == 'event:/music')
        buses = master['buses'] | music['buses']
        key = event['input_bus']
        route = []
        while key in buses:
            route.append(buses[key])
            key = buses[key]['parent']
        self.assertEqual(['bus:/Music/music/gamemusic', 'bus:/Music/music', 'port:/Music'], [x['path'] for x in route[1:]])
        faders = [master['effects'][key] for bus in route for key in bus['pre_effects'] + bus['post_effects']]
        self.assertEqual([(7, -9)], [(x['dsp_type'], x['parameters'][0]['value']) for x in faders])
        self.assertEqual(0, sum(x['volume_db'] for x in route))
        cutscene = next(e for e in master['events'].values() if e['path'] == 'snapshot:/Duckers/ActiveCutsceneDucker')
        self.assertTrue(any(e['target'] == '74ee7440-95ae-493c-ac3f-06686d248ce5' and e['value'] == -80 for e in master['snapshots'][cutscene['snapshot']]['entries']))
        report = json.loads((MODULE / 'docs/reference/music-calibration-20261018.json').read_text())
        source = (MODULE / 'GwpMusicOutput.cs').read_text(encoding='utf-8-sig')
        gain = lambda name: float(re.search(rf'const float {name}\s*=\s*([-\d.]+)f;', source)[1])
        self.assertEqual(report['native_music_static_gain_db'], gain('NativeMusicBusGainDb'))
        self.assertEqual(report['content_compensation_db'], gain('ContentMakeupDb'))
        self.assertAlmostEqual(report['reference_output_lufs'], report['measured_output']['integrated_lufs'], delta=0.5)
        self.assertLessEqual(report['measured_output']['true_peak_dbfs'], -9.8)
        print('NATIVE_MUSIC_MIX: installed event route, -9 dB fader, calibration within 0.5 LU and bounded true peak')


if __name__ == '__main__':
    unittest.main()
