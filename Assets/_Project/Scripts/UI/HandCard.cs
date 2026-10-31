using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 手牌卡 / 抽卡按钮的点击上报（index = -1 表示抽卡按钮）。
    /// F1（docs/14，ADR-062）：字卡按下不再立即上报点选——改走「按下起点 → 拖拽/点选分辨」；
    /// 功能按钮（抽/卖/下一波/红铲，index &lt; 0）仍按下即点（旧路径零变化）。
    /// 独立文件：MonoBehaviour 类名须与文件名一致（同 TowerPoolDef 的规则）。
    /// </summary>
    public class HandCard : MonoBehaviour
    {
        private int index = -99;

        public void Init(int cardIndex) => index = cardIndex;

        private void OnMouseDown()
        {
            if (GameManager.Instance == null) return;
            SfxPlayer.Click(); // UI 点击音（字卡按下也响；拖拽/点选由 Update 分辨）
            if (index < 0)
                GameManager.Instance.OnHandCardClicked(index); // 功能按钮：按下即点（旧路径）
            else
                GameManager.Instance.OnHandCardPressed(index); // 字卡：点选/拖拽由 Update 分辨
        }
    }
}
