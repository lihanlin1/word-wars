using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 弹道视图：箭形流光，按塔字着色（弩朱红 / 矛青 / 其余浓墨）。
    /// 主条沿飞行方向转向；衬底尾迹（宣纸色）保证弹道飞过深色墨路时
    /// 也立得住对比度。尺寸读 GameParams（arrowLength/arrowWidth，Inspector 直调）：
    /// ADR-025 修复白图 PPU 后，旧 1.98×0.6 巨箭回退为修复前实显示尺寸 0.16×0.05
    /// （当年「子弹太小看不清→三倍放大三轮」全系同一根因的补偿，如今参数归位可自助调）。
    /// 每帧读模型位置飞行；命中/消散由 GameManager 归还池。
    /// 矛命中 = AOE 溅射起点（v3.0；伤害判定在 BattleModel.DamageArea）。
    /// </summary>
    public class ProjectileView : MonoBehaviour
    {
        public ProjectileState State { get; private set; }

        // [SerializeField]：对象池克隆需要复制此引用（Instantiate 快照序列化字段）
        [SerializeField] private SpriteRenderer dot;
        [SerializeField] private SpriteRenderer halo; // 宣纸衬底尾迹
        [SerializeField] private GameParams cfg;      // 弹道尺寸参数（可空：回落内置缺省）

        /// <summary>在模板上调用一次（SpriteRenderer 视图，无需 TMP 构件）。cfg 可空。</summary>
        public void Init(Sprite white, GameParams gameParams = null)
        {
            cfg = gameParams;
            dot = GetComponent<SpriteRenderer>();
            if (dot == null) dot = gameObject.AddComponent<SpriteRenderer>();
            dot.sprite = white;
            ApplySize();

            if (halo == null)
            {
                var haloGo = new GameObject("Halo", typeof(SpriteRenderer));
                haloGo.transform.SetParent(transform, false);
                haloGo.transform.localPosition = new Vector3(-0.12f, 0f, 0.02f); // 略后偏 = 尾迹
                haloGo.transform.localScale = new Vector3(1.25f, 1.5f, 1f);       // 相对主条更长更宽
                halo = haloGo.GetComponent<SpriteRenderer>();
                halo.sprite = white;
                var paper = HexUtil.ToColor("#F7F2E7");
                paper.a = 0.85f;
                halo.color = paper;
            }
        }

        /// <summary>长轴 = 飞行方向。每次 Show 重取：Play 中改 GameParams，下一发即生效。</summary>
        private void ApplySize()
        {
            float len = cfg != null ? cfg.arrowLength : 0.16f;
            float wid = cfg != null ? cfg.arrowWidth : 0.05f;
            transform.localScale = new Vector3(len, wid, 1f);
        }

        public void Show(ProjectileState state)
        {
            State = state;
            dot.color = ColorFor(state.charId);
            transform.position = new Vector3(state.position.x, state.position.y, -0.35f);
            ApplySize(); // 池对象复用兜底 + 支持改参后即时生效
        }

        private void Update()
        {
            if (State == null) return;
            var p = new Vector3(State.position.x, State.position.y, -0.35f);
            // 朝向 = 本帧位移方向（模型不存速度向量；位移极小时保持原朝向防抖）
            Vector2 delta = p - transform.position;
            if (delta.sqrMagnitude > 1e-6f)
            {
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            transform.position = p;
        }

        private static Color ColorFor(string charId)
        {
            if (charId == "弩") return HexUtil.ToColor("#B83A2E"); // 狙击朱红（单发重伤的「重」感）
            if (charId == "矛") return HexUtil.ToColor("#3E6B8C"); // 群体青（远射程溅射的「凉」感）
            return HexUtil.ToColor("#26231F");                     // 默认浓墨
        }

        public void ClearState() => State = null;
    }
}
