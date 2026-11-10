using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 主界面 / 选关场景共用：相机 + 世界空间按钮/文字构建（与战斗内 UI 同一套观感）。
    /// 场景里只需挂一个继承本类的控制器；字体引用由 ProjectSetup 建场景时注入。
    /// </summary>
    public abstract class MenuSceneBase : MonoBehaviour
    {
        [SerializeField] protected TMP_FontAsset font;
        [SerializeField] protected Sprite whiteSprite;

        protected void EnsureScene()
        {            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera));
                go.tag = "MainCamera";
                cam = go.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HexUtil.ToColor("#F7F2E7");
            cam.transform.position = new Vector3(0f, 0f, -10f);

            if (whiteSprite == null)
            {
                // PPU = 贴图边长 → 白图恒为 1×1 世界单位（与 ProjectSetup 的 white.png 口径一致），
                // 否则按钮/细线的 localScale 里写的就是「几倍贴图」而不是「几个世界单位」。
                var tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                    Mathf.Max(1, tex.width));
            }
        }

        /// <summary>
        /// 屏幕适配系数：设计稿按 19.2 × 10.8（16:9）摆，窄屏（4:3 / 16:10 / 平板）整体等比缩小、
        /// 宽屏不动。菜单类界面全是世界坐标，不缩放会直接出框；文本是 SDF，缩放不糊。
        /// </summary>
        protected float ScreenFit()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return 1f;
            float halfW = cam.orthographicSize * cam.aspect;
            return Mathf.Clamp(Mathf.Min(halfW / 9.6f, 1f), 0.35f, 1f);
        }

        /// <summary>安全切场景：场景不在 Build Settings 时不抛异常（提示去跑 Setup 菜单）。</summary>
        protected static void LoadSceneSafe(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene))
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            else
                Debug.LogWarning("[Menu] 场景不在 Build Settings：" + scene + "（跑一次「Word Wars/Setup/重建主界面与选关场景」）");
        }

        protected TextMeshPro Label(Transform parent, string text, Vector3 pos, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, float width = 8f)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(MeshRenderer), typeof(MeshFilter));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.enableWordWrapping = false;
            t.text = text;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 1.8f);
            return t;
        }

        /// <summary>
        /// 同上，但让 Left 真的**从 pos.x 起笔**、Right 真的**到 pos.x 收笔**（Center 不变）。
        /// （Label 的 rect 以 pos 为中心，Left/Right 会以 pos 为中心摆，于是「左列」往左溢出屏幕、
        /// 两列互相压字。这里不碰 pivot——TMP 的排版按 rect 算，改 pivot 未必触发重建——
        /// 而是把整块文字平移半个宽度，几何上等价且没有重建时序风险。）
        /// </summary>
        protected TextMeshPro LabelAligned(Transform parent, string text, Vector3 pos, float size, Color color,
            TextAlignmentOptions align, float width)
        {
            float dx = 0f;
            if (align == TextAlignmentOptions.Left || align == TextAlignmentOptions.TopLeft ||
                align == TextAlignmentOptions.BottomLeft || align == TextAlignmentOptions.BaselineLeft ||
                align == TextAlignmentOptions.MidlineLeft || align == TextAlignmentOptions.CaplineLeft)
                dx = width * 0.5f;
            else if (align == TextAlignmentOptions.Right || align == TextAlignmentOptions.TopRight ||
                     align == TextAlignmentOptions.BottomRight || align == TextAlignmentOptions.BaselineRight ||
                     align == TextAlignmentOptions.MidlineRight || align == TextAlignmentOptions.CaplineRight)
                dx = -width * 0.5f;

            return Label(parent, text, new Vector3(pos.x + dx, pos.y, pos.z), size, color, align, width);
        }

        /// <summary>世界空间按钮（纸底 + 判定 + 文字挂等比 holder）。返回 holder（用于整块销毁/显隐）。</summary>
        protected GameObject Button(Transform parent, string name, Vector3 pos, Vector2 size, string text,
            Color textColor, System.Action onClick, out TextMeshPro label, float fontSize = 8f, Color? bg = null)
        {
            var holder = new GameObject(name + "Holder");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = pos;

            var body = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D));
            body.transform.SetParent(holder.transform, false);
            body.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = bg ?? new Color(0.93f, 0.89f, 0.80f, 0.97f);
            body.GetComponent<BoxCollider2D>().size = Vector2.one * 1.02f;

            label = Label(holder.transform, text, new Vector3(0f, 0.02f, -0.15f), fontSize, textColor,
                TextAlignmentOptions.Center, size.x + 0.6f);
            // 无回调 = 装饰/置灰按钮：不挂 WorldButton（否则点了也响一声「咔」）
            if (onClick != null) body.AddComponent<WorldButton>().Init(onClick, label);
            return holder;
        }
    }
}
