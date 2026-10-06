using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace PixelDialogue.EditorTools
{
    /// <summary>
    /// 生成支持中文的 TMP 字体资源，并把它设为 TMP 的默认字体 / 全局回退字体。
    ///
    /// 菜单：Tools > PixelDialogue > 生成中文字体资源 (TMP)
    ///
    /// 背景：Unity 自带的 LiberationSans SDF 没有任何汉字字形，所以中文会显示成 □。
    /// 本工具会把系统里的中文字体拷进 Assets/Fonts/，用它创建一个
    /// 「动态图集（Dynamic）」字体资源 —— 用到哪个字才生成哪个字的字形，
    /// 所以初始资源很小，也不会出现"字库没收录某个生僻字"的问题。
    /// </summary>
    public static class ChineseFontSetup
    {
        const string FontFolder = "Assets/Fonts";
        const string OutputFolder = "Assets/Fonts/Generated";
        const string OutputName = "ChinesePixelUI SDF";
        const int SamplingPointSize = 64;   // 采样字号，越大越清晰（配合动态图集几乎不占额外空间）
        const int AtlasPadding = 6;         // 内边距，防止笔画被裁切
        const int AtlasSize = 2048;

        // 按优先级排列的候选系统字体（都是 Windows 自带，无版权风险）
        static readonly string[] Candidates =
        {
            @"C:\Windows\Fonts\simhei.ttf",    // 黑体：笔画均匀，最接近像素游戏 UI
            @"C:\Windows\Fonts\msyh.ttc",      // 微软雅黑
            @"C:\Windows\Fonts\Deng.ttf",      // 等线
            @"C:\Windows\Fonts\simkai.ttf",    // 楷体
            @"C:\Windows\Fonts\simsun.ttc",    // 宋体
        };

        // 预生成这些字，避免第一次显示时卡顿（数字、标点、常用字）
        const string PreloadCharacters =
            "0123456789" +
            "abcdefghijklmnopqrstuvwxyz" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "，。！？、；：（）【】《》…—～·「」『』" +
            "你好旅行者我是谁什么在哪里有可以说的话请问";

        [MenuItem("Tools/PixelDialogue/生成中文字体资源 (TMP)", false, 40)]
        public static void Generate()
        {
            // 1) 找一个可用的系统字体
            string sourcePath = null;
            foreach (string candidate in Candidates)
            {
                if (File.Exists(candidate))
                {
                    sourcePath = candidate;
                    break;
                }
            }

            if (sourcePath == null)
            {
                EditorUtility.DisplayDialog("没找到中文字体",
                    "在 C:\\Windows\\Fonts 里没找到 simhei.ttf / msyh.ttc 等字体。\n" +
                    "请手动把一个中文 ttf 放进 Assets/Fonts/，然后用 Window > TextMeshPro > Font Asset Creator 生成。",
                    "知道了");
                return;
            }

            EnsureFolder(FontFolder);
            EnsureFolder(OutputFolder);

            string targetName = Path.GetFileName(sourcePath);
            string targetPath = FontFolder + "/" + targetName;

            // 2) 把系统字体拷进工程（Unity 的 Font 资源必须是工程内的文件）
            if (!File.Exists(targetPath))
            {
                File.Copy(sourcePath, targetPath);
                AssetDatabase.ImportAsset(targetPath);
                Debug.Log($"[PixelDialogue] 已拷贝字体到 {targetPath}");
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(targetPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[PixelDialogue] 无法加载字体资源：{targetPath}");
                return;
            }

            // 3) 生成动态字体资源
            string outputPath = $"{OutputFolder}/{OutputName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);

            if (existing != null)
            {
                bool overwrite = EditorUtility.DisplayDialog("字体资源已存在",
                    $"{outputPath} 已经存在，要重新生成吗？\n" +
                    "（如果只是想把它设为默认字体，点「否」，然后我直接帮你设置）",
                    "重新生成", "跳过生成，只设置");

                if (!overwrite)
                {
                    ApplyAsDefault(existing);
                    return;
                }

                AssetDatabase.DeleteAsset(outputPath);
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError("[PixelDialogue] TMP_FontAsset.CreateFontAsset 返回空，可能字体文件不被支持。");
                return;
            }

            fontAsset.name = OutputName;
            AssetDatabase.CreateAsset(fontAsset, outputPath);

            // 图集与材质也要落盘，否则动态加字之后会丢
            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    var tex = fontAsset.atlasTextures[i];
                    if (tex == null) continue;
                    tex.name = OutputName + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = OutputName + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            // 4) 预生成常用字
            if (!string.IsNullOrEmpty(PreloadCharacters))
            {
                fontAsset.TryAddCharacters(PreloadCharacters, out string missing);
                if (!string.IsNullOrEmpty(missing))
                    Debug.LogWarning($"[PixelDialogue] 字体缺少这些预生成字符：{missing}");
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PixelDialogue] 中文字体资源已生成：{outputPath}（源字体 {targetName}）");

            ApplyAsDefault(fontAsset);
        }

        /// <summary>把生成的字体设为 TMP 默认字体，并加入全局回退列表。</summary>
        static void ApplyAsDefault(TMP_FontAsset fontAsset)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[PixelDialogue] 找不到 TMP Settings，请在 Window > TextMeshPro > Settings 里手动把字体设为默认。");
                return;
            }

            var so = new SerializedObject(settings);

            // 设成默认字体
            var defaultFontProp = so.FindProperty("m_defaultFontAsset");
            if (defaultFontProp != null)
            {
                defaultFontProp.objectReferenceValue = fontAsset;
            }

            // 加进全局回退列表（这样即使某个文字对象用的是别的字体，也能显示中文）
            var fallbackProp = so.FindProperty("m_fallbackFontAssets");
            if (fallbackProp != null)
            {
                bool already = false;
                for (int i = 0; i < fallbackProp.arraySize; i++)
                {
                    if (fallbackProp.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                    {
                        already = true;
                        break;
                    }
                }

                if (!already)
                {
                    fallbackProp.InsertArrayElementAtIndex(fallbackProp.arraySize);
                    fallbackProp.GetArrayElementAtIndex(fallbackProp.arraySize - 1).objectReferenceValue = fontAsset;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"[PixelDialogue] 已把 {fontAsset.name} 设为 TMP 默认字体并加入全局回退。");
            EditorUtility.DisplayDialog("完成",
                $"中文字体资源已生成：\n{AssetDatabase.GetAssetPath(fontAsset)}\n\n" +
                "已同时设置为 TMP 默认字体 + 全局回退字体。\n" +
                "回到场景运行即可看到中文正常显示。\n\n" +
                "如果个别文字对象还是方框，选中它、把 Font Asset 换成这个字体即可。",
                "好");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        [MenuItem("Tools/PixelDialogue/检查对话系统场景配置", false, 41)]
        public static void CheckScene()
        {
            var lines = new System.Text.StringBuilder();

            var manager = Object.FindObjectOfType<DialogueManager>();
            lines.AppendLine(manager == null
                ? "✗ 场景里没有 DialogueManager（用 Tools > PixelDialogue > 创建对话系统）"
                : $"✓ DialogueManager: {manager.gameObject.name}");

            if (manager != null)
            {
                lines.AppendLine(manager.bubble == null
                    ? "✗ DialogueManager.bubble 没有赋值"
                    : $"✓ Bubble: {manager.bubble.gameObject.name}");

                if (manager.bubble != null && manager.bubble.bodyText == null)
                    lines.AppendLine("✗ Bubble.bodyText 没有赋值（必须是 TextMeshProUGUI）");

                lines.AppendLine(manager.player == null
                    ? "· player 未赋值，运行时会按 Tag 查找"
                    : $"✓ Player: {manager.player.name}");
            }

            var bubble = Object.FindObjectOfType<InteractionBubble>();
            lines.AppendLine(bubble == null
                ? "✗ 场景里没有 InteractionBubble（NPC 头顶的按键提示不会出现）"
                : $"✓ InteractionBubble: {bubble.gameObject.name}");

            var interactor = Object.FindObjectOfType<DialogueInteractor>();
            lines.AppendLine(interactor == null
                ? "✗ 场景里没有 DialogueInteractor（玩家身上缺少互动脚本）"
                : $"✓ DialogueInteractor 在 {interactor.gameObject.name} 上");

            int triggers = Object.FindObjectsOfType<DialogueTrigger>().Length;
            lines.AppendLine($"· DialogueTrigger 数量：{triggers}");

            Debug.Log("[PixelDialogue] 场景配置检查：\n" + lines);
            EditorUtility.DisplayDialog("场景配置检查", lines.ToString(), "好");
        }
    }
}
