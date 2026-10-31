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
        private static readonly Color Dim = new Color(0.62f, 0.60f, 0.56f, 0.9f);
        private static readonly Color LockedBg = new Color(0.80f, 0.77f, 0.70f, 0.7f);

        private SaveData save;
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
            }

            Add(Label(root, hardMode ? "困难模式（敌人血量 ×2）" : "简单模式",
                new Vector3(0f, -2.4f, -1.7f), 6f, hardMode ? Vermilion : BlueInk,
                TextAlignmentOptions.Center, 12f).gameObject);
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

            Add(Label(root, hardMode ? "困难模式" : "简单模式",
                new Vector3(0f, -2.6f, -1.7f), 6f, hardMode ? Vermilion : BlueInk,
                TextAlignmentOptions.Center, 8f).gameObject);
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
