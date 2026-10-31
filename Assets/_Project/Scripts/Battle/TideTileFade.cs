using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 潮汐贴砖渐变（F4 验收追加，ADR-035）：水面覆盖层与路面砖共用。
    /// 与 GridCell.SetSubmerged 同节奏——列错峰扫掠延迟 + 1.2s 插值到目标色；
    /// Begin 捕获当前色为起点（快速连切保持连续，不闪跳）。
    /// 目标色 alpha≈0（水面退净）渐变完成即隐藏物体省渲染，Begin 会先唤醒。
    /// </summary>
    public class TideTileFade : MonoBehaviour
    {
        private SpriteRenderer sprite;
        private Color from, to;
        private float t = 1f;    // 1 = 无渐变进行
        private float delay;     // 扫掠等待（列错峰，涨左→右 / 退右→左）
        private float duration = 1.2f; // 单格渐变时长（潮汐 1.2s；换面路面色 0.35s 走参数覆盖）

        /// <summary>所在列（扫掠延迟用；路面砖/水面砖都是整数格心）。</summary>
        public int Col => Mathf.RoundToInt(transform.localPosition.x);

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
        }

        /// <summary>发起渐变（同色早退；透明目标顺带收进隐藏态）。
        /// duration 缺省 1.2s = 潮汐扫掠节奏；换面路面色改写传短时长，手感跟纸闪一致。</summary>
        public void Begin(Color target, float sweepDelay, float duration = 1.2f)
        {
            this.duration = Mathf.Max(0.05f, duration);
            if (sprite == null) sprite = GetComponent<SpriteRenderer>(); // 懒建格子首次唤醒前 Awake 未跑
            if (sprite.color == target)
            {
                to = target; // 同色早退也写目标：to 永不残留字段缺省 (0,0,0,0)（防误判退净的第二道保险）
                t = 1f;
                delay = 0f;
                if (target.a < 0.01f) gameObject.SetActive(false);
                return;
            }
            from = sprite.color;
            to = target;
            t = 0f;
            delay = Mathf.Max(0f, sweepDelay);
            gameObject.SetActive(true); // 退净收起后再涨：先唤醒再渐变
        }

        private void Update()
        {
            if (t >= 1f)
            {
                // 退净收进隐藏态（Update 随之停摆，下次 Begin 唤醒）。
                // 必须查现色：fresh 砖的 to 是字段缺省 (0,0,0,0) 透明黑、t 缺省 1——
                // 只看 to 会把「从未渐变过」误判成「退净完成」，路面砖建出来一帧就自藏（实测事故）
                if (to.a < 0.01f && sprite != null && sprite.color.a < 0.01f) gameObject.SetActive(false);
                return;
            }
            if (delay > 0f)
            {
                delay -= Time.deltaTime;
                return;
            }
            t = Mathf.Min(1f, t + Time.deltaTime / duration); // 单格渐变（潮汐 1.2s / 换面 0.35s）
            sprite.color = Color.Lerp(from, to, t);
        }
    }
}
