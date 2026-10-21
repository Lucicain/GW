using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        // Only the open barter UI uses this reference. Saving is unavailable in
        // that UI; the resulting hero custody and commission use existing saves.
        private CasePrisonerTransfer? _casePrisonerTransfer;

        private bool IsCaseTargetMissing => CaseHero is { IsDead: false, IsPrisoner: false } hero &&
            (hero.IsFugitive || hero.IsDisabled || hero.PartyBelongedTo?.IsActive != true);

        internal bool CanWithdrawUnavailableCommission => IsTrackingBountyTarget &&
            !HasCompletedEnforcement && CaseHero?.IsDead == false &&
            CrimePool.GetRecord(CaseHero)?.HasOpenCase == true &&
            (IsCaseTargetMissing || CaseHero.IsPrisoner && CaseHero.PartyBelongedToAsPrisoner != null &&
                CaseHero.PartyBelongedToAsPrisoner != MobileParty.MainParty?.Party);

        private bool CanBuyCasePrisoner()
        {
            if (!CanWithdrawUnavailableCommission || CaseHero?.IsPrisoner != true || MobileParty.MainParty?.IsActive != true) return false;
            Hero? seller = Hero.OneToOneConversationHero;
            PartyBase? holder = CaseHero?.PartyBelongedToAsPrisoner;
            if (seller == null || seller.IsPrisoner || seller == Hero.MainHero || holder == null) return false;
            if (holder.MobileParty != null) return holder.MobileParty.LeaderHero == seller;
            var settlement = holder.Settlement;
            return settlement != null && seller.CurrentSettlement == settlement &&
                (settlement.Town?.Governor == seller || settlement.OwnerClan?.Leader == seller);
        }

        private bool PrepareCaseCustodyOffer()
        {
            if (!CanBuyCasePrisoner()) return false;
            MBTextManager.SetTextVariable("GWP_CASE_CUSTODY_REQUEST",
                GwpText.Create(GwpTextCatalog.gwp_case_custody_request, "NAME", CaseHero!.Name));
            return true;
        }

        private void RegisterCaseCustodyDialogues(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("gwp_case_custody_request", "lord_talk_speak_diplomacy_2", "gwp_case_custody_offer",
                "{GWP_CASE_CUSTODY_REQUEST}", PrepareCaseCustodyOffer, null, 105);
            starter.AddDialogLine("gwp_case_custody_offer", "gwp_case_custody_offer", "gwp_case_custody_options",
                GwpText.Get(GwpTextCatalog.gwp_case_custody_offer), null, null);
            starter.AddPlayerLine("gwp_case_custody_buy", "gwp_case_custody_options", "gwp_case_custody_result",
                GwpText.Get(GwpTextCatalog.gwp_case_custody_buy), CanBuyCasePrisoner, OpenCaseCustodyBarter);
            starter.AddPlayerLine("gwp_case_custody_leave", "gwp_case_custody_options", "lord_pretalk",
                GwpText.Get(GwpTextCatalog.gwp_case_custody_leave), null, null);
            starter.AddDialogLine("gwp_case_custody_done", "gwp_case_custody_result", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_case_custody_done),
                () => _casePrisonerTransfer?.Transferred == true, () =>
                {
                    RegisterCaptureInCustody();
                    _casePrisonerTransfer = null;
                    LeaveCaseClerk();
                }, 110);
            starter.AddDialogLine("gwp_case_custody_declined", "gwp_case_custody_result", "gwp_case_custody_options",
                GwpText.Get(GwpTextCatalog.gwp_case_custody_declined), null, () => _casePrisonerTransfer = null);
            starter.AddPlayerLine("gwp_case_custody_withdraw", "gwp_case_report_options", "gwp_case_custody_withdrawn",
                GwpText.Get(GwpTextCatalog.gwp_case_custody_withdraw), () => CanWithdrawUnavailableCommission,
                () => WithdrawUnavailableCommission());
            starter.AddDialogLine("gwp_case_custody_withdrawn", "gwp_case_custody_withdrawn", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_case_custody_withdrawn), null, LeaveCaseClerk);
        }

        private void OpenCaseCustodyBarter()
        {
            _casePrisonerTransfer = null;
            if (!CanBuyCasePrisoner()) return;
            Hero captive = CaseHero!;
            Hero seller = Hero.OneToOneConversationHero;
            PartyBase holder = captive.PartyBelongedToAsPrisoner;
            var transfer = new CasePrisonerTransfer(captive, seller, holder, Hero.MainHero, MobileParty.MainParty.Party);
            _casePrisonerTransfer = transfer;
            BarterManager manager = Campaign.Current.BarterManager;
            BarterManager.BarterBeginEventDelegate original = manager.BarterBegin;
            try
            {
                manager.BarterBegin = data =>
                {
                    // Only this prisoner is for sale here. Native ransom-release
                    // entries would free him instead of transferring custody.
                    data.GetBarterables().RemoveAll(entry => !(entry is GoldBarterable) && !(entry is ItemBarterable));
                    transfer.SetIsOffered(true);
                    transfer.CurrentAmount = 1;
                    data.AddBarterable<PrisonerBarterGroup>(transfer, true);
                    original?.Invoke(data);
                };
                manager.StartBarterOffer(Hero.MainHero, seller, MobileParty.MainParty.Party, holder,
                    null, (entry, data, context) => false);
            }
            finally { manager.BarterBegin = original; }
        }

        internal bool WithdrawUnavailableCommission()
        {
            if (!CanWithdrawUnavailableCommission) return false;
            _activeQuest?.CancelUnavailableCommission();
            // Release the player's reservation, not the offender's unpaid case.
            EndBountyTaskState(true);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_case_custody_withdrawn)));
            return true;
        }

        private sealed class CasePrisonerTransfer : TransferPrisonerBarterable
        {
            private readonly Hero _captive;
            private readonly PartyBase _holder, _destination;
            internal bool Transferred { get; private set; }

            internal CasePrisonerTransfer(Hero captive, Hero seller, PartyBase holder, Hero buyer, PartyBase destination)
                : base(captive, seller, holder, buyer, destination)
            { _captive = captive; _holder = holder; _destination = destination; }

            public override void Apply()
            {
                if (Transferred) return;
                // The native barterable requires war with the prisoner. A case
                // commission grants custody authority without declaring war.
                if (!_captive.IsPrisoner || _captive.PartyBelongedToAsPrisoner != _holder ||
                    CrimePool.GetRecord(_captive)?.HasOpenCase != true ||
                    !GwpPlayerCaseAssignment.HasAuthority(_captive))
                    throw new InvalidOperationException("Case prisoner no longer available for transfer: " + _captive.StringId);
                TransferPrisonerAction.Apply(_captive.CharacterObject, _holder, _destination);
                Transferred = _captive.PartyBelongedToAsPrisoner == _destination;
                if (!Transferred) throw new InvalidOperationException("Case prisoner transfer failed: " + _captive.StringId);
            }
        }
    }
}
