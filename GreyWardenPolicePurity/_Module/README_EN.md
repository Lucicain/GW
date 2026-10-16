# GreyWarden

GreyWarden adds an independent religious order descended from the Imperial priesthood. Its members regard protecting the common folk and pursuing raiders as duties of faith. Six Warden lords handle cases, aid villages, and raise successors on the campaign map, while village priests and congregations provide recruits and supplies. Players may be pursued for crimes or earn the Wardens' trust, join them, and accept contracts.

This local player build is v1.4-r15 (16 October 2026) for Bannerlord 1.4.8, provided for playtesting and feedback. The latest runtime art is included; art source files and editor directories are excluded. Players on 1.5.2 should use the earlier v1.5-r1 package.
Chinese: README.md

## Main features

- Warden lords lead their own parties and handle training, caravan protection, rural protection, local petitions, village reconstruction, and player affairs. Adult successors inherit duties that still survive.
- Wardens record attacks on caravans and villagers as well as village raids, then pursue offenders by duty and distance. Strong targets draw assistance armies, while fast targets may be intercepted by cavalry.
- The Case Ledger shows open cases, assigned parties, and the judicial treasury. Captured lords retain a criminal record and recover gradually from deterrence, while repeat offenders face stronger lasting suppression.
- Players can earn standing by protecting civilians and helping cases, join the Wardens, take bounties, order troops, receive battlefield support and village gifts, and appeal a fief decision.
- Wardens physically rendezvous to exchange and deliver troops, resolve local issues, rebuild raided villages, and manage case income, operating expenses, and naval ships.
- The mod includes a Warden troop tree, black-and-gold equipment, Warden weapons with their own combat effects, the Warden warhorse, dual blades, a dedicated shield, kicks, shield bashes, passive great-shield protection, and sparring with Warden lords. Grey Wardens never rout in battle and have their own battle music.

- A recruitment invitation becomes available at 20 Grey Warden standing. Accept it to use the dispatch conversation with Warden soldiers in your party. New contracts also require sufficient standing and the Warden commander set.
- Player-led cases include negotiation, collecting money and goods, prisoner delivery, and soldiers dispatched for support or reports. Keeping case proceeds or using excessive force can lead to a later investigation.

## Changes in this player build

Compared with the formal v1.4-r14 replacement package dated 7 October 2026:

- Warden priests preach in villages, gather believers to defend them, and hold congregations and burnings. Rituals have artwork, sound, and village smoke.
- Congregations donate money and goods, receive prisoners and equipment, and maintain stores of arms. Once a priest has established herself in a village, its defenders grow over time like a militia.
- Warden parties and faith villages keep armories that issue captured weapons and armour to Warden soldiers from the most senior down: veterans only fill gaps in their own kit, while recruits and zealots take better gear. Horses, capes, and anything soldiers cannot use stay out of the armory and are sold.
- Villages send out missions and armsbearers, while traders take surplus goods to towns. Players can meet these parties, speak with them, and buy their goods.
- Warden lords visit villages for recruits (taking the least experienced defenders first), supplies, prisoner handovers, and care for the wounded. When the training lord has many veterans, she sends a transport that delivers veterans and surplus arms to strengthen a village's defenders.
- Other soldiers in a Warden lord's party are converted into Warden soldiers of matching rank, and their equipment goes to the armory.
- Wardens can send an envoy to negotiate before pursuing a case. The accused may pay, ask for time, propose a duel, or submit to the envoy.
- A new campaign starts on excellent terms with the Warden lords. Players who meet the marriage requirements can propose directly to any adult Warden lord; if she leads the clan, another Warden takes over first. A Warden lord who marries you keeps her Warden lord combat abilities.
- Warden lords gain the combat effects of Warden equipment with any weapon, shield, or mount, and so does the Warden commander in Custom Battle. When struck down they hold on briefly at the brink; knocking out an enemy during that time restores their health, and every kill makes them stronger for the rest of the battle.
- The order, its priests, and its congregations now have consistent descriptions and dialogue, with revised village menus and map notices. Five founding lords have new appearances; the clan leader retains hers. The six founding lords now have distinct voices and personalities.
- The Warden clan has a new black banner with a golden bird. Warden warhorses come in several coat colours.
- Updated helmet faceplate positions and clothing cloth assets.
- Assistance armies form and disband far less often. Returning detachments, borrowed ships, and transfers of wounded troops and experience are improved, and detachments that return on land rejoin their lord first. Ongoing duties should resume after loading a save made with the same version.
- Fixed the atonement task offered at negative standing: the target can now be negotiated with, Warden envoys no longer take over the same case, and the task can be turned in once resolved.

Useful feedback includes whether villages develop naturally, envoy negotiations are clear, the Wardens feel too strong or weak, and any wording that remains confusing. If something goes wrong, note what you were doing and the people and place involved.

## Known issues

- When a village has fewer than two defenders and no militia party yet, returning troops may lose their wound status and remaining experience.
- Intermittent crashes when exiting the game remain unresolved. Whether this player package reproduces them is still unconfirmed.
- The game occasionally closes during play without an error message. The cause is still under investigation; please tell us what you were doing when it happens.
- Performance as village activity and armory stocks grow still needs observation.

## Installation and updating

