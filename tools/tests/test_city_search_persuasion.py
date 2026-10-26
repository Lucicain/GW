"""Run installed native persuasion and the production Harmony patch without starting Bannerlord.

Campaign/hero inputs and scene startup are isolated. Probability arithmetic, native
skill/trait/perk formulas, round ownership and the actual patch execute in their DLLs.
Synthetic equipment also exercises native simulation; its zero armour denominator is
temporarily replaced only to obtain an independent finite reference for the correction.
"""
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]
GAME = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')


class CitySearchPersuasionTests(unittest.TestCase):
    def test_native_dll_and_production_patch(self):
        for project, properties in [
            (ROOT / 'GreyWardenPolicePurity/GreyWardenPolicePurity.csproj', ['-p:DeployToLiveModule=false']),
            (ROOT / 'tools/CitySearchPersuasionTests/CitySearchPersuasionTests.csproj', []),
        ]:
            result = subprocess.run(['dotnet', 'build', str(project), '-c', 'Release', '--nologo', '-v', 'quiet'] + properties,
                                    capture_output=True, text=True, encoding='utf-8', errors='replace')
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        result = subprocess.run([
            str(ROOT / 'tools/CitySearchPersuasionTests/bin/Release/net472/CitySearchPersuasionTests.exe'), str(GAME),
            str(ROOT / 'GreyWardenPolicePurity/bin/Release/isolated/GreyWardenPolicePurity.dll')],
            capture_output=True, text=True, encoding='utf-8', errors='replace')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        print(result.stdout)


if __name__ == '__main__':
    unittest.main()
