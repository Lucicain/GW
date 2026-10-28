# GreyWarden / 灰袍守卫

GreyWarden adds an independent religious order descended from the Imperial church to Mount & Blade II: Bannerlord. Its members protect civilians, pursue raiders, support villages and train successors. Village priests and believers provide recruits and supplies. Players can face the Wardens for their crimes, or earn their trust, join the order and take commissions.

This guide describes v1.4-r16 (2026-10-28) for Bannerlord 1.4.8. Release packages include runtime art and exclude editable sources and editor folders. For Bannerlord 1.5.2, use the previously released v1.5-r1.
中文：README.md

## Main features

- Six founding Warden lords lead their own parties, handling training, trade routes, village protection, local requests, rebuilding and player affairs. Adult successors inherit duties that still have an adult holder.
- The Wardens record attacks on caravans and villagers, and village raids. They first take the nearest eligible case in their own field, gather allies against strong opponents and send detachments drawn from existing troops, with cavalry preferred.
- Grey Warden Affairs has five tabs: My Affairs, Members, Duties, Faith and Armory. It shows assigned Wardens, messenger journeys and separate negotiation reports, and the Armory tab organises your party equipment. Offenders retain criminal records and face deterrence that gradually wears off.
- Players can earn reputation by protecting civilians and helping the Wardens. Membership grants access to commissions, troop orders, battlefield support and fief appeals.
- When a target hides in a town or castle, you can lead troops in to search for them: get past the watch, negotiate, duel or brawl in the lord's hall, then take them out of the settlement. You can also ask the Wardens for a black-armoured case team to come along.
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
- Periodic support is not yet limited to stalled cases, so prolonged unresolved cases can cause repeated deployments.
- During city searches, armed townspeople who are not fighting draw their weapons before fleeing; hitting the watch with arrows or thrown weapons does not start a fight.

## Installation and updates

1. Delete the old Modules/GreyWarden folder.
2. Extract the complete GreyWarden folder into the game's Modules directory.
3. Enable GreyWarden in the launcher.

Start a new campaign for this release. Saves are not supported across versions. Replace the complete module when updating.

## Changelog

### 2026-10-28 v1.4-r16 (Bannerlord 1.4.8)

Compared with v1.4-r15 (2026-10-17):

#### Additions and changes

- New city search: when a target has no party and is hiding in a town or castle lord's hall, you can take the commission and lead a small band of soldiers through the gate yourself. The watch will question you; you can talk your way in, withdraw, or force your way through.
- Confront the target in the hall. If talks succeed, escort them out of the settlement to complete the case. If talks fail, both sides settle on a duel or a brawl: towns with an arena hold the duel there, otherwise the hall is cleared for it.
- Fighting inside is war with the owner's faction: the garrison keeps sending reinforcements that pursue you, townspeople and merchants flee, and you must fight your way out through the gate. Casualties on both sides and the fallen in the streets persist between the hall and the streets.
- Each sheltered case gives you one personal search and one request for help. After you ask for help, a Warden lord sends a black-armoured case team that follows you into the settlement; it appears locked in the troop selection and is replaced if it is destroyed on the way. The Wardens also send case teams of their own to collect fines from sheltered offenders.
- Rebuilt Grey Warden Affairs: commissions, orders, peace and fief business are shown separately; choose commissions from the case list and follow assignees, messenger journeys and negotiation reports. Fief reconsideration requests can be sent by messenger.
- The player armory is open: organise your party's armory from Affairs, keeping the best equipment for your establishment and returning the rest to your inventory.
- Armory losses are now settled per soldier: issued equipment left on the field is captured or destroyed only when that side loses. Warden lords keep their own armories.
- Your current pursuit target is marked in red as a case target on its map nameplate.
- When joining a battle involving caravans, villagers, raids or Warden enforcement, choosing a side first opens a preparation screen where you can go back or stay out; you join only after confirming.
- Warden soldiers now fight on past lethal wounds too, for different durations for recruits, light infantry, heavy infantry and archers; the lords' duration is shorter. Shooting, throwing, kicks, shield bashes and kills all make Wardens grow during the battle.
- Surplus low-tier village guards are promoted two-for-one at the burnings; the training lord delivers veterans in a single trip; villager parties make dedicated trips to sell goods in town.
- The Wardens declare war only once they can actually send an interceptor, and re-check nearby strength before declaring or attacking. No new case war starts while both sides are inside settlements.
- After your request for help arrives, your own forces count towards the Wardens' strength. If a target is held by a third party, you can buy them out or withdraw the commission.
- Arranged duels move to another town if the venue becomes hostile, besieged or rebellious. Warden envoys no longer have a travel deadline and are replaced if destroyed on the way.
- New campaigns no longer set your relation with Warden members to the maximum, and Warden relations with local notables change normally; free courtship remains.

#### Fixes

- Fixed a possible crash when a surrendered offender's former party stayed attached to an army.
- Fixed handovers starting before boarding or landing had finished; courier and envoy handovers between sea and land are more reliable.
- Ordinary pursuits keep sending replacement interceptors after a detachment is lost; other existing soldiers fill in when healthy cavalry are short.
- Recalibrated battle music volume.

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

## Contact

- Bilibili: Lucicain
- Personal QQ: 157652226
- QQ group: 981323752
