using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 网格系统（v2.3 ADR-011 → v3.0 ADR-021）：读取 ChapterConfig 章图布局生成
    /// 运行时格子，取代 W1 时代写在场景里的 Slot Transform 列表——布局成数据。
    /// 一章一图：章内切关（L1-L5）不重建布局，重开时同图重建。
    /// 塔移动 = 改绑格子；开垦 = 格子状态机 + 计时回调（03 §4.1）。
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, GridCell> cells = new Dictionary<Vector2Int, GridCell>();
        private Sprite whiteSprite;
        private TMP_FontAsset font; // 格子上文字（铲 glyph 等）

        // —— F1.1（ch3 幽夜）迷雾遮罩：全图暗色砖层 + 光圈挖洞（硬圈）——
        [Tooltip("雾砖颜色（夜色暗度与浓度）——Inspector 可调（用户调参领地）")]
        [SerializeField] private Color fogColor = new Color(0.05f, 0.06f, 0.12f, 0.55f);
        private Transform fogMask;
        private readonly Dictionary<Vector2Int, SpriteRenderer> fogTiles = new Dictionary<Vector2Int, SpriteRenderer>();

        public int Count => cells.Count;
        public IEnumerable<GridCell> All => cells.Values;
        // F2.3（ADR-054）：陆路 + 水路经过的整数格（Build 时算一次）——红铲禁造格判据。
        // 路径格**没有 GridCell**，不能靠「这格没格子」反推（否则会造到路上）。
        private readonly HashSet<Vector2Int> routeCells = new HashSet<Vector2Int>();

        /// <summary>场景创建时调用一次。</summary>
        public void Init(Sprite white, TMP_FontAsset fontAsset)
        {
            whiteSprite = white;
            font = fontAsset;
        }

        /// <summary>按章图布局重建全部格子（重开时；章内切关同图不重建；先清旧）。</summary>
        public void Build(ChapterConfig chapter)
        {
            Clear();
            EnsureBackdrop(); // 棋盘底纹（关卡无关，Clear 不销毁——见 backdrop 字段注释）
            if (chapter == null) return;
            routeCells.Clear();
            routeCells.UnionWith(CollectRouteCells(chapter.landRoute));
            routeCells.UnionWith(CollectRouteCells(chapter.waterRoute));
            routeCells.UnionWith(CollectRouteCells(chapter.voidRoute)); // F1.2（ch5 双面）：虚路也是路——不可造格
            foreach (var c in chapter.cells)
            {
                var go = new GameObject("Cell", typeof(GridCell));
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(c.x, c.y, 0f);
                var cell = go.GetComponent<GridCell>();
                cell.Init(c.x, c.y, c.state, c.elevation, whiteSprite, font); // F1.1：海拔随格传入（ch1 缺省 4=高地）
                cells[new Vector2Int(c.x, c.y)] = cell;
            }

            // P2 终修（08 §7，用户澄清 10-07 两次）：水路（与墨路重合的汇合尾段除外）上边一行 =
            // 河岸浅滩带，整行常显水域蓝。有格处格子自己蓝（水格 WaterFill / 普通格 MarkRiverBank）；
            // 无格空洞处露宣纸底纹（暖米黄 + 上方荒地墨渍 = 用户实测的「黄棕旱地」观感）→ 铺
            // RiverBank 装饰砖补洞，与水路砖行拼成有宽度的整片河道。墨路砖位（拐角汇合点）
            // 不铺（蓝砖盖深灰褐砖 = 错位观感）。玩法零变化；ch1 无水路 → 零进入零变化。
            if (chapter.waterRoute != null && chapter.waterRoute.Count >= 2)
            {
                var roadLand = CollectRouteCells(chapter.landRoute);
                riverBank = new GameObject("RiverBank").transform; // 旧层已随 Clear 销毁（backdrop 外全清）
                riverBank.SetParent(transform, false);
                var marked = new HashSet<Vector2Int>();
                foreach (var rc in CollectRouteCells(chapter.waterRoute))
                {
                    if (roadLand.Contains(rc)) continue; // 汇合共享段 = 墨路砖（深灰褐）
                    var above = new Vector2Int(rc.x, rc.y + 1);
                    if (marked.Contains(above)) continue;
                    marked.Add(above);
                    if (cells.TryGetValue(above, out var bank)) bank.MarkRiverBank();
                    else if (!roadLand.Contains(above)) AddRiverBankTile(above); // 空位且非墨路砖位 → 补洞
                }
            }

            // F1.1（ch3 幽夜，06 §11.2）：章开雾 → 全图暗色遮罩砖层。范围按相机视野外扩 1 格
            // 铺满（v2 修正：首版只铺格子区 ±9×±4，用户验收「上下都有空」——相机视野大于格子区
            // 露出无雾亮带）；相机缺失兜底 ±11×±7。光圈挖洞归 SetFogLights（关级家灯半径
            // 4.5→0.5；F1.2 LightSystem 动态光源集同接口重算）。ch1/ch2 fogEnabled=false →
            // 零进入零变化（铁律）。
            if (chapter.fogEnabled)
            {
                fogMask = new GameObject("FogMask").transform; // 旧层已随 Clear 销毁（backdrop 外全清）
                fogMask.SetParent(transform, false);
                var cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
                float halfH = cam != null ? cam.orthographicSize : 5.4f;
                float halfW = halfH * (cam != null ? cam.aspect : 16f / 9f);
                int x1 = Mathf.CeilToInt(halfW) + 1, y1 = Mathf.CeilToInt(halfH) + 1;
                for (int x = -x1; x <= x1; x++)
                    for (int y = -y1; y <= y1; y++)
                        AddFogTile(x, y);
            }

            // F1.1（ch4 火山，06 §12.1）：章开火山 → 火山口装饰（纯 sprite 视觉锚，非功能格——
            // 喷射引擎 F2 起按格池落岩浆，此处只管「章图绕着什么转」的图面语义）。锥盘 + 口部
            // 两层圆：z=0.95/0.94 在底纹(0.9)之后、格根(0)之前——格/塔/路全在装饰前不遮挡。
            // 挂 GridSystem 下（Clear 随格子整组销毁、Build 按章重建——fogMask/riverBank 同生命周期）。
            // ch1-3 volcanoEnabled=false → 零进入零变化（铁律）。
            if (chapter.volcanoEnabled)
            {
                var volcano = new GameObject("VolcanoMouth").transform;
                volcano.SetParent(transform, false);
                var cx = chapter.volcanoCenter.x;
                var cy = chapter.volcanoCenter.y;
                var cone = new GameObject("Cone", typeof(SpriteRenderer));
                cone.transform.SetParent(volcano, false);
                cone.transform.localPosition = new Vector3(cx, cy, 0.95f);
                cone.transform.localScale = new Vector3(1.5f, 1.5f, 1f); // 山体锥盘（3×3 视觉占位）
                var coneSr = cone.GetComponent<SpriteRenderer>();
                coneSr.sprite = whiteSprite;
                coneSr.color = HexUtil.ToColor("#4A3B33"); // 深褐山体
                var mouth = new GameObject("Mouth", typeof(SpriteRenderer));
                mouth.transform.SetParent(volcano, false);
                mouth.transform.localPosition = new Vector3(cx, cy, 0.94f); // 口部在山体前（z 更小更靠相机）
                mouth.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                var mouthSr = mouth.GetComponent<SpriteRenderer>();
                mouthSr.sprite = whiteSprite;
                mouthSr.color = new Color(0.13f, 0.06f, 0.05f, 0.95f); // 焦黑红口
            }
        }

        /// <summary>
        /// 雾砖：z=-0.2（v2 修正：首版 -0.37 把方向推反——z 越小越靠相机，-0.37 实际盖住了塔、
        /// 敌反而浮在雾上）。正确窗口：盖棋盘（格根 0/Fill 0.06/路 0.5/河岸 0.68/水 0.7/底纹
        /// 0.9 全入夜=夜色层）；塔（-0.35）与塔字（-0.7）、敌（-0.4）、光环（-0.23）、手牌面板
        /// （-1.7）、飘字（SpawnFloat 钳 -0.45）全在雾前不受影响——玩家操作面永远可读。
        /// 雾中敌压暗归 F2.1 tint（敌自身变色，正规路径非遮罩）。挖洞 = 亮格砖 enabled 关
        /// （不销毁——光源集重算零重建零 GC）。
        /// </summary>
        private void AddFogTile(int x, int y)
        {
            var go = new GameObject("F" + x + "_" + y, typeof(SpriteRenderer));
            go.transform.SetParent(fogMask, false);
            go.transform.localPosition = new Vector3(x, y, -0.2f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = fogColor;
            fogTiles[new Vector2Int(x, y)] = sr;
        }

        /// <summary>
        /// F1.2 光圈挖洞（硬圈）：格中心到任一光源距离 ≤ 半径 = 亮（砖隐藏）。
        /// 数据源 = BattleModel.GetLights()（家灯 + 灯塔并集，光集变化事件驱动重画）——
        /// 挖洞口径与 IsLit 交战谓词一致（所见即所得：亮区 = 可交战区）。
        /// 无雾章（fogEnabled=false 未建层）调用为空操作。
        /// </summary>
        public void SetFogLights(IReadOnlyList<(Vector2 pos, double radius)> lights)
        {
            if (fogTiles.Count == 0 || lights == null || lights.Count == 0) return;
            foreach (var kv in fogTiles)
            {
                bool lit = false;
                for (int i = 0; i < lights.Count; i++)
                    if (Vector2.Distance(kv.Key, lights[i].pos) <= lights[i].radius + 0.01) { lit = true; break; }
                kv.Value.enabled = !lit;
            }
        }

        /// <summary>
        /// 河岸带空洞装饰砖：水路上邻行的无格位置铺浅滩蓝，整行水域不留宣纸洞。
        /// z=0.68：路面砖(0.5)之后、淹没水面层(0.7)之前——涨潮膨胀集的 W 砖仍能盖上来
        /// （半透明青灰透出蓝底 = 深水观感）、退潮 W 砖隐藏露纯蓝；与 W 砖同 0.7 会 z-fighting。
        /// </summary>
        private void AddRiverBankTile(Vector2Int pos)
        {
            var go = new GameObject("B" + pos.x + "_" + pos.y, typeof(SpriteRenderer));
            go.transform.SetParent(riverBank, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0.68f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = GridCell.RiverBankFill;
        }

        /// <summary>折线经过的整数格集合（正交折线步长采样取整去重；与 PathSystem 路面砖铺格同口径）。</summary>
        private static HashSet<Vector2Int> CollectRouteCells(List<Vector2> wps)
        {
            var set = new HashSet<Vector2Int>();
            if (wps == null || wps.Count < 2) return set;
            for (int i = 1; i < wps.Count; i++)
            {
                var a = wps[i - 1];
                var b = wps[i];
                float len = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
                for (int s = 0; s <= steps; s++)
                {
                    float k = (float)s / steps;
                    set.Add(new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, k)),
                                           Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, k))));
                }
            }
            return set;
        }

        public GridCell Get(int x, int y) =>
            cells.TryGetValue(new Vector2Int(x, y), out var c) ? c : null;

        /// <summary>该坐标是否被陆路/水路经过（F2.3 红铲禁造格——与 PathSystem 铺砖同口径）。</summary>
        public bool IsRouteCell(int x, int y) => routeCells.Contains(new Vector2Int(x, y));

        /// <summary>
        /// F2.3（ADR-054）红铲造格：在无格坐标生成一块**普通空地格**（CellState.Empty / 海拔 4 高地缺省）。
        /// 只建格、不给任何特判——灰烬/岩浆/迷雾/水系规则一律走既有管线；能不能造由调用方判
        /// （路径/区外/已有格在 GameManager 侧先拦）。已存在该格 = 返回 null。
        /// ch3 迷雾砖层已按相机视野铺满整屏（含新格位）→ 新格天生在雾下，挖洞由 SetFogLights 重算；
        /// ch2 河岸装饰砖 z=0.68 在新格（fill 0.06）之后，被盖住即正常观感，不另做清理。
        /// </summary>
        public GridCell AddCell(int x, int y)
        {
            var key = new Vector2Int(x, y);
            if (cells.ContainsKey(key)) return null;
            var go = new GameObject("Cell", typeof(GridCell));
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, y, 0f);
            var cell = go.GetComponent<GridCell>();
            cell.Init(x, y, CellState.Empty, 4, whiteSprite, font);
            cells[key] = cell;
            return cell;
        }

        /// <summary>
        /// 格子（关卡级持久对象，非战斗内生灭单位）：整场重建，不走对象池。
        /// 只清格子——底纹（关卡无关）跨关存活，避免「Destroy 延迟到帧末 +
        /// Find 查旧底纹存在 → 跳过重建 → 帧末被销毁」的切关消失 bug。
        /// </summary>
        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (backdrop != null && child == backdrop) continue;
                Destroy(child.gameObject);
            }
            cells.Clear();
            routeCells.Clear(); // F2.3：路线格集也随图清（Build 重算；空章不残留上一章路线）
            sheetTiles.Clear(); // ADR-035 水面层随格子整组销毁（重开/切章懒重建）——引用表同步清防假 null 残留
            fogTiles.Clear();  // F1.1 雾砖层随格子整组销毁（fogMask 子物体 = backdrop 之外全清），Build 按章重建
            fogMask = null;
        }

        private Transform riverBank; // 河岸带装饰层（P2 终修）：Clear 随格子销毁（backdrop 之外全清），Build 重建
        private Transform backdrop; // 棋盘底纹（关卡无关）：Clear 保留、整局只建一次；被外部销毁后 Unity 假 null 会触发重建
        private static Texture2D _boardTex;
        private static Sprite _boardSprite;

        // ============ F4 验收追加（ADR-035）：淹没区连片水面覆盖层 ============
        // 用户定案（2026-10-04）：涨潮不只塔防格变色——被淹格周围的棋盘空隙也铺水：
        // 低地连成整片湖、干格像露出水面的小岛、堤路从水上穿过。
        // 层序（z 越小越靠前）：格 0.06 < 路面 0.5 < 水面 0.7 < 底纹 0.9。
        // 覆盖 = 淹没格（海拔 ≤ 水线，水格豁免恒青）8 邻域膨胀填缝；水线 ≥ 4 = 整版全开（F5 蛟全图洪水）。
        // ch1 全图海拔 4 > 水线 0-3 → 锚点恒空 → 层永不创建（懒建），零变化铁律不破。
        private Transform sheet; // 水面层（懒建；Clear 随格子销毁——重开/切章后首个潮位事件重建）
        private readonly Dictionary<Vector2Int, TideTileFade> sheetTiles = new Dictionary<Vector2Int, TideTileFade>();
        private static readonly Color SheetFill = new Color(0.62f, 0.73f, 0.78f, 0.78f); // 淹没深青系半透明（GridCell.FloodFill #9FB9C6 同源；透出棋盘格纹当水底纹理）
        private static readonly Color SheetDry = new Color(0.62f, 0.73f, 0.78f, 0f);     // 退净（隐藏态目标色）

        /// <summary>
        /// 棋盘底纹：2×2 格宣纸棋盘纹理（隔格微差 + 每格左下 2px 淡墨格线），
        /// SpriteRenderer Tiled 平铺 22×12 格盖住可视区。半格错位摆放让格线落在
        /// x.5 / y.5 上，与可建格（整数中心）的边界严格对齐。纹理静态缓存——
        /// 重开/切关反复 Build 不重复造图。z=0.9 在路面(0.5)之后、最底层。
        /// </summary>
        private void EnsureBackdrop()
        {
            if (backdrop != null) return; // 已建（Unity null 重载：被销毁后会走到重建）

            if (_boardSprite == null)
            {
                const int cell = 64; // 64px = 1 世界单位（PPU=cell）
                _boardTex = new Texture2D(cell * 2, cell * 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                };
                var paperA = new Color32(0xF7, 0xF2, 0xE7, 0xFF); // 宣纸底（与相机底色一致）
                var paperB = new Color32(0xF1, 0xEC, 0xDF, 0xFF); // 隔格微差
                var line = new Color32(0xE4, 0xDD, 0xCC, 0xFF);   // 淡墨格线
                for (int y = 0; y < cell * 2; y++)
                    for (int x = 0; x < cell * 2; x++)
                    {
                        bool alt = (x / cell + y / cell) % 2 == 1;
                        bool isLine = x % cell < 2 || y % cell < 2;
                        _boardTex.SetPixel(x, y, isLine ? line : (alt ? paperB : paperA));
                    }
                _boardTex.Apply();
                _boardSprite = Sprite.Create(_boardTex,
                    new Rect(0f, 0f, cell * 2, cell * 2), new Vector2(0.5f, 0.5f), cell);
            }

            var go = new GameObject("Backdrop", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.5f, 0.5f, 0.9f); // 半格错位：格线对齐可建格边界
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = _boardSprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(22f, 16f); // 22×16 格：16:9 盖 ±9.6/±5.4；窄屏相机放大后（见 FitCameraWidth）也不会露底
            backdrop = go.transform;
        }

        // ============ ADR-035：淹没区连片水面覆盖层（F4 验收追加） ============

        /// <summary>
        /// 按水线铺/退整片水面（GameManager 水线事件驱动，与格子淹没同扫掠参数同节奏）。
        /// 覆盖集 = 淹没格（海拔 ≤ 水线；水格豁免——恒水不参与，F1 先例）的 8 邻域膨胀——
        /// 填掉格子间空隙连成整片；水线 ≥ 4 = 全图洪水：整版全开（含格界外的画布边缘，F5 蛟 B 方案）。
        /// ch1 无可淹锚点 → 层永不创建（懒建幂等）。
        /// </summary>
        public void ApplyWaterSheet(int waterLevel, bool rising, float perCol, int minX, int maxX)
        {
            bool anyAnchor = false;
            foreach (var c in cells.Values)
                if (!c.IsWater && c.Elevation <= waterLevel) { anyAnchor = true; break; }
            if (!anyAnchor && sheet == null) return; // 全干且层没建过：无事可做（ch1 常态）

            EnsureSheet();
            float span = perCol > 0f ? (maxX - minX) * perCol : 0f; // 扫掠总跨度（格界外的列不超尾巴）

            var covered = new HashSet<Vector2Int>();
            if (waterLevel >= 4)
            {
                covered.UnionWith(sheetTiles.Keys); // 全图洪水：整版（建层即相口满铺）
            }
            else
            {
                foreach (var c in cells.Values)
                {
                    if (c.IsWater || c.Elevation > waterLevel) continue;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            covered.Add(new Vector2Int(c.X + dx, c.Y + dy));
                }
            }

            foreach (var kv in sheetTiles)
            {
                bool on = covered.Contains(kv.Key);
                float delay = perCol <= 0f ? 0f : Mathf.Clamp(
                    rising ? (kv.Key.x - minX) * perCol : (maxX - kv.Key.x) * perCol, 0f, span);
                kv.Value.Begin(on ? SheetFill : SheetDry, delay);
            }
        }

        /// <summary>懒建水面层（相口满铺 21×11 格：列 -10..10 / 行 -5..5；正常潮位只亮膨胀集）。</summary>
        private void EnsureSheet()
        {
            if (sheet != null && sheetTiles.Count > 0) return; // 已建；被销毁（Clear/外部）→ Unity 假 null 走重建
            if (sheet != null) Destroy(sheet.gameObject);      // 半建防御（理论不可达）
            sheetTiles.Clear();

            var sheetGo = new GameObject("Sheet");
            sheetGo.transform.SetParent(transform, false);
            sheet = sheetGo.transform;
            for (int x = -10; x <= 10; x++)
                for (int y = -7; y <= 7; y++)   // ±7.5：窄屏相机放大后仍能盖满上下（原 ±5.5 会露一条干边）
                {
                    var go = new GameObject("W" + x + "_" + y, typeof(SpriteRenderer));
                    go.transform.SetParent(sheet, false);
                    go.transform.localPosition = new Vector3(x, y, 0.7f); // 路(0.5)之后、底纹(0.9)之前
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sprite = whiteSprite;
                    sr.color = SheetDry;
                    sheetTiles[new Vector2Int(x, y)] = go.AddComponent<TideTileFade>();
                    go.SetActive(false); // 退净态：不渲染不跑 Update（Begin 唤醒）
                }
        }
    }
}
