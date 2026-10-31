using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 安全区适配（docs/16 F3.2，场景级做法）：只把场景里预先建好的 `SafeAreaRoot`
    /// 的锚点设成 <see cref="Screen.safeArea"/>——**不做任何运行时重挂/搬家**，
    /// HUD 元素天生就在容器里，整体跟着内缩，不会跑位。
    /// 场景由 ProjectSetup 生成（Canvas → SafeAreaRoot → HUD 内容）；找不到容器则自动禁用。
    /// </summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform root;
        private Rect lastSafe = new Rect(-1, -1, 0, 0);

        private void Start()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { enabled = false; return; }
            foreach (Transform c in canvas.transform)
                if (c.name == "SafeAreaRoot") { root = (RectTransform)c; break; }
            if (root == null) { enabled = false; return; }
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafe) Apply();
        }

        private void Apply()
        {
            if (root == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safe = Screen.safeArea;
            lastSafe = safe;

            float minX = safe.xMin / Screen.width;
            float minY = safe.yMin / Screen.height;
            float maxX = safe.xMax / Screen.width;
            float maxY = safe.yMax / Screen.height;

            root.anchorMin = new Vector2(minX, minY);
            root.anchorMax = new Vector2(maxX, maxY);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }
    }
}
