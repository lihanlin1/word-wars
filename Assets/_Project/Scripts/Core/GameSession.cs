namespace WordWars
{
    /// <summary>
    /// 跨场景会话参数（主界面 / 选关 → Game 传参）。静态字段跨场景存活，
    /// 由选关/主界面写入、GameManager.Start 读取后即视为已消费。
    /// </summary>
    public static class GameSession
    {
        /// <summary>是否有主界面/选关写入的会话参数（false = 直接启动 Game 场景，走存档）。</summary>
        public static bool Active = false;

        /// <summary>目标模式：false = 简单，true = 困难。</summary>
        public static bool HardMode = false;

        /// <summary>指定进入的关索引（0 基）；-1 = 按存档/模式默认。</summary>
        public static int StartLevelIndex = -1;

        /// <summary>true = 从该关所在章的第一关开始（覆盖存档进度）。</summary>
        public static bool FromChapterFirst = false;

        /// <summary>true = 带存档快照进场（「继续」）；false = 全新进场（选关 / 从第一关）。</summary>
        public static bool ResumeSnapshot = false;

        public static void Reset()
        {
            Active = false;
            HardMode = false;
            StartLevelIndex = -1;
            FromChapterFirst = false;
            ResumeSnapshot = false;
        }
    }
}
