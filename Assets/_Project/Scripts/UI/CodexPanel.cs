using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 图鉴（用户 2026-10-28）：三个主页面 = 塔防 / 敌人 / 地图。
    /// 只显示当前进度下「已解锁/已遭遇」的条目——未解锁用「？」占位（不剧透）。
    ///   · 塔防 = 已拥有卡（save.backpack.ownedCards）；点卡片看详细属性
    ///   · 敌人 = 已遭遇敌字（save.seenEnemies），分「首领 / 小怪」两段
    ///   · 地图 = 已进入过的章（按 maxLevelUnlocked），预览章图 + 机制描述（ChapterConfig.codexDesc）
    /// 运行时自建世界坐标 UI（同 DeckPanel/菜单场景口径：SpriteRenderer + BoxCollider2D + OnMouseDown）。
    /// z：底幕 -2.0 / 纸 -2.05 / 内容 -2.1 / 文字 -2.2（相机 z=-10 朝 +z，z 越小越靠前）。
    /// </summary>
    public class CodexPanel : MonoBehaviour
    {
        private static readonly Color DimBg = new Color(0.13f, 0.11f, 0.09f, 0.88f);
        private static readonly Color Paper = HexUtil.ToColor("#F7F2E7");
        private static readonly Color Ink = HexUtil.ToColor("#26231F");
        private static readonly Color FadeInk = new Color(0.38f, 0.35f, 0.31f, 0.95f);
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");
        private static readonly Color BlueInk = HexUtil.ToColor("#2B4C7E");
        private static readonly Color CardBg = new Color(0.93f, 0.89f, 0.80f, 0.98f);
        private static readonly Color LockedBg = new Color(0.22f, 0.20f, 0.17f, 0.9f);
        private static readonly Color LockedText = new Color(0.52f, 0.49f, 0.44f, 0.9f);
        private static readonly Color TabOn = HexUtil.ToColor("#B83A2E");
        private static readonly Color TabOff = new Color(0.55f, 0.52f, 0.47f, 0.95f);

        private const int TabTower = 0;
        private const int TabEnemy = 1;
        private const int TabMap = 2;

        private TMP_FontAsset font;
        private Sprite white;
        private TowerPoolDef towerPool;
        private EnemyTableDef enemyTable;
        private ChapterConfig[] chapters;

        private GameObject root;
        private GameObject contentRoot;
        private GameObject detailRoot;
        private GameObject[] tabGos;
        private TextMeshPro[] tabLabels;
        private TextMeshPro tipLabel;
        private int tab = TabTower;
        private int mapIndex;

        public bool IsOpen { get; private set; }

        /// <summary>菜单场景创建一次。数据源可空（空 = 图鉴内显示未配置提示）。</summary>
        public void Init(TMP_FontAsset fontAsset, Sprite whiteSprite,
            TowerPoolDef pool, EnemyTableDef enemies, ChapterConfig[] chapterList)
        {
            font = fontAsset;
            white = whiteSprite;
            towerPool = pool;
            enemyTable = enemies;
            chapters = chapterList;

            root = new GameObject("Root");
            root.transform.SetParent(transform, false);

            var bgGo = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            bgGo.transform.SetParent(root.transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 0f, -2.0f);
            bgGo.transform.localScale = new Vector3(30f, 15f, 1f);
            var dim = bgGo.GetComponent<SpriteRenderer>();
            dim.sprite = white;
            dim.color = DimBg;
            bgGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.01f;

            var paperGo = new GameObject("Paper", typeof(SpriteRenderer), typeof(BoxCollider2D));
            paperGo.transform.SetParent(root.transform, false);
            paperGo.transform.localPosition = new Vector3(0f, 0f, -2.05f);
            paperGo.transform.localScale = new Vector3(17.8f, 9.8f, 1f);
            var paper = paperGo.GetComponent<SpriteRenderer>();
            paper.sprite = white;
            paper.color = Paper;
            paperGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.01f;

            Label(root.transform, "Title", "图 鉴", new Vector3(-7.4f, 4.05f, -2.2f), 12f, Ink,
                TextAlignmentOptions.Left, 6f);

            MakeButton(root.transform, "返回", new Vector3(7.35f, 4.05f, -2.1f), new Vector2(2.2f, 0.85f),
                5.5f, new Color(0.86f, 0.82f, 0.74f, 1f), Ink, Hide);

            // 三个主页面
            string[] names = { "塔防", "敌人", "地图" };
            tabGos = new GameObject[3];
            tabLabels = new TextMeshPro[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                tabGos[i] = MakeButton(root.transform, names[i], new Vector3(-4.4f + i * 4.4f, 3.25f, -2.1f),
                    new Vector2(3.6f, 0.9f), 6.5f, TabOff, Paper, () => SwitchTab(idx));
                tabLabels[i] = tabGos[i].transform.Find("Label")?.GetComponent<TextMeshPro>();
            }

            contentRoot = new GameObject("Content");
            contentRoot.transform.SetParent(root.transform, false);
            contentRoot.transform.localPosition = new Vector3(0f, 0f, -2.1f);

            tipLabel = Label(root.transform, "Tip", "", new Vector3(0f, -4.45f, -2.2f), 4f, FadeInk,
                TextAlignmentOptions.Center, 16f);

            root.SetActive(false);
        }

        public void Show()
        {
            root.SetActive(true);
            IsOpen = true;
            mapIndex = Mathf.Clamp(mapIndex, 0, Mathf.Max(0, (chapters != null ? chapters.Length : 1) - 1));
            SwitchTab(tab);
        }

        public void Hide()
        {
            CloseDetail();
            IsOpen = false;
            if (root != null) root.SetActive(false);
        }

        private void SwitchTab(int which)
        {
            tab = which;
            for (int i = 0; i < tabGos.Length; i++)
            {
                var bodyTr = tabGos[i].transform.Find("Body");
                var sr = bodyTr != null ? bodyTr.GetComponent<SpriteRenderer>() : null;
                if (sr != null) sr.color = i == tab ? TabOn : TabOff;
                if (tabLabels[i] != null) tabLabels[i].color = i == tab ? Paper : new Color(0.22f, 0.20f, 0.17f);
            }
            CloseDetail();
            Rebuild();
        }

        private void Rebuild()
        {
            // 先隐藏再销毁（Destroy 延迟到帧末，立刻重建会重影一帧）
            for (int i = contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var ch = contentRoot.transform.GetChild(i).gameObject;
                ch.SetActive(false);
                Destroy(ch);
            }

            if (tab == TabTower) BuildTowers();
            else if (tab == TabEnemy) BuildEnemies();
            else BuildMaps();
        }

        // ================= 塔防 =================

        private void BuildTowers()
        {
            if (towerPool == null || towerPool.chars == null || towerPool.chars.Count == 0)
            {
                tipLabel.text = "图鉴数据未配置（跑一次「Word Wars/Setup/重建主界面与选关场景」）";
                return;
            }
            var save = SaveSystem.Load();
            int count = towerPool.chars.Count;
            int cols = Mathf.Min(7, count);
            int owned = 0;
            for (int i = 0; i < count; i++)
            {
                var def = towerPool.chars[i];
                if (def == null) continue;
                bool unlocked = save != null && save.backpack != null &&
                                save.backpack.ownedCards != null && save.backpack.ownedCards.Contains(def.charId);
                if (unlocked) owned++;

                int row = i / cols;
                int rowLen = Mathf.Min(cols, count - row * cols);
                int col = i % cols;
                float x = (col - (rowLen - 1) * 0.5f) * 2.05f;
                float y = 1.55f - row * 1.75f;

                MakeCard(contentRoot.transform, new Vector3(x, y, 0f), 1.5f,
                    unlocked ? def.charId : "？",
                    unlocked ? CardBg : LockedBg,
                    unlocked ? Ink : LockedText,
                    "", unlocked ? (Action)(() => ShowTowerDetail(def)) : null);
            }
            tipLabel.text = "已解锁 " + owned + " / " + count + "　·　点卡片查看属性";
        }

        private void ShowTowerDetail(TowerCharStats def)
        {
            if (def == null) return;
            CloseDetail();

            detailRoot = new GameObject("Detail");
            detailRoot.transform.SetParent(root.transform, false);

            var bg = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            bg.transform.SetParent(detailRoot.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, -2.5f);
            bg.transform.localScale = new Vector3(30f, 15f, 1f);
            var bgs = bg.GetComponent<SpriteRenderer>();
            bgs.sprite = white;
            bgs.color = new Color(0.10f, 0.09f, 0.07f, 0.75f);
            bg.GetComponent<BoxCollider2D>().size = Vector2.one * 1.01f;

            var paperGo = new GameObject("Paper", typeof(SpriteRenderer), typeof(BoxCollider2D));
            paperGo.transform.SetParent(detailRoot.transform, false);
            paperGo.transform.localPosition = new Vector3(0f, -0.2f, -2.55f);
            paperGo.transform.localScale = new Vector3(13.4f, 7.6f, 1f);
            var ps = paperGo.GetComponent<SpriteRenderer>();
            ps.sprite = white;
            ps.color = Paper;
            paperGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.01f;

            var glyph = Label(detailRoot.transform, "Glyph", def.charId, new Vector3(-4.6f, 1.5f, -2.7f),
                18f, Ink, TextAlignmentOptions.Center, 3f);
            glyph.color = Ink;

            Label(detailRoot.transform, "Name", def.charId + "　" + def.role, new Vector3(0.7f, 2.6f, -2.7f),
                7f, Ink, TextAlignmentOptions.Center, 8.2f);

            var lines = new List<string>();
            if (def.damage > 0)
                lines.Add("伤害 " + def.damage.ToString("0.#") + "　·　攻击间隔 " + def.interval.ToString("0.##") + "s　·　射程 " + def.range.ToString("0.##") + " 格");
            else
                lines.Add("无攻击（经济塔）　·　射程 " + def.range.ToString("0.##") + " 格");
            if (def.rangePerLevel > 0) lines.Add("射程成长：每级 +" + def.rangePerLevel.ToString("0.##") + " 格");
            lines.Add("卡价 " + def.value + " 墨");
            lines.Add("");
            lines.Add("特性");
            foreach (var t in Traits(def)) lines.Add("· " + t);

            var body = Label(detailRoot.transform, "Body", string.Join("\n", lines),
                new Vector3(-5.6f, 1.0f, -2.7f), 4.6f, FadeInk, TextAlignmentOptions.TopLeft, 11.6f, wrap: true);
            body.lineSpacing = 10f;
            body.rectTransform.pivot = new Vector2(0f, 1f);

            MakeButton(detailRoot.transform, "返回", new Vector3(4.6f, -3.0f, -2.65f), new Vector2(2.6f, 0.9f),
                5.5f, new Color(0.86f, 0.82f, 0.74f, 1f), Ink, CloseDetail);
        }

        private List<string> Traits(TowerCharStats d)
        {
            var list = new List<string>();
            switch (d.aoeShape)
            {
                case AoeShape.Rect: list.Add("矩形溅射（" + d.aoeSize.ToString("0.##") + "×" + d.aoeParam.ToString("0.##") + " 格）"); break;
                case AoeShape.Circle: list.Add("圆形溅射（半径 " + d.aoeSize.ToString("0.##") + " 格）"); break;
                case AoeShape.Sector: list.Add("扇形溅射（半径 " + d.aoeSize.ToString("0.##") + " 格 / " + d.aoeParam.ToString("0") + "°）"); break;
            }
            if (d.antiAir) list.Add("可攻击空中");
            if (d.ranged) list.Add("远程弹道");
            else if (d.damage > 0) list.Add("近战瞬发");
            if (d.inkPerPulse > 0) list.Add("产墨 " + d.inkPerPulse.ToString("0.#") + " / 跳（升阶 ×2）");
            if (d.waterImmune) list.Add("水系：可入水格，淹没不沉默");
            if (d.slowFactor > 0) list.Add("命中减速 " + (d.slowFactor * 100f).ToString("0") + "% / " + d.slowSeconds.ToString("0.#") + "s");
            if (d.wetSeconds > 0) list.Add("命中附「湿」" + d.wetSeconds.ToString("0.#") + "s");
            if (d.wetDamageMult > 1.0) list.Add("对湿敌伤害 ×" + d.wetDamageMult.ToString("0.##"));
            if (d.freezeSeconds > 0) list.Add("命中冻结 " + d.freezeSeconds.ToString("0.#") + "s");
            if (d.burnDps > 0) list.Add("命中燃烧 " + d.burnDps.ToString("0.#") + "/s（" + d.burnSeconds.ToString("0.#") + "s）");
            if (d.createsMirror) list.Add("双面覆盖：对面生成镜像（" + (d.mirrorDamageRatio * 100f).ToString("0") + "% 伤害）");
            if (d.chainJumps > 0) list.Add("链电 " + d.chainJumps + " 跳（每跳 ×" + d.chainRatio.ToString("0.##") + "）");
            if (d.voidDamageMult > 1.0) list.Add("虚面伤害 ×" + d.voidDamageMult.ToString("0.##"));
            if (d.soulBanner) list.Add("收魂：每 10 魂产墨（按最高阶结算）");
            if (d.landPulseSlower) list.Add("陆上产墨间隔 ×2（水中正常）");
            if (list.Count == 0) list.Add("无特殊机制");
            return list;
        }

        private void CloseDetail()
        {
            if (detailRoot != null) Destroy(detailRoot);
            detailRoot = null;
        }

        // ================= 敌人 =================

        private void BuildEnemies()
        {
            if (enemyTable == null || enemyTable.enemies == null || enemyTable.enemies.Count == 0)
            {
                tipLabel.text = "图鉴数据未配置（跑一次「Word Wars/Setup/重建主界面与选关场景」）";
                return;
            }
            var save = SaveSystem.Load();

            var bosses = new List<EnemyStats>();
            var minions = new List<EnemyStats>();
            foreach (var e in enemyTable.enemies)
                if (e != null) (e.isBoss ? bosses : minions).Add(e);

            // —— 首领段 ——
            Label(contentRoot.transform, "BossLabel", "首领", new Vector3(-7.3f, 2.75f, -0.1f), 5.5f, Vermilion,
                TextAlignmentOptions.Left, 3f);
            int shownBoss = 0;
            for (int i = 0; i < bosses.Count; i++)
            {
                var e = bosses[i];
                bool seen = IsEnemySeen(save, e.charId);
                if (seen) shownBoss++;
                float x = -5.2f + (i % 6) * 2.2f;
                float y = 2.05f - (i / 6) * 1.45f;
                MakeEnemyCard(e, x, y, 1.55f, 10f, seen);
            }

            // —— 小怪段 ——
            Label(contentRoot.transform, "MinionLabel", "小怪", new Vector3(-7.3f, 1.05f, -0.1f), 5.5f, BlueInk,
                TextAlignmentOptions.Left, 3f);
            int cols = 9;
            int shownMinion = 0;
            for (int i = 0; i < minions.Count; i++)
            {
                var e = minions[i];
                bool seen = IsEnemySeen(save, e.charId);
                if (seen) shownMinion++;
                int row = i / cols;
                int rowLen = Mathf.Min(cols, minions.Count - row * cols);
                int col = i % cols;
                float x = (col - (rowLen - 1) * 0.5f) * 1.72f;
                float y = 0.35f - row * 1.22f;
                MakeEnemyCard(e, x, y, 1.15f, 8f, seen);
            }

            tipLabel.text = "已遭遇：" + (shownBoss + shownMinion) + " / " + (bosses.Count + minions.Count) +
                            "（首领 " + shownBoss + "/" + bosses.Count + "）";
        }

        private static bool IsEnemySeen(SaveData save, string charId)
        {
            return save != null && save.seenEnemies != null && save.seenEnemies.Contains(charId);
        }

        private void MakeEnemyCard(EnemyStats e, float x, float y, float size, float glyphSize, bool seen)
        {
            var holder = new GameObject("E_" + e.charId);
            holder.transform.SetParent(contentRoot.transform, false);
            holder.transform.localPosition = new Vector3(x, y, 0f);

            var body = new GameObject("Body", typeof(SpriteRenderer), typeof(BoxCollider2D));
            body.transform.SetParent(holder.transform, false);
            body.transform.localScale = new Vector3(size, size, 1f);
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = seen ? (e.isBoss ? new Color(0.93f, 0.86f, 0.78f, 1f) : CardBg) : LockedBg;
            body.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
            if (!seen) body.AddComponent<PanelClicker>().Init(null);

            var g = Label(holder.transform, "Glyph", seen ? e.charId : "？",
                new Vector3(0f, 0.12f, -0.1f), glyphSize, seen ? Ink : LockedText, TextAlignmentOptions.Center, size);
            if (seen && e.isBoss) g.color = Vermilion;

            Label(holder.transform, "Hp", seen ? "血 " + e.hp.ToString("0") : "",
                new Vector3(0f, -0.34f, -0.1f), 2.9f, seen ? BlueInk : LockedText, TextAlignmentOptions.Center, size);
        }

        // ================= 地图 =================

        private void BuildMaps()
        {
            if (chapters == null || chapters.Length == 0)
            {
                tipLabel.text = "图鉴数据未配置（跑一次「Word Wars/Setup/重建主界面与选关场景」）";
                return;
            }
            var save = SaveSystem.Load();
            for (int i = 0; i < chapters.Length; i++)
            {
                var ch = chapters[i];
                if (ch == null) continue;
                bool unlocked = IsChapterUnlocked(save, i);
                int idx = i;
                MakeButton(contentRoot.transform, unlocked ? ShortTitle(ch.title) : "？",
                    new Vector3(-6.0f + i * 3.0f, 2.6f, 0f), new Vector2(2.8f, 0.8f), 4.2f,
                    unlocked ? (idx == mapIndex ? TabOff : CardBg) : LockedBg,
                    unlocked ? Ink : LockedText,
                    unlocked ? (Action)(() => { mapIndex = idx; Rebuild(); }) : null);
            }

            if (chapters.Length == 0) { tipLabel.text = ""; return; }
            int cur = Mathf.Clamp(mapIndex, 0, chapters.Length - 1);
            var chapter = chapters[cur];
            if (chapter == null || !IsChapterUnlocked(save, cur))
            {
                tipLabel.text = "该章节尚未遇到";
                return;
            }

            DrawMapPreview(chapter);
            var desc = Label(contentRoot.transform, "Desc",
                string.IsNullOrEmpty(chapter.codexDesc) ? "暂无描述" : chapter.codexDesc,
                new Vector3(2.55f, 1.75f, -0.1f), 4.2f, Ink, TextAlignmentOptions.TopLeft, 6.3f, wrap: true);
            desc.lineSpacing = 10f;
            desc.rectTransform.pivot = new Vector2(0f, 1f);
            tipLabel.text = "章节地图 · 机制说明";
        }

        private static string ShortTitle(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            int dot = t.IndexOf('·');
            return dot > 0 ? t.Substring(0, dot).Trim() : t;
        }

        private static bool IsChapterUnlocked(SaveData save, int index)
        {
            if (index == 0) return true;   // 第一章开局即可看
            if (save == null) return false;
            return save.maxLevelUnlocked >= index * 5 + 2;
        }

        /// <summary>章图预览：格子 + 陆路/水路折线 + 家（缩放到左半区）。</summary>
        private void DrawMapPreview(ChapterConfig ch)
        {
            var pane = new GameObject("Preview");
            pane.transform.SetParent(contentRoot.transform, false);
            pane.transform.localPosition = new Vector3(-4.6f, -1.0f, 0.01f);

            var bg = new GameObject("Bg", typeof(SpriteRenderer));
            bg.transform.SetParent(pane.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0f);
            bg.transform.localScale = new Vector3(8.4f, 5.2f, 1f);
            var bs = bg.GetComponent<SpriteRenderer>();
            bs.sprite = white;
            bs.color = new Color(0.90f, 0.86f, 0.76f, 1f);

            const float k = 0.40f; // 网格单位 → 世界单位

            // 格子
            if (ch.cells != null)
                foreach (var c in ch.cells)
                {
                    if (c == null) continue;
                    var go = new GameObject("Cell", typeof(SpriteRenderer));
                    go.transform.SetParent(pane.transform, false);
                    go.transform.localPosition = new Vector3(c.x * k, c.y * k, -0.02f);
                    go.transform.localScale = new Vector3(0.86f * k, 0.86f * k, 1f);
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sprite = white;
                    sr.color = c.state == CellState.Water ? new Color(0.42f, 0.62f, 0.72f, 0.95f)
                             : c.state == CellState.Fallow ? new Color(0.72f, 0.65f, 0.52f, 0.9f)
                             : new Color(0.98f, 0.95f, 0.88f, 1f);
                }

            // 路线
            DrawRoute(pane.transform, ch.landRoute, k, new Color(0.15f, 0.13f, 0.10f, 0.55f), 0.16f);
            DrawRoute(pane.transform, ch.waterRoute, k, new Color(0.24f, 0.42f, 0.55f, 0.65f), 0.14f);

            // 家
            if (ch.landRoute != null && ch.landRoute.Count > 0)
            {
                var last = ch.landRoute[ch.landRoute.Count - 1];
                Label(pane.transform, "Home", "家", new Vector3(last.x * k, last.y * k, -0.06f), 5f, Vermilion,
                    TextAlignmentOptions.Center, 2f);
            }
        }

        private void DrawRoute(Transform parent, List<Vector2> pts, float k, Color color, float thickness)
        {
            if (pts == null || pts.Count < 2) return;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.001f) continue;
                var go = new GameObject("Seg", typeof(SpriteRenderer));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3((a.x + b.x) * 0.5f * k, (a.y + b.y) * 0.5f * k, -0.03f);
                go.transform.localScale = new Vector3(len * k, thickness, 1f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = white;
                sr.color = color;
            }
        }

        // ================= 基础件 =================

        private GameObject MakeCard(Transform parent, Vector3 pos, float size, string glyph,
            Color bg, Color glyphColor, string sub, Action onClick)
        {
            var holder = new GameObject("Card_" + glyph);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = pos;

            var body = new GameObject("Body", typeof(SpriteRenderer), typeof(BoxCollider2D));
            body.transform.SetParent(holder.transform, false);
            body.transform.localScale = new Vector3(size, size, 1f);
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = bg;
            body.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
            body.AddComponent<PanelClicker>().Init(onClick);

            Label(holder.transform, "Glyph", glyph, new Vector3(0f, sub == "" ? 0f : 0.08f, -0.1f),
                Mathf.Max(4f, size * 7.2f), glyphColor, TextAlignmentOptions.Center, size);
            if (!string.IsNullOrEmpty(sub))
                Label(holder.transform, "Sub", sub, new Vector3(0f, -0.36f, -0.1f), 3f, glyphColor,
                    TextAlignmentOptions.Center, size);
            return holder;
        }

        private GameObject MakeButton(Transform parent, string text, Vector3 pos, Vector2 size, float fontSize,
            Color bg, Color fg, Action onClick)
        {
            var holder = new GameObject("Btn_" + text);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = pos;

            var body = new GameObject("Body", typeof(SpriteRenderer), typeof(BoxCollider2D));
            body.transform.SetParent(holder.transform, false);
            body.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = bg;
            body.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
            body.AddComponent<PanelClicker>().Init(onClick);

            var label = Label(holder.transform, "Label", text, new Vector3(0f, 0.02f, -0.1f), fontSize, fg,
                TextAlignmentOptions.Center, size.x + 0.4f);
            label.name = "Label";
            return holder;
        }

        private TextMeshPro Label(Transform parent, string name, string text, Vector3 pos, float size,
            Color color, TextAlignmentOptions align, float width, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.enableWordWrapping = wrap;
            t.text = text;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, wrap ? 4f : 1.6f);
            return t;
        }
    }
}
