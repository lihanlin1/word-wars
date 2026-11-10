using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WordWars
{
    /// <summary>
    /// 主界面（用户 2026-10-24）：选关 / 新手引导（L0）/ 删档；左侧「图鉴」占位（暂不实现）。
    /// 场景只挂本控制器，UI 运行时自建（与战斗内世界空间按钮同一观感）。
    /// </summary>
    public class MenuSceneController : MenuSceneBase
    {
        private TextMeshPro wipeLabel;
        private TextMeshPro progressLabel;
        private bool wipeArmed;

        private static readonly Color InkColor = HexUtil.ToColor("#26231F");
        private static readonly Color FadeColor = new Color(0.45f, 0.43f, 0.40f, 0.9f);
        private static readonly Color DangerColor = HexUtil.ToColor("#B83A2E");

        // 图鉴数据源（ProjectSetup 建场景时注入；缺 = 图鉴内提示「未配置」）
        [SerializeField] private TowerPoolDef codexTowerPool;
        [SerializeField] private EnemyTableDef codexEnemyTable;
        [SerializeField] private ChapterConfig[] codexChapters;

        private CodexPanel codex;

        private void Start()
        {
            EnsureScene();
            SaveSystem.InitSlots(); // 三存档槽（自动进上次的槽）
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new GameObject("MenuRoot");
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one * ScreenFit();

            // —— 抬头：主标题 + 英文小字 ——
            // 字号 22 的字身约 2.2 格，标题中心 3.45 → 底边约 2.25；副标题中心 1.85 → 顶边约 2.09，
            // 留 0.16 的空档（旧版两行间距 0.95、字号 26+8，英文直接被「墨」的捺盖住）。
            Label(root.transform, "墨 守", new Vector3(0f, 3.45f, -1.7f), 22f, InkColor,
                TextAlignmentOptions.Center, 12f);
            Label(root.transform, "Word Wars", new Vector3(0f, 1.85f, -1.7f), 4.4f, FadeColor,
                TextAlignmentOptions.Center, 12f);

            // —— 主操作：一大一次，间距 0.3 以上（旧版 1.25 高的按钮只隔 0.2，像一根长条切三刀）——
            Button(root.transform, "LevelSelect", new Vector3(0f, 0.70f, -1.7f), new Vector2(4.6f, 1.3f),
                "选关", InkColor, () => LoadSceneSafe("LevelSelect"), out _, 10f);

            Button(root.transform, "Tutorial", new Vector3(0f, -0.85f, -1.7f), new Vector2(4.2f, 1.15f),
                "新手引导", InkColor, StartTutorial, out _, 8f);

            // —— 次级入口：底部一栏（左 图鉴 / 中 商店 / 右 删档），删档最远最轻 ——
            Button(root.transform, "Dex", new Vector3(-6.2f, -3.15f, -1.7f), new Vector2(2.4f, 0.85f),
                "图鉴", InkColor, OpenCodex, out _, 5.2f);

            if (ShopUnlocked())
                Button(root.transform, "Shop", new Vector3(0f, -3.15f, -1.7f), new Vector2(2.4f, 0.85f),
                    "商店", DangerColor, OpenShop, out _, 5.2f);

            // 右下角（原删档位）→ 存档入口（用户 2026-11-07：替换 WipeHolder，不另找位置）
            Button(root.transform, "Wipe", new Vector3(6.2f, -3.15f, -1.7f), new Vector2(2.2f, 0.8f),
                "存档", InkColor, OpenSaves, out wipeLabel, 4.8f);

            progressLabel = Label(root.transform, "", new Vector3(0f, -4.15f, -1.7f), 4.2f, FadeColor,
                TextAlignmentOptions.Center, 18f);
            RefreshProgress();
        }

        private void RefreshProgress()
        {
            if (progressLabel == null) return;
            var s = SaveSystem.Load();
            if (s == null)
            {
                progressLabel.text = "新档　·　金币 0";
                return;
            }
            int li = s.chapterState != null ? s.chapterState.levelIndex : 0;
            int ink = SaveSystem.InitialInk(s.playerMeta != null ? s.playerMeta.inkCapSteps : 0);
            string where = li <= 0
                ? "教学关"
                : ("第" + ((li - 1) / 5 + 1) + "章 · 第" + ((li - 1) % 5 + 1) + "关");
            progressLabel.text = "当前进度：" + where + "　·　初始墨 " + ink + "　·　金币 " + s.gold;
        }

        // ============ 图鉴 / 商店（用户 2026-10-28） ============

        /// <summary>图鉴：首次打开时创建面板（数据来自场景注入的塔池/敌表/章图）。</summary>
        // ============ 存档槽（用户 2026-11-05） ============

        private GameObject savePanel;

        private void OpenSaves()
        {
            if (savePanel != null) { savePanel.SetActive(true); BuildSavePanel(); return; }
            savePanel = new GameObject("SavePanel");
            savePanel.transform.SetParent(transform, false);
            BuildSavePanel();
        }

        private void BuildSavePanel()
        {
            foreach (Transform ch in savePanel.transform) Destroy(ch.gameObject);

            var dim = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            dim.transform.SetParent(savePanel.transform, false);
            dim.transform.localPosition = new Vector3(0f, 0f, -2.0f);
            dim.transform.localScale = new Vector3(30f, 15f, 1f);
            var ds = dim.GetComponent<SpriteRenderer>();
            ds.sprite = whiteSprite;
            ds.color = new Color(0.13f, 0.11f, 0.09f, 0.88f);
            dim.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            Label(savePanel.transform, "存 档", new Vector3(0f, 3.7f, -2.2f), 14f,
                HexUtil.ToColor("#26231F"), TextAlignmentOptions.Center, 8f);
            Button(savePanel.transform, "SaveBack", new Vector3(7.2f, 3.7f, -2.1f), new Vector2(2.2f, 0.9f),
                "返回", HexUtil.ToColor("#26231F"), () => savePanel.SetActive(false), out _, 6f);

            for (int i = 1; i <= SaveSystem.SlotCount; i++)
            {
                int slot = i;
                float x = -6.2f + (i - 1) * 6.2f;
                bool has = SaveSystem.HasSlot(slot);
                bool cur = SaveSystem.Slot == slot;

                var card = new GameObject("Slot" + i, typeof(SpriteRenderer), typeof(BoxCollider2D));
                card.transform.SetParent(savePanel.transform, false);
                card.transform.localPosition = new Vector3(x, -0.2f, -2.1f);
                card.transform.localScale = new Vector3(5.4f, 4.6f, 1f);
                var cs = card.GetComponent<SpriteRenderer>();
                cs.sprite = whiteSprite;
                cs.color = cur ? new Color(0.93f, 0.86f, 0.76f, 1f) : new Color(0.93f, 0.89f, 0.80f, 1f);
                card.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

                Label(savePanel.transform, "存档 " + i + (cur ? " · 当前" : ""),
                    new Vector3(x, 1.5f, -2.2f), 6f, HexUtil.ToColor("#B83A2E"), TextAlignmentOptions.Center, 5.2f);
                Label(savePanel.transform, "简单 ★" + SaveSystem.ClearedCount(slot, false),
                    new Vector3(x, 0.6f, -2.2f), 5.2f, HexUtil.ToColor("#26231F"), TextAlignmentOptions.Center, 5.2f);
                Label(savePanel.transform, "困难 ★" + SaveSystem.ClearedCount(slot, true),
                    new Vector3(x, -0.1f, -2.2f), 5.2f, HexUtil.ToColor("#2B4C7E"), TextAlignmentOptions.Center, 5.2f);
                if (!has)
                    Label(savePanel.transform, "新档",
                        new Vector3(x, -0.8f, -2.2f), 4.5f, new Color(0.45f, 0.43f, 0.40f), TextAlignmentOptions.Center, 5.2f);

                Button(savePanel.transform, "Enter" + i, new Vector3(x, -1.7f, -2.15f), new Vector2(2.3f, 0.85f),
                    cur ? "当前" : "进入", HexUtil.ToColor("#26231F"),
                    () => { SaveSystem.UseSlot(slot); RefreshMenu(); }, out _, 5.5f);
                if (has)
                    Button(savePanel.transform, "Del" + i, new Vector3(x, -2.6f, -2.15f), new Vector2(2.3f, 0.75f),
                        "删除", HexUtil.ToColor("#B83A2E"), () => ConfirmDelete(slot), out _, 5f);
            }
        }

        private void ConfirmDelete(int slot)
        {
            foreach (Transform ch in savePanel.transform) Destroy(ch.gameObject);

            var dim = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            dim.transform.SetParent(savePanel.transform, false);
            dim.transform.localPosition = new Vector3(0f, 0f, -2.2f);
            dim.transform.localScale = new Vector3(30f, 15f, 1f);
            var ds = dim.GetComponent<SpriteRenderer>();
            ds.sprite = whiteSprite;
            ds.color = new Color(0.13f, 0.11f, 0.09f, 0.9f);
            dim.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            var paper = new GameObject("Paper", typeof(SpriteRenderer), typeof(BoxCollider2D));
            paper.transform.SetParent(savePanel.transform, false);
            paper.transform.localPosition = new Vector3(0f, 0f, -2.25f);
            paper.transform.localScale = new Vector3(11f, 5.6f, 1f);
            var ps = paper.GetComponent<SpriteRenderer>();
            ps.sprite = whiteSprite;
            ps.color = HexUtil.ToColor("#F7F2E7");
            paper.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            Label(savePanel.transform, "删除存档 " + slot + "？", new Vector3(0f, 1.2f, -2.4f), 11f,
                HexUtil.ToColor("#B83A2E"), TextAlignmentOptions.Center, 8f);
            Label(savePanel.transform, "进度 / 背包 / 解锁将全部清空（不可恢复）",
                new Vector3(0f, 0.35f, -2.4f), 4.8f, new Color(0.45f, 0.43f, 0.40f), TextAlignmentOptions.Center, 9f);
            Button(savePanel.transform, "DelYes", new Vector3(-2.2f, -1.3f, -2.35f), new Vector2(3.2f, 1.1f),
                "确认删除", HexUtil.ToColor("#B83A2E"),
                () => { SaveSystem.DeleteSlot(slot); BuildSavePanel(); }, out _, 7f);
            Button(savePanel.transform, "DelNo", new Vector3(2.2f, -1.3f, -2.35f), new Vector2(3.2f, 1.1f),
                "取消", HexUtil.ToColor("#26231F"), BuildSavePanel, out _, 7f);
        }

        /// <summary>切槽后整块重建主界面（进度/商店解锁跟着变）。</summary>
        private void RefreshMenu()
        {
            var old = transform.Find("MenuRoot");
            if (old != null) Destroy(old.gameObject);
            savePanel = null; // 旧面板随重建清掉（挂在 transform 下独立存在 → 手动销毁）
            foreach (Transform ch in transform)
                if (ch.name == "SavePanel") Destroy(ch.gameObject);
            BuildUi();
        }

        private void OpenCodex()
        {
            if (codex == null)
            {
                var go = new GameObject("CodexPanel", typeof(CodexPanel));
                codex = go.GetComponent<CodexPanel>();
                codex.Init(font, whiteSprite, codexTowerPool, codexEnemyTable, codexChapters);
            }
            codex.Show();
        }

        /// <summary>商店解锁：首次通关第一章（清 L5 → maxLevelUnlocked ≥ 7）。</summary>
        private static bool ShopUnlocked()
        {
            var s = SaveSystem.Load();
            return s != null && s.maxLevelUnlocked >= 7;
        }

        /// <summary>商店：独立场景（用户 2026-11-04，ADR-085）。</summary>
        private void OpenShop() => LoadSceneSafe("Shop");

        /// <summary>新手引导 = 第 0 关（L0 教学），简单模式。</summary>
        private void StartTutorial()
        {
            GameSession.Reset();
            GameSession.Active = true;
            GameSession.HardMode = false;
            GameSession.StartLevelIndex = 0;
            LoadSceneSafe("Game");
        }

        /// <summary>删档：两次点确认（第一下变文案，第二下真删）。</summary>
        private void OnWipeClicked()
        {
            if (!wipeArmed)
            {
                wipeArmed = true;
                if (wipeLabel != null)
                {
                    wipeLabel.text = "确认删档"; // 按钮只有 2.2 宽，加问号会顶出框
                    wipeLabel.color = HexUtil.ToColor("#B83A2E");
                }
                return;
            }
            wipeArmed = false;
            SaveSystem.Delete();
            if (wipeLabel != null)
            {
                wipeLabel.text = "已删档";
                wipeLabel.color = new Color(0.45f, 0.43f, 0.40f, 0.9f);
            }
            RefreshProgress();
            Debug.Log("[Menu] 已删档（全部进度/解锁/背包）");
        }
    }
}
