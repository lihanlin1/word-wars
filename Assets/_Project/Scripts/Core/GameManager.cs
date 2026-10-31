using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 全局单例：驱动战斗模型、管理视图池、处理输入与 HUD。
    /// v3.0（ADR-016/017）：词塔合成/喂养删除，合并心智只留同字同级升阶；
    /// 抽卡一口价 = 池总价值；卖 = 卡价值 × 50% × 字位；铲为池内普通卡。
    /// 判定走 MergeRules 纯逻辑层；交互真值表见 03_技术方案 §4.3.1
    /// （本文件 OnCellClicked 为其路由实现）。
    /// </summary>
    public class GameManager : MonoBehaviour, IPathSet
    {
        public static GameManager Instance { get; private set; }

        [Header("引用（由 Setup 菜单生成并保存进场景）")]
        [SerializeField] private PathSystem path;

        /// <summary>IPathSet（F1.2，ADR-032）：敌线路路由入口（BattleModel.Paths）。
        /// lane 1 无水路（单线章）时由 PathSystem 回退 lane 0——水路敌误入单线章走主路不炸。</summary>
        public PathRoute Get(int lane) => path != null ? path.GetRoute(lane) : default;

        /// <summary>IPathSet（F1.3，ADR-040 同路双向）：口选端——port 0 下口（起点端半程）/ 1 上口（末端倒序半程）。
        /// 单向章两口同回全程（ch1/ch2 零变化铁律）。</summary>
        public PathRoute GetPort(int lane, int port) => path != null ? path.GetPort(lane, port) : default;

        /// <summary>IPathSet（F1.2，ch5 双面 / ADR-058）：按面取路由——虚面无虚路时由
        /// PathSystem 回退陆路（ch1-4 恒 face 0，新路径零进入）。</summary>
        public PathRoute GetFace(int face) => path != null ? path.GetFace(face) : default;
        [SerializeField] private Hud hud;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite whiteSprite;

        // 暂停（用户 2026-10-12 点题）：右上角钮/P 键切换；冻结战斗推进与视觉轮询，
        // 全屏遮罩点任意处继续。veil 根节点常驻建好即藏（显隐即暂停态）。
        private bool paused;
        private GameObject pauseVeilRoot;
        /// <summary>暂停态只读外露（F2.1：喷射 marker 冻结同闸家族——防「暂停了红闪还在闪」）。</summary>
        public bool Paused => paused;

        // 倍速（用户 2026-10-21 点题）：右上角「×1 ↔ ×2」钮，实现只切 Time.timeScale。
        // 战斗推进全吃 Time.deltaTime（Update 里 model.Tick / waveClock / 红印 DoT / 践踏 /
        // 喷排），所以一次赋值 = 整场同倍加速，不必在 Tick 里乘系数；演出协程走 WaitForSeconds
        // 也随之加速（= 整场快进，不是「跳过演出」）。暂停是独立 flag、不读写 timeScale，
        // 两者互不干扰（暂停遮罩 z 在本钮之前，暂停中点不到它）。
        // 跨关保持（会话内偏好，同大部分塔防；不写档——存档口径不变）。
        private const float GameSpeedNormal = 1f;
        private const float GameSpeedFast = 2f; // 口播就是 ×2；要 1.5 倍改这一行
        private float gameSpeed = GameSpeedNormal;
        private TextMeshPro speedLabel;

        // 换面（ch5 双面，docs/13 F2.5 / ADR-060）：右下「下一波」正上方钮 + Q 键。
        // CD 从按下起算（GameParams.faceSwitchCooldown 8s）；CD 内点 = 扣 5 墨立即换面并重置；
        // 0.2s 纸色闪（不暂停）。换关/R 回滚 → 面 = 实（BattleModel.Reset）。
        private GameObject faceBtnRoot;
        private TextMeshPro faceLabel;
        private TextMeshPro faceSubLabel;
        private SpriteRenderer faceFlash;
        private float faceCd;
        private float faceFlashRemaining;
        private float faceFlashTotal = 0.2f;
        private string faceUiSig;
        // —— F2.4 修订（用户 2026-10-22 点题）：虚面「整屏浅蓝 + 每 10s 红闪」——
        private SpriteRenderer faceTint;      // 浅蓝覆盖层（虚面常驻，实面淡出）
        private SpriteRenderer voidPulse;     // 红闪覆盖层（虚面每 10s 冲一次峰值后淡出）
        private float faceTintAlpha;          // 当前浅蓝强度（向目标渐变）
        private float voidPulseTimer;         // 距下次红闪（只在虚面递减）
        private float voidPulseLeft;          // 本次红闪剩余淡出时间（0 = 不在闪）
        private float voidPulseTotal = 0.5f;  // 本次红闪总时长（分母）

        [Header("W2 配置（Setup 菜单创建 SO 资产后注入）")]
        [SerializeField] private TowerPoolDef towerPool;     // 塔数值（v3.0：含 value 卡价值，铲为普通池卡）
        [SerializeField] private EnemyTableDef enemyTable;   // 刷怪数值
        [SerializeField] private GameParams gameParams;      // 全局参数
        [SerializeField] private LevelConfig[] levels;       // 关卡（波次/卡池/资源；布局在章图）
        [SerializeField] private ChapterConfig chapter;      // 章图（ADR-021：章内 5 关共用格子布局）
        private ChapterConfig activeChapter;                 // F4.3（ADR-031）：当前关实际章图（关卡引用优先，空走场景引用）
        [SerializeField] private UIPanelConfig panelConfig;  // 界面布局参数（ADR-024：Inspector 调面板/手牌条；空 = 内置缺省）
        [SerializeField] private SoundConfig soundConfig;    // 战斗音效槽位（ADR-028：用户自备音频；空 = 全静音）

        private static readonly Color BlueInk = HexUtil.ToColor("#2B4C7E");
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");
        private static readonly Color GrayInk = new Color(0.55f, 0.53f, 0.50f, 0.9f);
        private static readonly Color NuoGoldInk = HexUtil.ToColor("#C9A227"); // F5.1 傩面金脸（与优位格同色相）

        private BattleModel model;
        private ViewPool<EnemyView> enemyViews;
        private ViewPool<TowerView> towerViews;
        private ViewPool<FloatingText> floatViews;
        private ViewPool<ProjectileView> projectileViews;

        // F6（ch5 傩神 P2）：傩神本体 + 三面随行（纯表现：无血条/不可选中/不可击杀）
        private EnemyState shenBoss;
        private bool nuoShenArriving;
        private readonly List<TextMeshPro> shenMasks = new List<TextMeshPro>();

        private EnemyView enemyTemplate;
        private TowerView towerTemplate;
        private FloatingText floatTemplate;
        private ProjectileView projectileTemplate;
        private SfxPlayer sfx; // 战斗音效（ADR-028；cfg 空则全静音）
        private TutorialController tutorial; // L0 教学引导（ADR-030；仅教学关激活）

        private HandModel hand;
        private HandBar handBar;
        private int selectedCardIndex = -1;

        // —— F1（docs/14，ADR-062）手牌拖拽放塔（触屏单指 = 鼠标；桌面鼠标同管线）——
        private int pressedCardIndex = -1;   // 按下的手牌索引（-1 = 无；点选/拖拽共用起点）
        private Vector2 pressedScreenPos;    // 按下时的屏幕坐标（阈值判定）
        private bool handDragging;           // 已进入拖拽态
        private int draggingCardIndex = -1;  // 拖拽中的手牌索引
        private TowerView dragGhost;         // 拖拽塔虚影（跟手，上偏移）
        private float dragRange = 3f;        // 拖拽卡的射程（射程圈半径）
        private const float HandDragThresholdPx = 12f; // 起拖阈值（屏幕像素）
        private const float DragGhostLiftY = 0.25f;    // 虚影上偏移（格；用户 2026-10-25 从 0.5 收到 0.25）

        // —— F2（docs/14，ADR-063）拿起塔拖到卖出区 = 卖 ——
        private bool towerDragging;           // 拿起塔后进入拖拽（超阈值）
        private Vector2 towerPickupScreenPos; // 拿起瞬间屏幕坐标（阈值判定）
        private const float TowerDragThresholdPx = 12f;
        private const float SellDropRadius = 1.6f; // 卖出区命中半径（世界单位；按钮外围 1.5 倍放宽）

        // —— F4（docs/14，ADR-064）常驻回滚按钮 + 二次确认 ——
        private GameObject retryBtnRoot;   // 常驻回滚按钮（左下角）
        private GameObject confirmRoot;    // 回滚二次确认遮罩根

        // —— 家（用户 2026-10-23）：道路尽头可升级产墨（章内累积、章界清零）——
        private int homeLevel;             // 0 = 未升级（不产墨）；1-5 级
        private double homeInkTimer;       // 产墨计时
        private GameObject homeRoot;       // 家视图根
        private TextMeshPro homeLabel;     // 家文字（Lv + 产墨率）
        private Vector2 homePos;           // 家位置（实路终点）

        // —— 临时格（用户 2026-10-23）：左下空地格中的塔不攻击/不产墨（模型按坐标 gate）——

        private GridSystem grid;
        private LevelConfig level;
        private int levelIndex;

        // —— 章进度与存档（v3.0-D，ADR-023）——
        private BackpackSystem backpack;                  // 背包 + 构筑 5 卡
        private SaveData save;                            // 存档 v3（03 §6 schema）
        private bool hardMode;                            // 当前模式：false=简单 / true=困难（进度分开记）
        /// <summary>当前模式的章进度状态（困难/简单分开；save 为空时 null）。</summary>
        private ChapterStateData CurrentChapterState => save == null ? null : save.StateFor(hardMode);
        private LevelStartSnapshotData snapshot;          // 本关开始快照（回滚锚点）
        private LevelStartSnapshotData pendingField;      // 面板确认后要进场的场（推进=上关场末）
        private DeckPanel deckPanel;                      // 构筑面板（L3 起）
        private ClearPanel clearPanel;                    // P5 通关结算画面（有奖励时）
        private DiaryPanel diaryPanel;                    // P6 日记画面（章首关）

        // —— P5（用户 2026-10-26）：通关奖励改到「点下一关」后逐张展示 ——
        private List<string> pendingRewardCards;          // 胜利帧记下的奖励卡（首次通关才有）
        private int pendingRewardLevel = -1;              // 奖励所属关卡（文案来源）
        private readonly List<string> clearQueue = new List<string>();
        private int clearQueueIndex;
        private int clearQueueNext;
        private int clearQueueLevel = -1;
        private bool clearQueueTier6;
        private LevelStartSnapshotData clearQueueField;
        private readonly Dictionary<TowerState, TowerView> towerViewMap = new Dictionary<TowerState, TowerView>();

        // —— F1.1/F1.2 初始墨（ADR-053）：唯一来源 = 存档 playerMeta.inkCapSteps ——
        private int InkSteps => save != null && save.playerMeta != null ? save.playerMeta.inkCapSteps : 0;
        /// <summary>本关起手墨（60 + 10×步数，封顶 120）。headless 同源读 SaveSystem.InitialInk。</summary>
        private int CurrentInitialInk => SaveSystem.InitialInk(InkSteps);

        // —— F2 红铲（ADR-054）：局内道具·花墨在无格空位造格 ——
        private bool redShovelMode;                       // 放置模式（点按钮进入；成格/取消退出）
        private int redShovelPaidInk;                     // 本次已扣墨（取消 = 全额退；成格 = 转化为地皮）
        private int redShovelBought;                      // 本章已买次数（章内跨关累积、章界清零——价格计数）
        private readonly List<Vector2Int> extraCells = new List<Vector2Int>(); // 本章红铲造出的格（章内持久）
        private bool resumingSave;                        // 读档首进（Start）：造格取章内状态，不吃关开始快照
        private GameObject shovelCatcher;                 // 点选层（合法区矩形；含空位红框子物体）
        private int shovelMinX, shovelMaxX, shovelMinY, shovelMaxY; // 合法区（章图包围盒外扩 1 格 ∩ 可视区）

        // —— 拿起态（v2.3 移动 / 塔对塔合并）——
        private TowerState pickedTower;
        private GridCell pickedOrigin;
        private TowerView pickedView;
        private RangeIndicator rangeIndicator; // 拿起塔时显示射程圈（用户需求）
        private TrampleIndicator trampleIndicator; // F6.1：麟践踏光环提示圈（常显随行）
        private EnemyState trampleTrackedBoss; // F7.5③：蓄力里程跟踪对象（换 Boss/进关自动重挂）
        private double lastTrampleDist;        // F7.5③：上一帧弧长（正差累加走格数；回环重置负差不计）

        // —— 波次时间线（关卡波次表驱动，02 任务 6 最小实现）——
        private class WaveRuntime
        {
            public WaveLayout layout;
            public int pending;      // 未结算（死亡/漏过）敌数
            public int groupsLeft;   // 未出生完的组数
            public bool bonusGiven;
        }

        private double waveClock;
        private int nextWaveIndex;
        private double[] waveStart;
        private double timelineShift;     // 提前召唤：整体时间线前移量
        private readonly List<WaveRuntime> waveRuntimes = new List<WaveRuntime>();
        private readonly Dictionary<EnemyState, WaveRuntime> enemyWave = new Dictionary<EnemyState, WaveRuntime>();
        private readonly List<Coroutine> spawnRoutines = new List<Coroutine>();
        private bool won;

        // —— F2.1 喷射引擎（ch4 火山，docs/11 序5 / 06 §12.2 / ADR-047 喷射幅）——
        // 每波开始 +delay 秒 → 种子随机 N 格红闪 flash 秒 → 落地（本单元纯视觉红印；三类
        // 落点结算 F2.3、红印 DoT F2.4）。章开关×关数据双门（volcanoEnabled && eruptionMax>0）
        // ——ch1-3 两层全关铁律零进入。序列 = 种子分片纯函数（流号×波号），R 回滚/同关重进
        // 重放同序列（防回滚刷好运）；换关重生成（种子入 chapterState 随档持久）。
        private class EruptionBatch
        {
            public int waveIndex;                              // 序列种子成分（重放键）
            public List<Vector2Int> targets = new List<Vector2Int>();
            public List<EruptionMarker> markers = new List<EruptionMarker>();
            public double warnClock, landClock;                // 波钟刻度（暂停/胜负与波次钟同冻结）
            public bool warned;
        }
        private bool volcanoActive;                            // 双门结果（DoEnterLevel 定格）
        private int eruptionSeed;                              // 当前关序列种子（0 = 未生成）
        private List<Vector2Int> eruptionPool;                 // AB 收窄池：塔防/垦格 ∪ 路径 ±1 带宽
        private readonly List<EruptionBatch> eruptionBatches = new List<EruptionBatch>();
        // —— F2.3 三类落点结算（06 §12.2 落点三分表）——
        private HashSet<Vector2Int> eruptionRoadCells;         // 路面格集（无格 + 在路 = 路径格判定）
        private readonly Dictionary<Vector2Int, double> lavaMarks = new Dictionary<Vector2Int, double>(); // 红印 DoT 区（路径格入册，到期秒数波钟；tick 消费归 F2.4）
        private float lavaDotClock;                           // F2.4：红印整秒跳累积钟（满 1s 结算一轮 dps——节拍与塔攻速同频）
        // 免疫名单 = 火/炮/烬（06 §12.2「火·炮·烬幸存」——F4.2/F4.4/F4.5 落卡时转 TowerPoolDef
        // 标记位，字面名单先钉：卡未实装空跑零命中=前瞻）。与 IsAshTower（灰烬格可放名单，含盆）分立：
        // 盆可放灰但**不免疫**——被岩浆再毁 = 盆与其上之塔同毁（06 字面），两集刻意不同。
        private static readonly HashSet<string> LavaImmuneTowers = new HashSet<string> { "火", "炮", "烬" };

        // —— Phase 0.2：通关/失败大按钮（真机前必需——手机无键盘；键盘 N/R 保留为桌面调试通道）——
        // 结构三层（宽按钮文字等比父坑，HandBar 下一波钮同款）：等比 holder（挂字/挪位）→
        // 非等比 body（纸底+判定）→ Label（等比，不被横向拉宽）
        private GameObject resultButtonsRoot;
        private Transform nextHolder;
        private Transform retryHolder;
        private WorldButton btnNext;   // N：下一关 / 章末「回第一章」
        private WorldButton btnRetry;  // R：回滚本关
        private int resultButtonMode = -1; // -1 隐藏；0 过关双钮；1 章末单钮；2 失败单钮（幂等判据）

        private float hudTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// 倍速是**全局静态状态**（Time.timeScale）：本对象销毁（退出 Play / 换场景）必须复位，
        /// 否则下一次进来会以 2 倍起步、而按钮文字还显示 ×1（状态错位）。
        /// </summary>
        private void OnDestroy()
        {
            // 只由真身复位（Awake 里被销毁的重复体也会走本方法，不该动全局速度）
            if (Instance == this) Time.timeScale = 1f;
        }

        private void Start()
        {
            if (path == null)
            {
                Debug.LogError("[GameManager] 场景里没有 PathSystem。请先运行菜单 Word Wars/Setup 生成场景。");
                enabled = false;
                return;
            }
            if (towerPool == null || enemyTable == null || gameParams == null)
            {
                Debug.LogError("[GameManager] 缺少配置（塔字池/敌人表/游戏参数）。请重跑菜单 Word Wars/Setup/自愈场景（保留手动调整）。");
                enabled = false;
                return;
            }
            if (levels == null || levels.Length == 0)
            {
                Debug.LogError("[GameManager] 缺少关卡配置（LevelConfig[]）。请重跑菜单 Word Wars/Setup/自愈场景（保留手动调整）。");
                enabled = false;
                return;
            }
            if (chapter == null)
            {
                Debug.LogError("[GameManager] 缺少章图配置（ChapterConfig）。请重跑菜单 Word Wars/Setup/自愈场景（保留手动调整）。");
                enabled = false;
                return;
            }

            model = new BattleModel
            {
                Paths = this, // F1.2 双线（ADR-032）：lane 0 陆路 / 1 水路；敌人生成时缓存各自路由
                EnemyStatsLookup = enemyTable.Find, // 分裂/召唤产物数值来源（ADR-022）
                MaxTowerLevel = gameParams.maxTowerLevel,
                Tier6MaxLevel = gameParams.tier6MaxLevel,       // P2：6 阶上限（仅已解锁塔可达）
                Tier6UnlockedLookup = c => save != null && save.IsTier6Unlocked(c), // P2：6 阶解锁查询（困难模式解锁）
                DamageGrowth = gameParams.damageGrowth,
                InkGrowth = gameParams.inkGrowth,
                InkPulseSeconds = gameParams.inkPulseSeconds,
                SubmergeSeconds = gameParams.submergeSeconds, // F2.2：淹没沉默倒计时（黄底 20）
                SoulsPerPayout = gameParams.soulsPerPayout,   // F3.4（ch5 幡）：几魂结一次墨（黄底 10）
                LightVulnerableMult = gameParams.lightVulnerableMult, // F2.2 光-敌交互：蛾光脆 ×1.5
                LightHinderedMult = gameParams.lightHinderedMult,     // 蝠光滞 ×0.7
                NocturnalFogMult = gameParams.nocturnalFogMult,       // 狼夜行雾中 ×1.3
                MageStunChance = gameParams.mageStunChance,           // F3.3 法塔光内眩晕概率 20%
                PerchSecondsPerLevel = gameParams.perchSecondsPerLevel, // F5.1 枭栖灯倒计时 5×灯阶
                BurnVulnerableMult = gameParams.burnVulnerableMult,    // F5.2 燃烧易伤倍率（鳄「水怪怕火」×2——施加侧定格）
                FaceInactiveSpeedMult = gameParams.faceInactiveSpeedMult, // F2.2（ch5）：非激活面敌 ×0.5 只压移动
                NuoFaceCycleSeconds = gameParams.nuoFaceCycleSeconds,      // F5.1（ch5 傩面）换脸周期 18s
                NuoFaceWarnSeconds = gameParams.nuoFaceWarnSeconds,        //   预告 3s（面具翻转）
                NuoGraceSeconds = gameParams.nuoGraceSeconds,              //   换脸后 1.5s 可打不还手
                NuoGreenSpeedMult = gameParams.nuoGreenSpeedMult,          //   青脸速 ×1.25
                NuoGreenDamageTakenMult = gameParams.nuoGreenDamageTakenMult, // 青脸受伤 ×1.5（输出窗）
                NuoRedSmashInterval = gameParams.nuoRedSmashInterval,      // 赤脸劈塔间隔 6s
                NuoGoldWarnSeconds = gameParams.nuoGoldWarnSeconds,        // 金脸起手 2s（可被打断）
                NuoGoldSealSeconds = gameParams.nuoGoldSealSeconds,        //   封印 3s
                NuoGoldCdSeconds = gameParams.nuoGoldCdSeconds,            //   打断/封印后 6s
                ShenSmashInterval = gameParams.shenSmashInterval,          // F6（ch5 傩神）赤劈 7s
                ShenSealInterval = gameParams.shenSealInterval,            //   金封 7s
                ShenDanceInterval = gameParams.shenDanceInterval,          //   傩舞 10s 一次
                ShenDanceSeconds = gameParams.shenDanceSeconds,            //   停 2.5s
                ShenDanceDamageMult = gameParams.shenDanceDamageMult,      //   舞中受伤 ×1.5
            };
            // F4.1/F7.4/F4.5 灰烬增益统一律（06 §12.2「死灰复燃」）：火/炮/烬站灰烬格 → **全部伤害** ×ashDamageMult
            //（直接攻击 + 燃烧跳伤；用户 2026-10-18 变更——原只加成燃烧，3/s vs 直接 27/s 体感≈零）。
            // 注入式（grid = 表现层资产，模型不持格引用——EnemyStatsLookup 同模式）；直接伤害取命中时刻、
            // 燃烧取施加时刻（已挂的不降温）。tower.position 是格中心坐标（PlaceTower/MoveTowerToCell 定格）
            // ——四舍五入即归属格；null-grid/非灰格 = ×1（ch1-3 无灰烬零路径铁律）。
            model.AshBuffLookup = t =>
            {
                if (t == null || grid == null) return 1.0;
                var c = grid.Get(Mathf.RoundToInt(t.position.x), Mathf.RoundToInt(t.position.y));
                return c != null && c.TileState == TileState.Ash ? gameParams.ashDamageMult : 1.0;
            };
            model.EnemySpawned += OnEnemySpawned;
            model.EnemyDied += OnEnemyDied;
            model.EnemyRevived += OnEnemyRevived; // F4.3（ch5 傀）：换影复活 → 飘字（「换影」）
            model.EnemyDamaged += OnEnemyDamaged; // 0.4 修正：逐敌受击（AoE 溅射可视化）
            model.LightsChanged += OnLightsChanged; // F1.2：光集变化（进关/灯增删/升阶/移动）→ 迷雾重挖
            model.FaceChanged += OnFaceChanged; // F2.1（ch5 双面）：换面完成 → 重绑格-塔/视图显隐/敌 tint
            model.InkPulsed += OnInkPulsed; // 0.3 Juice：产墨跳档飘字（ADR-027）
            model.WaterLevelChanged += OnWaterLevelChanged; // F2.1：水线切换 → 格子淹没视觉（ADR-032）
            model.TowerDrowned += OnTowerDrowned; // F2.2：淹没倒计时归零 → 清格/释放视图（ADR-032）
            model.LampDevoured += OnLampDevoured; // F5.1：枭吞噬灯塔 → 清格/释放视图/播报
            model.EnemyReached += OnEnemyReached;
            model.EnemyEnraged += OnEnemyEnraged;
            model.BossPhase2 += OnBossPhase2; // F5.2：蛟 1/3 血转阶段 → 两幕剧编排（蓄水→洪水→潮落）
            model.BossLooped += OnBossLooped; // F7.5②：二圈顺延（到终点不判败——安全网路径）
            model.NuoMaskWarn += OnNuoMaskWarn;     // F5.1（ch5 傩面）：换脸预告（面具翻转+起势）
            model.NuoMaskChanged += OnNuoMaskChanged; // F5.1：换脸完成（字色随脸 + 脸名飘字）
            model.NuoSmashTower += OnNuoSmashTower;       // F5.2 赤脸：毁塔不毁格
            model.NuoGoldCast += OnNuoGoldCast;           // F5.2 金脸：起手锁定
            model.NuoGoldInterrupted += OnNuoGoldInterrupted; // F5.2 金脸：换面打断
            model.NuoSealApplied += OnNuoSealApplied;     // F5.2 金脸：封印落地
            model.NuoFaceFollowed += OnNuoFaceFollowed;   // F5.2 金脸：跟面 → 同帧改明暗
            model.ShenDanceStarted += OnShenDanceStarted; // F6.2（ch5 傩神）：傩舞起（三面同时施法）
            model.ShenDanceEnded += OnShenDanceEnded;     // F6.2 傩舞止            model.TowerFired += OnTowerFired;
            model.ProjectileSpawned += OnProjectileSpawned;
            model.ProjectileHit += OnProjectileHit;
            model.ProjectileRemoved += OnProjectileRemoved;
            model.ChainLinked += OnChainLinked; // F3.2：雷链电电弧（from → to）
            model.SoulsPulsed += OnSoulsPulsed; // F3.4：幡每 10 魂结墨 → 幡上飘 +N

            CreateTemplatesAndPools();
            CreateGridSystem();
            CreateRangeIndicator();
            CreateTrampleIndicator(); // F6.1：麟践踏提示圈（常隐，有光环敌才 Show）
            CreateHandBar();
            CreateDeckPanel();
            CreateStoryPanels();
            CreateTutorialController();
            CreateBlackVeil(); // F5.2 枭黑幕（全场黑转场演出件；常隐 alpha 0）
            CreateRedVeil();   // F6.2 麟共振全场红闪（演出件；常隐 alpha 0）

            // 操作提示为规则附属文案（v3.0-D：R=回滚、N=带塔推进/切关）——运行时直写，场景旧值不参与
            if (hud != null && hud.hintText != null)
                hud.hintText.text =
                    "点手牌→点空地放塔 · 点塔拿起→点目标(移动/合并) · 抽=一口价补满 · 铲点荒地开垦 · 盆点灰格除灰(塔防格复绿) · 空格下一波 D抽 R重玩 N推进";

            // docs/16 F3.2（安全区）：运行时整体重挂 Canvas 内容会打乱 HUD（实测墨 UI 跑位），
            // 暂时**不自动挂载**，等做场景级安全区容器再启用。类保留（SafeAreaFitter.cs）。
            // 存档 v3：有档续玩（背包/构筑/快照回场），无档新开
            backpack = new BackpackSystem();
            save = SaveSystem.Load();
            // 用户 2026-10-26：音效开关消费点（settings.sfxOn；BGM 由 bgmOn 独立管）
            sfx?.SetSfxEnabled(save == null || save.settings == null || save.settings.sfxOn);
            if (save != null) backpack.LoadFrom(save);

            // P3.3（用户 2026-10-24）：主界面/选关写入的会话参数优先；直接启动 Game 场景则走存档
            bool session = GameSession.Active;
            hardMode = session ? GameSession.HardMode : (save != null && save.lastMode == 1);
            if (save != null && session) save.lastMode = hardMode ? 1 : 0; // 记住本次模式

            // F1.1/F2.4 旧档/手改档兜底：新字段缺对象 = 补一个（缺省 0 / 空表 → 旧行为）
            if (save != null && save.playerMeta == null) save.playerMeta = new PlayerMetaData();
            if (CurrentChapterState != null && CurrentChapterState.extraCells == null)
                CurrentChapterState.extraCells = new List<SnapCellData>();
            SaveSystem.LogPath(); // 测试删档：Console 里找路径

            int startIdx;
            LevelStartSnapshotData startField;
            if (session && GameSession.StartLevelIndex >= 0)
            {
                startIdx = Mathf.Clamp(GameSession.StartLevelIndex, 0, levels.Length - 1);
                if (GameSession.FromChapterFirst && startIdx > 0)
                    startIdx = ((startIdx - 1) / 5) * 5 + 1; // 本章第一关
                startField = (GameSession.ResumeSnapshot && save != null) ? CurrentChapterState.levelStartSnapshot : null;
            }
            else
            {
                startIdx = save != null ? Mathf.Clamp(CurrentChapterState.levelIndex, 0, levels.Length - 1) : 0;
                startField = save != null ? CurrentChapterState.levelStartSnapshot : null;
            }
            GameSession.Reset(); // 消费会话参数
            resumingSave = save != null && !session; // F2.4：直接启动/读档续玩 = 地皮按章内状态
            RequestEnterLevel(startIdx, startField);
        }

        /// <summary>构筑面板（运行时自建，世界坐标；镜像 HandBar 模式；布局走 UIPanelConfig）。</summary>
        private void CreateDeckPanel()
        {
            var go = new GameObject("DeckPanel", typeof(DeckPanel));
            deckPanel = go.GetComponent<DeckPanel>();
            deckPanel.Init(font, whiteSprite, panelConfig);
            // F1.3（ADR-053）：选卡界面左上角「初始墨 N +」——读存档、点击即 +1 步并立即写档。
            // 正式版把 UpgradeInitialInk 换成激励视频即可（UI / 存档结构不动）。
            deckPanel.BindInk(() => CurrentInitialInk, () => InkSteps, UpgradeInitialInk);
        }

        /// <summary>
        /// P5/P6（用户 2026-10-25）：通关结算 + 日记画面（运行时自建，世界坐标，同 DeckPanel 模式）。
        /// 文案全部来自 LevelConfig（Inspector 编辑）；空数据 = 不出现。
        /// </summary>
        private void CreateStoryPanels()
        {
            var clearGo = new GameObject("ClearPanel", typeof(ClearPanel));
            clearPanel = clearGo.GetComponent<ClearPanel>();
            clearPanel.Init(font, whiteSprite);

            var diaryGo = new GameObject("DiaryPanel", typeof(DiaryPanel));
            diaryPanel = diaryGo.GetComponent<DiaryPanel>();
            diaryPanel.Init(font, whiteSprite);
        }

        /// <summary>
        /// F1.3 初始墨升级（「+」按钮 / 正式版广告位）：步数 +1（0-6 封顶）→ 立即写档。
        /// 面板打开期间 GameManager 暂停（Tick 不跑）——本方法自成闭环，不依赖时间推进。
        /// 不扣任何资源、不做门槛判断、不接 SDK（原型期零 SDK 口径）。
        /// </summary>
        private bool UpgradeInitialInk()
        {
            if (save == null) save = new SaveData();
            if (save.playerMeta == null) save.playerMeta = new PlayerMetaData();
            if (save.playerMeta.inkCapSteps >= SaveSystem.InkCapMaxSteps) return false;
            save.playerMeta.inkCapSteps++;
            WriteSave();
            Debug.Log("[F1.3] 初始墨升级：步数 " + save.playerMeta.inkCapSteps + "/" +
                SaveSystem.InkCapMaxSteps + " → 下关起手 " + CurrentInitialInk + " 墨");
            return true;
        }

        /// <summary>拿起塔时的攻击范围指示器（运行时自建，场景零引用）。</summary>
        private void CreateRangeIndicator()
        {
            var go = new GameObject("RangeIndicator", typeof(RangeIndicator));
            go.transform.SetParent(transform, false);
            rangeIndicator = go.GetComponent<RangeIndicator>();
            rangeIndicator.Init(whiteSprite);
        }

        /// <summary>F6.1 麟践踏光环提示圈（运行时自建，场景零引用——同射程圈家族）。
        /// 样式初值取 GameParams（缺省色兜底），之后每帧由 UpdateTrample 刷新（Play 调参即时可见）。</summary>
        private void CreateTrampleIndicator()
        {
            var go = new GameObject("TrampleIndicator", typeof(TrampleIndicator));
            go.transform.SetParent(transform, false);
            trampleIndicator = go.GetComponent<TrampleIndicator>();
            trampleIndicator.Init(whiteSprite);
            if (gameParams != null)
                trampleIndicator.SetStyle(gameParams.trampleCircleColor,
                    gameParams.trampleDiscAlpha, gameParams.trampleRimAlpha,
                    gameParams.trampleChargeColor);
        }

        /// <summary>L0 教学引导（ADR-030）：运行时自建气泡控制器（纯渲染件，无碰撞不挡点击）。</summary>
        private void CreateTutorialController()
        {
            var go = new GameObject("TutorialController", typeof(TutorialController));
            go.transform.SetParent(transform, false);
            tutorial = go.GetComponent<TutorialController>();
            tutorial.Init(font, whiteSprite);
        }

        /// <summary>
        /// F5.2 枭转阶段黑幕（06 §11.4）：全场黑转场演出件——世界最前 z=-2.5（盖手牌/构筑/
        /// 雾/敌塔全链；Screen Space Hud 顶栏天然盖不住——Canvas 层级在 World 前，原型备案）。
        /// 常隐 alpha 0；OwlNightfallRoutine 协程驱动淡入/淡出；DoEnterLevel 随场重置（防半黑残留）。
        /// </summary>
        private void CreateBlackVeil()
        {
            var go = new GameObject("BlackVeil", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -2.5f); // 全场最前
            go.transform.localScale = new Vector3(24f, 15f, 1f);    // 相机视野 ±11×±7 外扩（雾砖同口径）
            blackVeil = go.GetComponent<SpriteRenderer>();
            blackVeil.sprite = whiteSprite;
            blackVeil.color = new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>
        /// F6.2 麟共振全场红闪（06 §12.5）：满屏红幕演出件——z=-2.6 全场最前（比枭黑幕 -2.5
        /// 更靠相机；ch4 无枭 → 两幕永不同席），盖手牌/构筑/雾/敌塔全链。常隐 alpha 0；
        /// LeiResonanceRoutine 协程驱动淡入/淡出；颜色/透明度 GameParams 外露（Play 可调）；
        /// DoEnterLevel 随场重置（防半红残留）。
        /// 「全场」= 独立满屏幕，不走格级红闪（EruptionMarker 管喷发落点，雨另路不掺）。
        /// </summary>
        private void CreateRedVeil()
        {
            var go = new GameObject("RedVeil", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -2.6f); // 全场最前
            go.transform.localScale = new Vector3(24f, 15f, 1f);     // 相机视野 ±11×±7 外扩（黑幕同口径）
            redVeil = go.GetComponent<SpriteRenderer>();
            redVeil.sprite = whiteSprite;
            redVeil.color = new Color(1f, 0.4f, 0.2f, 0f);
        }

        /// <summary>红幕透明度写（颜色每次取 GameParams——Play 中调色/调浓即时可见）。</summary>
        private void SetRedVeilAlpha(float a)
        {
            if (redVeil == null) return;
            var c = gameParams != null ? gameParams.leiResonanceColor : new Color(0.88f, 0.32f, 0.12f);
            c.a = a;
            redVeil.color = c;
        }

        private void CreateTemplatesAndPools()
        {
            Transform root = transform;

            // 模板必须「激活状态下 AddComponent + Init」最后再隐藏：
            // TextMeshPro 的 Awake 只在物体激活时执行（缓存 MeshRenderer 等内部引用），
            // 未激活就设置 font 会在 TMP 内部空引用（W1 踩坑记录）。
            enemyTemplate = InitTemplate<EnemyView>("EnemyTemplate", root, v => v.Init(font, whiteSprite)); // F1.2：视图不再持路径，位置走 State.route
            towerTemplate = InitTemplate<TowerView>("TowerTemplate", root, v => v.Init(font, whiteSprite));
            floatTemplate = InitTemplate<FloatingText>("FloatTemplate", root, v => v.Init(font));

            // 弹道模板：SpriteRenderer 视图，不需要 TMP 三件套（同「先激活再隐藏」纪律）
            var projGo = new GameObject("ProjectileTemplate", typeof(SpriteRenderer));
            projGo.transform.SetParent(root, false);
            projectileTemplate = projGo.AddComponent<ProjectileView>();
            projectileTemplate.Init(whiteSprite, gameParams);
            projGo.SetActive(false);

            // 战斗音效播放器（ADR-028）：AudioSource 在 Init 自建，声源常驻（不随池化熄灭）
            var sfxGo = new GameObject("SfxPlayer", typeof(SfxPlayer));
            sfxGo.transform.SetParent(root, false);
            sfx = sfxGo.GetComponent<SfxPlayer>();
            sfx.Init(soundConfig);

            enemyViews = new ViewPool<EnemyView>(root);
            towerViews = new ViewPool<TowerView>(root);
            floatViews = new ViewPool<FloatingText>(root);
            projectileViews = new ViewPool<ProjectileView>(root);
        }

        private static T InitTemplate<T>(string name, Transform parent, System.Action<T> init) where T : Component
        {
            // 3D TMP 需要 RectTransform + MeshRenderer/MeshFilter
            var go = new GameObject(name, typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            go.transform.SetParent(parent, false);
            var comp = go.AddComponent<T>(); // 此时物体激活：Awake 立即执行，内部引用就绪
            init(comp);                       // 设置 font / 搭血条等初始化
            go.SetActive(false);              // 最后隐藏，作为克隆母本
            return comp;
        }

        private void CreateGridSystem()
        {
            var go = new GameObject("Grid", typeof(GridSystem));
            go.transform.SetParent(transform, false);
            grid = go.GetComponent<GridSystem>();
            grid.Init(whiteSprite, font);
        }

        private void CreateHandBar()
        {
            var go = new GameObject("HandBar", typeof(HandBar));
            handBar = go.GetComponent<HandBar>();
            // 手牌条高度走 UIPanelConfig（ADR-024；空配置回落内置缺省 -4.55）。
            // z=-0.5（F1.1 v2，用户验收「手牌/抽/卖/下一波不被雾遮」）：浮于雾层（-0.2）与敌（-0.4）
            // 之前——幽夜章常驻操作面（卡/按钮）永不被夜色压暗；ch1/ch2 无雾层同 z 无遮挡对象零观感差。
            float barY = panelConfig != null ? panelConfig.handBarY : -4.55f;
            handBar.Init(font, whiteSprite, new Vector3(0f, barY, -0.5f), gameParams.handSize, panelConfig);

            BuildResultButtons(); // 0.2：跟手牌条同一初始化窗口（font/白图已就绪）
            BuildPauseUi();       // 暂停钮+全屏遮罩（用户 2026-10-12；常驻跨关，同窗口建）
            BuildSpeedUi();       // 倍速钮 ×1/×2（用户 2026-10-21：暂停钮左边，同层常驻）
            BuildFaceUi();        // F2.5（ch5）：换面钮（章/关双门在 DoEnterLevel 逐关开）
            BuildRetryButton();   // F4.1（docs/14）：左下角常驻回滚钮 + 二次确认
            BuildHomeUi();        // 家（用户 2026-10-23）：道路尽头可升级产墨
        }

        // ============ 通关/失败大按钮（Phase 0.2） ============

        /// <summary>
        /// 暂停 UI（用户 2026-10-12 点题）：右上角暂停钮（WorldButton 世界空间，触屏单指同
        /// 管线——移动端天然可用）+ 全屏半透明遮罩。z 序：遮罩 -2.8 = 全景最前（盖 BlackVeil
        /// -2.5 与一切——暂停压所有演出；文字 -2.9 浮于遮罩前）。点遮罩任意处继续 = collider
        /// 射线最前收 OnMouseDown，其余点击（格/手牌/按钮）全被挡。右上波次栏让位 y-50→-120
        ///（运行时改，场景值与用户手调锚均不动）。已知降级备案：boss 演出协程走墙钟
        /// WaitForSeconds，暂停中会继续走完（Tick 冻结但演出层不冻结——Boss 转阶段 5s 窗口
        /// 内暂停属罕见场景，原型级可接受）。
        /// </summary>
        private void BuildPauseUi()
        {
            // —— 暂停按钮（世界空间右上，MakeResultButton 三层同款：holder 等比 → body 判定 → label 挂 holder）——
            var btnHolder = new GameObject("PauseBtnHolder");
            btnHolder.transform.SetParent(transform, false);
            btnHolder.transform.position = new Vector3(8.55f, 4.95f, -1.7f); // 右上角（z 同结果按钮层）
            var btnGo = new GameObject("PauseBtn", typeof(SpriteRenderer), typeof(BoxCollider2D));
            btnGo.transform.SetParent(btnHolder.transform, false);
            btnGo.transform.localPosition = Vector3.zero;
            btnGo.transform.localScale = new Vector3(1.7f, 0.7f, 1f);
            var bg = btnGo.GetComponent<SpriteRenderer>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.93f, 0.89f, 0.80f, 0.95f); // 卡纸（结果按钮同款）
            var col = btnGo.GetComponent<BoxCollider2D>();
            col.size = Vector2.one * 1.02f; // 本地 × scale ≈ 1.73×0.71，指尖友好
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            labelGo.transform.SetParent(btnHolder.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.02f, -0.15f); // 浮于纸底前（z 越小越靠相机）
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 8f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = HexUtil.ToColor("#26231F"); // 浓墨
            label.text = "菜单"; // F4.2（docs/14）：暂停改菜单（仍暂停 + 中间菜单面板）
            labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 1.5f);
            // WorldButton 必须与 collider 同物体（OnMouseDown 不沿父子链传播——首轮无法点击
            // 的根因；MakeResultButton 474 行同款惯例：挂 body 非 holder）
            btnGo.AddComponent<WorldButton>().Init(TogglePause, label);

            // —— 全屏菜单遮罩（z=-2.8 全景最前；veil 本体 = WorldButton：点面板外 = 关闭并继续）——
            var veilGo = new GameObject("PauseVeilHolder");
            veilGo.transform.SetParent(transform, false);
            veilGo.transform.position = new Vector3(0f, 0f, -2.8f);
            var veilBody = new GameObject("PauseVeil", typeof(SpriteRenderer), typeof(BoxCollider2D));
            veilBody.transform.SetParent(veilGo.transform, false);
            veilBody.transform.localPosition = Vector3.zero;
            veilBody.transform.localScale = new Vector3(22f, 12f, 1f); // 全屏覆盖（格区 ±10×±5.5 外余量）
            var vbg = veilBody.GetComponent<SpriteRenderer>();
            vbg.sprite = whiteSprite;
            vbg.color = new Color(0.02f, 0.02f, 0.02f, 0.6f); // 深墨半透明
            var vcol = veilBody.GetComponent<BoxCollider2D>();
            vcol.size = Vector2.one * 1.02f; // × scale 全屏判定
            veilBody.AddComponent<WorldButton>().Init(ResumeGame, null); // F4.2：点面板外 = 关闭 + 继续

            // —— 居中菜单面板：纸底 + collider 挡住遮罩（点面板空白不触发关闭）——
            var panel = new GameObject("MenuPanel", typeof(SpriteRenderer), typeof(BoxCollider2D));
            panel.transform.SetParent(veilGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            panel.transform.localScale = new Vector3(6.2f, 7.6f, 1f);
            var pbg = panel.GetComponent<SpriteRenderer>();
            pbg.sprite = whiteSprite;
            pbg.color = new Color(0.93f, 0.89f, 0.80f, 0.98f);
            panel.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            // 标题「菜单」
            var titleGo = new GameObject("MenuTitle", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            titleGo.transform.SetParent(veilGo.transform, false);
            titleGo.transform.localPosition = new Vector3(0f, 3.0f, -0.2f);
            var title = titleGo.AddComponent<TextMeshPro>();
            title.font = font;
            title.fontSize = 12f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = HexUtil.ToColor("#26231F");
            title.text = "菜单";
            titleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(5f, 1.4f);

            // 继续 / 回滚本关 / 退出到主界面（占位灰态）——按钮加宽到 4.2（4-5 字不换行）
            MakePanelButton(veilGo.transform, "MenuResume", new Vector3(0f, 2.0f, -0.2f), "继续", HexUtil.ToColor("#26231F"), false, ResumeGame, 4.2f, 8f);
            MakePanelButton(veilGo.transform, "MenuRetry", new Vector3(0f, 1.1f, -0.2f), "重玩本关", HexUtil.ToColor("#26231F"), false, ConfirmRetry, 4.2f, 8f);
            MakePanelButton(veilGo.transform, "MenuExit", new Vector3(0f, 0.2f, -0.2f), "退出到主界面", HexUtil.ToColor("#26231F"), false, ExitToMainMenu, 4.2f, 8f);

            // —— 测试区（仅测试用：随时可跳关/删档）——
            var testGo = new GameObject("MenuTestLabel", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            testGo.transform.SetParent(veilGo.transform, false);
            testGo.transform.localPosition = new Vector3(0f, -0.55f, -0.2f);
            var testLabel = testGo.AddComponent<TextMeshPro>();
            testLabel.font = font;
            testLabel.fontSize = 6f;
            testLabel.alignment = TextAlignmentOptions.Center;
            testLabel.color = new Color(0.45f, 0.43f, 0.40f, 0.9f);
            testLabel.text = "—— 测试 ——";
            testGo.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 0.8f);

            MakePanelButton(veilGo.transform, "MenuPrev", new Vector3(-1.25f, -1.4f, -0.2f), "上一关", HexUtil.ToColor("#26231F"), false, PrevLevel, 2.4f, 7.5f);
            MakePanelButton(veilGo.transform, "MenuNext", new Vector3(1.25f, -1.4f, -0.2f), "下一关", HexUtil.ToColor("#26231F"), false, AdvanceLevel, 2.4f, 7.5f);
            MakePanelButton(veilGo.transform, "MenuWipe", new Vector3(0f, -2.4f, -0.2f), "删档重开", Vermilion, false, DeleteSaveAndRestart, 4.2f, 8f);

            pauseVeilRoot = veilGo;
            veilGo.SetActive(false); // 建好即藏（显隐 = 暂停态）

            // —— 右上角波次栏让位（用户 2026-10-12）：y -50 → -120，顶栏给暂停钮留位 ——

            if (hud != null && hud.waveText != null)
                hud.waveText.rectTransform.anchoredPosition = new Vector2(-349f, -120f);
        }

        /// <summary>
        /// 倍速钮（用户 2026-10-21 点题）：暂停钮左边，显示 ×1；点一下 ×2、再点回 ×1。
        /// 契约：**只改 Time.timeScale**（暂停不读 timeScale，所以不需要「暂停时跳过」这类特判）；
        /// 文字即状态（×1/×2 与实际倍速永远同源 SetGameSpeed）。几何与暂停钮同款三层结构
        /// （holder 等比 → body 判定+纸底 → Label 挂 holder，否则非等比缩放会把字拉宽）。
        /// 位置算法：暂停钮 8.55 ± 1.7/2 → 左缘 7.70；本钮宽 1.2、留 0.10 缝 → 右缘 7.60、中心 7.00。
        /// </summary>
        private void BuildSpeedUi()
        {
            var holder = new GameObject("SpeedBtnHolder");
            holder.transform.SetParent(transform, false);
            holder.transform.position = new Vector3(7.00f, 4.95f, -1.7f); // 与暂停钮同高、同 z（结果按钮层）
            var go = new GameObject("SpeedBtn", typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.2f, 0.7f, 1f);
            var bg = go.GetComponent<SpriteRenderer>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.93f, 0.89f, 0.80f, 0.95f); // 卡纸（暂停钮同款）
            var col = go.GetComponent<BoxCollider2D>();
            col.size = Vector2.one * 1.04f; // 本地 × scale ≈ 1.25×0.73（略大于纸底，指尖够点）

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            labelGo.transform.SetParent(holder.transform, false); // 挂 holder（等比）：body 非等比会把字拉宽
            labelGo.transform.localPosition = new Vector3(0f, 0.02f, -0.15f); // 浮于纸底前（z 越小越靠相机）
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 8f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = HexUtil.ToColor("#26231F"); // 浓墨（暂停钮同款）
            label.enableWordWrapping = false;         // "×1" 两字不许换行
            label.text = SpeedText(gameSpeed);
            labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(2.4f, 1.5f);

            // WorldButton 必须与 collider 同物体（OnMouseDown 不沿父子链传播——暂停钮同坑）
            go.AddComponent<WorldButton>().Init(ToggleSpeed, label);
            speedLabel = label;
        }

        private static string SpeedText(float speed) => "×" + speed.ToString("0.#");

        /// <summary>
        /// 倍速切换（右上角钮）：×1 ↔ ×2。无暂停/结算特判——暂停时遮罩（z=-2.8）盖在本钮
        /// （z=-1.7）之前收不到点击；结算时切速度无害（Tick 已停，且下一关沿用偏好）。
        /// </summary>
        private void ToggleSpeed()
        {
            SetGameSpeed(gameSpeed >= GameSpeedFast ? GameSpeedNormal : GameSpeedFast);
        }

        /// <summary>倍速唯一写入口（钮/将来键盘调试通道共用）：写 timeScale + 刷文字，两处永不错位。</summary>
        private void SetGameSpeed(float speed)
        {
            gameSpeed = speed;
            Time.timeScale = speed;
            if (speedLabel != null) speedLabel.text = SpeedText(speed);
        }

        // ============ 换面（ch5 双面，docs/13 F2.5 / ADR-060） ============

        /// <summary>
        /// 换面钮：右下「下一波」正上方（Q1 定稿 B 案）。几何——手牌条顶边 -3.85（下一波 1.4 高、
        /// barY -4.55），章图底行 y=-3 的敌字中心 -3，故按钮只占中间空带 -3.83..-3.17（高 0.66）：
        /// **不压手牌条、不压敌字**（2.2×1.0 会盖住底行路面与收尾敌字，按实机几何收窄高度）。
        /// 章开关 × 关开关双门（ch1-4 / 五L1 = 不出现）。
        /// </summary>
        private void BuildFaceUi()
        {
            var holder = new GameObject("FaceBtnHolder");
            holder.transform.SetParent(transform, false);
            holder.transform.position = new Vector3(8.1f, -3.5f, -1.4f); // 与结果/暂停按钮同层附近；用户 2026-10-24：压到构筑面板（z=-1.55 遮罩）之后，避免结束后盖在背包界面上
            faceBtnRoot = holder;

            var go = new GameObject("FaceBtn", typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(2.2f, 0.66f, 1f);
            var bg = go.GetComponent<SpriteRenderer>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.93f, 0.89f, 0.80f, 0.95f); // 卡纸（暂停/倍速钮同款）
            var col = go.GetComponent<BoxCollider2D>();
            col.size = Vector2.one * 1.04f; // 本地 × scale ≈ 2.29×0.69（比纸底略大，指尖够点）

            var mainGo = new GameObject("FaceLabel", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            mainGo.transform.SetParent(holder.transform, false);
            mainGo.transform.localPosition = new Vector3(0f, 0.11f, -0.15f); // 挂 holder：body 非等比不拉宽字
            var main = mainGo.AddComponent<TextMeshPro>();
            main.font = font;
            main.fontSize = 6.5f;
            main.alignment = TextAlignmentOptions.Center;
            main.enableWordWrapping = false;
            main.text = "实";
            mainGo.GetComponent<RectTransform>().sizeDelta = new Vector2(2.2f, 0.6f);

            var subGo = new GameObject("FaceSubLabel", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            subGo.transform.SetParent(holder.transform, false);
            subGo.transform.localPosition = new Vector3(0f, -0.16f, -0.15f);
            var sub = subGo.AddComponent<TextMeshPro>();
            sub.font = font;
            sub.fontSize = 4.6f;
            sub.alignment = TextAlignmentOptions.Center;
            sub.enableWordWrapping = false;
            sub.text = "Q";
            subGo.GetComponent<RectTransform>().sizeDelta = new Vector2(2.2f, 0.5f);

            // WorldButton 必须与 collider 同物体（暂停/倍速钮同坑）
            go.AddComponent<WorldButton>().Init(ToggleFace, main);
            faceLabel = main;
            faceSubLabel = sub;
            holder.SetActive(false); // 双门在 DoEnterLevel 逐关开

            // —— 0.2s 纸色闪（z=-2.6：盖战场/格/敌，在暂停遮罩 -2.8 之后、手牌条 -0.5 之前）——
            var flashGo = new GameObject("FaceFlash", typeof(SpriteRenderer));
            flashGo.transform.SetParent(transform, false);
            flashGo.transform.position = new Vector3(0f, 0f, -2.6f);
            flashGo.transform.localScale = new Vector3(22f, 12f, 1f);
            var fsr = flashGo.GetComponent<SpriteRenderer>();
            fsr.sprite = whiteSprite;
            fsr.color = new Color(0.9686f, 0.9490f, 0.9059f, 0f); // 纸色 #F7F2E7（ADR-055 纸边同源）
            faceFlash = fsr;

            // —— F2.4 修订（用户 2026-10-22）：虚面色调层 + 红闪层 ——
            // z：红闪 -2.55（盖浅蓝）/ 浅蓝 -2.5（盖战场与手牌条，HUD 是 Screen Space Canvas 天然不受影响）。
            // 都无 collider → 不挡点击。颜色/强度/节奏全走 GameParams（Inspector 可调）。
            faceTint = MakeFullScreenLayer("FaceTint", -2.5f, new Color(0.62f, 0.78f, 0.92f, 0f));
            voidPulse = MakeFullScreenLayer("VoidPulse", -2.55f, new Color(0.72f, 0.20f, 0.16f, 0f));
        }

        /// <summary>全屏覆盖层（无 collider，纯色；alpha 0 起步）。给换面色调/红闪共用。</summary>
        private SpriteRenderer MakeFullScreenLayer(string name, float z, Color color)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 0f, z);
            go.transform.localScale = new Vector3(22f, 12f, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = color;
            return sr;
        }

        /// <summary>换面可用（章开关 × 关开关；五L1 教学关关闭）。</summary>
        private bool FaceSwitchAvailable =>
            activeChapter != null && activeChapter.faceEnabled && level != null && level.faceSwitch;

        /// <summary>
        /// 换面（钮 / Q 键共用唯一入口）：CD 从按下起算（8s）；CD 内点 = 扣 5 墨立即换面并重置 CD；
        /// 墨不足拒绝（置灰由 RefreshFaceUi 刷）；0.2s 防抖；不暂停（docs/13 F2.5 口径）。
        /// 手上有塔/选中卡时拒绝——避免悬空塔跨面（放置谓词/虚影都按当前面算）。
        /// **结算后（won/Failed，2026-10-22 用户点题）**：换面**免费、无 CD、无防抖**——
        /// 玩家需要切到另一面整理塔防（卖/挪），不再被 5 墨与 8s CD 卡住。
        /// </summary>
        private void ToggleFace()
        {
            if (model == null || !FaceSwitchAvailable) return;
            if (paused) return;
            bool gameOver = won || model.Failed;
            if (pickedTower != null || selectedCardIndex >= 0)
            {
                SfxPlayer.ClickInvalid();
                SpawnFloat("先放下手中的塔", new Vector3(8.1f, -2.9f, 0f), GrayInk);
                return;
            }
            if (!gameOver)
            {
                if (faceFlashRemaining > 0f) return; // 0.2s 窗内不接第二下（闪没走完不叠切）
                int cost = gameParams != null ? gameParams.faceSwitchCost : 5;
                if (faceCd > 0f)
                {
                    if (model.Ink < cost)
                    {
                        SpawnFloat("墨不足", new Vector3(8.1f, -2.9f, 0f), GrayInk);
                        RefreshFaceUi();
                        return;
                    }
                    model.Ink -= cost;
                    RefreshHud();
                }
                faceCd = gameParams != null ? gameParams.faceSwitchCooldown : 8f;
            }
            else
            {
                faceCd = 0f;              // 结算后：免费换面（不再进 CD、不再扣墨）
                faceFlashRemaining = 0f;  // 清残留防抖/纸闪，确保可连续切换
            }
            sfx?.PlayFaceSwitch(); // 换面（ADR-083）
            model.SetActiveFace(model.ActiveFace == 0 ? 1 : 0);
        }

        /// <summary>模型换面回调（SetActiveFace 唯一出口）：重绑格-塔 + 视图显隐 + 敌 tint + 纸闪。</summary>
        private void OnFaceChanged(int face)
        {
            ApplyFaceBindings();
            // F2.4 修订（用户 2026-10-22）：路面「本面实 / 对面虚 / 公共段不变」+ 虚面色调从这一刻起算
            if (path != null) path.ApplyFaceRoadTint(face);
            voidPulseLeft = 0f;                                     // 换面清掉未走完的红闪
            voidPulseTimer = face == 1 ? PulseInterval : 0f;         // 进虚面：满格起算（10s 后第一次闪）
            float dur = gameParams != null ? gameParams.faceSwitchFlashSeconds : 0.2f;
            faceFlashTotal = Mathf.Max(0.05f, dur);
            faceFlashRemaining = faceFlashTotal;
            if (faceFlash != null)
            {
                var c = faceFlash.color;
                c.a = 0.55f;
                faceFlash.color = c;
            }
            RefreshFaceUi();
            Debug.Log("[F2] 换面 → " + (face == 0 ? "实" : "虚") + "（本面塔 " + CountFaceTowers(face) + "）");
        }

        private int CountFaceTowers(int face)
        {
            int n = 0;
            if (model != null) foreach (var t in model.Towers) if (t.face == face) n++;
            return n;
        }

        // ============ F2.4 修订（用户 2026-10-22）：虚面「整屏浅蓝 + 每 10s 红闪」 ============

        private float PulseInterval => gameParams != null ? Mathf.Max(1f, gameParams.voidPulseInterval) : 10f;
        private float TintTarget => gameParams != null ? Mathf.Clamp01(gameParams.voidFaceTintAlpha) : 0.18f;

        /// <summary>
        /// 虚面色调轮询（每帧，随 <c>!paused</c> 段跑）：浅蓝常驻（实面淡出），红闪只在虚面计时。
        /// 红闪暂停/胜负时冻结（不闪），倍速下按游戏时间走（×2 时 10 秒 = 5 真实秒，与全局同节奏）。
        /// 强度/节奏/颜色全在 GameParams（Inspector 可调；`voidFaceTintAlpha = 0` 即整层关掉）。
        /// </summary>
        private void UpdateVoidFaceWash()
        {
            if (faceTint == null || voidPulse == null) return;
            float dt = Time.deltaTime;
            bool isVoid = model != null && model.ActiveFace == 1;
            bool alive = model != null && !model.Failed && !won;

            // ① 浅蓝：向目标 alpha 渐变（GameParams.voidFaceTintFadeSeconds 走完全程）
            float fade = Mathf.Max(0.05f, gameParams != null ? gameParams.voidFaceTintFadeSeconds : 0.35f);
            float tintTarget = isVoid ? TintTarget : 0f;
            faceTintAlpha = Mathf.MoveTowards(faceTintAlpha, tintTarget, dt / fade * Mathf.Max(0.05f, TintTarget));
            var tc = gameParams != null ? gameParams.voidFaceTint : new Color(0.62f, 0.78f, 0.92f, 1f);
            tc.a = faceTintAlpha;
            faceTint.color = tc;

            // ② 红闪：只在虚面 + 未定胜负时计时；到点冲峰值，再按 voidPulseFadeSeconds 线性淡出
            if (isVoid && alive)
            {
                voidPulseTimer -= dt;
                if (voidPulseTimer <= 0f)
                {
                    voidPulseTimer = PulseInterval;
                    voidPulseTotal = Mathf.Max(0.05f, gameParams != null ? gameParams.voidPulseFadeSeconds : 0.5f);
                    voidPulseLeft = voidPulseTotal;
                }
            }
            float pa = 0f;
            if (voidPulseLeft > 0f)
            {
                voidPulseLeft = Mathf.Max(0f, voidPulseLeft - dt);
                pa = (gameParams != null ? Mathf.Clamp01(gameParams.voidPulseAlpha) : 0.32f) *
                     Mathf.Clamp01(voidPulseLeft / Mathf.Max(0.05f, voidPulseTotal));
            }
            var pc = gameParams != null ? gameParams.voidPulseColor : new Color(0.72f, 0.20f, 0.16f, 1f);
            pc.a = pa;
            voidPulse.color = pc;
        }

        /// <summary>
        /// F2.3 格-塔绑定：`GridCell.Tower` = **当前面那格**（单引用，ADR-059）。换面时整表重绑，
        /// 塔视图按面 SetActive；敌视图按面切半透明+冷色（F2.4）。只读层不动模型数值。
        /// </summary>
        private void ApplyFaceBindings()
        {
            if (model == null || grid == null) return;
            int face = model.ActiveFace;

            // 临时格（用户 2026-10-24）：暂存塔**不跟换面逻辑**——两面都常驻可见/可操作（共用暂存）。
            var tempCell = grid.Get(TempCellX, TempCellY);
            var tempTower = tempCell != null ? tempCell.Tower : null;

            foreach (var cell in grid.All)
            {
                if (cell == tempCell) continue; // 临时格保留绑定（面无关）
                if (cell.Tower != null)
                {
                    cell.Tower = null;
                    cell.RefreshVisuals();
                }
            }
            foreach (var t in model.Towers)
            {
                if (t.face != face) continue;
                // ★ F3.1 修订（用户 2026-10-22）：**镜像不占格** —— 永不写进 cell.Tower。
                // 玩家可以在镜影那一格放别的塔（影随即隐去），占位塔移走/卖掉 → 影自动恢复。
                if (t.isMirror) continue;
                if (t == tempTower) continue; // 临时格已绑定（面无关）
                var cell = grid.Get(Mathf.RoundToInt(t.position.x), Mathf.RoundToInt(t.position.y));
                if (cell == null) continue;
                cell.Tower = t;
                cell.RefreshVisuals();
            }
            foreach (var kv in towerViewMap)
            {
                if (kv.Key.isMirror) continue; // 镜像由 RefreshMirrorPresentation 统一摆（含被占隐藏）
                if (kv.Key == tempTower) { kv.Value.gameObject.SetActive(true); continue; } // 临时格常显（面无关）
                kv.Value.gameObject.SetActive(kv.Key.face == face);
            }
            RefreshMirrorPresentation();
            foreach (var kv in activeEnemies)
                kv.Value.SetFaceDim(kv.Key.face != face && !kv.Key.crossFaceAttackable); // F6.1：傩神跨面可打 → 不暗显
            RecomputeHighlights(); // 高亮/射程圈按当前面重算（放置谓词读 cell.Tower）
        }

        /// <summary>换面钮文字（文字即状态：面名 / CD 秒数 / 跳 CD 墨价 / 墨不足置灰）。</summary>
        private void RefreshFaceUi()
        {
            if (faceLabel == null) return;
            string face = model != null && model.ActiveFace == 1 ? "虚" : "实";
            int cost = gameParams != null ? gameParams.faceSwitchCost : 5;
            string sub;
            bool dim = false;
            if (faceCd > 0f)
            {
                bool afford = model != null && model.Ink >= cost;
                sub = faceCd.ToString("0.0") + "s · " + (afford ? cost + "墨" : "缺墨");
                dim = !afford;
            }
            else sub = "Q";
            string sig = face + "|" + sub;
            if (sig == faceUiSig) return; // 帧间去重（TMP 每帧改字会 SetVerticesDirty）
            faceUiSig = sig;
            faceLabel.text = face;
            faceLabel.color = dim ? GrayInk
                : (face == "虚" ? BlueInk : HexUtil.ToColor("#26231F"));
            if (faceSubLabel != null)
            {
                faceSubLabel.text = sub;
                faceSubLabel.color = dim ? GrayInk : new Color(0.42f, 0.40f, 0.37f, 0.95f);
            }
        }

        /// <summary>暂停切换（右上角钮 / P 键）。结束态拦：结算画面有自己的按钮交互，暂停无意义。</summary>
        private void TogglePause()
        {
            if (model == null || model.Failed || won) return;
            if (paused) ResumeGame();
            else
            {
                paused = true;
                SfxPlayer.Menu(); // 菜单打开音（用户 2026-10-26）
                if (pauseVeilRoot != null) pauseVeilRoot.SetActive(true);
            }
        }

        /// <summary>恢复（PauseVeil 点任意处 / Toggle 回切 / 进关复位三入口共用）。</summary>
        private void ResumeGame()
        {
            paused = false;
            if (pauseVeilRoot != null) pauseVeilRoot.SetActive(false);
        }

        // ============ F4.1（docs/14，ADR-064）常驻回滚按钮 + 二次确认 ============

        /// <summary>
        /// 左下角常驻「回滚」按钮（z 同换面钮层；战斗中/结束后都可用）+ 二次确认遮罩（初始隐藏）。
        /// 位置 (-8.1, -3.5) = 换面钮 (8.1, -3.5) 的镜像，落在「章图底行 ↔ 手牌条顶」的空带里。
        /// </summary>
        private void BuildRetryButton()
        {
            var holder = new GameObject("RetryBtnHolder");
            holder.transform.SetParent(transform, false);
            holder.transform.position = new Vector3(-8.1f, -3.5f, -1.7f);
            retryBtnRoot = holder;

            var go = new GameObject("RetryBtn", typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.SetParent(holder.transform, false);
            go.transform.localScale = new Vector3(1.7f, 0.7f, 1f);
            var bg = go.GetComponent<SpriteRenderer>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.93f, 0.89f, 0.80f, 0.95f);
            var col = go.GetComponent<BoxCollider2D>();
            col.size = Vector2.one * 1.02f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            labelGo.transform.SetParent(holder.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.02f, -0.15f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 7f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = HexUtil.ToColor("#26231F");
            label.text = "重开";
            labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 1.2f);
            go.AddComponent<WorldButton>().Init(ConfirmRetry, label);

            BuildRetryConfirm();
        }

        /// <summary>回滚二次确认：深墨遮罩 + 居中「回滚本关？」+ 确认/取消。点遮罩空白 = 取消。</summary>
        private void BuildRetryConfirm()
        {
            var root = new GameObject("RetryConfirm");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(0f, 0f, -3.0f); // 最前（盖菜单遮罩 -2.8）
            confirmRoot = root;

            // 全屏遮罩：点空白 = 取消（WorldButton）
            var veil = new GameObject("Veil", typeof(SpriteRenderer), typeof(BoxCollider2D));
            veil.transform.SetParent(root.transform, false);
            veil.transform.localScale = new Vector3(22f, 12f, 1f);
            var vbg = veil.GetComponent<SpriteRenderer>();
            vbg.sprite = whiteSprite;
            vbg.color = new Color(0.02f, 0.02f, 0.02f, 0.6f);
            veil.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
            veil.AddComponent<WorldButton>().Init(CancelRetry, null);

            // 居中面板：纸底 + collider 挡住遮罩（点面板空白不触发取消）
            var panel = new GameObject("Panel", typeof(SpriteRenderer), typeof(BoxCollider2D));
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            panel.transform.localScale = new Vector3(6f, 3f, 1f);
            var pbg = panel.GetComponent<SpriteRenderer>();
            pbg.sprite = whiteSprite;
            pbg.color = new Color(0.93f, 0.89f, 0.80f, 0.98f);
            panel.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            // 标题
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            titleGo.transform.SetParent(root.transform, false);
            titleGo.transform.localPosition = new Vector3(0f, 0.75f, -0.2f);
            var title = titleGo.AddComponent<TextMeshPro>();
            title.font = font;
            title.fontSize = 10f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = HexUtil.ToColor("#26231F");
            title.text = "重玩本关？";
            titleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(6f, 1.2f);

            // 确认 / 取消（复用世界按钮三层结构，挂 root 下摆位）
            MakePanelButton(confirmRoot.transform, "ConfirmYes", new Vector3(-1.3f, -0.45f, -0.2f), "确认", Vermilion, false, DoRetry);
            MakePanelButton(confirmRoot.transform, "ConfirmNo", new Vector3(1.3f, -0.45f, -0.2f), "取消", HexUtil.ToColor("#26231F"), false, CancelRetry);

            root.SetActive(false);
        }

        /// <summary>面板按钮（holder 等比 → body 判定 → label 挂 holder）。dim = 占位灰态（无回调）。</summary>
        private WorldButton MakePanelButton(Transform parent, string name, Vector3 localPos, string text, Color color, bool dim, System.Action onClick,
            float width = 2.4f, float fontSize = 8f)
        {
            var holder = new GameObject(name + "Holder");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;

            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.SetParent(holder.transform, false);
            go.transform.localScale = new Vector3(width, 0.9f, 1f);
            var bg = go.GetComponent<SpriteRenderer>();
            bg.sprite = whiteSprite;
            bg.color = dim ? new Color(0.72f, 0.69f, 0.64f, 0.85f) : new Color(0.85f, 0.80f, 0.70f, 0.98f);
            go.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            labelGo.transform.SetParent(holder.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.02f, -0.15f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false; // 按钮字不换行（宽度已按字数放宽）
            label.color = dim ? new Color(0.5f, 0.48f, 0.45f, 1f) : color;
            label.text = text;
            labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(width + 0.6f, 1.2f);

            var wb = go.AddComponent<WorldButton>();
            wb.Init(dim ? (System.Action)null : onClick, label);
            return wb;
        }

        private void ConfirmRetry()
        {
            if (!paused) paused = true;               // 确认期间暂停（防敌人继续推进）
            if (confirmRoot != null) confirmRoot.SetActive(true);
        }

        private void DoRetry()
        {
            SfxPlayer.Confirm(); // 回滚二次确认（ADR-083）
            if (confirmRoot != null) confirmRoot.SetActive(false);
            RetryLevel(); // RequestEnterLevel 内 ResumeGame 复位暂停 + 场重建
        }

        private void CancelRetry()
        {
            if (confirmRoot != null) confirmRoot.SetActive(false);
            if (pauseVeilRoot == null || !pauseVeilRoot.activeSelf) ResumeGame(); // 非菜单态才恢复运行
        }

        /// <summary>P3.3（用户 2026-10-24）：菜单「退出到主界面」——先落盘再切场景。</summary>
        private void ExitToMainMenu()
        {
            WriteSave();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // ============ 家（用户 2026-10-23）：道路尽头可升级产墨 ============

        /// <summary>
        /// 家视图（运行时自建一次）：纸底 + 「家 LvN / N墨/5s」文字 + 点击升级。
        /// 等级 0 = 不产墨；1-5 级产墨 1/2/4/8/16 每 5s（同墨塔 ×2 成长）；每章重置。
        /// </summary>
        private void BuildHomeUi()
        {
            var root = new GameObject("HomeRoot");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(0f, 0f, -1.75f); // 浮于路面/敌前
            homeRoot = root;

            // 点击判定（独立不可见 collider——字本体不挂 collider）
            var hit = new GameObject("HomeHit", typeof(BoxCollider2D));
            hit.transform.SetParent(root.transform, false);
            hit.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            hit.GetComponent<BoxCollider2D>().size = Vector2.one;
            hit.AddComponent<WorldButton>().Init(UpgradeHome, null);

            // 「家」字：塔防文字风格（阶色 = 升级次数，无纸底/无额外文字）
            var labelGo = new GameObject("HomeChar", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.15f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.text = "家";
            labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 3f);
            homeLabel = label;

            root.SetActive(false); // 进关定位后再显示
        }

        /// <summary>家阶色（同塔：L1 黑 / L2 绿 / L3 蓝 / L4 紫 / L5 金；未升级灰）。</summary>
        private static Color HomeTierColor(int level)
        {
            switch (level)
            {
                case 1: return HexUtil.ToColor("#26231F");
                case 2: return HexUtil.ToColor("#4F7A45");
                case 3: return HexUtil.ToColor("#3E6B8C");
                case 4: return HexUtil.ToColor("#6E5594");
                case 5: return HexUtil.ToColor("#A67C1B");
                default: return new Color(0.55f, 0.53f, 0.50f, 0.9f); // 未升级：灰
            }
        }

        /// <summary>进关定位：家 = 实路终点；刷新文字。章界清零已在 DoEnterLevel 处理。</summary>
        private void RefreshHome()
        {
            if (homeRoot == null) return;
            if (path == null) { homeRoot.SetActive(false); return; }
            var route = path.GetFace(0);
            if (route.PointAt == null || route.Length <= 0) { homeRoot.SetActive(false); return; }
            homePos = (activeChapter != null && activeChapter.bidirectional)
                ? path.HomePosition                                  // 双向章（ch3）：家 = 弧长中点（正中格，敌半程终点）
                : route.PointAt(route.Length);                       // 单向章：家 = 路线终点
            homeRoot.transform.position = new Vector3(homePos.x, homePos.y, -1.75f);
            homeRoot.SetActive(true);
            UpdateHomeLabel();
        }

        /// <summary>家当前每跳产墨（0 = 未升级；1/2/4/8/16 同墨塔 ×inkGrowth）。</summary>
        private int HomeInkPerPulse
        {
            get
            {
                if (homeLevel <= 0) return 0;
                double g = gameParams != null ? gameParams.inkGrowth : 2.0;
                int b = gameParams != null ? gameParams.homeInkBase : 1;
                return Mathf.RoundToInt((float)(b * System.Math.Pow(g, homeLevel - 1)));
            }
        }

        private void UpdateHomeLabel()
        {
            if (homeLabel == null) return;
            // P1.2（用户 2026-10-24）：简单模式家为灰态（不可升级）；困难模式按阶色
            homeLabel.color = hardMode ? HomeTierColor(homeLevel) : new Color(0.55f, 0.53f, 0.50f, 0.55f);
        }

        /// <summary>点击家 = 升级（当前无成本，同初始墨升级；满级弹文案）。</summary>
        private void UpgradeHome()
        {
            if (model == null) return;
            // P1.2（用户 2026-10-24）：家只有困难模式能升级（简单模式出现但灰态）
            if (!hardMode)
            {
                SpawnFloat("困难模式才能升级家", (Vector3)homePos + Vector3.up * 0.9f, GrayInk);
                return;
            }
            int max = gameParams != null ? gameParams.homeMaxLevel : 5;
            if (homeLevel >= max)
            {
                SpawnFloat("家已满级", (Vector3)homePos + Vector3.up * 0.9f, GrayInk);
                return;
            }
            homeLevel++;
            if (save != null && CurrentChapterState != null) CurrentChapterState.homeLevel = homeLevel;
            homeInkTimer = 0; // 升级后重新计时
            UpdateHomeLabel();
            SpawnFloat("家升级 → Lv" + homeLevel, (Vector3)homePos + Vector3.up * 0.9f, BlueInk);
            RefreshHud();
            WriteSave(); // 章内即时落盘（与红铲同口径）
        }

        /// <summary>家产墨（每 homeInkPulseSeconds 一跳；只在未定胜负时调用 = 经济随战斗停）。</summary>
        private void UpdateHomeInk(double dt)
        {
            // P1.2：简单模式家不产墨（homeLevel 恒 0，双保险）
            if (!hardMode || homeLevel <= 0 || model == null || gameParams == null) return;
            homeInkTimer += dt;
            double interval = Mathf.Max(0.5f, gameParams.homeInkPulseSeconds);
            int guard = 0;
            while (homeInkTimer >= interval && guard++ < 4)
            {
                homeInkTimer -= interval;
                int per = HomeInkPerPulse;
                model.Ink += per;
                sfx?.PlayInkPulse(); // 家产墨（0.3s 节流）
                SpawnFloat("+" + per, (Vector3)homePos + Vector3.up * 0.5f, BlueInk, 9f);
                RefreshHud();
            }
        }

        // ============ 临时格（用户 2026-10-23）：一个「被沉默」的空地格 ============

        // 临时格坐标（左下；各章通用）。塔放上去不攻击/不产墨（BattleModel 按坐标跳过）。
        private const int TempCellX = -6;
        private const int TempCellY = -4;

        /// <summary>
        /// 进关创建临时格（普通空地格，像塔防格）：塔用正常放置/拖拽/互换流程上去，
        /// 只是模型按坐标把它当「沉默」——不攻击、不产墨。零自定义视图/存塔逻辑 = 无脱离 bug。
        /// 视觉搬到地图外（红铲原位，左下 UI 区）；逻辑坐标不变（模型 gate / 换面重绑用）。
        /// </summary>
        private void EnsureTempCell()
        {
            if (grid == null) return;
            var cell = grid.AddCell(TempCellX, TempCellY); // 已有则内部忽略
            if (cell != null)
            {
                // 用户 2026-10-24：格子不要放在地图上 → 视觉搬到左下 UI 区（红铲原位）
                cell.transform.position = new Vector3(-6.3f, -4.55f, 0f);
            }
            if (model != null)
            {
                model.TempCellX = TempCellX;
                model.TempCellY = TempCellY;
            }
        }

        private void BuildResultButtons()
        {
            var rootGo = new GameObject("ResultButtons");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = new Vector3(0f, 0f, -1.7f); // 面板卡层附近，浮于战场之上
            resultButtonsRoot = rootGo;

            // 手感优化（用户 2026-10-09 三轮+四轮更正）：「下一关」右下角下一波原位（坐标取
            // handBar.NextButtonWorldPos；结束后下一波已灰、本钮盖其上）；「回滚」**左下角
            // 对称位**（四轮更正——三轮口误右下）。slot 存字段供 ShowResultButtons 复用
            //（上轮只改 Build 初始位、被 Show 硬编码重摆打回——本轮三处硬编码一并改）。
            resultSlot = handBar != null ? handBar.NextButtonWorldPos : new Vector3(8.1f, -4.55f, 0f);
            resultSlot = new Vector3(resultSlot.x, resultSlot.y, 0f); // z 丢弃（root 层 -1.7 自带）
            retrySlot = new Vector3(-resultSlot.x, resultSlot.y, 0f);
            btnNext = MakeResultButton("BtnNext", resultSlot, true, 8f, AdvanceLevel, out nextHolder);
            btnRetry = MakeResultButton("BtnRetry", retrySlot, false, 6f, RetryLevel, out retryHolder);
            resultButtonsRoot.SetActive(false);
        }

        private WorldButton MakeResultButton(string name, Vector3 localPos, bool primary, float fontSize, Action onClick, out Transform holder)
        {
            // 三层结构（宽按钮文字等比父坑，HandBar 下一波钮同款）：
            // holder 等比（挪位/显隐/挂字）→ body 非等比（纸底+判定，白方块拉宽无所谓）→
            // Label 挂 holder 下（等比，字形不被横向拉伸）
            var holderGo = new GameObject(name + "Holder");
            holderGo.transform.SetParent(resultButtonsRoot.transform, false);
            holderGo.transform.localPosition = localPos;
            holder = holderGo.transform;

            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.SetParent(holderGo.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(2.5f, 1f, 1f); // 插A 五轮（用户 2026-10-09）：按钮缩身 5×1.3 → 2.5×1
            var bg = go.GetComponent<SpriteRenderer>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.93f, 0.89f, 0.80f, 0.95f); // 卡纸（HandBar 同款）
            var col = go.GetComponent<BoxCollider2D>();
            col.size = Vector2.one * 1.02f; // 本地单位 × scale ≈ 2.55×1.02，指尖友好

            if (primary)
            {
                // 朱框垫底放大一圈（HandBar 卡框同法）：主操作「下一关」的视觉层级
                var frameGo = new GameObject("Frame", typeof(SpriteRenderer));
                frameGo.transform.SetParent(go.transform, false);
                frameGo.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                frameGo.transform.localScale = new Vector3(1.06f, 1.14f, 1f);
                var frame = frameGo.GetComponent<SpriteRenderer>();
                frame.sprite = whiteSprite;
                frame.color = HexUtil.ToColor("#B83A2E"); // 朱
            }

            // Label：挂 holder（等比）而非 body——字宽 ≈1.2/字（fontSize 12），
            // z=-0.15 浮于纸底前。插A 五轮：字号分级参数化（下一关 8 / 回滚 6——主次按钮层级）。
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            labelGo.transform.SetParent(holderGo.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.02f, -0.15f);
            var label = labelGo.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = HexUtil.ToColor("#26231F"); // 浓墨
            label.enableWordWrapping = false;
            var rt = (RectTransform)labelGo.transform;
            rt.sizeDelta = new Vector2(4.6f, 0.9f);

            var btn = go.AddComponent<WorldButton>();
            btn.Init(onClick, label);
            return btn;
        }

        /// <summary>结果按钮显示（幂等：失败态 RefreshHud 每 0.1s 重刷无害）。</summary>
        private void ShowResultButtons(int mode)
        {
            if (resultButtonsRoot == null || resultButtonMode == mode) return;
            resultButtonMode = mode;
            resultButtonsRoot.SetActive(true);
            nextHolder.gameObject.SetActive(true);
            retryHolder.gameObject.SetActive(true);
            // 插A 四轮（2026-10-09）：下一关右下（下一波原位）、回滚**左下对称**（用户口径
            // 更正）；单钮模式（章末占右下/失败占左下）各守其位。
            nextHolder.localPosition = resultSlot;
            retryHolder.localPosition = retrySlot;

            if (mode == 0) // 过关：双钮（N 右下 + R 左下对角）
            {
                btnNext.SetText("下一关");
                btnRetry.SetText("重玩本关");
            }
            else if (mode == 1) // 章末：单钮清场回 L1（右下下一波原位）
            {
                nextHolder.localPosition = resultSlot;
                btnNext.SetText("回第一章");
                retryHolder.gameObject.SetActive(false);
            }
            else // 失败：单钮回滚（左下角）
            {
                retryHolder.localPosition = retrySlot;
                btnRetry.SetText("不服再战");
                nextHolder.gameObject.SetActive(false);
            }
        }

        private void HideResultButtons()
        {
            resultButtonMode = -1;
            if (resultButtonsRoot != null) resultButtonsRoot.SetActive(false);
        }

        /// <summary>N：推进下一关（章内带塔 / 章界清场带背包，ADR-031）；胜利与否同语义。
        /// 2026-10-05 用户定案：未胜快进原为「清场跳进」，改与胜利推进一致——验收塔存续不必真打通
        /// （用户实测：N 快进丢塔，排查两眼一抹黑）；正式操作仍走结果大按钮，此键保持调试通道定位。</summary>
        private void AdvanceLevel()
        {
            if (won)
            {
                int next = levelIndex + 1;
                if (next < levels.Length)
                    // F4.3（ADR-031）章界检测：下一关换章图 → 清场带背包（塔/垦地不带；背包/构筑全保留）
                    RequestEnterLevel(next, ChapterOf(levels[next]) != activeChapter ? null : CaptureField(), true);
                else
                    RequestEnterLevel(1, null, true); // 原型全通（二L5 毕业考通关）：清场回 L1 重开循环，背包/构筑保留
            }
            else
            {
                // 测试跳关（带塔快进，语义同胜利推进）：末关环绕回 L1——L0 教学不是跳关目的地（误触重跑六步教学很伤）。
                // 曾用 (levelIndex+1) % levels.Length：L0 插位后末关环绕落 0 = 教学关（用户踩中）
                int next = levelIndex + 1;
                if (next >= levels.Length) next = 1;
                // 章界/环绕换章照样清场（同上分支口径）；章内则照搬当前场（含塔与垦地）
                RequestEnterLevel(next, ChapterOf(levels[next]) != activeChapter ? null : CaptureField(), true);
            }
        }

        /// <summary>R：回滚本关开始快照（ADR-018）。</summary>
        private void RetryLevel() => RequestEnterLevel(levelIndex, snapshot);

        // ============ 测试键（docs/14 菜单，仅测试用） ============

        /// <summary>上一关（不进 L0 教学；章界照清场，同 AdvanceLevel 口径）。</summary>
        private void PrevLevel()
        {
            if (levels == null || levels.Length == 0) return;
            int prev = Mathf.Max(1, levelIndex - 1);
            RequestEnterLevel(prev, ChapterOf(levels[prev]) != activeChapter ? null : CaptureField());
        }

        /// <summary>删档重开：删存档文件 + 重载场景（全新档；测试用）。</summary>
        private void DeleteSaveAndRestart()
        {
            SfxPlayer.Confirm(); // 删档确认（ADR-083）
            SaveSystem.Delete();
            var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(sc.buildIndex >= 0 ? sc.buildIndex : 0);
        }

        // ============ F2 红铲造格（ADR-054，2026-10-20 用户定案） ============

        /// <summary>当前价 = 首价 + 每次递增 × 本章已买次数（50 / 100 / 150…）。</summary>
        private int RedShovelPrice => gameParams != null
            ? gameParams.redShovelBaseCost + gameParams.redShovelCostStep * redShovelBought
            : 50;

        /// <summary>本关是否显示红铲按钮（关卡序号 &gt;= 解锁关；L0-L2 不显示）。</summary>
        private bool RedShovelUnlocked => gameParams != null && levelIndex >= gameParams.redShovelUnlockLevel;

        /// <summary>
        /// F2.2 按钮入口：未进模式 = 扣墨进模式；已在模式 = 取消并全额退款。
        /// 墨不足 / 未解锁 = 无事发生（按钮侧置灰/隐藏，任务单 F2.2 口径）。
        /// </summary>
        private void ToggleRedShovel()
        {
            if (model == null || level == null) return;
            if (redShovelMode) { CancelRedShovel(true); return; }
            if (!RedShovelUnlocked) return;

            int cost = RedShovelPrice;
            if (model.Ink < cost) return; // 置灰：点了无事发生（不扣墨、不飘字）

            if (pickedTower != null)
            {
                CancelPickup();
                if (pickedTower != null) return; // 灰烬格归位被拒（塔仍在手）：先处理这塔再进造格
            }
            selectedCardIndex = -1; // 手牌选中态清掉（高亮让位给造格红框）
            model.Ink -= cost;
            redShovelPaidInk = cost;
            redShovelMode = true;
            BuildShovelField();
            SpawnFloat("万能铲 " + cost + " 墨：点朱红格造地（再点按钮退款）",
                handBar != null ? handBar.ShovelButtonWorldPos + Vector3.up * 0.7f : Vector3.zero, BlueInk);
            RecomputeHighlights(); // 选中卡清除后高亮同步收掉
            RefreshHud();
        }

        /// <summary>
        /// 退出红铲模式。refund = true：全额退墨（取消 / 换关 / 回滚 / 开面板）；
        /// 成交（造格成功）由调用方先清 paidInk 再退出——扣的墨已转化为地皮。
        /// </summary>
        private void CancelRedShovel(bool refund)
        {
            bool wasActive = redShovelMode || redShovelPaidInk > 0;
            redShovelMode = false;
            DestroyShovelField();
            if (refund && redShovelPaidInk > 0 && model != null)
            {
                model.Ink += redShovelPaidInk;
                SpawnFloat("万能铲退款 +" + redShovelPaidInk + "墨",
                    handBar != null ? handBar.ShovelButtonWorldPos + Vector3.up * 0.7f : Vector3.zero, BlueInk);
            }
            redShovelPaidInk = 0;
            if (wasActive) RefreshHud();
        }

        /// <summary>
        /// 合法区 = 章图已有机格包围盒外扩 1 格（再与可视区求交：外扩格可能出屏，屏外点不到）。
        /// 只算一次（进关时）——章内格子布局固定，红铲新格都在区内。
        /// </summary>
        private void ComputeShovelBounds(ChapterConfig chapter)
        {
            if (chapter == null || chapter.cells == null || chapter.cells.Count == 0)
            {
                shovelMinX = shovelMaxX = shovelMinY = shovelMaxY = 0;
                return;
            }
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in chapter.cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }
            // ±9 × ±4 = 可视建造区（1920×1080 / ortho 5.4；格心再大半格就出屏，手感上点不到）
            shovelMinX = Mathf.Max(-9, minX - 1);
            shovelMaxX = Mathf.Min(9, maxX + 1);
            shovelMinY = Mathf.Max(-4, minY - 1);
            shovelMaxY = Mathf.Min(4, maxY + 1);
        }

        /// <summary>F2.2 合法性（与飘字文案同源）：区内 ∧ 无格 ∧ 非路径（陆路 + 水路航道）。</summary>
        private bool CanShovelAt(int x, int y, out string reason)
        {
            reason = null;
            if (x < shovelMinX || x > shovelMaxX || y < shovelMinY || y > shovelMaxY)
            {
                reason = "只能造在格子区外扩一格内";
                return false;
            }
            if (grid == null) return false;
            if (grid.Get(x, y) != null) { reason = "此处已有格"; return false; }
            // 路径格根本没有 GridCell——必须显式查路线（不能靠「这格没格子」反推）
            if (grid.IsRouteCell(x, y)) { reason = "路径不可造格"; return false; }
            return true;
        }

        /// <summary>
        /// 建「点选层」：一块盖住合法区的碰撞体（z=-0.3：格 0 之前 / 手牌条 -0.4 之后 /
        /// 暂停遮罩 -2.8 之前）+ 每个合法空位一块朱红半透明底（视觉提示）。
        /// 路径格与已有格也落在本层内——统一进 OnShovelFieldClicked 分案飘字。
        /// </summary>
        private void BuildShovelField()
        {
            DestroyShovelField();
            if (grid == null) return;

            float cx = (shovelMinX + shovelMaxX) * 0.5f;
            float cy = (shovelMinY + shovelMaxY) * 0.5f;
            float w = shovelMaxX - shovelMinX + 1;
            float h = shovelMaxY - shovelMinY + 1;

            shovelCatcher = new GameObject("ShovelField", typeof(BoxCollider2D), typeof(WorldButton));
            shovelCatcher.transform.SetParent(transform, false);
            shovelCatcher.transform.position = new Vector3(cx, cy, -0.3f);
            var col = shovelCatcher.GetComponent<BoxCollider2D>();
            col.size = new Vector2(w, h);
            shovelCatcher.GetComponent<WorldButton>().Init(OnShovelFieldClicked, null);

            for (int x = shovelMinX; x <= shovelMaxX; x++)
                for (int y = shovelMinY; y <= shovelMaxY; y++)
                {
                    if (!CanShovelAt(x, y, out _)) continue;
                    var go = new GameObject("ShovelGhost", typeof(SpriteRenderer));
                    go.transform.SetParent(shovelCatcher.transform, false);
                    go.transform.localPosition = new Vector3(x - cx, y - cy, 0.13f); // 世界 z≈-0.17：雾前、敌后
                    go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sprite = whiteSprite;
                    sr.color = new Color(0.72f, 0.23f, 0.18f, 0.18f);
                }
        }

        private void DestroyShovelField()
        {
            if (shovelCatcher != null) Destroy(shovelCatcher); // 红框是其子物体，一并销毁
            shovelCatcher = null;
        }

        /// <summary>红铲点选（WorldButton 回调，同帧取指针）：非法 → 飘字拒绝且**不退出模式、不扣墨**。</summary>
        private void OnShovelFieldClicked()
        {
            if (!redShovelMode || grid == null) return;
            var cam = Camera.main;
            Vector3 wp = cam != null ? cam.ScreenToWorldPoint(Input.mousePosition) : Vector3.zero;
            int x = Mathf.RoundToInt(wp.x);
            int y = Mathf.RoundToInt(wp.y);

            if (!CanShovelAt(x, y, out string reason))
            {
                SpawnFloat(reason, new Vector3(x, y, 0f) + Vector3.up * 0.5f, GrayInk);
                return;
            }

            var cell = grid.AddCell(x, y);
            if (cell == null) return; // 竞态兜底（已有格）

            // 成交：扣的墨不退（已转化为地皮），退出模式，计数 +1（下次更贵）
            redShovelMode = false;
            redShovelPaidInk = 0;
            DestroyShovelField();
            redShovelBought++;
            extraCells.Add(new Vector2Int(x, y));
            sfx?.PlayRedShovel(); // 红铲造格（ADR-083）
            if (activeChapter != null && activeChapter.fogEnabled && model != null)
                grid.SetFogLights(model.GetLights()); // ch3：新格落在雾砖层下，挖洞重算一次（所见即所得）
            SpawnFloat("造格 +1", new Vector3(x, y, 0f) + Vector3.up * 0.5f, BlueInk);
            Debug.Log("[F2.2] 红铲造格 (" + x + "," + y + ")：本章已买 " + redShovelBought +
                " 次，下次 " + RedShovelPrice + " 墨；本章造格合计 " + extraCells.Count);
            RecomputeHighlights();
            RefreshHud();
            WriteSave();
        }

        private void Update()
        {
            if (model == null) return;

            // 构筑面板/结算/日记打开 = 全局暂停（03 §4.6：面板期间不出怪/不走表/不吃键）
            bool panelOpen = (deckPanel != null && deckPanel.IsOpen)
                          || (clearPanel != null && clearPanel.IsOpen)
                          || (diaryPanel != null && diaryPanel.IsOpen);
            if (panelOpen) return;

            // 暂停闸（用户 2026-10-12 点题）：paused 只冻结战斗推进与视觉轮询（Tick/波次/tint/
            // 栖灯倒数）；输入段照常——键盘调试通道照用（R/N 进关自动复位暂停）、「点击任意
            // 处继续」走 PauseVeil 的 OnMouseDown（collider 最前），不经本闸。BGM 照播。
            if (!paused)
            {
                if (!model.Failed && !won)
                {
                    model.Tick(Time.deltaTime);
                    waveClock += Time.deltaTime;
                    TryLaunchDueWaves();
                    UpdateEruptions(); // F2.1：喷排/落地轮询（波钟刻度——与波次同闸：暂停/胜负同冻结）
                    UpdateLavaMarks(Time.deltaTime); // F2.4：红印 DoT 整秒跳（同闸同钟域）
                    UpdateTrample();                 // F6.1：麟践踏光环（格变灰 + 提示圈随行——同闸：暂停/胜负同冻结）
                    UpdateHomeInk(Time.deltaTime);   // 家（用户 2026-10-23）：每 5s 产墨（胜负同冻结）
                }

                // F3.1 修订（ch5）：镜像不占格 → 每帧摆显隐/空心描边（被占则隐）；F3.4 幡魂计数小字。
                RefreshMirrorPresentation();
                RefreshSoulCounters();

                // F2.5（ch5 双面）：换面 CD（游戏时间——倍速同步）与 0.2s 纸闪淡出（不暂停战斗）。
                if (faceCd > 0f)
                {
                    faceCd = Mathf.Max(0f, faceCd - Time.deltaTime);
                    RefreshFaceUi();
                }
                if (faceFlashRemaining > 0f && faceFlash != null)
                {
                    faceFlashRemaining = Mathf.Max(0f, faceFlashRemaining - Time.deltaTime);
                    var fc = faceFlash.color;
                    fc.a = 0.55f * (faceFlashRemaining / Mathf.Max(0.01f, faceFlashTotal));
                    faceFlash.color = fc;
                    if (faceFlashRemaining <= 0f) RefreshFaceUi();
                }
                UpdateVoidFaceWash(); // F2.4 修订：虚面浅蓝 + 每 10s 红闪（暂停/胜负冻结）
                UpdateShenMasks();    // F6.1（ch5 傩神）：三面随行（沿路摆位；不在场自动收）

                // F2.1/F2.2 敌 tint（ch3 幽夜，06 §11.2）：雾中普通敌深影剪影 / 潜行敌（蝠猫）
                // 褪色幽灵浅影 / 入光全色现形——11.8 曲线②「看得见但易漏」。读 e.litCache（model
                // Tick 敌循环本帧已算——速度结算/伤害加成/视觉同帧同值）；view 内 bool 去重零开销。
                // fog 关（ch1/ch2）整块跳过（铁律零变化）。枭红闪 F5 同管线加分支。
                if (activeChapter != null && activeChapter.fogEnabled && gameParams != null)
                {
                    foreach (var kv in activeEnemies)
                        kv.Value.SetFogTint(kv.Key.litCache, gameParams.fogEnemyDim, gameParams.fogEnemyAlpha,
                            gameParams.fogStealthColor, gameParams.fogStealthAlpha);
                }

                // F5.1 枭栖灯锁定视觉：灯保持点亮（塔字不灰——06 §11.4 杀窗口径）+ 头顶朱色秒数
                // 倒数（吞噬倒计时，秒尽灯消）。单 Boss 单栖位 → 遍 activeEnemies 查 perchTower
                //（枭 ×1 微开销）；灯被吞 = OnLampDevoured 释放视图，无残留可清。
                if (activeChapter != null && activeChapter.fogEnabled && model != null)
                {
                    foreach (var kv in activeEnemies)
                        if (kv.Key.perchTower != null && towerViewMap.TryGetValue(kv.Key.perchTower, out var lockView))
                            lockView.ShowLockCountdown(kv.Key.perchRemaining);
                }
            }

            // F1（docs/14，ADR-062）手牌拖拽轮询：起点在 HandCard.OnMouseDown（OnHandCardPressed），
            // 此处按屏幕像素阈值分辨「点选 / 拖拽」。功能按钮（index<0）已在 OnMouseDown 即时点选，
            // 不经过本分支。paused 时不轮询（暂停遮罩已挡掉手牌交互）。
            if (pressedCardIndex >= 0 && !paused)
            {
                if (!handDragging)
                {
                    if (Input.GetMouseButton(0) &&
                        ((Vector2)Input.mousePosition - pressedScreenPos).magnitude > HandDragThresholdPx)
                    {
                        StartHandCardDrag(pressedCardIndex); // 可拖 → handDragging=true；不可拖 → 保持 false（松手按点选兜底）
                    }
                    else if (Input.GetMouseButtonUp(0))
                    {
                        int idx = pressedCardIndex;
                        pressedCardIndex = -1;
                        OnHandCardClicked(idx); // 阈值内松手 = 点选（旧路径）
                    }
                }
                else
                {
                    if (Input.GetMouseButton(0)) UpdateHandCardDrag();
                    else EndHandCardDrag(); // 松手
                }
            }

            // F2（docs/14，ADR-063）拿起塔拖拽：超阈值塔跟手；松手过卖出区 = 卖，否则回弹原格。
            if (pickedTower != null && !paused)
            {
                if (!towerDragging && Input.GetMouseButton(0) &&
                    ((Vector2)Input.mousePosition - towerPickupScreenPos).magnitude > TowerDragThresholdPx)
                {
                    towerDragging = true;
                }
                if (towerDragging)
                {
                    if (Input.GetMouseButton(0)) FollowTowerDrag();
                    else EndTowerDrag();
                }
            }

            // F3.1（docs/14 ADR-063）：点空白 = 取消（右键等价，触屏兜底）——未命中任何交互体才取消。
            if (!paused && Input.GetMouseButtonDown(0))
                HandleBlankClick();

            // 空格 = 下一波提前召唤；D = 抽卡；R = 回滚本关开始（ADR-018）；
            // N = 推进下一关（章内带塔 / 章界与末关清场回 L1）——胜不胜利同语义（2026-10-05 定案）。
            // 键盘 = 桌面调试通道（0.2 起正式操作走结果大按钮，语义同 AdvanceLevel/RetryLevel）
            if (Input.GetKeyDown(KeyCode.Space)) EarlySummonNextWave();
            if (Input.GetKeyDown(KeyCode.D)) TryDraw();
            if (Input.GetKeyDown(KeyCode.R)) RetryLevel();
            if (Input.GetKeyDown(KeyCode.N)) AdvanceLevel();
            if (Input.GetKeyDown(KeyCode.Q)) ToggleFace(); // F2.5（ch5 双面）：换面（与按钮同入口；非双面章空转）
            // F2.1 调试通道：T = 水线 0→4 循环（Play 拖 Chapter2.asset 预览潮汐渐变；
            // 4 = 全图洪水预览（ADR-035 / F5 蛟 B 方案）：塔全沉默 + 整屏汪洋 + 墨路淹没。
            // ch1 全图海拔 4 → 0-3 段无视觉效果；按到 4 时整图也会淹（仅调试预览，非 gameplay）
            if (Input.GetKeyDown(KeyCode.T)) model.SetWaterLevel((model.WaterLevel + 1) % 5);
            // F3.2 眩晕调试通道：G = 全场敌眩晕 2s（手摆验收 ADR-039——停走 / 连按只刷新不叠加 /
            // 琥珀金 tint 与冻结冰蓝可辨；触发方 F3.3 法塔落地后本键留作回归速查）
            if (Input.GetKeyDown(KeyCode.G)) model.ApplyStunAll(2.0);
            // F4.1 燃烧调试通道：B = 全场敌挂燃（GameParams burnDps×burnSeconds——手摆验收：
            // 整秒跳 -3 飘字 / 连按只刷新时长不叠伤 / 3s 熄灭 / 冻结中照样烧）。触发方 F4.2
            // 火塔落地后留回归速查（G 眩晕家族同惯例）；灰烬增益半边归 F4.2 联合验收。
            if (Input.GetKeyDown(KeyCode.B) && gameParams != null)
                model.ApplyBurnAll(gameParams.burnSeconds, gameParams.burnDps);
            // F7.1 手测脚手架（用户 2026-10-18 点题）：Y = 场上 Boss 直压半血——省手磨
            //（速达转阶段三段演出/二阶段验收）。只压血不改机制：Tick 血线判定照常触发转阶段；
            // 无敌中（演出窗）压血无害（Damage 早退，但血线判定独立）。PC/Editor 限定家族。
            if (Input.GetKeyDown(KeyCode.Y) && model != null)
            {
                int hit = 0;
                for (int i = 0; i < model.Enemies.Count; i++)
                {
                    var be = model.Enemies[i];
                    if (be.dead || be.reached || !be.isBoss) continue;
                    double half = be.maxHp * 0.5;
                    if (be.hp > half) be.hp = half;
                    hit++;
                }
                Debug.Log("[F7.1] 调试：Boss 压至半血 ×" + hit);
            }
            // F1.2 调试验收通道（ch4 火山）：H = 鼠标指向格灰烬态循环（Normal→Ash→Green→
            // Normal）——喷射引擎 F2 落地前的手摆谓词/视觉验收；F2 后留回归速查（T 水线/
            // G 眩晕家族同惯例）。正交相机 z 取 |cam.z| 拉到格平面（z=0）。
            if (Input.GetKeyDown(KeyCode.H) && grid != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var mp = Input.mousePosition;
                    var w = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, -cam.transform.position.z));
                    var hc = grid.Get(Mathf.RoundToInt(w.x), Mathf.RoundToInt(w.y));
                    if (hc != null) hc.DebugCycleTileState();
                }
            }
            // 暂停切换（用户 2026-10-12）：桌面调试通道同款（Space/D/R/N/T/G 家族）；
            // 移动端正式操作走右上角暂停钮（触屏单指同管线）。
            if (Input.GetKeyDown(KeyCode.P)) TogglePause();

            // 手感优化（用户 2026-10-09）：右键 = 取消——拿起塔放回原格（复用 CancelPickup，
            // 与点虚影原格同路径）/ 选中手牌清选。桌面调试通道（移动端正式操作走触屏无右键；
            // 不影响任何已有交互——再点同卡 toggle、点虚影原格放回均保留）。
            if (Input.GetMouseButtonDown(1))
            {
                if (pickedTower != null) CancelPickup();
                else if (selectedCardIndex >= 0)
                {
                    selectedCardIndex = -1;
                    RecomputeHighlights();
                }
            }

            hudTimer -= Time.deltaTime;
            if (hudTimer <= 0f)
            {
                hudTimer = 0.1f;
                CheckWon();
                PollBossStage(); // BGM/黑闪轮询（Boss 出场切轨 + 枭氛围闪；幂等）
                RefreshHud();
            }
        }

        // ============ 波次时间线（LevelConfig.waves 驱动） ============

        private void ComputeWaveSchedule()
        {
            int n = level.waves.Count;
            waveStart = new double[n];
            for (int i = 0; i < n; i++)
                waveStart[i] = i == 0 ? level.prepSeconds : waveStart[i - 1] + level.waves[i].startAfterPrev;
        }

        /// <summary>波 i 的实际起始时刻（含提前召唤偏移）。</summary>
        private double EffectiveStart(int i)
        {
            return waveStart[i] - timelineShift;
        }

        private void TryLaunchDueWaves()
        {
            while (nextWaveIndex < level.waves.Count && waveClock >= EffectiveStart(nextWaveIndex))
                LaunchWave(nextWaveIndex++);
        }

        /// <summary>
        /// F5.2（ch5 傩面青脸）：每波开波这一刻，若场上傩面正处**青脸**（且已过换脸 grace），召 6 小鬼。
        /// 绑波次、不按固定节拍（防无限刷墨 = 06 §14.3 的设计意图）；从傩面身上放出（同面、同进度
        /// 向后错开），不是从出怪口刷——「切虚面收墨」才有意义。
        /// </summary>
        private void TryNuoMaskSummon()
        {
            if (model == null || gameParams == null || enemyTable == null) return;
            if (string.IsNullOrEmpty(gameParams.nuoSummonChar)) return;
            var stats = enemyTable.Find(gameParams.nuoSummonChar);
            if (stats == null) return;
            int count = model.Enemies.Count; // 先取快照：召唤物追加在表尾，本循环不回访（与分裂同口径）
            for (int i = 0; i < count; i++)
            {
                var e = model.Enemies[i];
                if (e.dead || e.reached) continue;
                if (e.nuoMask && e.nuoMaskIndex == 1 && e.nuoGraceRemaining <= 0)
                    SummonGhosts(e, stats, e.face, gameParams.nuoSummonPerWave);   // P1 青脸：本面 6 鬼
                else if (e.nuoShen)
                    SummonGhosts(e, stats, 1, gameParams.shenSummonPerWave);       // P2 青：虚面 3 鬼
            }
        }

        /// <summary>F5.2/F6.2 召鬼：从召主身上同面放出、按序号向后错开（防叠字），并飘字。</summary>
        private void SummonGhosts(EnemyState src, EnemyStats stats, int face, int n)
        {
            if (src == null || stats == null || n <= 0) return;
            for (int k = 0; k < n; k++)
            {
                var child = model.SpawnEnemy(stats, null, false, 0, 1.0, face);
                child.dist = Mathf.Max(0f, (float)src.dist - 0.6f - k * 0.55f); // 向后错开防叠字
            }
            SpawnFloat("召鬼 ×" + n, (Vector3)model.GetEnemyPosition(src) + Vector3.up * 0.6f, BlueInk, 11f);
        }

        private void LaunchWave(int i)
        {
            var layout = level.waves[i];
            model.Wave = i + 1;
            if (hud != null) hud.SetWave(model.Wave);

            // F2.1 潮汐预报（06 §10.2）：波开始时切水线——波次表可预读即潮汐表。
            // Hold = 沿用当前水线（ch1 全 Hold → 潮汐系统零激活，铁律不破）
            if (layout.tide != TideLevel.Hold)
                model.SetWaterLevel((int)layout.tide);

            // F2.1（ch4 火山，06 §12.2「每波开始后 ~4s 随机喷射」）：波开始排喷。
            // 章开关×关数据双门在 volcanoActive（ch1-3 恒 false 零进入——铁律）。
            if (volcanoActive) ScheduleEruption(i);

            // F4.6（ADR-031 / 06 §10.5）：ch2 事件化教学气泡——潮汐首秀（二L1 W2）、
            // 撤离三选项（二L2 W2）、湿冻连携（二L4 W4）。ch1 无命中零变化。
            // F4.4（06 §11.5）：ch3 同模式三处 + 三L5 开场枭预告（不做 ch3 版 L0——章定案）。
            // F5.3（06 §12.6）：ch4 唯一气泡 = 四L5 开场「麟至则地燃」（用户 2026-10-18 终裁；
            // 岩浆零文字、不做 ch4 版 L0）。
            var tip = Chapter2Tip(i) ?? Chapter3Tip(i) ?? Chapter4Tip(i);
            if (tip != null) tutorial?.ShowTip(tip, 7f);

            var rt = new WaveRuntime { layout = layout, groupsLeft = layout.groups.Count };
            waveRuntimes.Add(rt);

            // F5.2（ch5 傩面青脸）：召鬼**绑波次**（开波这一瞬判定，不按固定节拍——防无限刷墨）
            TryNuoMaskSummon();

            // F1.3（ADR-040 同路双向）：口=上/下 → 整波同口；口=双 → 组间交替（组 0 下、组 1 上循环，
            // 「这波从哪边来」的灯网考校，06 §11.6）。单向章 GetPort 回退全程，此分派零效果（ch1/ch2 铁律）。
            bool alternate = layout.spawnPort == SpawnPort.Both;
            int fixedPort = layout.spawnPort == SpawnPort.Upper ? 1 : 0;
            int groupIndex = 0;
            foreach (var g in layout.groups)
            {
                if (g.count <= 0) { rt.groupsLeft--; continue; }
                int port = alternate ? (groupIndex % 2) : fixedPort;
                spawnRoutines.Add(StartCoroutine(SpawnGroupRoutine(rt, g, port)));
                groupIndex++;
            }
            if (rt.groupsLeft <= 0) TryWaveCleared(rt); // 空波兜底
        }

        private IEnumerator SpawnGroupRoutine(WaveRuntime rt, SpawnGroup g, int port)
        {
            for (int k = 0; k < g.count; k++)
            {
                // F2.1（ch5 双面）：组级显式面优先（-1 = 按关配比自动）；ch1-4 voidRatio=0 → 恒实面
                int face = g.face >= 0 ? (g.face == 1 ? 1 : 0) : AutoFaceFor(k);
                SpawnFromTable(g.charId, rt, port, face);
                if (k < g.count - 1) yield return new WaitForSeconds((float)g.interval);
            }
            rt.groupsLeft--;
            TryWaveCleared(rt);
        }

        /// <summary>
        /// F2.1（ch5 双面）：组内第 k 只敌的面——按关配比 voidRatio **均匀散布**（第 k 只落在
        /// 累计区间 [k·p, (k+1)·p) 是否跨 100 的整数格上）。确定性：R 回滚重放同序号同面。
        /// </summary>
        private int AutoFaceFor(int index)
        {
            if (level == null || level.voidRatio <= 0f) return 0;
            int p = Mathf.Clamp(Mathf.RoundToInt(level.voidRatio * 100f), 0, 100);
            return ((index + 1) * p) / 100 > (index * p) / 100 ? 1 : 0;
        }

        // ============ F2.1 喷射引擎（ch4 火山，docs/11 序5 / 06 §12.2 / ADR-047 喷射幅） ============

        /// <summary>
        /// 波开始排喷：N = eruptionMin..Max 随机（「数量与落点均不可预报」——min=0 时本波可
        /// 出 0 颗）；落点 = 池内均匀**无放回**采样 N 格（本波不重复）。N 与落点分两个独立
        /// 种子流（重放键 = levelSeed ⊕ 流号 ⊕ 波号——06/任务单 F2.1 定案的独立流口径），
        /// R 回滚重进同波同序列（防回滚刷好运）。排喷日志即验收口径：回滚前后 Console
        /// 逐波比对落点一致（任务单 F2.1 验收行）。
        /// </summary>
        private void ScheduleEruption(int waveIndex)
        {
            if (level == null || eruptionPool == null || eruptionPool.Count == 0) return;
            int min = level.eruptionMin, max = level.eruptionMax;
            if (max <= 0) return; // 关数据禁喷（章开关已拦在 volcanoActive）

            var countRng = new System.Random(EruptionSeqSeed(1, waveIndex));
            int n = countRng.Next(Mathf.Max(0, min), max + 1); // min=0 → 本波可能 0 颗（数量随机）
            if (n <= 0) { Debug.Log("[F2.1] W" + (waveIndex + 1) + " 喷射：0 颗（数量随机）"); return; }
            n = Mathf.Min(n, eruptionPool.Count);

            var pickRng = new System.Random(EruptionSeqSeed(2, waveIndex));
            var remaining = new List<Vector2Int>(eruptionPool);
            var targets = new List<Vector2Int>(n);
            for (int k = 0; k < n; k++)
            {
                int idx = pickRng.Next(remaining.Count);
                targets.Add(remaining[idx]);
                remaining[idx] = remaining[remaining.Count - 1];
                remaining.RemoveAt(remaining.Count - 1); // 无放回：本波 N 格不重复
            }

            float delay = gameParams != null ? gameParams.eruptionDelaySeconds : 4f;
            float flash = gameParams != null ? gameParams.eruptionFlashSeconds : 5f;
            eruptionBatches.Add(new EruptionBatch
            {
                waveIndex = waveIndex,
                targets = targets,
                warnClock = waveClock + delay,          // 波开始（launch 帧波钟）+ 延迟
                landClock = waveClock + delay + flash,  // 红闪时长 = 撤离窗口（F2.2）
            });
            Debug.Log("[F2.1] W" + (waveIndex + 1) + " 喷排 " + n + " 格 → " + FormatCells(targets));
        }

        /// <summary>喷排/落地轮询（波钟刻度——与 TryLaunchDueWaves 同闸：暂停/胜负同冻结）。</summary>
        private void UpdateEruptions()
        {
            if (eruptionBatches.Count == 0) return;
            float flash = gameParams != null ? gameParams.eruptionFlashSeconds : 5f;
            float scar = gameParams != null ? gameParams.lavaMarkSeconds : 10f;
            for (int i = eruptionBatches.Count - 1; i >= 0; i--)
            {
                var b = eruptionBatches[i];
                if (!b.warned && waveClock >= b.warnClock)
                {
                    b.warned = true;
                    foreach (var t in b.targets)
                        b.markers.Add(EruptionMarker.Create(grid != null ? grid.transform : transform,
                            whiteSprite, t, flash, scar));
                }
                if (waveClock >= b.landClock)
                {
                    foreach (var m in b.markers)
                        if (m != null) m.Land();
                    SettleEruption(b); // F2.3：三类落点结算（06 §12.2 落点三分表）
                    sfx?.PlayLavaLand(); // 用户 2026-10-12 点题：落地音（红闪结束瞬间；一批一声防同帧叠放爆音）
                    // 落地结算（塔防格灰烬+塔毁 / 路径格 burst+红印挂敌 / 空地纯视觉）归 F2.3/F2.4
                    Debug.Log("[F2.1] W" + (b.waveIndex + 1) + " 岩浆落地 " + b.targets.Count + " 格 → " + FormatCells(b.targets));
                    eruptionBatches.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// AB 收窄随机池（用户 2026-10-12 终裁，推翻初稿「全图格子」）：章图塔防/垦格 ∪ 路径
        /// 折线 ±1 带宽（Chebyshev ≤1，含路面格本体——「路径格」三分落点由此可达）。带宽内
        /// 无格空地位照收（= 06 三分「空地」类——纯红印视觉）。水格排除（ch4 无水，防御式）。
        /// 折线采样与 GridSystem.CollectRouteCells/PathSystem 路面砖同口径（轴对齐段）。
        /// </summary>
        private void BuildEruptionPool()
        {
            eruptionPool = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            if (grid != null)
                foreach (var c in grid.All)
                {
                    if (c.IsWater) continue;
                    var p = new Vector2Int(c.X, c.Y);
                    if (seen.Add(p)) eruptionPool.Add(p); // 塔防格（Empty/Cultivating）+ 垦格（Fallow）
                }
            var route = activeChapter != null ? activeChapter.landRoute : null;
            eruptionRoadCells = new HashSet<Vector2Int>();
            if (route != null && route.Count >= 2)
            {
                var band = new HashSet<Vector2Int>();
                for (int i = 1; i < route.Count; i++)
                {
                    var a = route[i - 1];
                    var b = route[i];
                    float len = Vector2.Distance(a, b);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
                    for (int s = 0; s <= steps; s++)
                    {
                        float k = (float)s / steps;
                        var rc = new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, k)),
                                                Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, k)));
                        eruptionRoadCells.Add(rc); // F2.3：路面格本体集（落点三分判定——无格+在路=路径格）
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                                band.Add(new Vector2Int(rc.x + dx, rc.y + dy));
                    }
                }
                foreach (var p in band)
                    if (seen.Add(p)) eruptionPool.Add(p);
            }
            Debug.Log("[F2.1] 喷射随机池（AB 收窄口径）：" + eruptionPool.Count + " 格");
        }

        /// <summary>喷射序列分片种子：levelSeed ⊕ 流号 ⊕ 波号（独立流——重放同波同序列）。</summary>
        private int EruptionSeqSeed(int stream, int waveIndex)
        {
            unchecked
            {
                int h = eruptionSeed;
                h = h * 397 ^ stream * 1000003;
                h = h * 397 ^ (waveIndex + 1) * 65599;
                return h & int.MaxValue; // 非负（System.Random 接受任意 int，习惯上保持正）
            }
        }

        // ============ F2.3 三类落点结算（ch4 火山，docs/11 序7 / 06 §12.2 落点三分表） ============

        /// <summary>
        /// 落地结算（红闪结束瞬间，UpdateEruptions 调）：①塔防格 → SetAsh（无塔也变灰 =
        /// 灾害即红利：白送火系地皮）+ 塔按免疫（普通塔毁·零回收；火/炮/烬幸存）——**拿起 ≠
        /// 撤离**（用户终裁 2026-10-13）：塔拿在手里悬着、origin 格被烧 = 照样毁——撤离 =
        /// 放到安全格（MoveTowerToCell 完成）或卖出，骑墙 = 与不动同毁（逼窗口内完整决策）；
        /// 免疫按塔字不按位置（火/炮/烬拿在手里 origin 烧灰也幸存）②路径格 → 本格敌 burst
        /// （lavaBurstDamage，四舍五入位匹配）+ 红印 DoT 区入册（到期波钟；tick 消费归
        /// F2.4）③空地 → 无事（红印纯视觉 = F2.1 marker 疤痕）。
        /// 垦中格被烧 = 埋灰暂停（GridCell.Update 门禁，F3.1 已立）。R 回滚/换关复位：格
        /// 重建清灰烬、快照塔回场、被毁塔不返还（撤离窗口是唯一软垫——F3.4 口径）。
        /// 逆序遍历敌防分裂Mutation（蛛系死亡当场产卵追加表尾——正序遍历会漏访新增）。
        /// </summary>
        private void SettleEruption(EruptionBatch b)
        {
            double burst = gameParams != null ? gameParams.lavaBurstDamage : 15;
            float markSec = gameParams != null ? gameParams.lavaMarkSeconds : 10f;
            foreach (var t in b.targets)
            {
                var cell = grid != null ? grid.Get(t.x, t.y) : null;
                if (cell != null)
                {
                    cell.SetAsh();
                    if (pickedTower != null && pickedOrigin == cell &&
                        !LavaImmuneTowers.Contains(pickedTower.charId))
                        DestroyPickedTowerByLava(); // 用户终裁：拿起悬空 = 骑墙 = 同毁
                    else if (cell.Tower != null && !LavaImmuneTowers.Contains(cell.Tower.charId))
                        DestroyTowerByLava(cell, cell.Tower);
                }
                else if (eruptionRoadCells != null && eruptionRoadCells.Contains(t))
                {
                    int hit = 0;
                    for (int j = model.Enemies.Count - 1; j >= 0; j--)
                    {
                        var e = model.Enemies[j];
                        if (e.dead || e.reached || e.burnImmune) continue; // F4.1/F5.1：敌侧岩浆免疫位（火蜥/岩龟/麟——现全 false 零拦截）
                        var p = model.GetEnemyPosition(e);
                        if (Mathf.RoundToInt(p.x) == t.x && Mathf.RoundToInt(p.y) == t.y)
                        {
                            model.DamageLava(e, burst); // 受击反馈（闪白+飘字）经 EnemyDamaged 免费获得
                            hit++;
                        }
                    }
                    lavaMarks[t] = waveClock + markSec; // 红印 DoT 区入册（F2.4 tick 消费）
                    if (hit > 0) Debug.Log("[F2.3] 路径格 (" + t.x + "," + t.y + ") burst " + burst + " × " + hit + " 敌");
                }
                // else 空地：无事（红印视觉 = marker 疤痕淡出）
            }
        }

        /// <summary>
        /// 岩浆毁塔（零回收——06 §12.2「与格同毁零回收」）：OnLampDevoured 同构（清格/释放
        /// 视图/重算高亮）+ model.RemoveTower（枭吞灯是模型侧先删、表现层跟——这里 GM 主导
        /// 结算，模型侧一并在此删）。免疫名单外全毁（经济塔不豁免 06 字面；火/炮/烬幸存——F4
        /// 落卡前全字面命中 = 卡未实装即全毁，正确）。
        /// </summary>
        private void DestroyTowerByLava(GridCell cell, TowerState t) => DestroyTowerCore(cell, t, "焚毁");

        /// <summary>F5.2（ch5 傩面赤脸）：毁塔不毁格——格留空、可原地重建（与岩浆同管线，只换飘字）。</summary>
        private void DestroyTowerByNuo(GridCell cell, TowerState t) => DestroyTowerCore(cell, t, "毁");

        /// <summary>毁塔公共管线：清格 + 释放视图 + 删模型 + 断镜链 + 飘字 + 刷新（岩浆/赤脸共用）。</summary>
        private void DestroyTowerCore(GridCell cell, TowerState t, string label)
        {
            cell.Tower = null;
            cell.RefreshVisuals();
            if (towerViewMap.TryGetValue(t, out var view))
            {
                towerViewMap.Remove(t);
                towerViews.Release(view);
            }
            model.RemoveTower(t);
            // F3.1（ch5 镜）：本体被焚 → 对面镜像同灭；影被焚 → 只断链接（本体在自己那面照常结算）
            if (t.isMirror)
            {
                if (t.mirrorSource != null) t.mirrorSource.mirror = null;
            }
            else DetachMirror(t);
            SpawnFloat(label, (Vector3)t.position + Vector3.up * 0.5f, Vermilion, 9f); // 事件反馈（卖出/吞噬同家族——非教学文字）
            RecomputeHighlights();
            RefreshHud();
        }

        /// <summary>
        /// 岩浆毁**拿起中**的塔（用户终裁 2026-10-13：拿起 ≠ 撤离——骑墙同毁）：
        /// DiscardPickedTower(true) 同构（清 origin 虚影/收射程圈/释放视图/删模型）减去
        /// 卖出加墨；飘字钉在 origin 格上空（拿起中 position 未改 = 仍是原格坐标）。
        /// 免疫照名单按塔字（火/炮/烬拿在手里也幸存——与在哪无关）。
        /// </summary>
        private void DestroyPickedTowerByLava()
        {
            var t = pickedTower;
            Vector2 at = t.position;
            DiscardPickedTower(true); // 卖出管线全套（TrySell 同构，墨 0）
            SpawnFloat("焚毁", (Vector3)at + Vector3.up * 0.5f, Vermilion, 9f);
            RecomputeHighlights();
            RefreshHud();
        }

        /// <summary>
        /// F2.4 红印 DoT tick（06 §12.2 路径格红印 = 10s 燃烧区）：**整秒跳**——敌在红印格上
        /// 每满 1s 吃一轮 lavaMarkDps（飘字/闪白节拍与塔攻速同频——每帧 dps×dt 会产生
        /// -0.0x 小数飘字刷屏）。到期条目每帧除名（与 marker 疤痕同寿命 = lavaMarkSeconds
        /// 同源同长）；空地红印无入册 = 天然零路径（06 字面纯视觉）。敌侧岩浆/燃烧免疫
        ///（火蜥/岩龟/麟——burnImmune 同拦，F5.1 打标即时生效）+ 火蜥红印加速 ×1.3（F5.1：
        /// 格集合同步给 model，提速判定走 BattleModel 位移链）。
        /// i-frame（Boss 转阶段）DamageLava 内拦；逆序遍历敌防分裂 mutation 追加漏访
        ///（SettleEruption 同律）。跳钟册空即复位（首跳恒在入册满 1s 后落——相位干净）。
        /// </summary>
        private void UpdateLavaMarks(float dt)
        {
            // F5.1：红印格集合同步给 model（火蜥提速判定——每帧推、同帧同值；空册也推 = 清空，
            // 防陈旧提速）。册小（≤ 一波喷 + 10s 寿命），遍历代价可忽略。
            model.SyncLavaMarks(lavaMarks.Keys);

            if (lavaMarks.Count == 0)
            {
                lavaDotClock = 0f;
                return;
            }

            // 到期除名（每帧检查——红印寿命与 marker 疤痕同源）
            List<Vector2Int> expired = null;
            foreach (var kv in lavaMarks)
                if (waveClock >= kv.Value)
                {
                    if (expired == null) expired = new List<Vector2Int>();
                    expired.Add(kv.Key);
                }
            if (expired != null)
                for (int i = 0; i < expired.Count; i++) lavaMarks.Remove(expired[i]);
            if (lavaMarks.Count == 0) return;

            lavaDotClock += dt;
            if (lavaDotClock < 1f) return;
            lavaDotClock -= 1f;

            // 整秒跳：站上/路过红印格的敌人各吃一轮 dps（受击反馈闪白+飘字经 EnemyDamaged 免费获得）
            double dps = gameParams != null ? gameParams.lavaMarkDps : 2;
            for (int j = model.Enemies.Count - 1; j >= 0; j--)
            {
                var e = model.Enemies[j];
                if (e.dead || e.reached || e.burnImmune) continue; // F4.1/F5.1 钩子位：敌侧岩浆免疫（火蜥红印加速 ×1.3 同期 F5.1）
                var p = model.GetEnemyPosition(e);
                var c = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                if (lavaMarks.ContainsKey(c)) model.DamageLava(e, dps);
            }
        }

        /// <summary>
        /// F6.1/F6.2 单格岩浆结算（践踏 / 共振雨共用）：格变灰烬 + 塔按免疫名单——普通塔毁·零回收
        ///（「焚毁」飘字）、火/炮/烬幸存（脚下灰烬 = 死灰复燃燃烧 ×2）、**拿起中骑墙同毁**（78f1224 口径）。
        /// 调用侧负责「已灰且无塔 = 无事」短路（幂等——防每帧重刷）；SettleEruption（F2.3 三分结算）
        /// 不走本口（须另判路径/空地二类），零改动零风险。
        /// </summary>
        private void SettleBuildCell(GridCell cell)
        {
            bool holding = pickedTower != null && pickedOrigin == cell;
            if (cell.TileState == TileState.Ash && cell.Tower == null && !holding)
                return; // 已灰且无塔 = 无事（幂等短路）
            cell.SetAsh(); // 与岩浆落地同语义：格变灰烬（green -> ash = 盆同毁；塔另按免疫名单）
            if (holding && !LavaImmuneTowers.Contains(pickedTower.charId))
                DestroyPickedTowerByLava();
            else if (cell.Tower != null && !LavaImmuneTowers.Contains(cell.Tower.charId))
                DestroyTowerByLava(cell, cell.Tower);
        }

        /// <summary>
        /// F6.1 麟践踏光环（06 §12.5，用户定案）：**距麟中心 ≤ trampleRadius 格的建造格持续变灰烬**
        /// ——塔按免疫规则（火/炮/烬幸存 = 死灰复燃获增益；普通塔毁·零回收；拿起中骑墙同毁——78f1224 口径），
        /// **路径格不毁**（路径无格 = 天然豁免，与 F2.3 三分判定同源）；**提示圈随行常显**
        ///（射程圈渲染复用 = 预告义务在 Boss 层保留）。
        /// 数据驱动：trampleRadius > 0 的敌才有光环（ch1-3 全敌 0 → 零路径铁律）；多光环取血最厚者
        ///（麟唯一，防御性）。同闸调用（暂停/胜负冻结）。
        /// F7.5③（用户定案 2026-10-19）：每走 chargeEveryCells 格原地**蓄力** chargeSeconds 秒
        /// ——提示圈放大 + 换高亮色 → 归零重踏（chargeRadius）→ 回常规半径续走。**蓄力期不无敌**
        ///（Damage 照常——这就是补出来的输出窗；「免疫控制可以，免疫输出是反模式」纪律）。
        /// </summary>
        private void UpdateTrample()
        {
            if (trampleIndicator == null) return;

            EnemyState boss = null;
            for (int i = 0; i < model.Enemies.Count; i++)
            {
                var e = model.Enemies[i];
                if (e.dead || e.reached || e.trampleRadius <= 0) continue;
                if (boss == null || e.maxHp > boss.maxHp) boss = e;
            }
            if (boss == null || grid == null)
            {
                trampleTrackedBoss = null; // F7.5③：收圈即摘跟踪（下只麟重新挂里程）
                trampleIndicator.Hide(); // 无光环敌（进关/败/胜/麟已死）= 收圈
                return;
            }
            if (trampleTrackedBoss != boss) // 换 Boss（进关/R/新麟）：里程重挂，防跨只差值污染
            {
                trampleTrackedBoss = boss;
                lastTrampleDist = boss.dist;
            }

            // F7.5③ 蓄力状态机（数据驱动：chargeEveryCells > 0 才有路径——ch1-3 全敌 0 零路径）。
            // i-frame 演出期不推进（holdMove 由演出持有——避免误解除站桩；演出收尾由二圈重置清零蓄力）。
            bool charging = false;
            if (boss.chargeEveryCells > 0 && boss.chargeRadius > 0 && !boss.invulnerable)
            {
                double walked = boss.dist - lastTrampleDist;
                if (walked > 0) boss.chargeWalked += walked; // 回环重置（dist 归零）负差不计
                lastTrampleDist = boss.dist;

                if (boss.chargeRemaining > 0)
                {
                    charging = true;
                    boss.chargeRemaining -= Time.deltaTime; // 同闸调用（暂停/胜负冻结 → 不进本口）
                    if (boss.chargeRemaining <= 0)
                    {
                        boss.chargeRemaining = 0;
                        var stompPos = model.GetEnemyPosition(boss);
                        SettleTrampleAt(stompPos, (float)boss.chargeRadius); // 放大半径重踏一次
                        boss.holdMove = false; // 站定结束，续走（此处定身必为蓄力持有——i-frame 已排除）
                        SpawnFloat("重踏！", (Vector3)stompPos + Vector3.up * 1f, Vermilion, 11f);
                        Debug.Log("[F7.5] 麟蓄力：重踏 " + boss.chargeRadius.ToString("0.##") + " 格");
                    }
                }
                else if (boss.chargeWalked >= boss.chargeEveryCells)
                {
                    boss.chargeWalked = 0;
                    boss.chargeRemaining = boss.chargeSeconds;
                    boss.holdMove = true; // 站定（复用 F5.2 定身闸：只停位移，Damage/燃烧照常）
                    charging = true;
                    Debug.Log("[F7.5] 麟蓄力：走满 " + boss.chargeEveryCells.ToString("0.##") +
                        " 格站定 " + boss.chargeSeconds.ToString("0.##") + "s（圈扩 " +
                        boss.chargeRadius.ToString("0.##") + " 格高亮）");
                }
            }
            else
            {
                lastTrampleDist = boss.dist; // 不推进里程（i-frame 演出期/无蓄力配置）
            }

            // 样式每帧跟 GameParams（Play 中调颜色/透明度即时生效——任务单「外露」口径）；
            // F7.5③：蓄力高亮色（圈放大 + 换色 = 「要重踏了」的可读预告）
            if (gameParams != null)
                trampleIndicator.SetStyle(gameParams.trampleCircleColor,
                    gameParams.trampleDiscAlpha, gameParams.trampleRimAlpha,
                    gameParams.trampleChargeColor);

            var p = model.GetEnemyPosition(boss);
            float showRadius = charging ? (float)boss.chargeRadius : (float)boss.trampleRadius;
            trampleIndicator.Show(showRadius, new Vector3(p.x, p.y, 0f), charging); // 常显随行（二阶段放大自动跟）

            SettleTrampleAt(p, (float)boss.trampleRadius); // 常驻光环（每帧幂等；蓄力站定脚下照犁）
        }

        /// <summary>
        /// F6.1/F7.5③ 践踏结算（单一口）：以 p 为心、radius 为径，把圆内**建造格**逐一结算
        /// （SettleBuildCell——已灰且无塔幂等短路）；路径/图外无格 = 天然豁免（「路径格不毁」）。
        /// 两种时机共用：常驻光环（每帧，半径 = trampleRadius）与蓄力重踏（一次性，半径 = chargeRadius）。
        /// </summary>
        private void SettleTrampleAt(Vector2 p, float radius)
        {
            if (grid == null) return;
            int cx = Mathf.RoundToInt(p.x);
            int cy = Mathf.RoundToInt(p.y);
            int r = Mathf.CeilToInt(radius);
            float rr = radius;
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    var cell = grid.Get(cx + dx, cy + dy);
                    if (cell == null) continue; // 路径/图外：无格 = 天然豁免（06 §12.5「路径格不毁」）
                    if (Vector2.Distance(p, new Vector2(cx + dx, cy + dy)) > rr + 0.01f) continue; // 圆内（与提示圈同口径）
                    SettleBuildCell(cell); // 单格岩浆结算（与 F6.2 共振雨共用一口）
                }
            }
        }

        private static string FormatCells(List<Vector2Int> cells)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append('(').Append(cells[i].x).Append(',').Append(cells[i].y).Append(')');
            }
            return sb.ToString();
        }

        private void SpawnFromTable(string charId, WaveRuntime rt, int port = 0, int face = 0)
        {
            var s = enemyTable.Find(charId);
            if (s == null)
            {
                Debug.LogError("[GameManager] 敌人表里没有字：" + charId);
                return;
            }
            var e = model.SpawnEnemy(s, port: port, face: face);
            rt.pending++;
            enemyWave[e] = rt;
        }

        /// <summary>提前召唤下一波（空间/按钮）：整条时间线前移到当前时刻。</summary>
        private void EarlySummonNextWave()
        {
            if (model == null || model.Failed || won || level == null) return;
            if (nextWaveIndex >= level.waves.Count)
            {
                if (handBar != null)
                    SpawnFloat("已是最后一波", handBar.NextButtonWorldPos + Vector3.up * 0.6f, GrayInk);
                return;
            }
            double due = EffectiveStart(nextWaveIndex);
            if (waveClock < due) timelineShift += due - waveClock;
            TryLaunchDueWaves();
            tutorial?.Notify(TutorialEvent.Wave); // L0 教学：开战（教学收尾步，ADR-030）
        }

        /// <summary>
        /// 图鉴（用户 2026-10-28）：敌人「已遭遇」记录——首次上场即入档（每类只写一次）。
        /// 旧档缺 seenEnemies 行 = 空表，零迁移；新敌写档一次（低频，不刷盘）。
        /// </summary>
        private void RecordCodexEnemy(string charId)
        {
            if (save == null || string.IsNullOrEmpty(charId)) return;
            if (save.seenEnemies == null) save.seenEnemies = new List<string>();
            if (save.seenEnemies.Contains(charId)) return;
            save.seenEnemies.Add(charId);
            SaveSystem.Write(save);
        }

        private void OnEnemyResolved(EnemyState e)
        {
            if (enemyWave.TryGetValue(e, out var rt))
            {
                enemyWave.Remove(e);
                rt.pending--;
                TryWaveCleared(rt);
            }
        }

        private void TryWaveCleared(WaveRuntime rt)
        {
            if (rt.bonusGiven || rt.groupsLeft > 0 || rt.pending > 0) return;
            rt.bonusGiven = true;
            model.Ink += gameParams.waveClearBonus; // 每波清空奖励（GDD §4）
            SpawnFloat("波清 +" + gameParams.waveClearBonus + "墨", new Vector3(0f, 4.4f, 0f), BlueInk);
            RefreshHud();
        }

        private void CheckWon()
        {
            if (won || model.Failed || level == null) return;
            if (nextWaveIndex < level.waves.Count) return;
            foreach (var rt in waveRuntimes)
                if (rt.groupsLeft > 0 || rt.pending > 0) return;
            // F6（ch5 傩神）：Boss 不占波次名额 → 波次清空 ≠ 通关：傩面/傩神未死（或黑幕过场中）不算赢
            if (nuoShenArriving) return;
            foreach (var e in model.Enemies)
                if (!e.dead && !e.reached && (e.nuoMask || e.nuoShen)) return;

            won = true;
            sfx?.PlayVictory(); // 胜利音（ADR-083）
            LogTuningReport("通关"); // F7.1 探针：场末体检（胜利帧）
            // 手感修正（用户 2026-10-09 六轮）：此处原清拿起/选中态——但 ClearPickupState
            // 只清引用不清虚影/浮起，胜利时拿着的塔变「幽灵」：视觉残留、点原格静默无反应
            //（无源+Tower==null 双跳）。改**不清**——结束后摆弄口径一致（拿着塔继续放/挪；
            // 点「下一关」时 DoEnterLevel 场重建+快照（tower.position 未动）塔回原格，无残留。
            // 失败链（model.Failed）本就不清，两态行为一致。
            if (hud != null)
                hud.SetWaveText("通关");

            // 通关横幅文案（结算画面关闭后才亮；uGUI 永远盖在世界 UI 上，不能与结算同屏）
            string bannerMsg;
            if (levelIndex < levels.Length - 1)
            {
                // F4.3（ADR-031）：章界过关（下一关换章）文案区分——清场推进 vs 章内带塔推进
                bannerMsg = ChapterOf(levels[levelIndex + 1]) != activeChapter
                    ? "章节通关！下一关将进入新地图"
                    : "通关！按 N 带塔进下一关 · R 重玩本关";
            }
            else
                bannerMsg = "全卷通关！感谢游玩！";

            int resultMode = levelIndex < levels.Length - 1 ? 0 : 1;

            // P5（用户 2026-10-25）：首次通关 + 本关有奖励卡 → 先出结算画面；
            // 无奖励 / 重打旧关 → 跳过结算，直接横幅 + 结果按钮。
            bool firstClear = true;
            if (save != null)
            {
                var cleared = hardMode ? save.hardClearedLevels : save.easyClearedLevels;
                firstClear = cleared == null || !cleared.Contains(levelIndex);
            }
            var rewardCards = level != null ? level.rewardCards : null;
            // P5（用户 2026-10-26）：胜利帧不再弹结算——只记下「首次通关 + 有奖励」，
            // 等玩家点「下一关」后再逐张展示（每卡一屏）。
            if (clearPanel != null && firstClear && rewardCards != null && rewardCards.Count > 0)
            {
                pendingRewardCards = new List<string>(rewardCards);
                pendingRewardLevel = levelIndex;
            }
            hud?.ShowBanner(bannerMsg);
            ShowResultButtons(resultMode); // 0.2：过关双钮 / 章末单钮
            // P4（用户 2026-10-24/25）：通关标记（选关右上角 ⭐）——按模式记录已通关的关索引
            if (save != null)
            {
                var list = hardMode ? save.hardClearedLevels : save.easyClearedLevels;
                if (list == null) list = new List<int>();
                if (!list.Contains(levelIndex)) list.Add(levelIndex);
                if (hardMode) save.hardClearedLevels = list; else save.easyClearedLevels = list;
            }
            WriteSave(levelIndex); // 通关即解锁下一关（线性解锁，06 §1）并落盘
        }

        // ============ 手牌：抽卡 / 选中 / 卖出 ============

        /// <summary>抽卡按钮 / D 键（v3.0：一口价 = 池总价值，补满手牌）。</summary>
        public void TryDraw()
        {
            // 手感口径反转（用户 2026-10-10 三轮定案，推翻 10-09 二轮收拦）：结束后全操作
            // 可用**含买/卖**（刷牌/卖卡回墨照常），只停生产（Tick 停 = 墨塔停产 + 战斗冻结）。
            if (model == null || hand == null) return;
            Vector3 at = handBar != null ? handBar.DrawButtonWorldPos + Vector3.up * 0.6f : Vector3.zero;

            // 手感优化（用户 2026-10-09）：抽卡 = **刷新**——弃全手（未用牌按卖价自动回墨）+
            // 均等重抽补满。抽卡价不变（池总价值一口价）；净成本 = DrawCost - Σ旧牌卖回。
            // 事务性：Ink + refund 不足整笔不动（先判后扣）。L0 教学兼容（手牌空时 refund=0
            // 即原「补满」语义，Draw 通知照发）。
            if (!hand.HasSource)
            {
                SfxPlayer.ClickInvalid(); // 无效操作音（用户 2026-10-26）
                SpawnFloat("本关卡池为空", at, Vermilion);
                return;
            }
            double refund = 0;
            for (int i = 0; i < hand.Cards.Count; i++)
            {
                var def = towerPool.Find(hand.Cards[i]);
                refund += MergeRules.CardSellValue(def != null ? def.value : 0, gameParams.sellRate);
            }
            if (model.Ink + refund < hand.DrawCost)
            {
                SfxPlayer.ClickInvalid(); SpawnFloat("墨不足", at, Vermilion);
                return;
            }
            // 事务性执行：旧牌回墨 → 扣抽卡费 → 弃全手 → 重抽补满（TryDraw 此时从空手
            // 抽满 HandSize，池空门已判必成）。
            model.Ink += refund;   // 旧牌卖回（折半口径与 TrySell 卡一致）
            model.Ink -= hand.DrawCost;
            hand.DiscardHand();
            hand.TryDraw(out _);
            sfx?.PlayDraw(); // 抽卡成功（ADR-083）
            SpawnFloat(refund > 0 ? "换牌 旧牌一键售卖" + (int)refund + "墨" : "换牌", at, BlueInk);
            selectedCardIndex = -1;
            RecomputeHighlights();
            RefreshHud();
            tutorial?.Notify(TutorialEvent.Draw); // L0 教学：抽卡完成（ADR-030）
        }

        /// <summary>
        /// 手牌卡/按钮点击。index：0..N-1 = 手牌；-1 抽；-2 卖；-3 下一波。
        /// 再点一次同卡 = 取消（真值表：手牌卡 → 手牌区 = 取消，无警示）。
        /// </summary>
        public void OnHandCardClicked(int index)
        {
            // 手感优化（2026-10-09）：结束后可选手牌（摆弄口径；Tick 停 = 放下不产不打的纯摆位）。
            if (model == null || hand == null) return;

            // F2.2 红铲（-4）：进/退造格模式（先于其他手牌动作判定）
            if (index == HandBar.ShovelClick) { ToggleRedShovel(); return; }
            // 其他手牌动作 = 状态清理：退出造格模式并退款（否则点选层会挡住放塔）
            if (redShovelMode) CancelRedShovel(true);

            if (index == -1) { TryDraw(); return; }
            if (index == -2) { TrySell(); return; }
            if (index == -3) { EarlySummonNextWave(); return; }

            if (pickedTower != null) return; // 真值表：塔源 → 手牌区 无效（塔不收回手牌）
            if (index >= hand.Cards.Count) return; // 空卡位

            selectedCardIndex = selectedCardIndex == index ? -1 : index;
            RecomputeHighlights();
            RefreshHud();
        }

        // ============ F1（docs/14，ADR-062）手牌拖拽放塔 ============

        /// <summary>字卡按下（HandCard.OnMouseDown 上报）：记起点；点选/拖拽由 Update 轮询分辨。</summary>
        public void OnHandCardPressed(int index)
        {
            pressedCardIndex = index;
            pressedScreenPos = Input.mousePosition;
            handDragging = false;
        }

        /// <summary>
        /// F1.1 起拖：超过阈值进入拖拽态（selectedCardIndex 置为拖拽卡、不立刻消费）；
        /// 塔虚影 + 射程圈跟手。道具卡（铲/盆）不拖、塔源在手不拖——返回 false（仍按点选兜底）。
        /// </summary>
        private bool StartHandCardDrag(int index)
        {
            if (model == null || hand == null) return false;
            if (index < 0 || index >= hand.Cards.Count) return false;
            string card = hand.Cards[index];
            if (card == HandModel.ShovelId || card == HandModel.PotId) return false; // 道具卡不拖
            if (pickedTower != null) return false; // 塔源在手 → 不抢拖拽
            if (redShovelMode) CancelRedShovel(true); // F1.3：拖字卡退出造格模式（与点选口径一致）

            handDragging = true;
            draggingCardIndex = index;
            selectedCardIndex = index; // 拖拽 = 选中态（合法格高亮/放置谓词复用）；不立刻消费

            var def = towerPool != null ? towerPool.Find(card) : null;
            dragRange = def != null ? (float)def.range : 3f;

            // 虚影：池取 TowerView，字 = 卡字，上浮 + 微放大（SetLifted 口径）
            dragGhost = towerViews.Get(towerTemplate);
            dragGhost.transform.SetParent(transform, false); // 挂 GameManager 根，不挂格
            dragGhost.Show(card, 1);
            dragGhost.SetLifted(true);

            if (rangeIndicator != null) rangeIndicator.Show(dragRange, Vector3.zero); // 先亮，下一行跟手
            UpdateHandCardDrag();
            RecomputeHighlights();
            RefreshHud(); // 手牌选中框/卖出钮可用态随拖拽选中刷新
            return true;
        }

        /// <summary>F1.2 拖拽中每帧跟手：虚影上偏移 1.5 格 + 射程圈换位。</summary>
        public void UpdateHandCardDrag()
        {
            if (!handDragging || dragGhost == null) return;
            Vector3 wp = PointerWorldPos();
            dragGhost.transform.position = new Vector3(wp.x, wp.y + DragGhostLiftY, -0.5f); // 上偏移 + 相机前
            if (rangeIndicator != null) rangeIndicator.Show(dragRange, wp);
        }

        /// <summary>
        /// F1.2 松手：射线到格 → 复用 OnCellClicked 全部分支（放塔/合并/铲/盆/文案）；
        /// 空白/手牌区 → 回弹清选（不消费）。虚影/射程圈在此统一收。
        /// </summary>
        private void EndHandCardDrag()
        {
            if (!handDragging) return;
            var cell = CellUnderPointer();
            if (cell != null) OnCellClicked(cell); // 全复用（PlaceTower 内 hand.ConsumeAt(selectedCardIndex)）
            else if (selectedCardIndex >= 0)
            {
                selectedCardIndex = -1;            // 空白：回弹 + 清选
                RecomputeHighlights();
            }

            if (dragGhost != null) { towerViews.Release(dragGhost); dragGhost = null; }
            if (rangeIndicator != null) rangeIndicator.Hide();
            handDragging = false;
            draggingCardIndex = -1;
            pressedCardIndex = -1;
            RefreshHud();
        }

        /// <summary>
        /// 指针下的格子：**命中点 = 指针 + 虚影上偏移**（用户 2026-10-25：拖动时塔/虚影视觉上移 0.5 格，
        /// 落点判定也跟着上移 0.5，松手不再「往下跳半格」）。
        /// 优先取整查 grid（普通格）；查不到再 collider 命中（视觉搬出地图的临时格）。
        /// </summary>
        private GridCell CellUnderPointer()
        {
            if (grid == null) return null;
            Vector3 wp = PointerWorldPos() + Vector3.up * DragGhostLiftY;
            var cell = grid.Get(Mathf.FloorToInt(wp.x + 0.5f), Mathf.FloorToInt(wp.y + 0.5f));
            if (cell != null) return cell;
            var hit = Physics2D.OverlapPoint(wp);
            return hit != null ? hit.GetComponent<GridCell>() : null;
        }

        /// <summary>指针世界坐标（正交相机 z 取 |cam.z| 拉到格平面 z=0）。</summary>
        private Vector3 PointerWorldPos()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            var mp = Input.mousePosition;
            return cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, -cam.transform.position.z));
        }

        // ============ F2（docs/14，ADR-063）拿起塔拖拽 ============

        /// <summary>F2.1 塔拖拽跟手：pickedView 世界坐标跟手指（上偏移 0.5 格）+ 射程圈跟随预览新位置。</summary>
        private void FollowTowerDrag()
        {
            if (pickedView == null) return;
            Vector3 wp = PointerWorldPos();
            pickedView.transform.position = new Vector3(wp.x, wp.y + DragGhostLiftY, -0.5f);
            if (rangeIndicator != null && pickedTower != null) rangeIndicator.Show(pickedTower.range, wp);
        }

        /// <summary>
        /// F2.1 塔拖拽松手：卖出区优先 → 卖；落到塔防格 → 移动/合并/取消（复用 OnCellClicked 的
        /// pickedTower 分支）；非法/空白 → 回弹原格浮起态（仍拿起中）。
        /// </summary>
        private void EndTowerDrag()
        {
            towerDragging = false;
            if (pickedTower == null) return;

            // ⓪ 小拖动（< 半格）= 点击塔：保持拿起（回弹原格浮起），不取消/不移动
            //   （用户 2026-10-23：拖一点点就松手不该判成「取消拿塔」）
            if (pickedOrigin != null)
            {
                float dragDist = Vector2.Distance((Vector2)PointerWorldPos(), (Vector2)pickedOrigin.transform.position);
                if (dragDist < 0.5f)
                {
                    if (pickedView != null) pickedView.SetLifted(true);
                    if (rangeIndicator != null) rangeIndicator.Show(pickedTower.range, pickedOrigin.transform.position);
                    return;
                }
            }

            // ① 卖出区优先
            if (handBar != null &&
                Vector2.Distance((Vector2)PointerWorldPos(), (Vector2)handBar.SellButtonWorldPos) <= SellDropRadius)
            {
                TrySell(); // 拿起塔分支（回墨 价值×50%×字位 + 飘字 + 释放视图）
                return;
            }

            // ② 拖到格 = 移动/合并/取消（OnCellClicked 的 pickedTower 分支全复用：
            //    原格=取消 / 有塔=合并 / 合法=移动 / 水格·灰烬·荒地=文案且塔留手里）
            var cell = CellUnderPointer();
            if (cell != null)
            {
                OnCellClicked(cell);
                if (pickedTower == null) return; // 移动/合并/取消已结算（ClearPickupState）
            }

            // ③ 非法/空白：回弹原格浮起态（仍拿起中，可再点格移动/再拖卖/点空白取消）
            if (pickedView != null) pickedView.SetLifted(true);
            if (rangeIndicator != null && pickedOrigin != null)
                rangeIndicator.Show(pickedTower.range, pickedOrigin.transform.position);
        }

        // ============ F3（docs/14，ADR-063）点空白 = 取消 ============

        /// <summary>F3.1：点空白 = 取消（右键等价，触屏兜底）——指针下无 GridCell/HandCard/WorldButton 才取消。</summary>
        private void HandleBlankClick()
        {
            if (confirmRoot != null && confirmRoot.activeSelf) return; // F4：回滚确认面板打开时不判空白
            if (PointerOverInteractive()) return;
            if (pickedTower != null) { SfxPlayer.ClickInvalid(); CancelPickup(); return; }
            if (selectedCardIndex >= 0)
            {
                SfxPlayer.ClickInvalid();
                selectedCardIndex = -1;
                RecomputeHighlights();
                RefreshHud();
            }
        }

        /// <summary>指针是否压在任何交互体上（格 / 手牌卡·按钮 / 世界按钮）。</summary>
        private bool PointerOverInteractive()
        {
            var hit = Physics2D.OverlapPoint(PointerWorldPos());
            if (hit == null) return false;
            return hit.GetComponent<GridCell>() != null
                || hit.GetComponent<HandCard>() != null
                || hit.GetComponent<WorldButton>() != null;
        }

        /// <summary>卖出区（按钮）：拿起中的塔卖 价值×50%×字位；选中手牌卡（含铲）卖 价值×50%。</summary>
        private void TrySell()
        {
            // 手感口径反转（用户 2026-10-10 三轮定案）：结束后买/卖照常（与刷牌同口径）——
            // 10-09 二轮曾收拦「纯摆弄」，本轮用户明确「包括买和卖」；生产照停（Tick 停）。
            if (model == null || handBar == null) return;
            Vector3 at = handBar.SellButtonWorldPos + Vector3.up * 0.6f;

            if (pickedTower != null)
            {
                int value = MergeRules.SellValue(pickedTower, gameParams.sellRate);
                DiscardPickedTower(true);
                model.Ink += value;
                sfx?.PlaySell(); // 卖出（ADR-083）
                SpawnFloat("卖出 +" + value + "墨", at, BlueInk);
                RecomputeHighlights();
                RefreshHud();
                tutorial?.Notify(TutorialEvent.Sell); // L0 教学：卖出完成（ADR-030）
                return;
            }

            if (selectedCardIndex >= 0 && selectedCardIndex < hand.Cards.Count)
            {
                string card = hand.Cards[selectedCardIndex];
                var def = towerPool.Find(card);
                int value = MergeRules.CardSellValue(def != null ? def.value : 0, gameParams.sellRate);
                hand.ConsumeAt(selectedCardIndex);
                selectedCardIndex = -1;
                model.Ink += value;
                sfx?.PlaySell(); // 卖出（ADR-083）
                SpawnFloat("卖出 +" + value + "墨", at, BlueInk);
                RecomputeHighlights();
                RefreshHud();
                tutorial?.Notify(TutorialEvent.Sell); // L0 教学：卖出完成（ADR-030）
                return;
            }

            SfxPlayer.ClickInvalid();
            SpawnFloat("先选塔防", at, GrayInk);
        }

        // ============ 格子交互（03 §4.3.1 交互真值表路由） ============

        /// <summary>
        /// 任意格子的点击入口。源 = 拿起中的塔 / 选中的手牌（字卡或铲）/ 无源。
        /// 放回自己原处 = 取消；开垦中格一律「进度环闪一下」。
        /// </summary>
        public void OnCellClicked(GridCell cell)
        {
            // 手感优化（2026-10-09）：结束后可继续摆弄（放/挪/合/虚影取消照常；Tick 停 =
            // 战斗与生产冻结——「可操作但经济塔停产」正解）。
            if (model == null || cell == null) return;

            // —— 源 = 拿起中的塔（移动 / 合并 / 取消）——
            if (pickedTower != null)
            {
                if (cell == pickedOrigin) { CancelPickup(); return; }
                if (cell.Tower != null) { PickedTowerOnTower(cell); return; }
                if (cell.State == CellState.Water && !pickedTower.waterImmune)
                {
                    // F1.1 水格挡明 → F3.2 放行水系（水/冰/渔特权，GDD §3.4「水中是特权不是限制」）
                    SfxPlayer.ClickInvalid();
                    SpawnFloat("水格仅水系塔可放置", CellTop(cell), GrayInk);
                    return;
                }
                // F1.2 灰烬挡明（ch4，06 §12.2）：普通塔不可入灰烬格——白名单火/炮/烬/盆
                //（F3 三卡进池前恒拒，谓词先立卡后到）。塔的毁/幸存是 F2.3 喷射结算事，这里只管放置。
                // F4.5（2026-10-20）：烬进白名单（文案同步「火/炮/烬」）。
                if (cell.TileState == TileState.Ash && !IsAshTower(pickedTower.charId))
                {
                    SfxPlayer.ClickInvalid();
                    SpawnFloat("灰烬格仅火/炮/烬可放", CellTop(cell), GrayInk);
                    return;
                }
                if (cell.State == CellState.Empty ||
                    (cell.State == CellState.Water && pickedTower.waterImmune)) { MoveTowerToCell(cell); return; } // F3.2：水系可入水格（F4.3b：绿框不再单列放行条——绿框只出现在塔防格，Empty 已覆盖）
                if (cell.IsCultivating) { cell.Flash(); return; }
                SfxPlayer.ClickInvalid();
                SpawnFloat("荒地须先开垦", CellTop(cell), GrayInk);
                return;
            }

            // —— 源 = 选中的手牌 ——
            if (selectedCardIndex >= 0 && selectedCardIndex < hand.Cards.Count)
            {
                string card = hand.Cards[selectedCardIndex];

                if (card == HandModel.ShovelId)
                {
                    // F1.2 灰烬格禁垦（ch4，程序定案黄底 docs/11 §0：烧毁地无开垦价值——盆是唯一除灰
                    // 路径：塔防格复绿可建 / 荒地回可垦（F4.3b））
                    if (cell.TileState == TileState.Ash)
                    {
                        SfxPlayer.ClickInvalid();
                        SpawnFloat("灰烬：需盆复绿", CellTop(cell), GrayInk);
                        return;
                    }
                    if (cell.State == CellState.Fallow)
                    {
                        hand.ConsumeAt(selectedCardIndex);
                        selectedCardIndex = -1;
                        cell.StartCultivating(gameParams.farmSeconds); // 铲消耗 → 15s 计时
                        sfx?.FarmStart(); // 挖土循环起（ADR-029；多块同挖引用计数）
                        SpawnFloat("开垦中…", CellTop(cell), BlueInk);
                        RecomputeHighlights();
                        RefreshHud();
                    }
                    else if (cell.IsCultivating) cell.Flash();
                    else { SfxPlayer.ClickInvalid(); SpawnFloat("铲只能开垦荒地", CellTop(cell), Vermilion); } // P0 文案⑥
                    return;
                }

                if (card == HandModel.PotId)
                {
                    // F4.3 盆（用户 2026-10-13 终裁：**同铲口径**——放下即消耗、不可移动/不可卖；
                    // 设计档「移动/卖照常通用」作废）。道具卡·复绿：仅灰烬格可放。
                    // F4.3b（用户 2026-10-18 报障终裁）：**绿框只授给塔防格**——烧前 CellState.Empty
                    // 的格回绿框（任意塔含墨可建，与旧口径一致）；烧前是荒地/垦中的格 → ClearAsh
                    // 除灰回原象，**开垦要求照旧**（盆只除灰、不代开垦——垦中格进度自动续走，
                    // 06 §12.2/F3.3 门禁）。旧版「绿框 = 通用可建」是无条件放行条 → 会把 CellState
                    // 检查整个绕过（未开垦荒地复绿后能直接建塔——本次报障即此缝）。
                    // 绿格再被岩浆烧 = SetAsh 回灰（盆+其上之塔同毁；塔按免疫规则——F2.3 既有）。
                    if (cell.TileState != TileState.Ash)
                    {
                        SfxPlayer.ClickInvalid();
                        SpawnFloat("盆只能复灰烬格", CellTop(cell), Vermilion);
                        return;
                    }
                    hand.ConsumeAt(selectedCardIndex);
                    selectedCardIndex = -1;
                    if (cell.State == CellState.Empty)
                    {
                        cell.SetGreen();
                        SpawnFloat("盆·复绿", CellTop(cell), BlueInk);
                    }
                    else
                    {
                        cell.ClearAsh();
                    }
                    RecomputeHighlights();
                    RefreshHud();
                    return;
                }

                if (cell.Tower != null) { CardOnTower(card, cell); return; }
                // F1.2 灰烬挡明（手牌放置路径同拦——pickedTower 分支同款；白名单 F3 卡后放行）
                if (cell.TileState == TileState.Ash && !IsAshTower(card))
                {
                    SpawnFloat("灰烬格仅火/炮/烬可放", CellTop(cell), GrayInk);
                    return;
                }
                if (cell.State == CellState.Empty ||
                    (cell.State == CellState.Water && IsWaterTower(card))) { PlaceTower(card, cell); return; } // F3.2：水系可入水格（F4.3b：绿框不再单列放行条——绿框只出现在塔防格，Empty 已覆盖）
                if (cell.IsCultivating) { cell.Flash(); return; }
                return; // 荒地：真值表「无效」（无文案）
            }

            // —— 无源：拿起塔 / 加速开垦 ——
            if (cell.Tower != null) { PickupTower(cell); return; }
            if (cell.IsCultivating) { TrySpeedup(cell); return; }
            if (cell.State == CellState.Fallow)
            {
                SpawnFloat("荒地：需要铲开垦", CellTop(cell), GrayInk);
                return;
            }
            SpawnFloat("先点手牌选字", CellTop(cell), Vermilion);
        }

        /// <summary>开垦完成回调（GridCell 计时结束）。</summary>
        public void OnCellCultivated(GridCell cell)
        {
            sfx?.FarmStop(); // 挖土循环计数-1，归零即停（加速完成同走此路）
            sfx?.PlayFarmComplete(); // 开垦完成「叮」（ADR-083）
            SpawnFloat("开垦完成", CellTop(cell), BlueInk);
            RecomputeHighlights(); // 荒地→空地：合法目标集合变化
        }

        /// <summary>F3.2：水系塔（水/冰/渔）——可建水格与临时淹没格（GDD §3.4 特权）。</summary>
        private bool IsWaterTower(string card)
        {
            var def = towerPool != null ? towerPool.Find(card) : null;
            return def != null && def.waterImmune;
        }

        /// <summary>
        /// F1.2（ch4 火山，06 §12.2/§12.3）：灰烬格白名单——火/炮/烬/盆可建（其余全拒）。
        /// F3 三卡进池前恒空集 = 灰烬格对现有全部塔拒绝（零误伤，谓词先立卡后到）。
        /// 字符串字面量白名单（铲 ShovelId 道具卡同族口径——不走 TowerDef 防误配）。
        /// F4.5（2026-10-20）：烬进名单（灰烬格可建，且吃全伤 ×2——见 IsLavaBuffTower）。
        /// </summary>
        private bool IsAshTower(string card) => card == "火" || card == "炮" || card == "烬" || card == "盆";

        /// <summary>
        /// F4.2（06 §12.2）岩浆增益位名单：**火/炮/烬**站在灰烬格 = **全部伤害** ×ashDamageMult（金框提示）。
        /// 与 IsAshTower（可建名单，含盆）分立——盆可放灰但灰格是它唯一归宿，无增益语义；
        /// LavaImmuneTowers（免疫毁塔名单）同集不同用（免疫 ≠ 可建 ≠ 增益，三名单各自独立）。
        /// F4.5（2026-10-20）：烬进增益名单——麒麟践踏把脚下格变灰 = 原地变灰、伤害翻倍（设计配位）。
        /// </summary>
        private bool IsLavaBuffTower(string card) => card == "火" || card == "炮" || card == "烬";

        /// <summary>
        /// F4.3（ADR-031）：关卡实际章图——LevelConfig.chapter 优先（平铺 12 关跨章），
        /// 空 = 场景引用（ch1 旧资产零迁移铁律：YAML 缺行走引用兜底）。
        /// </summary>
        private ChapterConfig ChapterOf(LevelConfig lvl)
        {
            return lvl != null && lvl.chapter != null ? lvl.chapter : chapter;
        }

        // ============ 章界横幅（F4.3 / ADR-031，表现层——05 风险表可砍档） ============

        private Coroutine chapterBannerRoutine;

        /// <summary>章界横幅：章标题 3 秒自动收（复用 hud 顶横幅槽；通关/失守文案会自然顶替）。</summary>
        private void ShowChapterBanner(string title)
        {
            if (chapterBannerRoutine != null) StopCoroutine(chapterBannerRoutine);
            chapterBannerRoutine = StartCoroutine(ChapterBannerCo(title));
        }

        private IEnumerator ChapterBannerCo(string title)
        {
            hud.ShowBanner(title);
            yield return new WaitForSeconds(3f);
            hud.HideBanner();
            chapterBannerRoutine = null;
        }

        /// <summary>
        /// F4.6（ADR-031 / 06 §10.5）+ F5.4 预告义务：ch2 四处一次性气泡（发射波索引驱动，ch1 无命中零变化）。
        /// 键 = 平铺关卡索引（6=二L1 / 7=二L2 / 9=二L4 / 10=二L5），波位 = 潮汐/考校关键波（CSV 备注列）。
        /// </summary>
        private string Chapter2Tip(int waveIndex)
        {
            if (waveIndex == 1 && levelIndex == 6) return "一般塔防无法放置在水格";       // 二L1 W2：潮汐首秀
            if (waveIndex == 0 && levelIndex == 10) return "传闻 · 蛟怒则泽国";     // 二L5 W1：预告义务（06 §10.4 三件套之一）
            return null;
        }

        /// <summary>
        /// F4.4（06 §11.5）：ch3 一次性事件气泡（同 Chapter2Tip 模式——波次索引驱动）。
        /// 三处教学（三L1 迷雾首见 / 三L2 放灯与升阶 / 三L3 找浅影·蝠 W1 首秀）+ 三L5 开场
        /// 枭定场（任务单原文文案）。ch1/ch2（levelIndex 0-10）恒不命中零变化。
        /// </summary>
        private string Chapter3Tip(int waveIndex)
        {
            if (waveIndex == 0 && levelIndex == 11) return "暗处的敌人似乎无法被攻击";   // 三L1 W1：迷雾首见
            if (waveIndex == 0 && levelIndex == 15) return "传闻 · 枭至则灯昏";           // 三L5 W1：开场定场（Boss 预告，枭 W7 压轴）
            return null;
        }

        /// <summary>
        /// F5.3（06 §12.5/§12.6）：**ch4 唯一气泡**——四L5 开场定场「麟至则地燃」（用户 2026-10-18 终裁
        /// 「新增气泡只留这句」：原 L2 灰烬格放火 / L3 放盆两处随 F2.2 砍定，让玩家自探索）。
        /// 同 Chapter2Tip/Chapter3Tip 模式：波次索引驱动；**岩浆零文字**（红闪即教学——用户定案，
        /// L1 不加气泡）；**不做 ch4 版 L0**（章定案，06 §12.6）。ch1-3（levelIndex 0-15）恒不命中零变化。
        /// </summary>
        private string Chapter4Tip(int waveIndex)
        {
            if (waveIndex == 0 && levelIndex == 20) return "传闻 · 麟至则地燃"; // 四L5 W1：开场定场（Boss 预告，麟 W7 压轴）
            return null;
        }

        /// <summary>加速开垦：25 墨立即完成（点开垦中的格子）。</summary>
        private void TrySpeedup(GridCell cell)
        {
            Vector3 at = CellTop(cell);
            if (model.Ink >= gameParams.farmSpeedupCost)
            {
                model.Ink -= gameParams.farmSpeedupCost;
                cell.ForceCompleteCultivation(); // 下一帧 Update 内转空地并回调
                SpawnFloat("加速开垦！", at, BlueInk);
                RefreshHud();
            }
            else
            {
                SfxPlayer.ClickInvalid(); SpawnFloat("墨不足", at, Vermilion);
            }
        }

        /// <summary>手牌字卡 → 空地：放置新 L1 塔（放置免费，墨已在抽卡时消耗）。</summary>
        private void PlaceTower(string cardChar, GridCell cell)
        {
            var def = towerPool.Find(cardChar);
            if (def == null)
            {
                Debug.LogError("[GameManager] 塔字池里没有字：" + cardChar);
                return;
            }

            // ★ F3.1 修订（用户 2026-10-22）：镜不再要求「对面同格空位」——镜像不占格，
            // 对面被占就先隐影，占位塔一走影自动恢复（RefreshMirrorPresentation）。
            hand.ConsumeAt(selectedCardIndex);
            selectedCardIndex = -1;

            var t = NewTowerState(def, cell, model.ActiveFace, 1);
            model.AddTower(t); // 内含 ApplyLevelStats（v2.2 通道重算）
            cell.Tower = t;
            cell.RefreshVisuals();

            var view = towerViews.Get(towerTemplate);
            view.transform.SetParent(cell.transform, false);
            view.Show(def.charId, 1);
            towerViewMap[t] = view;

            AttachMirror(t); // F3.1：镜 → 对面同坐标生镜像（其余卡 createsMirror=false 零路径）

            RecomputeHighlights();
            RefreshHud();
            sfx?.PlayPlace(); // 放置成功（ADR-083）
            tutorial?.Notify(TutorialEvent.Place); // L0 教学：放置完成（ADR-030）
        }

        /// <summary>
        /// F3.1（ch5 镜）：按塔池定义造一颗塔（放置 / 镜像共用同一张字段表——避免两处手抄漏字段）。
        /// level 由调用方定（放置 = 1；镜像恒 1）。字段口径与 v3.0 放置表逐项同源。
        /// </summary>
        private TowerState NewTowerState(TowerCharStats def, GridCell cell, int face, int level)
        {
            return new TowerState
            {
                charId = def.charId,
                baseDamage = def.damage,
                interval = def.interval,
                baseRange = def.range,
                rangePerLevel = def.rangePerLevel,
                baseInkPerPulse = def.inkPerPulse,
                antiAir = def.antiAir,
                ranged = def.ranged,
                value = def.value, // v3.0：卡价值（卖出 = 价值×50%×字位）
                aoeShape = def.aoeShape,       // v3.0 AOE：矛矩形/棍圆形/戟扇形
                aoeBaseSize = def.aoeSize,
                aoeBaseParam = def.aoeParam,
                level = Mathf.Clamp(level, 1, model != null ? model.MaxLevelFor(def.charId) : (gameParams != null ? gameParams.maxTowerLevel : 5)),
                position = new Vector2(cell.X, cell.Y),
                elevation = cell.Elevation,    // F2.2：海拔随格（淹没判定在 BattleModel.Tick）
                waterImmune = def.waterImmune, // F2.2：水系免疫（F3 水/冰/渔打标）
                onWaterCell = cell.IsWater,    // F3.3：渔塔动态跳隔用（水中 5s/陆上 10s）
                slowFactor = def.slowFactor,   // F3.1：命中缓速（水）
                slowSeconds = def.slowSeconds,
                wetSeconds = def.wetSeconds,   // F3.1：命中附湿（水 → 冰查 ×2）
                freezeSeconds = def.freezeSeconds, // F3.1：命中冻结（冰）
                wetDamageMult = def.wetDamageMult,
                landPulseSlower = def.landPulseSlower, // F3.3：渔陆上跳隔 ×2
                burnSeconds = def.burnSeconds,         // F4.1/F4.2：火/炮命中挂燃（0 = 无燃烧零路径）
                burnDps = def.burnDps,
                chainJumps = def.chainJumps,           // F3.2：链电参数（雷）
                chainRatio = def.chainRatio,
                chainRadius = def.chainRadius,
                voidDamageMult = def.voidDamageMult,   // F3.3：虚面加成（煞）
                soulBanner = def.soulBanner,           // F3.4：魂幡（幡）
                soulPayout = def.soulPayout,
                face = face,                           // F2.1（ch5 双面）：塔落在指定面
            };
        }

        // ============ F3.1（ch5 镜）：双面覆盖 ============

        /// <summary>
        /// F3.1 修订（用户 2026-10-22）：**镜像不占格** —— 每帧按「本面同格有没有别的塔」摆镜像：
        ///   有 → 隐去（不画；模型侧 BattleModel.MirrorBlocked 同真值 → 也不攻击/不产墨）；
        ///   无 → 显示（空心描边字：在自己面 = 满档描边，在另一面 = 投影淡档 + 压在本体前一层）。
        /// 幂等；逐帧调用（占格变化散在放塔/移塔/卖塔/焚毁/淹没各处，逐帧同步最省心、零状态机）。
        /// </summary>
        private void RefreshMirrorPresentation()
        {
            if (model == null || towerViewMap == null) return;
            foreach (var kv in towerViewMap)
            {
                var m = kv.Key;
                if (m == null || !m.isMirror) continue;
                bool blocked = model.MirrorBlocked(m);
                var go = kv.Value.gameObject;
                if (go.activeSelf == blocked) go.SetActive(!blocked);
                // 配色跟本体阶数走（镜像阶 = 本体阶 → L1 黑 … L5 金）
                kv.Value.SetShownLevel(m.level);
                if (!blocked) kv.Value.SetGhost(true, m.face != model.ActiveFace);
            }
        }

        /// <summary>
        /// F3.4 幡：魂计数广播（右上角小字「n/10」）。魂是**关卡级**一份（两面共用）→ 每个幡显示同一个值；
        /// 非幡塔恒隐藏（ch1-4 无幡 → 全隐藏 = 零变化）。帧间幂等（TMP 改字自带去重）。
        /// </summary>
        private void RefreshSoulCounters()
        {
            if (model == null || towerViewMap == null) return;
            int per = Mathf.Max(1, model.SoulsPerPayout);
            int remain = model.Souls % per;
            foreach (var kv in towerViewMap)
            {
                if (kv.Key == null) continue;
                if (kv.Key.soulBanner) kv.Value.ShowSouls(remain, per);
                else kv.Value.HideSouls();
            }
        }

        /// <summary>
        /// F3.1：给镜本体建镜像 —— 对面同坐标 / 伤害 = **本体伤害 × mirrorDamageRatio**（镜 0.5）。
        /// 镜像只活在模型里 + 一张视图（不绑 cell.Tower——镜像不占格）。
        /// ★ 用户 2026-10-22 定稿：镜像**随本体升阶**（阶数、伤害、配色全跟本体；恒 = 本体 50%）；
        /// 不进快照（派生状态，回场 EnsureMirrors 按本体阶重建）。
        /// </summary>
        private void AttachMirror(TowerState body)
        {
            if (body == null || body.isMirror || body.mirror != null || model == null || grid == null) return;
            var def = towerPool != null ? towerPool.Find(body.charId) : null;
            if (def == null || !def.createsMirror) return;
            var cell = grid.Get(Mathf.RoundToInt(body.position.x), Mathf.RoundToInt(body.position.y));
            if (cell == null) return;
            int face = body.face == 1 ? 0 : 1;
            // ★ F3.1 修订（用户 2026-10-22）：对面同格被占也照建 —— 镜像不占格，先建着，
            // 被盖住就隐去（RefreshMirrorPresentation / MirrorBlocked），占位塔一走自动现身。

            var m = NewTowerState(def, cell, face, body.level); // 阶跟本体（配色/数值同步的根）
            m.isMirror = true;
            m.mirrorSource = body;
            m.baseDamage = body.baseDamage * def.mirrorDamageRatio; // 基伤 = 本体基伤 ×50%：AddTower → ApplyLevelStats 同一套成长
            model.AddTower(m);
            var view = towerViews.Get(towerTemplate);
            view.transform.SetParent(cell.transform, false);
            view.Show(def.charId, body.level);
            towerViewMap[m] = view;
            view.gameObject.SetActive(true);
            RefreshMirrorPresentation(); // 按「本面同格是否被占」摆显隐/描边（可能一出生就被盖住 → 隐）
            body.mirror = m;
        }

        /// <summary>F3.1：本体离场（卖/合并源/焚毁）→ 镜像同步消失（零回收——镜像从未花墨）。</summary>
        private void DetachMirror(TowerState body)
        {
            if (body == null || body.mirror == null || model == null) return;
            var m = body.mirror;
            body.mirror = null;
            m.mirrorSource = null;
            if (grid != null)
            {
                var cell = grid.Get(Mathf.RoundToInt(m.position.x), Mathf.RoundToInt(m.position.y));
                if (cell != null && cell.Tower == m)
                {
                    cell.Tower = null;
                    cell.RefreshVisuals();
                }
            }
            if (towerViewMap.TryGetValue(m, out var view))
            {
                towerViewMap.Remove(m);
                towerViews.Release(view);
            }
            model.RemoveTower(m);
        }

        /// <summary>F3.1：快照不含镜像（派生）——回场后按本体逐个重建缺的镜像。</summary>
        private void EnsureMirrors()
        {
            if (model == null) return;
            foreach (var t in model.Towers.ToArray())
                if (!t.isMirror && t.mirror == null) AttachMirror(t);
        }

        // ============ 拿起 / 移动（v2.3：免费无限次） ============

        private void PickupTower(GridCell cell)
        {
            // F3.1（ch5 镜）：镜影不可移——它是本体的投影，只能操作本体
            if (cell.Tower != null && cell.Tower.isMirror)
            {
                SpawnFloat("镜影 · 不可动", CellTop(cell) + Vector3.up * 0.7f, GrayInk);
                return;
            }
            // F5.1 枭栖灯锁死（全锁口径备案 09 §7：移/卖/合并全拦——防「栖灯中升阶刷吞噬窗」
            // 漏洞 + 状态机简洁；06 §11.4 字面「不能移动/卖出」的外延处置）
            if (cell.Tower != null && cell.Tower.locked)
            {
                SpawnFloat("枭踞灯上", CellTop(cell) + Vector3.up * 0.7f, Vermilion);
                return;
            }
            pickedTower = cell.Tower;
            pickedOrigin = cell;
            sfx?.PlayPickup(); // 拿起塔（ADR-083）
            pickedView = towerViewMap.TryGetValue(pickedTower, out var v) ? v : null;
            cell.Tower = null;       // 腾出；原格虚影占位（点它 = 取消放回）
            cell.SetGhost(true);
            cell.RefreshVisuals();
            if (pickedView != null) pickedView.SetLifted(true);
            if (rangeIndicator != null)
                rangeIndicator.Show(pickedTower.range, cell.transform.position); // 射程圈：拿起即显示
            towerPickupScreenPos = Input.mousePosition; // F2：拖拽阈值起点
            towerDragging = false;
            if (handBar != null) handBar.SetSellEmphasis(true); // F2.2：卖钮高亮放大
            RecomputeHighlights();
        }

        private void CancelPickup()
        {
            if (pickedTower == null) return;
            // F2.3 灰烬闸（用户终裁后转为防御线）：拿起悬空普通塔 origin 被烧 = 结算当场已毁
            //（SettleEruption picked 分支）——走到这里的幸存塔 = 免疫名单内（火/炮/烬，IsAshTower
            // 同字照放合法）。防御对象是 F4 落卡后两名单分立的组合：**免疫岩浆但灰格不可放**
            //（LavaImmune ≠ IsAshTower）的塔拿在手里 origin 烧灰 → 幸存 → 取消放回 = 绕过
            // 放置谓词的非法占位。拒后塔留手里——另寻格放下或卖出。
            if (pickedOrigin.TileState == TileState.Ash && !IsAshTower(pickedTower.charId))
            {
                SpawnFloat("格已焚毁", CellTop(pickedOrigin) + Vector3.up * 0.7f, Vermilion);
                return;
            }
            pickedOrigin.Tower = pickedTower;
            pickedOrigin.SetGhost(false);
            pickedOrigin.RefreshVisuals();
            if (pickedView != null) pickedView.SetLifted(false);
            ClearPickupState();
            RecomputeHighlights();
        }

        private void MoveTowerToCell(GridCell cell)
        {
            // ★ F3.1 修订（用户 2026-10-22）：本体带镜像移动不再受「对面同格」限制（镜像不占格），
            // 影跟着坐标走；落点对面若被占，影隐去，占位塔一走自动恢复。
            // 模型：只改坐标（免费、无限次、不影响战斗状态）
            pickedTower.position = new Vector2(cell.X, cell.Y);
            model.RefreshLights(); // F1.2：塔移动（灯换位）→ 光集/迷雾挖洞重算（IsLit 谓词实时读缓存）
            pickedTower.elevation = cell.Elevation; // F2.2：海拔随格移动（撤离「移」免费 → 撤到高处即自救）
            pickedTower.onWaterCell = cell.IsWater; // F3.3：渔挪进/出水格 → 动态跳隔即时切换
            pickedTower.face = model.ActiveFace;    // 临时格（面无关）取回后放到当前面 → 归当前面
            // 表现：改绑格子
            if (pickedView != null)
            {
                pickedView.transform.SetParent(cell.transform, false);
                pickedView.SetLifted(false);
            }
            cell.Tower = pickedTower;
            cell.RefreshVisuals();
            // F3.1（ch5 镜）：镜像跟移（同坐标；cell.Tower 只绑本面本体，镜像视图换父即可）
            if (pickedTower.mirror != null)
            {
                var m = pickedTower.mirror;
                m.position = new Vector2(cell.X, cell.Y);
                m.elevation = cell.Elevation;
                m.onWaterCell = cell.IsWater;
                if (towerViewMap.TryGetValue(m, out var mv))
                    mv.transform.SetParent(cell.transform, false);
            }
            if (pickedOrigin != null)
            {
                pickedOrigin.SetGhost(false);
                pickedOrigin.RefreshVisuals();
            }
            ClearPickupState();
            RecomputeHighlights();
            tutorial?.Notify(TutorialEvent.Move); // L0 教学：移动完成（ADR-030）
        }

        /// <summary>丢弃拿起中的塔（卖出/合并消耗源塔时共用）。</summary>
        private void DiscardPickedTower(bool removeFromModel)
        {
            if (pickedTower == null) return;
            DetachMirror(pickedTower); // F3.1：拿着的是镜本体 → 镜像同步消失（卖/合并源/焚毁三路共用本出口）
            if (pickedView != null)
            {
                towerViewMap.Remove(pickedTower);
                towerViews.Release(pickedView);
            }
            if (removeFromModel) model.RemoveTower(pickedTower);
            if (pickedOrigin != null)
            {
                pickedOrigin.SetGhost(false);
                pickedOrigin.RefreshVisuals();
            }
            ClearPickupState();
        }

        private void ClearPickupState()
        {
            pickedTower = null;
            pickedOrigin = null;
            pickedView = null;
            towerDragging = false;                    // F2：拖拽态随拿起结束清
            if (handBar != null) handBar.SetSellEmphasis(false); // F2.2：卖钮复原
            if (rangeIndicator != null) rangeIndicator.Hide(); // 射程圈随拿起态结束隐藏（取消/移动/卖出/合并共用出口）
        }

        // ============ 合并判定执行（规则在 MergeRules） ============

        /// <summary>手牌字卡 → 场上塔：同字 L1 → 升阶（v3.0：词塔合成/喂养已删）。</summary>
        private void CardOnTower(string card, GridCell cell)
        {
            var tower = cell.Tower;
            if (tower.isMirror) // F3.1（ch5 镜）：镜影不可升
            {
                SpawnFloat("镜影 · 不可升", CellTop(cell) + Vector3.up * 0.7f, GrayInk);
                return;
            }
            if (tower.locked) // F5.1 锁死灯不可被卡合并（全锁口径）
            {
                SpawnFloat("枭踞灯上", CellTop(cell) + Vector3.up * 0.7f, Vermilion);
                return;
            }
            var res = MergeRules.EvaluateCardOnTower(card, tower, model.MaxLevelFor(card));
            Vector3 above = CellTop(cell) + Vector3.up * 0.7f;

            if (res.reject != MergeRules.RejectReason.None)
            {
                SpawnFloat(RejectText(res.reject), CellTop(cell) + Vector3.up * 0.3f, Vermilion);
                return;
            }

            if (res.kind == MergeRules.MergeKind.PairUpgrade)
            {
                hand.ConsumeAt(selectedCardIndex);
                selectedCardIndex = -1;
                model.TryUpgradeTower(tower);
                sfx?.PlayMerge(tower.level); // 卡上塔升阶（结果等级）
                ShowTower(cell, tower);
                SpawnFloat("升阶 Lv" + tower.level, above, BlueInk);
                tutorial?.Notify(TutorialEvent.Merge); // L0 教学：卡上塔升阶完成（ADR-030）
            }

            RecomputeHighlights();
            RefreshHud();
        }

        /// <summary>拿起中的塔 → 场上塔：同字同级合并 +1；异字无效（v3.0：喂养/合成已删）。</summary>
        private void PickedTowerOnTower(GridCell targetCell)
        {
            var target = targetCell.Tower;
            // F3.1（ch5 镜）：镜影不可升——目标格若是对面投影，合并拒绝（先于锁定判定）
            if (target.isMirror)
            {
                SpawnFloat("镜影 · 不可升", CellTop(targetCell) + Vector3.up * 0.7f, GrayInk);
                return;
            }
            if (target.locked) // F5.1 锁死灯不可被塔合并（全锁口径——拿起态点击）
            {
                SpawnFloat("枭踞灯上", CellTop(targetCell) + Vector3.up * 0.7f, Vermilion);
                return;
            }
            var res = MergeRules.EvaluateTowerOnTower(pickedTower, target, model.MaxLevelFor(pickedTower.charId));
            Vector3 above = CellTop(targetCell) + Vector3.up * 0.7f;

            // 用户 2026-10-23：不能合成（异字 / 同级不同 / 满级）→ **换位**（不再只弹文案）
            if (res.reject != MergeRules.RejectReason.None || !res.Hit)
            {
                SwapPickedWithTower(targetCell);
                return;
            }

            // 同字同级：结果留在目标格，源格腾空
            model.MergeTowers(pickedTower, target);
            DiscardPickedTower(false);
            ShowTower(targetCell, target);
            if (towerViewMap.TryGetValue(target, out var merged)) merged.Pop(); // 0.3：合并色跳弹跳强调
            sfx?.PlayMerge(target.level); // 合成按结果等级（ADR-083）
            SpawnFloat("合并 Lv" + target.level, above, BlueInk);
            tutorial?.Notify(TutorialEvent.Merge); // L0 教学：塔上塔合并完成（ADR-030）

            RecomputeHighlights();
            RefreshHud();
        }

        // ============ 互换塔位置（用户 2026-10-23：不能合成 → 换位） ============

        /// <summary>
        /// 拿起塔 ↔ 目标塔互换位置：地形双向校验（水格/灰烬）→ 模型坐标互换 → 格-塔重绑 →
        /// 视图互换 → 镜像跟移 → 清空拿起态。异字 / 同字不同级 / 满级都走这里。
        /// </summary>
        private void SwapPickedWithTower(GridCell targetCell)
        {
            var target = targetCell != null ? targetCell.Tower : null;
            var origin = pickedOrigin;
            if (target == null || origin == null || pickedTower == null) return;

            if (!CanTowerOccupy(pickedTower, targetCell) || !CanTowerOccupy(target, origin))
            {
                SpawnFloat("此格不能换位", CellTop(targetCell) + Vector3.up * 0.5f, GrayInk);
                return;
            }

            // 模型：坐标/海拔/水格互换
            Vector2 op = pickedTower.position;
            int oe = pickedTower.elevation;
            bool ow = pickedTower.onWaterCell;
            pickedTower.position = new Vector2(targetCell.X, targetCell.Y);
            pickedTower.elevation = targetCell.Elevation;
            pickedTower.onWaterCell = targetCell.IsWater;
            target.position = op;
            target.elevation = oe;
            target.onWaterCell = ow;
            pickedTower.face = model.ActiveFace; // 临时格（面无关）换位后归当前面
            model.RefreshLights();

            // 格-塔绑定互换
            targetCell.Tower = pickedTower;
            origin.Tower = target;

            // 视图互换
            if (pickedView != null)
            {
                pickedView.transform.SetParent(targetCell.transform, false);
                pickedView.SetLifted(false);
            }
            if (towerViewMap.TryGetValue(target, out var targetView))
                targetView.transform.SetParent(origin.transform, false);

            MoveMirrorTo(pickedTower, targetCell);
            MoveMirrorTo(target, origin);

            origin.SetGhost(false);
            origin.RefreshVisuals();
            targetCell.RefreshVisuals();
            ClearPickupState();
            RecomputeHighlights();
            RefreshHud();
            SpawnFloat("换位", CellTop(targetCell) + Vector3.up * 0.5f, BlueInk);
        }

        /// <summary>地形能否容纳该塔（水格限水系 / 灰烬限白名单）——放置与换位同口径。</summary>
        private bool CanTowerOccupy(TowerState t, GridCell cell)
        {
            if (t == null || cell == null) return false;
            if (cell.TileState == TileState.Ash && !IsAshTower(t.charId)) return false;
            if (cell.State == CellState.Empty) return true;
            if (cell.State == CellState.Water && t.waterImmune) return true;
            return false;
        }

        /// <summary>镜像跟本体坐标（镜影不占格，只改坐标 + 视图父级）。</summary>
        private void MoveMirrorTo(TowerState t, GridCell cell)
        {
            if (t == null || t.mirror == null || cell == null) return;
            var m = t.mirror;
            m.position = new Vector2(cell.X, cell.Y);
            m.elevation = cell.Elevation;
            m.onWaterCell = cell.IsWater;
            if (towerViewMap.TryGetValue(m, out var mv)) mv.transform.SetParent(cell.transform, false);
        }

        private void ShowTower(GridCell cell, TowerState t)
        {
            if (towerViewMap.TryGetValue(t, out var v)) v.Show(t.charId, t.level);
            cell.RefreshVisuals();
        }

        /// <summary>P0 拒绝文案（03 §4.3.1 v3.0 修订）。</summary>
        private static string RejectText(MergeRules.RejectReason r)
        {
            switch (r)
            {
                case MergeRules.RejectReason.HandLevel: return "手牌只能配一级塔";
                case MergeRules.RejectReason.MaxLevel: return "已达五级上限";
                case MergeRules.RejectReason.NoMerge: return "此二字无法合成";
                default: return "无法合成"; // 塔对塔同字不同级等：真值表「无效」兜底
            }
        }

        // ============ 合法目标高亮（发现性：选牌/拿塔后朱色提示） ============

        private void RecomputeHighlights()
        {
            ClearHighlights();
            // 手感优化（用户 2026-10-09）：结束（胜/败）后可继续操作——高亮随放行（Tick 停 =
            // 墨塔停产/战斗停，「经济停、摆弄行」口径；抽卡与提前召唤仍拦）。
            if (model == null || grid == null) return;

            string card = null;
            bool shovel = false;
            bool pot = false;
            if (pickedTower == null && selectedCardIndex >= 0 && selectedCardIndex < hand.Cards.Count)
            {
                card = hand.Cards[selectedCardIndex];
                shovel = card == HandModel.ShovelId;
                pot = card == HandModel.PotId; // F4.3：盆走独立分支（不落塔——非法目标/合法目标同真值）
            }
            bool cardWater = card != null && IsWaterTower(card); // F3.2：水系手牌 → 水格也是合法目标

            // F3.1 优位预览（06 §11.2「放塔 UI 画射程∩光照」）：选中卡/拿起塔时合法格里
            // 「放这里有效」的格升金框（攻击塔 = 射程圆与光照圆相交；灯 = 照明圆触路）。
            var cardDef = card != null && towerPool != null ? towerPool.Find(card) : null;

            foreach (var cell in grid.All)
            {
                if (pickedTower != null)
                {
                    if (cell == pickedOrigin) continue; // 虚影格不高亮（点它 = 取消）
                    if (cell.Tower != null)
                    {
                        var res = MergeRules.EvaluateTowerOnTower(pickedTower, cell.Tower, model.MaxLevelFor(pickedTower.charId));
                        if (res.Hit && !cell.Tower.locked) HighlightTower(cell.Tower); // F5.1 锁死灯不亮合并目标
                    }
                    else if ((cell.State == CellState.Empty &&
                              (cell.TileState != TileState.Ash || IsAshTower(pickedTower.charId))) || // F1.2：灰烬格非白名单不亮（与放行同真值）
                        (cell.State == CellState.Water && pickedTower.waterImmune)) // F3.2：水系移动入水格（F4.3b：绿框不再单列亮条——同上）
                    {
                        // F3.1 修订（2026-10-22）：镜像不占格 → 移动预览不再因「对面同格」禁放
                        cell.SetHighlight(true, IsPrimeSpot(new Vector2(cell.X, cell.Y),
                            pickedTower.range, pickedTower.charId == "灯", pickedTower.damage > 0) ||
                            (cell.TileState == TileState.Ash && IsLavaBuffTower(pickedTower.charId))); // F4.2 灰格增益位金框（火/炮）
                    }
                }
                else if (shovel)
                {
                    if (cell.State == CellState.Fallow && cell.TileState != TileState.Ash) // F1.2：灰烬格禁垦——不亮（与放行同真值）
                        cell.SetHighlight(true);
                }
                else if (pot)
                {
                    if (cell.TileState == TileState.Ash) // F4.3：盆合法目标 = 灰烬格（与放行同真值；绿色复用 F3.1 绿框）
                        cell.SetHighlight(true);
                }
                else if (card != null)
                {
                    if (cell.Tower != null)
                    {
                        var res = MergeRules.EvaluateCardOnTower(card, cell.Tower, model.MaxLevelFor(card));
                        if (res.Hit && !cell.Tower.locked) HighlightTower(cell.Tower); // F5.1 锁死灯不亮合并目标
                    }
                    else if ((cell.State == CellState.Empty &&
                              (cell.TileState != TileState.Ash || IsAshTower(card))) || // F1.2：灰烬格非白名单不亮（与放行同真值）
                        (cell.State == CellState.Water && cardWater)) // F3.2：水系手牌落水格（F4.3b：绿框不再单列亮条——同上）
                    {
                        // F3.1 修订（2026-10-22）：镜手牌不再因「对面同格」不亮
                        cell.SetHighlight(true, (cardDef != null && IsPrimeSpot(new Vector2(cell.X, cell.Y),
                            cardDef.range, cardDef.charId == "灯", cardDef.damage > 0)) ||
                            (cell.TileState == TileState.Ash && IsLavaBuffTower(card))); // F4.2 灰格增益位金框（火/炮手牌）
                    }
                }
            }
        }

        private void HighlightTower(TowerState t)
        {
            if (towerViewMap.TryGetValue(t, out var v))
            {
                v.SetHighlight(true);
                v.ShowMergePreview(t.level + 1); // F1.1（docs/16）：合并结果等级预览
            }
        }

        /// <summary>
        /// F3.1 优位判定（06 §11.2「放塔 UI 画射程∩光照」的格选择视角——触屏无 hover，选中即
        /// 全图分级引导）：攻击塔（combat = damage>0）= 射程圆与任一光照圆相交（放这里打得到
        /// 光里的敌）；灯 = 照明圆触路（照亮敌道）；经济/机制卡恒 false（无交战语义——墨放哪
        /// 都产墨，range 字段存在但不构成优位）。ch1/ch2 光集恒空 → 攻击塔恒 false、灯卡不存在
        /// → 零变化（铁律）；拿起塔用当前 range（升级后预览同步变大）。
        /// </summary>
        private bool IsPrimeSpot(Vector2 pos, double range, bool isLamp, bool combat)
        {
            if (isLamp)
                return path != null && path.DistanceToRoute(pos) <= range + 0.01;
            if (!combat) return false;
            var lights = model.GetLights();
            for (int i = 0; i < lights.Count; i++)
                if (Vector2.Distance(pos, lights[i].pos) <= range + lights[i].radius + 0.01)
                    return true;
            return false;
        }

        private void ClearHighlights()
        {
            if (grid != null)
                foreach (var cell in grid.All) cell.SetHighlight(false);
            foreach (var v in towerViewMap.Values)
            {
                v.SetHighlight(false);
                v.HideMergePreview(); // F1.1：清高亮同时清合并预览
            }
        }

        // ============ 模型事件 → 表现 ============

        private void OnEnemySpawned(EnemyState e)
        {
            RecordCodexEnemy(e != null ? e.charId : null); // 图鉴：记录已遭遇敌字
            var view = enemyViews.Get(enemyTemplate);
            view.Show(e);
            view.SetFaceDim(e.face != model.ActiveFace && !e.crossFaceAttackable); // F2.4（ch5 双面）：对面上场的敌 = 半透明冷色；F6.1 傩神跨面不暗显
            activeEnemies[e] = view;
            if (e.isBoss) bossSpawnClock = waveClock; // F7.1 探针：Boss 入场钟（③ 拉锯时长量数）
            // 分裂/召唤产物挂来源敌的波次：波清与胜利判定计入（父结算前生成，父此刻仍在 enemyWave）
            if (e.spawner != null && enemyWave.TryGetValue(e.spawner, out var rt))
            {
                enemyWave[e] = rt;
                rt.pending++;
            }
        }

        private void OnEnemyDied(EnemyState e)
        {
            sfx?.PlayKill(); // 击杀（ADR-083；4 声道轮转防同帧爆音）
            LogBossDuration(e, "击破"); // F7.1 探针：Boss 战时长
            // F6.1（ch5 傩神）：傩面碎 → 3s 黑幕 → 傩神自起点入场（三面随行）
            if (e.charId == "傩面") StartCoroutine(NuoShenArrivalRoutine());
            // 死亡动画由视图轮询 State.dead 自行播放；这里掉墨飘字 + 波次结算
            if (e.inkDrop > 0 && activeEnemies.ContainsKey(e)) // 召唤物/分裂虫 ink=0：不飘 "+0"
                SpawnFloat("+" + e.inkDrop.ToString("0"),
                    (Vector3)model.GetEnemyPosition(e), BlueInk, 10f); // 战斗数值小号
            OnEnemyResolved(e);
        }

        /// <summary>F4.3（ch5 傀）：换影复活——在**新位置**（另一面同进度）飘「换影」，让玩家看见它跑到了对面。
        /// 视图 tint/半透明由 F2.4 的逐帧面态刷新自动跟上（本单元不加状态机）。</summary>
        private void OnEnemyRevived(EnemyState e)
        {
            if (e == null) return;
            SpawnFloat("换影", (Vector3)model.GetEnemyPosition(e) + Vector3.up * 0.6f, Vermilion, 11f);
            // 换面态要**立刻**改：面态刷新的两条入口（出生 / 换面）都不会再走到这只敌
            //（若等到下次换面才刷新，它会以「当前面全亮」的样子跑在对面 = 骗读）。
            if (activeEnemies.TryGetValue(e, out var view))
                view.SetFaceDim(e.face != model.ActiveFace && !e.crossFaceAttackable);
        }

        // ============ F5.1（ch5 傩面 P1，ADR-069）：三脸轮换的表现 ============

        /// <summary>
        /// 换脸预告（周期末尾 3s）：面具翻转 + 起势。表现降级档 = 字色闪三下 + 飘「换脸」；
        /// 面与落点此帧**不动**（预告只是给玩家「该换面/该撤塔」的反应窗）。
        /// </summary>
        private void OnNuoMaskWarn(EnemyState e)
        {
            if (e == null) return;
            var p = (Vector3)model.GetEnemyPosition(e) + Vector3.up * 0.9f;
            SpawnFloat("换脸", p, Vermilion, 11f);
        }

        /// <summary>换脸完成：字色随脸（赤/青/金）+ 脸名飘字。落点已在模型侧按同进度换好面。</summary>
        private void OnNuoMaskChanged(EnemyState e)
        {
            if (e == null) return;
            if (activeEnemies.TryGetValue(e, out var view))
            {
                view.SetMaskTint(e.nuoMaskIndex);
                view.PlayMaskFlip();                          // F5.3：面具翻转演出（X 轴压扁再展开）
                view.SetFaceDim(e.face != model.ActiveFace && !e.crossFaceAttackable); // 换面落点可能换到对面 → 立刻按面改明暗
            }
            sfx?.PlayNuoMaskChange(); // F5.3：换脸音（空槽静音跳过）
            var p = (Vector3)model.GetEnemyPosition(e) + Vector3.up * 0.9f;
            string name = e.nuoMaskIndex == 0 ? "赤面" : (e.nuoMaskIndex == 1 ? "青面" : "金面");
            var col = e.nuoMaskIndex == 0 ? Vermilion : (e.nuoMaskIndex == 1 ? BlueInk : NuoGoldInk);
            SpawnFloat(name, p, col, 12f);
        }

        // ============ F5.2（ch5 傩面 P1）三脸招式表现 ============

        /// <summary>
        /// 赤脸：劈同面最近一塔 —— **毁塔不毁格**（格留空、可原地重建）。拿起中的塔走 discard
        /// 管线（与岩浆同口径：拿起 ≠ 安全）；定位失败也删模型，绝不留幽灵塔。
        /// </summary>
        private void OnNuoSmashTower(EnemyState e, TowerState t)
        {
            if (t == null) return;
            if (pickedTower == t)
            {
                DiscardPickedTower(true);
            }
            else
            {
                var cell = grid != null ? grid.Get(Mathf.RoundToInt(t.position.x), Mathf.RoundToInt(t.position.y)) : null;
                if (cell != null && cell.Tower == t) DestroyTowerByNuo(cell, t);
                else
                {
                    model.RemoveTower(t);
                    if (towerViewMap.TryGetValue(t, out var v))
                    {
                        towerViewMap.Remove(t);
                        towerViews.Release(v);
                    }
                    if (t.isMirror) { if (t.mirrorSource != null) t.mirrorSource.mirror = null; }
                    else DetachMirror(t);
                    RefreshHud();
                }
            }
            SpawnFloat("吃塔", (Vector3)model.GetEnemyPosition(e) + Vector3.up * 0.4f, Vermilion, 11f);
            sfx?.PlayNuoSmash();
        }

        /// <summary>金脸起手（2s）：锁定最近塔、可被换面打断——给玩家一个明确的「读招窗」。</summary>
        private void OnNuoGoldCast(EnemyState e, TowerState t)
        {
            SpawnFloat("念咒中", (Vector3)model.GetEnemyPosition(e) + Vector3.up * 0.9f, NuoGoldInk, 11f);
        }

        /// <summary>金脸被打断（起手期换面）：金进 6s CD —— 换面的代价被兑现成收益。</summary>
        private void OnNuoGoldInterrupted(EnemyState e)
        {
            SpawnFloat("打断", (Vector3)model.GetEnemyPosition(e) + Vector3.up * 0.9f, BlueInk, 11f);
        }

        /// <summary>金脸封印落地：目标塔停火 3s（表现走 RefreshTowerSilence 的「封」标记位）。</summary>
        private void OnNuoSealApplied(EnemyState e, TowerState t)
        {
            if (t == null) return;
            SpawnFloat("封", (Vector3)t.position + Vector3.up * 0.6f, NuoGoldInk, 11f);
            sfx?.PlayNuoSeal();
        }

        /// <summary>金脸跟面（玩家换面）：模型侧已换路，视图立刻按新面去半透明（同帧，不留骗读）。</summary>
        private void OnNuoFaceFollowed(EnemyState e)
        {
            if (e == null) return;
            if (activeEnemies.TryGetValue(e, out var view))
                view.SetFaceDim(e.face != model.ActiveFace && !e.crossFaceAttackable);
        }

        // ============ F6（ch5 傩神 P2，ADR-070）：黑幕入场 + 三面随行 + 傩舞 ============

        /// <summary>
        /// F6.1 傩面碎 → **3s 黑幕**（淡入 0.6 / 全黑 1.8 / 淡出 0.6，z=-5 盖住场内不盖 HUD）
        /// → 傩神自起点入场（实路、不换面）+ 三面随行。演出按游戏时间走（倍速同步）。
        /// </summary>
        private System.Collections.IEnumerator NuoShenArrivalRoutine()
        {
            if (nuoShenArriving) yield break;
            nuoShenArriving = true;
            float total = gameParams != null ? gameParams.shenBlackoutSeconds : 3f;
            float fadeIn = 0.6f, fadeOut = 0.6f;
            float hold = Mathf.Max(0f, total - fadeIn - fadeOut);

            var layer = MakeFullScreenLayer("NuoShenBlackout", -5f, new Color(0f, 0f, 0f, 0f));
            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.deltaTime;
                var c = layer.color; c.a = Mathf.Clamp01(t / fadeIn); layer.color = c;
                yield return null;
            }
            // 全黑停留：暂停时不计时（暂停是独立 flag、不读写 timeScale → 这里自己扣）
            float held = 0f;
            while (held < hold)
            {
                if (!paused) held += Time.deltaTime;
                yield return null;
            }
            // 傩神入场（不走波表——GDD §7.7：Boss 不占波次名额）
            if (enemyTable != null && model != null && !model.Failed)
            {
                var stats = enemyTable.Find("傩神");
                if (stats != null)
                {
                    shenBoss = model.SpawnEnemy(stats, null, false, 0, 1.0, 0);
                    shenBoss.dist = 0;
                    CreateShenMasks();
                    SpawnFloat("傩神降临", (Vector3)model.GetEnemyPosition(shenBoss) + Vector3.up * 1.2f, Vermilion, 14f);
                }
            }

            t = 0f;
            while (t < fadeOut)
            {
                t += Time.deltaTime;
                var c = layer.color; c.a = 1f - Mathf.Clamp01(t / fadeOut); layer.color = c;
                yield return null;
            }
            Destroy(layer.gameObject);
            nuoShenArriving = false;
        }

        /// <summary>F6.1 三面随行：三枚小字（赤/青/金）无血条、不可选中、不可击杀，恒在傩神身后沿路排开。</summary>
        private void CreateShenMasks()
        {
            ClearShenMasks();
            string[] names = { "赤", "青", "金" };
            Color[] cols = { Vermilion, BlueInk, NuoGoldInk };
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("ShenMask" + i, typeof(TextMeshPro));
                go.transform.SetParent(transform, false);
                var tmp = go.GetComponent<TextMeshPro>();
                tmp.font = font;
                tmp.fontSize = 12f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.text = names[i];
                tmp.color = cols[i];
                shenMasks.Add(tmp);
            }
        }

        private void ClearShenMasks()
        {
            foreach (var m in shenMasks)
                if (m != null) Destroy(m.gameObject);
            shenMasks.Clear();
        }

        /// <summary>逐帧摆三面（沿路线取傩神身后 0.9/1.8/2.7 格处）；傩神不在场即收。</summary>
        private void UpdateShenMasks()
        {
            if (shenMasks.Count == 0) return;
            if (shenBoss == null || shenBoss.dead || shenBoss.reached || shenBoss.route.PointAt == null)
            {
                ClearShenMasks();
                return;
            }
            for (int i = 0; i < shenMasks.Count; i++)
            {
                double d = System.Math.Max(0.0, shenBoss.dist - 0.9 * (i + 1));
                Vector2 p = shenBoss.route.PointAt(d);
                shenMasks[i].transform.position = new Vector3(p.x, p.y + 0.45f, -0.45f);
            }
        }

        /// <summary>F6.2 傩舞起：停 2.5s + 受伤 ×1.5 + **三面同时施法**（青召在此补；赤/金在模型内同帧落）。</summary>
        private void OnShenDanceStarted(EnemyState e)
        {
            if (e == null) return;
            sfx?.PlayNuoDance(); // 傩舞开始（ADR-083）
            SpawnFloat("傩 舞", (Vector3)model.GetEnemyPosition(e) + Vector3.up * 1.5f, NuoGoldInk, 14f);
            if (enemyTable != null && gameParams != null && !string.IsNullOrEmpty(gameParams.nuoSummonChar))
            {
                var stats = enemyTable.Find(gameParams.nuoSummonChar);
                SummonGhosts(e, stats, 1, gameParams.shenSummonPerWave);  // 青：虚面 3 鬼
            }
        }

        private void OnShenDanceEnded(EnemyState e)
        {
            if (e == null) return;
            SpawnFloat("舞止", (Vector3)model.GetEnemyPosition(e) + Vector3.up * 1.2f, Vermilion, 11f);
        }

        /// <summary>产墨跳档飘字（0.3 Juice，ADR-027）：墨塔每 InkPulseSeconds 一跳 +N（ch2 渔塔同管线）。</summary>
        private void OnInkPulsed(TowerState t, double amount)
        {
            sfx?.PlayInkPulse();
            SpawnFloat("+" + amount.ToString("0"), (Vector3)t.position + Vector3.up * 0.45f, BlueInk, 10f);
        }

        /// <summary>F3.4（ch5 幡）：每 10 魂结墨 —— 与墨塔产墨同款「+N」蓝字（位置钉在结算的那个幡上）。</summary>
        private void OnSoulsPulsed(TowerState banner, double amount)
        {
            sfx?.PlayInkPulse();
            SpawnFloat("+" + amount.ToString("0"), (Vector3)banner.position + Vector3.up * 0.45f, BlueInk, 10f);
        }

        // ============ F2.1 潮汐（ADR-032 / 06 §10.2） ============

        /// <summary>
        /// 水线切换：格子淹没视觉 + 潮位播报 + 撤离警报。塔沉默规则判定在 model 侧（F2.2，即时）。
        /// F2.4 扫掠（06 §10.2）：涨潮左→右淹、退潮右→左干（右侧最早干）——按列错峰延迟，
        /// 单格渐变 2.5s + 列间跨度 5.5s ≈ 总扫过 8s。水格恒淹不参与扫掠。
        /// </summary>
        private void OnWaterLevelChanged(int oldLevel, int newLevel)
        {
            bool rising = newLevel > oldLevel;
            int minX = 0, maxX = 0;
            float perCol = 0f;
            if (grid != null)
            {
                minX = int.MaxValue; maxX = int.MinValue;
                foreach (var c in grid.All)
                {
                    if (c.X < minX) minX = c.X;
                    if (c.X > maxX) maxX = c.X;
                }
                perCol = maxX > minX ? 6.5f / (maxX - minX) : 0f; // 列间跨度 6.5s + 单格 1.2s ≈ 8s（06 §10.2）
                foreach (var cell in grid.All)
                {
                    // 水格 IsWater 恒 true 但 SetSubmerged 内部豁免（F1 水色零变化）
                    bool on = cell.IsWater || cell.Elevation <= newLevel;
                    float delay = cell.IsWater || perCol <= 0f ? 0f
                        : rising ? (cell.X - minX) * perCol   // 涨潮：左列先淹
                        : (maxX - cell.X) * perCol;            // 退潮：右列先干
                    cell.SetSubmerged(on, delay);
                }
                // F4 验收追加（ADR-035）：淹没区连片水面——被淹格周围的棋盘空隙铺半透明深青水层
                // （底纹上、路面下：干格像露出水面的孤岛、堤路横穿水面）；水线 ≥ 4 整版全开（F5 蛟 B 方案）
                grid.ApplyWaterSheet(newLevel, rising, perCol, minX, maxX);
            }
            // F4 验收追加（ADR-035）：路面砖随潮——水路砖 ≥ 1 加深一档、墨路砖 ≥ 4（全图洪水）淹没
            if (path != null) path.SetRoadTide(newLevel, rising, perCol, minX, maxX);
            if (hud != null)
            {
                SpawnFloat(TideName(newLevel), new Vector3(0f, 4.1f, 0f), BlueInk, 12f);
                // F2.3 撤离警报（GDD §3.4 三选项）：涨/大涨且确有塔将被淹才提示（降噪）。
                // 三选项 = 移（免费，挪高处即自救）/ 卖（×50%×字位）/ 不动（倒计时归零消失）
                if (newLevel >= 2 && model != null)
                {
                    int atRisk = 0;
                    foreach (var t in model.Towers)
                        if (!t.waterImmune && t.elevation <= newLevel) atRisk++;
                    if (atRisk > 0)
                        SpawnFloat("涨潮了！",
                            new Vector3(0f, 3.3f, 0f), Vermilion, 10f);
                }
            }
        }

        private static string TideName(int level)
        {
            switch (level)
            {
                case 0: return "退潮";
                case 1: return "平潮";
                case 2: return "涨潮";
                case 3: return "大涨";
                default: return "洪水"; // 4 = 全图洪水（F5 蛟 / T 键预览，ADR-035）
            }
        }

        // ============ F2.2 淹没沉默（ADR-032 / GDD §3.4） ============

        /// <summary>塔淹没倒计时归零消失（0 回收，撤离「不动」结局）：清格占用 + 释放视图 + 播报。</summary>
        /// <summary>F1.2：光照集变化（进关/灯增删/升阶/移动/淹没出局）→ 迷雾挖洞重画。无雾章空操作。</summary>
        private void OnLightsChanged()
        {
            if (grid == null || model == null) return;
            if (activeChapter == null || !activeChapter.fogEnabled) return;
            grid.SetFogLights(model.GetLights());
        }

        private void OnTowerDrowned(TowerState t)
        {
            if (towerViewMap.TryGetValue(t, out var view))
            {
                // 视图恒挂格子下（放置/移动都 SetParent）→ 父即所在格
                var cell = view.transform.parent != null
                    ? view.transform.parent.GetComponent<GridCell>()
                    : null;
                if (cell != null && cell.Tower == t)
                {
                    cell.Tower = null;
                    cell.RefreshVisuals();
                }
                towerViewMap.Remove(t);
                towerViews.Release(view);
            }
            if (pickedTower == t)
            {
                // 手持中被淹：原格虚影须显式清（ClearPickupState 不碰 ghost，防残留）
                if (pickedOrigin != null)
                {
                    pickedOrigin.SetGhost(false);
                    pickedOrigin.RefreshVisuals();
                }
                ClearPickupState(); // 一并清拿起态（防视图野指针）
            }
            SpawnFloat("淹没", (Vector3)t.position + Vector3.up * 0.5f, BlueInk, 9f);
            // F3.1（ch5 镜）：本体被淹 → 对面镜像同灭；影被淹 → 只断链接（本体在自己那面照常结算）
            if (t.isMirror)
            {
                if (t.mirrorSource != null) t.mirrorSource.mirror = null;
            }
            else DetachMirror(t);
            RecomputeHighlights();
            RefreshHud();
        }

        /// <summary>F5.1 枭吞噬灯塔（0 回收）：清格占用 + 释放视图 + 播报（淹没消失同构——
        /// 锁死拦了拿起态 → 无虚影清理分支；「灯被吞噬」终局文案与「淹没」分离）。</summary>
        private void OnLampDevoured(TowerState t)
        {
            if (towerViewMap.TryGetValue(t, out var view))
            {
                var cell = view.transform.parent != null
                    ? view.transform.parent.GetComponent<GridCell>()
                    : null;
                if (cell != null && cell.Tower == t)
                {
                    cell.Tower = null;
                    cell.RefreshVisuals();
                }
                towerViewMap.Remove(t);
                towerViews.Release(view);
            }
            SpawnFloat("灯被吞噬", (Vector3)t.position + Vector3.up * 0.5f, Vermilion, 9f);
            RecomputeHighlights();
            RefreshHud();
        }

        /// <summary>塔状态视觉同步（0.1s 节流）：**沉默 > 入梦 > 常态** 三级——
        /// 淹没灰显 + 倒计时（优先）/ F4.2 魇入梦（冷灰蓝 + 「梦」字）/ 都无则回阶色。</summary>
        private void RefreshTowerSilence()
        {
            if (model == null) return;
            foreach (var t in model.Towers)
            {
                if (!towerViewMap.TryGetValue(t, out var view)) continue;
                if (t.submerged)
                {
                    view.ClearAsleep();               // 沉默优先：梦/封标记让位
                    view.ClearSealed();
                    view.ShowSilence(t.submergeCountdown);
                }
                else if (t.sealRemaining > 0)
                {
                    if (view.Silenced) view.ClearSilence();
                    view.ClearAsleep();
                    view.ShowSealed();
                }
                else if (t.sleepRemaining > 0)
                {
                    if (view.Silenced) view.ClearSilence();
                    view.ClearSealed();
                    view.ShowAsleep();
                }
                else
                {
                    if (view.Silenced) view.ClearSilence();
                    view.ClearAsleep();
                    view.ClearSealed();
                }
            }
        }

        /// <summary>激怒反馈（ADR-022）：牛过半血时朱红提示（机制数值可在敌表调）。</summary>
        private void OnEnemyEnraged(EnemyState e)
        {
            SpawnFloat("加速",
                (Vector3)model.GetEnemyPosition(e), Vermilion, 12f);
        }

        // ============ F5 蛟两幕剧（06 §10.4 / ADR-036） ============

        private Coroutine bossRoutine; // 转阶段序列（蛟：蓄水→洪水→召唤暂停→潮落 / 枭：蓄水→黑幕→换端二圈）；DoEnterLevel 随场拆
        private SpriteRenderer blackVeil; // F5.2 枭黑幕（CreateBlackVeil 建；常隐）
        private SpriteRenderer redVeil;   // F6.2 麟共振全场红闪（CreateRedVeil 建；常隐）
        // F7.1 手测探针（用户 2026-10-18 点题，Console 零玩法影响）：
        private double bossSpawnClock = -1; // Boss 入场战斗钟（waveClock 刻度——暂停/胜负冻结；<0 = 无）
        private bool tuningReportDone;      // 场末体检一次制（RefreshHud 每帧重刷防重复）
        private Vector3 resultSlot = new Vector3(8.1f, -4.55f, 0f); // 插A 三轮：结算钮右下角下一波原位（Build 实测取 handBar）
        private Vector3 retrySlot = new Vector3(-8.1f, -4.55f, 0f); // 插A 四轮：回滚钮左下角对称位（用户口径更正）
        private bool bossBgmOn;          // BGM 轮询标记（Boss 在场；PollBossStage 幂等切轨）
        private Coroutine flickerRoutine; // 枭氛围黑闪协程（PollBossStage 启停；DoEnterLevel 拆）

        /// <summary>F5.2 蓄水预警：1/3 血触发的第一幕（蛟吼/水线涌起/晕染 = 表现降级档，计时与文案必留）。</summary>
        private void OnBossPhase2(EnemyState dragon)
        {
            // F5.2/F6.2 分叉：枭走黑幕换端二圈（OnOwlPhase2）；麟走火山共振（OnLeiPhase2，
            // 06 §12.5）；蛟走洪水两幕剧（原编排）。
            if (dragon.charId == "枭")
            {
                OnOwlPhase2(dragon);
                return;
            }
            if (dragon.charId == "麟")
            {
                OnLeiPhase2(dragon);
                return;
            }
            // F5.2 转阶段 i-frame + 定身（用户定案 2026-10-05）：蓄水窗内蛟无敌（防抢杀跳戏）+
            // 站桩演出；洪水落地双解除（见 BossFloodRoutine）。命中反馈照放、伤害数字不显（防误导）。
            dragon.invulnerable = true;
            dragon.holdMove = true;
            SpawnFloat("蛟怒——泽国将至！", (Vector3)model.GetEnemyPosition(dragon), Vermilion, 12f);
            sfx?.PlayBossRoar(); // 蛟机制音效（用户 2026-10-09）：蓄水窗怒吼（Boss 通用槽）
            if (bossRoutine != null) StopCoroutine(bossRoutine);
            bossRoutine = StartCoroutine(BossFloodRoutine(dragon));
        }

        /// <summary>
        /// F5.2/F5.3 两幕剧序列：蓄水 5s → 全图洪水（B 方案，水线 4）+ 蛟二阶段水乘 + 召唤暂停 10s
        /// → 潮落 4 档退水（4→3→2→1→0，右→左，步长 = bossEbbSeconds/4）。
        /// 蓄水窗内抢杀/漏过/失败 → 序列自中止（洪水从未发生）；洪水落地后潮落走完不中断——
        /// 蛟死 = 胜利，潮照落塔照干（战场自然复原）；R 回滚/切关 = DoEnterLevel 拆协程 + Reset 清水线。
        /// 永久水路不退（航道制判定与水线无关）→ 鱼虾召唤永远快，水塔必须留守（06 §10.4）。
        /// </summary>
        private IEnumerator BossFloodRoutine(EnemyState dragon)
        {
            // 蓄水预警（表现可降级；计时与文案保留）
            yield return new WaitForSeconds(gameParams.bossChargeSeconds);
            if (dragon.dead || dragon.reached || model.Failed) yield break; // 抢杀/漏过/败：洪水从未发生

            // 第二幕开幕：全图洪水（B 方案无高地豁免）——水线 4 连带 F4.7 全套视觉（整屏汪洋 +
            // 墨路淹没 + 全塔沉默 + 全敌吃水中判定）。蛟一阶段 1.0 悠游 → 二阶段吃满 ×1.5 冲印章；
            // 召唤暂停 10s = 纯抢救窗口（拖塔向右 / 卖 / 水塔应急部署）。
            model.SetWaterLevel(4);
            dragon.invulnerable = false; // i-frame 随蓄水窗结束（洪水落地 = 演出完毕，可打可动）
            dragon.holdMove = false;
            dragon.waterSpeedMult = gameParams.bossPhase2WaterMult;
            model.PauseSummons(gameParams.bossSummonPauseSeconds);
            SpawnFloat("洪水滔天！", (Vector3)model.GetEnemyPosition(dragon) + Vector3.up * 1.2f, Vermilion, 12f);
            sfx?.PlayFloodSurge(); // 蛟机制音效（用户 2026-10-09）：洪水落地

            // 抢救窗口本体（实测修复 2026-10-05：此 yield 漏写——4 与 3 同帧连发，洪水只存在一帧，
            // 地图直接停在大涨位，用户实测「水只涨了一点/洪水没发生」）。洪水全淹保持 = 召唤暂停时长
            // （同一窗口双语义，06 §10.4）；潮落首步 = 窗口结束才落地——赶在 20s 淹死线内让右列高地先干。
            yield return new WaitForSeconds(gameParams.bossSummonPauseSeconds);

            // F5.3 潮落 60s 右→左：4 档退水复用 SetWaterLevel 反向档（每档自带右→左扫掠 + 潮名飘字）。
            // （步长再长 = 塔全淹死、再短 = 无消失压力——bossEbbSeconds 拨杆即此张力）。
            float step = gameParams.bossEbbSeconds / 4f;
            sfx?.PlayFloodEbb(); // 蛟机制音效（用户 2026-10-09）：潮落起点（60s 退水一声）
            for (int lvl = 3; lvl >= 0; lvl--)
            {
                model.SetWaterLevel(lvl);
                yield return new WaitForSeconds(step);
            }
            bossRoutine = null;
        }

        /// <summary>
        /// F5.2 枭转阶段（06 §11.4 用户定案）：1/2 血 → 无敌定身 5s（蛟 i-frame ADR-036 同款：
        /// 防抢杀跳戏，命中反馈照放伤害数字不显）→ 全场黑幕转场（fade 0.4 + hold 1 + fade 0.4
        /// ≈ 1.8s——「1-2s 转场演出，非持续状态」：**不停时**，黑幕期间战场照跑（普通敌照走，
        /// 原型备案）→ 幕全黑处换端：port 翻转 + route 重置（GetPort 半程另一端）+ dist 归零 +
        /// 移速 ×1.25（黄底二圈强化）→ 幕退枭从另一端出场（雾中红闪恢复/撞灯再栖）。
        /// 栖灯中触发（灯被吞完才回位 → holdMove 站桩接黑幕）链路自洽无需特判。
        /// 「二圈栖灯触发更频」黄底 = 换端路面位 + 被吃灯不返还的残网——自然涌现，无机制强制。
        /// </summary>
        private void OnOwlPhase2(EnemyState owl)
        {
            // 枭改动（用户 2026-10-09 定案）：转阶段时若正在栖灯 → **直接秒杀当前灯**（不等
            // 倒计时吞完）并继续机制——半血节奏不被栖灯拖住。吞噬走 DevourLamp 原管线
            //（灯消 0 回收 + LampDevoured → 清格/释放视图/飘字全复用）。秒杀后不回原路
            //（holdMove 定身接黑幕；dist 将被换端归零重置——perchReturnDist 不消费）。
            if (owl.perchTower != null)
            {
                var lamp = owl.perchTower;
                owl.perchTower = null;
                model.DevourLamp(lamp);
                // 诊断（用户 2026-10-10 报「半血没秒杀正在啃的灯」）：静态链全对，日志区分
                // 三种实况——秒了/触发时不在啃（时序竞争：啃窗 3×阶数秒外落刀）/上游没触发。
                Debug.Log("[F5.2] 枭转阶段：秒杀栖灯 " + lamp.charId + " L" + lamp.level +
                    " @(" + (int)lamp.position.x + "," + (int)lamp.position.y + ")");
            }
            else
            {
                Debug.Log("[F5.2] 枭转阶段：触发时未栖灯（半血落刀在啃窗外——赶路/回位/未撞灯）");
            }
            owl.invulnerable = true;
            owl.holdMove = true;
            SpawnFloat("枭怒——夜幕将临！", (Vector3)model.GetEnemyPosition(owl), Vermilion, 12f);
            if (bossRoutine != null) StopCoroutine(bossRoutine);
            bossRoutine = StartCoroutine(OwlNightfallRoutine(owl));
        }

        private IEnumerator OwlNightfallRoutine(EnemyState owl)
        {
            yield return new WaitForSeconds(gameParams.bossChargeSeconds); // 蓄水窗（蛟同款 5s）
            if (owl.dead || owl.reached || model.Failed) yield break; // 抢杀/漏过/败：黑幕从未发生

            // 幕起（0.4s 淡入）
            yield return FadeVeil(0f, 1f, 0.4f);
            if (owl.dead || owl.reached || model.Failed)
            {
                yield return FadeVeil(1f, 0f, 0.4f); // 窗口内败退：幕落了事
                bossRoutine = null;
                yield break;
            }

            // 幕全黑：换端重走（幕遮瞬移——玩家只见「另一端冒出」）
            owl.spawnPort = 1 - owl.spawnPort;
            owl.route = model.Paths != null ? model.Paths.GetPort(owl.lane, owl.spawnPort) : owl.route;
            owl.dist = 0;
            owl.speed *= gameParams.owlPhase2SpeedMult; // 黄底 +25%
            owl.invulnerable = false; // 幕中解除：幕退即可打（雾中红闪/入光现形照常）
            owl.holdMove = false;

            yield return new WaitForSeconds(1f); // 幕保持一拍（「另一端冒出」前摇感）

            // 幕落（0.4s 淡出）——枭已在新端走出
            SpawnFloat("夜幕重临", (Vector3)model.GetEnemyPosition(owl) + Vector3.up * 1.2f, Vermilion, 12f);
            yield return FadeVeil(1f, 0f, 0.4f);
            bossRoutine = null;
        }

        /// <summary>黑幕淡入/淡出（秒级插值；Init 未建则跳过——防御）。</summary>
        private IEnumerator FadeVeil(float from, float to, float seconds)
        {
            if (blackVeil == null) yield break;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                blackVeil.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            blackVeil.color = new Color(0f, 0f, 0f, to);
        }

        /// <summary>
        /// F7.5② 二圈顺延（模型侧安全网——06 §12.5）：Boss 带未兑换额外圈到终点 → 重置回起点不判败。
        /// **正常路径**由转阶段收尾的路由重置承担（LeiResonanceRoutine 末尾并清零 lapsLeft）；
        /// 本口只兜「收尾重置前就到终点」的时序边缘（两条路都消耗 lapsLeft → 必不双圈）。
        /// </summary>
        private void OnBossLooped(EnemyState e)
        {
            if (e == null) return;
            e.chargeWalked = 0; // 按模型侧已清零口径同步蓄力里程（防御：模型口也清零）
            // F5.1（ch5 傩面）：到出口扣印 → 回起点重走（无限圈）；与麟的二圈顺延共用一个事件口
            if (e.nuoMask)
            {
                SpawnFloat("傩面回环", (Vector3)model.GetEnemyPosition(e) + Vector3.up * 1.2f, Vermilion, 12f);
                return;
            }
            Debug.Log("[F7.5] 麟第二圈开始（到终点顺延——安全网路径）");
        }

        /// <summary>
        /// F6.2 麟转阶段（06 §12.5 用户定案）：1/2 血 → 无敌定身 5s（蛟 ADR-036 / 枭先例 i-frame：
        /// 防抢杀跳戏，命中反馈照放、伤害数字不显）→ 全场红闪 3s → 火山共振：**岩浆雨**
        ///（随机 6-8 格建造格变灰烬，塔按规则结算——与 F6.1 践踏同单格口径）+ **幼蜥群 ×6**
        ///（出怪口入场、不掉墨）→ **二圈重置**（F7.5②，回环起点重走——见下）。
        ///
        /// i-frame 覆盖整段演出（5s 蓄水 + 3s 红闪）至雨落——**非只 5s**：裸字面「定身 5s」下
        /// 玩家可在红闪窗击杀麟 → 雨不发生 = 跳戏（ADR-036 同一防跳戏纪律；蛟「洪水落地双解除」
        /// 因洪水恰在 5s 落地才与之重合，麟多出的红闪段同理并入演出窗）。
        /// F7.5②（用户定案 2026-10-19）：雨落只解 i-frame（可打），**定身留到收尾** = 站桩看完
        /// 召唤入场 → 路由重置回环起点重走第二圈（漏过判败点顺延到二圈终点）。
        /// </summary>
        private void OnLeiPhase2(EnemyState lei)
        {
            lei.invulnerable = true;
            lei.holdMove = true;
            lei.lapsLeft = 1; // F7.5②：发放一枚额外圈（转阶段收尾重置时兑换清零；模型侧到终点顺延为安全网）
            SpawnFloat("麟怒——火山共振！", (Vector3)model.GetEnemyPosition(lei), Vermilion, 12f);
            sfx?.PlayBossRoar(); // Boss 通用槽（蛟蓄水窗同槽）：共振怒吼
            if (bossRoutine != null) StopCoroutine(bossRoutine);
            bossRoutine = StartCoroutine(LeiResonanceRoutine(lei));
        }

        private IEnumerator LeiResonanceRoutine(EnemyState lei)
        {
            float charge = gameParams != null ? gameParams.bossChargeSeconds : 5f;
            float flash = gameParams != null ? gameParams.leiResonanceSeconds : 3f;
            float peak = gameParams != null ? gameParams.leiResonanceAlpha : 0.34f;

            // 第一幕：无敌定身蓄力（蛟/枭同款 bossChargeSeconds）
            yield return new WaitForSeconds(charge);
            if (lei.dead || lei.reached || model.Failed) yield break; // 抢杀/漏过/败：共振从未发生

            // 第二幕：全场红闪（fade 0.4 淡入 → 保持到总时长；撤离窗口 = 红幕段）
            float fadeIn = Mathf.Min(0.4f, flash);
            yield return FadeRedVeil(0f, peak, fadeIn, Mathf.Max(0f, flash - fadeIn));
            if (lei.dead || lei.reached || model.Failed)
            {
                yield return FadeRedVeil(peak, 0f, 0.4f, 0f); // 窗口内败退：幕落了事（雨不发生）
                bossRoutine = null;
                yield break;
            }

            // 第三幕：共振岩浆雨——随机 6-8 格建造格变灰烬（塔按规则结算：普通塔毁·零回收；
            // 火/炮幸存 = 死灰复燃；拿起中骑墙同毁）。落点走 F2.1 种子流独立流号（回滚重放一致）。
            var rainCells = PickResonanceCells();
            for (int i = 0; i < rainCells.Count; i++)
            {
                var cell = grid != null ? grid.Get(rainCells[i].x, rainCells[i].y) : null;
                if (cell != null) SettleBuildCell(cell);
            }
            Debug.Log("[F6.2] 麟共振岩浆雨 " + rainCells.Count + " 格 -> " + FormatCells(rainCells));
            SpawnFloat("岩浆雨", (Vector3)model.GetEnemyPosition(lei) + Vector3.up * 1.2f, Vermilion, 12f);
            sfx?.PlayLavaLand(); // 落地音（喷发落地同槽；整场一声防叠爆音）

            // 雨落 = 演出完毕：解 i-frame（可打）+ **二阶段**（06 §12.5 / F6.3）——
            // 践踏圈 1.5 → 2.5（改敌字段：提示圈每帧读敌 trampleRadius = 自动同步放大）+ 移速 ×1.25
            //（蛟「洪水落地设 waterSpeedMult」同律：演出毕即强化）。
            // F7.5②：holdMove **不在此解除**（用户定案 2026-10-19）——定身留到收尾，麟站桩看完
            // 幼蜥入场，再路由重置回环起点重走第二圈；二圈开始即恢复可打（i-frame 只覆盖演出）。
            lei.invulnerable = false;
            lei.trampleRadius = gameParams != null ? gameParams.leiPhase2TrampleRadius : 2.5;
            lei.speed *= gameParams != null ? gameParams.leiPhase2SpeedMult : 1.25;
            Debug.Log("[F6.3] 麟二阶段：践踏半径 " + lei.trampleRadius.ToString("0.##") +
                " / 移速 ×" + (gameParams != null ? gameParams.leiPhase2SpeedMult : 1.25));
            yield return FadeRedVeil(peak, 0f, 0.6f, 0f); // 幕落

            // 幼蜥群从出怪口入场（不掉墨）；时间错开防叠字（同波表出怪间隔口径）。
            // F7.5④（用户定案 2026-10-19）「召唤物弱化」：蛟召鱼 24 血 / 酋长召野人 150 血——
            // **召唤物从来是弱怪，不是第二个 Boss**。三处弱化：血 ×leiSummonHpMult（0.35 ≈ 227）、
            // **不免疫燃烧**（幼体火鳞未成——父表 Chapter4Lizard 不动，只覆盖召唤体）、速 ×leiSummonSpeedMult。
            int cnt = gameParams != null ? gameParams.leiSummonCount : 6;
            string id = gameParams != null ? gameParams.leiSummonChar : "火蜥";
            float gap = gameParams != null ? gameParams.leiSummonGapSeconds : 0.25f;
            double hpMult = gameParams != null ? gameParams.leiSummonHpMult : 0.35;
            double summonSpeedMult = gameParams != null ? gameParams.leiSummonSpeedMult : 1.3;
            var st = (enemyTable != null && !string.IsNullOrEmpty(id)) ? enemyTable.Find(id) : null;
            if (st == null)
            {
                if (cnt > 0) Debug.LogError("[F6.2] 共振召唤物不在敌表：" + id);
            }
            else
            {
                for (int k = 0; k < cnt; k++)
                {
                    if (k > 0 && gap > 0f) yield return new WaitForSeconds(gap);
                    if (model.Failed || won) break; // 败/胜即停（防写真场）
                    var spawn = model.SpawnEnemy(st, null, true, 0, hpMult); // noInkDrop = 不掉墨（蛟召唤纪律同款）
                    if (spawn != null)
                    {
                        spawn.burnImmune = false;      // F7.5④：幼蜥不免疫燃烧（父表不动——只覆盖召唤体）
                        spawn.speed *= summonSpeedMult; // F7.5④：幼蜥更灵活
                    }
                }
                Debug.Log("[F6.2] 共振召唤 " + id + " x" + cnt + "（出怪口入场、不掉墨、血 ×" +
                    hpMult.ToString("0.##") + " = " + (st.hp * hpMult).ToString("0") +
                    "、不免疫燃烧、速 ×" + summonSpeedMult.ToString("0.##") + "）");
            }

            // F7.5② 二圈（用户定案 2026-10-19）：演出收尾 = 麟路由重置回**出怪口**重走第二圈
            //（蛟潮落路由重置同构；二阶段参数保留——速 ×1.25 / 践踏圈 2.5 / 蓄力照旧）。
            // lapsLeft 在此**兑换**（清零）→ 模型侧「到终点顺延」只作安全网，必不双圈。
            // 收尾才解除定身（站桩看完召唤入场）；蓄力挂账同时清零（防重置后立刻补一次重踏）。
            lei.dist = 0;
            lei.chargeWalked = 0;
            lei.chargeRemaining = 0;
            lei.lapsLeft = 0;
            lei.holdMove = false;
            Debug.Log("[F7.5] 麟第二圈开始");
            bossRoutine = null;
        }

        /// <summary>
        /// F6.2 共振岩浆雨落点（06 §12.5）：**同走 F2.1 种子流**（eruptionSeed 与独立流号 4/5
        /// 与关锚混种 = 关内固定 -> R 回滚/读档重放同雨同格，防回滚刷好运）。
        /// 落点池 = 喷射随机池的**建造格子集**（有格 = 能变灰烬；路面/带宽空地无格可灰 ->
        /// 不入雨——与 F6.1 践踏同目标集、与 F2.3「路径格不毁」同源）。数量 6-8 黄底随机、无放回。
        /// </summary>
        private List<Vector2Int> PickResonanceCells()
        {
            var pool = new List<Vector2Int>();
            if (eruptionPool != null && grid != null)
                foreach (var q in eruptionPool)
                    if (grid.Get(q.x, q.y) != null) pool.Add(q);
            var picks = new List<Vector2Int>();
            if (pool.Count == 0) return picks;

            int min = gameParams != null ? gameParams.leiRainMin : 6;
            int max = gameParams != null ? gameParams.leiRainMax : 8;
            if (max <= 0) return picks;

            int anchor = level != null ? level.waves.Count : 0; // 关锚（雨不在波钟轴上）
            int cnt = new System.Random(EruptionSeqSeed(4, anchor)).Next(Mathf.Max(0, min), max + 1);
            cnt = Mathf.Min(cnt, pool.Count);
            var pickRng = new System.Random(EruptionSeqSeed(5, anchor));
            for (int k = 0; k < cnt; k++)
            {
                int idx = pickRng.Next(pool.Count);
                picks.Add(pool[idx]);
                pool[idx] = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1); // 无放回：本场 N 格不重复（同 F2.1 口径）
            }
            return picks;
        }

        /// <summary>红幕淡入/保持（fade 秒 from->to，再 hold 秒保持 to；收幕传 to=0）。</summary>
        private IEnumerator FadeRedVeil(float from, float to, float fadeSeconds, float holdSeconds)
        {
            if (redVeil == null) yield break;
            float t = 0f;
            float dur = Mathf.Max(0.0001f, fadeSeconds);
            while (t < dur)
            {
                t += Time.deltaTime;
                SetRedVeilAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            SetRedVeilAlpha(to);
            if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);
        }

        /// <summary>
        /// BGM/枭氛围轮询（用户 2026-10-09：Boss 出场切 bossBgm + 枭存活期间每 owlFlickerSeconds
        /// 秒黑闪 2 下）。Boss 判定 = phase2HpRatio > 0（蛟/枭通用；ch1 无 Boss 零触发）；
        /// 退场（死/漏，activeEnemies.Remove 后）切回章默认轨 + 停黑闪。0.1s 粒度轮询幂等；
        /// bgmOn 存档开关关闭则只走黑闪不走切轨。
        /// </summary>
        private void PollBossStage()
        {
            if (model == null) return;
            EnemyState boss = null;
            foreach (var kv in activeEnemies)
                if (!kv.Key.dead && !kv.Key.reached && kv.Key.phase2HpRatio > 0) { boss = kv.Key; break; }
            bool on = boss != null;
            if (on == bossBgmOn) return; // 幂等（标记不变不动作）
            bossBgmOn = on;
            var bgmChapter = level != null && level.chapter != null ? level.chapter : chapter;

            if (on)
            {
                if (sfx != null && (save == null || (save.settings != null && save.settings.bgmOn)))
                    sfx.PlayBgm(bgmChapter != null ? bgmChapter.bossBgm : null);
                if (boss.charId == "枭" && flickerRoutine == null &&
                    gameParams != null && gameParams.owlFlickerSeconds > 0)
                    flickerRoutine = StartCoroutine(OwlFlickerRoutine()); // 枭专属氛围（蛟不闪）
            }
            else
            {
                if (flickerRoutine != null) { StopCoroutine(flickerRoutine); flickerRoutine = null; }
                if (sfx != null && (save == null || (save.settings != null && save.settings.bgmOn)))
                    sfx.PlayBgm(bgmChapter != null ? bgmChapter.bgm : null); // 切回章默认（Boss 退场）
            }
        }

        /// <summary>
        /// 枭氛围黑闪（用户 2026-10-09）：存活期间每 owlFlickerSeconds 秒闪 2 下（峰值
        /// owlFlickerAlpha、每闪 0.12s、双闪间隔 0.18s）。转阶段黑幕演出（bossRoutine 非空）
        /// 在跑时本轮让位跳过——BlackVeil 单幕不双写；协程由 PollBossStage 幂等启停。
        /// </summary>
        private IEnumerator OwlFlickerRoutine()
        {
            var wait = new WaitForSeconds((float)gameParams.owlFlickerSeconds);
            while (true)
            {
                yield return wait;
                if (bossRoutine != null) continue; // 转阶段黑幕演出让位（BlackVeil 单写者）
                yield return FlashVeil();
                yield return new WaitForSeconds(0.18f);
                if (bossRoutine != null) continue;
                yield return FlashVeil();
            }
        }

        /// <summary>单次黑闪：峰值 alpha 保持 0.12s → 归零（快而沉的夜枭节奏）。</summary>
        private IEnumerator FlashVeil()
        {
            if (blackVeil == null || gameParams == null) yield break;
            blackVeil.color = new Color(0f, 0f, 0f, gameParams.owlFlickerAlpha);
            yield return new WaitForSeconds(0.12f);
            if (bossRoutine == null) blackVeil.color = new Color(0f, 0f, 0f, 0f); // 转阶段刚抢幕则不清（幕接管）
        }

        private void OnEnemyReached(EnemyState e)
        {
            sfx?.PlayLeak(); // 漏怪扣印（ADR-083）
            LogBossDuration(e, "漏过"); // F7.1 探针：Boss 战时长（漏过 = 败，时长照样记）
            // F4.4（ch5 小鬼）：墨漏播报——朱字「−3 墨」，与掉印区分（印在 HUD 上、墨在手牌条上）
            if (e != null && e.inkLeak > 0)
                SpawnFloat("−" + e.inkLeak.ToString("0") + " 墨",
                    (Vector3)model.GetEnemyPosition(e) + Vector3.up * 0.5f, Vermilion, 11f);
            OnEnemyResolved(e); // 漏过也是「已结算」（波清判定按场上无该波敌人）
        }

        /// <summary>
        /// F7.1 手测探针（用户 2026-10-18 点题）：**场末一行体检**——灰烬格占比（② 曲线
        /// 「L5 场末 ≤1/3」判定）+ 塔数/均阶（④ 曲线「场末期望」比对，如 ch1-L1 = 8 塔均 L1.5）
        /// + 余墨。分母 = 章图建造格总数（有格非水 = 灰烬可能落点；路径/图外不算）。
        /// 一关一次（tuningReportDone 幂等——RefreshHud 失败链每帧重刷会重复调）。纯 Console。
        /// </summary>
        private void LogTuningReport(string tag)
        {
            if (tuningReportDone || model == null || level == null) return;
            tuningReportDone = true;

            int total = 0, ash = 0, green = 0;
            if (grid != null)
                foreach (var c in grid.All)
                {
                    if (c.IsWater) continue;
                    total++;
                    if (c.TileState == TileState.Ash) ash++;
                    else if (c.TileState == TileState.Green) green++;
                }
            int towers = model.Towers.Count;
            double lvSum = 0;
            for (int i = 0; i < model.Towers.Count; i++) lvSum += model.Towers[i].level;
            Debug.Log(string.Format(
                "[F7.1] 场末体检（{0}）「{1}」index {2}：灰烬 {3}/{4} 格 = {5:0.0}% · 绿格 {6} · 塔 {7} 均 L{8:0.0} · 余墨 {9:0}",
                tag, level != null ? level.title : "?", levelIndex, ash, total,
                total > 0 ? 100.0 * ash / total : 0.0, green, towers,
                towers > 0 ? lvSum / towers : 0.0, model.Ink));
        }

        /// <summary>F7.1 探针：Boss 战时长（战斗钟 = waveClock——暂停/胜负冻结，对齐 ③「≤3 分钟」体感锚）。</summary>
        private void LogBossDuration(EnemyState e, string how)
        {
            if (e == null || !e.isBoss || bossSpawnClock < 0) return;
            Debug.Log(string.Format("[F7.1] Boss 战时长：{0}（{1}）{2:0.0} 秒（战斗钟；体感锚 ≤3 分钟）",
                e.charId, how, waveClock - bossSpawnClock));
            bossSpawnClock = -1;
        }

        private void OnTowerFired(TowerState tower, EnemyState target)
        {
            sfx?.PlayAttack(tower.charId); // 开火音（远程=发射瞬间，近战=挥击；ADR-028）
            if (tower.ranged) return; // 远程：伤害与受击反馈在弹道命中时
            if (activeEnemies.TryGetValue(target, out var view)) view.FlashHit();
            sfx?.PlayHit(); // 近战命中即受击（与挥击叠放，嫌叠可留空其一槽位）
            // 0.4 修正：伤害数字撤出此处，改由 OnEnemyDamaged 逐敌驱动——旧版只飘主目标，
            // 棍圆/戟扇溅射在暗处扣血零视觉 = 用户实录「单体」观感的根因。
            // i-frame 零伤无事件不跳字口径不变（F5.2 防「跳字不掉血」误导）。
        }

        /// <summary>
        /// 0.4 修正（ch4 前置，用户实录「棍/戟单体」根因）：逐敌受击反馈——Damage 落血即来，
        /// 矛矩形/棍圆/戟扇的**每个溅射目标**都闪白+跳字。机制自 ch1 v3.0-A3（56c8198，ADR-019）
        /// 起即正确，缺的从来是反馈层：旧飘字只挂 OnTowerFired/OnProjectileHit 的主目标。
        /// </summary>
        private void OnEnemyDamaged(EnemyState e, double dmg, TowerState attacker)
        {
            if (activeEnemies.TryGetValue(e, out var view)) view.FlashHit();
            SpawnFloat("-" + dmg.ToString("0"), (Vector3)model.GetEnemyPosition(e),
                HexUtil.ToColor("#26231F"), 10f); // 战斗数值固定小号（沿旧主目标口径）
        }

        private void OnProjectileSpawned(ProjectileState p)
        {
            if (projectileViews == null) return;
            var view = projectileViews.Get(projectileTemplate);
            view.Show(p);
            activeProjectiles[p] = view;
        }

        private void OnProjectileHit(ProjectileState p)
        {
            if (activeEnemies.TryGetValue(p.target, out var view)) view.FlashHit();
            sfx?.PlayHit(); // 弹道命中受击音（ADR-028）
            // 0.4 修正：伤害数字撤出此处改由 OnEnemyDamaged 逐敌驱动——矛矩形溅射同样可见了；
            // i-frame 零伤无事件不跳字口径不变。
        }

        private void OnProjectileRemoved(ProjectileState p)
        {
            if (activeProjectiles.TryGetValue(p, out var view))
            {
                view.ClearState();
                projectileViews.Release(view);
                activeProjectiles.Remove(p);
            }
        }

        // ============ F3.2（ch5 雷）：链电电弧 ============

        /// <summary>链电表现（可砍档）：两点之间一条 0.12s 青白细光，随后自毁（每次链跳一条，1.6s 一发至多 3 条）。</summary>
        private void OnChainLinked(Vector2 from, Vector2 to)
        {
            if (whiteSprite == null) return;
            StartCoroutine(ChainArcRoutine(from, to));
        }

        private System.Collections.IEnumerator ChainArcRoutine(Vector2 a, Vector2 b)
        {
            var go = new GameObject("ChainArc", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = new Color(0.72f, 0.86f, 1f, 0.9f); // 电青白
            Vector2 d = b - a;
            float len = d.magnitude;
            go.transform.position = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, -1.45f);
            go.transform.localScale = new Vector3(Mathf.Max(0.05f, len), 0.07f, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            float t = 0f;
            while (t < 0.12f) { t += Time.deltaTime; yield return null; }
            Destroy(go);
        }

        private readonly Dictionary<EnemyState, EnemyView> activeEnemies = new Dictionary<EnemyState, EnemyView>();
        private readonly Dictionary<ProjectileState, ProjectileView> activeProjectiles = new Dictionary<ProjectileState, ProjectileView>();

        public void ReleaseEnemy(EnemyView view)
        {
            if (view.State != null) activeEnemies.Remove(view.State);
            view.ClearState();
            enemyViews.Release(view);
        }

        public void ReleaseFloat(FloatingText f)
        {
            if (floatViews != null) floatViews.Release(f);
        }

        private void SpawnFloat(string text, Vector3 pos, Color color, float size = 0f)
        {
            if (floatViews == null) return;
            // F1.1 v2（ch3 幽夜，用户验收「UI 不被遮挡」）：飘字 z 统一钳到雾层（-0.2）与敌（-0.4）
            // 之前——夜色遮罩不暗化任何提示/数值字（波清/潮名等调用点原本 z=0 在雾后）。
            // 顺带修复既有小瑕疵：飘字不再被敌/塔遮挡（ch1/ch2 微变，改善型——文字本就不该被角色盖）。
            pos.z = Mathf.Min(pos.z, -0.45f);
            // size=0（缺省）= 交互文案，字号走 GameParams.toastFontSize（用户 2026-10-02「小很多」，14→7）；
            // 战斗数值（伤害/掉墨/激怒）由调用方显式传固定小号（10/12）
            if (size <= 0f) size = gameParams != null ? gameParams.toastFontSize : 7f;
            var f = floatViews.Get(floatTemplate);
            f.Show(text, pos, color, size);
        }

        private static Vector3 CellTop(GridCell cell) => cell.transform.position + Vector3.up * 0.5f;

        // ============ 进关 / 推进 / 回滚（v3.0-D，ADR-023） ============

        /// <summary>
        /// 进关入口：L3 起（index ≥ 3——L0 教学关占位 0 后整体 +1，ADR-030）先弹构筑面板
        /// （关前/失败重试均可改构筑，03 §4.6），确认后落场；L0-L2 直进（自动构筑 = 已解锁全部）。
        /// field = 带进场的快照：通关推进 = 上关场末；回滚 = 本关开始快照；null = 清场新开（章首/测试跳关）。
        /// </summary>
        private void RequestEnterLevel(int index, LevelStartSnapshotData field, bool advance = false)
        {
            if (model == null || levels == null || levels.Length == 0) return;
            index = Mathf.Clamp(index, 0, levels.Length - 1);

            // 暂停复位（用户 2026-10-12）：暂停中回滚/切关 = 新关不残留暂停态（veil 一并收）。
            if (paused) ResumeGame();
            // F2.2（ADR-054）：红铲模式遇换关/回滚/开构筑面板 = 状态清理（全额退款退出，非取消键）
            CancelRedShovel(true);

            // —— 奖励解锁 ——
            // 普通模式（ADR-023）：进关意图确定即入包 levels[0..index].rewardCards 并集（幂等）。
            // 必须在构筑面板之前——否则首次进 L4 时棍/戟不在面板上（7选5 教学失效）。
            // 困难模式（用户 2026-10-26 改）：6 阶解锁跟「普通模式上一关解锁的卡牌」→ levels[0..index-1]。
            bool tier6Changed = false;
            if (!hardMode)
            {
                for (int i = 0; i <= index; i++)
                    if (levels[i] != null && levels[i].rewardCards != null)
                        foreach (var card in levels[i].rewardCards) backpack.AddCard(card);
            }
            else if (save != null)
            {
                if (save.tier6Unlocked == null) save.tier6Unlocked = new List<string>();
                for (int i = 0; i < index; i++)
                    if (levels[i] != null && levels[i].rewardCards != null)
                        foreach (var card in levels[i].rewardCards)
                        {
                            if (save.tier6Unlocked.Contains(card)) continue;
                            save.tier6Unlocked.Add(card);
                            tier6Changed = true;
                            Debug.Log("[6阶] 困难模式解锁：" + card);
                        }
            }
            if (tier6Changed) SaveSystem.Write(save); // 解锁即时落盘（不等下次自然存档）

            // P5（用户 2026-10-26）：通关奖励卡改在「点下一关之后」逐张展示（每张卡一屏），
            // 不再在胜利帧弹出（避免压住左下「回滚」按钮与结果按钮）。
            if (advance && pendingRewardCards != null && pendingRewardCards.Count > 0)
            {
                HideResultButtons();
                hud?.HideBanner();
                SetPlayHudVisible(false);
                clearQueue.Clear();
                clearQueue.AddRange(pendingRewardCards);
                clearQueueIndex = 0;
                clearQueueTier6 = hardMode;
                clearQueueLevel = pendingRewardLevel;
                clearQueueNext = index;
                clearQueueField = field;
                pendingRewardCards = null;
                pendingRewardLevel = -1;
                ShowNextClearCard();
                return;
            }
            // 非推进入口：只有「本关回滚重打」（index == 待展示关）保留奖励，换到别的关则丢弃
            if (!advance && index != pendingRewardLevel)
            {
                pendingRewardCards = null;
                pendingRewardLevel = -1;
            }

            // P6（用户 2026-10-25）：日记画面——进关前、背包之前；diaryPages 空 = 跳过本画面。
            // 重打同样显示（右上「跳过」可略过）；章首关由策划在 LevelConfig 里填页。
            var diaryPages = levels[index] != null ? levels[index].diaryPages : null;
            if (diaryPanel != null && diaryPages != null && diaryPages.Count > 0)
            {
                pendingField = field;
                HideResultButtons();
                hud?.HideBanner();
                SetPlayHudVisible(false);
                float speed = gameParams != null ? gameParams.diaryCharSeconds : 0.035f;
                diaryPanel.Show(levels[index].title, diaryPages, speed,
                    () => ContinueEnterLevel(index, field));
                return;
            }

            ContinueEnterLevel(index, field);
        }

        /// <summary>P5：逐张展示通关奖励卡（每张一屏）；队列走完 → 继续进关流程。</summary>
        private void ShowNextClearCard()
        {
            if (clearQueueIndex >= clearQueue.Count)
            {
                int idx = clearQueueNext;
                var f = clearQueueField;
                clearQueueField = null;
                ContinueEnterLevel(idx, f);
                return;
            }
            var lvl = clearQueueLevel >= 0 && clearQueueLevel < levels.Length ? levels[clearQueueLevel] : null;
            var one = new List<string> { clearQueue[clearQueueIndex] };
            clearPanel.Show(one, clearQueueTier6,
                lvl != null ? lvl.clearTitle : null,
                lvl != null ? lvl.clearDesc : null,
                () => { clearQueueIndex++; ShowNextClearCard(); });
        }

        /// <summary>进关期间隐藏常驻「回滚」钮（它在 z=-1.7，会盖在结算/日记面板之上）。</summary>
        private void SetPlayHudVisible(bool visible)
        {
            if (retryBtnRoot != null) retryBtnRoot.SetActive(visible);
        }

        /// <summary>P6 拆段：日记之后（或没有日记时）的进关续段——构筑面板 → 落场。</summary>
        private void ContinueEnterLevel(int index, LevelStartSnapshotData field)
        {
            // P（用户 2026-10-26）：背包解锁后（曾进入过 L3），回到前几关（L1/L2）也弹构筑面板；L0 教学不弹。
            bool backpackUnlocked = save != null && save.maxLevelUnlocked >= 4;
            if (index > 0 && (index >= 3 || backpackUnlocked) && deckPanel != null)
            {
                pendingField = field;
                // P1（08 §7，用户实测 2026-10-05）：面板一开就收结果按钮——原只靠 DoEnterLevel 收
                // （等选卡确认才执行），面板开着期间「下一关/回滚」还漂在场上可点，与选卡点击冲突。
                // 此处收口覆盖四个入口：结果大按钮×2 / N 键 / R 键（都汇于本方法）；面板打开期间
                // Update 的 panelOpen 早退先于失败态 0.1s 重刷，不会被顶回来。
                HideResultButtons();
                hud?.HideBanner(); // 用户 2026-10-25：进背包界面时隐藏中间红色「…下一关」横幅（uGUI 盖在世界 UI 上）
                // 面板列出「全部已拥有卡」（用户 2026-10-26：背包全局通用）——
                // 本关池里的卡排前面（设计顺序），其余按解锁顺序补后面；第一次打某关时
                // 只有那时已解锁的卡，天然等价于旧的「关卡池」口径。
                var cards = new List<string>();
                foreach (var c in levels[index].poolChars)
                    if (backpack.OwnedCards.Contains(c)) cards.Add(c);
                foreach (var c in backpack.OwnedCards)
                    if (!cards.Contains(c) && towerPool != null && towerPool.Find(c) != null) cards.Add(c);
                deckPanel.Show(cards, backpack.Deck, towerPool, chosen =>
                {
                    backpack.SetDeck(chosen);
                    var f = pendingField;
                    pendingField = null;
                    DoEnterLevel(index, f);
                });
                return;
            }
            DoEnterLevel(index, field);
        }

        /// <summary>落场（原 Restart 全量重建 + 快照/背包/存档三件事）。面板确认后的唯一路径。</summary>
        private void DoEnterLevel(int index, LevelStartSnapshotData field)
        {
            levelIndex = Mathf.Clamp(index, 0, levels.Length - 1);
            level = levels[levelIndex];
            SetPlayHudVisible(true); // P5：结算/日记期间隐藏的常驻回滚钮，落场时恢复

            foreach (var r in spawnRoutines) if (r != null) StopCoroutine(r);
            spawnRoutines.Clear();
            if (bossRoutine != null) { StopCoroutine(bossRoutine); bossRoutine = null; } // F5：蛟两幕剧随场拆（防陈旧步进器改写新场水线）
            if (blackVeil != null) blackVeil.color = new Color(0f, 0f, 0f, 0f); // F5.2 枭黑幕随场重置（防幕中拆场半黑残留）
            if (redVeil != null) SetRedVeilAlpha(0f); // F6.2 红幕随场重置（防幕中拆场半红残留）
            if (flickerRoutine != null) { StopCoroutine(flickerRoutine); flickerRoutine = null; } // 枭氛围黑闪随场拆
            bossBgmOn = false; // BGM 轮询标记复位（Boss 轮询重新发现才切轨）

            // BGM（用户 2026-10-09：每章专属）：进关切章默认轨（Boss 轮询接管切轨/切回）；
            // level.chapter 空 = 沿用场景 chapter 引用（LevelConfig 同回退口径）；bgmOn 存档开关。
            var bgmChapter = level != null && level.chapter != null ? level.chapter : chapter;
            if (sfx != null && (save == null || (save.settings != null && save.settings.bgmOn)))
                sfx.PlayBgm(bgmChapter != null ? bgmChapter.bgm : null);

            // 场重建：开垦中格子全毁（快照按完成态恢复）→ 挖土循环计数不可信，强停归零（ADR-029）
            sfx?.FarmReset();

            // 塔视图必须先于 grid.Build 释放（视图挂在格子下，格子销毁会连带销毁）
            foreach (var view in towerViewMap.Values) towerViews.Release(view);
            towerViewMap.Clear();
            ClearPickupState();
            selectedCardIndex = -1;

            // F1.2（ADR-053）：初始墨唯一来源 = 玩家存档（level.startInk 废弃——不再读取）。
            model.Reset(CurrentInitialInk, level.startLives);
            // P1.1（用户 2026-10-24）：困难模式全体敌人血量 × GameParams.hardEnemyHpMult（简单 = 1）
            model.EnemyHpMult = hardMode ? (gameParams != null ? gameParams.hardEnemyHpMult : 2.0) : 1.0;

            foreach (var view in activeEnemies.Values)
            {
                view.ClearState();
                enemyViews.Release(view);
            }
            activeEnemies.Clear();

            // F6（ch5 傩神）：本体引用与三面随行随场清（黑幕协程由 StopAllCoroutines 兜底见下）
            shenBoss = null;
            nuoShenArriving = false;
            ClearShenMasks();

            // F1（docs/14）：拖拽态随场清（防换关残留虚影/卡索引）
            if (dragGhost != null) { towerViews.Release(dragGhost); dragGhost = null; }
            if (rangeIndicator != null) rangeIndicator.Hide();
            handDragging = false;
            draggingCardIndex = -1;
            pressedCardIndex = -1;

            foreach (var view in activeProjectiles.Values)
            {
                view.ClearState();
                projectileViews.Release(view);
            }
            activeProjectiles.Clear();

            waveRuntimes.Clear();
            enemyWave.Clear();
            waveClock = 0;
            bossSpawnClock = -1;    // F7.1 探针随场清
            tuningReportDone = false;
            nextWaveIndex = 0;
            timelineShift = 0;
            won = false;

            // 章图（ADR-021 章内 5 关同图；F4.3/ADR-031 平铺跨章：关卡章引用优先，空走场景引用）+ 快照格子状态（ADR-018 垦地保留）
            var targetChapter = ChapterOf(level);
            bool firstBoot = activeChapter == null;                    // F2.4：首启不算换章（读档续玩要保留章内造格）
            bool chapterSwitched = activeChapter != targetChapter;     // F4.3：章界检测（首进 activeChapter=null 也算切换——开场横幅）
            bool chapterBoundary = chapterSwitched && !firstBoot;       // F2.4：真换章 = 章界清场口径
            activeChapter = targetChapter;
            grid.Build(targetChapter);
            if (path != null) path.BuildLanes(targetChapter); // F1.2：路线随章重建（ch1 蛇形 = 数据缺省值，观感不变）
            EnsureTempCell(); // 临时格（用户 2026-10-23）：左下空地格（塔放上去不攻击/不产墨）

            // F2.4（ADR-054）红铲造格：章界清空 / 章内回放（快照优先 = R 回滚；否则读章内状态）。
            // 顺序在 grid.Build 之后、快照格回放之前——快照里的新格必须先能 Get 到。
            if (chapterBoundary && save != null && CurrentChapterState != null)
            {
                CurrentChapterState.extraCells.Clear();
                CurrentChapterState.redShovelBought = 0;
            }
            extraCells.Clear();
            // 读档续玩（resumingSave）= 地皮按章内状态；R 回滚 / 章内推进 = 按关开始快照
            var extraSrc = (field != null && !resumingSave)
                ? field.extraCells
                : (save != null && CurrentChapterState != null ? CurrentChapterState.extraCells : null);
            if (extraSrc != null)
                foreach (var c in extraSrc)
                    if (grid.AddCell(c.x, c.y) != null) extraCells.Add(new Vector2Int(c.x, c.y));
            redShovelBought = (field != null && !resumingSave)
                ? field.redShovelBought
                : (save != null && CurrentChapterState != null ? CurrentChapterState.redShovelBought : 0);
            // 家（用户 2026-10-23）：章内跨关累积、章界清零（章级状态，不进关开始快照）
            if (chapterBoundary && save != null && CurrentChapterState != null)
                CurrentChapterState.homeLevel = 0;
            homeLevel = save != null && CurrentChapterState != null
                ? Mathf.Clamp(CurrentChapterState.homeLevel, 0, gameParams != null ? gameParams.homeMaxLevel : 5)
                : 0;
            homeInkTimer = 0;
            RefreshHome(); // 定位到实路终点 + 刷新文字
            resumingSave = false;
            redShovelMode = false;   // 模式不跨关（已扣墨随退款结算在 RequestEnterLevel 完成）
            redShovelPaidInk = 0;
            DestroyShovelField();
            ComputeShovelBounds(targetChapter);
            // F1.1+F1.2（ch3 幽夜，06 §11.2）：迷雾渲染 + LightSystem 交战谓词接线。光照集 =
            // 家灯（弧长中点家 + 关级 homeLampRadius 收缩曲线）∪ 灯塔当前 range——进关先落家灯，
            // 放/卖/升/移灯由 LightsChanged 事件重挖（OnLightsChanged）。ch1/ch2 fogEnabled=false
            // → SetFog(false) 谓词恒真短路 + 不建层（铁律零变化）。
            if (targetChapter != null && targetChapter.fogEnabled)
            {
                model.SetFog(true, path != null ? path.HomePosition : Vector2.zero, level.homeLampRadius);
                if (grid != null) grid.SetFogLights(model.GetLights());
            }
            else model.SetFog(false, Vector2.zero, 0);

            // F2.1（ch4 火山，06 §12.2 / ADR-047 喷射幅）：喷射引擎随场复位 + 种子入档。
            // 红闪/红印 marker 挂 grid 子层——上方 grid.Build 已随场整组销毁（R 回滚同走本
            // 方法 = 完整重置语义天然成立），这里清批次表防陈旧波钟。种子换关重生成、同关
            // 沿用（R 回滚/读档续玩重放同序列——06「防回滚刷好运」）；生成即落盘（ADR-023
            // 四时机之一）。双门：章 volcanoEnabled × 关 eruptionMax>0（ch1-3 铁律零进入）。
            eruptionBatches.Clear();
            lavaMarks.Clear(); // F2.3：红印 DoT 册随场复位（格灰烬/毁塔由 grid 重建+快照回场天然复位）
            lavaDotClock = 0f; // F2.4：整秒跳钟随场归零（相位干净——首跳满 1s 后落）
            volcanoActive = targetChapter != null && targetChapter.volcanoEnabled &&
                            level != null && level.eruptionMax > 0;
            if (volcanoActive)
            {
                var cs = save != null ? CurrentChapterState : null;
                if (cs == null || cs.eruptionSeed == 0 || cs.eruptionSeedLevel != levelIndex)
                {
                    eruptionSeed = UnityEngine.Random.Range(1, int.MaxValue); // 非零（0 = 未生成哨兵）
                    if (cs != null)
                    {
                        cs.eruptionSeed = eruptionSeed;
                        cs.eruptionSeedLevel = levelIndex;
                        WriteSave();
                    }
                }
                else eruptionSeed = cs.eruptionSeed;
                BuildEruptionPool();
            }
            else { eruptionSeed = 0; eruptionPool = null; }
            snapshot = field ?? new LevelStartSnapshotData();
            if (field != null && field.tiles != null)
            {
                foreach (var t in field.tiles)
                {
                    var cell = grid.Get(t.x, t.y);
                    if (cell == null) continue;
                    cell.RestoreState((CellState)t.state);         // 格型（空/荒/垦中——快照按完成态）
                    cell.RestoreTileState((TileState)t.tileState); // F7.3 跨关灰烬/复绿回放（旧档 0=常态空转）
                }
            }
            // 快照塔回场（推进 = 上关场末照搬）
            if (field != null && field.towers != null) RestoreTowers(field.towers);
            EnsureMirrors(); // F3.1：镜本体回场后补建对面镜像（快照不含派生镜像）

            // F2.5（ch5 双面）：换面状态随场复位（面 = 实由 model.Reset 保证；CD/纸闪/按钮门逐关重置）。
            // ApplyFaceBindings = 快照回场后的唯一绑定收口（对面塔视图隐藏、cell.Tower 只绑实面）。
            faceCd = 0f;
            faceFlashRemaining = 0f;
            if (faceFlash != null)
            {
                var fc = faceFlash.color;
                fc.a = 0f;
                faceFlash.color = fc;
            }
            if (faceBtnRoot != null) faceBtnRoot.SetActive(FaceSwitchAvailable);
            faceUiSig = null;
            RefreshFaceUi();
            ApplyFaceBindings();
            // F2.4 修订（用户 2026-10-22）：新关一律从实面起 → 路面全实（公共段恒实）、浅蓝/红闪清零
            if (path != null) path.ApplyFaceRoadTint(model != null ? model.ActiveFace : 0);
            faceTintAlpha = 0f;
            voidPulseTimer = 0f;
            voidPulseLeft = 0f;
            if (faceTint != null)
            {
                var tc0 = faceTint.color;
                tc0.a = 0f;
                faceTint.color = tc0;
            }
            if (voidPulse != null)
            {
                var pc0 = voidPulse.color;
                pc0.a = 0f;
                voidPulse.color = pc0;
            }

            // 抽卡池（v3.0-D，用户 2026-10-26 改「背包全局通用」）：
            //   常规关 = 构筑本身（解锁过的卡在任何关都能用，不再 ∩ 本关 poolChars）；
            //   教学关 L0 = 仍只用本关池（六步教学要可控）。
            // 一口价 = Σ 池内卡价值（构筑变化实时随之变动）。
            var pool = new List<string>();
            IEnumerable<string> source = level.levelId == 0
                ? backpack.EffectivePool(level.poolChars)
                : backpack.GlobalPool(level.poolChars);
            foreach (var c in source)
            {
                if (towerPool.Find(c) != null) pool.Add(c);
                else Debug.LogWarning("[GameManager] 池里有塔字池未定义的字：" + c + "（已跳过）");
            }
            // 防线：构筑为空/全无效 → 退回「已拥有 ∩ 本关池」，再不行就全部已拥有（不卡死）
            if (pool.Count == 0)
            {
                Debug.LogWarning("[GameManager] 构筑不可用，退回已拥有卡兜底");
                foreach (var c in level.poolChars)
                    if (backpack.OwnedCards.Contains(c) && towerPool.Find(c) != null) pool.Add(c);
                if (pool.Count == 0)
                    foreach (var c in backpack.OwnedCards)
                        if (towerPool.Find(c) != null) pool.Add(c);
            }
            int drawCost = 0;
            foreach (var c in pool) drawCost += towerPool.Find(c).value;
            hand = new HandModel(pool, gameParams.handSize, drawCost, Environment.TickCount);

            if (hud != null)
            {
                hud.HideBanner();
                hud.SetLevel(level.title);
                hud.SetWaveText("备战");
                if (chapterSwitched && activeChapter != null)
                    ShowChapterBanner(activeChapter.title); // F4.3：章界横幅（3 秒自收，通关/失守文案自然顶替）
            }
            HideResultButtons(); // 0.2：进关收结果按钮（模式复位，下轮幂等判据干净）

            // L0 教学（ADR-030）：进教学关开教；其余关收气泡。重进教学关重教一遍——
            // DoEnterLevel 重建空手牌，首步抽卡永远可完成，步骤不会卡死
            if (level.levelId == 0) tutorial?.Begin(TutorialAnchors());
            else tutorial?.End();

            ComputeWaveSchedule();
            ClearHighlights();
            RefreshHud();

            WriteSave();
        }

        // ============ L0 教学锚点（ADR-030） ============

        /// <summary>
        /// 六步气泡锚点（世界坐标，z=-2 盖在全部 UI 前）。按钮位运行时可查故惰性求值；
        /// 单行 7 号气泡停在手牌条顶与底行格子之间的空带（y≈-3.2），不遮按钮也不遮棋盘。
        /// </summary>
        private Func<Vector3>[] TutorialAnchors()
        {
            return new Func<Vector3>[]
            {
                () => ButtonAnchor(handBar.DrawButtonWorldPos),
                () => ButtonAnchor(handBar.SellButtonWorldPos),
                () => BarAnchor(), // 放置
                () => BarAnchor(), // 合成
                () => BarAnchor(), // 移动
                () => ButtonAnchor(handBar.NextButtonWorldPos),
            };
        }

        /// <summary>按钮锚点：按钮正上方 1.3（空带中央）；x 钳 ±5.8（7.6 宽气泡不出 9.6 半屏宽）。</summary>
        private static Vector3 ButtonAnchor(Vector3 p) =>
            new Vector3(Mathf.Clamp(p.x, -5.8f, 5.8f), p.y + 1.3f, -2f);

        /// <summary>手牌条中带锚点（放置/合成/移动三步共用）。</summary>
        private Vector3 BarAnchor() =>
            new Vector3(0f, (handBar != null ? handBar.transform.position.y : -4.55f) + 1.4f, -2f);

        /// <summary>捕获当前场（通关推进用）：塔（字/级/格位）+ 全部格子状态。开垦中视同完成（玩家已付铲）。</summary>
        private LevelStartSnapshotData CaptureField()
        {
            var snap = new LevelStartSnapshotData();
            // F2.3（ch5 双面）：塔按**模型**遍历（两面都存 face）——cell.Tower 只绑当前面，逐格
            // 遍历会丢对面塔。拿起中的塔不在格上（cell.Tower 已清）→ 照旧不存（跨关丢失 = 旧行为）。
            foreach (var t in model.Towers)
            {
                if (t == pickedTower) continue;
                if (t.isMirror) continue; // F3.1：镜影是派生状态，不进快照（回场 EnsureMirrors 重建）
                snap.towers.Add(new SnapTowerData
                {
                    charId = t.charId,
                    level = t.level,
                    x = Mathf.RoundToInt(t.position.x),
                    y = Mathf.RoundToInt(t.position.y),
                    face = t.face,
                });
            }
            foreach (var cell in grid.All)
            {
                int st = (int)cell.State;
                if (st == (int)CellState.Cultivating) st = (int)CellState.Empty;
                // F7.3 灰烬跨关（用户 2026-10-18 定案 A「原样累积」）：tileState 一并带走——
                // 章内推进（AdvanceLevel → 本快照）自然续到下一关；章界清场 field=null 不带（ch1-3 恒 0 零变化）。
                snap.tiles.Add(new SnapTileData
                {
                    x = cell.X,
                    y = cell.Y,
                    state = st,
                    tileState = (int)cell.TileState,
                });
            }
            // F2.4（ADR-054）：红铲造格 + 价格计数随快照走（推进续到下一关 / R 回滚退掉）
            foreach (var c in extraCells)
                snap.extraCells.Add(new SnapCellData { x = c.x, y = c.y });
            snap.redShovelBought = redShovelBought;
            return snap;
        }

        /// <summary>快照塔回场（脏数据防线：格不存在/被占/非合法落点 → 跳过该塔，黄字留痕）。</summary>
        private void RestoreTowers(List<SnapTowerData> towers)
        {
            // F2.3（ch5 双面）：同格两面可各有一塔——按面记占位（cell.Tower 只绑当前面，
            // 对面塔回场后仅存视图 + 模型，换面时由 ApplyFaceBindings 重新绑上）。
            var occupiedVoid = new HashSet<Vector2Int>();
            foreach (var t in towers)
            {
                var def = towerPool.Find(t.charId);
                var cell = grid.Get(t.x, t.y);
                // 合法落点 = 空地 ∪ 永久水格（限水系免疫塔，F3.2 口径）。
                // 实测修复（2026-10-05）：旧防线「State != Empty」一刀切写于 ch1 时代（当时可建格只有
                // 空地/荒地两态）——水路水塔章内推进全被当脏数据跳掉（用户实测：过关水格上的卡全清）
                bool legal = cell != null && def != null &&
                    (cell.State == CellState.Empty ||
                     (cell.State == CellState.Water && def.waterImmune));
                int face = t.face == 1 ? 1 : 0;
                bool slotTaken = face == 0
                    ? cell != null && cell.Tower != null
                    : occupiedVoid.Contains(new Vector2Int(t.x, t.y));
                if (!legal || slotTaken)
                {
                    Debug.LogWarning("[GameManager] 快照塔回放跳过：" + t.charId + " L" + t.level +
                        " (" + t.x + "," + t.y + ") 面" + face);
                    continue;
                }
                var ts = new TowerState
                {
                    charId = def.charId,
                    baseDamage = def.damage,
                    interval = def.interval,
                    baseRange = def.range,
                    rangePerLevel = def.rangePerLevel,
                    baseInkPerPulse = def.inkPerPulse,
                    antiAir = def.antiAir,
                    ranged = def.ranged,
                    value = def.value,
                    aoeShape = def.aoeShape,
                    aoeBaseSize = def.aoeSize,
                    aoeBaseParam = def.aoeParam,
                    // P2（用户 2026-10-25）：等级钳制走 MaxLevelFor（该塔已解锁 6 阶 → 可回放 L6；否则仍 5）
                    level = Mathf.Clamp(t.level, 1, model != null ? model.MaxLevelFor(t.charId) : (gameParams != null ? gameParams.maxTowerLevel : 5)),
                    position = new Vector2(t.x, t.y),
                    elevation = cell.Elevation,    // F2.2：海拔由所在格推导（快照零新字段，06 §10.7）
                    waterImmune = def.waterImmune, // F2.2：免疫由塔池定义推导
                    onWaterCell = cell.IsWater,    // F3.3：渔动态跳隔（快照零新字段，同 06 §10.7 口径）
                    slowFactor = def.slowFactor,   // F3.1：状态效果参数由塔池定义推导
                    slowSeconds = def.slowSeconds,
                    wetSeconds = def.wetSeconds,
                    freezeSeconds = def.freezeSeconds,
                    wetDamageMult = def.wetDamageMult,
                    landPulseSlower = def.landPulseSlower,
                    burnSeconds = def.burnSeconds, // F4.1/F4.2：火/炮挂燃随快照回放（def 推导——快照零新字段，06 §10.7 同口径）
                    burnDps = def.burnDps,
                    chainJumps = def.chainJumps,   // F3.2：链电由塔池定义推导（快照零新字段）
                    chainRatio = def.chainRatio,
                    chainRadius = def.chainRadius,
                    voidDamageMult = def.voidDamageMult, // F3.3：虚面加成由塔池定义推导
                    soulBanner = def.soulBanner,         // F3.4：魂幡标记由塔池定义推导（快照零新字段）
                    soulPayout = def.soulPayout,
                    face = face,                   // F2.3（ch5 双面）：面归属随快照回场
                };
                model.AddTower(ts); // 内含 ApplyLevelStats（等级通道重算）
                if (face == 0) cell.Tower = ts;
                else occupiedVoid.Add(new Vector2Int(t.x, t.y));
                cell.RefreshVisuals();

                var view = towerViews.Get(towerTemplate);
                view.transform.SetParent(cell.transform, false);
                view.Show(def.charId, ts.level);
                view.gameObject.SetActive(face == model.ActiveFace); // 对面塔先藏（换面再显）
                towerViewMap[ts] = view;
            }
        }

        /// <summary>存档 v2（03 §6 schema）：进关/通关/退避时各写一次。clearedLevelIndex ≥ 0 = 该关刚通关（解锁下一关）。</summary>
        private void WriteSave(int clearedLevelIndex = -1)
        {
            if (save == null) save = new SaveData();
            save.lastSavedUtc = DateTime.UtcNow.ToString("o");
            save.maxLevelUnlocked = Mathf.Max(save.maxLevelUnlocked, levelIndex + 1);
            if (clearedLevelIndex >= 0)
                save.maxLevelUnlocked = Mathf.Max(save.maxLevelUnlocked, clearedLevelIndex + 2);
            save.maxLevelUnlocked = Mathf.Min(save.maxLevelUnlocked, levels.Length);
            backpack.WriteTo(save);
            CurrentChapterState.chapter = activeChapter != null ? activeChapter.chapterId : 1; // F4.3：平铺跨章，章号随实际章图（原型信息位，无读取方）
            CurrentChapterState.levelIndex = levelIndex;
            if (snapshot != null) CurrentChapterState.levelStartSnapshot = snapshot; // 关开始快照 = 回滚锚点（空 = 面板未确认前不动旧锚）
            // F2.4（ADR-054）：红铲造格 + 价格计数入章内状态（章界在 DoEnterLevel 清空）
            if (CurrentChapterState.extraCells == null) CurrentChapterState.extraCells = new List<SnapCellData>();
            CurrentChapterState.extraCells.Clear();
            foreach (var c in extraCells)
                CurrentChapterState.extraCells.Add(new SnapCellData { x = c.x, y = c.y });
            CurrentChapterState.redShovelBought = redShovelBought;
            CurrentChapterState.homeLevel = homeLevel; // 家（用户 2026-10-23）：章内跨关带
            save.lastMode = hardMode ? 1 : 0;           // v3：记住当前模式（选关/主界面恢复）
            SaveSystem.Write(save);
        }

        private void OnApplicationQuit() => WriteSave();

        // 移动端生命周期（03 §8-8）：Home 键 = OnApplicationPause(true)，是存档主时机
        private void OnApplicationPause(bool paused)
        {
            if (paused) WriteSave();
        }

        private void RefreshHud()
        {
            RefreshTowerSilence(); // F2.2：沉默灰显/倒计时同步（不依赖 hud，先于空判）
            if (hud == null || model == null) return;
            hud.SetStats(model.Ink, model.Lives);
            if (handBar != null && hand != null && level != null)
            {
                // F2.1/F2.2 红铲：显示条件 = 关卡解锁；置灰条件 = 墨不足（价格永远显示）
                int shovelPrice = RedShovelPrice;
                handBar.Refresh(hand, model.Ink, selectedCardIndex, pickedTower != null,
                    nextWaveIndex < level.waves.Count,
                    shovelPrice, RedShovelUnlocked, redShovelMode,
                    redShovelMode || model.Ink >= shovelPrice);
            }

            if (model.Failed)
            {
                LogTuningReport("失败"); // F7.1 探针：场末体检（失败帧；幂等一次制）
                sfx?.PlayDefeat(); // 重大失败音（ADR-083）
                hud.ShowBanner("卷轴失守 · 再战一局");
                ShowResultButtons(2); // 0.2：失败单钮「回滚再战」（幂等，0.1s 重刷无害）
            }
            else if (!won && nextWaveIndex == 0 && waveStart != null && waveStart.Length > 0)
            {
                // 备战倒计时（首波前的布防窗口）。教学关首波 start=9999（纯数据挡怪，ADR-030）→
                // 只显「备战」不带秒数
                double remain = Math.Max(0, EffectiveStart(0) - waveClock);
                hud.SetWaveText((remain > 599 ? "备战" : "备战 " + Math.Ceiling(remain) + "s") + NextTideHint());
            }
            else if (!won && level != null && nextWaveIndex > 0 && nextWaveIndex < level.waves.Count)
            {
                // F2.3 潮汐预报：波间持续可读（06 §10.2 波次表 = 潮汐预报，玩家可预读撤离）
                hud.SetWaveText($"第 {model.Wave} 波" + NextTideHint());
            }
        }

        /// <summary>
        /// F2.3 下一波潮位预告（挂在波次栏）：涨/大涨 = 撤离警报，退潮 = 反攻窗口；
        /// 平/沿用不占栏（降噪）。ch1 全 Hold → 恒空串，波次栏零变化。
        /// </summary>
        private string NextTideHint()
        {
            if (level == null || nextWaveIndex < 0 || nextWaveIndex >= level.waves.Count) return "";
            // F5.4 预告义务（06 §10.4 三件套之二）：下一波含蛟 = Boss 行洪水预告（文字档，图标降级）。
            // ch1 波表无蛟 → 恒不命中，零变化。
            foreach (var g in level.waves[nextWaveIndex].groups)
                if (g.charId == "蛟") return " · 蛟怒·洪水将至";
            // F4.4（ch3）：下一波含枭 = Boss 行暗影预告（文字档——同蛟「图标降级」原型口径；
            // 「暗影图标」= 美术资产，原型期文字降级备案）。枭只在三L5 波表 → ch1/2 恒不命中。
            foreach (var g in level.waves[nextWaveIndex].groups)
                if (g.charId == "枭") return " · 枭至·灯昏将至";
            // F5.3 预告义务（06 §12.5 三件套之二）：下一波含麟 = Boss 行地燃预告（用户 2026-10-18
            // 定夺「留」——沿 ch2 蛟/ch3 枭先例，文字档图标降级）。麟在四L5 官方波表 W7，随 F6
            // 落表即生效（本分支先就位——ch1-3 波表无麟，恒不命中零变化）。
            foreach (var g in level.waves[nextWaveIndex].groups)
                if (g.charId == "麟") return " · 麟至·地燃将至";
            switch (level.waves[nextWaveIndex].tide)
            {
                case TideLevel.Rise: return " · 涨潮将至";
                case TideLevel.Surge: return " · 大涨将至";
                case TideLevel.Ebb: return " · 退潮将至";
                default: return "";
            }
        }
    }
}
