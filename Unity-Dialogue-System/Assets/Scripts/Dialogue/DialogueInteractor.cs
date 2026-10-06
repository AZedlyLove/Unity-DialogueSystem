using UnityEngine;

namespace PixelDialogue
{
    /// <summary>
    /// 挂在玩家身上。负责：
    ///   1. 找出最近的、范围内的 NPC；
    ///   2. 在它头顶显示/隐藏"按 E 互动"提示（带平滑淡入淡出）；
    ///   3. 玩家按 E 时开始对话。
    ///
    /// 需求里的"靠近 NPC 时在其上方出现按键"就靠这个脚本 + InteractionBubble 完成。
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueInteractor : MonoBehaviour
    {
        [Header("检测")]
        [Tooltip("额外的检测半径补偿；最终范围 = NPC 的 interactRadius + 这里的值")]
        public float radiusPadding = 0.1f;

        [Tooltip("多久重新搜索一次最近 NPC（秒）。0 = 每帧搜索")]
        public float searchInterval = 0.08f;

        [Header("按键")]
        [Tooltip("互动键，一般与 DialogueManager.advanceKey 相同")]
        public string interactKey = "E";

        [Header("提示")]
        [Tooltip("提示泡泡的挂点，留空自动用玩家自己的 transform")]
        public Transform promptAnchorOverride;

        [Tooltip("提示在 NPC 头顶再往上的世界偏移，避免和 NPC 的图像重叠")]
        public Vector3 promptExtraOffset = new Vector3(0f, 0f, 0f);

        [Header("其他")]
        [Tooltip("对话进行中自动禁止再次触发互动")]
        public bool blockWhileTalking = true;

        /// <summary>当前提示指向的 NPC。</summary>
        public DialogueTrigger Current { get; private set; }

        float _nextSearch;
        InteractionBubble _promptBubble;

        void Awake()
        {
            _promptBubble = InteractionBubble.Instance;
        }

        void Update()
        {
            if (DialogueManager.IsActive && blockWhileTalking)
            {
                SetCurrent(null);
                return;
            }

            if (searchInterval <= 0f || Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + searchInterval;
                SetCurrent(FindNearest());
            }

            if (Current == null) return;

            if (DialogueInput.GetKeyDown(interactKey))
            {
                DialogueTrigger target = Current;
                SetCurrent(null); // 先收起提示，避免气泡和提示同时存在
                if (!target.TryStartDialogue(transform))
                    Current = target; // 没能开始（例如正在对话），把提示放回去
            }
        }

        DialogueTrigger FindNearest()
        {
            DialogueTrigger best = null;
            float bestSqr = float.MaxValue;
            Vector3 self = transform.position;

            var all = DialogueTrigger.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                DialogueTrigger t = all[i];
                if (t == null || !t.isActiveAndEnabled) continue;
                if (!t.IsPlayerInRange(transform, radiusPadding)) continue;

                float sqr = (t.transform.position - self).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = t;
                }
            }

            return best;
        }

        void SetCurrent(DialogueTrigger trigger)
        {
            if (Current == trigger) return;
            Current = trigger;

            if (_promptBubble == null) _promptBubble = InteractionBubble.Instance;
            if (_promptBubble == null) return;

            if (trigger != null)
                _promptBubble.ShowAt(trigger.PromptWorldPosition + promptExtraOffset);
            else
                _promptBubble.Hide();
        }

        void OnDisable()
        {
            SetCurrent(null);
        }
    }
}
