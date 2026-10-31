using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>世界空间伤害/收益飘字（池化）。上飘 + 淡出。</summary>
    public class FloatingText : MonoBehaviour
    {
        // [SerializeField]：对象池克隆需要复制此引用
        [SerializeField] private TextMeshPro label;
        private float life;
        private float lifeMax = 0.7f;
        private Vector3 vel;

        public void Init(TMP_FontAsset font)
        {
            label = GetComponent<TextMeshPro>();
            if (label == null) label = gameObject.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 7;
            label.alignment = TextAlignmentOptions.Center;
            // 宽 8 = 容纳最长文案（9 字 × 7 号 ≈ 6.3 单位）单行直排，不在 3 宽旧框里折行
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(8f, 1.2f);
        }

        public void Show(string text, Vector3 worldPos, Color color, float size = 7f)
        {
            label.text = text;
            label.color = color;
            // 交互文案（拒绝/状态提示）缺省 7 号（用户 2026-10-02「太大，要小很多」，替代旧默认 14；
            // 实际值经 SpawnFloat 读 GameParams.toastFontSize）；战斗数值飘字由调用方显式传小号
            label.fontSize = size;
            transform.position = worldPos + new Vector3(0, 0.6f, -0.8f);
            life = lifeMax;
            vel = new Vector3(0, 1.6f, 0);
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (life <= 0f) return;
            life -= Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            vel *= 1f - 2.5f * Time.deltaTime;
            var c = label.color;
            c.a = Mathf.Clamp01(life / lifeMax);
            label.color = c;
            if (life <= 0f && GameManager.Instance != null)
                GameManager.Instance.ReleaseFloat(this);
        }
    }
}
