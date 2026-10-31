using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>W1 极简 HUD：墨水 / 印 / 关卡 / 波次 / 提示 / 横幅。W2 重做完整版（抽卡按钮+手牌区）。</summary>
    public class Hud : MonoBehaviour
    {
        public TextMeshProUGUI inkText;
        public TextMeshProUGUI livesText;
        public TextMeshProUGUI levelText; // 关卡标题（波次下方一行，v2.3）
        public TextMeshProUGUI waveText;
        public TextMeshProUGUI hintText;
        public TextMeshProUGUI bannerText;

        /// <summary>关卡标题（如「第一卷 · 墨启」；切关/重开时刷新）。</summary>
        public void SetLevel(string title)
        {
            if (levelText != null) levelText.text = title;
        }

        public void SetStats(double ink, int lives)
        {
            if (inkText != null) inkText.text = $"墨 {ink:0}";
            if (livesText != null)
            {
                // 用户 2026-10-28：印 → ♥（空格分隔，按剩余数）
                int n = Mathf.Max(0, lives);
                if (n <= 0) livesText.text = "";
                else
                {
                    var sb = new System.Text.StringBuilder(n * 2);
                    for (int i = 0; i < n; i++)
                    {
                        if (i > 0) sb.Append(' ');
                        sb.Append('♥');
                    }
                    livesText.text = sb.ToString();
                }
            }
        }

        public void SetWave(int wave)
        {
            if (waveText != null) waveText.text = $"第 {wave} 波";
        }

        /// <summary>自由文本波次栏（备战倒计时 / 通关等）。</summary>
        public void SetWaveText(string text)
        {
            if (waveText != null) waveText.text = text;
        }

        public void ShowBanner(string msg)
        {
            if (bannerText == null) return;
            bannerText.text = msg;
            bannerText.gameObject.SetActive(true);
        }

        public void HideBanner()
        {
            if (bannerText != null) bannerText.gameObject.SetActive(false);
        }
    }
}
