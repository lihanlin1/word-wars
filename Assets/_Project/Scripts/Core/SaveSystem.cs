using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WordWars
{
    // ============ 存档 v2 数据结构（03 §6 schema；JsonUtility 嵌套可序列化） ============

    [Serializable]
    public class BackpackData
    {
        public List<string> ownedCards = new List<string>();
    }

    [Serializable]
    public class SnapTowerData
    {
        public string charId = "";
        public int level = 1;
        public int x;
        public int y;
        // F2.1（ch5 双面）：塔的面归属 0=实/1=虚。旧档 YAML 缺行 = 0 → ch1-4 零迁移。
        public int face;
    }

    /// <summary>坐标项（F2.4 红铲造格：章内状态与关开始快照共用）。</summary>
    [Serializable]
    public class SnapCellData
    {
        public int x;
        public int y;
    }

    /// <summary>
    /// 玩家元进度（F1.1，ADR-053）：跨章永久继承的账号级字段（不属于任何一章）。
    /// inkCapSteps 缺行 = 0 = 初始墨 60 —— 旧档零迁移（不删档、不弹提示）。
    /// </summary>
    [Serializable]
    public class PlayerMetaData
    {
        public int inkCapSteps; // 0-6，每步 +10 墨（60 → 120）
    }

    [Serializable]
    public class SnapTileData
    {
        public int x;
        public int y;
        public int state; // CellState 序列化为 int（0 空/1 荒/2 开垦中——快照里不出现 2，见捕获逻辑）
        // F7.3 灰烬跨关（用户 2026-10-18 定案 A「原样累积」）：TileState 序列化为 int
        //（0 常态/1 灰烬/2 复绿）。旧档 JsonUtility 缺行 = 0 = 常态 → 零迁移、ch1-3 恒 0。
        public int tileState;
    }

    /// <summary>关开始快照（ADR-018）：塔列表 + 格子状态；进关回放 = 保留/回滚两用。</summary>
    [Serializable]
    public class LevelStartSnapshotData
    {
        public List<SnapTowerData> towers = new List<SnapTowerData>();
        public List<SnapTileData> tiles = new List<SnapTileData>();

        // F2.4（ADR-054）：红铲造格与价格计数一并入快照——R 回滚 = 回本关进关态
        //（本关买出来的格与次数一起退掉）。旧档缺字段 = 空列表 / 0，零迁移。
        public List<SnapCellData> extraCells = new List<SnapCellData>();
        public int redShovelBought;
    }

    [Serializable]
    public class ChapterStateData
    {
        public int chapter = 1;
        public int levelIndex;                                  // 最近进入的关（0 基）
        public LevelStartSnapshotData levelStartSnapshot = new LevelStartSnapshotData();
        // F2.1（ch4 火山，06 §12.2 / ADR-047）：喷射序列种子——进关生成入档（换关重生成），
        // 同关沿用（R 回滚/读档续玩重放同序列——06「防回滚刷好运」）。旧档 JsonUtility 缺行
        // = 初始值（0 / -1）→ 进关重生成，兼容零迁移。
        public int eruptionSeed;               // 0 = 未生成（哨兵——生成恒非零）
        public int eruptionSeedLevel = -1;     // 种子所属关索引（≠ 当前关 → 重生成）
        // F2.4（ADR-054）：红铲造格（章内跨关带、章界清空）+ 价格计数（当前价 = base + step × 次数）。
        // 旧档缺字段 = 空列表 / 0 → 零迁移。
        public List<SnapCellData> extraCells = new List<SnapCellData>();
        public int redShovelBought;
        // 家（用户 2026-10-23）：章内跨关累积、章界清零（升级次数 0-5；旧档缺字段 = 0 零迁移）
        public int homeLevel;
    }

    [Serializable]
    public class SettingsData
    {
        public bool sfxOn = true;
        public bool bgmOn = true;
        public float speed = 1f;
    }

    [Serializable]
    public class SaveData
    {
        public int saveVersion = 3;
        public string lastSavedUtc = "";
        public int maxLevelUnlocked = 1;
        public BackpackData backpack = new BackpackData();
        public List<string> deck = new List<string>();
        public ChapterStateData chapterState = new ChapterStateData(); // 简单模式进度（v2 沿用）
        public SettingsData settings = new SettingsData();
        // F1.1（ADR-053）：玩家元进度（初始墨升级步数）。旧档缺 playerMeta 行 = 缺省 0 = 60 墨。
        public PlayerMetaData playerMeta = new PlayerMetaData();

        /// <summary>图鉴（用户 2026-10-28）：已遇到过的敌字（进关生成即记；旧档缺行 = 空表）。</summary>
        public List<string> seenEnemies = new List<string>();

        // —— v3（用户 2026-10-24）：困难模式 + 6 阶解锁 ——
        /// <summary>困难模式进度（与简单分开记；旧档缺行 = 空进度）。</summary>
        public ChapterStateData hardChapterState = new ChapterStateData();
        /// <summary>6 阶已解锁的塔字（**全局共享**：困难模式解锁后普通模式也可用）。</summary>
        public List<string> tier6Unlocked = new List<string>();
        /// <summary>困难模式已通关的关索引（选关右上角 ⭐；用户 2026-10-24）。</summary>
        public List<int> hardClearedLevels = new List<int>();
        /// <summary>简单模式已通关的关索引（选关右上角 ⭐；用户 2026-10-25）。</summary>
        public List<int> easyClearedLevels = new List<int>();
        /// <summary>上次选择的模式（0=简单 1=困难；主界面/选关记住，缺省简单）。</summary>
        public int lastMode;

        /// <summary>按模式取进度状态（hard=true 取困难）。</summary>
        public ChapterStateData StateFor(bool hard) => hard ? hardChapterState : chapterState;

        public bool IsTier6Unlocked(string charId) =>
            tier6Unlocked != null && !string.IsNullOrEmpty(charId) && tier6Unlocked.Contains(charId);
    }

    /// <summary>
    /// 存档读写（03 §6）：persistentDataPath/save_v2.json。
    /// 原型不接 SDK、不做云档；失败静默（红字日志）绝不炸主流程。
    /// 测试重置 = 删该文件（Start 时会把路径打进 Console）。
    /// </summary>
    public static class SaveSystem
    {
        // ============ F1.1 初始墨元进度（ADR-053，2026-10-20 用户定案） ============
        // 初始墨不再由关卡设置（LevelConfig.startInk 废弃）：60 起，选卡界面「+」+10/步、
        // 封顶 120、永久继承（存档）。当前版本无成本升级（正式版预定看广告——原型期零 SDK）。
        public const int InkCapBase = 60;
        public const int InkCapStep = 10;
        public const int InkCapMaxSteps = 6;

        /// <summary>初始墨 = 60 + 10 × 步数（0-6 → 60-120）。实机与 headless 唯一同源口径。</summary>
        public static int InitialInk(int steps) =>
            InkCapBase + InkCapStep * Mathf.Clamp(steps, 0, InkCapMaxSteps);

        private static string Path =>
            System.IO.Path.Combine(Application.persistentDataPath, "save_v2.json");

        /// <summary>读档：无档/坏档/版本不符 → null（走新档流程）。v2 自动迁移到 v3。</summary>
        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
                if (data == null || (data.saveVersion != 2 && data.saveVersion != 3))
                {
                    Debug.LogWarning("[SaveSystem] 存档版本不符或为空，按新档处理：" + Path);
                    return null;
                }
                // v2 → v3 迁移（用户 2026-10-24）：补困难进度 / 6 阶解锁，版本升 3。
                if (data.saveVersion < 3)
                {
                    data.saveVersion = 3;
                    Debug.Log("[SaveSystem] 存档 v2 → v3 迁移（困难进度/6 阶解锁初始化）");
                }
                if (data.chapterState == null) data.chapterState = new ChapterStateData();
                if (data.hardChapterState == null) data.hardChapterState = new ChapterStateData();
                if (data.tier6Unlocked == null) data.tier6Unlocked = new List<string>();
                if (data.hardClearedLevels == null) data.hardClearedLevels = new List<int>();
                if (data.easyClearedLevels == null) data.easyClearedLevels = new List<int>();
                if (data.backpack == null) data.backpack = new BackpackData();
                if (data.playerMeta == null) data.playerMeta = new PlayerMetaData();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 读档失败（按新档处理）：" + e.Message);
                return null;
            }
        }

        public static void Write(SaveData data)
        {
            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 写档失败：" + e.Message);
            }
        }

        /// <summary>Console 打印存档路径（测试期删档重开用）。</summary>
        public static void LogPath() => Debug.Log("[Word Wars] 存档路径：" + Path);

        /// <summary>测试删档：删除存档文件（下次进场景 = 全新档）。</summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] 删档失败：" + e.Message);
            }
        }
    }
}
