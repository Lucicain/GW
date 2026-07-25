# GreyWarden

GreyWarden adds an independent law-enforcement clan that investigates and resolves cases on Bannerlord's campaign map. Six Warden lords detect crimes, pursue offenders, assemble assistance armies, aid settlements, and raise successors. The player may be judged as an outlaw or earn the Wardens' trust and join their work.

One package supports **Bannerlord 1.4.x (1.4.5, 1.4.6, and 1.4.7)**.
中文：[README.md](README.md)

## Main features

- **A living Warden clan:** Six founding women lead their own parties and specialize in training, caravan protection, rural protection, local petitions, village reconstruction, and player affairs. Adult successors inherit duties that still survive; an office can disappear permanently when its last holder dies.
- **Continent-wide enforcement:** Wardens record attacks on caravans and villagers as well as village raids, then assign cases by duty and distance. Pursuit creates limited wars with a stated enforcement reason. Strong targets draw assistance armies, while fast targets may be intercepted by detached cavalry.
- **Cases, records, and deterrence:** The Case Ledger lists open cases, assigned parties, assistance tasks, and the judicial treasury. Lords captured by Wardens keep a lasting record and receive crime-specific deterrence that recovers over time; repeat offenders develop an increasingly high recovery floor.
- **Player standing and the outlaw route:** Protecting civilians and helping Warden cases raises standing. Attacking civilians, raiding, coercion, or resisting arrest lowers it. Lesser offences bring provost patrols and fines; serious offences bring pursuit by a Warden lord, imprisonment, or an atonement assignment.
- **Membership and bounties:** At sufficient standing, a herald seeks out the player. Members receive commander equipment, may take bounties on real offenders, gain a Warden escort and tracking updates, and collect payment after completing the case. The player may leave and later reapply at a higher standing requirement.
- **Standing-based support:** Members can order troops that the Training Warden collects, trains, and personally delivers from real Warden forces. Higher standing raises order limits, unlocks elite troops, and lowers prices. Eligible members may also receive battlefield relief, collect village gifts, and appeal a fief decision after helping in a siege without receiving land.
- **Training and physical exchanges:** The Training Warden develops Warden troops, then meets other Warden lords in settlements to exchange elite soldiers for lower-tier troops that still need training. Shortages for player orders use the same physical rendezvous instead of creating soldiers from nothing.
- **Settlement work:** Wardens resolve native town and village issues, rebuild raided villages, and may adopt a girl during disaster relief as a future clan member. Her origin, growth, and adoption history remain visible in the encyclopedia.
- **Judicial treasury and naval support:** Fines, troop-order payments, case funding, and village protection income enter the treasury; reconstruction and other duties spend it. With naval content enabled, Wardens manage ships according to party needs and sell excess captured vessels.
- **Readable case information:** Sighting locations in quest logs and criminal-record locations in hero encyclopedia details are clickable settlement links. Warden war pages explain whether the conflict comes from pursuit, resisting arrest, or a bounty.
- **Dedicated combat content:** The mod includes a Warden troop tree, black-and-gold equipment, the obsidian commander's shield, kicks, shield bashes, and passive great-shield protection. In peacetime, the player may also spar with Warden lords in towns or in the field.

## Installation and updating

1. Delete the old `Modules/GreyWarden` folder.
2. Extract the complete `GreyWarden` folder from the archive into the game's `Modules` directory.
3. Enable `GreyWarden` in the launcher.

Existing saves remain supported. Replace the complete module when updating rather than copying only part of it.
Version 1.4.5 has been tested in game, 1.4.6 passes a full interface cross-build, and 1.4.7 remains the routine-test baseline.

## Changelog

### 2026-07-26 v1.4-r8

Compared with `v1.4-r7`:

#### Added and adjusted

- Repeated arrests now increase both deterrence strength and its minimum recovery level. A first arrest can still recover fully, while persistent repeat offenders eventually remain at maximum suppression. Special deterrence greetings now trigger probabilistically.
- Sighting locations in bounty and atonement logs, plus locations in hero criminal-record details, are now clickable links to the relevant settlement encyclopedia page.
- Ordinary case parties and assistance armies may both detach fast cavalry interceptors. Pursuit speed is estimated from normal party composition rather than temporary terrain or conditions.
- The three elite Warden upgrade branches now use equal selection weight. Landless Wardens also sell captured ships beyond their party's needs.

#### Fixed

- Fixed troop orders crashing immediately when the Training Warden had none of the requested troop type, and fixed prepared troops upgrading again and reducing the delivery stock.
- When an order is short, the Training Warden now meets other Warden lords for a physical exchange and continues to another party if the first cannot supply enough.
- Fixed an error when opening hero criminal-record and deterrence details. The compact native popup appearance is restored while inline settlement links remain available.

### 2026-07-25 v1.4-r7

Compared with `v1.4-r6`:

#### Added and adjusted

- Added training exchanges, a three-branch elite troop tree, physical troop orders, and fief appeals. Joining or rejoining also grants the obsidian commander's shield.
- Hero encyclopedia pages now estimate deterrence recovery time, and deterrence lasts longer.
- Assistance armies assess the target and nearby support, keep adding available lords, and close the case peacefully if every eligible force would still be insufficient.
- Wardens choose between gathering, dispersed pursuit, and cavalry interception according to local strength and pursuit speed.

#### Fixed

- Fixed weaker lords failing to request help, assistance parties failing to gather, fast targets slowing armies, and viable cases stalling instead of entering battle.
- Fixed offenders hiding in settlements, blocking gates, or attaching to other armies and causing prolonged standoffs.
- Fixed cases and empty armies lingering after the initiating Warden was defeated, including the related crash.
- Fixed the Training Warden and Noble Affairs Liaison being interrupted by ordinary dialogue or duties, repeatedly intercepting the player, or entering battle after a handover.
- Fixed fief appeals resolving too early, preventing the player from voting, or ignoring support spent on the player's claim.
- Fixed the player losing Warden standing or receiving a fine merely because Wardens pursued another offender from the player's kingdom.
- Provost patrols now initiate conversation on contact. Also fixed a possible crash after returning from a field spar initiated inside a castle.

## Contact

- Bilibili: `Lucicain`
- Personal QQ: `157652226`
- QQ group: `981323752`
