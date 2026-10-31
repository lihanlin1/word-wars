using System.Collections;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 塔字视图：端正字。
    /// v3.0 升阶色阶（用户需求 2026-10-02，替代字号阶梯 10+3/级，ADR-026）：
    /// 字号恒定（单字 10），阶 = 颜色分层 黑→绿→蓝→紫→金（L1-L5）。
    /// 多字分支为词塔时代遗留（v3.0 已删合成，恒 15 字号走同一色阶）。
    /// </summary>
    public class TowerView : MonoBehaviour
    {
        // [SerializeField]：对象池克隆需要复制这些引用（W2a 坑③）
        [SerializeField] private TextMeshPro label;
        [SerializeField] private SpriteRenderer halo;
        [SerializeField] private TextMeshPro countdown; // F2.2 沉默倒计时（头顶小字）
        [SerializeField] private TextMeshPro soulText;  // F3.4 幡：魂计数（右上角小字 n/10）
        [SerializeField] private TextMeshPro mergeText; // F1.1（docs/16）：合并结果等级预览（可配对塔「→L3」）
        [SerializeField] private TextMeshPro sleepText; // F4.2（ch5 魇）：入梦标记（左上「梦」）

        private static readonly Color InkColor = HexUtil.ToColor("#26231F");   // 浓墨
        private static readonly Color Vermilion = HexUtil.ToColor("#B83A2E"); // 朱
        private static readonly Color GraySilence = HexUtil.ToColor("#9A968E"); // F2.2 沉默灰显
        private static readonly Color SleepInk = HexUtil.ToColor("#4A6C8C");    // F4.2 入梦小字（冷靛）
        private static readonly Color SleepChar = new Color(0.62f, 0.68f, 0.78f, 1f); // F4.2 入梦字面（蒙一层冷灰蓝）

        private int shownLevel = 1; // 灰显恢复用（ClearSilence 回阶色）
        private bool lifted;        // 拿起态（半透明）：沉默解除时保 alpha 一致
        private bool ghosted;       // F3.1 镜像（空心描边字）
        private bool ghostFaint;    //   投影态（不在当前面）：描边更细更淡
        private bool hollowOn;      //   材质已切到空心（只用于复原，避免普通塔建材质实例）

        // 升阶色阶（ADR-026，用户 2026-10-02 定稿）：黑→绿→蓝→紫→金，L1-L5。
        // 浓墨起手（=基础塔观感不变）、墨金封顶；取值对宣纸底 #F7F2E7 对比度可读。
        private static readonly Color[] TierColors =
        {
            HexUtil.ToColor("#26231F"), // L1 黑（浓墨）
            HexUtil.ToColor("#4F7A45"), // L2 绿
            HexUtil.ToColor("#3E6B8C"), // L3 蓝（青，同矛弹道）
            HexUtil.ToColor("#6E5594"), // L4 紫
            HexUtil.ToColor("#A67C1B"), // L5 金（墨金）
            HexUtil.ToColor("#B83A2E"), // L6 红（6 阶：困难模式奖励解锁）
        };

        /// <summary>在模板上调用一次。</summary>
        public void Init(TMP_FontAsset font, Sprite white)
        {
            label = GetComponent<TextMeshPro>();
            if (label == null) label = gameObject.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 10;
            label.alignment = TextAlignmentOptions.Center;
            label.color = InkColor;

            // 合法目标高亮光环（垫在字后、格前）；克隆复制引用故必须 [SerializeField]
            var haloGo = new GameObject("Halo", typeof(SpriteRenderer));
            haloGo.transform.SetParent(transform, false);
            haloGo.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            haloGo.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
            halo = haloGo.GetComponent<SpriteRenderer>();
            halo.sprite = white;
            halo.color = new Color(Vermilion.r, Vermilion.g, Vermilion.b, 0.45f);
            halo.enabled = false;

            // F2.2 沉默倒计时（头顶小字，字号 6）：被淹灰显 + 秒数倒数；干态隐藏。
            // 克隆复制引用故 [SerializeField]（同 halo 坑③）。
            var cdGo = new GameObject("Countdown", typeof(TextMeshPro));
            cdGo.transform.SetParent(transform, false);
            cdGo.transform.localPosition = new Vector3(0f, 0.72f, -0.45f);
            countdown = cdGo.GetComponent<TextMeshPro>();
            countdown.font = font;
            countdown.fontSize = 6;
            countdown.alignment = TextAlignmentOptions.Center;
            countdown.color = Vermilion;
            countdown.gameObject.SetActive(false);

            // F1.1（docs/16）：合并结果等级预览（可配对塔下方「→L3」；非配对时隐藏）
            var mgGo = new GameObject("MergePreview", typeof(TextMeshPro));
            mgGo.transform.SetParent(transform, false);
            mgGo.transform.localPosition = new Vector3(0f, -0.72f, -0.45f);
            mergeText = mgGo.GetComponent<TextMeshPro>();
            mergeText.font = font;
            mergeText.fontSize = 5f;
            mergeText.alignment = TextAlignmentOptions.Center;
            mergeText.color = HexUtil.ToColor("#A67C1B");
            mergeText.enableWordWrapping = false;
            mergeText.gameObject.SetActive(false);

            // F3.4（ch5 幡）：右上角魂计数小字（n/10）。非幡塔恒隐藏（Show 复位），
            // 刷新由 GameManager 每帧按 model.Souls 广播（同一关两面共用一份魂）。
            var soulGo = new GameObject("Souls", typeof(TextMeshPro));
            soulGo.transform.SetParent(transform, false);
            soulText = soulGo.GetComponent<TextMeshPro>();
            soulText.font = font;
            soulText.fontSize = 4.4f;
            soulText.alignment = TextAlignmentOptions.Right;
            soulText.enableWordWrapping = false; // 单行：「3/10」不许折行
            soulText.color = HexUtil.ToColor("#2B4C7E"); // 蓝墨（同 HUD 墨色——一眼归到「墨经济」）
            // ★定位必须连 RectTransform 一起钉：`Right` 对齐 = **在本框内右对齐**，
            // 只改 localPosition 而框宽是默认 100 → 字跑到几十格开外（用户 2026-10-22 实测：
            // 跑到第 10 格右上角）。故锚点取格子中心、pivot 取右中、框宽 1.2 →
            // 文字右缘恰好钉在 (0.78, 0.55)，向左最多占 1.2 格宽（不出格）。
            var soulRt = soulText.rectTransform;
            soulRt.anchorMin = soulRt.anchorMax = new Vector2(0.5f, 0.5f);
            soulRt.pivot = new Vector2(1f, 0.5f);
            soulRt.sizeDelta = new Vector2(1.2f, 0.5f);
            soulRt.anchoredPosition = new Vector2(0.78f, 0.55f);
            soulText.text = "";
            soulText.gameObject.SetActive(false);

            // F4.2（ch5 魇）：入梦标记——左上角一个冷蓝「梦」字（独立槽，不跟淹没倒计时抢；
            // 两者可同时存在，语义不同）。只由 GameManager 轮询开/关。
            var slGo = new GameObject("Sleep", typeof(TextMeshPro));
            slGo.transform.SetParent(transform, false);
            slGo.transform.localPosition = new Vector3(-0.52f, 0.64f, -0.45f);
            sleepText = slGo.GetComponent<TextMeshPro>();
            sleepText.font = font;
            sleepText.fontSize = 4.6f;
            sleepText.alignment = TextAlignmentOptions.Center;
            sleepText.color = SleepInk;
            sleepText.text = "梦";
            sleepText.gameObject.SetActive(false);

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(1.6f, 1.6f);
        }

        public void Show(string charId, int level = 1)
        {
            shownLevel = level;
            ghosted = false; // F3.1：池化复用/换面重显 → 先复位镜像态（GM 每帧按面再设）
            ghostFaint = false;
            label.text = charId;
            // 升阶色阶（ADR-026）：字号恒定，阶 = 颜色（黑绿蓝紫金）；多字 = 词塔遗留分支
            bool single = charId.Length == 1;
            label.fontSize = single ? 10f : 15f;
            ApplyLabelColor(TierColor(level));

            var rt = (RectTransform)transform;
            rt.sizeDelta = single ? new Vector2(1.6f, 1.6f) : new Vector2(2.4f, 1.6f);

            // 复位（池化复用 & 拿起态残留）
            rt.localPosition = new Vector3(0f, 0f, -0.35f);
            rt.localScale = Vector3.one;
            SetHighlight(false);
            HideSouls();    // F3.4：池化复用 → 魂计数先藏（幡由 GM 每帧再开）
            ClearAsleep();  // F4.2：入梦标记复位（池化复用残留）
            HideMergePreview(); // F1.1：合并预览复位（池化复用残留）
            ClearSealed();  // F5.2：封印标记复位（池化复用残留）
            ClearSilence(); // 显塔即干态（池化复用残留清零）
        }

        /// <summary>升阶颜色（1..6 钳制；异常入参兜底走阶首/阶尾）。P5 结算画面复用。</summary>
        public static Color TierColor(int level)
        {
            if (level < 1) level = 1;
            if (level > TierColors.Length) level = TierColors.Length;
            return TierColors[level - 1];
        }

        /// <summary>
        /// F3.1（ch5 镜）：镜像画法 = **空心描边字**（字面全透明 + 描边）——
        /// 一眼读出「这是枚镜子，不是本体」。**描边色 = 阶色**（L1 黑 / L2 绿 / L3 蓝 / L4 紫 / L5 金，
        /// 镜像阶 = 本体阶，配色由 `SetShownLevel` 同步）。
        /// 两种描边档：
        ///   · 镜像在**当前面**（满显）= 描边 0.15 / 阶色不透明；
        ///   · 镜像在**另一面**（投影，与本体重叠）= 描边 0.10 / 阶色 55%，且抬到本体的前一层
        ///     （z 更靠相机）——这样描边压在本体字上，看得见一圈同色轮廓。
        /// 实现要点：TMP 的描边是 Shader 关键字 `OUTLINE_ON` + `_OutlineWidth`/`_OutlineColor`；
        /// 只写 `label.outlineWidth` **不会开关键字**（这正是上一版「看不出描边」的原因）。
        /// 面透明走材质 `_FaceColor.a = 0`（顶点色 alpha 归零会把描边一起乘没）。
        /// **打包注意**：`OUTLINE_ON` 是 `shader_feature` —— 变体只随「构建里有材质用到它」保留，
        /// 故 `Assets/_Project/Resources/KaiTi_SDF_Outline.mat`（同 shader + 关键字预置）是**变体锚**，
        /// 别删；否则 Player 包里镜像会变成「面透明 + 没描边」= 整个字看不见（Editor 不受影响）。
        /// </summary>
        private const string TmpOutlineKeyword = "OUTLINE_ON";
        private const float GhostOutlineWidth = 0.15f;      // 用户 2026-10-22：再细一档（0.20 → 0.15）
        private const float GhostOutlineWidthFaint = 0.10f; // 投影档同步收细（0.13 → 0.10）

        /// <summary>落在字上的颜色（唯一出口）：ghosted = 空心描边（面 alpha 归零），否则实心。
        /// 只在真用到描边时才碰材质（`hollowOn` 闸）——普通塔零材质实例（ch1-4 零变化）。</summary>
        private void ApplyLabelColor(Color c)
        {
            if (ghosted)
            {
                var mat = label.fontMaterial; // TMP 按实例复制材质：本塔的字不影响别的塔
                if (mat != null)
                {
                    var ink = c;                              // 描边 = 阶色（黑/绿/蓝/紫/金；沉默 = 灰）
                    ink.a = ghostFaint ? 0.55f : 1f;          // 投影档淡一档
                    mat.EnableKeyword(TmpOutlineKeyword);
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, ghostFaint ? GhostOutlineWidthFaint : GhostOutlineWidth);
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, ink);
                    var face = mat.GetColor(ShaderUtilities.ID_FaceColor);
                    face.a = 0f; // 面全透明 → 只剩描边（顶点 alpha 不能归零：描边也吃顶点 alpha）
                    mat.SetColor(ShaderUtilities.ID_FaceColor, face);
                    hollowOn = true;
                }
                label.color = Color.white; // 顶点色（rgb 乘面、a 乘描边）→ 须纯白
                return;
            }

            if (hollowOn)
            {
                var mat = label.fontMaterial;
                if (mat != null)
                {
                    mat.DisableKeyword(TmpOutlineKeyword); // 回实心：关关键字 + 面 alpha 复原（幂等）
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
                    var face = mat.GetColor(ShaderUtilities.ID_FaceColor);
                    face.a = 1f;
                    mat.SetColor(ShaderUtilities.ID_FaceColor, face);
                }
                hollowOn = false;
            }
            label.color = c;
        }

        /// <summary>基色 = 沉默灰 / 阶色，拿起态半透明。</summary>
        private Color BaseColor()
        {
            var c = Silenced ? GraySilence : TierColor(shownLevel);
            if (lifted) c.a = 0.75f;
            return c;
        }

        /// <summary>
        /// F3.1（ch5 镜）：镜像**配色**跟随阶数（L1 黑 → L5 金）——镜像阶 = 本体阶（数值同步，见
        /// `BattleModel.SyncMirror`），这里只管把字色/描边改到对应阶。幂等（每帧路径会调，阶没变直接返回）。
        /// </summary>
        public void SetShownLevel(int level)
        {
            if (shownLevel == level) return;
            shownLevel = level;
            ApplyLabelColor(BaseColor());
        }

        /// <summary>
        /// F3.1（ch5 镜）：镜像 = 空心描边（**两面都是**——切到它那面也认得出是镜子）；
        /// faint = 它在另一面（投影档：更细更淡 + 压在本体前一层）。幂等（每帧路径会调）。
        /// </summary>
        public void SetGhost(bool on, bool faint = false)
        {
            if (ghosted == on && ghostFaint == faint) return;
            ghosted = on;
            ghostFaint = on && faint;
            if (on) SetHighlight(false); // 描边态不带朱色光环
            // 投影档抬到本体前面（z 小 = 更近相机；TMP 走 Transparent 队列按距离排序），
            // 否则同格同 z 谁先画不确定，描边可能被本体盖住。
            if (!lifted)
            {
                var rt = (RectTransform)transform;
                rt.localPosition = new Vector3(0f, 0f, ghostFaint ? -0.46f : -0.35f);
            }
            ApplyLabelColor(BaseColor());
        }

        /// <summary>拿起态（移动/合并中）：微放大上浮，半透明提示「手持中」。</summary>
        public void SetLifted(bool on)
        {
            lifted = on;
            var rt = (RectTransform)transform;
            rt.localScale = on ? Vector3.one * 1.18f : Vector3.one;
            rt.localPosition = on
                ? new Vector3(0f, 0.3f, -0.5f)
                : new Vector3(0f, 0f, -0.35f);
            ApplyLabelColor(BaseColor());
        }

        /// <summary>成为当前操作的合法目标（发现性规范 03 §4.3）：朱色光环。</summary>
        public void SetHighlight(bool on)
        {
            if (halo != null) halo.enabled = on;
        }

        /// <summary>F1.1（docs/16）：合并结果等级预览（可配对塔显示「→L3」）。</summary>
        public void ShowMergePreview(int resultLevel)
        {
            if (mergeText == null) return;
            mergeText.text = "→L" + resultLevel;
            mergeText.gameObject.SetActive(true);
        }

        public void HideMergePreview()
        {
            if (mergeText != null) mergeText.gameObject.SetActive(false);
        }

        // ============ F3.4 幡：魂计数（右上角小字） ============

        /// <summary>右上角魂进度「n/10」（只有幡显示；值域 = 本关总魂 % 每结数）。</summary>
        public void ShowSouls(int remain, int per)
        {
            if (soulText == null) return;
            if (!soulText.gameObject.activeSelf) soulText.gameObject.SetActive(true);
            string s = remain + "/" + per;
            if (soulText.text != s) soulText.text = s; // 帧间去重（TMP 改字会 SetVerticesDirty）
        }

        public void HideSouls()
        {
            if (soulText == null) return;
            if (soulText.gameObject.activeSelf) soulText.gameObject.SetActive(false);
        }

        // ============ F2.2 淹没沉默视觉（ADR-032 / GDD §3.4） ============

        /// <summary>沉默灰显中（GameManager 轮询驱动 ShowSilence/ClearSilence）。</summary>
        public bool Silenced { get; private set; }
        private bool lockCountdown; // F5.1 枭栖灯锁定：倒计时显示中（与沉默互斥自理——谁后刷谁显示，原型期边缘备案）

        /// <summary>被淹沉默：灰显 + 头顶倒计时秒数（0.1s 节流刷新，取整显示）。</summary>
        public void ShowSilence(double secondsLeft)
        {
            Silenced = true;
            ApplyLabelColor(GraySilence);
            if (countdown != null)
            {
                countdown.gameObject.SetActive(true);
                countdown.text = Mathf.Max(0, Mathf.CeilToInt((float)secondsLeft)).ToString();
            }
        }

        /// <summary>塔干：解除灰显回阶色（保留拿起半透明），倒计时隐藏。</summary>
        public void ClearSilence()
        {
            if (!Silenced && (countdown == null || !countdown.gameObject.activeSelf)) return; // 幂等
            Silenced = false;
            ApplyLabelColor(Asleep ? SleepChar : BaseColor()); // 入梦中被淹解除 → 回梦色（F4.2）
            if (countdown != null) countdown.gameObject.SetActive(false);
        }

        /// <summary>F5.1 枭栖灯锁定：灯保持点亮（塔字不灰——06 §11.4 杀窗口径）+ 头顶朱色
        /// 倒计时秒数（复用沉默小字；吞噬倒计时——秒尽灯消）。锁定与沉默并存时后刷者显示
        ///（原型期边缘 case 备案：锁灯被淹的显示竞争无功能影响）。</summary>
        public void ShowLockCountdown(double secondsLeft)
        {
            lockCountdown = true;
            if (countdown != null)
            {
                countdown.gameObject.SetActive(true);
                countdown.text = Mathf.Max(0, Mathf.CeilToInt((float)secondsLeft)).ToString();
            }
        }

        /// <summary>F5.1 解除锁定倒计时（灯被吞噬前外因移除等）——沉默态的倒计时由沉默管线自理。</summary>
        public void ClearLockCountdown()
        {
            if (!lockCountdown) return;
            lockCountdown = false;
            if (countdown != null && !Silenced) countdown.gameObject.SetActive(false);
        }

        // ============ F4.2（ch5 魇）入梦视觉：字面蒙冷灰蓝 + 左上「梦」字 ============

        /// <summary>入梦中（GameManager 轮询驱动 ShowAsleep/ClearAsleep；沉默优先——见 RefreshTowerSilence）。</summary>
        public bool Asleep { get; private set; }

        /// <summary>入梦：停火（模型侧 sleepRemaining 闸），表现 = 字面蒙冷灰蓝 + 左上「梦」字。</summary>
        public void ShowAsleep()
        {
            if (Asleep) return;
            Asleep = true;
            ApplyLabelColor(Silenced ? GraySilence : SleepChar);
            if (sleepText != null) sleepText.gameObject.SetActive(true);
        }

        /// <summary>醒来：回阶色（沉默中则留给沉默管线），藏「梦」字。幂等。</summary>
        public void ClearAsleep()
        {
            if (!Asleep) return;
            Asleep = false;
            if (sleepText != null && !Sealed) sleepText.gameObject.SetActive(false);
            if (!Silenced && !Sealed) ApplyLabelColor(BaseColor());
        }

        // ============ F5.2（ch5 傩面金脸）封印视觉：字面蒙琥珀金 + 左上「封」字 ============

        /// <summary>封印中（GameManager 轮询驱动；优先级：沉默 &gt; 封印 &gt; 入梦——见 RefreshTowerSilence）。</summary>
        public bool Sealed { get; private set; }
        private static readonly Color SealChar = new Color(0.79f, 0.63f, 0.15f, 1f); // 琥珀金（与眩晕同色相、不同语义位）

        /// <summary>封印：停火（模型侧 sealRemaining 闸），表现 = 字面蒙琥珀金 + 左上「封」字。</summary>
        public void ShowSealed()
        {
            if (Sealed) return;
            Sealed = true;
            Asleep = false;                     // 同一枚标记位：封印优先显示
            ApplyLabelColor(Silenced ? GraySilence : SealChar);
            if (sleepText != null)
            {
                sleepText.text = "封";
                sleepText.color = SealChar;
                sleepText.gameObject.SetActive(true);
            }
        }

        /// <summary>解封：标记位还原「梦」字并藏起（入梦若还在，下一帧 ShowAsleep 会重亮）。幂等。</summary>
        public void ClearSealed()
        {
            if (!Sealed) return;
            Sealed = false;
            if (sleepText != null)
            {
                sleepText.text = "梦";
                sleepText.color = SleepInk;
                sleepText.gameObject.SetActive(false);
            }
            if (!Silenced && !Asleep) ApplyLabelColor(BaseColor());
        }

        // ============ 0.3 Juice：合并色跳强调 ============

        private Coroutine popRoutine;

        /// <summary>合并/升阶亮相弹跳（0.25s 正弦半波 1→1.45→1）：新阶颜色跳变更「看得见」。</summary>
        public void Pop()
        {
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(PopRoutine());
        }

        private IEnumerator PopRoutine()
        {
            const float dur = 0.25f;
            float t = 0f;
            var rt = (RectTransform)transform;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                rt.localScale = Vector3.one * (1f + 0.45f * Mathf.Sin(Mathf.PI * k));
                yield return null;
            }
            rt.localScale = Vector3.one;
            popRoutine = null;
        }
    }
}
