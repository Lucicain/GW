# GreyWarden

GreyWarden adds an independent law-enforcement clan that handles real cases on Bannerlord's campaign map. Six Warden lords investigate crimes, pursue offenders, assemble assistance armies, aid settlements, and raise successors. The player may be pursued for crimes or earn the Wardens' trust, join them, and accept contracts.

v1.4-r14 is for Bannerlord 1.4.8. Players on 1.5.2 should use the earlier v1.5-r1 package; the additions in this release have not been released for the 1.5 branch.
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

## Installation and updating

1. Delete the old Modules/GreyWarden folder.
2. Extract the complete GreyWarden folder from the archive into the game's Modules directory.
3. Enable GreyWarden in the launcher.

Existing campaigns remain supported. Finish any old bounty already in progress before updating because it is not carried into the revised system. Replace the complete module rather than copying only part of it.

## Changelog

### 2026-10-02 v1.4-r14 (Bannerlord 1.4.8)

Compared to v1.4-r13:

#### Added and changed

- A new Grey Warden affairs button sits in the bottom-left map bar between Clan and Kingdom. Once you have joined the Wardens it opens their affairs directly; before that it is unavailable.
- The equipment issued when you join or rejoin the Grey Wardens now comes in the best quality for every piece.
- Riders carrying ordinary prisoners go to the nearest town to sell them first, then resume their errand, and tell you how much the sale brought in.
- Prisoners escorted by your riders no longer escape on the road.
- When a companion with an outstanding case travels in your party, the Wardens come to you about that companion's own case, and you can pay the fine on their behalf to close it. Their case is no longer counted against you, and you are no longer stopped over it again and again.
- A contract target you stop on the way to join an army no longer refuses to talk on the strength of the whole army, and a refusal no longer names a specific day.

#### Fixed

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
