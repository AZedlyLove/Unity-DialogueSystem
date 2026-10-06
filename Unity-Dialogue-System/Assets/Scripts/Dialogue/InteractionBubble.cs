using UnityEngine;

namespace PixelDialogue
{
    /// <summary>
    /// NPC 头顶的"按 E 互动"提示泡泡。世界空间 UI，平滑淡入淡出 + 轻微浮动。
    /// 场景里只需要一个，由 DialogueInteractor 调用；不需要挂在每个 NPC 身上。
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionBubble : MonoBehaviour
    {
        public static InteractionBubble Instance { get; private set; }

        [Header("引用")]
        [Tooltip("整体缩放作用的根节点，留空则用本物体")]
        public Transform root;

        [Tooltip("CanvasGroup，留空自动获取或添加")]
        public CanvasGroup canvasGroup;

        [Tooltip("显示按键名的文字，例如 \"E\"。可留空（纯图标）")]
        public TMPro.TMP_Text keyLabel;

        [Tooltip("按键名默认显示的内容，例如 E / Space")]
        public string keyLabelText = "E";

        [Header("位置")]
        [Tooltip("跟随平滑时间，越小越跟手")]
        public float followSmoothTime = 0.06f;

        [Header("出现 / 消失")]
        public float fadeInDuration = 0.16f;
        public float fadeOutDuration = 0.12f;

        [Tooltip("出现时的起始缩放")]
        public float popFromScale = 0.6f;

        [Tooltip("出现时的过冲缩放，1 表示不过冲")]
        public float popOvershoot = 1.12f;

        [Header("待机动画")]
        [Tooltip("上下浮动幅度（世界单位）")]
        public float floatAmplitude = 0.05f;

        [Tooltip("上下浮动速度")]
        public float floatSpeed = 3f;

        [Tooltip("呼吸缩放幅度，0 表示关闭")]
        public float pulseAmplitude = 0.04f;

        [Tooltip("呼吸速度")]
        public float pulseSpeed = 5f;

        enum Phase { Hidden, FadeIn, Visible, FadeOut }

        Phase _phase = Phase.Hidden;
        float _phaseTimer;
        Vector3 _targetPosition;
        Vector2 _velocity;
        Vector3 _baseScale = Vector3.one;
        float _animTime;
        bool _duplicate;

        public bool IsVisible => _phase != Phase.Hidden;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InteractionBubble] 场景里存在多个提示泡泡，保留第一个。", this);
                _duplicate = true;
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (root == null) root = transform;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _baseScale = root.localScale;

            if (keyLabel != null)
                keyLabel.text = keyLabelText;

            gameObject.SetActive(true);
            ApplyHidden();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void ApplyHidden()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            root.localScale = _baseScale * popFromScale;
        }

        /// <summary>把提示移到某个世界坐标并弹出。</summary>
        public void ShowAt(Vector3 worldPosition)
        {
            _targetPosition = worldPosition;

            if (_phase == Phase.Hidden || _phase == Phase.FadeOut)
            {
                _phase = Phase.FadeIn;
                _phaseTimer = 0f;
                _animTime = 0f;
                transform.position = worldPosition;
                _velocity = Vector2.zero;
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>提示换个位置（切换最近 NPC 时调用）。</summary>
        public void MoveTo(Vector3 worldPosition)
        {
            _targetPosition = worldPosition;
        }

        public void Hide()
        {
            if (_phase == Phase.Hidden || _phase == Phase.FadeOut) return;
            _phase = Phase.FadeOut;
            _phaseTimer = 0f;
        }

        /// <summary>立刻隐匿（切换场景时用）。</summary>
        public void HideImmediate()
        {
            _phase = Phase.Hidden;
            ApplyHidden();
        }

        void Update()
        {
            if (_duplicate) return;

            float dt = Time.unscaledDeltaTime;

            switch (_phase)
            {
                case Phase.FadeIn:
                    _phaseTimer += dt;
                    {
                        float p = Mathf.Clamp01(_phaseTimer / Mathf.Max(0.0001f, fadeInDuration));
                        float eased = EaseOutBack(p, popOvershoot);
                        canvasGroup.alpha = Mathf.Clamp01(p * 1.4f);
                        root.localScale = _baseScale * Mathf.LerpUnclamped(popFromScale, 1f, eased);
                        if (p >= 1f)
                        {
                            _phase = Phase.Visible;
                            canvasGroup.alpha = 1f;
                            root.localScale = _baseScale;
                        }
                    }
                    break;

                case Phase.Visible:
                    canvasGroup.alpha = 1f;
                    _animTime += dt;
                    {
                        float bob = Mathf.Sin(_animTime * floatSpeed) * floatAmplitude;
                        float pulse = pulseAmplitude > 0f
                            ? 1f + Mathf.Sin(_animTime * pulseSpeed) * pulseAmplitude
                            : 1f;
                        root.localScale = _baseScale * pulse;
                        transform.position = SmoothTo(_targetPosition + new Vector3(0f, bob, 0f));
                    }
                    return; // 位置已在上面处理

                case Phase.FadeOut:
                    _phaseTimer += dt;
                    {
                        float p = Mathf.Clamp01(_phaseTimer / Mathf.Max(0.0001f, fadeOutDuration));
                        canvasGroup.alpha = 1f - EaseInCubic(p);
                        root.localScale = _baseScale * Mathf.Lerp(1f, popFromScale * 0.9f, EaseInCubic(p));
                        if (p >= 1f)
                        {
                            _phase = Phase.Hidden;
                            ApplyHidden();
                        }
                    }
                    break;

                case Phase.Hidden:
                    return;
            }

            // 淡入 / 淡出阶段也要平滑跟随，避免切换 NPC 时瞬移
            transform.position = SmoothTo(_targetPosition);
        }

        Vector3 SmoothTo(Vector3 desired)
        {
            float smooth = Mathf.Max(0.0001f, followSmoothTime);
            return Vector2.SmoothDamp(transform.position, desired, ref _velocity, smooth, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        static float EaseInCubic(float p) => p * p * p;

        static float EaseOutBack(float p, float overshoot)
        {
            float s = Mathf.Max(0f, overshoot - 1f) * 3.2f;
            float c3 = s + 1f;
            float q = p - 1f;
            return 1f + c3 * q * q * q + s * q * q;
        }
    }
}
