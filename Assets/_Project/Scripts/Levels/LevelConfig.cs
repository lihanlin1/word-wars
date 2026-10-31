using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWars
{
    // CellState / CellLayout 已随 ADR-021 迁往 ChapterConfig.cs（章图归属章）

    /// <summary>一组同型敌人的生成计划（波内各组并行推进）。</summary>
    [Serializable]
    public class SpawnGroup
    {
        public string charId = "一";
        public int count = 5;
        [Tooltip("组内相邻出生间隔（秒）")]
        public double interval = 1.5;
        [Tooltip("F2.1（ch5 双面）：-1 = 按关卡 voidRatio 自动分面；0 = 实面；1 = 虚面。ch1-4 缺行 = -1 + 配比 0 → 全实面")]
        public int face = -1;
    }

    /// <summary>
    /// 潮位档（F2.1 / ADR-032 / 06 §10.2 波次档位制，schema 由程序定案——06 §10.6 授权）。
    /// 挂波次表：波开始时切换水线；ch1 波次缺省 Hold = 潮汐系统不激活（旧资产 YAML 缺行
    /// → 字段初始化器生效，ch1 观感/玩法零变化铁律）。
    /// 淹没 ⟺ 格子海拔 ≤ 水线：Ebb=全干 / Calm=淹潮间低带 / Rise=淹潮间全部 / Surge=+左低地。
    /// </summary>
    public enum TideLevel
    {
        Hold = -1, // 沿用当前水线（不改）
        Ebb = 0,   // 退：水线 0，全干
        Calm = 1,  // 平：水线 1
        Rise = 2,  // 涨：水线 2
        Surge = 3, // 大涨：水线 3（L4-L5 涨潮波追加左侧低地）
    }

    /// <summary>
    /// 出怪口（F1.3 / ADR-040，06 §11.1 同路双向）：波级选择——上口/下口/双口（组间交替）。
    /// ch1/ch2 单向章（bidirectional=false）此字段无效——旧资产 YAML 缺行 → 缺省 Both，零迁移。
    /// </summary>
    public enum SpawnPort
    {
        Both = 0,  // 双口：组序交替（组 0 下口、组 1 上口循环——「这波从哪边来」的灯网考校）
        Upper = 1, // 上口：路线末端出生（倒序半程）
        Lower = 2, // 下口：路线起点端出生（正序半程）
    }

    /// <summary>
    /// 一波：生成组列表 + 与上一波起始的间隔（秒，不等清场，波可重叠）。
    /// 数据源：design/numbers/波次草稿.csv。
    /// </summary>
    [Serializable]
    public class WaveLayout
    {
        public List<SpawnGroup> groups = new List<SpawnGroup>();
        [Tooltip("与上一波『起始』的间隔（秒）；首波由关卡 prepSeconds 决定")]
        public double startAfterPrev = 20;

        [Tooltip("潮位档（波开始时切换）：Hold=沿用；Ebb=退；Calm=平；Rise=涨；Surge=大涨。ch1 全 Hold")]
        public TideLevel tide = TideLevel.Hold;

        [Tooltip("出怪口（F1.3 同路双向章）：上口/下口/双口组间交替。单向章无效（缺省 Both）")]
        public SpawnPort spawnPort = SpawnPort.Both;
    }

    /// <summary>
    /// 关卡配置（v3.0 03 §4.7）：波次表/卡池/起始资源。格子布局已拆去 ChapterConfig
    /// （章内 5 关共用一张章图）；本 SO 只管关卡节奏与解锁。
    /// ⚠️ 文件名铁律：ScriptableObject 类与文件名一致（LevelConfig.cs）。
    /// </summary>
    [CreateAssetMenu(menuName = "WordWars/关卡", fileName = "Level")]
    public class LevelConfig : ScriptableObject
    {
        [Header("基础")]
        public int levelId = 1;
        public string title = "第一卷";
        // 废弃（F1.2 / ADR-053，2026-10-20）：初始墨改为玩家存档级（SaveSystem.InitialInk）——
        // 本字段保留仅为旧资产反序列化兼容（不再被任何运行时路径读取，ProjectSetup 也不再写）。
        public int startInk = 60;
        public int startLives = 5;
        [Tooltip("备战期（秒）：进场到第一波起始")]
        public double prepSeconds = 20;

        [Tooltip("家灯照明半径（F1.1，ch3 幽夜）：家 = 路正中格，半径逐关收缩 4.5→0.5（拐杖移除曲线）；0 = 无家灯（ch1/ch2 缺省零变化）")]
        public float homeLampRadius = 0f;

        [Header("火山（F1.2，ch4，06 §12.2——喷射引擎 F2.1 接线消费）")]
        [Tooltip("活跃度下限：每波岩浆颗数 N ∈ [下限, 上限]（波内随机）。四L1-五 = 1/2/2-3/3-4/4-6（黄底，06 §12.6 曲线）。0/0 = 无喷射——ch1-3 YAML 缺行零变化铁律")]
        public int eruptionMin = 0;
        [Tooltip("活跃度上限：与下限合成随机区间（下限 == 上限 = 固定颗数）。红闪预警/波后延迟/burst/红印等全局参数集中 GameParams 火山区")]
        public int eruptionMax = 0;

        [Header("章图（F4.3 / ADR-031：levels 平铺 12 关）")]
        [Tooltip("本关章图。空 = 沿用 GameManager.chapter 场景引用（ch1 旧资产零迁移）；二L1-二L5 指到 Chapter2.asset")]
        public ChapterConfig chapter;

        [Header("卡池（v3.0：铲为池内普通卡；用户 2026-10-26 起「背包全局通用」——本字段只决定展示/抽卡顺序，不再硬性限制）")]
        public List<string> poolChars = new List<string> { "刀", "矛", "弓" };

        [Header("通关奖励卡（入背包；阶段 D 背包系统消费，现阶段只存数据）")]
        public List<string> rewardCards = new List<string>();

        [Header("波次表")]
        public List<WaveLayout> waves = new List<WaveLayout>();

        [Header("通关结算（P5，2026-10-25）：仅首次通关 + 有奖励卡 时出现；文案可自行编辑")]
        [Tooltip("结算标题。空 = 默认「恭喜获得新塔防」（困难模式默认「恭喜解锁 6 阶」）")]
        public string clearTitle = "";
        [Tooltip("结算说明（多行）。空 = 不显示说明行")]
        [TextArea(2, 6)] public string clearDesc = "";

        [Header("日记（P6，2026-10-25）：章首关填；空 = 不进日记画面")]
        [Tooltip("日记正文，每条 = 一页（逐字显示；点击全显；再点翻页；右上「跳过」直接略过）。重打仍显示")]
        public List<string> diaryPages = new List<string>();

        [Header("第五章·双面（F2.1，docs/13 / ADR-059）")]
        [Tooltip("虚面出怪比例 0-1：组内按序号均匀分配（确定性——R 回滚同分法）。ch1-4 缺行 = 0 全实面")]
        [Range(0f, 1f)] public float voidRatio = 0f;
        [Tooltip("换面按钮开关：五L1 = false（教学关不出现）；五L2-L5 = true。ch1-4 缺行 = false")]
        public bool faceSwitch = false;
    }
}
