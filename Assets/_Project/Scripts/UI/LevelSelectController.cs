using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WordWars
{
    /// <summary>
    /// 选关场景（用户 2026-10-24）：简单/困难切换 + 章节卡片 → 关卡列表 + 从第一关/继续。
    /// 困难模式关解锁跟简单进度（maxLevelUnlocked）。
    /// </summary>
    public class LevelSelectController : MenuSceneBase
    {
        private static readonly Color Ink = HexUtil.ToColor("#26231F");
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");
        private static readonly Color BlueInk = HexUtil.ToColor("#2B4C7E");
        private static readonly Color Gold = HexUtil.ToColor("#A67C1B");
        private static readonly Color Dim = new Color(0.62f, 0.60f, 0.56f, 0.9f);
        private static readonly Color LockedBg = new Color(0.80f, 0.77f, 0.70f, 0.7f);
        // 程序化宝箱配色（没有美术素材时用）
        private static readonly Color ChestOutline = new Color(0.12f, 0.10f, 0.08f, 0.55f);
        private static readonly Color ChestLid = HexUtil.ToColor("#6B4F35");
        private static readonly Color ChestBody = HexUtil.ToColor("#8A6A45");
        private static readonly Color ChestLock = HexUtil.ToColor("#D9A43B");

        private SaveData save;
        [SerializeField] private ShopConfig shopConfig; // 宝箱金额（BuildMenuScene 自动注入）
        [Tooltip("宝箱图标（空 = 程序化画一个占位箱子）；有图直接拖进来即可替换")]
        [SerializeField] private Sprite chestIcon;
        private bool hardMode;
        private int openChapter; // 0 = 章节列表；1-5 = 章节详情
        private Transform root;
        private readonly List<GameObject> view = new List<GameObject>();
        private GameObject jumpConfirm; // 中途硬闯确认框（P，用户 2026-10-26）

        private void Start()
        {
            EnsureScene();
            save = SaveSystem.Load();
            hardMode = save != null && save.lastMode == 1;
            root = new GameObject("LevelSelectRoot").transform;
            root.SetParent(transform, false);
            Rebuild();
        }

        private void Add(GameObject go) { if (go != null) view.Add(go); }

        private void Clear()
        {
            foreach (var go in view) if (go != null) Destroy(go);
            view.Clear();
        }

        private void Rebuild()
        {
            Clear();
            ClearJumpConfirm();
            root.localScale = Vector3.one * ScreenFit();

            Add(Label(root, "选 关", new Vector3(0f, 4.25f, -1.7f), 16f, Ink,
                TextAlignmentOptions.Center, 8f).gameObject);

            // 模式切换（右上）
            Add(Button(root, "Mode", new Vector3(6.6f, 4.25f, -1.7f), new Vector2(3.0f, 1.0f),
                hardMode ? "困难模式" : "简单模式", hardMode ? Vermilion : BlueInk, ToggleMode, out _, 7.5f));

            // 返回（左上）：章节列表 = 主界面；章节详情 = 返回列表
            Add(Button(root, "Back", new Vector3(-7.4f, 4.25f, -1.7f), new Vector2(2.0f, 0.9f),
                openChapter == 0 ? "主界面" : "返回", Ink, OnBack, out _, 6.5f));

            if (openChapter == 0) BuildChapterList();
            else BuildChapterDetail(openChapter);
        }

        // ============ 章节列表 ============

        private void BuildChapterList()
        {
            for (int c = 1; c <= 5; c++)
            {
                int first = (c - 1) * 5 + 1;
                int last = c * 5;
                bool anyUnlocked = IsLevelUnlocked(first);
                float x = -6.0f + (c - 1) * 3.0f;
                int chapter = c;
                Add(Button(root, "Ch" + c, new Vector3(x, 0.6f, -1.7f), new Vector2(2.6f, 2.0f),
                    "第" + c + "章\n" + first + "-" + last + "关",
                    anyUnlocked ? Ink : Dim,
                    anyUnlocked ? (System.Action)(() => { openChapter = chapter; Rebuild(); }) : null,
                    out _, 7.5f, anyUnlocked ? (Color?)null : LockedBg));

                // 章进度：按当前模式统计已通关关数（旧版卡片只有章号+关段，看不出打到哪了）
                int cleared = 0;
                for (int l = first; l <= last; l++) if (IsCleared(l)) cleared++;
                Add(Label(root, "★ " + cleared + " / 5", new Vector3(x - 0.55f, -0.85f, -1.7f), 4.4f,
                    cleared > 0 ? Gold : Dim, TextAlignmentOptions.Center, 3.2f).gameObject);

                // 宝箱图标（用户 2026-11-07：从章节详情挪到列表，每个「★ N/5」旁边一个，按当前模式各算各的）
                AddChest(c, x + 0.75f, -0.85f);
            }

            AddModeNotes();
        }

        // ============ 章节详情 ============

        private void BuildChapterDetail(int ch)
        {
            int first = (ch - 1) * 5 + 1;
            int last = ch * 5;

            Add(Label(root, "第" + ch + "章", new Vector3(0f, 2.8f, -1.7f), 12f, Ink,
                TextAlignmentOptions.Center, 8f).gameObject);

            // 5 个关卡按钮（一排放）
            for (int i = 0; i < 5; i++)
            {
                int idx = first + i;
                bool unlocked = IsLevelUnlocked(idx);
                float x = -6.0f + i * 3.0f;
                int level = idx;
                var btn = Button(root, "Lv" + idx, new Vector3(x, 1.0f, -1.7f), new Vector2(2.4f, 1.6f),
                    "第" + (i + 1) + "关", unlocked ? Ink : Dim,
                    unlocked ? (System.Action)(() => StartLevel(level, false)) : null,
                    out _, 8f, unlocked ? (Color?)null : LockedBg);
                Add(btn);
                // 通关标记（右上角 ★）——按当前模式显示
                if (IsCleared(idx))
                    Label(btn.transform, "★", new Vector3(0.95f, 0.62f, -0.3f), 6.5f,
                        HexUtil.ToColor("#A67C1B"), TextAlignmentOptions.Center, 1f);
            }

            Add(Button(root, "First", new Vector3(-2.4f, -1.3f, -1.7f), new Vector2(3.6f, 1.1f),
                "从头开始", Ink, () => StartLevel(first, false), out _, 8f));

            Add(Button(root, "Continue", new Vector3(2.4f, -1.3f, -1.7f), new Vector2(3.6f, 1.1f),
                "继续存档", Ink, () => StartLevel(SavedLevelIn(first, last), true), out _, 7.5f));

            // 宝箱已挪到章节列表（用户 2026-11-07）：这里只保留「从头开始 / 继续存档」。
            AddModeNotes();
        }

        /// <summary>
        /// 底部两行「模式说明」（用户 2026-11-06 定稿）：当前模式高亮、另一条压淡。
        /// 旧版是单个「简单模式」标签，和右上角那颗同名按钮重复，看不出两种难度到底差在哪。
        /// </summary>
        private void AddModeNotes()
        {
            Add(Label(root, "简单模式 · 新手友好，但会禁用天赋", new Vector3(0f, -2.55f, -1.7f), 4.8f,
                hardMode ? Dim : BlueInk, TextAlignmentOptions.Center, 14f).gameObject);
            Add(Label(root, "困难模式 · 敌人血量翻倍，但可以配置天赋", new Vector3(0f, -3.05f, -1.7f), 4.8f,
                hardMode ? Vermilion : Dim, TextAlignmentOptions.Center, 14f).gameObject);
        }

        /// <summary>本模式存档进度（clamp 到本章范围）；无档 = 本章第一关。</summary>
        private int SavedLevelIn(int first, int last)
        {
            int saved = 0;
            if (save != null)
            {
                var st = save.StateFor(hardMode);
                if (st != null) saved = st.levelIndex;
            }
            return Mathf.Clamp(saved, first, last);
        }

        /// <summary>关解锁：≤ maxLevelUnlocked-1（至少放行 L1，教学可选不强制）。</summary>
        private bool IsLevelUnlocked(int idx)
        {
            int max = save != null ? save.maxLevelUnlocked : 1;
            return idx <= Mathf.Max(1, max - 1);
        }

        /// <summary>当前模式该关是否已通关（选关右上角 ★）。</summary>
        private bool IsCleared(int idx)
        {
            if (save == null) return false;
            var list = hardMode ? save.hardClearedLevels : save.easyClearedLevels;
            return list != null && list.Contains(idx);
        }

        // ============ 章节宝箱（用户 2026-11-07） ============

        private string ChestKey(int ch) => ch + "_" + (hardMode ? 1 : 0);

        private bool IsChestClaimed(int ch)
        {
            return save != null && save.claimedChests != null && save.claimedChests.Contains(ChestKey(ch));
        }

        /// <summary>该章·该模式已通关关数（星数）。</summary>
        private int ChapterStars(int ch)
        {
            if (save == null) return 0;
            var list = hardMode ? save.hardClearedLevels : save.easyClearedLevels;
            if (list == null) return 0;
            int first = (ch - 1) * 5 + 1, n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] >= first && list[i] <= first + 4) n++;
            return n;
        }

        private void ClaimChest(int ch, Vector3 iconPos)
        {
            if (save == null || ChapterStars(ch) < 5 || IsChestClaimed(ch)) return;
            if (save.claimedChests == null) save.claimedChests = new System.Collections.Generic.List<string>();
            save.claimedChests.Add(ChestKey(ch));
            int amount = shopConfig != null ? Mathf.Max(0, shopConfig.chestGold) : 0;
            save.gold += amount;
            SaveSystem.Write(save);
            SfxPlayer.Confirm();
            SpawnGoldToast(iconPos, amount);
            Rebuild();
        }

        /// <summary>
        /// 章节宝箱图标（用户 2026-11-07）：有 chestIcon 就贴图，没有就程序化画一个
        /// （轮廓 + 箱体 + 箱盖 + 金锁，四块白图拼的）。三种状态：
        ///   可领 = 满色 + 金锁，可点；已领 = 开盖 + 极淡；未满 = 闭盖 + 淡。
        /// 命中区 1.1×1.0（比图标大一圈，指尖友好）；不可领时不挂 collider。
        /// </summary>
        private void AddChest(int ch, float x, float y)
        {
            bool claimed = IsChestClaimed(ch);
            bool ready = !claimed && ChapterStars(ch) >= 5;

            var holder = new GameObject("Chest_" + ch);
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = new Vector3(x, y, -1.8f);

            float a = ready ? 1f : (claimed ? 0.20f : 0.38f);

            if (chestIcon != null)
            {
                var go = new GameObject("Icon", typeof(SpriteRenderer));
                go.transform.SetParent(holder.transform, false);
                float s = 0.66f / Mathf.Max(0.01f, chestIcon.bounds.size.y);
                go.transform.localScale = new Vector3(s, s, 1f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = chestIcon;
                sr.color = new Color(1f, 1f, 1f, a);
            }
            else
            {
                // z：轮廓最后 → 箱体 → 箱盖 → 金锁最前（本工程 z 越小越靠相机）
                ChestPart(holder.transform, 0f, 0.02f, 0.74f, 0.58f, 0.02f, Fade(ChestOutline, a));
                ChestPart(holder.transform, 0f, -0.10f, 0.64f, 0.28f, 0f, Fade(ChestBody, a));
                ChestPart(holder.transform, 0f, claimed ? 0.22f : 0.14f, 0.68f, 0.18f, -0.01f, Fade(ChestLid, a));
                ChestPart(holder.transform, 0f, -0.02f, 0.13f, 0.15f, -0.02f, Fade(ChestLock, a));
            }

            Add(holder);

            if (!ready) return;
            var col = holder.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.1f, 1.0f);
            holder.AddComponent<PanelClicker>().Init(() => ClaimChest(ch, new Vector3(x, y, 0f)));
        }

        /// <summary>宝箱的一块（白图染色）。挂 holder 下，z 由调用方排。</summary>
        private void ChestPart(Transform parent, float x, float y, float w, float h, float z, Color c)
        {
            var go = new GameObject("Part", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = c;
        }

        private static Color Fade(Color c, float a) { c.a *= a; return c; }

        /// <summary>领奖反馈：+N 金币从箱子位置向上飘淡出（不挂 view，Rebuild 不会把它一起清掉）。</summary>
        private void SpawnGoldToast(Vector3 iconPos, int amount)
        {
            if (amount <= 0) return;
            var t = Label(root, "+" + amount + " 金币", new Vector3(iconPos.x, iconPos.y + 0.5f, -1.75f), 4.2f,
                Gold, TextAlignmentOptions.Center, 3.6f);
            StartCoroutine(ToastRoutine(t));
        }

        private System.Collections.IEnumerator ToastRoutine(TextMeshPro t)
        {
            Vector3 p0 = t.transform.localPosition;
            Color c0 = t.color;
            const float life = 1.1f;
            for (float e = 0f; e < life; e += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = Mathf.Clamp01(e / life);
                t.transform.localPosition = p0 + Vector3.up * (0.55f * k);
                var c = c0;
                c.a = 1f - k * k;
                t.color = c;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        private void ToggleMode()
        {
            hardMode = !hardMode;
            if (save != null)
            {
                save.lastMode = hardMode ? 1 : 0;
                SaveSystem.Write(save);
            }
            Rebuild();
        }

        private void OnBack()
        {
            if (openChapter != 0) { openChapter = 0; Rebuild(); }
            else LoadSceneSafe("MainMenu");
        }

        private void StartLevel(int idx, bool resume)
        {
            if (!IsLevelUnlocked(idx)) return;

            // P（用户 2026-10-26）：只允许「本章第一关」或「本模式存档进度」直接开打；
            // 其余关卡（中途硬闯）先弹确认，防手滑跳过剧情/奖励。
            bool chapterFirst = idx % 5 == 1; // 关卡号 1 基：1/6/11/16/21
            var st = save != null ? save.StateFor(hardMode) : null;
            bool savedProgress = st != null && st.levelIndex == idx;
            if (resume || chapterFirst || savedProgress) { Launch(idx, resume); return; }

            ShowJumpConfirm(idx);
        }

        private void Launch(int idx, bool resume)
        {
            GameSession.Reset();
            GameSession.Active = true;
            GameSession.HardMode = hardMode;
            GameSession.StartLevelIndex = idx;
            GameSession.ResumeSnapshot = resume;
            LoadSceneSafe("Game");
        }

        /// <summary>中途硬闯确认：暗幕 + 「你真的要直接打这一关？」+ 确定/取消。</summary>
        private void ShowJumpConfirm(int idx)
        {
            ClearJumpConfirm();
            var holder = new GameObject("JumpConfirm");
            holder.transform.SetParent(transform, false);
            jumpConfirm = holder;
            var hi = holder.transform;

            // 底幕（z=-2.0 → 盖住选关页 z=-1.7 的按钮；自身吃点击）
            var dimGo = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            dimGo.transform.SetParent(hi, false);
            dimGo.transform.localPosition = new Vector3(0f, 0f, -2.0f);
            dimGo.transform.localScale = new Vector3(30f, 15f, 1f);
            var dim = dimGo.GetComponent<SpriteRenderer>();
            dim.sprite = whiteSprite;
            dim.color = new Color(0.15f, 0.13f, 0.10f, 0.82f);
            dimGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            Label(hi, "你真的要直接打这一关？", new Vector3(0f, 0.9f, -2.1f), 11f,
                HexUtil.ToColor("#F7F2E7"), TextAlignmentOptions.Center, 16f);
            Label(hi, "第 " + idx + " 关 · 既不是本章第一关，也不是存档进度", new Vector3(0f, 0.05f, -2.1f), 6.2f,
                new Color(0.86f, 0.83f, 0.75f), TextAlignmentOptions.Center, 16f);

            Button(hi, "JumpYes", new Vector3(-2.3f, -1.7f, -2.1f), new Vector2(3.2f, 1.1f),
                "确定", Vermilion, () => { ClearJumpConfirm(); Launch(idx, false); }, out _, 8.5f);
            Button(hi, "JumpNo", new Vector3(2.3f, -1.7f, -2.1f), new Vector2(3.2f, 1.1f),
                "取消", Ink, ClearJumpConfirm, out _, 8.5f);
        }

        private void ClearJumpConfirm()
        {
            if (jumpConfirm != null) Destroy(jumpConfirm);
            jumpConfirm = null;
        }
    }
}
