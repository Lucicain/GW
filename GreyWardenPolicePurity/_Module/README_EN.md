# GreyWarden

GreyWarden adds an independent law-enforcement clan that handles real cases on Bannerlord's campaign map. Six Warden lords investigate crimes, pursue offenders, assemble assistance armies, aid settlements, and raise successors. The player may be pursued for crimes or earn the Wardens' trust, join them, and accept contracts.

The current development build supports the Bannerlord 1.5.2 beta. Use the matching GreyWarden release for older game versions.
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

### 2026-08-30 v1.4-r11 (development)

Compared with v1.4-r10:

#### Added and adjusted

- The current development build supports Bannerlord 1.5.2 beta while retaining the existing campaign, combat, and paired-blade gameplay.
- The separate Twinblade troop has been removed. Light infantry now upgrade to heavy infantry, archers, or knights.
- In the current stability baseline, Grey Warden archers carry only paired blades with bows and arrows removed; the Grey Warden Custom Battle commander also carries only the pair.
- Custom Battle places the Grey Warden commander first while preserving every native commander entry, with full Warden troop and equipment registration.

#### Fixed

- Fixed the incompatibility introduced by Bannerlord 1.5.2 changing the damage-model interface, which previously prevented GreyWarden from compiling and loading.
- Fixed crashes during Custom Battle screen initialization and troop preview, restoring stable entry.
- Fixed the pre-battle dual-blade registration error in 1.5.2; the native equipment flow now registers the pair without reattaching weapons in previews.
- Fixed a remaining direct error during the first Custom Battle agent equipment pass; paired-blade attributes now apply only while a real archer's battle equipment is being built.
- Fixed Warden archers drawing only one blade in the field; they now draw the pair through the same routine the player character uses, leaving ordinary soldiers and every other troop untouched.
- Fixed invalidated paired-blade templates and shared weapon handling interfering with encyclopedia and Custom Battle previews; both previews now use the Warden's real body, armour, mount, and paired-blade equipment instead of display-only sword-and-shield replacements.
- Fixed missing or badly posed character models in the encyclopedia and the Custom Battle preview. The fault reached every character, native troops included: the paired-blade actions had been written into the action resources shared by all humans, and now live only in the Grey Warden dual-blade actions, leaving the shared resources exactly as native ships them.
- The paired-blade actions now come in separate male and female versions, so female Warden archers and commanders no longer run male animations.
- Fixed possible errors or freezes when the off-hand blade contacted or defended against ordinary soldiers; it retains melee attacks and parries but cannot block ranged attacks.
- Restored the paired blades to the same appearance as the Grey Warden arming sword, using GreyWarden's own item definitions without loading ROT or other external-mod resources; AI use is limited to Warden archers and the Custom Battle Warden commander, while player use remains available.
- When NavalDLC owns the Custom Battle screen, its separate character catalogue is covered as well; the Grey Warden commander remains first while native commander choices are preserved.

### 2026-08-28 v1.4-r10

Compared with v1.4-r9:

#### Added and adjusted

- Personally defeating the forces of a criminal with an open case now builds Warden standing, counted from personal kills like bandit suppression and rescue work, opening an extra early-game path to rise.
- All Warden weapons, armor, shields, and harnesses use Bannerlord's native non-merchandise setting. They are neither generated as normal town stock nor included in native equipment loot; no extra script restricts owning or selling them.
- Warden lord parties no longer receive ships for free; naval vessels are now purchased through the native economy, removing the treasury growth caused by granting ships and later selling surplus.
- Once a case closes, the Wardens immediately restore neutrality with factions that no longer justify war, so Wardens who just fought alongside the player will not immediately turn and attack them afterwards.
- Warden heavy infantry retain sword-and-shield equipment as a separate final-tier unit. Archers now carry both a bow and the paired blades; no separate Twinblade troop is added.
- Heavy infantry no longer carry the extra small mace, and the dedicated Custom Battle commander uses the native sword-and-shield loadout.
- Players and AI share the same paired-blade attacks, four-direction melee blocks, and movement transitions without requiring ROT. A left-hand swing can trigger the Warden knockdown rule even when the target is defending. Players receive a pair when joining or rejoining the Wardens.
- When archers use the paired blades, left- and right-hand swings, overhead and downward cuts, and thrusts can all trigger the Warden knockdown rule. Kicks and shield bashes retain their separate native-compatible checks.
- The paired blades do not appear in smithing or town crafting orders and cannot be player-crafted. Players formally obtain their pair when joining or rejoining the Wardens.
- Dual-blade actions now load as an independent resource assigned only to players and AI that enter battle with the complete pair; encyclopedia, Custom Battle, and ordinary hero displays load only the native human actions.
- AI dual wielding no longer patches global Agent or weapon-data methods. Qualification data is written only to the existing off-hand weapon of a real battle AI after that agent finishes spawning, keeping encyclopedia and Custom Battle display characters outside the path.

#### Fixed

- Fixed severe-wanted players being approached by a Warden lord without entering the enforcement conversation; the fine, atonement, or refusal choices now open normally with repeat-contact protection. Accepting atonement or payment now fully closes the meeting instead of reopening the same dialogue.
- Fixed pickets travelling to the player's old position during a pursuit; they now follow the player's live position and initiate the meeting once they are close enough.
- Player warrants now enter the Warden lord's native decision auction at normal enforcement priority; when selected, the lord follows the player's live position instead of travelling to an old coordinate first.
- Fixed GreyWarden failing to load and closing the game at startup after upgrading to Bannerlord 1.4.8 because of changed game interfaces.
- Fixed the incomplete dual-wield resource load order that prevented the game from starting while action files were being read.
- Fixed the left blade being generated as an ordinary main-hand weapon, which left it invisible and disabled attacks and blocks after drawing the pair.
- Fixed new Sandbox campaigns closing during creation when a town crafting order selected the dual-blade-only template.
- Fixed the game closing when the paired blades were picked up from the ground. Either blade may now be collected first; both enter their required slots and are drawn safely once the pair is complete.
- Fixed encyclopedia and Custom Battle character previews disappearing or entering broken poses after ground pickup support was added, while retaining both pickup orders.

## Contact

- Bilibili: Lucicain
- Personal QQ: 157652226
- QQ group: 981323752
