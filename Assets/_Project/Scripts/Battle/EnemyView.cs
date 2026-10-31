using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 敌字视图：朱红歪斜字 + 血条。每帧从模型数据推算位置，不持有逻辑。
    /// 死亡播字散动画后自动归还对象池。
    /// </summary>
    public class EnemyView : MonoBehaviour
    {
        public EnemyState State { get; private set; }

        // [SerializeField]：对象池靠序列化复制字段，非序列化私有字段在 Instantiate 克隆后会丢失
        [SerializeField] private TextMeshPro label;
        [SerializeField] private SpriteRenderer hpFg;
        [SerializeField] private GameObject hpBarRoot;
        private float deathTimer = -1f;
        private bool releasing;

        // 字号基线（楷体动态 SDF）：普通敌小字无血条，Boss 大字带血条
        private const float NormalFontSize = 12f;
        private const float BossFontSize = 24f;

        // F3.1 状态染色（ADR-033）：缓速=蓝墨 / 冻结=冰蓝；帧间去重防 TMP 每帧 SetVerticesDirty
        // F3.2（ADR-039）增眩晕=琥珀金（独立于冻结冰蓝——视觉语义不混；目眩金花）
        private static readonly Color VermilionBase = HexUtil.ToColor("#B83A2E"); // 朱砂（常态）
        private static readonly Color SlowTint = HexUtil.ToColor("#2B4C7E");      // 缓速（蓝墨）
        private static readonly Color FrozenTint = HexUtil.ToColor("#7FB5C9");    // 冻结（冰蓝）
        private static readonly Color StunTint = HexUtil.ToColor("#B8860B");      // 眩晕（琥珀金）
        private int statusTint = 0;   // 0=朱 1=缓 2=冻（染色去重）

        // F2.1 幽夜敌 tint（06 §11.2）：雾中深色半透明剪影 / 入光全色现形。与状态色两层解耦——
        // 显示色 = 状态基色（上面三色）× 雾系数（fogDim 压暗 + fogAlphaMul 降透明），
        // 雾中缓速/冻结 = 状态色再压暗（乘法叠加不打架）。潜行浅影（F2.2 stealth 打标后
        // GameManager 侧换参传入）/ 枭红闪（F5）复用同管线（RefreshLabelColor 加分支即可）。
        private bool fogged;
        private float fogDim = 1f, fogAlphaMul = 1f;
        private Color stealthFogColor = new Color(0.75f, 0.76f, 0.82f); // 潜行浅影色（SetFogTint 帧传入覆盖）
        private float stealthFogAlpha = 0.35f;

        // F2.4（ch5 双面，docs/13 / ADR-059）：非激活面敌 = 半透明 + 冷色 + 无血条、不可选中
        // （只读层不动模型——速度半速在 BattleModel，血条仅 Boss）。去重同雾态：bool 不变零开销。
        private bool faceDimmed;
        private static readonly Color FaceDimTint = new Color(0.62f, 0.70f, 0.85f); // 冷月色
        private const float FaceDimAlpha = 0.55f;

        // F5.1（ch5 傩面）：三脸染色（0=赤 常态朱砂 / 1=青 / 2=金）——与状态色解耦：
        // 状态色（缓/冻/眩）优先，无状态时用脸color；换脸由 GameManager 事件驱动 SetMaskTint。
        private int maskTint;
        private static readonly Color NuoGreenTint = HexUtil.ToColor("#2F6F86"); // 青面（虚）
        private static readonly Color NuoGoldTint = HexUtil.ToColor("#C9A227");  // 金面（跟随）
        // F5.3：面具翻转演出（字沿 X 轴 1→0→1，0.26s）——不做 Coroutine：叠在 Tick 的缩放回弹链路尾部，
        // 池化复用零残留（maskFlipRemaining 归零即无效果）。
        private const float MaskFlipSeconds = 0.26f;
        private float maskFlipRemaining;

        /// <summary>F5.3（ch5 傩面）：换脸瞬间播「面具翻过去」——X 轴压扁再展开。</summary>
        public void PlayMaskFlip()
        {
            maskFlipRemaining = MaskFlipSeconds;
        }

        /// <summary>在模板上调用一次：搭好字与血条结构，克隆体自动继承。
        /// F1.2 起不再持有 PathSystem——位置从 State.route（敌自己的线路缓存）换算。</summary>
        public void Init(TMP_FontAsset font, Sprite box)
        {
            label = GetComponent<TextMeshPro>();
            if (label == null) label = gameObject.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = NormalFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = HexUtil.ToColor("#B83A2E"); // 朱砂

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(2.4f, 2.4f);

            // 血条：底(淡墨) + 前景(朱红)，挂在字上方；默认隐藏，几何按 Boss 字号(24)配
            var bgGo = new GameObject("HpBg", typeof(SpriteRenderer));
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 1.25f, -0.15f);
            bgGo.transform.localScale = new Vector3(5f, 0.4f, 1f);
            var bg = bgGo.GetComponent<SpriteRenderer>();
            bg.sprite = box;
            bg.color = new Color(0.55f, 0.53f, 0.50f, 0.55f);
            hpBarRoot = bgGo;
            bgGo.SetActive(false);

            var fgGo = new GameObject("HpFg", typeof(SpriteRenderer));
            fgGo.transform.SetParent(bgGo.transform, false);
            fgGo.transform.localPosition = Vector3.zero;
            fgGo.transform.localScale = Vector3.one;
            hpFg = fgGo.GetComponent<SpriteRenderer>();
            hpFg.sprite = box;
            hpFg.color = new Color(0.72f, 0.23f, 0.18f, 0.95f);
        }

        public void Show(EnemyState state)
        {
            State = state;
            releasing = false;
            deathTimer = -1f;
            label.text = WrapName(state.charId); // 多字敌名 2 字换行（召唤野人→2×2）
            float fs = state.isBoss ? BossFontSize : NormalFontSize;
            if (state.charId.Length >= 2) fs *= 0.9f; // 多字缩一档防溢出
            label.fontSize = fs;
            statusTint = 0; // F3.1：染色复位（池化复用残留——新敌无状态，回朱砂）
            maskTint = 0;   // F5.1：傩面脸染色复位（池化复用残留）
            maskFlipRemaining = 0f; // F5.3：翻转演出复位（池化复用残留）
            var c = VermilionBase; c.a = 1f; label.color = c;
            fogged = false; fogDim = 1f; fogAlphaMul = 1f; // F2.1：池化复用复位（进关重算雾态）
            faceDimmed = false; // F2.4：池化复用复位（新敌先按激活面全色——OnEnemySpawned 再按面判定）
            transform.rotation = Quaternion.Euler(0f, 0f, 7f); // 敌字微歪斜
            transform.localScale = Vector3.one;
            if (hpBarRoot != null) hpBarRoot.SetActive(state.isBoss); // 血条仅 Boss
            SetHp(1f);
        }

        /// <summary>多字敌名每 2 字换行（"召唤野人"→"召唤/野人"两行；≤2 字原样）。</summary>
        private static string WrapName(string s)
        {
            if (s.Length <= 2) return s;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i += 2)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(s, i, Mathf.Min(2, s.Length - i));
            }
            return sb.ToString();
        }

        /// <summary>受击反馈：短暂放大。</summary>
        public void FlashHit()
        {
            transform.localScale = Vector3.one * 1.25f;
        }

        private void Update()
        {
            if (State == null || releasing) return;

            // 死亡字散（0.3：先弹后散——首帧 1.45 弹起再缩到 0.55 淡出，替代纯淡出）
            if (deathTimer >= 0f)
            {
                deathTimer -= Time.deltaTime;
                float k = Mathf.Clamp01(deathTimer / 0.3f);
                var c = label.color; c.a = k * (faceDimmed ? FaceDimAlpha : 1f); label.color = c;
                transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.45f, k);
                if (deathTimer <= 0f)
                {
                    releasing = true;
                    if (GameManager.Instance != null) GameManager.Instance.ReleaseEnemy(this);
                }
                return;
            }

            // 位置：从模型路程推算（飞行敌悬空 + 轻微上下浮动）；走敌自己的线路（F1.2 水路敌走水线）
            // F5.1 栖灯 override：枭离路站灯位（弹道/索敌同位——GetEnemyPosition 同口径，视觉所见即战果）
            Vector2 pos = State.perchTower != null ? State.perchTower.position
                : State.route.PointAt != null ? State.route.PointAt(State.dist) : Vector2.zero;
            float fly = State.flying
                ? BattleModel.FlyerOffsetY + Mathf.Sin(Time.time * 3f + GetInstanceID() % 10) * 0.08f
                : 0f;
            transform.position = new Vector3(pos.x, pos.y + fly, -0.4f);

            // 血条 + 缩放回弹
            SetHp((float)(State.hp / State.maxHp));
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, 10f * Time.deltaTime);
            // F5.3（ch5 傩面）：面具翻转——X 轴 |cos| 压扁再展开（叠在回弹之上；归零即无效果）
            if (maskFlipRemaining > 0f)
            {
                maskFlipRemaining -= Time.deltaTime;
                float fk = Mathf.Abs(Mathf.Cos(Mathf.PI * (1f - Mathf.Clamp01(maskFlipRemaining / MaskFlipSeconds))));
                var fsc = transform.localScale;
                transform.localScale = new Vector3(fsc.x * fk, fsc.y, fsc.z);
                if (maskFlipRemaining <= 0f) maskFlipRemaining = 0f;
            }

            // F3.1 状态染色（冻结 > 眩晕 > 缓速 > 常态；去重只在变化时写色）
            int tint = State.frozenRemaining > 0 ? 2
                : State.stunRemaining > 0 ? 3
                : State.slowRemaining > 0 ? 1 : 0;
            if (tint != statusTint)
            {
                statusTint = tint;
                RefreshLabelColor(); // F2.1：状态色与雾系数两层合成（不再直写三色）
            }

            // F5.1 枭红闪（06 §11.4「红色闪烁·看得见打不了」）：雾中每帧正弦 alpha 呼吸
            //（有意每帧写色——其余敌走去重管线；入光/栖灯 litCache true → 本分支停写，
            // 由 F2.2 SetFogTint 链的去重触发刷常态色，切换干净。雾中枭被 IsLit 拦截
            // 永不被命中 → 无状态色可冲突）。枭专属：ch1/2 无枭零路径。
            if (State.charId == "枭" && !State.litCache)
            {
                var c = VermilionBase;
                c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                label.color = c;
            }

            if (State.dead) deathTimer = 0.3f;
            else if (State.reached)
            {
                releasing = true;
                if (GameManager.Instance != null) GameManager.Instance.ReleaseEnemy(this);
            }
        }

        /// <summary>
        /// F2.1/F2.2 雾态切换（GameManager 每帧驱动，内部去重——bool 不变零开销）：入光 = 全色
        /// 现形；雾中普通敌 = 深色半透明剪影（状态基色 × dim × alphaMul）；雾中潜行敌（State.stealth，
        /// 蛾蝠猫）= 褤色幽灵浅影（独立色 + 更透，覆盖状态色——潜行优先级最高）。参数全部来自
        /// GameParams（11.8 曲线②用户调参位）。
        /// </summary>
        public void SetFogTint(bool lit, float dim, float alphaMul, Color stealthColor, float stealthAlpha)
        {
            stealthFogColor = stealthColor;
            stealthFogAlpha = stealthAlpha;
            bool f = !lit;
            if (f == fogged) return;
            fogged = f;
            fogDim = f ? dim : 1f;
            fogAlphaMul = f ? alphaMul : 1f;
            RefreshLabelColor();
        }

        /// <summary>
        /// F2.4（ch5 双面）：非激活面切换（GameManager 换面/生成时驱动，内部去重）。半透明冷色 +
        /// 关血条（Boss 也只留当前面可见）；位置/推进照常由模型算（半速在 BattleModel）。
        /// </summary>
        public void SetFaceDim(bool dim)
        {
            if (dim == faceDimmed) return;
            faceDimmed = dim;
            RefreshLabelColor();
            if (hpBarRoot != null) hpBarRoot.SetActive(!dim && State != null && State.isBoss);
        }

        /// <summary>
        /// F5.1（ch5 傩面）：三脸染色（0=赤/1=青/2=金）。与状态色/雾/面暗三层解耦——状态色优先级
        /// 更高（暖/冻/眩照常盖脸），无状态时才用脸color。GameManager 在 NuoMaskChanged 事件里驱动。
        /// </summary>
        public void SetMaskTint(int mask)
        {
            mask = mask == 1 ? 1 : (mask == 2 ? 2 : 0);
            if (mask == maskTint) return;
            maskTint = mask;
            RefreshLabelColor();
        }

        /// <summary>
        /// 显示色 = 状态基色（冻>眩>缓>朱）× 雾系数；潜行敌雾中走浅影色（独立分支——F2.2 三级
        /// 的第二级；枭红闪 F5 在此加 time 闪变分支即第三级）。F2.4 非激活面再乘冷色 ×0.55 alpha。
        /// 死亡淡出（直改 alpha）其后衔接。
        /// </summary>
        private void RefreshLabelColor()
        {
            Color c;
            if (fogged && State != null && State.stealth)
            {
                c = stealthFogColor;
                c.a *= stealthFogAlpha;
            }
            else
            {
                c = statusTint == 2 ? FrozenTint
                    : statusTint == 3 ? StunTint
                    : statusTint == 1 ? SlowTint
                    : maskTint == 1 ? NuoGreenTint
                    : maskTint == 2 ? NuoGoldTint : VermilionBase;
                c.r *= fogDim; c.g *= fogDim; c.b *= fogDim; c.a *= fogAlphaMul;
            }
            if (faceDimmed) // F2.4：非激活面（冷色 + 半透明——「看得见，不在这一面」）
            {
                c.r *= FaceDimTint.r; c.g *= FaceDimTint.g; c.b *= FaceDimTint.b;
                c.a *= FaceDimAlpha;
            }
            label.color = c;
        }

        private void SetHp(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (hpFg != null)
            {
                hpFg.transform.localScale = new Vector3(ratio, 1f, 1f);
                hpFg.transform.localPosition = new Vector3(-0.5f * (1f - ratio), 0f, -0.01f);
            }
        }

        public void ClearState()
        {
            State = null;
        }
    }

    /// <summary>常用色值转换（#RRGGBB → Color）。</summary>
    public static class HexUtil
    {
        public static Color ToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.magenta;
        }
    }
}
