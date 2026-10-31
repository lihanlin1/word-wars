using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWars
{
    /// <summary>AOE 形态（v3.0 GDD §5.1 特效列）。</summary>
    public enum AoeShape
    {
        None = 0,   // 单体（刀/弓/弩/墨）
        Rect = 1,   // 矛：以命中点为中心的矩形（宽×高）
        Circle = 2, // 棍：以塔为中心的圆
        Sector = 3, // 戟：以塔为中心、朝主目标方向的扇形（半径×张角）
    }

    /// <summary>
    /// 单字塔数值（GDD §5.1 的可编辑形态）。
    /// v3.0：+value 卡价值（一口价/卖价）；+AOE 三形态（矛矩形/棍圆形/戟扇形，仅地面）。
    /// 普通 [Serializable] 数据类可以与别的类共住一个文件，无文件名限制。
    /// </summary>
    [Serializable]
    public class TowerCharStats
    {
        [Tooltip("塔字，如 刀")]
        public string charId = "刀";
        [Tooltip("定位说明，仅文档用途")]
        public string role = "近战高伤";
        [Tooltip("卡价值（v3.0）：一口价 = 池内卡价值之和；卖出 = 价值×50%×字位")]
        public int value = 8;
        [Tooltip("伤害（0 = 无攻击的纯经济塔）")]
        public double damage = 30;
        [Tooltip("攻击间隔（秒）")]
        public double interval = 0.9;
        [Tooltip("射程（格）")]
        public double range = 1.8;
        [Tooltip("经济塔：每次产墨量（间隔 = GameParams.inkPulseSeconds，默认 5 秒/次；升阶 ×2/级）")]
        public double inkPerPulse;
        [Tooltip("能否命中空中敌")]
        public bool antiAir;
        [Tooltip("远程：发射弹道；近战：瞬发")]
        public bool ranged;
        [Tooltip("v2.2 射程成长通道：每级 +N 格（弓/弩 0.25；近战与矛为 0，矛走 AOE 范围成长）")]
        public double rangePerLevel;
        [Tooltip("F2.2 水系免疫（ADR-032；F3 水/冰/渔打 true）：被淹不沉默不倒计不消失；可建水格/临时淹没格")]
        public bool waterImmune;
        [Tooltip("F3.1 水塔：命中缓速——slowFactor=减速比例（0.3=慢30%），slowSeconds=持续秒；0=无")]
        public double slowFactor;
        public double slowSeconds;
        [Tooltip("F3.1 水塔：命中附带「湿」标记秒数（冰塔对湿敌 ×2）；0=无")]
        public double wetSeconds;
        [Tooltip("F3.1 冰塔：命中冻结秒数；0=无")]
        public double freezeSeconds;
        [Tooltip("F3.1 冰塔：对湿敌伤害倍率（1=无加成，冰=2）")]
        public double wetDamageMult = 1.0;
        [Tooltip("F3.3 渔塔：陆上产墨跳隔 ×2（水中 = GameParams.inkPulseSeconds 基准 5s）；墨塔 false")]
        public bool landPulseSlower;
        [Tooltip("AOE 形态（仅地面）：None=单体；Rect=命中点矩形；Circle=塔中心圆；Sector=塔中心扇形")]
        public AoeShape aoeShape = AoeShape.None;
        [Tooltip("Rect=宽(格)；Circle/Sector=半径(格)。成长 +0.15/级")]
        public double aoeSize;
        [Tooltip("Rect=高(格)；Sector=张角(度)")]
        public double aoeParam;
        [Tooltip("F4.1（06 §12.3）火/炮：命中附加燃烧秒数（0=无燃烧——ch1-3 全 0 零路径）；重复触发刷新时长")]
        public double burnSeconds;
        [Tooltip("F4.1 火/炮：每整秒一跳燃烧伤害；灰烬增益 = ×GameParams.ashDamageMult（施加侧 AshBuffLookup——F7.4 起该塔直接攻击同享）")]
        public double burnDps;
        [Tooltip("F3.1（ch5 镜）：建成时在**对面同坐标**生成镜像（= 双面覆盖卡）；对面同格已有塔 → 红框禁放")]
        public bool createsMirror;
        [Tooltip("F3.1 镜像伤害比例（镜 = 0.5 → 22 伤的镜像 11 伤/1.0s）；镜像不可卖/升/移，本体卖/毁同步消失")]
        public double mirrorDamageRatio = 0.5;
        [Tooltip("F3.2（ch5 雷）：链电跳数（雷 = 3；0 = 无链——ch1-4 全部缺省零路径）。每跳沿路径后方传导")]
        public int chainJumps;
        [Tooltip("F3.2 链电：每跳伤害倍率（0.6 → 26→15.6→9.4→5.6）")]
        public double chainRatio = 0.6;
        [Tooltip("F3.2 链电：链距（格）——下一跳须在上一跳落点此半径内，超距断链（2.0）")]
        public double chainRadius = 2.0;
        [Tooltip("F3.3（ch5 煞）：**虚面**（face=1）伤害倍率（1.5）；1 = 无加成——ch1-4 全部缺省 1 = 零变化。全游戏唯一虚面加成卡")]
        public double voidDamageMult = 1.0;
        [Tooltip("F3.4（ch5 幡）：魂幡标记——敌死每 10 魂结一次墨（多幡只按**最高阶**结算，不叠加；魂每关清零）")]
        public bool soulBanner;
        [Tooltip("F3.4 幡：L1 每 10 魂产墨（升阶 ×2/级：5/10/20/40/80）。魂不进墨塔统计")]
        public double soulPayout = 5;
    }

    /// <summary>
    /// 塔字池：抽卡来源与放塔数值的唯一数据源。
    /// ⚠️ Unity 铁律：ScriptableObject/MonoBehaviour 派生类必须放在与类同名的 .cs 文件里
    /// （TowerPoolDef.cs），否则资产序列化时 m_Script 引用写不出来 → 加载返回 null。
    /// （W2a 曾把它和词库共放 CardDefs.cs，产出坏资产，即此教训。）
    /// </summary>
    [CreateAssetMenu(menuName = "WordWars/塔字池", fileName = "TowerPool")]
    public class TowerPoolDef : ScriptableObject
    {
        public List<TowerCharStats> chars = new List<TowerCharStats>();

        public TowerCharStats Find(string charId) =>
            chars.Find(c => c.charId == charId);
    }
}
