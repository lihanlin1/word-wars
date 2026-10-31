using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WordWars.EditorTools
{
    /// <summary>
    /// W0/W1 场景生成器 + 自愈器，两个菜单：
    /// ① 自愈场景（日常默认）：只补 GameManager 的缺失引用，绝不触碰场景手动调整
    ///    （位置/对齐/字号/颜色）。用户改了 UI 并 Ctrl+S 后，永远只跑这个。
    /// ② 强制重建场景（慎用）：整场重新生成，会覆盖手动调整。仅结构性大改时用
    ///    （新增 HUD 元素/改路线/场景损坏）。重建前建议先提交场景文件。
    /// </summary>
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string FontDir = "Assets/_Project/Art/Fonts";
        private const string FontPath = FontDir + "/KaiTi_SDF_Dynamic.asset";
        private const string TtfPath = FontDir + "/KaiTi.ttf";
        private const string SpriteDir = "Assets/_Project/Art/Sprites";
        private const string WhitePng = SpriteDir + "/white.png";
        private const string SODir = "Assets/_Project/ScriptableObjects";
        private const string TowerPoolPath = SODir + "/Towers/TowerPool.asset";
        private const string EnemyTablePath = SODir + "/Enemies/EnemyTable.asset";
        private const string GameParamsPath = SODir + "/GameParams.asset";
        private const string UIPanelConfigPath = SODir + "/UIPanelConfig.asset";
        private const string SoundConfigPath = SODir + "/SoundConfig.asset";
        private const string LevelDir = SODir + "/Levels";
        private const string ChapterDir = SODir + "/Chapters";

        [MenuItem("Word Wars/Setup/强制重建场景（覆盖手动调整·慎用）")]
        public static void CreateGameScene()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Word Wars Setup",
                    "强制重建会覆盖你对 Game 场景的全部手动修改（位置/对齐/字号等）。\n" +
                    "如果只是缺引用/资产没接上，请改跑「自愈场景（保留手动调整）」。\n\n继续覆盖重建？", "覆盖重建", "取消"))
                return;

            EnsureFolders();

            var font = GetOrCreateFont();
            if (font == null)
            {
                EditorUtility.DisplayDialog("字体创建失败",
                    "具体原因见 Console。常见两类：\n" +
                    "1. 缺字体源文件：把 C:\\Windows\\Fonts\\simkai.ttf 拷到\n" + FontDir + "\n" +
                    "2. TMP 资源未导入：Window → TextMeshPro → Import TMP Essential Resources", "知道了");
                return;
            }

            var white = GetOrCreateWhiteSprite();
            if (white == null)
            {
                EditorUtility.DisplayDialog("贴图创建失败", "white.png 生成或导入失败，请检查 " + SpriteDir, "知道了");
                return;
            }

            // —— W2 配置资产（已存在则复用，调过的数值不会被覆盖）——
            var towerPool = GetOrCreateTowerPool();
            var enemyTable = GetOrCreateEnemyTable();
            var gameParams = GetOrCreateGameParams();
            var uiPanelConfig = GetOrCreateUIPanelConfig();
            var soundConfig = GetOrCreateSoundConfig();
            var chapter = GetOrCreateChapter();
            var level0 = GetOrCreateLevel(0);
            var level1 = GetOrCreateLevel(1);
            var level2 = GetOrCreateLevel(2);
            var level3 = GetOrCreateLevel(3);
            var level4 = GetOrCreateLevel(4);
            var level5 = GetOrCreateLevel(5);
            // F4.4（ADR-031）：平铺 12 关 = L0 + ch1×5 + ch2×5（二L1-L5 = Level6-10，章引用挂 Chapter2）
            var levelC2L1 = GetOrCreateLevel(6);
            var levelC2L2 = GetOrCreateLevel(7);
            var levelC2L3 = GetOrCreateLevel(8);
            var levelC2L4 = GetOrCreateLevel(9);
            var levelC2L5 = GetOrCreateLevel(10);
            // F4.2（ADR-040）：平铺 16 关 = + ch3×5（三L1-三L5 = Level11-15，章引用挂 Chapter3）
            var levelC3L1 = GetOrCreateLevel(11);
            var levelC3L2 = GetOrCreateLevel(12);
            var levelC3L3 = GetOrCreateLevel(13);
            var levelC3L4 = GetOrCreateLevel(14);
            var levelC3L5 = GetOrCreateLevel(15);
            // F1.1（ch4，docs/11 数据先行序）：四L1 骨架先行 = 章图有真关卡可走；
            // F1.3：四L2-L5 建全（客串骨架波——新敌/30 波全表归 F5.1/F6 替换）
            var levelC4L1 = GetOrCreateLevel(16);
            var levelC4L2 = GetOrCreateLevel(17);
            var levelC4L3 = GetOrCreateLevel(18);
            var levelC4L4 = GetOrCreateLevel(19);
            var levelC4L5 = GetOrCreateLevel(20);
            // F1.3（ch5，docs/13）：五L1 骨架先行 = 双面章图有真关卡可走；
            // F7.1 换 30 波官方全表（骨架期：纸/影客串，面配比按关级）
            var levelC5L1 = GetOrCreateLevel(21);
            var levelC5L2 = GetOrCreateLevel(22);
            var levelC5L3 = GetOrCreateLevel(23);
            var levelC5L4 = GetOrCreateLevel(24);
            var levelC5L5 = GetOrCreateLevel(25);
            if (towerPool == null || enemyTable == null || gameParams == null || uiPanelConfig == null ||
                soundConfig == null || chapter == null || level0 == null || level1 == null ||
                level2 == null || level3 == null || level4 == null || level5 == null ||
                levelC2L1 == null || levelC2L2 == null || levelC2L3 == null || levelC2L4 == null || levelC2L5 == null ||
                levelC3L1 == null || levelC3L2 == null || levelC3L3 == null || levelC3L4 == null || levelC3L5 == null ||
                levelC4L1 == null || levelC4L2 == null || levelC4L3 == null || levelC4L4 == null || levelC4L5 == null ||
                levelC5L1 == null || levelC5L2 == null || levelC5L3 == null || levelC5L4 == null || levelC5L5 == null)
            {
                EditorUtility.DisplayDialog("配置创建失败", "塔字池/敌人表/游戏参数/章图/关卡 SO 创建失败，原因见 Console。", "知道了");
                return;
            }

            // 返工补丁：给旧资产补新增字段的缺省值（不动玩家已调的数值）
            PatchLegacyAssets(towerPool, enemyTable, uiPanelConfig);

            // F1.3 建图 + F4.3 章间推进已落地：ch2 五关（Level6-10）入 levels 平铺数组（ADR-031），
            // 各关 chapter 引用挂 Chapter2；ch1 关卡留空走场景引用兜底
            var chapter2 = GetOrCreateChapter2();
            var chapter3 = GetOrCreateChapter3(); // F4.2（ADR-040）：ch3 章图（同路双向 + fog）
            var chapter4 = GetOrCreateChapter4(); // F1.1（ch4，docs/11）：火山章图（回环跑道 + 火山口装饰）
            var chapter5 = GetOrCreateChapter5(); // F1.1（ch5，docs/13）：双面同图（实/虚两路等长）
            ValidateChapter(chapter);
            if (chapter2 != null) ValidateChapter(chapter2);
            if (chapter3 != null) ValidateChapter(chapter3);
            if (chapter4 != null) ValidateChapter(chapter4);
            if (chapter5 != null) ValidateChapter(chapter5);
            ValidateChapter5Cards(towerPool, levelC5L1, levelC5L2, levelC5L3, levelC5L4, levelC5L5); // F3.5：池 21 + 解锁序

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // —— 相机：正交 + 宣纸底色（横屏）——
            var cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 5.4f; // 设计高 10.8 格，16:9 时宽 19.2 格
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HexUtil.ToColor("#F7F2E7");
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // 删默认平行光（2D 用不到）
            var light = Object.FindObjectOfType<Light>();
            if (light != null) Object.DestroyImmediate(light.gameObject);

            // —— 路线：横屏蛇形，左→右三段折返（ADR-014 路上格）——
            //    全部整数坐标：路线本身占棋盘格行 y=3/0/-3（竖段 x=±5），
            //    PathSystem 运行时沿折线铺墨路砖；种子格子核过不压路。
            var pathGo = new GameObject("Path", typeof(PathSystem));
            var path = pathGo.GetComponent<PathSystem>();
            Vector2[] waypoints =
            {
                new Vector2(-9f, 3f), new Vector2(5f, 3f),
                new Vector2(5f, 0f), new Vector2(-5f, 0f),
                new Vector2(-5f, -3f), new Vector2(9f, -3f)
            };
            for (int i = 0; i < waypoints.Length; i++)
            {
                var p = new GameObject("P" + i);
                p.transform.SetParent(pathGo.transform, false);
                p.transform.position = new Vector3(waypoints[i].x, waypoints[i].y, 0f);
            }

            // —— 网格（v2.3）：格子由 GridSystem 按 LevelConfig 运行时生成，
            //    场景不再摆放固定塔位槽（布局进关卡数据，逐关差异化）——

            // —— HUD（横屏 1920×1080）——
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // 按高度匹配

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // —— 安全区容器（docs/16 F3.2，场景级）：HUD 内容挂它下面，运行时只调锚点不搬家 ——
            var safeRootGo = new GameObject("SafeAreaRoot", typeof(RectTransform));
            safeRootGo.transform.SetParent(canvasGo.transform, false);
            var safeRt = (RectTransform)safeRootGo.transform;
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;
            canvasGo.AddComponent<SafeAreaFitter>();

            var hud = canvasGo.AddComponent<Hud>();
            // —— HUD 布局 = 用户定稿（2026-09-25 手动调整后烧入，勿随意改动）——
            // 墨：左缘右移约 1/3 屏宽；波次：钉右上角（换分辨率不跑位）；关卡：画面中线。
            hud.inkText = MakeText(safeRootGo.transform, font, "墨 60", new Vector2(-586f, 490f), TextAlignmentOptions.Left, 46);
            hud.inkText.color = HexUtil.ToColor("#2B4C7E"); // 蓝墨
            hud.livesText = MakeText(safeRootGo.transform, font, "印印印印印", new Vector2(0f, 490f), TextAlignmentOptions.Center, 46);
            hud.waveText = MakeText(safeRootGo.transform, font, "", new Vector2(-349f, -50f), TextAlignmentOptions.Right, 46, new Vector2(1f, 1f));
            hud.levelText = MakeText(safeRootGo.transform, font, "", new Vector2(0f, 428f), TextAlignmentOptions.Right, 28);
            hud.levelText.color = new Color(0.55f, 0.53f, 0.50f); // 淡墨：关卷名做背景信息，不与波次抢视觉
            hud.hintText = MakeText(safeRootGo.transform, font,
                "点手牌→点空地放塔 · 点塔拿起→点目标(移动/合并) · 抽=一口价补满 · 铲点荒地开垦 · 空格下一波 D抽 R重开 N换关",
                new Vector2(0f, 405f), TextAlignmentOptions.Center, 24);
            hud.hintText.color = new Color(0.55f, 0.53f, 0.50f);
            hud.hintText.enableWordWrapping = false; // 单行不折行
            hud.bannerText = MakeText(safeRootGo.transform, font, "", new Vector2(0f, 0f), TextAlignmentOptions.Center, 72);
            hud.bannerText.color = HexUtil.ToColor("#B83A2E");
            hud.bannerText.gameObject.SetActive(false);

            // 字体赋值验证：任何一层防呆失败都会在这里报错，方便定位
            foreach (var txt in new[] { hud.inkText, hud.livesText, hud.levelText, hud.waveText, hud.hintText, hud.bannerText })
            {
                if (txt == null) { Debug.LogError("[Word Wars Setup] HUD 文本为空"); continue; }
                if (txt.font != font)
                    Debug.LogError("[Word Wars Setup] HUD 字体未生效: " + txt.name +
                        " 实际=" + (txt.font != null ? txt.font.name : "null"));
            }

            // —— GameManager（注入引用）——
            var gmGo = new GameObject("GameManager", typeof(GameManager));
            // 注入引用：当场从磁盘重载（防建场景过程中的资产刷新把内存实例刷成 fake-null）
            var towerPoolRef = AssetDatabase.LoadAssetAtPath<TowerPoolDef>(TowerPoolPath);
            var enemyTableRef = AssetDatabase.LoadAssetAtPath<EnemyTableDef>(EnemyTablePath);
            var gameParamsRef = AssetDatabase.LoadAssetAtPath<GameParams>(GameParamsPath);
            var uiPanelConfigRef = AssetDatabase.LoadAssetAtPath<UIPanelConfig>(UIPanelConfigPath);
            var soundConfigRef = AssetDatabase.LoadAssetAtPath<SoundConfig>(SoundConfigPath);
            var chapterRef = AssetDatabase.LoadAssetAtPath<ChapterConfig>(ChapterDir + "/Chapter1.asset");
            var levelRefs = new LevelConfig[]
            {
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level0.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level1.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level2.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level3.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level4.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level5.asset"),
                // F4.4：ch2 五关（二L1-L5 = Level6-10；章引用在各自资产里 → Chapter2）
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level6.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level7.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level8.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level9.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level10.asset"),
                // F4.2（ADR-040）：ch3 五关（三L1-L5 = Level11-15；章引用在各自资产里 → Chapter3）
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level11.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level12.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level13.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level14.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level15.asset"),
                // F1.1/F1.3（ch4，docs/11）：四L1-L5 = Level16-20（章引用在资产里 → Chapter4；
                // 骨架波客串敌——新敌与 30 波全表归 F5.1/F6，火/盆/炮卡归 F4 随池进）
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level16.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level17.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level18.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level19.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level20.asset"),
                // F1.3（ch5）：五L1-L5 = Level21-25（章引用在资产里 → Chapter5）
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level21.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level22.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level23.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level24.asset"),
                AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelDir + "/Level25.asset"),
            };
            if (towerPoolRef == null || enemyTableRef == null || gameParamsRef == null ||
                uiPanelConfigRef == null || soundConfigRef == null || chapterRef == null || levelRefs[0] == null ||
                levelRefs[1] == null || levelRefs[2] == null || levelRefs[3] == null || levelRefs[4] == null ||
                levelRefs[5] == null || levelRefs[6] == null || levelRefs[7] == null || levelRefs[8] == null ||
                levelRefs[9] == null || levelRefs[10] == null || levelRefs[11] == null || levelRefs[12] == null ||
                levelRefs[13] == null || levelRefs[14] == null || levelRefs[15] == null || levelRefs[16] == null ||
                levelRefs[17] == null || levelRefs[18] == null || levelRefs[19] == null || levelRefs[20] == null ||
                levelRefs[21] == null || levelRefs[22] == null || levelRefs[23] == null || levelRefs[24] == null ||
                levelRefs[25] == null)
            {
                EditorUtility.DisplayDialog("配置注入失败",
                    "塔字池/敌人表/游戏参数/关卡注入失败，原因见 Console（红字）。\n通常重跑一次本菜单即可自愈。", "知道了");
                return;
            }

            var so = new SerializedObject(gmGo.GetComponent<GameManager>());
            so.FindProperty("path").objectReferenceValue = path;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("whiteSprite").objectReferenceValue = white;
            so.FindProperty("towerPool").objectReferenceValue = towerPoolRef;
            so.FindProperty("enemyTable").objectReferenceValue = enemyTableRef;
            so.FindProperty("gameParams").objectReferenceValue = gameParamsRef;
            so.FindProperty("panelConfig").objectReferenceValue = uiPanelConfigRef;
            so.FindProperty("soundConfig").objectReferenceValue = soundConfigRef;
            so.FindProperty("chapter").objectReferenceValue = chapterRef;
            var levelsProp = so.FindProperty("levels");
            levelsProp.arraySize = levelRefs.Length;
            for (int i = 0; i < levelRefs.Length; i++)
                levelsProp.GetArrayElementAtIndex(i).objectReferenceValue = levelRefs[i];
            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.DisplayDialog("Word Wars Setup",
                "Game 场景已生成：\n" + ScenePath +
                "\n\n按 Play：点「抽」一口价（池总价值）补满 5 张手牌 → 点手牌选字 → 点空地格放置。" +
                "\n点塔 = 拿起（点空地移动 / 点同字同级塔合并升阶；拿起时显示射程圈）。" +
                "\n铲点荒地开垦（15s，25 墨加速）；卖 = 卡价值×50%×字位。" +
                "\n空格 = 提前召唤下一波，D = 抽卡，R = 重开，N = 推进（章内带塔 / 章界清场进新章；L0 教学 → ch1×5 → ch2×5 → ch3×5 → ch4×5 → ch5×5 平铺 26 关）。" +
                "\n\n数值在 Assets/_Project/ScriptableObjects/ 用 Inspector 直接调（章图格子布局在 Chapters/，波次在 Levels/，界面布局在 UIPanelConfig.asset）。", "好");
        }

        /// <summary>
        /// 生成主界面 / 选关两个新场景（用户 2026-10-24）：只含相机 + 控制器（UI 运行时自建），
        /// 并把 Build Settings 顺序设为 主界面 → 选关 → Game（启动进主界面）。
        /// </summary>
        [MenuItem("Word Wars/Setup/重建主界面与选关场景")]
        public static void BuildMenuScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var white = GetOrCreateWhiteSprite();
            if (font == null || white == null)
            {
                EditorUtility.DisplayDialog("生成失败", "字体或白图缺失（先跑一次「强制重建场景」）。", "知道了");
                return;
            }

            string menuPath = "Assets/_Project/Scenes/MainMenu.unity";
            string selectPath = "Assets/_Project/Scenes/LevelSelect.unity";
            BuildMenuScene(menuPath, typeof(MenuSceneController), font, white);
            BuildMenuScene(selectPath, typeof(LevelSelectController), font, white);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(menuPath, true),
                new EditorBuildSettingsScene(selectPath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };

            // 回到 Game 场景（日常开发重心）
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Setup] 主界面/选关场景已生成，Build Settings：MainMenu(0) / LevelSelect(1) / Game(2)");
        }

        /// <summary>
        /// 用户 2026-10-28：只给现有主界面补「图鉴数据源」引用（不重建 UI、不动 Build Settings）。
        /// 比「重建主界面与选关场景」安全——已生成的按钮布局原样保留。
        /// </summary>
        [MenuItem("Word Wars/Setup/自愈主界面引用（图鉴数据）")]
        public static void SelfHealMenuRefs()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string menuPath = "Assets/_Project/Scenes/MainMenu.unity";
            if (!File.Exists(menuPath))
            {
                EditorUtility.DisplayDialog("自愈失败", "主界面场景不存在，请跑「重建主界面与选关场景」。", "知道了");
                return;
            }
            var scene = EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Single);
            var ctrl = Object.FindObjectOfType<MenuSceneController>();
            if (ctrl == null)
            {
                EditorUtility.DisplayDialog("自愈失败", "主界面场景里找不到 MenuSceneController。", "知道了");
                return;
            }
            var so = new SerializedObject(ctrl);
            int patched = 0;
            patched += PatchRef(so, "codexTowerPool", AssetDatabase.LoadAssetAtPath<TowerPoolDef>(TowerPoolPath));
            patched += PatchRef(so, "codexEnemyTable", AssetDatabase.LoadAssetAtPath<EnemyTableDef>(EnemyTablePath));
            var chs = so.FindProperty("codexChapters");
            if (chs != null)
            {
                chs.arraySize = 5;
                for (int i = 0; i < 5; i++)
                    chs.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<ChapterConfig>(ChapterDir + "/Chapter" + (i + 1) + ".asset");
                patched++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Setup] 主界面图鉴引用已补齐（" + patched + " 项）");
            EditorUtility.DisplayDialog("自愈完成", "主界面图鉴数据源已注入（塔池 / 敌表 / 5 章图）。", "好");
        }

        private static void BuildMenuScene(string path, System.Type controllerType, TMP_FontAsset font, Sprite white)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HexUtil.ToColor("#F7F2E7");
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var go = new GameObject("Controller");
            var comp = go.AddComponent(controllerType);
            var so = new SerializedObject(comp);
            var f = so.FindProperty("font");
            if (f != null) f.objectReferenceValue = font;
            var w = so.FindProperty("whiteSprite");
            if (w != null) w.objectReferenceValue = white;

            // 图鉴数据源（用户 2026-10-28）：只有主界面控制器有这些字段
            var tp = so.FindProperty("codexTowerPool");
            if (tp != null) tp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TowerPoolDef>(TowerPoolPath);
            var et = so.FindProperty("codexEnemyTable");
            if (et != null) et.objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyTableDef>(EnemyTablePath);
            var chs = so.FindProperty("codexChapters");
            if (chs != null)
            {
                chs.arraySize = 5;
                for (int i = 0; i < 5; i++)
                    chs.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<ChapterConfig>(ChapterDir + "/Chapter" + (i + 1) + ".asset");
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        /// <summary>
        /// 自愈（日常默认跑这个）：加载现有 Game 场景，只给 GameManager 补**缺失**的引用，
        /// 已有引用一律不动，场景里的手动调整（位置/对齐/字号/颜色）原样保留。
        /// 场景不存在或结构损坏（缺 GameManager/PathSystem/Hud）时提示转强制重建。
        /// </summary>
        [MenuItem("Word Wars/Setup/自愈场景（保留手动调整）")]
        public static void SelfHealScene()
        {
            EnsureFolders();

            var font = GetOrCreateFont();
            var white = GetOrCreateWhiteSprite();
            if (font == null || white == null)
            {
                EditorUtility.DisplayDialog("自愈失败",
                    "字体/白图资源缺失，原因见 Console（红字）。", "知道了");
                return;
            }

            var towerPool = GetOrCreateTowerPool();
            var enemyTable = GetOrCreateEnemyTable();
            var gameParams = GetOrCreateGameParams();
            // 枭改动（用户 2026-10-09 定案）：吞噬窗口 5→3×阶数——哨兵只升未调值
            //（资产 ==5 旧缺省才改 3；Inspector 手调过 ≠5 不动 = 领地规则）。
            if (gameParams != null && Mathf.Approximately((float)gameParams.perchSecondsPerLevel, 5f))
            {
                gameParams.perchSecondsPerLevel = 3;
                EditorUtility.SetDirty(gameParams);
                Debug.Log("[Setup] GameParams 自愈：枭吞噬窗口 5→3×阶数（用户 2026-10-09 定案）");
            }
            var uiPanelConfig = GetOrCreateUIPanelConfig();
            var soundConfig = GetOrCreateSoundConfig();
            var chapter = GetOrCreateChapter();
            var level0 = GetOrCreateLevel(0);
            var level1 = GetOrCreateLevel(1);
            var level2 = GetOrCreateLevel(2);
            var level3 = GetOrCreateLevel(3);
            var level4 = GetOrCreateLevel(4);
            var level5 = GetOrCreateLevel(5);
            // F4.4（ADR-031）：自愈也确保 ch2 五关存在 + 章引用（新档直接可玩到二L5）
            var levelC2L1 = GetOrCreateLevel(6);
            var levelC2L2 = GetOrCreateLevel(7);
            var levelC2L3 = GetOrCreateLevel(8);
            var levelC2L4 = GetOrCreateLevel(9);
            var levelC2L5 = GetOrCreateLevel(10);
            // F4.2（ADR-040）：自愈也确保 ch3 五关存在 + 章引用（新档直接可玩到三L5）
            var levelC3L1 = GetOrCreateLevel(11);
            var levelC3L2 = GetOrCreateLevel(12);
            var levelC3L3 = GetOrCreateLevel(13);
            var levelC3L4 = GetOrCreateLevel(14);
            var levelC3L5 = GetOrCreateLevel(15);
            // F1.1（ch4）：自愈也确保四L1 骨架存在 + 章引用（新档直接可走到四L1）；
            // F1.3：四L2-L5 建全——21 关平铺收口（客串骨架波，新敌/30 波全表归 F5.1/F6）
            var levelC4L1 = GetOrCreateLevel(16);
            var levelC4L2 = GetOrCreateLevel(17);
            var levelC4L3 = GetOrCreateLevel(18);
            var levelC4L4 = GetOrCreateLevel(19);
            var levelC4L5 = GetOrCreateLevel(20);
            // F1.3（ch5，docs/13）：五L1 骨架先行 = 双面章图有真关卡可走；
            // F7.1 换 30 波官方全表（骨架期：纸/影客串，面配比按关级）
            var levelC5L1 = GetOrCreateLevel(21);
            var levelC5L2 = GetOrCreateLevel(22);
            var levelC5L3 = GetOrCreateLevel(23);
            var levelC5L4 = GetOrCreateLevel(24);
            var levelC5L5 = GetOrCreateLevel(25);
            if (towerPool == null || enemyTable == null || gameParams == null || uiPanelConfig == null ||
                soundConfig == null || chapter == null || level0 == null || level1 == null ||
                level2 == null || level3 == null || level4 == null || level5 == null ||
                levelC2L1 == null || levelC2L2 == null || levelC2L3 == null || levelC2L4 == null || levelC2L5 == null ||
                levelC3L1 == null || levelC3L2 == null || levelC3L3 == null || levelC3L4 == null || levelC3L5 == null ||
                levelC4L1 == null || levelC4L2 == null || levelC4L3 == null || levelC4L4 == null || levelC4L5 == null ||
                levelC5L1 == null || levelC5L2 == null || levelC5L3 == null || levelC5L4 == null || levelC5L5 == null)
            {
                EditorUtility.DisplayDialog("自愈失败",
                    "塔字池/敌人表/游戏参数/章图/关卡 SO 缺失，原因见 Console。", "知道了");
                return;
            }
            PatchLegacyAssets(towerPool, enemyTable, uiPanelConfig);

            // F1.3 建图；F4.3 章间推进落地后 ch2 五关入 levels 平铺（ADR-031）——本流程上方已创建并校验
            var chapter2 = GetOrCreateChapter2();
            var chapter3 = GetOrCreateChapter3(); // F4.2（ADR-040）：ch3 章图同建同校验
            var chapter4 = GetOrCreateChapter4(); // F1.1（ch4，docs/11）：火山章图同建同校验
            var chapter5 = GetOrCreateChapter5(); // F1.1（ch5，docs/13）：双面章图同建同校验（两路等长）
            ValidateChapter(chapter);
            if (chapter2 != null) ValidateChapter(chapter2);
            if (chapter3 != null) ValidateChapter(chapter3);
            if (chapter4 != null) ValidateChapter(chapter4);
            if (chapter5 != null) ValidateChapter(chapter5);
            ValidateChapter5Cards(towerPool, levelC5L1, levelC5L2, levelC5L3, levelC5L4, levelC5L5); // F3.5：池 21 + 解锁序

            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("自愈跳过",
                    "Game 场景还不存在——请先跑一次「强制重建场景」。", "知道了");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var gm = Object.FindObjectOfType<GameManager>();
            var path = Object.FindObjectOfType<PathSystem>();
            var hud = Object.FindObjectOfType<Hud>();
            if (gm == null || path == null || hud == null)
            {
                EditorUtility.DisplayDialog("自愈失败",
                    "场景缺 GameManager/PathSystem/Hud（结构损坏或被误删）。\n" +
                    "请跑「强制重建场景」——注意会覆盖手动调整，建议先提交当前场景文件。", "知道了");
                return;
            }

            // 引用统一从磁盘重载注入（防 fake-null，与强制重建同规矩）
            var so = new SerializedObject(gm);
            int patched = 0;
            patched += PatchRef(so, "path", path);
            patched += PatchRef(so, "hud", hud);
            patched += PatchRef(so, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
            patched += PatchRef(so, "whiteSprite", AssetDatabase.LoadAssetAtPath<Sprite>(WhitePng));
            patched += PatchRef(so, "towerPool", AssetDatabase.LoadAssetAtPath<TowerPoolDef>(TowerPoolPath));
            patched += PatchRef(so, "enemyTable", AssetDatabase.LoadAssetAtPath<EnemyTableDef>(EnemyTablePath));
            patched += PatchRef(so, "gameParams", AssetDatabase.LoadAssetAtPath<GameParams>(GameParamsPath));
            patched += PatchRef(so, "panelConfig", AssetDatabase.LoadAssetAtPath<UIPanelConfig>(UIPanelConfigPath));
            patched += PatchRef(so, "soundConfig", AssetDatabase.LoadAssetAtPath<SoundConfig>(SoundConfigPath));
            patched += PatchRef(so, "chapter", AssetDatabase.LoadAssetAtPath<ChapterConfig>(ChapterDir + "/Chapter1.asset"));

            // P3.2（docs/16 F3.2，用户 2026-10-25）：安全区容器自愈——Canvas 无 SafeAreaRoot 时
            // 建一个（stretch 全幅）+ 把 Canvas 直接子物体挂进去（worldPositionStays 保留位置）+
            // 挂 SafeAreaFitter。运行时只调 SafeAreaRoot 锚点（不重挂/不搬家）。
            {
                var canvas = hud.GetComponentInParent<Canvas>();
                bool hasRoot = false;
                if (canvas != null)
                    foreach (Transform c in canvas.transform)
                        if (c.name == "SafeAreaRoot") { hasRoot = true; break; }
                if (canvas != null && !hasRoot)
                {
                    var safeGo = new GameObject("SafeAreaRoot", typeof(RectTransform));
                    safeGo.transform.SetParent(canvas.transform, false);
                    var srt = (RectTransform)safeGo.transform;
                    srt.anchorMin = Vector2.zero;
                    srt.anchorMax = Vector2.one;
                    srt.offsetMin = Vector2.zero;
                    srt.offsetMax = Vector2.zero;
                    var kids = new List<Transform>();
                    foreach (Transform c in canvas.transform)
                        if (c != srt.transform) kids.Add(c);
                    foreach (var c in kids)
                        c.SetParent(srt, true); // 保留位置：HUD 手调不丢
                    if (canvas.GetComponent<SafeAreaFitter>() == null)
                        canvas.gameObject.AddComponent<SafeAreaFitter>();
                    patched++;
                    Debug.Log("[Word Wars Setup] 安全区容器已补（SafeAreaRoot + SafeAreaFitter）");
                }
            }

            // levels 数组：长度不对、任一元素空、或顺序不符 → 整组重接
            //（v3.0：L0 教学 + L1-L5 共六关，ADR-030。顺序也是接线的一部分——L0 必须居首，
            //  否则新档跳过教学 + L5 通关出「下一关」钻回 L0。曾漏检：手动把 Level0 拖进
            //  第 6 槽形成 [L1..L5,L0]，六槽全满被自愈放过。
            //  F4.4/ADR-031：扩 12 关平铺（+ 二L1-L5 = Level6-10），同规则整组重接；
            //  F4.2/ADR-040：扩 16 关（+ 三L1-L5 = Level11-15）——漏改此处实录：只改主流程注入段，
            //  自愈把 levels 停在 11 项，二L5 仍判「全卷通关」清场回 L1（用户 2026-10-08 验收抓出））
            var levelsProp = so.FindProperty("levels");
            var levelRefs = new LevelConfig[] { level0, level1, level2, level3, level4, level5,
                                                levelC2L1, levelC2L2, levelC2L3, levelC2L4, levelC2L5,
                                                levelC3L1, levelC3L2, levelC3L3, levelC3L4, levelC3L5,
                                                levelC4L1, levelC4L2, levelC4L3, levelC4L4, levelC4L5,
                                                levelC5L1, levelC5L2, levelC5L3, levelC5L4, levelC5L5 };
            if (levelsProp != null)
            {
                bool broken = levelsProp.arraySize != levelRefs.Length;
                if (!broken)
                    for (int i = 0; i < levelRefs.Length; i++)
                        if (levelsProp.GetArrayElementAtIndex(i).objectReferenceValue != levelRefs[i]) { broken = true; break; }
                if (broken)
                {
                    levelsProp.arraySize = levelRefs.Length;
                    for (int i = 0; i < levelRefs.Length; i++)
                        levelsProp.GetArrayElementAtIndex(i).objectReferenceValue = levelRefs[i];
                    patched++;
                }
            }
            so.ApplyModifiedProperties();

            // —— 路线折点迁移（ADR-014 路上格）：旧场景 ±8.5/±3.4/±5.5 → 整数格 ——
            // 只迁移「仍等于旧种子值」的折点；玩家手动挪过的（不匹配）跳过并提示。
            Vector2[] oldWaypoints =
            {
                new Vector2(-8.5f, 3.4f), new Vector2(5.5f, 3.4f),
                new Vector2(5.5f, 0f), new Vector2(-5.5f, 0f),
                new Vector2(-5.5f, -3.4f), new Vector2(8.5f, -3.4f)
            };
            Vector2[] gridWaypoints =
            {
                new Vector2(-9f, 3f), new Vector2(5f, 3f),
                new Vector2(5f, 0f), new Vector2(-5f, 0f),
                new Vector2(-5f, -3f), new Vector2(9f, -3f)
            };
            for (int i = 0; i < oldWaypoints.Length; i++)
            {
                var wp = path.transform.Find("P" + i);
                if (wp == null) continue;
                var cur = new Vector2(wp.position.x, wp.position.y);
                if ((cur - oldWaypoints[i]).sqrMagnitude < 0.01f)
                {
                    wp.position = new Vector3(gridWaypoints[i].x, gridWaypoints[i].y, 0f);
                    patched++;
                    Debug.Log("[Word Wars Setup] 折点 P" + i + " 迁整数格: " + oldWaypoints[i] + " → " + gridWaypoints[i]);
                }
                else if ((cur - gridWaypoints[i]).sqrMagnitude > 0.01f)
                {
                    Debug.LogWarning("[Word Wars Setup] 折点 P" + i + " 不匹配旧种子（疑手动调整过），跳过迁移: " + cur);
                }
            }

            Debug.Log("[Word Wars Setup] 自愈完成：补 " + patched + " 项（引用/折点）；场景布局未触碰");
            if (patched > 0)
            {
                EditorSceneManager.SaveScene(scene);
                EditorUtility.DisplayDialog("自愈完成",
                    "补齐 " + patched + " 项缺失引用/折点迁移并保存。\n手动调整（位置/对齐/字号/颜色）原样保留。", "好");
            }
            else
            {
                EditorUtility.DisplayDialog("自愈完成",
                    "引用完整，未做任何改动——场景保持原样（你的手动调整都在）。", "好");
            }
        }

        /// <summary>只补空引用：字段已有值（含玩家手动接的）一律不动。返回 1=补了 / 0=没动。</summary>
        private static int PatchRef(SerializedObject so, string field, Object value)
        {
            if (value == null) return 0;
            var prop = so.FindProperty(field);
            if (prop == null) return 0;
            if (prop.objectReferenceValue != null) return 0;
            prop.objectReferenceValue = value;
            return 1;
        }

        /// <summary>删档（测试期常用）：删 save_v2.json。Play 运行中拒绝——游戏胜利时会重写存档。</summary>
        [MenuItem("Word Wars/Setup/删档（重置存档）")]
        public static void DeleteSave()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("先停 Play", "Play 运行中，游戏会在胜利/推进时重写存档——先停止 Play 再删。", "知道了");
                return;
            }
            string path = Path.Combine(Application.persistentDataPath, "save_v2.json");
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log("[删档] 已删除：" + path + "（下次 Play 从 L0 教学关全新开始）");
            }
            else
            {
                Debug.Log("[删档] 本来就没有存档（" + path + "）——干净的");
            }
        }

        [MenuItem("Word Wars/Setup/重建中文字体资产")]
        public static void RebuildFont()
        {
            EnsureFolders();
            if (File.Exists(FontPath))
            {
                EditorUtility.DisplayDialog("Word Wars Setup",
                    "字体资产已存在，本次跳过。\n删除重建会更换 GUID，导致已生成场景的引用失效（HUD 会回退默认字体变方框）。\n" +
                    "如确需重建：手动删除 KaiTi_SDF_Dynamic.asset 及其 .meta 后，重跑「强制重建场景」。",
                    "知道了");
                return;
            }
            var font = GetOrCreateFont();
            EditorUtility.DisplayDialog("Word Wars Setup",
                font != null ? "中文字体资产已创建。" : "字体创建失败，原因见 Console。", "好");
        }

        // ============ 资产创建 ============

        private static TMP_FontAsset GetOrCreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null) return existing;

            // 前置保障：TMP 着色器就绪（未导入 essentials 时 CreateFontAsset 会抛 ArgumentNullException）
            if (!EnsureTmpEssentials())
            {
                Debug.LogError("[Word Wars Setup] TMP Essential Resources 导入后着色器仍不可用，无法创建字体资产。");
                return null;
            }

            // 找工程内现成的字体源文件（W1 用系统楷体 simkai.ttf 拷入；W4 换思源宋体）
            var ttfPath = TtfPath;
            if (!File.Exists(ttfPath))
            {
                var files = Directory.GetFiles(FontDir, "*.ttf");
                if (files.Length > 0) ttfPath = files[0];
            }
            if (!File.Exists(ttfPath))
            {
                Debug.LogError("[Word Wars Setup] 工程里没有字体源文件（.ttf）。请把 C:\\Windows\\Fonts\\simkai.ttf 拷贝到 " + FontDir + " 后重试。");
                return null;
            }

            // 必须从「已导入的字体文件」创建：按名字创建的 OS 字体不携带字面数据，TMP 会报
            // "Unable to load font face / Include Font Data"。
            AssetDatabase.ImportAsset(ttfPath);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (sourceFont == null)
            {
                Debug.LogError("[Word Wars Setup] 字体源文件导入失败：" + ttfPath);
                return null;
            }

            TMP_FontAsset asset;
            try { asset = TMP_FontAsset.CreateFontAsset(sourceFont); }
            catch (System.Exception e) { Debug.LogException(e); return null; }
            if (asset == null) return null;

            asset.name = "KaiTi_SDF_Dynamic";
            AssetDatabase.CreateAsset(asset, FontPath);
            if (asset.material != null) AssetDatabase.AddObjectToAsset(asset.material, FontPath);
            if (asset.atlasTexture != null) AssetDatabase.AddObjectToAsset(asset.atlasTexture, FontPath);
            AssetDatabase.SaveAssets();

            // 关键：保存后必须从磁盘重新加载。CreateAsset/SaveAssets 触发的资产刷新可能
            // 卸载内存实例（fake-null），之后 t.font = font 会静默失效并回退默认字体
            // LiberationSans（HUD 变方框的元凶）。图集纹理序列化为 0×0 是动态字体正常
            // 行为：TMP 运行时会按需 Resize。
            var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            return reloaded != null ? reloaded : asset;
        }

        /// <summary>
        /// 确保 TMP 着色器可用：工程未导入 TMP Essential Resources 时，
        /// TMP 内部 Shader.Find 找不到着色器 → CreateFontAsset 里 new Material(null)
        /// 抛 ArgumentNullException（shader）。这里静默导入 essentials 解决。
        /// TMP 的着色器引用是惰性查找，导入后同一次调用即可生效。
        /// </summary>
        private static bool EnsureTmpEssentials()
        {
            if (Shader.Find("TextMeshPro/Mobile/Distance Field") != null) return true;

            var pkg = FindTmpEssentialsPackage();
            if (pkg == null)
            {
                Debug.LogError("[Word Wars Setup] 找不到 TMP Essential Resources.unitypackage。" +
                    "请手动执行：Window → TextMeshPro → Import TMP Essential Resources 后重试。");
                return false;
            }

            try { AssetDatabase.ImportPackage(pkg, false); }
            catch (System.Exception e) { Debug.LogException(e); return false; }
            AssetDatabase.Refresh();

            return Shader.Find("TextMeshPro/Mobile/Distance Field") != null;
        }

        private static string FindTmpEssentialsPackage()
        {
            // PackageCache 物理路径（带版本号，通配查找）
            if (Directory.Exists("Library/PackageCache"))
            {
                foreach (var dir in Directory.GetDirectories("Library/PackageCache", "com.unity.textmeshpro*"))
                {
                    var p = dir.Replace('\\', '/') + "/Package Resources/TMP Essential Resources.unitypackage";
                    if (File.Exists(p)) return p;
                }
            }
            // 兜底：包管理器虚拟路径
            const string alias = "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";
            return File.Exists(alias) ? alias : null;
        }

        private static Sprite GetOrCreateWhiteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePng);
            if (existing != null)
            {
                // ADR-025 自愈：W2 生成时漏设 PPU（默认 100）→ 8px 图只有 0.08 世界单位，
                // 全部白图视觉按 8% 尺寸渲染（Dim 小长方体/按钮过小的根因）。补正为 PPU 8 = 1×1 单位。
                if (Mathf.Abs(existing.pixelsPerUnit - 8f) > 0.01f)
                {
                    var imp = (TextureImporter)AssetImporter.GetAtPath(WhitePng);
                    if (imp != null)
                    {
                        imp.spritePixelsPerUnit = 8f;
                        imp.SaveAndReimport();
                        Debug.LogWarning("[Word Wars Setup] white.png PPU 已补正为 8（8px = 1×1 世界单位），白图类视觉恢复设计尺寸。");
                    }
                }
                return existing;
            }

            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            Directory.CreateDirectory(SpriteDir);
            File.WriteAllBytes(WhitePng, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(WhitePng);
            var importer = (TextureImporter)AssetImporter.GetAtPath(WhitePng);
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 8f; // ADR-025：8px↔PPU8 = 1×1 世界单位（漏设默认 100 是 W2 事故根因）
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(WhitePng);
        }

        // ============ SO 配置（W2 数值唯一数据源） ============

        private static TowerPoolDef GetOrCreateTowerPool()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TowerPoolDef>(TowerPoolPath);
            if (existing != null)
            {
                MergeChapter2Cards(existing); // F3.2：水系三卡自愈合并（只添缺，不动用户调过的值）
                MergeChapter3Cards(existing); // F3.4（ch3）：灯/法自愈合并（只添缺，不动用户调过的值）
                MergeChapter4Cards(existing); // F4.2-F4.5（ch4）：火山四卡自愈合并（只添缺；盆/炮/烬随各自单元进）
                MergeChapter5Cards(existing); // F3.1（ch5）：镜自愈合并（只添缺，不动用户调过的值）
                return existing;
            }

            if (File.Exists(TowerPoolPath))
                DeleteBrokenAsset(TowerPoolPath);

            var def = ScriptableObject.CreateInstance<TowerPoolDef>();
            def.chars = new List<TowerCharStats>
            {
                // GDD §5.1 v3.0（design/numbers/塔防表.csv）；value 驱动一口价与卖价
                // AOE 形态字段（矛矩形/棍圆形/戟扇形）随战斗机制实装补
                new TowerCharStats { charId = "刀", role = "近战高伤", value = 8, damage = 30, interval = 0.9, range = 1.8 },
                new TowerCharStats { charId = "弓", role = "远程单体", value = 10, damage = 12, interval = 0.7, range = 3.5, antiAir = true, ranged = true, rangePerLevel = 0.25 },
                new TowerCharStats { charId = "矛", role = "中远程群体", value = 14, damage = 12, interval = 1.2, range = 3.0, ranged = true, aoeShape = AoeShape.Rect, aoeSize = 2, aoeParam = 1 },   // 命中点矩形 2×1，仅地面
                new TowerCharStats { charId = "墨", role = "经济产墨", value = 16, damage = 0, interval = 1.0, inkPerPulse = 1 },
                new TowerCharStats { charId = "铲", role = "机制卡·开垦(放塔流程拦截)", value = 6, damage = 0, interval = 1.0 },
                new TowerCharStats { charId = "棍", role = "近战范围", value = 12, damage = 14, interval = 1.3, range = 1.5, aoeShape = AoeShape.Circle, aoeSize = 1.5 },                        // 塔中心圆 r1.5，仅地面
                new TowerCharStats { charId = "戟", role = "近战重范围", value = 16, damage = 28, interval = 1.6, range = 2.0, aoeShape = AoeShape.Sector, aoeSize = 2, aoeParam = 120 },         // 塔中心扇形 120°r2，仅地面
                new TowerCharStats { charId = "弩", role = "远程狙击", value = 18, damage = 40, interval = 1.4, range = 4.5, antiAir = true, ranged = true, rangePerLevel = 0.25 },
                Chapter2Water, Chapter2Fisherman, Chapter2Ice, // F3：水系三卡（GDD §5.2 黄底；渔塔=Fisherman 避与鱼敌重名——CS0102 实录）
                Chapter3Lamp, Chapter3Mage,                   // F3.4（ch3）：灯/法（GDD §5.2 黄底）
                Chapter4Fire,                                  // F4.2（ch4）：火山四卡之一（GDD §5.5 黄底）
                Chapter4Pot,                                   // F4.3（ch4）：盆（道具卡·复绿）
                Chapter4Cannon,                                // F4.4（ch4）：炮（远程对空溅射）
                Chapter4Ember,                                 // F4.5（ch4）：烬（远程单体·对空点杀）
                Chapter5Mirror,                                // F3.1（ch5）：镜（双面覆盖·对面镜像半伤）
                Chapter5Thunder,                               // F3.2（ch5）：雷（远程链电·后方三跳）
                Chapter5Wrath,                                 // F3.3（ch5）：煞（近战重击·虚面 ×1.5）
                Chapter5Banner,                                // F3.4（ch5）：幡（魂幡经济·击杀聚魂）
            };
            return SaveAndReload(def, TowerPoolPath);
        }

        // ============ F3 水系三卡（GDD §5.2，2026-10-04 定案；数值黄底，W3 定稿） ============

        /// <summary>水塔：范围控制——命中缓速 30%/2s + 湿标记 3s；水格/淹没格可建、免疫沉默。</summary>
        private static TowerCharStats Chapter2Water => new TowerCharStats
        {
            charId = "水", role = "水泽范围控制",
            value = 12, damage = 10, interval = 1.0, range = 3.0,
            ranged = true,
            waterImmune = true,
            slowFactor = 0.3, slowSeconds = 2, wetSeconds = 3, // F3.1 状态：缓速 + 湿（冰查 ×2）
        };

        /// <summary>渔塔：经济——零攻击；产墨 1/2/4/8/16 每跳（×2 通道）；水中 5s/跳、陆上 10s/跳。</summary>
        private static TowerCharStats Chapter2Fisherman => new TowerCharStats
        {
            charId = "渔", role = "水泽经济·水中满速产墨",
            value = 12, damage = 0, interval = 1.0,
            inkPerPulse = 1,
            waterImmune = true,
            landPulseSlower = true, // F3.3：陆上跳隔 ×2（InkPulseSeconds 基准 5s → 陆上 10s）
        };

        /// <summary>冰塔：远程强控——冻结 1.2s + 湿敌 ×2 + 可对空；水格可建、免疫沉默。</summary>
        private static TowerCharStats Chapter2Ice => new TowerCharStats
        {
            charId = "冰", role = "水泽远程强控·湿冻连携",
            value = 16, damage = 16, interval = 1.5, range = 3.5,
            ranged = true, antiAir = true, // GDD §5.2：可打空中（对空远程 +1）
            waterImmune = true,
            freezeSeconds = 1.2, wetDamageMult = 2.0, // F3.1 状态：冻结 + 查湿翻倍
        };

        /// <summary>
        /// F3.2 自愈合并：已有塔池缺哪张水系卡就补哪张（按 charId 查缺）。
        /// 只添缺不动旧值——用户/策划 Inspector 调过的数值永不覆盖（双轨纪律）。
        /// </summary>
        private static void MergeChapter2Cards(TowerPoolDef pool)
        {
            bool changed = false;
            if (pool.chars == null) pool.chars = new List<TowerCharStats>();
            var have = new HashSet<string>();
            foreach (var c in pool.chars) have.Add(c.charId);
            foreach (var add in new[] { Chapter2Water, Chapter2Fisherman, Chapter2Ice })
            {
                if (have.Contains(add.charId)) continue;
                pool.chars.Add(add);
                changed = true;
                Debug.Log("[Setup] 塔池自愈补卡：" + add.charId + "（" + add.role + "）");
            }
            if (changed)
            {
                EditorUtility.SetDirty(pool); // SO 直改必须 SetDirty（坑清单）
                AssetDatabase.SaveAssets();
            }
        }

        private static EnemyTableDef GetOrCreateEnemyTable()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyTableDef>(EnemyTablePath);
            if (existing != null)
            {
                MergeChapter2Enemies(existing); // F4.1：水系四敌自愈合并（只添缺，不动用户调过的值）
                MergeChapter3Enemies(existing); // F4.1（ch3）：幽夜五敌自愈合并（只添缺，不动用户调过的值）
                MergeChapter4Enemies(existing); // F5.1（ch4）：火山四新敌自愈合并（烬鼠/火蜥/岩龟/岩燕）
                MergeChapter5Enemies(existing); // F4.1（ch5）：诡异卷敌自愈合并（纸/影；魇/傀/小鬼后续单元追加）
                return existing;
            }

            if (File.Exists(EnemyTablePath))
                DeleteBrokenAsset(EnemyTablePath);

            var def = ScriptableObject.CreateInstance<EnemyTableDef>();
            def.enemies = new List<EnemyStats>
            {
                // GDD §6 v3.0 第一章草原 10 型 + Boss 召唤物（design/numbers/敌人表.csv）
                // 特性字段（蛛分裂/牛半血加速/酋长召唤）随战斗机制实装补
                new EnemyStats { charId = "虫", hp = 15, speed = 1.0, inkDrop = 2, livesCost = 1 },
                new EnemyStats { charId = "蛛", hp = 30, speed = 0.8, inkDrop = 3, livesCost = 1, onDeathSpawns = "虫", onDeathSpawnCount = 3 }, // 死亡分裂 3 虫（分裂虫不掉墨）
                new EnemyStats { charId = "狼", hp = 18, speed = 1.8, inkDrop = 3, livesCost = 1 },
                new EnemyStats { charId = "熊", hp = 120, speed = 0.6, inkDrop = 6, livesCost = 1 },
                new EnemyStats { charId = "虎", hp = 300, speed = 0.9, inkDrop = 10, livesCost = 1 },     // 厚血精英
                new EnemyStats { charId = "鸟", hp = 40, speed = 1.2, inkDrop = 5, livesCost = 1, flying = true },
                new EnemyStats { charId = "鹰", hp = 60, speed = 1.6, inkDrop = 8, livesCost = 1, flying = true },
                new EnemyStats { charId = "牛", hp = 150, speed = 0.8, inkDrop = 12, livesCost = 1, enrageAtRatio = 0.5, enrageSpeedMult = 2.0 }, // 半血速度×2
                new EnemyStats { charId = "野人", hp = 700, speed = 0.5, inkDrop = 15, livesCost = 2 },  // 精英扣 2 印
                new EnemyStats { charId = "酋长", hp = 9000, speed = 0.7, inkDrop = 30, livesCost = 5, isBoss = true, summons = "召唤野人", summonInterval = 20, summonCount = 2, summonCap = 6 }, // Boss 召唤（20s×2 上限6）
                new EnemyStats { charId = "召唤野人", hp = 150, speed = 0.8, inkDrop = 0, livesCost = 1 }, // 召唤物不掉墨
                Chapter2Fish, Chapter2Shrimp, Chapter2Turtle, Chapter2Crocodile, // F4：水系四敌（GDD §6 黄底）
                Chapter2Dragon, // F4.4 随二L5W7 落池（一阶段=水路Boss+召唤；转阶段洪水归 F5 实装）
                Chapter3Rat, Chapter3Moth, Chapter3Bat, Chapter3Cat, Chapter3Owl, // F4.1（ch3）：幽夜五敌（GDD §6 黄底；枭状态机归 F5）
                Chapter4AshRat, Chapter4FireLizard, Chapter4RockTurtle, Chapter4RockSwallow, // F5.1：火山四新敌（GDD §6 黄底）
                Chapter4BossLei, // F6.1：麟 Boss（28000/0.6 + 践踏光环 1.5 + 免疫燃烧岩浆）
                Chapter5Paper, Chapter5Shadow, // F4.1（ch5）：纸 / 影（06 §14.6 黄底）
                Chapter5Nightmare,             // F4.2（ch5）：魇（飞行 + 入梦 2s）
                Chapter5Puppet,                // F4.3（ch5）：傀（精英 + 换影复活）
                Chapter5Imp,                   // F4.4（ch5）：小鬼（青脸召唤物 · 到终点 −3 墨）
                Chapter5NuoMask,               // F5.1（ch5）：傩面 P1（10000 / 0.55 / 三脸轮换 / 到出口回起点）
                Chapter5NuoShen,               // F6（ch5）：傩神 P2（12000 / 0.5 / 三面随行 + 傩舞）
            };
            return SaveAndReload(def, EnemyTablePath);
        }

        // ============ F4 水系敌（GDD §6 / 敌人草稿.csv，2026-10-04；数值黄底，W3 定稿） ============

        /// <summary>鱼：水路快脆炮灰——水中 ×1.5（1.4→2.1）。</summary>
        private static EnemyStats Chapter2Fish => new EnemyStats
        {
            charId = "鱼", hp = 24, speed = 1.4, inkDrop = 3, livesCost = 1,
            lane = 1, waterSpeedMult = 1.5, // F4.1（ADR-034）：水路航道段 ×1.5，过汇合转主路原速
        };

        /// <summary>虾：水路小群体（波量大调剂）——水中 ×1.5。</summary>
        private static EnemyStats Chapter2Shrimp => new EnemyStats
        {
            charId = "虾", hp = 32, speed = 1.0, inkDrop = 3, livesCost = 1,
            lane = 1, waterSpeedMult = 1.5,
        };

        /// <summary>龟：水路厚血慢速（熊量级上调）——水中 0.8 绝对值（0.5 × 1.6 换算，黄底口径）。</summary>
        private static EnemyStats Chapter2Turtle => new EnemyStats
        {
            charId = "龟", hp = 200, speed = 0.5, inkDrop = 8, livesCost = 1,
            lane = 1, waterSpeedMult = 1.6, // CSV「水中0.8」= 0.5×1.6；planner 若要 ×1.5 → 0.75 调表即可
        };

        /// <summary>鳄：两栖精英——水路出生→汇合转主路，水路段 ×1.5、主路段原速（分段变速零专用代码：航道判定天然分段）。
        /// **F5.2**：火山客串 = 挂水系标签，图内**燃烧易伤 ×2**（水怪怕火，06 §12.4 望文生义克向反转）；
        /// ch2 本图零变化（ch2 池无火/炮 = 无燃烧源，标记无消费点）。</summary>
        private static EnemyStats Chapter2Crocodile => new EnemyStats
        {
            charId = "鳄", hp = 650, speed = 0.7, inkDrop = 14, livesCost = 2, // 精英扣 2 印（野人同档）
            lane = 1, waterSpeedMult = 1.5,
            burnVulnerable = true, // F5.2：燃烧易伤 ×2（火山图内「水怪怕火」）
        };

        /// <summary>
        /// 蛟（Boss，GDD §6 黄底）：水路出身→汇合转主路；召唤 2 鱼/20s 上限 6（复用酋长管线）。
        /// 一阶段水中不吃加速（慢速悠游，06 §10.3）——waterSpeedMult=1；1/3 血转阶段（F5 实装）：
        /// 蓄水 5s → 全图洪水（水线 4）+ 召唤暂停 10s + 蛟水乘改 1.5 → 潮落 60s 右→左四档退水。
        /// 召唤物 = 正规鱼（掉墨 3，CSV 口径——与酋长零墨召唤物不同，经济刷取风险黄底期观察）。
        /// </summary>
        private static EnemyStats Chapter2Dragon => new EnemyStats
        {
            charId = "蛟", hp = 13000, speed = 0.8, inkDrop = 40, livesCost = 5,
            isBoss = true, lane = 1, waterSpeedMult = 1.0, phase2HpRatio = 1.0 / 3.0,
            summons = "鱼", summonInterval = 20, summonCount = 2, summonCap = 6,
        };

        /// <summary>
        /// F4.1 自愈合并：已有敌表缺哪只水系敌就补哪只（按 charId 查缺）。
        /// 只添缺不动旧值——用户/策划 Inspector 调过的数值永不覆盖（双轨纪律）。蛟（Boss）归 F5 种子。
        /// </summary>
        private static void MergeChapter2Enemies(EnemyTableDef table)
        {
            bool changed = false;
            if (table.enemies == null) table.enemies = new List<EnemyStats>();
            var have = new HashSet<string>();
            foreach (var e in table.enemies) have.Add(e.charId);
            foreach (var add in new[] { Chapter2Fish, Chapter2Shrimp, Chapter2Turtle, Chapter2Crocodile, Chapter2Dragon })
            {
                if (have.Contains(add.charId)) continue;
                table.enemies.Add(add);
                changed = true;
                Debug.Log("[Setup] 敌表自愈补敌：" + add.charId);
            }
            // F5.2 自愈补缺：旧表蛟行建于 phase2HpRatio 字段落地前（缺省 0）——0 视为缺、补 1/3；
            // 用户/策划显式调过非 0 值则永不覆盖（双轨纪律；F4.5 哨兵升级同型）。
            foreach (var e in table.enemies)
                if (e.charId == "蛟" && e.phase2HpRatio <= 0)
                {
                    e.phase2HpRatio = 1.0 / 3.0;
                    changed = true;
                    Debug.Log("[Setup] 敌表自愈补蛟转阶段血线：1/3");
                }
            // F5.2 自愈补缺：旧表鳄行建于 burnVulnerable 字段落地前（缺省 false）——补 true
            //（设计定案标记：火山图内燃烧 ×2。bool 无「未设」哨兵，故同蛟型直接保证为 true；
            // 若策划要改口径，改本行或告知程序侧换哨兵）。
            foreach (var e in table.enemies)
                if (e.charId == "鳄" && !e.burnVulnerable)
                {
                    e.burnVulnerable = true;
                    changed = true;
                    Debug.Log("[Setup] 敌表自愈补鳄燃烧易伤：×2（水怪怕火）");
                }
            if (changed)
            {
                EditorUtility.SetDirty(table); // SO 直改必须 SetDirty（坑清单）
                AssetDatabase.SaveAssets();
            }
        }

        // ============ F3.4/F4.1 ch3 幽夜卡与敌（GDD §5.2/§6 / 草稿 CSV，2026-10-08；数值黄底手测定稿） ============

        /// <summary>
        /// 灯（ch3 黄底）：照明辅助——零攻击零产墨；照明半径 T1 1.5、每阶+0.5→T5 3.5
        /// （v2 2026-10-08 用户验收反馈「所有阶小一点」：range 2.0→1.5 全阶 -0.5，升阶步进不稀释；
        /// 借 range/rangePerLevel 升阶管线——升阶=世界变亮，配对合并联动零新做）。
        /// LightSystem（F1.2）按灯 range 画光圈；被枭栖时锁死+5×阶数秒后消失（F5.1）。
        /// </summary>
        private static TowerCharStats Chapter3Lamp => new TowerCharStats
        {
            charId = "灯", role = "幽夜照明·光圈",
            value = 10, damage = 0, interval = 1.0, range = 1.5,
            rangePerLevel = 0.5, // 塔池草稿.csv「半径+0.5/级」：T1 1.5 → T5 3.5
        };

        /// <summary>
        /// 法（ch3 黄底）：远程控制——可对空，14 伤害/1.0s/射程 3.5；塔位于灯光圈内 → 攻击
        /// 20% 概率眩晕、时长 = 灯阶数 ÷ 2（多盏取最高阶；F3.3 实装；枭免疫眩晕）。
        /// </summary>
        private static TowerCharStats Chapter3Mage => new TowerCharStats
        {
            charId = "法", role = "幽夜远程控制·灯下眩晕",
            value = 14, damage = 14, interval = 1.0, range = 3.5,
            ranged = true, antiAir = true,
            rangePerLevel = 0.25, // 塔池草稿.csv「伤害×1.8/级;射程+0.25格/级」
        };

        /// <summary>
        /// F4.2 火塔（GDD §5.5 / 06 §12.3 黄底）：近战范围——塔中心圆 r1.5 溅射仅地面 +
        /// 命中挂燃（3/s×3s）；**免疫岩浆**（LavaImmuneTowers 白名单——红闪落脚下幸存
        /// 「死灰复燃」）；灰烬格可建（IsAshTower 白名单）+ 站灰格**全伤** ×ashDamageMult
        ///（AshBuffLookup——F4.1/F7.4 机制）。数值随 F7 手测定稿（当前任务单行：
        /// value 12 / 10 伤 / 1.2s / 圆 r1.5）。
        /// </summary>
        private static TowerCharStats Chapter4Fire => new TowerCharStats
        {
            charId = "火", role = "火山近战范围·命中挂燃",
            value = 12, damage = 10, interval = 1.2, range = 1.5,
            aoeShape = AoeShape.Circle, aoeSize = 1.5, // 塔中心圆 r1.5 仅地面（DamageArea 排除飞行）
            burnSeconds = 3, burnDps = 3,              // F4.1：命中挂燃（与 GameParams 黄底默认同源——Inspector 双处可调）
        };

        /// <summary>
        /// F4.3 盆（GDD §5.5 / 06 §12.3）：**道具卡·复绿**，零攻击零产墨，**仅可放灰烬格**——
        /// 放下即消耗（同铲口径）；烧前为塔防格 → 格变绿框（SetGreen：任意塔含墨可建其上），
        /// 烧前为荒地/垦中 → ClearAsh 除灰回原象（**开垦要求照旧**，用户 2026-10-18 终裁）；
        /// 绿格再被岩浆烧 = 回 Ash（盆+其上之塔同毁；塔按免疫规则才是幸存方——用户 2026-10-13 口径①）。
        /// **不可移动/不可卖**（用户 2026-10-13 终裁——设计档原「移动/卖照常通用」作废）；
        /// **不参与合成**（放塔流程在 GameManager 拦截，同铲）。数值随 F7 手测定稿。
        /// </summary>
        private static TowerCharStats Chapter4Pot => new TowerCharStats
        {
            charId = "盆", role = "机制卡·复绿(放塔流程拦截)",
            value = 6, damage = 0, interval = 1.0, // 同铲种子形态（机制卡：无攻击无产墨）
        };

        /// <summary>
        /// F4.4 炮（GDD §5.5 / 06 §12.3）：远程范围·**对空**——射程 4.0、命中点**圆溅射 r1.2
        /// 仅地面**（DamageArea 圆以传入点为中心：远程 = 命中点——0.4 管线复用）；命中附加燃烧
        /// （3/s×3s）；**免疫岩浆**（LavaImmuneTowers 名单）+ 灰烬格可建（IsAshTower）+ 站灰格燃烧
        /// ×ashDamageMult（AshBuffLookup——F7.4 起直接攻击同享）；放塔 UI 灰格增益位金框（IsLavaBuffTower）。
        /// 数值随 F7 手测定稿。
        /// </summary>
        private static TowerCharStats Chapter4Cannon => new TowerCharStats
        {
            charId = "炮", role = "火山远程范围·对空命中挂燃",
            value = 16, damage = 12, interval = 1.6, range = 4.0,
            ranged = true, antiAir = true,          // 弹道 + 可打空中（岩燕——F5.1 落敌）
            aoeShape = AoeShape.Circle, aoeSize = 1.2, // 命中点圆 r1.2（溅射不吃空：DamageArea 跳过飞行）
            burnSeconds = 3, burnDps = 3,             // F4.1：命中挂燃
        };

        /// <summary>
        /// F4.5 烬（GDD §5.5 / 06 §12.3；用户定案 2026-10-20）：远程**单体**——90 伤 / 3.0s / 射程 4.5，
        /// **可打空中**、**无燃烧**（纯直伤，与火/炮的 DoT 身份分开）；value 20（全游戏最贵）。
        /// **免疫岩浆**（LavaImmuneTowers 名单）+ 灰烬格可建（IsAshTower）+ 站灰格**全伤** ×ashDamageMult
        /// （AshBuffLookup——与火/炮同一条统一律，零新逻辑）；放塔 UI 灰格增益位金框（IsLavaBuffTower）。
        /// 升阶 = 伤害 ×1.8/级（通用）、射程 +0.25/级（单体卡无 AOE 范围通道）。数值随 F7 手测定稿。
        /// **设计配位**：最终 Boss 麒麟的单体答案——炮开荒清群、烬收尾打 Boss；对烬鼠海（50 血）严重溢出。
        /// </summary>
        private static TowerCharStats Chapter4Ember => new TowerCharStats
        {
            charId = "烬", role = "火山远程单体·对空点杀",
            value = 20, damage = 90, interval = 3.0, range = 4.5,
            ranged = true, antiAir = true,   // 弹道 + 可打空中
            rangePerLevel = 0.25,            // 塔池草稿.csv「伤害×1.8/级;射程+0.25格/级」
        };

        /// <summary>F3.4 自愈合并：已有塔池缺灯/法就补（按 charId 查缺；只添缺不动旧值——双轨纪律）。
        /// 灯射程改版自愈（v2 2026-10-08 用户验收「所有阶小一点」）：哨兵 = range 仍为旧种子 2.0
        /// （未手调）→ 升 1.5（全阶 -0.5）；自调过则永不覆盖（蛟 phase2 哨兵同型）。</summary>
        private static void MergeChapter3Cards(TowerPoolDef pool)
        {
            bool changed = false;
            if (pool.chars == null) pool.chars = new List<TowerCharStats>();
            var have = new HashSet<string>();
            foreach (var c in pool.chars) have.Add(c.charId);
            foreach (var add in new[] { Chapter3Lamp, Chapter3Mage })
            {
                if (have.Contains(add.charId)) continue;
                pool.chars.Add(add);
                changed = true;
                Debug.Log("[Setup] 塔池自愈补卡：" + add.charId + "（" + add.role + "）");
            }

            // 灯射程哨兵升级：旧种子 2.0 → 1.5（T1 1.5/每阶+0.5 → T5 3.5）
            var lamp = pool.chars.Find(c => c.charId == "灯");
            if (lamp != null && lamp.range == 2.0)
            {
                lamp.range = 1.5;
                changed = true;
                Debug.Log("[Setup] 塔池自愈：灯照明半径 2.0 → 1.5（用户 2026-10-08 验收反馈，全阶 -0.5）");
            }
            if (changed)
            {
                EditorUtility.SetDirty(pool); // SO 直改必须 SetDirty（坑清单）
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>F4.2/F4.3/F4.4/F4.5 自愈合并：已有塔池缺火/盆/炮/烬就补（各单元随池进的既定
        /// 纪律，F1.3 骨架「防幽灵卡」条款的兑现）。只添缺不动旧值——双轨纪律。</summary>
        private static void MergeChapter4Cards(TowerPoolDef pool)
        {
            bool changed = false;
            if (pool.chars == null) pool.chars = new List<TowerCharStats>();
            var have = new HashSet<string>();
            foreach (var c in pool.chars) have.Add(c.charId);
            foreach (var add in new[] { Chapter4Fire, Chapter4Pot, Chapter4Cannon, Chapter4Ember })
            {
                if (have.Contains(add.charId)) continue;
                pool.chars.Add(add);
                changed = true;
                Debug.Log("[Setup] 塔池自愈补卡：" + add.charId + "（" + add.role + "）");
            }
            if (changed)
            {
                EditorUtility.SetDirty(pool); // SO 直改必须 SetDirty（坑清单）
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>F3.1 自愈合并：已有塔池缺镜就补（按 charId 查缺；只添缺不动旧值——双轨纪律）。</summary>
        private static void MergeChapter5Cards(TowerPoolDef pool)
        {
            bool changed = false;
            if (pool.chars == null) pool.chars = new List<TowerCharStats>();
            var have = new HashSet<string>();
            foreach (var c in pool.chars) have.Add(c.charId);
            foreach (var add in new[] { Chapter5Mirror, Chapter5Thunder, Chapter5Wrath, Chapter5Banner })
            {
                if (have.Contains(add.charId)) continue;
                pool.chars.Add(add);
                changed = true;
                Debug.Log("[Setup] 塔池自愈补卡：" + add.charId + "（" + add.role + "）");
            }
            if (changed)
            {
                EditorUtility.SetDirty(pool); // SO 直改必须 SetDirty（坑清单）
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// F3.1 镜（06 §14.5；docs/13 F3.1）：20 墨 / 22 伤 / 1.0s / 射程 3.5 / 单体可对空，
        /// **双面覆盖**——建成时在对面同坐标生成 50% 镜像（11 伤 / 1.0s）；镜像不可卖/升/移，
        /// 本体卖/毁同步消失；对面同格已有塔 → 红框禁放。镜像不随本体升阶（数值定格）。
        /// 配位：双面章唯一的「一卡压两面」卡——实面放镜 = 虚面同格也有火力。
        /// </summary>
        private static TowerCharStats Chapter5Mirror => new TowerCharStats
        {
            charId = "镜", role = "诡异双面覆盖·镜像半伤",
            value = 20, damage = 22, interval = 1.0, range = 3.5,
            antiAir = true, ranged = true,   // 单体可对空（弹道 = 折射光）
            createsMirror = true, mirrorDamageRatio = 0.5,
        };

        /// <summary>
        /// F3.2 雷（06 §14.5；docs/13 F3.2）：18 墨 / 26 伤 / 1.6s / 射程 4.0 / 可对空，
        /// **链电**——命中后沿路径**后方**（dist 更小）逐跳传导 3 跳，每跳 ×0.6（26→15.6→9.4→5.6），
        /// 下一跳须在上一跳落点 2.0 格内，超距/无目标/已链过即断链；同面筛选（跨面不链）。
        /// 配位：虚面收小鬼/狼群的清场卡（一侧放电，整列后排一起掉血）。
        /// </summary>
        private static TowerCharStats Chapter5Thunder => new TowerCharStats
        {
            charId = "雷", role = "诡异远程链电·后方三跳",
            value = 18, damage = 26, interval = 1.6, range = 4.0,
            ranged = true, antiAir = true,   // 可对空（链按真实坐标含悬空高度）
            chainJumps = 3, chainRatio = 0.6, chainRadius = 2.0,
        };

        /// <summary>
        /// F3.3 煞（06 §14.5；docs/13 F3.3）：16 墨 / 24 伤 / 1.3s / 相邻圆 r1.5（近战范围，仅地面），
        /// **虚面（face=1）伤害 ×1.5** —— 全游戏唯一虚面加成卡（不改全局数值）。
        /// 配位：虚面收小鬼/狼群的主力；实面同卡只是普通近战 AOE（平伤）。
        /// </summary>
        private static TowerCharStats Chapter5Wrath => new TowerCharStats
        {
            charId = "煞", role = "诡异近战重击·虚面加成",
            value = 16, damage = 24, interval = 1.3, range = 1.5,
            aoeShape = AoeShape.Circle, aoeSize = 1.5, // 塔中心圆 r1.5，仅地面
            voidDamageMult = 1.5,                       // F3.3：虚面 ×1.5（实面 = 1.0 平伤）
        };

        /// <summary>
        /// F3.4 幡（06 §14.5；docs/13 F3.4）：12 墨 / 无攻击，**魂幡经济**——地图上每有敌死 +1 魂
        /// （两面都算），每 10 魂结 +5 墨，升阶收益 ×2/级（5/10/20/40/80）；**多幡不叠加**，只按最高阶幡结算；
        /// 魂不进墨塔统计、每关清零（Q3 定稿）。配位：双面章唯一「不占地也能吃两面击杀」的产墨卡。
        /// </summary>
        private static TowerCharStats Chapter5Banner => new TowerCharStats
        {
            charId = "幡", role = "诡异魂幡经济·击杀聚魂",
            value = 12, damage = 0, interval = 1.0,
            soulBanner = true, soulPayout = 5,
        };

        // ============ F4.1 ch3 幽夜敌（GDD §6 / 敌人草稿.csv，2026-10-08；数值黄底手测定稿） ============
        /// <summary>鼠：夜行群体炮灰（雾中深影可见）——鱼量级 ×1.5。nocturnal 夜行加速 F2.2 打标。</summary>
        private static EnemyStats Chapter3Rat => new EnemyStats
        {
            charId = "鼠", hp = 36, speed = 1.5, inkDrop = 4, livesCost = 1,
        };

        /// <summary>蛾：飞行小群——光中受伤 ×1.5（飞蛾扑火）。</summary>
        private static EnemyStats Chapter3Moth => new EnemyStats
        {
            charId = "蛾", hp = 30, speed = 1.3, inkDrop = 4, livesCost = 1,
            flying = true, lightVulnerable = true, // F2.2 光脆
        };

        /// <summary>蝠：飞行潜行（雾中颜色变浅、光中现形）+ 光中减速 ×0.7。</summary>
        private static EnemyStats Chapter3Bat => new EnemyStats
        {
            charId = "蝠", hp = 45, speed = 1.5, inkDrop = 6, livesCost = 1,
            flying = true, stealth = true, lightHindered = true, // F2.2 潜行 + 光滞
        };

        /// <summary>猫：潜行精英（雾中颜色变浅、光中现形）——虎量级（扣 2 印）。</summary>
        private static EnemyStats Chapter3Cat => new EnemyStats
        {
            charId = "猫", hp = 260, speed = 1.1, inkDrop = 12, livesCost = 2,
            stealth = true, // F2.2 潜行
        };

        /// <summary>
        /// 枭（Boss，GDD §6 黄底）：沿 S 路步行 0.7；雾中红闪不可选中（F2.1 tint + F1.2 谓词）；
        /// 踏入光照瞬移栖灯（灯锁死 5×灯阶数秒后消失、期间灯亮=枭可打=杀窗、枭回原位续走——F5.1）；
        /// 1/2 血无敌 5s→黑屏转场→换端重走两圈（F5.2）；免疫眩晕（F3.3）。
        /// 本单元（F1.3+F4.2）先落基础数值 + phase2HpRatio（蛟先例：字段先落数值、状态机后续批次实装）。
        /// </summary>
        private static EnemyStats Chapter3Owl => new EnemyStats
        {
            charId = "枭", hp = 19500, speed = 0.7, inkDrop = 50, livesCost = 5,
            isBoss = true, phase2HpRatio = 0.5, stunImmune = true, // 1/2 血转阶段（ADR-041）；免疫眩晕（Boss 纪律——旧资产走自愈）
        };

        // ============ F5.1 火山四新敌（GDD §6 / design/numbers/草稿/敌人草稿.csv 四L 段；
        //              数值黄底，F7 手测定稿。麟 28000 归 F6——本单元不落） ============

        /// <summary>烬鼠：火山群体炮灰（波量 15-25 海量）——AoE 靶（棍/戟/矛范围体验）。</summary>
        private static EnemyStats Chapter4AshRat => new EnemyStats
        {
            charId = "烬鼠", hp = 50, speed = 1.6, inkDrop = 2, livesCost = 1,
        };

        /// <summary>
        /// 火蜥：**灾害区原生**——免疫岩浆与燃烧（burnImmune 一旗双拦：岩浆 burst/红印 DoT + 塔挂燃，
        /// 三处消费点同拦）+ **站/路过红印格速度 ×1.3**（lavaMarkSpeedMult——灾害区是它的主场）。
        /// 首秀四L2 W1（免疫示范波，波次草稿.csv）。
        /// </summary>
        private static EnemyStats Chapter4FireLizard => new EnemyStats
        {
            charId = "火蜥", hp = 650, speed = 1.2, inkDrop = 14, livesCost = 1, // F7.4 血 65 → 650；F7.5④ 定价 掉墨 8 → 14；扣印 1（2026-10-22 用户裁定：2 → 1 回落——成组中速扣 2 漏 3 只即败过狠）
            burnImmune = true,        // F5.1：免疫燃烧+岩浆（F2.3 burst / F2.4 DoT / F4.1 挂燃）
            lavaMarkSpeedMult = 1.3,  // F5.1：红印上提速（「岩浆烫它 = 白送加速」）
        };

        /// <summary>岩龟：厚血慢速 + **燃烧挂不上**（硬壳 = burnImmune）——AoE 单体对照，
        /// 逼玩家做目标选择（火/炮对它是纯伤害，无 DoT 加成）。首秀四L3 W1。</summary>
        private static EnemyStats Chapter4RockTurtle => new EnemyStats
        {
            charId = "岩龟", hp = 4000, speed = 0.5, inkDrop = 30, livesCost = 2, // F7.4 血 400 → 4000；F7.5④ 定价 掉墨 12 → 30（肉怪重定价；扣印 2 不变）
            burnImmune = true,        // F5.1：硬壳拒燃（含岩浆免疫）
        };

        /// <summary>岩燕：飞行 + 快速（空靶——考炮对空 F4.4 antiAir 的对位靶；弓/弩/法亦可）。
        /// 首秀四L4 W1（岩燕×5 + 鸟×6 混编）。</summary>
        private static EnemyStats Chapter4RockSwallow => new EnemyStats
        {
            charId = "岩燕", hp = 65, speed = 1.8, inkDrop = 7, livesCost = 1,
            flying = true,
        };

        /// <summary>
        /// 麟（Boss，GDD §6 / 敌人草稿.csv 黄底；HP 锚 24000-32000，F7.1 手测定稿）：沿环步行 0.6；
        /// **践踏光环**（trampleRadius 1.5——半径内建造格持续变灰、路径格不毁、提示圈常显，F6.1）；
        /// **免疫燃烧与岩浆**（burnImmune 一旗三拦——蛟免疫冰冻/枭免疫眩晕同款纪律，F6.1 打标）；
        /// 转阶段（1/2 血：无敌定身 5s → 全场红闪 3s → 共振雨 6-8 格 + 召唤幼蜥×6，**F6.2 已落**）与
        /// 二阶段（践踏 1.5→2.5、移速 ×1.25，**F6.3 已落**——演出毕即强化）。
        /// **F7.5（用户定案 2026-10-19）：践踏蓄力**（每 7 格站定 2.5s，圈扩 2.5 高亮，**蓄力期不无敌**
        /// = 补出来的输出窗）+ **二圈**（转阶段收尾回环起点重走，漏过判败顺延到二圈终点）。
        /// 漏过即败走 isBoss 既有管线（二圈经 lapsLeft 顺延）。
        /// </summary>
        private static EnemyStats Chapter4BossLei => new EnemyStats
        {
            charId = "麟", hp = 15000, speed = 0.6, inkDrop = 60, livesCost = 5, // F7.1b ③ 定档：28000 → 24000 → **15000**（用户手测定档 2026-10-18；F7.5 补输出窗后有效窗 ≈150s）
            isBoss = true,
            burnImmune = true,   // F6.1：免疫燃烧+岩浆（F2.3 burst / F2.4 红印 DoT / F4.1 挂燃 三处同拦）
            trampleRadius = 1.5, // F6.1：践踏光环半径（F6.3 二阶段运行时改写 2.5）
            phase2HpRatio = 0.5, // F6.2：1/2 血转阶段（无敌定身 5s → 全场红闪 3s → 共振雨 + 幼蜥群 ×6）
            chargeEveryCells = 7,   // F7.5③：每走 7 格原地蓄力一次（黄底 6-8；0 = 关闭）
            chargeSeconds = 2.5,    // F7.5③：蓄力站定 2.5s（**不无敌**——Damage/燃烧照常 = 免费输出窗）
            chargeRadiusMult = 2.5, // F7.5③：蓄力重踏半径 2.5 格（常规走行仍 trampleRadius 1.5）
        };

        /// <summary>F5.1/F6.1 自愈合并：已有敌表缺 ch4 五敌就补（按 charId 查缺；只添缺不动旧值——
        /// 双轨纪律，ch2/ch3 同律；麟随 F6.1 入名单）。</summary>
        private static void MergeChapter4Enemies(EnemyTableDef table)
        {
            bool changed = false;
            if (table.enemies == null) table.enemies = new List<EnemyStats>();
            var have = new HashSet<string>();
            foreach (var e in table.enemies) have.Add(e.charId);
            foreach (var add in new[] { Chapter4AshRat, Chapter4FireLizard, Chapter4RockTurtle, Chapter4RockSwallow, Chapter4BossLei })
            {
                if (have.Contains(add.charId)) continue;
                table.enemies.Add(add);
                changed = true;
                Debug.Log("[Setup] 敌表自愈补敌：" + add.charId);
            }
            // F6.2 自愈补缺：旧表麟行由 F6.1 建于 phase2HpRatio 落地前（缺省 0）——0 视为缺、补 1/2；
            // 用户/策划显式调过非 0 值则永不覆盖（双轨纪律；蛟 1/3 同型）。
            foreach (var e in table.enemies)
                if (e.charId == "麟" && e.phase2HpRatio <= 0)
                {
                    e.phase2HpRatio = 0.5;
                    changed = true;
                    Debug.Log("[Setup] 敌表自愈补麟转阶段血线：1/2");
                }
            // F7.1b ③ 麟拉锯定档（用户手测 2026-10-18）：实机连试三档——28000 / 24000 打不过
            // → 用户定 **15000**（新种子默认值）。哨兵 = hp 仍为**旧种子值 28000 / 24000**（= 未手调）
            // 才降；手调过（∉ 二者）永不覆盖（双轨纪律；蛟 phase2HpRatio / 枭 perchSecondsPerLevel 同型）。
            // 数值锚：走程 弧长 49 ÷ 速 0.6 ≈ 82s，转阶段 8s 免伤 ⇒ 有效开火窗 ≈74s
            // → 15000 需均 ≈203 DPS（正解构筑 150-330 DPS 的中段：拉锯但可过）。
            // ⚠️ 低于母本锚下限 24000（-37%）——定稿回写时锚注一并改（docs/11 §6 F7.1）。
            // 注：用户若在 **Play 模式**里改 Inspector，退出即回滚（Unity 坑）→ 调参一律非运行期改，
            // 或改完跑一次本自愈把值落盘（哨兵按旧种子值判定「未手调」，手调值不受影响）。
            foreach (var e in table.enemies)
                if (e.charId == "麟" && (Mathf.Approximately((float)e.hp, 28000f) ||
                                        Mathf.Approximately((float)e.hp, 24000f)))
                {
                    e.hp = 15000;
                    changed = true;
                    Debug.Log("[Setup] ch4 麟调血：旧种子值 → 15000（F7.1b ③ 用户手测定档）");
                }
            // F7.4 小怪血量上调（用户手测 2026-10-19「火山关小怪太薄，尤其岩龟/火蜥」）：
            // 火蜥 65 → 650 / 岩龟 400 → 4000。哨兵 = 仍为**旧种子值**（未手调）才升；手调过永不覆盖
            //（双轨纪律，麟 hp 哨兵同型）。⚠️ 岩燕同为 65——必须按 charId 分派勿误伤（本次不动它）。
            foreach (var e in table.enemies)
            {
                if (e.charId == "火蜥" && Mathf.Approximately((float)e.hp, 65f))
                {
                    e.hp = 650;
                    changed = true;
                    Debug.Log("[Setup] ch4 火蜥调血：65 → 650（F7.4 用户手测）");
                }
                else if (e.charId == "岩龟" && Mathf.Approximately((float)e.hp, 400f))
                {
                    e.hp = 4000;
                    changed = true;
                    Debug.Log("[Setup] ch4 岩龟调血：400 → 4000（F7.4 用户手测）");
                }
            }
            // F7.5④ 肉怪重定价（用户定案 2026-10-19，扣印 2026-10-22 回落）：火蜥 掉墨 8 → 14、
            // **扣印 1**（曾 1 → 2，2026-10-22 用户裁定回落——成组中速扣 2 漏 3 只即 6 印直接败过狠）；
            // 岩龟 掉墨 12 → 30（扣印 2 不变）。哨兵 = 仍为**旧种子值**（未手调）才改；手调过永不覆盖
            //（双轨纪律，麟 hp / F7.4 血哨兵同型）。掉墨单字段判（只调过掉墨的不误伤扣印）。
            foreach (var e in table.enemies)
            {
                if (e.charId == "火蜥")
                {
                    if (Mathf.Approximately((float)e.inkDrop, 8f))
                    {
                        e.inkDrop = 14;
                        changed = true;
                        Debug.Log("[Setup] ch4 火蜥定价：掉墨 8 → 14（F7.5④ 肉怪重定价）");
                    }
                }
                else if (e.charId == "岩龟" && Mathf.Approximately((float)e.inkDrop, 12f))
                {
                    e.inkDrop = 30;
                    changed = true;
                    Debug.Log("[Setup] ch4 岩龟定价：掉墨 12 → 30（F7.5④ 肉怪重定价）");
                }
            }
            // F7.5③ 麟践踏蓄力参数自愈（用户定案 2026-10-19）：三字段后落 → 旧麟行缺省 0
            //（chargeRadiusMult 有 2.5 缺省值，仍按 <= 0 补以兼容旧序列化）。哨兵 = <= 0（未设/缺字段）
            // 才补；**手调过（> 0，含黄底 6-8 档）永不覆盖**（双轨纪律，同型）。
            foreach (var e in table.enemies)
            {
                if (e.charId != "麟") continue;
                if (e.chargeEveryCells <= 0)
                {
                    e.chargeEveryCells = 7;
                    changed = true;
                    Debug.Log("[Setup] ch4 麟蓄力：chargeEveryCells → 7（F7.5③）");
                }
                if (e.chargeSeconds <= 0)
                {
                    e.chargeSeconds = 2.5;
                    changed = true;
                    Debug.Log("[Setup] ch4 麟蓄力：chargeSeconds → 2.5（F7.5③）");
                }
                if (e.chargeRadiusMult <= 0)
                {
                    e.chargeRadiusMult = 2.5;
                    changed = true;
                    Debug.Log("[Setup] ch4 麟蓄力：chargeRadiusMult → 2.5（F7.5③）");
                }
            }
            if (changed)
            {
                EditorUtility.SetDirty(table); // SO 直改必须 SetDirty（坑清单）
                AssetDatabase.SaveAssets();
            }
        }

        // ============ F4.1 ch5 诡异卷四敌（06 §14.6；数值黄底，F7.2 手测定稿） ============

        /// <summary>
        /// 纸：炮灰——薄而快、扎堆（纸人海 = 链电/AoE 靶）。实虚两面都出（面由关级 `voidRatio`
        /// 自动分：五L2 起 70:30——「另一面也在流血」的第一课）。
        /// </summary>
        private static EnemyStats Chapter5Paper => new EnemyStats
        {
            charId = "纸", hp = 70, speed = 1.5, inkDrop = 3, livesCost = 1,
        };

        /// <summary>
        /// 影：**虚面追击者**——不吃非激活面半速（`nocturnal` 一旗两用：ch3 语义是「雾中 ×1.3」，
        /// 而 ch5 章 `fogEnabled=0` → 雾速那一支恒不触发（litCache 恒 true），只剩「半速白名单」这一半，
        /// 见 `BattleModel` 速度结算两行）。面恒虚：波组 `face=1` 显式指定（影只从虚路来）。
        /// </summary>
        private static EnemyStats Chapter5Shadow => new EnemyStats
        {
            charId = "影", hp = 90, speed = 1.6, inkDrop = 4, livesCost = 1,
            nocturnal = true,
        };

        /// <summary>
        /// 魇：**飞行**；每 6s 使半径 2.5 内（同面）最近一塔**入梦 2s**（停火、不降级）。
        /// 只锁攻击 = 墨塔照产墨（spec「停火」字面）。可被雷/镜/弩打（对空塔照常）。
        /// 首秀五L3 W2（波次草稿.csv）。
        /// </summary>
        private static EnemyStats Chapter5Nightmare => new EnemyStats
        {
            charId = "魇", hp = 150, speed = 1.3, inkDrop = 5, livesCost = 1,
            flying = true,
            sleepInterval = 6,   // 黄底 6s
            sleepSeconds = 2,    // 黄底 2s
            sleepRadius = 2.5,   // 黄底 2.5 格（Inspector 可调）
        };

        /// <summary>
        /// 傀：精英（3200 血 / 0.6 速 / 掉墨 25 / 扣 2 印）；**换影复活**——首次致死改为在
        /// **另一面同进度**以 50% 血复活一次（`reviveHpRatio` 0.5；`revived` 一旗一次性 → 二次死亡真死）。
        /// 首秀五L4 W2（波次草稿.csv）。
        /// </summary>
        private static EnemyStats Chapter5Puppet => new EnemyStats
        {
            charId = "傀", hp = 3200, speed = 0.6, inkDrop = 25, livesCost = 2,
            reviveHpRatio = 0.5,
        };

        /// <summary>
        /// 小鬼：**召唤物**（青脸 F5.2 每波召 6 只）——45 血弱怪，到终点 **−3 墨**（`inkLeak`，不扣印：
        /// `livesCost` 配 0）。击杀 **+3 墨**（inkDrop 3，走正常掉落）。不进波表（永远弱怪，不参与
        /// 精英/Boss 计数）；面随召唤者（F5.2 青脸在虚面 → 小鬼也在虚面）。
        /// </summary>
        private static EnemyStats Chapter5Imp => new EnemyStats
        {
            charId = "小鬼", hp = 45, speed = 1.2, inkDrop = 3, livesCost = 0,
            inkLeak = 3, // 到终点扣墨（不扣印）
        };

        /// <summary>
        /// F5.1（ch5 傩面 P1）：Boss —— 三脸轮换（赤实 / 青虚 / 金跟随玩家面），18s 一张脸。
        /// 到出口只扣 3 印 + 回起点重走（`endlessLap`，不判败）；Boss 纪律：免疫眩晕/燃烧。
        /// </summary>
        private static EnemyStats Chapter5NuoMask => new EnemyStats
        {
            charId = "傩面", hp = 10000, speed = 0.55, inkDrop = 25, livesCost = 3,
            isBoss = true, nuoMask = true, endlessLap = true,
            stunImmune = true, burnImmune = true,
        };

        /// <summary>
        /// F6（ch5 傩神 P2）：傩面碎后黑幕 3s → 自起点入场（实路、不换面）。三面随行 + 傩舞。
        /// 入场由 GameManager 触发（不进波表——GDD §7.7「Boss 关全程刷小怪、不占波次名额」）。
        /// </summary>
        private static EnemyStats Chapter5NuoShen => new EnemyStats
        {
            charId = "傩神", hp = 10000, speed = 0.5, inkDrop = 0, livesCost = 3,
            isBoss = true, nuoShen = true, endlessLap = true,
            crossFaceAttackable = true, // F6.1（用户 2026-10-22）：二阶段虚实两面都能打到 Boss（血量同步 12000 → 10000）
            stunImmune = true, burnImmune = true,
        };

        /// <summary>F4.1 自愈合并：已有敌表缺 ch5 敌就补（按 charId 查缺；只添缺不动旧值——双轨纪律）。
        /// 客串 狼（ch1 已有，F4.4 直接用）与 猫（ch3 已有）不入本表——按需在波表点名即可。</summary>
        private static void MergeChapter5Enemies(EnemyTableDef table)
        {
            bool changed = false;
            if (table.enemies == null) table.enemies = new List<EnemyStats>();
            var have = new HashSet<string>();
            foreach (var e in table.enemies) have.Add(e.charId);
            foreach (var add in new[] { Chapter5Paper, Chapter5Shadow, Chapter5Nightmare, Chapter5Puppet, Chapter5Imp, Chapter5NuoMask, Chapter5NuoShen })
            {
                if (have.Contains(add.charId)) continue;
                table.enemies.Add(add);
                changed = true;
                Debug.Log("[Setup] 敌表自愈补敌：" + add.charId);
            }
            if (changed)
            {
                EditorUtility.SetDirty(table); // SO 直改必须 SetDirty（坑清单）
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>F4.1 自愈合并：已有敌表缺 ch3 敌就补（按 charId 查缺；只添缺不动旧值——双轨纪律）。</summary>
        private static void MergeChapter3Enemies(EnemyTableDef table)
        {
            bool changed = false;
            if (table.enemies == null) table.enemies = new List<EnemyStats>();
            var have = new HashSet<string>();
            foreach (var e in table.enemies) have.Add(e.charId);
            foreach (var add in new[] { Chapter3Rat, Chapter3Moth, Chapter3Bat, Chapter3Cat, Chapter3Owl })
            {
                if (have.Contains(add.charId)) continue;
                table.enemies.Add(add);
                changed = true;
                Debug.Log("[Setup] 敌表自愈补敌：" + add.charId);
            }

            // F2.2 光-敌标记迁移（一次性）：蛾光脆 / 蝠潜行+光滞 / 猫潜行 / 狼夜行（ch1 狼 ch3
            // 客串挂标——ch1 fog 关恒光照 → 夜行零触发铁律）。**狼 nocturnal = 迁移标志位**：
            // 未标才整组打（用户从没机会动过——字段刚落）；已标 = 迁移过永不重跑——用户后期
            // 手关任何标（如想关猫潜行）不会被自愈复活（双轨纪律，灯哨兵同型思路）。
            var wolf = table.enemies.Find(x => x.charId == "狼");
            if (wolf != null && !wolf.nocturnal)
            {
                wolf.nocturnal = true;
                var moth = table.enemies.Find(x => x.charId == "蛾");
                if (moth != null) moth.lightVulnerable = true;
                var bat = table.enemies.Find(x => x.charId == "蝠");
                if (bat != null) { bat.stealth = true; bat.lightHindered = true; }
                var cat = table.enemies.Find(x => x.charId == "猫");
                if (cat != null) cat.stealth = true;
                changed = true;
                Debug.Log("[Setup] 敌表自愈：光-敌标记（蛾光脆 / 蝠潜行光滞 / 猫潜行 / 狼夜行）");
            }

            // F5.1 枭眩晕免疫自愈（独立哨兵，幂等）：stunImmune 字段后落 → 旧枭资产未带；false
            // 即补（原生缺省 false —— 用户无理由手关：设计定死 Boss 免疫硬控）。枭在 ch1/2 敌表
            // 不存在 → 只动三L 资产，零变化铁律不破。
            var owl = table.enemies.Find(x => x.charId == "枭");
            if (owl != null && !owl.stunImmune)
            {
                owl.stunImmune = true;
                changed = true;
                Debug.Log("[Setup] 敌表自愈：枭眩晕免疫（F5.1）");
            }

            if (changed)
            {
                EditorUtility.SetDirty(table); // SO 直改必须 SetDirty（坑清单）
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>全局玩法参数（v3.0）：已存在则复用（玩家调参不覆盖）。</summary>
        private static GameParams GetOrCreateGameParams()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameParams>(GameParamsPath);
            if (existing != null) return existing;

            if (File.Exists(GameParamsPath))
                DeleteBrokenAsset(GameParamsPath);

            var def = ScriptableObject.CreateInstance<GameParams>();
            // 字段初始值即 GDD §4 基线 v3.0（一口价=池总价值 / 手牌 5 / 波清 15 / 卖=价值×50%×字位 / 开垦 15s+25墨 / 上限 5 级）
            return SaveAndReload(def, GameParamsPath);
        }

        private static UIPanelConfig GetOrCreateUIPanelConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UIPanelConfig>(UIPanelConfigPath);
            if (existing != null) return existing;

            if (File.Exists(UIPanelConfigPath))
                DeleteBrokenAsset(UIPanelConfigPath);

            // 字段初始值 = 当前面板实测布局（D2 定稿）；生成后用户 Inspector 直调（ADR-024）
            var def = ScriptableObject.CreateInstance<UIPanelConfig>();
            return SaveAndReload(def, UIPanelConfigPath);
        }

        private static SoundConfig GetOrCreateSoundConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SoundConfig>(SoundConfigPath);
            if (existing != null)
            {
                PatchUiSfxSlots(existing);
                return existing;
            }

            if (File.Exists(SoundConfigPath))
                DeleteBrokenAsset(SoundConfigPath);

            // 音效槽位资产（ADR-028）：生成后用户把自备 AudioClip 拖进 Inspector；槽位空 = 静音
            var def = ScriptableObject.CreateInstance<SoundConfig>();
            var created = SaveAndReload(def, SoundConfigPath);
            PatchUiSfxSlots(created);
            return created;
        }

        /// <summary>
        /// 用户 2026-10-26（ADR-083）：策划端第一批 UI 音效自动入槽（只填空槽，手填过的不动）。
        /// 文件：Assets/_Project/Audio/SFX/{点击（大）, 菜单（大）, 错误点击, 错误点击（大）}.mp3
        /// 映射：click / menu / clickInvalid / defeat。
        /// </summary>
        private static void PatchUiSfxSlots(SoundConfig cfg)
        {
            if (cfg == null) return;
            bool dirty = false;
            dirty |= AssignSfxIfEmpty(ref cfg.click, "点击（大）");
            dirty |= AssignSfxIfEmpty(ref cfg.menu, "菜单（大）");
            dirty |= AssignSfxIfEmpty(ref cfg.clickInvalid, "错误点击");
            dirty |= AssignSfxIfEmpty(ref cfg.defeat, "错误点击（大）");
            if (!dirty) return;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] UI 音效槽位已自动补全（click/menu/clickInvalid/defeat）");
        }

        private static bool AssignSfxIfEmpty(ref AudioClip slot, string fileName)
        {
            if (slot != null) return false;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/" + fileName + ".mp3");
            if (clip == null) return false;
            slot = clip;
            return true;
        }

        // ============ 章图与关卡（design/numbers/关卡草稿.csv + 波次草稿.csv） ============

        private static LevelConfig GetOrCreateLevel(int id)
        {
            string path = LevelDir + "/Level" + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            if (existing != null)
            {
                // F4.4 自愈：ch2 关卡（id ≥ 6）章引用丢了就补——必须指 Chapter2（ADR-031 平铺跨章）；
                // F4.2（ch3 同律）：id ≥ 11 补 Chapter3；F1.1（ch4 同律）：id ≥ 16 补 Chapter4
                if (id >= 21 && existing.chapter == null)
                {
                    existing.chapter = GetOrCreateChapter5();
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Setup] 关卡章引用自愈：Level" + id + " → Chapter5");
                }
                else if (id >= 16 && existing.chapter == null)
                {
                    existing.chapter = GetOrCreateChapter4();
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Setup] 关卡章引用自愈：Level" + id + " → Chapter4");
                }
                else if (id >= 11 && existing.chapter == null)
                {
                    existing.chapter = GetOrCreateChapter3();
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Setup] 关卡章引用自愈：Level" + id + " → Chapter3");
                }
                else if (id >= 6 && existing.chapter == null)
                {
                    existing.chapter = GetOrCreateChapter2();
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Setup] 关卡章引用自愈：Level" + id + " → Chapter2");
                }
                // F2.1（喷射引擎落地，2026-10-12）：Level16 活跃度哨兵撤销——升级使命已在
                // F1.2/F1.3 验收期完成（用户资产 0/0|1/1 → 1/2）；禁喷的正路 = 章
                // volcanoEnabled 或关 eruptionMax=0（用户领地——自愈不再复活数值，铁律）。
                // F4.5 加卡「烬」+ 发放节奏重排（用户定案 2026-10-20 · D24）：
                // **新发放序 = 四L1 盆 → 四L2 火 → 四L3 炮 → 四L4 烬 → 四L5 无新卡**
                //（旧序 火/盆/炮 整体前移：盆挪到 L1、炮提到 L3、烬新落 L4；06 §12.6 / GDD §5.5）。
                // 池 = **只添缺**（16:盆 / 17:盆·火 / 18:盆·火·炮 / 19-20:盆·火·炮·烬）；
                // 奖励卡 = **旧序指纹迁移**（旧 L16=[]→[盆]、旧 L18=[盆]→[炮]、旧 L19=[炮]→[烬]）——
                // 只认「旧序原样」这一种指纹，迁移后指纹自然失效 → 幂等；指纹不符（用户手调过）
                // 一个字都不动（双轨纪律）。注：奖励卡靠「≤本关 rewardCards 并集」发（ADR-023），
                // 故 L18 换成炮后包内仍是 盆·火·炮（盆由 L16 补发）。
                if (id >= 16 && id <= 20)
                {
                    bool poolDirty = false;
                    if (existing.poolChars != null)
                    {
                        if (!existing.poolChars.Contains("盆")) { existing.poolChars.Add("盆"); poolDirty = true; }
                        if (id >= 17 && !existing.poolChars.Contains("火")) { existing.poolChars.Add("火"); poolDirty = true; }
                        if (id >= 18 && !existing.poolChars.Contains("炮")) { existing.poolChars.Add("炮"); poolDirty = true; }
                        if (id >= 19 && !existing.poolChars.Contains("烬")) { existing.poolChars.Add("烬"); poolDirty = true; }
                    }
                    if (existing.rewardCards != null)
                    {
                        if (id == 16 && existing.rewardCards.Count == 0)
                        {
                            existing.rewardCards.Add("盆"); // 旧 L1 无新卡 → 新 L1 = 盆
                            poolDirty = true;
                        }
                        if (id == 18 && existing.rewardCards.Count == 1 && existing.rewardCards[0] == "盆")
                        {
                            existing.rewardCards[0] = "炮"; // 旧 L3 = 盆 → 新 L3 = 炮（盆前移到 L1）
                            poolDirty = true;
                        }
                        if (id == 19 && existing.rewardCards.Count == 1 && existing.rewardCards[0] == "炮")
                        {
                            existing.rewardCards[0] = "烬"; // 旧 L4 = 炮 → 新 L4 = 烬（炮前移到 L3）
                            poolDirty = true;
                        }
                    }
                    if (poolDirty)
                    {
                        EditorUtility.SetDirty(existing);
                        AssetDatabase.SaveAssets();
                        Debug.Log("[Setup] 关卡塔池自愈：Level" + id + " 发放序迁移（池：" +
                            (existing.poolChars != null ? existing.poolChars.Count : 0) + " 选 5 / 奖励：" +
                            (existing.rewardCards != null && existing.rewardCards.Count > 0 ? existing.rewardCards[0] : "—") + "）");
                    }
                }
                // F7.1a 修障（用户报障 2026-10-18「出怪表里没有火山怪」）：**ch4 波表哨兵**。
                // 症因：Level16-20 由 F1.1/F1.3「客串骨架波」创建，而 F5.1/F6.1 的官方波表只写进了本
                // 文件的种子函数；本分支对既有资产只自愈章引用/池/奖励卡 —— 波表**无迁移通道**，
                // 于是种子改了、资产不动（ch4 一直出鼠/虫/狼，麟从未进 W7 波）。
                // 哨兵 = 波内**零火山怪**（烬鼠/火蜥/岩龟/岩燕/麟 全缺）→ 判为未迁移骨架 → 整体替换
                // 成官方表（临时实例跑种子取 waves = 与种子单源一致，不复制第二份表）。
                // 替换后波内必有火山怪 → 哨兵自然解除（一次性）；用户手调过的波表（含火山怪）
                // 永不进入本分支（双轨纪律：只补未迁移，不覆盖领地）。
                if (id >= 16 && id <= 20 && !HasVolcanoWaveEnemy(existing.waves))
                {
                    var probe = ScriptableObject.CreateInstance<LevelConfig>();
                    switch (id)
                    {
                        case 16: SeedChapter4Level1(probe); break;
                        case 17: SeedChapter4Level2(probe); break;
                        case 18: SeedChapter4Level3(probe); break;
                        case 19: SeedChapter4Level4(probe); break;
                        case 20: SeedChapter4Level5(probe); break;
                    }
                    var official = probe.waves;
                    Object.DestroyImmediate(probe); // 本类为 static 工具类，须显式限定（同 132/630 行惯例）
                    existing.waves = official;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Setup] ch4 波表哨兵：Level" + id + " 骨架波 → 官方表（" +
                        (official != null ? official.Count : 0) + " 波；火山怪已进表 —— 用户报障 2026-10-18）");
                }
                // F4.1（ch5）波表哨兵：ch4 修障同款（种子改了、既有资产不出新敌）。判据双向：
                // 资产波表里**一个 ch5 新敌都没有** 且 **官方种子该关已含新敌** → 判未迁移 → 整表替换。
                // 后半条让「后续单元还没轮到这一关」时保持沉默（不拿骨架表刷一遍日志）。
                if (id >= 21 && id <= 25 && !HasChapter5WaveEnemy(existing.waves))
                {
                    var probe5 = ScriptableObject.CreateInstance<LevelConfig>();
                    switch (id)
                    {
                        case 21: SeedChapter5Level1(probe5); break;
                        case 22: SeedChapter5Level2(probe5); break;
                        case 23: SeedChapter5Level3(probe5); break;
                        case 24: SeedChapter5Level4(probe5); break;
                        case 25: SeedChapter5Level5(probe5); break;
                    }
                    var official5 = probe5.waves;
                    bool ready = HasChapter5WaveEnemy(official5); // 官方表已换到含新敌（本单元或更早单元落的）
                    Object.DestroyImmediate(probe5);
                    if (ready)
                    {
                        existing.waves = official5;
                        EditorUtility.SetDirty(existing);
                        AssetDatabase.SaveAssets();
                        Debug.Log("[Setup] ch5 波表哨兵：Level" + id + " 骨架波 → 官方表（" +
                            (official5 != null ? official5.Count : 0) + " 波；ch5 新敌已进表）");
                    }
                }
                return existing;
            }

            if (File.Exists(path))
                DeleteBrokenAsset(path);

            var def = ScriptableObject.CreateInstance<LevelConfig>();
            def.levelId = id;
            switch (id)
            {
                case 0: SeedLevel0(def); break;
                case 1: SeedLevel1(def); break;
                case 2: SeedLevel2(def); break;
                case 3: SeedLevel3(def); break;
                case 4: SeedLevel4(def); break;
                case 5: SeedLevel5(def); break;
                case 6: SeedChapter2Level1(def); break;
                case 7: SeedChapter2Level2(def); break;
                case 8: SeedChapter2Level3(def); break;
                case 9: SeedChapter2Level4(def); break;
                case 10: SeedChapter2Level5(def); break;
                case 11: SeedChapter3Level1(def); break;
                case 12: SeedChapter3Level2(def); break;
                case 13: SeedChapter3Level3(def); break;
                case 14: SeedChapter3Level4(def); break;
                case 15: SeedChapter3Level5(def); break;
                case 16: SeedChapter4Level1(def); break;
                case 17: SeedChapter4Level2(def); break;
                case 18: SeedChapter4Level3(def); break;
                case 19: SeedChapter4Level4(def); break;
                case 20: SeedChapter4Level5(def); break;
                // F1.3（ch5，docs/13）：五L1-L5 骨架（F7.1 换 30 波官方全表）
                case 21: SeedChapter5Level1(def); break;
                case 22: SeedChapter5Level2(def); break;
                case 23: SeedChapter5Level3(def); break;
                case 24: SeedChapter5Level4(def); break;
                case 25: SeedChapter5Level5(def); break;
                default: SeedLevel1(def); break;
            }
            // F4.4（ADR-031）：ch2 关卡挂章引用（平铺跨章；ch1 关卡留空走场景引用兜底）；
            // F4.2（ch3 同律）：三L1-三L5 = Level11-15 → Chapter3；
            // F1.1（ch4 同律）：四L1 = Level16 → Chapter4
            if (id >= 21) def.chapter = GetOrCreateChapter5();
            else if (id >= 16) def.chapter = GetOrCreateChapter4();
            else if (id >= 11) def.chapter = GetOrCreateChapter3();
            else if (id >= 6) def.chapter = GetOrCreateChapter2();
            return SaveAndReload(def, path);
        }

        /// <summary>
        /// F7.1a ch4 波表哨兵谓词：波内是否出现任一火山怪（含 Boss 麟）。
        /// ch4 关卡波表必含火山怪 —— 全缺即判「未迁移的 F1.1/F1.3 骨架波」。
        /// 数据源：GDD §6 / design/numbers/草稿/敌人草稿.csv 四L 段。
        /// </summary>
        private static readonly string[] VolcanoEnemyIds = { "烬鼠", "火蜥", "岩龟", "岩燕", "麟" };

        private static bool HasVolcanoWaveEnemy(List<WaveLayout> waves)
        {
            if (waves == null) return false;
            for (int i = 0; i < waves.Count; i++)
            {
                var w = waves[i];
                if (w == null || w.groups == null) continue;
                for (int j = 0; j < w.groups.Count; j++)
                {
                    var g = w.groups[j];
                    if (g == null) continue;
                    for (int k = 0; k < VolcanoEnemyIds.Length; k++)
                        if (g.charId == VolcanoEnemyIds[k]) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// F4.1（ch5）波表哨兵：ch5 五关由 F1.3「骨架波」创建（虫/狼/熊/鼠），而官方波表只写在
        /// 种子函数里 —— 既有资产无迁移通道（ch4 同一个坑，见 F7.1a 修障）。本哨兵 = 波内
        /// **零 ch5 新敌**（纸/影/魇/傀 全缺）且**官方种子该关确有新敌** → 整体替换。
        /// 替换后哨兵自然解除（一次性）；用户手调过的波表（含新敌）永不进入本分支（双轨纪律）。
        /// 逐单元增长：F4.1 纸/影 · F4.2 魇 · F4.3 傀 · F4.4 小鬼（召唤物不进波表）。
        /// </summary>
        private static readonly string[] Chapter5WaveEnemies = { "纸", "影", "魇", "傀" };

        private static bool HasChapter5WaveEnemy(List<WaveLayout> waves)
        {
            if (waves == null) return false;
            for (int i = 0; i < waves.Count; i++)
            {
                var w = waves[i];
                if (w == null || w.groups == null) continue;
                for (int j = 0; j < w.groups.Count; j++)
                {
                    var g = w.groups[j];
                    if (g == null) continue;
                    for (int k = 0; k < Chapter5WaveEnemies.Length; k++)
                        if (g.charId == Chapter5WaveEnemies[k]) return true;
                }
            }
            return false;
        }

        private static ChapterConfig GetOrCreateChapter()
        {
            string path = ChapterDir + "/Chapter1.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ChapterConfig>(path);
            if (existing != null) return existing;

            if (File.Exists(path))
                DeleteBrokenAsset(path);

            var def = ScriptableObject.CreateInstance<ChapterConfig>();
            SeedChapter1(def);
            return SaveAndReload(def, path);
        }

        /// <summary>
        /// 第一章章图（ADR-021 定稿）：12 空地 + 6 可垦，章内 L1-L5 共用。
        /// 蛇形整数路（行 y=3/0/-3、竖段 x=±5）避让路面；左手起点密（敌出
        /// 场即入射程）、末段出口收口（8,-2 等）；垦地是好位投资而非废位。
        /// 覆盖谱：除 (7,1) 弓矛专属（近战无覆盖）外，其余格近战均达路面。
        /// </summary>
        private static void SeedChapter1(ChapterConfig d)
        {
            d.chapterId = 1;
            d.title = "第一章 · 草原狩猎";

            // 陆路（F1.2 章数据化）= 蛇形三折（与类缺省值一致，显式写以自证；水路空 = 单线章）
            d.landRoute = new List<Vector2>
            {
                new Vector2(-9f, 3f), new Vector2(5f, 3f),
                new Vector2(5f, 0f), new Vector2(-5f, 0f),
                new Vector2(-5f, -3f), new Vector2(9f, -3f),
            };
            d.waterRoute = new List<Vector2>();

            d.cells = new List<CellLayout>();
            // 12 空地
            foreach (var p in new[] { (-8, 2), (-6, 2), (-3, 2), (1, 2), (3, 2),
                                      (-2, 1), (1, 1),
                                      (-2, -1), (2, -1),
                                      (0, -2), (6, -2), (8, -2) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty });
            // 6 可垦（-4,1 中路交叉 / 7,1 远程专属 / -6,-2 末段拐角 …）
            foreach (var p in new[] { (-2, 2), (-4, 1), (7, 1), (-6, -2), (1, -2), (4, -2) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Fallow });
        }

        /// <summary>第二章·水泽章图（F1.3）：存在即返回，不存在则按种子创建。</summary>
        private static ChapterConfig GetOrCreateChapter2()
        {
            string path = ChapterDir + "/Chapter2.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ChapterConfig>(path);
            if (existing != null) return existing;

            if (File.Exists(path))
                DeleteBrokenAsset(path);

            var def = ScriptableObject.CreateInstance<ChapterConfig>();
            SeedChapter2(def);
            return SaveAndReload(def, path);
        }

        /// <summary>
        /// 第二章·水泽章图（06 §10.1 定案，格数黄底）：
        /// 陆空 10 + 垦 4 + 永久水格 8 + 潮间带 8（低带 4 平潮淹 / 高带 4 涨潮淹）+ 滩涂 2（退潮露）。
        /// 双线：陆路走上沿 y=3、水路走下沿 y=-3，x=4 中段汇合共奔 y=-1 印章（共享尾段，ADR-032）。
        /// 海拔左低右高（潮间带/滩涂/左低地全在左半场）；右上 = 安全终局线（撤离方向 = 增援方向）。
        /// </summary>
        private static void SeedChapter2(ChapterConfig d)
        {
            d.chapterId = 2;
            d.title = "第二章 · 水泽";

            // 陆路（上路）：(-9,3)→(4,3)→(4,-1)→(9,-1)，全长 22
            d.landRoute = new List<Vector2>
            {
                new Vector2(-9f, 3f), new Vector2(4f, 3f),
                new Vector2(4f, -1f), new Vector2(9f, -1f),
            };
            // 水路（下路）：(-9,-3)→(4,-3)→(4,-1)→(9,-1)，全长 20；与陆路共享尾段 (4,-1)→(9,-1) = 汇合
            d.waterRoute = new List<Vector2>
            {
                new Vector2(-9f, -3f), new Vector2(4f, -3f),
                new Vector2(4f, -1f), new Vector2(9f, -1f),
            };

            d.cells = new List<CellLayout>();
            // 陆空地 10（高地 4 = 永不淹：右上安全终局线 + 顶行弓位）
            foreach (var p in new[] { (-5, 2), (-3, 2), (-1, 2), (1, 2), (3, 2),
                                      (8, 2), (5, 1), (7, 1), (5, 0), (7, 0) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty });
            // 垦 4：左 3 风险投资（elev 3 = 仅大涨淹，L4-L5 高潮位波威胁）+ 右 1 安全
            d.cells.Add(new CellLayout { x = -7, y = 2, state = CellState.Fallow, elevation = 3 });
            d.cells.Add(new CellLayout { x = 6, y = 2, state = CellState.Fallow });
            d.cells.Add(new CellLayout { x = -3, y = -1, state = CellState.Fallow, elevation = 3 });
            d.cells.Add(new CellLayout { x = -1, y = -1, state = CellState.Fallow, elevation = 3 });
            // 潮间带低带 4（elev 1 = 平潮即淹、退潮露）——贴水侧
            foreach (var p in new[] { (-8, 0), (-6, 0), (-5, 0), (-3, 0) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty, elevation = 1 });
            // 潮间带高带 4（elev 2 = 涨潮淹）
            foreach (var p in new[] { (-8, 1), (-6, 1), (-4, 1), (-2, 1) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty, elevation = 2 });
            // 滩涂 2（elev 1，退潮才露的临时可建格；贴水路左端出生区）
            foreach (var p in new[] { (-9, -2), (-9, -1) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty, elevation = 1 });
            // 永久水格 8（沿水路 y=-3 右沿一排：水系三卡专属地皮，F3 起可建；水路敌 ×1.5 航道）
            foreach (var p in new[] { (-8, -2), (-6, -2), (-4, -2), (-2, -2),
                                      (0, -2), (1, -2), (2, -2), (3, -2) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Water });
        }

        /// <summary>第三章·幽夜章图（F1.3/F4.2）：存在即返回（旧种子走 v2 改版自愈），不存在则按种子创建。</summary>
        private static ChapterConfig GetOrCreateChapter3()
        {
            string path = ChapterDir + "/Chapter3.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ChapterConfig>(path);
            if (existing != null)
            {
                PatchChapter3Geometry(existing); // v2 改版自愈（哨兵：旧种子未手调才升级）
                return existing;
            }

            if (File.Exists(path))
                DeleteBrokenAsset(path);

            var def = ScriptableObject.CreateInstance<ChapterConfig>();
            SeedChapter3(def);
            return SaveAndReload(def, path);
        }

        /// <summary>
        /// 路线改版自愈（2026-10-08 用户验收反馈）：旧版两口都挤 x=-5 左中 → v2 双口拉到
        /// 地图边缘（下口 (9,-4) 右下角、上口 (-9,4) 左上角·对角贴边）+ 下半回折左右互换
        /// （旧右竖 → 新左竖，上半右竖——左右交错）。哨兵 = landRoute 与旧种子 8 点精确匹配
        /// （= 从未被 Inspector 手调）才整组替换路线+格子；动过任意一点 = 用户领地不碰（双轨纪律，
        /// 蛟 phase2HpRatio 哨兵升级同型）。列数/开关字段（fogEnabled 等）不动。
        /// </summary>
        private static void PatchChapter3Geometry(ChapterConfig d)
        {
            var oldRoute = new List<Vector2>
            {
                new Vector2(-5f, -4f), new Vector2(5f, -4f),
                new Vector2(5f, -2f), new Vector2(0f, -2f),
                new Vector2(0f, 2f), new Vector2(5f, 2f),
                new Vector2(5f, 4f), new Vector2(-5f, 4f),
            };
            if (d.landRoute == null || d.landRoute.Count != oldRoute.Count) return;
            for (int i = 0; i < oldRoute.Count; i++)
                if (d.landRoute[i] != oldRoute[i]) return; // 手调过 → 用户领地，不升级

            SeedChapter3Geometry(d); // 旧种子确认未调 → 整组换 v2（路线 + 格子）
            EditorUtility.SetDirty(d); // SO 直改必须 SetDirty（坑清单）
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Chapter3 路线改版自愈 v2：双口拉到地图边缘（下口右下角/上口左上角）+ 下半左右互换");
        }

        /// <summary>
        /// 第三章·幽夜章图（06 §11.1 定案，格数黄底；几何 v2 2026-10-08 用户验收反馈改版）：
        /// 同路双向、家钉正中、**对角双口贴边**。竖向折返路 8 折点全长 46：
        /// 下口 (9,-4)（右下角·边缘）→ 下横左行 → 左下竖上行 → 中下横收中 → 中竖过家 (0,0)
        /// → 中上横 → 右上竖 → 上口 (-9,4)（左上角·边缘）。中心对称（W[i] = -W[N-1-i]）——
        /// 弧长中点恒 = (0,0) 家；上下半各 23 格（敌半程）；下半回折在左竖、上半在右竖（左右交错）。
        /// 陆空 12 + 垦 4，海拔全 4（ch3 无潮汐——迷雾是状态不是格态，06 §11.1）；无水路。
        /// fogEnabled=true（渲染层 F1.1 落地时消费）；bidirectional=true（半程路由 ADR-040）。
        /// </summary>
        private static void SeedChapter3(ChapterConfig d)
        {
            d.chapterId = 3;
            d.title = "第三章 · 幽夜";
            d.fogEnabled = true;
            d.bidirectional = true;

            SeedChapter3Geometry(d);
        }

        /// <summary>
        /// 章图几何（v2：双口拉边 + 下半左右互换——旧版两口都挤 x=-5 左中，用户 2026-10-08 验收反馈）。
        /// 全长 46（14+2+5+4+5+2+14），弧长中点 23 = (0,0) 家；路与格子集中灌此，路线改版自愈共用。
        /// </summary>
        private static void SeedChapter3Geometry(ChapterConfig d)
        {
            // v2 路线：下口右下角 → 左竖回折 → 中竖过家 → 右竖 → 上口左上角（对角贴边 + 左右交错）
            d.landRoute = new List<Vector2>
            {
                new Vector2(9f, -4f), new Vector2(-5f, -4f),
                new Vector2(-5f, -2f), new Vector2(0f, -2f),
                new Vector2(0f, 2f), new Vector2(5f, 2f),
                new Vector2(5f, 4f), new Vector2(-9f, 4f),
            };
            d.waterRoute = new List<Vector2>(); // 单线章（ch3 无水路——夜行陆空敌）

            d.cells = new List<CellLayout>();
            // 陆空 12（海拔缺省 4 = 永不淹；ch3 无潮汐）
            // —— 中竖必经段旁 4（火力场核心：两股流汇合走中竖到家——全图覆盖性价比最高）——
            foreach (var p in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty });
            // —— 下半路旁 4（左下竖 + 中下横 + 下横条右段）——
            foreach (var p in new[] { (-6, -3), (-3, -1), (0, -3), (4, -3) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty });
            // —— 上半路旁 4（中上横 + 右上竖 + 上横条左段）——
            foreach (var p in new[] { (3, 3), (6, 3), (0, 3), (-4, 3) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty });
            // 垦 4（翼端预警灯位 = 出怪口两翼：下口双翼 + 上口双翼——06 §11.1「翼端灯=揭潜行雷达」）
            foreach (var p in new[] { (8, -3), (8, -2), (-8, 3), (-8, 2) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Fallow });
        }

        /// <summary>第四章·火山章图：不存在则按种子创建（新章无旧版迁移——首版即 v1，无 Patch 哨兵）。</summary>
        private static ChapterConfig GetOrCreateChapter4()
        {
            string path = ChapterDir + "/Chapter4.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ChapterConfig>(path);
            if (existing != null) return existing;

            if (File.Exists(path))
                DeleteBrokenAsset(path);

            var def = ScriptableObject.CreateInstance<ChapterConfig>();
            SeedChapter4(def);
            return SaveAndReload(def, path);
        }

        /// <summary>
        /// 第四章·火山章图（06 §12.1 定案，格数黄底）：**回环跑道**绕火山口一周 → 出环奔家，
        /// 家钉环外出口旁。单向全程 9 折点全长 49（入口北走廊 14+1 / 环四段 6×4 / 出口 4+6）——
        /// 敌绕环一圈 = 塔获得**二次接触机会**（随机灾害世界的结构性补偿，06 字面）。
        /// 路线：左上 (−9,4) 入场 → 北走廊东行 → (5,3) 入环【逆时针：北沿西 → 西沿南 →
        /// 南沿东 → 东沿北】→ (5,3) 绕完一圈 → 东走廊 (9,3) → 东外沿南下 → 家 (9,−3)。
        /// 折点 (5,3) 自交 = 回环拓扑必然（CollectRoadTiles 铺砖 HashSet 去重；ValidateChapter
        /// 无自交校验——敌两次经过同格是设计本意，弧长参数化天然支持）。
        /// 火山口 (2,0) 居中偏一侧（volcanoCenter 装饰锚——Build 渲染锥盘，F2 喷射/F5 践踏复用环心）。
        /// 陆空 12（环内 8 贴火山口 = 灰烬视觉高发/火系主场 + 出口终局 4）+ 垦 4（入口两翼+家旁两翼）。
        /// volcanoEnabled=true；海拔全 4（ch4 无潮汐——岩浆是事件不是格态，06 §12.1）；无水路。
        /// </summary>
        private static void SeedChapter4(ChapterConfig d)
        {
            d.chapterId = 4;
            d.title = "第四章 · 火山";
            d.volcanoEnabled = true;
            d.volcanoCenter = new Vector2(2f, 0f);

            d.landRoute = new List<Vector2>
            {
                new Vector2(-9f, 4f), new Vector2(5f, 4f),   // 入口北走廊（y=4 顶行东行 14）
                new Vector2(5f, 3f), new Vector2(-1f, 3f),   // 入环 + 北沿西行（第 1 边）
                new Vector2(-1f, -3f), new Vector2(5f, -3f), // 西沿南下 + 南沿东行（第 2/3 边）
                new Vector2(5f, 3f), new Vector2(9f, 3f),    // 东沿北上回环 + 东走廊出环（第 4 边）
                new Vector2(9f, -3f),                         // 东外沿南下 → 家（终局防线 = 出口段）
            };
            d.waterRoute = new List<Vector2>(); // 单线章（ch4 无水路——夜行/水栖敌不入火山）

            d.cells = new List<CellLayout>();
            // 陆空 12 —— 环内 8（贴火山口一圈：灰烬视觉高发区/火系主场——06 §12.1 战术分区字面落地；
            // (2,0) 留给火山口装饰非格位）
            foreach (var p in new[] { (0, 2), (2, 2), (4, 2), (0, -2), (2, -2), (4, -2), (1, 0), (3, 0) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty });
            // 陆空 —— 出口终局 4（东走廊 (y=3) 与东外沿 (x=9) 夹角带 + 北走廊东端外 (6,4)）
            foreach (var p in new[] { (8, 2), (8, 0), (8, -2), (6, 4) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty });
            // 垦 4（入口两翼 = 出生口南侧 + 家旁两翼 = 终点南侧——远端开垦投资位）
            foreach (var p in new[] { (-9, 3), (-8, 3), (9, -4), (8, -4) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Fallow });
        }

        /// <summary>第五章·诡异章图：不存在则按种子创建（新章首版即 v1，无旧版迁移）。</summary>
        private static ChapterConfig GetOrCreateChapter5()
        {
            string path = ChapterDir + "/Chapter5.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ChapterConfig>(path);
            if (existing != null) return existing;

            if (File.Exists(path))
                DeleteBrokenAsset(path);

            var def = ScriptableObject.CreateInstance<ChapterConfig>();
            SeedChapter5(def);
            return SaveAndReload(def, path);
        }

        /// <summary>
        /// 第五章·诡异章图（06 §14.1 定案；docs/13 附录 A）：**双面同图**——
        /// 一张格网、两套等长路线（实路 landRoute / 虚路 voidRoute，各 41 格）。
        /// **拓扑（用户 2026-10-22 改图）**：实/虚各一个出生口（实 (-9,3) / 虚 (-9,-3)，不再共口）
        /// → 左中段汇入公共段 (-4,0)→(-1,0) → 分岔（实路上环 y=3 / 虚路下环 y=-3）→ 右沿 x=8 竖段
        /// → 折回 → 汇合 (4,0)→(9,0) 奔家 (9,0)。两路互为 y=0 镜像（换面 = 干净的镜像跳，ADR-069）。
        /// 两路等长 41 是傩面换面「同路程进度落点」的硬前提（ValidateChapter 校验）；**出生口不同
        /// 但终点（家）必同**。格 = 14 块（6 空 + 6 垦 + 中段新增 2 空）；红铲买格两面共享（F2.4）。
        /// </summary>
        private static void SeedChapter5(ChapterConfig d)
        {
            d.chapterId = 5;
            d.title = "第五章 · 诡异";
            d.faceEnabled = true;

            d.landRoute = new List<Vector2>
            {
                new Vector2(-9f, 3f), new Vector2(-4f, 3f),  // 实路出生口北上 + 东行（5）
                new Vector2(-4f, 0f), new Vector2(-1f, 0f),  // 南下入公共段 + 东行（3+3）
                new Vector2(-1f, 3f), new Vector2(8f, 3f),   // 北上 + 上环东行（3+9）
                new Vector2(8f, -3f), new Vector2(4f, -3f),  // 东沿南下 + 西行回折（6+4）
                new Vector2(4f, 0f), new Vector2(9f, 0f),    // 北上汇合 + 奔家（3+5）
            };
            d.voidRoute = new List<Vector2>
            {
                new Vector2(-9f, -3f), new Vector2(-4f, -3f), // 虚路出生口南下 + 东行（5）
                new Vector2(-4f, 0f), new Vector2(-1f, 0f),  // 北上入公共段 + 东行（3+3）
                new Vector2(-1f, -3f), new Vector2(8f, -3f), // 南下 + 下环东行（3+9）
                new Vector2(8f, 3f), new Vector2(4f, 3f),    // 东沿北上 + 西行回折（6+4）
                new Vector2(4f, 0f), new Vector2(9f, 0f),    // 南下汇合 + 奔家（3+5）
            };
            d.waterRoute = new List<Vector2>(); // 无水路（lane 恒 0；face 与 lane 正交）

            d.cells = new List<CellLayout>();
            // 空 8（两面同坐标）：左公共段控制 2 + 岔口 2 + 右汇合段控制 2 + 中段新增 2
            foreach (var p in new[] { (-6, 1), (-6, -1), (1, 1), (1, -1), (7, 1), (7, -1), (-8, 0), (-7, 0) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Empty });
            // 垦 6：上/下环中段两翼 + 右沿两翼（远端开垦投资位；红铲买格两面共享）
            foreach (var p in new[] { (-3, 2), (-3, -2), (3, 2), (3, -2), (5, 2), (5, -2) })
                d.cells.Add(new CellLayout { x = p.Item1, y = p.Item2, state = CellState.Fallow });
        }

        /// <summary>
        /// 四L1 骨架（F1.1 数据先行——Level17-20 与 30 波全表归 F4.1）：
        /// 火山首战 = 草原客串 5 波（06 §12.4 客串原值混入、章间难度重置口径）；
        /// **F4.5 新发放序（用户 2026-10-20）：L1 +盆（先教「救」）→ L2 +火 → L3 +炮 → L4 +烬**；
        /// 池 14 选 5（ch3 全池 13 + 盆）；homeLampRadius=0（ch4 无雾）；tide 恒 Hold（无潮汐）。
        /// </summary>
        private static void SeedChapter4Level1(LevelConfig d)
        {
            d.title = "火山 · 初燃";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.eruptionMin = 1; d.eruptionMax = 2; // F1.2/F1.3 活跃度：四L1 = 每波 1-2 颗（GDD §7.6 与 06 §12.6 双源一致「1-2」；任务单 38 行「1」系转写孤本分歧——程序侧按双源落，F6 手测终裁；旧资产升级已毕，哨兵 F2.1 撤销）
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯", "法", "盆" }; // F4.5 新序：+盆 14 选 5
            d.rewardCards = new List<string> { "盆" }; // F4.5 发放序（2026-10-20）：L1 盆 → L2 火 → L3 炮 → L4 烬

            // F5.1：官方波表替换骨架（波次草稿.csv 四L1 段逐行转录——组间隔 = 「出怪间隔秒」列、
            // 波间隔 = 「与上波起始间隔秒」列）。逐波「浆=N」草稿值均落在本关活跃度区间 1-2 内——
            // 数量/落点随机归 F2.1 用户定案，故不逐波钉死（区间即曲线）。口 = 单口（回环章图无口列）。
            d.waves = new List<WaveLayout>
            {
                WaveT(20, TideLevel.Hold, Group("烬鼠", 8, 1.2)),                                          // W1 烬鼠首秀 + 掉墨反馈（塔初成）
                WaveT(22, TideLevel.Hold, Group("烬鼠", 10, 1.0), Group("虫", 6, 1.0)),                    // W2 草原客混入：AoE 靶预热（棍/戟/矛范围体验）
                WaveT(22, TideLevel.Hold, Group("狼", 6, 1.0), Group("虫", 8, 1.0)),                       // W3 快怪流：旧卡节奏回归
                WaveT(25, TideLevel.Hold, Group("熊", 3, 1.2), Group("烬鼠", 8, 1.2)),                     // W4 厚血+海量混编：撤离窗口体感（红闪内决策）
                WaveT(25, TideLevel.Hold, Group("虎", 1, 0.9), Group("狼", 8, 0.9), Group("烬鼠", 6, 0.9)),// W5 L1 收官：精英+海量（8 塔均 L1.5 综合检验）
            };
        }

        // ============ 四L1-L5 波表 = 官方表（波次草稿.csv 四L 段逐行转录；骨架客串已撤）：
        // 四L1-L4 随 F5.1 落、四L5（含麟×1 Boss 波）随 F6.1 落。池序沿各卡单元随池进
        //（F4.2 14/F4.3 15/F4.4 16 = GDD §7.6 终态）；rewardCards 随各卡单元同步写
        //（幽灵卡禁令，备案 docs/11 §7）。数值全黄底，F7 手测定稿。 ============

        /// <summary>四L2 初沸：活跃度 2-3；火蜥首秀（免疫示范）+ 鳄客混入（燃烧 ×2 挂标归 F5.2）；6 波。</summary>
        private static void SeedChapter4Level2(LevelConfig d)
        {
            d.title = "火山 · 初沸";
            d.startLives = 5; d.prepSeconds = 20;
            d.eruptionMin = 2; d.eruptionMax = 3; // GDD §7.6「2-3」
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯", "法", "盆", "火" }; // F4.5 新序：+火 15 选 5（池 ∩ 拥有 = 抽卡域）
            d.rewardCards = new List<string> { "火" }; // F4.2：通关四L1 进包即拥有（ADR-023 并集口径）
            // F5.1：官方波表替换骨架（波次草稿.csv 四L2 段；浆草稿值 ∈ 2-3 区间）。
            d.waves = new List<WaveLayout>
            {
                WaveT(22, TideLevel.Hold, Group("火蜥", 3, 1.1), Group("烬鼠", 10, 1.1)),                   // W1 火蜥首秀（免疫示范：岩浆烫不到它）
                WaveT(25, TideLevel.Hold, Group("鳄", 1, 1.2), Group("虫", 8, 1.2), Group("烬鼠", 6, 1.2)), // W2 鳄客首现（火烧水怪——燃烧 ×2 挂标归 F5.2）
                WaveT(25, TideLevel.Hold, Group("熊", 4, 1.2), Group("火蜥", 3, 1.2)),                     // W3 厚血+免疫混编：火塔灰烬格增益初验
                WaveT(25, TideLevel.Hold, Group("牛", 2, 1.0), Group("火蜥", 4, 1.0), Group("烬鼠", 10, 1.0)), // W4 加速+灾害区原生（10 塔·火 1-2）
                WaveT(25, TideLevel.Hold, Group("鳄", 2, 1.0), Group("熊", 3, 1.0), Group("虫", 10, 1.0)), // W5 水怪怕火收割点（燃烧 ×2 体感）
                WaveT(25, TideLevel.Hold, Group("牛", 3, 0.9), Group("火蜥", 5, 0.9), Group("烬鼠", 12, 0.9)), // W6 L2 收官：免疫大编队（火位哲学检验）
            };
        }

        /// <summary>四L3 熔迹：活跃度 2-4；岩龟首秀（硬壳拒燃·AoE 对照）+ 对空线预热；6 波。</summary>
        private static void SeedChapter4Level3(LevelConfig d)
        {
            d.title = "火山 · 熔迹";
            d.startLives = 5; d.prepSeconds = 20;
            d.eruptionMin = 2; d.eruptionMax = 4; // GDD §7.6「2-4」
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯", "法", "盆", "火", "炮" }; // F4.5 新序：+炮 16 选 5
            d.rewardCards = new List<string> { "炮" }; // F4.5 发放序：L3 = 炮（盆已前移到 L1）
            d.waves = new List<WaveLayout>
            {
                WaveT(22, TideLevel.Hold, Group("岩龟", 1, 1.2), Group("虫", 10, 1.2)),                   // W1 岩龟首秀：硬壳拒燃（AoE 对照）
                WaveT(25, TideLevel.Hold, Group("火蜥", 4, 1.0), Group("烬鼠", 12, 1.0)),                  // W2 红印加速流（灾害区原生大编队）
                WaveT(25, TideLevel.Hold, Group("岩龟", 2, 1.0), Group("熊", 3, 1.0), Group("狼", 8, 1.0)),// W3 拒燃+快脆混编：目标选择考校
                WaveT(25, TideLevel.Hold, Group("鸟", 8, 1.0), Group("岩龟", 2, 1.0)),                     // W4 空袭+地龟双线：对空火力分配
                WaveT(25, TideLevel.Hold, Group("鳄", 2, 0.9), Group("岩龟", 2, 0.9), Group("烬鼠", 10, 0.9)), // W5 水怪+硬壳（12 塔·火 2 盆 1-2）
                WaveT(25, TideLevel.Hold, Group("牛", 4, 0.9), Group("岩龟", 2, 0.9), Group("火蜥", 4, 0.9)), // W6 尖峰预热：三系混编（盆复活节奏首验）
            };
        }

        /// <summary>四L4 岩潮：活跃度 3-5（章内最难）；岩燕首秀（空靶——炮 antiAir 主场）+ 三态靶混编；6 波。</summary>
        private static void SeedChapter4Level4(LevelConfig d)
        {
            d.title = "火山 · 岩潮";
            d.startLives = 5; d.prepSeconds = 20;
            d.eruptionMin = 3; d.eruptionMax = 5; // GDD §7.6「3-5」
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯", "法", "盆", "火", "炮", "烬" }; // F4.5：+烬 17 选 5（GDD §7.6 四L4 终态）
            d.rewardCards = new List<string> { "烬" }; // F4.5 发放序：L4 = 烬（炮已前移到 L3）
            d.waves = new List<WaveLayout>
            {
                WaveT(22, TideLevel.Hold, Group("岩燕", 5, 1.1), Group("鸟", 6, 1.1)),                     // W1 岩燕首秀（空靶——考炮对空）
                WaveT(25, TideLevel.Hold, Group("岩龟", 2, 0.9), Group("岩燕", 4, 0.9), Group("烬鼠", 12, 0.9)), // W2 AoE 三态靶：海/厚/空全覆盖
                WaveT(25, TideLevel.Hold, Group("鳄", 3, 1.0), Group("火蜥", 5, 1.0)),                     // W3 燃烧 ×2 与免疫对撞（火塔输出巅峰）
                WaveT(25, TideLevel.Hold, Group("野人", 2, 1.0), Group("岩龟", 2, 1.0), Group("岩燕", 5, 1.0)), // W4 精英+硬壳+空：三线火力分配
                WaveT(25, TideLevel.Hold, Group("牛", 4, 0.9), Group("岩燕", 6, 0.9), Group("烬鼠", 15, 0.9)), // W5 海量空地双洪峰（15 塔·炮 1-2 火 2）
                WaveT(28, TideLevel.Hold, Group("野人", 2, 1.0), Group("岩龟", 3, 1.0), Group("火蜥", 5, 1.0)), // W6 L4 尖峰：拒燃全家桶（章内最难）
            };
        }

        /// <summary>四L5 麟至：活跃度 4-6；7 波（F6.1 换官方表）；W7 = 麟×1 Boss 波——
        /// 践踏犁地（F6.1）/ 共振岩浆雨与二阶段（F6.2/F6.3）。</summary>
        private static void SeedChapter4Level5(LevelConfig d)
        {
            d.title = "火山 · 麟至";
            d.startLives = 5; d.prepSeconds = 20;
            d.eruptionMin = 4; d.eruptionMax = 6; // GDD §7.6「4-6」
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯", "法", "盆", "火", "炮", "烬" }; // F4.5：17 选 5（GDD §7.6 四L5 终态）
            d.rewardCards = new List<string>(); // L5 预告位（GDD「预告麟至则地燃」——06 §12.5 三件套归 F6）
            // F6.1：官方波表替换骨架（波次草稿.csv 四L5 段逐行转录；浆草稿值 ∈ 4-6 区间，
            // 转阶段共振雨另加 6-8 格归 F6.2）。W7 = 麟×1 Boss 波。
            d.waves = new List<WaveLayout>
            {
                WaveT(22, TideLevel.Hold, Group("烬鼠", 15, 0.9), Group("狼", 8, 0.9)),                    // W1 海量热身（14-15 塔绞肉场）
                WaveT(25, TideLevel.Hold, Group("火蜥", 6, 1.0), Group("岩龟", 2, 1.0)),                  // W2 免疫+硬壳（灰烬增益主输出检验）
                WaveT(25, TideLevel.Hold, Group("岩燕", 8, 1.0), Group("鸟", 6, 1.0)),                    // W3 空袭洪峰（对空火力峰值）
                WaveT(25, TideLevel.Hold, Group("野人", 2, 1.0), Group("岩龟", 2, 1.0), Group("岩燕", 5, 1.0)), // W4 三精英同场（毁建拉锯预热）
                WaveT(25, TideLevel.Hold, Group("鳄", 3, 0.9), Group("火蜥", 5, 0.9), Group("烬鼠", 15, 0.9)),  // W5 大战前夜：燃烧 ×2 收割场（火系主场验收）
                WaveT(28, TideLevel.Hold, Group("野人", 2, 0.9), Group("熊", 4, 0.9), Group("岩龟", 2, 0.9)),  // W6 清杂+留种塔（麟将至——波次栏 Boss 行 F6.1 生效）
                WaveT(30, TideLevel.Hold, Group("麟", 1, 1.5)),                                           // W7 麟至：践踏犁地 + 共振岩浆雨（毕业考：毁建拉锯）
            };
        }

        // ============ 五L1-L5 骨架（F1.3，docs/13）：客串旧敌先跑通双面章图；
        // F4 落新敌（纸/影/魇/傀/小鬼）→ F7.1 换 30 波官方全表（波次草稿.csv 五L 段） ============

        /// <summary>ch5 基础 17 张（五L1 池；顺序 = 设计表，也是构筑面板显示序）。</summary>
        private static readonly string[] Chapter5BasePool =
        {
            "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯", "法", "盆", "火", "炮", "烬",
        };

        /// <summary>
        /// ch5 四张新卡的**发放序**：第 1 张发给五L1、第 2 张给五L2、第 3 张给五L3、第 4 张给五L4，
        /// 五L5 不发新卡（池仍满 21）。**黄底：用户 2026-10-22 调过序，改这一行即可**
        /// ——重建 / 自愈都从这里取，别再去五个种子函数里逐行改（会漏）。
        /// 校验器只认结构（池累积 + 四张发全），不认顺序，所以随便调都不会报错。
        /// </summary>
        private static readonly string[] Chapter5RewardOrder = { "镜", "雷", "幡", "煞" };

        /// <summary>第 levelNo 关（1..5）的池 = 基础 17 + 前 levelNo 张新卡（五L5 自然封顶 = 全池 21）。</summary>
        private static List<string> Chapter5PoolAt(int levelNo)
        {
            var list = new List<string>(Chapter5BasePool);
            for (int k = 0; k < levelNo && k < Chapter5RewardOrder.Length; k++) list.Add(Chapter5RewardOrder[k]);
            return list;
        }

        /// <summary>第 levelNo 关（1..5）通关新发的卡 = 发序第 levelNo 张；五L5（超出发序）= 不发。</summary>
        private static List<string> Chapter5RewardAt(int levelNo)
        {
            if (levelNo < 1 || levelNo > Chapter5RewardOrder.Length) return new List<string>();
            return new List<string> { Chapter5RewardOrder[levelNo - 1] };
        }

        /// <summary>五L1 骨架：只开实路（虚路淡显零敌人、无切换按钮——F2.5 起 L2 才出钮）。池 17 选 5 无新卡。</summary>
        private static void SeedChapter5Level1(LevelConfig d)
        {
            d.title = "诡异 · 初见";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.voidRatio = 0f;    // F2.1：L1 100:0（教学关不出现虚面敌/换面钮）
            d.faceSwitch = false;
            d.poolChars = Chapter5PoolAt(1); // 基础 17 + 发序第 1 张（18 张；入包时机 = 进关即并集，本关就能选镜）
            d.rewardCards = Chapter5RewardAt(1); // 发序第 1 张入包（具体卡见 Chapter5RewardOrder）
            d.waves = new List<WaveLayout>
            {
                // F4.1：波表照 `波次草稿.csv` 五L1 段逐行转录（组间隔 / 与上波起点间隔 / 面=100:0 落在 voidRatio=0）。
                // F7.1（用户 2026-10-22）：前几关加客串「鼠」（36/1.5/掉墨 4，实面）供前期开荒攒墨——A 案以 csv 为主，客串为额外加菜。
                WaveT(20, TideLevel.Hold, Group("纸", 8, 1.4), GroupF("鼠", 3, 1.4, 0)),                                  // W1 纸首秀 + 鼠客串（攒墨）
                WaveT(22, TideLevel.Hold, Group("纸", 10, 1.3), GroupF("鼠", 3, 1.3, 0)),                                 // W2 建塔节奏 + 鼠
                WaveT(22, TideLevel.Hold, Group("纸", 10, 1.2), Group("狼", 2, 1.2), GroupF("鼠", 3, 1.2, 0)),             // W3 客串回归：狼+鼠
                WaveT(25, TideLevel.Hold, Group("纸", 12, 1.1), Group("狼", 2, 1.1), GroupF("鼠", 5, 1.1, 0)),             // W4 收官小考（8 塔均 L1.5）
            };
        }

        /// <summary>五L2 骨架：虚面首现位（F4 换影）+ 换面按钮首关（F2.5）。</summary>
        private static void SeedChapter5Level2(LevelConfig d)
        {
            d.title = "诡异 · 双影";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.voidRatio = 0.3f;  // F2.1：30% 虚面（换面钮首关）
            d.faceSwitch = true;
            d.poolChars = Chapter5PoolAt(2); // 基础 17 + 前 2 张（19 选 5）
            d.rewardCards = Chapter5RewardAt(2); // 发序第 2 张入包（首秀关；具体卡见 Chapter5RewardOrder）
            d.waves = new List<WaveLayout>
            {
                // F4.1：波表照 `波次草稿.csv` 五L2 段逐行转录（面=70:30 由 voidRatio 0.3 自动分；
                // 影恒虚面 → 组级 face=1）。W1 虚面首现但无影——「另一面也在流血」的第一课。
                // F7.1（用户 2026-10-22）：加客串鼠/猫（实面）攒墨；W5 收官猫×1 = 大墨惊喜（260 血）。
                WaveT(22, TideLevel.Hold, Group("纸", 10, 1.2), GroupF("鼠", 3, 1.2, 0)),                                 // W1 虚面首现（不可打）+ 鼠
                WaveT(25, TideLevel.Hold, Group("纸", 8, 1.1), GroupF("影", 2, 1.1, 1), GroupF("鼠", 3, 1.1, 0)),          // W2 影首秀：夜行不吃半速
                WaveT(25, TideLevel.Hold, Group("纸", 10, 1.0), GroupF("影", 4, 1.0, 1), GroupF("鼠", 3, 1.0, 0)),         // W3 第一次必须换面
                WaveT(25, TideLevel.Hold, Group("纸", 8, 1.0), GroupF("影", 6, 1.0, 1), GroupF("鼠", 4, 1.0, 0)),          // W4 往返收墨（5 墨跳 CD 首用）
                WaveT(25, TideLevel.Hold, Group("纸", 10, 0.9), GroupF("影", 6, 0.9, 1), Group("狼", 2, 0.9), GroupF("猫", 1, 0.9, 0)), // W5 收官 + 猫大墨
            };
        }

        /// <summary>五L3 骨架：空陆混编位（F4 换魇=飞行入梦）+ 雷首秀位（F3.2）。</summary>
        private static void SeedChapter5Level3(LevelConfig d)
        {
            d.title = "诡异 · 魇梦";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.voidRatio = 0.4f;  // F2.1：40% 虚面
            d.faceSwitch = true;
            d.poolChars = Chapter5PoolAt(3); // 基础 17 + 前 3 张（20 选 5）
            d.rewardCards = Chapter5RewardAt(3); // 发序第 3 张入包（首秀关；具体卡见 Chapter5RewardOrder）
            d.waves = new List<WaveLayout>
            {
                // F4.2：波表照 `波次草稿.csv` 五L3 段逐行转录（面=60:40；影恒虚、魇两面自动分）。
                // F7.1（用户 2026-10-22）：加客串鼠/猫（实面）攒墨；W5 猫×1 = 大墨惊喜。
                WaveT(22, TideLevel.Hold, Group("纸", 8, 1.1), GroupF("影", 4, 1.1, 1), GroupF("鼠", 3, 1.1, 0)),                        // W1 常规 + 鼠
                WaveT(25, TideLevel.Hold, Group("纸", 8, 1.0), GroupF("影", 4, 1.0, 1), Group("魇", 1, 1.0), GroupF("鼠", 3, 1.0, 0)),   // W2 魇首秀：入梦 2s
                WaveT(25, TideLevel.Hold, Group("纸", 10, 1.0), GroupF("影", 4, 1.0, 1), Group("魇", 1, 1.0), GroupF("鼠", 3, 1.0, 0)),  // W3 雷首秀：链电打扎堆
                WaveT(25, TideLevel.Hold, Group("纸", 6, 0.9), GroupF("影", 8, 0.9, 1), Group("魇", 2, 0.9), GroupF("鼠", 4, 0.9, 0)),   // W4 空陆混编
                WaveT(25, TideLevel.Hold, Group("纸", 12, 0.9), GroupF("影", 6, 0.9, 1), Group("魇", 2, 0.9), GroupF("猫", 1, 0.9, 0)),  // W5 链电考校 + 猫大墨
                WaveT(28, TideLevel.Hold, Group("纸", 12, 0.8), GroupF("影", 10, 0.8, 1), Group("魇", 3, 0.8), GroupF("鼠", 4, 0.8, 0)), // W6 收官（10-11 塔均 L2.3）
            };
        }

        /// <summary>五L4 骨架：50:50 双线压力位（F4 换傀=换面复活）+ 幡首秀位（F3.4）。</summary>
        private static void SeedChapter5Level4(LevelConfig d)
        {
            d.title = "诡异 · 傀戏";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.voidRatio = 0.5f;  // F2.1：50:50
            d.faceSwitch = true;
            d.poolChars = Chapter5PoolAt(4); // 基础 17 + 前 4 张（21 选 5 · 全卡池）
            d.rewardCards = Chapter5RewardAt(4); // 发序第 4 张入包（首秀关；具体卡见 Chapter5RewardOrder）
            d.waves = new List<WaveLayout>
            {
                // F4.3：波表照 `波次草稿.csv` 五L4 段逐行转录（面=50:50 双线同压；傀两面自动分）
                WaveT(22, TideLevel.Hold, Group("纸", 7, 1.0), GroupF("影", 7, 1.0, 1)),                                       // W1 均势开局
                WaveT(25, TideLevel.Hold, Group("纸", 6, 1.0), GroupF("影", 6, 1.0, 1), Group("傀", 1, 1.0)),                  // W2 傀首秀：换面 50% 复活
                WaveT(25, TideLevel.Hold, Group("纸", 6, 0.9), GroupF("影", 6, 0.9, 1), Group("魇", 2, 0.9)),                  // W3 空袭：对空缺口检查
                WaveT(25, TideLevel.Hold, Group("纸", 7, 0.9), GroupF("影", 7, 0.9, 1), Group("傀", 1, 0.9)),                  // W4 双线同时压
                WaveT(25, TideLevel.Hold, GroupF("影", 12, 0.9, 1), Group("魇", 2, 0.9)),                                     // W5 虚面重压：押面取舍
                WaveT(28, TideLevel.Hold, Group("纸", 8, 0.8), GroupF("影", 8, 0.8, 1), Group("傀", 1, 0.8), Group("魇", 2, 0.8)), // W6 L4 尖峰
            };
        }

        /// <summary>五L5 骨架：傩面/傩神 Boss 位（F5/F6 替换 W1/W5 入场 + W7 毕业考）。</summary>
        private static void SeedChapter5Level5(LevelConfig d)
        {
            d.title = "诡异 · 傩面";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.voidRatio = 0.45f; // F2.1：45% 虚面（毕业考）
            d.faceSwitch = true;
            d.poolChars = Chapter5PoolAt(5); // 封顶：仍是全池 21（五L5 不发新卡）
            d.rewardCards = Chapter5RewardAt(5); // 五L5 无新卡（超出发序 = 空）
            d.waves = new List<WaveLayout>
            {
                // F4.4：波表照 `波次草稿.csv` 五L5 段逐行转录（面=55:45；小鬼不进波表 —— 青脸召唤）。
                // Boss 入场（W1 傩面 10000 / W5 傩神 12000）归 F5.1 / F6.1 在同一张表上追加。
                WaveT(22, TideLevel.Hold, Group("纸", 6, 1.0), GroupF("影", 6, 1.0, 1), GroupF("傩面", 1, 1.0, 0)),      // W1 P1 开始（傩面入场：赤脸/实路，面显式钉 0）
                WaveT(25, TideLevel.Hold, GroupF("影", 8, 0.9, 1), Group("魇", 2, 0.9)),                                        // W2 青脸召鬼：小鬼收墨
                WaveT(25, TideLevel.Hold, Group("纸", 6, 0.9), GroupF("影", 6, 0.9, 1), Group("傀", 1, 0.9)),                   // W3 金脸封印：换面打断
                WaveT(25, TideLevel.Hold, Group("纸", 6, 0.9), GroupF("影", 6, 0.9, 1), Group("魇", 2, 0.9)),                   // W4 P1 收尾（三脸全见）
                WaveT(28, TideLevel.Hold, Group("纸", 6, 0.9), GroupF("影", 6, 0.9, 1)),                                        // W5 P2 开始（傩神入场位）
                WaveT(25, TideLevel.Hold, GroupF("影", 8, 0.9, 1), Group("傀", 1, 0.9), Group("魇", 2, 0.9)),                   // W6 傩舞窗口：×1.5 集火
                WaveT(30, TideLevel.Hold, Group("纸", 8, 0.8), GroupF("影", 8, 0.8, 1), Group("魇", 3, 0.8)),                   // W7 终局
            };
        }

        /// <summary>
        /// F3.5（ch5）：卡池 + 解锁结构机器校验。四件事：
        ///   ① 四张 ch5 特卡参数在档（镜 createsMirror·50% / 雷 链 3×0.6@2.0 / 煞 虚面 ×1.5 / 幡 聚魂）
        ///      —— 缺字段 = 卡进了池但机制哑火（比报错更难查）；
        ///   ② 解锁**结构**（不认死顺序：发放序是黄底，用户会调）：每关池 = 上一关池 ∪ 本关 rewardCards，
        ///      池只增不减、增量 = 本关发放张数、新卡不得提前出现在更早的池里、四张新卡必须发全；
        ///   ③ 池内每个字都在 `TowerPool` 里（拼错 = 构筑面板缺价、放不下）；
        ///   ④ 池规模 = 17 + 已发新卡累计（现状 18/19/20/21/21——五L5 不发卡但池仍满 21）。
        /// 只警告不阻断（黄底数据：去 TowerPool.asset / Level22-25.asset 改，重跑自愈复检）。
        /// </summary>
        private static void ValidateChapter5Cards(TowerPoolDef pool, LevelConfig c5l1, LevelConfig c5l2,
            LevelConfig c5l3, LevelConfig c5l4, LevelConfig c5l5)
        {
            if (pool == null) return;
            int issues = 0;

            // ① 四张特卡参数
            var mirror = pool.Find("镜");
            if (mirror == null || !mirror.createsMirror || System.Math.Abs(mirror.mirrorDamageRatio - 0.5) > 1e-6)
            {
                Debug.LogWarning("[ch5 卡池] 镜：缺 createsMirror 或 mirrorDamageRatio≠0.5 —— 影子不生成 / 不是本体 50%");
                issues++;
            }
            var thunder = pool.Find("雷");
            if (thunder == null || thunder.chainJumps != 3 ||
                System.Math.Abs(thunder.chainRatio - 0.6) > 1e-6 || System.Math.Abs(thunder.chainRadius - 2.0) > 1e-6)
            {
                Debug.LogWarning("[ch5 卡池] 雷：chainJumps≠3 / chainRatio≠0.6 / chainRadius≠2.0 —— 链电口径跑偏");
                issues++;
            }
            var wrath = pool.Find("煞");
            if (wrath == null || System.Math.Abs(wrath.voidDamageMult - 1.5) > 1e-6)
            {
                Debug.LogWarning("[ch5 卡池] 煞：voidDamageMult≠1.5 —— 虚面加成缺失（唯一虚面加成卡）");
                issues++;
            }
            var banner = pool.Find("幡");
            if (banner == null || !banner.soulBanner || banner.soulPayout <= 0)
            {
                Debug.LogWarning("[ch5 卡池] 幡：soulBanner 未开或 soulPayout≤0 —— 击杀不聚魂");
                issues++;
            }

            // ② 解锁结构（**不认死顺序**——用户 2026-10-22 调过发放序）：
            //    每关池 = 上一关池 + 本关 rewardCards（累积、只增不减），新卡不得提前出现在更早的池里；
            //    五关合计恰好把四张新卡发完（顺序随意，但一张不能少、不能重）。
            var levels = new[] { c5l1, c5l2, c5l3, c5l4, c5l5 };
            var names = new[] { "五L1", "五L2", "五L3", "五L4", "五L5" };
            var unlocked = new HashSet<string>();
            List<string> prevPool = null;
            for (int i = 0; i < levels.Length; i++)
            {
                var lv = levels[i];
                if (lv == null) continue;
                var poolChars = lv.poolChars ?? new List<string>();
                var rewards = lv.rewardCards ?? new List<string>();

                // ③ 池内每个字都得在 TowerPool（拼错 = 面板缺价、放不下）
                foreach (var c in poolChars)
                    if (pool.Find(c) == null)
                    {
                        Debug.LogWarning($"[ch5 卡池] {names[i]}：池内「{c}」不在 TowerPool —— 构筑面板缺价、放不下");
                        issues++;
                    }

                // 本关新卡：须在本关池里（入包即可选）、且此前没发过
                foreach (var c in rewards)
                {
                    if (!poolChars.Contains(c))
                    {
                        Debug.LogWarning($"[ch5 卡池] {names[i]}：奖励「{c}」不在本关池里 —— 入包了却选不到");
                        issues++;
                    }
                    if (unlocked.Contains(c))
                    {
                        Debug.LogWarning($"[ch5 卡池] {names[i]}：奖励「{c}」重复发放（此前已入包）");
                        issues++;
                    }
                    unlocked.Add(c);
                }

                // ④ 池规模 = 基础 17 + 已发新卡累计（本关算完后；现状 18/19/20/21/21——
                //    五L5 不发新卡，但池仍是满 21）
                int expect = 17 + unlocked.Count;
                if (poolChars.Count != expect)
                {
                    Debug.LogWarning($"[ch5 卡池] {names[i]}：池 {poolChars.Count} 张，设计值 {expect} 张（= 17 + 已发新卡 {unlocked.Count} 张）");
                    issues++;
                }

                // 池只增不减 + 增量 = 本关发放张数
                if (prevPool != null)
                {
                    foreach (var c in prevPool)
                        if (!poolChars.Contains(c))
                        {
                            Debug.LogWarning($"[ch5 卡池] {names[i]}：池里丢了上一关就有的「{c}」（池应只增不减）");
                            issues++;
                        }
                    int added = poolChars.Count - prevPool.Count;
                    if (added != rewards.Count)
                    {
                        Debug.LogWarning($"[ch5 卡池] {names[i]}：池 +{added} 张但发了 {rewards.Count} 张卡（应 1:1）");
                        issues++;
                    }
                }

                prevPool = poolChars;
            }

            // 四张新卡必须发全（顺序随意，缺一张就是少一条构筑支线）
            foreach (var c in Chapter5NewCards)
                if (!unlocked.Contains(c))
                {
                    Debug.LogWarning($"[ch5 卡池] 四张新卡漏发「{c}」（镜/雷/煞/幡 应全部在 ch5 内入包）");
                    issues++;
                }

            if (issues == 0)
                Debug.Log("[ch5 卡池] 校验通过：21 张全池 / 发放序 " + RewardSig(c5l2) + " → " + RewardSig(c5l3) +
                    " → " + RewardSig(c5l4) + " → " + RewardSig(c5l5));
        }

        /// <summary>ch5 四张新卡（发放顺序可调——本表只用于「发全了吗」，不参与顺序判定）。</summary>
        private static readonly string[] Chapter5NewCards = { "镜", "雷", "煞", "幡" };

        /// <summary>某关 rewardCards 的一行摘要（日志用；空 = 「—」）。</summary>
        private static string RewardSig(LevelConfig lv)
        {
            if (lv == null || lv.rewardCards == null || lv.rewardCards.Count == 0) return "—";
            return string.Join("+", lv.rewardCards);
        }

        /// <summary>
        /// 章图机器校验（F1.3）：重复格 / 出屏 / 压路（陆路 + 水路路面砖）/ 折线非轴对齐 / 双线汇合。
        /// 只警告不阻断——章图是黄底数据，校验提示问题、由策划/用户在 Inspector 调，重跑自愈复检。
        /// </summary>
        private static void ValidateChapter(ChapterConfig d)
        {
            if (d == null) return;
            int issues = 0;

            // 1. 重复格
            var seen = new HashSet<Vector2Int>();
            foreach (var c in d.cells)
            {
                if (!seen.Add(new Vector2Int(c.x, c.y)))
                {
                    Debug.LogWarning($"[章图校验] {d.title}：格子重复 ({c.x},{c.y})");
                    issues++;
                }
            }

            // 2. 格子出屏（相机 ±9.6 / ±5.4，格中心留半格余量）
            foreach (var c in d.cells)
                if (Mathf.Abs(c.x) > 9 || Mathf.Abs(c.y) > 4)
                {
                    Debug.LogWarning($"[章图校验] {d.title}：格子出屏 ({c.x},{c.y})");
                    issues++;
                }

            // 3. 路线：非轴对齐折段（CollectRoadTiles 只铺水平/垂直段，斜段会漏砖）+ 路面出屏
            var routes = new[] { d.landRoute, d.waterRoute, d.voidRoute };
            var routeNames = new[] { "陆路", "水路", "虚路" };
            var road = new HashSet<Vector2Int>();
            for (int r = 0; r < routes.Length; r++)
            {
                var route = routes[r];
                if (route == null || route.Count < 2) continue;
                for (int i = 1; i < route.Count; i++)
                {
                    Vector2 a = route[i - 1], b = route[i];
                    if (!Mathf.Approximately(a.x, b.x) && !Mathf.Approximately(a.y, b.y))
                    {
                        Debug.LogWarning($"[章图校验] {d.title}·{routeNames[r]}：折点 {i} 非轴对齐 ({a.x},{a.y})→({b.x},{b.y})，路面砖会缺失");
                        issues++;
                    }
                }
                foreach (var t in PathSystem.CollectRoadTiles(route.ToArray()))
                {
                    road.Add(t);
                    if (Mathf.Abs(t.x) > 9 || Mathf.Abs(t.y) > 4)
                    {
                        Debug.LogWarning($"[章图校验] {d.title}·{routeNames[r]}：路面出屏 ({t.x},{t.y})");
                        issues++;
                    }
                }
            }

            // 4. 压路（格子与路面砖同格：放不进也点不到，硬冲突）
            foreach (var c in d.cells)
                if (road.Contains(new Vector2Int(c.x, c.y)))
                {
                    Debug.LogWarning($"[章图校验] {d.title}：格子压路 ({c.x},{c.y})");
                    issues++;
                }

            // 5. 双线汇合：水路非空 → 终点须与陆路一致（共享尾段共奔印章）
            if (d.waterRoute != null && d.waterRoute.Count >= 2 &&
                d.landRoute != null && d.landRoute.Count >= 2)
            {
                Vector2 landEnd = d.landRoute[d.landRoute.Count - 1];
                Vector2 waterEnd = d.waterRoute[d.waterRoute.Count - 1];
                if (landEnd != waterEnd)
                {
                    Debug.LogWarning($"[章图校验] {d.title}：水路终点 ({waterEnd.x},{waterEnd.y}) ≠ 陆路终点 ({landEnd.x},{landEnd.y})——两线须汇合共奔印章");
                    issues++;
                }
            }

            // 6. F1.3（ADR-040）同路双向：水路非空 = 不支持（lane 1 敌走全程单口，半程路由只切陆路）；
            //    家 = 陆路弧长中点（敌半程终点）须在屏内——手调折点后家漂出屏 = 双口漏过点不可见
            if (d.bidirectional)
            {
                if (d.waterRoute != null && d.waterRoute.Count >= 2)
                {
                    Debug.LogWarning($"[章图校验] {d.title}：同路双向章不支持水路（lane 1 敌走全程单口）");
                    issues++;
                }
                var home = PolylineMidpoint(d.landRoute);
                if (Mathf.Abs(home.x) > 9 || Mathf.Abs(home.y) > 4)
                {
                    Debug.LogWarning($"[章图校验] {d.title}：家（弧长中点 {home.x},{home.y}）出屏——双口半程终点不可见");
                    issues++;
                }
            }

            // 7. F1.2（ch5 双面）：双面章 → 虚路必填 + 与陆路等长（±0.01）+ **同终点（家）**；
            //    两路等长是傩面换面「同路程进度落点」的硬前提。出生口自 2026-10-22 用户改图起
            //    不再要求一致（实 (-9,3) / 虚 (-9,-3)，两路互为 y=0 镜像——换面 = 干净镜像跳）。
            if (d.faceEnabled)
            {
                if (d.voidRoute == null || d.voidRoute.Count < 2)
                {
                    Debug.LogWarning($"[章图校验] {d.title}：双面章缺虚路（voidRoute）——非激活面无敌道");
                    issues++;
                }
                else
                {
                    double landLen = PolylineLength(d.landRoute);
                    double voidLen = PolylineLength(d.voidRoute);
                    if (System.Math.Abs(landLen - voidLen) > 0.01)
                    {
                        Debug.LogWarning($"[章图校验] {d.title}：虚实两路不等长（实 {landLen:0.00} / 虚 {voidLen:0.00}）——换面进度落点会跳变");
                        issues++;
                    }
                    if (d.landRoute != null && d.landRoute.Count >= 2)
                    {
                        if (Vector2.Distance(d.landRoute[d.landRoute.Count - 1], d.voidRoute[d.voidRoute.Count - 1]) > 0.01f)
                        {
                            Debug.LogWarning($"[章图校验] {d.title}：虚实两路终点（家）不一致——漏怪不共奔同一印章");
                            issues++;
                        }
                    }
                }
            }
            else if (d.voidRoute != null && d.voidRoute.Count > 0)
            {
                Debug.LogWarning($"[章图校验] {d.title}：写了虚路但 faceEnabled=false——虚路不会生效");
                issues++;
            }

            if (issues == 0)
                Debug.Log($"[章图校验] {d.title}：全部通过（格 {d.cells.Count}；陆路 {d.landRoute?.Count ?? 0} 折、水路 {d.waterRoute?.Count ?? 0} 折）");
            else
                Debug.LogWarning($"[章图校验] {d.title}：共 {issues} 处问题——黄底数据可在 Inspector 调整后重跑自愈复检");
        }

        /// <summary>折线总长（F1.2 双面等长校验；空折线 0）。</summary>
        private static double PolylineLength(List<Vector2> wps)
        {
            if (wps == null || wps.Count < 2) return 0;
            double total = 0;
            for (int i = 1; i < wps.Count; i++) total += Vector2.Distance(wps[i - 1], wps[i]);
            return total;
        }

        /// <summary>折线弧长中点（F1.3 / ADR-040：家 = 敌半程终点；空折线返回原点）。</summary>
        private static Vector2 PolylineMidpoint(List<Vector2> wps)
        {
            if (wps == null || wps.Count < 2) return Vector2.zero;
            double total = 0;
            for (int i = 1; i < wps.Count; i++) total += Vector2.Distance(wps[i - 1], wps[i]);
            double half = total / 2.0, walked = 0;
            for (int i = 1; i < wps.Count; i++)
            {
                double seg = Vector2.Distance(wps[i - 1], wps[i]);
                if (seg > 0 && walked + seg >= half)
                    return Vector2.Lerp(wps[i - 1], wps[i], (float)((half - walked) / seg));
                walked += seg;
            }
            return wps[wps.Count - 1];
        }

        /// <summary>L1 教学关（v3.0）：刀矛弓 3 种池；4 波（虫蛛，波次草稿.csv）。格子在章图（ADR-021）。</summary>
        /// <summary>
        /// L0 教学关（ADR-030）：单字池「刀」= 起手必有对（合成教学不靠抽卡运气）；
        /// 唯一波 start=9999 = 永不自动开战（纯数据挡怪），教学六步走完由玩家点「下一波」手动开打。
        /// 战斗压力刻意轻（3 只虫 < 5 命）：即便全漏塔也能通关进 L1，教学关不可失败。
        /// </summary>
        private static void SeedLevel0(LevelConfig d)
        {
            d.title = "教学 · 执笔入门";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.poolChars = new List<string> { "刀" };
            d.rewardCards = new List<string> { "刀" }; // 入包即拥有；进 L1 时再并上 刀矛弓 全套

            d.waves = new List<WaveLayout>
            {
                Wave(9999, Group("虫", 3, 2.0)), // 手动开战才触发（EarlySummon 平移时间线，数值安全）
            };
        }

        private static void SeedLevel1(LevelConfig d)
        {
            d.title = "第一卷 · 墨启";
            d.startLives = 5;
            d.prepSeconds = 20; // 备战 20s（波次草稿.csv）
            d.poolChars = new List<string> { "刀", "矛", "弓" };
            d.rewardCards = new List<string> { "刀", "矛", "弓" }; // 初始背包（关卡草稿.csv）

            d.waves = new List<WaveLayout>
            {
                Wave(20, Group("虫", 5, 1.5)),
                Wave(22, Group("虫", 6, 1.2), Group("蛛", 2, 2.0)),   // 蛛首秀（分裂软压力）
                Wave(22, Group("虫", 8, 1.0), Group("蛛", 3, 1.8)),
                Wave(22, Group("蛛", 5, 1.5), Group("虫", 6, 0.8)),  // 虫潮收官小考
            };
        }

        /// <summary>L2（v3.0）：+墨 4 种池；5 波（狼熊虎首秀，波次草稿.csv）。格子在章图。</summary>
        private static void SeedLevel2(LevelConfig d)
        {
            d.title = "第二卷 · 霜刃";
            d.startLives = 5;
            d.prepSeconds = 18;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨" };
            d.rewardCards = new List<string> { "墨" };

            d.waves = new List<WaveLayout>
            {
                Wave(18, Group("狼", 6, 1.5), Group("虫", 4, 1.2)),  // 狼首秀（快速）+ 热身
                Wave(20, Group("熊", 2, 4.0), Group("虫", 8, 1.0)),  // 熊首秀（厚血）
                Wave(20, Group("狼", 8, 1.0), Group("蛛", 3, 1.8)),
                Wave(20, Group("熊", 3, 3.5), Group("狼", 6, 1.0)),
                Wave(22, Group("虎", 1, 5.0), Group("熊", 2, 3.0), Group("狼", 8, 0.9)), // 虎首秀收官
            };
        }

        /// <summary>L3（v3.0）：+铲 5 种全上；6 波（鸟鹰空袭，波次草稿.csv）。格子在章图。</summary>
        private static void SeedLevel3(LevelConfig d)
        {
            d.title = "第三卷 · 垦拓";
            d.startLives = 5;
            d.prepSeconds = 18;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲" }; // 铲：池内普通卡（均等权重）
            d.rewardCards = new List<string> { "铲" }; // 关卡草稿.csv：L3 另解锁背包（阶段 D 接管）

            d.waves = new List<WaveLayout>
            {
                Wave(18, Group("鸟", 4, 2.0), Group("虫", 6, 1.2)),                        // 鸟首秀：只有弓能打
                Wave(20, Group("鹰", 3, 1.8), Group("狼", 6, 1.2)),                        // 鹰首秀 + 开垦回报
                Wave(20, Group("鸟", 6, 1.5), Group("蛛", 4, 1.8), Group("熊", 2, 3.5)),  // 空+地混编
                Wave(20, Group("鹰", 4, 1.2), Group("鸟", 6, 1.0)),                        // 纯空袭波
                Wave(22, Group("熊", 3, 3.0), Group("狼", 8, 0.9), Group("鸟", 4, 1.5)),
                Wave(22, Group("鹰", 5, 1.0), Group("虎", 1, 4.5), Group("蛛", 4, 1.5)),  // 收官综合
            };
        }

        /// <summary>
        /// L4（v3.0）：+棍戟 7 种池（构筑 7 选 5 阶段 D 接管，现阶段全池）；6 波（牛首秀，
        /// 波次草稿.csv）。牛半血加速为阶段 C 机制，现阶段先按数值跑。
        /// </summary>
        private static void SeedLevel4(LevelConfig d)
        {
            d.title = "第四卷 · 砺锋";
            d.startLives = 5;
            d.prepSeconds = 18;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟" };
            d.rewardCards = new List<string> { "棍", "戟" };

            d.waves = new List<WaveLayout>
            {
                Wave(18, Group("牛", 2, 3.0), Group("虫", 8, 1.0)),   // 牛首秀：半血加速（阶段 C 机制）
                Wave(20, Group("牛", 2, 3.0), Group("鸟", 6, 1.5)),   // 构筑提醒：空中敌仍在（别丢弓）
                Wave(20, Group("熊", 4, 3.0), Group("狼", 8, 0.9), Group("鹰", 3, 1.5)), // 地面厚血+空快混编
                Wave(20, Group("牛", 3, 2.5), Group("虎", 1, 4.0)),   // 加速+厚血：输出爆发考校
                Wave(22, Group("牛", 3, 2.5), Group("蛛", 5, 1.5), Group("鸟", 4, 1.2)), // 分裂+加速+空三重
                Wave(22, Group("牛", 4, 2.0), Group("熊", 3, 3.0)),   // 收官：加速怪大考
            };
        }

        /// <summary>
        /// L5（v3.0）：+弩 8 种全池；7 波（野人/酋长首秀，波次草稿.csv）。酋长召唤+到点即败
        /// 为阶段 C 机制，现阶段是 9000 血肉墙（可磨死）；数值照 CSV 母本不动。
        /// </summary>
        private static void SeedLevel5(LevelConfig d)
        {
            d.title = "第五卷 · 围猎";
            d.startLives = 5;
            d.prepSeconds = 18;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩" };
            d.rewardCards = new List<string> { "弩" };

            d.waves = new List<WaveLayout>
            {
                Wave(18, Group("野人", 1, 6.0), Group("狼", 8, 1.0)), // 野人首秀：700 血+扣 2 印（阶段 C livesCost）
                Wave(20, Group("野人", 2, 5.0), Group("熊", 3, 3.0)), // 超厚+高血组合
                Wave(20, Group("牛", 3, 2.5), Group("鹰", 5, 1.2), Group("鸟", 6, 1.0)), // 加速+空袭混编
                Wave(20, Group("野人", 2, 4.5), Group("虎", 1, 4.0), Group("牛", 2, 2.5)), // 三精英同场
                Wave(22, Group("野人", 2, 4.0), Group("熊", 4, 2.5), Group("狼", 8, 0.9)), // 大混战
                Wave(22, Group("牛", 4, 2.0), Group("虎", 1, 3.5), Group("鸟", 6, 1.0)), // 大战前夜：清杂+留种塔
                Wave(25, Group("野人", 2, 5.0), Group("酋长", 1, 5.0)), // Boss：召唤+到点即败（阶段 C 机制）
            };
        }

        private static SpawnGroup Group(string charId, int count, double interval) =>
            new SpawnGroup { charId = charId, count = count, interval = interval };

        /// <summary>F4.1（ch5 双面）：**指定面**的组（1 = 虚面——影只从虚路来；-1 = 按关级配比自动）。
        /// ch1-4 一律用 `Group`（缺省 -1 + voidRatio 0 → 恒实面），故本助手不影响旧章。</summary>
        private static SpawnGroup GroupF(string charId, int count, double interval, int face) =>
            new SpawnGroup { charId = charId, count = count, interval = interval, face = face };

        private static WaveLayout Wave(double startAfterPrev, params SpawnGroup[] groups) =>
            new WaveLayout { groups = new List<SpawnGroup>(groups), startAfterPrev = startAfterPrev };

        /// <summary>F4.4：带潮位档的波（ch2 专用；ch1 用 Wave 全 Hold）。</summary>
        private static WaveLayout WaveT(double startAfterPrev, TideLevel tide, params SpawnGroup[] groups) =>
            new WaveLayout { groups = new List<SpawnGroup>(groups), startAfterPrev = startAfterPrev, tide = tide };

        /// <summary>ch3 波次（F1.3 / ADR-040）：口=上/下/双（波次草稿.csv 备注列「口=」转录）；潮位缺省 Hold（ch3 无潮汐）。</summary>
        private static WaveLayout WaveT(double startAfterPrev, SpawnPort port, params SpawnGroup[] groups) =>
            new WaveLayout { groups = new List<SpawnGroup>(groups), startAfterPrev = startAfterPrev, spawnPort = port };

        // ============ 第二章关卡种子（F4.4 / ADR-031；design/numbers/草稿/波次草稿.csv 二L 前缀，
        //              数值黄底 W3 定稿；潮位列照 CSV 备注「潮=」逐波落，L4-L5「大涨」仅 W2 标大——
        //              与 06 §10.2 括号口径的分叉转策划侧） ============

        /// <summary>二L1：无新卡（8 选 5，旧卡遇新图）；4 波；潮汐首秀（W2 涨潮 = 第一次撤离）。</summary>
        private static void SeedChapter2Level1(LevelConfig d)
        {
            d.title = "水泽 · 初潮";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩" }; // 8 选 5
            d.rewardCards = new List<string>(); // 无新卡（GDD §5.2 发放节奏：潮汐先教自己）

            d.waves = new List<WaveLayout>
            {
                WaveT(25, TideLevel.Calm, Group("虫", 6, 1.2), Group("狼", 2, 1.2)),   // 草原客开场=章间难度重置
                WaveT(25, TideLevel.Rise, Group("鱼", 4, 1.0), Group("虾", 3, 1.0)),   // 水路敌首秀；岸弓打水
                WaveT(25, TideLevel.Calm, Group("虾", 6, 1.0), Group("虫", 4, 1.0)),   // 水退（涨→平）：虾群量测
                WaveT(25, TideLevel.Rise, Group("鱼", 5, 1.0), Group("虾", 4, 1.0), Group("狼", 3, 1.0)), // 二次撤离
            };
        }

        /// <summary>二L2：+水（9 选 5）；5 波；缓速首验 + 撤离三选项实战。</summary>
        private static void SeedChapter2Level2(LevelConfig d)
        {
            d.title = "水泽 · 缓涛";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水" };
            d.rewardCards = new List<string> { "水" };

            d.waves = new List<WaveLayout>
            {
                WaveT(25, TideLevel.Calm, Group("虫", 8, 1.0), Group("鱼", 3, 1.0)),   // 缓速首验：水塔对鱼
                WaveT(25, TideLevel.Rise, Group("龟", 2, 1.5), Group("虾", 4, 1.5)),   // 龟首秀于涨潮
                WaveT(25, TideLevel.Ebb, Group("龟", 3, 1.2), Group("鱼", 6, 1.2)),    // 退潮窗口扩张：反攻节奏
                WaveT(25, TideLevel.Calm, Group("狼", 6, 1.2), Group("熊", 2, 1.2), Group("鱼", 4, 1.2)), // 陆水双线
                WaveT(25, TideLevel.Rise, Group("龟", 3, 1.0), Group("鱼", 5, 1.0), Group("虫", 6, 1.0)), // 撤离考校收官
            };
        }

        /// <summary>二L3：+渔（10 选 5）；6 波；渔水中满速经济 + 鳄两栖分段首秀。</summary>
        private static void SeedChapter2Level3(LevelConfig d)
        {
            d.title = "水泽 · 渔汛";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔" };
            d.rewardCards = new List<string> { "渔" };

            d.waves = new List<WaveLayout>
            {
                WaveT(22, TideLevel.Calm, Group("鱼", 5, 1.0), Group("虾", 5, 1.0), Group("虫", 3, 1.0)), // 渔入池
                WaveT(25, TideLevel.Rise, Group("鳄", 1, 1.5), Group("龟", 2, 1.5)),   // 鳄首秀：两栖分段变速
                WaveT(25, TideLevel.Ebb, Group("虾", 8, 1.0), Group("鱼", 5, 1.0)),    // 渔经济窗口（退潮=水中地皮多）
                WaveT(25, TideLevel.Calm, Group("鳄", 2, 1.2), Group("狼", 5, 1.2)),   // 鳄+快怪双线
                WaveT(25, TideLevel.Rise, Group("熊", 3, 1.2), Group("龟", 2, 1.2), Group("虾", 4, 1.2)), // 混编撤离
                WaveT(25, TideLevel.Rise, Group("鳄", 2, 1.0), Group("虾", 8, 1.0), Group("鱼", 6, 1.0)), // 收官：大撤离+水路洪峰
            };
        }

        /// <summary>二L4：+冰（11 选 5）；6 波；高潮位大淹没（W2 大涨 = 首淹左低地）+ 湿→冻连携教学。</summary>
        private static void SeedChapter2Level4(LevelConfig d)
        {
            d.title = "水泽 · 冰封";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰" };
            d.rewardCards = new List<string> { "冰" };

            d.waves = new List<WaveLayout>
            {
                WaveT(25, TideLevel.Calm, Group("牛", 2, 1.2), Group("鱼", 4, 1.2)),   // 草原客主力：牛加速+水路
                WaveT(25, TideLevel.Surge, Group("牛", 4, 1.2), Group("熊", 3, 1.2)),  // 首个大淹没：+左低地 2-4 格（高潮位撤离教学）
                WaveT(25, TideLevel.Calm, Group("鳄", 2, 1.5), Group("龟", 3, 1.5)),   // 厚血混编：控制链检验
                WaveT(25, TideLevel.Rise, Group("虎", 1, 1.2), Group("牛", 3, 1.2), Group("虾", 6, 1.2)), // 湿→冻连携考校
                WaveT(25, TideLevel.Calm, Group("鹰", 4, 1.0), Group("鸟", 5, 1.0), Group("鱼", 4, 1.0)), // 空袭混编：冰对空价值
                WaveT(25, TideLevel.Rise, Group("牛", 4, 1.0), Group("鳄", 2, 1.0), Group("虾", 8, 1.0)), // 尖峰收官
            };
        }

        /// <summary>二L5：11 选 5（无新卡）；7 波；W7 蛟毕业考（1/3 血转阶段归 F5 实装——现阶段蛟=水路 Boss+召唤）。</summary>
        private static void SeedChapter2Level5(LevelConfig d)
        {
            d.title = "水泽 · 蛟怒";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰" };
            d.rewardCards = new List<string>(); // 预告文案「蛟怒则泽国」归 F5.4/F4.6（L5 开场文案）

            d.waves = new List<WaveLayout>
            {
                WaveT(22, TideLevel.Calm, Group("鱼", 6, 1.0), Group("虾", 4, 1.0), Group("狼", 4, 1.0)), // 热身
                WaveT(25, TideLevel.Rise, Group("鳄", 2, 1.5), Group("龟", 2, 1.5)),   // 中坚：撤离+输出并行
                WaveT(25, TideLevel.Calm, Group("野人", 1, 1.2), Group("熊", 2, 1.2)), // 精英回归：草原客压轴
                WaveT(25, TideLevel.Rise, Group("虾", 10, 0.8), Group("鱼", 8, 0.8)),  // 水路洪峰：鱼虾海
                WaveT(25, TideLevel.Calm, Group("野人", 2, 1.2), Group("牛", 3, 1.2)), // 大战前夜：种塔存活窗口
                WaveT(25, TideLevel.Rise, Group("鳄", 3, 1.0), Group("龟", 3, 1.0), Group("狼", 8, 1.0)), // 清场尖峰
                WaveT(30, TideLevel.Calm, Group("蛟", 1, 1.5), Group("鱼", 4, 1.5)),   // Boss：蛟 13000 + 召唤 2 鱼/20s 上限 6
            };
        }

        // ============ 第三章关卡种子（F4.3 / ADR-040；design/numbers/草稿/波次草稿.csv 三L 前缀，
        //              数值黄底手测定稿；出口「口=」照 CSV 备注列逐波转录；潮位全 Hold（ch3 无潮汐）） ============

        /// <summary>三L1：无新卡（11 选 5，旧卡遇黑夜）；5 波；迷雾首见 + 家灯 4.5 教「光是什么」（06 §11.5）。</summary>
        private static void SeedChapter3Level1(LevelConfig d)
        {
            d.title = "幽夜 · 入暮";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.homeLampRadius = 4.5f; // 家灯 L1 = 4.5（06 §11.2 拐杖曲线起点）
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰" }; // 11 选 5
            d.rewardCards = new List<string>(); // 无新卡（家灯先教视野——06 §11.5：L1 无新卡）

            d.waves = new List<WaveLayout>
            {
                WaveT(25, SpawnPort.Upper, Group("鼠", 4, 1.2)),                                           // W1 口=上：夜行首战+家灯视野体验
                WaveT(25, SpawnPort.Lower, Group("鼠", 5, 1.0), Group("虫", 4, 1.0)),                     // W2 口=下：下口首见（双口轮替教学）
                WaveT(25, SpawnPort.Both, Group("虫", 8, 1.0), Group("鼠", 6, 1.0)),                       // W3 口=双：首次双口同波（深影→光内开火循环节奏）
                WaveT(25, SpawnPort.Upper, Group("狼", 4, 1.0), Group("虫", 6, 1.0), Group("鼠", 4, 1.0)), // W4 口=上：夜狼首现（雾中加速体感——F2.2 nocturnal）
                WaveT(25, SpawnPort.Both, Group("鼠", 8, 0.9), Group("虫", 6, 0.9), Group("狼", 2, 0.9)),  // W5 口=双：L1 收官光圈火力综合
            };
        }

        /// <summary>三L2：+灯（12 选 5）；6 波；放灯+灯配对升阶 + 飞蛾扑火（蛾光中易伤 F2.2）；家灯 3.5。</summary>
        private static void SeedChapter3Level2(LevelConfig d)
        {
            d.title = "幽夜 · 灯信";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.homeLampRadius = 3.5f;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯" }; // 12 选 5
            d.rewardCards = new List<string> { "灯" };

            d.waves = new List<WaveLayout>
            {
                WaveT(25, SpawnPort.Upper, Group("鼠", 6, 1.1), Group("蛾", 2, 1.1)),                       // W1 口=上：蛾首秀（空中）——对空旧卡+光中易伤
                WaveT(25, SpawnPort.Lower, Group("蛾", 5, 1.0), Group("虫", 6, 1.0)),                      // W2 口=下：蛾群体感（家灯照蛾=快杀）
                WaveT(25, SpawnPort.Both, Group("鼠", 8, 1.0), Group("蛾", 3, 1.0), Group("狼", 3, 1.0)),  // W3 口=双：放灯课（灯放路中段照蛾=光廊初试）
                WaveT(25, SpawnPort.Upper, Group("狼", 6, 1.0), Group("蛾", 4, 1.0)),                      // W4 口=上：夜狼+蛾混编（灯照还原狼速首验）
                WaveT(25, SpawnPort.Both, Group("鸟", 4, 1.0), Group("蛾", 4, 1.0), Group("鼠", 5, 1.0)),  // W5 口=双：空中客+蛾（对空火力分配）
                WaveT(25, SpawnPort.Both, Group("蛾", 8, 0.9), Group("鼠", 6, 0.9), Group("狼", 4, 0.9)),  // W6 口=双：L2 收官（灯位选择检验：中段 vs 翼端）
            };
        }

        /// <summary>三L3：无新卡（12 选 5）；6 波；潜行=找浅影（蝠首秀+夜狼客大波）+ 灯位哲学；家灯 2.5。</summary>
        private static void SeedChapter3Level3(LevelConfig d)
        {
            d.title = "幽夜 · 浮影";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.homeLampRadius = 2.5f;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯" };
            d.rewardCards = new List<string>(); // L3 无新卡（潜行敌成势，灯位哲学——06 §11.5）

            d.waves = new List<WaveLayout>
            {
                WaveT(25, SpawnPort.Upper, Group("蝠", 3, 1.1), Group("鼠", 6, 1.1)),                      // W1 口=上：蝠首秀（潜行）——看漏→进光现形
                WaveT(25, SpawnPort.Lower, Group("蝠", 4, 1.0), Group("蛾", 3, 1.0)),                      // W2 口=下：空潜混编（灯=对空缓速场，蝠光中 ×0.7）
                WaveT(25, SpawnPort.Both, Group("狼", 8, 1.0), Group("蝠", 3, 1.0)),                       // W3 口=双：夜狼大波（灯照路=狼速还原，灯廊价值）
                WaveT(25, SpawnPort.Upper, Group("熊", 3, 1.0), Group("蝠", 5, 1.0), Group("虫", 8, 1.0)), // W4 口=上：厚血客+潜空（光中磨熊）
                WaveT(25, SpawnPort.Both, Group("鼠", 10, 0.9), Group("蛾", 5, 0.9), Group("狼", 5, 0.9)), // W5 口=双：群混（光廊全覆盖检验）
                WaveT(25, SpawnPort.Both, Group("蝠", 6, 1.0), Group("虎", 1, 1.0), Group("狼", 6, 1.0)),  // W6 口=双：L3 收官（潜行+精英+夜狼综合）
            };
        }

        /// <summary>三L4：+法（13 选 5）；6 波；灯+法连携眩晕 + 光-敌交互全家桶；家灯 1.5。</summary>
        private static void SeedChapter3Level4(LevelConfig d)
        {
            d.title = "幽夜 · 蚀月";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.homeLampRadius = 1.5f;
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯", "法" }; // 13 选 5
            d.rewardCards = new List<string> { "法" };

            d.waves = new List<WaveLayout>
            {
                WaveT(25, SpawnPort.Upper, Group("猫", 1, 1.0), Group("鼠", 8, 1.0)),                      // W1 口=上：猫首秀（潜行精英）——翼端预警灯+现形打法
                WaveT(25, SpawnPort.Lower, Group("猫", 2, 1.0), Group("蛾", 6, 1.0)),                      // W2 口=下：法对空首验（灯下法打蛾群眩晕，连携首秀）
                WaveT(25, SpawnPort.Both, Group("牛", 3, 1.0), Group("蝠", 6, 1.0)),                       // W3 口=双：加速客+潜空（法眩晕 vs 牛半血加速）
                WaveT(25, SpawnPort.Upper, Group("猫", 2, 1.0), Group("虎", 1, 1.0), Group("狼", 6, 1.0)), // W4 口=上：双精英（浅影找猫+光中磨虎）
                WaveT(25, SpawnPort.Both, Group("鼠", 12, 0.9), Group("蛾", 8, 0.9), Group("蝠", 4, 0.9)), // W5 口=双：大群混（光交互全家桶：易伤+减速+夜行）
                WaveT(25, SpawnPort.Both, Group("猫", 3, 1.0), Group("牛", 2, 1.0), Group("鹰", 5, 1.0)),  // W6 口=双：L4 收官（潜精英+加速+空快综合）
            };
        }

        /// <summary>三L5：13 选 5；7 波；枭 Boss 毕业考（灯网冗余+诱饵灯+栖窗输出+两圈）；家灯 0.5（全靠自建）。</summary>
        private static void SeedChapter3Level5(LevelConfig d)
        {
            d.title = "幽夜 · 枭鸣";
            d.startLives = 5;
            d.prepSeconds = 20;
            d.homeLampRadius = 0.5f; // 拐杖抽完：全靠自建灯网（06 §11.2 收缩曲线终点）
            d.poolChars = new List<string> { "刀", "矛", "弓", "墨", "铲", "棍", "戟", "弩", "水", "渔", "冰", "灯", "法" };
            d.rewardCards = new List<string>(); // 预告文案「枭至则灯昏」归 F4.4（L5 开场气泡）

            d.waves = new List<WaveLayout>
            {
                WaveT(22, SpawnPort.Upper, Group("野人", 1, 1.0), Group("狼", 8, 1.0)),                    // W1 口=上：野人客首现（700 血扣 2 印，光中磨）
                WaveT(25, SpawnPort.Both, Group("猫", 2, 1.0), Group("熊", 3, 1.0), Group("蝠", 5, 1.0)),  // W2 口=双：潜精英+厚血+潜空（灯网覆盖考）
                WaveT(25, SpawnPort.Lower, Group("牛", 4, 1.0), Group("鹰", 6, 1.0)),                      // W3 口=下：加速+空快（对空+灯缓速双线）
                WaveT(25, SpawnPort.Both, Group("野人", 2, 1.0), Group("虎", 1, 1.0), Group("蛾", 8, 1.0)),// W4 口=双：三精英+蛾群（光伤清蛾=飞蛾扑火收割）
                WaveT(25, SpawnPort.Both, Group("猫", 3, 0.9), Group("鼠", 10, 0.9), Group("狼", 8, 0.9)), // W5 口=双：大混战（灯网冗余预热——枭将至）
                WaveT(25, SpawnPort.Both, Group("野人", 2, 0.9), Group("熊", 4, 0.9), Group("狼", 8, 0.9)),// W6 口=双：大战前夜（清杂+留种塔）
                WaveT(30, SpawnPort.Upper, Group("枭", 1, 1.5)),                                           // W7 口=上：Boss 枭 19500——红闪步行→栖灯吃灯（F5 状态机）
            };
        }

        /// <summary>
        /// 返工补丁：只修正「字段新增后旧资产的缺省值」，玩家已调过的数值一概不动。
        /// v3.0 后旧补丁（弓火枪箭射程成长/兽血量）随旧字删除自然失效，此处保留骨架供后续补丁挂载。
        /// </summary>
        private static void PatchLegacyAssets(TowerPoolDef pool, EnemyTableDef enemies, UIPanelConfig uiPanel)
        {
            bool dirty = false;

            // ADR-022（阶段 C）：旧敌表缺机制字段 → 按 敌人草稿.csv 补「启用值」。
            // 哨兵判定：只补未启用的（空/0）；已启用或玩家自调过的一概不动。
            var spider = enemies.Find("蛛");
            if (spider != null && string.IsNullOrEmpty(spider.onDeathSpawns))
            {
                spider.onDeathSpawns = "虫";
                spider.onDeathSpawnCount = 3;
                dirty = true;
            }
            var ox = enemies.Find("牛");
            if (ox != null && ox.enrageAtRatio <= 0)
            {
                ox.enrageAtRatio = 0.5;
                ox.enrageSpeedMult = 2.0;
                dirty = true;
            }
            var boss = enemies.Find("酋长");
            if (boss != null && string.IsNullOrEmpty(boss.summons))
            {
                boss.summons = "召唤野人";
                boss.summonInterval = 20;
                boss.summonCount = 2;
                boss.summonCap = 6;
                dirty = true;
            }

            // F4.5（ADR-031 续 / ADR-024 模式）：构筑面板 11 选 5 两行化升级。
            // 旧库存（间距 1.9 / 卡 1.3）装 11 卡会挤第三行（row2 y=-2.85）撞 Σ 行与出阵按钮。
            // 哨兵 v2（2026-10-04 实测修正）：旧条件「列数==4」漏判用户调过列数的资产
            // （D2 调 5 列 → 不命中 → 3 行撞出阵按钮）；改为 spacingX==1.9 && cardSize==1.3
            // 纯旧库存指纹判定，不再依赖列数——列数是高频调参位，间距/卡宽才是旧版指纹。
            // 升后 spacingX≠1.9，幂等不重触发。
            if (uiPanel != null &&
                Mathf.Approximately(uiPanel.deckBagSpacingX, 1.9f) &&
                Mathf.Approximately(uiPanel.deckCardSize, 1.3f))
            {
                uiPanel.deckBagColumns = 6;        // 11 卡 = 6+5 两行
                uiPanel.deckBagSpacingX = 1.45f;   // 宽 8.7，卡间 0.3 裕
                uiPanel.deckCardSize = 1.15f;      // 缩卡适配密排
                uiPanel.deckBagRowY = 0.6f;        // 首行顶 1.175 不压背包区标签（底 ≈1.225），行 2 落 -1.1
                // SO 直改必须 SetDirty + SaveAssets（敌表 dirty=false 时旧代码只标脏不落盘——顺手修）
                EditorUtility.SetDirty(uiPanel);
                AssetDatabase.SaveAssets();
                Debug.Log("[Word Wars Setup] 构筑面板升两行缺省（F4.5 v2：间距+卡宽旧指纹判定，含调过列数的资产）");
            }

            if (dirty)
            {
                // SO 直改必须 SetDirty + SaveAssets，否则只在内存里活一会儿就丢
                EditorUtility.SetDirty(enemies);
                AssetDatabase.SaveAssets();
                Debug.Log("[Word Wars Setup] 敌表补 ADR-022 机制字段（数值一概未动）");
            }
        }

        /// <summary>
        /// SO 落盘 + 从磁盘重载。重载失败（脚本引用写坏/被刷新卸载）时返回 null 并红字报错，
        /// 让调用方的判空弹窗拦住流程，绝不静默继续。
        /// </summary>
        private static T SaveAndReload<T>(T def, string path) where T : ScriptableObject
        {
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            var reloaded = AssetDatabase.LoadAssetAtPath<T>(path);
            if (reloaded == null)
                Debug.LogError("[Word Wars Setup] 资产创建后无法加载（脚本引用写坏）：" + path +
                    "\n最常见原因：ScriptableObject 类没放在与类同名的 .cs 文件里（详见 03_技术方案 §2 文件名铁律）。");
            return reloaded;
        }

        /// <summary>自愈：文件在但类型加载失败（m_Script 悬空的坏资产）→ 删除重建。</summary>
        private static void DeleteBrokenAsset(string path)
        {
            Debug.LogWarning("[Word Wars Setup] 发现坏资产（脚本引用悬空，加载失败），删除重建：" + path);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory("Assets/_Project/Scenes");
            Directory.CreateDirectory(FontDir);
            Directory.CreateDirectory(SpriteDir);
            Directory.CreateDirectory(SODir);
            Directory.CreateDirectory(SODir + "/Towers");
            Directory.CreateDirectory(SODir + "/Enemies");
            Directory.CreateDirectory(SODir + "/Words");
            Directory.CreateDirectory(LevelDir);
            Directory.CreateDirectory(ChapterDir);
            Directory.CreateDirectory("Assets/_Project/Scripts/Levels");
            AssetDatabase.Refresh();
        }

        private static TextMeshProUGUI MakeText(Transform parent, TMP_FontAsset font, string text,
            Vector2 anchoredPos, TextAlignmentOptions align, float size, Vector2? anchor = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.5f); // 缺省居中锚；可传角锚钉边
            rt.sizeDelta = new Vector2(700f, 90f);
            rt.anchoredPosition = anchoredPos;
            var t = go.GetComponent<TextMeshProUGUI>();

            // 字体赋值双保险：属性路径负责驱动 TMP 材质接线；序列化直写保证存进场景
            try { t.font = font; }
            catch (System.Exception e) { Debug.LogWarning("[Word Wars Setup] font 属性赋值异常（已用序列化直写兜底）：" + e.Message); }
            var so = new SerializedObject(t);
            so.FindProperty("m_fontAsset").objectReferenceValue = font;
            so.ApplyModifiedProperties();

            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = HexUtil.ToColor("#26231F"); // 浓墨
            return t;
        }
    }
}
