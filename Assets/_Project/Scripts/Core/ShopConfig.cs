using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 局外商店价格（用户 2026-11-04，ADR-085）：全部黄底，Inspector 直接改。
    /// 缺省 0 = 免费（价格待用户填）；公式：第 n 次购买 = base + growth × n（n 从 0 起）。
    /// 新塔防 = 占位（暂不实现，只留开关）。
    /// ⚠️ 文件名铁律：ScriptableObject 类名与文件名一致。
    /// </summary>
    [CreateAssetMenu(menuName = "WordWars/商店配置", fileName = "ShopConfig")]
    public class ShopConfig : ScriptableObject
    {
        [Header("墨上限（初始墨 60 → 200，+10/步，共 14 步）")]
        [Tooltip("第 1 步价格（当前步数 0 时）")]
        public int inkStepPrice = 0;
        [Tooltip("每多买过一步，价格 +该值")]
        public int inkStepPriceGrowth = 0;

        [Header("天赋槽（默认 1，上限 7）")]
        [Tooltip("买第 2 个槽位的价格（当前槽位 1 时）")]
        public int slotPrice = 0;
        [Tooltip("每多一个槽位，价格 +该值")]
        public int slotPriceGrowth = 0;

        [Header("6 阶单塔解锁（全局共享，普通/困难都可用）")]
        public int tier6Price = 0;

        [Header("章节宝箱（用户 2026-11-07）")]
        [Tooltip("集齐 5 星（该章该模式 5 关全通）可领一次的金币数；0 = 待填")]
        public int chestGold = 0;

        [Header("新塔防（占位：暂不实现）")]
        public bool newTowerEnabled = false;

        /// <summary>当前已买 steps 步时，再买一步的价格。</summary>
        public int InkStepPrice(int steps) => Mathf.Max(0, inkStepPrice + inkStepPriceGrowth * Mathf.Max(0, steps));

        /// <summary>当前已有 slots 个槽位时，再买一个的价格。</summary>
        public int SlotPrice(int slots) => Mathf.Max(0, slotPrice + slotPriceGrowth * Mathf.Max(0, slots - 1));

        public int Tier6Price => Mathf.Max(0, tier6Price);
    }
}
