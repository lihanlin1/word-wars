using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 局外商店（用户 2026-11-04，ADR-085）：第 4 个场景。
    /// 2026-11-06 重排版式：栏目式价目表（标题列 / 说明列 / 价格列 / 操作列，
    /// 行间细线 + 页眉文武线），修掉旧版「左列以 pos 为**中心**摆放」导致的
    /// 标题压说明、两段文字都往屏幕左边缘溢出、金币被右边缘切掉的问题；
    /// 整块内容按相机可视宽度等比缩放，4:3 / 16:10 不出框。
    /// 五类：新塔防（占位）/ 墨上限 / 天赋×7 / 天赋槽 / 6 阶单塔解锁。
    /// 价格全在 ShopConfig（黄底）；天赋名称/描述/价格在 TalentConfig。
    /// 金币：存档 v4 的 save.gold，所有购买立即写档。
    /// 运行时自建世界坐标 UI（同 MainMenu/LevelSelect 口径）。
    /// </summary>
    public class ShopController : MenuSceneBase
    {
        private static readonly Color Ink = HexUtil.ToColor("#26231F");
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");
        private static readonly Color Gold = HexUtil.ToColor("#A67C1B");
        private static readonly Color Fade = new Color(0.45f, 0.43f, 0.40f, 0.95f);
        private static readonly Color Paper = HexUtil.ToColor("#F7F2E7");
        private static readonly Color DisabledBg = new Color(0.84f, 0.81f, 0.74f, 1f);
        private static readonly Color CardPaper = HexUtil.ToColor("#F3EBDA");
        private static readonly Color CardPaperOwned = HexUtil.ToColor("#E5DCC9");
        private static readonly Color DimBg = new Color(0.13f, 0.11f, 0.09f, 0.86f);

        // ===== 版式常量（设计稿 19.2 × 10.8，16:9 下 1 世界单位 ≈ 77 px）=====
        private const float CameraOrtho = 5.4f;      // MenuSceneBase.EnsureScene 固定值
        private const float DesignHalfW = 9.6f;      // 16:9 下的相机半宽
        private const float ContentX = 8.45f;        // 内容左右边界（两侧各留 1.15）
        private const float HeaderY = 4.10f;         // 页眉中线
        private const float RowTopY = 2.72f;         // 第一行中线
        private const float RowGap = 1.45f;          // 行距
        private const float TitleX = -8.35f;         // 标题列：左起笔
        private const float DescX = -5.30f;          // 说明列：左起笔
        private const float MetaRightX = 5.45f;      // 价格/数量列：右收笔
        private const float BtnX = 7.30f;            // 操作列：按钮中线
        private const float BtnW = 2.20f;
        private const float BtnH = 0.90f;
        private const string HintBase = "金币来源：通关结算（剩余墨 ×1 + 剩余印 ×10）";

        /// <summary>一行商品的样式（决定按钮填色 / 字色 / 能不能点）。</summary>
        private enum RowKind { Buy, View, Soon }

        [SerializeField] private ShopConfig shopConfig;
        [SerializeField] private TalentConfig talentConfig;
        [SerializeField] private TowerPoolDef towerPool;

        private SaveData save;
        private Transform root;
        private TextMeshPro goldLabel;
        private TextMeshPro hintLabel;
        private Transform overlay;
        private float fit = 1f;
        private float flashTimer;
        private readonly List<GameObject> view = new List<GameObject>();

        private void Start()
        {
            EnsureScene();
            save = SaveSystem.Load() ?? new SaveData();
            root = new GameObject("ShopRoot").transform;
            root.SetParent(transform, false);
            RebuildShop();
        }

        private void Update()
        {
            if (flashTimer <= 0f) return;
            flashTimer -= Time.deltaTime;
            if (flashTimer > 0f || hintLabel == null) return;
            hintLabel.text = HintBase;
            hintLabel.color = Fade;
        }

        /// <summary>按相机可视范围算整体缩放：设计稿 19.2 宽 / 10.8 高，只缩不放。</summary>
        private float FitScale()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return 1f;
            float halfW = cam.orthographicSize * cam.aspect;
            float k = Mathf.Min(halfW / DesignHalfW, cam.orthographicSize / CameraOrtho);
            return Mathf.Clamp(k, 0.35f, 1f);
        }

        private void Add(GameObject go) { if (go != null) view.Add(go); }

        private void ClearShop()
        {
            foreach (var go in view) if (go != null) Destroy(go);
            view.Clear();
        }

        // ================= 主列表 =================

        private void RebuildShop()
        {
            CloseOverlay();
            ClearShop();

            fit = FitScale();
            root.gameObject.SetActive(true);
            root.localScale = new Vector3(fit, fit, 1f);
            flashTimer = 0f;

            BuildHeader();

            int steps = InkSteps;
            int inkNow = SaveSystem.InitialInk(steps);
            bool inkMax = steps >= SaveSystem.InkCapMaxSteps;
            int bought = save.talents != null ? save.talents.Count : 0;
            int total = talentConfig != null && talentConfig.talents != null ? talentConfig.talents.Count : 7;
            int slots = Mathf.Clamp(save.talentSlots, 1, 7);
            bool slotMax = slots >= 7;
            int t6 = save.tier6Unlocked != null ? save.tier6Unlocked.Count : 0;
            int towers = Tier6Chars().Count; // 铲/盆不参与 6 阶（用户 2026-11-07）

            // 1) 新塔防（占位）
            AddRow(0, "新塔防",
                shopConfig != null && shopConfig.newTowerEnabled ? "新内容即将开放" : "后续版本开放",
                string.Empty, "未开放", null, RowKind.Soon);

            // 2) 墨上限
            AddRow(1, "墨上限",
                "初始墨 " + inkNow + (inkMax ? "（已满）" : " → " + (inkNow + SaveSystem.InkCapStep)) +
                "　·　第 " + steps + "/" + SaveSystem.InkCapMaxSteps + " 步",
                inkMax ? "已满" : PriceLabel(InkStepPrice(steps)),
                inkMax ? "已满" : "购买",
                inkMax ? (Action)null : BuyInkStep,
                inkMax ? RowKind.Soon : RowKind.Buy);

            // 3) 天赋库
            AddRow(2, "天赋", "永久解锁，可反复选配", "已购 " + bought + "/" + total,
                "查看", OpenTalents, RowKind.View);

            // 4) 天赋槽
            AddRow(3, "天赋槽", "当前 " + slots + " 个　·　上限 7 个",
                slotMax ? "已满" : PriceLabel(SlotPrice(slots)),
                slotMax ? "已满" : "购买",
                slotMax ? (Action)null : BuySlot,
                slotMax ? RowKind.Soon : RowKind.Buy);

            // 5) 6 阶单塔解锁
            AddRow(4, "6 阶解锁", "全局共享，普通/困难都可用", "已解锁 " + t6 + "/" + towers,
                "查看", OpenTier6, RowKind.View);

            hintLabel = Label(root, HintBase, new Vector3(0f, -4.30f, -1.7f), 3.9f, Fade,
                TextAlignmentOptions.Center, 16f);
            Add(hintLabel.gameObject);
        }

        private void BuildHeader()
        {
            const float z = -1.7f;

            Add(Button(root, "Back", new Vector3(-7.32f, HeaderY, z), new Vector2(1.9f, 0.85f),
                "返回", Ink, () => LoadSceneSafe("MainMenu"), out _, 5f));
            Add(Label(root, "商 店", new Vector3(0f, HeaderY, z), 14f, Ink,
                TextAlignmentOptions.Center, 10f).gameObject);

            goldLabel = LabelAligned(root, GoldText(), new Vector3(ContentX, HeaderY, z), 7.2f, Gold,
                TextAlignmentOptions.Right, 5.4f);
            Add(goldLabel.gameObject);

            // 文武线：粗一细，压住页眉
            Add(Rule(new Vector3(0f, 3.32f, z), ContentX * 2f, 0.05f, new Color(0.15f, 0.13f, 0.11f, 0.45f)));
            Add(Rule(new Vector3(0f, 3.20f, z), ContentX * 2f, 0.014f, new Color(0.15f, 0.13f, 0.11f, 0.20f)));
        }

        /// <summary>一行商品：细线上沿 → 标题 / 说明 / 价格 / 按钮 四列对齐。</summary>
        private void AddRow(int index, string title, string desc, string meta, string btnText,
            Action click, RowKind kind)
        {
            const float z = -1.7f;
            float y = RowTopY - index * RowGap;

            Add(Rule(new Vector3(0f, y - RowGap * 0.5f, z), ContentX * 2f, 0.012f,
                new Color(0.15f, 0.13f, 0.11f, 0.12f)));

            Add(LabelAligned(root, title, new Vector3(TitleX, y, z), 6f, Ink,
                TextAlignmentOptions.Left, 3.0f).gameObject);
            Add(LabelAligned(root, desc, new Vector3(DescX, y, z), 4.1f, Fade,
                TextAlignmentOptions.Left, 8f).gameObject);

            if (!string.IsNullOrEmpty(meta))
                Add(LabelAligned(root, meta, new Vector3(MetaRightX, y, z), 4.1f,
                    kind == RowKind.Buy ? Gold : Fade, TextAlignmentOptions.Right, 3.2f).gameObject);

            Color textColor;
            Color? bg;
            if (kind == RowKind.Buy) { textColor = Paper; bg = Vermilion; }
            else if (kind == RowKind.Soon) { textColor = Fade; bg = DisabledBg; }
            else { textColor = Ink; bg = null; }

            Add(Button(root, "Row" + index, new Vector3(BtnX, y, z), new Vector2(BtnW, BtnH),
                btnText, textColor, kind == RowKind.Soon ? null : click, out _, 5f, bg));
        }

        /// <summary>1×1 白图拉伸成细线（分隔线 / 页眉线）。</summary>
        private GameObject Rule(Vector3 pos, float width, float height, Color color)
        {
            var go = new GameObject("Rule", typeof(SpriteRenderer));
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(width, height, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = color;
            return go;
        }

        private string GoldText()
        {
            if (save == null) save = new SaveData();
            return "<size=58%><color=#8C8880>金币</color></size> " + save.gold;
        }

        private static string PriceLabel(int price) => price <= 0 ? "免费" : price + " 金币";

        private int InkSteps => save.playerMeta != null ? save.playerMeta.inkCapSteps : 0;
        private int InkStepPrice(int steps) => shopConfig != null ? shopConfig.InkStepPrice(steps) : 0;
        private int SlotPrice(int slots) => shopConfig != null ? shopConfig.SlotPrice(slots) : 0;
        private int Tier6Price => shopConfig != null ? shopConfig.Tier6Price : 0;

        private bool TryPay(int price)
        {
            if (price <= 0) return true;
            if (save.gold < price) { Flash("金币不足"); return false; }
            save.gold -= price;
            return true;
        }

        /// <summary>底部提示行临时改成红字，1.6 秒后自己变回「金币来源」。</summary>
        private void Flash(string msg)
        {
            if (hintLabel == null) return;
            hintLabel.text = msg;
            hintLabel.color = Vermilion;
            flashTimer = 1.6f;
        }

        private void Persist()
        {
            SaveSystem.Write(save);
            RefreshGold();
        }

        private void RefreshGold()
        {
            if (goldLabel != null) goldLabel.text = GoldText();
        }

        // ================= 购买 =================

        private void BuyInkStep()
        {
            int steps = InkSteps;
            if (steps >= SaveSystem.InkCapMaxSteps) return;
            if (!TryPay(InkStepPrice(steps))) return;
            if (save.playerMeta == null) save.playerMeta = new PlayerMetaData();
            save.playerMeta.inkCapSteps = steps + 1;
            Persist();
            RebuildShop();
        }

        private void BuySlot()
        {
            int slots = Mathf.Clamp(save.talentSlots, 1, 7);
            if (slots >= 7) return;
            if (!TryPay(SlotPrice(slots))) return;
            save.talentSlots = slots + 1;
            Persist();
            RebuildShop();
        }

        private void BuyTalent(TalentDef def)
        {
            if (def == null) return;
            if (save.talents == null) save.talents = new List<string>();
            if (save.talents.Contains(def.id)) return;
            if (!TryPay(def.price)) return;
            save.talents.Add(def.id);
            Persist();
            OpenTalents();
        }

        private void BuyTier6(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return;
            if (save.tier6Unlocked == null) save.tier6Unlocked = new List<string>();
            if (save.tier6Unlocked.Contains(charId)) return;
            if (!TryPay(Tier6Price)) return;
            save.tier6Unlocked.Add(charId);
            Persist();
            OpenTier6();
        }

        // ================= 遮罩面板 =================

        private void CloseOverlay()
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
        }

        /// <summary>暗底 + 纸面 + 页眉；内容挂在 Content（等比缩放）下，返回 Content。</summary>
        private Transform OpenOverlay(string title, string subtitle)
        {
            CloseOverlay();
            // 模态：先关掉主列表（直接 SetActive，物理判定一起失效，
            // 避免「点遮罩空白处 → 命中后面那排购买按钮 → 白扣金币」）
            if (root != null) root.gameObject.SetActive(false);

            overlay = new GameObject("Overlay_" + title).transform;
            overlay.SetParent(transform, false);

            var cam = Camera.main;
            float halfW = cam != null && cam.orthographic ? cam.orthographicSize * cam.aspect : DesignHalfW;
            float halfH = cam != null && cam.orthographic ? cam.orthographicSize : CameraOrtho;

            // 暗底必须带判定：否则遮罩后面的商品按钮还能被点到（误触直接扣金币）
            var dim = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            dim.transform.SetParent(overlay, false);
            dim.transform.localPosition = new Vector3(0f, 0f, -2.0f);
            dim.transform.localScale = new Vector3(halfW * 2f + 2f, halfH * 2f + 2f, 1f);
            var ds = dim.GetComponent<SpriteRenderer>();
            ds.sprite = whiteSprite;
            ds.color = DimBg;
            dim.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            var content = new GameObject("Content").transform;
            content.SetParent(overlay, false);
            content.localScale = new Vector3(fit, fit, 1f);

            var paper = new GameObject("Paper", typeof(SpriteRenderer), typeof(BoxCollider2D));
            paper.transform.SetParent(content, false);
            paper.transform.localPosition = new Vector3(0f, 0f, -2.05f);
            paper.transform.localScale = new Vector3(17.6f, 9.2f, 1f);
            var ps = paper.GetComponent<SpriteRenderer>();
            ps.sprite = whiteSprite;
            ps.color = Paper;
            paper.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            LabelAligned(content, title, new Vector3(-7.95f, 3.85f, -2.15f), 8.5f, Ink,
                TextAlignmentOptions.Left, 9f);
            if (!string.IsNullOrEmpty(subtitle))
                LabelAligned(content, subtitle, new Vector3(-7.95f, 3.30f, -2.15f), 3.9f, Fade,
                    TextAlignmentOptions.Left, 12f);

            LabelAligned(content, "金币 " + save.gold, new Vector3(6.0f, 3.85f, -2.15f), 6f, Gold,
                TextAlignmentOptions.Right, 4.4f);
            Button(content, "OverlayClose", new Vector3(7.22f, 3.85f, -2.1f), new Vector2(1.9f, 0.8f),
                "返回", Ink, () => { CloseOverlay(); RebuildShop(); }, out _, 4.8f);

            LabelAligned(content, "点击未拥有的卡片购买（价格在卡片底部）", new Vector3(0f, -4.05f, -2.15f), 3.4f,
                Fade, TextAlignmentOptions.Center, 14f);
            return content;
        }

        /// <summary>卡片底：白图染色 + 可选点击区（不点 = 纯装饰）。</summary>
        private GameObject Card(Transform parent, float x, float y, float w, float h, Color color, Action onClick)
        {
            var go = new GameObject("Card", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, -2.1f);
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = color;
            if (onClick != null)
            {
                go.AddComponent<BoxCollider2D>().size = Vector2.one * 1.02f;
                go.AddComponent<PanelClicker>().Init(onClick);
            }
            return go;
        }

        // ================= 天赋库 =================

        private void OpenTalents()
        {
            if (talentConfig == null || talentConfig.talents == null || talentConfig.talents.Count == 0)
            {
                CloseOverlay();
                Flash("天赋配置缺失（跑一次「Word Wars/Setup/重建主界面与选关场景」）");
                return;
            }

            var list = talentConfig.talents;
            int owned = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && save.talents != null && save.talents.Contains(list[i].id)) owned++;

            var t = OpenOverlay("天赋库", "已购 " + owned + "/" + list.Count + "　·　永久解锁，可反复选配");

            for (int i = 0; i < list.Count; i++)
            {
                var def = list[i];
                if (def == null) continue;
                int col = i % 4, row = i / 4;
                int rowLen = Mathf.Min(4, list.Count - row * 4);
                float x = (col - (rowLen - 1) * 0.5f) * 3.95f;
                float y = 1.05f - row * 2.70f;

                bool has = save.talents != null && save.talents.Contains(def.id);
                Card(t, x, y, 3.85f, 2.55f, has ? CardPaperOwned : CardPaper,
                    has ? (Action)null : () => BuyTalent(def));

                float nameX = x;
                TextAlignmentOptions nameAlign = TextAlignmentOptions.Center;
                if (def.icon != null)
                {
                    float s = 0.72f / Mathf.Max(0.01f, def.icon.bounds.size.x);
                    var iconGo = new GameObject("Icon", typeof(SpriteRenderer));
                    iconGo.transform.SetParent(t, false);
                    iconGo.transform.localPosition = new Vector3(x - 1.45f, y + 0.92f, -2.15f);
                    iconGo.transform.localScale = new Vector3(s, s, 1f);
                    var isr = iconGo.GetComponent<SpriteRenderer>();
                    isr.sprite = def.icon;
                    isr.color = Color.white;
                    nameX = x - 0.72f;
                    nameAlign = TextAlignmentOptions.Left;
                }

                LabelAligned(t, def.name, new Vector3(nameX, y + 0.92f, -2.2f), 4.8f,
                    has ? Fade : Vermilion, nameAlign, 3.4f);

                var d = LabelAligned(t, def.desc, new Vector3(x - 1.70f, y + 0.08f, -2.2f), 3f, Fade,
                    TextAlignmentOptions.TopLeft, 3.40f);
                d.enableWordWrapping = true;
                d.rectTransform.sizeDelta = new Vector2(3.40f, 1.20f);

                LabelAligned(t, has ? "已购买" : PriceLabel(def.price), new Vector3(x, y - 0.92f, -2.2f), 4f,
                    has ? Fade : Gold, TextAlignmentOptions.Center, 3.5f);
            }
        }

        // ================= 6 阶解锁 =================

        /// <summary>6 阶只对「塔」开放：铲/盆是功能牌，没有阶（用户 2026-11-07）。</summary>
        private static bool HasTier6(TowerCharStats d) =>
            d != null && d.charId != "铲" && d.charId != "盆";

        private List<TowerCharStats> Tier6Chars()
        {
            var list = new List<TowerCharStats>();
            if (towerPool == null || towerPool.chars == null) return list;
            foreach (var d in towerPool.chars) if (HasTier6(d)) list.Add(d);
            return list;
        }

        private void OpenTier6()
        {
            if (towerPool == null || towerPool.chars == null || towerPool.chars.Count == 0)
            {
                CloseOverlay();
                Flash("塔字池缺失（跑一次「Word Wars/Setup/重建主界面与选关场景」）");
                return;
            }

            var chars = Tier6Chars();
            int unlocked = 0;
            for (int i = 0; i < chars.Count; i++)
                if (chars[i] != null && save.tier6Unlocked != null && save.tier6Unlocked.Contains(chars[i].charId)) unlocked++;

            string sub = (Tier6Price <= 0 ? "免费解锁" : "每座 " + Tier6Price + " 金币") +
                         "　·　已解锁 " + unlocked + "/" + chars.Count;
            var t = OpenOverlay("6 阶解锁", sub);

            const int cols = 7;
            for (int i = 0; i < chars.Count; i++)
            {
                var def = chars[i];
                if (def == null) continue;
                int row = i / cols, col = i % cols;
                int rowLen = Mathf.Min(cols, chars.Count - row * cols);
                float x = (col - (rowLen - 1) * 0.5f) * 2.1f;
                float y = 1.35f - row * 1.90f;

                bool has = save.tier6Unlocked != null && save.tier6Unlocked.Contains(def.charId);
                Card(t, x, y, 1.90f, 1.55f, has ? CardPaperOwned : CardPaper,
                    has ? (Action)null : () => BuyTier6(def.charId));

                LabelAligned(t, def.charId, new Vector3(x, y + 0.22f, -2.2f), 8f,
                    has ? Fade : Vermilion, TextAlignmentOptions.Center, 1.8f);
                LabelAligned(t, has ? "已解锁" : PriceLabel(Tier6Price), new Vector3(x, y - 0.50f, -2.2f), 2.9f,
                    has ? Fade : Gold, TextAlignmentOptions.Center, 1.8f);
            }
        }
    }
}
