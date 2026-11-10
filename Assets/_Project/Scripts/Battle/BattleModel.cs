using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWars
{
    /// <summary>敌人的纯数据状态（表现层只读，不写入）。</summary>
    public class EnemyState
    {
        public string charId;
        public double hp;
        public double maxHp;
        public double speed;      // 格/秒
        public double dist;       // 已走路程（格）
        public double inkDrop;    // 死亡掉墨
        public int livesCost;     // 漏过扣印
        public bool flying;       // 空中：免疫近战
        public bool isBoss;       // Boss：大字 + 血条，漏过即失败
        public bool dead;
        public bool reached;

        // —— F1.2 双线（ADR-032）：敌走哪条路 + 生成时缓存的路由（长度/坐标换算零分配）——
        public int lane;               // 0=陆路 1=水路（拷自 EnemyStats.lane）
        public int spawnPort;          // F5.2 出怪口（0=下/1=上；枭二圈换端用；ch1/2 无消费）
        // —— F2.1（ch5 双面，docs/13 / ADR-059）：面归属 0=实 1=虚（与 lane 正交；ch1-4 恒 0 零路径）——
        public int face;
        public PathRoute route;        // Paths.Get(lane) 的缓存（SpawnEnemy 时取）
        public double waterSpeedMult = 1.0; // F4.1（ADR-034）：水中速乘（拷自 EnemyStats；蛟二阶段 F5 运行时改写）

        // —— F2.2 光-敌标记（06 §11.2；镜像 EnemyStats，生成时拷入）——
        public bool stealth;           // 潜行：雾中浅影（褪色幽灵影；蝠/猫）
        public bool lightVulnerable;   // 光脆：光中受伤 ×（蛾）
        public bool lightHindered;     // 光滞：光中减速（蝠）
        public bool nocturnal;         // 夜行：雾中提速（狼客串）
        public bool litCache;          // 本帧光照态（Tick 敌循环首步算好——速度结算/受伤加成/视图 tint 同帧同值，所见即所得）

        // —— F5.2 转阶段（06 §10.4 蛟两幕剧；镜像 EnemyStats，生成时拷入）——
        public double phase2HpRatio; // 转阶段血线（0=无；蛟=1/3——ch1 敌全缺省 0，零变化铁律由构造保证）
        public bool phase2;          // 已转阶段（一次性触发防抖）
        public bool invulnerable;    // 转阶段 i-frame（蓄水窗：Damage 早退零伤零状态；洪水落地解除）
        public bool holdMove;        // 转阶段定身（蓄水窗站桩演出；洪水落地解除——状态计时照走）

        // —— 阶段 C 机制（ADR-022；镜像 EnemyStats，生成时拷入）——
        public string onDeathSpawns;   // 死亡分裂：产物敌字（空=无）
        public int onDeathSpawnCount;
        public double enrageAtRatio;   // 激怒阈值比例（0=无）
        public double enrageSpeedMult = 2.0;
        public bool enraged;
        public string summons;         // 定期召唤：召唤物敌字（空=无）
        public double summonInterval;
        public int summonCount = 2;
        public int summonCap = 6;
        public double summonClock;     // 距下次召唤（秒）
        // —— F4.2（ch5 魇）入梦（缺省 0 = 关闭——旧敌零路径）——
        public double sleepInterval;   // 每 N 秒入梦一次（魇 6）
        public double sleepSeconds;    // 入梦时长（魇 2）
        public double sleepRadius;     // 入梦半径（格；魇 2.5）
        public double sleepClock;      // 距下次入梦（秒；生成时置满间隔）
        // —— F4.3（ch5 傀）换影复活（缺省 0 = 关闭——旧敌零路径）——
        public double reviveHpRatio;   // 首次致死改为换面复活，血量 = maxHp × 该值（傀 0.5）
        public bool revived;           // 已复活过（一次性——二次死亡真死）
        // —— F4.4（ch5 小鬼）墨漏（缺省 0 = 关闭——旧敌零路径）——
        public double inkLeak;         // 到终点扣墨（小鬼 3；与 livesCost 正交——小鬼 livesCost = 0）

        // —— F5.1（ch5 傩面 P1）三脸轮换（缺省关闭——旧敌零路径）——
        public bool nuoMask;             // 是傩面：吃 18s 换脸循环
        public bool endlessLap;          // 到出口扣印后回起点重走（Boss 到点不判败）
        public int nuoMaskIndex;         // 当前脸 0=赤(实) 1=青(虚) 2=金(跟随玩家面)
        public double nuoCycleClock;     // 距下次换脸（秒；生成时置满周期）
        public bool nuoWarned;           // 本轮预告已播（防同周期重复触发）
        public double nuoGraceRemaining; // 换脸后可打不还手剩余（>0 = 三脸招式全停）
        public double nuoSmashClock;     // 赤脸：距下次劈塔（秒）
        public double nuoGoldWarn;       // 金脸：起手剩余（>0 = 起手中）
        public double nuoGoldCd;         // 金脸：被打断后的 CD 剩余
        public int nuoGoldFace;          // 金脸：起手锁定的面（起手期玩家换面 = 打断）
        public TowerState nuoSealTarget; // 金脸：起手锁定的封印靶

        // —— F6（ch5 傩神 P2）：三面随行 + 傩舞（缺省关闭——旧敌零路径）——
        public bool nuoShen;
        public double shenSmashClock;     // 赤：距下次劈塔（秒）
        public double shenSealClock;      // 金：距下次封印（秒）
        public double shenDanceClock;     // 距下次傩舞（秒）
        public double shenDanceRemaining; // 傩舞剩余（>0 = 停步 + 受伤 ×1.5 + 三面同时施法）
        public bool crossFaceAttackable; // F6.1（ch5 傩神）：跨面可被攻击（两面塔都能索敌/溅射；表现层不暗显）

        // —— F3.1 状态效果（ADR-033 / GDD §5.2，模式对齐激怒：Tick 内联结算）——
        public double slowFactor;      // 缓速比例（0.3 = 慢 30%；0=无）——水塔命中刷新
        public double slowRemaining;   // 缓速剩余秒
        public double wetRemaining;    // 湿剩余秒（水塔命中标记 3s；冰塔查它 ×2——水冰连携轴）
        public double frozenRemaining; // 冻结剩余秒（>0 停走；激怒/召唤照常走——冻结只锁位移）
        public double stunRemaining;   // F3.2（ADR-039）眩晕剩余秒：>0 停走——独立于冻结（视觉金 tint 不混冰蓝、湿冰连携只查 frozen 不误触）；赋值即刷新（不叠层，任务单口径）；枭免疫归 F5 打标拦截

        // —— F4.1（06 §12.3）燃烧：第五状态（冻结/湿/眩晕后独立字段——ADR-039 先例定案）——
        public double burnRemaining;   // 燃烧剩余秒（>0 在烧）；重复触发刷新时长（赋值即刷——不叠层，任务单口径）
        public double burnDps;         // 整秒跳燃烧伤害（不叠取最高——新值更大才覆盖；熄灭清零防陈旧残留）
        public double burnTick;        // 整秒跳相位钟（1s 一跳与塔攻速/红印 DoT 同节拍——防 -0.0x 小数飘字刷屏）
        public bool burnImmune;        // 免疫燃烧+岩浆（火蜥/岩龟/麟——F5.1 打标；缺省 false 旧敌零迁移；红印 DoT 同拦——F2.4 钩子位消费）
        public bool burnVulnerable;    // F5.2：燃烧易伤（鳄「水怪怕火」×burnVulnerableMult——施加侧定格；缺省 false 零迁移）
        public double lavaMarkSpeedMult = 1.0; // F5.1：红印格上速度乘（火蜥 1.3「灾害区原生」；缺省 1 零变化——ch1/2/3 全敌零迁移）
        public double trampleRadius;           // F6.1：践踏光环半径（格；>0 才有光环——麟 1.5，F6.3 二阶段改写 2.5）
        // —— F7.5②③ 践踏蓄力 / 二圈（06 §12.5 用户定案 2026-10-19；缺省 0 = 关闭，ch1-3 零路径）——
        public double chargeEveryCells;        // 每走 N 格蓄力一次（拷自 EnemyStats；0 = 无蓄力）
        public double chargeSeconds;           // 蓄力站定秒数（蓄力期**不无敌**——Damage 照常单独放行）
        public double chargeRadius;            // 蓄力重踏半径（格，绝对值 2.5——常规走行仍 trampleRadius 1.5）
        public double chargeWalked;            // 距上次蓄力的已走格数（GM UpdateTrample 累加；回环重置清零）
        public double chargeRemaining;         // >0 = 蓄力站定中（GM 倒计时；归零做一次放大半径重踏结算）
        public int lapsLeft;                   // 额外圈数（>0 = 到终点重置回起点不判败——麟二圈；两条兑换路径都消耗它）
        // —— F5.1 枭栖灯（06 §11.4；枭专属字段，ch1/2 无枭零路径）——
        public bool stunImmune;        // 免疫眩晕（枭——蛟免疫冰冻同款纪律：Boss 毕业考不许被硬控锁死）
        public TowerState perchTower;  // 栖灯目标灯（null = 非栖灯态；期间枭离路站灯位 = 可被打 = 杀窗）
        public double perchRemaining;  // 栖灯倒计时剩余（触发时 = PerchSecondsPerLevel × 灯阶——T1 3s→T5 15s；枭改动 2026-10-09：5→3 用户定案）
        public double perchReturnDist; // 栖灯前路面弧长位（灯灭回位续走——栖灯不推进度）
        public EnemyState spawner;     // 分裂/召唤来源（挂父波次 + 召唤上限计数）
    }

    /// <summary>
    /// 塔的纯数据状态。
    /// v2.2 配对式升阶：等级只存 level，数值一律按基线 × 通道重算
    /// （damage = baseDamage × growth^(level-1)），避免连续合并的乘法漂移。
    /// </summary>
    public class TowerState
    {
        public string charId;
        // —— 基线（创建时从配置拷入，之后不变）——
        public double baseDamage;
        public double interval;          // 攻击间隔（秒，不成长）
        public double baseRange;
        public double rangePerLevel;     // 射程成长通道（弓/弩 0.25；近战与矛 0，矛走 AOE 成长）
        public double baseInkPerPulse;   // 经济塔（墨）：基线产墨/跳（ADR-027 离散产墨）
        public bool antiAir;             // 能否命中空中敌（近战/矛/棍/戟 = false）
        public bool ranged;              // 远程塔发射弹道，近战瞬发
        public double value;             // 卡价值（v3.0 卖出 = 价值 × 50% × 字位；放置时从池拷入）
        public AoeShape aoeShape;        // AOE 形态（v3.0：矛矩形/棍圆形/戟扇形；None=单体）
        public double aoeBaseSize;       // Rect=宽；Circle/Sector=半径
        public double aoeBaseParam;      // Rect=高；Sector=张角(度)
        // —— 派生/运行时 ——
        public int level = 1;            // 1~5（v2.2 上限 5 级）
        public double damage;            // ApplyLevelStats 重算
        public double range;
        public double inkPerPulse;       // 每跳产墨量（ApplyLevelStats 重算，×2/级）
        public double inkTimer;          // 产墨脉冲累计器（ADR-027：攒满 InkPulseSeconds 跳一档）
        public double aoeSize;           // ApplyLevelStats 重算（+0.15/级成长）
        public double aoeParam;          // ApplyLevelStats 重算（Rect 高同步成长；张角固定）
        public double cooldown;
        public double sleepRemaining;    // F4.2（ch5 魇）：入梦剩余秒（>0 停火——不降级、墨塔照产墨）
        public double sealRemaining;     // F5.2（ch5 傩面金脸）：封印剩余秒（>0 停火——与入梦正交、可叠）
        public Vector2 position;
        // —— F2.2 潮汐淹没（ADR-032 / GDD §3.4）——
        public int elevation = 4;             // 所在格海拔（放置/移动/快照回放时从格子拷入；ch1 恒 4 = 恒干）
        public bool waterImmune;              // 水系塔标记（TowerDef 拷入；F3 水/冰/渔 true → 免疫沉默）
        public bool submerged;                // 运行时：被淹沉默中（塔干 = 解除 + 倒计时清零）
        public double submergeCountdown = 20; // 沉默倒计时（秒；入水置满，Tick 内扣，归零消失）
        // —— F3 状态效果参数（ADR-033；TowerDef 拷入，Damage 按攻击方字段施加）——
        public double slowFactor;             // 命中缓速比例（水 0.3）
        public double slowSeconds;            // 缓速持续（水 2s）
        public double wetSeconds;             // 命中附湿（水 3s；冰查标记 ×2）
        public double freezeSeconds;          // 命中冻结（冰 1.2s）
        public double wetDamageMult = 1.0;    // 对湿敌倍率（冰 2.0）

        // —— F4.1（06 §12.3）火/炮命中附加燃烧（F4.2/F4.4 落卡赋值；0 = 无燃烧——ch1-3 全 0 零路径铁律）——
        public double burnSeconds;            // 命中挂燃时长（黄底 3s；重复触发刷新时长）
        public double burnDps;                // 每整秒一跳燃烧伤害（黄底 3；灰烬增益 ×AshBuffLookup 施加侧定格）
        public bool landPulseSlower;          // F3.3 渔：陆上跳隔 ×2（水中走基准）
        public bool onWaterCell;              // F3.3 渔：站在永久水格（放置/移动拷入；临时淹没走海拔判定）
        public bool locked;                   // F5.1 枭栖灯锁死：点击全拦（移/卖/合并——全锁口径防升阶刷窗漏洞，程序侧备案 09 §7）

        // —— F2.1（ch5 双面）：塔的面归属（0 实/1 虚；非激活面整层停火——F2.2）——
        public int face;

        // —— F3.1（ch5 镜）：镜像 = 本体在对面同坐标的投影（派生状态，**不进快照**）——
        public bool isMirror;     // true = 镜像：不可卖/升/移，随本体消失
        public TowerState mirror; // 本体 → 自己的镜像（镜像侧为 null；本体卖/毁时据此同步清视图+模型）
        public TowerState mirrorSource; // 镜像 → 本体（影被环境毁时只断链接，本体留）

        // —— F3.2（ch5 雷）：链电（TowerDef 拷入；0 = 无链——ch1-4 零路径）——
        public int chainJumps;        // 跳数（雷 3）
        public double chainRatio;     // 每跳倍率（0.6）
        public double chainRadius;    // 链距（格，2.0）

        // —— F3.3（ch5 煞）：虚面伤害倍率（1 = 无加成——ch1-4 零路径）——
        public double voidDamageMult = 1.0;

        // —— F3.4（ch5 幡）：魂幡（敌死聚魂 → 每 10 魂产墨；多幡按最高阶，不叠加）——
        public bool soulBanner;
        public double soulPayout;     // L1 基准产墨（升阶 ×2/级）
    }

    /// <summary>弹道的纯数据状态（W2b）：追踪目标飞行，命中结算伤害。</summary>
    public class ProjectileState
    {
        public Vector2 position;
        public EnemyState target;
        public double speed;      // 格/秒
        public double damage;
        public string charId;     // 视觉用：按塔字着色（火朱/冰青/余墨黑）
        public TowerState source; // 特效预留（W3 溅射/减速挂这里）
    }

    /// <summary>
    /// 战斗逻辑模型：只做数值与判定，不碰任何引擎对象（渲染相关）。
    /// 表现层通过事件订阅，W3 的 Headless 平衡模拟器直接 new 一个跑整关。
    /// </summary>
    public class BattleModel
    {
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public readonly List<TowerState> Towers = new List<TowerState>();
        public readonly List<ProjectileState> Projectiles = new List<ProjectileState>();

        /// <summary>全局弹速（格/秒）。原型期一刀切，分化再进塔字池。</summary>
        public double ProjectileSpeed = 10;

        /// <summary>飞行敌在路径上方的悬空高度（格）。模型与视图共用同一常量。</summary>
        public const float FlyerOffsetY = 0.8f;

        /// <summary>升阶规则（GDD §5.1 v2.2）：伤害 ×1.8/级、产墨 ×2/级、射程线性；上限 5 级。</summary>
        public int MaxTowerLevel = 5;
        /// <summary>6 阶等级上限（P2，用户 2026-10-24；仅已解锁该塔 6 阶时可达）。</summary>
        public int Tier6MaxLevel = 6;
        /// <summary>6 阶解锁查询（GM 注入：该塔字是否已解锁 6 阶；null = 全未解锁）。</summary>
        public Func<string, bool> Tier6UnlockedLookup;
        public double DamageGrowth = 1.8;
        public double InkGrowth = 2.0;
        public double InkPulseSeconds = 5; // 墨塔产墨间隔（ADR-027；GameParams 注入）
        public double SubmergeSeconds = 20; // 塔被淹沉默倒计时（F2.2；GameParams 注入，GDD §3.4 黄底）

        // —— F2.2 光-敌交互倍率（06 §11.2；GameParams 注入，Inspector 调参位）——
        public double LightVulnerableMult = 1.5; // 蛾：光中所受伤害 ×1.5（飞蛾扑火）
        public double LightHinderedMult = 0.7;   // 蝠：光中速度 ×0.7（灯塔压制）
        public double NocturnalFogMult = 1.3;    // 狼：雾中速度 ×1.3（夜行；入光还原 1.0）

        // —— F3.3 法塔眩晕（06 §11.2；GameParams 注入）——
        public double MageStunChance = 0.2;      // 法塔光照内命中触发眩晕的概率（20%）

        // —— F5.1 枭栖灯（06 §11.4；GameParams 注入）——
        public double PerchSecondsPerLevel = 5;  // 栖灯倒计时 = 此值 × 灯阶（T1 5s → T5 25s，黄底公式 5）
        public double AoeGrowth = 0.15; // AOE 范围/级（GDD §5.1 增益线性：范围半径 +0.15 格/级）

        public double Ink;
        public int Lives;
        public int Kills;
        public int Wave;
        public bool Failed;

        // ============ F3.4（ch5 幡，docs/13 / ADR-064）============
        /// <summary>魂（本关累计；敌死 +1，两面都算）。每关清零（Q3 定稿，Reset 里清）。</summary>
        public int Souls;
        /// <summary>几次魂结一次墨（GameParams 注入，缺省 10）。</summary>
        public int SoulsPerPayout = 10;

        // ============ 临时格（用户 2026-10-23）============
        /// <summary>临时格坐标：该格上的塔不攻击/不产墨（GM 进关注入；int.MinValue = 不启用）。</summary>
        public int TempCellX = int.MinValue;
        public int TempCellY = int.MinValue;

        /// <summary>困难模式：全体敌人血量倍率（GM 按模式注入；1 = 简单，2 = 困难）。</summary>
        public double EnemyHpMult = 1;

        // ============ F2.1（ch5 双面，docs/13 / ADR-059）============
        /// <summary>当前激活面：0=实（landRoute）1=虚（voidRoute）。单面章恒 0 —— 全部 face 谓词零路径。</summary>
        public int ActiveFace { get; private set; }
        /// <summary>非激活面敌速乘（F2.2；夜行 nocturnal 与 Boss 免疫——06 §14.2 黄底 0.5）。</summary>
        public double FaceInactiveSpeedMult = 0.5;

        // ============ F5.1（ch5 傩面 P1，docs/13 / ADR-069）三脸周期 ============
        /// <summary>换脸周期（秒；GameParams 注入，黄底 18）。</summary>
        public double NuoFaceCycleSeconds = 18;
        /// <summary>换脸预告（秒；周期末尾这么长时播面具翻转）。</summary>
        public double NuoFaceWarnSeconds = 3;
        /// <summary>换脸后可打不还手（秒；招式全停、伤害照吃）。</summary>
        public double NuoGraceSeconds = 1.5;
        /// <summary>青脸移速乘（黄底 1.25）。</summary>
        public double NuoGreenSpeedMult = 1.25;
        /// <summary>青脸受伤乘（黄底 1.5 —— 「血薄」= 唯一高效输出窗）。</summary>
        public double NuoGreenDamageTakenMult = 1.5;
        /// <summary>赤脸劈塔间隔（秒；黄底 6 —— 18s 窗口内 3 次）。</summary>
        public double NuoRedSmashInterval = 6;
        /// <summary>金脸起手（秒；黄底 2 —— 起手期玩家换面可打断）。</summary>
        public double NuoGoldWarnSeconds = 2;
        /// <summary>金脸封印时长（秒；黄底 3 —— 目标塔停火）。</summary>
        public double NuoGoldSealSeconds = 3;
        /// <summary>金脸 CD（秒；黄底 6 —— 打断后/封印后都要等）。</summary>
        public double NuoGoldCdSeconds = 6;
        /// <summary>金脸跟随玩家面（同进度换路；不是「换脸」——无 grace、不播脸名）。</summary>
        public event Action<EnemyState> NuoFaceFollowed;
        /// <summary>赤脸劈塔（表现层：清格 + 释放视图 + 「毁」飘字）。</summary>
        public event Action<EnemyState, TowerState> NuoSmashTower;
        /// <summary>金脸起手（表现层：起势提示 + 锁定目标）。</summary>
        public event Action<EnemyState, TowerState> NuoGoldCast;
        /// <summary>金脸被打断（起手期玩家换面）。</summary>
        public event Action<EnemyState> NuoGoldInterrupted;
        /// <summary>金脸封印落地（目标塔已置 sealRemaining）。</summary>
        public event Action<EnemyState, TowerState> NuoSealApplied;

        // ============ F6（ch5 傩神 P2，docs/13 / ADR-070）傩舞 ============
        /// <summary>赤劈间隔（秒；黄底 7）。</summary>
        public double ShenSmashInterval = 7;
        /// <summary>金封印间隔（秒；黄底 7）。</summary>
        public double ShenSealInterval = 7;
        /// <summary>傩舞周期（秒；黄底 10）。</summary>
        public double ShenDanceInterval = 10;
        /// <summary>傩舞停步时长（秒；黄底 2.5）——唯一高效输出窗，也是唯一危险窗。</summary>
        public double ShenDanceSeconds = 2.5;
        /// <summary>傩舞期间受伤乘（黄底 1.5）。</summary>
        public double ShenDanceDamageMult = 1.5;
        /// <summary>傩舞起（三面同时施法：赤劈 + 金封 + 青召）。</summary>
        public event Action<EnemyState> ShenDanceStarted;
        /// <summary>傩舞止。</summary>
        public event Action<EnemyState> ShenDanceEnded;
        /// <summary>换脸预告（表现层播面具翻转+起势；还没换，面/落点都不动）。</summary>
        public event Action<EnemyState> NuoMaskWarn;
        /// <summary>换脸完成（面已切、dist 未动 = 同路程进度落点）。</summary>
        public event Action<EnemyState> NuoMaskChanged;

        /// <summary>换面唯一写入口（表现层换完重建格-塔绑定/视图显隐；同值幂等）。</summary>
        public void SetActiveFace(int face)
        {
            face = face == 1 ? 1 : 0;
            if (face == ActiveFace) return;
            ActiveFace = face;
            FaceChanged?.Invoke(face);
        }

        /// <summary>换面完成（表现层订阅：重绑格-塔 / 视图显隐 / 敌 tint）。</summary>
        public event Action<int> FaceChanged;

        /// <summary>注入：线路集（F1.2 双线，ADR-032）。lane 0=陆路 1=水路（无水路章回退陆路）；
        /// 敌人生成时按 lane 缓存各自 PathRoute，后续坐标换算/到终点判定全走缓存。</summary>
        public IPathSet Paths;
        /// <summary>注入：敌数值查询（分裂/召唤产物的数值来源；GameManager 接 enemyTable.Find）。</summary>
        public Func<string, EnemyStats> EnemyStatsLookup;
        /// <summary>
        /// F4.1 / F7.4 / F4.5 灰烬增益统一律（06 §12.2/§12.3）：**火/炮/烬站灰烬格 → 全部伤害 × 倍率**
        /// ——**直接攻击**与**燃烧跳伤**同走本查询（用户 2026-10-18 变更：原口径只加成燃烧跳伤，
        /// 3/s vs 直接 27/s 体感≈零 → 扩到全伤）；「死灰复燃」F3.2 半边于此闭环。
        /// 注入式（grid 是表现层资产，模型不持格引用——EnemyStatsLookup 同模式）：GameManager 接
        /// 「塔 → 格 TileState 查询 × 倍率」；null / 不在灰格 = ×1（ch1-3 无灰烬零路径）。
        /// 定格口径：**直接伤害取命中时刻**（塔移出灰格立即失 buff）；**燃烧取施加时刻**（已挂的
        /// 燃烧不降温——命中即定格）。
        /// </summary>
        public Func<TowerState, double> AshBuffLookup;

        /// <summary>
        /// F5.2（06 §12.4 / ADR-049）：燃烧易伤倍率——目标挂 burnVulnerable 时，**施加侧**把
        /// dps × 此值（鳄「水怪怕火」黄底 2）。与灰烬增益同律：施加时刻定格快照（「不叠取最高」
        /// 比较的是定格后的值）；缺省 1 = 无易伤（ch1-3 无燃烧源 + 旧敌不挂标 → 零变化铁律）。
        /// GameManager 从 GameParams 注入（倍率集中一处，Inspector 可调）。
        /// </summary>
        public double BurnVulnerableMult = 1.0;

        public event Action<EnemyState> EnemySpawned;
        public event Action<EnemyState> EnemyDied;
        /// <summary>F4.3（ch5 傀）：换影复活（首次致死 → 另一面同进度 50% 血）。表现层飘字/音效。</summary>
        public event Action<EnemyState> EnemyRevived;
        /// <summary>0.4 修正（ch4 前置）：逐敌受击——Damage 落血即抛（AoE 溅射与弹道单体同源；
        /// 金额 = 实际结算值，冰湿 ×2 / 蛾光脆 ×1.5 同源可见）。i-frame 零伤不抛（Damage 早退）=
        /// 「闪白照放、跳字不显」口径由表现层保留。</summary>
        public event Action<EnemyState, double, TowerState> EnemyDamaged;
        public event Action<EnemyState> EnemyReached;
        public event Action<EnemyState> EnemyEnraged;
        public event Action<TowerState, EnemyState> TowerFired;
        public event Action<TowerState, double> InkPulsed; // 0.3 Juice：墨/渔塔产墨跳档（ADR-027），飘字由表现层订阅

        // ============ F2.1 潮位状态机（ADR-032 / 06 §10.2 波次档位制） ============

        /// <summary>当前潮位水线 0-4（退/平/涨/大涨/洪水；ch1 恒 0 = 潮汐不激活）。淹没 ⟺ 格子海拔 ≤ 水线；4 = 全图洪水（F5 蛟 B 方案 + T 键预览，ADR-035）。</summary>
        public int WaterLevel { get; private set; }

        /// <summary>水线切换（波开始时由 GameManager 驱动；旧值/新值双参 = F2.4 扫掠方向判定）。</summary>
        public event Action<int, int> WaterLevelChanged;

        /// <summary>F2.2：塔淹没倒计时归零 → 消失（0 回收）——表现层清格子占用/释放视图/播报。</summary>
        public event Action<TowerState> TowerDrowned;
        /// <summary>F5.1：枭栖灯倒计时归零吞噬灯塔（0 回收）——表现层清格/释放视图/播报。</summary>
        public event Action<TowerState> LampDevoured;

        /// <summary>F5.2（06 §10.4）：Boss 血量过转阶段血线（蛟=1/3）→ 两幕剧开幕（蓄水→洪水→潮落由表现层编排）。</summary>
        public event Action<EnemyState> BossPhase2;

        /// <summary>F7.5②（06 §12.5）：Boss 带额外圈（lapsLeft>0）到终点 → 顺延重置回起点不判败。
        /// 表现层订阅打日志/提示；**安全网路径**（正常由转阶段收尾的重置承担——见 GameManager）。</summary>
        public event Action<EnemyState> BossLooped;

        /// <summary>F5.2：召唤暂停 N 秒（蛟洪水后的纯抢救窗口：生兵停摆；0 = 立即恢复）。
        /// 全局暂停——ch2 唯一召唤者即蛟；ch1 永不触发。</summary>
        public void PauseSummons(double seconds) => summonPauseRemaining = Math.Max(0, seconds);

        private double summonPauseRemaining; // 召唤暂停剩余秒（Tick 头统一扣，防多召唤者重复扣）

        /// <summary>置水线（0-4 钳位，4 = 全图洪水；同值幂等）。塔沉默判定（F2.2）走 Tick 内海拔比对，不走本事件。</summary>
        public void SetWaterLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 4); // ADR-035：放开 4 = F5 蛟全图洪水 / T 键预览（波次表最高 Surge=3 不受影响）
            if (level == WaterLevel) return;
            int old = WaterLevel;
            WaterLevel = level;
            WaterLevelChanged?.Invoke(old, level);
        }
        public event Action<ProjectileState> ProjectileSpawned;
        public event Action<ProjectileState> ProjectileHit;
        public event Action<ProjectileState> ProjectileRemoved;
        /// <summary>F3.2（ch5 雷）：链电电弧（from → to 世界坐标，表现层画一条 0.12s 电光；可砍档）。</summary>
        public event Action<Vector2, Vector2> ChainLinked;
        /// <summary>F3.4（ch5 幡）：每 SoulsPerPayout 魂结算一次（t = 结算的最高阶幡，amount = 墨）。</summary>
        public event Action<TowerState, double> SoulsPulsed;
        public event Action<double, int> StatsChanged;

        /// <summary>天赋·咋瓦鲁多：剩余时停秒数（>0 = 敌人静止；塔/弹道/DoT 照走）。GameManager 递减。</summary>
        public double TimeStopRemaining;

        /// <summary>天赋·升阶秘籍（劣）：降一阶（1 阶不降）。</summary>
        public bool DowngradeTower(TowerState t)
        {
            if (t == null || t.level <= 1) return false;
            t.level--;
            ApplyLevelStats(t);
            SyncMirror(t);
            RefreshLights();
            return true;
        }

        /// <summary>天赋·六个核弹：全图敌人受到 dmg（含 Boss；无敌帧照拦）。</summary>
        public void DamageAll(double dmg)
        {
            int n = Enemies.Count; // 快照：击杀分裂会追加表尾，本循环不回访
            for (int i = 0; i < n; i++)
            {
                var e = Enemies[i];
                if (e == null || e.dead || e.reached) continue;
                Damage(e, dmg, null);
            }
        }

        public void Reset(double startInk, int lives)
        {
            Enemies.Clear();
            Towers.Clear();
            Projectiles.Clear();
            Ink = startInk;
            Lives = lives;
            Kills = 0;
            Souls = 0; // F3.4：魂每关清零（Q3 定稿）
            Wave = 0;
            Failed = false;
            ActiveFace = 0; // F2.1（ch5）：进关/回滚 → 面 = 实（docs/13 F2.5；不抛事件——表现层本轮随场重建）
            summonPauseRemaining = 0; // F5.2：蛟转阶段状态随 Reset 清（回滚/切关重放两幕剧）
            lavaMarkCells.Clear();    // F5.1：红印格集合随 Reset 清（进关/回滚——GM lavaMarks 同刻清，双清防陈旧提速）
            // F2.1：进关/回滚水线归零（06 §10.7 潮位属关卡数据，存档零改动——快照重放波次表自带潮位）。
            // 回滚时格子水色可能停在涨潮——归零须发事件让表现层退水。
            if (WaterLevel != 0)
            {
                int old = WaterLevel;
                WaterLevel = 0;
                WaterLevelChanged?.Invoke(old, 0);
            }
            RaiseStats();
        }

        /// <summary>
        /// 生成敌人（v3.0-C：整表 EnemyStats 拷入，取代旧散参）。
        /// spawner = 分裂/召唤来源（产物挂父波次）；noInkDrop = 分裂产物不掉墨（防刷经济）。
        /// </summary>
        /// <summary>生成敌人（F1.3：port = 出怪口 0 下/1 上——同路双向章选半程路由；缺省 0 = 单向行为不变）。</summary>
        public EnemyState SpawnEnemy(EnemyStats s, EnemyState spawner = null, bool noInkDrop = false, int port = 0,
                                     double hpMult = 1.0, int face = 0)
        {
            var e = new EnemyState
            {
                charId = s.charId,
                hp = s.hp * hpMult * EnemyHpMult,          // F7.4：召唤血脉压；× EnemiesHpMult（困难模式全体翻倍）
                maxHp = s.hp * hpMult * EnemyHpMult,
                speed = s.speed,
                dist = 0,
                inkDrop = noInkDrop ? 0 : s.inkDrop,
                livesCost = s.livesCost,
                flying = s.flying,
                isBoss = s.isBoss,
                onDeathSpawns = s.onDeathSpawns,
                onDeathSpawnCount = s.onDeathSpawnCount,
                enrageAtRatio = s.enrageAtRatio,
                enrageSpeedMult = s.enrageSpeedMult,
                summons = s.summons,
                summonInterval = s.summonInterval,
                summonCount = s.summonCount,
                summonCap = s.summonCap,
                summonClock = s.summonInterval,
                spawner = spawner,
                lane = s.lane, // F1.2：水路敌（鱼虾龟鳄蛟）走水线
                spawnPort = port, // F5.2：出怪口存档（枭二圈换端 1-port；ch1/2 无消费零影响）
                face = face == 1 ? 1 : 0, // F2.1：面归属（ch1-4 恒 0 零路径）
                // F1.3：口选端（双向章半程；单向回退全程）。F2.1：虚面走 voidRoute（面优先于口——
                // 双面章单口单路，口对虚面无意义；缺省 face 0 恒走原 lane/口路径 = ch1-4 零变化）
                route = Paths != null
                    ? (face == 1 ? Paths.GetFace(1) : Paths.GetPort(s.lane, port))
                    : default,
                waterSpeedMult = s.waterSpeedMult, // F4.1：水中速乘（ADR-034）
                phase2HpRatio = s.phase2HpRatio,   // F5.2：转阶段血线（蛟=1/3；余敌 0）
                stealth = s.stealth,               // F2.2：潜行浅影（蝠/猫）
                lightVulnerable = s.lightVulnerable, // F2.2：蛾光脆
                lightHindered = s.lightHindered,   // F2.2：蝠光滞
                nocturnal = s.nocturnal,           // F2.2：狼夜行
                stunImmune = s.stunImmune,         // F5.1：枭免疫眩晕（Boss 纪律——法塔对其只剩伤害无控）
                burnImmune = s.burnImmune,         // F4.1：免疫燃烧+岩浆（火蜥/岩龟/麟 F5.1 打标；缺省 false 零拦截）
                burnVulnerable = s.burnVulnerable, // F5.2：燃烧易伤（鳄 ×2「水怪怕火」；缺省 false 零变化）
                lavaMarkSpeedMult = s.lavaMarkSpeedMult, // F5.1：红印提速（火蜥 1.3；缺省 1 零变化）
                trampleRadius = s.trampleRadius,         // F6.1：践踏光环半径（麟 1.5；缺省 0 零路径）
                // F7.5②③：践踏蓄力三字段（缺省 0 = 关闭——ch1-3 零路径铁律由 chargeEveryCells=0 闸住）
                chargeEveryCells = s.chargeEveryCells,
                chargeSeconds = s.chargeSeconds,
                chargeRadius = s.chargeRadiusMult,
                // F4.2（ch5 魇）：入梦三字段 + 首跳计时（缺省 0 = 关闭，旧敌零路径）
                sleepInterval = s.sleepInterval,
                sleepSeconds = s.sleepSeconds,
                sleepRadius = s.sleepRadius,
                sleepClock = s.sleepInterval,
                // F4.3（ch5 傀）：换影复活（缺省 0 = 关闭，旧敌零路径）
                reviveHpRatio = s.reviveHpRatio,
                inkLeak = s.inkLeak, // F4.4（ch5 小鬼）：墨漏（缺省 0 = 关闭）
                // F5.1（ch5 傩面）：三脸轮换 + 到出口回起点（缺省 false = 旧敌零路径）
                nuoMask = s.nuoMask,
                endlessLap = s.endlessLap,
                nuoMaskIndex = 0,                        // 首脸 = 赤（实路）
                nuoCycleClock = NuoFaceCycleSeconds,      // 首脸从满周期起算
                nuoSmashClock = NuoRedSmashInterval,
                // F6（ch5 傩神 P2）：三面施法时钟（缺省 false = 旧敌零路径）
                nuoShen = s.nuoShen,
                shenSmashClock = ShenSmashInterval,
                shenSealClock = ShenSealInterval,
                shenDanceClock = ShenDanceInterval,
                crossFaceAttackable = s.crossFaceAttackable, // F6.1（ch5 傩神）：跨面可被攻击
            };
            Enemies.Add(e);
            EnemySpawned?.Invoke(e);
            return e;
        }

        public void AddTower(TowerState t)
        {
            t.cooldown = 0;
            ApplyLevelStats(t);
            Towers.Add(t);
            RefreshLights(); // F1.2：塔增（灯入池）→ 光集重算
        }

        /// <summary>按等级通道重算派生数值（升阶后调用；基线字段不变）。</summary>
        public void ApplyLevelStats(TowerState t)
        {
            t.damage = t.baseDamage * Math.Pow(DamageGrowth, t.level - 1);
            t.range = t.baseRange + t.rangePerLevel * (t.level - 1);
            t.inkPerPulse = t.baseInkPerPulse * Math.Pow(InkGrowth, t.level - 1);
            // AOE 范围成长（GDD §5.1 增益线性 +0.15/级）：矩形宽高同步长，扇形张角固定
            t.aoeSize = t.aoeBaseSize + AoeGrowth * (t.level - 1);
            t.aoeParam = t.aoeShape == AoeShape.Rect
                ? t.aoeBaseParam + AoeGrowth * (t.level - 1)
                : t.aoeBaseParam;
        }

        /// <summary>该塔字的最大等级：已解锁 6 阶 → Tier6MaxLevel，否则 MaxTowerLevel。</summary>
        public int MaxLevelFor(string charId)
        {
            if (Tier6UnlockedLookup != null && Tier6UnlockedLookup(charId))
                return Mathf.Max(MaxTowerLevel, Tier6MaxLevel);
            return MaxTowerLevel;
        }

        /// <summary>升 1 级（配对合并）。已达上限返回 false。</summary>
        public bool TryUpgradeTower(TowerState t)
        {
            if (t.level >= MaxLevelFor(t.charId)) return false;
            t.level++;
            ApplyLevelStats(t);
            SyncMirror(t);   // F3.1（用户 2026-10-22 定稿）：镜像随本体升阶（恒 = 本体伤害 ×50%）
            RefreshLights(); // F1.2：升阶灯 range 变（世界变亮）→ 光集重算
            return true;
        }

        /// <summary>
        /// F3.1（ch5 镜）：本体升阶 → 对面镜像同步阶数并重算。
        /// 镜像建时 `baseDamage` 已 = 本体基伤 × `mirrorDamageRatio`（0.5），走同一套 `DamageGrowth`
        /// ⇒ 任意阶都**恒等于本体伤害的 50%**（L1 11 / L2 16.5 / L3 24.75…）。射程同理跟阶。
        /// 镜像自身不可升（GM 拦），且镜像没有自己的 mirror → 本方法对镜像零路径。
        /// </summary>
        private void SyncMirror(TowerState body)
        {
            var m = body != null ? body.mirror : null;
            if (m == null) return;
            m.level = body.level;
            ApplyLevelStats(m);
        }

        /// <summary>
        /// 配对合并（v2.2）：两个同字同级塔 → 目标塔 +1 级，源塔移除。
        /// 调用方先用 MergeRules 判定条件；结果留在目标格、源格腾空由表现层处理。
        /// </summary>
        public bool MergeTowers(TowerState source, TowerState target)
        {
            if (source == null || target == null || source == target) return false;
            if (!TryUpgradeTower(target)) return false;
            RemoveTower(source); // F1.2：改走 RemoveTower（等价 + 光集自钩）
            return true;
        }

        /// <summary>移除塔（卖出 / 合并源 / 淹没出局）。</summary>
        public void RemoveTower(TowerState t)
        {
            Towers.Remove(t);
            RefreshLights(); // F1.2：塔删（灯卖出）→ 光集重算
        }

        // ============ F1.2 LightSystem（ch3 幽夜）：光照集合与交战谓词（06 §11.2） ============
        // 光照 = 家灯 + 各灯塔当前射程圆并集。塔 targeting 谓词「敌 ∈ 光照」单点拦截一切攻击
        // 性行为（缓速/冻结随「无目标」拦在 FindTarget 源头）；墨/渔等经济塔不走索敌天然豁免；
        // 法增益判定（F3.3）与敌 Tick 光敌交互（F2.2）复用 IsLit。fog 关（ch1/ch2）谓词恒真
        // 短路 + 光集恒空——零逻辑零性能差（铁律零变化）。

        private bool fogOn;
        private Vector2 homeLightPos;   // 家灯 = 弧长中点家 + 关级半径（4.5→0.5 拐杖曲线）
        private double homeLightRadius;
        private readonly List<(Vector2 pos, double radius)> lightCache = new List<(Vector2, double)>();

        /// <summary>光照集变化（进关/塔增删/升阶/移动）——渲染层订阅重挖迷雾光圈。</summary>
        public event Action LightsChanged;

        /// <summary>进关设置光照（fog 关 = 谓词恒真短路；家灯圆心 = 弧长中点家）。</summary>
        public void SetFog(bool on, Vector2 homePos, double homeRadius)
        {
            fogOn = on;
            homeLightPos = homePos;
            homeLightRadius = homeRadius;
            RefreshLights();
        }

        /// <summary>
        /// 重算光照缓存（幂等可重入）：家灯 + 场上灯塔当前 range（升阶变亮走 ApplyLevelStats
        /// 管线零新做）。塔增删/升阶（本类内部钩）、移动（GameManager 改 position 后）、淹没
        /// 出局（Tick 内钩）全覆盖；漏钩症状 = 灯不在了光圈还亮（坑档案「多处接线」预防）。
        /// </summary>
        public void RefreshLights()
        {
            lightCache.Clear();
            if (fogOn)
            {
                if (homeLightRadius > 0) lightCache.Add((homeLightPos, homeLightRadius));
                for (int i = 0; i < Towers.Count; i++)
                {
                    var t = Towers[i];
                    if (t.charId == "灯" && t.range > 0) lightCache.Add((t.position, t.range));
                }
            }
            LightsChanged?.Invoke();
        }

        /// <summary>当前光照集（渲染挖洞同口径——玩家所见即所得）。</summary>
        public IReadOnlyList<(Vector2 pos, double radius)> GetLights() => lightCache;

        // —— F5.1 红印格集合（GM 同步：火蜥「灾害区原生」提速判定；ch1-3 恒空 → 零路径）——
        private readonly HashSet<Vector2Int> lavaMarkCells = new HashSet<Vector2Int>();

        /// <summary>
        /// F5.1：红印格集合同步（GameManager.UpdateLavaMarks 每帧推——同帧同值、所见即所得；
        /// 空册/null = 清空）。判定口径与红印 DoT 同源（Mathf.RoundToInt 取格），不另立新规。
        /// </summary>
        public void SyncLavaMarks(IEnumerable<Vector2Int> cells)
        {
            lavaMarkCells.Clear();
            if (cells == null) return;
            foreach (var c in cells) lavaMarkCells.Add(c);
        }

        /// <summary>F5.1：位置是否踩在红印格上（火蜥提速判定——DoT 同口径）。</summary>
        public bool IsOnLavaMark(Vector2 pos) =>
            lavaMarkCells.Count > 0 &&
            lavaMarkCells.Contains(new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y)));

        /// <summary>
        /// 交战谓词：位置 ∈ 任一光照圆（与挖洞渲染同口径）。空集 = 全暗不可打（忠实集合语义；
        /// 理论不达——家灯恒 ≥0.5）。
        /// </summary>
        public bool IsLit(Vector2 pos)
        {
            if (!fogOn) return true;
            for (int i = 0; i < lightCache.Count; i++)
                if (Vector2.Distance(pos, lightCache[i].pos) <= lightCache[i].radius + 0.01) return true;
            return false;
        }

        // ============ F3.2 眩晕（ADR-039：独立字段对齐冻结内联模式） ============
        // 触发方 F3.3 法塔（光内 20% 概率、时长 = 灯阶数÷2、多灯取最高阶）；枭免疫 F5.1 打标
        // 拦截（stunImmune 归枭批次，本单元不落）。不叠层 = 赋值即刷新。

        /// <summary>施加眩晕（重复触发刷新时长——不叠加；枭免疫 F5.1 stunImmune 拦截——Boss 毕业考纪律）。</summary>
        public void ApplyStun(EnemyState e, double seconds)
        {
            if (e == null || e.dead || e.stunImmune || seconds <= 0) return;
            e.stunRemaining = seconds; // 直接赋值 = 刷新语义
        }

        /// <summary>全场施加（F3.2 调试通道 G 键——手摆验收：停走 / 刷新不叠加 / 金 tint）。</summary>
        public void ApplyStunAll(double seconds)
        {
            for (int i = 0; i < Enemies.Count; i++) ApplyStun(Enemies[i], seconds);
        }

        // ============ F4.1 燃烧（06 §12.3：第五状态；火/炮命中挂燃——F4.2/F4.4 落卡生效） ============

        /// <summary>
        /// 施加燃烧（任务单口径：**不叠取最高** dps + **重复触发刷新时长**——连击只续时不叠伤；
        /// burnImmune 拦截（火蜥/岩龟/麟 F5.1 打标）；**burnVulnerable 加成**（鳄 ×BurnVulnerableMult，
        /// F5.2——目标侧标记，故在施加口内乘，B 键调试通道同享）。灰烬增益 ×AshBuffLookup
        /// 由调用方算好传入（施加时刻快照——Damage 施加段 / B 键调试通道共用）。
        /// </summary>
        public void ApplyBurn(EnemyState e, double seconds, double dps)
        {
            if (e == null || e.dead || e.burnImmune || seconds <= 0 || dps <= 0) return;
            if (e.burnVulnerable) dps *= BurnVulnerableMult; // F5.2：燃烧易伤定格（鳄 ×2「水怪怕火」）
            if (dps > e.burnDps) e.burnDps = dps; // 不叠取最高
            e.burnRemaining = seconds;             // 重复触发刷新时长（相位钟不动——续时不重拍）
        }

        /// <summary>全场施加（F4.1 调试通道 B 键——手摆验收：挂燃 / 整秒跳 / 刷新不叠加 / 熄灭清场）。
        /// dps 直取 GameParams（GM 侧）——模拟火塔命中（无灰烬增益半边，归 F4.2 联合验收）。</summary>
        public void ApplyBurnAll(double seconds, double dps)
        {
            for (int i = 0; i < Enemies.Count; i++) ApplyBurn(Enemies[i], seconds, dps);
        }

        /// <summary>
        /// F3.3：覆盖指定位置的灯塔里最高阶（0 = 无灯覆盖/家灯不算——家灯无阶不贡献眩晕时长）。
        /// 法塔眩晕时长 = 此值 ÷ 2（T1 0.5s → T5 2.5s，06 §11.2「多盏灯取最高阶」）。逐塔现算
        ///（塔位低频变化，每次命中一次遍历零负担——不动 lightCache 结构，联动面最小）。
        /// </summary>
        public int GetBestLampLevel(Vector2 pos)
        {
            int best = 0;
            for (int i = 0; i < Towers.Count; i++)
            {
                var t = Towers[i];
                if (t.charId != "灯" || t.range <= 0 || t.level <= best) continue;
                if (Vector2.Distance(pos, t.position) <= t.range + 0.01) best = t.level;
            }
            return best;
        }

        /// <summary>F5.1：覆盖指定位置的灯塔里最高阶（null = 无灯塔覆盖——家灯照明不触发栖灯）。
        /// 枭触发判定与法塔增益判定同构，但返回灯本体（要瞬移/锁定它）。</summary>
        private TowerState FindPerchLamp(Vector2 pos)
        {
            TowerState best = null;
            for (int i = 0; i < Towers.Count; i++)
            {
                var t = Towers[i];
                if (t.charId != "灯" || t.range <= 0) continue;
                if (Vector2.Distance(pos, t.position) <= t.range + 0.01 &&
                    (best == null || t.level > best.level)) best = t;
            }
            return best;
        }

        /// <summary>
        /// F4.2（ch5 魇）：入梦目标 = **同面 + 半径内 + 非镜像**的最近塔（并列取 `Towers` 先序 =
        /// 无随机 → R 回滚重放同结果）。无塔返回 null（该次入梦空放，不屯）。
        /// </summary>
        private TowerState FindSleepTarget(EnemyState e)
        {
            if (e == null || e.sleepRadius <= 0) return null;
            var p = GetEnemyPosition(e);
            TowerState found = null;
            double best = double.MaxValue;
            for (int i = 0; i < Towers.Count; i++)
            {
                var t = Towers[i];
                if (t.isMirror || t.face != e.face) continue; // 影不是实体塔；跨面不生效（铁律）
                double d = Vector2.Distance(p, t.position);
                if (d <= e.sleepRadius && d < best)
                {
                    best = d;
                    found = t;
                }
            }
            return found;
        }

        /// <summary>
        /// F5.1（ch5 傩面 P1，ADR-069）：换脸。面按脸定（赤=实 / 青=虚 / 金=玩家当前面），
        /// **落点 = 另一面同一路程进度**——两面等长（各 41 格，两路互为 y=0 镜像），故 dist 直留不改，只换 route。
        /// 换脸后进 1.5s 可打不还手（招式全停，伤害照吃）；金脸起手/CD 一并归零。
        /// </summary>
        private void ApplyNuoMask(EnemyState e, int mask)
        {
            e.nuoMaskIndex = mask;
            int face = mask == 0 ? 0 : (mask == 1 ? 1 : ActiveFace);
            if (e.face != face && Paths != null)
            {
                e.face = face;
                e.route = Paths.GetFace(face);
            }
            e.nuoGraceRemaining = NuoGraceSeconds;
            e.nuoGoldWarn = 0;
            e.nuoGoldCd = 0;
            e.nuoSealTarget = null;
            e.nuoSmashClock = NuoRedSmashInterval; // 进入赤脸也按满间隔起算（不叠在换脸瞬间）
            NuoMaskChanged?.Invoke(e);
        }

        /// <summary>F5.1 赤脸：同面**最近**一塔（无距离限制；影/跨面不算）。并列取 Towers 先序 = 无随机（R 回滚同序列）。</summary>
        private TowerState FindNuoNearestTower(EnemyState e, TowerState skip = null)
        {
            if (e == null) return null;
            var p = GetEnemyPosition(e);
            TowerState found = null;
            double best = double.MaxValue;
            for (int i = 0; i < Towers.Count; i++)
            {
                var t = Towers[i];
                if (t == skip) continue;
                if (t.isMirror || t.face != e.face) continue;
                double d = Vector2.Distance(p, t.position);
                if (d < best)
                {
                    best = d;
                    found = t;
                }
            }
            return found;
        }

        // ============ F5.2（ch5 傩面 P1，ADR-069）三脸招式 ============

        /// <summary>
        /// 赤脸（实）：每 NuoRedSmashInterval 秒劈**同面最近一塔**——毁塔不毁格（格留空可重建）。
        /// 无距离限制（用户 2026-10-22 定案）；表现层清格/释放视图/飘字归 GameManager。
        /// </summary>
        private void TickNuoRed(EnemyState e, double dt)
        {
            e.nuoSmashClock -= dt;
            if (e.nuoSmashClock > 0) return;
            e.nuoSmashClock = NuoRedSmashInterval;
            var t = FindNuoNearestTower(e);
            if (t != null) NuoSmashTower?.Invoke(e, t);
        }

        /// <summary>
        /// 金脸（跟随玩家面）：起手 NuoGoldWarnSeconds → 封印**当前面距其最近**的塔 NuoGoldSealSeconds。
        /// 起手锁定靶与面：起手期玩家换面 = **打断**（金脸跟面 → 靶已被甩到另一面），进 NuoGoldCdSeconds。
        /// 封印落地后同样进 CD（18s 窗口内最多两次 = 有节奏的压迫，不是连封）。
        /// </summary>
        private void TickNuoGold(EnemyState e, double dt)
        {
            if (e.nuoGoldWarn > 0)
            {
                if (ActiveFace != e.nuoGoldFace)
                {
                    e.nuoGoldWarn = 0;
                    e.nuoGoldCd = NuoGoldCdSeconds;
                    e.nuoSealTarget = null;
                    NuoGoldInterrupted?.Invoke(e);
                    return;
                }
                e.nuoGoldWarn -= dt;
                if (e.nuoGoldWarn > 0) return;
                var t = e.nuoSealTarget;
                e.nuoSealTarget = null;
                e.nuoGoldCd = NuoGoldCdSeconds;
                if (t != null && t.face == e.face && Towers.Contains(t))
                {
                    t.sealRemaining = NuoGoldSealSeconds;
                    NuoSealApplied?.Invoke(e, t);
                }
                return;
            }
            if (e.nuoGoldCd > 0)
            {
                e.nuoGoldCd -= dt;
                return;
            }
            var target = FindNuoNearestTower(e);
            if (target == null) return;              // 场上没塔 = 起手也不起（不空放）
            e.nuoGoldWarn = NuoGoldWarnSeconds;
            e.nuoGoldFace = ActiveFace;
            e.nuoSealTarget = target;
            NuoGoldCast?.Invoke(e, target);
        }

        /// <summary>
        /// F5.1 枭吞噬灯塔（倒计时归零）：移除塔（0 回收——淹没消失管线的近亲：RemoveAt +
        /// RefreshLights + 事件三件套）+ 解锁（防残留态）。语义与 TowerDrowned 分离：吞噬 vs
        /// 沉没（播报与结局不同）；表现层清格/释放视图/播报归 GameManager（OnLampDevoured）。
        /// 枭改动（用户 2026-10-09）：转 public——转阶段半血时栖灯中直接秒杀复用同管线。
        /// </summary>
        public void DevourLamp(TowerState lamp)
        {
            lamp.locked = false; // 已出局，解锁防残留
            Towers.Remove(lamp);
            RefreshLights(); // F1.2：灯没了 → 光集重算（该照明圈变暗）
            LampDevoured?.Invoke(lamp);
        }

        /// <summary>敌军世界坐标（飞行敌上浮 FlyerOffsetY；索敌/弹道/飘字统一用它）。</summary>
        public Vector2 GetEnemyPosition(EnemyState e)
        {
            // F5.1 栖灯 override：枭离路站灯位（可被打 = 杀窗——位置在光圈中心恒亮）。
            if (e.perchTower != null) return e.perchTower.position;
            Vector2 p = e.route.PointAt != null ? e.route.PointAt(e.dist) : Vector2.zero;
            if (e.flying) p.y += FlyerOffsetY;
            return p;
        }

        public void Tick(double dt)
        {
            if (summonPauseRemaining > 0) summonPauseRemaining -= dt; // F5.2：召唤暂停（蛟洪水后的纯抢救窗口）

            // —— 敌人前进 & 到终点 ——
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.dead || e.reached) continue;

                // 激怒（ADR-022）：血量比例过阈值 → 速度×倍率，一次性
                if (!e.enraged && e.enrageAtRatio > 0 && e.hp <= e.maxHp * e.enrageAtRatio)
                {
                    e.enraged = true;
                    e.speed *= e.enrageSpeedMult;
                    EnemyEnraged?.Invoke(e);
                }

                // F5.2（06 §10.4）：Boss 转阶段一次性触发（每敌血线，蛟=1/3）。isBoss 不作门槛——
                // ch1 酋长同标 isBoss，裸用必误触发洪水（零变化铁律破坏）；故 ch1 敌全缺省 0 → 零触发。
                // 序列编排归 GameManager；蛟水乘改写 = 洪水落地时（未怒期间虽在水路也慢速悠游）。
                if (!e.phase2 && e.phase2HpRatio > 0 && e.hp <= e.maxHp * e.phase2HpRatio)
                {
                    e.phase2 = true;
                    // F5.2：GameManager 按 charId 分叉——蛟走洪水两幕剧 / 枭走黑屏换端
                    //（OnOwlPhase2：蓄水 5s → 黑幕 → 换端重走二圈 +25% 速）。phase2 标记先锁
                    //（一次性，本处即触发点）。
                    BossPhase2?.Invoke(e);
                }

                // 定期召唤（ADR-022）：计时到点补召唤物（存活不足上限才补）
                // F5.2：召唤暂停窗口内生兵停摆（蛟洪水后的纯抢救窗口）
                if (!string.IsNullOrEmpty(e.summons) && e.summonInterval > 0 && summonPauseRemaining <= 0)
                {
                    e.summonClock -= dt;
                    if (e.summonClock <= 0)
                    {
                        e.summonClock += e.summonInterval;
                        DoSummon(e);
                    }
                }

                // F4.2（ch5 魇）：入梦——每 sleepInterval 秒让**同面最近**一塔入梦 sleepSeconds 秒
                //（停火不降级；只锁攻击——墨塔照产墨）。镜像跳过（影不是实体塔）。
                // 选靶用「最近 + 并列取 Towers 先序」= 无随机 → R 回滚同序列（ADR-062）。
                if (e.sleepInterval > 0 && !e.dead && !e.reached)
                {
                    e.sleepClock -= dt;
                    if (e.sleepClock <= 0)
                    {
                        e.sleepClock += e.sleepInterval;
                        var victim = FindSleepTarget(e);
                        if (victim != null) victim.sleepRemaining = e.sleepSeconds; // 赋值 = 刷新（不叠层）
                    }
                }

                // F2.2 光-敌交互（06 §11.2）：本帧光态——敌循环首步算好（速度结算 / Damage 光脆
                // 加成 / 视图 tint 三方同帧同值，所见即所得）。fog 关 IsLit 恒真：ch1 狼夜行
                // !lit 恒 false 零触发、ch1 无蛾蝠零路径——铁律零变化。
                e.litCache = IsLit(GetEnemyPosition(e));

                // —— F5.1（ch5 傩面 P1，ADR-069）：三脸轮换 ——
                // 周期 18s：剩 3s 播预告（不动面）→ 到点换脸（**dist 不动 = 另一面同路程进度落点**，
                // 两面等长 41 格）→ 换脸后 1.5s 可打不还手。金脸额外跟随玩家面（玩家换面即同步，
                // 属「跟随」不是「换脸」，不给 grace）。缺省 nuoMask=false → ch1-4 零路径。
                if (e.nuoMask)
                {
                    if (e.nuoCycleClock > 0)
                    {
                        e.nuoCycleClock -= dt;
                        if (!e.nuoWarned && e.nuoCycleClock <= NuoFaceWarnSeconds)
                        {
                            e.nuoWarned = true;
                            NuoMaskWarn?.Invoke(e);
                        }
                        if (e.nuoCycleClock <= 0)
                        {
                            ApplyNuoMask(e, (e.nuoMaskIndex + 1) % 3);
                            e.nuoCycleClock = NuoFaceCycleSeconds;
                            e.nuoWarned = false;
                        }
                    }
                    if (e.nuoGraceRemaining > 0)
                        e.nuoGraceRemaining = System.Math.Max(0, e.nuoGraceRemaining - dt);
                    if (e.nuoMaskIndex == 2 && Paths != null && e.face != ActiveFace)
                    {
                        e.face = ActiveFace;                 // 金脸：跟随玩家所在面（同进度换路）
                        e.route = Paths.GetFace(e.face);
                        NuoFaceFollowed?.Invoke(e);          // 表现层同帧改明暗（否则会停在「对面半透明」）
                    }
                    // —— F5.2（ch5 傩面 P1）三脸招式：grace（换脸后 1.5s）= 可打不还手，全停 ——
                    if (e.nuoGraceRemaining <= 0)
                    {
                        if (e.nuoMaskIndex == 0) TickNuoRed(e, dt);        // 赤（实）：劈最近一塔
                        else if (e.nuoMaskIndex == 2) TickNuoGold(e, dt);  // 金（跟随面）：起手 → 封印
                    }
                }

                // —— F6.1/F6.2（ch5 傩神 P2，ADR-070）：傩舞 + 三面施法 ——
                // 傩舞每 10s 起一次、停 2.5s：停步 + 受伤 ×1.5 + **三面同时施法**（赤劈、金封立即，
                // 青召由 GameManager 在 ShenDanceStarted 里补——虚面 3 鬼）。非舞期赤/金各按 7s 走。
                if (e.nuoShen)
                {
                    if (e.shenDanceRemaining > 0)
                    {
                        e.shenDanceRemaining = System.Math.Max(0, e.shenDanceRemaining - dt);
                        if (e.shenDanceRemaining <= 0) ShenDanceEnded?.Invoke(e);
                    }
                    else
                    {
                        e.shenDanceClock -= dt;
                        if (e.shenDanceClock <= 0)
                        {
                            e.shenDanceClock = ShenDanceInterval;
                            e.shenDanceRemaining = ShenDanceSeconds;
                            e.shenSmashClock = ShenSmashInterval;
                            e.shenSealClock = ShenSealInterval;
                            ShenDanceStarted?.Invoke(e);
                            var danceSmash = FindNuoNearestTower(e);
                            if (danceSmash != null) NuoSmashTower?.Invoke(e, danceSmash);
                            var danceSeal = FindNuoNearestTower(e, danceSmash); // 劈与封不砸同一座（还有塔时）
                            if (danceSeal != null)
                            {
                                danceSeal.sealRemaining = NuoGoldSealSeconds;
                                NuoSealApplied?.Invoke(e, danceSeal);
                            }
                        }
                        else
                        {
                            e.shenSmashClock -= dt;
                            if (e.shenSmashClock <= 0)
                            {
                                e.shenSmashClock = ShenSmashInterval;
                                var sm = FindNuoNearestTower(e);
                                if (sm != null) NuoSmashTower?.Invoke(e, sm);
                            }
                            e.shenSealClock -= dt;
                            if (e.shenSealClock <= 0)
                            {
                                e.shenSealClock = ShenSealInterval;
                                var sl = FindNuoNearestTower(e);
                                if (sl != null)
                                {
                                    sl.sealRemaining = NuoGoldSealSeconds;
                                    NuoSealApplied?.Invoke(e, sl);
                                }
                            }
                        }
                    }
                }

                // F5.1 枭栖灯触发（06 §11.4）：踏入灯塔照明圈 → 瞬移光源灯（多盏取最高阶——
                // 法塔 GetBestLampLevel 同款口径备案）。家灯光照不算（无灯可栖——枭在家灯圈
                // 照常走，但入光即可打）；塔攻击对栖灯枭天然放行（位置 = 灯位 = 光圈中心恒亮
                // = 杀窗）。锁死 + 5×阶秒倒计时；ch1/2 无枭零路径（铁律）。
                if (e.perchTower == null && e.charId == "枭" && !e.dead && !e.reached)
                {
                    var lamp = FindPerchLamp(GetEnemyPosition(e));
                    if (lamp != null)
                    {
                        e.perchTower = lamp;
                        lamp.locked = true;
                        e.perchRemaining = PerchSecondsPerLevel * lamp.level;
                        e.perchReturnDist = e.dist; // 记路面位（灯灭回位续走——不推进度）
                    }
                }

                // F3.1 状态结算（ADR-033）：冻结 > 缓速防呆（冻住就别动，缓速照样倒计时）；
                // 湿不锁任何东西——只作冰塔倍率标记。冻结只停位移，激怒/召唤照常（防呆：单一职责）。
                // F3.2（ADR-039）眩晕插链：冻结 > 眩晕 > 移动——冻结中触发的眩晕满程等待
                //（冻结优先，结束后从头倒计时 = 「取最长锁定」语义；眩晕只停位移同冻结）。
                // F5.1 栖灯插链：冻结 > 栖灯 > 眩晕 > 移动——栖灯只停位移（枭免疫眩晕无冲突）。
                if (e.slowRemaining > 0) e.slowRemaining -= dt;
                if (e.wetRemaining > 0) e.wetRemaining -= dt;
                if (e.frozenRemaining > 0) e.frozenRemaining -= dt;
                else if (e.perchTower != null)
                {
                    // F5.1 栖灯中：站灯位（GetEnemyPosition override）不移动、dist 不动（不推进度）。
                    // 灯被外因移除（锁死拦了移/卖/合并，仅剩淹没路径——TowerDrowned 管线）→ 直接
                    // 回位（灯已没了，无吞噬可做）；倒计时归零 → 吞噬（DevourLamp：消失 0 回收）+ 回位。
                    if (!Towers.Contains(e.perchTower))
                        e.perchTower = null;
                    else
                    {
                        e.perchRemaining -= dt;
                        if (e.perchRemaining <= 0)
                        {
                            var lamp = e.perchTower;
                            e.perchTower = null;
                            DevourLamp(lamp);
                        }
                    }
                    if (e.perchTower == null) e.dist = e.perchReturnDist; // 回原路面位续走
                }
                else if (e.stunRemaining > 0) e.stunRemaining -= dt; // 眩晕：停走 + 计时（对齐冻结分支位）
                else if (!e.holdMove) // F5.2 转阶段定身（蓄水窗站桩；激怒/召唤/状态计时照走）
                {
                    // F4.1（ADR-034）：水中速乘 × 缓速乘叠乘（不互斥；激怒已永久并入 e.speed）。
                    // 水中 = 水路航道（lane 1 且未过汇合点里程）或全图洪水（水线 ≥ 4，F5 蛟转阶段用）。
                    double mult = 1;
                    if (e.slowRemaining > 0) mult *= 1 - e.slowFactor;
                    // F5.1（ch5 傩面）：青脸移速 ×1.25（黄底）——「血薄但更快」的另一半
                    if (e.nuoMask && e.nuoMaskIndex == 1) mult *= NuoGreenSpeedMult;
                    if (e.waterSpeedMult > 1 &&
                        (WaterLevel >= 4 || (e.lane == 1 && e.dist < e.route.WaterUntil)))
                        mult *= e.waterSpeedMult;
                    // F2.2 光-敌交互：蝠光滞 ×0.7 / 狼夜行雾中 ×1.3（入光还原 = 不乘）。
                    if (e.lightHindered && e.litCache) mult *= LightHinderedMult;
                    if (e.nocturnal && !e.litCache) mult *= NocturnalFogMult;
                    // F5.1 火蜥「灾害区原生」：踩在红印格上提速 ×1.3（红印格集合由 GM 同帧同步——
                    // GetEnemyPosition 走同一坐标口径：栖灯/飞高都跟着走；火蜥不飞不栖无实害）。
                    if (e.lavaMarkSpeedMult > 1 && IsOnLavaMark(GetEnemyPosition(e))) mult *= e.lavaMarkSpeedMult;
                    // F2.2（ch5 双面）：非激活面只压**移动**——夜行（影）与 Boss 免疫（06 §14.2 黄底 0.5）。
                    // DoT/弹道/召唤不冻结（本块外的平行块照走）。ch1-4 face 恒 0 = ActiveFace → 零路径。
                    if (e.face != ActiveFace && !e.nocturnal && !e.isBoss) mult *= FaceInactiveSpeedMult;
                    // F6（ch5 傩神）：傩舞停步（2.5s 定身——唯一高效输出窗，也是唯一危险窗）
                    if (e.nuoShen && e.shenDanceRemaining > 0) mult = 0;
                    // 天赋·咋瓦鲁多（用户 2026-11-04）：时停期间敌全静止（含 Boss），塔照打
                    if (TimeStopRemaining > 0) mult = 0;
                    e.dist += e.speed * mult * dt;
                }

                // F4.1 燃烧 tick（06 §12.3 黄底 3/秒×3s——整秒跳与塔攻速/红印 DoT 同节拍，防
                // -0.0x 小数飘字刷屏）：**位移链的平行兄弟块**（非链内分支——燃烧不锁任何东西，
                // 冻结/眩晕/栖灯中照样烧，湿同构只作标记）；伤害走 Damage(e, dps, null) 无主口径
                //（i-frame 照拦、击杀走正常死亡管线——分裂当帧追加表尾，本循环正序索引不回访新增，
                // 下一帧续烧）。入口三门（未死未达 + 在烧或有整拍未付）：帧界恰好压秒时 rem/tick
                // 同源 dt 漂移至多 ε，双钟门互兜防丢尾拍（3s 恰 3 拍）；熄灭清场（dps 归零）防
                // 陈旧残留跨状态误伤。
                if (!e.dead && !e.reached && (e.burnRemaining > 0 || e.burnTick >= 1.0))
                {
                    if (e.burnRemaining > 0)
                    {
                        e.burnTick += dt;
                        e.burnRemaining -= dt;
                    }
                    while (e.burnTick >= 1.0)
                    {
                        e.burnTick -= 1.0;
                        Damage(e, e.burnDps, null); // 受击反馈（闪白+飘字）经 EnemyDamaged 免费获得
                    }
                    if (e.burnRemaining <= 0)
                    {
                        e.burnRemaining = 0;
                        e.burnTick = 0;
                        e.burnDps = 0; // 熄灭清场
                    }
                }
                if (e.dist >= e.route.Length) // F1.2：各自线路的终点（水路敌也汇到印章）
                {
                    // F7.5② 二圈顺延（用户定案 2026-10-19）：Boss 带未兑换的额外圈（麟转阶段发放）
                    // → 到终点**不判败**，重置回环起点续走（第二圈到终点才判）。lapsLeft 两条兑换
                    // 路径（本处 / 转阶段收尾重置）都清零 → 必不双圈。并不扣 Lives、不置 reached。
                    // F5.1（ch5 傩面，用户 2026-10-22 定案）：**到出口只扣 3 印 + 回起点重走**
                    //（不判死、不结束本关；印扣光照常败）。与麟的 lapsLeft 二圈不同：那是一次性
                    // 额外圈、本处是无限圈（Boss 只要没被打死就一直转）。
                    if (e.endlessLap)
                    {
                        Lives -= e.livesCost;
                        e.dist = 0;          // 回出怪口（dist 归零 = 同面同进度起点）
                        e.chargeWalked = 0;
                        BossLooped?.Invoke(e);
                        RaiseStats();
                        if (Lives <= 0) Failed = true;
                    }
                    else if (e.isBoss && e.lapsLeft > 0)
                    {
                        e.lapsLeft--;
                        e.dist = 0;          // 路径进度归零 = 回出怪口（麟为回环章图——见 06 §12.5）
                        e.chargeWalked = 0;  // 蓄力里程随圈起算
                        BossLooped?.Invoke(e);
                        RaiseStats();
                    }
                    else
                    {
                        e.reached = true;
                        Lives -= e.livesCost;
                        // F4.4（ch5 小鬼）：**墨漏**——到终点扣墨不扣印（livesCost 配 0）；下限 0 不为负。
                        // 其余敌 inkLeak = 0 → 本行零路径（ch1-4 零变化）。
                        if (e.inkLeak > 0) Ink = Math.Max(0, Ink - e.inkLeak);
                        EnemyReached?.Invoke(e);
                        if (e.isBoss || Lives <= 0) Failed = true; // Boss 到点即败（敌人草稿.csv）
                        RaiseStats();
                    }
                }
            }

            // —— 塔索敌攻击（离终点最近优先）——
            for (int i = 0; i < Towers.Count; i++)
            {
                var t = Towers[i];
                // F4.2（ch5 魇）：入梦计时**先于面闸**——换面不冻结持续效果（docs/06 §14.2「入梦继续
                // 计时」），否则睡在对面那面的塔会被「压住秒数」，切回来还剩一大截。
                if (t.sleepRemaining > 0) t.sleepRemaining -= dt;
                // F5.2（ch5 傩面金脸）：封印计时同入梦口径——先于面闸扣（换面不冻结秒数）
                if (t.sealRemaining > 0) t.sealRemaining -= dt;
                // F2.2（ch5 双面）：非激活面塔整层停火（产墨/攻击/淹没计时全停——「不在场」语义；
                // 视图由表现层 SetActive(false) 同步隐藏）。ch1-4 face 恒 0 = 零路径。
                if (t.face != ActiveFace) continue;
                // 临时格（用户 2026-10-23）：左下「暂存」格上的塔不攻击/不产墨（整段跳过；
                // 塔仍在模型里，可正常拿/移/卖/换位——只是当格子是「沉默」的）。零坐标 = 不启用。
                if (t.position.x == TempCellX && t.position.y == TempCellY) continue;
                // F3.1 修订（用户 2026-10-22）：镜像不占格 —— 同面同坐标站着别的塔 = 影被盖住：
                // 不攻击、不产墨、不参与任何结算（表现层同刻隐去）。占位塔一走自动恢复（无状态机）。
                if (t.isMirror && MirrorBlocked(t)) continue;
                // F2.2 淹没沉默（ADR-032 / GDD §3.4）：非水系塔海拔 ≤ 水线 → 不攻击不产墨，
                // 头顶倒计时（SubmergeSeconds 黄底 20）归零 → 消失（0 回收）；塔干 = 解除 + 清零。
                // ch1：海拔恒 4 > 水线恒 0 → 恒干，本块零触发（铁律不破）。
                if (t.submerged)
                {
                    if (t.waterImmune || t.elevation > WaterLevel)
                    {
                        t.submerged = false;
                        t.submergeCountdown = SubmergeSeconds; // 塔干：倒计时清零（可再淹再计）
                    }
                    else
                    {
                        t.submergeCountdown -= dt;
                        if (t.submergeCountdown <= 0)
                        {
                            Towers.RemoveAt(i);
                            i--;
                            RefreshLights(); // F1.2：淹没出局（若为灯）→ 光集重算
                            TowerDrowned?.Invoke(t); // 沉默出局：表现层清格/释放视图（「不动」结局）
                        }
                        continue; // 沉默：产墨与攻击全停
                    }
                }
                else if (!t.waterImmune && t.elevation <= WaterLevel)
                {
                    t.submerged = true;
                    t.submergeCountdown = SubmergeSeconds; // 入水置满（防字段默认值与注入值漂移）
                    continue; // 入水即沉默：本 tick 不再产墨/攻击（塔干解除则当 tick 即恢复行动）
                }
                // 墨塔离散产墨（ADR-027，用户需求 2026-10-02：每 InkPulseSeconds 一跳，替代连续滴灌）
                if (t.inkPerPulse > 0)
                {
                    t.inkTimer += dt;
                    var pulseInterval = InkPulseSeconds < 0.25 ? 0.25 : InkPulseSeconds; // 防呆下限（0 → 帧帧跳）
                    // F3.3 渔（ADR-033 / GDD §5.2）：陆上跳隔 ×2（水中 5s = 墨塔基准 → 陆上 10s）。
                    // 水中 = 永久水格（onWaterCell）或当前被淹（海拔 ≤ 水线）——渔免疫沉默照常产
                    //（GDD §5.2：涨潮期间渔是唯一不停工的经济）。ch1 塔全 landPulseSlower=false → 零变化。
                    if (t.landPulseSlower && !(t.onWaterCell || t.elevation <= WaterLevel))
                        pulseInterval *= 2;
                    if (t.inkTimer >= pulseInterval)
                    {
                        t.inkTimer -= pulseInterval;
                        Ink += t.inkPerPulse;
                        InkPulsed?.Invoke(t, t.inkPerPulse); // 0.3：产墨跳档反馈（表现层飘 +N）
                    }
                }
                t.cooldown -= dt;
                if (t.damage <= 0) continue; // 无攻击塔（墨）
                if (t.sleepRemaining > 0) continue; // F4.2 入梦：停火（醒后按已走完的冷却立刻开枪）
                if (t.sealRemaining > 0) continue;  // F5.2 傩面金脸封印：停火（同上口径——冷却照走）
                if (t.cooldown > 0) continue;
                var target = FindTarget(t);
                if (target == null) continue;
                t.cooldown = t.interval;
                TowerFired?.Invoke(t, target);

                if (t.ranged)
                {
                    // 远程：发射弹道，命中时才结算伤害
                    var p = new ProjectileState
                    {
                        position = t.position,
                        target = target,
                        speed = ProjectileSpeed,
                        damage = t.damage,
                        charId = t.charId,
                        source = t
                    };
                    Projectiles.Add(p);
                    ProjectileSpawned?.Invoke(p);
                }
                else
                {
                    // 近战：瞬发（刀单体；棍圆形/戟扇形以塔为中心 AOE）
                    if (t.aoeShape == AoeShape.None) Damage(target, t.damage, t);
                    else DamageArea(t, t.position, target);
                }
            }

            // —— 弹道飞行与命中（追踪目标当前位置）——
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var p = Projectiles[i];
                bool remove;
                if (p.target.dead || p.target.reached)
                {
                    remove = true; // 目标没了：弹丸消散
                }
                else
                {
                    Vector2 to = GetEnemyPosition(p.target) - p.position;
                    double step = p.speed * dt;
                    if (to.magnitude <= step)
                    {
                        // 矛 AOE：以命中点为中心矩形溅射；单体塔照旧直伤
                        var src = p.source;
                        if (src != null && src.aoeShape != AoeShape.None)
                            DamageArea(src, p.position, p.target);
                        else
                        {
                            Damage(p.target, p.damage, p.source); // F3.1：带攻击方（状态施加）
                            // F3.2（ch5 雷）：主目标命中后沿路径后方链电（chainJumps = 0 的塔零路径）
                            if (src != null && src.chainJumps > 0)
                                ApplyChain(src, p.target, p.position, p.damage);
                        }
                        ProjectileHit?.Invoke(p);
                        remove = true;
                    }
                    else
                    {
                        // Unity 的 Vector2 只有 float 运算符重载（无 double），模型数值进来须显式转
                        p.position += to / to.magnitude * (float)step;
                        remove = false;
                    }
                }
                if (remove)
                {
                    Projectiles.RemoveAt(i);
                    ProjectileRemoved?.Invoke(p);
                }
            }

            // —— 清理已结算单位 ——
            Enemies.RemoveAll(e => e.dead || e.reached);
        }

        /// <summary>
        /// F3.4 冰对湿敌的有效伤害（GDD §5.2 湿 ×2）。结算（Damage）与飘字显示共用本函数
        /// ——单一来源，改规则只动这里。攻击方 null / 非冰 / 敌不湿 → 原值。
        /// </summary>
        public static double EffectiveDamage(TowerState attacker, double dmg, EnemyState target)
        {
            if (attacker != null && attacker.wetDamageMult > 1 &&
                target != null && target.wetRemaining > 0)
                return dmg * attacker.wetDamageMult;
            return dmg;
        }

        /// <summary>
        /// 伤害结算（近战瞬发与弹道命中共用）：含击杀、掉墨、分裂、事件、F3.1 状态施加。
        /// attacker = 攻击塔（缓速/湿/冻结按它的字段施加；null = 无来源纯伤害）。
        /// 湿加成先算（EffectiveDamage——飘字同源），状态后施（打死了就不糊状态）。
        /// </summary>
        private void Damage(EnemyState target, double dmg, TowerState attacker)
        {
            if (target.invulnerable) return; // F5.2 转阶段 i-frame（蓄水窗）：零伤零状态——演出不可打断（防抢杀跳戏）

            dmg = EffectiveDamage(attacker, dmg, target); // F3.4 冰打湿敌 ×2
            // F3.3（ch5 煞）：虚面伤害 ×1.5（唯一虚面加成，只落在卡身上、不改全局数值）。
            // 判定读**攻击方所在面**（塔只能打同面敌 → 等价读目标面）；voidDamageMult 缺省 1 = ch1-4 零变化。
            if (attacker != null && attacker.face == 1) dmg *= attacker.voidDamageMult;
            // F2.2 光脆（06 §11.2）：蛾光中受伤 ×1.5（飞蛾扑火）——读 litCache（敌循环本帧已算，
            // 与速度结算/视图 tint 同值）。fog 关 litCache 恒 true + ch1 无蛾 → 零路径零变化。
            if (target.lightVulnerable && target.litCache) dmg *= LightVulnerableMult;
            // F5.1（ch5 傩面）：青脸「血薄」= 受到伤害 ×1.5（唯一高效输出窗；缺省 false/0 旧敌零路径）
            if (target.nuoMask && target.nuoMaskIndex == 1) dmg *= NuoGreenDamageTakenMult;
            // F6（ch5 傩神）：傩舞窗受伤 ×1.5（唯一高效输出窗）
            if (target.nuoShen && target.shenDanceRemaining > 0) dmg *= ShenDanceDamageMult;
            // F4.1/F7.4/F4.5 灰烬增益（用户 2026-10-18 变更：扩到**全部伤害**）：火/炮/烬站灰烬格 →
            // 直接伤害同享 ×ashDamageMult（原只加成燃烧跳伤——3/s vs 直接 27/s 体感≈零）。
            // 取命中时刻（塔移出灰格立即失 buff）；飘字同源（本值随下方 EnemyDamaged 出去）。
            if (attacker != null) dmg *= AshBuffLookup?.Invoke(attacker) ?? 1.0;

            target.hp -= dmg;
            EnemyDamaged?.Invoke(target, dmg, attacker); // 0.4：逐敌受击反馈（溅射可见；击杀 = 字与死亡演出同帧，沿旧主目标口径）
            // F4.3（ch5 傀）：**换影复活**——首次致死改为「另一面同进度 + 50% 血」复活一次。
            // 语义上**不算死亡**：不掉墨 / 不加魂 / 不触发分裂 / 不计击杀（Kills 不动），
            // 故本块先于死亡管线并直接 return（跳过本次命中的后续状态施加——过杀伤害一并作废）。
            if (target.hp <= 0 && !target.dead && target.reviveHpRatio > 0 && !target.revived)
            {
                target.revived = true;
                target.face = target.face == 1 ? 0 : 1;                       // 换到另一面
                if (Paths != null) target.route = Paths.GetFace(target.face);  // 另一面的同等长路径（同进度 dist 不动）
                target.hp = target.maxHp * target.reviveHpRatio;
                target.dead = false;
                EnemyRevived?.Invoke(target); // 表现层飘字/音效；ch1-4 无 reviving 敌 → 零路径
                return;
            }
            if (target.hp <= 0 && !target.dead)
            {
                target.dead = true;
                Kills++;
                Ink += target.inkDrop;
                AddSoul(); // F3.4（ch5 幡）：敌死 +1 魂（两面都算；无幡 = 只计数不结算）
                SplitOnDeath(target); // 先于 EnemyDied：产物挂父波次时父还在 enemyWave（GameManager 侧注册依赖此时序）
                EnemyDied?.Invoke(target);
                RaiseStats();
                return;
            }

            // F3.3 法塔眩晕（06 §11.2）：法塔**位于光照内**（IsLit 谓词复用——移出光圈即失效）
            // → 命中 20% 概率眩晕，时长 = 覆盖灯最高阶 ÷ 2（家灯无阶不贡献；多灯取最高）。
            // 先伤后眩（血扣了才晕）；枭免疫归 F5.1 调用侧拦截（stunImmune 批次）。ch1/ch2
            // 无法卡（池未含）+ 光集恒空 → 零路径（铁律）。
            if (attacker != null && attacker.charId == "法" && IsLit(attacker.position))
            {
                int lampLevel = GetBestLampLevel(attacker.position);
                if (lampLevel > 0 && UnityEngine.Random.value < MageStunChance)
                    ApplyStun(target, lampLevel / 2.0);
            }

            // F3.1 状态施加（GDD §5.2：水=缓速+湿；冰=冻结）：数值即塔池定义，命中刷新不叠加
            if (attacker != null && !target.dead)
            {
                if (attacker.slowFactor > 0 && attacker.slowSeconds > 0)
                {
                    target.slowFactor = attacker.slowFactor;
                    target.slowRemaining = attacker.slowSeconds;
                }
                if (attacker.wetSeconds > 0)
                    target.wetRemaining = attacker.wetSeconds;
                if (attacker.freezeSeconds > 0 && !target.phase2) // F5：蛟转阶段后免疫冰冻（用户三案选一 2026-10-05：
                    target.frozenRemaining = attacker.freezeSeconds; // 一阶段照吃=削血工具；二阶段免疫=护冲锋戏，冰塔连冻锁不死毕业考）
                // F4.1 燃烧施加（06 §12.3 火/炮命中挂燃——F4.2/F4.4 落卡赋值非 0 才有路径）：
                // 不叠取最高 + 重复触发刷新时长（任务单口径）；灰烬增益统一律 = AshBuffLookup
                //（火/炮站灰烬格 ×ashDamageMult——F7.4 起直接伤害同享，此处为燃烧施加侧定格，「死灰复燃」F3.2 半边于此闭环）；
                // burnImmune 拦截（F5.1 敌打标——火蜥/岩龟已置 true）；burnVulnerable 加成在
                // ApplyBurn 内施加侧定格（F5.2 鳄 ×2）。
                if (attacker.burnSeconds > 0 && attacker.burnDps > 0 && !target.burnImmune)
                    ApplyBurn(target, attacker.burnSeconds,
                        attacker.burnDps * (AshBuffLookup?.Invoke(attacker) ?? 1.0));
            }
        }

        /// <summary>
        /// F2.3/F2.4 岩浆灾害伤害（06 §12.2）：无攻击方纯伤害——路径格 burst 15 / 红印 DoT 2/秒。
        /// attacker = null → 不享冰湿/法眩晕/缓速冻结/燃烧施加（纯环境伤）；i-frame（Boss
        /// 转阶段无敌）照拦；蛾光脆 ×1.5 属敌侧状态与攻击方无关照吃（ch4 无蛾零路径）。
        /// **F4.1 统一定夺（2026-10-13）**：红印 DoT 走 lavaMarkDps 自有通道——**不入 burn
        /// 状态**（不刷新时长/不享灰烬增益/敌侧免疫位共用 burnImmune 拦截语义一致）：
        /// burn = 塔挂的计时状态（3/s×3s 衰减）、红印 = 地格驻留区（2/s 驻留即烧）——两律
        /// 分立（06 §12.2 字面）；黄底可推翻。
        /// </summary>
        public void DamageLava(EnemyState target, double dmg) => Damage(target, dmg, null);

        /// <summary>死亡分裂（ADR-022）：蛛→虫×3；分裂产物不掉墨（敌人草稿.csv「防刷」），生成于死亡位置后方。</summary>
        private void SplitOnDeath(EnemyState parent)
        {
            if (string.IsNullOrEmpty(parent.onDeathSpawns) || parent.onDeathSpawnCount <= 0) return;
            var s = EnemyStatsLookup?.Invoke(parent.onDeathSpawns);
            if (s == null)
            {
                Debug.LogWarning("[BattleModel] 分裂产物不在敌人表：" + parent.onDeathSpawns);
                return;
            }
            for (int k = 0; k < parent.onDeathSpawnCount; k++)
            {
                var child = SpawnEnemy(s, parent, true, 0, 1.0, parent.face); // noInkDrop：分裂虫不掉墨；F2.1 随父面
                child.dist = Math.Max(0, parent.dist - 0.3 - k * 0.4); // 略错开防叠字
            }
        }

        /// <summary>定期召唤（ADR-022）：每 summonInterval 秒 × summonCount 只，同场存活上限 summonCap。</summary>
        private void DoSummon(EnemyState summoner)
        {
            int alive = 0;
            for (int j = 0; j < Enemies.Count; j++)
            {
                var o = Enemies[j];
                if (!o.dead && !o.reached && o.spawner == summoner) alive++;
            }
            int n = Math.Min(summoner.summonCount, summoner.summonCap - alive);
            if (n <= 0) return;

            var s = EnemyStatsLookup?.Invoke(summoner.summons);
            if (s == null)
            {
                Debug.LogWarning("[BattleModel] 召唤物不在敌人表：" + summoner.summons);
                return;
            }
            for (int k = 0; k < n; k++)
            {
                var child = SpawnEnemy(s, summoner, false, 0, 1.0, summoner.face); // F2.1：召唤物随召主面
                child.dist = Math.Max(0, summoner.dist - 0.5 - k * 0.5); // 跟在召唤者身后错开
            }
        }

        /// <summary>
        /// F3.1 修订（用户 2026-10-22）：镜像不占格 —— 「影被盖住」= 自己那一面、同坐标上站着
        /// 一枚**非镜像**塔。盖住期间影完全不存在（不攻击/不产墨；表现层 SetActive(false) 同真值）。
        /// 只在镜像身上调用（每帧 ≤ 镜数，塔表 &lt; 40 直扫）。
        /// </summary>
        public bool MirrorBlocked(TowerState m)
        {
            if (m == null || !m.isMirror) return false;
            int x = Mathf.RoundToInt(m.position.x), y = Mathf.RoundToInt(m.position.y);
            for (int i = 0; i < Towers.Count; i++)
            {
                var o = Towers[i];
                if (o == null || o == m || o.isMirror || o.face != m.face) continue;
                if (Mathf.RoundToInt(o.position.x) == x && Mathf.RoundToInt(o.position.y) == y) return true;
            }
            return false;
        }

        /// <summary>
        /// F3.4（ch5 幡）：敌死 +1 魂（**两面都算**——本方法不读 face）；每到 `SoulsPerPayout`（10）的倍数
        /// 结算一次：取场上**最高阶**幡，产墨 = `soulPayout × 2^(级-1)`（多幡不叠加、不重复结算）。
        /// 当次场上无幡 → 该次不结算（不屯着、不补发——避免「先攒魂再补幡白嫖」）。
        /// ch1-4 无幡 → 魂只增不发 = 零路径零表现。
        /// </summary>
        private void AddSoul()
        {
            Souls++;
            if (SoulsPerPayout <= 0 || Souls % SoulsPerPayout != 0) return;
            TowerState best = null;
            for (int i = 0; i < Towers.Count; i++)
            {
                var b = Towers[i];
                if (b == null || !b.soulBanner) continue;
                if (best == null || b.level > best.level) best = b; // 同阶取先入者（幂等，无实质差异）
            }
            if (best == null) return;
            double amount = best.soulPayout * Math.Pow(2, best.level - 1);
            Ink += amount;
            SoulsPulsed?.Invoke(best, amount);
        }

        /// <summary>
        /// 范围伤害（v3.0 AOE）：AOE 塔**溅射**均「仅地面」（GDD §5.1 特效列，空中免疫溅射）。
        /// 矛 Rect 以命中点为中心；棍 Circle / 戟 Sector 以塔为中心（Sector 朝主目标方向）；
        /// F4.4 炮 Circle 走远程 = 命中点为中心。
        /// **直击例外**（F4.4）：主目标照伤不吃空中豁免——远程 AoE 塔（炮，antiAir）的命中点
        /// 就是主目标，若整个跳过飞行敌 = 直击落空（溅射集合的空中豁免只该管溅射）。
        /// 非对空塔（ch1-3 矛/棍/戟）FindTarget 永不返回飞行敌 → 主目标恒地面 = 零变化（铁律）。
        /// </summary>
        private void DamageArea(TowerState t, Vector2 center, EnemyState primary)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.dead || e.reached) continue;
                if (e.face != t.face && !e.crossFaceAttackable) continue;     // F2.2（ch5 双面）：溅射同样只打本面（无例外）；F6.1 傩神 crossFaceAttackable = 两面皆可溅射
                bool primaryHit = e == primary;      // 直击主目标（含飞行——F4.4）
                if (e.flying && !primaryHit) continue; // 空中免疫溅射
                Vector2 pos = GetEnemyPosition(e);
                bool hit = false;
                switch (t.aoeShape)
                {
                    case AoeShape.Rect: // 轴对齐矩形（宽×高，中心=命中点）
                        hit = Math.Abs(pos.x - center.x) <= t.aoeSize / 2 &&
                              Math.Abs(pos.y - center.y) <= t.aoeParam / 2;
                        break;
                    case AoeShape.Circle: // 圆（中心=传入点：近战=塔位 / 远程=命中点）
                        hit = (pos - center).sqrMagnitude <= t.aoeSize * t.aoeSize;
                        break;
                    case AoeShape.Sector: // 扇形（中心=塔位，朝主目标，半径×张角）
                        double d2 = (pos - center).sqrMagnitude;
                        if (d2 <= t.aoeSize * t.aoeSize)
                        {
                            Vector2 face = GetEnemyPosition(primary) - center;
                            Vector2 to = pos - center;
                            double diff = Math.Atan2(to.y, to.x) - Math.Atan2(face.y, face.x);
                            while (diff > Math.PI) diff -= 2 * Math.PI;
                            while (diff < -Math.PI) diff += 2 * Math.PI;
                            hit = Math.Abs(diff) <= (t.aoeParam * Math.PI / 180.0) / 2;
                        }
                        break;
                }
                if (hit || primaryHit) Damage(e, t.damage, t); // F3.1：AOE 溅射同样带攻击方（状态随溅射施加）
            }
        }

        /// <summary>
        /// F3.2（ch5 雷）：链电 —— 主目标命中后**沿路径后方**（dist 更小 = 更靠近入口）逐跳传导，
        /// 每跳伤害 ×chainRatio（26 → 15.6 → 9.4 → 5.6），下一跳须在上一跳落点 chainRadius（2.0）内，
        /// 无合法目标 / 超距 / 已链过 → 断链。**同面筛选**（与 FindTarget 同一条铁律，无例外）；
        /// 可打空中（雷 antiAir 直击 → 链不另设空中豁免，链距按真实坐标含悬空高度）。
        /// chainJumps == 0（ch1-4 全部塔缺省）→ 本方法永不被调用 = 零路径。
        /// </summary>
        private void ApplyChain(TowerState t, EnemyState primary, Vector2 hitPos, double baseDamage)
        {
            if (t == null || primary == null || t.chainJumps <= 0) return;
            var hit = new HashSet<EnemyState>();
            hit.Add(primary);
            var cur = primary;
            Vector2 curPos = hitPos;
            double dmg = baseDamage;

            for (int k = 0; k < t.chainJumps; k++)
            {
                dmg *= t.chainRatio;
                EnemyState next = null;
                Vector2 nextPos = default;
                double bestD = double.MaxValue;
                for (int i = 0; i < Enemies.Count; i++)
                {
                    var e = Enemies[i];
                    if (e == null || e.dead || e.reached) continue;
                    if (e.face != t.face) continue;              // 同面（跨面不可链）
                    if (hit.Contains(e)) continue;               // 已链过不回头
                    if (e.dist >= cur.dist) continue;            // 只在路径后方
                    Vector2 pos = GetEnemyPosition(e);
                    double d = (pos - curPos).magnitude;
                    if (d > t.chainRadius) continue;             // 链距超限 → 断
                    if (d < bestD) { bestD = d; next = e; nextPos = pos; }
                }
                if (next == null) break;
                ChainLinked?.Invoke(curPos, nextPos);            // 表现层电弧（可砍档）
                Damage(next, dmg, t);
                hit.Add(next);
                cur = next;
                curPos = nextPos;
            }
        }

        private EnemyState FindTarget(TowerState t)
        {
            EnemyState best = null;
            double bestDist = -1;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.dead || e.reached) continue;
                if (e.face != t.face && !e.crossFaceAttackable) continue; // F2.1/F2.2（ch5 双面）：跨面不可索敌（一条谓词覆盖全部塔，无例外）；F6.1 傩神 crossFaceAttackable = 两面皆可索敌
                if (e.flying && !t.antiAir) continue; // 空中：仅对空塔可命中（近战刀打不了）
                Vector2 pos = GetEnemyPosition(e); // 含飞行敌悬空高度，射程判定与视觉一致
                if ((pos - t.position).sqrMagnitude > t.range * t.range) continue;
                if (!IsLit(pos)) continue; // F1.2 交战谓词：雾中敌不可选中（缓速/冻结随无目标拦在源头；fog 关恒真短路）
                if (e.dist > bestDist)
                {
                    bestDist = e.dist;
                    best = e;
                }
            }
            return best;
        }

        private void RaiseStats() => StatsChanged?.Invoke(Ink, Lives);
    }
}
