using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// F2.1 喷射标记（ch4 火山，06 §12.2 / docs/11 序5）：红闪预警 → 红印残留的独立视觉层。
    /// 为什么独立层而不进 GridCell：①路面格/带宽空地不是 GridCell（章图 cells 只含塔防/垦格）
    /// ——落点三分三类目标（塔防/路径/空地）一套视觉通用；②不碰 ch1-3 格视觉管线（铁律
    /// 零风险）；③挂 grid 子层随 Build 整组销毁——R 回滚/换关的「完整重置」零额外代码。
    /// 层序 z=-0.05（z 越小越靠相机）：盖格 fill(0.05)/blot(0.04)/路面(0.5)，在开垦进度条
    /// (-0.06/-0.07)/铲 glyph(-0.08)/雾(-0.2)/塔(-0.35)/敌(-0.4)/飘字(-0.45)之后——红光垫底
    /// 不遮单位，塔上红环预警全读、垦进度条仍可见。
    /// 预警 = 半秒一明一暗脉冲（1.6Hz——「这里要出事」）；落地转红印线性淡出
    /// （lavaMarkSeconds，本单元纯视觉——三类落点结算 F2.3 / 红印 DoT F2.4 接线）。
    /// </summary>
    public class EruptionMarker : MonoBehaviour
    {
        private static readonly Color LavaRed = HexUtil.ToColor("#E0521E"); // 熔岩橙红（红闪/红印同源）

        private SpriteRenderer sr;
        private float warnRemaining;      // 红闪倒计时（= 撤离窗口长度，GameParams.eruptionFlashSeconds）
        private float scarTotal, scarRemaining; // 红印残留（lavaMarkSeconds；Land 后才开始倒数）
        private bool landed;

        /// <summary>预警期标记（挂 grid 子层——随 Build 整组销毁，无需手动清理）。</summary>
        public static EruptionMarker Create(Transform parent, Sprite white, Vector2Int cell,
            float warnSeconds, float scarSeconds)
        {
            var go = new GameObject("Lava_" + cell.x + "_" + cell.y, typeof(EruptionMarker));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(cell.x, cell.y, -0.05f);
            go.transform.localScale = new Vector3(0.92f, 0.92f, 1f); // 贴格（fill 0.74 / frame 0.94 之间偏满）
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            var c0 = LavaRed; // 初帧即熔岩色（防 Update 前一帧白块闪现）
            c0.a = 0.25f;
            sr.color = c0;
            var m = go.GetComponent<EruptionMarker>();
            m.sr = sr;
            m.warnRemaining = Mathf.Max(0.1f, warnSeconds);
            m.scarTotal = Mathf.Max(0.5f, scarSeconds);
            m.scarRemaining = 0f;
            m.landed = false;
            return m;
        }

        /// <summary>落地（波钟 landClock 到点，GameManager 调）：红闪转红印淡出。</summary>
        public void Land()
        {
            if (landed) return;
            landed = true;
            warnRemaining = 0f;
            scarRemaining = scarTotal;
            sr.enabled = true;   // 预警计尽的一帧曾藏（极端时间差兜底），落地必显
            transform.localScale = new Vector3(1.02f, 1.02f, 1f); // 落地一拍放大（砸击感）再随淡出回落
        }

        private void Update()
        {
            // 暂停同闸（GM 冻结家族：Tick/波次/tint/栖灯倒数——红闪同冻结，防「暂停了还在闪」）
            if (GameManager.Instance != null && GameManager.Instance.Paused) return;

            if (!landed)
            {
                if (warnRemaining <= 0f) return; // 等待 Land()（波钟与倒计时同 deltaTime 源，同帧到点）
                warnRemaining -= Time.deltaTime;
                float k = Mathf.PingPong(Time.time * 1.6f, 1f); // 0→1→0：半秒一明一暗
                var c = LavaRed;
                c.a = Mathf.Lerp(0.25f, 0.75f, k);
                sr.color = c;
                float s = Mathf.Lerp(0.92f, 1.0f, k); // 明相呼吸微放大
                transform.localScale = new Vector3(s, s, 1f);
                if (warnRemaining <= 0f) sr.enabled = false;
            }
            else
            {
                if (scarRemaining <= 0f) { Destroy(gameObject); return; } // 红印淡尽自毁（grid 重建也整组销毁）
                scarRemaining -= Time.deltaTime;
                float t = Mathf.Clamp01(scarRemaining / scarTotal); // 1→0 线性淡出
                var c = LavaRed;
                c.a = 0.55f * t;
                sr.color = c;
                float s = Mathf.Lerp(0.92f, 1.02f, t);
                transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
