using System;
using TMPro;
using UnityEngine;

namespace WordWars
{
    /// <summary>教学动作事件（GameManager 各操作成功后上报；L0 专用，ADR-030）。</summary>
    public enum TutorialEvent
    {
        Draw,   // 抽卡成功
        Sell,   // 卖出成功（拿起塔 / 手牌卡两路）
        Place,  // 手牌字卡 → 空地放置成功
        Merge,  // 升阶成功（卡上塔 / 塔上塔两路）
        Move,   // 塔移动成功
        Wave,   // 提前召唤下一波（教学收尾 = 开战）
    }

    /// <summary>
    /// L0 教学引导（ADR-030，用户需求 2026-10-02：抽卡/卖卡/放置/合成/移动五项 + 点「下一波」开战收尾）。
    /// 世界空间气泡 = 宣纸底 + 浓墨 7 号单行字（与飘字同字号口径，用户 2026-10-02 定档），
    /// 停在手牌条与底行格子之间的空带（不遮按钮也不遮棋盘）；正弦摇曳引眼。
    /// 纯渲染件无碰撞体 → 不挡按钮/格子点击。
    /// 推进 = 事件上报：当前步匹配即前进；先做了后面的动作 → 该步记完成，序列到达时自动跳过
    /// （乱序操作不卡死）；六步全完成收起气泡（开战即隐，战场干净）。
    /// 锚点由 GameManager 传入（按钮世界位运行时可查，惰性求值）。
    /// 骨架在 Init（激活态）一次建好规避 TMP Awake 坑（W1，03 §2）；Begin/End 只做显隐与步骤复位。
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        private struct Step
        {
            public TutorialEvent evt;
            public string text;
        }

        // 步骤序 = 用户口径（抽→卖→放→合→移），末步教「下一波」开战。
        // 文案单行 ≤10 字（7 号 ×0.1 ≈ 7.0 单位，排版框 7.4 兜住）
        private static readonly Step[] Steps =
        {
            new Step { evt = TutorialEvent.Draw,  text = "点「抽」获得手牌" },
            new Step { evt = TutorialEvent.Sell,  text = "选手牌，点「卖」进行售卖" },
            new Step { evt = TutorialEvent.Place, text = "选手牌，点地图塔防格放置" },
            new Step { evt = TutorialEvent.Merge, text = "两个相同塔防可以进行合成" },
            new Step { evt = TutorialEvent.Move,  text = "点塔拿起，点地图塔防格移动" },
            new Step { evt = TutorialEvent.Wave,  text = "点右下角「下一波」迎敌！" },
        };

        private readonly bool[] done = new bool[Steps.Length];
        private Func<Vector3>[] anchors;
        private int index = -1;        // -1 = 未在教学中（气泡隐藏）
        private Vector3 anchorPos;
        private float bobClock;
        private float tipTimer = -1f;  // F4.6：提示模式倒计时（-1 = 无；与 index 互斥）

        private SpriteRenderer bg;
        private TextMeshPro label;

        /// <summary>建骨架（激活态一次建好；font/white 由 GameManager 注入）。</summary>
        public void Init(TMP_FontAsset font, Sprite white)
        {
            var bgGo = new GameObject("BubbleBg", typeof(SpriteRenderer));
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localPosition = Vector3.zero;
            bgGo.transform.localScale = new Vector3(7.6f, 1.0f, 1f);
            bg = bgGo.GetComponent<SpriteRenderer>();
            bg.sprite = white;
            var paper = HexUtil.ToColor("#F7F2E7"); // 宣纸（同格子虚框内衬）
            paper.a = 0.96f;
            bg.color = paper;

            var labelGo = new GameObject("BubbleText", typeof(RectTransform), typeof(TextMeshPro));
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.05f); // 字在纸前
            label = labelGo.GetComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 7;
            label.alignment = TextAlignmentOptions.Center;
            label.color = HexUtil.ToColor("#26231F"); // 浓墨
            var rt = (RectTransform)labelGo.transform;
            rt.sizeDelta = new Vector2(7.4f, 0.9f);

            gameObject.SetActive(false); // 骨架建好才许隐藏（TMP Awake 只在激活时跑，W1）
        }

        /// <summary>进 L0 开教。stepAnchors：每步一个锚点（世界坐标，z 由调用方给出）。</summary>
        public void Begin(Func<Vector3>[] stepAnchors)
        {
            anchors = stepAnchors;
            for (int i = 0; i < done.Length; i++) done[i] = false;
            index = 0;
            tipTimer = -1f; // F4.6：进 L0 清提示模式（两模式互斥）
            ShowStep(0);
        }

        /// <summary>离关 / 教学完成：收气泡并复位（重进教学关即重教）。</summary>
        public void End()
        {
            index = -1;
            tipTimer = -1f; // F4.6：提示也一并收
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>
        /// F4.6（ADR-031 / 06 §10.5）：ch2 一次性事件气泡——不做 ch2 版 L0，关键时刻点拨。
        /// 复用 L0 气泡皮（宣纸底 + 浓墨 7 号 + 摇曳），倒计时自收；与步骤序列互斥。
        /// 锚点 = 手牌条上空带（同 L0 兜底位，不遮按钮/棋盘）。
        /// </summary>
        public void ShowTip(string text, float seconds = 6f)
        {
            index = -1; // 防御：提示模式不吃步骤事件
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            label.text = text;
            anchorPos = new Vector3(0f, -3.2f, -2f);
            bobClock = 0f;
            tipTimer = seconds;
        }

        /// <summary>动作成功上报：当前步匹配前进；后面的同型步也记完成（乱序跳过不卡死）。</summary>
        public void Notify(TutorialEvent evt)
        {
            if (index < 0) return;
            for (int i = index; i < Steps.Length; i++)
                if (Steps[i].evt == evt) done[i] = true;
            while (index < Steps.Length && done[index]) index++;
            if (index >= Steps.Length)
            {
                End(); // 六步全完成：末步开战即隐身，不挡战斗视野
                return;
            }
            ShowStep(index);
        }

        private void ShowStep(int i)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            label.text = Steps[i].text;
            anchorPos = anchors != null && i < anchors.Length && anchors[i] != null
                ? anchors[i]()
                : new Vector3(0f, -3.2f, -2f);
            bobClock = 0f;
        }

        private void Update()
        {
            // F4.6 提示模式：倒计时自收（早退——不吃步骤推进逻辑）
            if (tipTimer >= 0f)
            {
                tipTimer -= Time.deltaTime;
                if (tipTimer < 0f)
                {
                    gameObject.SetActive(false);
                    return;
                }
                bobClock += Time.deltaTime;
                transform.position = anchorPos + Vector3.up * (Mathf.Sin(bobClock * 2.2f) * 0.05f);
                return;
            }

            if (index < 0) return;
            bobClock += Time.deltaTime;
            // 慢频轻摇曳引眼（±0.05，不越出空带）；每帧以锚点为基准重算，防坐标漂移
            transform.position = anchorPos + Vector3.up * (Mathf.Sin(bobClock * 2.2f) * 0.05f);
        }
    }
}
