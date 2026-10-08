#if GWP_DIAGNOSTICS
namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 取证：菜单里的打出音效是不是放了两份（用户 2026-10-08：地图声音和点开以后的声音重叠）。
    /// 记每次开始、结束，带上视图和菜单的编号，看有没有两个视图同时在放。用户确认不再重叠后整份删掉。
    /// </summary>
    public sealed partial class GwpFireswornSoundView
    {
        partial void TraceMenuSound(string stage, string path) =>
            GwpAiDiagnostics.WriteFaith(stage,
                "view=" + GetHashCode() + "; output=" + (_output?.GetHashCode() ?? 0) +
                "; menu=" + (MenuContext?.GameMenu?.StringId ?? "-") + "; file=" + System.IO.Path.GetFileName(path));
    }
}
#endif
