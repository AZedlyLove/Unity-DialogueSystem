using System.Collections.Generic;
using UnityEngine;

namespace PixelDialogue
{
    /// <summary>
    /// 挂在 NPC 身上。负责：
    ///   1. 登记自己，让玩家的 DialogueInteractor 能找到它；
    ///   2. 提供"按键提示"锚点（提示图标出现在 NPC 头顶）；
    ///   3. 被玩家按键触发时，把对话交给 DialogueManager。
    ///
    /// 触发方式有两种，任选：
    ///   A. 距离检测：在玩家身上放 DialogueInteractor，NPC 只需本脚本 + 对话数据。
    ///   B. 触发器检测：给 NPC 加一个 CircleCollider2D(Is Trigger)，
    ///      并勾上下面的 useTriggerEvents，玩家需要有 Rigidbody2D + Collider2D。
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueTrigger : MonoBehaviour
    {
        public static readonly List<DialogueTrigger> All = new List<DialogueTrigger>();

        [Header("对话内容")]
        [Tooltip("要播放的对话数据；留空则使用下面的内联文本")]
        public DialogueData dialogue;

        [TextArea(2, 6)]
        [Tooltip("没有指定 DialogueData 时使用这里的临时文本，便于快速测试")]
        public string quickTestText = "你好，旅行者！";

        [Header("提示")]
        [Tooltip("按键提示的挂点。留空会自动用本物体，并按 promptOffset 偏移到头顶")]
        public Transform promptAnchor;

        [Tooltip("提示相对挂点的世界坐标偏移，默认往上抬一点到头顶")]
        public Vector3 promptOffset = new Vector3(0f, 0.9f, 0f);

        [Tooltip("这个 NPC 与玩家的最大互动距离（仅距离检测模式使用）")]
        public float interactRadius = 1.4f;

        [Header("可选：触发器模式")]
        public bool useTriggerEvents;

        [Header("行为")]
        [Tooltip("对话结束后是否只能触发一次")]
        public bool oneShot;

        [Tooltip("对话结束后是否触发事件（用于接任务、给道具等）")]
        public bool invokeEventAfterDialogue = true;

        [Tooltip("对话进行中是否把玩家定住")]
        public bool freezePlayerWhileTalking = true;

        public bool HasPlayed { get; private set; }

        /// <summary>对话结束的广播，外部可订阅做任务推进等逻辑。</summary>
        public event System.Action<DialogueTrigger> DialogueFinished;

        public Vector3 PromptWorldPosition =>
            (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset;

        /// <summary>玩家是否处于可互动范围内。</summary>
        public bool IsPlayerInRange(Transform player, float extraRadius = 0f)
        {
            if (player == null) return false;
            float r = interactRadius + extraRadius;
            return (player.position - transform.position).sqrMagnitude <= r * r;
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        /// <summary>由 DialogueInteractor 调用，开始这段对话。</summary>
        public bool TryStartDialogue(Transform player)
        {
            if (oneShot && HasPlayed) return false;
            if (DialogueManager.IsActive) return false;

            Transform target = player != null ? player : DialogueManager.ResolvePlayer();
            if (target == null) return false;

            DialogueData data = ResolveData();
            if (data == null) return false;

            HasPlayed = true;

            bool ok = DialogueManager.Instance.Play(data, target, freezeTarget: freezePlayerWhileTalking);
            if (ok)
                DialogueManager.Instance.CurrentSession.Finished += OnSessionFinished;

            return ok;
        }

        void OnSessionFinished(DialogueSession session)
        {
            session.Finished -= OnSessionFinished;

            if (invokeEventAfterDialogue)
                DialogueFinished?.Invoke(this);
        }

        DialogueData ResolveData()
        {
            if (dialogue != null && dialogue.LineCount > 0) return dialogue;
            if (!string.IsNullOrEmpty(quickTestText)) return DialogueData.CreateTemp(quickTestText);
            return null;
        }

        // ---- 触发器模式（可选）----
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!useTriggerEvents) return;
            if (!other.CompareTag("Player")) return;
            TryStartDialogue(other.transform);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, interactRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(PromptWorldPosition, 0.08f);
        }
    }
}
