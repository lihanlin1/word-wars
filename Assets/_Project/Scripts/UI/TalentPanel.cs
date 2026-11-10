using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 天赋配置面板（用户 2026-11-04，ADR-086）：进困难新章时弹一次，从已购天赋里选 ≤ 槽位个，
    /// 确认后锁定该章（存 hardChapterState.talentChapter + chapterTalents），本章 5 关内不可改。
    /// 运行时自建全屏面板（同 DeckPanel/CodexPanel 口径）；面板打开期间 GameManager 暂停。
    /// 2026-11-06 修版式 + 图层：
    ///  · 整块前移 1.2（root.z）——「万能铲 / 菜单 / ×1」是世界坐标按钮（z≈-1.6），会压在面板上；
    ///  · 打开期间收起 HUD Canvas——ScreenSpaceOverlay 永远画在世界精灵之上，只能整体收起来；
    ///  · 卡片重排成 4 列网格、按行居中，名称/描述/「已选」各占一条基线，不再互相压字。
    /// </summary>
    public class TalentPanel : MonoBehaviour
    {
        private static readonly Color DimBg = new Color(0.13f, 0.11f, 0.09f, 0.88f);
        private static readonly Color Paper = HexUtil.ToColor("#F7F2E7");
        private static readonly Color Ink = HexUtil.ToColor("#26231F");
        private static readonly Color Fade = new Color(0.42f, 0.39f, 0.34f, 0.95f);
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");
        private static readonly Color CardBg = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color SelBg = new Color(0.93f, 0.86f, 0.76f, 1f);

        // ===== 版式（设计稿 19.2 × 10.8）=====
        private const float PanelZ = -1.2f;     // 整块前移量（压过世界坐标 HUD 与世界层遮罩）
        private const float CardW = 3.55f;
        private const float CardH = 2.40f;
        private const float CardGapX = 3.80f;   // 4 列 → 占 11.4，居中后两侧各留 3.9
        private const float RowY0 = 1.15f;
        private const float RowGapY = 2.70f;
        private const float BottomY = -3.72f;   // 「已选 / 确定」这一行

        private TMP_FontAsset font;
        private Sprite white;
        private TalentConfig talents;
        private Canvas hudCanvas;
        private GameObject root;
        private Transform grid;
        private TextMeshPro subLabel;
        private TextMeshPro countLabel;
        private GameObject confirmGo;
        private TextMeshPro confirmLabel;
        private SpriteRenderer confirmBg;

        private readonly List<string> owned = new List<string>();
        private readonly List<string> picked = new List<string>();
        private int slots = 1;
        private Action<List<string>> onConfirm;

        public bool IsOpen { get; private set; }

        public void Init(TMP_FontAsset fontAsset, Sprite whiteSprite, TalentConfig talentConfig, Canvas hud = null)
        {
            font = fontAsset;
            white = whiteSprite;
            talents = talentConfig;
            hudCanvas = hud;

            root = new GameObject("Root");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, PanelZ);

            var bg = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            bg.transform.SetParent(root.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, -1.55f);
            bg.transform.localScale = new Vector3(30f, 15f, 1f);
            var bs = bg.GetComponent<SpriteRenderer>();
            bs.sprite = white;
            bs.color = DimBg;
            bg.GetComponent<BoxCollider2D>().size = Vector2.one * 1.01f;

            var paper = new GameObject("Paper", typeof(SpriteRenderer), typeof(BoxCollider2D));
            paper.transform.SetParent(root.transform, false);
            paper.transform.localPosition = new Vector3(0f, 0f, -1.6f);
            paper.transform.localScale = new Vector3(17.4f, 9.2f, 1f);
            var ps = paper.GetComponent<SpriteRenderer>();
            ps.sprite = white;
            ps.color = Paper;
            paper.GetComponent<BoxCollider2D>().size = Vector2.one * 1.01f;

            Label(root.transform, "Title", "天赋配置", new Vector3(0f, 3.72f, -1.8f), 9.5f, Ink,
                TextAlignmentOptions.Center, 10f);
            subLabel = Label(root.transform, "Sub", "从已购天赋中选择", new Vector3(0f, 3.05f, -1.8f), 3.9f,
                Fade, TextAlignmentOptions.Center, 14f);

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform, false);
            grid = gridGo.transform;

            countLabel = Label(root.transform, "Count", "", new Vector3(-8.05f, BottomY, -1.8f), 4.8f, Ink,
                TextAlignmentOptions.Left, 6f);

            confirmGo = new GameObject("Confirm", typeof(SpriteRenderer), typeof(BoxCollider2D));
            confirmGo.transform.SetParent(root.transform, false);
            confirmGo.transform.localPosition = new Vector3(6.1f, BottomY, -1.65f);
            confirmGo.transform.localScale = new Vector3(3.2f, 1.05f, 1f);
            confirmBg = confirmGo.GetComponent<SpriteRenderer>();
            confirmBg.sprite = white;
            confirmBg.color = Vermilion;
            confirmGo.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
            confirmGo.AddComponent<PanelClicker>().Init(Confirm);
            confirmLabel = Label(root.transform, "ConfirmLabel", "确 定", new Vector3(6.1f, BottomY, -1.8f),
                6f, Paper, TextAlignmentOptions.Center, 3.2f);

            root.SetActive(false);
        }

        public void Show(List<string> ownedIds, int slotCount, Action<List<string>> confirmCallback)
        {
            owned.Clear();
            if (ownedIds != null) owned.AddRange(ownedIds);
            slots = Mathf.Clamp(slotCount, 1, 7);
            picked.Clear();
            onConfirm = confirmCallback;

            SetHudVisible(false); // 模态：HUD Canvas 让位（否则「墨/印/操作提示条」压在面板上）
            if (subLabel != null)
                subLabel.text = "从已购天赋中选 ≤" + slots + " 个 · 再点一次取消 · 本章锁定，下一章可重配";

            root.SetActive(true); // 先激活再建卡排（TMP Awake 坑）
            IsOpen = true;
            SfxPlayer.Menu();
            BuildGrid();
        }

        public void Hide()
        {
            IsOpen = false;
            if (root != null) root.SetActive(false);
            SetHudVisible(true);
        }

        private void SetHudVisible(bool visible)
        {
            if (hudCanvas != null) hudCanvas.gameObject.SetActive(visible);
        }

        private void BuildGrid()
        {
            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                var ch = grid.GetChild(i).gameObject;
                ch.SetActive(false);
                Destroy(ch);
            }

            // 先过滤空定义，再算行列——否则 owned 里有坏 id 时行宽会算错、整排偏左
            var defs = new List<TalentDef>();
            for (int i = 0; i < owned.Count; i++)
            {
                var d = talents != null ? talents.Find(owned[i]) : null;
                if (d != null) defs.Add(d);
            }

            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                int col = i % 4, row = i / 4;
                int rowLen = Mathf.Min(4, defs.Count - row * 4);
                float x = (col - (rowLen - 1) * 0.5f) * CardGapX;
                float y = RowY0 - row * RowGapY;
                bool sel = picked.Contains(def.id);
                string id = def.id;

                // 选中朱框：画在卡片底下、比卡大 3% → 只露一圈边（先画才能被卡盖住）
                if (sel)
                {
                    var frame = new GameObject("Frame_" + id, typeof(SpriteRenderer));
                    frame.transform.SetParent(grid, false);
                    frame.transform.localPosition = new Vector3(x, y, -1.67f);
                    frame.transform.localScale = new Vector3(CardW * 1.035f, CardH * 1.04f, 1f);
                    var fs = frame.GetComponent<SpriteRenderer>();
                    fs.sprite = white;
                    fs.color = Vermilion;
                }

                var body = new GameObject("T_" + id, typeof(SpriteRenderer), typeof(BoxCollider2D));
                body.transform.SetParent(grid, false);
                body.transform.localPosition = new Vector3(x, y, -1.7f);
                body.transform.localScale = new Vector3(CardW, CardH, 1f);
                var sr = body.GetComponent<SpriteRenderer>();
                sr.sprite = white;
                sr.color = sel ? SelBg : CardBg;
                body.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
                body.AddComponent<PanelClicker>().Init(() => Toggle(id));

                // 名称（卡片顶部）→ 描述（中段，可换行）→ 选中标记（底部）
                // 描述用 Top 对齐：文字顶边 = 框中心 + 半高（1.05/2）→ 传 y+0.03 让顶边落在 y+0.55
                Label(grid, "Name", def.name, new Vector3(x, y + 0.85f, -1.85f), 4.8f,
                    sel ? Vermilion : Ink, TextAlignmentOptions.Center, CardW - 0.3f);

                var d = Label(grid, "Desc", def.desc, new Vector3(x - 1.62f, y + 0.03f, -1.85f), 2.8f, Fade,
                    TextAlignmentOptions.TopLeft, CardW - 0.31f);
                d.enableWordWrapping = true;
                d.rectTransform.sizeDelta = new Vector2(CardW - 0.31f, 1.05f);

                if (sel)
                    Label(grid, "Sel", "已选", new Vector3(x, y - 0.95f, -1.85f), 3.2f, Vermilion,
                        TextAlignmentOptions.Center, 2f);
            }

            Refresh();
        }

        private void Toggle(string id)
        {
            if (picked.Contains(id)) picked.Remove(id);
            else if (picked.Count < slots) picked.Add(id);
            BuildGrid();
        }

        private void Refresh()
        {
            countLabel.text = "已选 " + picked.Count + " / " + slots;
            countLabel.color = picked.Count > 0 ? Ink : Fade;
            bool ok = picked.Count <= slots;
            confirmBg.color = ok ? Vermilion : new Color(0.62f, 0.58f, 0.52f, 0.9f);
            confirmLabel.color = ok ? Paper : new Color(0.85f, 0.83f, 0.78f, 0.9f);
        }

        private void Confirm()
        {
            if (!IsOpen || picked.Count > slots) return;
            var chosen = new List<string>(picked);
            Hide();
            var cb = onConfirm;
            onConfirm = null;
            cb?.Invoke(chosen);
        }

        /// <summary>
        /// 建一条文字。Left = 从 pos.x 起笔、Right = 到 pos.x 收笔（TMP 的 rect 以 pos 为中心，
        /// 不补这半个宽度的话「左列」会整块往左溢出）。
        /// </summary>
        private TextMeshPro Label(Transform parent, string name, string text, Vector3 pos, float size,
            Color color, TextAlignmentOptions align, float width)
        {
            float dx = 0f;
            if (align == TextAlignmentOptions.Left || align == TextAlignmentOptions.TopLeft ||
                align == TextAlignmentOptions.BottomLeft) dx = width * 0.5f;
            else if (align == TextAlignmentOptions.Right || align == TextAlignmentOptions.TopRight ||
                     align == TextAlignmentOptions.BottomRight) dx = -width * 0.5f;

            var go = new GameObject(name, typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x + dx, pos.y, pos.z);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.enableWordWrapping = false;
            t.text = text;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 1.6f);
            return t;
        }
    }
}
