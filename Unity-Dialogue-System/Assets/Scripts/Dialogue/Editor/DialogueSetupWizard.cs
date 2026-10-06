using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelDialogue;

namespace PixelDialogue.EditorTools
{
    /// <summary>
    /// 一键在场景里搭好对话系统所需的全部对象（世界空间气泡 UI + 提示泡泡 + 管理器）。
    /// 菜单：Tools > PixelDialogue > 创建对话系统
    /// </summary>
    public static class DialogueSetupWizard
    {
        const string RootName = "DialogueSystem";
        const string MenuPath = "Tools/PixelDialogue/创建对话系统";

        [MenuItem(MenuPath, false, 0)]
        public static void CreateDialogueSystem()
        {
            if (Object.FindObjectOfType<DialogueManager>() != null)
            {
                if (!EditorUtility.DisplayDialog("对话系统已存在",
                        "场景里已经有 DialogueManager 了。要再建一套吗？", "再建一套", "取消"))
                    return;
            }

            var root = new GameObject(RootName);

            // ---------------- 管理器 ----------------
            var manager = root.AddComponent<DialogueManager>();

            // ---------------- 气泡（世界空间 Canvas） ----------------
            DialogueBubbleView bubble = BuildBubble(root.transform, out Transform bubbleWorldRoot);

            // ---------------- 互动提示泡泡 ----------------
            InteractionBubble prompt = BuildPrompt(root.transform);

            // ---------------- 连线 ----------------
            manager.bubble = bubble;

            Undo.RegisterCreatedObjectUndo(root, "Create Dialogue System");
            Selection.activeGameObject = root;

            Debug.Log("[PixelDialogue] 对话系统已创建。\n" +
                      "接下来：\n" +
                      "  1) 给玩家加 DialogueInteractor 组件；\n" +
                      "  2) 给 NPC 加 DialogueTrigger 组件，并在对话数据里指定 DialogueData；\n" +
                      "  3) 把玩家的移动脚本拖到 DialogueManager.playerBehavioursToDisable 上（可选）。\n" +
                      "详细说明见 Assets/Scripts/Dialogue/README_对话系统使用说明.md", root);
        }

        [MenuItem("Tools/PixelDialogue/给选中对象添加 DialogueTrigger", false, 20)]
        public static void AddTriggerToSelection()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("没有选中对象", "请先在 Hierarchy 里选中一个或多个 NPC。", "好");
                return;
            }

