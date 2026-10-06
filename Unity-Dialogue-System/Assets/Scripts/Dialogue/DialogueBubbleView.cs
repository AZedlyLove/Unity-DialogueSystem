using UnityEngine;

namespace PixelDialogue
{
    /// <summary>
    /// 一个世界空间的"头顶聊天气泡"视图。
    /// 负责：平滑出现 / 平滑消失 / 打字机文字 / 跟随目标 / 继续指示箭头闪烁。
    /// 逻辑由 DialogueSession / DialogueBubbleView 驱动，本类只管表现。
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueBubbleView : MonoBehaviour
    {
        public enum Phase { Hidden, FadeIn, Idle, FadeOut }

        [Header("引用")]
        [Tooltip("整个气泡的根节点，缩放动画作用在它上面（一般就是本物体）")]
        public Transform bubbleRoot;

        [Tooltip("用于整体淡入淡出的 CanvasGroup")]
        public CanvasGroup canvasGroup;

        [Tooltip("文字组件。用 TextMeshProUGUI（推荐，配合 maxVisibleCharacters 打字机）")]
        public TMPro.TMP_Text bodyText;

        [Tooltip("名字文字，可留空")]
        public TMPro.TMP_Text nameText;

        [Tooltip("名字行节点，留空则不会显示/隐藏名字")]
        public GameObject nameRow;

        [Tooltip("头像，可留空")]
        public UnityEngine.UI.Image portraitImage;

        [Tooltip("头像节点，留空则忽略")]
        public GameObject portraitRoot;

        [Tooltip("打字完成后闪动的\"继续\"箭头，可留空")]
        public GameObject continueIndicator;

        [Tooltip("气泡的 RectTransform 挂点，气泡会出现在这个位置上方")]
        public Transform anchor;

        [Header("跟随")]
        [Tooltip("气泡是否平滑跟随目标（关闭则瞬间吸附）")]
        public bool smoothFollow = true;

        [Tooltip("跟随平滑时间，越小越跟手；像素游戏建议 0.03~0.08")]
        public float followSmoothTime = 0.05f;

        [Tooltip("气泡相对目标的世界坐标偏移")]
        public Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);

        [Tooltip("跟随在 LateUpdate 中执行（推荐开启，避免抖动）")]
        public bool followInLateUpdate = true;

        [Header("出现 / 消失过渡")]
        [Tooltip("淡入时长（秒，不受 Time.timeScale 影响）")]
        public float fadeInDuration = 0.18f;

        [Tooltip("淡出时长（秒）")]
        public float fadeOutDuration = 0.14f;

        [Tooltip("出现时的起始缩放（Q 弹效果的起点）")]
        public float popFromScale = 0.72f;

        [Tooltip("出现时的最大过冲缩放，1 表示不过冲")]
        public float popOvershoot = 1.06f;

        [Tooltip("出现时从目标下方升起的世界距离")]
        public float riseDistance = 0.22f;

        [Header("打字机")]
        [Tooltip("每秒显示多少个字符（会被打字速度倍率再乘一次）")]
        public float charactersPerSecond = 34f;

        [Header("继续指示")]
        [Tooltip("箭头闪烁频率")]
        public float indicatorBlinkSpeed = 3.2f;

        [Tooltip("箭头上下浮动幅度（像素/世界单位）")]
        public float indicatorBobAmount = 3f;

        // ---- 运行时状态 ----
        Phase _phase = Phase.Hidden;
        float _phaseTimer;
        CanvasGroup _group;
        RectTransform _rootRect;
        Vector2 _followVelocity;
        Vector3 _visualOffset;
        Vector3 _rootBaseScale = Vector3.one;

        int _totalVisibleChars;
        float _shownChars;
        float _speedMultiplier = 1f;
        bool _typing;
        Vector3 _indicatorBaseLocalPos;
        float _indicatorTimer;

        public bool IsVisible => _phase != Phase.Hidden;
        public bool IsTyping => _typing;
        public bool IsTextComplete => !_typing;

