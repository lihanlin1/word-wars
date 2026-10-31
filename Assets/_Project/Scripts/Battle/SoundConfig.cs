using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 战斗音效配置（阶段 E 音效接口，ADR-028）：音频文件由用户自备，拖入 Inspector 槽位。
    /// 槽位空 = 该类音效静音跳过（可只填部分槽位先用起来）。
    /// 文件建议放 Assets/_Project/Art/Audio/，wav/ogg/mp3 均可，选短促单声（&lt;0.5s）。
    /// ⚠️ 文件名铁律：类名与文件名一致（SoundConfig.cs）。
    /// </summary>
    [CreateAssetMenu(menuName = "WordWars/音效配置", fileName = "SoundConfig")]
    public class SoundConfig : ScriptableObject
    {
        [Header("塔攻击（开火瞬间）")]
        [Tooltip("近战挥击（刀/棍/戟共用）")]
        public AudioClip meleeAttack;
        [Tooltip("箭啸发射（弓/矛共用）")]
        public AudioClip bowShot;
        [Tooltip("重箭发射（弩专用）")]
        public AudioClip crossbowShot;

        [Header("敌人")]
        [Tooltip("受击（近战/弹道命中通用）")]
        public AudioClip enemyHit;

        [Header("铲")]
        [Tooltip("开垦挖土循环：开垦中持续播放、完成即停（选本身可无缝循环的音频）")]
        public AudioClip shovelLoop;

        [Header("Boss 机制（蛟两幕剧，用户 2026-10-09）")]
        [Tooltip("蛟蓄水怒吼（转阶段蓄水窗起点；Boss 通用怒槽——枭黑幕转场后续可复用）")]
        public AudioClip bossRoar;
        [Tooltip("洪水落地（水线 4 全图涌入）")]
        public AudioClip floodSurge;
        [Tooltip("潮落退水（4→0 档扫掠起点）")]
        public AudioClip floodEbb;

        [Header("第四章·火山（用户 2026-10-12 点题）")]
        [Tooltip("岩浆落地（红闪结束瞬间。一喷一声：同批 N 格同刻落地只放一路，防同帧叠放爆音；要逐格轰鸣改 GameManager UpdateEruptions 落地循环）")]
        public AudioClip lavaLand;

        [Header("第五章·傩面（F5.1-F5.2，用户 2026-10-22）")]
        [Tooltip("换脸落地（面具翻转完成瞬间；每 18s 一次）")]
        public AudioClip nuoMaskChange;
        [Tooltip("赤脸劈塔（毁塔不毁格；每 6s 一次）")]
        public AudioClip nuoSmash;
        [Tooltip("金脸封印落地（目标塔停火 3s）")]
        public AudioClip nuoSeal;

        [Header("通用交互（用户 2026-10-26，ADR-083；走 clickVolume 低音量档）")]
        [Tooltip("点击：按钮/格子/手牌/塔 按下（含误触）")]
        public AudioClip click;
        [Tooltip("无效点击：操作被拒绝（荒地须开垦/水格/灰烬/墨不足/非法位/空区取消）")]
        public AudioClip clickInvalid;
        [Tooltip("确认：构筑面板「出阵」、回滚/删档二次确认")]
        public AudioClip confirm;
        [Tooltip("菜单开合：暂停菜单/构筑面板/结算/日记 打开")]
        public AudioClip menu;

        [Header("塔操作（用户 2026-10-26）")]
        [Tooltip("放置成功")]
        public AudioClip place;
        [Tooltip("拿起塔")]
        public AudioClip pickup;
        [Tooltip("卖出成功（塔/手牌）")]
        public AudioClip sell;

        [Header("合成升阶（按结果等级；槽空向上降级）")]
        [Tooltip("L2-L3 合成")]
        public AudioClip mergeLow;
        [Tooltip("L4 合成")]
        public AudioClip mergeMid;
        [Tooltip("L5 合成（金）")]
        public AudioClip mergeHigh;
        [Tooltip("L6 合成（红阶；音量 ×1.15、不做音调抖动）")]
        public AudioClip mergeT6;

        [Header("战斗反馈（用户 2026-10-26）")]
        [Tooltip("敌人被击杀（非漏过）")]
        public AudioClip kill;
        [Tooltip("漏怪扣印")]
        public AudioClip leak;
        [Tooltip("通关")]
        public AudioClip victory;
        [Tooltip("失败（卷轴失守）")]
        public AudioClip defeat;

        [Header("资源经济 + ch5（用户 2026-10-26）")]
        [Tooltip("抽卡成功")]
        public AudioClip draw;
        [Tooltip("产墨跳（墨塔/家；全局 0.3s 节流）")]
        public AudioClip inkPulse;
        [Tooltip("开垦完成")]
        public AudioClip farmComplete;
        [Tooltip("红铲造格成功")]
        public AudioClip redShovel;
        [Tooltip("换面")]
        public AudioClip faceSwitch;
        [Tooltip("傩舞开始")]
        public AudioClip nuoDance;

        [Header("全局")]
        [Range(0f, 1f)]
        [Tooltip("音效总音量")]
        public float volume = 0.7f;
        [Range(0f, 1f)]
        [Tooltip("点击类音量档（click / clickInvalid / confirm；高频音效低一档防刺耳）")]
        public float clickVolume = 0.45f;

        [Header("BGM（用户 2026-10-09）")]
        [Range(0f, 1f)]
        [Tooltip("BGM 音量（独立于音效总音量；存档 bgmOn 开关在 SaveSystem）")]
        public float bgmVolume = 0.5f;
    }
}
