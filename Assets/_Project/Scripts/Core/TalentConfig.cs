using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordWars
{
    /// <summary>单条天赋定义（用户 2026-11-04，ADR-086）：id 固定给代码判定用，名称/描述/价格可改。</summary>
    [Serializable]
    public class TalentDef
    {
        [Tooltip("代码判定用的固定 id（勿改）：nuke/family/prodigy/upgrade/upgrade_poor/timestop/interest")]
        public string id = "";
        public string name = "";
        [Tooltip("天赋图标（美术自备；空 = 不显示图标）")]
        public Sprite icon;
        [TextArea(2, 4)] public string desc = "";
        [Tooltip("商店价格（金币；0 = 免费/待填）")]
        public int price = 0;
    }

    /// <summary>
    /// 天赋表（7 个）：商店购买 → 进天赋库 → 困难章配置时选 ≤ 槽位个。
    /// ⚠️ 文件名铁律：ScriptableObject 类名与文件名一致。
    /// </summary>
    [CreateAssetMenu(menuName = "WordWars/天赋配置", fileName = "TalentConfig")]
    public class TalentConfig : ScriptableObject
    {
        public List<TalentDef> talents = new List<TalentDef>();

        public TalentDef Find(string id)
        {
            if (string.IsNullOrEmpty(id) || talents == null) return null;
            for (int i = 0; i < talents.Count; i++)
                if (talents[i] != null && talents[i].id == id) return talents[i];
            return null;
        }
    }

    /// <summary>天赋 id 常量（代码与资产共用同一套字符串）。</summary>
    public static class TalentIds
    {
        public const string Nuke = "nuke";               // 六个核弹
        public const string Family = "family";           // 家族企业（家产墨）
        public const string Prodigy = "prodigy";         // 天赋异禀（合并 33% 越阶）
        public const string Upgrade = "upgrade";         // 升阶秘籍
        public const string UpgradePoor = "upgrade_poor"; // 升阶秘籍（劣）
        public const string TimeStop = "timestop";       // 咋瓦鲁多
        public const string Interest = "interest";       // 利滚利

        public static readonly string[] All =
        {
            Nuke, Family, Prodigy, Upgrade, UpgradePoor, TimeStop, Interest
        };
    }
}
