# GreyWarden / 灰袍守卫

GreyWarden adds an independent religious order descended from the Imperial church to Mount & Blade II: Bannerlord. Its members protect civilians, pursue raiders, support villages and train successors. Village priests and believers provide recruits and supplies. Players can face the Wardens for their crimes, or earn their trust, join the order and take commissions.

This guide describes the current development build for Bannerlord 1.4.8. The latest published release remains v1.4-r15 (2026-10-17); changes listed under "In development" have not been released. Release packages include runtime art and exclude editable sources and editor folders. For Bannerlord 1.5.2, use the previously released v1.5-r1.
中文：README.md

## Main features

- Six founding Warden lords lead their own parties, handling training, trade routes, village protection, local requests, rebuilding and player affairs. Adult successors inherit duties that still have an adult holder.
- The Wardens record attacks on caravans and villagers, and village raids. They first take the nearest eligible case in their own field, gather allies against strong opponents and send detachments drawn from existing troops, with cavalry preferred.
- Grey Warden Affairs has five tabs: My Affairs, Members, Duties, Faith and Armory. It shows assigned Wardens, messenger journeys and separate negotiation reports. The player armory is not yet open. Offenders retain criminal records and face deterrence that gradually wears off.
- Players can earn reputation by protecting civilians and helping the Wardens. Membership grants access to commissions, troop orders, battlefield support and fief appeals.
- The mod includes a Warden troop tree, black-and-gold equipment, warhorses, shields, dual blades, kicks, shield bashes and sparring. Wardens fight without routing and have their own battle music.
- The training lord sends healthy veterans to villages. Player troop orders are paid when placed and delivered by the training lord when ready. An order messenger carries the order and payment, not the trained soldiers.
- At 20 Warden reputation, a recruitment messenger comes to invite you. Members can talk to Warden soldiers in their party and send them on errands. New commissions require membership, at least 20 reputation and a Warden duty standard equipped in the banner slot. Unequipping it disables new commissions; existing work continues. Choose cases in Affairs; periodic commission offers have been removed.
- The map-bar Affairs button requires membership and 20 reputation. The Warden clan's encyclopedia page also has a browsing entrance for non-members; browsing grants no membership or commission rights.
- In Duties, cases appear first and are sorted by the offender's current distance from the player. Rows show offender and assignee portraits. Select a case to see the estimated case fee, current location and strength. The case fee is separate from the fine.
- Commissions allow negotiation, collection of fines and goods, prisoner delivery and requests for help. Keeping case proceeds or using excessive force can bring an investigation.
- Resolve a case through negotiation, defeat the offender's party or capture the offender, then report back. Case fees are assessed separately; delivering a captive also earns the ransom value. Wars entered on the Wardens' behalf can be referred to them for mediation in person or by messenger.

## Known issues

- A crash has occurred while exiting the game; the cause remains unresolved.
- A lord may stay in a settlement for a long time when threatened by enemies outside.
- Performance in campaigns with many developed villages and large armories still needs observation.
- In the development build, periodic support is not yet limited to stalled cases, so prolonged unresolved cases can cause repeated deployments.

## Installation and updates

1. Delete the old Modules/GreyWarden folder.
2. Extract the complete GreyWarden folder into the game's Modules directory.
3. Enable GreyWarden in the launcher.

Start a new campaign for this release. Saves are not supported across versions. Replace the complete module when updating.

## Changelog

### In development (2026-10-19, not released)

- Rebuilt Affairs with case selection, player messenger journeys and separate negotiation reports, distinguishing case support, peace requests, fief reconsideration and troop orders.
- Added native portrait frames, offender and assignee portraits; corrected case sorting, footer overlap and return from encyclopedia pages.
- Ordinary pursuits keep checking for replacement interceptors after a detachment is lost. Other existing soldiers can fill places when healthy cavalry are insufficient.
- Declarations of war and case attacks check native regional strength. No new case war is declared while either party is in a settlement; following continues and eligibility is checked again outside.
- Warden envoys have no travel deadline. They wait if the offender temporarily loses their party; a lost envoy counts as an unsuccessful negotiation.
- Prevented handovers before navigation transitions finish. Player messengers and Warden envoys share the sea/land handover check.
- Recalibrated battle music volume and reviewed quest, dialogue and gameplay descriptions.

### 2026-10-17 v1.4-r15 (Bannerlord 1.4.8)

Compared with v1.4-r14 (the replacement package dated 2026-10-07):

#### Additions and changes

- Warden priests preach, gather believers to defend villages and hold assemblies and executions by fire. Ceremonies include illustrations, sound and village smoke. Zealots now wear pilgrim, monk and ascetic clothing.
- Believers donate money and goods, receive prisoners and equipment, and maintain village armories. Established priests build up village guards; Warden lords visit to recruit, deliver supplies and tend the wounded.
- Villager parties recruit Warden troops in proportion to the village's believers and exchange them for veterans from the village guard. Villages can transfer soldiers, prisoners and equipment; villager parties take surplus goods to town.
- Armories issue equipment to senior troops first. Light infantry and higher ranks keep their existing kit and fill empty slots; recruits and zealots can replace their gear. Surplus horses, cloaks and equipment are sold.
- The training lord sends healthy, fully trained veterans and surplus equipment to villages in smaller groups.
- Wardens send envoys before pursuing a case. Offenders may pay, request time, propose a tournament duel or surrender. An envoy already on the road finishes the negotiation.
- Criminal records stay with the offender. Defeat, capture or temporarily losing a party does not erase outstanding guilt; pursuit can resume when the offender returns.
- Against strong opponents, Wardens call allies who can arrive in time. The lead Warden follows the offender while allies gather, waits when nearby enemies are too strong, and can follow an offender into a settlement.
- Villages with more believers are less attractive targets for raids, alongside the Wardens' existing deterrence.
- Equip the Warden duty standard to take new commissions and display the order's black-and-gold emblem; unequip it to restore your own. Commissions can be taken from Grey Warden Affairs. Leaving returns the standard; rejoining grants a standard without another full equipment set.
- Warden lords wear religious civilian clothing in towns and castles. Five founders have new faces, the clan leader retains her face, and all six have distinct voices and personalities.
- Warden lords retain their combat abilities with ordinary weapons, shields and mounts, including actual tournament fights.
- The order has a new gold bird emblem on a black field, a ceremonial duty standard, varied horse coats, adjusted helmet face openings and updated cloth.
- Revised descriptions and dialogue for the order, priests and believers. Arranged duels, local requests and relief duties are handled in turn, with paused work resuming afterward.

#### Fixes

- Atonement commissions at negative reputation allow negotiation and completion; Warden envoys no longer intervene in a case already assigned to the player.
- Improved temporary-party reunions, borrowed-ship returns and transfers of wounded troops and experience.

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

## Contact

- Bilibili: Lucicain
- Personal QQ: 157652226
- QQ group: 981323752
