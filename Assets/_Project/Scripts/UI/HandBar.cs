using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 手牌条（世界坐标，画面底部）：手牌 5 张（v2.3）+ 右端「抽 / 卖 / 下一波」按钮。
    /// 交互为「点选式」：点卡选中（朱框高亮）→ 点目标；拖拽留给 W4 触控专项。
    /// HandCard 上报 index：0..N-1 = 手牌；-1 抽；-2 卖；-3 下一波。
    /// 全部代码生成，无需预制体。
    /// </summary>
    public class HandBar : MonoBehaviour
    {
        private const float CardSpacing = 1.7f;   // （已外露 UIPanelConfig；此常量仅作空配置缺省）
        private const float CardSize = 1.15f;
        private const float BarShiftX = -0.7f;    // 整条左移，给右端三按钮腾位
        private const float DrawX = 4.55f;
        private const float SellX = 6.05f;
        private const float NextX = 8.1f;
        private const float ButtonSize = 1.5f;
        private const float NextW = 2.4f;
        private const float NextH = 1.4f;
        private const float CardFontSize = 10f;
        private const float NextFontSize = 7.5f;
        // F2 红铲（ADR-054）：手牌区左侧的独立功能钮（不占手牌位、不进塔字池）
        // 用户 2026-10-23：红铲移到左上角；原位置让给「临时格」（塔暂存）
        private const float ShovelX = -7.55f;
        private const float ShovelTopY = 9.15f; // 相对手牌条（y=-4.55）→ 世界 y≈4.6（左上，按钮上沿 5.3 不出屏）
        private const float ShovelW = 2.0f;
        private const float ShovelH = 1.4f;
        private const float ShovelFontSize = 6.5f;

        private static readonly Color InkColor = HexUtil.ToColor("#26231F");       // 浓墨
        private static readonly Color ShovelColor = HexUtil.ToColor("#6B4F35");    // 铲：褐
        private static readonly Color BlueInk = HexUtil.ToColor("#2B4C7E");
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");
        private static readonly Color CardEdge = new Color(0.15f, 0.13f, 0.11f, 0.26f); // 卡面常态细边

        private TextMeshPro[] cardLabels;
        private SpriteRenderer[] cardBgs;
        private SpriteRenderer[] cardFrames;

        private TMP_FontAsset font;
        private Sprite whiteSprite;
        private TextMeshPro drawCostLabel;
        private SpriteRenderer drawBg;
        private SpriteRenderer sellBg;
        private Vector3 sellBaseScale; // F2（docs/14）：卖钮常态 scale（拿起塔高亮放大用）
        private SpriteRenderer nextBg;
        // F2 红铲（ADR-054）：整钮显隐 + 纸底 + 主字「红铲/取消」+ 价签「50墨/退款」
        private GameObject shovelGo;
        private SpriteRenderer shovelBg;
        private TextMeshPro shovelLabel;
        private TextMeshPro shovelPriceLabel;
        private UIPanelConfig cfg; // null = 内置缺省（ADR-024）

        /// <summary>按钮世界坐标（飘字反馈用）。</summary>
        public Vector3 DrawButtonWorldPos { get; private set; }
        public Vector3 SellButtonWorldPos { get; private set; }
        public Vector3 NextButtonWorldPos { get; private set; }
        /// <summary>F2 红铲按钮世界坐标（飘字反馈用）。</summary>
        public Vector3 ShovelButtonWorldPos { get; private set; }

        /// <summary>红铲点击索引（HandCard 上报值；负号段 -1 抽 / -2 卖 / -3 下一波 之后）。</summary>
        public const int ShovelClick = -4;

        // —— 布局取值（cfg 为空回落内置缺省；手牌条只在建时读一次，改资产需重进 Play）——
        private float CCardSpacing => Mathf.Max(0.4f, cfg != null ? cfg.handCardSpacing : CardSpacing);
        private float CCardSize => Mathf.Max(0.3f, cfg != null ? cfg.handCardSize : CardSize);
        private float CBarShiftX => cfg != null ? cfg.handBarShiftX : BarShiftX;
        private float CDrawX => cfg != null ? cfg.handDrawX : DrawX;
        private float CSellX => cfg != null ? cfg.handSellX : SellX;
        private float CNextX => cfg != null ? cfg.handNextX : NextX;
        private float CButtonSize => Mathf.Max(0.3f, cfg != null ? cfg.handButtonSize : ButtonSize);
        private float CNextW => Mathf.Max(0.4f, cfg != null ? cfg.handNextW : NextW);
        private float CNextH => Mathf.Max(0.3f, cfg != null ? cfg.handNextH : NextH);
        private float CCardFontSize => Mathf.Max(1f, cfg != null ? cfg.handCardFontSize : CardFontSize);
        private float CNextFontSize => Mathf.Max(1f, cfg != null ? cfg.handNextFontSize : NextFontSize);
        private float CShovelX => cfg != null ? cfg.handShovelX : ShovelX;
        private float CShovelW => Mathf.Max(0.4f, cfg != null ? cfg.handShovelW : ShovelW);
        private float CShovelH => Mathf.Max(0.3f, cfg != null ? cfg.handShovelH : ShovelH);
        private float CShovelFontSize => Mathf.Max(1f, cfg != null ? cfg.handShovelFontSize : ShovelFontSize);

        public void Init(TMP_FontAsset fontAsset, Sprite white, Vector3 centerPos, int cardCount,
            UIPanelConfig config = null)
        {
            font = fontAsset;
            whiteSprite = white;
            cfg = config;
            transform.position = centerPos;

            cardLabels = new TextMeshPro[cardCount];
            cardBgs = new SpriteRenderer[cardCount];
            cardFrames = new SpriteRenderer[cardCount];
            for (int i = 0; i < cardCount; i++)
            {
                float x = (i - (cardCount - 1) * 0.5f) * CCardSpacing + CBarShiftX;
                var card = MakeCard("Card" + (i + 1), x, CCardSize, CCardSize);
                card.label.fontSize = CCardFontSize;
                cardLabels[i] = card.label;
                cardBgs[i] = card.bg;
                cardFrames[i] = card.frame;
                card.click.Init(i);
            }

            // 抽（-1）：主字 + 底部价格（固定价，满手/缺墨变灰）
            var draw = MakeCard("DrawButton", CDrawX, CButtonSize, CButtonSize);
            // ⚠️ 卡底是 1.5 倍的局部缩放，标签挂在它下面会被同倍放大——旧版「抽」就是这样撑爆按钮的。
            //    主字改挂手牌条根（等比），字号按世界单位给。
            draw.label.gameObject.SetActive(false);
            var drawLabel = MakeLabel(transform, "DrawLabel", new Vector3(CDrawX, 0.2f, -0.05f), 9f);
            drawLabel.text = "抽";
            draw.click.Init(-1);
            drawBg = draw.bg;
            drawCostLabel = MakeLabel(transform, "DrawCost", new Vector3(CDrawX, -0.52f, -0.05f), 3.2f);
            drawCostLabel.color = BlueInk;
            drawCostLabel.text = ""; // 价格由 Refresh 按一口价（池总价值）动态填
            DrawButtonWorldPos = draw.bg.transform.position;

            // 卖（-2）：手牌卡或拿起中的塔 → 回 2×字位
            var sell = MakeCard("SellButton", CSellX, CButtonSize, CButtonSize);
            sell.label.gameObject.SetActive(false);
            var sellLabel = MakeLabel(transform, "SellLabel", new Vector3(CSellX, 0.2f, -0.05f), 9f);
            sellLabel.text = "卖";
            sell.click.Init(-2);
            sellBg = sell.bg;
            sellBaseScale = sellBg.transform.localScale; // F2：常态 scale 存底（拿起放大）
            var sellSub = MakeLabel(transform, "SellSub", new Vector3(CSellX, -0.52f, -0.05f), 3.2f);
            sellSub.color = BlueInk;
            sellSub.text = "半价卖出";
            SellButtonWorldPos = sell.bg.transform.position;

            // 下一波（-3）：提前召唤。
            // 宽条按钮是非等比缩放：文字若挂在它下面会被横向拉伸变形，
            // 故主文字挂到手牌条根（等比），按钮只提供纸底与点击判定。
            var next = MakeCard("NextButton", CNextX, CNextW, CNextH);
            next.label.text = ""; // 主文字另挂条根
            next.click.Init(-3);
            nextBg = next.bg;
            var nextLabel = MakeLabel(transform, "NextLabel", new Vector3(CNextX, 0.02f, -0.05f), CNextFontSize);
            nextLabel.text = "下一波";
            NextButtonWorldPos = next.bg.transform.position;

            // 红铲（-4，ADR-054）：局内道具——花墨在章图无格空位造一格地皮。
            // 宽条按钮同「下一波」：主字与价签挂条根（等比），按钮只提供纸底与点击判定。
            var shovel = MakeCard("ShovelButton", CShovelX, CShovelW, CShovelH);
            shovel.bg.transform.localPosition = new Vector3(CShovelX, ShovelTopY, 0.1f); // 左上角
            shovel.label.text = "";
            shovel.click.Init(ShovelClick);
            shovelGo = shovel.bg.gameObject;
            shovelBg = shovel.bg;
            shovelLabel = MakeLabel(transform, "ShovelLabel", new Vector3(CShovelX, ShovelTopY + 0.18f, -0.05f), CShovelFontSize);
            shovelLabel.text = "万能铲";
            shovelPriceLabel = MakeLabel(transform, "ShovelPrice", new Vector3(CShovelX, ShovelTopY - 0.36f, -0.05f), 4.5f);
            shovelPriceLabel.text = "";
            shovelPriceLabel.color = BlueInk;
            ShovelButtonWorldPos = shovel.bg.transform.position;
        }

        /// <summary>
        /// 刷新：卡面/铲配色/选中朱框；抽卡价格与可用态；卖出/下一波按钮点亮条件。
        /// 手感口径反转（用户 2026-10-10 三轮）：ended 参数删除——结束后买/卖照常（只停生产），
        /// 置灰仅反映真实可用态（墨够不够/有没有得卖）。
        /// </summary>
        public void Refresh(HandModel hand, double ink, int selected, bool towerPicked, bool canNextWave,
            int shovelPrice, bool shovelVisible, bool shovelActive, bool shovelAfford)
        {
            int n = cardLabels != null ? cardLabels.Length : 0;
            for (int i = 0; i < n; i++)
            {
                bool has = hand != null && i < hand.Cards.Count;
                string ch = has ? hand.Cards[i] : "";

                if (cardLabels[i].text != ch) cardLabels[i].text = ch;

                // 铲 = 功能牌，用褐色区分（不进塔字池）
                var baseC = ch == HandModel.ShovelId ? ShovelColor : InkColor;
                baseC.a = has ? 1f : 0.2f;
                cardLabels[i].color = baseC;

                var c = cardBgs[i].color;
                c.a = has ? 0.95f : 0.12f;
                cardBgs[i].color = c;

                cardFrames[i].enabled = has;
                cardFrames[i].color = i == selected ? Vermilion : CardEdge;
            }

            if (drawCostLabel == null || hand == null) return;

            drawCostLabel.text = hand.DrawCost.ToString("0") + "墨";
            bool afford = hand.CanDraw && ink >= hand.DrawCost;
            drawCostLabel.color = afford ? BlueInk : Vermilion;
            if (drawBg != null)
            {
                var c = drawBg.color;
                c.a = afford ? 0.95f : 0.55f;
                drawBg.color = c;
            }

            if (sellBg != null)
            {
                bool sellable = towerPicked || (selected >= 0 && selected < hand.Cards.Count);
                var c = sellBg.color;
                c.a = sellable ? 0.95f : 0.5f;
                sellBg.color = c;
            }

            if (nextBg != null)
            {
                var c = nextBg.color;
                c.a = canNextWave ? 0.95f : 0.5f;
                nextBg.color = c;
            }

            // 红铲（F2.2，ADR-054）：未解锁 = 整钮隐藏（L0-L2）；模式中 = 「取消/退款」朱底；
            // 墨不足 = 置灰但价格照显（点了无事发生——判定在 GameManager.ToggleRedShovel）。
            if (shovelGo != null)
            {
                shovelGo.SetActive(shovelVisible);
                shovelLabel.gameObject.SetActive(shovelVisible);
                shovelPriceLabel.gameObject.SetActive(shovelVisible);
                if (shovelVisible)
                {
                    shovelLabel.text = shovelActive ? "取消" : "万能铲";
                    shovelPriceLabel.text = shovelActive ? "全额退款" : shovelPrice + "墨";
                    var c = shovelActive ? Vermilion : new Color(0.93f, 0.89f, 0.80f, 0.95f);
                    if (!shovelActive) c.a = shovelAfford ? 0.95f : 0.5f;
                    shovelBg.color = c;
                    shovelLabel.color = shovelActive ? new Color(0.96f, 0.94f, 0.90f) : InkColor;
                    shovelPriceLabel.color = shovelActive ? new Color(0.96f, 0.94f, 0.90f) : BlueInk;
                }
            }
        }

        /// <summary>F2（docs/14 ADR-063）：卖钮强调态——拿起塔时高亮放大（提示可拖到此处卖），放回复原。</summary>
        public void SetSellEmphasis(bool on)
        {
            if (sellBg == null) return;
            sellBg.transform.localScale = on ? sellBaseScale * 1.3f : sellBaseScale;
        }

        // ============ 构件 ============

        private class CardParts
        {
            public SpriteRenderer bg;
            public SpriteRenderer frame;
            public TextMeshPro label;
            public HandCard click;
        }

        private CardParts MakeCard(string name, float x, float sizeX, float sizeY)
        {
            // 卡底：纸色方块 + 点击判定
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, 0f, 0.1f);
            go.transform.localScale = new Vector3(sizeX, sizeY, 1f);
            var bg = go.GetComponent<SpriteRenderer>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.93f, 0.89f, 0.80f, 0.95f); // 卡纸
            var col = go.GetComponent<BoxCollider2D>();
            col.size = Vector2.one * 1.02f;

            // 朱框：略大、垫在卡底后面——常态是淡墨细边（让手牌从棋盘里"立"起来），选中时点成朱色
            var frameGo = new GameObject("Frame", typeof(SpriteRenderer));
            frameGo.transform.SetParent(go.transform, false);
            frameGo.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            frameGo.transform.localScale = new Vector3(1.07f, 1.07f, 1f);
            var frame = frameGo.GetComponent<SpriteRenderer>();
            frame.sprite = whiteSprite;
            frame.color = CardEdge;
            frame.enabled = true;

            var parts = new CardParts
            {
                bg = bg,
                frame = frame,
                label = MakeLabel(bg.transform, "Label", new Vector3(0f, 0.02f, -0.15f), 10f),
                click = go.AddComponent<HandCard>()
            };
            return parts;
        }

        private TextMeshPro MakeLabel(Transform parent, string name, Vector3 localPos, float size)
        {
            // 3D TMP 需要 RectTransform + MeshRenderer/MeshFilter；物体激活中，Awake 已就绪
            var go = new GameObject(name, typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = InkColor;
            t.enableWordWrapping = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(1.6f, 0.6f);
            return t;
        }
    }
}
