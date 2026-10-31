using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 界面布局参数（v3.0-D3，ADR-024）：把构筑面板/手牌条的位置、尺寸、字号
    /// 外露成 SO——用户在 Inspector 里直接调界面，不用改代码（用户工作模式：
    /// Unity 内测试调参）。
    /// 挂接：ProjectSetup 自愈生成资产 + 注入 GameManager.panelConfig；
    /// 为空时组件用内置缺省（= 本处字段初始值），不接也能跑。
    /// 生效时机：构筑面板每次打开都重取布局（Play 中改资产 → 下次开面板即生效）；
    /// 手牌条只在进 Play 时读一次（改完需重进 Play）。
    /// ⚠️ 文件名铁律：类名与文件名一致。
    /// </summary>
    [CreateAssetMenu(menuName = "WordWars/界面参数", fileName = "UIPanelConfig")]
    public class UIPanelConfig : ScriptableObject
    {
        [Header("构筑面板 · 卡与槽位")]
        [Tooltip("卡/槽位边长（世界单位，1 格 = 1）。F4.5 两行方案缩卡：11 选 5 下 6 列×2 行不撞 Σ 行")]
        public float deckCardSize = 1.15f;
        [Tooltip("构筑槽位行高度（0 = 战场中心，视野 ±5.4）")]
        public float deckSlotY = 2.6f;
        [Tooltip("构筑槽位水平间距")]
        public float deckSlotSpacing = 1.55f;
        [Tooltip("背包网格每行卡数。F4.5：6 列装 11 卡 = 两行（旧 4 列会挤第三行撞 Σ）")]
        public int deckBagColumns = 6;
        [Tooltip("背包第一行高度（两行方案 0.6：首行顶 1.175 不压背包区标签（底 ≈1.225）；行 2 落 -1.1、卡底 -1.675，距 Σ 顶 -2.15 留 0.475）")]
        public float deckBagRowY = 0.6f;
        [Tooltip("背包卡水平间距（6 列×1.45 = 宽 8.7，卡 1.15 → 卡间 0.3 裕）")]
        public float deckBagSpacingX = 1.45f;
        [Tooltip("背包卡行距")]
        public float deckBagSpacingY = 1.7f;

        [Header("构筑面板 · 文字")]
        [Tooltip("标题「背包 · 构筑」高度 / 字号")]
        public float deckTitleY = 4.5f;
        public float deckTitleFontSize = 10f;
        [Tooltip("「构筑」分区标签高度")]
        public float deckZoneDeckY = 3.55f;
        [Tooltip("「背包」分区标签高度")]
        public float deckZoneBagY = 1.55f;
        [Tooltip("两个分区标签字号")]
        public float deckZoneFontSize = 6.5f;
        [Tooltip("卡面大字字号（≈字号×0.1×卡边长 = 字高）")]
        public float deckCharFontSize = 8f;
        [Tooltip("卡面「价 x」小字字号")]
        public float deckPriceFontSize = 3.5f;
        [Tooltip("合计行高度 / 字号")]
        public float deckSumY = -2.55f;
        public float deckSumFontSize = 8f;

        [Header("构筑面板 · 初始墨（F1.3 / ADR-053：左上角「初始墨 N +」）")]
        [Tooltip("标签 X（负 = 左上；缺省 -7.1 与居中标题不撞）")]
        public float deckInkLabelX = -7.1f;
        [Tooltip("标签行高度（缺省同标题行 4.5）")]
        public float deckInkLabelY = 4.5f;
        [Tooltip("标签字号（「初始墨 60」≈ 3.1 宽）")]
        public float deckInkFontSize = 5.5f;
        [Tooltip("「+」按钮相对标签的 X 偏移（标签右缘外 0.3）")]
        public float deckInkPlusDX = 2.5f;
        [Tooltip("「+」按钮边长（点击区随之）")]
        public float deckInkPlusSize = 0.95f;

        [Header("构筑面板 · 分页（F4.2b：每页 6 列×2 行 = 12 张，第 3 行起进第 2 页）")]
        [Tooltip("「上一页/下一页」按钮距中心 X（对称；缺省 6.0 = 背包网格右缘 4.2 外，宽裕不压卡）")]
        public float deckPageArrowX = 6.0f;
        [Tooltip("分页按钮行高度（缺省 -0.25 = 两行卡竖向中缝，6 列网格扣掉两侧不出卡）")]
        public float deckPageArrowY = -0.25f;
        [Tooltip("分页按钮宽")]
        public float deckPageArrowW = 2.4f;
        [Tooltip("分页按钮高")]
        public float deckPageArrowH = 1.0f;
        [Tooltip("分页按钮文字字号")]
        public float deckPageFontSize = 4.2f;

        [Header("出阵按钮")]
        [Tooltip("按钮宽（点击区随之）")]
        public float deckConfirmW = 4.8f;
        [Tooltip("按钮高")]
        public float deckConfirmH = 1.6f;
        [Tooltip("按钮高度（0 = 战场中心）")]
        public float deckConfirmY = -3.65f;
        [Tooltip("按钮文字字号")]
        public float deckConfirmFontSize = 12f;

        [Header("手牌条（改完需重进 Play）")]
        [Tooltip("手牌条高度（0 = 战场中心）")]
        public float handBarY = -4.55f;
        [Tooltip("手牌卡边长")]
        public float handCardSize = 1.15f;
        [Tooltip("手牌卡水平间距")]
        public float handCardSpacing = 1.7f;
        [Tooltip("整条水平偏移（负 = 左移）")]
        public float handBarShiftX = -0.7f;
        [Tooltip("手牌卡字号")]
        public float handCardFontSize = 10f;
        [Tooltip("抽卡按钮 X")]
        public float handDrawX = 4.55f;
        [Tooltip("卖出按钮 X")]
        public float handSellX = 6.05f;
        [Tooltip("下一波按钮 X")]
        public float handNextX = 8.1f;
        [Tooltip("抽/卖按钮边长")]
        public float handButtonSize = 1.5f;
        [Tooltip("下一波按钮宽")]
        public float handNextW = 2.4f;
        [Tooltip("下一波按钮高")]
        public float handNextH = 1.4f;
        [Tooltip("下一波文字字号")]
        public float handNextFontSize = 7.5f;

        [Header("手牌条 · 红铲（F2 / ADR-054；改完需重进 Play）")]
        [Tooltip("红铲按钮 X（手牌区左缘 -4.675 之外；负 = 左）")]
        public float handShovelX = -6.3f;
        [Tooltip("红铲按钮宽")]
        public float handShovelW = 2.0f;
        [Tooltip("红铲按钮高")]
        public float handShovelH = 1.4f;
        [Tooltip("红铲按钮主字号（价签固定 4.5）")]
        public float handShovelFontSize = 6.5f;
    }
}
