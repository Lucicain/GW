"""Adversarial sequences over the existing production assistance harness.

This fixture deliberately does not claim to test real navigation or regional
strength: those boundaries are controlled in test_assistance. It tests scheduling,
membership and save/load transitions across actual production methods.
"""
from unittest.mock import patch
import re
import unittest

import test_assistance


SCENARIOS = r'''
  Console.WriteLine("SCENARIO_MATRIX_BEGIN");
  var audit=Setup(out var chief,out var helper,out var enemy);
  helper.Position.X=100000;helper.Reachable=false;
  Check(!audit.GetAvailableAssistanceCandidates(chief).Contains(helper),"unreachable helper rejected");
  audit.UpdateLordAssistance();
  Check(audit._assistanceGroups.Count==0,"no empty commitment for unreachable helper");

  audit=Setup(out chief,out helper,out enemy);
  chief.EnvoyBlocked=null;
  foreach(EnvoyStage stage in new[]{EnvoyStage.Travelling,EnvoyStage.Grace,EnvoyStage.Duel,EnvoyStage.DuelDecided,EnvoyStage.Aftermath}){
   audit._envoyCases["crime"]=new EnvoyCase{Stage=stage};audit.UpdateLordAssistance();
   Check(audit._assistanceGroups.Count==0,"unresolved main-case envoy stage blocks formation: "+stage);
  }
  audit._envoyCases["crime"].Stage=EnvoyStage.Failed;audit.UpdateLordAssistance();
  Check(audit._assistanceGroups.Count==1,"failed main-case envoy permits assistance");

  audit=Setup(out chief,out helper,out enemy);
  audit._envoyCases["crime"]=new EnvoyCase{Stage=EnvoyStage.Failed};
  CrimeState.Tasks["helper"]=new PoliceTask{PolicePartyId="helper",TargetCrimeId="own",TargetCrime=new CrimeRecord{CrimeId="own",Offender=enemy}};
  audit._envoyCases["own"]=new EnvoyCase{Stage=EnvoyStage.Travelling,WardenPartyId="helper"};
  audit.UpdateLordAssistance();
  Check(audit.IsAssistanceMember("helper")&&CrimeState.Tasks["helper"].TargetCrimeId=="own","helper with own travelling envoy retains own task");
  audit._envoyCases["own"].Stage=EnvoyStage.Failed;audit.UpdateLordAssistance();
  Check(audit.IsAssistanceMember("helper"),"own envoy failure alone does not remove helper");
  CrimeState.Tasks.Remove("helper");audit.UpdateLordAssistance();
  Check(audit.IsAssistanceMember("helper"),"own task removal alone does not end another chief's group");

  audit=Setup(out chief,out helper,out enemy);
  audit._envoyCases["crime"]=new EnvoyCase{Stage=EnvoyStage.Failed};
  audit._envoyCases["own"]=new EnvoyCase{Stage=EnvoyStage.Duel,WardenPartyId="helper"};
  Check(!audit.GetAvailableAssistanceCandidates(chief).Contains(helper),"own duel appointment excludes candidate");

  audit=Setup(out chief,out helper,out enemy);var team=audit.Group(chief,helper);
  helper.Position.X=enemy.Position.X;
  Check(TryGetAssistanceDuty(helper,out var earlyTarget,out _,out _,out _)&&earlyTarget==chief,"early helper currently returns to chief, not offender");
  Check(team.LeaderPartyId=="leader"&&team.TargetPartyId=="enemy","arrival order does not switch chief or target");

  audit=Setup(out chief,out helper,out enemy);team=audit.Group(chief,helper);
  helper.Position.X=100;chief.Position.X=0;var held=new MobileParty{StringId="near_chief",Strength=50,Position=new Vec{X=1}};
  MobileParty.All.Add(held);enemy.Strength=130;
  Check(audit.GetAvailableAssistanceCandidates(chief).First()==held,"recruitment ranks distance to chief, not target");

  audit=Setup(out chief,out helper,out enemy);team=audit.Group(chief,helper);
  var second=new MobileParty{StringId="second",Strength=150,Position=new Vec{X=20}};MobileParty.All.Add(second);
  enemy.Strength=250;audit.UpdateLordAssistance();
  Check(team.MemberPartyIds.Contains("second")&&audit._assistanceGroups.Count==1,"stable enemy growth recruits reachable reinforcement");
  Check(team.MemberPartyIds.Distinct().Count()==team.MemberPartyIds.Count,"growth does not duplicate members");

  audit=Setup(out chief,out helper,out enemy);team=audit.Group(chief,helper);
  chief.Strength=200;helper.Position.X=20;audit._assistanceAssignedHours["helper"]=0;
  helper.MapEvent=new object();for(int n=0;n<8;n++)audit.TryShrinkAssistanceGroup(chief,team,100);
  Check(team.MemberPartyIds.Contains("helper"),"surplus release does not extract fighting member");
  helper.MapEvent=null;for(int n=0;n<4;n++)audit.TryShrinkAssistanceGroup(chief,team,100);
  Check(!team.MemberPartyIds.Contains("helper"),"same member may release after battle and sufficient surplus");

  audit=Setup(out chief,out helper,out enemy);team=audit.Group(chief,helper);
  helper.IsActive=false;var savedAudit=new Store{IsSaving=true};audit.SyncAssistanceData(savedAudit);
  var loadedAudit=new PoliceEnforcementBehavior();_instance=loadedAudit;savedAudit.IsSaving=false;loadedAudit.SyncAssistanceData(savedAudit);
  loadedAudit.ReconcileAssistanceFormations();
  Check(loadedAudit._assistanceGroups.Count==0&&CrimeState.Tasks.ContainsKey("leader"),"load with lost last helper releases formation but keeps task");

  audit=Setup(out chief,out helper,out enemy);team=audit.Group(chief,helper);
  enemy.Passers=10000;audit.UpdateLordAssistance();
  Check(team.MemberPartyIds.SequenceEqual(new[]{"helper"})&&audit.CaseFailures==0,"huge passing regional threat does not expand or abandon stable assignment");
  enemy.Passers=0;enemy.Strength=10000;audit.UpdateLordAssistance();
  Check(audit.CaseFailures==1&&audit._assistanceGroups.Count==0,"unreachable stable growth releases group");

  audit=Setup(out chief,out helper,out enemy);team=audit.Group(chief,helper);
  helper.MapEvent=new object();audit.ReleaseAssistanceGroup("leader","case_finished_in_battle");
  Check(audit._assistanceGroups.Count==1&&team.PendingReleaseReason=="case_finished_in_battle","group release waits for detached helper's battle");
  var pendingStore=new Store{IsSaving=true};audit.SyncAssistanceData(pendingStore);
  var pendingReload=new PoliceEnforcementBehavior();_instance=pendingReload;pendingStore.IsSaving=false;pendingReload.SyncAssistanceData(pendingStore);
  Check(pendingReload._assistanceGroups["leader"].PendingReleaseReason=="case_finished_in_battle","pending release persists across same-version save/load");
  helper.MapEvent=null;pendingReload.ReconcileAssistanceFormations();
  Check(pendingReload._assistanceGroups.Count==0,"pending release completes after helper battle ends");
  Console.WriteLine("SCENARIO_MATRIX_END");
'''


class AssistanceScenarioTests(unittest.TestCase):
    def test_adversarial_production_sequences(self):
        original = test_assistance.STUBS
        augmented, count = re.subn(r'(?m)^(\s*public static void Main\(\)\{)',
                                   lambda match: match.group(1) + SCENARIOS, original)
        self.assertEqual(1, count)
        with patch.object(test_assistance, 'STUBS', augmented):
            case = test_assistance.AssistanceTests()
            case.test_production_lifecycle_gathering_support_and_visit_statistics()


if __name__ == '__main__':
    unittest.main()
