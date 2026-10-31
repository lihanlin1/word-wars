using System;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 通用世界坐标大按钮（Phase 0.2：N/R 可点化——真机前必需，手机无键盘）。
    /// 白图底 + BoxCollider2D + OnMouseDown（触屏单指 = 模拟鼠标，与 HandCard 同管线）。
    /// ⚠️ 文字不是本物体的子物体：宽按钮 body 是非等比缩放，字挂它下面会被横向拉宽
    /// （HandBar 下一波按钮同坑）——Label 挂等比 holder 层，经 Init 显式注入引用。
    /// 文本运行时可改（通关「下一关」/ 章末「回第一章」复用同一颗）。
    /// ⚠️ 文件名铁律：MonoBehaviour 类名与文件名一致（WorldButton.cs）。
    /// </summary>
    public class WorldButton : MonoBehaviour
    {
        private Action onClick;

        // [SerializeField]：克隆复制字段铁律（本类当前不复用池，保持口径一致）
        [SerializeField] private TextMeshPro label;

        public void Init(Action click, TextMeshPro textLabel)
        {
            onClick = click;
            label = textLabel;
        }

        public void SetText(string text)
        {
            if (label != null) label.text = text;
        }

        private void OnMouseDown()
        {
            SfxPlayer.Click(); // 用户 2026-10-26：UI 点击音（槽位空/关音效 = 静默）
            onClick?.Invoke();
        }
    }
}
