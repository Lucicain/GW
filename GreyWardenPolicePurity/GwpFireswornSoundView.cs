using HarmonyLib;
using SandBox.View.Menu;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;

namespace GreyWardenPolicePurity
{
    // Presentation state is deliberately not saved: a menu visit plays once, and closing its view stops it.
    public sealed class GwpFireswornSoundView : MenuView
    {
        private GwpRitualSoundOutput? _output;
        private bool _started;

        internal void ActivateMenu()
        {
            if (MenuContext.GameMenu.StringId != GwpFireswornMenus.RitualMenuId)
            {
                EndVisit();
                return;
            }
            if (_started)
                return;
            _started = true;
            float gain = NativeConfig.DisableSound ? 0 :
                NativeOptions.GetConfig(NativeOptions.NativeOptionsType.MasterVolume) *
                NativeOptions.GetConfig(NativeOptions.NativeOptionsType.SoundVolume);
            _output = new GwpRitualSoundOutput(GwpFireswornArt.SoundPath(MenuContext.CurrentBackgroundMeshName), gain);
        }

        internal void EndVisit()
        {
            StopSound();
            _started = false;
        }

        private void StopSound()
        {
            if (_output == null)
                return;
            _output.Dispose();
            _output = null;
        }

        protected override void OnActivate() => ActivateMenu();
        protected override void OnDeactivate() => StopSound();

        protected override void OnMenuContextUpdated(MenuContext newMenuContext) => ActivateMenu();

        protected override void OnFrameTick(float dt)
        {
            if (MenuContext.GameMenu.StringId != GwpFireswornMenus.RitualMenuId)
                EndVisit();
            else if (_output?.IsFinished == true)
                StopSound();
        }

        protected override void OnFinalize()
        {
            EndVisit();
            base.OnFinalize();
        }
    }

    // Native arena menus set their panel sound during menu initialization; the handler then activates views.
    // Attach after that activation (not while native code is enumerating MenuViews).
    [HarmonyPatch(typeof(MenuViewContext),
        "TaleWorlds.CampaignSystem.GameState.IMenuContextHandler.OnMenuActivate")]
    internal static class GwpFireswornSoundMenuActivatePatch
    {
        private static void Postfix(MenuViewContext __instance)
        {
            GwpFireswornSoundView? view = __instance.GetMenuView<GwpFireswornSoundView>();
            if (view == null && __instance.MenuContext.GameMenu.StringId == GwpFireswornMenus.RitualMenuId)
                view = (GwpFireswornSoundView)__instance.AddMenuView<GwpFireswornSoundView>();
            view?.ActivateMenu();
        }
    }
}
