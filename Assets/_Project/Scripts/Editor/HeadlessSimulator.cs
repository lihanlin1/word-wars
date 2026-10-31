using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace WordWars.EditorTools
{
    /// <summary>
    /// Headless 战斗模拟器（Phase 0.4 = ch1 胜率收官；F6 直接扩展跑 ch2 双线/潮汐）。
    /// 纯 BattleModel 驱动：无场景/无视图/无协程——波次时间线与策略玩家在本文复刻
    /// （GameManager 驱动逻辑照抄：waveStart 串联 / SpawnGroup 间隔刷怪 / 波清 +bonus 墨 /
    /// CheckWon 口径 = 波次计数不含分裂虫召唤物）。
    ///
    /// 胜率区间（08 任务单 Phase 0.4 收官口径）：标准关 60-85% / L4 55-75% / Boss 45-65%。
    ///
    /// 策略玩家 v1（保守下界口径）：
    /// - 抽：墨够就补满手牌（一口价）；
    /// - 放：手牌按价值降序，各挑「覆盖度」最高空格（覆盖度 = 射程内路径采样点数，对空塔另计空路 0.5 权重）；
    /// - 合并：同字同级贪心合并，并进覆盖度高的那格（移动免费）；
    /// - 垦：手有铲 + 有荒地 + 剩余墨 ≥ 加速价 → 即刻完成（消耗铲）；
    /// - 不提前召唤、不卖卡。构筑 >5 卡按 DeckPriority 挑 5。
    /// 报表：Console 汇总 + design/numbers/sim/ch1_headless.csv（与数值母本同处，双轨不回写）。
    /// </summary>
    public static class HeadlessSimulator
    {
        private const int Runs = 100;
        private const double Dt = 0.1;
        private const double ThinkInterval = 0.5;
        private const double SampleStep = 0.25;

        // 构筑策略 v1（>5 卡的关挑 5）：弓对空必带 → 铲扩地 → 墨经济 → 刀矛 → 弩戟棍
        private static readonly string[] DeckPriority = { "弓", "铲", "墨", "刀", "矛", "弩", "戟", "棍" };

        [MenuItem("Word Wars/Setup/Headless 模拟（ch1 胜率报表）")]
        public static void Run()
        {
            var towerPool = AssetDatabase.LoadAssetAtPath<TowerPoolDef>("Assets/_Project/ScriptableObjects/Towers/TowerPool.asset");
            var enemyTable = AssetDatabase.LoadAssetAtPath<EnemyTableDef>("Assets/_Project/ScriptableObjects/Enemies/EnemyTable.asset");
            var gameParams = AssetDatabase.LoadAssetAtPath<GameParams>("Assets/_Project/ScriptableObjects/GameParams.asset");
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterConfig>("Assets/_Project/ScriptableObjects/Chapters/Chapter1.asset");
            if (towerPool == null || enemyTable == null || gameParams == null || chapter == null)
            {
                EditorUtility.DisplayDialog("资产缺失", "塔字池/敌人表/参数/章图 SO 加载失败（先跑自愈）。", "知道了");
                Debug.LogError("[Headless] 资产缺失：TowerPool / EnemyTable / GameParams / Chapter1");
                return;
            }

            var levels = new LevelConfig[6];
            for (int i = 0; i <= 5; i++)
            {
                levels[i] = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Project/ScriptableObjects/Levels/Level" + i + ".asset");
                if (levels[i] == null)
                {
                    EditorUtility.DisplayDialog("关卡缺失", "Level" + i + ".asset 不存在——先跑自愈。", "知道了");
                    return;
                }
            }

            // 敌表口径披露（双轨：资产值 vs CSV 母本可能已分叉——虎/酋长是锚点校验对象）
            foreach (var id in new[] { "虎", "酋长" })
            {
                var s = enemyTable.Find(id);
                if (s != null) Debug.Log($"[Headless] 敌表口径 {id}：HP {s.hp:0}（模拟读资产值，非 CSV 母本）");
            }

            // 战场几何：线路集 + 路径采样（覆盖度评分用）
            var pathSet = new SimPathSet(chapter);
            var groundSamples = SamplePolyline(chapter.landRoute, SampleStep);
            var airSamples = new List<Vector2>(groundSamples.Count);
            foreach (var p in groundSamples) airSamples.Add(p + Vector2.up * BattleModel.FlyerOffsetY);

            var baseEmpty = new List<Vector2Int>();
            var baseFallow = new List<Vector2Int>();
            foreach (var c in chapter.cells)
            {
                if (c.state == CellState.Empty) baseEmpty.Add(new Vector2Int(c.x, c.y));
                else if (c.state == CellState.Fallow) baseFallow.Add(new Vector2Int(c.x, c.y));
            }

            var csv = new StringBuilder();
            csv.AppendLine("关,runs,胜,胜率%,均塔,均阶,剩余墨,波清,区间%,判定,构筑");
            Debug.Log($"[Headless] 开跑：L1-L5 × {Runs} 局（策略 v1：贪心放置/合并，铲走加速开垦；dt={Dt}s）");

            bool canceled = false;
            for (int lvl = 1; lvl <= 5 && !canceled; lvl++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Headless 模拟", $"L{lvl} …", (lvl - 1) / 5f))
                {
                    canceled = true;
                    break;
                }

                var level = levels[lvl];

                // 构筑口径（复刻 RequestEnterLevel/EffectivePool/AddCard 自动上位）：
                // owned = levels[0..lvl].rewardCards 并集；池 = poolChars ∩ owned；>5 卡按优先级挑 5
                var owned = new List<string>();
                for (int i = 0; i <= lvl; i++)
                    foreach (var card in levels[i].rewardCards)
                        if (!string.IsNullOrEmpty(card) && !owned.Contains(card)) owned.Add(card);
                var poolChars = new List<string>();
                foreach (var c in level.poolChars)
                    if (owned.Contains(c) && towerPool.Find(c) != null && !poolChars.Contains(c)) poolChars.Add(c);
                var deck = new List<string>(poolChars);
                if (deck.Count > BackpackSystem.DeckSize)
                {
                    deck.Clear();
                    foreach (var c in DeckPriority)
                        if (poolChars.Contains(c) && deck.Count < BackpackSystem.DeckSize) deck.Add(c);
                    foreach (var c in poolChars) // 优先级未覆盖的兜底补满
                        if (!deck.Contains(c) && deck.Count < BackpackSystem.DeckSize) deck.Add(c);
                }
                int drawCost = 0;
                foreach (var c in deck) drawCost += towerPool.Find(c).value;

                int wins = 0;
                double sumTowers = 0, sumLevels = 0, sumInk = 0, sumWaves = 0;
                for (int run = 0; run < Runs; run++)
                {
                    var r = SimulateOne(level, deck, drawCost, towerPool, enemyTable, gameParams,
                        pathSet, baseEmpty, baseFallow, groundSamples, airSamples, 1000 + run);
                    if (r.won) wins++;
                    sumTowers += r.towers;
                    sumLevels += r.avgLevel;
                    sumInk += r.inkLeft;
                    sumWaves += r.wavesCleared;
                }

                double winrate = 100.0 * wins / Runs;
                string band = lvl == 4 ? "55-75" : lvl == 5 ? "45-65" : "60-85";
                string verdict = winrate < ParseBand(band, true) ? "偏低" : winrate > ParseBand(band, false) ? "偏高" : "达标";
                Debug.Log($"[Headless] L{lvl}「{level.title}」胜率 {winrate:0}%（{wins}/{Runs}）｜均塔 {sumTowers / Runs:0.0} 均阶 {sumLevels / Runs:0.00}｜剩墨 {sumInk / Runs:0}｜波清 {sumWaves / Runs:0.0}/{level.waves.Count}｜区间 {band}% {verdict}｜构筑 {string.Join("", deck.ToArray())}｜一口价 {drawCost}");
                csv.AppendLine($"L{lvl},{Runs},{wins},{winrate:0},{sumTowers / Runs:0.0},{sumLevels / Runs:0.00},{sumInk / Runs:0},{sumWaves / Runs:0.0},{band},{verdict},{string.Join("", deck.ToArray())}");
            }

            EditorUtility.ClearProgressBar();
            if (!canceled) WriteReport(csv.ToString());
            else Debug.LogWarning("[Headless] 用户取消——报表未落盘");
        }

        // ============ 单局模拟 ============

        private struct RunResult
        {
            public bool won;
            public int towers;
            public double avgLevel;
            public double inkLeft;
            public int wavesCleared;
        }

        private static RunResult SimulateOne(
            LevelConfig level, List<string> deck, int drawCost,
            TowerPoolDef towerPool, EnemyTableDef enemyTable, GameParams gp,
            SimPathSet pathSet, List<Vector2Int> baseEmpty, List<Vector2Int> baseFallow,
            List<Vector2> groundSamples, List<Vector2> airSamples, int seed)
        {
            var model = new BattleModel
            {
                Paths = pathSet,
                EnemyStatsLookup = enemyTable.Find,
                MaxTowerLevel = gp.maxTowerLevel,
                DamageGrowth = gp.damageGrowth,
                InkGrowth = gp.inkGrowth,
                InkPulseSeconds = gp.inkPulseSeconds,
            };
            model.Reset(SaveSystem.InitialInk(0), level.startLives); // F1.2（ADR-053）：与实机同源（headless 无档 = 0 步 = 60）

            var hand = new HandModel(new List<string>(deck), gp.handSize, drawCost, seed);

            var empty = new List<Vector2Int>(baseEmpty);
            var fallow = new List<Vector2Int>(baseFallow);
            var towerCell = new Dictionary<TowerState, Vector2Int>();

            // 波次时间线（复刻 ComputeWaveSchedule）
            int waveCount = level.waves.Count;
            var waveStart = new double[waveCount];
            for (int i = 0; i < waveCount; i++)
                waveStart[i] = i == 0 ? level.prepSeconds : waveStart[i - 1] + level.waves[i].startAfterPrev;

            // 波清计数（口径 = 真实游戏 waveRuntimes：只计波表刷出的敌，分裂虫/召唤物不计）。
            // waveResolved[w] = 第 w 波表刷敌中已结算（死/漏）数——胜败与波奖都看它。
            var enemyWaveOf = new Dictionary<EnemyState, int>();
            var waveResolved = new int[waveCount];
            var waveTotal = new int[waveCount];
            var waveBonusGiven = new bool[waveCount];
            model.EnemyDied += e => { if (enemyWaveOf.TryGetValue(e, out int w)) waveResolved[w]++; };
            model.EnemyReached += e => { if (enemyWaveOf.TryGetValue(e, out int w)) waveResolved[w]++; };

            var pending = new List<(double at, string charId, int wave)>();
            int nextWave = 0;
            double t = 0, think = 0;
            double timeout = (waveCount > 0 ? waveStart[waveCount - 1] : 0) + 180;
            bool won = false;

            while (t < timeout)
            {
                if (model.Failed) break;

                // 策略思考（0.5s 一拍）
                think += Dt;
                if (think >= ThinkInterval)
                {
                    think = 0;
                    Think(model, hand, gp, towerPool, empty, fallow, towerCell, groundSamples, airSamples);
                }

                // 波次推进（无提前召唤策略——保守口径）
                while (nextWave < waveCount && t >= waveStart[nextWave])
                {
                    foreach (var g in level.waves[nextWave].groups)
                    {
                        waveTotal[nextWave] += g.count;
                        for (int k = 0; k < g.count; k++)
                            pending.Add((t + k * g.interval, g.charId, nextWave));
                    }
                    nextWave++;
                }

                // 到点刷怪（复刻 SpawnGroupRoutine 的间隔节奏）
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (pending[i].at > t) continue;
                    var s = enemyTable.Find(pending[i].charId);
                    if (s != null)
                    {
                        var e = model.SpawnEnemy(s);
                        enemyWaveOf[e] = pending[i].wave; // 只登记波属；结算数走 EnemyDied/Reached 事件
                    }
                    pending.RemoveAt(i);
                }

                model.Tick(Dt);
                t += Dt;

                // 波清奖励（复刻 TryWaveCleared：波表敌全结算 → +bonus 一次）
                for (int w = 0; w < nextWave; w++)
                {
                    if (waveBonusGiven[w] || waveResolved[w] < waveTotal[w]) continue;
                    waveBonusGiven[w] = true;
                    model.Ink += gp.waveClearBonus;
                }

                // 胜利判定（复刻 CheckWon：全波已发 + 无待刷 + 波表敌全结算；场上余孽不阻塞）
                if (nextWave >= waveCount && pending.Count == 0 && !model.Failed)
                {
                    bool done = true;
                    for (int w = 0; w < waveCount; w++)
                        if (waveResolved[w] < waveTotal[w]) { done = false; break; }
                    if (done) { won = true; break; }
                }
            }

            int towers = model.Towers.Count;
            double levelSum = 0;
            foreach (var tw in model.Towers) levelSum += tw.level;
            int wavesCleared = 0;
            for (int w = 0; w < waveCount; w++) if (waveBonusGiven[w]) wavesCleared++;

            return new RunResult
            {
                won = won,
                towers = towers,
                avgLevel = towers > 0 ? levelSum / towers : 0,
                inkLeft = model.Ink,
                wavesCleared = wavesCleared,
            };
        }

        // ============ 策略玩家（v1：抽满→合并→放置→开垦） ============

        private static void Think(
            BattleModel model, HandModel hand, GameParams gp, TowerPoolDef towerPool,
            List<Vector2Int> empty, List<Vector2Int> fallow, Dictionary<TowerState, Vector2Int> towerCell,
            List<Vector2> groundSamples, List<Vector2> airSamples)
        {
            // ① 抽：一口价补满（放置免费——墨的唯一大 sink）
            while (hand.CanDraw && hand.HasSource && model.Ink >= hand.DrawCost)
            {
                if (!hand.TryDraw(out _)) break;
                model.Ink -= hand.DrawCost;
            }

            // ② 合并：同字同级贪心并到覆盖度高的格（移动免费，ADR-018）
            bool mergedAny = true;
            while (mergedAny)
            {
                mergedAny = false;
                TowerState source = null, keep = null;
                foreach (var a in model.Towers)
                {
                    foreach (var b in model.Towers)
                    {
                        if (a == b || a.charId != b.charId || a.level != b.level) continue;
                        if (a.level >= model.MaxTowerLevel) continue;
                        var defA = towerPool.Find(a.charId);
                        double sa = CellScore(defA, towerCell[a], groundSamples, airSamples);
                        double sb = CellScore(defA, towerCell[b], groundSamples, airSamples);
                        if (sa >= sb) { source = b; keep = a; } else { source = a; keep = b; }
                        break;
                    }
                    if (source != null) break;
                }
                if (source != null && keep != null)
                {
                    if (model.MergeTowers(source, keep))
                    {
                        towerCell.Remove(source);
                        mergedAny = true;
                    }
                }
            }

            // ③ 放置：手牌按价值降序，各挑覆盖度最高的空格（放置免费）
            if (empty.Count > 0)
            {
                var toPlace = new List<string>(hand.Cards);
                toPlace.Sort((x, y) => towerPool.Find(y).value.CompareTo(towerPool.Find(x).value));
                foreach (var card in toPlace)
                {
                    if (card == HandModel.ShovelId) continue; // 铲只走开垦
                    if (empty.Count == 0) break;
                    var def = towerPool.Find(card);
                    if (def == null) continue;
                    int best = -1;
                    double bestScore = 0;
                    for (int i = 0; i < empty.Count; i++)
                    {
                        double s = CellScore(def, empty[i], groundSamples, airSamples);
                        if (s > bestScore) { bestScore = s; best = i; }
                    }
                    if (best < 0) break; // 没有一个格能覆盖到路（剩卡留手）

                    var cellPos = empty[best];
                    empty.RemoveAt(best);
                    int at = hand.Cards.IndexOf(card);
                    if (at >= 0) hand.ConsumeAt(at);

                    var t = new TowerState
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
                        level = 1,
                        position = new Vector2(cellPos.x, cellPos.y),
                        chainJumps = def.chainJumps, // F3.2：链电参数（headless 与实机同源）
                        chainRatio = def.chainRatio,
                        chainRadius = def.chainRadius,
                        voidDamageMult = def.voidDamageMult, // F3.3：虚面加成（煞）
                        soulBanner = def.soulBanner,         // F3.4：魂幡（幡）
                        soulPayout = def.soulPayout,
                    };
                    model.AddTower(t);
                    towerCell[t] = cellPos;
                }
            }

            // ④ 开垦（铲）：有铲 + 有荒地 + 剩墨够加速 → 即刻完成换一格空地
            while (empty.Count == 0 && fallow.Count > 0 &&
                   hand.Cards.Contains(HandModel.ShovelId) && model.Ink >= gp.farmSpeedupCost)
            {
                int si = hand.Cards.IndexOf(HandModel.ShovelId);
                hand.ConsumeAt(si);
                model.Ink -= gp.farmSpeedupCost;
                empty.Add(fallow[fallow.Count - 1]);
                fallow.RemoveAt(fallow.Count - 1);
            }
        }

        /// <summary>覆盖度（选位启发）：射程内地面路采样点数 + 对空塔另计空路（0.5 权重）。</summary>
        private static double CellScore(TowerCharStats def, Vector2Int cell,
            List<Vector2> groundSamples, List<Vector2> airSamples)
        {
            if (def == null) return 0;
            double r2 = def.range * def.range;
            var pos = new Vector2(cell.x, cell.y);
            double s = 0;
            foreach (var p in groundSamples)
                if ((p - pos).sqrMagnitude <= r2) s += 1;
            if (def.antiAir)
                foreach (var p in airSamples)
                    if ((p - pos).sqrMagnitude <= r2) s += 0.5;
            return s;
        }

        private static double ParseBand(string band, bool low)
        {
            var parts = band.Split('-');
            return double.Parse(low ? parts[0] : parts[1], CultureInfo.InvariantCulture);
        }

        // ============ 线路集（纯 C# 版 PathSystem，无 MonoBehaviour） ============

        private class SimPathSet : IPathSet
        {
            private readonly PathRoute land;
            private readonly PathRoute water;

            public SimPathSet(ChapterConfig ch)
            {
                land = Build(ch.landRoute, false);
                water = Build(ch.waterRoute, true);
            }

            public PathRoute Get(int lane) => lane == 1 && water.PointAt != null ? water : land;

            /// <summary>F1.3 接口同步：sim 只跑 ch1 单向章——口回退 lane 全程（真实双口在 PathSystem）。</summary>
            public PathRoute GetPort(int lane, int port) => Get(lane);

            /// <summary>F1.2 接口同步：sim 只跑单面章——面回退陆路（真实双面在 PathSystem）。</summary>
            public PathRoute GetFace(int face) => land;

            private static PathRoute Build(List<Vector2> wps, bool waterLane)
            {
                if (wps == null || wps.Count < 2) return default;
                var cum = new double[wps.Count];
                double len = 0;
                for (int i = 1; i < wps.Count; i++)
                {
                    len += Vector2.Distance(wps[i - 1], wps[i]);
                    cum[i] = len;
                }
                Vector2[] pts = wps.ToArray();
                // F4.1（ADR-034）：水路末段 = 汇合后共享尾段（岸）——与 PathSystem 同口径
                double waterUntil = waterLane ? len - cum[cum.Length - 2] : 0;
                return new PathRoute(len, d => PointAt(pts, cum, d), waterUntil);
            }

            private static Vector2 PointAt(Vector2[] p, double[] cum, double d)
            {
                double length = cum[cum.Length - 1];
                if (d <= 0) return p[0];
                if (d >= length) return p[p.Length - 1];
                int seg = 1;
                while (seg < cum.Length - 1 && cum[seg] < d) seg++;
                double segStart = cum[seg - 1];
                double segLen = cum[seg] - segStart;
                float k = segLen <= 0 ? 0f : (float)((d - segStart) / segLen);
                return Vector2.Lerp(p[seg - 1], p[seg], k);
            }
        }

        /// <summary>沿折线采样（覆盖度评分用；含首尾）。</summary>
        private static List<Vector2> SamplePolyline(List<Vector2> wps, double step)
        {
            var list = new List<Vector2>();
            if (wps == null || wps.Count < 2) return list;
            for (int i = 1; i < wps.Count; i++)
            {
                Vector2 a = wps[i - 1], b = wps[i];
                int n = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(a, b) / step));
                for (int k = 0; k < n; k++) list.Add(Vector2.Lerp(a, b, (float)k / n));
            }
            list.Add(wps[wps.Count - 1]);
            return list;
        }

        // ============ 报表落盘 ============

        private static void WriteReport(string csv, string fileName = "ch1_headless.csv")
        {
            try
            {
                // dataPath = <工程>/Assets → 上两级 = 仓库根（design/ 所在）
                string root = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
                string dir = Path.Combine(root, "design", "numbers", "sim");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, fileName);
                File.WriteAllText(path, csv, new UTF8Encoding(true)); // BOM：Excel 中文直开
                Debug.Log("[Headless] 报表落盘：" + path);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Headless] 报表落盘失败（Console 汇总仍有效）：" + ex.Message);
            }
        }

        // ============ F6.1 · ch2 按章连跑（双线 + 潮汐档位 + 塔保留 + 蛟两幕剧，08 任务单） ============

        private const int RunsCh2 = 200;

        // ch2 构筑优先（>5 卡挑 5）：水系三卡 = 章身份 + 蛟二阶段反制位优先；弓保对空；其余沿 ch1 序
        private static readonly string[] DeckPriorityCh2 = { "水", "冰", "渔", "弓", "铲", "墨", "刀", "矛", "弩", "戟", "棍" };

        /// <summary>章图格信息：放置合法性/淹没/撤离在策略侧复刻 GameManager 口径（海拔 + 水格态）。</summary>
        private struct CellInfo
        {
            public CellState state;
            public int elevation;
        }

        private struct Ch2RunResult
        {
            public bool won;
            public int towers;
            public double avgLevel;
            public double inkLeft;
            public int wavesCleared;
            public int drowned;
            public List<(string charId, int level, Vector2Int cell)> carried;
        }

        [MenuItem("Word Wars/Setup/Headless 模拟（ch2 按章连跑）")]
        public static void RunChapter2()
        {
            var towerPool = AssetDatabase.LoadAssetAtPath<TowerPoolDef>("Assets/_Project/ScriptableObjects/Towers/TowerPool.asset");
            var enemyTable = AssetDatabase.LoadAssetAtPath<EnemyTableDef>("Assets/_Project/ScriptableObjects/Enemies/EnemyTable.asset");
            var gameParams = AssetDatabase.LoadAssetAtPath<GameParams>("Assets/_Project/ScriptableObjects/GameParams.asset");
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterConfig>("Assets/_Project/ScriptableObjects/Chapters/Chapter2.asset");
            if (towerPool == null || enemyTable == null || gameParams == null || chapter == null)
            {
                EditorUtility.DisplayDialog("资产缺失", "塔字池/敌人表/参数/Chapter2 加载失败（先跑自愈）。", "知道了");
                Debug.LogError("[Headless] 资产缺失：TowerPool / EnemyTable / GameParams / Chapter2");
                return;
            }
            if (chapter.waterRoute == null || chapter.waterRoute.Count < 2)
            {
                EditorUtility.DisplayDialog("章图缺水路", "Chapter2.waterRoute 缺失——双线图是 ch2 模拟的前提。", "知道了");
                return;
            }

            // levels 0-10 全载（owned 并集口径 = levels[0..lvl].rewardCards，与 ch1 同法）
            var levels = new LevelConfig[11];
            for (int i = 0; i <= 10; i++)
            {
                levels[i] = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Project/ScriptableObjects/Levels/Level" + i + ".asset");
                if (levels[i] == null)
                {
                    EditorUtility.DisplayDialog("关卡缺失", "Level" + i + ".asset 不存在——先跑自愈。", "知道了");
                    return;
                }
            }

            // 锚点披露（双轨：模拟读资产值非 CSV 母本；蛟 = F6.2 定稿校验对象，锚 10000-16000）
            var jiao = enemyTable.Find("蛟");
            if (jiao != null) Debug.Log($"[Headless] 敌表口径 蛟：HP {jiao.hp:0}（锚 10000-16000）");

            // 战场几何：双线采样（陆路覆盖度 + 水路覆盖度 + 空路）与格信息
            var pathSet = new SimPathSet(chapter);
            var groundSamples = SamplePolyline(chapter.landRoute, SampleStep);
            var waterSamples = SamplePolyline(chapter.waterRoute, SampleStep);
            var airSamples = new List<Vector2>(groundSamples.Count);
            foreach (var p in groundSamples) airSamples.Add(p + Vector2.up * BattleModel.FlyerOffsetY);

            var cellInfo = new Dictionary<Vector2Int, CellInfo>();
            var baseEmpty = new List<Vector2Int>();
            var baseFallow = new List<Vector2Int>();
            var baseWater = new List<Vector2Int>(); // 永久水格：水系三卡专属地皮（F3.2），普通塔禁入
            foreach (var c in chapter.cells)
            {
                cellInfo[new Vector2Int(c.x, c.y)] = new CellInfo { state = c.state, elevation = c.elevation };
                if (c.state == CellState.Empty) baseEmpty.Add(new Vector2Int(c.x, c.y));
                else if (c.state == CellState.Fallow) baseFallow.Add(new Vector2Int(c.x, c.y));
                else if (c.state == CellState.Water) baseWater.Add(new Vector2Int(c.x, c.y));
            }

            // 构筑 per 关（二L1-L5）：owned = levels[0..lvl].rewardCards 并集 ∩ 关卡池；>5 按优先级挑 5
            var decks = new List<string>[5];
            var drawCosts = new int[5];
            for (int k = 0; k < 5; k++)
            {
                int lvl = 6 + k;
                var owned = new List<string>();
                for (int i = 0; i <= lvl; i++)
                    foreach (var card in levels[i].rewardCards)
                        if (!string.IsNullOrEmpty(card) && !owned.Contains(card)) owned.Add(card);
                var poolChars = new List<string>();
                foreach (var c in levels[lvl].poolChars)
                    if (owned.Contains(c) && towerPool.Find(c) != null && !poolChars.Contains(c)) poolChars.Add(c);
                var deck = new List<string>(poolChars);
                if (deck.Count > BackpackSystem.DeckSize)
                {
                    deck.Clear();
                    foreach (var c in DeckPriorityCh2)
                        if (poolChars.Contains(c) && deck.Count < BackpackSystem.DeckSize) deck.Add(c);
                    foreach (var c in poolChars)
                        if (!deck.Contains(c) && deck.Count < BackpackSystem.DeckSize) deck.Add(c);
                }
                decks[k] = deck;
                foreach (var c in deck) drawCosts[k] += towerPool.Find(c).value;
            }

            Debug.Log($"[Headless] ch2 开跑：二L1-L5 连跑 × {RunsCh2} 旅程（策略 v2：潮汐预报选位/免费撤离/水系三卡；dt={Dt}s）｜" +
                $"格：空 {baseEmpty.Count} 荒 {baseFallow.Count} 水 {baseWater.Count}｜条件胜率口径 = 分母为到达数（塔保留旅程）");

            var csv = new StringBuilder();
            csv.AppendLine("关,runs,到达,胜,胜率%,区间%,判定,均塔,均阶,淹死塔,波清,构筑");
            var reached = new int[5];
            var won = new int[5];
            var sumTowers = new double[5];
            var sumLevels = new double[5];
            var sumDrown = new double[5];
            var sumWaves = new double[5];
            bool canceled = false;

            for (int run = 0; run < RunsCh2 && !canceled; run++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Headless ch2 模拟", $"旅程 {run + 1}/{RunsCh2}", (float)run / RunsCh2))
                {
                    canceled = true;
                    break;
                }

                // 一条旅程 = 二L1→二L5 串行（失败止步）；empty/fallow/塔在关间照搬（ADR-018 快照语义）
                var empty = new List<Vector2Int>(baseEmpty);
                var fallow = new List<Vector2Int>(baseFallow);
                var waterCells = new List<Vector2Int>(baseWater);
                var carried = new List<(string, int, Vector2Int)>();

                for (int k = 0; k < 5; k++)
                {
                    var r = SimulateCh2Level(levels[6 + k], decks[k], drawCosts[k], towerPool, enemyTable, gameParams,
                        pathSet, cellInfo, groundSamples, waterSamples, airSamples, empty, fallow, waterCells, carried, 7000 + run * 5 + k);
                    reached[k]++;
                    sumTowers[k] += r.towers;
                    sumLevels[k] += r.avgLevel;
                    sumDrown[k] += r.drowned;
                    sumWaves[k] += r.wavesCleared;
                    if (!r.won) break; // 旅程止步本关
                    won[k]++;
                    carried = r.carried;
                }
            }

            EditorUtility.ClearProgressBar();
            for (int k = 0; k < 5; k++)
            {
                var level = levels[6 + k];
                int denom = Math.Max(1, reached[k]);
                double rate = reached[k] > 0 ? 100.0 * won[k] / reached[k] : 0;
                string band = k == 3 ? "55-75" : k == 4 ? "45-65" : "60-85";
                string verdict = reached[k] == 0 ? "未到达" :
                    rate < ParseBand(band, true) ? "偏低" : rate > ParseBand(band, false) ? "偏高" : "达标";
                Debug.Log($"[Headless] 二L{k + 1}「{level.title}」条件胜率 {rate:0}%（{won[k]}/{reached[k]} 到达）｜均塔 {sumTowers[k] / denom:0.0} 均阶 {sumLevels[k] / denom:0.00}｜淹死塔 {sumDrown[k] / denom:0.0}｜波清 {sumWaves[k] / denom:0.0}/{level.waves.Count}｜区间 {band}% {verdict}｜构筑 {string.Join("", decks[k].ToArray())}｜一口价 {drawCosts[k]}");
                csv.AppendLine($"二L{k + 1},{RunsCh2},{reached[k]},{won[k]},{rate:0},{band},{verdict},{sumTowers[k] / denom:0.0},{sumLevels[k] / denom:0.00},{sumDrown[k] / denom:0.0},{sumWaves[k] / denom:0.0},{string.Join("", decks[k].ToArray())}");
            }

            double completion = 100.0 * won[4] / RunsCh2;
            Debug.Log($"[Headless] 章通关率（二L1→二L5 一命通关）：{completion:0}%（{won[4]}/{RunsCh2}）");
            csv.AppendLine($"章通关,{RunsCh2},,,{completion:0},,,,一命通关口径");

            if (!canceled) WriteReport(csv.ToString(), "ch2_headless.csv");
            else Debug.LogWarning("[Headless] 用户取消——报表未落盘");
        }

        /// <summary>
        /// ch2 单关模拟：ch1 SimulateOne 全套 + 潮汐（波开始切水线，GameManager.LaunchWave 同口径）
        /// + 蛟两幕剧（BossFloodRoutine 的 sim-time 镜像）+ 策略 v2（撤离/预报选位）+ 关间带塔。
        /// </summary>
        private static Ch2RunResult SimulateCh2Level(
            LevelConfig level, List<string> deck, int drawCost,
            TowerPoolDef towerPool, EnemyTableDef enemyTable, GameParams gp,
            SimPathSet pathSet, Dictionary<Vector2Int, CellInfo> cellInfo,
            List<Vector2> groundSamples, List<Vector2> waterSamples, List<Vector2> airSamples,
            List<Vector2Int> empty, List<Vector2Int> fallow, List<Vector2Int> waterCells,
            List<(string charId, int level, Vector2Int cell)> carriedIn, int seed)
        {
            var model = new BattleModel
            {
                Paths = pathSet,
                EnemyStatsLookup = enemyTable.Find,
                MaxTowerLevel = gp.maxTowerLevel,
                DamageGrowth = gp.damageGrowth,
                InkGrowth = gp.inkGrowth,
                InkPulseSeconds = gp.inkPulseSeconds,
                SubmergeSeconds = gp.submergeSeconds, // F2.2 淹没倒计时（ch1 sim 走缺省 20；ch2 对齐参数）
            };
            model.Reset(SaveSystem.InitialInk(0), level.startLives); // F1.2（ADR-053）：与实机同源（headless 无档 = 0 步 = 60）

            var hand = new HandModel(new List<string>(deck), gp.handSize, drawCost, seed);

            var towerCell = new Dictionary<TowerState, Vector2Int>();
            int drowned = 0;

            // 蛟两幕剧状态（闭包捕获 t：事件在 Tick 内同步触发 = 当前 sim 时刻）
            EnemyState bossDragon = null;
            double floodAt = double.PositiveInfinity, nextEbbAt = double.PositiveInfinity;
            bool floodDone = false, floodAborted = false;
            int ebbLevel = -1;
            double t = 0;

            model.TowerDrowned += tw =>
            {
                // 淹死格释放（按格态归位；水格实际不会出现——水系免疫不淹、普通塔只站空地格）
                if (towerCell.TryGetValue(tw, out var c))
                {
                    towerCell.Remove(tw);
                    if (cellInfo[c].state == CellState.Water) waterCells.Add(c); else empty.Add(c);
                }
                drowned++;
            };
            model.BossPhase2 += dragon =>
            {
                if (bossDragon != null) return; // 一次性防抖（蛟唯一）
                bossDragon = dragon;
                dragon.invulnerable = true; // 蓄水窗 i-frame + 定身（OnBossPhase2 同款，用户定案 2026-10-05）
                dragon.holdMove = true;
                floodAt = t + gp.bossChargeSeconds;
            };

            // 带塔回场（RestoreTowers 同口径：空地格 ← 空；水格 ← 水系免疫；脏数据跳过）
            foreach (var ct in carriedIn)
            {
                var def = towerPool.Find(ct.charId);
                if (def == null || !cellInfo.TryGetValue(ct.cell, out var info)) continue;
                if (info.state == CellState.Empty && empty.Contains(ct.cell)) empty.Remove(ct.cell);
                else if (info.state == CellState.Water && def.waterImmune && waterCells.Contains(ct.cell)) waterCells.Remove(ct.cell);
                else continue;
                var ts = BuildTower(def, ct.cell, Mathf.Clamp(ct.level, 1, model.MaxTowerLevel), cellInfo);
                model.AddTower(ts); // 内含 ApplyLevelStats（等级通道重算）
                towerCell[ts] = ct.cell;
            }

            // 波次时间线（同 ch1）
            int waveCount = level.waves.Count;
            var waveStart = new double[waveCount];
            for (int i = 0; i < waveCount; i++)
                waveStart[i] = i == 0 ? level.prepSeconds : waveStart[i - 1] + level.waves[i].startAfterPrev;

            var enemyWaveOf = new Dictionary<EnemyState, int>();
            var waveResolved = new int[waveCount];
            var waveTotal = new int[waveCount];
            var waveBonusGiven = new bool[waveCount];
            model.EnemyDied += e => { if (enemyWaveOf.TryGetValue(e, out int w)) waveResolved[w]++; };
            model.EnemyReached += e => { if (enemyWaveOf.TryGetValue(e, out int w)) waveResolved[w]++; };

            var pending = new List<(double at, string charId, int wave)>();
            int nextWave = 0;
            double think = 0;
            double timeout = (waveCount > 0 ? waveStart[waveCount - 1] : 0) + 300; // 蛟 13000 血 + 潮落 75s → 上限放宽
            bool won = false;

            while (t < timeout)
            {
                if (model.Failed) break;

                think += Dt;
                if (think >= ThinkInterval)
                {
                    think = 0;
                    // 潮汐预报（波次表可预读 = 潮汐表，06 §10.2）：未发波的最大潮位
                    double forecast = 0;
                    for (int w = nextWave; w < waveCount; w++)
                        if (level.waves[w].tide != TideLevel.Hold) forecast = Math.Max(forecast, (int)level.waves[w].tide);
                    ThinkCh2(model, hand, gp, towerPool, cellInfo, empty, fallow, waterCells, towerCell,
                        groundSamples, waterSamples, airSamples, forecast);
                }

                // 波次推进 + 潮位切换（GameManager.LaunchWave 同口径：波开始切水线，Hold 沿用）
                while (nextWave < waveCount && t >= waveStart[nextWave])
                {
                    var layout = level.waves[nextWave];
                    if (layout.tide != TideLevel.Hold) model.SetWaterLevel((int)layout.tide);
                    foreach (var g in layout.groups)
                    {
                        waveTotal[nextWave] += g.count;
                        for (int k = 0; k < g.count; k++)
                            pending.Add((t + k * g.interval, g.charId, nextWave));
                    }
                    nextWave++;
                }

                // 到点刷怪（复刻 SpawnGroupRoutine 的间隔节奏；召唤物走模型内管线不计波属）
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (pending[i].at > t) continue;
                    var s = enemyTable.Find(pending[i].charId);
                    if (s != null)
                    {
                        var e = model.SpawnEnemy(s);
                        enemyWaveOf[e] = pending[i].wave;
                    }
                    pending.RemoveAt(i);
                }

                model.Tick(Dt);
                t += Dt;

                // 蛟两幕剧推进（GameManager.BossFloodRoutine 同时序）：
                // 蓄水窗（i-frame 定身）→ 洪水 4 + 解除 + ×水乘 + 召唤暂停 → 潮落 4 档（首步 = 暂停窗结束）
                if (bossDragon != null && !floodDone && !floodAborted && t >= floodAt)
                {
                    if (bossDragon.dead || bossDragon.reached || model.Failed) floodAborted = true; // 抢杀/漏过/败：洪水从未发生
                    else
                    {
                        model.SetWaterLevel(4);
                        bossDragon.invulnerable = false;
                        bossDragon.holdMove = false;
                        bossDragon.waterSpeedMult = gp.bossPhase2WaterMult;
                        model.PauseSummons(gp.bossSummonPauseSeconds);
                        floodDone = true;
                        ebbLevel = 3;
                        nextEbbAt = t + gp.bossSummonPauseSeconds; // 潮落首步赶 20s 淹死线（实测口径）
                    }
                }
                if (floodDone && ebbLevel >= 0 && t >= nextEbbAt)
                {
                    model.SetWaterLevel(ebbLevel); // 4→3→2→1→0
                    ebbLevel--;
                    nextEbbAt = t + gp.bossEbbSeconds / 4.0;
                }

                // 波清奖励（复刻 TryWaveCleared）
                for (int w = 0; w < nextWave; w++)
                {
                    if (waveBonusGiven[w] || waveResolved[w] < waveTotal[w]) continue;
                    waveBonusGiven[w] = true;
                    model.Ink += gp.waveClearBonus;
                }

                // 胜利判定（同 ch1：波表敌全结算；召唤物/场上余孽不阻塞）
                if (nextWave >= waveCount && pending.Count == 0 && !model.Failed)
                {
                    bool done = true;
                    for (int w = 0; w < waveCount; w++)
                        if (waveResolved[w] < waveTotal[w]) { done = false; break; }
                    if (done) { won = true; break; }
                }
            }

            // 场末快照（蛟死 = 胜，潮照落塔照干——淹没中的塔一并带走，下场水位 0 自然干）
            var carriedOut = new List<(string charId, int level, Vector2Int cell)>();
            foreach (var tw in model.Towers)
                if (towerCell.TryGetValue(tw, out var c)) carriedOut.Add((tw.charId, tw.level, c));

            int towers = model.Towers.Count;
            double levelSum = 0;
            foreach (var tw in model.Towers) levelSum += tw.level;
            int wavesCleared = 0;
            for (int w = 0; w < waveCount; w++) if (waveBonusGiven[w]) wavesCleared++;

            return new Ch2RunResult
            {
                won = won,
                towers = towers,
                avgLevel = towers > 0 ? levelSum / towers : 0,
                inkLeft = model.Ink,
                wavesCleared = wavesCleared,
                drowned = drowned,
                carried = carriedOut,
            };
        }

        /// <summary>
        /// 策略玩家 v2（ch2）：抽满 → 撤离（F2.3 三选项的 sim 镜像：免费移动）→ 合并 →
        /// 放置（潮汐合法性 F3.2 + 预报安全优先）→ 开垦。普通塔只上干格；水系三卡全格合法
        /// （水格/淹没格常驻合法）；无干格可移 = 不动（等淹死——毕业考左半场的设计结局）。
        /// </summary>
        private static void ThinkCh2(
            BattleModel model, HandModel hand, GameParams gp, TowerPoolDef towerPool,
            Dictionary<Vector2Int, CellInfo> cellInfo, List<Vector2Int> empty, List<Vector2Int> fallow,
            List<Vector2Int> waterCells, Dictionary<TowerState, Vector2Int> towerCell,
            List<Vector2> groundSamples, List<Vector2> waterSamples, List<Vector2> airSamples,
            double forecastMaxTide)
        {
            // ① 抽：一口价补满（放置免费——墨的唯一大 sink）
            while (hand.CanDraw && hand.HasSource && model.Ink >= hand.DrawCost)
            {
                if (!hand.TryDraw(out _)) break;
                model.Ink -= hand.DrawCost;
            }

            // ② 撤离：已沉默的普通塔免费移到最优干格（两层：先「预报安全」再「当前干」；水系免疫无需撤离）
            for (int i = 0; i < model.Towers.Count; i++)
            {
                var t = model.Towers[i];
                if (!t.submerged || t.waterImmune) continue;
                var def = towerPool.Find(t.charId);
                if (def == null) continue;
                int best = -1;
                double bestScore = 0;
                for (int pass = 0; pass < 2 && best < 0; pass++)
                {
                    for (int c = 0; c < empty.Count; c++)
                    {
                        var info = cellInfo[empty[c]];
                        if (info.elevation <= model.WaterLevel) continue; // 只移当前干格
                        if (pass == 0 && info.elevation <= forecastMaxTide) continue; // 第一层：预报安全
                        double s = CellScore(def, empty[c], groundSamples, airSamples);
                        if (best < 0 || s > bestScore) { bestScore = s; best = c; } // 撤离 = 保命优先：干格哪怕零覆盖也收（塔活着才有 DPS）
                    }
                }
                if (best >= 0)
                {
                    var cell = empty[best];
                    empty.RemoveAt(best);
                    if (towerCell.TryGetValue(t, out var old)) { towerCell.Remove(t); empty.Add(old); }
                    t.position = new Vector2(cell.x, cell.y);
                    t.elevation = cellInfo[cell].elevation;
                    t.onWaterCell = cellInfo[cell].state == CellState.Water;
                    towerCell[t] = cell;
                }
            }

            // ③ 合并：同字同级贪心并到覆盖度高的格（同 ch1；移动免费）
            bool mergedAny = true;
            while (mergedAny)
            {
                mergedAny = false;
                TowerState source = null, keep = null;
                foreach (var a in model.Towers)
                {
                    foreach (var b in model.Towers)
                    {
                        if (a == b || a.charId != b.charId || a.level != b.level) continue;
                        if (a.level >= model.MaxTowerLevel) continue;
                        var defA = towerPool.Find(a.charId);
                        double sa = CellScore(defA, towerCell[a], groundSamples, airSamples);
                        double sb = CellScore(defA, towerCell[b], groundSamples, airSamples);
                        if (sa >= sb) { source = b; keep = a; } else { source = a; keep = b; }
                        break;
                    }
                    if (source != null) break;
                }
                if (source != null && keep != null)
                {
                    if (model.MergeTowers(source, keep))
                    {
                        towerCell.Remove(source);
                        mergedAny = true;
                    }
                }
            }

            // ④ 放置：手牌按价值降序；合法性异构（普通塔 = 干空地，水系三卡 = 水格 ∪ 空地全合法，
            //    F3.2 含临时淹没格——洪水期水塔应急部署是设计玩法，不能被普通塔的阻塞连坐）；
            //    预报安全层 +1000 只在「有覆盖的格」之间分档（零覆盖的安全格 = 没用的塔，不放）
            if (empty.Count > 0 || waterCells.Count > 0)
            {
                var toPlace = new List<string>(hand.Cards);
                toPlace.Sort((x, y) => towerPool.Find(y).value.CompareTo(towerPool.Find(x).value));
                foreach (var card in toPlace)
                {
                    if (card == HandModel.ShovelId) continue; // 铲只走开垦
                    var def = towerPool.Find(card);
                    if (def == null) continue;
                    // 候选：普通塔 = 空地；水系三卡 = 水格 ∪ 空地
                    List<Vector2Int> cells;
                    if (def.waterImmune)
                    {
                        cells = new List<Vector2Int>(waterCells.Count + empty.Count);
                        cells.AddRange(waterCells);
                        cells.AddRange(empty);
                    }
                    else cells = empty;
                    int best = -1;
                    double bestScore = 0;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        var info = cellInfo[cells[i]];
                        bool legal = def.waterImmune || info.elevation > model.WaterLevel; // F3.2 合法性
                        if (!legal) continue;
                        double s = CellScore(def, cells[i], groundSamples, airSamples);
                        if (def.waterImmune)
                        {
                            s += WaterCellScore(def, cells[i], waterSamples);
                            if (def.landPulseSlower && info.state == CellState.Water) s += 8; // 渔水中 5s/跳 vs 陆上 10s
                        }
                        if (s <= 0) continue; // 零覆盖格不放（同 ch1「剩卡留手」——安全但没用不如不放）
                        if (def.waterImmune || info.elevation > forecastMaxTide) s += 1000; // 预报安全层
                        if (s > bestScore) { bestScore = s; best = i; }
                    }
                    if (best < 0) continue; // 本卡没合法格 → 试下一张（不整队 break：普通塔被洪水憋 ≠ 水塔也没地方）

                    var cellPos = cells[best];
                    if (!empty.Remove(cellPos)) waterCells.Remove(cellPos); // 从源列表摘除（水塔可落两列表）
                    int at = hand.Cards.IndexOf(card);
                    if (at >= 0) hand.ConsumeAt(at);

                    var t = BuildTower(def, cellPos, 1, cellInfo);
                    model.AddTower(t);
                    towerCell[t] = cellPos;
                }
            }

            // ⑤ 开垦（铲）：同 ch1——无空地 + 有荒地 + 墨够加速 → 换一格
            while (empty.Count == 0 && fallow.Count > 0 &&
                   hand.Cards.Contains(HandModel.ShovelId) && model.Ink >= gp.farmSpeedupCost)
            {
                int si = hand.Cards.IndexOf(HandModel.ShovelId);
                hand.ConsumeAt(si);
                model.Ink -= gp.farmSpeedupCost;
                empty.Add(fallow[fallow.Count - 1]);
                fallow.RemoveAt(fallow.Count - 1);
            }
        }

        /// <summary>
        /// 塔构造（GameManager 放置/RestoreTowers 同字段清单全量拷入）。ch1 sim 当年没拷
        /// F3 状态字段（ch1 无水系卡无害）；ch2 必须补：海拔/水格/免疫/缓速/湿/冻/连携。
        /// </summary>
        private static TowerState BuildTower(TowerCharStats def, Vector2Int cell, int level, Dictionary<Vector2Int, CellInfo> cellInfo)
        {
            var info = cellInfo[cell];
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
                value = def.value,
                aoeShape = def.aoeShape,
                aoeBaseSize = def.aoeSize,
                aoeBaseParam = def.aoeParam,
                level = level,
                position = new Vector2(cell.x, cell.y),
                elevation = info.elevation,
                waterImmune = def.waterImmune,
                onWaterCell = info.state == CellState.Water,
                slowFactor = def.slowFactor,
                slowSeconds = def.slowSeconds,
                wetSeconds = def.wetSeconds,
                freezeSeconds = def.freezeSeconds,
                wetDamageMult = def.wetDamageMult,
                landPulseSlower = def.landPulseSlower,
                chainJumps = def.chainJumps,   // F3.2：链电参数（headless 与实机同源；0 = 无链）
                chainRatio = def.chainRatio,
                chainRadius = def.chainRadius,
                voidDamageMult = def.voidDamageMult, // F3.3：虚面加成（煞）
                soulBanner = def.soulBanner,         // F3.4：魂幡（幡）
                soulPayout = def.soulPayout,
            };
        }

        /// <summary>水路覆盖度（水系塔选位）：射程内水路采样点数——缓速压航道/渔压鱼群。</summary>
        private static double WaterCellScore(TowerCharStats def, Vector2Int cell, List<Vector2> waterSamples)
        {
            if (def == null) return 0;
            double r2 = def.range * def.range;
            var pos = new Vector2(cell.x, cell.y);
            double s = 0;
            foreach (var p in waterSamples)
                if ((p - pos).sqrMagnitude <= r2) s += 1;
            return s;
        }
    }
}
