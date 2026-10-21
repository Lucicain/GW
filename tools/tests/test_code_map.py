"""Declaration inventory regression; this does not replace Harmony runtime binding."""
import importlib.util
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("code_map", ROOT / "tools/Generate-CodeMap.py")
MAP = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MAP)


class CodeMapTests(unittest.TestCase):
    def test_dynamic_conditional_and_explicit_interface_targets(self):
        source = '''
// [HarmonyPatch] internal static class Commented { }
#if GWP_DIAGNOSTICS
[HarmonyPatch]
internal static class Dynamic {
    private static MethodBase TargetMethod() { return Resolve(); }
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void After() { var braces = @"{ "" }"; /* } */ }
}
#endif
[HarmonyPatch(typeof(Target), "Namespace.IThing.Run")]
[HarmonyPriority(Priority.Low)]
internal static class Explicit {
    private static bool Prefix() { return true; }
}
internal static class Unrelated {
    private static void Finalizer() { }
}
'''
        rows = MAP.extract_patches(source, "fixture.cs")
        self.assertEqual([r[3] for r in rows], ["Dynamic", "Explicit"])
        self.assertEqual(rows[0][1], "TargetMethod()")
        self.assertEqual(rows[0][4], "Postfix")
        self.assertEqual(rows[1][1], "Namespace.IThing.Run")
        self.assertEqual(rows[1][4], "Prefix")
        self.assertRegex(rows[0][5], r"fixture.cs:\d+$")

    def test_multiple_multiline_attributes_and_array_overload(self):
        source = '''
[HarmonyPatch(
    typeof(First), nameof(First.Run), new[] { typeof(int), typeof(string) })]
[HarmonyPatch(typeof(Second), "get_Value", MethodType.Getter)]
public static class Both {
    [HarmonyPrefix] private static void Before() { }
    [HarmonyFinalizer] private static void Finally() { }
}
[HarmonyPatch(typeof(Unknown))]
internal static class Unresolved { private static void Postfix() { } }
'''
        rows = MAP.extract_patches(source, "fixture.cs")
        self.assertEqual(len(rows), 3)
        self.assertEqual([r[0] for r in rows[:2]], ["First", "Second"])
        self.assertEqual(rows[1][2], "Getter")
        self.assertEqual(rows[0][4], "Finalizer/Prefix")
        self.assertEqual(rows[2][3], "Unresolved")
        self.assertIn("未静态解析", rows[2][0])

    def test_current_repository_declarations_are_not_dropped(self):
        # Independent attribute-to-class inventory catches silent omissions.
        declared, extracted = set(), set()
        for path in (ROOT / "GreyWardenPolicePurity").glob("*.cs"):
            text = path.read_text(encoding="utf-8-sig")
            clean = MAP.mask_non_code(text)
            for attr in re.finditer(r"\[HarmonyPatch\b", clean):
                cls = re.search(r"\bclass\s+(\w+)", clean[attr.end():])
                self.assertIsNotNone(cls, path.name)
                declared.add(cls.group(1))
            extracted.update(row[3] for row in MAP.extract_patches(text, path.name))
        self.assertGreaterEqual(len(declared), 100)
        self.assertEqual(extracted, declared)
        self.assertTrue({"GwpNativeRegionPatch", "GwpArmyExitDisorganizedPatch",
                         "GwpNavalCustomBattleCommanderListPatch"} <= extracted)


if __name__ == "__main__":
    unittest.main()
