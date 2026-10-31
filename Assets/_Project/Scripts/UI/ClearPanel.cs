using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 通关结算画面（P5，用户 2026-10-25 定案）。
    /// 只在该关「首次通关 + 有奖励卡」时出现（GameManager 判定）；没有奖励直接跳过本画面。
    /// 结构：上标题（clearTitle，空→默认）/ 中奖励字（大字、阶色；困难 = 红 + 「6 阶解锁」角标）/
    ///       下说明（clearDesc，空→不显示）/ 继续按钮（点任意处亦可）。
    /// 世界坐标 + SpriteRenderer + 物理点击（同 DeckPanel，无预制体）。
    /// z：底幕 -1.55 / 纸 -1.6 / 奖励字 -1.7 / 文字 -1.8（相机 z=-10 朝 +z，z 越小越靠前）。
    /// </summary>
    public class ClearPanel : MonoBehaviour
    {
        private static readonly Color DimBg = new Color(0.13f, 0.11f, 0.09f, 0.86f);
        private static readonly Color Paper = HexUtil.ToColor("#F7F2E7");
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");
        private static readonly Color Ink = HexUtil.ToColor("#26231F");
        private static readonly Color FadeInk = new Color(0.36f, 0.32f, 0.27f, 0.95f);

        private TMP_FontAsset font;
        private Sprite white;
        private GameObject root;
        private TextMeshPro titleLabel;
        private TextMeshPro descLabel;
        private TextMeshPro badgeLabel;
        private GameObject continueGo;
        private SpriteRenderer continueBg;
        private TextMeshPro continueLabel;
        private Transform charRoot;
        private Action onContinue;

        public bool IsOpen { get; private set; }

        /// <summary>运行时自建一次（GameManager 启动时调用）。</summary>
        public void Init(TMP_FontAsset fontAsset, Sprite whiteSprite)
        {
            font = fontAsset;
            white = whiteSprite;

            root = new GameObject("Root");
            root.transform.SetParent(transform, false);

            // 底幕：压暗全场 + 挡住战场点击（点任意处 = 继续）
            var bgGo = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            bgGo.transform.SetParent(root.transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 0f, -1.55f);
            bgGo.transform.localScale = new Vector3(30f, 15f, 1f);
            var dim = bgGo.GetComponent<SpriteRenderer>();
            dim.sprite = white;
            dim.color = DimBg;
            bgGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.01f;
            bgGo.AddComponent<PanelClicker>().Init(OnContinueClicked);

            // 中央宣纸
            var paperGo = new GameObject("Paper", typeof(SpriteRenderer));
            paperGo.transform.SetParent(root.transform, false);
            paperGo.transform.localPosition = new Vector3(0f, 0f, -1.6f);
            paperGo.transform.localScale = new Vector3(15.6f, 8.8f, 1f);
            var paper = paperGo.GetComponent<SpriteRenderer>();
            paper.sprite = white;
            paper.color = Paper;

            titleLabel = MakeLabel("Title", 7f);
            titleLabel.color = Vermilion;
            titleLabel.transform.localPosition = new Vector3(0f, 3.1f, -1.8f);
            titleLabel.rectTransform.sizeDelta = new Vector2(14f, 1.6f);

            var charsGo = new GameObject("Chars");
            charsGo.transform.SetParent(root.transform, false);
            charsGo.transform.localPosition = new Vector3(0f, 0.55f, -1.7f);
            charRoot = charsGo.transform;

            badgeLabel = MakeLabel("Badge", 4.4f);
            badgeLabel.color = Vermilion;
            badgeLabel.transform.localPosition = new Vector3(0f, -1.35f, -1.8f);
            badgeLabel.text = "6 阶解锁（困难模式奖励）";

            descLabel = MakeLabel("Desc", 4.6f);
            descLabel.color = FadeInk;
            descLabel.enableWordWrapping = true;
            descLabel.alignment = TextAlignmentOptions.Top;
            descLabel.transform.localPosition = new Vector3(0f, -2.0f, -1.8f);
            descLabel.rectTransform.sizeDelta = new Vector2(13.4f, 2.4f);
            descLabel.rectTransform.pivot = new Vector2(0.5f, 1f);

            // 继续按钮
            continueGo = new GameObject("Continue", typeof(SpriteRenderer), typeof(BoxCollider2D));
            continueGo.transform.SetParent(root.transform, false);
            continueBg = continueGo.GetComponent<SpriteRenderer>();
            continueBg.sprite = white;
            continueBg.color = Vermilion;
            continueGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
            continueGo.AddComponent<PanelClicker>().Init(OnContinueClicked);
            continueLabel = MakeLabel("ContinueLabel", 8f);
            continueLabel.text = "继 续";
            continueLabel.color = Paper;

            root.SetActive(false); // 骨架建完才隐藏（TMP 激活态创建）
        }

        /// <summary>
        /// 打开结算。rewards = 本次获得的塔字（普通=新卡 / 困难=6 阶解锁）；
        /// title/desc 来自 LevelConfig（空则走默认文案）。
        /// </summary>
        public void Show(List<string> rewards, bool tier6, string title, string desc, Action onDone)
        {
            onContinue = onDone;

            root.SetActive(true);
            IsOpen = true;
            SfxPlayer.Menu(); // 结算打开音（用户 2026-10-26）

            titleLabel.text = string.IsNullOrWhiteSpace(title)
                ? (tier6 ? "恭喜解锁 6 阶" : "恭喜获得新塔防")
                : title;
            badgeLabel.gameObject.SetActive(tier6);

            bool hasDesc = !string.IsNullOrWhiteSpace(desc);
            descLabel.gameObject.SetActive(hasDesc);
            if (hasDesc) descLabel.text = desc;

            // 奖励字：重建
            for (int i = charRoot.childCount - 1; i >= 0; i--)
                Destroy(charRoot.GetChild(i).gameObject);

            int n = rewards != null ? rewards.Count : 0;
            int show = Mathf.Min(n, 4);
            float spacing = show <= 1 ? 0f : Mathf.Min(2.8f, 11f / Mathf.Max(1, show - 1));
            for (int i = 0; i < show; i++)
            {
                var go = new GameObject("Reward" + rewards[i],
                    typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
                go.transform.SetParent(charRoot, false);
                go.transform.localPosition = new Vector3((i - (show - 1) * 0.5f) * spacing, 0f, 0f);
                var t = go.AddComponent<TextMeshPro>();
                if (font != null) t.font = font;
                t.fontSize = 20f;
                t.alignment = TextAlignmentOptions.Center;
                t.enableWordWrapping = false;
                t.raycastTarget = false;
                t.text = rewards[i];
                // 阶色：困难 = 6 阶红；普通 = 新卡（入场即 L1 阶色）
                t.color = tier6 ? Vermilion : TowerView.TierColor(1);
            }

            // 继续按钮
            continueGo.transform.localPosition = new Vector3(0f, -3.55f, -1.65f);
            continueGo.transform.localScale = new Vector3(4.8f, 1.5f, 1f);
            continueLabel.transform.localPosition = new Vector3(0f, -3.55f, -1.8f);
            continueLabel.fontSize = 8f;
            continueLabel.color = Paper;
        }

        public void Hide()
        {
            IsOpen = false;
            if (root != null) root.SetActive(false);
        }

        private void OnContinueClicked()
        {
            if (!IsOpen) return;
            Hide();
            var cb = onContinue;
            onContinue = null;
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
