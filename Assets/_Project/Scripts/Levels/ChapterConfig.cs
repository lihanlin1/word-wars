using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 格子状态。配置层写 Empty/Fallow/Water（初始布局）；
    /// Cultivating 是运行时态（铲开垦计时中），占用 = Tower != null 的派生态。
    /// Water（F1.1，ch2 水泽）：永久水格——恒为水（潮汐不增减），仅水系塔可建、
    /// 不可开垦；海拔字段对其无效。
    /// </summary>
    public enum CellState
    {
        Empty = 0,       // 空地：可放塔/移入
        Fallow = 1,      // 非空地（荒地）：须铲开垦
        Cultivating = 2, // 开垦中（运行时态，配置不写入）
        Water = 3,       // 永久水格（ch2 水泽卷：水路敌航道 + 水系三卡专属地皮）
    }

    /// <summary>
    /// 灰烬状态（F1.2，ch4 火山，06 §12.2/§12.8）：塔防格的**运行时烧毁标记**——
    /// 非新格型（CellState 配置层不动，两层正交）。Normal = 缺省（ch1-3 零变化铁律——
    /// 旧资产 YAML 无此字段行，字段初始化器生效）；Ash = 岩浆/践踏烧毁（**本关内永久**：
    /// 普通塔不可直接放，火/炮/烬/盆白名单可建；恢复唯一路径 = 盆）；Green = 盆复绿
    ///（绿框 = 通用可建含墨；可被岩浆再毁回 Ash）。烧到垦中格 = 进度埋灰暂停
    ///（submerged 同构），盆复绿续跑。战斗态：R 回滚/换关随 grid 重建复位。
    /// </summary>
    public enum TileState
    {
        Normal = 0, // 未烧（缺省——ch1-3 恒此态，谓词零进入）
        Ash = 1,    // 灰烬（烧毁——焦土视觉 + 放置白名单）
        Green = 2,  // 复绿（盆——苔绿框 + 通用可建）
    }

    /// <summary>
    /// 格子布局项（1 格 = 1 世界单位，坐标取整数；ADR-014 路上格后路线占
    /// 整数格行 y=3/0/-3、竖段 x=±5——布局时避开路面格即可，种子已核）。
    /// </summary>
    [Serializable]
    public class CellLayout
    {
        public int x;
        public int y;
        public CellState state = CellState.Empty;

        /// <summary>
        /// 海拔（F1.1 数据层，ADR-032 潮汐淹没判定）：潮位水线 退=0/平=1/涨=2/大涨=3，
        /// 被淹 ⟺ 海拔 ≤ 水线。4 = 高地默认（永不淹，ch1 全图即此档，零改动兼容）；
        /// 3 = 左低地（仅大涨淹，L4-L5 高潮位波追加）；2 = 潮间带高带（涨潮淹）；
        /// 1 = 潮间带低带/滩涂（平潮即淹、退潮露出——滩涂与低带同档，视觉另行区分）。
        /// </summary>
        [Range(1, 4)] public int elevation = 4;
    }

    /// <summary>
    /// 章图配置（v3.0 03 §4.7，ADR-021）：一章一图，章内 5 关（L1-L5）共享
    /// 格子布局与主题。从 LevelConfig 拆出——关卡只管波次/卡池/资源，地图归属章。
    /// 第一章定稿：12 空地 + 6 可垦（关卡草稿.csv「章图共用 12 空 6 垦」）。
    /// 第二章水泽（F1.1 起）：格子扩 Water 态 + 海拔（潮汐淹没判定，ADR-032）——
    /// 见 CellLayout.elevation 注释；ch1 旧资产无新字段行 → 缺省「全高地」零改动兼容。
    /// ⚠️ 文件名铁律：ScriptableObject 类与文件名一致（ChapterConfig.cs）。
    /// </summary>
    [CreateAssetMenu(menuName = "WordWars/章节", fileName = "Chapter")]
    public class ChapterConfig : ScriptableObject
    {
        [Header("基础")]
        public int chapterId = 1;
        public string title = "第一章 · 草原狩猎";

        [Header("图鉴（用户 2026-10-28）")]
        [Tooltip("地图机制描述（图鉴「地图」页显示；空 = 显示「暂无描述」）。多行可用；换行用 \\n")]
        [TextArea(3, 10)] public string codexDesc = "";

        [Header("章图格子布局（12 空地 + 6 可垦；动布局避开路面行 y=3/0/-3 与竖段 x=±5）")]
        public List<CellLayout> cells = new List<CellLayout>();

        [Header("路线（F1.2 章数据化，ADR-032：折点世界坐标取整数）")]
        [Tooltip("陆路（主路）折点。缺省 = 第一章蛇形——旧资产无此字段行走缺省，零迁移")]
        public List<Vector2> landRoute = new List<Vector2>
        {
            new Vector2(-9f, 3f), new Vector2(5f, 3f),
            new Vector2(5f, 0f), new Vector2(-5f, 0f),
            new Vector2(-5f, -3f), new Vector2(9f, -3f),
        };

        [Tooltip("水路折点（ch2 双线：出生左端→沿水路→汇合转主路→印章；空 = 单线章）")]
        public List<Vector2> waterRoute = new List<Vector2>();

        [Header("第三章·幽夜（F1.1/F1.3，ADR-037/040）")]
        [Tooltip("黑夜迷雾开关（章级）：false = ch1/ch2 全亮零变化铁律；true = 暗色遮罩+光圈挖洞（渲染层 F1.1 落地）")]
        public bool fogEnabled;

        [Tooltip("同路双向（F1.3）：true = 敌分上下口出生、各走半程到正中家（弧长中点）；false = 单向全程（ch1/ch2）")]
        public bool bidirectional;

        [Header("第四章·火山（F1.1，ADR-046 预：ch4 火山卷）")]
        [Tooltip("火山开关（章级）：false = ch1-3 全链零变化铁律（YAML 缺行缺省）；true = 章图渲染火山口装饰（F1.1）+ 喷射引擎按活跃度运转（F2 起）")]
        public bool volcanoEnabled;

        [Tooltip("火山口中心（章图视觉锚——Build 渲染装饰锥盘；喷射格池/践踏光环的环心参照，F2/F5 消费）")]
        public Vector2 volcanoCenter = Vector2.zero;

        [Header("第五章·诡异（F1.2，ADR-058：双面同图）")]
        [Tooltip("双面开关（章级）：false = ch1-4 全链零变化铁律（YAML 缺行缺省）；true = 一张图两面（实/虚两路 + 双面塔）")]
        public bool faceEnabled;

        [Tooltip("虚路折点（faceEnabled 时必填）：与陆路等长、同起终点——傩面换面按同路程进度落点的前提；空 = 单面章")]
        public List<Vector2> voidRoute = new List<Vector2>();

        [Header("BGM（用户 2026-10-09：每章专属 + Boss 关专属；音频自备拖入，空槽静音跳过）")]
        [Tooltip("本章默认 BGM（进关循环播放；空 = 静音）")]
        public AudioClip bgm;
        [Tooltip("本章 Boss 出场切换轨（Boss 死/漏切回默认；空 = 不切换）")]
        public AudioClip bossBgm;
    }
}
