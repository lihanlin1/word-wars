using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWars
{
    /// <summary>敌人数值（GDD §6 的可编辑形态；波次编排 W3 进关卡 SO）。</summary>
    [Serializable]
    public class EnemyStats
    {
        [Tooltip("敌字，如 一")]
        public string charId = "一";
        public double hp = 20;
        [Tooltip("速度（格/秒）")]
        public double speed = 1.0;
        [Tooltip("死亡掉墨")]
        public double inkDrop = 2;
        [Tooltip("漏过扣印")]
        public int livesCost = 1;
        [Tooltip("空中：免疫近战")]
        public bool flying;
        [Tooltip("Boss：大字 + 血条，漏过即败")]
        public bool isBoss;
        [Tooltip("线路：0=陆路，1=水路（ch2 鱼/虾/龟/鳄/蛟走水路；缺省 0——ch1 敌表零改动）")]
        public int lane;
        [Tooltip("F4.1（ADR-034）：水中速度乘数——水路航道段（过汇合点前）生效；1=不吃水（缺省，ch1 零改动）。鱼/虾/鳄 1.5、龟 1.6（0.5→0.8 绝对值换算）；蛟二阶段由 F5 运行时改写")]
        public double waterSpeedMult = 1.0;
        [Tooltip("F5.2（06 §10.4）：转阶段血线——血量比例 ≤ 该值触发两幕剧（蓄水→洪水→潮落）。0=无（缺省）；蛟=1/3。isBoss 不作门槛（ch1 酋长同标——防误触发）")]
        public double phase2HpRatio;

        [Tooltip("F2.2（06 §11.2）：潜行——雾中浅影（褪色幽灵影）、入光全色现形；蝠/猫（缺省 false，旧敌零迁移）")]
        public bool stealth;
        [Tooltip("F2.2 光-敌交互：光脆——光中所受伤害 × lightVulnerableMult（GameParams；蛾）")]
        public bool lightVulnerable;
        [Tooltip("F2.2 光-敌交互：光滞——光中速度 × lightHinderedMult（GameParams；蝠）")]
        public bool lightHindered;
        [Tooltip("F2.2 光-敌交互：夜行——雾中速度 × nocturnalFogMult（GameParams）、入光还原；狼（ch1 客串挂标：ch1 fog 关恒光照 → 零触发铁律）")]
        public bool nocturnal;
        [Tooltip("F5.1 免疫眩晕（枭——蛟免疫冰冻同款纪律：Boss 毕业考不许被硬控锁死；缺省 false 旧敌零迁移）")]
        public bool stunImmune;
        [Tooltip("F4.1/F5.1 免疫燃烧+岩浆（火蜥/岩龟/麟——burn 状态施加拦 + 红印 DoT/burst 拦；缺省 false 旧敌零迁移。F5.1 火蜥/岩龟已置 true，麟 F6）")]
        public bool burnImmune;
        [Tooltip("F5.2 燃烧易伤：被挂燃时燃烧伤害 × burnVulnerableMult（鳄「水怪怕火」火山图 ×2，06 §12.4；缺省 false 旧敌零迁移——ch1-3 无燃烧源故零变化）")]
        public bool burnVulnerable;
        [Tooltip("F5.1 红印提速：站/路过红印格（岩浆残留）时速度 × 该值（火蜥 1.3「灾害区原生」）；缺省 1 = 无（旧敌零迁移）")]
        public double lavaMarkSpeedMult = 1.0;
        [Tooltip("F6.1 践踏光环半径（格）：>0 = 该敌每帧把半径内**建造格**变灰烬（塔按免疫规则：火/炮幸存，普通塔毁；路径格无格天然豁免）——麟 1.5（二阶段 2.5 由 F6.3 运行时改写）；缺省 0 旧敌零迁移")]
        public double trampleRadius;

        // —— F7.5②③（用户定案 2026-10-19）：践踏蓄力（Boss 输出窗三件之一）——
        // 缺省全 0/关闭 = 旧敌零迁移（ch1-3 零路径铁律由 chargeEveryCells = 0 闸住）。
        [Tooltip("F7.5③ 践踏蓄力：每走多少格原地蓄力一次（0 = 关闭；麟 7——黄底 6-8）")]
        public double chargeEveryCells;
        [Tooltip("F7.5③ 蓄力站定秒数（**蓄力期不无敌**——Damage 照常，这才是免费输出窗）；麟 2.5")]
        public double chargeSeconds;
        [Tooltip("F7.5③ 蓄力重踏半径（格，**绝对值**——常规走行仍是 trampleRadius 1.5；蓄力中提示圈放大到此值并高亮，结束按此半径做一次践踏结算）；麟 2.5")]
        public double chargeRadiusMult = 2.5;

        [Tooltip("死亡分裂：产物敌字（空=不分裂；蛛→虫。分裂产物不掉墨——敌人草稿.csv）")]
        public string onDeathSpawns;
        [Tooltip("死亡分裂数量")]
        public int onDeathSpawnCount;

        [Tooltip("激怒阈值：血量比例 ≤ 该值时速度 × 倍率（0=无；牛=0.5×2）")]
        public double enrageAtRatio;
        [Tooltip("激怒速度倍率")]
        public double enrageSpeedMult = 2.0;

        [Tooltip("定期召唤：召唤物敌字（空=不召唤；酋长→召唤野人 每 20s×2 上限 6）")]
        public string summons;
        [Tooltip("召唤间隔（秒）")]
        public double summonInterval;
        [Tooltip("每次召唤数量")]
        public int summonCount = 2;
        [Tooltip("同场召唤物存活上限")]
        public int summonCap = 6;

        // —— F4.2（ch5 魇）入梦：缺省 0 = 关闭（旧敌零迁移）——
        [Tooltip("F4.2（ch5 魇）：入梦——每 N 秒让半径内一塔入梦（停火不降级）。0 = 关闭；魇 6s")]
        public double sleepInterval;
        [Tooltip("F4.2 入梦时长（秒；魇 2）")]
        public double sleepSeconds = 2;
        [Tooltip("F4.2 入梦半径（格；魇黄底 2.5）")]
        public double sleepRadius = 2.5;

        // —— F4.3（ch5 傀）换影复活：缺省 0 = 关闭（旧敌零迁移）——
        [Tooltip("F4.3（ch5 傀）：**换影复活**——首次致死改为在**另一面同进度**以该比例血复活一次（0 = 不复活；傀 0.5）")]
        public double reviveHpRatio;
        [Tooltip("F4.4（ch5 小鬼）：**墨漏**——到终点扣墨而非扣印（0 = 关闭；小鬼 3。livesCost 同步配 0）")]
        public double inkLeak;

        // —— F5.1（ch5 傩面 P1）三脸轮换：缺省关闭（旧敌零迁移）——
        [Tooltip("F5.1（ch5 傩面）：**三脸轮换**——赤=实 / 青=虚 / 金=跟随玩家面；每 GameParams.nuoFaceCycleSeconds 换一张脸（缺省 false 旧敌零路径）")]
        public bool nuoMask;
        [Tooltip("F5.1（ch5 傩面）：**到出口扣 livesCost 印后回起点重走**（Boss 到点不判败、不结束本关；缺省 false = 到点即败）")]
        public bool endlessLap;
        [Tooltip("F6.1（ch5 傩神 P2）：**三面随行**（纯表现：无血条/不可选中/不可击杀）+ **傩舞**（每 shenDanceInterval 秒停 2.5s、受伤 ×1.5、三面同时施法）")]
        public bool nuoShen;
        [Tooltip("F6.1（ch5 傩神）：**跨面可被攻击**——塔不论实/虚面都能索敌/溅射到它（Boss 二阶段可被两面集火；缺省 false = 铁律「塔只打本面」）")]
        public bool crossFaceAttackable;
    }

    /// <summary>敌人表：刷怪数值的唯一数据源。</summary>
    [CreateAssetMenu(menuName = "WordWars/敌人表", fileName = "EnemyTable")]
    public class EnemyTableDef : ScriptableObject
    {
        public List<EnemyStats> enemies = new List<EnemyStats>();

        public EnemyStats Find(string charId) =>
            enemies.Find(e => e.charId == charId);
    }
}
