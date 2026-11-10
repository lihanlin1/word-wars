using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 背包/构筑面板（v3.0-D2/D3，ADR-023/024）：关前/失败重试时改构筑。
    /// 结构（用户 D1 验收反馈「有点乱」→ 分区化）：
    ///   上「构筑」= 5 个槽位（本关抽卡池；点槽位卡 = 移出）
    ///   中「背包」= 本关池内全部已拥有卡（点卡 = 入构筑；已入构筑的在背包区隐藏——两区不重影）
    ///   底「Σ 一口价 + 出阵大按钮」
    /// 世界坐标 + SpriteRenderer + 物理点击（镜像 HandBar 模式，无预制体）。
    /// 交互索引（DeckCard）：-1 = 出阵；-2 = 上一页；-3 = 下一页；0..n-1 = 背包卡；100+k = 构筑槽位 k。
    /// F4.2b 分页（05 §5 扩容阶梯「13-24 卡：分页优先于滚动」）：每页 6 列×2 行 = 12 张，
    /// 第 3 行起进第 2 页——页码并入背包区标签 + 两侧「上一页/下一页」按钮；
    /// 行数约束由分页接管（不再加密列数压 3 行——旧法「第三行」会撞 Σ 行）。
    /// z 层：底 -1.55 / 卡与按钮 -1.65 / 文字 -1.8（文字全挂等比父 root，避开非等比拉伸坑）。
    /// 布局参数：UIPanelConfig（ADR-024，Inspector 可调）——每次 Show 重取
    /// （Play 中改资产 → 下次开面板即生效）；空配置回落内置缺省。
    /// TMP 一律在激活态创建（Awake 只在激活对象跑——03 §2 已档坑）。
    /// 面板打开期间 GameManager 暂停（Tick/输入由 IsOpen 门禁）。
    /// </summary>
    public class DeckPanel : MonoBehaviour
    {
        private const int DeckSize = BackpackSystem.DeckSize;
        private const int SlotClickBase = 100;
        private const int PagePrevClick = -2;   // 分页：上一页（DeckCard 上报索引）
        private const int PageNextClick = -3;   // 分页：下一页
        private const int InkPlusClick = -4;    // F1.3：初始墨「+」（与 handbar 的 -4 分属两套上报通道）
        private const int BagRowsPerPage = 2;   // 05 §5 阶梯：6 列×2 行 = 12 张/页

        private static readonly Color InkColor = HexUtil.ToColor("#26231F");       // 浓墨
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E");      // 朱砂
        private static readonly Color BlueInk = HexUtil.ToColor("#2B4C7E");        // 靛青
        private static readonly Color CardPaper = new Color(0.93f, 0.89f, 0.80f, 0.97f);
        private static readonly Color DimBg = new Color(0.15f, 0.13f, 0.10f, 0.80f);
        private static readonly Color ShovelBrown = HexUtil.ToColor("#6B4F35");    // 铲：褐
        private static readonly Color ZoneLabelColor = new Color(0.72f, 0.67f, 0.58f, 0.95f);
        private static readonly Color EmptySlotBg = new Color(0.32f, 0.29f, 0.24f, 0.85f);
        private static readonly Color EmptySlotEdge = new Color(0.55f, 0.50f, 0.42f, 0.9f);
        private static readonly Color ConfirmDisabled = new Color(0.62f, 0.58f, 0.52f, 0.9f);

        private TMP_FontAsset font;
        private Sprite whiteSprite;
        private UIPanelConfig cfg;       // null = 内置缺省
        private GameObject root;
        private Canvas hudCanvas;        // 屏幕空间 HUD：面板打开期间整体收起（Overlay 压不住）

        // 常驻骨架（Init 搭一次；位置/字号由 ApplyLayout 从 cfg 重取）
        private GameObject confirmGo;
        private GameObject[] slotGos;
        private SpriteRenderer[] slotBgs;
        private SpriteRenderer[] slotFrames;
        private TextMeshPro[] slotLabels;
        private TextMeshPro titleLabel;
        private TextMeshPro zoneDeckLabel;
        private TextMeshPro zoneBagLabel;
        private TextMeshPro sumLabel;
        private SpriteRenderer confirmBg;
        private TextMeshPro confirmLabel;
        private Transform bagGrid;        // 背包卡区（每次 Show 重建）

        // 分页（F4.2b）：常驻骨架（Init 一次）+ 每次 Show 重算页数
        private GameObject pagePrevGo;
        private GameObject pageNextGo;
        private SpriteRenderer pagePrevBg;
        private SpriteRenderer pageNextBg;
        private TextMeshPro pagePrevLabel;
        private TextMeshPro pageNextLabel;
        private int bagPage;              // 当前页（0 起）
        private int bagPageCount = 1;     // 总页数（Show 按卡数算；1 = 单页不出按钮）

        // F1.3（ADR-053）：左上角「初始墨 N」+「+」——数据源与升级动作由 GameManager 注入
        private TextMeshPro inkLabel;
        private GameObject inkPlusGo;
        private SpriteRenderer inkPlusBg;
        private TextMeshPro inkPlusLabel;
        private Func<int> inkProvider;      // 当前初始墨
        private Func<int> inkStepProvider;  // 当前步数（0-6）
        private Func<bool> inkUpgrade;      // 升级一步（成功 = true；由 GameManager 写档）
        private Func<int> inkPriceProvider;  // v4：下一步要花多少金币（商店同价）

        // 每次 Show 的数据
        private readonly List<string> cards = new List<string>();
        private readonly List<bool> selected = new List<bool>();
        private readonly List<int> pickPages = new List<int>(); // 每张背包卡所在页（与 cards 同下标）
        private readonly List<GameObject> pickGos = new List<GameObject>();
        private TowerPoolDef pool;
        private Action<List<string>> onConfirm;

        public bool IsOpen { get; private set; }

        // ============ 布局取值（cfg 为空回落内置缺省；下限防呆） ============

        private float CCardSize => Mathf.Max(0.3f, cfg != null ? cfg.deckCardSize : 1.3f);
        private float CSlotY => cfg != null ? cfg.deckSlotY : 2.6f;
        private float CSlotSpacing => Mathf.Max(0.4f, cfg != null ? cfg.deckSlotSpacing : 1.55f);
        private int CBagColumns => Mathf.Max(1, cfg != null ? cfg.deckBagColumns : 6);
        private float CBagRowY => cfg != null ? cfg.deckBagRowY : 0.55f;
        private float CBagSpacingX => Mathf.Max(0.4f, cfg != null ? cfg.deckBagSpacingX : 1.9f);
        private float CBagSpacingY => Mathf.Max(0.4f, cfg != null ? cfg.deckBagSpacingY : 1.7f);
        private float CTitleY => cfg != null ? cfg.deckTitleY : 4.5f;
        private float CTitleFontSize => Mathf.Max(1f, cfg != null ? cfg.deckTitleFontSize : 10f);
        private float CZoneDeckY => cfg != null ? cfg.deckZoneDeckY : 3.55f;
        private float CZoneBagY => cfg != null ? cfg.deckZoneBagY : 1.55f;
        private float CZoneFontSize => Mathf.Max(1f, cfg != null ? cfg.deckZoneFontSize : 6.5f);
        private float CCharFontSize => Mathf.Max(1f, cfg != null ? cfg.deckCharFontSize : 8f);
        private float CPriceFontSize => Mathf.Max(1f, cfg != null ? cfg.deckPriceFontSize : 3.5f);
        private float CSumY => cfg != null ? cfg.deckSumY : -2.55f;
        private float CSumFontSize => Mathf.Max(1f, cfg != null ? cfg.deckSumFontSize : 8f);
        private float CConfirmY => cfg != null ? cfg.deckConfirmY : -3.65f;
        private float CConfirmW => Mathf.Max(0.5f, cfg != null ? cfg.deckConfirmW : 4.8f);
        private float CConfirmH => Mathf.Max(0.3f, cfg != null ? cfg.deckConfirmH : 1.6f);
        private float CConfirmFontSize => Mathf.Max(1f, cfg != null ? cfg.deckConfirmFontSize : 12f);

        // —— 分页（F4.2b：按钮落在背包网格左右两侧、竖向居中于两行之间）——
        private float CPageArrowX => Mathf.Max(0.5f, cfg != null ? cfg.deckPageArrowX : 6.0f);
        private float CPageArrowY => cfg != null ? cfg.deckPageArrowY : -0.25f;
        private float CPageArrowW => Mathf.Max(0.5f, cfg != null ? cfg.deckPageArrowW : 2.4f);
        private float CPageArrowH => Mathf.Max(0.3f, cfg != null ? cfg.deckPageArrowH : 1.0f);
        private float CPageFontSize => Mathf.Max(1f, cfg != null ? cfg.deckPageFontSize : 4.2f);

        // —— 初始墨（F1.3）——
        private float CInkLabelX => cfg != null ? cfg.deckInkLabelX : -7.1f;
        private float CInkLabelY => cfg != null ? cfg.deckInkLabelY : 4.5f;
        private float CInkFontSize => Mathf.Max(1f, cfg != null ? cfg.deckInkFontSize : 5.5f);
        private float CInkPlusDX => cfg != null ? cfg.deckInkPlusDX : 2.5f;
        private float CInkPlusSize => Mathf.Max(0.3f, cfg != null ? cfg.deckInkPlusSize : 0.95f);

        /// <summary>场景创建时调用一次：搭常驻骨架（位置/字号默认值，随后 ApplyLayout 归位）。</summary>
        public void Init(TMP_FontAsset fontAsset, Sprite white, UIPanelConfig config, Canvas hud = null)
        {
            font = fontAsset;
            whiteSprite = white;
            cfg = config;
            hudCanvas = hud;

            root = new GameObject("Root");
            root.transform.SetParent(transform, false);
            // 世界坐标 HUD（万能铲 / 菜单 / ×1 在 z≈-1.6）会盖在面板上 → 整块前移 1.2
            root.transform.localPosition = new Vector3(0f, 0f, -1.2f);
            // 全程激活态搭骨架（TMP Awake 坑），最后才隐藏

            // 底幕：暗色盖全场 + 大碰撞体挡住面板后面的战场点击
            var bgGo = new GameObject("Dim", typeof(SpriteRenderer), typeof(BoxCollider2D));
            bgGo.transform.SetParent(root.transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 0.3f, -1.55f);
            bgGo.transform.localScale = new Vector3(30f, 15f, 1f);
            var dim = bgGo.GetComponent<SpriteRenderer>();
            dim.sprite = white;
            dim.color = DimBg;
            var col = bgGo.GetComponent<BoxCollider2D>();
            col.size = Vector2.one * 1.01f; // localScale 已放大，1.01 即全屏

            titleLabel = MakeLabel(root.transform, "Title", Vector3.zero, CTitleFontSize);
            titleLabel.text = "背包 · 构筑";
            titleLabel.color = CardPaper;

            // —— 初始墨（F1.3，ADR-053）：左上角常驻「初始墨 N」+「+」——
            inkLabel = MakeLabel(root.transform, "InkLabel", Vector3.zero, CInkFontSize);
            inkLabel.color = CardPaper;
            inkLabel.text = "初始墨 60";

            inkPlusGo = new GameObject("InkPlus", typeof(SpriteRenderer), typeof(BoxCollider2D));
            inkPlusGo.transform.SetParent(root.transform, false);
            inkPlusBg = inkPlusGo.GetComponent<SpriteRenderer>();
            inkPlusBg.sprite = white;
            var inkCol = inkPlusGo.GetComponent<BoxCollider2D>();
            inkCol.size = Vector2.one * 1.02f;
            inkPlusGo.AddComponent<DeckCard>().Init(InkPlusClick);
            inkPlusLabel = MakeLabel(root.transform, "InkPlusLabel", Vector3.zero, CInkFontSize);
            inkPlusLabel.text = "＋";
            inkPlusLabel.color = InkColor;

            // —— 构筑区（上）：5 槽位 ——
            zoneDeckLabel = MakeLabel(root.transform, "ZoneDeck", Vector3.zero, CZoneFontSize);
            zoneDeckLabel.text = "构筑（抽卡池）· 点卡移出";
            zoneDeckLabel.color = ZoneLabelColor;

            slotGos = new GameObject[DeckSize];
            slotBgs = new SpriteRenderer[DeckSize];
            slotFrames = new SpriteRenderer[DeckSize];
            slotLabels = new TextMeshPro[DeckSize];
            for (int k = 0; k < DeckSize; k++)
            {
                var go = new GameObject("Slot" + k, typeof(SpriteRenderer), typeof(BoxCollider2D));
                go.transform.SetParent(root.transform, false);
                slotGos[k] = go;
                slotBgs[k] = go.GetComponent<SpriteRenderer>();
                slotBgs[k].sprite = white;
                var sCol = go.GetComponent<BoxCollider2D>();
                sCol.size = Vector2.one * 1.02f;
                go.AddComponent<DeckCard>().Init(SlotClickBase + k);

                var frameGo = new GameObject("Frame", typeof(SpriteRenderer));
                frameGo.transform.SetParent(go.transform, false);
                frameGo.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                frameGo.transform.localScale = new Vector3(1.08f, 1.08f, 1f);
                slotFrames[k] = frameGo.GetComponent<SpriteRenderer>();
                slotFrames[k].sprite = white;

                slotLabels[k] = MakeLabel(go.transform, "Char", new Vector3(0f, 0f, -0.15f), CCharFontSize);
            }

            // —— 背包区（中）：卡网格（Show 时重建） ——
            zoneBagLabel = MakeLabel(root.transform, "ZoneBag", Vector3.zero, CZoneFontSize);
            zoneBagLabel.text = "背包 · 点卡加入";
            zoneBagLabel.color = ZoneLabelColor;

            var gridGo = new GameObject("BagGrid");
            gridGo.transform.SetParent(root.transform, false);
            gridGo.transform.localPosition = new Vector3(0f, 0f, -1.65f); // 卡层 z（不然落在暗幕后）
            bagGrid = gridGo.transform;

            // —— 分页（F4.2b）：左右两侧「上一页 / 下一页」（单页时隐藏）——
            // 文字挂 root 等比（按钮非等比缩放，文字挂按钮下会被横向拉宽——出阵钮同款坑）
            pagePrevGo = new GameObject("PagePrev", typeof(SpriteRenderer), typeof(BoxCollider2D));
            pagePrevGo.transform.SetParent(root.transform, false);
            pagePrevBg = pagePrevGo.GetComponent<SpriteRenderer>();
            pagePrevBg.sprite = white;
            var ppCol = pagePrevGo.GetComponent<BoxCollider2D>();
            ppCol.size = Vector2.one * 1.02f;
            pagePrevGo.AddComponent<DeckCard>().Init(PagePrevClick);
            pagePrevLabel = MakeLabel(root.transform, "PagePrevLabel", Vector3.zero, CPageFontSize);
            pagePrevLabel.text = "上一页";

            pageNextGo = new GameObject("PageNext", typeof(SpriteRenderer), typeof(BoxCollider2D));
            pageNextGo.transform.SetParent(root.transform, false);
            pageNextBg = pageNextGo.GetComponent<SpriteRenderer>();
            pageNextBg.sprite = white;
            var pnCol = pageNextGo.GetComponent<BoxCollider2D>();
            pnCol.size = Vector2.one * 1.02f;
            pageNextGo.AddComponent<DeckCard>().Init(PageNextClick);
            pageNextLabel = MakeLabel(root.transform, "PageNextLabel", Vector3.zero, CPageFontSize);
            pageNextLabel.text = "下一页";

            // —— 底部：Σ + 出阵 ——
            sumLabel = MakeLabel(root.transform, "Sum", Vector3.zero, CSumFontSize);
            sumLabel.color = BlueInk;

            confirmGo = new GameObject("Confirm", typeof(SpriteRenderer), typeof(BoxCollider2D));
            confirmGo.transform.SetParent(root.transform, false);
            confirmBg = confirmGo.GetComponent<SpriteRenderer>();
            confirmBg.sprite = white;
            confirmBg.color = Vermilion;
            var cCol = confirmGo.GetComponent<BoxCollider2D>();
            cCol.size = Vector2.one * 1.02f;
            confirmGo.AddComponent<DeckCard>().Init(-1);
            // 主文字挂 root（等比）：按钮非等比缩放，文字挂按钮下会被横向拉伸（HandBar 同款坑）
            confirmLabel = MakeLabel(root.transform, "ConfirmLabel", Vector3.zero, CConfirmFontSize);
            confirmLabel.text = "开 始";
            confirmLabel.color = CardPaper;

            ApplyLayout();
            root.SetActive(false); // 骨架建完才隐藏
        }

        /// <summary>
        /// F1.3（ADR-053）：注入初始墨数据源与升级动作（存档由 GameManager 持有）。
        /// 升级动作返回 true = 已写档 → 面板原地刷新（面板打开 = 全局暂停，本链路自成闭环）。
        /// 正式版换激励视频只改注入的动作，UI/存档结构不动。
        /// </summary>
        public void BindInk(Func<int> ink, Func<int> steps, Func<bool> upgrade, Func<int> price = null)
        {
            inkProvider = ink;
            inkStepProvider = steps;
            inkUpgrade = upgrade;
            inkPriceProvider = price;
        }

        /// <summary>常驻骨架归位（位置/尺寸/字号全从 cfg 重取）——Init 与每次 Show 都跑。</summary>
        private void ApplyLayout()
        {
            if (root == null) return;

            titleLabel.transform.localPosition = new Vector3(0f, CTitleY, -1.8f);
            titleLabel.fontSize = CTitleFontSize;

            // 初始墨（F1.3）：标签居中于 CInkLabelX，加号在其右侧固定偏移
            inkLabel.transform.localPosition = new Vector3(CInkLabelX, CInkLabelY, -1.8f);
            inkLabel.fontSize = CInkFontSize;
            inkPlusGo.transform.localPosition = new Vector3(CInkLabelX + CInkPlusDX, CInkLabelY, -1.65f);
            inkPlusGo.transform.localScale = new Vector3(CInkPlusSize, CInkPlusSize, 1f);
            inkPlusLabel.transform.localPosition = new Vector3(CInkLabelX + CInkPlusDX, CInkLabelY, -1.8f);
            inkPlusLabel.fontSize = CInkFontSize;
            zoneDeckLabel.transform.localPosition = new Vector3(0f, CZoneDeckY, -1.8f);
            zoneDeckLabel.fontSize = CZoneFontSize;
            zoneBagLabel.transform.localPosition = new Vector3(0f, CZoneBagY, -1.8f);
            zoneBagLabel.fontSize = CZoneFontSize;
            sumLabel.transform.localPosition = new Vector3(0f, CSumY, -1.8f);
            sumLabel.fontSize = CSumFontSize;

            for (int k = 0; k < DeckSize; k++)
            {
                float x = (k - (DeckSize - 1) * 0.5f) * CSlotSpacing;
                slotGos[k].transform.localPosition = new Vector3(x, CSlotY, -1.65f);
                slotGos[k].transform.localScale = new Vector3(CCardSize, CCardSize, 1f);
                slotLabels[k].fontSize = CCharFontSize;
            }

            confirmGo.transform.localPosition = new Vector3(0f, CConfirmY, -1.65f);
            confirmGo.transform.localScale = new Vector3(CConfirmW, CConfirmH, 1f);
            confirmLabel.transform.localPosition = new Vector3(0f, CConfirmY, -1.8f);
            confirmLabel.fontSize = CConfirmFontSize;

            // 分页按钮（F4.2b）：背包区标签行两侧
            pagePrevGo.transform.localPosition = new Vector3(-CPageArrowX, CPageArrowY, -1.65f);
            pagePrevGo.transform.localScale = new Vector3(CPageArrowW, CPageArrowH, 1f);
            pagePrevLabel.transform.localPosition = new Vector3(-CPageArrowX, CPageArrowY, -1.8f);
            pagePrevLabel.fontSize = CPageFontSize;
            pageNextGo.transform.localPosition = new Vector3(CPageArrowX, CPageArrowY, -1.65f);
            pageNextGo.transform.localScale = new Vector3(CPageArrowW, CPageArrowH, 1f);
            pageNextLabel.transform.localPosition = new Vector3(CPageArrowX, CPageArrowY, -1.8f);
            pageNextLabel.fontSize = CPageFontSize;
        }

        /// <summary>
        /// 打开面板。cards = 全部已拥有卡（本关池顺序在前，其余按解锁顺序，用户 2026-10-26 背包全局通用）；
        /// currentDeck = 当前构筑（预选入槽）。
        /// onConfirm 在「出阵」有效点击时回传所选列表（顺序同 cards）。
        /// </summary>
        public void Show(List<string> cardsInPool, List<string> currentDeck,
            TowerPoolDef towerPool, Action<List<string>> confirmCallback)
        {
            cards.Clear();
            selected.Clear();
            pickPages.Clear();
            pickGos.Clear();
            pool = towerPool;
            onConfirm = confirmCallback;

            if (hudCanvas != null) hudCanvas.gameObject.SetActive(false); // 模态：HUD 让位
            root.SetActive(true); // 先激活再建卡排（TMP Awake 坑）
            IsOpen = true;
            SfxPlayer.Menu(); // 用户 2026-10-26：面板打开音
            ApplyLayout(); // 每次开面板重取布局（Play 中改资产即时生效）

            // 重建背包网格
            for (int i = bagGrid.childCount - 1; i >= 0; i--)
                Destroy(bagGrid.GetChild(i).gameObject);

            int n = cardsInPool != null ? cardsInPool.Count : 0;
            int cols = CBagColumns;
            // F4.2b 分页（05 §5 阶梯）：每页 BagRowsPerPage 行——行数约束交分页接管，
            // 旧「加密列数压 ≤3 行」撤销（6 列 13+ 卡第 3 行 y≈-2.8 撞 Σ 行 -2.55）。
            // 每页容量 = cols × 2；ch1（≤8 卡）/ch2（11 卡）在 6 列下 ≤2 行 = 单页零变化。
            bagPageCount = Mathf.Max(1, (n + cols * BagRowsPerPage - 1) / (cols * BagRowsPerPage));
            bagPage = 0;
            for (int i = 0; i < n; i++)
            {
                var c = cardsInPool[i];
                cards.Add(c);
                selected.Add(currentDeck != null && currentDeck.Contains(c));

                int row = i / cols;
                int rowLen = Mathf.Min(cols, n - row * cols);
                int colInRow = i % cols;
                int page = row / BagRowsPerPage;          // 第 3 行起 = 第 2 页
                int rowInPage = row % BagRowsPerPage;     // 页内行（每页从顶行起排）
                float x = (colInRow - (rowLen - 1) * 0.5f) * CBagSpacingX;
                float y = CBagRowY - rowInPage * CBagSpacingY;
                pickPages.Add(page);

                var go = new GameObject("Pick" + c, typeof(SpriteRenderer), typeof(BoxCollider2D));
                go.transform.SetParent(bagGrid, false);
                go.transform.localPosition = new Vector3(x, y, 0f);
                go.transform.localScale = new Vector3(CCardSize, CCardSize, 1f);
                var bg = go.GetComponent<SpriteRenderer>();
                bg.sprite = whiteSprite;
                bg.color = c == "铲" ? ShovelBrown * 0.25f + CardPaper * 0.75f : CardPaper;
                var col = go.GetComponent<BoxCollider2D>();
                col.size = Vector2.one * 1.02f;
                go.AddComponent<DeckCard>().Init(i);
                pickGos.Add(go);

                var label = MakeLabel(go.transform, "Char", new Vector3(0f, 0.1f, -0.15f), CCharFontSize);
                label.text = c;
                label.color = InkColor;
                var def = pool != null ? pool.Find(c) : null;
                var price = MakeLabel(go.transform, "Price", new Vector3(0f, -0.34f, -0.15f), CPriceFontSize);
                price.text = def != null ? "价 " + def.value : "";
                price.color = BlueInk;
            }

            Refresh();
        }

        public void Hide()
        {
            IsOpen = false;
            if (root != null) root.SetActive(false);
            if (hudCanvas != null) hudCanvas.gameObject.SetActive(true);
        }

        /// <summary>
        /// 点击分发：-1 = 出阵（选满才放行）；100+k = 构筑槽位 k（移出第 k 张）；
        /// 0..n-1 = 背包卡（入构筑，满 5 拒绝并提示）。
        /// </summary>
        public void OnCardClicked(int i)
        {
            if (!IsOpen) return;

            // F1.3（ADR-053）初始墨「+」：封顶即无操作（按钮已置灰）——须先于「i < 0 = 出阵」分支
            if (i == InkPlusClick)
            {
                if (inkStepProvider != null && inkStepProvider() >= SaveSystem.InkCapMaxSteps) return;
                if (inkUpgrade != null && inkUpgrade()) { RefreshInk(); Refresh(); }
                return;
            }

            // 分页（F4.2b）：-2 上一页 / -3 下一页（须先于 -1 出阵分支拦下）
            if (i == PagePrevClick)
            {
                if (bagPage > 0) { bagPage--; Refresh(); }
                return;
            }
            if (i == PageNextClick)
            {
                if (bagPage < bagPageCount - 1) { bagPage++; Refresh(); }
                return;
            }

            if (i < 0)
            {
                if (CountSelected() != RequiredCount()) { SfxPlayer.ClickInvalid(); return; } // 未选满：出阵无效
                var chosen = new List<string>();
                for (int k = 0; k < cards.Count; k++)
                    if (selected[k]) chosen.Add(cards[k]);
                SfxPlayer.Confirm(); // 出阵确认（ADR-083）
                Hide();
                onConfirm?.Invoke(chosen);
                return;
            }

            if (i >= SlotClickBase)
            {
                int idx = IndexOfSlot(i - SlotClickBase);
                if (idx >= 0) selected[idx] = false;
                Refresh();
                return;
            }

            if (i >= cards.Count) return;
            if (selected[i]) return; // 已在构筑（背包区此时是隐藏态，正常到不了）

            if (CountSelected() >= DeckSize)
            {
                // 满 5：挂在合计行提示，下一次有效操作覆盖
                if (sumLabel != null)
                {
                    sumLabel.text = "构筑已满";
                    sumLabel.color = Vermilion;
                }
                SfxPlayer.ClickInvalid(); // 构筑已满（ADR-083）
                return;
            }
            selected[i] = true;
            Refresh();
        }

        private int CountSelected()
        {
            int c = 0;
            foreach (var s in selected) if (s) c++;
            return c;
        }

        private int RequiredCount() => Mathf.Min(DeckSize, cards.Count);

        /// <summary>构筑槽位 k 当前对应的背包卡下标（无 = -1）。</summary>
        private int IndexOfSlot(int k)
        {
            if (k < 0 || k >= DeckSize) return -1;
            int seen = 0;
            for (int c = 0; c < cards.Count; c++)
            {
                if (!selected[c]) continue;
                if (seen == k) return c;
                seen++;
            }
            return -1;
        }

        private void Refresh()
        {
            RefreshInk(); // F1.3：初始墨标签 + 加号亮灰
            if (bagPage >= bagPageCount) bagPage = bagPageCount - 1;
            // 当前页被选空（尾页常见：13 卡第 2 页仅 1 张）→ 回退到有卡的页（防空页观感）
            while (bagPage > 0 && !PageHasVisibleCards(bagPage)) bagPage--;
            // 首页本身就空（构筑预选吃满首页）→ 前挪到第一个有卡的页
            while (bagPage < bagPageCount - 1 && !PageHasVisibleCards(bagPage)) bagPage++;

            // 背包区：已入构筑的卡隐藏（两区不重影——构筑里是什么，背包区就不重复出现）；
            // 分页（F4.2b）：非当前页的卡一并隐藏
            for (int i = 0; i < pickGos.Count; i++)
                pickGos[i].SetActive(!selected[i] && pickPages[i] == bagPage);
            RefreshPageControls();

            // 构筑槽位
            for (int k = 0; k < DeckSize; k++)
            {
                int idx = IndexOfSlot(k);
                bool filled = idx >= 0;
                slotBgs[k].color = filled ? CardPaper : EmptySlotBg;
                slotFrames[k].color = filled ? Vermilion : EmptySlotEdge;
                slotLabels[k].text = filled ? cards[idx] : "＋";
                slotLabels[k].color = filled ? InkColor : EmptySlotEdge;
            }

            // Σ 一口价 + 出阵
            int count = CountSelected();
            int required = RequiredCount();
            int sum = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (!selected[i]) continue;
                var def = pool != null ? pool.Find(cards[i]) : null;
                if (def != null) sum += def.value;
            }
            sumLabel.text = "目前抽卡消耗 = " + sum + " 墨";
            sumLabel.color = count == required ? BlueInk : Vermilion;

            bool ok = count == required;
            confirmBg.color = ok ? Vermilion : ConfirmDisabled;
            if (confirmLabel != null)
                confirmLabel.color = ok ? CardPaper : new Color(0.85f, 0.83f, 0.78f, 0.9f);
        }

        /// <summary>该页是否还有未入构筑的卡（分页回退判据）。</summary>
        private bool PageHasVisibleCards(int page)
        {
            for (int i = 0; i < pickPages.Count; i++)
                if (pickPages[i] == page && !selected[i]) return true;
            return false;
        }

        /// <summary>F1.3（ADR-053）：初始墨标签 + 「+」亮灰（封顶 120 置灰，点了无操作）。</summary>
        private void RefreshInk()
        {
            if (inkLabel == null) return;
            int ink = inkProvider != null ? inkProvider() : SaveSystem.InitialInk(0);
            int steps = inkStepProvider != null ? inkStepProvider() : 0;
            bool max = steps >= SaveSystem.InkCapMaxSteps;
            int price = inkPriceProvider != null ? inkPriceProvider() : 0;
            inkLabel.text = max
                ? "初始墨 " + ink + "（已满）"
                : "初始墨 " + ink + " · " + (price <= 0 ? "免费" : price + "金");
            inkPlusBg.color = max ? ConfirmDisabled : CardPaper;
            inkPlusLabel.color = max ? ZoneLabelColor : InkColor;
        }

        /// <summary>分页控件刷新（F4.2b）：单页隐藏按钮；多页亮页码 + 边界置灰。</summary>
        private void RefreshPageControls()
        {
            bool multi = bagPageCount > 1;
            pagePrevGo.SetActive(multi);
            pageNextGo.SetActive(multi);
            pagePrevLabel.gameObject.SetActive(multi);
            pageNextLabel.gameObject.SetActive(multi);

            if (!multi)
            {
                zoneBagLabel.text = "背包 · 点卡加入";
                return;
            }

            bool canPrev = bagPage > 0;
            bool canNext = bagPage < bagPageCount - 1;
            pagePrevBg.color = canPrev ? CardPaper : ConfirmDisabled;
            pagePrevLabel.color = canPrev ? InkColor : ZoneLabelColor;
            pageNextBg.color = canNext ? CardPaper : ConfirmDisabled;
            pageNextLabel.color = canNext ? InkColor : ZoneLabelColor;
            zoneBagLabel.text = "背包 · 点卡加入";
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
            t.enableWordWrapping = false; // 长文案不折行（HandBar 同款）
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(3f, 1.2f);
            return t;
        }
    }
}
