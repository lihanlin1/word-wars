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

        // 图鉴数据源（ProjectSetup 建场景时注入；缺 = 图鉴内提示「未配置」）
        [SerializeField] private TowerPoolDef codexTowerPool;
        [SerializeField] private EnemyTableDef codexEnemyTable;
        [SerializeField] private ChapterConfig[] codexChapters;

        private CodexPanel codex;
        private GameObject shopRoot;

        private void Start()
        {
            EnsureScene();
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new GameObject("MenuRoot");
            root.transform.SetParent(transform, false);

            Label(root.transform, "墨 守", new Vector3(0f, 3.3f, -1.7f), 26f,
                HexUtil.ToColor("#26231F"), TextAlignmentOptions.Center, 12f);
            Label(root.transform, "Word Wars", new Vector3(0f, 2.35f, -1.7f), 8f,
                new Color(0.45f, 0.43f, 0.40f, 0.9f), TextAlignmentOptions.Center, 12f);

            // —— 三个主选项 ——
            Button(root.transform, "LevelSelect", new Vector3(0f, 0.75f, -1.7f), new Vector2(4.2f, 1.25f),
                "选关", HexUtil.ToColor("#26231F"), () => LoadSceneSafe("LevelSelect"), out _, 10f);

            Button(root.transform, "Tutorial", new Vector3(0f, -0.7f, -1.7f), new Vector2(4.2f, 1.25f),
                "新手引导", HexUtil.ToColor("#26231F"), StartTutorial, out _, 10f);

            Button(root.transform, "Wipe", new Vector3(0f, -2.15f, -1.7f), new Vector2(4.2f, 1.25f),
                "删档", HexUtil.ToColor("#B83A2E"), OnWipeClicked, out wipeLabel, 10f);

            // —— 图鉴（左侧，用户 2026-10-28）——
            Button(root.transform, "Dex", new Vector3(-6.9f, 0f, -1.7f), new Vector2(2.2f, 1.2f),
                "图鉴", HexUtil.ToColor("#26231F"), OpenCodex, out _, 7f);

            // —— 商店（右侧，预留：首次通关第一章后出现）——
            if (ShopUnlocked())
                Button(root.transform, "Shop", new Vector3(6.9f, 0f, -1.7f), new Vector2(2.2f, 1.2f),
                    "商店", HexUtil.ToColor("#B83A2E"), OpenShop, out _, 7f);

            // 进度显示（F3.2）：最近关 + 初始墨档位
            progressLabel = Label(root.transform, "", new Vector3(0f, -3.4f, -1.7f), 5.5f,
                new Color(0.45f, 0.43f, 0.40f, 0.9f), TextAlignmentOptions.Center, 16f);
            RefreshProgress();
        }

        private void RefreshProgress()
        {
            if (progressLabel == null) return;
            var s = SaveSystem.Load();
            if (s == null)
            {
                progressLabel.text = "新档";
                return;
            }
            int li = s.chapterState != null ? s.chapterState.levelIndex : 0;
            int ink = SaveSystem.InitialInk(s.playerMeta != null ? s.playerMeta.inkCapSteps : 0);
            string where = li <= 0
                ? "教学关"
                : ("第" + ((li - 1) / 5 + 1) + "章 · 第" + ((li - 1) % 5 + 1) + "关");
            progressLabel.text = "当前进度：" + where + "　·　初始墨 " + ink;
        }

        // ============ 图鉴 / 商店（用户 2026-10-28） ============

        /// <summary>图鉴：首次打开时创建面板（数据来自场景注入的塔池/敌表/章图）。</summary>
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

        /// <summary>商店（预留）：占位面板——后续换真正商店只改这里。</summary>
        private void OpenShop()
        {
            if (shopRoot != null) { shopRoot.SetActive(true); return; }

            shopRoot = new GameObject("ShopPanel");
            shopRoot.transform.SetParent(transform, false);

            var dim = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            dim.transform.SetParent(shopRoot.transform, false);
            dim.transform.localPosition = new Vector3(0f, 0f, -2.0f);
            dim.transform.localScale = new Vector3(30f, 15f, 1f);
            var ds = dim.GetComponent<SpriteRenderer>();
            ds.sprite = whiteSprite;
            ds.color = new Color(0.13f, 0.11f, 0.09f, 0.88f);
            dim.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            var paper = new GameObject("Paper", typeof(SpriteRenderer), typeof(BoxCollider2D));
            paper.transform.SetParent(shopRoot.transform, false);
            paper.transform.localPosition = new Vector3(0f, 0f, -2.05f);
            paper.transform.localScale = new Vector3(11f, 6.2f, 1f);
            var ps = paper.GetComponent<SpriteRenderer>();
            ps.sprite = whiteSprite;
            ps.color = HexUtil.ToColor("#F7F2E7");
            paper.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            Label(shopRoot.transform, "商 店", new Vector3(0f, 1.7f, -2.2f), 14f,
                HexUtil.ToColor("#B83A2E"), TextAlignmentOptions.Center, 8f);
            Label(shopRoot.transform, "（敬请期待）", new Vector3(0f, 0.5f, -2.2f), 6f,
                new Color(0.45f, 0.43f, 0.40f, 0.95f), TextAlignmentOptions.Center, 8f);
            Button(shopRoot.transform, "ShopClose", new Vector3(0f, -1.6f, -2.1f), new Vector2(3.2f, 1f),
                "返回", HexUtil.ToColor("#26231F"), () => shopRoot.SetActive(false), out _, 6.5f);
        }

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
                    wipeLabel.text = "确认删档？";
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
