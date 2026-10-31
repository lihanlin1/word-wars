using System;
using System.Collections.Generic;

namespace WordWars
{
    /// <summary>
    /// 手牌 + 抽卡（纯数据，W3 平衡模拟器可直接复用）。
    /// v3.0（ADR-017）：池 = 关卡解锁的全部卡（L1-L2）或玩家构筑的 5 卡（L3 起，
    /// 构筑系统 W3 实装）；抽 = 从池内**均等权重**随机补满手牌，可重复。
    /// 抽卡花费 = **池总价值**（一口价，由 GameManager 按 Σvalue 计算后传入）。
    /// 铲是池内普通卡（价值 6，不再有独立权重）；卖卡回墨由 GameManager 结算。
    /// </summary>
    public class HandModel
    {
        /// <summary>铲的功能牌 ID（数值上与塔卡统一进池，放塔流程在 GameManager 拦截）。</summary>
        public const string ShovelId = "铲";

        /// <summary>
        /// F4.3 盆的功能牌 ID（同铲口径：放下即消耗、不可移动/不可卖——用户 2026-10-13 终裁；
        /// 仅灰烬格可放 → 烧前为塔防格的回绿框 SetGreen（任意塔含墨可建其上），
        /// 烧前为荒地/垦中的走 ClearAsh 除灰回原象、**开垦要求照旧**——用户 2026-10-18 终裁）。
        /// </summary>
        public const string PotId = "盆";

        public int HandSize { get; }
        public double DrawCost { get; }
        public int DrawCount { get; private set; }

        public List<string> Cards { get; } = new List<string>();

        /// <summary>手牌未满（不满即可抽，抽了就补满）。
        /// 手感优化（用户 2026-10-09）：抽卡改为**刷新**语义——满手也可抽（换牌：弃全手
        /// 未用牌按卖价自动回墨 + 均等重抽补满）。门只剩「池非空」（与 HasSource 等价，
        /// 保留两属性供各自语境调用）。HandBar 按钮态自动跟随。</summary>
        public bool CanDraw => HasSource;

        /// <summary>还有可抽来源（池非空）。</summary>
        public bool HasSource => pool.Count > 0;

        private readonly List<string> pool;
        private readonly Random rng;

        public HandModel(List<string> unlockedPool, int handSize, int drawCost, int seed)
        {
            pool = unlockedPool ?? new List<string>();
            HandSize = handSize;
            DrawCost = drawCost;
            rng = new Random(seed);
        }

        /// <summary>抽卡：随机补足手牌空位（墨由调用方扣）。满手/无来源返回 false。</summary>
        public bool TryDraw(out List<string> drawn)
        {
            drawn = null;
            if (!CanDraw || !HasSource) return false;

            DrawCount++;
            int n = HandSize - Cards.Count;
            drawn = new List<string>(n);
            for (int i = 0; i < n; i++)
                drawn.Add(pool[rng.Next(pool.Count)]); // 均等权重（含铲）
            Cards.AddRange(drawn);
            return true;
        }

        /// <summary>消耗指定手牌（放置/开垦/升阶/卖出时调用）。</summary>
        public string ConsumeAt(int index)
        {
            if (index < 0 || index >= Cards.Count) return null;
            var c = Cards[index];
            Cards.RemoveAt(index);
            return c;
        }

        /// <summary>
        /// 手感优化（用户 2026-10-09）：弃全手牌（刷新前置——旧牌已由调用方按卖价回墨）。
        /// 不动 DrawCount 统计（弃手 ≠ 重置抽卡计数；Clear 仍用于关卡重置）。
        /// </summary>
        public void DiscardHand() => Cards.Clear();

        public void Clear()
        {
            Cards.Clear();
            DrawCount = 0;
        }
    }
}
