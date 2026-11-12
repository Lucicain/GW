# GreyWarden / 灰袍守卫

GreyWarden adds an independent religious order descended from the Imperial church to Mount & Blade II: Bannerlord. Its members protect civilians, pursue raiders, support villages and train successors. Village priests and believers provide recruits and supplies. Players can face the Wardens for their crimes, or earn their trust, join the order and take commissions.

This guide covers r16.2 (2026-11-12). Use v1.4-r16.2 for Bannerlord 1.4.8 or v1.5-r16.2 for Bannerlord 1.5.7. Both downloads contain the same Grey Warden gameplay, art and text.
中文：README.md

## Main features

- Six founding Warden lords lead their own parties, handling training, trade routes, village protection, local requests, rebuilding and player affairs. Adult successors inherit duties that still have an adult holder.
- The Wardens record attacks on caravans and villagers, and village raids. They first take the nearest eligible case in their own field, gather allies against strong opponents and send detachments drawn from existing troops, with cavalry preferred.
- Grey Warden Affairs has five tabs: My Affairs, Members, Duties, Faith and Armory. It shows assigned Wardens, messenger journeys and separate negotiation reports, and the Armory tab organises your party equipment. Offenders retain criminal records and face deterrence that gradually wears off.
- Players can earn reputation by protecting civilians and helping the Wardens. Membership grants access to commissions, troop orders, battlefield support and fief appeals.
- When a target hides in a town or castle, you can lead troops in to search for them: get past the watch, negotiate, duel or brawl in the lord's hall, then take them out of the settlement. You can also ask the Wardens for a black-armoured case team to come along.
- The mod includes a Warden troop tree, black-and-gold equipment, warhorses, shields, dual blades, kicks, shield bashes and sparring. Wardens fight without routing and have their own battle music.
- Player troop orders are paid when placed and delivered by the training lord when ready. An order messenger carries the order and payment, not the trained soldiers.
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

## Changelog

### 2026-11-12 r16.2 (Bannerlord 1.4.8 / 1.5.7)

If you downloaded the November 11 build of v1.4-r16.2, download the updated archive. After upgrading to Bannerlord 1.5.7, use v1.5-r16.2.

#### Additions and changes

- Offenders have more ways to respond in negotiations based on their personality. They may pay extra, name their own price, retreat to a settlement with their troops, or pay in full and lead a party to rebuild a village.
- An offender who agrees to surrender to an envoy can lead their party back to a settlement in their own realm before handing themselves over.
- Aethelflaed is the adoptive mother of the other five founding lords. Three of her daughters each adopt one child at the start, and children later adopted from villages are also recorded in the family tree.
- The hero encyclopedia uses the order's own terms for Grey Warden adoptive relationships. The standard encyclopedia calls adopted peers Fellow Wardens.

#### Fixes

- Fixed GreyWarden failing to load after the Bannerlord 1.5.7 update.
- Fixed a mod compatibility error.
- Fixed an error when smelting paired blades.
- Fixed arranged duels that stayed open after the winner was decided, and parties that kept patrolling outside their duel venue.
- Fixed targets leaving a settlement after a city search was accepted, and case teams sometimes heading to the wrong settlement.
- Fixed party assignments and pending peace requests carrying into a new campaign after returning to the main menu.
- Fixed repeated notifications when a fine was refused.

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

## Contact

- Bilibili: Lucicain
- Personal QQ: 157652226
- QQ group: 981323752
