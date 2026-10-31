using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 攻击范围指示器（用户需求 2026-09-28）：拿起塔时在原格显示射程圈——
    /// 半透明蓝墨圆盘 + 细描边圈，半径 = 塔当前射程（含升级成长）。
    /// 生命周期由 GameManager 驱动：PickupTower 显示，ClearPickupState 隐藏。
    /// 塔拿起后停在原格微浮（不跟鼠标），圈位置固定即可。
    /// </summary>
    public class RangeIndicator : MonoBehaviour
    {
        private const int Segments = 64;
        private static readonly Color BlueInk = HexUtil.ToColor("#2B4C7E");

        private SpriteRenderer disc; // 半透明盘
        private LineRenderer rim;    // 细描边圈

        /// <summary>在 GameManager.Start 创建后调用一次。</summary>
        public void Init(Sprite white)
        {
            var discGo = new GameObject("Disc", typeof(SpriteRenderer));
            discGo.transform.SetParent(transform, false);
            discGo.transform.localPosition = Vector3.zero;
            disc = discGo.GetComponent<SpriteRenderer>();
            disc.sprite = white;
            var c = BlueInk;
            c.a = 0.13f;
            disc.color = c;

            // 描边圈：本地半径 0.5——根物体整体缩放 2R 后盘径 2R、圈半径 R，两者同心对齐
            rim = gameObject.AddComponent<LineRenderer>();
            rim.material = new Material(Shader.Find("Sprites/Default")); // 原型期运行时材质（W4 美术轮换）
            var rc = BlueInk;
            rc.a = 0.55f;
            rim.startColor = rim.endColor = rc;
            rim.startWidth = rim.endWidth = 0.05f;
            rim.useWorldSpace = false; // 本地圆：transform 挪到哪圈跟到哪
            rim.loop = true;
            rim.positionCount = Segments;
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                rim.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f));
            }

            Hide();
        }

        /// <summary>显示：半径 radius（世界单位），圆心 center（世界坐标）。</summary>
        public void Show(double radius, Vector3 center)
        {
            float r = (float)radius;
            gameObject.SetActive(true);
            transform.position = new Vector3(center.x, center.y, -0.15f); // 格子层(0.06)之前、塔(-0.35)之后
            transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
