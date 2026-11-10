using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 全局玩法参数（v3.0，跨关卡共享）：一口价抽卡（价格=池总价值，动态）/ 经济 / 开垦 / 升阶上限与通道。
    /// Inspector 直改，Play 即生效；关卡差异值（起始墨、池子）在 LevelConfig。
    /// ⚠️ 文件名铁律：类名与文件名一致（GameParams.cs）。
    /// </summary>
    [CreateAssetMenu(menuName = "WordWars/游戏参数", fileName = "GameParams")]
    public class GameParams : ScriptableObject
    {
        [Header("抽卡（v3.0 一口价·补满）")]
        [Tooltip("手牌上限（补满至该数）。抽卡花费 = 池内卡价值之和（运行时计算，无固定价）")]
        public int handSize = 5;

        [Header("经济")]
        [Tooltip("每波清空奖励墨")]
        public int waveClearBonus = 15;
        [Tooltip("卖出折扣：回墨 = 卡价值 × 此值 × 字位（0.5 = 半价回收）")]
        public double sellRate = 0.5;

        [Header("初始墨元进度（ADR-053，2026-10-20 用户定案）")]
        [Tooltip("广告退路参数位：0 = 不启用（当前口径 = 免费升级，只留「+」入口）；N = 每通关 N 关解锁 1 步（未接 SDK）")]
        public int inkCapUpgradeEveryLevels = 0;

        [Header("红铲造格（ADR-054，2026-10-20 用户定案；全黄底）")]
        [Tooltip("首价（墨）：在章图无格空位造一格空地")]
        public int redShovelBaseCost = 50;
        [Tooltip("每次递增（墨）：当前价 = 首价 + 本值 × 本章已买次数；章内跨关累积、章界重置")]
        public int redShovelCostStep = 50;
        [Tooltip("解锁关卡序号（levels 平铺索引：3 = ch1 L3；L0-L2 不显示按钮）")]
        public int redShovelUnlockLevel = 3;

        [Header("开垦")]
        public double farmSeconds = 15;
        [Tooltip("立即完成开垦的花费")]
        public int farmSpeedupCost = 25;

        [Header("墨塔产墨（ADR-027）")]
        [Tooltip("产墨间隔（秒）。每次产量 = 塔池条目 inkPerPulse（升阶 ×2/级）")]
        public float inkPulseSeconds = 5f;

        [Header("潮汐淹没（F2.2 / ADR-032，ch2）")]
        [Tooltip("塔被淹沉默倒计时（秒，黄底 GDD §3.4）：归零 = 塔消失（0 回收，撤离三选项的「不动」结局）")]
        public double submergeSeconds = 20;

        [Header("Boss 蛟转阶段（F5 / 06 §10.4，二L5）")]
        [Tooltip("蓄水预警秒数：1/3 血触发 → 洪水落地（蛟吼/晕染为表现降级档，此计时与文案必留——05 风险表）")]
        public float bossChargeSeconds = 5f;
        [Tooltip("洪水落地后的召唤暂停秒数（纯抢救窗口：蛟生兵停摆，玩家腾手拖塔向右/卖/应急布水塔）")]
        public float bossSummonPauseSeconds = 10f;
        [Tooltip("潮落总时长（黄底拨杆）：4 档退水步长 = 总长/4（右→左），首步在召唤暂停结束时落地。越长 = 塔淹死越多")]
        public float bossEbbSeconds = 60f;
        [Tooltip("蛟二阶段水中速乘（敌人草稿 ×1.5；一阶段 1.0 慢速悠游——洪水落地时运行时改写）")]
        public float bossPhase2WaterMult = 1.5f;

        [Header("升阶（v2.2 配对式）")]
        public int maxTowerLevel = 5;
        [Tooltip("伤害通道：×N/级（L5≈10.5×）")]
        public double damageGrowth = 1.8;
        [Tooltip("经济通道：×N/级（墨塔 1/2/4/8/16）")]
        public double inkGrowth = 2.0;

        [Header("弹道视觉（ADR-025 后白图=真实 1×1 单位）")]
        [Tooltip("弹道箭长（世界单位）。0.16 = 修复前（白图 8% 时代）1.98×0.6 的实显示尺寸")]
        public float arrowLength = 0.16f;
        [Tooltip("弹道箭宽（世界单位）")]
        public float arrowWidth = 0.05f;

        [Header("飘字")]
        [Tooltip("交互反馈文案字号（不能合成/升阶/开垦完成等；战斗数值飘字走代码固定小号 10）")]
        public float toastFontSize = 7f;

        [Header("F2.1 幽夜敌 tint（ch3）——11.8 曲线② 关键调参位")]
        [Tooltip("雾中敌字变暗系数（0-1，越小越暗；1 = 不变暗）。看得见但易漏的分寸就在这两个参数")]
        public float fogEnemyDim = 0.3f;
        [Tooltip("雾中敌字透明度（0-1；1 = 不透明）——与 dim 合成「深色半透明剪影」")]
        public float fogEnemyAlpha = 0.55f;

        [Header("F2.2 光-敌交互（ch3，06 §11.2）")]
        [Tooltip("蛾·光脆：光中所受伤害 × 此值（1.5 = 飞蛾扑火）")]
        public double lightVulnerableMult = 1.5;
        [Tooltip("蝠·光滞：光中速度 × 此值（0.7 = 灯塔压制）")]
        public double lightHinderedMult = 0.7;
        [Tooltip("狼·夜行：雾中速度 × 此值（1.3；入光还原 1.0）")]
        public double nocturnalFogMult = 1.3;
        [Tooltip("法塔·光内眩晕概率（0.2 = 20%；时长固定 = 灯阶数 ÷ 2，T1 0.5s→T5 2.5s 不外露）")]
        public double mageStunChance = 0.2;
        [Tooltip("枭·栖灯倒计时 = 此值 × 灯阶数（3：T1 灯 3s → T5 灯 15s 吞噬窗口；枭改动 2026-10-09：5→3 用户定案，自愈哨兵已备）")]
        public double perchSecondsPerLevel = 3;
        [Tooltip("枭·二圈移速乘（1.25 = +25%，黄底：二圈更凶——换端 + 残灯网自然涌现）")]
        public double owlPhase2SpeedMult = 1.25;
        [Tooltip("枭·氛围黑闪周期秒（存活期间每 N 秒闪 2 下；0 = 关闭；转阶段黑幕演出时让位）")]
        public double owlFlickerSeconds = 5;
        [Range(0f, 1f)]
        [Tooltip("枭·黑闪峰值黑度（1 = 全黑；0.6 = 夜幕氛围不瞎屏）")]
        public float owlFlickerAlpha = 0.6f;
        [Tooltip("潜行敌（蝠/猫）雾中色——褪色幽灵影（浅影级，区别于普通敌深影）")]
        public Color fogStealthColor = new Color(0.75f, 0.76f, 0.82f);
        [Tooltip("潜行敌雾中透明度（比普通敌更透——半藏感）")]
        [Range(0f, 1f)] public float fogStealthAlpha = 0.35f;

        [Header("F1.2 火山岩浆（ch4，06 §12.2——喷射引擎 F2.1/F2.3/F2.4 消费；全黄底）")]
        [Tooltip("波开始到岩浆落地的延迟（秒）——「每波开始后 ~4s 随机喷射」（06 §12.2）")]
        public float eruptionDelaySeconds = 4f;
        [Tooltip("落地前红闪预警时长（秒）——唯一预警·零文字（用户定案）；红闪期 = 撤离窗口（F2.2：移/卖/不动三选项）")]
        public float eruptionFlashSeconds = 5f;
        [Tooltip("路径格落点：本格敌人即时 burst 伤害（06 §12.2 黄底 15；空地无事、塔防格走灰烬结算 F2.3）")]
        public double lavaBurstDamage = 15;
        [Tooltip("红印（岩浆残留）DoT：站立/路过敌人每秒燃烧伤害（黄底 2——F4.1 燃烧体系落地前先走此通道）")]
        public double lavaMarkDps = 2;
        [Tooltip("红印持续秒数（消退后恢复；火蜥红印上加速 ×1.3 归 F5.1 敌标记位）")]
        public float lavaMarkSeconds = 10f;
        [Tooltip("F4.1 燃烧：每整秒一跳的伤害（06 §12.3 黄底 3——火/炮命中挂燃 3 秒；B 键调试直灌此值）")]
        public double burnDps = 3;
        [Tooltip("F4.1 燃烧持续秒数（重复触发刷新时长不叠加；黄底 3）")]
        public float burnSeconds = 3f;
        [Tooltip("F4.1/F7.4/F4.5 灰烬增益统一律：火/炮/烬站灰烬格 → **全部伤害**（直接攻击 + 燃烧跳伤）× 此倍率（「死灰复燃」06 §12.2 黄底 2 = +100%；用户 2026-10-18 变更：原只加成燃烧不加攻击伤害，体感≈零 → 扩到全伤）")]
        public double ashDamageMult = 2;
        [Tooltip("F5.2 燃烧易伤倍率：敌挂 burnVulnerable（鳄「水怪怕火」）被挂燃 → 燃烧伤害 × 此倍率（06 §12.4 黄底 2；施加时刻定格——与灰烬增益同律）")]
        public double burnVulnerableMult = 2;

        [Header("F6.1 麟践踏提示圈（06 §12.5 用户定案：射程圈渲染复用）")]
        [Tooltip("提示圈颜色（半透明盘 + 细描边同源；默认朱砂偏暗——与红闪同色系但不抢眼）")]
        public Color trampleCircleColor = new Color(0.78f, 0.20f, 0.16f);
        [Tooltip("填充盘透明度（0 = 只留描边圈）")]
        [Range(0f, 1f)] public float trampleDiscAlpha = 0.10f;
        [Tooltip("描边圈透明度")]
        [Range(0f, 1f)] public float trampleRimAlpha = 0.65f;
        [Tooltip("F7.5③ 蓄力高亮色：蓄力中提示圈换此色（同时放大到敌表 chargeRadiusMult 2.5——「要重踏了」的可读预告；黄底）")]
        public Color trampleChargeColor = new Color(1f, 0.55f, 0.15f);

        [Header("F6.2 麟转阶段·火山共振（06 §12.5 用户定案）")]
        [Tooltip("全场红闪时长（秒）——蓄水窗（bossChargeSeconds 5s）之后开始；红闪计尽即岩浆雨落地（同步解除无敌定身）")]
        public float leiResonanceSeconds = 3f;
        [Tooltip("全场红闪峰值透明度（满屏红幕；0 = 不闪只落雨）")]
        [Range(0f, 1f)] public float leiResonanceAlpha = 0.34f;
        [Tooltip("全场红闪色（默认熔岩橙红——与红闪/红印同源；Play 中可调）")]
        public Color leiResonanceColor = new Color(0.88f, 0.32f, 0.12f);
        [Tooltip("共振岩浆雨：建造格变灰烬的格数范围（含端点；黄底 6-8；落点走 F2.1 种子流独立流号 = 回滚重放一致）")]
        public int leiRainMin = 6;
        public int leiRainMax = 8;
        [Tooltip("共振召唤数量（火蜥群；出怪口入场、不掉墨——蛟召唤纪律同款）")]
        public int leiSummonCount = 6;
        [Tooltip("F7.5④ 共振召唤物血量乘（**幼蜥变体**：火蜥本体现在 650——满血 ×6 = 3900 会把 Boss 战顶回太肉；默认 0.35 = 227/只。拧 1 = 满血）")]
        public double leiSummonHpMult = 0.35;
        [Tooltip("F7.5④ 共振召唤物移速乘（幼蜥更灵活——蛟召唤鱼/酋长野人同款「召唤物是弱怪不是第二 Boss」纪律；默认 1.3）")]
        public double leiSummonSpeedMult = 1.3;
        [Tooltip("共振召唤物敌字（须在敌表；空 = 不召唤）")]
        public string leiSummonChar = "火蜥";
        [Tooltip("共振召唤入场间隔（秒；时间错开防叠字——同波表出怪间隔口径）")]
        public float leiSummonGapSeconds = 0.25f;
        [Tooltip("F6.3 二阶段移速乘（转阶段演出完毕即生效——黄底 ×1.25）")]
        public double leiPhase2SpeedMult = 1.25;
        [Tooltip("F6.3 二阶段践踏半径（格；一阶段 1.5 落敌表，二阶段运行时改写敌字段——黄底 2.5；提示圈自动同步放大）")]
        public double leiPhase2TrampleRadius = 2.5;

        [Header("第五章·双面（F2，docs/13 / ADR-060）")]
        [Tooltip("非激活面敌移动速乘（只压移动：DoT/弹道/召唤不冻结；夜行影与 Boss 免疫。黄底 0.5）")]
        [Range(0.1f, 1f)] public float faceInactiveSpeedMult = 0.5f;
        [Tooltip("换面 CD（秒，从按下起算）。黄底 8")]
        public float faceSwitchCooldown = 8f;
        [Tooltip("CD 内换面扣墨（点一次 = 立即换面 + CD 重置；墨不足置灰。黄底 5）")]
        public int faceSwitchCost = 5;
        [Tooltip("换面纸色闪时长（秒；0.2 不暂停——docs/13 F2.5）")]
        public float faceSwitchFlashSeconds = 0.2f;
        [Tooltip("F3.4（ch5 幡）：几魂结一次墨（黄底 10；产墨量在幡卡自己的 soulPayout，升阶 ×2/级）")]
        public int soulsPerPayout = 10;

        [Header("第五章·虚面色调（F2.4 修订，用户 2026-10-22 点题）")]
        [Tooltip("虚面整屏浅蓝色调（覆盖层颜色；强度走 voidFaceTintAlpha）")]
        public Color voidFaceTint = new Color(0.62f, 0.78f, 0.92f, 1f);
        [Tooltip("虚面浅蓝覆盖率（0 = 关闭；黄底 0.18——够明显又不糊字）")]
        [Range(0f, 0.6f)] public float voidFaceTintAlpha = 0.18f;
        [Tooltip("虚面浅蓝淡入/淡出时长（秒；黄底 0.35）")]
        public float voidFaceTintFadeSeconds = 0.35f;
        [Tooltip("虚面红闪间隔（秒；站在虚面每过这么久闪一次。黄底 10）")]
        public float voidPulseInterval = 10f;
        [Tooltip("虚面红闪峰值透明度（黄底 0.32——一眼能看见但不至于瞎）")]
        [Range(0f, 1f)] public float voidPulseAlpha = 0.32f;
        [Tooltip("虚面红闪淡出时长（秒；黄底 0.5）")]
        public float voidPulseFadeSeconds = 0.5f;
        [Tooltip("虚面红闪颜色（黄底朱红）")]
        public Color voidPulseColor = new Color(0.72f, 0.20f, 0.16f, 1f);

        [Header("第五章·傩面 P1（F5，docs/13 / ADR-069）")]
        [Tooltip("换脸周期（秒；赤→青→金→赤……）。黄底 18")]
        public float nuoFaceCycleSeconds = 18f;
        [Tooltip("换脸预告（秒；周期末尾这么长时播面具翻转+起势。黄底 3）")]
        public float nuoFaceWarnSeconds = 3f;
        [Tooltip("换脸后可打不还手（秒；招式全停、伤害照吃。黄底 1.5）")]
        public float nuoGraceSeconds = 1.5f;
        [Tooltip("赤脸：劈最近一塔的间隔（秒；毁塔不毁格。黄底 6）")]
        public float nuoRedSmashInterval = 6f;
        [Tooltip("青脸：移速乘（黄底 1.25）")]
        public float nuoGreenSpeedMult = 1.25f;
        [Tooltip("青脸：受到伤害乘（血薄 = 唯一高效输出窗。黄底 1.5）")]
        public float nuoGreenDamageTakenMult = 1.5f;
        [Tooltip("青脸：每波召唤小鬼数量（黄底 6；绑波次不按节拍）")]
        public int nuoSummonPerWave = 6;
        [Tooltip("青脸：召唤物敌字（须在敌表；空 = 不召唤）")]
        public string nuoSummonChar = "小鬼";
        [Tooltip("金脸：封印起手（秒；起手期玩家换面 = 打断。黄底 2）")]
        public float nuoGoldWarnSeconds = 2f;
        [Tooltip("金脸：封印时长（秒；目标塔停火。黄底 3）")]
        public float nuoGoldSealSeconds = 3f;
        [Tooltip("金脸：被打断后的 CD（秒；黄底 6）")]
        public float nuoGoldCdSeconds = 6f;

        [Header("第五章·傩神 P2（F6，docs/13 / ADR-070）")]
        [Tooltip("傩舞周期（秒；黄底 10）")]
        public float shenDanceInterval = 10f;
        [Tooltip("傩舞停步时长（秒；黄底 2.5）——停 + 三面同时施法 + 受伤 ×1.5")]
        public float shenDanceSeconds = 2.5f;
        [Tooltip("傩舞期间受伤乘（黄底 1.5）")]
        public float shenDanceDamageMult = 1.5f;
        [Tooltip("赤脸劈塔间隔（秒；黄底 7）")]
        public float shenSmashInterval = 7f;
        [Tooltip("金脸封印间隔（秒；黄底 7）")]
        public float shenSealInterval = 7f;
        [Tooltip("青脸：每波在虚面召鬼数量（黄底 3）")]
        public int shenSummonPerWave = 3;
        [Tooltip("傩面碎后黑幕总时长（秒；黄底 3）")]
        public float shenBlackoutSeconds = 3f;

        [Header("家（道路尽头，用户 2026-10-23）")]
        [Tooltip("家最高升级次数（黄底 5；升级逻辑同墨塔：1 / 2 / 4 / 8 / 16 墨/跳）")]
        public int homeMaxLevel = 5;
        [Tooltip("家产墨间隔（秒；黄底 5）")]
        public float homeInkPulseSeconds = 5f;
        [Tooltip("家 1 级每跳产墨（黄底 1；往后 × inkGrowth，同墨塔）")]
        public int homeInkBase = 1;

        [Header("困难模式 & 6 阶（用户 2026-10-24）")]
        [Tooltip("困难模式敌人血量倍率（黄底 2；只该模式生效）")]
        public float hardEnemyHpMult = 2f;
        [Tooltip("6 阶塔颜色（红）")]
        public Color tier6Color = new Color(0.72f, 0.20f, 0.16f, 1f);
        [Tooltip("6 阶等级上限（黄底 6；仅已解锁该塔 6 阶时才可达）")]
        public int tier6MaxLevel = 6;

        [Header("局外金币（用户 2026-11-04，ADR-084）")]
        [Tooltip("结算：剩余墨 → 金币 的倍率（黄底 1 = 1 墨 1 金币；向下取整）")]
        public float goldPerInk = 1f;
        [Tooltip("结算：每颗剩余印（♥）折合金币（黄底 10；满印 5 颗 = 50）")]
        public int goldPerLife = 10;

        [Header("天赋（用户 2026-11-04，ADR-086）")]
        [Tooltip("六个核弹：每章发数（跨 5 关共享，章界重置；黄底 6）")]
        public int nukePerChapter = 6;
        [Tooltip("核弹伤害（全图，含 Boss；黄底 10000）")]
        public double nukeDamage = 10000;
        [Tooltip("咋瓦鲁多：时停秒数（黄底 5）")]
        public float timeStopSeconds = 5f;
        [Tooltip("升阶秘籍：冷却秒数（黄底 60）")]
        public float tomeCd = 60f;
        [Tooltip("升阶秘籍（劣）：冷却秒数（黄底 30）")]
        public float tomePoorCd = 30f;

        [Header("日记（P6，用户 2026-10-25）")]
        [Tooltip("日记打字机速度：每字秒数（黄底 0.035；越大越慢）")]
        public float diaryCharSeconds = 0.035f;
    }
}