        void Awake()
        {
            if (bubbleRoot == null) bubbleRoot = transform;
            _group = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _rootRect = bubbleRoot as RectTransform;

            if (bubbleRoot != transform)
                _rootBaseScale = bubbleRoot.localScale;
            else
                _rootBaseScale = transform.localScale;

            if (continueIndicator != null)
            {
                _indicatorBaseLocalPos = continueIndicator.transform.localPosition;
                continueIndicator.SetActive(false);
            }

            ApplyHiddenState();
        }

        void ApplyHiddenState()
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            bubbleRoot.localScale = _rootBaseScale * popFromScale;
        }

        // ---------------------------------------------------------------
        //  对外接口
        // ---------------------------------------------------------------

        /// <summary>朝某个世界坐标弹出气泡，并开始打字。</summary>
        public void Show(string speakerName, Sprite portrait, string content, Transform followTarget, float typeSpeedMultiplier = 1f)
        {
            SetAnchor(followTarget);
            if (_rootRect != null && followTarget != null)
                _rootRect.position = followTarget.position + worldOffset;

            SetContent(speakerName, portrait, content);
            SetSpeedMultiplier(typeSpeedMultiplier);

            if (_phase == Phase.Hidden || _phase == Phase.FadeOut)
            {
                _phase = Phase.FadeIn;
                _phaseTimer = 0f;
                _visualOffset = new Vector3(0f, -riseDistance, 0f);
                bubbleRoot.localScale = _rootBaseScale * popFromScale;
            }

            _group.blocksRaycasts = false;
            _group.interactable = false;
            if (_rootRect != null && followTarget != null)
                _followVelocity = Vector2.zero;
        }

        /// <summary>同一个气泡里切换到下一句（不复现出现动画，保持连续感）。</summary>
        public void SetContent(string speakerName, Sprite portrait, string content)
        {
            string safe = content ?? string.Empty;

            if (nameText != null)
            {
                nameText.text = speakerName ?? string.Empty;
                nameText.gameObject.SetActive(true);
            }
            if (nameRow != null)
                nameRow.SetActive(!string.IsNullOrEmpty(speakerName));

            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.enabled = portrait != null;
            }
            if (portraitRoot != null)
                portraitRoot.SetActive(portrait != null);

            if (bodyText != null)
            {
                bodyText.text = safe;
                bodyText.ForceMeshUpdate(); // 必须先刷新，否则 visibleCharacterCount 是上一句的数字
                _totalVisibleChars = bodyText.textInfo != null ? bodyText.textInfo.characterCount : safe.Length;
                bodyText.maxVisibleCharacters = 0;
            }
            else
            {
                _totalVisibleChars = safe.Length;
                _typing = false; // 没有 TMP_Text 组件时不打字，避免卡住
            }

            _shownChars = 0f;
            _typing = _totalVisibleChars > 0;
            _speedMultiplier = 1f;

            if (_typing)
                ApplyVisibleChars(0);
            else if (bodyText != null)
                bodyText.maxVisibleCharacters = int.MaxValue;

            if (continueIndicator != null) continueIndicator.SetActive(false);
        }

        /// <summary>把当前句立刻显示完整。</summary>
        public void CompleteTyping()
        {
            if (bodyText == null) { _typing = false; return; }

            _shownChars = _totalVisibleChars;
            bodyText.maxVisibleCharacters = int.MaxValue;
            _typing = false;
            if (continueIndicator != null) continueIndicator.SetActive(true);
        }

        /// <summary>播放消失过渡。</summary>
        public void Hide(bool instant = false)
        {
            if (continueIndicator != null) continueIndicator.SetActive(false);

            if (instant)
            {
                _phase = Phase.Hidden;
                ApplyHiddenState();
                return;
            }

            if (_phase == Phase.Hidden) return;
            _phase = Phase.FadeOut;
            _phaseTimer = 0f;
        }

        public void SetAnchor(Transform target)
        {
            anchor = target;
            enabled = true;
        }

        /// <summary>打字速度倍率（打字机速度 / 按住加速等）。</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0.05f, multiplier);
        }

        // ---------------------------------------------------------------
        //  每帧表现
        // ---------------------------------------------------------------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            switch (_phase)
            {
                case Phase.FadeIn:
                    _phaseTimer += dt;
                    {
                        float p = Mathf.Clamp01(_phaseTimer / Mathf.Max(0.0001f, fadeInDuration));
                        float eased = EaseOutBack(p, popOvershoot);
                        _group.alpha = Mathf.Clamp01(p * 1.35f);
                        bubbleRoot.localScale = _rootBaseScale * Mathf.LerpUnclamped(popFromScale, 1f, eased);
                        _visualOffset = new Vector3(0f, Mathf.Lerp(-riseDistance, 0f, EaseOutCubic(p)), 0f);
                        if (p >= 1f)
                        {
                            _phase = Phase.Idle;
                            _visualOffset = Vector3.zero;
                            bubbleRoot.localScale = _rootBaseScale;
                            _group.alpha = 1f;
                        }
                    }
                    break;

                case Phase.Idle:
                    _group.alpha = 1f;
                    break;

                case Phase.FadeOut:
                    _phaseTimer += dt;
                    {
                        float p = Mathf.Clamp01(_phaseTimer / Mathf.Max(0.0001f, fadeOutDuration));
                        float eased = EaseInCubic(p);
                        _group.alpha = 1f - eased;
                        bubbleRoot.localScale = _rootBaseScale * Mathf.Lerp(1f, 0.88f, eased);
                        _visualOffset = new Vector3(0f, Mathf.Lerp(0f, -riseDistance * 0.5f, eased), 0f);
                        if (p >= 1f)
                        {
                            _phase = Phase.Hidden;
                            ApplyHiddenState();
                        }
                    }
                    break;
            }

            if (_phase != Phase.Hidden)
                UpdateTyping(dt);

            UpdateIndicator(dt);
        }

        void UpdateTyping(float dt)
        {
            if (!_typing) return;

            _shownChars += charactersPerSecond * _speedMultiplier * dt;
            int shown = Mathf.FloorToInt(_shownChars);

            if (shown >= _totalVisibleChars)
            {
                CompleteTyping();
                return;
            }

            ApplyVisibleChars(shown);
        }

        void ApplyVisibleChars(int count)
        {
            if (bodyText != null)
                bodyText.maxVisibleCharacters = Mathf.Max(0, count);
        }

        void UpdateIndicator(float dt)
        {
            if (continueIndicator == null) return;

            bool shouldShow = _phase == Phase.Idle && !_typing;
            if (continueIndicator.activeSelf != shouldShow)
                continueIndicator.SetActive(shouldShow);
            if (!shouldShow) return;

            _indicatorTimer += dt * indicatorBlinkSpeed;
            float bob = Mathf.Sin(_indicatorTimer) * indicatorBobAmount;
            continueIndicator.transform.localPosition = _indicatorBaseLocalPos + new Vector3(0f, bob, 0f);
        }

        void LateUpdate()
        {
            if (!followInLateUpdate) return;
            FollowStep();
        }

        void FollowStep()
        {
            if (anchor == null || _rootRect == null) return;

            Vector3 desired = anchor.position + worldOffset + _visualOffset;

            if (!smoothFollow)
            {
                _rootRect.position = desired;
                _followVelocity = Vector2.zero;
                return;
            }

            float smooth = Mathf.Max(0.0001f, followSmoothTime);
            _rootRect.position = Vector2.SmoothDamp(
                _rootRect.position, desired, ref _followVelocity, smooth, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        static float EaseOutCubic(float p) => 1f - Mathf.Pow(1f - p, 3f);
        static float EaseInCubic(float p) => p * p * p;

        /// <summary>轻微过冲的弹出缓动，p=0 → 0，p=1 → 1，中间最多到 overshoot。</summary>
        static float EaseOutBack(float p, float overshoot)
        {
            float s = Mathf.Max(0f, overshoot - 1f) * 3.2f;
            float c3 = s + 1f;
            float q = p - 1f;
            return 1f + c3 * q * q * q + s * q * q;
        }
    }
}
