# AI 领主震慑台词与震慑状态

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[罪案登记与震慑](../state/crime-intake.md#震慑gwpaideterrencestate)。分组见[清单](text-review-inventory-20261104.md)第 4 批；[可疑用词核实稿](text-review-term-check-20261104.md)「附带发现」建议本组单独出稿。

场景说明只给审阅时看。本组问题集中，改动多，表里只列现中文；英文现稿与中文意思基本对应，改稿中英一起给。

## 核实到的情境（作者用，不进台词）

- **什么时候出现**：玩家和任何一位非灰袍领主开始对话时（不含战后俘虏对话），若这位领主的震慑总量 ≥ 0.25，每次对话有 50% 的概率（`PoliceAIDeterrenceBehavior.DeterrenceGreetingChance`）先说这一段，再回到原版领主对话菜单。玩家没有先提灰袍。
- **组成**：一句旁白（`GetIntro`）＋一个对话框里的两半句（`GetResponse` = 前半句 + 空格 + 后半句）。旁白按来源 × 档位 6 句；前半句按来源 × 档位 × 口吻 66 句；后半句按玩家是否入会 × 档位 × 口吻 66 句。档位：总量 ≤ 3 轻、≤ 6 中、更高重。口吻按五项性格的绝对值加权随机抽，全为 0 时用 neutral。
- **「本人」来源**：本人被灰袍或承办的玩家押走、和平交罚金、塞私钱、约斗（他赢了也算）、被打垮没被抓，都会记直接震慑（[罪案登记](../state/crime-intake.md)，用户裁定 2026-10-17）。所以「被捕」「锁链」只对一部分人成立。
- **「身边的人」来源**：同族的人受惩处会转述一半；**同一场战斗里一起战败、自己没案子的别家领主**也会记一份（`ApplyBattleWitnessShock`）。所以「家族」「族人」「血亲」对后一种人不成立。改稿统一用「我们的人」「身边的人」。
- **村庄与商队合在一起**：`TryBuildPainDialogue` 把村庄暴力和商队袭击两套点数相加后选台词，只劫过商队的领主也会说「烧毁一个村庄」。改稿用「抢」「村子和商队」。
- **对玩家说的后半句**：`IsPlayerGreyWardenMember` 判断入会，不看执勤旗。玩家拿的是灰袍交办的案子，没有纸面通缉令，也没有灰袍的印信（核实稿）。没入会的玩家可能从没劫掠过，原稿默认对方正要去烧村。
- 两半句与说话人当下处境无关：同一位领主可能是玩家亲手办过的人，台词不能假定玩家和那回的事有关。

## 通用问题

1. **格言式空话**：「权威在成为特权以前，首先是一份债务」「怒火消失的速度，总比后果更快」「恐惧很快就会耗尽」一类，是本组最大的问题，改成领主当面说的口语。
2. **说错条件**：「家族/族人」31 条、「被捕/锁链」4 条、只说「村庄」12 条（与别的问题重叠计）。
3. **编造**：后半句里「通缉令」「纸上的墨迹」「印记」6 条（另 2 条已在核实稿）。
4. **称呼**：灰袍由女性统领，已批准文本用「她们」，原稿多处用「他们」。
5. 旁白里「其家族」「其举止」生硬。

## 待用户定

- **出现时机**：现在玩家跟任何一位受过震慑的领主搭话，都有一半机会先听一段灰袍往事，没入会的玩家也一样，没有由头。是否改成只在玩家已入会时出现，或同一位领主隔一段时间才说一次，属玩法改动，另批。
- **旁白显示在领主的对话框里**：原版领主对话没有这种第三人称旁白。保留旁白（本稿已改顺），还是去掉、只留领主自己的话，请定。

## 逐条

### 一、开场旁白

[领主开口前的一句神态描写，按震慑来源（本人 / 身边的人）和档位取一句。显示在领主的对话框里。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_intro_personal_low` | {HERO_NAME}平静地看着你。与灰袍的那次交锋仍被其记在心里，却还没有左右其举止。 | 「其」生硬 | {HERO_NAME}看了你一眼，像是想起了跟灰袍打交道的那回事。 | {HERO_NAME} gives you a look, as if remembering a past run-in with the Grey Wardens. |
| `gwp_ai_det_intro_personal_medium` | {HERO_NAME}的语气变得更加慎重。亲自面对灰袍的经历，至今仍影响着其说出的每一句话。 | 「其」生硬 | {HERO_NAME}说话时斟酌着字句，显然还记着灰袍那回的事。 | {HERO_NAME} chooses each word with care, plainly still mindful of the Grey Wardens. |
| `gwp_ai_det_intro_personal_high` | {HERO_NAME}刻意保持镇定，直视着你。灰袍曾在其身上留下的影响，已经深得难以掩饰。 | 「其」生硬 | {HERO_NAME}强作镇定地看着你，可灰袍那回的事显然还压在心上。 | {HERO_NAME} meets your eye with forced calm, but the business with the Grey Wardens plainly still weighs on them. |
| `gwp_ai_det_intro_family_low` | {HERO_NAME}开口前短暂停顿了一下。灰袍与其族人之间发生的事，显然还没有被完全忘记。 | 说法不准：家族来源也包括同场战败的别家领主；「其」生硬 | {HERO_NAME}开口前顿了一下。灰袍办了身边的人，这事还没过去。 | {HERO_NAME} pauses before speaking. What the Grey Wardens did to people close to them is not forgotten. |
| `gwp_ai_det_intro_family_medium` | {HERO_NAME}谨慎地选择着措辞。家族与灰袍的那次冲突，至今仍是一件不能轻视的事。 | 同上 | {HERO_NAME}说话很小心。身边的人栽在灰袍手里，这事至今不敢小看。 | {HERO_NAME} speaks carefully. People close to them fell foul of the Grey Wardens, and that is still taken seriously. |
| `gwp_ai_det_intro_family_high` | {HERO_NAME}的神色一度变得冷硬。灰袍的行动所造成的后果，至今仍沉重地压在其家族身上。 | 同上 | {HERO_NAME}的脸色冷了下来。灰袍那回办了身边的人，到现在还压在心头。 | {HERO_NAME}'s face hardens. What the Grey Wardens did to people close to them still weighs heavily. |

### 二、前半句：本人受过灰袍惩处（轻）

[领主讲本人受过灰袍惩处的那回事，按性格抽一种口吻。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_core_personal_low_honorable` | 我确实越过了界，被人阻止也是应有的结果。 | 无 | 保留 | 保留 |
| `gwp_ai_det_core_personal_low_dishonorable` | 规矩只有在不妨碍必要之事时才有用。被抓只能证明我选错了时机。 | 说法不准：不一定被捕过（交罚金、约斗、被打垮也算）；格言腔 / 翻译腔 | 规矩挡路的时候，我向来不怎么理会。那回只是撞上了灰袍。 | When rules get in my way I seldom mind them. That time I simply ran into the Wardens. |
| `gwp_ai_det_core_personal_low_valorous` | 一次失败不会让我害怕，但它让我知道不能轻视灰袍的能耐。 | 无 | 保留 | 保留 |
| `gwp_ai_det_core_personal_low_cautious` | 我不想为了区区一次劫掠村庄，再去面对灰袍。 | 说法不准：只说村庄，劫商队的领主也会抽到 | 为了抢那么一回，再去招惹灰袍？不值得。 | Provoke the Wardens again for one more raid? Not worth it. |
| `gwp_ai_det_core_personal_low_merciful` | 不管我当时有什么理由，都不该让村民承担代价。 | 说法不准：只说村庄，劫商队的领主也会抽到 | 不管我当时有什么理由，都不该让那些百姓吃苦。 | Whatever my reasons were, those common folk should not have suffered for them. |
| `gwp_ai_det_core_personal_low_cruel` | 他们已经表明了态度。我只不过暂时不打算再试一次。 | 「他们」指灰袍；稍改口语 | 灰袍的态度我看明白了。只是眼下不想再试一回。 | I understand where the Wardens stand. I just don't care to test it again right now. |
| `gwp_ai_det_core_personal_low_generous` | 追随我的人和我一样为那个选择付出了代价。下一次，我有责任作出更好的判断。 | 格言腔 / 翻译腔 | 跟着我的人也为那回的事吃了苦。下一回，我得替他们多想想。 | My people paid for that choice as well. Next time I owe them better judgment. |
| `gwp_ai_det_core_personal_low_closefisted` | 我的损失超过了收获。我不会再这样浪费自己的士兵和钱财。 | 格言腔 / 翻译腔 | 那一回赔进去的比抢到的还多。这种亏本买卖我不做第二次。 | I lost more that time than I took. I won't make that bad bargain twice. |
| `gwp_ai_det_core_personal_low_calculating` | 那件事让我明白，什么时候代价会开始超过收益。 | 无 | 保留 | 保留 |
| `gwp_ai_det_core_personal_low_impulsive` | 我一时愤怒便采取了行动，怒气尚未消退，代价就已经找上了我。 | 格言腔 / 翻译腔 | 那回我一时火起就动了手，火还没消，灰袍就找上门了。 | I acted in a temper that time, and the Wardens found me before it had cooled. |
| `gwp_ai_det_core_personal_low_neutral` | 那件事已经过去，但我并没有丢掉它留下的教训。 | 无 | 保留 | 保留 |

### 二、前半句：本人受过灰袍惩处（中）

[领主讲本人受过灰袍惩处的那回事，按性格抽一种口吻。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_core_personal_medium_honorable` | 我破坏了安宁，也为此承担了后果。否认这一点，只会在过错之外再添一分怯懦。 | 格言腔 / 翻译腔 | 那回是我坏了规矩，后果我也认了。现在再抵赖，就太没骨气了。 | I broke the peace that time, and I accepted what followed. Denying it now would be cowardly. |
| `gwp_ai_det_core_personal_medium_dishonorable` | 我不会假装他们的规矩不可冒犯。我只学会了一件事：公然破坏规矩，就是把武器递给敌人。 | 格言腔 / 翻译腔 | 我不会装作灰袍的规矩碰不得。我只学会了一件事：别明着坏规矩，让她们抓住把柄。 | I won't pretend the Wardens' rules are sacred. I've only learned not to break them in the open and hand them the excuse. |
| `gwp_ai_det_core_personal_medium_valorous` | 我不怕艰难的战斗，但只有蠢人才会把勇气当成不计后果。 | 格言腔 / 翻译腔 | 我不怕硬仗，可明知灰袍会来还往上撞，那是蠢。 | I don't fear a hard fight, but charging in when you know the Wardens will come is just stupid. |
| `gwp_ai_det_core_personal_medium_cautious` | 你尽可以把这叫作谨慎。我知道他们封死一个人身边所有道路的速度有多快。 | 格言腔 / 翻译腔 | 你尽可以说我胆小。灰袍堵人的本事，我是领教过的。 | Call me timid if you like. I've seen how fast the Wardens can close in on someone. |
| `gwp_ai_det_core_personal_medium_merciful` | 相比锁链，我记得更清楚的是那些受惊的村民。仅凭这一点，也足够让我停手。 | 说法不准：不一定被捕过（交罚金、约斗、被打垮也算）（「锁链」）；说法不准：只说村庄，劫商队的领主也会抽到 | 那回的事，我记得最清楚的不是灰袍，是那些吓坏了的百姓。光凭这个，我也该收手。 | What I remember most is not the Wardens but the frightened people. That alone is reason enough to stop. |
| `gwp_ai_det_core_personal_medium_cruel` | 他们下手够重，让耐心变得有用。可别把这当成悔意。 | 格言腔 / 翻译腔 | 灰袍下手够重，我学会了忍。可别当成我后悔了。 | The Wardens hit hard enough to teach me patience. Don't mistake it for regret. |
| `gwp_ai_det_core_personal_medium_generous` | 追随我的人承担了那个决定造成的损失。忠义要求我不能再如此草率地牺牲他们。 | 格言腔 / 翻译腔 | 跟着我的人替那个决定吃了亏。我不能再这样拿他们去冒险。 | My people bore the cost of that decision. I can't gamble with them like that again. |
| `gwp_ai_det_core_personal_medium_closefisted` | 烧毁一个村庄，不值得让我同时赔上士兵、钱财和自由。 | 说法不准：只说村庄，劫商队的领主也会抽到（「烧毁一个村庄」） | 抢一回，赔上兵、赔上钱，说不定还搭上自己。这笔账怎么算都不划算。 | One raid, and I lose men, money, maybe my own freedom. However you count it, it doesn't pay. |
| `gwp_ai_det_core_personal_medium_calculating` | 损失我已经算过两遍。若没有更大的目的，没有哪个村庄值得招来那样的追捕。 | 说法不准：只说村庄，劫商队的领主也会抽到 | 那回的损失我算过两遍。没有更大的图谋，犯不着招来那样的追捕。 | I've counted that loss twice. Without some greater purpose, it's not worth bringing that pursuit down on me. |
| `gwp_ai_det_core_personal_medium_impulsive` | 我让怒火替我选了道路，而灰袍决定了这条路在哪里结束。 | 格言腔 / 翻译腔 | 那回我由着性子来，结果是灰袍替我收的场。 | I let my temper choose the road that time, and the Wardens decided where it ended. |
| `gwp_ai_det_core_personal_medium_neutral` | 自从那次被捕以后，每次劫掠都多了一层盘算：灰袍会不会赶来。 | 说法不准：不一定被捕过（交罚金、约斗、被打垮也算）（「自从那次被捕以后」） | 那回以后，每次要动手，我都得先想想灰袍会不会赶来。 | Since then, every time I think of striking, I have to ask myself whether the Wardens will come. |

### 二、前半句：本人受过灰袍惩处（重）

[领主讲本人受过灰袍惩处的那回事，按性格抽一种口吻。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_core_personal_high_honorable` | 我确实有罪，所受的惩罚也并不过分。教训再深，也不会让那次裁断变得不公。 | 格言腔 / 翻译腔 | 我确实有罪，灰袍罚得不重。这一课我记得很深。 | I was guilty, and the Wardens were not too harsh. It was a lesson I took to heart. |
| `gwp_ai_det_core_personal_high_dishonorable` | 我后悔的是被抓，而不是越过他们划下的界线。真正改变的是，我现在会把脚步藏得更好。 | 说法不准：不一定被捕过（交罚金、约斗、被打垮也算）（「被抓」） | 我后悔的是撞在灰袍手上，不是坏了她们的规矩。现在我只是藏得更好。 | What I regret is falling into the Wardens' hands, not breaking their rules. Now I just hide my tracks better. |
| `gwp_ai_det_core_personal_high_valorous` | 我仍然敢再面对他们，但这不代表我会愚蠢到把他们当成无害之人。 | 格言腔 / 翻译腔 | 再跟灰袍交手我也敢，可我不会傻到把她们当成好对付的。 | I'd face the Wardens again, but I'm not fool enough to think them harmless. |
| `gwp_ai_det_core_personal_high_cautious` | 我靠着学会避开某些战斗才活了下来。再给他们一个追捕我的理由，只是毫无必要的愚蠢。 | 格言腔 / 翻译腔 | 我能活到今天，靠的就是知道哪些仗不能打。再给灰袍一个找我的理由，那是自找死路。 | I've lived this long by knowing which fights to avoid. Giving the Wardens another reason to hunt me would be asking to die. |
| `gwp_ai_det_core_personal_high_merciful` | 真正压在我心上的，不是他们对我做了什么，而是我的选择早已伤害了那些无力反抗的人。 | 无 | 保留 | 保留 |
| `gwp_ai_det_core_personal_high_cruel` | 他们做过什么，我记得一清二楚。我的克制只是算计，不是屈服，更不是原谅。 | 「他们」指灰袍；稍改口语 | 灰袍做过什么，我记得一清二楚。我忍着，是算过了，不是服了，更不是原谅。 | I remember exactly what the Wardens did. If I hold back, it's calculation, not surrender, and certainly not forgiveness. |
| `gwp_ai_det_core_personal_high_generous` | 我不能只为满足又一次劫掠，就要求家臣和亲族再次承受那样的代价。 | 格言腔 / 翻译腔 | 我不能为了再抢一回，又让手下和家里人替我吃那份苦。 | I won't make my people and my household suffer that again just for one more raid. |
| `gwp_ai_det_core_personal_high_closefisted` | 我不会为了终将被灰袍连本带利夺回的战利品，把自己耗得一无所有。 | 格言腔 / 翻译腔 | 抢来的东西，灰袍迟早连本带利拿回去。我不会为这个把自己掏空。 | Whatever I take, the Wardens will take back with interest sooner or later. I won't bleed myself dry for that. |
| `gwp_ai_det_core_personal_high_calculating` | 现在，每一条通往村庄的路都要算上同一笔账：灰袍会从我这里取走什么作为代价。 | 说法不准：只说村庄，劫商队的领主也会抽到（「每一条通往村庄的路」） | 如今每打一回主意，我都得先算一笔账：灰袍会从我这儿拿走什么。 | Now every plan starts with one sum: what the Wardens will take from me for it. |
| `gwp_ai_det_core_personal_high_impulsive` | 每当怒火升起，那次惩罚的记忆都会在我行动以前把它压下去。 | 格言腔 / 翻译腔 | 每回火气上来，想起那回的事，我就先压下去了。 | Whenever my temper rises, remembering that time is what holds it down. |
| `gwp_ai_det_core_personal_high_neutral` | 那次处置改变了我看待每次劫掠、每个俘虏，以及每一条归路的方式。 | 格言腔 / 翻译腔 | 那回以后，我看每一次出兵、每一条退路，都不一样了。 | Since then I look differently at every march and every road home. |

### 二、前半句：身边的人受过灰袍惩处（轻）

[领主讲身边的人受过灰袍惩处的那回事，按性格抽一种口吻。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_core_family_low_honorable` | 我的族人的确越过了界。我不能说灰袍出手回应是错的。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 我们的人那回确实越了界。灰袍出手，我说不出个错字。 | Our people did cross the line that time. I can't say the Wardens were wrong to act. |
| `gwp_ai_det_core_family_low_dishonorable` | 血亲比外人的规矩更重要。我评判灰袍，只看他们对我的族人做了什么，而不是他们给出的罪名。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 我看灰袍，只看她们对我们的人做了什么，不看她们安的什么罪名。 | I judge the Wardens by what they did to our people, not by the charges they named. |
| `gwp_ai_det_core_family_low_valorous` | 我的家族受过打击，却没有失去勇气。不过，那次警告已经足够清楚。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 我们的人吃了一回亏，可没丢了胆气。不过那一下，大家都看清楚了。 | Our people took a blow, but they didn't lose their nerve. Still, everyone saw that warning clearly. |
| `gwp_ai_det_core_family_low_cautious` | 发生在族人身上的事让我们看清了一种危险。若无必要，家族主动招惹它才是愚蠢。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 身边的人栽过一回，我们算是看清了这份危险。没必要，谁也不会去招惹。 | Seeing our people fall foul of them showed us the danger. No one goes looking for it without need. |
| `gwp_ai_det_core_family_low_merciful` | 比起族人受伤的颜面，我更在意那些遭受伤害的村民。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到；说法不准：只说村庄，劫商队的领主也会抽到 | 比起我们的人丢了面子，我更在意那些遭了殃的百姓。 | I care more about the people who suffered than about our lost pride. |
| `gwp_ai_det_core_family_low_cruel` | 他们动了我的血亲。即便我暂时不再追究，也不会忘记。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 灰袍动了我们的人。我暂时不追究，可不会忘。 | The Wardens laid hands on our people. I'll let it be for now, but I won't forget. |
| `gwp_ai_det_core_family_low_generous` | 无论族人犯了什么错，他仍是我们的人。我们必须纠正他，却不能抛弃他。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 不管那回谁犯了错，总归是我们的人。该管教就管教，可不能撒手不管。 | Whoever was at fault, they're still our people. We correct them; we don't abandon them. |
| `gwp_ai_det_core_family_low_closefisted` | 族人的愚行变成了整个家族的账单。那原本应该由他自己承担。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 别人犯的蠢，最后账单落到大家头上。那本该他自己担着。 | Someone else's folly ended up on all our bills. They should have carried it themselves. |
| `gwp_ai_det_core_family_low_calculating` | 发生在族人身上的事，让家族对其中的风险有了一个有用的估量。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 那回的事，让我们对这里头的风险有了个准数。 | That business gave us a fair measure of the risk involved. |
| `gwp_ai_det_core_family_low_impulsive` | 我的族人还没有想清楚就采取了行动，结果却留给我们所有人铭记。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 有人没想清楚就动了手，结果让我们大家都记住了。 | Someone struck before thinking, and all of us remember the result. |
| `gwp_ai_det_core_family_low_neutral` | 那件事牵连了我的家人，家族自然会记得。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 那回的事牵连了我们的人，自然记着。 | That business touched our people, so naturally it is remembered. |

### 二、前半句：身边的人受过灰袍惩处（中）

[领主讲身边的人受过灰袍惩处的那回事，按性格抽一种口吻。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_core_family_medium_honorable` | 我的族人破坏了安宁，家族也为此付出了代价。血缘并不会让责任凭空消失。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 我们的人坏了安宁，大家也跟着吃了亏。是自己人，并不能把账抹掉。 | Our people broke the peace, and we all paid for it. Being one of us doesn't wipe the slate. |
| `gwp_ai_det_core_family_medium_dishonorable` | 无论我的族人做过什么，外人都没有权力羞辱我们的家族，再把这称为公正。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 不管我们的人做过什么，外人也没资格这样羞辱我们，还管这叫公道。 | Whatever our people did, outsiders have no right to shame us like that and call it justice. |
| `gwp_ai_det_core_family_medium_valorous` | 我们并没有被吓倒，但也不会再盲目冲进同一种惩罚之中。 | 格言腔 / 翻译腔 | 我们没被吓倒，可也不会再闭着眼往同样的刀口上撞。 | We weren't cowed, but we won't charge blindly onto the same blade again. |
| `gwp_ai_det_core_family_medium_cautious` | 家族没有兴趣仅仅为了证明自己不怕，就再次走进同一个陷阱。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 就为了显得不怕，再往同一个坑里跳？我们没这个兴致。 | Jump into the same pit again just to show we're not afraid? We have no taste for that. |
| `gwp_ai_det_core_family_medium_merciful` | 我宁愿家人学会克制，也不愿再让农民为贵族的选择失去亲人。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 我宁可身边的人学会收手，也不愿再看农户为我们这些人的决定丢了亲人。 | I'd rather our people learned restraint than see more farmers lose their families over our decisions. |
| `gwp_ai_det_core_family_medium_cruel` | 灰袍让我的家族为别人的错误流了血。这种账不会被轻易忘记。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到；「为别人的错误」不通 | 灰袍让我们的人流了血。这笔账不会轻易忘。 | The Wardens made our people bleed. That debt won't be forgotten easily. |
| `gwp_ai_det_core_family_medium_generous` | 一个族人的错误让所有站在他身边的人付出了代价。我们现在的责任，是修补它造成的损害。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 一个人犯的错，让站在他身边的人都付了代价。我们现在该做的，是把窟窿补上。 | One person's mistake cost everyone who stood beside them. Our task now is to repair the damage. |
| `gwp_ai_det_core_family_medium_closefisted` | 我不打算再为某个亲族对劫掠的贪欲付账。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到；说法不准：只说村庄，劫商队的领主也会抽到 | 我不打算再替谁的贪心掏钱。 | I don't intend to pay for anyone's greed again. |
| `gwp_ai_det_core_family_medium_calculating` | 一个鲁莽的选择让我们损失了多少士兵、钱财和自由，家族已经全部算清了。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到；「自由」不一定 | 一个鲁莽的决定让我们赔了多少兵、多少钱，我们都算清楚了。 | We've counted exactly how many men and how much silver one rash decision cost us. |
| `gwp_ai_det_core_family_medium_impulsive` | 一个头脑发热的决定把灰袍引到了我们所有人面前。这种事情不会轻易被忘记。 | 格言腔 / 翻译腔 | 一个头脑发热的决定，把灰袍引到了我们所有人面前。这种事忘不掉。 | One hot-headed decision brought the Wardens down on all of us. That isn't forgotten. |
| `gwp_ai_det_core_family_medium_neutral` | 发生在一个族人身上的事，已经改变了整个家族谈论劫掠与报复的方式。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 那回以后，我们说起抢掠和报复，口气都不一样了。 | Since then, we talk differently about raiding and revenge. |

### 二、前半句：身边的人受过灰袍惩处（重）

[领主讲身边的人受过灰袍惩处的那回事，按性格抽一种口吻。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_core_family_high_honorable` | 整个家族都已经明白：身份和血统不能成为伤害受我们保护之人的借口。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 那回以后大家都明白了：出身再高，也不能拿来当伤人的借口。 | Since then we all understand: no birth is high enough to excuse harming people. |
| `gwp_ai_det_core_family_high_dishonorable` | 我的家族把那场惩罚视为羞辱，而不是裁断。我们有所克制，并不代表承认他们拥有这种权力。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 那场惩罚，我们只当是羞辱，不当是裁断。我们收敛，不等于认了她们有这个权。 | We took that punishment as an insult, not a judgment. Holding back doesn't mean we grant them the right. |
| `gwp_ai_det_core_family_high_valorous` | 我的家族仍有勇气，只是这份勇气如今总会与对灰袍的清晰记忆同行。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 我们的胆气还在，只是如今每回提刀，都会想起灰袍。 | Our courage is still there; it's just that now every time we draw steel, we remember the Wardens. |
| `gwp_ai_det_core_family_high_cautious` | 我们知道他们能够怎样彻底包围一个家族。避免再次陷入那种危险，是为了生存，不是屈服。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 灰袍能把一伙人围得多死，我们见识过。躲着点，是为了活命，不是认怂。 | We've seen how tightly the Wardens can close in on a whole band. Steering clear is survival, not surrender. |
| `gwp_ai_det_core_family_high_merciful` | 如果我的族人终于明白自己造成了多少痛苦，那么这场惩罚至少还能留下些善果。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 要是我们的人终于明白自己害了多少人，那场惩罚也算没白受。 | If our people finally understand how much harm they did, then that punishment wasn't for nothing. |
| `gwp_ai_det_core_family_high_cruel` | 他们的手至今仍沉重地压在我的家族身上。我们没有屈服，只是学会了何时报复的代价太高。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 灰袍那一手至今还压在我们身上。我们没服软，只是学会了什么时候报复太贵。 | The Wardens' hand still weighs on us. We haven't yielded; we've only learned when revenge costs too much. |
| `gwp_ai_det_core_family_high_generous` | 整个家族都承担了那份重担。当有人把灾祸带回家时，我有责任保护那些仍然忠于家族的人。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 那份担子大家一起扛了。谁再把祸带回来，我得护着那些还肯跟着我们的人。 | We all carried that burden. If anyone brings trouble home again, I have to protect those who still stand with us. |
| `gwp_ai_det_core_family_high_closefisted` | 灰袍让我们明白，一个亲族的愚行可能变成所有人的损失。我不会允许这种事发生第二次。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 一个人的蠢事，变成了所有人的损失。这种事我不许再有第二回。 | One person's folly became everyone's loss. I won't allow it a second time. |
| `gwp_ai_det_core_family_high_calculating` | 现在，家族里没有人会在谈论劫掠村庄时，不先计算灰袍将会作出的回应。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到；说法不准：只说村庄，劫商队的领主也会抽到 | 如今谁再提抢掠，都得先算灰袍会怎么回应。 | Now anyone who mentions raiding has to reckon first with how the Wardens will answer. |
| `gwp_ai_det_core_family_high_impulsive` | 整个家族都承受了一个失控冲动造成的后果。如今就连愤怒，也必须先面对这段记忆。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 大家都吃过一个人冲动的苦头。如今谁再发火，也得先想想那回的事。 | We all suffered for one person's impulse. Now anyone who loses their temper has to remember that first. |
| `gwp_ai_det_core_family_high_neutral` | 那场后果笼罩了整个家族。如今没有人还会把灰袍当成与自己无关的遥远传闻。 | 说法不准：「家族/族人/血亲」，同场战败的别家领主也会抽到 | 那回的事压在我们所有人身上。如今谁也不会把灰袍当成远处的传闻。 | That business hangs over all of us. No one treats the Wardens as a distant rumour anymore. |

### 三、后半句：对玩家说（玩家已入会，轻）

[紧接前半句，同一个对话框里，中间一个空格。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_audience_warden_low_honorable` | 既然你为他们效力，就应当遵守他们出手干预时所宣称维护的同一套规矩。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 你既然替灰袍办事，就该守她们拿来要求别人的那套规矩。 | If you work for the Wardens, keep to the same rules they hold others to. |
| `gwp_ai_det_audience_warden_low_dishonorable` | 你替他们拿着通缉令，但纸上的墨迹并不能自动把权力变成美德。 | 编造：「通缉令 / 纸上的墨迹 / 印记」，玩家拿的是灰袍交办的案子 | 你替灰袍办事，不等于你做什么都对。 | Working for the Wardens doesn't make everything you do right. |
| `gwp_ai_det_audience_warden_low_valorous` | 你现在也带着他们的权威；证明这份权威依靠的是勇气，而不是恫吓。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 你如今也替灰袍出头了。要让人服，靠的是胆量，不是吓唬。 | Now you stand up for the Wardens too. Earn respect with courage, not threats. |
| `gwp_ai_det_audience_warden_low_cautious` | 权威会让蠢人变得鲁莽。别让他们的名号把你推入一场尚未衡量过的战斗。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 顶着灰袍的名头，蠢人也会变莽撞。别让它把你拖进一场没掂量过的仗。 | The Wardens' name can make a fool reckless. Don't let it drag you into a fight you haven't weighed. |
| `gwp_ai_det_audience_warden_low_merciful` | 如果你与他们共事，就利用自己的位置保护村民，别等到只能事后惩罚。 | 格言腔 / 翻译腔 | 你既然跟她们共事，就趁早护着村民，别等出了事才来罚人。 | Since you work with them, protect the villagers early instead of punishing someone after the harm is done. |
| `gwp_ai_det_audience_warden_low_cruel` | 原来你选择了替他们办事。别以为仅凭这一点，所有领主都会低头。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 原来你替她们办事了。别以为凭这个，哪个领主都得给你低头。 | So you run errands for them now. Don't think that makes every lord bow to you. |
| `gwp_ai_det_audience_warden_low_generous` | 既然为他们效力，就记住那些追随你、并会为你的每一道命令付出代价的人。 | 格言腔 / 翻译腔 | 替她们办事，也别忘了跟着你的人。你每下一道令，都是他们去拼。 | Working for them, don't forget the people who follow you. Every order you give, they're the ones who pay for it. |
| `gwp_ai_det_audience_warden_low_closefisted` | 如果悬赏会给你钱，就不要假装钱财与公正是同一回事。 | 「悬赏」是界面词；格言腔 / 翻译腔 | 办案有赏钱拿，可别把赏钱当成公道。 | There's pay in the cases you take, but don't confuse the pay with justice. |
| `gwp_ai_det_audience_warden_low_calculating` | 你既然选择替灰袍办事，与其彼此装糊涂，不如把话说明白。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_warden_low_impulsive` | 通缉令不能为愤怒中的行动开脱。接下来发生的事，仍然要由你负责。 | 编造：「通缉令 / 纸上的墨迹 / 印记」，玩家拿的是灰袍交办的案子 | 替灰袍办事，也不能拿它给你的火气开脱。出了事，还是你担。 | Working for the Wardens is no excuse for acting in anger. Whatever happens is still on you. |
| `gwp_ai_det_audience_warden_low_neutral` | 你在替灰袍做事，应该明白为什么这件事会被人记住。 | 无 | 保留 | 保留 |

### 三、后半句：对玩家说（玩家已入会，中）

[紧接前半句，同一个对话框里，中间一个空格。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_audience_warden_medium_honorable` | 如果你以他们的名义行事，就该让规矩高于报复，即便被指控的人并不值得同情。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 你既然以她们的名义行事，就让规矩压过报复，哪怕对方不值得可怜。 | If you act in their name, put the rules above revenge, even when the accused deserves no pity. |
| `gwp_ai_det_audience_warden_medium_dishonorable` | 你可以使用他们的权威，但不要一边为自己歪曲规矩，一边向别人宣扬纯洁。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 你可以借她们的名头，但别一边替自己破规矩，一边教训别人守规矩。 | Use their name if you like, but don't bend the rules for yourself while preaching them to others. |
| `gwp_ai_det_audience_warden_medium_valorous` | 既然承担他们的权威，就该堂堂正正面对持械的犯人，把恐吓留给更卑劣的人。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 既然替她们出头，就光明正大地对付拿刀的人。吓唬人的事，留给那些下作的。 | If you stand for them, face armed men openly. Leave the bullying to lesser people. |
| `gwp_ai_det_audience_warden_medium_cautious` | 一纸通缉令，代替不了判断何时前进、何时退让的能力。 | 编造：「通缉令 / 纸上的墨迹 / 印记」，玩家拿的是灰袍交办的案子 | 替灰袍办事，也得自己判断什么时候进、什么时候退。名头替不了你。 | Working for the Wardens, you still have to judge when to press and when to give ground. Their name won't do it for you. |
| `gwp_ai_det_audience_warden_medium_merciful` | 利用你在他们之中的位置，阻止下一个村庄受害，而不是只在事后惩罚罪人。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 你在她们那边说得上话，就趁早拦着下一回出事，别光等着事后罚人。 | You have some say among them. Use it to stop the next village from being hurt, not just to punish afterwards. |
| `gwp_ai_det_audience_warden_medium_cruel` | 你可以替他们追捕别人，但别把一纸通缉令误当成刀枪不入。 | 编造：「通缉令 / 纸上的墨迹 / 印记」，玩家拿的是灰袍交办的案子 | 你可以替她们抓别人，可别以为替灰袍办事，刀就砍不到你身上。 | You can hunt others for them, but don't think working for the Wardens makes you proof against steel. |
| `gwp_ai_det_audience_warden_medium_generous` | 保护好你的同伴，也保护好村民。权威在成为特权以前，首先是一份债务。 | 格言腔 / 翻译腔（「权威在成为特权以前，首先是一份债务」） | 护好你的人，也护好村民。差事是灰袍给的，担子可是你的。 | Look after your people, and the villagers too. The Wardens gave you the work, but the burden is yours. |
| `gwp_ai_det_audience_warden_medium_closefisted` | 不要仅仅为了增加自己的赏金，就拿别人的性命作为花费。 | 格言腔 / 翻译腔 | 别为了多拿几个赏钱，就拿别人的命去填。 | Don't spend other people's lives just to earn a little more pay. |
| `gwp_ai_det_audience_warden_medium_calculating` | 与灰袍合作的人应该明白：有分寸的执法，比无谓的羞辱更容易换来长久的服从。 | 格言腔 / 翻译腔 | 跟灰袍做事的人该明白：有分寸地办，比当众羞辱人更能让人长久服气。 | Anyone working with the Wardens should know that measured enforcement keeps people in line longer than humiliation. |
| `gwp_ai_det_audience_warden_medium_impulsive` | 不要因为身后有他们的印记，就让一次冒犯把执法变成报复。 | 见[核实稿](text-review-term-check-20261104.md) | 见核实稿 | 见核实稿 |
| `gwp_ai_det_audience_warden_medium_neutral` | 你如今站在通缉令的另一边，应该记得这些手段最终会在人身上留下什么。 | 编造：「通缉令 / 纸上的墨迹 / 印记」，玩家拿的是灰袍交办的案子 | 你如今站到了办案的那一边，该记得这些手段最后落在人身上是什么样。 | You're on the side that does the hunting now. Remember what these methods leave on people in the end. |

### 三、后半句：对玩家说（玩家已入会，重）

[紧接前半句，同一个对话框里，中间一个空格。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_audience_warden_high_honorable` | 你现在代表着他们。若他们的公正真有意义，你的行为就必须比自己追捕的罪行更加干净。 | 「他们」指灰袍，已批准文本用「她们」；格言腔 / 翻译腔 | 你现在替她们出面。她们要真讲公道，你自己的手就得比你追的人干净。 | You speak for them now. If their justice means anything, your own hands must be cleaner than those you hunt. |
| `gwp_ai_det_audience_warden_high_dishonorable` | 如果你把自己的行为称为公正，就用始终如一的行为来证明，而不是依靠惩罚别人的权力。 | 格言腔 / 翻译腔 | 你要说自己办的是公道，就拿一贯的做法来证明，别光靠罚人的权力。 | If you call what you do justice, prove it by being consistent, not by the power to punish. |
| `gwp_ai_det_audience_warden_high_valorous` | 要和他们站在一起，就堂堂正正地站；但衡量勇气的应是你保护了谁，而不是你能吓倒谁。 | 格言腔 / 翻译腔 | 要跟她们站一起，就堂堂正正地站。勇气看的是你护住了谁，不是你吓住了谁。 | If you stand with them, stand openly. Courage is measured by whom you protect, not whom you frighten. |
| `gwp_ai_det_audience_warden_high_cautious` | 如果灰袍派你追捕危险的人，记住，谨慎才能让执法者活下来。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_warden_high_merciful` | 在你做得到的时候，挡住灰袍与不必要的痛苦；公正没有必要变成残忍。 | 格言腔 / 翻译腔（「挡住灰袍与不必要的痛苦」读不通） | 能拦的时候，替那些不该受的苦拦一拦。办案不必办成残忍。 | When you can, stand between people and needless suffering. Enforcement doesn't have to become cruelty. |
| `gwp_ai_det_audience_warden_high_cruel` | 他们的印记留在我的家族身上，而你选择了站在他们一边。别指望我会把其中任何一样当成友谊。 | 见[核实稿](text-review-term-check-20261104.md) | 见核实稿 | 见核实稿 |
| `gwp_ai_det_audience_warden_high_generous` | 他们给你的权威，也让你必须为所有因你的决定而陷入危险的人负责。 | 格言腔 / 翻译腔 | 她们给你的这份差事，也把每个因你的决定陷进危险的人，都交到了你手上。 | The work they gave you also puts in your hands everyone your decisions put at risk. |
| `gwp_ai_det_audience_warden_high_closefisted` | 如果你只为收益而效力，就要明白：不受约束的执法，最终造成的损失会超过报酬。 | 格言腔 / 翻译腔 | 要是你只图赏钱，就得明白：办案办过了头，赔进去的比挣的多。 | If you're only in it for the pay, know this: overdo the enforcement and you'll lose more than you earn. |
| `gwp_ai_det_audience_warden_high_calculating` | 既然你与他们共事，就该明白：恐惧很快就会耗尽，始终如一的规矩才能维持得更久。 | 格言腔 / 翻译腔 | 你跟她们共事，该明白：吓唬人的劲头很快就用完，一贯的规矩才撑得久。 | Working with them, you should know fear wears off quickly; consistent rules last longer. |
| `gwp_ai_det_audience_warden_high_impulsive` | 如果脾气支配了你的剑，他们的权威也无法阻止你变成无辜者面对的另一种危险。 | 格言腔 / 翻译腔 | 要是火气管住了你的剑，灰袍的名头也拦不住你变成另一个祸害。 | If your temper rules your sword, the Wardens' name won't stop you becoming another danger to innocent people. |
| `gwp_ai_det_audience_warden_high_neutral` | 你选择与灰袍共事，就应该知道：通缉令了结以后，他们做过的事仍会被人记很久。 | 编造：「通缉令 / 纸上的墨迹 / 印记」，玩家拿的是灰袍交办的案子 | 你选了跟灰袍共事，就该知道：案子了了，她们做过的事还会被人记很久。 | You chose to work with the Wardens, so know this: long after a case is closed, what they did is remembered. |

### 三、后半句：对玩家说（玩家没入会，轻）

[紧接前半句，同一个对话框里，中间一个空格。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_audience_outsider_low_honorable` | 这是一句诚实的劝告：不要靠伤害村庄，让同样的裁断落到自己头上。 | 格言腔 / 翻译腔 | 我实话劝你一句：别去伤那些村子和商队，免得同样的事落到你头上。 | An honest word of advice: leave the villages and caravans alone, or the same will fall on you. |
| `gwp_ai_det_audience_outsider_low_dishonorable` | 守不守承诺由你，但至少不要让自己成为如此容易对付的猎物。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_outsider_low_valorous` | 真想证明勇气，就去面对持有武器的战士，而不是无法反抗的村民。 | 说法不准：只说村庄，劫商队的领主也会抽到 | 真想显本事，就去对付拿刀的兵，别拿村民和赶车的开刀。 | If you want to prove yourself, face armed soldiers, not villagers and carters. |
| `gwp_ai_det_audience_outsider_low_cautious` | 你没有必要通过招惹一群会不知疲倦地追捕你的人来证明什么。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_outsider_low_merciful` | 趁那些惊恐的家庭还没有为你付出代价，你仍然可以选择一条更好的路。 | 格言腔 / 翻译腔 | 那些人家还没吃你的苦，你还来得及选条好路。 | Those families haven't suffered at your hands yet. You can still choose a better road. |
| `gwp_ai_det_audience_outsider_low_cruel` | 你尽可以照自己的意思做，只是别说从来没人提醒过你后果。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_outsider_low_generous` | 不要让追随者和家人为一个只用来满足你自己的选择付出代价。 | 格言腔 / 翻译腔 | 别让跟着你的人和家里人，替一个只图你自己痛快的决定买单。 | Don't make your followers and family pay for a choice that only pleases you. |
| `gwp_ai_det_audience_outsider_low_closefisted` | 如果良心不能打动你，那就至少保护好自己的钱袋和士兵。 | 格言腔 / 翻译腔 | 良心劝不动你，那就看在钱袋和兵的份上。 | If conscience won't move you, think of your purse and your soldiers. |
| `gwp_ai_det_audience_outsider_low_calculating` | 如果你只看重收益，记得把灰袍也算进你的账里。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_outsider_low_impulsive` | 不要让一时的怒火替你选择一条需要多年才能逃离的道路。 | 「需要多年才能逃离」是编的 | 别让一时的火气，把你推上一条很久都甩不脱的路。 | Don't let a moment's anger set you on a road you can't get off for a long time. |
| `gwp_ai_det_audience_outsider_low_neutral` | 在选择自己的目标以前，你最好记住这一点。 | 「在选择自己的目标以前」默认玩家要找目标 | 你最好记着这一点。 | You'd do well to remember that. |

### 三、后半句：对玩家说（玩家没入会，中）

[紧接前半句，同一个对话框里，中间一个空格。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_audience_outsider_medium_honorable` | 守住对无辜者的本分，你就不必像我一样亲自学会这个道理。 | 说法不准：「像我一样亲自学会」，家族来源时说话人没亲身经历 | 守住对无辜的人的本分，这个道理就不必等灰袍来教你。 | Do right by the innocent, and you won't need the Wardens to teach you this. |
| `gwp_ai_det_audience_outsider_medium_dishonorable` | 如果你越过那条界线，别以为身份或一个方便的借口能够保护你。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_outsider_medium_valorous` | 不要把欺凌弱者当作勇敢，也不要把灰袍的耐心当作软弱。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_outsider_medium_cautious` | 趁选择权还在自己手里，离那些村庄远一点。追捕开始以前退让，代价要小得多。 | 说法不准：只说村庄，劫商队的领主也会抽到 | 趁主意还在自己手里，离那些村子和商路远点。等追兵来了再退，就贵多了。 | While the choice is still yours, keep away from those villages and trade roads. Backing off once the hunt starts costs far more. |
| `gwp_ai_det_audience_outsider_medium_merciful` | 让村庄安宁一些吧。那里的百姓已经受够了我们这些领主带来的苦难。 | 保留原意，稍改口语 | 让那些村子安生些吧。那里的百姓已经受够了我们这些领主。 | Let the villages have some peace. The people there have had enough of lords like us. |
| `gwp_ai_det_audience_outsider_medium_cruel` | 你愿意走那条路就去吧，只要准备好付出的比抢到的更多。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_outsider_medium_generous` | 领主欠信任自己的人一份保护，而不是为了骄傲留下一路损失。 | 格言腔 / 翻译腔 | 领主欠那些信得过自己的人一份保护，不是一路的损失。 | A lord owes their people protection, not a trail of losses. |
| `gwp_ai_det_audience_outsider_medium_closefisted` | 一个村庄的战利品，补不回追捕可能夺走的士兵、钱财和自由。 | 说法不准：只说村庄，劫商队的领主也会抽到 | 抢一回得的东西，补不回追兵可能拿走的兵、钱和自由。 | What one raid brings in won't replace the men, money and freedom a pursuit can take. |
| `gwp_ai_det_audience_outsider_medium_calculating` | 如果你准备劫掠，就要明白：赃物花光以后，那份代价可能仍会追着你。 | 保留原意，稍改口语 | 要是你打算动手抢，记着：抢来的花光了，那份代价可能还追着你。 | If you mean to raid, remember: the cost may still be chasing you after the spoils are spent. |
| `gwp_ai_det_audience_outsider_medium_impulsive` | 下令点燃火把以前先停一下。怒火消失的速度，总比后果更快。 | 说法不准：默认玩家正要去烧村，未入会的玩家可能从没劫掠过（「点燃火把」） | 要下令动手以前，先停一停。火气过得快，后果可没那么快。 | Before you give the order, stop a moment. Anger passes faster than what follows. |
| `gwp_ai_det_audience_outsider_medium_neutral` | 在作出同样的选择之前，先想清楚后果。 | 无 | 保留 | 保留 |

### 三、后半句：对玩家说（玩家没入会，重）

[紧接前半句，同一个对话框里，中间一个空格。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_ai_det_audience_outsider_high_honorable` | 我明白地告诫你：保护你权力之下的村庄，否则公正迟早也会找到你。 | 说法不准：「你权力之下的村庄」，玩家不一定有封地 | 我明白告诉你：别去碰那些村民和商队，不然灰袍迟早也会找上你。 | I'll tell you plainly: leave the villagers and caravans alone, or sooner or later the Wardens will find you too. |
| `gwp_ai_det_audience_outsider_high_dishonorable` | 仔细选择你要破坏哪些规矩。灰袍更在意你做了什么，而不是你准备了什么借口。 | 无 | 保留 | 保留 |
| `gwp_ai_det_audience_outsider_high_valorous` | 如果你想找一个配得上的敌人，就找能够还手的人；若你选择弱者，灰袍自会回应。 | 格言腔 / 翻译腔 | 想找个像样的对手，就找能还手的。你要是去欺负弱的，灰袍自会来回你。 | If you want a worthy enemy, pick one who can fight back. Prey on the weak and the Wardens will answer. |
| `gwp_ai_det_audience_outsider_high_cautious` | 如果你还在乎保全自己，就不要给他们记住你名字的理由。 | 「他们」指灰袍 | 你要是还在乎自己的命，就别给灰袍记住你名字的理由。 | If you value your own skin, don't give the Wardens a reason to remember your name. |
| `gwp_ai_det_audience_outsider_high_merciful` | 先放过那些最早受苦的人，你也就不必亲自承受这份教训。 | 格言腔 / 翻译腔 | 先放过那些最先受苦的人，这份教训你就不必亲自去尝。 | Spare the people who always suffer first, and you won't have to learn this lesson yourself. |
| `gwp_ai_det_audience_outsider_high_cruel` | 你想烧什么都随你，但要明白，灰袍有办法让记忆活得比傲慢更久。 | 说法不准：默认玩家正要去烧村，未入会的玩家可能从没劫掠过（「你想烧什么」） | 你想干什么随你，只是要明白，灰袍记仇比你的傲气长久。 | Do what you like, but understand that the Wardens' memory outlasts your pride. |
| `gwp_ai_det_audience_outsider_high_generous` | 在让家族和追随者继承你决定的代价以前，先替他们想一想。 | 格言腔 / 翻译腔 | 让家里人和跟着你的人替你的决定买单以前，先替他们想想。 | Before your family and followers inherit the cost of your choices, think of them. |
| `gwp_ai_det_audience_outsider_high_closefisted` | 即便按照最自私的算法，答案也一样：一次劫掠不值得灰袍事后收取的代价。 | 稍改口语 | 就算按最自私的算法，答案也一样：抢一回，不值灰袍事后跟你要的那份代价。 | Even by the most selfish reckoning the answer is the same: one raid isn't worth what the Wardens make you pay afterwards. |
| `gwp_ai_det_audience_outsider_high_calculating` | 无论你以为烧毁村庄能得到多少，都先把此后多年的追捕算进去。 | 说法不准：默认玩家正要去烧村，未入会的玩家可能从没劫掠过（「烧毁村庄」）；「此后多年」是编的 | 不管你觉得抢一回能得多少，先把之后没完没了的追捕算进去。 | However much you think a raid will bring in, count in the pursuit that follows, which doesn't end soon. |
| `gwp_ai_det_audience_outsider_high_impulsive` | 如果让脾气指挥你，最后可能由灰袍决定它何时停止。 | 格言腔 / 翻译腔 | 要是由着脾气来，最后什么时候停，可就是灰袍说了算。 | Let your temper lead, and in the end it'll be the Wardens who decide when it stops. |
| `gwp_ai_det_audience_outsider_high_neutral` | 除非你已经准备好让灰袍沿着这条路一直追到你面前，否则就离它远一点。 | 稍改口语 | 除非你准备好让灰袍一路追到你面前，否则就离那条路远点。 | Unless you're ready for the Wardens to follow that road all the way to you, stay off it. |

### 四、百科里的行踪与状态

[人物百科页上的震慑按钮弹出详情，「大地图状态」「位置」两栏；`_003` 是取不到名字时的替补。]

| 键 | 现中文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_gwpaideterrencestate_001` | 状态不明 | 无 | 保留 | 保留 |
| `gwp_gwpaideterrencestate_002` | 未知位置 | 无 | 保留 | 保留 |
| `gwp_gwpaideterrencestate_003` | 此人 | 无 | 保留 | 保留 |
| `gwp_gwpaideterrencestate_032` | 未知位置 | 无 | 保留 | 保留 |
| `gwp_gwpaideterrencestate_033` | 状态不明 | 无 | 保留 | 保留 |
| `gwp_gwpaideterrencestate_036` | 处于俘虏状态 | 英文嵌进「Map status: …」后读成 “Map status: is in a captive state” | 保留 | captive |
| `gwp_gwpaideterrencestate_037` | 率队驻留 | 说法不准：条件是没有活动部队、人在城里，不是「率队」 | 留在城中 | staying in a settlement |
| `gwp_gwpaideterrencestate_038` | 率队活动 | 无 | 保留 | 保留 |

## 代码问题（不在本稿改）

1. 村庄与商队两套震慑合并后选台词（`GwpAiDeterrenceState.TryBuildPainDialogue` 的 `direct += caravanDirect`），台词无法区分他劫的是村子还是商队。本稿用通用说法绕开，不需要改代码；若要按类别分台词，属另一批改动。
2. 出现时机与旁白，见「待用户定」。

## 本稿统计

共 146 条：见核实稿 2 条，实审 144 条。建议改动 121 条（中英都改 120、只改中文 0、只改英文 1），保留 23 条。

- 编造 6 条（通缉令 / 纸 / 印记）。
- 说法不准：家族来源 34 条（含 3 句旁白）、不一定被捕 4 条、只说村庄 12 条、默认玩家要烧村 4 条，另有「你权力之下的村庄」「像我一样亲自学会」「率队驻留」各 1 条（有重叠）。
- 其余是格言腔、翻译腔与称呼。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
