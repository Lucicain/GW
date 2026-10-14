# 玩家词句索引（生成）

编辑 `strings.json`，本表仅供查找。

| 文本键 | 英文 | 中文 | 出现位置或情境 |
|---|---|---|---|
| gwp_adoption_chat_notice | {HERO.LINK} was taken in by the Grey Wardens from {VILLAGE}. | {HERO.LINK}被灰袍守卫从{VILLAGE}收留了。 | GreyWardenAdoptionLogEntry.cs:57 |
| gwp_adoption_log_entry | {HERO.LINK} was taken in by the Grey Wardens from {VILLAGE} and brought back to the inner courtyard to be raised. | {HERO.LINK}被灰袍守卫从{VILLAGE}收留，并带回内院抚养。 | GreyWardenAdoptionLogEntry.cs:77 |
| gwp_clan_description | The Grey Wardens began as an order within the Imperial priesthood, charged with patrol and punishment. After the Empire fractured, they broke from the rule of its lords but continued to bind themselves and others to Heaven's commandments. Women command the order, priests preach its faith, and village congregations supply recruits and provisions. They pursue raiders and protect villages and trade routes, regarding punishment as a religious duty. The common folk rely on their protection and fear the severity of their judgment. | 灰袍起初是帝国教会中承担巡察与惩戒的一支修会。帝国分裂后，她们脱离了诸侯的统辖，仍奉上天的诫命约束自身，也要求旁人守戒。修会由女性统领，教士在各地传教，村中的教众为她们提供兵员与供给。她们缉捕劫掠者，保护村庄与商路，并把惩戒罪行视为信仰的一部分。平民依靠她们的庇护，也畏惧她们严苛的审判。 | 家族百科总介绍；出身、政教分离与严苛惩戒为按用户方向拟写的模组设定，非原版事实。 |
| gwp_clan_intro | We are the Grey Wardens, bound by Heaven's commandments to watch over these lands. The order answers to no lord. We pursue those who raid villages or attack travellers, whoever they serve. | 我们是灰袍，奉上天的诫命巡察各地。修会不受诸侯差遣。有人劫掠村庄、袭击行旅，我们就会追捕，不论他为谁效命。 | 首次相识时的灰袍组织介绍，来自comment_strings.xml。 |
| gwp_clan_name | Grey Wardens | 灰袍守卫 | 家族百科与名牌的组织名，保留。 |
| gwp_clan_short_name | Grey Wardens | 灰袍 | 家族百科与名牌的组织名，保留。 |
| gwp_common_garrison | garrison | 驻地点 | PolicePatrolBehavior.cs:903 |
| gwp_common_understood | Understood | 知道了 | PlayerBountyBehavior.cs:170 |
| gwp_common_unknown_target | Unknown target | 未知目标 | PlayerBountyBehavior.cs:873, PlayerBountyBehavior.QuestAndNotification.cs:78, PoliceEnforcementBehavior.AtonementQuest.cs:45 |
| gwp_crime_aid_criminals | Aiding malefactors against the Grey Wardens | 帮助罪犯对抗灰袍守卫 | GwpText.cs:51 |
| gwp_crime_attack_caravan | Attack upon a caravan | 攻击商队 | GwpText.cs:35 |
| gwp_crime_attack_villagers | Attack upon villagers | 攻击村民 | GwpText.cs:33 |
| gwp_crime_forced_recruitment | Forced recruitment | 强迫募兵 | GwpText.cs:39 |
| gwp_crime_forced_supplies | Forced requisition of supplies | 强征给养 | GwpText.cs:41 |
| gwp_crime_join_village_raid | Participation in a village raid | 参与劫掠村庄 | GwpText.cs:47 |
| gwp_crime_kill_villagers | Slaying of villagers | 杀害村民 | GwpText.cs:45 |
| gwp_crime_obstruct_law | Obstruction of the law | 妨碍执法 | GwpText.cs:49 |
| gwp_crime_raid_caravan | Raid upon a caravan | 劫掠商队 | GwpText.cs:43 |
| gwp_crime_raid_village | Raid upon a village | 劫掠村庄 | GwpText.cs:37 |
| gwp_custom_commander_yao | Grey Warden Provost Yao | 灰袍纠察官尧 | _Module/ModuleData/sphpcustombattle.xml |
| gwp_enc_chenxi | {HERO_NAME} oversees the rebuilding of villages after war. When the pursuit has moved on, she often remains to arrange repairs, clear wells, and help scattered families return. She regards this work as a duty of faith, as necessary as pursuing the raiders. | {HERO_NAME}负责战后的村庄重建。追兵离开以后，她往往还留在村里，安排人手修屋、清井，帮助流散的人家回来。她把这些劳作视作应尽的侍奉，与追捕劫掠者同样要紧。 | 创始领主晨曦的百科介绍，对应重建；修屋清井是重建的叙事描写，不新增工程功能。 |
| gwp_enc_mise | {HERO_NAME} pursues those who attack villages. She remembers the names of many burned settlements and knows the paths their people take when fleeing. News of a raid sends her gathering riders; she holds that no one's faith can excuse their being robbed or killed. | {HERO_NAME}负责村庄遇袭后的追捕。她记得不少被烧毁的村名，也熟悉村民逃难时常走的小路。接到劫掠的消息，她便召集人手出发；在她看来，是否入教都不能成为任人劫杀的理由。 | 创始领主弥瑟的百科介绍；拟稿中的保护不限信仰来自“保护平民”方向，不增添信仰筛选机制。 |
| gwp_enc_muguang | {HERO_NAME} receives appeals from nobles for the order, with particular concern for wrongs done to its members. She first hears what happened, then brings those concerned before a public decision. When precedent offers no answer, she takes the matter back to the order for discussion. | {HERO_NAME}替修会接待贵族的申诉，尤其留意灰袍成员遭受的不公。她会先问清事情的经过，再召集有关的人公开议决。旧例不能解决的事，她也愿意带回修会商议。 | 创始领主暮光的百科介绍，对应贵族事务。 |
| gwp_enc_shengduo | {HERO_NAME} hears petitions for the order. Village headmen, merchants, and artisans bring disputes before her, and she enters the alleys to question those unwilling to approach an official. She carries old Imperial statutes to help establish the facts, then mediates under the order's rule. | {HERO_NAME}为修会听取各地请愿。村庄首领、商人和工匠都可能向她陈述争端，她也会走进巷道询问那些不愿见官的人。她随身带着旧帝国的法令，用它们辨明案情，再依修会的戒律主持调解。 | 创始领主圣铎的百科介绍，对应要人请愿。 |
| gwp_enc_vandi | {HERO_NAME} is the present Warden-General and oversees the training of the order's soldiers. Before dawn she often inspects recruits and has veterans close the gaps in their shield wall. She believes Heaven has charged the Wardens with protecting the common folk, and forbids them to use the order to pursue crowns or enlarge private estates. | {HERO_NAME}是灰袍现任统领，负责训练修会的战士。她常在天亮前检阅新兵，要求老兵亲自纠正盾墙的空隙。她相信守护百姓是上天交给灰袍的职责，不许部下以修会之名争夺王位或扩张私产。 | 创始领主梵蒂的百科介绍，对应练兵本职。 |
| gwp_enc_yoer | {HERO_NAME} protects the trade roads, riding between bridges, ferries, and markets. She pursues attacks on caravans and unlawful tolls, refusing to dismiss them as ordinary wartime losses. Many who trade along these roads know her grey mantle. | {HERO_NAME}负责保护商路，常在桥梁、渡口和集市之间奔走。她追查商队遇袭与沿途勒索，不肯把这些事当作战争中无须过问的小事。走这几条路的商人，许多都认得她的灰袍。 | 创始领主约珥的百科介绍，对应商队护卫。 |
| gwp_greet_bad | We have heard something of your deeds. Make amends, and there is still a way back. | 你做过的事，我们有所耳闻。肯改过，就还有挽回的余地。 | 已认识领主随机问候，玩家声望为负但未达通缉分支。 |
| gwp_greet_good | You have done some good for the common folk. Speak your mind. | 你为百姓做过些好事。有话请说。 | 已认识领主随机问候，玩家声望5至19。 |
| gwp_greet_high | You have helped those who suffered. The Wardens remember it. | 你帮过那些受害的人，灰袍记得这份情。 | 已认识领主随机问候，玩家声望20至39。 |
| gwp_greet_neutral | What brings you to the Grey Wardens? | 说吧，你找灰袍有什么事？ | 已认识领主随机问候，玩家声望0至4。 |
| gwp_greet_very_high | Villagers and merchants along the road have spoken well of you. The Wardens trust those who act for the common folk. | 沿途的村民和商人都称赞过你。灰袍信得过肯为百姓办事的人。 | 已认识的灰袍领主随机问候，玩家灰袍声望至少40；阈值仅在此说明，不放进台词。 |
| gwp_greet_very_low | Prey on the common folk again, and the Wardens will come for you. | 你再劫掠百姓，灰袍就会来追捕你。 | 已认识领主随机问候，玩家声望不高于-40；不将阈值当作新的追捕机制。 |
| gwp_greet_wanted | Your case is not settled. Tell me why you have come. | 你的案子还没有了结。先说说你的来意。 | 已认识领主随机问候，玩家声望-39至-11。 |
| gwp_grey_lord_followup | What do you need? | 你有什么事？ | 上述随机问候后的衔接句；原逻辑可能与上一句重复，拟稿不改对话流。 |
| gwp_greywardenadoptionlogentry_003 | Unknown Village | 未知村庄 | GreyWardenAdoptionLogEntry.cs:93 |
| gwp_greywardenfamilybehavior_001 | Aebbe | 澄音 | GreyWardenFamilyBehavior.cs:59 |
| gwp_greywardenfamilybehavior_002 | Aeffe | 祈安 | GreyWardenFamilyBehavior.cs:60 |
| gwp_greywardenfamilybehavior_003 | Eadgyth | 望舒 | GreyWardenFamilyBehavior.cs:61 |
| gwp_greywardenfamilybehavior_004 | Eadleofu | 清岚 | GreyWardenFamilyBehavior.cs:62 |
| gwp_greywardenfamilybehavior_005 | Eadwyn | 静禾 | GreyWardenFamilyBehavior.cs:63 |
| gwp_greywardenfamilybehavior_006 | Ealdgyth | 霁月 | GreyWardenFamilyBehavior.cs:64 |
| gwp_greywardenfamilybehavior_007 | Ealhburh | 朝露 | GreyWardenFamilyBehavior.cs:65 |
| gwp_greywardenfamilybehavior_008 | Ealhflaed | 星祷 | GreyWardenFamilyBehavior.cs:66 |
| gwp_greywardenfamilybehavior_009 | Ealhgyth | 守真 | GreyWardenFamilyBehavior.cs:67 |
| gwp_greywardenfamilybehavior_010 | Ealhswith | 昭宁 | GreyWardenFamilyBehavior.cs:68 |
| gwp_greywardenfamilybehavior_011 | Ealhthryth | 清律 | GreyWardenFamilyBehavior.cs:69 |
| gwp_greywardenfamilybehavior_012 | Ealhwaru | 若岚 | GreyWardenFamilyBehavior.cs:70 |
| gwp_greywardenfamilybehavior_013 | Ealhwyn | 听雪 | GreyWardenFamilyBehavior.cs:71 |
| gwp_greywardenfamilybehavior_014 | Eanburh | 慈光 | GreyWardenFamilyBehavior.cs:72 |
| gwp_greywardenfamilybehavior_015 | Eanflaed | 霜晨 | GreyWardenFamilyBehavior.cs:73 |
| gwp_greywardenfamilybehavior_016 | Eanswith | 明祷 | GreyWardenFamilyBehavior.cs:74 |
| gwp_greywardenfamilybehavior_017 | Eormenburh | 兰序 | GreyWardenFamilyBehavior.cs:75 |
| gwp_greywardenfamilybehavior_018 | Eormenhild | 雅宁 | GreyWardenFamilyBehavior.cs:76 |
| gwp_greywardenfamilybehavior_019 | Folcburh | 晨铃 | GreyWardenFamilyBehavior.cs:77 |
| gwp_greywardenfamilybehavior_020 | Frithugyth | 书弦 | GreyWardenFamilyBehavior.cs:78 |
| gwp_greywardenfamilybehavior_021 | Frithuswith | 怀澄 | GreyWardenFamilyBehavior.cs:79 |
| gwp_greywardenfamilybehavior_022 | Godgifu | 澜音 | GreyWardenFamilyBehavior.cs:80 |
| gwp_greywardenfamilybehavior_023 | Godwife | 芷宁 | GreyWardenFamilyBehavior.cs:81 |
| gwp_greywardenfamilybehavior_024 | Heahburh | 祷月 | GreyWardenFamilyBehavior.cs:82 |
| gwp_greywardenfamilybehavior_025 | Heregyth | 安禾 | GreyWardenFamilyBehavior.cs:83 |
| gwp_greywardenfamilybehavior_026 | Hereswith | 静澜 | GreyWardenFamilyBehavior.cs:84 |
| gwp_greywardenfamilybehavior_027 | Leofcwen | 思律 | GreyWardenFamilyBehavior.cs:85 |
| gwp_greywardenfamilybehavior_028 | Leofflaed | 清珑 | GreyWardenFamilyBehavior.cs:86 |
| gwp_greywardenfamilybehavior_029 | Leofgifu | 雨谣 | GreyWardenFamilyBehavior.cs:87 |
| gwp_greywardenfamilybehavior_030 | Leofrun | 远星 | GreyWardenFamilyBehavior.cs:88 |
| gwp_greywardenfamilybehavior_031 | Mildburh | 采祷 | GreyWardenFamilyBehavior.cs:89 |
| gwp_greywardenfamilybehavior_032 | Wulfflaed | 露祈 | GreyWardenFamilyBehavior.cs:90 |
| gwp_greywardenfamilybehavior_033 | Wulfgifu | 映岚 | GreyWardenFamilyBehavior.cs:91 |
| gwp_greywardenfamilybehavior_034 | Wulfgyth | 书宁 | GreyWardenFamilyBehavior.cs:92 |
| gwp_greywardenfamilybehavior_035 | Wulfthryth | 凝光 | GreyWardenFamilyBehavior.cs:93 |
| gwp_greywardenfamilybehavior_036 | Wulfwyn | 夕晨 | GreyWardenFamilyBehavior.cs:94 |
| gwp_greywardenfamilybehavior_037 |  II | 二 | GreyWardenFamilyBehavior.cs:99 |
| gwp_greywardenfamilybehavior_038 |  III | 三 | GreyWardenFamilyBehavior.cs:100 |
| gwp_greywardenfamilybehavior_039 |  IV | 四 | GreyWardenFamilyBehavior.cs:101 |
| gwp_greywardenfamilybehavior_040 |  V | 五 | GreyWardenFamilyBehavior.cs:102 |
| gwp_greywardenfamilybehavior_041 |  VI | 六 | GreyWardenFamilyBehavior.cs:103 |
| gwp_greywardenfamilybehavior_042 |  VII | 七 | GreyWardenFamilyBehavior.cs:104 |
| gwp_greywardenfamilybehavior_043 |  VIII | 八 | GreyWardenFamilyBehavior.cs:105 |
| gwp_greywardenfamilybehavior_044 |  IX | 九 | GreyWardenFamilyBehavior.cs:106 |
| gwp_greywardenfamilybehavior_045 |  X | 十 | GreyWardenFamilyBehavior.cs:107 |
| gwp_greywardenfamilybehavior_046 | she | 她 | GreyWardenFamilyBehavior.cs:534 |
| gwp_greywardenfamilybehavior_047 | The Grey Wardens took {VAR_1} in after {VAR_2} was sacked and burned. She now lives within the order's house. Still young, she knows the patrol calls and learns the commandments from her elders. | {VAR_1}在{VAR_2}遭劫焚毁后被灰袍收养，如今住在修会内院。她还年幼，已熟悉巡察队出门时的口令，也跟着长辈学习戒律。 | 收养女孩不足12岁时的百科介绍。 |
| gwp_greywardenfamilybehavior_048 | The Wardens adopted {VAR_1} after {VAR_2} was burned in her childhood. She is learning to read, examine cases, and follow the order's rule in preparation for her adult duties. | {VAR_1}幼时在{VAR_2}被焚后由灰袍收养。她正在修会学习读写、辨认案情和遵守戒律，为成年后的职责做准备。 | 收养女孩年满12岁、尚未成年时的百科介绍。 |
| gwp_greywardenfamilybehavior_049 | — | {VAR_1}幼年时因{VAR_2}毁于罪犯劫掠而失去旧日生活，后被灰袍守卫收养。她的来历常被灰袍内部视作这支执法修会存在意义的证明之一：缉捕罪犯并非终点，替灾后幸存者续起秩序与归宿，同样是灰袍法职责的一部分。 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_greywardenfamilybehavior_050 | {VAR_1} was born within the Grey Warden order. She has grown up hearing the priest explain its rule and watching her elders ride on patrol. The order raises her alongside the children it has taken in. | {VAR_1}生于灰袍修会。她从小听教士讲解戒律，也看着长辈们带兵出巡。修会将她与收养来的孩子一同抚育。 | 非收养后继成员不足12岁时的百科介绍。 |
| gwp_greywardenfamilybehavior_051 | {VAR_1} has grown up in the Grey Warden order and is learning to read, examine cases, and observe the rules of patrol. Her elders expect her to take up the order's duties when she comes of age. | {VAR_1}在灰袍修会长大，正在学习读写、辨认案情和巡察的规矩。长辈们期望她成年后能承担修会的职责。 | 非收养后继成员年满12岁、尚未成年时的百科介绍。 |
| gwp_greywardenfamilybehavior_052 | — | {VAR_1}成长于灰袍守卫所维系的旧帝国法统、家族血脉与修会戒律之中。她既是灰袍血脉的延续者，也是这支女性警察家族的新一代执法者。对许多百姓而言，她所代表的不是门第本身，而是灰袍仍会继续巡察道路、缉捕罪犯并守住秩序。 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_greywardentrooprequestbehavior_001 | — | 少量灰袍新兵 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_002 | — | 一队灰袍新兵 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_003 | — | 重装步兵小队 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_004 | — | 持弓执勤队 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_005 | — | 披甲骑巡队 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_006 | — | 我想调一些灰袍的人手。 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_007 | — | 先算了。 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_008 | — | 那就等你准备好再来开口。 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_009 | — | 款项核清。灰袍会把人交给你，但别把她们当成寻常雇兵使唤。 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_010 | — | 这点钱还不够雇他们。再添些吧。 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_011 | — | 这批人不是市井佣兵。按你现在的名声，这笔调拨要 {VAR_1} 金。若你愿意，现在就把款项放上谈判桌。 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_012 | — | 这名灰袍领主已从自己的部队中调出 {VAR_1} 名{VAR_2}，交由你指挥。 | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_013 | — | 调拨{VAR_1} | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_014 | — | ，按当前名声减免 {VAR_1}% | 未检出直接引用；保留 |
| gwp_greywardentrooprequestbehavior_015 | — | 我要 {VAR_1} 名{VAR_2}（{VAR_3} 金{VAR_4}） | 未检出直接引用；保留 |
| gwp_greywardenvillageadoptionbehavior_001 | nameless village | 无名村庄 | GreyWardenVillageAdoptionBehavior.cs:354 |
| gwp_greywardenvillagerewardbehavior_001 | Receive rewards from villagers (you can receive {VAR_1} dinars) | 领取村民酬谢（可领 {VAR_1} 第纳尔） | GreyWardenVillageRewardBehavior.cs:108 |
| gwp_greywardenvillagerewardbehavior_002 | The villagers have gathered no reward yet. | 村民尚未筹出酬谢。 | GreyWardenVillageRewardBehavior.cs:119 |
| gwp_greywardenvillagerewardbehavior_003 | You collect the villagers' reward of {VAR_1} denars. | 你领取了村民的酬谢 {VAR_1} 第纳尔。 | GreyWardenVillageRewardBehavior.cs:151 |
| gwp_greywardenvillagerewardsliderscreen_001 | Set the amount to be received | 设置领取金额 | GreyWardenVillageRewardSliderScreen.cs:242 |
| gwp_greywardenvillagerewardsliderscreen_002 | Drag the slider to select the villager reward to be received this time. You can currently claim up to {VAR_1} dinars. | 拖动滑块，选择本次要领取的村民酬谢。当前最多可领 {VAR_1} 第纳尔。 | GreyWardenVillageRewardSliderScreen.cs:243 |
| gwp_greywardenvillagerewardsliderscreen_003 | Confirm | 确认 | GreyWardenVillageRewardSliderScreen.cs:244 |
| gwp_greywardenvillagerewardsliderscreen_004 | Cancel | 取消 | GreyWardenVillageRewardSliderScreen.cs:245 |
| gwp_greywardenvillagerewardsliderscreen_005 | {VAR_1} Dinar | {VAR_1} 第纳尔 | GreyWardenVillageRewardSliderScreen.cs:251 |
| gwp_gwpaideterrencestate_001 | Unknown status | 状态不明 | GwpAiDeterrenceState.cs:343 |
| gwp_gwpaideterrencestate_002 | Unknown location | 未知位置 | GwpAiDeterrenceState.cs:344 |
| gwp_gwpaideterrencestate_003 | This person | 此人 | GwpAiDeterrenceState.cs:378 |
| gwp_gwpaideterrencestate_022 | — | 未知领主 | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_024 | — | {VAR_1}内 {VAR_2} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_025 | — | {VAR_1}内 {VAR_2} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_026 | — | {VAR_1}附近 {VAR_2} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_027 | — | 野外 {VAR_1} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_028 | — | {VAR_1}内 {VAR_2} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_029 | — | {VAR_1}附近 {VAR_2} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_030 | — | 野外 {VAR_1} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_031 | — | {VAR_1}内 {VAR_2} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_032 | Unknown location | 未知位置 | GwpAiDeterrenceState.cs:683 |
| gwp_gwpaideterrencestate_033 | Unknown status | 状态不明 | GwpAiDeterrenceState.cs:694 |
| gwp_gwpaideterrencestate_034 | — | 被囚于 {VAR_1} | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_035 | — | 被 {VAR_1} 俘虏 | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_036 | is in a captive state | 处于俘虏状态 | GwpAiDeterrenceState.cs:689 |
| gwp_gwpaideterrencestate_037 | holding position with a party | 率队驻留 | GwpAiDeterrenceState.cs:693 |
| gwp_gwpaideterrencestate_038 | travelling with a party | 率队活动 | GwpAiDeterrenceState.cs:691 |
| gwp_gwpaideterrencestate_039 | — | 在 {VAR_1} 休整 | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_040 | — | 正在逃亡 | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_041 | — | 刚获释 | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_042 | — | 正在赶路 | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_043 | — | 尚未重新现身 | 未检出直接引用；保留 |
| gwp_gwpaideterrencestate_044 | — | 状态不明 | 未检出直接引用；保留 |
| gwp_gwpbattlereinforcementbehavior_001 | You pray for help to come. | 你默默祈祷援军到来。 | GwpBattleReinforcementBehavior.cs:125 |
| gwp_gwpbattlereinforcementbehavior_002 | Grey Warden reinforcements have arrived! | 灰袍援军赶到了！ | GwpBattleReinforcementBehavior.cs:203 |
| gwp_battle_support_enemy | Grey Warden reinforcements have joined the enemy! | 敌方的灰袍援军抵达战场！ | GwpBattleReinforcementBehavior.cs:204 |
| gwp_gwpdata_001 | The Grey Wardens have declared you wanted. | 你已被灰袍通缉。 | GwpData.cs:391 |
| gwp_gwpdata_002 | The Grey Wardens no longer seek you. | 灰袍已撤销对你的通缉。 | GwpData.cs:406 |
| gwp_gwpdata_003 | The Grey Wardens have recorded your crime: {VAR_1} ({VAR_2}). | 灰袍记下了你的罪行：{VAR_1}（{VAR_2}）。 | GwpData.cs:1071 |
| gwp_gwpdata_005 | Reputation: {VAR_1} | 声望：{VAR_1} | GwpData.cs:1085 |
| gwp_gwpencyclopediaclanpagevm_001 | — | 关闭 | 未检出直接引用；保留 |
| gwp_gwpencyclopediaclanpagevm_002 | — | 宣战详情 | 未检出直接引用；保留 |
| gwp_gwpencyclopediaclanpagevm_003 | — | 查看灰袍守卫当前对各势力宣战的详细原因。 | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_001 | {VAR_1}: Grey Warden criminal record and deterrence | {VAR_1} 的灰袍案底与震慑 | GwpEncyclopediaHeroPageVM.cs:63 |
| gwp_gwpencyclopediaheropagevm_002 | Close | 关闭 | GwpEncyclopediaHeroPageVM.cs:68 |
| gwp_gwpencyclopediaheropagevm_003 | Record and deterrence | 案底与震慑 | GwpEncyclopediaHeroPageVM.cs:36 |
| gwp_gwpencyclopediaheropagevm_004 | View this character's permanent criminal record and current Grey Warden deterrence. | 查看此人物永久保留的犯罪案底与当前灰袍震慑。 | GwpEncyclopediaHeroPageVM.cs:38 |
| gwp_gwpencyclopediaheropagevm_005 | No record | 无记录 | GwpEncyclopediaHeroPageVM.cs:78 |
| gwp_gwpencyclopediaheropagevm_006 | Just | 刚刚 | GwpEncyclopediaHeroPageVM.cs:81 |
| gwp_gwpencyclopediaheropagevm_007 | {VAR_1} hours ago | {VAR_1} 小时前 | GwpEncyclopediaHeroPageVM.cs:86 |
| gwp_gwpencyclopediaheropagevm_008 | {VAR_1} days ago | {VAR_1} 天前 | GwpEncyclopediaHeroPageVM.cs:89 |
| gwp_gwpencyclopediaheropagevm_009 | — | 累计犯罪：{VAR_1} | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_010 | — | 被灰袍抓获：{VAR_1} | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_011 | — | 本人震慑：{VAR_1} | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_012 | — | 烧村欲望倍率：{VAR_1} | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_013 | — | 攻击村民欲望倍率：{VAR_1} | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_014 | — | 攻击商队欲望倍率：{VAR_1} | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_015 | Latest enforcement: {VAR_1} | 最近一次执法：{VAR_1} | GwpEncyclopediaHeroPageVM.cs:131 |
| gwp_gwpencyclopediaheropagevm_016 | Map status: {VAR_1} | 大地图状态：{VAR_1} | GwpEncyclopediaHeroPageVM.cs:132 |
| gwp_gwpencyclopediaheropagevm_017 | Location: {VAR_1} | 所在位置：{VAR_1} | GwpEncyclopediaHeroPageVM.cs:134 |
| gwp_gwppolicewarreasonservice_001 | — | 灰袍守卫 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_002 | — | {VAR_1}当前宣战详情 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_003 | — | 只有灰袍守卫家族页会显示宣战详情。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_004 | — | 未找到灰袍守卫家族，无法读取宣战原因。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_005 | — | 当前正式宣战对象：{VAR_1} 个 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_006 | — | 灰袍守卫当前没有正式宣战对象。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_007 | No outstanding warrant remains. The Grey Wardens are arranging peace. | 追捕事由已了，灰袍正着手议和。 | GwpPoliceWarReasonService.cs:164 |
| gwp_gwppolicewarreasonservice_008 | — | 家族额外信息： | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_010 | — | 收养冷却：全家族共享；自上一次成功收留女童起需等待 {VAR_1} 个游戏年。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_011 | — | 当前家族人数：{VAR_1}/{VAR_2}。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_012 | — | 当前善后任务：{VAR_1} | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_013 | — | 距离下一次可收养：冷却已结束，等待新的村庄被焚毁后触发善后。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_014 | — | 距离下一次可收养：{VAR_1}。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_015 | — | 上一个女童收留时间：{VAR_1}。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_016 | — | 上一个女童收留时间：本存档尚无成功收养记录。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_017 | — | 目标村庄 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_018 | — | 已记录 {VAR_1} 的善后请求，正在等待最近的警察接手。 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_gwppolicewarreasonservice_019 | — | 最近的警察已被抽调，正在补给后前往 {VAR_1}。 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_gwppolicewarreasonservice_020 | — | 最近的警察正在赶往 {VAR_1} 进行善后。 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_gwppolicewarreasonservice_021 | — | 警察正在 {VAR_1} 善后，剩余约 {VAR_2}。 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_gwppolicewarreasonservice_022 | — | 当前没有善后任务。 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_023 | Unrecorded enforcement party | 未记录的执法队 | GwpPoliceWarReasonService.cs:272 |
| gwp_gwppolicewarreasonservice_024 | Unknown Target | 未知目标 | GwpPoliceWarReasonService.cs:273 |
| gwp_gwppolicewarreasonservice_025 | Undocumented | 未记录 | GwpPoliceWarReasonService.cs:275 |
| gwp_gwppolicewarreasonservice_026 | — | 未记录 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_027 | — | 未知 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_028 | — | 未知 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_029 | {VAR_1}: {VAR_2} is pursuing {VAR_3} over {VAR_4} — {VAR_5} | {VAR_1}：{VAR_2} 正在追捕 {VAR_3}（{VAR_4}）—— {VAR_5} | GwpPoliceWarReasonService.cs:284 |
| gwp_gwppolicewarreasonservice_030 | Player bounty collaboration | 玩家悬赏协同 | GwpPoliceWarReasonService.cs:295 |
| gwp_gwppolicewarreasonservice_031 | Player case enforcement | 玩家案件执法 | GwpPoliceWarReasonService.cs:298 |
| gwp_gwppolicewarreasonservice_032 | Cross-faction pursuit | 跨势力追缉 | GwpPoliceWarReasonService.cs:301 |
| gwp_gwppolicewarreasonservice_033 | Law enforcement contract | 执法任务 | GwpPoliceWarReasonService.cs:303 |
| gwp_gwppolicewarreasonservice_034 | A Grey Warden party is escorting the player in pursuit of the quarry | 灰袍部队正在护送玩家追缉目标 | GwpPoliceWarReasonService.cs:309 |
| gwp_gwppolicewarreasonservice_035 | The target has been defeated and is escorting the player | 目标已被击败，正在押送玩家 | GwpPoliceWarReasonService.cs:312 |
| gwp_gwppolicewarreasonservice_036 | Formal war declared; pursuit continues | 已正式宣战并持续追击 | GwpPoliceWarReasonService.cs:315 |
| gwp_gwppolicewarreasonservice_037 | A case has been filed for tracking, but it has not been upgraded to a formal war. | 已立案追踪，尚未升级为正式战争 | GwpPoliceWarReasonService.cs:318 |
| gwp_gwppolicewarreasonservice_038 | Not recorded | 未记录 | GwpPoliceWarReasonService.cs:320 |
| gwp_gwppolicewarreasonservice_039 | — | 刚刚 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_040 | — | {VAR_1} 小时前 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_041 | — | {VAR_1} 天前 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_042 | Near {VAR_1} ({VAR_2}, {VAR_3}) | {VAR_1}附近 ({VAR_2}, {VAR_3}) | GwpPoliceWarReasonService.cs:341 |
| gwp_gwppolicewarreasonservice_043 | wild ({VAR_1}, {VAR_2}) | 野外 ({VAR_1}, {VAR_2}) | GwpPoliceWarReasonService.cs:343 |
| gwp_gwppolicewarreasonservice_044 | — | {VAR_1} 小时 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_045 | — | {VAR_1} 天 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_046 | — | {VAR_1} 天 {VAR_2} 小时 | 未检出直接引用；保留 |
| gwp_gwppolicewarreasonservice_047 | spring | 春 | GwpPoliceWarReasonService.cs:351 |
| gwp_gwppolicewarreasonservice_048 | Summer | 夏 | GwpPoliceWarReasonService.cs:352 |
| gwp_gwppolicewarreasonservice_049 | Autumn | 秋 | GwpPoliceWarReasonService.cs:353 |
| gwp_gwppolicewarreasonservice_050 | Winter | 冬 | GwpPoliceWarReasonService.cs:354 |
| gwp_gwppolicewarreasonservice_051 | Unknown season | 未知季 | GwpPoliceWarReasonService.cs:355 |
| gwp_gwppolicewarreasonservice_052 | Year {VAR_1}, {VAR_2}, day {VAR_3}, {VAR_4}:00 | {VAR_1}年{VAR_2}季第{VAR_3}天 {VAR_4}:00 | GwpPoliceWarReasonService.cs:358 |
| gwp_hero_chenxi | Eadgifu | 晨曦 | GreyWardenFamilyBehavior.cs:53, _Module/ModuleData/spspecialcharacters.xml |
| gwp_hero_mise | Mildthryth | 弥瑟 | GreyWardenFamilyBehavior.cs:51, _Module/ModuleData/spspecialcharacters.xml |
| gwp_hero_muguang | Wulfhild | 暮光 | GreyWardenFamilyBehavior.cs:54, _Module/ModuleData/spspecialcharacters.xml |
| gwp_hero_shengduo | Wynflaed | 圣铎 | GreyWardenFamilyBehavior.cs:52, _Module/ModuleData/spspecialcharacters.xml |
| gwp_hero_vandi | Aethelflaed | 梵蒂 | GreyWardenFamilyBehavior.cs:49, _Module/ModuleData/spspecialcharacters.xml |
| gwp_hero_yoer | Cyneburh | 约珥 | GreyWardenFamilyBehavior.cs:50, _Module/ModuleData/spspecialcharacters.xml |
| gwp_item_armor | Grey Warden Harness | 灰袍守护者之铠 | _Module/ModuleData/items.xml |
| gwp_item_boots | Grey Warden Sabatons | 灰袍守护者战靴 | _Module/ModuleData/items.xml |
| gwp_item_commander_armor | Grey Warden Commander's Harness | 灰袍指挥官之铠 | _Module/ModuleData/items.xml |
| gwp_item_commander_boots | Grey Warden Commander's Sabatons | 灰袍指挥官之靴 | _Module/ModuleData/items.xml |
| gwp_item_commander_gloves | Grey Warden Commander's Gauntlets | 灰袍指挥官护手 | _Module/ModuleData/items.xml |
| gwp_item_commander_helmet | Grey Warden Commander's Winged Helm | 灰袍指挥官翼盔 | _Module/ModuleData/items.xml |
| gwp_item_commander_horse_armor | Grey Warden Commander's Horse Armour | 灰袍指挥官马铠 | _Module/ModuleData/items.xml |
| gwp_item_commander_shoulders | Grey Warden Commander's Mantle | 灰袍指挥官护肩 | _Module/ModuleData/items.xml |
| gwp_item_eagle_shoulders | Grey Warden Eagle Mantle | 灰袍守护者鹰肩 | _Module/ModuleData/items.xml |
| gwp_item_gloves | Grey Warden Gauntlets | 灰袍守护者护手 | _Module/ModuleData/items.xml |
| gwp_item_great_shield | Grey Warden Great Shield | 灰袍守护者巨盾 | _Module/ModuleData/items.xml |
| gwp_item_heavy_armor | Grey Warden Plate Harness | 灰袍守护者重铠 | _Module/ModuleData/items.xml |
| gwp_item_helmet | Grey Warden Helm | 灰袍守护者之盔 | _Module/ModuleData/items.xml |
| gwp_item_hood | Grey Warden Hood | 灰袍守护者兜帽 | _Module/ModuleData/items.xml |
| gwp_item_horse_armor | Grey Warden Horse Armour | 灰袍守护者马铠 | _Module/ModuleData/items.xml |
| gwp_item_warhorse | Grey Warden Warhorse | 灰袍战马 | _Module/ModuleData/items.xml |
| gwp_item_mace | Grey Warden Calradic Mace | 灰袍卡拉德钉头锤 | _Module/ModuleData/items.xml |
| gwp_item_arrows | Grey Warden Piercing Arrows | 灰袍穿刺箭 | _Module/ModuleData/items.xml |
| gwp_item_lance | Grey Warden Lance | 灰袍守护者骑枪 | _Module/ModuleData/items.xml |
| gwp_item_leather_boots | Grey Warden Leather Boots | 灰袍守护者皮靴 | _Module/ModuleData/items.xml |
| gwp_item_leather_gloves | Grey Warden Leather Gloves | 灰袍守护者皮手套 | _Module/ModuleData/items.xml |
| gwp_item_light_armor | Grey Warden Light Harness | 灰袍守护者轻铠 | _Module/ModuleData/items.xml |
| gwp_item_obsidian_great_shield | Grey Warden Lord's Obsidian Great Shield | 灰袍领主黑曜巨盾 | _Module/ModuleData/items.xml |
| gwp_item_shoulders | Grey Warden Mantle | 灰袍守护者护肩 | _Module/ModuleData/items.xml |
| gwp_item_sword_1h | Grey Warden Arming Sword | 灰袍守护者单手剑 | _Module/ModuleData/items.xml |
| gwp_item_sword_2h | Grey Warden Greatsword | 灰袍守护者双手剑 | _Module/ModuleData/items.xml |
| gwp_item_winged_helmet | Grey Warden Winged Helm | 灰袍守护者翼盔 | _Module/ModuleData/items.xml |
| gwp_patrol_barter_name_cn | penalty clause | 罚金条款 | GwpBribeBarterable.cs:33 |
| gwp_patrol_returning_dialogue | This patrol has rendered its account and is returning to quarters. No further hearing will be held. | 本轮案卷已结，纠察队正奉命返回驻地。此刻不再受理交涉。 | PolicePatrolBehavior.cs:132 |
| gwp_playerbehaviormonitor_001 | — | 村民 | 未检出直接引用；保留 |
| gwp_playerbehaviormonitor_002 | — | 攻击村民 | 未检出直接引用；保留 |
| gwp_playerbehaviormonitor_003 | — | 商队 | 未检出直接引用；保留 |
| gwp_playerbehaviormonitor_004 | — | 攻击商队 | 未检出直接引用；保留 |
| gwp_playerbehaviormonitor_005 | Raid village | 劫掠村庄 | PlayerBehaviorMonitor.cs:168 |
| gwp_playerbehaviormonitor_006 | Unknown village | 未知村庄 | PlayerBehaviorMonitor.cs:168 |
| gwp_playerbehaviormonitor_007 | The Grey Wardens record your raid on {VAR_1}. | 灰袍记下了你劫掠{VAR_1}的罪行。 | PlayerBehaviorMonitor.cs:171 |
| gwp_playerbehaviormonitor_008 | — | 劫掠村庄 | 未检出直接引用；保留 |
| gwp_playerbehaviormonitor_009 | — | 未知村庄 | 未检出直接引用；保留 |
| gwp_playerbehaviormonitor_010 | Forced Recruitment | 强迫募兵 | PlayerBehaviorMonitor.cs:193 |
| gwp_playerbehaviormonitor_011 | Unknown Village | 未知村庄 | PlayerBehaviorMonitor.cs:194 |
| gwp_playerbehaviormonitor_012 | Forced Requisition of Supplies | 强征给养 | PlayerBehaviorMonitor.cs:213 |
| gwp_playerbehaviormonitor_013 | Unknown Village | 未知村庄 | PlayerBehaviorMonitor.cs:214 |
| gwp_playerbehaviormonitor_014 | Protect the Village from Plunder | 保护村庄免遭劫掠 | PlayerBehaviorMonitor.cs:322 |
| gwp_playerbehaviormonitor_015 | Rescue the Caravan | 解救商队 | PlayerBehaviorMonitor.cs:323 |
| gwp_playerbehaviormonitor_016 | Rescue the Villagers | 解救村民 | PlayerBehaviorMonitor.cs:324 |
| gwp_playerbehaviormonitor_017 | Defeat the robbers | 击败劫匪 | PlayerBehaviorMonitor.cs:325 |
| gwp_playerbehaviormonitor_018 | The Grey Wardens note your good deed: {VAR_1}. | 灰袍记下了你的义举：{VAR_1}。 | PlayerBehaviorMonitor.cs:327 |
| gwp_playerbehaviormonitor_019 | Raid the village | 劫掠村庄 | PlayerBehaviorMonitor.cs:375 |
| gwp_playerbehaviormonitor_020 | Force recruits | 强迫募兵 | PlayerBehaviorMonitor.cs:376 |
| gwp_playerbehaviormonitor_021 | Force supplies | 强征给养 | PlayerBehaviorMonitor.cs:377 |
| gwp_playerbehaviormonitor_022 | Raid the caravan | 劫掠商队 | PlayerBehaviorMonitor.cs:378 |
| gwp_playerbehaviormonitor_023 | Kill the villagers | 杀害村民 | PlayerBehaviorMonitor.cs:379 |
| gwp_playerbehaviormonitor_024 | Raid a village | 劫掠村庄 | PlayerBehaviorMonitor.cs:383 |
| gwp_playerbehaviormonitor_025 | Force recruitment | 强迫募兵 | PlayerBehaviorMonitor.cs:384 |
| gwp_playerbehaviormonitor_026 | Forced supplies | 强征给养 | PlayerBehaviorMonitor.cs:385 |
| gwp_playerbehaviormonitor_027 | Raid a caravan | 劫掠商队 | PlayerBehaviorMonitor.cs:386 |
| gwp_playerbehaviormonitor_028 | Kill villagers | 杀害村民 | PlayerBehaviorMonitor.cs:387 |
| gwp_playerbehaviormonitor_029 | The Grey Wardens record your crime: {VAR_1}. | 灰袍记下了你的罪行：{VAR_1}。 | PlayerBehaviorMonitor.cs:389 |
| gwp_playerbehaviormonitor_030 | Village | 村庄 | PlayerBehaviorMonitor.cs:575 |
| gwp_playerbehaviormonitor_031 | Caravan | 商队 | PlayerBehaviorMonitor.cs:576 |
| gwp_playerbehaviormonitor_032 | Villager | 村民 | PlayerBehaviorMonitor.cs:577 |
| gwp_playerbehaviormonitor_033 | Participate in raiding village | 参与劫掠村庄 | PlayerBehaviorMonitor.cs:578 |
| gwp_playerbehaviormonitor_034 | Raid Caravan | 劫掠商队 | PlayerBehaviorMonitor.cs:579 |
| gwp_playerbehaviormonitor_035 | Attack villagers | 攻击村民 | PlayerBehaviorMonitor.cs:580 |
| gwp_playerbehaviormonitor_036 | Join the defenders and protect {VAR_1}. | 加入防守方，保护{VAR_1}。 | PlayerBehaviorMonitor.cs:582 |
| gwp_playerbehaviormonitor_037 | joins the attacker, {VAR_1}. | 加入攻击方，{VAR_1}。 | PlayerBehaviorMonitor.cs:583 |
| gwp_playerbehaviormonitor_038 | Prisoner | 犯人 | PlayerBehaviorMonitor.cs:595 |
| gwp_playerbehaviormonitor_039 | Join the Grey Wardens and help hunt down criminals. | 加入灰袍守卫一方，协助缉拿罪犯。 | PlayerBehaviorMonitor.cs:599 |
| gwp_playerbehaviormonitor_040 | Join {VAR_1} against the Grey Wardens. | 加入 {VAR_1} 一方，对抗灰袍守卫。 | PlayerBehaviorMonitor.cs:600 |
| gwp_playerbehaviormonitor_041 | Join {VAR_1} against the Grey Wardens. | 加入 {VAR_1} 一方，对抗灰袍守卫。 | PlayerBehaviorMonitor.cs:602 |
| gwp_playerbehaviormonitor_042 | Join the Grey Wardens and help catch criminals. | 加入灰袍守卫一方，协助缉拿罪犯。 | PlayerBehaviorMonitor.cs:603 |
| gwp_playerbehaviormonitor_043 | The Grey Wardens note that you helped bring an outlaw to justice. | 灰袍记下了你协助缉拿罪犯的义举。 | PlayerBehaviorMonitor.cs:631 |
| gwp_playerbehaviormonitor_044 | Obstructed enforcement | 妨碍执法 | PlayerBehaviorMonitor.cs:652 |
| gwp_playerbehaviormonitor_045 | Helped criminals against the Grey Wardens | 帮助罪犯对抗灰袍守卫 | PlayerBehaviorMonitor.cs:654 |
| gwp_playerbehaviormonitor_046 | The Grey Wardens record that you obstructed them. | 灰袍记下了你妨碍执法的罪行。 | PlayerBehaviorMonitor.cs:658 |
| gwp_playerbehaviormonitor_047 | Picket | 纠察队 | PlayerBehaviorMonitor.cs:687 |
| gwp_playerbehaviormonitor_048 | Grey Wardens | 灰袍守卫 | PlayerBehaviorMonitor.cs:687 |
| gwp_playerbehaviormonitor_049 | The Grey Wardens record your attack on {VAR_1}. | 灰袍记下了你袭击{VAR_1}的罪行。 | PlayerBehaviorMonitor.cs:689 |
| gwp_playerbehaviormonitor_050 | the village | 这个村庄 | PlayerBehaviorMonitor.cs:477 |
| gwp_playerbehaviormonitor_051 | You have come to {VAR_1} and joined its militia against the raiders. When you are ready, stand with the villagers and meet the enemy. | 你已抵达{VAR_1}，并与当地民兵一同抵御劫掠者。待整备妥当，便与村民并肩迎敌。 | PlayerBehaviorMonitor.cs:495 |
| gwp_playerbehaviormonitor_052 | Stand with the militia and attack | 与民兵并肩迎敌 | PlayerBehaviorMonitor.cs:500 |
| gwp_playerbehaviormonitor_053 | — | 击倒在案罪犯部队 | 未检出直接引用；保留 |
| gwp_playerbehaviormonitor_054 | The Grey Wardens note that you broke a wanted outlaw's band. | 灰袍记下了你击溃在逃罪犯的义举。 | PlayerBehaviorMonitor.cs:719 |
| gwp_playerbountybehavior_001 | Grey Warden herald | 灰袍招募使者 | PlayerBountyBehavior.cs:431 |
| gwp_playerbountybehavior_002 | A Grey Warden envoy is coming from {VAR_1} to see you. | 灰袍的招募使者正从{VAR_1}前来见你。 | PlayerBountyBehavior.cs:459 |
| gwp_playerbountybehavior_003 | The Warden escort is gone, but the bounty still stands. | 随行的灰袍已离开，悬赏仍然有效。 | PlayerBountyBehavior.cs:627 |
| gwp_playerbountybehavior_004 | Grey Warden reconnaissance party | 灰袍侦察队 | PlayerBountyBehavior.cs:690 |
| gwp_playerbountybehavior_005 | [Investigation] The spies from {VAR_1} came to report: The target was sighted near {VAR_2}. | 【侦情】{VAR_1} 的探子来报：目标在 {VAR_2} 附近目击。 | PlayerBountyBehavior.cs:700 |
| gwp_playerbountybehavior_006 | — | 读档恢复：继续追踪目标（{VAR_1}）。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_007 | — | 读档恢复：目标已击败，前往领取赏金。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_008 | — | 未知目标 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_011 | — | 悬赏目标已消失（可能已被他人击败），任务自动取消 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_012 | There is no bounty to take up just now. | 眼下没有可接的悬赏。 | PlayerBountyBehavior.cs:775 |
| gwp_playerbountybehavior_013 | — | 目标已击败！前往领取赏金 {VAR_1} 第纳尔。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_014 | — | 警察领主 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_playerbountybehavior_015 | — | 找到你的护送警察对话，直接领取赏金 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_playerbountybehavior_016 | — | 前往 {VAR_1} 处领取赏金 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_017 | — | 悬赏目标已击败！{VAR_1}（{VAR_2} 第纳尔） | 未检出直接引用；保留 |
| gwp_playerbountybehavior_018 | — | 读档恢复：继续追踪目标（{VAR_1}）。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_019 | — | 读档恢复：目标已击败，向任意灰袍领主报告即可结案并领取赏金。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_020 | unknown location | 未知位置 | PlayerBountyBehavior.cs:687, PlayerBountyBehavior.cs:894, PlayerBountyBehavior.DialogueAndNotification.cs:839 |
| gwp_playerbountybehavior_dialogueandnotification_001 | I accept. I shall serve the Grey Wardens in this matter. | 我接受，愿为灰袍守卫效力。 | PlayerBountyBehavior.DialogueAndNotification.cs:47 |
| gwp_playerbountybehavior_dialogueandnotification_002 | Then take this harness. Wear it when you ride under our warrant, and bring the money or prisoner to a Warden-lord. | 那就收下这套甲胄，穿着它替我们办案。收来的罚金或捉到的罪犯，交给灰袍领主。 | PlayerBountyBehavior.DialogueAndNotification.cs:54 |
| gwp_playerbountybehavior_dialogueandnotification_003 |  If the pursuit brings war upon you, we will speak for you when the matter is settled. | 若因此惹上战事，交差后我们会出面调停。 | PlayerBountyBehavior.DialogueAndNotification.cs:55 |
| gwp_playerbountybehavior_dialogueandnotification_004 | No. I have no interest. | 不，我不感兴趣。 | PlayerBountyBehavior.DialogueAndNotification.cs:64 |
| gwp_playerbountybehavior_dialogueandnotification_005 | As you will. This herald will not trouble you again; if you later reconsider, speak to a Warden-lord. | 随你。这名使者不会再来打扰；以后若改变主意，可以亲自找一位灰袍领主谈。 | PlayerBountyBehavior.DialogueAndNotification.cs:71 |
| gwp_playerbountybehavior_dialogueandnotification_006 | Our business is concluded. Go on your way. | 我们的事务已了结，请继续前行。 | PlayerBountyBehavior.DialogueAndNotification.cs:156 |
| gwp_playerbountybehavior_dialogueandnotification_007 | — | 关于那个悬赏任务，我已击败目标，来结算赏金。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_008 | — | 关于那个悬赏任务，我已经完成了。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_009 | The Grey Wardens have heard of your deeds. Will you hunt outlaws for us? There is pay for the work. | 灰袍听说了你的事迹。愿不愿意替我们追捕罪犯？办妥了，自有酬劳。 | PlayerBountyBehavior.DialogueAndNotification.cs:189 |
| gwp_playerbountybehavior_dialogueandnotification_014 | You are now a Grey Warden bounty hunter. The black commander's gear is in your baggage; wear it, and the Wardens will entrust bounties to you. | 你已成为灰袍悬赏猎人。黑袍指挥官套装已放入你的行囊，穿上它，灰袍便会向你托付悬赏。 | PlayerBountyBehavior.DialogueAndNotification.cs:209 |
| gwp_playerbountybehavior_dialogueandnotification_015 | You decline the Wardens. You may still ask a Warden lord to join them later. | 你回绝了灰袍的招募。日后可以向灰袍领主请求加入。 | PlayerBountyBehavior.DialogueAndNotification.cs:224 |
| gwp_membership_rejoin_player | I wish to petition for a place among the Grey Warden sworn hunters again. | 我想再次申请成为灰袍受托猎手。 | PlayerBountyBehavior.DialogueAndNotification.cs:81 |
| gwp_membership_rejoin_response | Your petition is accepted. You are again recognised as a sworn hunter; take a fresh commander's harness and answer our warrants faithfully. | 你的申请获得批准。你重新成为灰袍受托猎手；收下这套新的指挥官装备，认真履行我们交付的通缉任务。 | PlayerBountyBehavior.DialogueAndNotification.cs:90 |
| gwp_membership_leave_player | I wish to relinquish my place with the Grey Wardens. | 我想退出灰袍，不再担任受托猎手。 | PlayerBountyBehavior.DialogueAndNotification.cs:99 |
| gwp_membership_leave_prompt_higher | If you withdraw, your warrants, detachments, and battlefield relief end. Readmission will require {VAR_1} standing and a personal appeal to a Warden-lord. Is this your final decision? | 退出后，你将失去通缉任务、兵员征调与战场支援。再次加入需要达到 {VAR_1} 声望，并亲自向灰袍领主申请。你确定这是最后决定吗？ | PlayerBountyBehavior.DialogueAndNotification.cs:347 |
| gwp_membership_leave_prompt_final | If you withdraw this time, your name will be struck permanently and no Warden-lord will readmit you. Is this your final decision? | 这次退出后，你的名字将被永久从名册上划去，任何灰袍领主都不会再接纳你。你确定这是最后决定吗？ | PlayerBountyBehavior.DialogueAndNotification.cs:340 |
| gwp_membership_leave_confirm | Yes. Remove my name from the roll. | 确定。把我的名字从名册上划掉吧。 | PlayerBountyBehavior.DialogueAndNotification.cs:117 |
| gwp_membership_leave_done | It is done. The equipment already entrusted to you remains yours, but it no longer grants any Grey Warden authority. | 已经办妥。此前交给你的装备仍归你所有，但它不再代表任何灰袍权限。 | PlayerBountyBehavior.DialogueAndNotification.cs:126 |
| gwp_membership_leave_cancel | No. I will remain. | 不，我继续留下。 | PlayerBountyBehavior.DialogueAndNotification.cs:135 |
| gwp_membership_leave_cancelled | Then your place on the roll remains unchanged. | 那么你在名册上的位置维持不变。 | PlayerBountyBehavior.DialogueAndNotification.cs:144 |
| gwp_bounty_membership_ended | You left the Grey Wardens, and the active bounty contract was withdrawn. | 你已经退出灰袍，正在执行的悬赏契约随之撤销。 | PlayerBountyBehavior.QuestAndNotification.cs:132 |
| gwp_playerbountybehavior_dialogueandnotification_016 | — | 出色的工作。任务已完成，这是约定的赏金：{VAR_1} 第纳尔。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_017 | — | 出色的工作。按照约定，这是你应得的赏金：{VAR_1} 第纳尔。希望我们还有合作的机会。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_018 | — | 已从警察领主处领取悬赏赏金：{VAR_1} 第纳尔 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_dispatch_troop_order | Carry my order for troops to the Wardens, and the coin with it. | 把我的练兵订单和订金一起送到灰袍手里。 | GwpWardenDispatchDialogue.cs:175 |
| gwp_player_troop_order_unaffordable | You cannot pay the deposit. The order was not placed. | 你付不起订金，订单未成立。 | GreyWardenTroopRequestBehavior.cs:557 |
| gwp_player_troop_order_short | What you offered falls short of the price. The order was not placed. | 所付钱物不足订价，订单未成立。 | GreyWardenTroopRequestBehavior.cs:517 |
| gwp_fa_charge_cooled | Let us take up that charge again. | 这桩案子，我们再谈一次。 | GwpFieldArrestBehavior.cs:119 |
| gwp_warden_mediation_owed | This war began when you fought for the Wardens. You may ask them to mediate. | 此战因替灰袍出手而起，可请灰袍出面调停。 | PoliceAntiWarDeclaration.cs:202 |
| gwp_warden_marry_ask | Be my wife. | 做我的妻子吧。 | GwpWardenMarriageBehavior.cs:40 |
| gwp_warden_marry_yes | Then it is settled. I am yours, and you are mine. | 那就这么定了。我是你的，你也是我的。 | GwpWardenMarriageBehavior.cs:43 |
| gwp_warden_mediation_ask | The fighting I did on your account has left me at war. Speak for me. | 我替你们打的那些仗，把我卷进战争里了。出面替我说句话。 | GreyWardenTroopRequestBehavior.cs:253 |
| gwp_warden_mediation_done | Consider it carried. Word goes out today, and the quarrels you took up for us are closed. | 这事交给我们。今天就把话递出去，你替我们挑起来的那几桩，都了了。 | GreyWardenTroopRequestBehavior.cs:346 |
| gwp_warden_mediation_moot | There is nothing left for us to carry. Whatever you took up on our account is already settled. | 没有什么要我们去递的了。你替我们担下的那些，早就平了。 | GreyWardenTroopRequestBehavior.cs:347 |
| gwp_dispatch_peace | Ride to the Wardens and have them close the quarrels I took up for them. | 去灰袍那儿走一趟，把我替他们挑起来的那几桩梁子了了。 | GwpWardenDispatchDialogue.cs:171 |
| gwp_dispatch_peace_done | The message was delivered. The Wardens have made peace on your behalf. | 信已送到，灰袍已代你与对方议和。 | GwpWardenDispatchBehavior.cs:637 |
| gwp_dispatch_peace_moot | The message was delivered, but there is nothing left to mediate. | 信已送到，但已无需调停。 | GwpWardenDispatchBehavior.cs:638 |
| gwp_fa_rebuff_upright | You have my answer. I do not give a man two. | 我的答复已经给过你了。一句话，我不说两遍。 | GwpFieldArrestLines.cs:70 |
| gwp_fa_rebuff_cold | Nothing has changed since we last spoke. | 上回谈完到现在，什么都没变。 | GwpFieldArrestLines.cs:72 |
| gwp_fa_rebuff_fierce | We settled this once already. Press me again and it will not be with words. | 这事已经了断过一回。再逼我，就不是动嘴了。 | GwpFieldArrestLines.cs:74 |
| gwp_fa_rebuff_soft | Please. We have been over it once already. Let it rest. | 行行好，这事已经翻过一遍了，让它歇歇吧。 | GwpFieldArrestLines.cs:76 |
| gwp_fa_rebuff_tight | You have had your talk out of me. Talk is not free, and you have spent it. | 你已经从我这儿讨过话了。话也是要本钱的，你花完了。 | GwpFieldArrestLines.cs:78 |
| gwp_fa_rebuff_plain | We have spoken about this. My answer stands. | 这事我们谈过了。我的答复不变。 | GwpFieldArrestLines.cs:80 |
| gwp_dispatch_order_moot | The Wardens cannot take the order now. Your deposit is being returned. | 灰袍眼下无法接单，订金将退回给你。 | GwpWardenDispatchBehavior.cs:665 |
| gwp_player_troop_order_filed_prepaid | Your order and deposit are with the Wardens. The Training Warden will bring the soldiers to you herself. | 订单与订金已交给灰袍，练兵领主训练完成后会亲自送来。 | GreyWardenTroopRequestBehavior.cs:585 |
| gwp_playerbountybehavior_dialogueandnotification_020 | — | 目标势力：{VAR_1}<br> | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_021 | Crime Type: {VAR_1} | 犯罪类型：{VAR_1} | PlayerBountyBehavior.DialogueAndNotification.cs:766 |
| gwp_playerbountybehavior_dialogueandnotification_022 | Last sighting: {VAR_1} nearby | 最后目击：{VAR_1} 附近 | PlayerBountyBehavior.DialogueAndNotification.cs:767 |
| gwp_playerbountybehavior_dialogueandnotification_023 | — | 队伍规模：{VAR_1} 人<br> | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_024 | — | 预计赏金：约 {VAR_1} 第纳尔<br> | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_026 | — | 完成后前往警察家族领主处领取赏金。 | 当前模块的C#/XML/XSLT中未检出该键的调用；保留条目，不重新接入。 |
| gwp_playerbountybehavior_dialogueandnotification_027 | Grey Warden commission | 灰袍办案委托 | PlayerBountyBehavior.DialogueAndNotification.cs:775 |
| gwp_playerbountybehavior_dialogueandnotification_028 | Accept the charge | 接受任务 | PlayerBountyBehavior.DialogueAndNotification.cs:779 |
| gwp_playerbountybehavior_dialogueandnotification_029 | Refuse | 拒绝 | PlayerBountyBehavior.DialogueAndNotification.cs:780 |
| gwp_playerbountybehavior_dialogueandnotification_030 | The target is gone. The bounty is withdrawn. | 目标已不在，悬赏取消。 | PlayerBountyBehavior.DialogueAndNotification.cs:795 |
| gwp_playerbountybehavior_dialogueandnotification_031 | — | 目标已失效，悬赏任务取消 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_032 | — | 灰袍护送方已就位，将跟随你追击，直至目标落败。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_033 | — | 目标：{VAR_1}（当前 {VAR_2} 人）。<br> | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_034 | Last sighted location: Near {VAR_1}.<br> | 最后目击位置：{VAR_1} 附近。<br> | PlayerBountyBehavior.DialogueAndNotification.cs:848 |
| gwp_playerbountybehavior_dialogueandnotification_035 | — | 击败目标后，向任意灰袍领主报告，即可领取约 {VAR_1} 第纳尔赏金。 | 未检出直接引用；保留 |
| gwp_bounty_contract_timed_out | The bounty has expired. The hunt is over. | 悬赏期限已到，追捕结束。 | PlayerBountyBehavior.cs:257 |
| gwp_bounty_contract_timed_out_log | The bounty contract expired before the quarry was defeated. | 悬赏契约到期时目标仍未被击败，任务失败。 | PlayerBountyBehavior.QuestAndNotification.cs:121 |
| gwp_bounty_ready_for_turnin | The field operation has ended. Deliver the fines or the assigned prisoner to any Grey Warden lord, in person or by your own men. | 外勤处理已结束。请将代收罚金或受托追捕的俘虏交给任意灰袍领主；你也可以派自己的人送去。 | PlayerBountyBehavior.QuestAndNotification.cs:115 |
| gwp_bounty_notification_description | New bounty contracts are available. | 新的悬赏契约可供挑选。 | PlayerBountyBehavior.QuestAndNotification.cs:149 |
| gwp_bounty_notification_title | Grey Warden bounty contracts | 灰袍悬赏契约 | PlayerBountyBehavior.QuestAndNotification.cs:153 |
| gwp_bounty_choice_nearest | Nearest | 最近 | PlayerBountyBehavior.DialogueAndNotification.cs:753 |
| gwp_bounty_choice_harder | Harder | 较难 | PlayerBountyBehavior.DialogueAndNotification.cs:751 |
| gwp_bounty_choice_easier | Easier | 较简单 | PlayerBountyBehavior.DialogueAndNotification.cs:752 |
| gwp_bounty_choice_unavailable | {VAR_1}: no separate valid contract | {VAR_1}：暂无可分配的独立契约 | PlayerBountyBehavior.DialogueAndNotification.cs:637 |
| gwp_bounty_choice_unavailable_hint | More open cases are needed for a separate choice. | 需要更多未结案件才能提供另一份不同的选择。 | PlayerBountyBehavior.DialogueAndNotification.cs:640 |
| gwp_bounty_choice_label | {VAR_1}: {VAR_2} — {VAR_3}; assessed fine {VAR_4} denars | {VAR_1}：{VAR_2}，{VAR_3}；应缴罚金 {VAR_4} 第纳尔 | PlayerBountyBehavior.DialogueAndNotification.cs:646 |
| gwp_bounty_choice_hint | {VAR_1}; last sighted near {VAR_2}. | {VAR_1}；最近在 {VAR_2} 附近出现。 | PlayerBountyBehavior.DialogueAndNotification.cs:652 |
| gwp_bounty_select_title | Select a Grey Warden bounty | 选择灰袍悬赏 | PlayerBountyBehavior.DialogueAndNotification.cs:660 |
| gwp_bounty_select_description | Choose a nearby, stronger or weaker offender. The amount shown is the assessed fine, not your personal payment. | 选择较近、较强或较弱的目标。下列金额是卷宗应收罚金，不是发给你的报酬。 | PlayerBountyBehavior.DialogueAndNotification.cs:661 |
| gwp_bounty_review_contract | Review contract | 查看契约 | PlayerBountyBehavior.DialogueAndNotification.cs:666 |
| gwp_bounty_difficulty_easy | Easy | 较简单 | PlayerBountyBehavior.DialogueAndNotification.cs:744 |
| gwp_bounty_difficulty_standard | Standard | 标准 | PlayerBountyBehavior.DialogueAndNotification.cs:746 |
| gwp_bounty_difficulty_hard | Hard | 较难 | PlayerBountyBehavior.DialogueAndNotification.cs:745 |
| gwp_bounty_contract_target | Target: {VAR_1} | 目标：{VAR_1} | PlayerBountyBehavior.DialogueAndNotification.cs:765 |
| gwp_bounty_contract_difficulty | Assessed difficulty: {VAR_1} | 评估难度：{VAR_1} | PlayerBountyBehavior.DialogueAndNotification.cs:768 |
| gwp_bounty_contract_reward | Assessed fine: {VAR_1} denars. Once you deliver, we will review the case and your losses and settle your expenses. | 卷宗应缴罚金：{VAR_1} 第纳尔。交付后，我们会核实案情和你部下的伤亡，结算办案酬劳。 | PlayerBountyBehavior.DialogueAndNotification.cs:769 |
| gwp_bounty_contract_deadline | The warrant remains active for 45 days. | 通缉令有效期为四十五天。 | PlayerBountyBehavior.DialogueAndNotification.cs:770 |
| gwp_bounty_contract_turnin | Break his army, collect the fine, or take him prisoner, then report to any Grey Warden lord. Bring him in alive and the Wardens also buy him from you at his ransom price. | 击溃其部队、收取罚金或将其俘虏，之后向任意灰袍领主复命。若把人活着押来，灰袍另按赎金价向你收购。 | PlayerBountyBehavior.DialogueAndNotification.cs:771 |
| gwp_bounty_quest_target | Target: {VAR_1}. Assessed difficulty: {VAR_2}.<br> | 目标：{VAR_1}。评估难度：{VAR_2}。<br> | PlayerBountyBehavior.DialogueAndNotification.cs:844 |
| gwp_bounty_quest_reward | Within 45 days, collect the assessed fine of {VAR_1} denars or capture the offender. Deliver the money or prisoner to a Grey Warden lord. Expenses depend on the actual delivery. | 在 45 天内收取卷宗应缴的 {VAR_1} 第纳尔罚金，或俘虏目标。将钱或人交给灰袍领主后，按实际交付结算办案酬劳。 | PlayerBountyBehavior.DialogueAndNotification.cs:851 |
| gwp_bounty_contract_accepted | You take up the bounty on {VAR_1}. The fine is {VAR_3} denars. | 你接下了悬赏：{VAR_1}。罚金 {VAR_3} 第纳尔。 | PlayerBountyBehavior.DialogueAndNotification.cs:857 |
| gwp_playerbountybehavior_dialogueandnotification_036 | — | 已接受悬赏任务：追击 {VAR_1} | 未检出直接引用；保留 |
| gwp_playerbountybehavior_dialogueandnotification_037 | — | （{VAR_1} 人），赏金约 {VAR_2} 第纳尔 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_encyclopedia_001 | Unspecified escort unit | 未指定护送部队 | PlayerBountyBehavior.Encyclopedia.cs:27 |
| gwp_playerbountybehavior_encyclopedia_002 | Unknown target | 未知目标 | PlayerBountyBehavior.Encyclopedia.cs:38 |
| gwp_playerbountybehavior_encyclopedia_003 | The target is still escaping, and the Grey Wardens troops are escorting the player to pursue | 目标仍在逃逸，灰袍部队正在护送玩家追捕 | PlayerBountyBehavior.Encyclopedia.cs:40 |
| gwp_playerbountybehavior_encyclopedia_004 | The target has been defeated, waiting for the player to receive the bounty and the Grey Wardens will deal with the aftermath | 目标已经被击败，等待玩家领取赏金并由灰袍善后 | PlayerBountyBehavior.Encyclopedia.cs:42 |
| gwp_playerbountybehavior_encyclopedia_005 | The bounty process is still in progress | 悬赏流程仍在进行 | PlayerBountyBehavior.Encyclopedia.cs:43 |
| gwp_playerbountybehavior_encyclopedia_006 | Player bounty collaboration: The current target is {VAR_1}, and the hostile forces are {VAR_2}. Escort force: {VAR_3}; current stage: {VAR_4}. | 玩家悬赏协同：当前目标是 {VAR_1}，敌对势力为 {VAR_2}。护送部队：{VAR_3}；当前阶段：{VAR_4}。 | PlayerBountyBehavior.Encyclopedia.cs:45 |
| gwp_playerbountybehavior_questandnotification_001 | Unknown target | 未知目标 | PlayerBountyBehavior.QuestAndNotification.cs:63 |
| gwp_playerbountybehavior_questandnotification_002 | Grey Warden commission: {VAR_1} | 灰袍委托：{VAR_1} | PlayerBountyBehavior.QuestAndNotification.cs:78 |
| gwp_playerbountybehavior_questandnotification_003 | Your report and delivery have been recorded and the commission is concluded. | 报告与交付已记入卷宗，这份委托已经结清。 | PlayerBountyBehavior.QuestAndNotification.cs:102 |
| gwp_playerbountybehavior_questandnotification_004 | — | 目标已失踪，悬赏任务取消。 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_questandnotification_005 | — | 未知目标 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_questandnotification_006 | — | 追缉目标：{VAR_1} | 未检出直接引用；保留 |
| gwp_playerbountybehavior_questandnotification_007 | — | 未知目标 | 未检出直接引用；保留 |
| gwp_playerbountybehavior_questandnotification_008 | — | 灰袍悬赏：{VAR_1} | 未检出直接引用；保留 |
| gwp_playerbountybehavior_questandnotification_009 | — | 该悬赏目标已失效 | 未检出直接引用；保留 |
| gwp_policeantirecruitmentmodel_001 | The Grey Wardens do not join wars of aggression declared by the realm’s ordinary diplomatic councils. | 灰袍守卫不会参与原版外交 AI 的主动宣战。 | PoliceAntiRecruitmentModel.cs:53 |
| gwp_policecrimemonitorenhanced_001 | Villager | 村民 | PoliceCrimeMonitorEnhanced.cs:362 |
| gwp_policecrimemonitorenhanced_002 | Attack villager | 攻击村民 | PoliceCrimeMonitorEnhanced.cs:365 |
| gwp_policecrimemonitorenhanced_003 | Attack villager | 攻击村民 | PoliceCrimeMonitorEnhanced.cs:368 |
| gwp_policecrimemonitorenhanced_004 | Caravan | 商队 | PoliceCrimeMonitorEnhanced.cs:375 |
| gwp_policecrimemonitorenhanced_005 | Attack caravan | 攻击商队 | PoliceCrimeMonitorEnhanced.cs:377 |
| gwp_policecrimemonitorenhanced_006 | Attack caravan | 攻击商队 | PoliceCrimeMonitorEnhanced.cs:379 |
| gwp_policecrimemonitorenhanced_007 | {VAR_1} Villager | {VAR_1}村的村民 | PoliceCrimeMonitorEnhanced.cs:409 |
| gwp_policecrimemonitorenhanced_008 | Raid village (start) | 劫掠村庄(开始) | PoliceCrimeMonitorEnhanced.cs:411 |
| gwp_policecrimemonitorenhanced_009 | Village={VAR_1} | 村庄={VAR_1} | PoliceCrimeMonitorEnhanced.cs:411 |
| gwp_policecrimemonitorenhanced_010 | Raid Village | 劫掠村庄 | PoliceCrimeMonitorEnhanced.cs:413 |
| gwp_policeenforcementbehavior_001 | The atonement contract times out, reputation -5. | 赎罪任务超时，声望 -5。 | PoliceEnforcementBehavior.cs:228 |
| gwp_policeenforcementbehavior_002 | Your atonement target is gone. The Wardens close the charge unanswered, and it is held against your name. | 你要赎罪的那个人不见了。灰袍把这笔账原样收起，记在你名下。 | PoliceEnforcementBehavior.cs:236 |
| gwp_policeenforcementbehavior_003 | You have defeated {VAR_1}. Report to any Warden lord to complete your atonement. | 你已击败赎罪目标{VAR_1}，向任意一位灰袍领主复命即可。 | PoliceEnforcementBehavior.cs:315 |
| gwp_policeenforcementbehavior_004 | Your atonement has failed. | 赎罪失败。 | PoliceEnforcementBehavior.cs:320 |
| gwp_policeenforcementbehavior_005 | Insufficient fine | 罚款不足 | PoliceEnforcementBehavior.cs:530 |
| gwp_policeenforcementbehavior_006 | Escort fine unpaid | 押送罚款未缴清 | PoliceEnforcementBehavior.cs:530 |
| gwp_policeenforcementbehavior_007 | Fortress | 堡垒 | PoliceEnforcementBehavior.cs:534 |
| gwp_policeenforcementbehavior_008 | You are brought to {VAR_1} and released after paying {VAR_3} denars. | 你被押送到{VAR_1}，缴纳罚金 {VAR_3} 第纳尔后获释。 | PoliceEnforcementBehavior.cs:536 |
| gwp_policeenforcementbehavior_009 | FORT | 堡垒 | PoliceEnforcementBehavior.cs:1367 |
| gwp_policeenforcementbehavior_010 | You are defeated by {VAR_1} and taken to {VAR_2}. | 你被{VAR_1}击败，正被押往{VAR_2}。 | PoliceEnforcementBehavior.cs:1369 |
| gwp_policeenforcementbehavior_atonementquest_001 | Unknown Target | 未知目标 | PoliceEnforcementBehavior.AtonementQuest.cs:35 |
| gwp_policeenforcementbehavior_atonementquest_002 | Unknown Target | 未知目标 | PoliceEnforcementBehavior.AtonementQuest.cs:41 |
| gwp_policeenforcementbehavior_atonementquest_003 | the Grey Wardens Atonement: The Hunt {VAR_1} | 灰袍赎罪令：追捕 {VAR_1} | PoliceEnforcementBehavior.AtonementQuest.cs:45 |
| gwp_policeenforcementbehavior_atonementquest_004 | The target has been defeated. Report to any Grey Warden lord to complete your atonement. | 已击败目标。向任意灰袍领主交付赎罪任务。 | 赎罪任务进度文字，原文仍用“灰袍警察”。 |
| gwp_policeenforcementbehavior_atonementquest_005 | Atonement completed: reputation +{VAR_1}, current reputation {VAR_2}. | 赎罪完成：声望 +{VAR_1}，当前声望 {VAR_2}。 | PoliceEnforcementBehavior.AtonementQuest.cs:76 |
| gwp_policeenforcementbehavior_atonementquest_006 | Unknown location | 未知位置 | PoliceEnforcementBehavior.AtonementQuest.cs:102 |
| gwp_policeenforcementbehavior_atonementquest_007 | Contract assigned: Defeat {VAR_2} within {VAR_1} days (case size {VAR_3} people). | 任务下达：在 {VAR_1} 天内击败 {VAR_2}（接案规模 {VAR_3} 人）。 | PoliceEnforcementBehavior.AtonementQuest.cs:113 |
| gwp_policeenforcementbehavior_atonementquest_008 | Scouts last reported the target near {VAR_1}. Once the task is done, report to any Grey Warden lord to complete your atonement. | 探子回报：目标最后出现在{VAR_1}附近。完成后向任意灰袍领主交付赎罪任务。 | 赎罪任务中的首次目标位置报告。 |
| gwp_policeenforcementbehavior_atonementquest_009 | Unknown location | 未知位置 | PoliceEnforcementBehavior.AtonementQuest.cs:130, PoliceEnforcementBehavior.AtonementQuest.cs:133 |
| gwp_policeenforcementbehavior_atonementquest_010 | Spy report: {VAR_1} recently appeared near {VAR_2} (about {VAR_3} people). | 探子回报：{VAR_1} 最近出现在 {VAR_2} 附近（约 {VAR_3} 人）。 | PoliceEnforcementBehavior.AtonementQuest.cs:135, PoliceEnforcementBehavior.AtonementQuest.cs:140 |
| gwp_policeenforcementbehavior_atonementquest_011 | The atonement target is still at large. Continue the hunt. | 赎罪的目标仍然在逃。继续追下去。 | PoliceEnforcementBehavior.AtonementQuest.cs:174 |
| gwp_policeenforcementbehavior_atonementquest_012 | The atonement target is still at large. Continue the hunt. | 赎罪的目标仍然在逃。继续追下去。 | PoliceEnforcementBehavior.AtonementQuest.cs:184 |
| gwp_policeenforcementbehavior_atonementquest_013 | The atonement target is still at large. Continue the hunt. | 赎罪的目标仍然在逃。继续追下去。 | PoliceEnforcementBehavior.AtonementQuest.cs:198 |
| gwp_policeenforcementbehavior_atonementquest_014 | Concerning the atonement (up to {VAR_1} standing may be restored upon discharge) | 关于赎罪任务（提交后恢复最多 {VAR_1} 点声望） | PoliceEnforcementBehavior.AtonementQuest.cs:211 |
| gwp_policeenforcementbehavior_atonementquest_015 | The account is verified. Your atonement is complete, and the roll allows the restoration of up to {VAR_1} standing. | 核验无误。你已完成赎罪任务，按案卷可恢复最多 {VAR_1} 点声望。 | PoliceEnforcementBehavior.AtonementQuest.cs:214 |
| gwp_policeenforcementbehavior_atonementquest_016 | Your atonement is complete. | 赎罪已完成。 | PoliceEnforcementBehavior.AtonementQuest.cs:229 |
| gwp_policeenforcementbehavior_atonementquest_017 | Your atonement is complete. Through the Wardens' mediation, you are at peace with {VAR_1}. | 赎罪完成，经灰袍调停，你与{VAR_1}恢复了和平。 | PoliceEnforcementBehavior.AtonementQuest.cs:249 |
| gwp_policeenforcementbehavior_delaypatrols_001 | Grey Warden provost relief party | 灰袍纠察支援队 | PoliceEnforcementBehavior.DelayPatrols.cs:682 |
| gwp_policeenforcementbehavior_delaypatrols_002 | — | 与灰袍守卫处于战争状态，声望 -4。当前声望：{VAR_1} | 未检出直接引用；保留 |
| gwp_policeenforcementbehavior_delaypatrols_003 | Accumulated crimes | 累计犯罪 | PoliceEnforcementBehavior.DelayPatrols.cs:1214 |
| gwp_policeenforcementbehavior_delaypatrols_004 | Reputation has reached {VAR_1} | 声望已达 {VAR_1} | PoliceEnforcementBehavior.DelayPatrols.cs:1216 |
| gwp_policeenforcementbehavior_delaypatrols_005 | Grey Warden pursuit detachment | 灰袍高速追截队 | PoliceEnforcementBehavior.DelayPatrols.cs:438 |
| gwp_troop_order_cohort_name | Grey Warden training cohort | 灰袍随行练兵队 | GreyWardenTroopRequestBehavior.Cohort.cs:156 |
| gwp_troop_order_cohort_lost | The soldiers being trained for you were lost on the road. The Wardens are training another company. | 为你训练的士兵在途中覆没，灰袍正重新训练一批。 | GreyWardenTroopRequestBehavior.Cohort.cs:101 |
| gwp_policeenforcementbehavior_dialogue_001 | By Grey Warden ordinance, the lawful fine must first be paid. | 按灰袍法令，先把正式罚金缴清。 | PoliceEnforcementBehavior.Dialogue.cs:65 |
| gwp_policeenforcementbehavior_dialogue_002 | The fine is acknowledged. This case is closed; you may depart. | 罚金确认。本轮案件结案，你可以离开了。 | PoliceEnforcementBehavior.Dialogue.cs:83 |
| gwp_policeenforcementbehavior_dialogue_003 | Your offer falls below the lawful fine. You may raise it or choose another response. | 你的出价低于正式罚金。你可以继续出价，或选择其他处理方式。 | PoliceEnforcementBehavior.Dialogue.cs:92 |
| gwp_policeenforcementbehavior_dialogue_004 | I confess the offence and accept judgment. Grant me a charge of atonement. | 我认罪认罚，请给我赎罪任务。 | PoliceEnforcementBehavior.Dialogue.cs:101 |
| gwp_policeenforcementbehavior_dialogue_005 | No charge of atonement can presently be assigned. You may pay the fine or refuse the order. | 当前无法分配赎罪任务。你可以继续缴纳罚金，或拒绝执法。 | PoliceEnforcementBehavior.Dialogue.cs:119 |
| gwp_policeenforcementbehavior_dialogue_006 | I refuse the order. Let arms decide. | 拒绝执法，开战。 | PoliceEnforcementBehavior.Dialogue.cs:128 |
| gwp_policeenforcementbehavior_dialogue_007 | Resistance is entered upon the roll. Grey Wardens—make the arrest! | 拒捕记录在案。灰袍守卫，执行抓捕！ | PoliceEnforcementBehavior.Dialogue.cs:146 |
| gwp_policeenforcementbehavior_dialogue_008 | You carry {VAR_1} denars, enough to pay in full. | 你当前携带 {VAR_1} 金，可直接缴清。 | PoliceEnforcementBehavior.Dialogue.cs:217 |
| gwp_policeenforcementbehavior_dialogue_009 | You carry {VAR_1} denars. You may make another offer at the table, or confess and accept judgment. | 你当前携带 {VAR_1} 金，可在谈判界面继续出价，或改选认罪认罚。 | PoliceEnforcementBehavior.Dialogue.cs:219 |
| gwp_policeenforcementbehavior_dialogue_010 | Stand! The Grey Wardens come under warrant. Your present standing is {VAR_1}, | 站住！灰袍守卫正在执行逮捕。你当前负声望 {VAR_1}， | PoliceEnforcementBehavior.Dialogue.cs:224 |
| gwp_policeenforcementbehavior_dialogue_011 | and the lawful fine in this case is {VAR_1} denars. {VAR_2} | 本案正式罚金 {VAR_1} 金。{VAR_2} | PoliceEnforcementBehavior.Dialogue.cs:225 |
| gwp_policeenforcementbehavior_dialogue_012 | Pay the lawful fine ({VAR_1} denars; clear the warrant) | 缴纳正式罚金（{VAR_1} 金，清除通缉） | PoliceEnforcementBehavior.Dialogue.cs:237 |
| gwp_policeenforcementbehavior_dialogue_013 | unknown quarry | 未知目标 | PoliceEnforcementBehavior.Dialogue.cs:321 |
| gwp_policeenforcementbehavior_dialogue_014 | A charge of atonement has been issued: pursue {VAR_1} (strength when assigned: {VAR_2}). | 赎罪任务已下达：追捕 {VAR_1}（接案规模 {VAR_2} 人）。 | PoliceEnforcementBehavior.Dialogue.cs:340 |
| gwp_policeenforcementbehavior_dialogue_015 | Fulfilment may restore up to {VAR_1} standing, but never above 0; | 完成可恢复最多 {VAR_1} 点声望（最高恢复到 0）； | PoliceEnforcementBehavior.Dialogue.cs:341 |
| gwp_policeenforcementbehavior_dialogue_016 | failure will impose a further loss of 5 standing. | 失败将追加 5 点负声望。 | PoliceEnforcementBehavior.Dialogue.cs:342 |
| gwp_policeenforcementbehavior_dialogue_017 | The Wardens charge you to defeat {VAR_1} within {VAR_2} days in atonement. | 灰袍命你在 {VAR_2} 天内击败{VAR_1}以赎罪。 | PoliceEnforcementBehavior.Dialogue.cs:345 |
| gwp_policeenforcementbehavior_dialogue_018 | Pay the lawful fine | 缴纳正式罚金 | PoliceEnforcementBehavior.Dialogue.cs:361 |
| gwp_policeenforcementbehavior_dialogue_019 | The fine of {VAR_1} denars is paid. You are no longer wanted. | 罚金 {VAR_1} 第纳尔已缴清，通缉撤销。 | PoliceEnforcementBehavior.Dialogue.cs:395 |
| gwp_policeenforcementbehavior_dialogue_020 | You refuse to submit. The Wardens will take you by force. | 你拒绝伏法，灰袍将强行缉拿。 | PoliceEnforcementBehavior.Dialogue.cs:475 |
| gwp_enforcement_companion_charge | Stand! {VAR_1} travels with you under a Grey Warden warrant. The fine for {VAR_1}'s case is {VAR_2} denars. {VAR_3} | 站住！与你同行的 {VAR_1} 有灰袍未结案件。本案罚金为 {VAR_2} 金。{VAR_3} | PoliceEnforcementBehavior.Dialogue.cs:226 |
| gwp_enforcement_companion_insufficient | You carry {VAR_1} denars. Return when you can pay the fine in full. | 你当前携带 {VAR_1} 金。凑齐罚金后可回来一次缴清。 | PoliceEnforcementBehavior.Dialogue.cs:220 |
| gwp_enforcement_companion_pay | Pay this companion's fine ({VAR_1} denars) | 替这名同伴缴纳罚金（{VAR_1} 金） | PoliceEnforcementBehavior.Dialogue.cs:249 |
| gwp_enforcement_companion_paid | {VAR_1} has paid the fine of {VAR_2} denars. The case is closed. | {VAR_1}已缴清罚金 {VAR_2} 第纳尔，案件了结。 | PoliceEnforcementBehavior.Dialogue.cs:285 |
| gwp_enforcement_companion_leave | I cannot settle this companion's case now. | 我现在无法替这名同伴结案。 | PoliceEnforcementBehavior.Dialogue.cs:137 |
| gwp_policepatrolbehavior_001 | A return order has been issued, waiting to be destroyed upon arrival at the settlement. | 已下发返程命令，等待到达定居点后销毁 | PolicePatrolBehavior.cs:79 |
| gwp_policepatrolbehavior_002 | has been scheduled: Destroy the pickets after returning to the settlement. | 已调度：返程至驻地后销毁纠察队 | PolicePatrolBehavior.cs:86 |
| gwp_policepatrolbehavior_003 | According to the Grey Wardens decree, the formal fine must be paid first. | 按灰袍法令，先把正式罚金缴清。 | PolicePatrolBehavior.cs:161 |
| gwp_policepatrolbehavior_004 | Penalty confirmation. This round of cases is concluded. | 罚金确认。本轮案件到此结案。 | PolicePatrolBehavior.cs:179 |
| gwp_policepatrolbehavior_005 | Your bid is less than the formal penalty. You can continue bidding, or refuse enforcement. | 你的出价低于正式罚金。你可以继续出价，或拒绝执法。 | PolicePatrolBehavior.cs:188 |
| gwp_policepatrolbehavior_006 | Yes, quote your release price. | 可以，报出你的放行价码。 | PolicePatrolBehavior.cs:207 |
| gwp_policepatrolbehavior_007 | That will do. You may go. | 够了。你可以走了。 | PolicePatrolBehavior.cs:225 |
| gwp_policepatrolbehavior_008 | That is not enough. Lay down your weapons! | 这点钱可不够。放下武器！ | PolicePatrolBehavior.cs:234 |
| gwp_policepatrolbehavior_009 | I refuse enforcement. | 我拒绝执法。 | PolicePatrolBehavior.cs:244 |
| gwp_policepatrolbehavior_010 | Refusal to Enforcement Recorded. Pickets, make arrests! | 拒绝执法已记录。纠察队，执行抓捕！ | PolicePatrolBehavior.cs:254 |
| gwp_policepatrolbehavior_011 | The return trip is being suppressed and refuses to enter the picket dialogue again. | 返程抑制中，拒绝再次进入纠察队对话 | PolicePatrolBehavior.cs:271 |
| gwp_policepatrolbehavior_012 | Enter the picket dialogue: party={VAR_1}, rep={VAR_2}, fine={VAR_3} | 进入纠察队对话：party={VAR_1}, rep={VAR_2}, fine={VAR_3} | PolicePatrolBehavior.cs:294 |
| gwp_policepatrolbehavior_013 | You carry {VAR_1} denars, enough to pay in full. | 你当前携带 {VAR_1} 金，可直接缴清。 | PolicePatrolBehavior.cs:297 |
| gwp_policepatrolbehavior_014 | You carry {VAR_1} denars. You may raise your offer at the table or refuse the order. | 你当前携带 {VAR_1} 金，可在谈判界面继续出价或选择拒绝。 | PolicePatrolBehavior.cs:298 |
| gwp_policepatrolbehavior_015 | Stand! This provost patrol acts under warrant. Your standing is {VAR_1}; the lawful fine is {VAR_2} denars. {VAR_3} | 站住！纠察队正在执法。你当前负声望 {VAR_1}，正式罚金 {VAR_2} 金。{VAR_3} | PolicePatrolBehavior.cs:301 |
| gwp_policepatrolbehavior_016 | Pay the lawful fine ({VAR_1} denars; end this pursuit) | 缴纳正式罚金（{VAR_1} 金，结束本轮追捕） | PolicePatrolBehavior.cs:321 |
| gwp_policepatrolbehavior_017 | Negotiate passage ({VAR_1} denars sought; standing unchanged) | 谈判放行（目标 {VAR_1} 金，声望不变） | PolicePatrolBehavior.cs:330 |
| gwp_policepatrolbehavior_018 | You refuse to submit. The patrol will take you by force. | 你拒绝伏法，纠察队将强行缉拿。 | PolicePatrolBehavior.cs:358 |
| gwp_policepatrolbehavior_019 | Pay the facilitation fee | 缴纳通融费 | PolicePatrolBehavior.cs:364 |
| gwp_policepatrolbehavior_020 | Pay the formal fine | 缴纳正式罚金 | PolicePatrolBehavior.cs:369 |
| gwp_policepatrolbehavior_021 | The patrol lets you pass. | 纠察队同意放行。 | PolicePatrolBehavior.cs:378 |
| gwp_policepatrolbehavior_022 | The fine is paid and you are no longer wanted. The patrol withdraws. | 罚金已缴清，通缉撤销，纠察队撤离。 | PolicePatrolBehavior.cs:400 |
| gwp_policepatrolbehavior_023 | The conversation ends and repatriation begins. active={VAR_1}, returning={VAR_2} | 对话结束，开始遣返。active={VAR_1}, returning={VAR_2} | PolicePatrolBehavior.cs:423 |
| gwp_policepatrolbehavior_024 | PlayerEncounter.Finish(false) has been called | PlayerEncounter.Finish(false) 已调用 | PolicePatrolBehavior.cs:429 |
| gwp_policepatrolbehavior_026 | — | 与灰袍守卫处于战争状态，声望 -4。当前声望：{VAR_1} | 未检出直接引用；保留 |
| gwp_policepatrolbehavior_027 | The Grey Wardens no longer seek you. | 灰袍已撤销对你的通缉。 | PolicePatrolBehavior.cs:494 |
| gwp_policepatrolbehavior_028 | A Grey Warden patrol is coming for you. | 灰袍纠察队正前来缉拿你。 | PolicePatrolBehavior.cs:535 |
| gwp_policepatrolbehavior_029 | Accumulated crimes | 累计犯罪 | PolicePatrolBehavior.cs:564 |
| gwp_policepatrolbehavior_030 | Reputation has reached {VAR_1} | 声望已达 {VAR_1} | PolicePatrolBehavior.cs:566 |
| gwp_policepatrolbehavior_031 | The Grey Wardens have ordered your arrest. | 灰袍已下令缉拿你。 | PolicePatrolBehavior.cs:569 |
| gwp_policepatrolbehavior_033 | — | 纠察队补给耗尽，正在撤回。 | 未检出直接引用；保留 |
| gwp_policepatrolbehavior_034 | You refuse to submit. The patrol will take you by force. | 你拒绝伏法，纠察队将强行缉拿。 | PolicePatrolBehavior.cs:701 |
| gwp_policepatrolbehavior_035 | Picket Patrol | 纠察队巡逻 | PolicePatrolBehavior.cs:729 |
| gwp_policepatrolbehavior_036 | A Grey Warden patrol has set out from {VAR_1} after you. | 灰袍纠察队已从{VAR_1}出发追捕你。 | PolicePatrolBehavior.cs:755 |
| gwp_policepatrolbehavior_037 | MapEventEnded It is detected that the player has been captured and enters the escort process | MapEventEnded 检测到玩家已被俘，进入押送流程 | PolicePatrolBehavior.cs:815 |
| gwp_policepatrolbehavior_038 | MapEventEnded No winner, the surrender path is processed as the picket victory | MapEventEnded 无 winner，按投降路径处理为纠察队胜利 | PolicePatrolBehavior.cs:858 |
| gwp_policepatrolbehavior_039 | The patrol has defeated you. You are being taken to {VAR_1}. | 你被纠察队击败，正被押往{VAR_1}。 | PolicePatrolBehavior.cs:903 |
| gwp_policepatrolbehavior_040 | The nearest town | 最近城镇 | PolicePatrolBehavior.cs:992 |
| gwp_policepatrolbehavior_041 | You are brought to {VAR_1} and released after paying {VAR_3} denars. | 你被押送到{VAR_1}，缴纳罚金 {VAR_3} 第纳尔后获释。 | PolicePatrolBehavior.cs:994 |
| gwp_policepatrolbehavior_042 | The patrol collects a fine of {VAR_1} denars. | 纠察队收取了罚金 {VAR_1} 第纳尔。 | PolicePatrolBehavior.cs:1006 |
| gwp_policepatrolbehavior_encyclopedia_001 | The player is in provost custody, under escort or awaiting judgment. | 玩家已被纠察队控制，正在押送或等待处罚 | PolicePatrolBehavior.Encyclopedia.cs:39 |
| gwp_policepatrolbehavior_encyclopedia_002 | {VAR_1} provost patrols remain upon the map under orders of arrest. | 地图上仍有 {VAR_1} 支纠察队在执行强制缉拿 | PolicePatrolBehavior.Encyclopedia.cs:41 |
| gwp_policepatrolbehavior_encyclopedia_003 | has entered the picket war state and is waiting for the end. | 已进入纠察队战争状态，正在等待收尾 | PolicePatrolBehavior.Encyclopedia.cs:43 |
| gwp_policepatrolbehavior_encyclopedia_004 | Provost enforcement: after the player refuses a lawful order while under warrant, the Grey Wardens declare war upon the player’s faction to compel arrest. Present stage: {VAR_1}; present standing: {VAR_2}. | 纠察队执法：玩家在负声望执法阶段拒绝配合后，灰袍守卫会对玩家所属势力宣战以强制缉拿。当前阶段：{VAR_1}；当前模组声望：{VAR_2}。 | PolicePatrolBehavior.Encyclopedia.cs:45 |
| gwp_policepatrolbehavior_helpers_001 | Through the Wardens' mediation, you are at peace with {VAR_1}. | 经灰袍调停，你与{VAR_1}恢复了和平。 | GwpPeace.cs:96 |
| gwp_policepatrolbehavior_helpers_002 | Through the Wardens' mediation, {VAR_1} no longer holds your crimes against you. | 经灰袍调停，{VAR_1}不再追究你的罪行。 | GwpPeace.cs:103 |
| gwp_policepatrolbehavior_helpers_003 | The return command has been issued, the number of candidates = {VAR_1}, returning = {VAR_2} | 已下发返程命令，候选数量={VAR_1}，returning={VAR_2} | PolicePatrolBehavior.Helpers.cs:60 |
| gwp_policepatrolbehavior_helpers_004 | The provost patrol reached {VAR_1}; disbanding: {VAR_2} | 纠察队已到达 {VAR_1}，执行销毁：{VAR_2} | PolicePatrolBehavior.Helpers.cs:92 |
| gwp_policepatrolbehavior_helpers_005 | The return destruction has been completed and the normal session has been restored. | 返程销毁已完成，恢复正常会话。 | PolicePatrolBehavior.Helpers.cs:178 |
| gwp_policeresourcemanager_001 | You lack the coin, so the Wardens take goods worth {VAR_1} denars. | 你的钱不够，灰袍没收了价值 {VAR_1} 第纳尔的物品。 | PoliceResourceManager.cs:463 |
| gwp_troop_archer | Grey Warden Archer | 灰袍守护者弓箭手 | _Module/ModuleData/spnpccharacters.xml |
| gwp_troop_heavy_infantry | Grey Warden Heavy Infantry | 灰袍守护者重步兵 | _Module/ModuleData/spnpccharacters.xml |
| gwp_troop_knight | Grey Warden Knight | 灰袍守护者骑士 | _Module/ModuleData/spnpccharacters.xml |
| gwp_troop_light_infantry | Grey Warden Light Infantry | 灰袍守护者轻步兵 | _Module/ModuleData/spnpccharacters.xml |
| gwp_troop_new_recruit | Grey Warden Recruit | 灰袍新兵 | _Module/ModuleData/spnpccharacters.xml |
| gwp_troop_req_high | — | 你在灰袍这里的名声已经够高。我可以调出弓手或披甲骑巡，但只能从我自己当前带领的部队中调。 | 未检出直接引用；保留 |
| gwp_troop_req_low | — | 你现在的名声只够调灰袍新兵，而且我只能把自己部队里现有的人交给你。 | 未检出直接引用；保留 |
| gwp_troop_req_mid | — | 你已证明自己值得托付。灰袍新兵之外，只要我自己的部队中数量足够，也可以调出重装步兵或执勤弓手。 | 未检出直接引用；保留 |
| gwp_sparring_challenge | Would you like to spar with me? I want to test my skill. | 愿意和我切磋一下吗？我想试试自己的水平。 | GreyWardenSparringBehavior.cs:107 |
| gwp_sparring_accept | Of course. Just a friendly match. | 当然。只是友好切磋，不伤和气。 | GreyWardenSparringBehavior.cs:117 |
| gwp_sparring_launch_failed | — | 这里不适合切磋，这次就先算了。 | 未检出直接引用；保留 |
| gwp_sparring_town_win | — | {OPPONENT}认输了，你赢得了这场切磋。 | 未检出直接引用；保留 |
| gwp_sparring_town_loss | — | 你输给了{OPPONENT}。 | 未检出直接引用；保留 |
| gwp_sparring_field_win | {OPPONENT} gives up. You won. | {OPPONENT}认输了，你赢了。 | GreyWardenFieldSparringMissionController.cs:841 |
| gwp_sparring_field_loss | You lost to {OPPONENT}. | 你输给了{OPPONENT}。 | GreyWardenFieldSparringMissionController.cs:845 |
| gwp_sparring_field_rule_loss | You did not dismount in time. You lose. | 你没有按时下马，这场算你输。 | GreyWardenFieldSparringMissionController.cs:838 |
| gwp_sparring_cannot_leave | — | 切磋还没有结束。 | 未检出直接引用；保留 |
| gwp_sparring_field_centre | How do you want to fight? | 你想怎么打？ | GreyWardenSparringBehavior.cs:127 |
| gwp_sparring_field_mounted | Let's fight on horseback. | 骑马打。 | GreyWardenSparringBehavior.cs:137 |
| gwp_sparring_field_foot | Let's fight on foot. | 步战。 | GreyWardenSparringBehavior.cs:147 |
| gwp_sparring_begin | The match has started. | 切磋开始。 | GreyWardenFieldSparringMissionController.cs:1622 |
| gwp_sparring_field_post_win | You fought well and earned that win. Keep it up. | 打得很好，你赢得很漂亮。继续保持。 | GreyWardenSparringBehavior.cs:157 |
| gwp_sparring_field_post_loss | Don't be discouraged. You fought well, and you'll be stronger next time. | 别灰心，你打得不错。下次再来，我相信你会更强。 | GreyWardenSparringBehavior.cs:187 |
| gwp_sparring_field_post_rule_violation | We agreed to fight on foot, but you refused to dismount. The loss is yours. | 说好步战，你却一直不肯下马。这场算你输。 | GreyWardenSparringBehavior.cs:167 |
| gwp_sparring_field_post_rule_reply | Understood. | 我明白了。 | GreyWardenSparringBehavior.cs:177 |
| gwp_sparring_field_post_reply | Thanks. Let's spar again sometime. | 谢谢。下次再切磋。 | GreyWardenSparringBehavior.cs:197 |
| gwp_sparring_talk | Talk | 交谈 | GreyWardenFieldSparringMissionController.cs:343 |
| gwp_gwpencyclopediaheropagevm_018 | — | 家族震慑：{VAR_1} | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_019 | — | 当前总震慑：{VAR_1} | 未检出直接引用；保留 |
| gwp_ai_det_audience_outsider_high_calculating | Whatever gain you imagine in a burning village, set it against years of pursuit before you decide. | 无论你以为烧毁村庄能得到多少，都先把此后多年的追捕算进去。 | GwpAiDeterrenceDialogueCatalog.cs:265 |
| gwp_ai_det_audience_outsider_high_cruel | Burn what you like, but understand that the Grey Wardens know how to make memory outlive pride. | 你想烧什么都随你，但要明白，灰袍有办法让记忆活得比傲慢更久。 | GwpAiDeterrenceDialogueCatalog.cs:259 |
| gwp_ai_det_audience_outsider_high_honorable | I tell you plainly: protect the villages under your power, or justice will eventually find you as well. | 我明白地告诫你：保护你权力之下的村庄，否则公正迟早也会找到你。 | GwpAiDeterrenceDialogueCatalog.cs:249 |
| gwp_ai_det_audience_outsider_high_merciful | Spare yourself this lesson by sparing the people who would otherwise suffer first. | 先放过那些最早受苦的人，你也就不必亲自承受这份教训。 | GwpAiDeterrenceDialogueCatalog.cs:257 |
| gwp_ai_det_audience_outsider_high_neutral | Stay away from that path unless you are prepared for the Grey Wardens to follow it back to you. | 除非你已经准备好让灰袍沿着这条路一直追到你面前，否则就离它远一点。 | GwpAiDeterrenceDialogueCatalog.cs:269 |
| gwp_ai_det_audience_outsider_high_valorous | If you want a worthy enemy, seek one who can fight back; the Grey Wardens will answer if you choose the helpless. | 如果你想找一个配得上的敌人，就找能够还手的人；若你选择弱者，灰袍自会回应。 | GwpAiDeterrenceDialogueCatalog.cs:253 |
| gwp_ai_det_audience_outsider_low_calculating | If profit is what guides you, remember to include the Grey Wardens in your reckoning. | 如果你只看重收益，记得把灰袍也算进你的账里。 | GwpAiDeterrenceDialogueCatalog.cs:221 |
| gwp_ai_det_audience_outsider_low_cruel | Do as you please, but do not say no one warned you what follows. | 你尽可以照自己的意思做，只是别说从来没人提醒过你后果。 | GwpAiDeterrenceDialogueCatalog.cs:215 |
| gwp_ai_det_audience_outsider_low_honorable | Take it as honest advice: do not bring the same judgment on yourself by preying on villages. | 这是一句诚实的劝告：不要靠伤害村庄，让同样的裁断落到自己头上。 | GwpAiDeterrenceDialogueCatalog.cs:205 |
| gwp_ai_det_audience_outsider_low_merciful | You can choose a better road before frightened families are made to pay for it. | 趁那些惊恐的家庭还没有为你付出代价，你仍然可以选择一条更好的路。 | GwpAiDeterrenceDialogueCatalog.cs:213 |
| gwp_ai_det_audience_outsider_low_neutral | You would be wise to remember that before choosing your own targets. | 在选择自己的目标以前，你最好记住这一点。 | GwpAiDeterrenceDialogueCatalog.cs:225 |
| gwp_ai_det_audience_outsider_low_valorous | Test your courage on armed warriors, not on villagers who cannot answer you. | 真想证明勇气，就去面对持有武器的战士，而不是无法反抗的村民。 | GwpAiDeterrenceDialogueCatalog.cs:209 |
| gwp_ai_det_audience_outsider_medium_calculating | If you intend to raid, understand that the cost may follow you long after the loot is spent. | 如果你准备劫掠，就要明白：赃物花光以后，那份代价可能仍会追着你。 | GwpAiDeterrenceDialogueCatalog.cs:243 |
| gwp_ai_det_audience_outsider_medium_cruel | Choose that road if you wish. Just be ready to pay more than you take. | 你愿意走那条路就去吧，只要准备好付出的比抢到的更多。 | GwpAiDeterrenceDialogueCatalog.cs:237 |
| gwp_ai_det_audience_outsider_medium_honorable | Keep faith with the innocent, and you will never need to learn that lesson as I did. | 守住对无辜者的本分，你就不必像我一样亲自学会这个道理。 | GwpAiDeterrenceDialogueCatalog.cs:227 |
| gwp_ai_det_audience_outsider_medium_merciful | Leave the villages in peace; their people have already endured enough from lords like us. | 让村庄安宁一些吧。那里的百姓已经受够了我们这些领主带来的苦难。 | GwpAiDeterrenceDialogueCatalog.cs:235 |
| gwp_ai_det_audience_outsider_medium_neutral | Consider the consequence before you make the same choice. | 在作出同样的选择之前，先想清楚后果。 | GwpAiDeterrenceDialogueCatalog.cs:247 |
| gwp_ai_det_audience_outsider_medium_valorous | Do not mistake cruelty toward the helpless for bravery, nor the Grey Wardens' patience for weakness. | 不要把欺凌弱者当作勇敢，也不要把灰袍的耐心当作软弱。 | GwpAiDeterrenceDialogueCatalog.cs:231 |
| gwp_ai_det_audience_warden_high_calculating | Because you work with them, you should understand this: fear spends quickly, but consistent law keeps paying. | 既然你与他们共事，就该明白：恐惧很快就会耗尽，始终如一的规矩才能维持得更久。 | GwpAiDeterrenceDialogueCatalog.cs:331 |
| gwp_ai_det_audience_warden_high_cruel | Their mark is on my house, and you have chosen their side. Do not expect me to confuse either with friendship. | 他们的印记留在我的家族身上，而你选择了站在他们一边。别指望我会把其中任何一样当成友谊。 | GwpAiDeterrenceDialogueCatalog.cs:325 |
| gwp_ai_det_audience_warden_high_honorable | You represent them now. If their justice is to mean anything, your conduct must be cleaner than the crimes you pursue. | 你现在代表着他们。若他们的公正真有意义，你的行为就必须比自己追捕的罪行更加干净。 | GwpAiDeterrenceDialogueCatalog.cs:315 |
| gwp_ai_det_audience_warden_high_merciful | Stand between the Grey Wardens and needless suffering when you can; justice has no need to become cruelty. | 在你做得到的时候，挡住灰袍与不必要的痛苦；公正没有必要变成残忍。 | GwpAiDeterrenceDialogueCatalog.cs:323 |
| gwp_ai_det_audience_warden_high_neutral | You chose to work with the Grey Wardens. Then you should know that what they do is remembered long after the warrant closes. | 你选择与灰袍共事，就应该知道：通缉令了结以后，他们做过的事仍会被人记很久。 | GwpAiDeterrenceDialogueCatalog.cs:335 |
| gwp_ai_det_audience_warden_high_valorous | Stand with them boldly if you must, but let your courage be measured by whom you protect, not whom you can frighten. | 要和他们站在一起，就堂堂正正地站；但衡量勇气的应是你保护了谁，而不是你能吓倒谁。 | GwpAiDeterrenceDialogueCatalog.cs:319 |
| gwp_ai_det_audience_warden_low_calculating | You chose to work with the Grey Wardens; that makes candour more useful than pretence. | 你既然选择替灰袍办事，与其彼此装糊涂，不如把话说明白。 | GwpAiDeterrenceDialogueCatalog.cs:287 |
| gwp_ai_det_audience_warden_low_cruel | So you chose to work with them. Do not expect that alone to make every lord bow. | 原来你选择了替他们办事。别以为仅凭这一点，所有领主都会低头。 | GwpAiDeterrenceDialogueCatalog.cs:281 |
| gwp_ai_det_audience_warden_low_honorable | Since you serve beside them, hold yourself to the same law they claimed to uphold when they intervened. | 既然你为他们效力，就应当遵守他们出手干预时所宣称维护的同一套规矩。 | GwpAiDeterrenceDialogueCatalog.cs:271 |
| gwp_ai_det_audience_warden_low_merciful | If you work with them, use that place to protect villagers before punishment becomes necessary. | 如果你与他们共事，就利用自己的位置保护村民，别等到只能事后惩罚。 | GwpAiDeterrenceDialogueCatalog.cs:279 |
| gwp_ai_det_audience_warden_low_neutral | You serve with the Grey Wardens, so you should understand why the matter is remembered. | 你在替灰袍做事，应该明白为什么这件事会被人记住。 | GwpAiDeterrenceDialogueCatalog.cs:291 |
| gwp_ai_det_audience_warden_low_valorous | You carry their authority now; prove that it rests on courage rather than intimidation. | 你现在也带着他们的权威；证明这份权威依靠的是勇气，而不是恫吓。 | GwpAiDeterrenceDialogueCatalog.cs:275 |
| gwp_ai_det_audience_warden_medium_calculating | A Grey Warden ally should know that measured enforcement wins more obedience than needless humiliation. | 与灰袍合作的人应该明白：有分寸的执法，比无谓的羞辱更容易换来长久的服从。 | GwpAiDeterrenceDialogueCatalog.cs:309 |
| gwp_ai_det_audience_warden_medium_cruel | You may hunt for them, but do not mistake a warrant for invincibility. | 你可以替他们追捕别人，但别把一纸通缉令误当成刀枪不入。 | GwpAiDeterrenceDialogueCatalog.cs:303 |
| gwp_ai_det_audience_warden_medium_honorable | If you act in their name, keep the law above vengeance, even when the accused deserves no sympathy. | 如果你以他们的名义行事，就该让规矩高于报复，即便被指控的人并不值得同情。 | GwpAiDeterrenceDialogueCatalog.cs:293 |
| gwp_ai_det_audience_warden_medium_merciful | Use your place among them to prevent the next village from suffering, not merely to punish afterward. | 利用你在他们之中的位置，阻止下一个村庄受害，而不是只在事后惩罚罪人。 | GwpAiDeterrenceDialogueCatalog.cs:301 |
| gwp_ai_det_audience_warden_medium_neutral | You know their methods from the other side of the warrant; remember what those methods leave behind. | 你如今站在通缉令的另一边，应该记得这些手段最终会在人身上留下什么。 | GwpAiDeterrenceDialogueCatalog.cs:313 |
| gwp_ai_det_audience_warden_medium_valorous | If you bear their authority, meet armed offenders openly and leave terror to lesser people. | 既然承担他们的权威，就该堂堂正正面对持械的犯人，把恐吓留给更卑劣的人。 | GwpAiDeterrenceDialogueCatalog.cs:297 |
| gwp_ai_det_core_family_high_calculating | No one in my clan now discusses a village raid without first accounting for the Grey Wardens' answer. | 现在，家族里没有人会在谈论劫掠村庄时，不先计算灰袍将会作出的回应。 | GwpAiDeterrenceDialogueCatalog.cs:194 |
| gwp_ai_det_core_family_high_cruel | Their hand still lies heavy on my clan. We have not submitted; we have merely learned when vengeance is too costly. | 他们的手至今仍沉重地压在我的家族身上。我们没有屈服，只是学会了何时报复的代价太高。 | GwpAiDeterrenceDialogueCatalog.cs:188 |
| gwp_ai_det_core_family_high_honorable | The lesson reached the whole clan: rank and blood do not excuse a crime against those under our protection. | 整个家族都已经明白：身份和血统不能成为伤害受我们保护之人的借口。 | GwpAiDeterrenceDialogueCatalog.cs:178 |
| gwp_ai_det_core_family_high_merciful | If my kin finally understand the suffering they caused, then perhaps something decent can still come from the punishment. | 如果我的族人终于明白自己造成了多少痛苦，那么这场惩罚至少还能留下些善果。 | GwpAiDeterrenceDialogueCatalog.cs:186 |
| gwp_ai_det_core_family_high_neutral | The consequences settled over the entire clan. None of us now treats the Grey Wardens as a distant concern. | 那场后果笼罩了整个家族。如今没有人还会把灰袍当成与自己无关的遥远传闻。 | GwpAiDeterrenceDialogueCatalog.cs:198 |
| gwp_ai_det_core_family_high_valorous | My house still has courage, but courage now walks beside a very clear memory of the Grey Wardens. | 我的家族仍有勇气，只是这份勇气如今总会与对灰袍的清晰记忆同行。 | GwpAiDeterrenceDialogueCatalog.cs:182 |
| gwp_ai_det_core_family_low_calculating | What happened to my kin gave the clan a useful measure of the risks involved. | 发生在族人身上的事，让家族对其中的风险有了一个有用的估量。 | GwpAiDeterrenceDialogueCatalog.cs:150 |
| gwp_ai_det_core_family_low_cruel | They laid hands on my blood. I remember it, even while I let the matter rest. | 他们动了我的血亲。即便我暂时不再追究，也不会忘记。 | GwpAiDeterrenceDialogueCatalog.cs:144 |
| gwp_ai_det_core_family_low_honorable | My kin crossed a boundary. I cannot call the Grey Wardens wrong for answering it. | 我的族人的确越过了界。我不能说灰袍出手回应是错的。 | GwpAiDeterrenceDialogueCatalog.cs:134 |
| gwp_ai_det_core_family_low_merciful | I am more troubled by the villagers who suffered than by my kin's wounded pride. | 比起族人受伤的颜面，我更在意那些遭受伤害的村民。 | GwpAiDeterrenceDialogueCatalog.cs:142 |
| gwp_ai_det_core_family_low_neutral | The affair touched my family, so naturally the clan remembers it. | 那件事牵连了我的家人，家族自然会记得。 | GwpAiDeterrenceDialogueCatalog.cs:154 |
| gwp_ai_det_core_family_low_valorous | My clan took a blow; it did not lose its courage. Still, the warning was clear. | 我的家族受过打击，却没有失去勇气。不过，那次警告已经足够清楚。 | GwpAiDeterrenceDialogueCatalog.cs:138 |
| gwp_ai_det_core_family_medium_calculating | The clan has counted the men, coin, and freedom that one reckless choice cost us. | 一个鲁莽的选择让我们损失了多少士兵、钱财和自由，家族已经全部算清了。 | GwpAiDeterrenceDialogueCatalog.cs:172 |
| gwp_ai_det_core_family_medium_cruel | The Grey Wardens made my clan bleed for another's mistake. Such accounts are not forgotten. | 灰袍让我的家族为别人的错误流了血。这种账不会被轻易忘记。 | GwpAiDeterrenceDialogueCatalog.cs:166 |
| gwp_ai_det_core_family_medium_honorable | My kinsman broke the peace and the clan paid for it. Responsibility does not vanish merely because blood is shared. | 我的族人破坏了安宁，家族也为此付出了代价。血缘并不会让责任凭空消失。 | GwpAiDeterrenceDialogueCatalog.cs:156 |
| gwp_ai_det_core_family_medium_merciful | I would rather my family learn restraint than leave more peasants grieving for what nobles chose to do. | 我宁愿家人学会克制，也不愿再让农民为贵族的选择失去亲人。 | GwpAiDeterrenceDialogueCatalog.cs:164 |
| gwp_ai_det_core_family_medium_neutral | What happened to one of us changed how the whole clan speaks of raids and reprisals. | 发生在一个族人身上的事，已经改变了整个家族谈论劫掠与报复的方式。 | GwpAiDeterrenceDialogueCatalog.cs:176 |
| gwp_ai_det_core_family_medium_valorous | We are not cowed, but neither will we charge blindly into the same punishment twice. | 我们并没有被吓倒，但也不会再盲目冲进同一种惩罚之中。 | GwpAiDeterrenceDialogueCatalog.cs:160 |
| gwp_ai_det_core_personal_high_calculating | Now every road toward a village includes the same figure in my reckoning: what the Grey Wardens will take in return. | 现在，每一条通往村庄的路都要算上同一笔账：灰袍会从我这里取走什么作为代价。 | GwpAiDeterrenceDialogueCatalog.cs:128 |
| gwp_ai_det_core_personal_high_cruel | I remember exactly what they did. My restraint is calculation, not surrender and certainly not forgiveness. | 他们做过什么，我记得一清二楚。我的克制只是算计，不是屈服，更不是原谅。 | GwpAiDeterrenceDialogueCatalog.cs:122 |
| gwp_ai_det_core_personal_high_honorable | I was guilty, and the punishment was deserved. The depth of the lesson does not make the judgment less just. | 我确实有罪，所受的惩罚也并不过分。教训再深，也不会让那次裁断变得不公。 | GwpAiDeterrenceDialogueCatalog.cs:112 |
| gwp_ai_det_core_personal_high_merciful | What weighs on me most is not what they did to me, but what my choices had already done to people who could not resist. | 真正压在我心上的，不是他们对我做了什么，而是我的选择早已伤害了那些无力反抗的人。 | GwpAiDeterrenceDialogueCatalog.cs:120 |
| gwp_ai_det_core_personal_high_neutral | Their intervention changed how I judge every raid, every captive, and every road back home. | 那次处置改变了我看待每次劫掠、每个俘虏，以及每一条归路的方式。 | GwpAiDeterrenceDialogueCatalog.cs:132 |
| gwp_ai_det_core_personal_high_valorous | I could face them again. That does not mean I am foolish enough to call them harmless. | 我仍然敢再面对他们，但这不代表我会愚蠢到把他们当成无害之人。 | GwpAiDeterrenceDialogueCatalog.cs:116 |
| gwp_ai_det_core_personal_low_calculating | That affair taught me where the cost begins to outweigh the gain. | 那件事让我明白，什么时候代价会开始超过收益。 | GwpAiDeterrenceDialogueCatalog.cs:84 |
| gwp_ai_det_core_personal_low_cruel | They made their point. I have simply chosen not to test it again yet. | 他们已经表明了态度。我只不过暂时不打算再试一次。 | GwpAiDeterrenceDialogueCatalog.cs:78 |
| gwp_ai_det_core_personal_low_honorable | I crossed a boundary, and being stopped was the proper consequence. | 我确实越过了界，被人阻止也是应有的结果。 | GwpAiDeterrenceDialogueCatalog.cs:68 |
| gwp_ai_det_core_personal_low_merciful | Whatever my reasons, villagers should not have borne the price. | 不管我当时有什么理由，都不该让村民承担代价。 | GwpAiDeterrenceDialogueCatalog.cs:76 |
| gwp_ai_det_core_personal_low_neutral | The matter is over, but I have not discarded its lesson. | 那件事已经过去，但我并没有丢掉它留下的教训。 | GwpAiDeterrenceDialogueCatalog.cs:88 |
| gwp_ai_det_core_personal_low_valorous | One defeat does not make me afraid, but it taught me to respect the Grey Wardens' reach. | 一次失败不会让我害怕，但它让我知道不能轻视灰袍的能耐。 | GwpAiDeterrenceDialogueCatalog.cs:72 |
| gwp_ai_det_core_personal_medium_calculating | I have measured the losses twice. No village is worth drawing that pursuit without a far greater purpose. | 损失我已经算过两遍。若没有更大的目的，没有哪个村庄值得招来那样的追捕。 | GwpAiDeterrenceDialogueCatalog.cs:106 |
| gwp_ai_det_core_personal_medium_cruel | They hurt me enough to make patience useful. Do not confuse that with remorse. | 他们下手够重，让耐心变得有用。可别把这当成悔意。 | GwpAiDeterrenceDialogueCatalog.cs:100 |
| gwp_ai_det_core_personal_medium_honorable | I broke the peace and answered for it. Denying that would only add cowardice to the fault. | 我破坏了安宁，也为此承担了后果。否认这一点，只会在过错之外再添一分怯懦。 | GwpAiDeterrenceDialogueCatalog.cs:90 |
| gwp_ai_det_core_personal_medium_merciful | I remember the frightened villagers more clearly than the chains. That is reason enough to stop. | 相比锁链，我记得更清楚的是那些受惊的村民。仅凭这一点，也足够让我停手。 | GwpAiDeterrenceDialogueCatalog.cs:98 |
| gwp_ai_det_core_personal_medium_neutral | Since that capture, every raid carries a second calculation: whether the Grey Wardens will answer. | 自从那次被捕以后，每次劫掠都多了一层盘算：灰袍会不会赶来。 | GwpAiDeterrenceDialogueCatalog.cs:110 |
| gwp_ai_det_core_personal_medium_valorous | I do not fear a hard fight, but only a fool mistakes courage for blindness to consequence. | 我不怕艰难的战斗，但只有蠢人才会把勇气当成不计后果。 | GwpAiDeterrenceDialogueCatalog.cs:94 |
| gwp_ai_det_intro_family_high | {HERO_NAME}'s expression hardens for a moment. The Grey Wardens' actions still hang heavily over their clan. | {HERO_NAME}的神色一度变得冷硬。灰袍的行动所造成的后果，至今仍沉重地压在其家族身上。 | GwpAiDeterrenceDialogueCatalog.cs:52 |
| gwp_ai_det_intro_family_low | {HERO_NAME} pauses briefly before speaking. The Grey Wardens' dealings with their kin have not quite been forgotten. | {HERO_NAME}开口前短暂停顿了一下。灰袍与其族人之间发生的事，显然还没有被完全忘记。 | GwpAiDeterrenceDialogueCatalog.cs:48 |
| gwp_ai_det_intro_family_medium | {HERO_NAME} chooses each word carefully. Their clan's encounter with the Grey Wardens remains a serious concern. | {HERO_NAME}谨慎地选择着措辞。家族与灰袍的那次冲突，至今仍是一件不能轻视的事。 | GwpAiDeterrenceDialogueCatalog.cs:50 |
| gwp_ai_det_intro_personal_high | {HERO_NAME} holds your gaze with deliberate composure. What the Grey Wardens did to them left a mark too deep to conceal. | {HERO_NAME}刻意保持镇定，直视着你。灰袍曾在其身上留下的影响，已经深得难以掩饰。 | GwpAiDeterrenceDialogueCatalog.cs:46 |
| gwp_ai_det_intro_personal_low | {HERO_NAME} regards you steadily. Their encounter with the Grey Wardens is remembered, but it does not rule their manner. | {HERO_NAME}平静地看着你。与灰袍的那次交锋仍被其记在心里，却还没有左右其举止。 | GwpAiDeterrenceDialogueCatalog.cs:42 |
| gwp_ai_det_intro_personal_medium | {HERO_NAME}'s voice grows more measured. Their own encounter with the Grey Wardens still weighs on every word. | {HERO_NAME}的语气变得更加慎重。亲自面对灰袍的经历，至今仍影响着其说出的每一句话。 | GwpAiDeterrenceDialogueCatalog.cs:44 |
| gwp_ai_det_audience_outsider_high_cautious | If self-preservation matters to you, do not give them a reason to learn your name. | 如果你还在乎保全自己，就不要给他们记住你名字的理由。 | GwpAiDeterrenceDialogueCatalog.cs:255 |
| gwp_ai_det_audience_outsider_high_closefisted | Even the most selfish arithmetic reaches the same answer: a raid is not worth what they collect afterward. | 即便按照最自私的算法，答案也一样：一次劫掠不值得灰袍事后收取的代价。 | GwpAiDeterrenceDialogueCatalog.cs:263 |
| gwp_ai_det_audience_outsider_high_dishonorable | Choose carefully which laws you break. The Grey Wardens care more about the deed than the excuse. | 仔细选择你要破坏哪些规矩。灰袍更在意你做了什么，而不是你准备了什么借口。 | GwpAiDeterrenceDialogueCatalog.cs:251 |
| gwp_ai_det_audience_outsider_high_generous | Think of your clan and followers before you make them inherit the price of your decision. | 在让家族和追随者继承你决定的代价以前，先替他们想一想。 | GwpAiDeterrenceDialogueCatalog.cs:261 |
| gwp_ai_det_audience_outsider_high_impulsive | If your temper commands you, the Grey Wardens may be the ones who decide when it stops. | 如果让脾气指挥你，最后可能由灰袍决定它何时停止。 | GwpAiDeterrenceDialogueCatalog.cs:267 |
| gwp_ai_det_audience_outsider_low_cautious | You need not prove anything by provoking people who will pursue you without tiring. | 你没有必要通过招惹一群会不知疲倦地追捕你的人来证明什么。 | GwpAiDeterrenceDialogueCatalog.cs:211 |
| gwp_ai_det_audience_outsider_low_closefisted | If conscience does not move you, then protect your own purse and soldiers. | 如果良心不能打动你，那就至少保护好自己的钱袋和士兵。 | GwpAiDeterrenceDialogueCatalog.cs:219 |
| gwp_ai_det_audience_outsider_low_dishonorable | Keep your promises if it suits you, but at least do not make yourself such easy prey. | 守不守承诺由你，但至少不要让自己成为如此容易对付的猎物。 | GwpAiDeterrenceDialogueCatalog.cs:207 |
| gwp_ai_det_audience_outsider_low_generous | Do not make your followers and family pay for a choice made only to satisfy you. | 不要让追随者和家人为一个只用来满足你自己的选择付出代价。 | GwpAiDeterrenceDialogueCatalog.cs:217 |
| gwp_ai_det_audience_outsider_low_impulsive | Do not let one angry moment choose a road that takes years to escape. | 不要让一时的怒火替你选择一条需要多年才能逃离的道路。 | GwpAiDeterrenceDialogueCatalog.cs:223 |
| gwp_ai_det_audience_outsider_medium_cautious | Turn away from the villages while the choice is still yours; retreat is cheaper before pursuit begins. | 趁选择权还在自己手里，离那些村庄远一点。追捕开始以前退让，代价要小得多。 | GwpAiDeterrenceDialogueCatalog.cs:233 |
| gwp_ai_det_audience_outsider_medium_closefisted | The loot from a village will not repay the men, coin, and freedom that pursuit can cost. | 一个村庄的战利品，补不回追捕可能夺走的士兵、钱财和自由。 | GwpAiDeterrenceDialogueCatalog.cs:241 |
| gwp_ai_det_audience_outsider_medium_dishonorable | If you cross that line, do not imagine that rank or a convenient excuse will shield you. | 如果你越过那条界线，别以为身份或一个方便的借口能够保护你。 | GwpAiDeterrenceDialogueCatalog.cs:229 |
| gwp_ai_det_audience_outsider_medium_generous | A lord owes protection to those who trust them, not a trail of losses created for pride. | 领主欠信任自己的人一份保护，而不是为了骄傲留下一路损失。 | GwpAiDeterrenceDialogueCatalog.cs:239 |
| gwp_ai_det_audience_outsider_medium_impulsive | Pause before you order the torches lit. Anger leaves faster than consequences. | 下令点燃火把以前先停一下。怒火消失的速度，总比后果更快。 | GwpAiDeterrenceDialogueCatalog.cs:245 |
| gwp_ai_det_audience_warden_high_cautious | If the Grey Wardens send you after dangerous people, remember that caution keeps an enforcer alive. | 如果灰袍派你追捕危险的人，记住，谨慎才能让执法者活下来。 | GwpAiDeterrenceDialogueCatalog.cs:321 |
| gwp_ai_det_audience_warden_high_closefisted | If you serve only for profit, understand that uncontrolled enforcement eventually costs more than it pays. | 如果你只为收益而效力，就要明白：不受约束的执法，最终造成的损失会超过报酬。 | GwpAiDeterrenceDialogueCatalog.cs:329 |
| gwp_ai_det_audience_warden_high_dishonorable | If you call what you do justice, prove it through consistent conduct rather than the power to punish. | 如果你把自己的行为称为公正，就用始终如一的行为来证明，而不是依靠惩罚别人的权力。 | GwpAiDeterrenceDialogueCatalog.cs:317 |
| gwp_ai_det_audience_warden_high_generous | Their authority makes you responsible for everyone placed in danger by your decisions. | 他们给你的权威，也让你必须为所有因你的决定而陷入危险的人负责。 | GwpAiDeterrenceDialogueCatalog.cs:327 |
| gwp_ai_det_audience_warden_high_impulsive | If temper governs your sword, their authority will not stop you from becoming another danger to the innocent. | 如果脾气支配了你的剑，他们的权威也无法阻止你变成无辜者面对的另一种危险。 | GwpAiDeterrenceDialogueCatalog.cs:333 |
| gwp_ai_det_audience_warden_low_cautious | Authority can make a fool reckless. Do not let their name push you into a fight you have not measured. | 权威会让蠢人变得鲁莽。别让他们的名号把你推入一场尚未衡量过的战斗。 | GwpAiDeterrenceDialogueCatalog.cs:277 |
| gwp_ai_det_audience_warden_low_closefisted | If a bounty pays you, do not pretend that coin and justice are the same thing. | 如果悬赏会给你钱，就不要假装钱财与公正是同一回事。 | GwpAiDeterrenceDialogueCatalog.cs:285 |
| gwp_ai_det_audience_warden_low_dishonorable | You carry their warrants, but ink alone does not turn power into virtue. | 你替他们拿着通缉令，但纸上的墨迹并不能自动把权力变成美德。 | GwpAiDeterrenceDialogueCatalog.cs:273 |
| gwp_ai_det_audience_warden_low_generous | If you serve them, remember the people who follow you and will pay for every order. | 既然为他们效力，就记住那些追随你、并会为你的每一道命令付出代价的人。 | GwpAiDeterrenceDialogueCatalog.cs:283 |
| gwp_ai_det_audience_warden_low_impulsive | A warrant does not excuse acting in anger. You are still answerable for what follows. | 通缉令不能为愤怒中的行动开脱。接下来发生的事，仍然要由你负责。 | GwpAiDeterrenceDialogueCatalog.cs:289 |
| gwp_ai_det_audience_warden_medium_cautious | A warrant is no substitute for knowing when to advance and when to withdraw. | 一纸通缉令，代替不了判断何时前进、何时退让的能力。 | GwpAiDeterrenceDialogueCatalog.cs:299 |
| gwp_ai_det_audience_warden_medium_closefisted | Do not spend other people's lives merely to enlarge your reward. | 不要仅仅为了增加自己的赏金，就拿别人的性命作为花费。 | GwpAiDeterrenceDialogueCatalog.cs:307 |
| gwp_ai_det_audience_warden_medium_dishonorable | Use their authority if you must, but do not preach purity while bending the law for yourself. | 你可以使用他们的权威，但不要一边为自己歪曲规矩，一边向别人宣扬纯洁。 | GwpAiDeterrenceDialogueCatalog.cs:295 |
| gwp_ai_det_audience_warden_medium_generous | Protect your companions and the villagers alike; authority is a debt before it is a privilege. | 保护好你的同伴，也保护好村民。权威在成为特权以前，首先是一份债务。 | GwpAiDeterrenceDialogueCatalog.cs:305 |
| gwp_ai_det_audience_warden_medium_impulsive | Do not let an insult turn enforcement into vengeance simply because their seal is behind you. | 不要因为身后有他们的印记，就让一次冒犯把执法变成报复。 | GwpAiDeterrenceDialogueCatalog.cs:311 |
| gwp_ai_det_core_family_high_cautious | We know how completely they can close around a clan. Avoiding that danger again is survival, not submission. | 我们知道他们能够怎样彻底包围一个家族。避免再次陷入那种危险，是为了生存，不是屈服。 | GwpAiDeterrenceDialogueCatalog.cs:184 |
| gwp_ai_det_core_family_high_closefisted | The Grey Wardens taught us that one relative's folly can become everyone's expense. I will not permit that twice. | 灰袍让我们明白，一个亲族的愚行可能变成所有人的损失。我不会允许这种事发生第二次。 | GwpAiDeterrenceDialogueCatalog.cs:192 |
| gwp_ai_det_core_family_high_dishonorable | My clan remembers the punishment as an insult, not a judgment. Restraint does not mean that we accept their right. | 我的家族把那场惩罚视为羞辱，而不是裁断。我们有所克制，并不代表承认他们拥有这种权力。 | GwpAiDeterrenceDialogueCatalog.cs:180 |
| gwp_ai_det_core_family_high_generous | The whole clan carried the burden. I owe protection to those who kept faith when another brought ruin home. | 整个家族都承担了那份重担。当有人把灾祸带回家时，我有责任保护那些仍然忠于家族的人。 | GwpAiDeterrenceDialogueCatalog.cs:190 |
| gwp_ai_det_core_family_high_impulsive | The whole clan felt the consequence of one unchecked impulse. Now even anger must answer to that memory. | 整个家族都承受了一个失控冲动造成的后果。如今就连愤怒，也必须先面对这段记忆。 | GwpAiDeterrenceDialogueCatalog.cs:196 |
| gwp_ai_det_core_family_low_cautious | What happened to my kin showed us a danger the clan would be foolish to provoke without need. | 发生在族人身上的事让我们看清了一种危险。若无必要，家族主动招惹它才是愚蠢。 | GwpAiDeterrenceDialogueCatalog.cs:140 |
| gwp_ai_det_core_family_low_closefisted | My kinsman's folly became a bill for the whole clan. They should have borne it alone. | 族人的愚行变成了整个家族的账单。那原本应该由他自己承担。 | GwpAiDeterrenceDialogueCatalog.cs:148 |
| gwp_ai_det_core_family_low_dishonorable | Blood comes before strangers' rules. I judge the Grey Wardens by what they did to my kin, not by the charge they named. | 血亲比外人的规矩更重要。我评判灰袍，只看他们对我的族人做了什么，而不是他们给出的罪名。 | GwpAiDeterrenceDialogueCatalog.cs:136 |
| gwp_ai_det_core_family_low_generous | Whatever my kinsman's fault, they are still ours. We must correct them without abandoning them. | 无论族人犯了什么错，他仍是我们的人。我们必须纠正他，却不能抛弃他。 | GwpAiDeterrenceDialogueCatalog.cs:146 |
| gwp_ai_det_core_family_low_impulsive | My kin acted before thinking, and the rest of us were left to remember the result. | 我的族人还没有想清楚就采取了行动，结果却留给我们所有人铭记。 | GwpAiDeterrenceDialogueCatalog.cs:152 |
| gwp_ai_det_core_family_medium_cautious | The clan has no desire to walk into the same trap merely to prove that it is unafraid. | 家族没有兴趣仅仅为了证明自己不怕，就再次走进同一个陷阱。 | GwpAiDeterrenceDialogueCatalog.cs:162 |
| gwp_ai_det_core_family_medium_closefisted | I have no intention of paying again for a relative's appetite for plunder. | 我不打算再为某个亲族对劫掠的贪欲付账。 | GwpAiDeterrenceDialogueCatalog.cs:170 |
| gwp_ai_det_core_family_medium_dishonorable | Whatever my kinsman did, outsiders had no right to humble our house and call the matter justice. | 无论我的族人做过什么，外人都没有权力羞辱我们的家族，再把这称为公正。 | GwpAiDeterrenceDialogueCatalog.cs:158 |
| gwp_ai_det_core_family_medium_generous | One kinsman's mistake cost everyone who stood beside them. Our duty now is to repair what it damaged. | 一个族人的错误让所有站在他身边的人付出了代价。我们现在的责任，是修补它造成的损害。 | GwpAiDeterrenceDialogueCatalog.cs:168 |
| gwp_ai_det_core_family_medium_impulsive | One hotheaded decision brought the Grey Wardens down on all of us. That is not easily forgotten. | 一个头脑发热的决定把灰袍引到了我们所有人面前。这种事情不会轻易被忘记。 | GwpAiDeterrenceDialogueCatalog.cs:174 |
| gwp_ai_det_core_personal_high_cautious | I survived by learning which fights to avoid. Giving them another reason to pursue me would be needless folly. | 我靠着学会避开某些战斗才活了下来。再给他们一个追捕我的理由，只是毫无必要的愚蠢。 | GwpAiDeterrenceDialogueCatalog.cs:118 |
| gwp_ai_det_core_personal_high_closefisted | I will not impoverish myself for plunder that the Grey Wardens can take back with interest. | 我不会为了终将被灰袍连本带利夺回的战利品，把自己耗得一无所有。 | GwpAiDeterrenceDialogueCatalog.cs:126 |
| gwp_ai_det_core_personal_high_dishonorable | I regret being caught, not crossing their line. What changed is that I now hide my steps more carefully. | 我后悔的是被抓，而不是越过他们划下的界线。真正改变的是，我现在会把脚步藏得更好。 | GwpAiDeterrenceDialogueCatalog.cs:114 |
| gwp_ai_det_core_personal_high_generous | I cannot ask my household to endure that cost again merely to satisfy one more raid. | 我不能只为满足又一次劫掠，就要求家臣和亲族再次承受那样的代价。 | GwpAiDeterrenceDialogueCatalog.cs:124 |
| gwp_ai_det_core_personal_high_impulsive | Whenever my temper rises, the memory of that punishment drags it down before I act. | 每当怒火升起，那次惩罚的记忆都会在我行动以前把它压下去。 | GwpAiDeterrenceDialogueCatalog.cs:130 |
| gwp_ai_det_core_personal_low_cautious | I have no wish to face the Grey Wardens again over something as small as a village raid. | 我不想为了区区一次劫掠村庄，再去面对灰袍。 | GwpAiDeterrenceDialogueCatalog.cs:74 |
| gwp_ai_det_core_personal_low_closefisted | I lost more than I gained. I will not waste my own men and coin that way again. | 我的损失超过了收获。我不会再这样浪费自己的士兵和钱财。 | GwpAiDeterrenceDialogueCatalog.cs:82 |
| gwp_ai_det_core_personal_low_dishonorable | Rules are useful until they obstruct necessity. Being caught only proved that I chose the wrong moment. | 规矩只有在不妨碍必要之事时才有用。被抓只能证明我选错了时机。 | GwpAiDeterrenceDialogueCatalog.cs:70 |
| gwp_ai_det_core_personal_low_generous | My followers paid for my choice as surely as I did. I owe them better judgment next time. | 追随我的人和我一样为那个选择付出了代价。下一次，我有责任作出更好的判断。 | GwpAiDeterrenceDialogueCatalog.cs:80 |
| gwp_ai_det_core_personal_low_impulsive | I acted in anger and paid for it before the anger had even cooled. | 我一时愤怒便采取了行动，怒气尚未消退，代价就已经找上了我。 | GwpAiDeterrenceDialogueCatalog.cs:86 |
| gwp_ai_det_core_personal_medium_cautious | Call it caution if you like. I know how quickly they can close every road around a person. | 你尽可以把这叫作谨慎。我知道他们封死一个人身边所有道路的速度有多快。 | GwpAiDeterrenceDialogueCatalog.cs:96 |
| gwp_ai_det_core_personal_medium_closefisted | A burned village is not worth my soldiers, my coin, and my freedom all at once. | 烧毁一个村庄，不值得让我同时赔上士兵、钱财和自由。 | GwpAiDeterrenceDialogueCatalog.cs:104 |
| gwp_ai_det_core_personal_medium_dishonorable | I will not pretend their law was sacred. I learned only that breaking it openly gives enemies a weapon. | 我不会假装他们的规矩不可冒犯。我只学会了一件事：公然破坏规矩，就是把武器递给敌人。 | GwpAiDeterrenceDialogueCatalog.cs:92 |
| gwp_ai_det_core_personal_medium_generous | The people who followed me bore the losses of my decision. Loyalty requires that I not spend them so carelessly again. | 追随我的人承担了那个决定造成的损失。忠义要求我不能再如此草率地牺牲他们。 | GwpAiDeterrenceDialogueCatalog.cs:102 |
| gwp_ai_det_core_personal_medium_impulsive | I let anger choose the road, and the Grey Wardens decided where that road ended. | 我让怒火替我选了道路，而灰袍决定了这条路在哪里结束。 | GwpAiDeterrenceDialogueCatalog.cs:108 |
| gwp_gwpencyclopediaclanpagevm_004 | Grey Warden affairs | 灰袍事务 | GwpAffairsNavigation.cs:52, GwpEncyclopediaClanPageVM.cs:30 |
| gwp_gwpencyclopediaclanpagevm_005 | Standing wars and their grounds, the case and duty pool, the judicial treasury, your standing, and the family roll. | 查看对外战争与开战依据、案件与差事总池、司法公库、你的声望，以及家族名册。 | GwpEncyclopediaClanPageVM.cs:32 |
| gwp_affairs_nav_need_membership | Only sworn hunters of the Grey Wardens can open their affairs. | 只有宣誓加入灰袍的悬赏猎人才能查看灰袍事务。 | GwpAffairsNavigation.cs:47 |
| gwp_gwpcasearchivescreen_001 | Grey Warden affairs | 灰袍事务 | GwpCaseArchiveScreen.cs:101 |
| gwp_gwpcasearchivescreen_002 | There are currently no tasks in the pool. | 当前任务池为空。 | GwpCaseArchiveScreen.cs:102 |
| gwp_gwpcasearchivescreen_003 | Refresh ledger | 刷新案卷 | GwpCaseArchiveScreen.cs:103 |
| gwp_gwpcasearchivescreen_004 | Close | 关闭 | GwpCaseArchiveScreen.cs:104 |
| gwp_gwpcasearchivescreen_005 | Open files: {VAR_1} criminal cases, {VAR_2} other matters &#124; In hand: {VAR_3} &#124; Unassigned: {VAR_4} | 在册卷宗：罪案 {VAR_1} 宗，其他事务 {VAR_2} 宗 &#124; 已有人办：{VAR_3} &#124; 尚无人办：{VAR_4} | GwpCaseArchiveScreen.cs:346 |
| gwp_gwpcasearchivescreen_006 | OPEN | 未结案 | GwpCaseArchiveScreen.cs:471 |
| gwp_gwpcasearchivescreen_008 | {VAR_1} — {VAR_2} | {VAR_1} — {VAR_2} | GwpCaseArchiveScreen.cs:473 |
| gwp_gwpcasearchivescreen_009 | Last offence: {VAR_1} &#124; Case opened: {VAR_2} | 最近案发：{VAR_1} &#124; 立案时间：{VAR_2} | GwpCaseArchiveScreen.cs:478 |
| gwp_gwpcasearchivescreen_010 | Unrecorded | 未记录 | GwpCaseArchiveScreen.cs:485, GwpCaseArchiveScreen.cs:522 |
| gwp_gwpcasearchivescreen_011 | Unrecorded | 未记录 | GwpCaseArchiveScreen.cs:488 |
| gwp_gwpcasearchivescreen_012 | Offence: {VAR_1} &#124; Victim: {VAR_2} &#124; Location: {VAR_3} | 罪行：{VAR_1} &#124; 受害者：{VAR_2} &#124; 地点：{VAR_3} | GwpCaseArchiveScreen.cs:491 |
| gwp_gwpcasearchivescreen_013 | Tracking: unassigned | 追踪情况：无人承办 | GwpCaseArchiveScreen.cs:793 |
| gwp_gwpcasearchivescreen_015 | {VAR_1} ({VAR_2}) | {VAR_1}（{VAR_2}） | GwpCaseArchiveScreen.cs:802 |
| gwp_gwpcasearchivescreen_016 | Tracking: {VAR_1} &#124; Stage: {VAR_2} | 追踪情况：{VAR_1} &#124; 当前阶段：{VAR_2} | GwpCaseArchiveScreen.cs:805 |
| gwp_gwpcasearchivescreen_017 | Player | 玩家 | GwpCaseArchiveScreen.cs:774, GwpCaseArchiveScreen.cs:813 |
| gwp_gwpcasearchivescreen_018 | supporting the commissioned player | 支援受托的玩家 | GwpCaseArchiveScreen.cs:824 |
| gwp_gwpcasearchivescreen_019 | escort after arrest | 抓捕后护送 | GwpCaseArchiveScreen.cs:826 |
| gwp_gwpcasearchivescreen_020 | preparing dispatch | 准备出动 | GwpCaseArchiveScreen.cs:828 |
| gwp_gwpcasearchivescreen_021 | wartime pursuit | 战时追捕 | GwpCaseArchiveScreen.cs:830 |
| gwp_gwpcasearchivescreen_022 | tracking target | 追踪目标 | GwpCaseArchiveScreen.cs:831 |
| gwp_gwpcasearchivescreen_023 | Spring | 春季 | GwpCaseArchiveScreen.cs:914 |
| gwp_gwpcasearchivescreen_024 | Summer | 夏季 | GwpCaseArchiveScreen.cs:915 |
| gwp_gwpcasearchivescreen_025 | Autumn | 秋季 | GwpCaseArchiveScreen.cs:916 |
| gwp_gwpcasearchivescreen_026 | Winter | 冬季 | GwpCaseArchiveScreen.cs:917 |
| gwp_gwpcasearchivescreen_027 | Unknown season | 未知季节 | GwpCaseArchiveScreen.cs:918 |
| gwp_gwpcasearchivescreen_028 | Year {VAR_1}, {VAR_2}, day {VAR_3}, {VAR_4}:00 | {VAR_1}年{VAR_2}第{VAR_3}日 {VAR_4}:00 | GwpCaseArchiveScreen.cs:922 |
| gwp_gwpcasearchivescreen_029 | near {VAR_1} ({VAR_2}, {VAR_3}) | {VAR_1}附近（{VAR_2}, {VAR_3}） | GwpCaseArchiveScreen.cs:935 |
| gwp_gwpcasearchivescreen_030 | wilderness ({VAR_1}, {VAR_2}) | 荒野（{VAR_1}, {VAR_2}） | GwpCaseArchiveScreen.cs:942 |
| gwp_gwpcasearchivescreen_031 | Unknown town | 未知城镇 | GwpCaseArchiveScreen.cs:936 |
| gwp_gwpcasearchivescreen_032 | Assistance — {VAR_1} | 协力任务 — {VAR_1} | GwpCaseArchiveScreen.cs:525 |
| gwp_gwpcasearchivescreen_033 | Assigned: {VAR_1} | 分配时间：{VAR_1} | GwpCaseArchiveScreen.cs:527 |
| gwp_gwpcasearchivescreen_034 | Assignee: {VAR_1} &#124; Supporting: {VAR_2} | 受领人：{VAR_1} &#124; 支援主办人：{VAR_2} | GwpCaseArchiveScreen.cs:530 |
| gwp_gwpcasearchivescreen_035 | Assistance reason: the lead Grey Warden's pursuit was blocked and requires a joint capture &#124; Source case: {VAR_1} — {VAR_2} | 协力原因：主办灰袍追击受阻，需要共同围捕 &#124; 来源案件：{VAR_1} — {VAR_2} | GwpCaseArchiveScreen.cs:533 |
| gwp_gwpcasearchivescreen_036 | Village relief — {VAR_1} | 村庄善后 — {VAR_1} | GwpCaseArchiveScreen.cs:547 |
| gwp_gwpcasearchivescreen_037 | Queued: {VAR_1} | 进入任务池：{VAR_1} | GwpCaseArchiveScreen.cs:549, GwpCaseArchiveScreen.cs:573, GwpCaseArchiveScreen.cs:605, GwpCaseArchiveScreen.cs:632, GwpCaseArchiveScreen.cs:667, GwpCaseArchiveScreen.cs:707 |
| gwp_gwpcasearchivescreen_038 | Assignee: {VAR_1} &#124; Stage: {VAR_2} | 受领人：{VAR_1} &#124; 当前阶段：{VAR_2} | GwpCaseArchiveScreen.cs:552 |
| gwp_gwpcasearchivescreen_039 | Assignee: waiting for forced assignment | 受领人：等待强制分配 | GwpCaseArchiveScreen.cs:554 |
| gwp_gwpcasearchivescreen_040 | Task type: forced adoption relief &#124; Remaining: {VAR_1} hours | 任务类型：强制收养善后 &#124; 剩余：{VAR_1}小时 | GwpCaseArchiveScreen.cs:556 |
| gwp_gwpcasearchivescreen_041 | Task type: forced adoption relief | 任务类型：强制收养善后 | GwpCaseArchiveScreen.cs:558 |
| gwp_gwpcasearchivescreen_042 | waiting for assignment | 等待分配 | GwpCaseArchiveScreen.cs:838, GwpCaseArchiveScreen.cs:852, GwpCaseArchiveScreen.cs:864 |
| gwp_gwpcasearchivescreen_043 | resupplying | 补给中 | GwpCaseArchiveScreen.cs:840 |
| gwp_gwpcasearchivescreen_044 | travelling to village | 前往村庄 | GwpCaseArchiveScreen.cs:842, GwpCaseArchiveScreen.cs:854 |
| gwp_gwpcasearchivescreen_045 | relief in progress | 驻村善后 | GwpCaseArchiveScreen.cs:844 |
| gwp_gwpcasearchivescreen_046 | unknown | 未知 | GwpCaseArchiveScreen.cs:845, GwpCaseArchiveScreen.cs:857, GwpCaseArchiveScreen.cs:869, GwpCaseArchiveScreen.cs:883, GwpCaseArchiveScreen.cs:897, GwpCaseArchiveScreen.cs:907 |
| gwp_gwpcasearchivescreen_047 | Unknown Grey Warden party | 未知灰袍队伍 | GwpCaseArchiveScreen.cs:511, GwpCaseArchiveScreen.cs:514, GwpCaseArchiveScreen.cs:545, GwpCaseArchiveScreen.cs:569, GwpCaseArchiveScreen.cs:602, GwpCaseArchiveScreen.cs:629, GwpCaseArchiveScreen.cs:701, GwpCaseArchiveScreen.cs:785, GwpCaseArchiveScreen.cs:798 |
| gwp_gwpcasearchivescreen_048 | Unknown target | 未知目标 | GwpCaseArchiveScreen.cs:520, GwpCaseArchiveScreen.cs:818 |
| gwp_greywardenfamilybehavior_053 | Village Reconstruction Warden | 灰袍村庄重建官 | GreyWardenFamilyBehavior.cs:312 |
| gwp_greywardenfamilybehavior_054 | Grey Warden Lawkeeper | 灰袍巡法官 | GreyWardenFamilyBehavior.cs:304, GreyWardenFamilyBehavior.cs:314 |
| gwp_greywardenfamilybehavior_055 | — | {VAR_1}幼年时因{VAR_2}毁于罪犯劫掠而失去旧日生活，后被灰袍守卫收养。成年后，她正式受职为{VAR_3}并承担对应职责；她的经历证明，法律不仅要缉捕罪犯，也必须替灾后幸存者恢复秩序与归宿。 | 未检出直接引用；保留 |
| gwp_greywardenfamilybehavior_056 | — | {VAR_1}在灰袍守卫保存的旧帝国法律、家族血脉与严整纪律中长大。成年后，她正式受职为{VAR_2}并承担对应职责。对民众而言，她代表着灰袍仍会守护道路、缉捕罪犯、修复暴力造成的破坏并维持和平。 | 未检出直接引用；保留 |
| gwp_reconstruction_001 | nameless village | 无名村庄 | GreyWardenVillageReconstructionBehavior.cs:285 |
| gwp_reconstruction_002 | {VAR_1} completed the reconstruction of {VAR_2}. | {VAR_1}完成了{VAR_2}的重建。 | GreyWardenVillageReconstructionBehavior.cs:375 |
| gwp_gwpcasearchivescreen_049 | Village reconstruction — {VAR_1} | 村庄重建 — {VAR_1} | GwpCaseArchiveScreen.cs:571 |
| gwp_gwpcasearchivescreen_050 | Assignee: {VAR_1} ({VAR_2}) &#124; Stage: {VAR_3} | 受领人：{VAR_1}（{VAR_2}） &#124; 当前阶段：{VAR_3} | GwpCaseArchiveScreen.cs:576 |
| gwp_gwpcasearchivescreen_051 | Assignee: waiting for a reconstruction warden | 受领人：等待村庄重建官 | GwpCaseArchiveScreen.cs:581 |
| gwp_gwpcasearchivescreen_052 | Assignee: reconstruction office has died out | 受领人：村庄重建职务已经断绝 | GwpCaseArchiveScreen.cs:582 |
| gwp_gwpcasearchivescreen_053 | Task type: village reconstruction &#124; Remaining: {VAR_1} hours &#124; Estimated allocation: {VAR_2} &#124; Treasury reserve: {VAR_3} | 任务类型：村庄重建 &#124; 剩余：{VAR_1}小时 &#124; 预计拨款：{VAR_2} &#124; 公库红线：{VAR_3} | GwpCaseArchiveScreen.cs:585 |
| gwp_gwpcasearchivescreen_054 | Task type: village reconstruction &#124; Estimated allocation: {VAR_1} &#124; Treasury reserve: {VAR_2} | 任务类型：村庄重建 &#124; 预计拨款：{VAR_1} &#124; 公库红线：{VAR_2} | GwpCaseArchiveScreen.cs:589 |
| gwp_gwpcasearchivescreen_055 | reconstruction in progress | 驻村重建中 | GwpCaseArchiveScreen.cs:856 |
| gwp_gwpcasearchivescreen_056 | Judicial treasury: {VAR_1} denars | 司法公库：{VAR_1} 第纳尔 | GwpCaseArchiveScreen.cs:363 |
| gwp_gwpcasearchivescreen_057 | Your standing: {VAR_1} &#124; Wanted — the lawful fine is {VAR_2} denars to a provost patrol, {VAR_3} to a Grey Warden lord | 你的声望：{VAR_1} &#124; 通缉中 —— 巡逻队罚金 {VAR_2} 第纳尔，灰袍领主罚金 {VAR_3} 第纳尔 | GwpCaseArchiveScreen.cs:422 |
| gwp_gwpcasearchivescreen_058 | in good standing | 声誉良好 | GwpCaseArchiveScreen.cs:429 |
| gwp_gwpcasearchivescreen_059 | nothing on record | 尚无记录 | GwpCaseArchiveScreen.cs:431 |
| gwp_gwpcasearchivescreen_060 | under suspicion, not yet wanted | 已有嫌疑，尚未通缉 | GwpCaseArchiveScreen.cs:432 |
| gwp_gwpcasearchivescreen_061 | Your standing: {VAR_1} &#124; {VAR_2} | 你的声望：{VAR_1} &#124; {VAR_2} | GwpCaseArchiveScreen.cs:435 |
| gwp_gwpcasearchivescreen_062 | Family: not yet established | 家族：尚未建立 | GwpCaseArchiveScreen.cs:373 |
| gwp_gwpcasearchivescreen_063 | ready to take in a girl | 已可收养女孩 | GwpCaseArchiveScreen.cs:376 |
| gwp_gwpcasearchivescreen_064 | {VAR_1} until the next adoption | 距下次收养还需 {VAR_1} | GwpCaseArchiveScreen.cs:377 |
| gwp_gwpcasearchivescreen_065 | last adoption {VAR_1} | 上次收养于 {VAR_1} | GwpCaseArchiveScreen.cs:381 |
| gwp_gwpcasearchivescreen_066 | no adoption on record | 尚无收养记录 | GwpCaseArchiveScreen.cs:384 |
| gwp_gwpcasearchivescreen_067 | Family: {VAR_1}/{VAR_2} &#124; {VAR_3} &#124; {VAR_4} | 家族：{VAR_1}/{VAR_2} &#124; {VAR_3} &#124; {VAR_4} | GwpCaseArchiveScreen.cs:387 |
| gwp_gwpcasearchivescreen_068 | less than an hour | 不足一小时 | GwpCaseArchiveScreen.cs:397 |
| gwp_gwpcasearchivescreen_069 | about {VAR_1} days | 约 {VAR_1} 天 | GwpCaseArchiveScreen.cs:401 |
| gwp_gwpcasearchivescreen_070 | about {VAR_1} hours | 约 {VAR_1} 小时 | GwpCaseArchiveScreen.cs:402 |
| gwp_gwpcasearchivescreen_071 | War — {VAR_1} | 对外战争 —— {VAR_1} | GwpCaseArchiveScreen.cs:456 |
| gwp_gwpcasearchivescreen_072 | Unnamed faction | 未具名势力 | GwpCaseArchiveScreen.cs:458 |
| gwp_gwpcasearchivescreen_073 | Ground {VAR_1} of {VAR_2} | 开战依据 {VAR_1}/{VAR_2} | GwpCaseArchiveScreen.cs:460 |
| gwp_gwpcasearchivescreen_074 | Ground for the war | 开战依据 | GwpCaseArchiveScreen.cs:462 |
| gwp_gwpcasearchivescreen_075 | Peace returns once no ground remains. | 依据全部消失后即恢复和平。 | GwpCaseArchiveScreen.cs:465 |
| gwp_location_duty_travelling | Travelling toward the last known position of {TARGET_PARTY}. | 正在前往{TARGET_PARTY}最后出现的位置。 | GwpPartyDutyMovementPatch.cs:183 |
| gwp_location_duty_pursuing | Pursuing {TARGET_PARTY}. | 正在追捕{TARGET_PARTY}。 | GwpPartyDutyMovementPatch.cs:171 |
| gwp_mapbar_reputation_title | Grey Warden Standing | 灰袍声望 | GwpMapBarReputationPatch.cs:67 |
| gwp_mapbar_reputation_hint | How the Grey Wardens judge you. Crimes lower it, good deeds raise it. | 灰袍守卫对你的评价。犯罪扣分，行善加分。 | GwpMapBarReputationPatch.cs:70 |
| gwp_mapbar_reputation_wanted | You are wanted. Grey Warden lords will open a case against you. | 你已被通缉。灰袍领主会对你立案。 | GwpMapBarReputationPatch.cs:77 |
| gwp_join_recruits_granted | {VAR_1} Grey Warden recruits have joined your party. | {VAR_1} 名灰袍新兵加入了你的队伍。 | PlayerBountyBehavior.DialogueAndNotification.cs:406 |
| gwp_gwpencyclopediaheropagevm_020 | — | 村庄保护震慑：{VAR_1}（本人 {VAR_2}，传递 {VAR_3}） | 未检出直接引用；保留 |
| gwp_gwpencyclopediaheropagevm_021 | — | 商队保护震慑：{VAR_1}（本人 {VAR_2}，传递 {VAR_3}） | 未检出直接引用；保留 |
| gwp_duty_training | Training Warden | 灰袍练兵者 | GreyWardenFamilyBehavior.cs:308 |
| gwp_duty_caravan | Caravan Protection Warden | 灰袍商路巡法者 | GreyWardenFamilyBehavior.cs:309 |
| gwp_duty_village | Village Protection Warden | 灰袍乡土巡法者 | GreyWardenFamilyBehavior.cs:310 |
| gwp_duty_issues | Petition Warden | 灰袍请愿巡行者 | GreyWardenFamilyBehavior.cs:311 |
| gwp_duty_player | Noble Affairs Liaison | 贵族事务协调官 | GreyWardenFamilyBehavior.cs:313 |
| gwp_adult_adopted_origin | {VAR_1} was taken in by the Grey Wardens after {VAR_2} was sacked and burned in her childhood. She remained to serve the order as an adult. {VAR_3} | {VAR_1}幼时因{VAR_2}遭劫焚毁而被灰袍收养，成年后留在修会任职。{VAR_3} | 收养成员成年后的百科开头，再拼接实际本职简介VAR_3。 |
| gwp_adult_born_origin | {VAR_1} grew up under the Grey Warden order's rule and took up its duties on reaching adulthood. {VAR_2} | {VAR_1}在灰袍修会长大，从小受戒律约束，成年后承担修会的职责。{VAR_2} | 非收养成员成年后的百科开头，再拼接实际本职简介VAR_2。 |
| gwp_bio_hint_law | She serves with the patrols, dealing with cases across the land. | 她随巡察队处理各地案件。 | 成年成员没有本职记录时的百科职责片段。 |
| gwp_bio_hint_training | She trains the soldiers, teaching new recruits to hold their ranks. | 她负责操练士兵，要求新兵先学会守住队列。 | 成年成员承担练兵本职时的百科职责片段。 |
| gwp_bio_hint_caravan | She patrols the trade roads and pursues those who attack caravans. | 她巡察商路，追捕袭击商队的人。 | 成年成员承担商队护卫时。 |
| gwp_bio_hint_village | She pursues village raiders and protects villagers on the roads. | 她负责村庄遇袭后的追捕，保护往来的村民。 | 成年成员承担护村本职时。 |
| gwp_bio_hint_issues | She travels between villages and towns to hear the notables' petitions. | 她往来于村庄和城镇，听取要人的请愿。 | 成年成员承担要人事务时。 |
| gwp_bio_hint_rebuild | She oversees rebuilding in ruined villages and helps their scattered people return. | 她留在受灾村庄安排重建，帮助流散的居民回来。 | 成年成员承担重建本职时。 |
| gwp_bio_hint_player | She receives appeals from nobles and seeks a public decision for Wardens who have suffered injustice. | 她受理贵族的申诉，为受到不公对待的灰袍成员请求公开议决。 | 成年成员承担贵族事务时；英文对齐现有中文与职责。 |
| gwp_issue_resolution_complete | — | {VAR_1}在{VAR_2}处驻留六小时并解决了请求：{VAR_3}。 | 未检出直接引用；保留 |
| gwp_issue_ledger_header | Petition — {VAR_1} | 请愿任务 — {VAR_1} | GwpCaseArchiveScreen.cs:603 |
| gwp_issue_ledger_assignment | Assignee: {VAR_1} ({VAR_2}) &#124; Stage: {VAR_3} | 受领人：{VAR_1}（{VAR_2}） &#124; 当前阶段：{VAR_3} | GwpCaseArchiveScreen.cs:608 |
| gwp_issue_ledger_waiting | Assigned to: no one yet; the petition is still on the shelf | 受领人：尚无；这份陈情还搁在架上 | GwpCaseArchiveScreen.cs:612 |
| gwp_issue_ledger_review | Task type: native town/village issue &#124; Issuer: {VAR_1} &#124; Settlement: {VAR_2} &#124; Remaining review: {VAR_3} hours | 任务类型：原版城镇/村庄任务 &#124; 发布人：{VAR_1} &#124; 地点：{VAR_2} &#124; 剩余审理：{VAR_3}小时 | GwpCaseArchiveScreen.cs:614 |
| gwp_issue_ledger_detail | Task type: native town/village issue &#124; Issuer: {VAR_1} &#124; Settlement: {VAR_2} | 任务类型：原版城镇/村庄任务 &#124; 发布人：{VAR_1} &#124; 地点：{VAR_2} | GwpCaseArchiveScreen.cs:617 |
| gwp_issue_stage_travel | travelling to the issuer | 前往任务发布人 | GwpCaseArchiveScreen.cs:866 |
| gwp_issue_stage_review | reviewing for six hours | 驻留审理六小时 | GwpCaseArchiveScreen.cs:868 |
| gwp_training_ledger_header | Training exchange — {VAR_1} | 练兵换兵 — {VAR_1} | 玩家灰袍事务台账中的标题、候选地点或阶段说明；没有宗教设定冲突，保留。 |
| gwp_training_ledger_assignment | Trainer: {VAR_1} &#124; Stage: {VAR_2} | 练兵领主：{VAR_1} &#124; 当前阶段：{VAR_2} | 玩家灰袍事务台账中的标题、候选地点或阶段说明；没有宗教设定冲突，保留。 |
| gwp_training_ledger_pending_place | To be selected | 待选定 | 玩家灰袍事务台账中的标题、候选地点或阶段说明；没有宗教设定冲突，保留。 |
| gwp_training_ledger_queued | Task type: troop training exchange &#124; Queued as the Training Warden's next duty | 任务类型：练兵换兵 &#124; 已列为练兵领主的下一项任务 | 玩家灰袍事务台账中的标题、候选地点或阶段说明；没有宗教设定冲突，保留。 |
| gwp_training_ledger_waiting | Troop training and exchange &#124; Destination: {VAR_1} &#124; Waiting for the current duty to end | 练兵换兵 &#124; 目的地：{VAR_1} &#124; 等待当前差事结束 | 玩家事务台账：已选目标村但领主仍有当前差事。 |
| gwp_training_ledger_stay | Troop training and exchange &#124; Staying at: {VAR_1} &#124; {VAR_2} hours remaining | 练兵换兵 &#124; 驻留地：{VAR_1} &#124; 尚需{VAR_2}小时 | 玩家事务台账：领主到村后的驻留阶段。 |
| gwp_training_ledger_travel | Troop training and exchange &#124; Travelling to {VAR_1} | 练兵换兵 &#124; 正前往{VAR_1} | 玩家事务台账：领主去目标村途中。 |
| gwp_training_stage_queued | queued as next duty | 已列为下一项任务 | 玩家灰袍事务台账中的标题、候选地点或阶段说明；没有宗教设定冲突，保留。 |
| gwp_training_stage_waiting | waiting for current duties | 等待完成当前任务 | 玩家灰袍事务台账中的标题、候选地点或阶段说明；没有宗教设定冲突，保留。 |
| gwp_training_stage_travel | Travelling to the exchange village | 前往换兵的村庄 | 玩家事务台账中的当前阶段名称。 |
| gwp_training_stage_stay | two-hour troop exchange | 驻留两小时并换兵 | 玩家灰袍事务台账中的标题、候选地点或阶段说明；没有宗教设定冲突，保留。 |
| gwp_det_ui_record | Recorded crimes: {VAR_1} &#124; Arrested by Grey Wardens: {VAR_2} | 累计犯罪：{VAR_1} &#124; 被灰袍抓获：{VAR_2} | GwpEncyclopediaHeroPageVM.cs:107 |
| gwp_det_ui_village_heading | Harm against villagers | 【侵害村民】 | GwpEncyclopediaHeroPageVM.cs:111 |
| gwp_det_ui_villager_desire | Desire to attack villagers: {VAR_1}% of normal (suppressed {VAR_2}%) | 攻击村民欲望：原版的 {VAR_1}%（已压制 {VAR_2}%） | GwpEncyclopediaHeroPageVM.cs:112 |
| gwp_det_ui_raid_desire | Desire to raid villages: {VAR_1}% of normal (the same suppression as attacks on villagers) | 烧村欲望：原版的 {VAR_1}%（与攻击村民共用同一压制） | GwpEncyclopediaHeroPageVM.cs:115 |
| gwp_det_ui_caravan_heading | Harm against caravans | 【侵害商队】 | GwpEncyclopediaHeroPageVM.cs:122 |
| gwp_det_ui_caravan_desire | Desire to attack caravans: {VAR_1}% of normal (suppressed {VAR_2}%) | 攻击商队欲望：原版的 {VAR_1}%（已压制 {VAR_2}%） | GwpEncyclopediaHeroPageVM.cs:123 |
| gwp_det_ui_source | Source of suppression: personal {VAR_1} &#124; passed on {VAR_2} ({VAR_3}) | 压制来源：本人 {VAR_1} &#124; 他人传递 {VAR_2}（{VAR_3}） | GwpEncyclopediaHeroPageVM.cs:210 |
| gwp_det_ui_source_personal | mainly personal experience | 本人经历为主 | GwpEncyclopediaHeroPageVM.cs:201 |
| gwp_det_ui_source_transmitted | mainly warnings passed on by clan members or witnesses | 族人转述或亲眼目击为主 | GwpEncyclopediaHeroPageVM.cs:203 |
| gwp_det_ui_source_none | no active deterrence | 当前没有有效压制 | GwpEncyclopediaHeroPageVM.cs:206 |
| gwp_det_ui_source_balanced | personal experience and passed-on warnings are similar | 本人经历与他人传递接近 | GwpEncyclopediaHeroPageVM.cs:207 |
| gwp_det_ui_recovery_complete | Estimated return to normal: already restored | 预计恢复原状：已经恢复 | GwpEncyclopediaHeroPageVM.cs:243 |
| gwp_det_ui_recovery_hours | {VAR_1} hours | {VAR_1} 小时 | GwpEncyclopediaHeroPageVM.cs:259 |
| gwp_det_ui_recovery_days | {VAR_1} days | {VAR_1} 天 | GwpEncyclopediaHeroPageVM.cs:263 |
| gwp_det_ui_recovery_paused | Estimated return to normal: recovery is currently paused; about {VAR_1} after it resumes | 预计恢复原状：当前暂停恢复；恢复后约需 {VAR_1} | GwpEncyclopediaHeroPageVM.cs:249 |
| gwp_det_ui_recovery_active | Estimated return to normal: about {VAR_1} | 预计恢复原状：约 {VAR_1} | GwpEncyclopediaHeroPageVM.cs:252 |
| gwp_det_ui_recovery_floor_complete | Minimum suppression: permanently fixed at level {VAR_1} | 最低压制：永久固定为 {VAR_1} 级 | GwpEncyclopediaHeroPageVM.cs:228 |
| gwp_det_ui_recovery_floor_paused | Estimated recovery to the level-{VAR_1} minimum: recovery is currently paused; about {VAR_2} after it resumes | 预计恢复至最低压制 {VAR_1} 级：当前暂停恢复；恢复后约需 {VAR_2} | GwpEncyclopediaHeroPageVM.cs:234 |
| gwp_det_ui_recovery_floor_active | Estimated recovery to the level-{VAR_1} minimum: about {VAR_2} | 预计恢复至最低压制 {VAR_1} 级：约 {VAR_2} | GwpEncyclopediaHeroPageVM.cs:237 |
| gwp_fief_reconsideration_title | Petition to reconsider {SETTLEMENT_NAME} | 重新审议{SETTLEMENT_NAME}的归属 | GwpSettlementReconsiderationDecision.cs:44 |
| gwp_fief_reconsideration_support | The Grey Wardens have presented a popular petition. The council will reconsider who should hold {SETTLEMENT_NAME}. | 灰袍已递交民间请愿，议会将重新审议谁应拥有{SETTLEMENT_NAME}。 | GwpSettlementReconsiderationDecision.cs:54 |
| gwp_fief_appeal_file | I wish to file a petition for reconsideration of a fief. | 我想为一块封地申请重新审议。 | GreyWardenPlayerRequestBehavior.cs:227 |
| gwp_fief_appeal_recorded | I will enter the petition. The Noble Affairs Liaison will come to you personally before any fee is collected. | 我会登记这份请愿。在收取任何费用前，贵族事务协调官会亲自来见你。 | GreyWardenPlayerRequestBehavior.cs:236 |
| gwp_fief_payment_offer | Your petition is in my hands. For fifty thousand denars paid into the Grey Warden public treasury, I will carry it to the people and reopen the fief decision. | 你的请愿已交到我手中。向灰袍公库支付五万第纳尔后，我会发动民间支持，并重新开启封地决议。 | GreyWardenPlayerRequestBehavior.cs:252, GreyWardenPlayerRequestBehavior.cs:261 |
| gwp_fief_payment_accept | Take the fifty thousand denars and proceed. | 收下这五万第纳尔，开始行动吧。 | GreyWardenPlayerRequestBehavior.cs:270 |
| gwp_fief_payment_accepted | The sum is entered in the public treasury. I leave for the disputed fief at once. | 款项已经记入公库。我现在就前往争议封地。 | GreyWardenPlayerRequestBehavior.cs:279 |
| gwp_fief_payment_defer | Set the petition aside for now. | 先搁置这份请愿。 | GreyWardenPlayerRequestBehavior.cs:286 |
| gwp_fief_payment_deferred | Very well. The petition will remain open. I will seek you out again when the time is right. | 好。请愿会继续保留，时机合适时我会再来找你。 | GreyWardenPlayerRequestBehavior.cs:295 |
| gwp_fief_payment_withdraw | Withdraw the petition. | 撤回这份请愿。 | GreyWardenPlayerRequestBehavior.cs:302 |
| gwp_fief_payment_withdrawn | Then no payment is due, and the petition is closed. | 那么无需支付费用，这份请愿就此关闭。 | GreyWardenPlayerRequestBehavior.cs:311 |
| gwp_fief_payment_insufficient | You do not have the required fifty thousand denars. You may set the petition aside or withdraw it without payment. | 你没有准备好所需的五万第纳尔。可以暂时搁置请愿，也可以直接撤回，无需付款。 | GreyWardenPlayerRequestBehavior.cs:243 |
| gwp_fief_select_title | Select a fief petition | 选择申诉封地 | GreyWardenPlayerRequestBehavior.cs:337 |
| gwp_fief_select_description | Choose the recently captured fief whose ownership should be reconsidered. | 选择一座近期攻下、需要重新审议归属的城镇或城堡。 | GreyWardenPlayerRequestBehavior.cs:338 |
| gwp_fief_select_hint | Captured with your participation and awarded to another clan. | 你参加了这场攻城战，但封地最终授予了其他家族。 | GreyWardenPlayerRequestBehavior.cs:331 |
| gwp_file_petition | File petition | 提交请愿 | GreyWardenPlayerRequestBehavior.cs:340 |
| gwp_cancel | Cancel | 取消 | GreyWardenPlayerRequestBehavior.cs:341, GreyWardenTroopRequestBehavior.cs:414, PlayerBountyBehavior.DialogueAndNotification.cs:667 |
| gwp_fief_auto_triggered | The Wardens' officer for noble affairs is on her way to see you. | 灰袍的贵族事务官正前来见你。 | GreyWardenPlayerRequestBehavior.cs:373 |
| gwp_fief_request_filed | Your petition for a fief is filed. The Wardens' officer for noble affairs is on her way to you. | 封地请愿已经登记，贵族事务官正前来见你。 | GreyWardenPlayerRequestBehavior.cs:374 |
| gwp_fief_vote_reopened | The Wardens have petitioned over {VAR_1}. The realm will deliberate the fief again, and you are among the candidates. | 灰袍已就{VAR_1}提出请愿，王国将重新商议封地归属，你在候选之列。 | GreyWardenPlayerRequestBehavior.cs:604 |
| gwp_fief_appeal_refunded | Your petition could not be brought forward. The fee has been returned in full. | 封地请愿未能提交，费用已全额退还。 | GreyWardenPlayerRequestBehavior.cs:918 |
| gwp_player_troop_order_file | I want the Training Warden to prepare soldiers for my command. | 我想让练兵官为我准备一批士兵。 | GreyWardenTroopRequestBehavior.cs:274 |
| gwp_player_troop_order_recorded | Settle the price with me now, and I will send the order. The Training Warden will train real troops and bring them to you when they are ready. | 价钱当场结清，我这就把单子送出去。练兵长会把兵训好，亲自给你送来。 | GreyWardenTroopRequestBehavior.cs:283 |
| gwp_player_troop_delivery_offer | Your order is ready: {VAR_1} {VAR_2}. The {VAR_3} denars were settled when you placed it; nothing further is owed. | 你要的兵已经齐了：{VAR_1} 名{VAR_2}。{VAR_3} 第纳尔在你下单那天就结清了，不欠什么。 | GreyWardenTroopRequestBehavior.cs:1113 |
| gwp_player_troop_delivery_accept | Then place them under my command. | 那就把他们交到我手上吧。 | GreyWardenTroopRequestBehavior.cs:308 |
| gwp_player_troop_delivery_accepted | They are yours. These soldiers now answer to you. | 人归你了。从今往后他们听你的。 | GreyWardenTroopRequestBehavior.cs:317 |
| gwp_player_troop_delivery_cancel | Cancel the order. Keep the soldiers. | 取消订单，士兵留给你。 | GreyWardenTroopRequestBehavior.cs:325 |
| gwp_player_troop_delivery_cancelled | Then no payment is due. They return to ordinary Grey Warden service. | 那么无需付款，他们会回到灰袍的日常勤务中。 | GreyWardenTroopRequestBehavior.cs:334 |
| gwp_player_troop_choice | {VAR_1} × {VAR_2} — {VAR_3} denars | {VAR_1}名{VAR_2} — {VAR_3}第纳尔 | GreyWardenTroopRequestBehavior.cs:399 |
| gwp_player_troop_order_title | Place a troop order | 提交调兵订单 | GreyWardenTroopRequestBehavior.cs:410 |
| gwp_player_troop_order_description | Your Grey Warden standing sets the maximum order and available branches. | 你在灰袍中的声望决定订单上限和可选兵种。 | GreyWardenTroopRequestBehavior.cs:411 |
| gwp_player_troop_order_confirm | Send order | 发送订单 | GreyWardenTroopRequestBehavior.cs:413 |
| gwp_player_troop_order_filed | The Training Warden has your order and will bring the soldiers to you once they are ready. | 练兵领主已收到订单，训练完成后会送到你身边。 | GreyWardenTroopRequestBehavior.cs:586 |
| gwp_player_request_unknown_liaison | Unknown Noble Affairs Liaison | 未知的贵族事务协调官 | GwpCaseArchiveScreen.cs:661 |
| gwp_player_request_ledger_header | Player fief appeal — {VAR_1} | 玩家封地申诉 — {VAR_1} | GwpCaseArchiveScreen.cs:664 |
| gwp_player_request_ledger_assignment | Liaison: {VAR_1} &#124; Stage: {VAR_2} | 协调官：{VAR_1} &#124; 当前阶段：{VAR_2} | GwpCaseArchiveScreen.cs:670 |
| gwp_player_request_ledger_lobbying | Task type: player fief appeal &#124; Fee: {VAR_1} &#124; Public support: {VAR_2}% &#124; Remaining stay: {VAR_3} hours | 任务类型：玩家封地申诉 &#124; 费用：{VAR_1} &#124; 民间支持：{VAR_2}% &#124; 剩余驻留：{VAR_3}小时 | GwpCaseArchiveScreen.cs:679 |
| gwp_player_request_ledger_detail | Task type: player fief appeal &#124; Fee: {VAR_1} &#124; Public support: {VAR_2}% | 任务类型：玩家封地申诉 &#124; 费用：{VAR_1} &#124; 民间支持：{VAR_2}% | GwpCaseArchiveScreen.cs:686 |
| gwp_player_request_fee_paid | paid into public treasury | 已交入公库 | GwpCaseArchiveScreen.cs:681, GwpCaseArchiveScreen.cs:688 |
| gwp_player_request_fee_unpaid | not yet collected | 尚未收取 | GwpCaseArchiveScreen.cs:682, GwpCaseArchiveScreen.cs:689 |
| gwp_player_request_stage_payment | travelling to collect payment | 正在前往玩家身边收款 | GwpCaseArchiveScreen.cs:890 |
| gwp_player_request_stage_travel | travelling to disputed fief | 正在前往争议封地 | GwpCaseArchiveScreen.cs:892 |
| gwp_player_request_stage_lobbying | organizing public petition | 正在组织民间请愿 | GwpCaseArchiveScreen.cs:894 |
| gwp_player_request_stage_vote | new fief vote opened | 新的封地投票已经开启 | GwpCaseArchiveScreen.cs:896 |
| gwp_player_request_stage_deferred | Petition set aside | 请愿已搁置 | GwpCaseArchiveScreen.cs:674 |
| gwp_fief_defer_complete | The officer for noble affairs will return to take up your petition. | 贵族事务官将回来继续处理你的封地请愿。 | GreyWardenPlayerRequestBehavior.cs:807 |
| gwp_player_troop_ledger_header | Player troop order — {VAR_1} × {VAR_2} | 玩家调兵订单 — {VAR_1}名{VAR_2} | GwpCaseArchiveScreen.cs:704 |
| gwp_player_troop_ledger_assignment | Trainer: {VAR_1} &#124; Stage: {VAR_2} | 练兵官：{VAR_1} &#124; 当前阶段：{VAR_2} | GwpCaseArchiveScreen.cs:710 |
| gwp_player_troop_ledger_detail | Task type: player troop order &#124; Ready: {VAR_1}/{VAR_2} &#124; Paid on order: {VAR_3} denars | 任务类型：玩家订兵 &#124; 已备：{VAR_1}/{VAR_2} &#124; 下单已付：{VAR_3} 第纳尔 | GwpCaseArchiveScreen.cs:714 |
| gwp_player_troop_stage_training | training real troops | 正在训练真实兵员 | GwpCaseArchiveScreen.cs:904 |
| gwp_player_troop_stage_delivery | travelling to deliver troops | 正在前往玩家身边交付 | GwpCaseArchiveScreen.cs:906 |
| gwp_item_dual_offhand | Grey Warden Dual Blade (Off Hand) | 灰袍双持剑（副手） | _Module/ModuleData/gwp_crafting_pieces.xml, _Module/ModuleData/items.xml |
| gwp_item_dual_mainhand | Grey Warden Dual Blade (Main Hand) | 灰袍双持剑（主手） | _Module/ModuleData/items.xml |
| gwp_fieldarrest_open | Stand where you are. I speak for the Grey Wardens. | 我受灰袍委托，为你名下的一件案子而来。 | GwpFieldArrestBehavior.cs:104 |
| gwp_fieldarrest_charge | The Grey Wardens charge you with {VAR_1}, against {VAR_2}. The fine is {VAR_3} denars. They sent me to settle this. | 灰袍指控你曾{VAR_1}，受害方是{VAR_2}。应缴罚金 {VAR_3} 第纳尔，我受托来处理这件事。 | GwpFieldArrestBehavior.cs:547 |
| gwp_fieldarrest_walk_away | Never mind. Go on your way. | 算了，你走吧。 | GwpFieldArrestBehavior.cs:145 |
| gwp_fieldarrest_victim_unknown | the Wardens' own record | 灰袍记录在案的受害人 | GwpFieldArrestBehavior.cs:539 |
| gwp_fieldarrest_crime_caravan | attacking a caravan under Warden protection | 袭击灰袍保护的商队 | GwpFieldArrestBehavior.cs:572 |
| gwp_fieldarrest_crime_village | violence against villagers under Warden protection | 对灰袍保护的村民动手 | GwpFieldArrestBehavior.cs:573 |
| gwp_fieldarrest_msg_fine | {VAR_1} pays the fine of {VAR_2} denars in full. The money belongs to the Wardens and must be handed to a Warden lord. | {VAR_1}缴清了 {VAR_2} 第纳尔罚金，须交给灰袍领主。 | GwpFieldArrestBehavior.cs:980 |
| gwp_fieldarrest_msg_refused | — | 对方拒捕。动手拿人。 | 未检出直接引用；保留 |
| gwp_field_fine_handed_in | — | 已上缴罚金 {VAR_1} 第纳尔。 | 未检出直接引用；保留 |
| gwp_field_settle_notice_title | Case settled on the road | 外勤处理结束，待交付 | PlayerBountyBehavior.cs:164 |
| gwp_field_settle_notice_body | The pursuit has ended. You must account for the commission and deliver the {VAR_1} denars collected as fines. Report to a Grey Warden lord; any shortfall must also be explained. | 追捕已结束。你需要交代这次办案的结果，并上缴作为罚金收到的 {VAR_1} 第纳尔。请找灰袍领主交付；未收足的部分也要说明。 | PlayerBountyBehavior.cs:166 |
| gwp_field_turnin_lord | — | 罚金已点清，案子销了。灰袍收下 {VAR_1} 第纳尔，你的办案酬劳是 {VAR_2}。 | 未检出直接引用；保留 |
| gwp_fieldarrest_dismissed | Then stop wasting my daylight. | 那就别耽误我的工夫。 | GwpFieldArrestBehavior.cs:150 |
| gwp_fieldarrest_msg_partial | {VAR_1} pays only {VAR_2} of the {VAR_3} denars owed. Hand what you collected to a Warden lord. | {VAR_1}只缴了 {VAR_2} 第纳尔，应缴 {VAR_3} 第纳尔。所收罚金须交给灰袍领主。 | GwpFieldArrestBehavior.cs:981 |
| gwp_fieldarrest_msg_captured | — | {VAR_1} 落在你手里。案底与声望罚款一并结清；把俘虏交给灰袍领主。 | 未检出直接引用；保留 |
| gwp_fieldarrest_seize | — | 我要依照通缉令，抓你归案。 | 未检出直接引用；保留 |
| gwp_fieldarrest_seize_reply | — | 原来灰袍就这点分量。动手吧。 | 未检出直接引用；保留 |
| gwp_fa_open_under_charge | You work for the Wardens? Tell me what you want. | 你替灰袍办事？说吧，找我什么事。 | GwpFieldArrestLines.cs:91 |
| gwp_fa_open_respected_known | You again. Or your order, at least. What is it this time? | 又见面了。我知道你替灰袍办过不少事。这回为了什么？ | GwpFieldArrestLines.cs:93 |
| gwp_fa_open_respected | I know the grey. Speak, and speak plainly. | 我听说过你。既然受灰袍委托，就把来意说清楚。 | GwpFieldArrestLines.cs:95 |
| gwp_fa_open_known | The Wardens again. I remember the last time. | 灰袍又来了。上次的事，我可还记得。 | GwpFieldArrestLines.cs:97 |
| gwp_fa_open_plain | The Wardens. What do you want with me? | 灰袍。找我什么事？ | GwpFieldArrestLines.cs:99 |
| gwp_fa_submit_upright | — | 这件事我认，罚金也照数交。请把交款记清楚。 | 未检出直接引用；保留 |
| gwp_fa_submit_cold | — | 比另一条路便宜。就这一个理由，你我都清楚。 | 未检出直接引用；保留 |
| gwp_fa_submit_soft | — | 钱补不回那些损失。至少这笔罚金，我愿意交。 | 未检出直接引用；保留 |
| gwp_fa_submit_tight | — | 钱都在这里了。点清楚，别再为这一笔来找我。 | 未检出直接引用；保留 |
| gwp_fa_submit_fierce | — | 行，给钱。别当成我怕了你。 | 未检出直接引用；保留 |
| gwp_fa_submit_plain | — | 拿去，点清楚，然后走人。 | 未检出直接引用；保留 |
| gwp_fa_plead_cold | — | 你张口就是这个数。军饷、草料、换马——你看看这支队伍，钱能藏在哪儿？ | 未检出直接引用；保留 |
| gwp_fa_plead_tight | — | 不管你们账上怎么写，我不是有钱人。你在我身上搜不出来。 | 未检出直接引用；保留 |
| gwp_fa_plead_plain | — | 这个数？我身上没有。行李你要翻就翻。 | 未检出直接引用；保留 |
| gwp_fa_resist_scorn | — | 你自己那笔账还没清，倒来收我的？拔剑吧。 | 未检出直接引用；保留 |
| gwp_fa_resist_fierce | — | 我这辈子没花钱买过路。拔剑。 | 未检出直接引用；保留 |
| gwp_fa_resist_cold | — | 你那支队伍我数过了，我自己的我也数过了。不给。 | 未检出直接引用；保留 |
| gwp_fa_resist_tight | — | 一个子儿你也别想拿走。一个都别想。 | 未检出直接引用；保留 |
| gwp_fa_resist_plain | — | 不给。有本事你来拿。 | 未检出直接引用；保留 |
| gwp_fa_task_rebuff | That is no answer. | 这不算回答。 | GwpFieldArrestLines.cs:124 |
| gwp_fa_task_final | You have run out of words, Warden. | 你没词了，灰袍。 | GwpFieldArrestLines.cs:125 |
| gwp_fa_task_later | I will hear no more arguments about this warrant. | 这份案子，我不想再听你争辩了。 | GwpFieldArrestLines.cs:126 |
| gwp_fa_task_authority | You are not my liege and you are not a Warden. You are a man they hired. | 你不是我的主君，也不是灰袍。你是他们雇来的人。 | GwpFieldArrestLines.cs:294 |
| gwp_fa_arg_authority_lead | The Wardens sent me to deal with this. I am conveying their demand. | 灰袍派我来处理这件事。我说的话，就是她们的要求。 | GwpFieldArrestLines.cs:296 |
| gwp_fa_arg_authority_charm | Hired or sworn, the dead are still dead and someone had to come. | 我受谁雇用，不会改变受害者的损失。我们先把这件事说清楚。 | GwpFieldArrestLines.cs:297 |
| gwp_fa_arg_authority_rogue | They hired me because they were tired of asking politely. | 灰袍把案子交给我，就不会因为你不认我而撤回去。 | GwpFieldArrestLines.cs:298 |
| gwp_fa_arg_authority_trade | Whether you accept me or not, the Wardens will still demand payment. Settling now saves you trouble. | 你认不认我，灰袍要求的赔偿都不会少。趁现在把事情解决，对你也省事。 | GwpFieldArrestLines.cs:299 |
| gwp_fa_task_under_charge | Your reputation is scarcely better than mine. Now you lecture me for the Wardens? | 你的名声也没比我好多少，倒来替灰袍教训我了？ | GwpFieldArrestLines.cs:302 |
| gwp_fa_arg_under_lead | My conduct is a separate matter. You must answer to the Wardens for this. | 我做过什么，不妨另说。眼前这些事，你得向灰袍交代。 | GwpFieldArrestLines.cs:304 |
| gwp_fa_arg_under_charm | Then let us avoid another bad outcome. Settle this today so everyone can go home. | 那就别再添一个坏结果了。今天把事情解决，让大家都能回去。 | GwpFieldArrestLines.cs:305 |
| gwp_fa_arg_under_rogue | That gives me all the more reason not to return empty-handed. Think carefully. | 正因为如此，我更不会空手回去。你最好想清楚。 | GwpFieldArrestLines.cs:306 |
| gwp_fa_arg_under_trade | Talking about me saves you not one denar. It does not reduce what you owe. | 议论我值不了一枚第纳尔。你该付的钱不会因此减少。 | GwpFieldArrestLines.cs:307 |
| gwp_fa_task_price | And that number - where did it come from? You say it as if it fell from the sky. | 这个数——哪儿来的？你说得像是天上掉下来的。 | GwpFieldArrestLines.cs:310 |
| gwp_fa_arg_price_lead | This is the fine the Wardens demand. I was sent to enforce it, not invent a price. | 这是灰袍要求的罚金。我受托来执行，不是来随口开价的。 | GwpFieldArrestLines.cs:312 |
| gwp_fa_arg_price_charm | The victims have already borne the loss. Do not add another fight to it. | 受害者已经承担了损失。别再为这件事添一场厮杀。 | GwpFieldArrestLines.cs:313 |
| gwp_fa_arg_price_rogue | It is the cheap answer. You are looking at the expensive one. | 这是便宜那条路。贵的那条正看着你。 | GwpFieldArrestLines.cs:314 |
| gwp_fa_arg_price_trade | The damage and the people harmed both need answering for. This sum covers more than this one encounter. | 损毁的财物、受害的人，都得有个交代。这个数不是只算你眼前这一次。 | GwpFieldArrestLines.cs:315 |
| gwp_fa_task_tally | You are charging me for things I did months apart, as if they were one crime. | 为什么把几次事情合在一起，收我这么一大笔钱？ | GwpFieldArrestLines.cs:318 |
| gwp_fa_arg_tally_lead | Time passing does not remove your responsibility. The Wardens hold you answerable for all of it. | 事情过去了，不等于责任也没了。灰袍要你一起承担。 | GwpFieldArrestLines.cs:320 |
| gwp_fa_arg_tally_charm | Each one had people in it. They were months apart for you, not for them. | 每一件里都有人。对你是隔了几个月，对他们不是。 | GwpFieldArrestLines.cs:321 |
| gwp_fa_arg_tally_rogue | If you wanted them charged separately you should have been caught sooner. | 想一件一件算，早点被逮住就是了。 | GwpFieldArrestLines.cs:322 |
| gwp_fa_arg_tally_trade | Settle these offences together today and spare your party repeated interruptions over them. | 今天一起付清，就不用为这些旧事反复停下队伍。 | GwpFieldArrestLines.cs:323 |
| gwp_fa_task_denial | My men did that. I did not give the order, and I will not pay for their appetite. | 那是我手下干的。我没下这个令，凭什么让我替他们的胃口付钱。 | GwpFieldArrestLines.cs:326 |
| gwp_fa_arg_denial_lead | They are your men. That is what the word means. | 那是你的兵。手下两个字就是这个意思。 | GwpFieldArrestLines.cs:328 |
| gwp_fa_arg_denial_charm | Nobody in that village could tell your hand from theirs. | 村里没一个人分得清是你的手还是他们的手。 | GwpFieldArrestLines.cs:329 |
| gwp_fa_arg_denial_rogue | They acted under your banner. Blame your men, and the Wardens will still come for you. | 他们打着你的旗号行事。推给部下，灰袍也还是会找你。 | GwpFieldArrestLines.cs:330 |
| gwp_fa_arg_denial_trade | They are your men. Pay for the harm first; how you recover it from them is your affair. | 他们是你的部下。先把损失赔了，你回去怎么向他们追究，是你的事。 | GwpFieldArrestLines.cs:331 |
| gwp_fa_task_war | There is a war on. Taking an enemy's goods is not a crime, it is the work. | 打着仗呢。拿敌方的东西不算犯罪，那叫干活。 | GwpFieldArrestLines.cs:334 |
| gwp_fa_arg_war_lead | Your war does not reach the drivers. The Wardens' law does. | 你的仗轮不到赶车的头上。灰袍的律轮得到。 | GwpFieldArrestLines.cs:336 |
| gwp_fa_arg_war_charm | Carters and guards. Not one of them was holding your line. | 车夫和护卫。没有一个是替你守过阵线的。 | GwpFieldArrestLines.cs:337 |
| gwp_fa_arg_war_rogue | Call it war if it helps. The Wardens call it a caravan and a body count. | 你愿意叫它打仗就叫吧。灰袍这边写的是一支商队和几具尸首。 | GwpFieldArrestLines.cs:338 |
| gwp_fa_arg_war_trade | Wars end. Accounts do not, until someone pays them. | 仗会打完。账不会，除非有人付。 | GwpFieldArrestLines.cs:339 |
| gwp_fa_task_consequence | And if I simply refuse to pay? What can you do about it? | 我要是干脆不交呢？你还能把我怎么样？ | GwpFieldArrestLines.cs:342 |
| gwp_fa_arg_consequence_lead | My departure would not make the Wardens drop this. We need an answer today. | 我离开了，灰袍也不会就此罢手。今天总得有个交代。 | GwpFieldArrestLines.cs:344 |
| gwp_fa_arg_consequence_charm | Then the next grey rider is not here to talk, and neither of us wants that. | 那下一个穿灰的来就不是找你说话了，这对谁都不好。 | GwpFieldArrestLines.cs:345 |
| gwp_fa_arg_consequence_rogue | You can wager that I cannot take you. You cannot choose how many Wardens come next time. | 你可以赌我拿不住你。下回灰袍来多少人，你可说了不算。 | GwpFieldArrestLines.cs:346 |
| gwp_fa_arg_consequence_trade | The debt remains until it is settled. Waiting does not make it disappear. | 这笔欠款会一直留着，不会因为你拖延就消失。 | GwpFieldArrestLines.cs:347 |
| gwp_fa_task_record | Your order has had me once already. Is this how it goes now - forever? | 你们抓过我一回了。以后就这么没完了是吗？ | GwpFieldArrestLines.cs:350 |
| gwp_fa_arg_record_lead | It goes until you stop giving them cause. That part is yours. | 你不再给他们理由，就完了。这一半在你手上。 | GwpFieldArrestLines.cs:352 |
| gwp_fa_arg_record_charm | They took you once and let you ride. Few orders would have. | 抓了一回还放你走的团，不多。 | GwpFieldArrestLines.cs:353 |
| gwp_fa_arg_record_rogue | You know how an arrest ends. Do you want to go through that again? | 被捕是什么结果，你已经经历过。还要再来一回吗？ | GwpFieldArrestLines.cs:354 |
| gwp_fa_arg_record_trade | Pay what is assessed now. Any future offence will have its own reckoning. | 先结清眼下这笔。以后再犯的事，另有新的账。 | GwpFieldArrestLines.cs:355 |
| gwp_fieldarrest_war_mediated | — | 灰袍调停：案子已经了结，由此与{VAR_1}结下的战事就此停息。 | 未检出直接引用；保留 |
| gwp_fa_desire_bribe | Property worth {GWP_BRIBE_AMOUNT} denars, for you alone. What you tell the Wardens is your affair. | 我给你价值 {GWP_BRIBE_AMOUNT} 第纳尔的财物，归你自己。回去怎么说，你来想办法。 | GwpOffenderDesire.cs:139 |
| gwp_fa_desire_duel | Fight me alone. If you win, I pay the fine; if I win, you collect nothing. | 你我单挑。你赢了，我缴罚金；我赢了，你就不收这笔钱。 | GwpOffenderDesire.cs:145 |
| gwp_fa_l1_failed | Enough. Your order has no claim on me, and neither do you. | 我不接受这份指控，你也休想凭几句话让我低头。 | GwpFieldArrestBehavior.cs:162, GwpFieldArrestBehavior.cs:184 |
| gwp_fa_l1_done | ...Very well. I answer to the charge. The question is how. | 好，我接受这次处置。接下来谈谈怎么了结。 | GwpFieldArrestBehavior.cs:166 |
| gwp_fa_l1_lost | I reject this demand from the Wardens. If you insist on force, draw. | 我不接受灰袍这次处置。你执意动手，就拔剑吧。 | GwpFieldArrestBehavior.cs:188 |
| gwp_fa_take_by_force | I will enforce the warrant by force. | 我要强制执行通缉令，拿下你。 | GwpFieldArrestBehavior.cs:194, GwpFieldArrestBehavior.cs:264, GwpFieldArrestBehavior.cs:300 |
| gwp_fa_leave_it | Ride on, then. This is not finished. | 那你走吧。这事没完。 | GwpFieldArrestBehavior.cs:198, GwpFieldArrestBehavior.cs:303, GwpFieldArrestBehavior.cs:307 |
| gwp_fa_l2_agreed | — | 那就照数全缴，没别的话说。 | 未检出直接引用；保留 |
| gwp_fa_l2_failed | No. I have told you how this will go. | 全额我不答应。我能接受的条件已经告诉你了。 | GwpFieldArrestBehavior.cs:217, GwpFieldArrestBehavior.cs:237 |
| gwp_fa_l2_done | ...The full sum, then. Have it your way. | ……那就全额。随你。 | GwpFieldArrestBehavior.cs:221 |
| gwp_fa_l2_lost | — | 我的条件不变。你愿意就照办，不愿意就动手。 | 未检出直接引用；保留 |
| gwp_fa_collect | — | 全额，照规矩来。 | 未检出直接引用；保留 |
| gwp_fa_take_anyway | Keep your silver. I am taking you in. | 你的银子留着。人我带走。 | GwpFieldArrestBehavior.cs:250 |
| gwp_fa_settled_line | — | 点清了。现在从我路上让开。 | 未检出直接引用；保留 |
| gwp_fa_desire_done_line | Then we understand each other. | 那咱们就算说明白了。 | GwpFieldArrestBehavior.cs:274, GwpFieldArrestBehavior.cs:288 |
| gwp_fa_desire_payfull | Very well. The full sum, and we are finished here. | 也罢。全额，到此为止。 | GwpOffenderDesire.cs:151 |
| gwp_fa_desire_surrender | That silver is my men's wages. Take me instead - I ride with you, and they go home. | 那些银子是我部下的军饷。你带我走吧——我跟你，让他们回家。 | GwpOffenderDesire.cs:137 |
| gwp_fa_desire_cunning | Half. That is what the baggage carries, and that is what you are getting. | 一半。辎重里就这么多，你能拿到的也就这么多。 | GwpOffenderDesire.cs:141 |
| gwp_fa_desire_rash | Take four fifths of the fine and let us finish this. | 八成交给你。就这么了结，别再争了。 | GwpOffenderDesire.cs:143 |
| gwp_fa_desire_deeds | I will pay two fifths. The men I blame will answer to me with their lives. | 我只交四成。那些惹事的部下，我会当场处死，给你一个交代。 | GwpOffenderDesire.cs:147 |
| gwp_fa_desire_haggle | I will give you seven tenths of the fine. That is my offer. | 我最多出七成。你愿意收，我们就此了结。 | GwpOffenderDesire.cs:149 |
| gwp_fa_accept_payfull | The full sum. Hand it over. | 全额。交出来。 | GwpOffenderDesire.cs:174 |
| gwp_fa_accept_surrender | Then ride with me. Your men can find their own way. | 那就跟我走。你的人自己想办法。 | GwpOffenderDesire.cs:160 |
| gwp_fa_accept_bribe | Hand it over. This conversation did not happen. | 拿过来。这段话没发生过。 | GwpOffenderDesire.cs:162 |
| gwp_fa_accept_cunning | Half, then. The rest stays against your name. | 我收这一半，剩下的差额也会写进报告。 | GwpOffenderDesire.cs:164 |
| gwp_fa_accept_rash | Most of it will do. The remainder stays on the account. | 八成我收下，短缺的数目一并记下。 | GwpOffenderDesire.cs:166 |
| gwp_fa_accept_duel | Draw, then. Let us settle it between us. | 那就拔剑。你我之间了结。 | GwpOffenderDesire.cs:168 |
| gwp_fa_accept_deeds | Pay for the deed, then. The dead stay on your record. | 那就为你做下的事付钱。那些人命的账留着。 | GwpOffenderDesire.cs:170 |
| gwp_fa_accept_haggle | Seven tenths, then. I will have to account for the shortfall. | 七成就七成。剩下的差额，我回去交代。 | GwpOffenderDesire.cs:172 |
| gwp_fieldarrest_msg_surrender | {VAR_1} surrenders. Deliver him to a Warden lord. | {VAR_1}投降了。请将他押交灰袍领主。 | GwpFieldArrestBehavior.cs:1024 |
| gwp_fieldarrest_msg_bribe | {VAR_1} slips you goods worth {VAR_2} denars. | {VAR_1}私下塞给你价值 {VAR_2} 第纳尔的财物。 | GwpFieldArrestBehavior.cs:1058 |
| gwp_fieldarrest_msg_own_men | He has several of his own men put to death before you to bear the blame. | 他当着你的面处决了几名手下顶罪。 | GwpFieldArrestBehavior.cs:1161 |
| gwp_fieldarrest_reaction_fallback | — | …… | 未检出直接引用；保留 |
| gwp_fieldarrest_msg_duel_lost | {VAR_1} wins the duel, and you let him go as agreed. | {VAR_1}赢了决斗，你依约放他离开。 | GwpFieldArrestBehavior.cs:1108 |
| gwp_fieldarrest_msg_duel_won | — | 你赢了单挑，向 {VAR_1} 收到 {VAR_2} 第纳尔。若未收足，交账时仍需说明。 | 未检出直接引用；保留 |
| gwp_report_open | — | 我有一笔路上办案收来的罚金要交代。 | 未检出直接引用；保留 |
| gwp_report_ask | — | 卷宗要一个数。他交了你多少？ | 未检出直接引用；保留 |
| gwp_report_hand_in | — | 你自己点。 | 未检出直接引用；保留 |
| gwp_report_no_money | — | 他身上榨不出什么东西。 | 未检出直接引用；保留 |
| gwp_report_no_money_reply | — | 那卷宗上就这么记。暂且。 | 未检出直接引用；保留 |
| gwp_report_result_short | — | 卷宗上写的是 {VAR_1}。你交上来 {VAR_2}。这个差额你自己担着。 | 未检出直接引用；保留 |
| gwp_report_result_clean | — | {VAR_1}，账平了。灰袍谢你。 | 未检出直接引用；保留 |
| gwp_report_short | — | 卷宗写的是 {VAR_1}，你交上来 {VAR_2}。灰袍声望 {VAR_3}。 | 未检出直接引用；保留 |
| gwp_report_exposed | — | 灰袍拿下了 {VAR_1}，他的钱袋和你当初说的对不上。灰袍声望 {VAR_2}。 | 未检出直接引用；保留 |
| gwp_det_ui_ledger_clean | Grey Warden account: nothing outstanding. | 灰袍账目：没有未清的账。 | GwpEncyclopediaHeroPageVM.cs:160 |
| gwp_det_ui_ledger_heading | Grey Warden account | 灰袍账目 | GwpEncyclopediaHeroPageVM.cs:165 |
| gwp_det_ui_ledger_case | Open case: {VAR_1} deed(s), {VAR_2} dead on this case alone. | 未结案件：{VAR_1} 起；仅这件案子就死了 {VAR_2} 人。 | GwpEncyclopediaHeroPageVM.cs:171 |
| gwp_det_ui_ledger_standing_lives | Civilian lives still on his record: {VAR_1}. | 尚未赎清的人命：{VAR_1} 条。 | GwpEncyclopediaHeroPageVM.cs:184 |
| gwp_det_ui_ledger_total | Owed in full: {VAR_1} denars. | 合计应缴：{VAR_1} 第纳尔。 | GwpEncyclopediaHeroPageVM.cs:189 |
| gwp_det_ui_ledger_no_case | No case is open against him, but the charge for the dead stands until it is paid off or he is taken. | 目前没有正在追捕的案件；若有待交付的罚金，交账后才会抵扣欠款。剿匪赎过也可能减少负声望。 | GwpEncyclopediaHeroPageVM.cs:193 |
| gwp_case_payment | Case fines delivered to the judicial treasury | 向灰袍司法公库上缴罚金 | GwpFieldFineBarterable.cs:28 |
| gwp_case_captive_ready | You hold the target captive. Deliver him to any Warden lord to collect the reward. | 目标已被你俘获。将他押交任意一位灰袍领主即可领取赏金。 | PlayerBountyBehavior.CaseSettlement.cs:421 |
| gwp_case_closed_elsewhere | {VAR_1} is beyond your reach now. The commission is void. Report to any Grey Warden lord, or send a man. | {VAR_1}已经不在你能追到的地方了。委托到此为止。去找任意灰袍领主复命，或者派个人去。 | PlayerBountyBehavior.CaseSettlement.cs:270 |
| gwp_case_report_open | I have come to report on the commission. | 我来交代上次受托办的案子。 | PlayerBountyBehavior.CaseSettlement.cs:452 |
| gwp_case_deliver_money | — | 罚金带来了，我们点一下。 | 未检出直接引用；保留 |
| gwp_case_report_poverty | — | 他确实付不起全额。我把收到的钱交上来，你们可以查。 | 未检出直接引用；保留 |
| gwp_case_report_zero | — | 这次我没有钱上缴，差额记在我名下。 | 未检出直接引用；保留 |
| gwp_case_missing_prisoner | — | 俘虏已经不在我手里了。这笔酬劳我领不了。 | 未检出直接引用；保留 |
| gwp_case_report_later | I am not ready to hand it over yet. | 钱和人还没有准备好，我改日再来。 | PlayerBountyBehavior.CaseSettlement.cs:462 |
| gwp_case_barter_accepted | We have counted the payment. Your report will be entered. | 钱物清点好了，这份委托可以结了。 | PlayerBountyBehavior.CaseSettlement.cs:475 |
| gwp_case_barter_cancelled | Nothing has been entered. We can count it again when you are ready. | 这次没有入账。你准备好了，我们再点。 | PlayerBountyBehavior.CaseSettlement.cs:490 |
| gwp_case_report_summary | The account lists {VAR_1} denars. Have you brought the fines, or the prisoner? If any money is missing, tell me why. | 卷宗上记着 {VAR_1} 第纳尔。你带来了罚金，还是把人押来了？钱若有短缺，也请说清缘由。 | PlayerBountyBehavior.CaseSettlement.cs:510 |
| gwp_case_missing_result | — | 人没有交到，办案酬劳就不能发。这份委托到此结束。 | 未检出直接引用；保留 |
| gwp_case_report_complete | Your report is entered. The treasury has paid {VAR_1} denars in expenses. This commission is concluded. | 交付已经记下。公库付给你 {VAR_1} 第纳尔办案酬劳，这份委托结清了。 | PlayerBountyBehavior.CaseSettlement.cs:668 |
| gwp_case_capture_failed | The target was not taken. The case remains open. | 目标未被俘获，案件尚未了结。 | GwpFieldArrestBehavior.cs:1003 |
| gwp_case_duel_failed | The duel could not begin. | 决斗未能开始。 | GwpFieldArrestBehavior.cs:1148 |
| gwp_case_cash_with_prisoner | — | 你之前还向他收过 {VAR_1} 第纳尔。把这笔钱补齐，和俘虏一起交来。 | 未检出直接引用；保留 |
| gwp_case_cash_still_due | — | 俘虏已经不在你手里，但先前收下的罚金仍要交代。 | 未检出直接引用；保留 |
| gwp_case_purse_short | I accept the fine, but my money and goods together are worth only {VAR_1} denars. That is what I can give now. | 罚金我认，可我身边的钱和东西合起来只值 {VAR_1} 第纳尔。现在能交的就是这些。 | GwpFieldArrestBehavior.cs:381, GwpFieldArrestBehavior.cs:393 |
| gwp_case_collect_amount | Present property worth {VAR_1} denars. I will report the payment and any shortfall. | 拿出价值 {VAR_1} 第纳尔的财物。实收和短缺我会回报。 | GwpFieldArrestBehavior.cs:401 |
| gwp_fa_payment_budget | — | 我还得发军饷、买粮草。交了这笔钱，我拿什么养这些人？ | 未检出直接引用；保留 |
| gwp_fa_payment_budget_lead | — | 他们由你统率，闯的祸也该由你负责。交清罚金，别再把他们卷进来。 | 未检出直接引用；保留 |
| gwp_fa_payment_budget_charm | — | 他们需要你平安带队回去。眼下还不必为这笔钱再死人。 | 未检出直接引用；保留 |
| gwp_fa_payment_budget_rogue | — | 想想打输一仗会丢掉什么。到时候你心疼的就不只是这袋钱了。 | 未检出直接引用；保留 |
| gwp_fa_payment_budget_trade | — | 补兵、换马、添置装备都要钱。和这些开销比，眼下缴清更合算。 | 未检出直接引用；保留 |
| gwp_fa_payment_terms | — | 我已经开了条件，为什么非得按你说的全额交？ | 未检出直接引用；保留 |
| gwp_fa_payment_terms_lead | — | 我受托收的是这笔罚金。你另开的条件，不能替我向灰袍交差。 | 未检出直接引用；保留 |
| gwp_fa_payment_terms_charm | — | 你已经认了这件事。把它妥善了结，我们也就不必为此交战。 | 未检出直接引用；保留 |
| gwp_fa_payment_terms_rogue | — | 你少交的钱，要我回去担责。我不会替你背这笔账。 | 未检出直接引用；保留 |
| gwp_fa_payment_terms_trade | — | 缴清后，我交账时能替你抵清这笔欠款。只交一部分，余下的还记在你名下。 | 未检出直接引用；保留 |
| gwp_case_pending_encyclopedia | Fine assessed: {VAR_1} denars. Paid by the offender: {VAR_2}. | 本次罚金：{VAR_1} 第纳尔。犯人已缴：{VAR_2} 第纳尔。 | GwpEncyclopediaHeroPageVM.cs:177 |
| gwp_case_duel_post_win | You won. I accept it. Inspect my property and let us settle the fine as agreed. | 你赢了，我认。照约定，看看我的财物，把罚金结清吧。 | GreyWardenSparringBehavior.cs:89 |
| gwp_case_duel_post_loss | You lost. As agreed, you collect no fine from me today. | 你输了。按约定，今天你不能向我收这笔罚金。 | GreyWardenSparringBehavior.cs:97 |
| gwp_case_duel_post_reply | I will report the outcome to the Grey Wardens. | 我会把结果向灰袍回报。 | GreyWardenSparringBehavior.cs:100 |
| gwp_case_barter_count | Let us check what you have brought. | 让我清点一下你带来的东西和人。 | PlayerBountyBehavior.CaseSettlement.cs:467 |
| gwp_case_report_acknowledge | Let me have the account. | 把结清的账目给我吧。 | PlayerBountyBehavior.CaseSettlement.cs:495 |
| gwp_fa_refuse_strength | I will not accept this demand. We have nothing to discuss. | 灰袍这次的要求，我不接受。我们没什么好谈的。 | GwpFieldArrestBehavior.cs:155 |
| gwp_fa_hear_charge | I have heard the charge. First tell me why I should answer to you. | 指控我听见了。不过，你先说说，我为什么要接受你的处置？ | GwpFieldArrestBehavior.cs:158 |
| gwp_fa_request_terms | Then tell me how you propose to settle this. | 那就说吧，你打算怎么了结？ | GwpFieldArrestBehavior.cs:201 |
| gwp_fa_counter_offer | Those terms will not do. Let us discuss the full fine. | 这个条件我不能接受。我们谈谈照数缴清的事。 | GwpFieldArrestBehavior.cs:210 |
| gwp_fa_count_money | Here are the money and goods. Choose what you will take. | 钱和东西都在这里，你来挑。 | GwpFieldArrestBehavior.cs:269, GwpFieldArrestBehavior.cs:277 |
| gwp_fa_collection_paid | The money is counted. We are done here. | 钱点清了。今天就到这里。 | GwpFieldArrestBehavior.cs:281 |
| gwp_fa_collection_empty | See for yourself. There is no money or property left to give you. | 你自己看吧，已经没有能交给你的钱和东西了。 | GwpFieldArrestBehavior.cs:284 |
| gwp_fa_accept_empty | I will report that you could not pay. Your debt remains. | 我会如实回报你付不起钱，欠账仍记在你名下。 | GwpFieldArrestBehavior.cs:286 |
| gwp_fa_collection_cancel | No payment has changed hands. What do you intend to do? | 钱还没交出去。你到底打算怎么办？ | GwpFieldArrestBehavior.cs:290 |
| gwp_fa_fight_reply | — | 那就拔剑吧。 | 未检出直接引用；保留 |
| gwp_fa_accept_count | Show me the money and goods. We will agree what they cover. | 把钱和东西拿出来，能抵多少，我们当面说清。 | GwpFieldArrestBehavior.cs:1198 |
| gwp_terms_final_objection | And if I pay the full amount, what do I gain by giving up my terms? | 就算照数交清，我放弃刚才的条件，又能换来什么？ | GwpFieldArrestLines.cs:183 |
| gwp_terms_final_lead | Your command stays with you. I need the account settled, not another battle. | 你仍带你的队伍。我来是为了结清这笔账，不是再打一仗。 | GwpFieldArrestLines.cs:184 |
| gwp_terms_final_charm | We both leave with our people alive. That is worth more than prolonging this quarrel. | 双方的人都能活着离开。这比继续争执要紧。 | GwpFieldArrestLines.cs:185 |
| gwp_terms_final_rogue | You remove the reason I am standing in your path. Do you really want to wager more? | 把这笔钱付了，我今天就没有拦你的理由。你还想赌上更多？ | GwpFieldArrestLines.cs:186 |
| gwp_terms_final_trade | An exact payment leaves no remainder to argue over when I deliver the account. | 交清了，回报时就不会还挂着一笔余款。 | GwpFieldArrestLines.cs:187 |
| gwp_fa_desire_cautious | Keep your weapons lowered. I will pay what you ask; there need be no battle. | 先把武器放下。钱我照数交，没必要打这一仗。 | GwpOffenderDesire.cs:133 |
| gwp_fa_desire_merciful | Enough people have suffered. Take the full fine, and let there be no more bloodshed. | 已经够多人受苦了。罚金我全交，别再流血了。 | GwpOffenderDesire.cs:134 |
| gwp_fa_desire_generous | I can spare the silver. Take the full amount and see that this account is settled. | 这笔钱我拿得出。照数拿去，把账结清。 | GwpOffenderDesire.cs:135 |
| gwp_report_audit_found | The Wardens find {VAR_1} denars missing from your accounts. | 灰袍查出你上交的账目短缺 {VAR_1} 第纳尔。 | GwpFieldReportLedger.cs:375 |
| gwp_case_false_payment | — | 【撒谎】只收上来这些，剩下的他实在付不起。我们点一下。 | 未检出直接引用；保留 |
| gwp_case_false_zero | — | 【撒谎】他身无分文，我一分钱也没收到。 | 未检出直接引用；保留 |
| gwp_case_prisoner_hide | — | 【隐瞒】人交给你们。我没有向他收过钱。 | 未检出直接引用；保留 |
| gwp_case_prisoner_partial | — | 【隐瞒】人和账上这笔钱一起交给你们。 | 未检出直接引用；保留 |
| gwp_case_full_payment | — | 足额的 {VAR_1} 第纳尔都在这里，把账结清吧。 | 未检出直接引用；保留 |
| gwp_case_true_payment | — | 实际收了 {VAR_1} 第纳尔，我全部交上来。剩下的没能收到，我如实回报。 | 未检出直接引用；保留 |
| gwp_case_prisoner_cash | — | 人交给你们，先前向他收过的 {VAR_1} 第纳尔也一并交上。 | 未检出直接引用；保留 |
| gwp_field_collection_money | Money in the offender's purse | 犯人钱袋中的第纳尔 | GwpFieldCollectionBarterable.cs:27 |
| gwp_case_truthful_poor_result | — | 实收的 {VAR_1} 第纳尔已经入账。他付不起的余款仍记在他名下，不追究你。办案酬劳为 {VAR_2} 第纳尔，委托结清。 | 未检出直接引用；保留 |
| gwp_case_truthful_gap_result | — | 实收的 {VAR_1} 第纳尔已经入账。未能收足的差额按办案责任记下。办案酬劳为 {VAR_2} 第纳尔，委托结清。 | 未检出直接引用；保留 |
| gwp_case_transfer_retry | — | 交付暂时没办成，钱已退回。你仍保管俘虏和款项，换一位灰袍领主再办。 | 未检出直接引用；保留 |
| gwp_terms_surrenderself_lead | Your men also need their commander. Paying keeps you at their head. | 你的部下也需要统帅。交钱了结，你仍能带着他们。 | GwpFieldArrestLines.cs:199 |
| gwp_terms_surrenderself_charm | You need not leave your people behind. Pay, and stay with them. | 不必把自己人丢下。交清罚金，你就能留在他们身边。 | GwpFieldArrestLines.cs:200 |
| gwp_terms_surrenderself_rogue | Do you trust whoever commands in your absence? Silver is easier to replace. | 你放心把指挥权交给别人？钱可比你的位子容易找回来。 | GwpFieldArrestLines.cs:201 |
| gwp_terms_surrenderself_trade | Captivity will cost your household and command as well. Settle in silver today. | 被关押也会拖累家族和队伍。用现钱了结，损失更可控。 | GwpFieldArrestLines.cs:202 |
| gwp_terms_bribethewarden_lead | I carry a warrant, not a begging bowl. Pay what it names. | 灰袍让我来收罚金，不是收你的好处。该交多少就交多少。 | GwpFieldArrestLines.cs:204 |
| gwp_terms_bribethewarden_charm | Do not put this between us. Settle openly and we can both ride away. | 别把事情弄成这样。光明正大地交清，双方都能走。 | GwpFieldArrestLines.cs:205 |
| gwp_terms_bribethewarden_rogue | You want me to risk my name for your loose change. Keep the bribe and bring the fine. | 拿这点私钱换我背锅？贿金收回去，罚金拿出来。 | GwpFieldArrestLines.cs:206 |
| gwp_terms_bribethewarden_trade | A private purse does not settle a public debt. The full account still needs paying. | 私下给我的钱抵不了公库的账。该缴的总数并没变。 | GwpFieldArrestLines.cs:207 |
| gwp_terms_cunninghalf_lead | Then open the other baggage. I will not accept your word for half an account. | 那就把别的行李也打开。我不会凭一句话就认下只剩一半。 | GwpFieldArrestLines.cs:209 |
| gwp_terms_cunninghalf_charm | Show me enough trust to stop bargaining with a story about your purse. | 别再拿钱袋当借口。把实情说清，事情也好收场。 | GwpFieldArrestLines.cs:210 |
| gwp_terms_cunninghalf_rogue | Half conveniently missing? I have heard that trick before. Find the rest. | 刚好只剩一半？这套说辞我听过。把另一半找出来。 | GwpFieldArrestLines.cs:211 |
| gwp_terms_cunninghalf_trade | Half the payment leaves half the account. Your story does not change the sum. | 付一半就还欠一半。你怎么解释钱袋，都改不了这个数。 | GwpFieldArrestLines.cs:212 |
| gwp_terms_rashmost_lead | If you want this finished, finish the payment. Do not leave the last fifth hanging. | 真想快点了结，就把最后两成交齐。别留个尾巴。 | GwpFieldArrestLines.cs:214 |
| gwp_terms_rashmost_charm | We are close enough to agreement. Do not throw it away over the remainder. | 已经谈到这一步了，别为最后一点余款闹翻。 | GwpFieldArrestLines.cs:215 |
| gwp_terms_rashmost_rogue | You risk the whole purse to keep its last fifth. Think before you refuse. | 为留下最后两成，把整只钱袋赌上？你再想想。 | GwpFieldArrestLines.cs:216 |
| gwp_terms_rashmost_trade | You have offered four fifths. Add the remaining fifth and there is no balance left. | 八成已经答应了，再补两成，账就齐了。 | GwpFieldArrestLines.cs:217 |
| gwp_terms_demandduel_lead | This warrant is not a contest. Meet the charge without risking your commander's life. | 这份通缉令不是比武请帖。别为它拿自己的命去赌。 | GwpFieldArrestLines.cs:219 |
| gwp_terms_demandduel_charm | You need not prove your courage to me. Spare both our people another loss. | 你不必向我证明勇气。双方都少受一次伤，不好吗？ | GwpFieldArrestLines.cs:220 |
| gwp_terms_demandduel_rogue | A duel lets you gamble away somebody else's debt. I will not take that wager. | 拿单挑免掉别人的血债？这个赌法我不接。 | GwpFieldArrestLines.cs:221 |
| gwp_terms_demandduel_trade | Even victory costs wounds and time. The fine is a known price; a duel is not. | 赢了也可能受伤耽误行程。罚金有定数，刀剑可没有。 | GwpFieldArrestLines.cs:222 |
| gwp_terms_deedsonly_lead | Killing your own soldiers will not discharge your responsibility. Pay the full fine. | 杀自己的兵也免不了你的责任。把罚金交齐。 | GwpFieldArrestLines.cs:224 |
| gwp_terms_deedsonly_charm | There have been enough deaths. Keep your men alive and settle this in silver. | 死的人已经够多了。留下他们的命，用钱了结。 | GwpFieldArrestLines.cs:225 |
| gwp_terms_deedsonly_rogue | Cutting down witnesses does not make your account smaller. Do not pretend it does. | 砍掉几个知情人，账就会变少？别拿这个糊弄我。 | GwpFieldArrestLines.cs:226 |
| gwp_terms_deedsonly_trade | Two fifths will not cover this account. Your men's lives are not a substitute for the rest. | 四成填不平这笔账。你部下的命也抵不了剩下的钱。 | GwpFieldArrestLines.cs:227 |
| gwp_terms_haggledown_lead | This is an assessed fine. I have no order to discount it by three tenths. | 这是核定的罚金，我没有给你打七折的命令。 | GwpFieldArrestLines.cs:229 |
| gwp_terms_haggledown_charm | Let us end this without another quarrel. Pay the remainder as well. | 别再为余款起争执了，照数交齐吧。 | GwpFieldArrestLines.cs:230 |
| gwp_terms_haggledown_rogue | You are bargaining as though I must sell. I can still enforce the warrant. | 你以为我非卖给你不可？我还可以照令抓人。 | GwpFieldArrestLines.cs:231 |
| gwp_terms_haggledown_trade | Seven tenths leaves three tenths unpaid. Close the balance now. | 七成交了还剩三成。把余款也结清。 | GwpFieldArrestLines.cs:232 |
| gwp_field_villagers | local villagers | 附近的村民 | GwpFieldArrestBehavior.cs:542 |
| gwp_fa_retry_payment | I still stand by my terms. Have you another proposal? | 我还是坚持刚才的条件。你还有别的说法吗？ | GwpFieldArrestBehavior.cs:770 |
| gwp_fa_retry_authority | That is not enough for me to accept your authority. Have you another reason? | 这还不足以让我接受你的处置。你还有别的理由吗？ | GwpFieldArrestBehavior.cs:771 |
| gwp_fa_critical_rebuff | Enough. I will hear no more of this. | 够了，这件事我不想再谈。 | GwpFieldArrestBehavior.cs:845 |
| gwp_rebuff_authority_lead | Your warrant is not a command I have sworn to obey. Give me a better reason. | 我可没向这份通缉令宣过誓。你得拿出更能让我信服的理由。 | GwpFieldArrestLines.cs:244 |
| gwp_rebuff_authority_charm | I hear your concern for them. That does not settle whether I answer to you. | 我听见你替他们说话了，但这还没说明我为什么该接受你的处置。 | GwpFieldArrestLines.cs:245 |
| gwp_rebuff_authority_rogue | Threats are easy to make. I have not agreed to your authority. | 威胁谁都会说。我还没答应听你的。 | GwpFieldArrestLines.cs:246 |
| gwp_rebuff_authority_trade | You keep counting the fine before I have accepted the charge. | 你一直在算罚金，可我还没认下这份指控。 | GwpFieldArrestLines.cs:247 |
| gwp_rebuff_surrenderself_lead | My answer is still to go with you. That money stays with my men. | 正因为我是统帅，才该由我跟你走。钱留给部下。 | GwpFieldArrestLines.cs:253 |
| gwp_rebuff_surrenderself_charm | My answer is still to go with you. That money stays with my men. | 他们不会因为我被押走就没了活路，没了军饷才会。 | 未检出直接引用；保留 |
| gwp_rebuff_surrenderself_rogue | My answer is still to go with you. That money stays with my men. | 我离开后谁来指挥，自有安排。你不必替我操心。 | 未检出直接引用；保留 |
| gwp_rebuff_surrenderself_trade | My answer is still to go with you. That money stays with my men. | 你算的是我的损失，我想的是他们明天吃什么。 | 未检出直接引用；保留 |
| gwp_rebuff_bribethewarden_lead | You can put a brave face on it. I am offering you a private payment, not the full fine. | 别急着拿通缉令压人。我说的是给你个人的好处。 | GwpFieldArrestLines.cs:254 |
| gwp_rebuff_bribethewarden_charm | You can put a brave face on it. I am offering you a private payment, not the full fine. | 你我都能好好离开，何必非要把钱送进公库？ | 未检出直接引用；保留 |
| gwp_rebuff_bribethewarden_rogue | You can put a brave face on it. I am offering you a private payment, not the full fine. | 嫌这点钱不值得冒险？那也是私下谈价，不是让我照数缴罚。 | 未检出直接引用；保留 |
| gwp_rebuff_bribethewarden_trade | You can put a brave face on it. I am offering you a private payment, not the full fine. | 公库的账是公库的账。我问的是你愿不愿意收这笔钱。 | 未检出直接引用；保留 |
| gwp_rebuff_cunninghalf_lead | You suspect another purse, but suspicion does not put more money on this table. Half is my offer. | 要翻行李就直说，但我不会因此改口答应全额。 | GwpFieldArrestLines.cs:255 |
| gwp_rebuff_cunninghalf_charm | You suspect another purse, but suspicion does not put more money on this table. Half is my offer. | 信任不是你说一句，我就得掏出另一半。我的条件没变。 | 未检出直接引用；保留 |
| gwp_rebuff_cunninghalf_rogue | You suspect another purse, but suspicion does not put more money on this table. Half is my offer. | 听过这种说辞，就能断定我还藏着钱？你得拿出点别的。 | 未检出直接引用；保留 |
| gwp_rebuff_cunninghalf_trade | You suspect another purse, but suspicion does not put more money on this table. Half is my offer. | 我知道半款还剩多少。我是在给你条件，不是在请你算数。 | 未检出直接引用；保留 |
| gwp_rebuff_rashmost_lead | I offered most of it to end this quickly. Now you want to keep arguing over the rest. | 我肯出八成，已经是让步。别把让步当成理所当然。 | GwpFieldArrestLines.cs:256 |
| gwp_rebuff_rashmost_charm | I offered most of it to end this quickly. Now you want to keep arguing over the rest. | 既然只差这一点，你也可以让一步，何必全要我来让？ | 未检出直接引用；保留 |
| gwp_rebuff_rashmost_rogue | I offered most of it to end this quickly. Now you want to keep arguing over the rest. | 我可没说要赌掉整只钱袋。你少拿最坏的事吓我。 | 未检出直接引用；保留 |
| gwp_rebuff_rashmost_trade | I offered most of it to end this quickly. Now you want to keep arguing over the rest. | 不用教我算那两成。我知道差多少，只是不想再给。 | 未检出直接引用；保留 |
| gwp_rebuff_demandduel_lead | All those reasons to avoid a duel. I still want this settled between the two of us. | 通缉令在你手上，剑也在你手上。为什么不敢亲自来了结？ | GwpFieldArrestLines.cs:257 |
| gwp_rebuff_demandduel_charm | All those reasons to avoid a duel. I still want this settled between the two of us. | 你说不必证明勇气，我却不想靠掏钱结束这件事。 | 未检出直接引用；保留 |
| gwp_rebuff_demandduel_rogue | All those reasons to avoid a duel. I still want this settled between the two of us. | 你不肯接这个赌，我也没答应换个办法。 | 未检出直接引用；保留 |
| gwp_rebuff_demandduel_trade | All those reasons to avoid a duel. I still want this settled between the two of us. | 你把伤口和行程都算进去了。我看重的可不只是钱。 | 未检出直接引用；保留 |
| gwp_rebuff_deedsonly_lead | Their punishment is mine to decide. I offered you two fifths, no more. | 我的兵由我处置。你来收你的钱，别替我发号施令。 | GwpFieldArrestLines.cs:258 |
| gwp_rebuff_deedsonly_charm | Their punishment is mine to decide. I offered you two fifths, no more. | 你想替他们求情？那也不会让我多交余下的罚金。 | 未检出直接引用；保留 |
| gwp_rebuff_deedsonly_rogue | Their punishment is mine to decide. I offered you two fifths, no more. | 知情人？他们是犯错的部下。别给这件事再添一份指控。 | 未检出直接引用；保留 |
| gwp_rebuff_deedsonly_trade | Their punishment is mine to decide. I offered you two fifths, no more. | 我知道钱不够你报的数。我只认我答应的四成。 | 未检出直接引用；保留 |
| gwp_rebuff_haggledown_lead | You call it an exact account; I call it too much. My offer is still seven tenths. | 你说没有打折的命令，我也没答应任你开价。 | GwpFieldArrestLines.cs:259 |
| gwp_rebuff_haggledown_charm | You call it an exact account; I call it too much. My offer is still seven tenths. | 想少一次争执，你同样可以接受我给的七成。 | 未检出直接引用；保留 |
| gwp_rebuff_haggledown_rogue | You call it an exact account; I call it too much. My offer is still seven tenths. | 我知道你能动手。不必每次还价都拿这句话压我。 | 未检出直接引用；保留 |
| gwp_rebuff_haggledown_trade | You call it an exact account; I call it too much. My offer is still seven tenths. | 三成有多少，我算得明白。我争的就是这三成。 | 未检出直接引用；保留 |
| gwp_voice_open_upright_0 | Show me the warrant. I will answer a proper charge. | 把委托给我看看。该答的话，我会答。 | GwpFieldDialogueVoice.cs:88 |
| gwp_voice_open_upright_1 | If you speak for the Wardens, state your business clearly. | 你既然代表灰袍，就把事情说清楚。 | GwpFieldDialogueVoice.cs:88 |
| gwp_voice_open_cold_0 | You did not chase me down to ask directions. Speak. | 你专程追上来，总不会只是问路。说吧。 | GwpFieldDialogueVoice.cs:89 |
| gwp_voice_open_cold_1 | The Wardens want me? Tell me the business first. | 灰袍找我？先说是什么事，再谈别的。 | GwpFieldDialogueVoice.cs:89 |
| gwp_voice_open_fierce_0 | You had better have a reason for blocking my road. | 拦我的路，总得有个说法吧。 | GwpFieldDialogueVoice.cs:90 |
| gwp_voice_open_fierce_1 | I can see your banner. What do you want? | 旗子我看见了。你找我做什么？ | GwpFieldDialogueVoice.cs:90 |
| gwp_voice_open_soft_0 | Speak here. There is no need to alarm the others. | 有什么事在这里说，别惊动后面的人。 | GwpFieldDialogueVoice.cs:91 |
| gwp_voice_open_soft_1 | The Wardens... Who has suffered this time? | 灰袍来了……又有谁受了苦？ | GwpFieldDialogueVoice.cs:91 |
| gwp_voice_open_tight_0 | Say your piece. I have promised you nothing. | 先说清楚，我可没答应给你什么。 | GwpFieldDialogueVoice.cs:92 |
| gwp_voice_open_tight_1 | A visit from your people is seldom cheap. | 你们找上门来，准没什么便宜事。 | GwpFieldDialogueVoice.cs:92 |
| gwp_voice_open_plain_0 | You work for the Wardens? What is this about? | 你是替灰袍办事的？找我有什么事？ | GwpFieldDialogueVoice.cs:93 |
| gwp_voice_open_plain_1 | Wait. Did you say the Wardens sent you? | 等一下。你刚才说，灰袍派你来的？ | GwpFieldDialogueVoice.cs:93 |
| gwp_voice_paid_0 | We counted the money together. Why are you back? | 钱已经当面点过了。你回来还有什么事？ | GwpFieldDialogueVoice.cs:94 |
| gwp_voice_paid_1 | I have paid you. What remains to discuss? | 我已经把钱交给你了，还有哪里没说清？ | GwpFieldDialogueVoice.cs:94 |
| gwp_voice_fight_upright_0 | Let everyone here see who draws first. | 那就让在场的人看清楚，是谁先拔的剑。 | GwpFieldDialogueVoice.cs:95 |
| gwp_voice_fight_upright_1 | I will not submit. Come and take me. | 我不会束手就擒。你要拿人，尽管来。 | GwpFieldDialogueVoice.cs:95 |
| gwp_voice_fight_cold_0 | There is nothing left to reckon. Form up. | 话说到这里，也没什么可算的了。列阵。 | GwpFieldDialogueVoice.cs:96 |
| gwp_voice_fight_cold_1 | You chose this course. I hope you counted the cost. | 你选了这条路。希望你算过代价。 | GwpFieldDialogueVoice.cs:96 |
| gwp_voice_fight_fierce_0 | Good! We could have spared ourselves the speeches. | 好！刚才那些话早该省了。 | GwpFieldDialogueVoice.cs:97 |
| gwp_voice_fight_fierce_1 | Come, then. Let us see if you can! | 来啊，看你有没有这个本事！ | GwpFieldDialogueVoice.cs:97 |
| gwp_voice_fight_soft_0 | Must this end in blood? Then I have to defend my people. | 一定要流血才肯罢休吗？那我也只能迎战。 | GwpFieldDialogueVoice.cs:98 |
| gwp_voice_fight_soft_1 | Stand back, away from us. It seems there is no avoiding this. | 退后，别站在我们中间。看来今天是躲不过了。 | GwpFieldDialogueVoice.cs:98 |
| gwp_voice_fight_tight_0 | Cannot take the money, so you take me? Try it. | 钱拿不走，就来抢人？你试试看。 | GwpFieldDialogueVoice.cs:99 |
| gwp_voice_fight_tight_1 | You want to hand me over? It will not be easy. | 想拿我去交差？没那么容易。 | GwpFieldDialogueVoice.cs:99 |
| gwp_voice_fight_plain_0 | Then there is nothing left to say. Draw. | 那就没什么好说的了。拔剑吧。 | GwpFieldDialogueVoice.cs:100 |
| gwp_voice_fight_plain_1 | I will not hold out my hands for your ropes. | 我可不会自己伸手让你绑。 | GwpFieldDialogueVoice.cs:100 |
| gwp_voice_betray_upright_0 | You took the money and now break our agreement? Everyone heard it! | 钱你收了，约定却不认了？在场的人都听见了！ | GwpFieldDialogueVoice.cs:101 |
| gwp_voice_betray_upright_1 | I paid as agreed. Is this how you serve the Wardens? | 我照约定付了钱。你就是这样替灰袍办事的？ | GwpFieldDialogueVoice.cs:101 |
| gwp_voice_betray_cold_0 | So payment was not the end. I gave your word too much credit. | 原来收钱还不算完。是我把你的话看得太重了。 | GwpFieldDialogueVoice.cs:102 |
| gwp_voice_betray_cold_1 | My money in your purse and me in your prison cart? Quite a bargain. | 钱进了你的口袋，我还得进你的囚车？好一笔买卖。 | GwpFieldDialogueVoice.cs:102 |
| gwp_voice_betray_fierce_0 | You take my money and turn on me? Draw your sword! | 拿了钱还敢翻脸？把剑拔出来！ | GwpFieldDialogueVoice.cs:103 |
| gwp_voice_betray_fierce_1 | I did not pay out of fear! Now you will learn that. | 我付钱可不是怕你！现在你就会知道。 | GwpFieldDialogueVoice.cs:103 |
| gwp_voice_betray_soft_0 | I paid so everyone could leave safely. Will you not even allow that? | 我交钱是为了让大家平安离开。你连这个也不肯？ | GwpFieldDialogueVoice.cs:104 |
| gwp_voice_betray_soft_1 | You have the money. Why force these people to fight as well? | 钱已经给了，为什么还要逼这些人拼命？ | GwpFieldDialogueVoice.cs:104 |
| gwp_voice_betray_tight_0 | Give that money back! Paid and arrested, is that your trade? | 把刚才的钱还来！收了钱还要抓我，你倒会做生意！ | GwpFieldDialogueVoice.cs:105 |
| gwp_voice_betray_tight_1 | I counted every coin into your hands, and now it means nothing? | 我一枚一枚数给你的，现在你说不算？ | GwpFieldDialogueVoice.cs:105 |
| gwp_voice_betray_plain_0 | Wait, I already paid you! How can you turn on me now? | 等等，钱不是已经给你了吗？你怎么说翻脸就翻脸？ | GwpFieldDialogueVoice.cs:106 |
| gwp_voice_betray_plain_1 | That was not our agreement! You have the money. What more do you want? | 刚才可不是这么说的！钱你也拿了，还要怎样？ | GwpFieldDialogueVoice.cs:106 |
| gwp_voice_success_upright_0 | That is a fair point. I accept it. | 这句话有道理，我认。 | GwpFieldDialogueVoice.cs:107 |
| gwp_voice_success_upright_1 | I cannot dispute that point. | 这一点我没法反驳你。 | GwpFieldDialogueVoice.cs:107 |
| gwp_voice_success_cold_0 | That is something I must consider. | 这倒是我得考虑的事。 | GwpFieldDialogueVoice.cs:108 |
| gwp_voice_success_cold_1 | That does change the reckoning. | 这样算，确实不同。 | GwpFieldDialogueVoice.cs:108 |
| gwp_voice_success_fierce_0 | All right. I hear you. | 行，这话我听进去了。 | GwpFieldDialogueVoice.cs:109 |
| gwp_voice_success_fierce_1 | That argument carries some weight. | 这句话还算有些分量。 | GwpFieldDialogueVoice.cs:109 |
| gwp_voice_success_soft_0 | I cannot ignore the people you speak of. | 你说的这些人，我不能不管。 | GwpFieldDialogueVoice.cs:110 |
| gwp_voice_success_soft_1 | Yes... I should give that more thought. | 是……这件事我该多想一想。 | GwpFieldDialogueVoice.cs:110 |
| gwp_voice_success_tight_0 | As you put it, there is something in it for me. | 照你这么说，我也不是一点好处都没有。 | GwpFieldDialogueVoice.cs:111 |
| gwp_voice_success_tight_1 | Those figures do make some sense. | 这么算倒还说得过去。 | GwpFieldDialogueVoice.cs:111 |
| gwp_voice_success_plain_0 | That is true. | 这倒也是。 | GwpFieldDialogueVoice.cs:112 |
| gwp_voice_success_plain_1 | You have persuaded me on that point. | 嗯，这一点你说服我了。 | GwpFieldDialogueVoice.cs:112 |
| gwp_voice_demand_payinfull_alt | I will pay the assessed sum. Let us settle this. | 照数付清就是了。把这件事了结吧。 | GwpFieldDialogueVoice.cs:62 |
| gwp_voice_demand_paytoavoidbattle_alt | Tell your men to hold. I will pay in full; nobody needs to die here. | 先叫你的人别动。钱我照付，犯不着在这里死人。 | GwpFieldDialogueVoice.cs:63 |
| gwp_voice_demand_payforpeace_alt | Enough people have suffered. I will pay in full; leave the others out of it. | 已经够多人受罪了。我把罚金付清，别再牵连别人。 | GwpFieldDialogueVoice.cs:64 |
| gwp_voice_demand_paygenerously_alt | I can afford it. Take the whole sum and settle the account. | 这些钱我出得起，全部拿去，把账结清。 | GwpFieldDialogueVoice.cs:65 |
| gwp_voice_demand_surrenderself_alt | The money must feed these men. If you need a prisoner, take me alone. | 钱得留着养活这些人。你要交差，就带我一个走。 | GwpFieldDialogueVoice.cs:66 |
| gwp_voice_demand_bribethewarden_alt | This is worth {GWP_BRIBE_AMOUNT} denars to you. Take it and let our discussion end here. | 这份好处值 {GWP_BRIBE_AMOUNT} 第纳尔，比白跑一趟强。拿了，我们就把话说到这里。 | GwpFieldDialogueVoice.cs:67 |
| gwp_voice_demand_cunninghalf_alt | I cannot raise that sum. Half is all there is. Take it if you want it. | 你要的数目我凑不齐。只有一半，你要就拿去。 | GwpFieldDialogueVoice.cs:68 |
| gwp_voice_demand_rashmost_alt | Four fifths. Take it. Spare me the argument over the rest. | 八成，拿去吧。剩下两成就别再费口舌了。 | GwpFieldDialogueVoice.cs:69 |
| gwp_voice_demand_demandduel_alt | Just you and me. You win, I pay the fine. I win, you leave empty-handed. | 我们两个来比一场。你赢，罚金照付；我赢，你空手走。 | GwpFieldDialogueVoice.cs:70 |
| gwp_voice_demand_deedsonly_alt | Two fifths is all I will pay. I will kill the men I blame myself. | 我只出四成。那几个办坏事的，我自己会杀给你看。 | GwpFieldDialogueVoice.cs:71 |
| gwp_voice_demand_haggledown_alt | Seven tenths is plenty. Think carefully about that offer. | 七成已经够多了。这个价钱，你好好想想。 | GwpFieldDialogueVoice.cs:72 |
| gwp_voice_hear_alt | I heard the charge. Why should I accept your authority? | 罪名我听见了。可我凭什么听你处置？ | GwpFieldDialogueVoice.cs:73 |
| gwp_voice_refuse_alt | You want me to accept your authority? No. Stand aside. | 让我听你处置？不。让开。 | GwpFieldDialogueVoice.cs:74 |
| gwp_voice_done1_alt | All right, I accept the charge. We still have to agree on how to settle it. | 行，这件案子我认。至于怎么了结，我们还得谈。 | GwpFieldDialogueVoice.cs:75 |
| gwp_voice_lost1_alt | You still have not given me a reason to submit. Stop circling the matter. | 你还没说出让我服气的理由。别再绕了。 | GwpFieldDialogueVoice.cs:76 |
| gwp_voice_done2_alt | All right, the sum you named. No further conditions. | 好，按你说的数目来。别再加别的条件了。 | GwpFieldDialogueVoice.cs:77 |
| gwp_voice_lost2_alt | I will not yield on that. My offer still stands. | 这一步我不让。刚才开的条件仍然算数。 | GwpFieldDialogueVoice.cs:78 |
| gwp_case_capture_missed | The target was not taken. The commission is not yet done. | 目标未被俘获，委托尚未完成。 | PlayerBountyBehavior.CaseSettlement.cs:142 |
| gwp_fa_task_authority_variant_1 | The Wardens may have a say, but that does not put you in command of me. | 灰袍管得着这件事，也不见得轮得到你来管我。 | GwpFieldDialogueVoice.cs:23 |
| gwp_fa_task_authority_variant_2 | You are running an errand for the Wardens. Why should I answer to you? | 你不过是替灰袍跑腿的。我凭什么听你的？ | GwpFieldDialogueVoice.cs:23 |
| gwp_fa_task_under_charge_variant_1 | Your own reputation is hardly spotless. Now you lecture me? | 你自己也没什么好名声，倒来教训我了？ | GwpFieldDialogueVoice.cs:24 |
| gwp_fa_task_under_charge_variant_2 | I might listen to someone else. You should put your own affairs right first. | 换个人来，我兴许还愿意听。你？先管好自己吧。 | GwpFieldDialogueVoice.cs:24 |
| gwp_fa_task_price_variant_1 | That much? What gives the Wardens cause to demand that sum? | 这么多钱？灰袍凭什么向我要这个数？ | GwpFieldDialogueVoice.cs:25 |
| gwp_fa_task_price_variant_2 | You ask for a great deal of money. You owe me an explanation. | 你开口就是这么大一笔，总得给我个说法吧。 | GwpFieldDialogueVoice.cs:25 |
| gwp_fa_task_tally_variant_1 | You mean to pursue the older offences too? How far back will you go? | 过去的事也要一起追究？你们到底想追到什么时候？ | GwpFieldDialogueVoice.cs:26 |
| gwp_fa_task_tally_variant_2 | Those incidents were long apart. Now you bring them all to me at once? | 那些事都隔了多久了，现在倒一起来找我？ | GwpFieldDialogueVoice.cs:26 |
| gwp_fa_task_denial_variant_1 | Ask my soldiers what they did. I gave no such order. | 我的兵做了什么，你找他们去。我没叫他们这么干。 | GwpFieldDialogueVoice.cs:27 |
| gwp_fa_task_denial_variant_2 | Why should I bear all the blame for what my men did? | 部下惹的事，凭什么全要我来担？ | GwpFieldDialogueVoice.cs:27 |
| gwp_fa_task_war_variant_1 | We cannot take enemy goods in war now? The Wardens reach too far. | 打仗还不能拿敌人的东西？灰袍未免管得太宽了。 | GwpFieldDialogueVoice.cs:28 |
| gwp_fa_task_war_variant_2 | That was an enemy caravan. I am fighting a war, not robbing travellers. | 那是敌人的商队。我在打仗，不是在拦路抢劫。 | GwpFieldDialogueVoice.cs:28 |
| gwp_fa_task_consequence_variant_1 | Suppose I refuse to pay today. What will you do? | 我今天就是不交，你打算怎么办？ | GwpFieldDialogueVoice.cs:29 |
| gwp_fa_task_consequence_variant_2 | I will take my men and ride on. Will the Wardens follow me forever? | 我带着人走我的路。灰袍还能一直追着我不成？ | GwpFieldDialogueVoice.cs:29 |
| gwp_fa_task_record_variant_1 | The Wardens already took me once. Was that not enough? | 灰袍以前已经抓过我一次了，还不够吗？ | GwpFieldDialogueVoice.cs:30 |
| gwp_fa_task_record_variant_2 | The Wardens again. Must your people follow wherever I go? | 又是灰袍。难道我往后走到哪里，你们都要跟着？ | GwpFieldDialogueVoice.cs:30 |
| gwp_terms_final_objection_variant_1 | What do I gain by giving up all my terms? | 我把条件全让了，对我又有什么好处？ | GwpFieldDialogueVoice.cs:31 |
| gwp_terms_final_objection_variant_2 | Why should I accept the loss and pay everything you ask? | 照你说的全部交清，我为什么要吃这个亏？ | GwpFieldDialogueVoice.cs:31 |
| gwp_fa_open_under_charge_variant_1 | You work for the Wardens now? That is new. What do you want? | 你也替灰袍办事了？这倒新鲜。找我什么事？ | GwpFieldDialogueVoice.cs:32 |
| gwp_fa_open_under_charge_variant_2 | I have heard of you. Tell me what the Wardens sent you for. | 听过你的名声。先说吧，灰袍让你来干什么？ | GwpFieldDialogueVoice.cs:32 |
| gwp_fa_open_respected_known_variant_1 | You. My last meeting with the Wardens was no pleasure. What is it now? | 是你。上次见到灰袍可不是什么好事，这回又怎么了？ | GwpFieldDialogueVoice.cs:33 |
| gwp_fa_open_respected_known_variant_2 | The Wardens want me again? Speak. I am listening. | 灰袍又找上我了？说吧，我听着。 | GwpFieldDialogueVoice.cs:33 |
| gwp_fa_open_respected_variant_1 | I have heard of you. State your business. | 你的名声我听过。有什么事，直说吧。 | GwpFieldDialogueVoice.cs:34 |
| gwp_fa_open_respected_variant_2 | If the Wardens sent you, there must be business to discuss. Go on. | 灰袍派你来，看来是有事要谈。说吧。 | GwpFieldDialogueVoice.cs:34 |
| gwp_fa_open_known_variant_1 | I have not forgotten being taken by your people. What do you want this time? | 上次被你们带走的滋味，我可没忘。这回要做什么？ | GwpFieldDialogueVoice.cs:35 |
| gwp_fa_open_known_variant_2 | Stopping me again? What do the Wardens want now? | 又拦我？灰袍这次有什么事？ | GwpFieldDialogueVoice.cs:35 |
| gwp_fa_desire_grace | Give me three days to raise the money. Come back then and I will answer for the payment. | 给我三天筹钱。到时候你再来，这笔罚金我会给你个交代。 | GwpOffenderDesire.cs:132 |
| gwp_fa_accept_grace | Three days, once only. I will return for payment. | 三天，只宽限这一次。我会回来收钱。 | GwpOffenderDesire.cs:158 |
| gwp_voice_grace_alt | I need three days to gather the money. I am asking for time, not forgiveness. | 我需要三天把钱凑起来。我求的是宽限，不是免掉罚金。 | GwpFieldDialogueVoice.cs:61 |
| gwp_grace_granted | You allowed {VAR_1} three days to raise the fine. The commission remains open; return to collect it or take the offender into custody. | 你给了{VAR_1}三天筹措罚金。委托尚未完成，届时仍须收钱或拿人。 | GwpFieldArrestBehavior.Grace.cs:31 |
| gwp_grace_due | The time you allowed {VAR_1} has expired. Seek payment or take him into custody; no payment has been received automatically. | 你给{VAR_1}的宽限已经到期。可以前去收钱或拿人。 | GwpFieldArrestBehavior.Grace.cs:48 |
| gwp_fa_grace_wait | Then I will come back on the day we named. | 那我就等到说好的那天再来。 | GwpFieldArrestBehavior.cs:141 |
| gwp_grace_return_early | You gave me until the day we named. It has not come yet. I have nothing ready for you before then. | 说好的日子还没到。到那天之前，我这儿拿不出什么给你。 | GwpFieldArrestBehavior.cs:454 |
| gwp_grace_return_due | The time we agreed is up. Let us see what I can pay now. | 说好的日子到了。你看看我现在能拿出多少吧。 | GwpFieldArrestBehavior.cs:455 |
| gwp_grace_collect | Show me what you can pay now. | 把现在能交的钱拿出来看看。 | GwpFieldArrestBehavior.cs:138 |
| gwp_terms_requestgrace_lead | The Wardens expect payment, not another delay. Show me what you have now. | 灰袍等的是罚金，不是再拖几天。先把你眼下能交的拿出来。 | GwpFieldArrestLines.cs:194 |
| gwp_terms_requestgrace_charm | Let us settle what you can today. There is no need to leave this hanging over your people. | 今天能交多少就先解决多少，别让这件事一直压着你和部下。 | GwpFieldArrestLines.cs:195 |
| gwp_terms_requestgrace_rogue | Three days to disappear? I would rather see the money you carry now. | 三天，够你走出多远？我更想先看看你现在的钱袋。 | GwpFieldArrestLines.cs:196 |
| gwp_terms_requestgrace_trade | Waiting costs both our parties. Pay what you can now and save another journey. | 你我再跑一趟都要花钱。今天把能付的付了，少耽误些工夫。 | GwpFieldArrestLines.cs:197 |
| gwp_rebuff_requestgrace_lead | I asked for time to raise it. Repeating the amount will not fill my purse today. | 我没说不交。我说的是筹钱需要时间。 | GwpFieldArrestLines.cs:252 |
| gwp_rebuff_requestgrace_charm | I asked for time to raise it. Repeating the amount will not fill my purse today. | 我也想今天就了结，可你总得容我把钱凑来。 | 未检出直接引用；保留 |
| gwp_rebuff_requestgrace_rogue | I asked for time to raise it. Repeating the amount will not fill my purse today. | 你不信我，我也没办法。但催得再紧，钱也不会自己出现。 | 未检出直接引用；保留 |
| gwp_rebuff_requestgrace_trade | I asked for time to raise it. Repeating the amount will not fill my purse today. | 再跑一趟是麻烦，可现在让我凑齐，同样办不到。 | 未检出直接引用；保留 |
| gwp_asset_gold | Gold | 第纳尔 | GwpAssetPayment.cs:222 |
| gwp_asset_value | {VAR_1} ({VAR_2} denars each) | {VAR_1}（每件 {VAR_2} 第纳尔） | GwpAssetPayment.cs:235 |
| gwp_duel_payment_pending | You win the duel. You may now collect his fine. | 你赢了决斗，可以收缴他的罚金。 | GwpFieldArrestBehavior.cs:1117 |
| gwp_duel_inspect_property | Show me your property. We will settle the fine now. | 把你的财物拿出来，现在结清罚金。 | GreyWardenSparringBehavior.cs:92, GwpFieldArrestBehavior.cs:135 |
| gwp_duel_collect_later | I will collect it later. | 我稍后再来收。 | GreyWardenSparringBehavior.cs:95 |
| gwp_duel_return_collect | You won our duel. I still owe you the payment we agreed. | 单挑是你赢了。照约定，我还欠着你这笔罚金。 | GwpFieldArrestBehavior.cs:448 |
| gwp_case_pay_any | I am ready to submit my report. Let us settle the account. | 我来交差，请清点结账。 | PlayerBountyBehavior.CaseSettlement.cs:457 |
| gwp_case_resume_report | About the shortfall in what I handed over... | 刚才交的钱物还差一些，我来说明原因。 | PlayerBountyBehavior.CaseSettlement.cs:460 |
| gwp_case_nothing_concealed | That is what he put in your hands, and all of it is here. Your report will be entered. | 他交到你手上的就是这些，一分不少地都在这儿了。你的汇报会照实登记。 | PlayerBountyBehavior.CaseSettlement.cs:480 |
| gwp_case_report_kept_result | The {VAR_1} denars are entered. Your expenses are {VAR_2} denars. The commission is concluded. | 这 {VAR_1} 第纳尔已经登记。你的办案费是 {VAR_2} 第纳尔。委托就此结清。 | PlayerBountyBehavior.CaseSettlement.cs:594 |
| gwp_voice_submit_upright_0 | The charge is fair. Take it, and let it be written that I paid it standing. | 这笔罚是公道的。拿去吧，记上一笔：我是站着交的。 | GwpFieldDialogueVoice.cs:115 |
| gwp_voice_submit_upright_1 | I will not argue a debt I owe. Count it. | 我欠的账我不争。点吧。 | GwpFieldDialogueVoice.cs:115 |
| gwp_voice_submit_upright_2 | Then it is owed, and I pay what is owed. Here. | 那就是欠着的，欠着的我就还。给。 | GwpFieldDialogueVoice.cs:115 |
| gwp_voice_submit_cold_0 | Cheaper than the alternative. That is the only reason, and we both know it. | 比另一条路便宜。就这一个理由，你我都清楚。 | GwpFieldDialogueVoice.cs:116 |
| gwp_voice_submit_cold_1 | The sum is smaller than the trouble. Take it. | 这个数比麻烦小。拿走。 | GwpFieldDialogueVoice.cs:116 |
| gwp_voice_submit_cold_2 | I have weighed it. You win this column. Count your money. | 我算过了。这一栏是你赢。点你的钱去。 | GwpFieldDialogueVoice.cs:116 |
| gwp_voice_submit_soft_0 | Take it. It buys nothing back, but take it. | 拿去吧。什么也换不回来，但还是拿去吧。 | GwpFieldDialogueVoice.cs:117 |
| gwp_voice_submit_soft_1 | If this closes the matter for them, then take it. | 要是这样能给他们一个了断，那就拿去。 | GwpFieldDialogueVoice.cs:117 |
| gwp_voice_submit_soft_2 | Here. I would rather it went to them than stayed with me. | 给。与其留在我手里，不如给他们。 | GwpFieldDialogueVoice.cs:117 |
| gwp_voice_submit_tight_0 | Every coin of it. I hope your order chokes on the counting. | 一个子儿不少。但愿你们那个修会点钱噎着。 | GwpFieldDialogueVoice.cs:118 |
| gwp_voice_submit_tight_1 | There. Count it twice, you will not find a coin short. | 在这儿。点两遍，也不会少一个子儿。 | GwpFieldDialogueVoice.cs:118 |
| gwp_voice_submit_tight_2 | Take it and be quick. Every moment of this costs me more. | 拿了就快走。这事多拖一刻我就多亏一刻。 | GwpFieldDialogueVoice.cs:118 |
| gwp_voice_submit_fierce_0 | Fine. Money, then. Do not mistake it for fear. | 行，给钱。别当成我怕了你。 | GwpFieldDialogueVoice.cs:119 |
| gwp_voice_submit_fierce_1 | Take your coin. It settles nothing between us. | 拿你的钱。你我之间什么也没了结。 | GwpFieldDialogueVoice.cs:119 |
| gwp_voice_submit_fierce_2 | Here, and let that be the end of your talking. | 给，你也别再说了。 | GwpFieldDialogueVoice.cs:119 |
| gwp_voice_submit_plain_0 | Here. Count it, and be gone. | 在这儿。点清楚，然后走。 | GwpFieldDialogueVoice.cs:120 |
| gwp_voice_submit_plain_1 | All right. The sum you named, no more. | 好吧。你报的那个数，一分不多。 | GwpFieldDialogueVoice.cs:120 |
| gwp_voice_submit_plain_2 | Take it. I want no further trouble over this. | 拿去。这事我不想再有麻烦。 | GwpFieldDialogueVoice.cs:120 |
| gwp_voice_plead_cold_0 | You have named a sum I cannot meet. Wages, feed, remounts - look at the column and tell me where it hides. | 你开的这个数我拿不出来。军饷、草料、换马——你指给我看它藏在哪一栏。 | GwpFieldDialogueVoice.cs:123 |
| gwp_voice_plead_cold_1 | The figure is beyond me. I can show you the accounts if you doubt it. | 这个数超出我的能耐。你要是不信，账本可以给你看。 | GwpFieldDialogueVoice.cs:123 |
| gwp_voice_plead_cold_2 | What you ask does not exist in my purse. That is simply the arithmetic. | 你要的东西不在我钱袋里。这就是个算术问题。 | GwpFieldDialogueVoice.cs:123 |
| gwp_voice_plead_tight_0 | I am not a rich man, whatever your ledger says. You will not find it on me. | 我不是什么富人，你们卷宗上怎么写是你们的事。这钱你在我身上找不到。 | GwpFieldDialogueVoice.cs:124 |
| gwp_voice_plead_tight_1 | Search me if you like. There is nothing like that sum here. | 你愿意搜就搜。这儿没有那么多。 | GwpFieldDialogueVoice.cs:124 |
| gwp_voice_plead_tight_2 | You have been told wrong about my means. I cannot pay it. | 有人把我的家底跟你说岔了。我付不出。 | GwpFieldDialogueVoice.cs:124 |
| gwp_voice_plead_upright_0 | I will not promise what I cannot pay. That sum is beyond me. | 我不会答应我付不出的东西。这个数我够不着。 | GwpFieldDialogueVoice.cs:125 |
| gwp_voice_plead_upright_1 | I would pay it if I had it. I do not. | 有的话我就付了。我没有。 | GwpFieldDialogueVoice.cs:125 |
| gwp_voice_plead_soft_0 | I have men to feed before I have coin for you. There is nothing left. | 我得先让手下的人吃上饭，才轮到你的钱。已经没剩下什么了。 | GwpFieldDialogueVoice.cs:126 |
| gwp_voice_plead_soft_1 | Do not take what these people still need. I cannot meet it. | 别拿这些人还要用的东西。这个数我凑不出。 | GwpFieldDialogueVoice.cs:126 |
| gwp_voice_plead_fierce_0 | I have no such money, and I will not pretend otherwise. | 我没有这笔钱，也不打算装作有。 | GwpFieldDialogueVoice.cs:127 |
| gwp_voice_plead_fierce_1 | Ask for what I have, not for what I do not. | 按我有的要，别按我没有的要。 | GwpFieldDialogueVoice.cs:127 |
| gwp_voice_plead_plain_0 | That sum? I do not carry it. Search the baggage if you like. | 那个数？我身上没带。你想翻辎重就翻。 | GwpFieldDialogueVoice.cs:128 |
| gwp_voice_plead_plain_1 | I cannot raise that. Not today, not from what I have here. | 这个数我凑不出来。今天不行，就凭我手上这点更不行。 | GwpFieldDialogueVoice.cs:128 |
| gwp_voice_resist_upright_0 | I will not pay for a charge I do not accept. Do what you came to do. | 我不认的罪，我不会掏钱。你们要办就办吧。 | GwpFieldDialogueVoice.cs:131 |
| gwp_voice_resist_upright_1 | Bring me before someone with the standing to judge it. Until then, no. | 把我带到有资格裁断这件事的人跟前。在那之前，不行。 | GwpFieldDialogueVoice.cs:131 |
| gwp_voice_resist_fierce_0 | I have never bought my way out of anything. Draw. | 我从来没花钱买过平安。拔刀。 | GwpFieldDialogueVoice.cs:132 |
| gwp_voice_resist_fierce_1 | You will take nothing from me while I stand. Come on, then. | 我站着的时候，你从我身上拿不走任何东西。来吧。 | GwpFieldDialogueVoice.cs:132 |
| gwp_voice_resist_fierce_2 | Enough talk. Let the swords settle it. | 话说够了。让刀来分。 | GwpFieldDialogueVoice.cs:132 |
| gwp_voice_resist_cold_0 | I have counted your column and I have counted mine. No. | 你那一列我数过，我这一列我也数过。不行。 | GwpFieldDialogueVoice.cs:133 |
| gwp_voice_resist_cold_1 | The odds do not favour you enough. My answer is no. | 这个胜算还不够你张这个口。我的答复是不。 | GwpFieldDialogueVoice.cs:133 |
| gwp_voice_resist_cold_2 | No. You may test that answer if you wish. | 不。你愿意的话，可以试试这个答复。 | GwpFieldDialogueVoice.cs:133 |
| gwp_voice_resist_tight_0 | You will not have a denar of it. Not one. | 一个第纳尔你也别想拿到。一个都没有。 | GwpFieldDialogueVoice.cs:134 |
| gwp_voice_resist_tight_1 | I would sooner bleed than pay that. Try me. | 要我交那个数，我宁可流血。你试试。 | GwpFieldDialogueVoice.cs:134 |
| gwp_voice_resist_tight_2 | Not a coin. Take it off my body if you can. | 一个子儿也没有。有本事从我尸首上摸。 | GwpFieldDialogueVoice.cs:134 |
| gwp_voice_resist_soft_0 | I cannot give you what my people still need. I will not. | 我的人还要用的东西，我不能给你。我不给。 | GwpFieldDialogueVoice.cs:135 |
| gwp_voice_resist_soft_1 | I am sorry it comes to this, but my answer is no. | 走到这一步我也难过，但我的答复是不。 | GwpFieldDialogueVoice.cs:135 |
| gwp_voice_resist_plain_0 | No. Come and take it, if you can. | 不。有本事就来拿。 | GwpFieldDialogueVoice.cs:136 |
| gwp_voice_resist_plain_1 | My answer is no. Do as you must. | 我的答复是不。你该怎么办就怎么办。 | GwpFieldDialogueVoice.cs:136 |
| gwp_voice_resist_scorn_0 | You are under your own charge and you come to collect mine? Draw. | 你自己头上还挂着案子，倒来收我的？拔刀吧。 | GwpFieldDialogueVoice.cs:137 |
| gwp_voice_resist_scorn_1 | A wanted man collecting fines. I will not answer to you. | 一个通缉犯跑来收罚金。我不向你交代。 | GwpFieldDialogueVoice.cs:137 |
| gwp_voice_resist_scorn_2 | Put your own name right before you read mine off a list. | 先把你自己的名字弄干净，再照单子念我的。 | GwpFieldDialogueVoice.cs:137 |
| gwp_case_short_question | There are still matters to explain in this case. Tell me what happened. | 这宗案子还有需要交代的地方。说说经过。 | PlayerBountyBehavior.CaseSettlement.cs:469, PlayerBountyBehavior.CaseSettlement.cs:484 |
| gwp_receipt_unknown | Choose what to hand over. | 请选择要上交的钱物。 | PlayerBountyBehavior.CaseSettlement.cs:633 |
| gwp_case_short_truth | Tell the truth | 如实交代 | PlayerBountyBehavior.CaseSettlement.cs:486 |
| gwp_case_short_lie | Lie | 撒谎 | PlayerBountyBehavior.CaseSettlement.cs:488 |
| gwp_collection_agreement | Honor our agreement | 履行约定 | GwpAssetPayment.cs:151 |
| gwp_request_case_support | I need your help taking the offender. Take the field with me. | 我需要你们帮忙拿下那个人。跟我一起上。 | PlayerBountyBehavior.Support.cs:162 |
| gwp_request_case_support_reply | Then let us ride. We stay at your shoulder, and we will not be behind you when it starts. | 那就走吧。我们跟着你，动起手来不会落在你后面。 | PlayerBountyBehavior.Support.cs:165 |
| gwp_support_requested_log | You have asked the Wardens to ride with you. | 你已请灰袍出兵相助。 | PlayerBountyBehavior.Support.cs:82 |
| gwp_offer_changed | What you hand over does not match the agreement. Adjust it and try again. | 交付的钱物与约定不符，请重新调整。 | GwpAssetOfferValidationPatch.cs:29 |
| gwp_case_defeat_recorded | You broke {VAR_1} in the field. Go and make your report. | 你在野外击溃了{VAR_1}。回去复命吧。 | PlayerBountyBehavior.CaseSettlement.cs:134 |
| gwp_case_truthful_defeat_result | — | 你在野外把他打垮了。实收的 {VAR_1} 第纳尔已经入账，余款记在他名下。花销 {VAR_2} 第纳尔，委托结清。 | 未检出直接引用；保留 |
| gwp_bounty_contract_expired_but_done | The time for the hunt has passed, but the target has been dealt with. Report to any Warden lord. | 追捕期限已过，但目标已受惩处。向任意一位灰袍领主复命即可。 | PlayerBountyBehavior.cs:250 |
| gwp_report_excess_found | {VAR_1} had already answered for his crimes, yet you struck him down. The Wardens have taken note. | 灰袍查明{VAR_1}已受过惩处，你仍对他动了手。 | GwpFieldReportLedger.cs:349 |
| gwp_support_granted_notice | {VAR_1} has agreed and is on her way to join you. | {VAR_1}已应允，正赶来与你会合。 | PlayerBountyBehavior.Support.cs:89 |
| gwp_support_player_war | The Wardens declare war on {VAR_1}, and you stand with them. | 灰袍向{VAR_1}宣战，你随之参战。 | PlayerBountyBehavior.Support.cs:154 |
| gwp_dispatch_talk_hint | Send these men on an errand | 派这些人去办事 | GwpPartyScreenTroopTalkPatch.cs:43 |
| gwp_dispatch_greeting | Commander. Say the word and we ride. | 指挥官。您吩咐，我们就走。 | GwpWardenDispatchDialogue.cs:159 |
| gwp_dispatch_report | Take our account of the commission to the Wardens for me. | 替我把这桩委托的账送到灰袍那里。 | GwpWardenDispatchDialogue.cs:163 |
| gwp_dispatch_support | Ride to the Wardens and ask them to take the field with me. | 去灰袍那里，请他们与我一同出阵。 | GwpWardenDispatchDialogue.cs:167 |
| gwp_dispatch_never_mind | Nothing for now. Back to your post. | 暂时没事，回岗位去吧。 | GwpWardenDispatchDialogue.cs:179 |
| gwp_dispatch_confirm | Then pick the men who ride with me, Commander. | 那就请指挥官点出随我上路的人。 | GwpWardenDispatchDialogue.cs:183 |
| gwp_dispatch_screen_report | Courier detail | 送信队 | GwpWardenDispatchDialogue.cs:267 |
| gwp_dispatch_screen_support | Request rider | 求援骑手 | GwpWardenDispatchDialogue.cs:268 |
| gwp_dispatch_pick_someone | Choose at least one soldier. | 至少选择一名士兵。 | GwpWardenDispatchDialogue.cs:258, GwpWardenDispatchDialogue.cs:428, GwpWardenDispatchDialogue.cs:455 |
| gwp_dispatch_name_report | Grey Warden courier detail | 灰袍送信队 | GwpWardenDispatchBehavior.cs:308 |
| gwp_dispatch_name_support | Grey Warden request rider | 灰袍求援骑手 | GwpWardenDispatchBehavior.cs:309 |
| gwp_dispatch_no_receiver | There is no Warden company within reach to receive them. | 附近没有可以接收的灰袍队伍。 | GwpWardenDispatchBehavior.cs:203, GwpWardenDispatchDialogue.cs:352 |
| gwp_dispatch_no_spare_ship | You are at sea with no ship to spare. They can set out once you reach land. | 你在海上，没有多余的船，靠岸后才能派出。 | GwpWardenDispatchBehavior.cs:211 |
| gwp_dispatch_no_rations | — | 你既拿不出干粮，也拿不出买粮的钱。先备点东西，再派人出门。 | 未检出直接引用；保留 |
| gwp_dispatch_sent | The company you sent has set out for {VAR_1}. | 你派出的队伍已出发前往{VAR_1}处。 | GwpWardenDispatchBehavior.cs:301 |
| gwp_dispatch_lost | The company you sent out was destroyed, and everything it carried is lost. | 你派出的队伍全军覆没，所带财物尽失。 | GwpWardenDispatchBehavior.cs:177 |
| gwp_dispatch_returning | The company you sent has done its work and is returning. | 你派出的队伍已办完差事，正在返回。 | GwpWardenDispatchBehavior.cs:731 |
| gwp_dispatch_prisoners_sold | Your men sold {VAR_1} prisoners in {VAR_2} for {VAR_3} denars. | 你的队伍在{VAR_2}卖出 {VAR_1} 名俘虏，得 {VAR_3} 第纳尔。 | GwpWardenDispatchBehavior.cs:100 |
| gwp_dispatch_support_moot | The message was delivered, but help is no longer needed. | 信已送到，但此事已无需援手。 | GwpWardenDispatchBehavior.cs:627 |
| gwp_dispatch_handover | Your detachment has rejoined you: {VAR_1} men ({VAR_2} wounded), {VAR_3} prisoners, {VAR_4} items and {VAR_5} denars returned to your party. | 派出的队伍已经归队：交回 {VAR_1} 人（其中 {VAR_2} 名伤员）、{VAR_3} 名俘虏、{VAR_4} 件物资和 {VAR_5} 第纳尔。 | GwpWardenDispatchBehavior.cs:847 |
| gwp_dispatch_report_done | The commission is settled. Your men are returning with {VAR_1} denars. | 委托已结清，你的队伍正带着 {VAR_1} 第纳尔赏金返回。 | PlayerBountyBehavior.CaseSettlement.cs:361 |
| gwp_dispatch_prisoner_failed | The handover failed. The prisoner is still in your hands. | 押送未能交接，俘虏仍在你手中。 | GwpWardenDispatchBehavior.cs:259 |
| gwp_dispatch_manifest_offer | Load {VAR_1} / {VAR_2} | 装载 {VAR_1} / {VAR_2} | GwpAssetOfferValidationPatch.cs:92 |
| gwp_dispatch_word_title | What your men are to say | 让他们怎么说 | GwpWardenDispatchDialogue.cs:396 |
| gwp_dispatch_word_body | They will hand over {VAR_1} of the {VAR_2} on the account. What account do they give of the rest? | 案子应缴 {VAR_2}，这次钱货合计 {VAR_1}。办案经过，让他们怎么交代？ | GwpWardenDispatchDialogue.cs:398 |
| gwp_dispatch_word_truth | Tell the truth | 如实交代 | GwpWardenDispatchDialogue.cs:401 |
| gwp_dispatch_word_lie | Lie | 撒谎 | GwpWardenDispatchDialogue.cs:402 |
| gwp_common_confirm | — | 确认 | 未检出直接引用；保留 |
| gwp_common_cancel | — | 取消 | 未检出直接引用；保留 |
| gwp_dispatch_report_moot | The commission is already settled. What your men carried is coming back to you. | 委托已经结清，所带财物将原样带回。 | GwpWardenDispatchBehavior.cs:679 |
| gwp_bounty_case_is_yours | The bounty is yours. If you need help, send word to the Wardens. | 你接下了这桩悬赏。需要援手时可派人向灰袍求援。 | PlayerBountyBehavior.DialogueAndNotification.cs:827 |
| gwp_support_no_responder | No Warden lord can come just now. | 眼下没有灰袍领主能够前来。 | PlayerBountyBehavior.Support.cs:56, PlayerBountyBehavior.Support.cs:73 |
| gwp_bounty_intel_lost | [Investigation] The scouts have lost the trail. There is no sighting to report this time. | 【侦情】探子跟丢了，这一次没有目击可报。 | PlayerBountyBehavior.cs:680 |
| gwp_case_withdrawn_result | The man was already beyond reach. The treasury covers {VAR_1} denars for your trouble. The commission is closed. | 那个人早就够不着了。公库付你 {VAR_1} 第纳尔，算这趟的花销。委托结清。 | PlayerBountyBehavior.CaseSettlement.cs:607 |
| gwp_case_report_withdrawn_summary | We have word that the man is beyond reach. Say the word and I will settle your expenses. | 我们这边已经得到消息，那个人够不着了。你说一声，我把你的花销结了。 | PlayerBountyBehavior.CaseSettlement.cs:505 |
| gwp_case_owner_player | Tracking: {VAR_1} &#124; Stage: commissioned to the player | 追踪情况：{VAR_1} &#124; 当前阶段：已交由玩家承办 | GwpCaseArchiveScreen.cs:781 |
| gwp_case_owner_player_supported | Tracking: {VAR_1} &#124; Supporting: {VAR_2} &#124; Stage: {VAR_3} | 追踪情况：{VAR_1} &#124; 支援：{VAR_2} &#124; 当前阶段：{VAR_3} | GwpCaseArchiveScreen.cs:787 |
| gwp_dispatch_trouble_battle | Word reaches you: the men you sent have been brought to battle. | 有消息传来：你派出去的人被堵上了，正在交战。 | GwpWardenDispatchBehavior.cs:384 |
| gwp_dispatch_trouble_mauled | Word reaches you: the men you sent have been badly cut up. | 有消息传来：你派出去的人伤亡过半。 | GwpWardenDispatchBehavior.cs:386 |
| gwp_dispatch_trouble_starving | — | 有消息传来：你派出去的人已经断粮。 | 未检出直接引用；保留 |
| gwp_case_settle_expenses | The commission is closed. We will settle what you are owed. | 这桩委托已经了结，把该付你的报酬结清就行。 | PlayerBountyBehavior.CaseSettlement.cs:464 |
| gwp_troop_zealot | Grey Warden Zealot | 灰袍狂信者 | 兵种树、名册、战场兵种名；现有二阶步兵。 |
| gwp_fs_priest_name | Grey Warden Priest {FIRSTNAME} | 灰袍教士{FIRSTNAME} | 村中教士姓名、名牌与百科链接。 |
| gwp_fs_priest_arrived | {PRIEST} has settled in {VILLAGE} to preach. | {PRIEST}已在{VILLAGE}定居传教。 | 新教士在村中落脚时的左下角灰色提示。 |
| gwp_fs_ritual_congregation_title | Congregation | 布道集会 | 布道进行时进村，仪式菜单标题。 |
| gwp_fs_ritual_damnation_title | Burning at the Stake | 火刑 | 火刑期间进村，仪式菜单标题。 |
| gwp_fs_ritual_congregation_text | The villagers of {VILLAGE} have gathered to hear the commandments, and new believers are taking their vows. Village business is suspended until the congregation ends, in about {HOURS} hours. Host: {HOST}. | {VILLAGE}的村民聚在广场上听讲戒律，新入教的人正在宣誓。集会尚未结束，暂时无法办理村中事务。还需约{HOURS}小时。主持：{HOST}。 | 布道期间的锁村画面；HOST无教士时会回退成村名，故使用中性的“主持方”。 |
| gwp_fs_ritual_damnation_text | People are bound to stakes in the square of {VILLAGE}, where a burning is under way. The gathered believers pray as smoke drifts over the roofs. Village business is suspended for about {HOURS} more hours. Host: {HOST}. | {VILLAGE}的广场上正在执行火刑。人被绑在木架上，围观的教众齐声祷告，浓烟飘过屋顶。村中事务暂时停办，还需约{HOURS}小时。主持：{HOST}。 | 火刑期间的锁村画面；不把火写成能裁定罪名的神，也不把实际被烧者一律称为罪犯。 |
| gwp_fs_village_option | Grey Warden faithful: {HOUSEHOLDS} households ({SHARE}%), {GUARD} defenders | 灰袍教众：{HOUSEHOLDS}户（{SHARE}%），守村{GUARD}人 | 有灰袍教众记录时的村庄菜单入口。 |
| gwp_fs_village_info | Of the {HEARTH} households in {VILLAGE}, {HOUSEHOLDS} follow the Grey Wardens' rule, and {GUARD} believers defend the village. They worship Heaven, hear the priest's teachings, and provide recruits for the Wardens. | {VILLAGE}共有{HEARTH}户，其中{HOUSEHOLDS}户奉行灰袍的戒律，{GUARD}名教众负责守村。他们信奉上天，听从教士的教诲，也为灰袍提供兵员。 | 点击村庄教众入口后的详情正文。 |
| gwp_fs_village_priest | {PRIEST} preaches here and tends to the faithful. When the Wardens recruit, he introduces villagers willing to serve. | {PRIEST}在此传教，并照管教众。灰袍来征募时，村中愿意从军的人由他引荐。 | 教众详情中存在在世教士的分支。 |
| gwp_fs_village_no_priest | There is currently no Grey Warden priest in the village. | 村中暂时没有灰袍教士。 | 教众详情中没有在世教士的分支。 |
| gwp_fs_village_info_title | The Grey Warden Faithful | 灰袍教众 | 村庄教众详情弹窗标题。 |
| gwp_fs_village_info_ok | Close | 关闭 | 村庄教众详情弹窗关闭按钮。 |
| gwp_fs_ritual_leave | Leave... | 离开…… | 仪式锁村画面的离开按钮。 |
| gwp_fs_militia_change | The Grey Warden faithful remain on watch | 灰袍教众继续留守 | 民兵每日变化的说明条目：抵消灰袍守村兵对应的原版退役项。 |
| gwp_fs_militia_stopped | The Grey Warden faithful guard the village; no new militia is raised | 灰袍教众负责守卫，停止征募新民兵 | 教士势力已达本村最强时，民兵变化的说明条目。 |
| gwp_fs_priest_lore | {PRIEST} teaches the Grey Wardens' rule in {VILLAGE}. He urges villagers to revere Heaven, aid the poor, and submit to the order's discipline. The Wardens' women command its troops and patrols; he gathers the faithful, conducts worship, and helps provide for those who defend the village or join the ranks. | {PRIEST}在{VILLAGE}传授灰袍的戒律。他教导村民敬畏上天，接济穷人，并服从修会的惩戒。灰袍的女统领掌管武装和巡察，他则召集教众、主持礼拜，为守村与从军的人备办衣食。 | 村中灰袍教士的百科介绍；宗教与分工说明。 |
| gwp_fs_village_stores | The cellar holds {PRISONERS} people awaiting the stake. The stores contain {ARMORY} pieces of arms and armour, with goods worth {GOODS} denars awaiting transport to market. | 地窖关押着{PRISONERS}人，等候火刑。库房存有{ARMORY}件兵器甲胄，另有价值{GOODS}第纳尔的货物待运往集市。 | 村庄教众详情最后一段，显示地牢、军械与货物。 |
| gwp_fs_caravan_party | Faithful Traders of {VILLAGE} | {VILLAGE}的教众货队 | 卖货队的大地图队名。 |
| gwp_fs_armory_option | Inspect the faithful's stores | 查看教众库房 | 仅开发版：有守村队的村庄菜单库房入口。 |
| gwp_fs_armory_close | Close | 关闭 | 仅开发版：库房明细关闭按钮。 |
| gwp_fs_armory_section_arms | Arms and Armour | 军械 | 仅开发版：库房明细中的军械分组。 |
| gwp_fs_armory_section_goods | Goods for Sale | 待售货物 | 仅开发版：村庄库房明细中的货仓分组。 |
| gwp_fs_armory_section_inventory | Baggage | 随队物资 | 仅开发版：领主库房明细中的背包分组。 |
| gwp_fs_armory_section | {TITLE} ({COUNT} pieces, worth {VALUE}) | {TITLE}（{COUNT}件，价值{VALUE}） | 仅开发版：库房每个分组的数量与估值标题。 |
| gwp_fs_lord_armory_ask | Let me see your company's stores. | 让我看看你们的随队物资。 | 仅开发版：与带队灰袍领主正常交谈时查看物资的玩家选项。 |
| gwp_fs_lord_armory_reply | Here is the inventory. | 清单在这里。 | 仅开发版：领主回复后打开库房明细。 |
| gwp_fs_armory_item | {NAME} × {COUNT} | {NAME} × {COUNT} | 仅开发版：库房物品逐项显示。 |
| gwp_fs_armory_village | Armory of {VILLAGE} ({ITEMS} pieces) | {VILLAGE}的军械库（{ITEMS}件） | 仅开发版：村庄库房弹窗标题。 |
| gwp_fs_armory_party | Armory of {PARTY} ({ITEMS} pieces) | {PARTY}的军械库（{ITEMS}件） | 仅开发版：领主部队库房弹窗标题。 |
| gwp_fs_armory_empty | The stores are empty. | 库房为空。 | 仅开发版：该持有者尚无军械账本时的说明。 |
| gwp_fs_armory_summary | These stores supply {MEN} people, of whom {ARMED} have weapons. Another {FREE} pieces are held in reserve, and {SURPLUS} surplus pieces will be sold at market. | 这里为{MEN}人保管装备，其中{ARMED}人已配有兵器；另存{FREE}件备用装备，{SURPLUS}件余货将运往集市出售。 | 仅开发版：库房摘要。MEN为配装记录数，ARMED为其中配有主手兵器的记录数。 |
| gwp_fs_party_pretalk | Anything else? | 还有什么事？ | 地图遇到教众队伍，买卖取消后回到主对话。 |
| gwp_fs_party_buy | What goods are you carrying? | 你们带了什么货？ | 只有卖货队有货、总价大于零且玩家付得起时显示的购买选项。 |
| gwp_fs_party_offer | We have {PRODUCTS}, for {TOTAL_PRICE}{GOLD_ICON} in all. The money will go home to support the faithful. | 有{PRODUCTS}，共计{TOTAL_PRICE}{GOLD_ICON}。这些钱要带回去供养教众。 | 卖货队报价，不把钱说成归某团火所有。 |
| gwp_fs_party_buy_yes | Agreed. Here is {TOTAL_PRICE}{GOLD_ICON}. | 成交，这是{TOTAL_PRICE}{GOLD_ICON}。 | 玩家确认买下整批货物。 |
| gwp_fs_party_buy_no | Never mind. | 算了。 | 玩家取消购买。 |
| gwp_fs_party_threaten | Hand over everything you have. | 把东西都交出来。 | 玩家威胁任意教众队伍。 |
| gwp_fs_party_refuse | We will not hand it over. Draw your sword if you mean to take it. | 我们不会交给你。要抢，就拔剑。 | 教众拒绝玩家威胁；没有交钱或投降分支。 |
| gwp_fs_party_fight | Then we have nothing more to discuss. | 那就没什么可谈的了。 | 威胁被拒后，玩家结束对话回到遭遇菜单；此项本身不直接发起战斗。 |
| gwp_fs_party_back_off | Leave, then. | 算了，你们走吧。 | 玩家收回威胁并离开遭遇。 |
| gwp_fs_party_leave | Carry on your way. | 你们继续赶路吧。 | 正常结束教众队伍对话并离开遭遇。 |
| gwp_fs_party_greet_cart_out | We come from {HOME}, taking these goods to sell in {TARGET}. The proceeds will support the faithful back home. | 我们从{HOME}来，要把这车货送到{TARGET}去卖，所得用来供养村里的教众。 | 地图遇到向集市出发的卖货队。 |
| gwp_fs_party_greet_mission | We come from {HOME}, bound for {TARGET} to preach and settle there. | 我们从{HOME}来，要到{TARGET}传教，此后就在那里住下。 | 地图遇到前往新村的传教队。 |
| gwp_fs_party_greet_balance | We come from {HOME}, carrying arms for the faithful of {TARGET}. Once they are delivered, we will stay to help guard the village. | 我们从{HOME}来，为{TARGET}的教众运送兵器。送到以后，我们也留在那里守村。 | 地图遇到运械队；抵达后人也留下。 |
| gwp_fs_mission_party | Grey Warden Mission | 灰袍传教队 | 传教队的大地图队名。 |
| gwp_fs_balance_party | Armsbearers of {VILLAGE} | {VILLAGE}的运械队 | 运械队的大地图队名。 |
| gwp_fs_status_balance | Carrying arms from {HOME} to the faithful of {TARGET}. | 正从{HOME}运送兵器给{TARGET}的教众。 | 运械队出发阶段的部队行为说明。 |
| gwp_notice_prisoners | Handed over {COUNT} prisoners | 交付{COUNT}名俘虏 | 领主向村庄地牢交人后，村庄名牌上的短通知。 |
| gwp_notice_donated | Donated {COUNT} items | 捐赠{COUNT}件物资 | 领主交物资后村庄名牌短通知；包含军械与货物，不全是装备。 |
| gwp_notice_congregation | {COUNT} households joined the Grey Warden faithful | {COUNT}户加入灰袍教众 | 布道完成后的村庄名牌通知；COUNT来自信众新增户数，原中文“人”单位不符。 |
| gwp_notice_damnation | {BURNED} were burned at the stake; {SWORN} more households joined the faith | {BURNED}遭火刑，另有{SWORN}户入教 | 火刑处理后的村庄名牌通知；BURNED由人数与户数片段组合。 |
| gwp_notice_burned_people | {COUNT} people | {COUNT}人 | 火刑通知中的俘虏与民兵人数片段。 |
| gwp_notice_burned_homes | {COUNT} households | {COUNT}户 | 火刑通知中的不信教户数片段，不改写成人数。 |
| gwp_notice_mission | {COUNT} believers settled here | {COUNT}名教众在此定居 | 传教队抵达后村庄名牌短通知；COUNT是实际留下的兵员人数。 |
| gwp_notice_balance | Delivered {COUNT} pieces of arms and armour | 运来{COUNT}件兵器甲胄 | 运械队抵达后的村庄名牌通知。 |
| gwp_notice_market | Sold {COUNT} goods | 售出{COUNT}件货物 | 卖货队在城镇卖货后的城镇名牌通知。 |
| gwp_notice_rebuilt | Rebuilt this village | 重建了这座村子 | 灰袍完成重建后的村庄名牌通知。 |
| gwp_notice_issue_solved | Resolved a petition | 处理了一件请愿 | 灰袍完成要人事务后的聚落名牌通知。 |
| gwp_fs_status_mission | Travelling to {TARGET} to preach. | 正前往{TARGET}传教。 | 传教队出发阶段的部队行为说明。 |
| gwp_fs_status_market | Taking goods from {HOME} to market in {TARGET}. | 正把{HOME}的货物运往{TARGET}集市。 | 卖货队出发阶段的部队行为说明。 |
| gwp_fs_status_returning | Returning to {HOME}. | 正返回{HOME}。 | 卖货队返回阶段的部队行为说明。 |
| gwp_fs_status_home | Keeping watch in {HOME}. | 驻守{HOME}。 | 已登记教众队处于本村阶段的部队行为说明。 |
| gwp_fs_status_visit | In {VILLAGE}: handed over {PRISONERS} prisoners, donated {DONATED} items, and recruited {RECRUITS} believers. | 在{VILLAGE}交付{PRISONERS}名俘虏、捐赠{DONATED}件物资，招募{RECRUITS}名教众。 | 灰袍领主在村庄停留且有本次交人、捐物或招兵记录时的行为说明。 |
| gwp_fs_status_healing | Tending the wounded in {VILLAGE}. | 在{VILLAGE}照料伤员。 | 灰袍领主在教众村庄停留、无本次交割记录且有伤兵时。 |
| gwp_fs_status_resting | Resting in {VILLAGE}. | 在{VILLAGE}休整。 | 灰袍领主在教众村庄停留、无本次交割记录且没有伤兵时。 |
| gwp_status_rebuilding | Rebuilding {VILLAGE} ({HOURS} hours left). | 正在重建{VILLAGE}（还剩{HOURS}小时）。 | GreyWardenVillageReconstructionBehavior.cs:138 |
| gwp_status_petition | Settling a petition in {SETTLEMENT} ({HOURS} hours left). | 正在{SETTLEMENT}处理请愿（还剩{HOURS}小时）。 | GreyWardenIssueResolutionBehavior.cs:110 |
| gwp_status_training | Drilling the Wardens' recruits. | 正在操练灰袍的新兵。 | GwpPartyDutyMovementPatch.cs:160 |
| gwp_fs_party_greet_cart_back | We have traded at market and are returning to {HOME}. | 我们在集市卖过货，正返回{HOME}。 | 地图遇到返村阶段的卖货队；返程可能仍有余货，故不再说全部售完。 |
| gwp_fs_party_products_separator | ,  | 、 | 货物列表与火刑人数/户数组合所用分隔符。 |
| gwp_fs_party_product | {COUNT} {ITEM} | {COUNT}件{ITEM} | 卖货队报价中的单个货物条目。 |
| gwp_fs_party_products_more |  and more | 等 | 卖货报价超过四种货物时追加的省略片段。 |
| gwp_fs_healing_hearth | Care from the Grey Warden faithful | 灰袍教众的照料 | 部队治疗速度的解释条目；这是加成来源，不是地图地点。 |
| gwp_envoy_party_name | Grey Warden envoy | 灰袍使者 | PoliceEnforcementBehavior.Envoy.cs:181 |
| gwp_envoy_duel_called | {VAR_1} has challenged {VAR_2} to settle the Wardens' charge at the tournament in {VAR_3}. | {VAR_1}向{VAR_2}提出，要在{VAR_3}的比武大会上了结灰袍的指控。 | PoliceEnforcementBehavior.Envoy.cs:566 |
| gwp_envoy_duel_warden_won | {VAR_1} placed above {VAR_2} at the tournament in {VAR_3}. {VAR_2} pays the Wardens in full. | {VAR_1}在{VAR_3}的比武大会上名次压过了{VAR_2}。{VAR_2}照数缴清了罚金。 | PoliceEnforcementBehavior.Envoy.cs:695 |
| gwp_envoy_duel_offender_won | {VAR_1} placed above {VAR_2} at the tournament in {VAR_3}, and the Wardens let him go as agreed. | {VAR_1}在{VAR_3}的比武大会上名次压过了{VAR_2}，灰袍依约放他离开。 | PoliceEnforcementBehavior.Envoy.cs:705 |