1. Delete the old Modules/GreyWarden folder.
2. Extract the complete GreyWarden folder from the archive into the game's Modules directory.
3. Enable GreyWarden in the launcher.

Start a new campaign for this version. Saves are not compatible across versions. Replace the complete module rather than copying only part of it.

## Changelog

### 2026-10-07 v1.4-r14 (Bannerlord 1.4.8)

Compared to v1.4-r13:

#### Added and changed

- A new Grey Warden affairs button sits in the bottom-left map bar between Clan and Kingdom. Once you have joined the Wardens it opens their affairs directly; before that it is unavailable.
- The equipment issued when you join or rejoin the Grey Wardens now comes in the best quality for every piece.
- Riders carrying ordinary prisoners go to the nearest town to sell them first, then resume their errand, and tell you how much the sale brought in.
- Prisoners escorted by your riders no longer escape on the road.
- When a companion with an outstanding case travels in your party, the Wardens come to you about that companion's own case, and you can pay the fine on their behalf to close it. Their case is no longer counted against you, and you are no longer stopped over it again and again.
- A contract target you stop on the way to join an army no longer refuses to talk on the strength of the whole army, and a refusal no longer names a specific day.

#### Fixed

- Grey Wardens remain independent instead of joining a kingdom, becoming mercenaries, or founding a kingdom.
- When a save loads, Wardens who have joined another kingdom leave it and its armies, restoring their independence.
- With RBM installed, Warden lords no longer automatically replace their equipment when entering a town.

- Asking the Wardens to mediate from the post-battle screen could crash the game.
- Warden lords sometimes took up and dropped the same case they lacked the strength for, over and over.
- After loading a save, Warden assistance armies could no longer take in members and failed when disbanding.
- Reporting right after capturing a contract target did not list the prisoner until you had moved on the map.
- Riders carrying prisoners could head for a distant town, give up before reaching it and turn back, going back and forth.
- Ships lent to your riders or to Warden detachments were sometimes not returned to their owner.

### 2026-09-25 v1.4-r13 (Bannerlord 1.4.8)

Compared to v1.4-r12:

#### Added and changed

- New Grey Warden battle music: when Grey Wardens make up at least half of either side's regular troops, the battle switches to a segmented Syndicate-style score whose intensity follows the fighting.
- Grey Wardens fight to the end: their morale still rises and falls, but they never rout.
- Battlefield support now has three chances: once when your side falls to a fifth of its starting strength, once at a tenth, and once when only one fighter is left; the first success ends the rolls. Its size depends on the enemy's remaining strength, and higher Grey Warden standing makes it both likelier and larger. Grey Warden soldiers and lords on the field can receive support on either side, and so can a Grey Warden character in custom battle.
- All Grey Warden troops are one tier higher, and each soldier now takes the field in one complete outfit instead of a mix of pieces.
- Grey Warden weapons carry their own combat effects, whoever wields them: the Warden sword, mace, two-handed sword and lance can knock down, dismount or break a weapon parry; the dual blades knock down more often; Warden piercing arrows can knock down foes on foot and deal double damage to shields; the Warden great shield holds up longer.
- Grey Warden heavy infantry now carry the new Grey Warden Calradic Mace, and Grey Warden archers the new Grey Warden Piercing Arrows.
- Grey Warden knights now carry a lance and a two-handed sword instead of a sword and shield. The lance is longer and can be braced on foot.
- Grey Warden knights ride the new Grey Warden warhorse: its charge throws and knocks down infantry more easily, it rears less often, and minor wounds do not check it. Damage to the rider is split evenly between rider and horse. Anyone riding a Grey Warden warhorse gets these effects.
- The player and Grey Warden archers now share the same off-hand blade.
- The battle-long skill bonus earned from kicks, shield bashes and arrows is now capped.
- Grey Warden detachments and the riders you send out no longer need food, so they no longer starve on long trips.
- Sending riders no longer requires rations or a travel purse. Riders carrying prisoners will stop in a town to sell them.
- Grey Warden detachments borrow ships from Warden lords and your riders borrow yours, so both can cross water. You cannot send riders while at sea without a ship to spare.
- Routing a contract target in the field now completes the contract for the full case fee; bringing the target in alive earns an additional ransom.
- A training cohort keeps following its trainer while the trainer is fighting or marching with an army.
- An interception detail drawn from a Warden lord's company hands its men to the nearest Warden lord if their own lord is defeated or captured, instead of losing them.

#### Fixed

- A troop order could never be completed: other troop types or outside soldiers joining the cohort filled its places. They are now sent back and replaced with the ordered troops.
- The training cohort trained some soldiers into troop types that were not ordered.
- The ledger's count of finished troops did not match the number that could actually be delivered.
- Training cohorts and riders were wounded by starvation on long trips.
- Routing a contract target was sometimes treated as the case having been closed by someone else, paying only a small fee.
- Quarrels taken up while fighting for the Wardens sometimes offered no mediation option when talking to a Warden lord.
- The game could crash when cavalry charged into Grey Warden archers.
- Grey Warden archers were slow to return to their bows after drawing their dual blades.
- The passive great-shield guard still caused lost health and a flinch.

## Contact

- Bilibili: Lucicain
- Personal QQ: 157652226
- QQ group: 981323752
