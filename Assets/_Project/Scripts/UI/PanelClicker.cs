using System;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 世界坐标面板点击上报（P5/P6）：物理 OnMouseDown → 回调。
    /// 单独成文件：MonoBehaviour 类名须与文件名一致（同 DeckCard/HandCard 规矩）。
    /// </summary>
    public class PanelClicker : MonoBehaviour
    {
        private Action onClick;

        public void Init(Action callback) => onClick = callback;

        private void OnMouseDown()
        {
            SfxPlayer.Click(); // UI 点击音（面板空白/继续/跳过）
            onClick?.Invoke();
        }
    }
}
