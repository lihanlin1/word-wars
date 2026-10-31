using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWars
{
    /// <summary>一条线路的运行时路由：长度 + 距离→坐标（PathSystem 预建缓存，取用零分配）。</summary>
    public readonly struct PathRoute
    {
        public readonly double Length;
        public readonly Func<double, Vector2> PointAt;

        /// <summary>
        /// F4.1（ADR-034）：水域终点里程——水路折线的末段 = 汇合后共享尾段（岸），
        /// 其前 = 水域航道；dist &lt; WaterUntil 即在水中（鱼/虾/龟/鳄 ×速乘）。
        /// 陆路 = 0（全程非水）。真实几何：路线行无格，「脚下格」恒空——水中 = 航道段而非格子。
        /// </summary>
        public readonly double WaterUntil;

        public PathRoute(double length, Func<double, Vector2> pointAt, double waterUntil = 0)
        {
            Length = length;
            PointAt = pointAt;
            WaterUntil = waterUntil;
        }
    }

    /// <summary>
    /// 线路集（F1.2，ch2 双线）：lane 0 = 陆路（主路），lane 1 = 水路。
    /// 无水路的章（ch1）对 lane 1 回退陆路——水路敌误入单线章不炸、走主路。
    /// F1.3（ADR-040）：GetPort 按「口」取半程路由（同路双向章）——lane 选线 / port 选端正交。
    /// </summary>
    public interface IPathSet
    {
        PathRoute Get(int lane);

        /// <summary>按出怪口取路由：port 0 = 路线起点端（下口）/ 1 = 末端（上口）。
        /// 单向章（bidirectional=false）两 port 同回 lane 全程——ch1/ch2 调用不传口，行为零变化。</summary>
        PathRoute GetPort(int lane, int port);

        /// <summary>F1.2（ch5 双面，ADR-058）：按**面**取路由（face 与 lane 正交）。
        /// 单面章（无虚路）face 1 回退陆路——ch1-4 恒 face 0，新路径零进入。</summary>
        PathRoute GetFace(int face);
    }

    /// <summary>
    /// 路线系统：折点 → 弧长参数化。敌人只存「已走路程」，
    /// 每帧由本类换算成世界坐标（表现与逻辑解耦的关键）。
    /// 折点留空时自动按层级顺序收集子物体。
    /// F1.2（ADR-032）：双线 + 汇合 = 两条完整折线（陆路/水路各自含汇合后共享尾段，
    /// 几何重合即「汇合」，无分支逻辑）；折点源 = ChapterConfig.landRoute/waterRoute（章数据），
    /// 每次进关由 GameManager 调 BuildLanes 重建；场景子物体折点仅作数据缺失时的兜底。
    /// 水路敌（EnemyStats.lane=1）走水路折线；水路砖渲蓝色区分墨路（P2：永久水 = 蓝）。
    /// </summary>
    public class PathSystem : MonoBehaviour
    {
        [Tooltip("留空则自动按层级顺序收集子物体（数据兜底；正式折点在 ChapterConfig")]
        [SerializeField] private Transform[] points;

        // —— ADR-035 路面砖潮汐变色（F4 验收追加，用户定案 2026-10-04）——
        // 水路砖 = 水线 ≥ 1（平潮起）加深一档、退潮回蓝；墨路砖 = 水线 ≥ 4（全图洪水，F5 蛟）才淹没。
        // ch1 单线无水路砖、水线恒 0-3 → 墨路砖同色早退，观感零变化。
        // P2（08 §7，用户 2026-10-07）：水路系整体转蓝（GridCell 恒水格同步）——「永久水 = 蓝 /
        // 临时淹没 = 灰青」双色相；加深态弃 FloodFrame 同源（原 #4E7A85 青灰会造成蓝→灰反向变浅观感）。
        // 候选 B（用户 Unity 对色不满意换此两行）：#3F76A8 / #2A5273（湛蓝更浓一档）。
        private static readonly Color RoadInk = new Color32(0x4E, 0x4A, 0x44, 0xFF);       // 墨路：深灰褐（原色）
        private static readonly Color WaterRoad = new Color32(0x5E, 0x8F, 0xAE, 0xFF);     // 水路：蓝青（永久水）
        private static readonly Color WaterRoadDeep = new Color32(0x3E, 0x6B, 0x88, 0xFF); // 水路涨水：深蓝墨
        private static readonly Color RoadFlooded = new Color32(0x9F, 0xB9, 0xC6, 0xFF);   // 墨路淹没：淹没深青（GridCell.FloodFill 同源）
        private static readonly Color VoidRoad = new Color32(0x8A, 0x84, 0x78, 0xFF);      // 虚路：淡墨（F1.2 ch5；非激活面切换由 F2.4 驱动）

        private readonly List<TideTileFade> roadLand = new List<TideTileFade>();  // 墨路砖（含汇合共享尾段）
        private readonly List<TideTileFade> roadLandOnly = new List<TideTileFade>(); // 实路**独有**的砖（换面淡显用；公共段不进）
        private readonly List<TideTileFade> roadWater = new List<TideTileFade>(); // 水路砖（重合段归墨路）

        // —— lane 0（陆路）——
        private Vector2[] pts;
        private double[] cumulative;

        // —— lane 1（水路；空 = 单线章）——
        private Vector2[] ptsW;
        private double[] cumulativeW;
        private double lengthW;

        private PathRoute route0; // 缓存路由（委托一次绑定，取用零分配）
        private PathRoute route1;

        // —— F1.3（ADR-040）同路双向：家钉弧长中点，两端口各走半程 ——
        private PathRoute routeLower; // 下口（起点端）：正序半程 [0, mid]
        private PathRoute routeUpper; // 上口（末端）：倒序半程（pointAt 映射 Length-s），敌走到终点=到家

        // —— F1.2（ch5 双面，ADR-058）：虚路（与陆路等长同起终点；空 = 单面章）——
        private Vector2[] ptsV;
        private double[] cumulativeV;
        private double lengthV;
        private PathRoute routeVoid;
        private readonly List<TideTileFade> roadVoid = new List<TideTileFade>(); // 虚路砖（淡墨）
        private HashSet<Vector2Int> voidTilesCache; // 虚路格集（null = 单面章；换面调色判据）

        public double Length { get; private set; }

        private void Awake()
        {
            if (points == null || points.Length == 0)
            {
                var children = new List<Transform>();
                foreach (Transform child in transform) children.Add(child);
                points = children.ToArray();
            }
            // F1.3 防御：场景兜底折点只建 route0——两口路由若未初始化（BuildLanes 前有敌出生的
            // 理论时序洞），GetPort 会拿到 default（Length 0 → 敌瞬判到达）。这里按单向语义兜底。
            if (points.Length >= 2)
            {
                BuildPolyline(ToVector2List(points), false);
                BuildPorts(false);
            }
            RebuildRoad();
        }

        /// <summary>按章数据重建双线路线 + 路面砖（每次进关调用；章内切关同图重复重建幂等）。</summary>
        public void BuildLanes(ChapterConfig chapter)
        {
            if (chapter == null || chapter.landRoute == null || chapter.landRoute.Count < 2) return;
            BuildPolyline(chapter.landRoute, false);
            BuildPolyline(chapter.waterRoute, true); // 空列表 = 单线章 → 清空水路
            BuildVoidRoute(chapter.voidRoute);        // F1.2（ch5）：空列表 = 单面章 → 清空虚路
            BuildPorts(chapter.bidirectional);
            RebuildRoad();
        }

        /// <summary>
        /// F1.2（ch5 双面，ADR-058）：虚路折线——与陆路**等长、同终点（家）**（ValidateChapter 校验；
        /// 出生口自 2026-10-22 用户改图起可各面各口，两路互为 y=0 镜像）。空列表 = 单面章（ch1-4）。
        /// </summary>
        private void BuildVoidRoute(List<Vector2> wps)
        {
            if (wps == null || wps.Count < 2)
            {
                ptsV = null; cumulativeV = null; lengthV = 0; routeVoid = default;
                return;
            }
            int n = wps.Count;
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) p[i] = wps[i];
            var cum = new double[n];
            double len = 0;
            for (int i = 1; i < n; i++)
            {
                len += Vector2.Distance(p[i - 1], p[i]);
                cum[i] = len;
            }
            ptsV = p; cumulativeV = cum; lengthV = len;
            routeVoid = new PathRoute(len, GetVoidPointAtDistance);
        }

        private Vector2 GetVoidPointAtDistance(double d)
        {
            if (ptsV == null || ptsV.Length == 0) return Vector2.zero;
            return PointOnPolyline(ptsV, cumulativeV, d);
        }

        /// <summary>
        /// F1.2（ch5 双面）：按**面**取路由（face 与 lane 正交）。face 1 无虚路 → 回退陆路
        ///（单面章容错：误传 face 1 不炸、走主路）。ch1-4 恒 face 0 → 行为零变化。
        /// </summary>
        public PathRoute GetFace(int face) => face == 1 && lengthV > 0 ? routeVoid : route0;

        /// <summary>F1.2：面路由长度（换面同进度落点 / 双面等长校验用；无虚路回退陆路长度）。</summary>
        public double LengthOfFace(int face) => face == 1 && lengthV > 0 ? lengthV : Length;

        /// <summary>
        /// F1.3（ADR-040）同路双向双口路由：bidirectional=true 时按陆路弧长中点切两条半程——
        /// 下口（起点端）正序 [0, mid]、上口（末端）倒序 [mid, Length]（pointAt 映射 Length-s）。
        /// 敌走到各自半程终点 = 到家（正中格）——到达/漏过判定零改动（走到自己 route 的终点）。
        /// 双向章不支持水路（lane 1 敌走 route1 全程——章图校验拦截；ch3 无水路敌）。半程
        /// WaterUntil=0（ch3 无潮汐；若后续章节组合双向+水路须重算倒序映射，非当前范围）。
        /// false（ch1/ch2）→ 两条半程 = 全程（GetPort 回退语义，零变化铁律）。
        /// </summary>
        private void BuildPorts(bool bidirectional)
        {
            if (!bidirectional || pts == null || Length <= 0)
            {
                routeLower = route0;
                routeUpper = route0;
                return;
            }
            double mid = Length / 2.0;
            routeLower = new PathRoute(mid, GetPointAtDistance);
            routeUpper = new PathRoute(Length - mid, GetUpperPointAtDistance);
        }

        private Vector2 GetUpperPointAtDistance(double s) => GetPointAtDistance(Length - s);

        /// <summary>
        /// F1.3：lane 与 port 正交——lane 选线（陆/水），port 选端（下/上口）。水路敌不受口
        /// 影响（双向章须无水路，校验拦截）；单向章 port 1 回退 lane 全程路由（ch1/ch2 零变化）。
        /// </summary>
        public PathRoute GetPort(int lane, int port)
        {
            if (lane == 1 && lengthW > 0) return route1; // 水路敌：口不适用（全程单口）
            return port == 1 ? routeUpper : routeLower;  // 陆路：下口（起点端）/ 上口（末端）
        }

        /// <summary>F1.1（ch3）：家 = 陆路弧长中点（双口半程终点；ch3 = (0,0) 战场中心）。
        /// 家灯光圈（挖洞）以它为圆心；ch1/ch2 无雾不消费。</summary>
        public Vector2 HomePosition => pts != null && Length > 0 ? GetPointAtDistance(Length / 2.0) : Vector2.zero;

        /// <summary>
        /// F3.1（ch3）：点到陆路折线的最短距离（逐段点到线段投影取 min）——灯卡优位判定用
        ///（照明圆触路 = 照亮敌道 = 优位格）。灯卡 ch3 池独有 → ch1/ch2 零调用。
        /// </summary>
        public double DistanceToRoute(Vector2 pos)
        {
            if (pts == null || pts.Length < 2) return double.MaxValue;
            double best = double.MaxValue;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                double d = DistanceToSegment(pos, pts[i], pts[i + 1]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>点到线段最短距（投影钳制 [0,1]；退化段（零长）回点到点距）。</summary>
        private static double DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            double t = abSqr > 1e-9f ? Vector2.Dot(p - a, ab) / abSqr : 0.0;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return Vector2.Distance(p, a + ab * (float)t);
        }

        private static List<Vector2> ToVector2List(Transform[] t)
        {
            var list = new List<Vector2>(t.Length);
            foreach (var p in t) if (p != null) list.Add(p.position);
            return list;
        }

        /// <summary>构建一条折线（water=false 陆路 / true 水路）+ 绑定缓存路由。</summary>
        private void BuildPolyline(List<Vector2> wps, bool water)
        {
            if (wps == null || wps.Count < 2)
            {
                if (water) { ptsW = null; cumulativeW = null; lengthW = 0; route1 = default; }
                return;
            }

            int n = wps.Count;
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) p[i] = wps[i];

            var cum = new double[n];
            double len = 0;
            for (int i = 1; i < n; i++)
            {
                len += Vector2.Distance(p[i - 1], p[i]);
                cum[i] = len;
            }

            if (water)
            {
                ptsW = p; cumulativeW = cum; lengthW = len;
                // F4.1（ADR-034）：末段 = 汇合后共享尾段（岸），其余 = 水域航道
                route1 = new PathRoute(len, GetLane1PointAtDistance, len - cum[n - 2]);
            }
            else
            {
                pts = p; cumulative = cum; Length = len;
                route0 = new PathRoute(len, GetPointAtDistance);
            }
        }

        /// <summary>取线路路由（lane 1 无水路时回退 lane 0——单线章容错）。</summary>
        public PathRoute GetRoute(int lane) => lane == 1 && lengthW > 0 ? route1 : route0;

        // ============ 路面砖（视觉） ============

        private void RebuildRoad()
        {
            var road = transform.Find("Road");
            if (road == null)
            {
                var roadGo = new GameObject("Road");
                roadGo.transform.SetParent(transform, false);
                road = roadGo.transform;
            }
            var stale = new List<Transform>();
            foreach (Transform c in road) stale.Add(c);
            for (int i = stale.Count - 1; i >= 0; i--) Destroy(stale[i].gameObject);
            roadLand.Clear(); // ADR-035：砖随 Road 子物体整组销毁重建，潮汐注册表同步清
            roadLandOnly.Clear();
            roadWater.Clear();
            roadVoid.Clear(); // F1.2（ch5）：虚路砖同组清

            // 棋盘路面砖（1格=1世界单位，沿折线铺满整数格）。白图运行时自造（1×1），
            // 不依赖场景引用——旧场景重编译后即生效。
            var landTiles = CollectRoadTiles(pts); // 墨路（含两线汇合后的共享尾段）
            var voidTiles = ptsV != null ? CollectRoadTiles(ptsV) : null; // F2.4 修订：公共段要认出来
            voidTilesCache = voidTiles;
            foreach (var t in landTiles)
            {
                var tile = MakeRoadTile(road, t, RoadInk);
                roadLand.Add(tile);
                // 公共段（两路同格）= 换面时**不变**的砖；实路独有砖才参与「本面实 / 对面虚」
                if (voidTiles == null || !voidTiles.Contains(t)) roadLandOnly.Add(tile);
            }
            HashSet<Vector2Int> waterTiles = null;
            if (ptsW != null)
            {
                waterTiles = CollectRoadTiles(ptsW);
                foreach (var t in waterTiles)
                    if (!landTiles.Contains(t)) // 重合段（汇合后共享尾段）归墨路
                        roadWater.Add(MakeRoadTile(road, t, WaterRoad));
            }
            // F1.2（ch5 双面）：虚路砖——与墨路/水路重合段不重铺（同一格只留一块砖，
            // 换面按「面」整层调色，不靠叠砖）。单面章 ptsV=null → 零进入。
            if (voidTiles != null)
            {
                foreach (var t in voidTiles)
                    if (!landTiles.Contains(t) && (waterTiles == null || !waterTiles.Contains(t)))
                        roadVoid.Add(MakeRoadTile(road, t, VoidRoad));
            }
        }

        /// <summary>
        /// F2.4 修订（用户 2026-10-22 点题）：换面时路面「**本面变实 / 对面变虚 / 公共段不变**」。
        /// 实面 = 深墨 `RoadInk`、虚面 = 淡墨 `VoidRoad`；公共段（两路同格）恒 RoadInk 不动。
        /// 0.35s 交叉淡入（与 0.2s 纸闪同手感，不拖沓）。
        /// 单面章（ptsV == null，ch1-4）早退 = 零变化；ch5 无潮汐 → 不会与 SetRoadTide 抢色。
        /// </summary>
        public void ApplyFaceRoadTint(int face)
        {
            if (voidTilesCache == null) return;
            Color near = RoadInk, far = VoidRoad;
            foreach (var t in roadLandOnly) t.Begin(face == 0 ? near : far, 0f, FaceRoadFadeSeconds);
            foreach (var t in roadVoid) t.Begin(face == 1 ? near : far, 0f, FaceRoadFadeSeconds);
        }

        private const float FaceRoadFadeSeconds = 0.35f;

        private TideTileFade MakeRoadTile(Transform road, Vector2Int t, Color color)
        {
            var tileGo = new GameObject("R" + t.x + "_" + t.y, typeof(SpriteRenderer));
            tileGo.transform.SetParent(road, false);
            tileGo.transform.localPosition = new Vector3(t.x, t.y, 0.5f); // 格子层(0.06)之后、水面层(0.7)之前
            var sr = tileGo.GetComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite;
            sr.color = color;
            return tileGo.AddComponent<TideTileFade>(); // ADR-035：潮汐渐变（水路加深 / 墨路淹没）
        }

        /// <summary>
        /// ADR-035 路面砖随潮（GameManager 水线事件驱动，与格子淹没同扫掠参数同节奏）：
        /// 水路砖 = 水线 ≥ 1 加深一档、退潮回蓝；墨路砖 = 水线 ≥ 4（全图洪水）淹没。
        /// ch1 无水路砖、水线恒 0-3 → 墨路砖同色早退零变化。
        /// </summary>
        public void SetRoadTide(int waterLevel, bool rising, float perCol, int minX, int maxX)
        {
            float span = perCol > 0f ? (maxX - minX) * perCol : 0f;
            Color water = waterLevel >= 1 ? WaterRoadDeep : WaterRoad;
            Color land = waterLevel >= 4 ? RoadFlooded : RoadInk;
            foreach (var t in roadWater)
                t.Begin(water, SweepDelay(t.Col, rising, perCol, minX, maxX, span));
            foreach (var t in roadLand)
                t.Begin(land, SweepDelay(t.Col, rising, perCol, minX, maxX, span));
        }

        /// <summary>扫掠延迟（列错峰，涨潮左→右 / 退潮右→左；格界外的列钳在总跨度内）。</summary>
        private static float SweepDelay(int col, bool rising, float perCol, int minX, int maxX, float span)
        {
            if (perCol <= 0f) return 0f;
            return Mathf.Clamp(rising ? (col - minX) * perCol : (maxX - col) * perCol, 0f, span);
        }

        /// <summary>沿折线收集路面格（水平段铺整行、垂直段铺整列；折点整数坐标时与棋盘严丝合缝）。
        /// 公开静态：ProjectSetup 章图机器校验复用（F1.3，压路检查）。</summary>
        public static HashSet<Vector2Int> CollectRoadTiles(Vector2[] polyline)
        {
            var tiles = new HashSet<Vector2Int>();
            if (polyline == null || polyline.Length < 2) return tiles;
            for (int i = 1; i < polyline.Length; i++)
            {
                Vector2 a = polyline[i - 1], b = polyline[i];
                if (Mathf.Approximately(a.y, b.y))
                {
                    int y = Mathf.RoundToInt(a.y);
                    int x0 = Mathf.RoundToInt(Mathf.Min(a.x, b.x));
                    int x1 = Mathf.RoundToInt(Mathf.Max(a.x, b.x));
                    for (int x = x0; x <= x1; x++) tiles.Add(new Vector2Int(x, y));
                }
                else if (Mathf.Approximately(a.x, b.x))
                {
                    int x = Mathf.RoundToInt(a.x);
                    int y0 = Mathf.RoundToInt(Mathf.Min(a.y, b.y));
                    int y1 = Mathf.RoundToInt(Mathf.Max(a.y, b.y));
                    for (int y = y0; y <= y1; y++) tiles.Add(new Vector2Int(x, y));
                }
            }
            return tiles;
        }

        private static Sprite _white;

        /// <summary>运行时 1×1 白图（Sprite.Create 独立于任何资产）。</summary>
        private static Sprite WhiteSprite
        {
            get
            {
                if (_white == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    _white = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                }
                return _white;
            }
        }

        // ============ 距离 → 坐标 ============

        /// <summary>按已走路程取世界坐标（lane 0 陆路）。</summary>
        public Vector2 GetPointAtDistance(double d)
        {
            if (pts == null || pts.Length == 0) return Vector2.zero;
            return PointOnPolyline(pts, cumulative, d);
        }

        /// <summary>按已走路程取世界坐标（lane 1 水路）。</summary>
        private Vector2 GetLane1PointAtDistance(double d)
        {
            if (ptsW == null || ptsW.Length == 0) return Vector2.zero;
            return PointOnPolyline(ptsW, cumulativeW, d);
        }

        private static Vector2 PointOnPolyline(Vector2[] p, double[] cum, double d)
        {
            double length = cum[cum.Length - 1];
            if (d <= 0) return p[0];
            if (d >= length) return p[p.Length - 1];

            int seg = 1;
            while (seg < cum.Length - 1 && cum[seg] < d) seg++;

            double segStart = cum[seg - 1];
            double segLen = cum[seg] - segStart;
            float t = segLen <= 0 ? 0f : (float)((d - segStart) / segLen);
            return Vector2.Lerp(p[seg - 1], p[seg], t);
        }

        private void OnDrawGizmosSelected()
        {
            // 编辑器预览：选中 Path 物体时显示折线
            var list = new List<Vector3>();
            if (points != null)
                foreach (var p in points) if (p != null) list.Add(p.position);
            else
                foreach (Transform child in transform) list.Add(child.position);

            Gizmos.color = new Color(0.15f, 0.14f, 0.12f, 0.8f);
            for (int i = 1; i < list.Count; i++)
            {
                Gizmos.DrawLine(list[i - 1], list[i]);
                Gizmos.DrawSphere(list[i], 0.15f);
            }
            if (list.Count > 0) Gizmos.DrawSphere(list[0], 0.28f);
        }
    }
}
