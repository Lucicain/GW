# Vanilla Marriage Reference

## Purpose

This note records the vanilla player marriage flow, the related API entry points, and the lowest-risk extension points for Grey Warden-specific romance content.

It is based on local decompilation of the current game install and the in-game concept strings.

## Primary Reference Sources

- `D:\steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll`
- `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\Languages\std_concept_strings_xml.xml`
- `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\Languages\CNs\std_TaleWorlds_CampaignSystem-zho-CN.xml`
- `C:\Users\lucif\source\repos\GreyWardenPolicePurity\GreyWardenPolicePurity\PoliceMarriageModel.cs`

## Relevant Vanilla Types

### Qualification and Outcomes

- `TaleWorlds.CampaignSystem.ComponentInterfaces.MarriageModel`
- `TaleWorlds.CampaignSystem.GameComponents.DefaultMarriageModel`
- `TaleWorlds.CampaignSystem.Actions.MarriageAction`
- `TaleWorlds.CampaignSystem.Actions.ChangeRomanticStateAction`

### Romance State and Courtship Flow

- `TaleWorlds.CampaignSystem.Romance`
- `TaleWorlds.CampaignSystem.CampaignBehaviors.RomanceCampaignBehavior`
- `TaleWorlds.CampaignSystem.ComponentInterfaces.RomanceModel`
- `TaleWorlds.CampaignSystem.GameComponents.DefaultRomanceModel`

### Final Marriage Negotiation

- `TaleWorlds.CampaignSystem.BarterSystem.Barterables.MarriageBarterable`
- `TaleWorlds.CampaignSystem.BarterSystem.BarterManager`
- `TaleWorlds.CampaignSystem.BarterSystem.BarterData`

### AI Marriage Offers to Player Clan

- `TaleWorlds.CampaignSystem.CampaignBehaviors.MarriageOfferCampaignBehavior`

### Potentially Useful for Non-Marriage “Join Player Clan” Endings

- `TaleWorlds.CampaignSystem.Actions.AdoptHeroAction`

## Vanilla Player Marriage Flow

Vanilla has two separate lines:

- direct player courtship with the target hero
- clan-level arranged marriage discussion with a clan leader

The direct romance line is the important one for player-to-NPC love stories.

### Courtship State Machine

The state storage lives in `Romance.RomanticState`.

Important `RomanceLevelEnum` values:

- `Untested`
- `CourtshipStarted`
- `CoupleDecidedThatTheyAreCompatible`
- `CoupleAgreedOnMarriage`
- `Marriage`
- `FailedInCompatibility`
- `FailedInPracticalities`
- `MatchMadeByFamily`
- `Ended`
- `Rejection`

### Player Direct Courtship Flow

1. Player opens romance dialogue.
2. If accepted, state moves to `CourtshipStarted`.
3. First persuasion sequence runs.
4. On success, state moves to `CoupleDecidedThatTheyAreCompatible`.
5. Second persuasion sequence runs after cooldown.
6. On success, state moves to `CoupleAgreedOnMarriage`.
7. Player then talks to the target or target clan leader for final terms.
8. Vanilla opens a `MarriageBarterable`.
9. If barter succeeds, `MarriageAction.Apply(...)` completes the marriage.
10. Final state becomes `Marriage`.

### Failure Handling

- Failure in stage 1 sets `FailedInCompatibility`.
- Failure in stage 2 sets `FailedInPracticalities`.
- War or other blocked conditions can also push the romance into a failed state.

### Cooldown

`RomanceCampaignBehavior` uses a one-day cooldown between persuasion attempts.

## Vanilla Eligibility Rules

The hard gate is `DefaultMarriageModel.IsCoupleSuitableForMarriage(...)`.

Important checks:

- both clans must be suitable for marriage
- neither clan can be eliminated, rebel, or bandit
- cannot be same sex
- cannot be close relatives within three generations
- both heroes must be able to marry
- both sides must not already be locked into another high-stage courtship
- both sides must not be clan leaders at the same time

`RomanceCampaignBehavior.MarriageCourtshipPossibility(...)` adds one more important rule:

- the two factions must not be at war

## What Vanilla Uses to Judge Courtship

`RomanceCampaignBehavior.GetRomanceReservations(...)` builds persuasion reservations from:

- compatibility traits
- attraction
- property / wealth / settlement count
- family approval and clan-leader relation

Important data inputs:

