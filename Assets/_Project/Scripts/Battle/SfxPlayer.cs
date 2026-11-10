using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 战斗音效播放器（阶段 E 起步，ADR-028/029）。
    /// 单发声道 ×4 轮转 PlayOneShot：多塔同帧开火不挤占；每发 ±6% 音调抖动防「同音机关枪感」
    /// （抖动写在该声道本次播放前，声道复用间隔 ≥4 发，正常听不出尾音被带调）。
    /// 循环声道 ×1（独立 AudioSource，loop）：铲开垦挖土——引用计数管理
    /// （多块地同挖只放一路声；计数归零即停；场重建强停归零，见 FarmReset）。
    /// 槽位空 = 静音跳过；cfg 为空（未自愈）= 全静音——不接也能跑。
    /// 音源走 GameObject 自带 AudioSource；AudioListener 在主相机（默认）。
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        private const int Channels = 4;

        private readonly AudioSource[] sources = new AudioSource[Channels];
        private AudioSource loopSource; // 铲开垦循环（ADR-029）
        private AudioSource bgmSource;  // 章级 BGM 循环（每章专属 + Boss 切轨，用户 2026-10-09）
        private int nextChannel;
        private int farmCount;          // 在挖地块数（引用计数）
        private SoundConfig cfg;
        private bool sfxEnabled = true; // 存档 settings.sfxOn（用户 2026-10-26：补消费点）
        private static SfxPlayer current; // 全局 UI 音转发（按钮/卡片不在 GameManager 引用链上）

        /// <summary>GameManager 建模板时调用一次；cfg 可空。</summary>
        public void Init(SoundConfig config)
        {
            cfg = config;
            current = this;
            for (int i = 0; i < Channels; i++)
            {
                if (sources[i] != null) continue;
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].loop = false;
            }
            if (loopSource == null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.loop = true; // 循环声道：播到显式 Stop 为止
            }
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true; // BGM 循环声道：切轨即换 clip
            }
        }

        /// <summary>
        /// BGM 切轨（用户 2026-10-09：每章 bgm + bossBgm 槽，挂 ChapterConfig）。
        /// clip 空 = 停播静音；**同 clip 在播 = 幂等跳过**（轮询每 0.1s 调，防抖动重启）；
        /// 音量走 SoundConfig.bgmVolume 独立分轨（bgmOn 存档开关由 GameManager 侧判定）。
        /// </summary>
        public void PlayBgm(AudioClip clip)
        {
            if (bgmSource == null) return;
            if (clip == null) { StopBgm(); return; }
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;
            bgmSource.clip = clip;
            bgmSource.volume = cfg != null ? cfg.bgmVolume : 0.5f;
            bgmSource.pitch = 1f;
            bgmSource.Play();
        }

        /// <summary>停 BGM（Boss 死切回空槽章 = 静音；进关重切由 GameManager 驱动）。</summary>
        public void StopBgm()
        {
            if (bgmSource != null && bgmSource.isPlaying) bgmSource.Stop();
        }

        /// <summary>开垦开始（铲落地）：引用计数；首块起循环，多块同挖不叠加音量。</summary>
        public void FarmStart()
        {
            farmCount++;
            if (farmCount == 1 && sfxEnabled && cfg != null && cfg.shovelLoop != null)
            {
                loopSource.clip = cfg.shovelLoop;
                loopSource.volume = cfg.volume;
                loopSource.pitch = 1f; // 循环声不做音调抖动（会听着走调）
                loopSource.Play();
            }
        }

        /// <summary>单块开垦完成（正常计时/加速同一条完成路，都走这里）。</summary>
        public void FarmStop()
        {
            farmCount--;
            if (farmCount <= 0) FarmReset();
        }

        /// <summary>强制停（切关/回滚场重建：开垦中格子全毁、计数不可信，直接归零）。</summary>
        public void FarmReset()
        {
            farmCount = 0;
            if (loopSource != null && loopSource.isPlaying) loopSource.Stop();
        }

        /// <summary>
        /// 塔开火（发射/挥击瞬间）：刀棍戟=挥击、弓矛=箭啸、弩=重箭；其余（墨/铲不开火）静音。
        /// </summary>
        public void PlayAttack(string charId)
        {
            if (cfg == null) return;
            switch (charId)
            {
                case "刀":
                case "棍":
                case "戟":
                    Play(cfg.meleeAttack);
                    break;
                case "弓":
                case "矛":
                    Play(cfg.bowShot);
                    break;
                case "弩":
                    Play(cfg.crossbowShot);
                    break;
            }
        }

        /// <summary>敌人受击（近战命中/弹道命中通用）。</summary>
        public void PlayHit()
        {
            if (cfg == null) return;
            Play(cfg.enemyHit);
        }

        /// <summary>蛟蓄水怒吼（转阶段蓄水窗起点；Boss 通用怒槽——枭后续可复用）。</summary>
        public void PlayBossRoar()
        {
            if (cfg == null) return;
            Play(cfg.bossRoar);
        }

        /// <summary>洪水落地（水线 4 全图涌入）。</summary>
        public void PlayFloodSurge()
        {
            if (cfg == null) return;
            Play(cfg.floodSurge);
        }

        /// <summary>潮落退水（4→0 档扫掠起点）。</summary>
        public void PlayFloodEbb()
        {
            if (cfg == null) return;
            Play(cfg.floodEbb);
        }

        /// <summary>岩浆落地（F2.1 追加，用户 2026-10-12 点题）：红闪结束瞬间——每批一声
        ///（同批 N 格同刻落地不叠放；槽位空 = 静音跳过，bossRoar 同惯例）。</summary>
        public void PlayLavaLand()
        {
            if (cfg == null) return;
            Play(cfg.lavaLand);
        }

        /// <summary>F5.1（ch5 傩面）换脸落地（面具翻转完成瞬间）。</summary>
        public void PlayNuoMaskChange()
        {
            if (cfg == null) return;
            Play(cfg.nuoMaskChange);
        }

        /// <summary>F5.2（ch5 傩面赤脸）劈塔（毁塔不毁格）。</summary>
        public void PlayNuoSmash()
        {
            if (cfg == null) return;
            Play(cfg.nuoSmash);
        }

        /// <summary>F5.2（ch5 傩面金脸）封印落地。</summary>
        public void PlayNuoSeal()
        {
            if (cfg == null) return;
            Play(cfg.nuoSeal);
        }

        /// <summary>存档设置：音效开关（false = 全部静音，BGM 独立由 bgmOn 管）。</summary>
        public void SetSfxEnabled(bool on) => sfxEnabled = on;

        // ============ 音效接口扩展（用户 2026-10-26，ADR-083） ============

        // 按钮/卡片/格子不在 GameManager 引用链上 → 静态入口转发到场景播放器（无播放器 = 静默）。
        public static void Click() { if (current != null) current.PlayClick(); }
        public static void ClickInvalid() { if (current != null) current.PlayClickInvalid(); }
        public static void Confirm() { if (current != null) current.PlayConfirm(); }
        public static void Menu() { if (current != null) current.PlayMenu(); }

        // —— 通用交互（clickVolume 低音量档）——
        public void PlayClick() { if (cfg == null) return; Play(cfg.click, cfg.clickVolume, true); }
        public void PlayClickInvalid() { if (cfg == null) return; Play(cfg.clickInvalid, cfg.clickVolume, true); }
        public void PlayConfirm() { if (cfg == null) return; Play(cfg.confirm, cfg.clickVolume, true); }
        public void PlayMenu() { if (cfg == null) return; Play(cfg.menu, cfg.clickVolume, false); }

        // —— 塔操作 ——
        public void PlayPlace() { if (cfg == null) return; Play(cfg.place); }
        public void PlayPickup() { if (cfg == null) return; Play(cfg.pickup); }
        public void PlaySell() { if (cfg == null) return; Play(cfg.sell); }

        /// <summary>
        /// 合成（按**结果等级**选阶：2-3 低 / 4 中 / 5 金 / 6+ 红阶）。
        /// 槽空向上降级（mergeT6 → mergeHigh → mergeMid → mergeLow；全空静音）；
        /// 6 阶音量 ×1.15、不做 pitch 抖动（锣鼓走调会怪，ADR-083 F4.2）。
        /// </summary>
        public void PlayMerge(int resultLevel)
        {
            if (cfg == null) return;
            AudioClip clip;
            if (resultLevel >= 6) clip = cfg.mergeT6;
            else if (resultLevel == 5) clip = cfg.mergeHigh;
            else if (resultLevel == 4) clip = cfg.mergeMid;
            else clip = cfg.mergeLow;

            if (clip == null)
            {
                if (resultLevel >= 6) clip = cfg.mergeHigh;
                if (clip == null && resultLevel >= 5) clip = cfg.mergeMid;
                if (clip == null && resultLevel >= 4) clip = cfg.mergeLow;
            }
            if (clip == null) return;

            float vol = cfg.volume * (resultLevel >= 6 ? 1.15f : 1f);
            Play(clip, vol, false);
        }

        // —— 战斗反馈 ——
        public void PlayKill() { if (cfg == null) return; Play(cfg.kill); }
        public void PlayLeak() { if (cfg == null) return; Play(cfg.leak); }
        public void PlayVictory() { if (cfg == null) return; Play(cfg.victory); }
        public void PlayDefeat() { if (cfg == null) return; Play(cfg.defeat); }

        // —— 资源经济 + ch5 ——
        public void PlayDraw() { if (cfg == null) return; Play(cfg.draw); }

        private float lastInkPulse = -1f;
        /// <summary>产墨跳（墨塔/家）：全局 0.3s 节流，多塔同跳只响一声（ADR-083 F3.9）。</summary>
        public void PlayInkPulse()
        {
            if (cfg == null) return;
            if (Time.unscaledTime - lastInkPulse < 0.3f) return;
            lastInkPulse = Time.unscaledTime;
            Play(cfg.inkPulse);
        }

        public void PlayFarmComplete() { if (cfg == null) return; Play(cfg.farmComplete); }
        public void PlayRedShovel() { if (cfg == null) return; Play(cfg.redShovel); }
        public void PlayFaceSwitch() { if (cfg == null) return; Play(cfg.faceSwitch); }
        public void PlayNuoDance() { if (cfg == null) return; Play(cfg.nuoDance); }
        public void PlayNuke() { if (cfg == null) return; Play(cfg.nuke); }
        public void PlayTimeStop() { if (cfg == null) return; Play(cfg.timeStop); }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }

        private void Play(AudioClip clip)
        {
            Play(clip, cfg != null ? cfg.volume : 0.7f, true);
        }

        private void Play(AudioClip clip, float volume, bool pitchJitter)
        {
            if (!sfxEnabled || clip == null) return; // 槽位空 / 总开关关 = 静音跳过
            var src = sources[nextChannel];
            nextChannel = (nextChannel + 1) % Channels;
            src.pitch = pitchJitter ? Random.Range(0.94f, 1.06f) : 1f;
            src.PlayOneShot(clip, volume);
        }
    }
}
