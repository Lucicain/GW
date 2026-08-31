# GreyWarden

GreyWarden adds an independent law-enforcement clan that handles real cases on Bannerlord's campaign map. Six Warden lords investigate crimes, pursue offenders, assemble assistance armies, aid settlements, and raise successors. The player may be pursued for crimes or earn the Wardens' trust, join them, and accept contracts.

This release ships two packages with identical content and different game support: v1.5-r1 for Bannerlord 1.5.2 and v1.4-r10 for Bannerlord 1.4.8. Pick the one matching your game; the wrong one reports that the module cannot be loaded at startup.
Chinese: README.md

## Main features

- Warden lords lead their own parties and handle training, caravan protection, rural protection, local petitions, village reconstruction, and player affairs. Adult successors inherit duties that still survive.
- Wardens record attacks on caravans and villagers as well as village raids, then pursue offenders by duty and distance. Strong targets draw assistance armies, while fast targets may be intercepted by cavalry.
- The Case Ledger shows open cases, assigned parties, and the judicial treasury. Captured lords retain a criminal record and recover gradually from deterrence, while repeat offenders face stronger lasting suppression.
- Players can earn standing by protecting civilians and helping cases, join the Wardens, take bounties, order troops, receive battlefield support and village gifts, and appeal a fief decision.
- Wardens physically rendezvous to exchange and deliver troops, resolve local issues, rebuild raided villages, and manage case income, operating expenses, and naval ships.
- The mod includes a Warden troop tree, black-and-gold equipment, dual blades, a dedicated shield, kicks, shield bashes, passive great-shield protection, and sparring with Warden lords.

## Installation and updating

1. Delete the old Modules/GreyWarden folder.
2. Extract the complete GreyWarden folder from the archive into the game's Modules directory.
3. Enable GreyWarden in the launcher.

Existing campaigns remain supported. Finish any old bounty already in progress before updating because it is not carried into the revised system. Replace the complete module rather than copying only part of it.

## Changelog

### 2026-08-31 v1.5-r1 (Bannerlord 1.5.2) / v1.4-r10 (Bannerlord 1.4.8)

Both packages carry the same content, and everything below applies to both. Compared with v1.4-r9:

#### Added and adjusted

- Ships as two packages, one for Bannerlord 1.5.2 and one for 1.4.8, with the same gameplay content in both. Each supports NavalDLC sea battles and its custom battle entry.
- New troop: the Warden Twinblade Guard, who carries a pair of Warden dual blades and no bow, upgrades from light infantry, and stands at the same tier as heavy infantry. Warden archers remain purely ranged.
- Twinblade Guards and AI-controlled Warden lords genuinely fight with both blades. The off-hand blade deals its own damage and can trigger the Warden knockdown according to troop tier.
- The player receives a pair of dual blades on joining or rejoining the Wardens. Four-way blocking, attack animations, and movement transitions are shared with the AI and need no other mod; a connecting off-hand swing can trigger the Warden knockdown even against a defending opponent.
- Dual blades can be picked up from the ground, but never appear in the smithing screen or in town smithing orders and cannot be crafted.
- Personally defeating the party of an offender with an open case now builds Warden standing, counted by the troops you down yourself, the same way bandit clearing and rescues do. Early standing has one more route.
- The two buttons on the Warden clan page are now one: Grey Warden affairs. Standing wars and their grounds, the case and duty pool, the judicial treasury, your standing, and the family roll all live on one page. Standing is no longer a message that flashes past when it changes, and while wanted the page lists what a provost patrol and a Warden lord each charge to settle it.
- Custom battle lists the Warden commander first while keeping the native commander options, and lord previews use each character's own appearance and equipment.
- Warden weapons, armour, shields, and horse harnesses now use the native non-merchandise rule, so they never restock in town markets and never enter ordinary equipment loot.
- Standing Warden parties no longer receive ships for free. Sea-battle ships are bought through the native economy, so the treasury no longer inflates from selling gifted ships.
- Wardens return to neutrality with a faction as soon as no enforcement reason remains, and no longer turn on the player straight after a battle in which the player helped them take an offender from their own realm.
- Warden heavy infantry keep sword and shield as a separate final tier and no longer carry an extra mallet.

#### Fixed

- Fixed losing standing for a raid you did not commit. While a village was being raided, anyone whose destination was that village — collecting taxes, running a duty, simply waiting there — was treated as the raider and charged the standing loss and a crime record. Only the party that actually raids is held responsible now.
- Fixed staying wanted after the fine was paid. With standing back at zero or above, Wardens no longer stop the player to demand a 0 denar fine, and a pursuit left over from a settled case is retired automatically.
- Fixed Warden lords reaching the player during a serious warrant without opening the enforcement conversation. The options to pay, atone, or resist now appear properly, and accepting judgment or paying ends the meeting cleanly.
- Fixed provost patrols travelling to the player's former position. Patrols now follow the player's live position and open the meeting once in range.
- Fixed GreyWarden failing to load and erroring on startup after the game changed version. 1.5.2 and 1.4.8 now each have their own package.
- Fixed custom battle crashing to desktop during screen setup and troop preview.

### 2026-08-05 v1.4-r9

Compared with v1.4-r8:

#### Added and adjusted

- Bounty notices now offer a nearest, harder, and easier contract. Payment is fixed by the difficulty assessed when the contract is accepted rather than headcount.
- The player now leads an accepted bounty operation. Warden parties escort the player, declare war when the player approaches the target, and may still detach fast cavalry to intercept a quicker offender.
- Any Warden lord can settle a defeated target. If payment remains unclaimed for five days, a settlement party seeks out the player, pays directly, and returns to nearby quarters.
- Bounties now share one forty-five-day limit. Escort duty ends when the target falls, and contract and settlement-party state continue across saves.
- The Training Warden transfers elite troops in proportion to her roster. GreyWarden no longer blocks native achievements when used on its own.

#### Fixed

- Fixed missing encyclopedia buttons, squeezed native page content, and broken settlement links. Warden commands are now added directly to the native hero and clan pages.
- Fixed campaigns failing to start when GreyWarden was the only enabled mod because a runtime component was missing.
- Fixed Wardens continuing their own pursuit, failing to declare war promptly, or losing interceptor support after the player accepted an assisted bounty.
- Fixed expired bounties, payment timers, escort duties, and war state being cleaned up incorrectly.
- Fixed rescued troops and returning interceptors causing overcapacity or the loss of elite soldiers.

## Contact

- Bilibili: Lucicain
- Personal QQ: 157652226
- QQ group: 981323752