- target traits: `Honor`, `Mercy`, `Valor`, `Calculating`
- attraction: `RomanceModel.GetAttractionValuePercentage(...)`
- wealth signal: owned settlements of the wooer clan
- family approval: target clan leader relation with player

## Final Marriage Terms

Vanilla does not go directly from romance success to marriage.

After `CoupleAgreedOnMarriage`, it opens a barter flow:

- `MarriageBarterable`
- `BarterManager.StartBarterOffer(...)`
- `BarterManager.InitializeMarriageBarterContext(...)`

The accumulated persuasion surplus is passed as `persuasionCostReduction`.
That means better romance persuasion can reduce the final marriage cost.

## What MarriageAction Actually Does

`MarriageAction.Apply(...)` is the real marriage commit point.

It does all of the following:

- sets both `Spouse`
- increases relation between the two heroes
- determines the post-marriage clan with `MarriageModel.GetClanAfterMarriage(...)`
- handles clan transfer side effects
- clears other active courtships
- sets romance state to `Marriage`

Important consequence:

- if the player is one side of the marriage, vanilla `DefaultMarriageModel.GetClanAfterMarriage(...)` keeps the result in the player clan

This is why a vanilla marriage with a Grey Warden woman would normally pull her into the player clan.

## Arranged Marriage / Clan Leader Route

Vanilla also supports proposing an alliance through a clan leader.

Important notes:

- player can nominate self or an eligible clan relative
- success can set `MatchMadeByFamily`
- final terms still go through a marriage barter

This route is separate from direct romance, but shares the same marriage end state.

## AI Offer System

`MarriageOfferCampaignBehavior` is not the main player romance behavior.

It handles:

- AI clans offering marriages to player-clan relatives
- map notifications for offers
- delayed completion when marriage is temporarily blocked

Useful if Grey Warden romance should never be initiated by AI clans.

## Grey Warden-Relevant Findings

### Current Mod State

Current local mod behavior in `PoliceMarriageModel`:

- Grey Warden women cannot marry AI NPCs
- Grey Warden women can still marry the player

So the current code is not a full “no marriage” rule. It is only an “AI marriage forbidden” rule.

### Minimal-Change Implication

If Grey Warden women are supposed to be vow-bound and not allowed to marry at all, the clean low-risk direction is:

- keep vanilla romance front half only if desired
- forbid vanilla marriage completion for Grey Warden women
- replace the final step with a custom “leave the order and join player clan” outcome

### Lowest-Risk Extension Points

1. `PoliceMarriageModel.IsCoupleSuitableForMarriage(...)`
- use this to hard-block true vanilla marriage for Grey Warden women

2. `RomanceCampaignBehavior` dialogue interception
- use this to replace the final marriage step for Grey Warden heroines

3. Custom outcome after high romance state
- instead of `MarriageAction.Apply(...)`, run a Grey Warden-specific defection / disavowal / clan-transfer consequence

## Caution About Clan Transfer APIs

`AdoptHeroAction.Apply(...)` exists, but it is not suitable for romance endings.

Why not:

- it assigns the hero as child of the main hero
- it also directly sets the clan to `Clan.PlayerClan`

That is useful to know, but wrong for a romance-driven defection.

If a Grey Warden heroine leaves the order for the player, the implementation should instead follow the side-effect handling pattern used by `MarriageAction`, without setting a spouse unless true marriage is intended.

## Recommended Use For Future Grey Warden Content

For a “love over vows” route:

- allow or stage-manage romance progression up to a custom threshold
- block the vanilla final marriage barter
- replace it with a Grey Warden-specific irreversible choice
- move the heroine into player clan only through custom consequence logic
- do not call `MarriageAction.Apply(...)` unless true marriage is desired

## Fast Reference

### Core Methods

- `DefaultMarriageModel.IsCoupleSuitableForMarriage(...)`
- `DefaultMarriageModel.GetClanAfterMarriage(...)`
- `Romance.GetRomanticLevel(...)`
- `Romance.GetRomanticState(...)`
- `ChangeRomanticStateAction.Apply(...)`
- `RomanceCampaignBehavior.MarriageCourtshipPossibility(...)`
- `MarriageAction.Apply(...)`
- `BarterManager.StartBarterOffer(...)`
- `BarterManager.InitializeMarriageBarterContext(...)`

### Core Dialogue States in RomanceCampaignBehavior

- `lord_special_request_flirt`
- `lord_start_courtship_response`
- `hero_courtship_task_pt1`
- `hero_courtship_task_pt2`
- `hero_courtship_final_barter`
- `lord_propose_marriage_conv_general_proposal`

