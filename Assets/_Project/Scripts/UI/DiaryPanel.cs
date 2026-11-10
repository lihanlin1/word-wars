using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 日记画面（P6，用户 2026-10-25 定案）：章首关进关前、背包界面之前显示。
    /// 文案 = LevelConfig.diaryPages（一条 = 一页，Inspector 编辑）；空 = 不出现（GameManager 判）。
    /// 交互：文字从左到右逐字显示（速度 GameParams.diaryCharSeconds，黄底可调）；
    ///       打字中点击 = 全显；全显后点击 = 下一页；最后一页点击 = 关闭。
    ///       右上「跳过 ›」= 立刻关闭；重打时同样显示但可跳过。
    /// 世界坐标 + SpriteRenderer + 物理点击（同 DeckPanel）。
    /// </summary>
    public class DiaryPanel : MonoBehaviour
    {
        private static readonly Color DimBg = new Color(0.13f, 0.11f, 0.09f, 0.86f);
        private static readonly Color Paper = HexUtil.ToColor("#F7F2E7");
        private static readonly Color Ink = HexUtil.ToColor("#26231F");
        private static readonly Color FadeInk = new Color(0.42f, 0.38f, 0.32f, 0.95f);

        private TMP_FontAsset font;
        private Sprite white;
        private GameObject root;
        private Canvas hudCanvas;   // 屏幕空间 HUD：面板打开期间整体收起（Overlay 压不住）
        private TextMeshPro titleLabel;
        private TextMeshPro bodyLabel;
        private TextMeshPro pageLabel;
        private TextMeshPro hintLabel;
        private TextMeshPro skipLabel;
        private GameObject skipGo;

        private readonly List<string> pages = new List<string>();
        private int pageIndex;
        private bool typing;
        private Coroutine typingRoutine;
        private float charSeconds = 0.035f;
        private Action onDone;

        public bool IsOpen { get; private set; }

        public void Init(TMP_FontAsset fontAsset, Sprite whiteSprite, Canvas hud = null)
        {
            font = fontAsset;
            white = whiteSprite;
            hudCanvas = hud;

            root = new GameObject("Root");
            root.transform.SetParent(transform, false);
            // 世界坐标 HUD（万能铲 / 菜单 / ×1 在 z≈-1.6）会盖在面板上 → 整块前移 1.2
            root.transform.localPosition = new Vector3(0f, 0f, -1.2f);

            // 底幕 + 点击（点任意处 = 继续/全显）
            var bgGo = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            bgGo.transform.SetParent(root.transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 0f, -1.55f);
            bgGo.transform.localScale = new Vector3(30f, 15f, 1f);
            var dim = bgGo.GetComponent<SpriteRenderer>();
            dim.sprite = white;
            dim.color = DimBg;
            bgGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.01f;
            bgGo.AddComponent<PanelClicker>().Init(OnAdvanceClicked);

            var paperGo = new GameObject("Paper", typeof(SpriteRenderer));
            paperGo.transform.SetParent(root.transform, false);
            paperGo.transform.localPosition = new Vector3(0f, 0f, -1.6f);
            paperGo.transform.localScale = new Vector3(15.6f, 8.8f, 1f);
            var paper = paperGo.GetComponent<SpriteRenderer>();
            paper.sprite = white;
            paper.color = Paper;

            titleLabel = MakeLabel("Title", 5.4f);
            titleLabel.color = FadeInk;
            titleLabel.transform.localPosition = new Vector3(0f, 3.35f, -1.8f);
            titleLabel.rectTransform.sizeDelta = new Vector2(14f, 1.2f);

            bodyLabel = MakeLabel("Body", 6.4f);
            bodyLabel.color = Ink;
            bodyLabel.enableWordWrapping = true;
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.transform.localPosition = new Vector3(-6.6f, 2.35f, -1.8f);
            bodyLabel.rectTransform.sizeDelta = new Vector2(13.2f, 5.2f);
            bodyLabel.rectTransform.pivot = new Vector2(0f, 1f);
            bodyLabel.lineSpacing = 12f;

            pageLabel = MakeLabel("Page", 3.6f);
            pageLabel.color = FadeInk;
            pageLabel.alignment = TextAlignmentOptions.Center;
            pageLabel.transform.localPosition = new Vector3(5.9f, -3.45f, -1.8f);
            pageLabel.rectTransform.sizeDelta = new Vector2(2.6f, 1f);

            hintLabel = MakeLabel("Hint", 3.8f);
            hintLabel.color = FadeInk;
            hintLabel.transform.localPosition = new Vector3(0f, -3.45f, -1.8f);
            hintLabel.text = "点击继续";

            // 跳过（右上角）：重打时快速略过，不打断打字机
            skipGo = new GameObject("Skip", typeof(SpriteRenderer), typeof(BoxCollider2D));
            skipGo.transform.SetParent(root.transform, false);
            skipGo.transform.localPosition = new Vector3(6.0f, 3.35f, -1.65f);
            skipGo.transform.localScale = new Vector3(2.6f, 1.0f, 1f);
            var skipBg = skipGo.GetComponent<SpriteRenderer>();
            skipBg.sprite = white;
            skipBg.color = new Color(0.36f, 0.32f, 0.27f, 0.18f);
            skipGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
            skipGo.AddComponent<PanelClicker>().Init(OnSkipClicked);
            skipLabel = MakeLabel("SkipLabel", 4f);
            skipLabel.text = "跳过 ›";
            skipLabel.color = FadeInk;
            skipLabel.alignment = TextAlignmentOptions.Center;
            skipLabel.transform.localPosition = new Vector3(6.0f, 3.35f, -1.8f);
            skipLabel.rectTransform.sizeDelta = new Vector2(2.6f, 1f);

            root.SetActive(false);
        }

        /// <summary>打开日记。title = 关卡名；pageList = 每页正文；speed = 每字秒数。</summary>
        public void Show(string title, List<string> pageList, float speed, Action onFinished)
        {
            pages.Clear();
            if (pageList != null)
                foreach (var p in pageList)
                    if (!string.IsNullOrEmpty(p)) pages.Add(p);

            onDone = onFinished;
            charSeconds = Mathf.Max(0.005f, speed);
            pageIndex = 0;

            if (hudCanvas != null) hudCanvas.gameObject.SetActive(false); // 模态：HUD 让位
            root.SetActive(true);
            IsOpen = true;
            SfxPlayer.Menu(); // 日记打开音（用户 2026-10-26）
            titleLabel.text = string.IsNullOrEmpty(title) ? "" : title;

            StartPage();
        }

        public void Hide()
        {
            StopTyping();
            IsOpen = false;
            if (root != null) root.SetActive(false);
            if (hudCanvas != null) hudCanvas.gameObject.SetActive(true);
        }

        private void StartPage()
        {
            StopTyping();
            pageLabel.text = (pageIndex + 1) + " / " + pages.Count;
            hintLabel.text = "点击继续";
            bodyLabel.text = "";
            typingRoutine = StartCoroutine(TypePage(pages[pageIndex]));
        }

        private IEnumerator TypePage(string full)
        {
            typing = true;
            int shown = 0;
            float acc = 0f;
            full ??= "";
            while (shown < full.Length)
            {
                acc += Time.unscaledDeltaTime;
                int target = Mathf.Min(full.Length, Mathf.FloorToInt(acc / charSeconds));
                if (target > shown)
                {
                    shown = target;
                    bodyLabel.text = full.Substring(0, shown);
                }
                yield return null;
            }
            bodyLabel.text = full;
            typing = false;
            typingRoutine = null;
            hintLabel.text = pageIndex < pages.Count - 1 ? "点击翻页" : "点击关闭";
        }

        private void StopTyping()
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;
            typing = false;
        }

        private void OnAdvanceClicked()
        {
            if (!IsOpen) return;

            // 打字中 → 全显；已全显 → 翻页 / 关闭
            if (typing)
            {
                StopTyping();
                bodyLabel.text = pages[pageIndex];
                hintLabel.text = pageIndex < pages.Count - 1 ? "点击翻页" : "点击关闭";
                return;
            }

            pageIndex++;
            if (pageIndex < pages.Count) StartPage();
            else Close();
        }

        private void OnSkipClicked()
        {
            if (!IsOpen) return;
            Close();
        }

        private void Close()
        {
            Hide();
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }

        private TextMeshPro MakeLabel(string name, float size)
        {
            // 3D TMP 需要 RectTransform + MeshRenderer/MeshFilter（同 DeckPanel 口径）
            var go = new GameObject(name, typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            go.transform.SetParent(root.transform, false);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.color = Ink;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(14f, 1.6f);
            return t;
        }
    }
}
