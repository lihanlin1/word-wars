using System.Collections.Generic;

namespace WordWars
{
    /// <summary>
    /// 背包 + 构筑（v3.0 03 §4.6，ADR-023）：卡种 = 塔字 charId。
    /// 背包 = 已拥有卡种集合；构筑 = 从中勾选 ≤5 张为当前抽卡池（DeckSize）。
    /// 入包时机 = 关卡开始（rewardCards 并集；「通关奖励」按解锁时机实现）。
    /// **用户 2026-10-26 终裁：背包全局通用**——解锁过的塔在任何关都能用（抽卡池 = 构筑本身，
    /// 不再按关卡 poolChars 过滤；关卡池只影响展示顺序，教学关 L0 例外）。
    /// 构筑未满 5 时新卡自动上位；L4 起关前有构筑面板（背包解锁后前几关也弹）。
    /// </summary>
    public class BackpackSystem
    {
        public const int DeckSize = 5;

        private readonly List<string> owned = new List<string>();
        private readonly List<string> deck = new List<string>();

        // 直露 List（非 IReadOnlyList）：调用方要用 List.Contains，免引 System.Linq
        public List<string> OwnedCards => owned;
        public List<string> Deck => deck;

        /// <summary>入包（幂等）；构筑未满自动上位。返回是否新入。</summary>
        public bool AddCard(string charId)
        {
            if (string.IsNullOrEmpty(charId) || owned.Contains(charId)) return false;
            owned.Add(charId);
            if (deck.Count < DeckSize && !deck.Contains(charId)) deck.Add(charId);
            return true;
        }

        /// <summary>整包设定构筑：须拥有、去重、≤5（超出的忽略）。</summary>
        public void SetDeck(IEnumerable<string> chosen)
        {
            deck.Clear();
            foreach (var c in chosen)
                if (owned.Contains(c) && !deck.Contains(c) && deck.Count < DeckSize)
                    deck.Add(c);
            // 防脏档：构筑空了（存档缺 deck 字段）→ 自动补前 5 张，避免空池卡死
            if (deck.Count == 0)
                foreach (var c in owned)
                {
                    if (deck.Count >= DeckSize) break;
                    deck.Add(c);
                }
        }

        /// <summary>本关有效抽卡池 = 构筑 ∩ 关卡池（按关卡池顺序返回，稳定）。</summary>
        public List<string> EffectivePool(List<string> poolChars)
        {
            var pool = new List<string>();
            if (poolChars == null) return pool;
            foreach (var c in poolChars)
                if (deck.Contains(c)) pool.Add(c);
            return pool;
        }

        /// <summary>
        /// 全局抽卡池（用户 2026-10-26 终裁「背包全局通用」）：抽卡池 = 构筑本身，
        /// **不再按关卡池过滤**——解锁过的塔在任何关都能用；第一次打某关时只受「已拥有」限制。
        /// preferredOrder 只决定显示/抽卡顺序（本关池里的卡排前面，其余按解锁顺序补）。
        /// </summary>
        public List<string> GlobalPool(List<string> preferredOrder)
        {
            var pool = new List<string>();
            if (preferredOrder != null)
                foreach (var c in preferredOrder)
                    if (deck.Contains(c) && !pool.Contains(c)) pool.Add(c);
            foreach (var c in deck)
                if (!pool.Contains(c)) pool.Add(c);
            return pool;
        }

        /// <summary>从存档恢复（先入包再落构筑——SetDeck 校验拥有性）。</summary>
        public void LoadFrom(SaveData s)
        {
            owned.Clear();
            deck.Clear();
            if (s == null || s.backpack == null || s.backpack.ownedCards == null) return;
            foreach (var c in s.backpack.ownedCards) AddCard(c);
            if (s.deck != null && s.deck.Count > 0) SetDeck(s.deck);
        }

        /// <summary>写回存档。</summary>
        public void WriteTo(SaveData s)
        {
            s.backpack.ownedCards = new List<string>(owned);
            s.deck = new List<string>(deck);
        }
    }
}
