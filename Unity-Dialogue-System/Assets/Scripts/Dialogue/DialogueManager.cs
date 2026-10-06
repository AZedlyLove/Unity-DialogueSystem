using UnityEngine;

namespace PixelDialogue
{
    /// <summary>
    /// 对话系统总控。场景里放一个（推荐挂在 DialogueSystem 空物体上，和气泡 UI 一起）。
    ///
    /// 最小用法（在任意脚本里）：
    ///     DialogueManager.Instance.Play(myDialogueData, playerTransform);
    ///
    /// 或者用快捷方式：
    ///     DialogueManager.PlayOnce(DialogueData.CreateTemp("你好呀"), playerTransform);
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("气泡（世界空间 UI）")]
        [Tooltip("场景里的 DialogueBubbleView，聊天气泡就出现在它上面")]
        public DialogueBubbleView bubble;

        [Tooltip("留空则在运行时按 playerTag 自动找玩家")]
        public Transform player;

        [Tooltip("自动寻找玩家时使用的 Tag")]
        public string playerTag = "Player";

        [Header("键位（同时兼容新旧输入系统）")]
        [Tooltip("继续 / 跳过打字所用的键，例如 E、Space、Return、Mouse0")]
        public string advanceKey = "E";

        [Tooltip("按住这个键可以加速打字（一般和 advanceKey 相同）")]
        public string skipTypingKey = "E";

        [Header("打字机")]
        [Tooltip("按住 skipTypingKey 时的加速倍率")]
        public float holdToSpeedMultiplier = 4f;

        [Header("整体节奏（秒）")]
        [Tooltip("气泡弹出后到第一句话开始打字的等待")]
        public float bubbleLeadIn = 0.06f;

        [Tooltip("没有内容时气泡最短停留时间")]
        public float minBubbleHold = 0.4f;

        [Tooltip("每句话之间的额外间隔")]
        public float defaultLineDelay = 0.08f;

        [Tooltip("标记为自动继续的句子，打完后停留多久")]
        public float defaultAutoAdvanceDelay = 1.2f;

        [Tooltip("最后一句读完后的额外停顿，之后气泡开始消失")]
        public float defaultEndDelay = 0.25f;

        [Tooltip("消失过渡的等待时间，应该略大于 BubbleView 的 fadeOutDuration")]
        public float bubbleFadeOutWait = 0.18f;

        [Header("玩家锁定")]
        [Tooltip("对话中是否锁住玩家")]
        public bool freezePlayerWhileTalking = true;

        [Tooltip("对话中要临时禁用的玩家脚本（比如 PlayerController / PlayerInput）")]
        public Behaviour[] playerBehavioursToDisable;

        public DialogueSession CurrentSession { get; private set; }

        public static bool IsActive => Instance != null && Instance.CurrentSession != null
                                       && Instance.CurrentSession.Current != DialogueSession.State.Finished;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DialogueManager] 场景里存在多个 DialogueManager，保留第一个。", this);
                enabled = false;
                return;
            }

            Instance = this;
            if (bubble == null) bubble = FindObjectOfType<DialogueBubbleView>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit() => PlayerMovementLock.ForceReset();

        /// <summary>找不到玩家时按 Tag 搜一次。</summary>
        public static Transform ResolvePlayer()
        {
            if (Instance == null) return null;
            if (Instance.player != null) return Instance.player;

            GameObject go = GameObject.FindGameObjectWithTag(Instance.playerTag);
            if (go != null) Instance.player = go.transform;
            return Instance.player;
        }

        /// <summary>播一段对话。返回 false 表示当前已有对话在进行。</summary>
        public bool Play(DialogueData data, Transform speakerAnchor, bool freezeTarget = true)
        {
            if (data == null || data.LineCount == 0)
            {
                Debug.LogWarning("[DialogueManager] Play 收到了空的 DialogueData。", this);
                return false;
            }

            if (IsActive)
                return false;

            if (bubble == null)
            {
                Debug.LogError("[DialogueManager] 没有指定 bubble（DialogueBubbleView），无法显示对话。", this);
                return false;
            }

            Transform anchor = speakerAnchor != null ? speakerAnchor : ResolvePlayer();
            if (anchor == null)
            {
                Debug.LogError("[DialogueManager] 找不到对话气泡的挂点，请给 player 赋值或设置 playerTag。", this);
                return false;
            }

            CurrentSession = DialogueSession.Create(this, data, anchor, bubble);
            CurrentSession.SetFreeze(freezeTarget);
            return true;
        }

        /// <summary>简化版：直接用字符串播一句话。</summary>
        public bool Play(string text, Transform speakerAnchor = null)
        {
            return Play(DialogueData.CreateTemp(text), speakerAnchor);
        }

        void Update()
        {
            if (CurrentSession == null) return;

            if (CurrentSession.Current == DialogueSession.State.Finished)
            {
                CurrentSession = null;
            }
        }

        /// <summary>强制结束当前对话（切场景、被攻击打断等）。</summary>
        public void ForceEnd()
        {
            if (CurrentSession != null)
                CurrentSession.Abort();
            CurrentSession = null;
        }
    }
}
