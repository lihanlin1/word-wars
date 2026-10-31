using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 构筑面板卡片的点击上报（镜像 HandCard 模式：物理 OnMouseDown，无 uGUI 依赖）。
    /// 独立文件：MonoBehaviour 类名须与文件名一致。
    /// index ≥ 0 = 卡位；-1 = 确认（出阵）。
    /// </summary>
    public class DeckCard : MonoBehaviour
    {
        private int index = -99;

        public void Init(int cardIndex) => index = cardIndex;

        private void OnMouseDown()
        {
            SfxPlayer.Click(); // UI 点击音（有效/无效都由面板自己再补「错误点击」）
            var panel = GetComponentInParent<DeckPanel>();
            if (panel != null) panel.OnCardClicked(index);
        }
    }
}
