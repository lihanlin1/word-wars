using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 战场格子（GridSystem 按 LevelConfig 运行时生成，取代 W1 的固定 TowerSlot）。
    /// 状态机（03 §4.1）：空地 / 非空地（荒地）/ 开垦中；占用 = Tower != null 的派生态。
    /// 三态美术（GDD §3.1）：空地淡墨虚框「▢」、荒地墨渍、开垦中进度显示
    /// （进度环原型期简化为顶部进度条，环形留 W4 美术轮换）。
    /// 点击统一上报 GameManager（点选式交互，鼠标/触屏零差异）。
    /// </summary>
    public class GridCell : MonoBehaviour
    {
        private static readonly Color FrameInk = HexUtil.ToColor("#8C8880");       // 淡墨虚框
        private static readonly Color Paper = HexUtil.ToColor("#F7F2E7");          // 宣纸（虚框内衬）
        private static readonly Color Blot = new Color(0.29f, 0.27f, 0.25f, 0.9f); // 荒地墨渍
        private static readonly Color BarInk = HexUtil.ToColor("#2B4C7E");         // 进度条（蓝墨）
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");      // 朱：高亮/闪烁/虚影
        private static readonly Color PrimeGold = HexUtil.ToColor("#C9A227");     // F3.1 金：优位格（射程∩光照 / 灯照路）——与朱同亮度级，一眼可辨
        // F1.1 地形基调（ch2 水泽，ADR-032）：会被淹的陆格青系预告风险；
        // 高地（海拔 4 = ch1 全图）仍走 Paper + FrameInk——旧章图观感零变化。
        // P2（08 §7，用户 2026-10-07）：水格原浅水青 #C2D7DA 与宣纸亮度近似（F2.4 注释实测
        // 同源问题：色差 39/255 肉眼难辨）→ 读不出「水」。改双色相语义：**永久水 = 蓝**，
        // 临时淹没 = 灰青（FloodFill 系不动）——涨潮时一眼分「河道 vs 新淹的地」。
        // 候选 B（用户 Unity 对色不满意换此两行）：#5E9BD1 / #3F76A8（湛蓝更浓一档）。
        private static readonly Color WaterFill = HexUtil.ToColor("#7FB3D9");   // 水格内衬（天蓝，永久水）
        private static readonly Color WaterFrame = HexUtil.ToColor("#4E86A0");  // 水格框（蓝墨）
        // P2 续修（用户澄清 2026-10-07）：「水路上边一行的格子」也要常蓝——那是河岸浅滩带
        //（水路砖 + 浅滩行拼成有宽度的河道）。浅滩格玩法 = 普通可建格不动（塔照放、淹没判定
        // 照海拔走：涨潮 FloodFill 深青盖上来、退潮回浅蓝，层次可辨）；视觉 = 水域。
        // 候选 B（用户要「和水格一样深」换此行）：#7FB3D9。
        public static readonly Color RiverBankFill = HexUtil.ToColor("#9CC3E3");   // 浅滩内衬（浅滩蓝，干态基色；GridSystem 河岸带装饰砖共用）
        public static readonly Color RiverBankFrame = HexUtil.ToColor("#4E86A0");  // 浅滩框（蓝墨，与水格框同源）
        private static readonly Color TideFill = HexUtil.ToColor("#E9F0F0");    // 潮间低带/滩涂内衬（淡青晕）
        private static readonly Color TideFrame = HexUtil.ToColor("#7FA8B0");   // 潮间带框（青，涨/平潮淹）
        private static readonly Color LowlandFrame = HexUtil.ToColor("#9DBCC2"); // 左低地框（淡青，仅大涨淹）
        // F2.4 淹没专用色（深于水格一档：涨潮新水更深，与干态拉开可读色差——
        // 借用 WaterFill 时低带 #E9F0F0→#C2D7DA 色差仅 39/255 肉眼难辨，用户实测反馈）
        private static readonly Color FloodFill = HexUtil.ToColor("#9FB9C6");   // 淹没内衬（深青）
        private static readonly Color FloodFrame = HexUtil.ToColor("#4E7A85");  // 淹没框（深青墨）
        // F1.2 灰烬/复绿色（ch4 火山，06 §12.2）：焦土深褐一眼读「这里烧过」；
        // 绿框 = 盆复绿（苔绿——「格变绿框」字面）。走 dryFill/dryFrame 地形基调管线
        //（MarkRiverBank 先例；ch4 无潮汐，淹没色不与灰烬同场——两层不冲突）。
        private static readonly Color AshFill = HexUtil.ToColor("#5A4A40");    // 焦土内衬
        private static readonly Color AshFrame = HexUtil.ToColor("#3A2E26");   // 焦框
        private static readonly Color GreenFill = HexUtil.ToColor("#DDE5C8");  // 复绿内衬（淡苔）
        private static readonly Color GreenFrame = HexUtil.ToColor("#7A8B5A"); // 复绿框（苔绿）

        public int X { get; private set; }
        public int Y { get; private set; }

        /// <summary>当前状态（占用是 Tower != null 的派生态，不进枚举）。</summary>
        public CellState State { get; private set; }

        /// <summary>海拔 1-4（ADR-032 潮汐判定：被淹 ⟺ 海拔 ≤ 潮位水线；水格忽略）。</summary>
        public int Elevation { get; private set; }

        /// <summary>永久水格（仅水系塔可建、不可开垦、恒淹）。</summary>
        public bool IsWater => State == CellState.Water;

        /// <summary>地形框色基调（虚影/高亮叠加时替换，见 ApplyVisuals）。</summary>
        private Color baseFrame = FrameInk;

        /// <summary>占用格的塔（GameManager 编排写入；null = 空置）。</summary>
        public TowerState Tower { get; set; }

        public bool Occupied => Tower != null;
        public bool IsCultivating => State == CellState.Cultivating;

        private SpriteRenderer frame;    // 虚框外环（空地/虚影/高亮共用）
        private SpriteRenderer fill;     // 虚框内衬（纸色，抠出环形）
        private SpriteRenderer blot;     // 荒地墨渍
        private SpriteRenderer barTrack; // 开垦进度条底
        private SpriteRenderer barFill;  // 开垦进度条
        private TextMeshPro shovelGlyph; // 开垦中：铲 glyph（铲在地里干活的可见反馈）

        private double farmTotal;       // > 0 表示开垦计时进行中
        private double farmRemaining;
        private float flashRemaining;   // 拒绝反馈：进度环（条）闪一下

        private bool ghost;     // 拿起塔时：原格虚影占位（点此格 = 取消）
        private bool highlight; // 朱色高亮：当前操作的合法目标
        private bool primeSpot; // F3.1 优位：金框（射程∩光照 / 灯照路）——仅 highlight 态有意义

        // F2.1 潮汐淹没（视觉层）：规则判定在 BattleModel（海拔 ≤ 水线，即时），
        // 这里只管水色——淹没 = 深青（FloodFill，F2.4），干 = F1 静态地形色；切换 8s 渐变（06 §10.2，不做扫掠）
        private bool submerged;
        private Color dryFill = Paper;   // 干态基色（Init 按水格/海拔定格，退潮恢复用）
        private Color dryFrame = FrameInk;
        private Color fadeFromFill, fadeFromFrame; // 渐变起点（切换瞬间捕获，快速连切保持连续）
        private float fadeT = 1f;        // 1 = 无渐变进行
        private float fadeDelay;         // F2.4 扫掠延迟（涨潮左→右 / 退潮右→左，GameManager 依列算）

        // F1.2（ch4 火山，06 §12.2）：灰烬 = 运行时烧毁标记（战斗态——Build 重建即复位，
        // 不进 CellState 配置层）。烧到垦中格 = 进度埋灰暂停（Update 计时门禁，submerged 同构）。
        private TileState tileState = TileState.Normal;
        /// <summary>灰烬态（Normal/Ash/Green——放置谓词与视觉共用；只读，变更走
        /// SetAsh/ClearAsh/SetGreen）。Green **仅**出现在原可建地（CellState.Empty）——见 SetGreen。</summary>
        public TileState TileState => tileState;

        // F4.3b（用户 2026-10-18 报障修正）：烧前地形基调定格——ClearAsh 除灰回原象用。
        // SetAsh 的幂等早退保证「已灰再烧」不会把灰后色记成基调。
        private Color preAshFill = Paper;
        private Color preAshFrame = FrameInk;

        public void Init(int x, int y, CellState initial, int elevation, Sprite white, TMP_FontAsset font)
        {
            X = x;
            Y = y;
            State = initial;
            Elevation = Mathf.Clamp(elevation, 1, 4); // 水格忽略海拔（恒淹），钳防 Inspector 越界
            Tower = null;
            farmTotal = 0;
            name = "Cell_" + x + "_" + y;

            // 点击判定：OnMouseDown 依赖同物体 Collider（旧 TowerSlot 三件套里有它，
            // 重做格子时漏掉导致整张网格不可点——放置/拿起/开垦全部失效）
            var col = GetComponent<BoxCollider2D>();
            if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * 1.06f; // 用户 2026-10-25：点击/放置判定略放大（1×1→1.06，仅边界 3% 余量，不实质侵入邻格）

            // 外环 + 内衬 = 空心虚框
            frame = MakeSprite("Frame", white, new Vector3(0f, 0f, 0.06f), new Vector3(0.94f, 0.94f, 1f));
            fill = MakeSprite("Fill", white, new Vector3(0f, 0f, 0.05f), new Vector3(0.74f, 0.74f, 1f));
            fill.color = Paper;

            // 地形基调（F1.1 静态；F2 潮汐淹没态另行动态覆盖）：水格天蓝底；
            // 海拔 1（潮间低带/滩涂）淡青晕 + 青框；海拔 2 青框；海拔 3 淡青框；
            // 海拔 4（高地 = ch1 全图）= 原宣纸 + 淡墨框，旧章图观感零变化
            if (State == CellState.Water) { fill.color = WaterFill; baseFrame = WaterFrame; }
            else if (Elevation <= 1) { fill.color = TideFill; baseFrame = TideFrame; }
            else if (Elevation == 2) baseFrame = TideFrame;
            else if (Elevation == 3) baseFrame = LowlandFrame;

            dryFill = fill.color; // F2.1：干态基色定格（退潮恢复目标）
            dryFrame = baseFrame;

            // 荒地墨渍：略歪略扁，避免机械方块感
            blot = MakeSprite("Blot", white, new Vector3(0f, 0f, 0.04f), new Vector3(1.02f, 0.9f, 1f));
            blot.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            blot.color = Blot;

            // 开垦进度条（格子上方，塔前方可见）
            barTrack = MakeSprite("BarTrack", white, new Vector3(0f, 0.62f, -0.06f), new Vector3(0.72f, 0.12f, 1f));
            barFill = MakeSprite("BarFill", white, new Vector3(0f, 0.62f, -0.07f), new Vector3(0.72f, 0.12f, 1f));
            var trackC = BarInk;
            trackC.a = 0.18f;
            barTrack.color = trackC;
            barFill.color = BarInk;

            // 铲 glyph（开垦中才显示）：墨渍上干活的可读反馈——铲用掉后
            // 看得见「它在地里」，完成即撤（用户需求 2026-09-25）
            if (font != null)
            {
                var glyphGo = new GameObject("ShovelGlyph", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
                glyphGo.transform.SetParent(transform, false);
                glyphGo.transform.localPosition = new Vector3(0f, 0.04f, -0.08f); // 墨渍(0.04)/进度条(-0.07)之前
                shovelGlyph = glyphGo.AddComponent<TextMeshPro>();
                shovelGlyph.font = font;
                shovelGlyph.fontSize = 7f; // 小于塔字（10），标识「工具在工作」而非可点对象
                shovelGlyph.alignment = TextAlignmentOptions.Center;
                shovelGlyph.color = HexUtil.ToColor("#6B4F35"); // 褐：与手牌铲卡同色（用户反馈：米褐太浅）
                shovelGlyph.enableWordWrapping = false;
                shovelGlyph.text = "铲";
                var rt = (RectTransform)glyphGo.transform;
                rt.sizeDelta = new Vector2(1.6f, 1.6f);
                glyphGo.SetActive(false);
            }

            ApplyVisuals();
        }

        /// <summary>铲开垦：进入开垦态并开始计时。</summary>
        public void StartCultivating(double seconds)
        {
            if (State != CellState.Fallow) return;
            State = CellState.Cultivating;
            farmTotal = System.Math.Max(0.1, seconds);
            farmRemaining = farmTotal;
            ApplyVisuals();
        }

        /// <summary>快照回放（ADR-018 章内保留）：直接置格子状态（开垦完成态恢复）。</summary>
        public void RestoreState(CellState s)
        {
            if (Tower != null) return; // 有塔的格子状态由塔逻辑管，不许快照覆盖
            State = s;
            ApplyVisuals();
        }

        /// <summary>加速：立即完成开垦（墨已由 GameManager 扣）。</summary>
        public void ForceCompleteCultivation()
        {
            if (!IsCultivating) return;
            farmRemaining = 0;
        }

        /// <summary>拒绝反馈（真值表）：任意源点开垦中格 → 进度环（条）闪一下。</summary>
        public void Flash()
        {
            flashRemaining = 0.3f;
        }

        /// <summary>拿起塔时：原格虚影占位（再点此格 = 取消放回）。</summary>
        public void SetGhost(bool on)
        {
            if (ghost == on) return;
            ghost = on;
            ApplyVisuals();
        }

        /// <summary>朱色高亮：合法目标提示（发现性规范 03 §4.3）。prime = F3.1 优位格金色框
        ///（放这里有效：攻击塔射程能覆盖光照 / 灯能照到路——只换框色不改形态，一眼分级）。</summary>
        public void SetHighlight(bool on, bool prime = false)
        {
            bool p = on && prime;
            if (highlight == on && primeSpot == p) return;
            highlight = on;
            primeSpot = p;
            ApplyVisuals();
        }

        /// <summary>
        /// P2 续修（08 §7，用户澄清 2026-10-07）：河岸浅滩标记——水路上边一行的普通格常显浅滩蓝。
        /// 只改干态基色（退潮恢复目标随之转浅滩蓝）；淹没变色照海拔判定走（涨潮深青盖浅蓝可辨），
        /// 玩法零变化（照旧普通可建格）。水格自身已恒蓝（WaterFill），跳过。
        /// </summary>
        public void MarkRiverBank()
        {
            if (IsWater) return;
            dryFill = RiverBankFill;
            dryFrame = RiverBankFrame;
            baseFrame = RiverBankFrame;
            ApplyVisuals(); // fadeT 门禁内置：渐变进行中不打断，收口时自然落浅滩蓝
        }

        /// <summary>
        /// F2.3 烧毁（岩浆/践踏结算调用——F2.3/F5.1 接线；F3.1 单元期 H 调试键直调）：格变灰烬
        /// （**本关内永久**——恢复唯一路径 = 盆：塔防格走 SetGreen、荒地/垦中格走 ClearAsh）。塔的毁/幸存结算归 F2.3 喷射引擎
        ///（免疫规则），这里只管格态与视觉。垦中格 = 进度埋灰暂停（06「垦进度埋灰不丢」——
        /// Update 计时门禁）。幂等：已是 Ash 直接回；Green 再烧 = 回 Ash（盆+其上之塔同毁归 F2.3）。
        /// </summary>
        public void SetAsh()
        {
            if (tileState == TileState.Ash) return;
            if (tileState == TileState.Normal)   // 只从「未被烧过的常态」记基调：绿框再烧不回绿（回烧前原象）
            {
                preAshFill = dryFill;
                preAshFrame = dryFrame;
            }
            tileState = TileState.Ash;
            dryFill = AshFill;    // 干态基色定格（MarkRiverBank 同法——退潮/复绿恢复目标随之）
            dryFrame = AshFrame;
            baseFrame = AshFrame; // F4.3b：frame.color 读 baseFrame（dryFrame 只是渐变目标）——不补则焦框要等一次潮汐才显色（ch4 永不），只剩内衬变色
            ApplyVisuals();
        }

        /// <summary>
        /// F4.3b 除灰（用户 2026-10-18 报障修正）：灰烬 → 回**烧前原象**——供「烧前本来就不可建」
        /// 的地皮恢复原状（荒地 → 荒地墨渍：**开垦要求照旧**；垦中格 → 进度条续走）。
        /// 与 SetGreen 分工：SetGreen = 烧前是塔防格（CellState.Empty）→ 绿框可建；
        /// 本方法 = 烧前非塔防格 → 不产绿框（免得绿框谎称可建）。视觉输出唯一路径，
        /// 谓词（放塔/高亮）只认 CellState —— 二者不再打结。Green 分支仅供 H 调试键循环。
        /// </summary>
        public void ClearAsh()
        {
            if (tileState == TileState.Normal) return;
            tileState = TileState.Normal;
            dryFill = preAshFill;
            dryFrame = preAshFrame;
            baseFrame = preAshFrame; // F4.3b：框色同源（同 SetAsh 注）
            ApplyVisuals();
        }

        /// <summary>
        /// F4.3 盆复绿（盆放置接线，**仅**烧前为塔防格 CellState.Empty 时调用——荒地向 ClearAsh）：
        /// 灰烬 → 绿框 = 通用可建（任意塔含墨；垦中格进度条**自动续走**——Update 计时门禁
        /// tileState != Ash 即放行，与任务单 F3.3「垦进度恢复续走」一致。注：本注释曾写
        ///「恢复」以完成态简化——黄底备案 docs/11 §0」为**陈旧表述**——备案从未落盘（悬空
        /// 引用），门禁实装口径即续走，程序侧核验 2026-10-13 定案）。可被岩浆再毁（SetAsh 回烧）。
        /// </summary>
        public void SetGreen()
        {
            tileState = TileState.Green;
            dryFill = GreenFill;
            dryFrame = GreenFrame;
            baseFrame = GreenFrame; // F4.3b：框色同源——「绿框」从此字面成立（原先只有内衬淡苔、框仍是淡墨）
            ApplyVisuals();
        }

        /// <summary>
        /// F7.3 灰烬跨关（用户 2026-10-18 定案 A「原样累积」）：快照回放专用——按快照落 tileState，
        /// 视觉一帧到位（输出同 SetAsh/SetGreen：填色 + 框色 + 基调一并改）。**基调按本关缺省捕获**
        ///（回放时格已由 grid.Build 复位成新关地皮）——跨关焦土日后除灰 → 回**新关**地皮原象。
        /// 幂等：已是目标态直接回；Normal 分支空转（新关缺省即常态）。
        /// </summary>
        public void RestoreTileState(TileState s)
        {
            if (s == TileState.Normal || s == tileState) return;
            if (s == TileState.Ash)
            {
                preAshFill = dryFill;
                preAshFrame = dryFrame;
                tileState = TileState.Ash;
                dryFill = AshFill;
                dryFrame = AshFrame;
                baseFrame = AshFrame;
            }
            else // Green（盆复绿跨关保留——否则玩家花的盆白费）
            {
                tileState = TileState.Green;
                dryFill = GreenFill;
                dryFrame = GreenFrame;
                baseFrame = GreenFrame;
            }
            ApplyVisuals();
        }

        /// <summary>
        /// F3.1 调试通道（H 键）：灰烬态循环 Normal→Ash→Green→Normal——喷射引擎 F2 落地前的
        /// 手摆验收（谓词+视觉），F2 后留回归速查（T 水线/G 眩晕家族同惯例）。
        /// F4.3b：复位走 ClearAsh（回烧前基调，不再硬编码 Paper——低地/河岸格也准）。
        /// </summary>
        public void DebugCycleTileState()
        {
            if (tileState == TileState.Normal) SetAsh();
            else if (tileState == TileState.Ash) SetGreen();
            else ClearAsh();
        }

        /// <summary>
        /// F2.1 淹没态切换（GameManager 依水线驱动）：状态即时（幂等），水色渐变。
        /// F2.4 扫掠：delay 按列错峰（涨潮左→右淹、退潮右→左干，06 §10.2），单格渐变 1.2s，
        /// 列间跨度 6.5s → 总扫过 ≈ 8s，波前 ≈ 3-4 格宽移动色带（可读「水线在走」）。
        /// 规则判定（塔沉默）不受延迟影响（BattleModel 即时）。
        /// 水格豁免：恒水不参与淹没切换（F1 静态水色零变化；否则会被淹没深色误染）。
        /// </summary>
        public void SetSubmerged(bool on, float delay = 0f)
        {
            if (IsWater || submerged == on) return;
            submerged = on;
            fadeFromFill = fill.color;
            fadeFromFrame = baseFrame;
            fadeT = 0f;
            fadeDelay = Mathf.Max(0f, delay);
        }

        private void Update()
        {
            if (IsCultivating)
            {
                // F2.2：被淹垦地暂停（GDD §3.4），塔干续走（进度条停在原地可视）；
                // F1.2（ch4）：烧到垦中格 = 进度埋灰暂停（06 §12.2「垦进度埋灰不丢」——盆复绿续跑）
                if (!submerged && tileState != TileState.Ash) farmRemaining -= Time.deltaTime;
                if (farmRemaining <= 0)
                {
                    State = CellState.Empty;
                    farmTotal = 0;
                    ApplyVisuals();
                    if (GameManager.Instance != null)
                        GameManager.Instance.OnCellCultivated(this);
                }
                else
                {
                    // 进度 = 已完成比例（条从左往右长）
                    float p = 1f - (float)(farmRemaining / farmTotal);
                    var s = barFill.transform.localScale;
                    s.x = 0.72f * p;
                    barFill.transform.localScale = s;
                    var pos = barFill.transform.localPosition;
                    pos.x = -0.72f * (1f - p) * 0.5f;
                    barFill.transform.localPosition = pos;

                    // 铲 glyph 挖土摆动（上下小幅正弦）：读作「铲在干活」
                    if (shovelGlyph != null)
                    {
                        float bob = Mathf.Sin((float)(farmTotal - farmRemaining) * 5f) * 0.06f;
                        var gp = shovelGlyph.transform.localPosition;
                        gp.y = 0.04f + bob;
                        shovelGlyph.transform.localPosition = gp;
                    }
                }
            }

            if (flashRemaining > 0f)
            {
                flashRemaining -= Time.deltaTime;
                barFill.color = flashRemaining > 0f ? Vermilion : BarInk;
            }

            // F2.1/F2.4 水线渐变：先扫掠等待（列错峰），后 1.2s 插值到淹没/干态；结束回 ApplyVisuals 收口
            if (fadeDelay > 0f)
            {
                fadeDelay -= Time.deltaTime;
            }
            else if (fadeT < 1f)
            {
                fadeT = Mathf.Min(1f, fadeT + Time.deltaTime / 1.2f); // 单格 1.2s：波前收窄成移动色带（列间 6.5s → 总 ≈ 8s）
                Color targetFill = submerged ? FloodFill : dryFill;
                Color targetFrame = submerged ? FloodFrame : dryFrame;
                fill.color = Color.Lerp(fadeFromFill, targetFill, fadeT);
                baseFrame = Color.Lerp(fadeFromFrame, targetFrame, fadeT);
                if (!ghost && !highlight) frame.color = baseFrame; // 高亮/虚影优先，不抢
                if (fadeT >= 1f) ApplyVisuals();
            }
        }

        private void OnMouseDown()
        {
            SfxPlayer.Click(); // 点击音（ADR-083；无效分支由 GameManager 再补 ClickInvalid）
            if (GameManager.Instance != null)
                GameManager.Instance.OnCellClicked(this);
        }

        /// <summary>占用关系变化后由 GameManager 调用（放塔/移塔/卖塔）。</summary>
        public void RefreshVisuals() => ApplyVisuals();

        private void ApplyVisuals()
        {
            // 虚框显隐：空地常显；荒地仅铲选中（高亮）时显朱框；开垦中不显；
            // 占用时藏（塔即视觉），虚影占位例外（拿起塔的原格提示可点回取消）
            bool frameOn;
            if (tileState == TileState.Ash) frameOn = true;
            // ↑ F4.3b：灰烬**恒显**——荒地/垦中格的 fill+frame 本就不显（下两行），灰盖上去等于
            //   「看不见自己被烧了」（必须看得见才谈得上用盆）。塔防格+灰与本行结果同，不变。
            else if (State == CellState.Cultivating) frameOn = false;
            else if (State == CellState.Fallow) frameOn = highlight;
            else frameOn = Tower == null || ghost;

            frame.enabled = frameOn;
            frame.color = ghost
                ? new Color(Vermilion.r, Vermilion.g, Vermilion.b, 0.55f)
                : highlight ? (primeSpot ? PrimeGold : Vermilion) : baseFrame; // 常态框色 = 地形基调（水/潮间带/高地；渐变期间 baseFrame 被插值）；优位金 > 合法朱
            fill.enabled = frameOn;
            if (fadeT >= 1f) // F2.1/F2.4：淹没深青（渐变进行中由 Update 接管，防中途跳色）
                fill.color = submerged ? FloodFill : dryFill;

            bool cultivating = IsCultivating;
            blot.enabled = (State == CellState.Fallow || cultivating) && tileState != TileState.Ash; // F4.3b：烧毁后无荒草（一片焦土）
            if (shovelGlyph != null)
                shovelGlyph.gameObject.SetActive(cultivating); // 铲 glyph：开垦中在场，完成即撤
            if (cultivating)
            {
                // 开垦中：墨渍随进度变淡（洗净为宣纸）
                var c = Blot;
                c.a = 0.9f * (float)(farmRemaining / farmTotal);
                blot.color = c;
            }
            else if (State == CellState.Fallow)
            {
                blot.color = Blot;
            }

            barTrack.enabled = cultivating;
            barFill.enabled = cultivating;
        }

        private SpriteRenderer MakeSprite(string childName, Sprite white, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject(childName, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = FrameInk;
            return sr;
        }
    }
}
