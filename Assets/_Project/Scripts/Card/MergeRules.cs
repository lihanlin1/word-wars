namespace WordWars
{
    /// <summary>
    /// 升阶/卖出的纯规则层（03 §4.3，判定与文案分离）。
    /// v3.0（ADR-016）：词塔双字合成与喂养整体删除，合并心智只保留
    /// 「同字同级升阶」一条；GameManager 只做编排与浮字文案。
    /// </summary>
    public static class MergeRules
    {
        // —— 字位成本表（GDD §5.1）：单字塔 1/2/4/8/16（词塔行已随 v3.0 作废）——
        private static readonly int[] SingleUnits = { 1, 2, 4, 8, 16 };

        /// <summary>单字塔 1/2/4/8/16。</summary>
        public static int CharUnits(int level)
        {
            if (level < 1) level = 1;
            if (level > SingleUnits.Length) level = SingleUnits.Length;
            return SingleUnits[level - 1];
        }

        public static int CharUnits(TowerState t) => CharUnits(t.level);

        /// <summary>场上塔卖出回墨 = 卡价值 × 50% × 字位（v3.0，GDD §4）。</summary>
        public static int SellValue(TowerState t, double sellRate) =>
            (int)System.Math.Round(t.value * sellRate * CharUnits(t));

        /// <summary>手牌卡（恒 L1 = 1 字位）卖出回墨 = 卡价值 × 50%。</summary>
        public static int CardSellValue(int cardValue, double sellRate) =>
            (int)System.Math.Round(cardValue * sellRate);

        /// <summary>配对升阶条件（v2.2，v3.0 保留）：同字同级且未达上限。</summary>
        public static bool CanPairUpgrade(string aId, int aLevel, string bId, int bLevel, int maxLevel) =>
            aId == bId && aLevel == bLevel && aLevel < maxLevel;

        /// <summary>命中类型（v3.0：只剩升阶一轨）。</summary>
        public enum MergeKind
        {
            None,        // 无命中
            PairUpgrade, // 同字同级升阶（卡+塔 / 塔+塔）
        }

        /// <summary>拒绝原因（03 §4.3.1 v3.0 修订版真值表）。</summary>
        public enum RejectReason
        {
            None,
            HandLevel,     // 手牌只能配一级塔
            MaxLevel,      // 已达五级上限
            NoMerge,       // 异字：此二字无法合成（词塔已删）
            LevelMismatch, // 塔对塔同字不同级（走「无法合成」兜底）
        }

        public struct EvaluateResult
        {
            public MergeKind kind;
            public RejectReason reject;
            public bool Hit => kind != MergeKind.None;
        }

        /// <summary>源 = 手牌字卡（恒 L1）→ 目标 = 场上塔。</summary>
        public static EvaluateResult EvaluateCardOnTower(string cardChar, TowerState target, int maxLevel)
        {
            var r = default(EvaluateResult);
            if (target == null || string.IsNullOrEmpty(cardChar)) return r;

            if (target.charId == cardChar)
            {
                if (target.level > 1) { r.reject = RejectReason.HandLevel; return r; }
                if (target.level >= maxLevel) { r.reject = RejectReason.MaxLevel; return r; }
                r.kind = MergeKind.PairUpgrade;
                return r;
            }
            r.reject = RejectReason.NoMerge;
            return r;
        }

        /// <summary>源 = 场上塔（拿起）→ 目标 = 场上塔。</summary>
        public static EvaluateResult EvaluateTowerOnTower(TowerState source, TowerState target, int maxLevel)
        {
            var r = default(EvaluateResult);
            if (source == null || target == null || source == target) return r;

            if (source.charId == target.charId)
            {
                if (source.level != target.level) { r.reject = RejectReason.LevelMismatch; return r; }
                if (target.level >= maxLevel) { r.reject = RejectReason.MaxLevel; return r; }
                r.kind = MergeKind.PairUpgrade;
                return r;
            }
            r.reject = RejectReason.NoMerge;
            return r;
        }
    }
}
