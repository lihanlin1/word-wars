using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// F6.1 麟践踏光环提示圈（06 §12.5 用户定案：**射程圈渲染复用**）：
    /// 半透明盘 + 细描边圈，半径 = 该敌当前 trampleRadius（二阶段放大 = F6.3 改写敌字段，
    /// 圈每帧跟读自动放大，零额外代码）。
    ///
    /// 生命周期由 GameManager 驱动（同 RangeIndicator 家族）：场上有光环敌 →
    /// Show（每帧跟位，常显 = 预告义务在 Boss 层保留），无 → Hide（进关/败/胜/无麟全收）。
    /// 颜色/透明度 = GameParams Inspector 外露（F6.1 任务单口径）——GameManager 每帧
    /// SetStyle，Play 中调参数即时可见（黄底调参纪律）。
    /// </summary>
    public class TrampleIndicator : MonoBehaviour
    {
        private const int Segments = 64;
        private static readonly Color Fallback = new Color(0.78f, 0.20f, 0.16f);

        private SpriteRenderer disc; // 半透明盘
        private LineRenderer rim;    // 细描边圈
        private Color style = Fallback;
        private Color styleCharged = new Color(1f, 0.55f, 0.15f); // F7.5③ 蓄力高亮色（GameParams 每帧覆盖）
        private float discAlpha = 0.10f;
        private float rimAlpha = 0.65f;
        private bool charged;        // F7.5③ 蓄力态（换高亮色；半径由调用侧给放大值）

        /// <summary>在 GameManager.Start 创建后调用一次。</summary>
        public void Init(Sprite white)
        {
            var discGo = new GameObject("Disc", typeof(SpriteRenderer));
            discGo.transform.SetParent(transform, false);
            discGo.transform.localPosition = Vector3.zero;
            disc = discGo.GetComponent<SpriteRenderer>();
            disc.sprite = white;

            // 描边圈：本地半径 0.5——根物体整体缩放 2R 后盘径 2R、圈半径 R（同 RangeIndicator 口径）
            rim = gameObject.AddComponent<LineRenderer>();
            rim.material = new Material(Shader.Find("Sprites/Default")); // 原型期运行时材质（同射程圈）
            rim.startWidth = rim.endWidth = 0.06f;
            rim.useWorldSpace = false; // 本地圆：transform 挪到哪圈跟到哪
            rim.loop = true;
            rim.positionCount = Segments;
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                rim.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f));
            }

            ApplyStyle();
            Hide();
        }

        /// <summary>样式（GameParams 外露——每帧调用幂等；Play 中调参即时生效）。
        /// F7.5③：chargeColor = 蓄力高亮色（蓄力中换色 + 放大 = 重踏预告）。</summary>
        public void SetStyle(Color color, float fillAlpha, float edgeAlpha, Color chargeColor)
        {
            style = color;
            styleCharged = chargeColor;
            discAlpha = fillAlpha;
            rimAlpha = edgeAlpha;
            ApplyStyle();
        }

        private void ApplyStyle()
        {
            var baseColor = charged ? styleCharged : style;
            if (disc != null)
            {
                var c = baseColor;
                c.a = discAlpha;
                disc.color = c;
            }
            if (rim != null)
            {
                var rc = baseColor;
                rc.a = rimAlpha;
                rim.startColor = rim.endColor = rc;
            }
        }

        /// <summary>常显：半径 radius（世界单位 = 格），圆心 center（世界坐标）。
        /// F7.5③ charged = 蓄力态（换高亮色；半径由调用侧给放大值——蓄力中 2.5、常规 1.5）。</summary>
        public void Show(double radius, Vector3 center, bool chargedNow = false)
        {
            charged = chargedNow;
            ApplyStyle();
            float r = (float)radius;
            gameObject.SetActive(true);
            // z = -0.155：格子层(0.06)之前、敌(-0.4)/塔(-0.35)之后（与射程圈 -0.15 错开 0.005 防共面闪烁）
            transform.position = new Vector3(center.x, center.y, -0.155f);
            transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
        }

        public void Hide()
        {
            charged = false;
            gameObject.SetActive(false);
        }
    }
}
