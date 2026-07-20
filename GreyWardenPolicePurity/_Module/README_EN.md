# GreyWarden

For **Mount & Blade II: Bannerlord 1.4.7**

Chinese version: [README.md](README.md)

## Installation

1. Delete the old `Modules/GreyWarden` folder.
2. Extract the complete new `GreyWarden` folder into the game's `Modules` directory.
3. Enable `GreyWarden` in the launcher.

This update rebuilds AI-lord record data. Existing saves automatically migrate cumulative offences, arrests, and deterrence values while retaining only currently open cases. Do not install by copying only part of the module.

## Latest update

### 2026-07-20 v1.4.7-r5

Compared with `v1.4.7-r4`:

#### Added and adjusted

- The Grey Warden clan encyclopedia now includes a Case Ledger showing the complete task pool and the progress of assigned cases, assistance, and adoption relief.
- Warden lords now pursue cases through Bannerlord's native map desires. Police duty outranks ordinary patrol, while resupply, recruitment, healing, trade, and safety remain native decisions.
- The Warden clan now operates through a real economy. Lords buy food and recruits, while fines, confiscation auctions, protection contributions, and case funding enter the judicial treasury.
- When facing a stronger target, the assigned lord can assemble other Warden lords into a native army. Later leaderless support joins that army, and members return to the task pool after the case ends.
- Every eligible Warden lord can take ordinary cases, and player warrants after refusal can also request lord assistance.
- Closed cases retain only lifetime offence, arrest, and deterrence totals instead of unnecessary event details.

#### Fixed

- Fixed Warden armies failing to engage through native AI even when their combined strength was sufficient.
- Fixed cases becoming stuck or assistance armies ending early after peace changes, battle settlement, or temporary target-state changes.
- Fixed leaderless support spawning in large batches, withdrawing after brief contact, or attacking one party at a time after an army had formed.
- Fixed police duty being overwritten by ordinary patrol or settlement movement while preserving necessary native logistics decisions.
- Fixed offenders hiding inside settlements and creating permanent standoffs. Once expelled, they remain outside until battle or case closure.
- Fixed unrelated battles closing cases, existing saves failing to load case data, and the Case Ledger refusing to scroll through all entries.

### 2026-07-19 v1.4.7-r4

Compared with `v1.4.7-r3`:

#### Added and adjusted

- AI lords now permanently retain their total crimes and Grey Warden arrests. Open cases are tracked separately from historical records, and deterrence recovery no longer erases criminal history.
- Personal and clan deterrence are now recorded separately. Lords captured in the same battle without an open case receive witness-style clan deterrence but are not marked as offenders or given an arrest; available Grey Wardens prioritize the nearest lord with an unresolved case.
- Lords discussing Grey Warden enforcement now respond according to personal or clan deterrence, its intensity, all five personality tendencies, and whether the player has joined the Grey Wardens. An eligible lord with active deterrence consistently uses the matching greeting in ordinary conversation.
- The player's personal kills in righteous battles now accumulate across battles and grant reputation when the threshold is reached. Reputation losses from criminal battles are still settled separately after each battle.

#### Fixed

- Fixed adoption notifications and encyclopedia records omitting the adopted hero's name and village of origin.
- Grey Warden records now extend the native hero encyclopedia page directly, fixing Messenger's button being disabled when both mods are active. GreyWarden itself still has no external prerequisites.

## Playable content

- Grey Warden reputation, crime records, fines, atonement, warrants, and pursuit.
- Village rewards, bounty contracts, troop requests, and battle reinforcements.
- AI-lord enforcement, the Case Ledger, assistance armies, and the Warden clan economy.
- Grey Warden troops, six founding lords, and the adopted-heir system.
- Kicks, shield bashes, passive great-shield protection, and the black-and-gold shield appearance.
- Town and field sparring with Grey Warden lords.

## Contact

- Bilibili: `Lucicain`
- Personal QQ: `157652226`
- QQ group: `981323752` (discussion, feedback, and file downloads)