            foreach (var go in selected)
            {
                if (go.GetComponent<DialogueTrigger>() == null)
                {
                    Undo.AddComponent<DialogueTrigger>(go);
                    Debug.Log($"[PixelDialogue] 已给 {go.name} 添加 DialogueTrigger。", go);
                }
            }
        }

        [MenuItem("Tools/PixelDialogue/给选中对象添加 DialogueInteractor", false, 21)]
        public static void AddInteractorToSelection()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("没有选中对象", "请先在 Hierarchy 里选中玩家对象。", "好");
                return;
            }

            foreach (var go in selected)
            {
                if (go.GetComponent<DialogueInteractor>() == null)
                {
                    Undo.AddComponent<DialogueInteractor>(go);
                    Debug.Log($"[PixelDialogue] 已给 {go.name} 添加 DialogueInteractor。", go);
                }
            }
        }

        // -----------------------------------------------------------------
        //  气泡
        // -----------------------------------------------------------------

        static DialogueBubbleView BuildBubble(Transform parent, out Transform worldRoot)
        {
            // 世界空间 Canvas（缩放 0.01 让 100 像素 = 1 世界单位，适合像素游戏）
            var canvasGo = new GameObject("BubbleCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 1000f);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            // 跟随节点：气泡视图挂在这里，位置 = 玩家位置 + 偏移
            var followGo = new GameObject("Bubble_Follow", typeof(RectTransform));
            followGo.transform.SetParent(canvasGo.transform, false);
            var followRect = followGo.GetComponent<RectTransform>();
            followRect.sizeDelta = new Vector2(10f, 10f);
            worldRoot = followGo.transform;

            // 气泡根（承载缩放动画）
            var rootGo = new GameObject("Bubble_Root", typeof(RectTransform), typeof(CanvasGroup));
            rootGo.transform.SetParent(followGo.transform, false);
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(480f, 10f);

            // 底板
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(rootGo.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(480f, 0f);

            var panelImage = panelGo.GetComponent<Image>();
            panelImage.sprite = FindSprite("UISprite");
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.06f, 0.07f, 0.12f, 0.94f);
            panelImage.raycastTarget = false;

            var layout = panelGo.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 18, 18);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 头像（默认隐藏）
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            portraitGo.transform.SetParent(panelGo.transform, false);
            var portraitImg = portraitGo.GetComponent<Image>();
            portraitImg.raycastTarget = false;
            portraitImg.enabled = false;
            var portraitLayout = portraitGo.GetComponent<LayoutElement>();
            portraitLayout.preferredWidth = 96f;
            portraitLayout.preferredHeight = 96f;
            portraitGo.SetActive(false);

            // 正文
            var textGo = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textGo.transform.SetParent(panelGo.transform, false);
            var body = textGo.GetComponent<TextMeshProUGUI>();
            body.text = "在这里输入对话内容……";
            body.fontSize = 34f;
            body.color = new Color(0.96f, 0.96f, 1f, 1f);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Overflow;
            body.raycastTarget = false;
            var bodyLayout = textGo.GetComponent<LayoutElement>();
            bodyLayout.preferredWidth = 400f;
            bodyLayout.flexibleWidth = 0f;

            // 名字行（气泡上方）
            var nameRowGo = new GameObject("NameRow", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            nameRowGo.transform.SetParent(rootGo.transform, false);
            var nameRowRect = nameRowGo.GetComponent<RectTransform>();
            nameRowRect.anchorMin = new Vector2(0.5f, 0.5f);
            nameRowRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRowRect.pivot = new Vector2(0.5f, 0.5f);
            nameRowRect.anchoredPosition = new Vector2(0f, 44f);
            nameRowRect.sizeDelta = new Vector2(180f, 0f);

            var nameBg = nameRowGo.GetComponent<Image>();
            nameBg.sprite = FindSprite("UISprite");
            nameBg.type = Image.Type.Sliced;
            nameBg.color = new Color(0.16f, 0.20f, 0.42f, 0.96f);
            nameBg.raycastTarget = false;

            var nameLayout = nameRowGo.GetComponent<HorizontalLayoutGroup>();
            nameLayout.padding = new RectOffset(18, 18, 6, 6);
            nameLayout.childAlignment = TextAnchor.MiddleCenter;
            nameLayout.childControlWidth = true;
            nameLayout.childControlHeight = true;
            nameLayout.childForceExpandWidth = false;

            var nameFitter = nameRowGo.GetComponent<ContentSizeFitter>();
            nameFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            nameFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var nameTextGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            nameTextGo.transform.SetParent(nameRowGo.transform, false);
            var nameText = nameTextGo.GetComponent<TextMeshProUGUI>();
            nameText.text = "名字";
            nameText.fontSize = 26f;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            nameText.raycastTarget = false;
            nameTextGo.GetComponent<LayoutElement>().preferredWidth = 60f;

            // 继续指示箭头（用文字 ▼ 代替图片，免素材）
            var indicatorGo = new GameObject("ContinueIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
            indicatorGo.transform.SetParent(rootGo.transform, false);
            var indicatorRect = indicatorGo.GetComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0.5f, 0f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.anchoredPosition = new Vector2(200f, -4f);
            indicatorRect.sizeDelta = new Vector2(40f, 40f);

            var indicatorText = indicatorGo.GetComponent<TextMeshProUGUI>();
            indicatorText.text = "▼";
            indicatorText.fontSize = 26f;
            indicatorText.color = new Color(1f, 0.85f, 0.35f, 1f);
            indicatorText.alignment = TextAlignmentOptions.Center;
            indicatorText.raycastTarget = false;

            // 视图组件挂在跟随节点上
            var view = followGo.AddComponent<DialogueBubbleView>();
            view.bubbleRoot = rootGo.transform;
            view.canvasGroup = rootGo.GetComponent<CanvasGroup>();
            view.bodyText = body;
            view.nameText = nameText;
            view.nameRow = nameRowGo;
            view.portraitImage = portraitImg;
            view.portraitRoot = portraitGo;
            view.continueIndicator = indicatorGo;
            view.anchor = null;

            return view;
        }

        static InteractionBubble BuildPrompt(Transform parent)
        {
            var canvasGo = new GameObject("PromptCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10; // 提示画在气泡之上

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200f, 200f);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var bubbleGo = new GameObject("InteractionBubble", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            bubbleGo.transform.SetParent(canvasGo.transform, false);
            var rect = bubbleGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(76f, 76f);

            var img = bubbleGo.GetComponent<Image>();
            img.sprite = FindSprite("Knob");
            img.color = new Color(1f, 1f, 1f, 0.96f);
            img.raycastTarget = false;

            var labelGo = new GameObject("KeyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(bubbleGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "E";
            label.fontSize = 40f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.12f, 0.14f, 0.2f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            var prompt = bubbleGo.AddComponent<InteractionBubble>();
            prompt.keyLabel = label;
            prompt.keyLabelText = "E";

            return prompt;
        }

        static Sprite FindSprite(string name)
        {
            // Unity 自带 UI 精灵；不同版本名字略有差异，找不到就返回 null（用纯色也行）
            string[] guids = AssetDatabase.FindAssets(name + " t:Sprite");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("UI/Skin") || path.Contains("Builtin"))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null) return sprite;
                }
            }

            // 退路：直接用内置资源名加载
            return Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        }
    }
}
