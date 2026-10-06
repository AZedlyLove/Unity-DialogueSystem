using UnityEngine;
using PixelDialogue;

namespace PixelDialogue.Examples
{
    /// <summary>
    /// 示例：进入区域自动触发对话（和 NPC 按键互动相反，适合剧情演出/场景旁白）。
    /// 用法：给一个带 Collider2D(Is Trigger) 的空物体挂上本脚本，玩家需要有 Rigidbody2D。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ExampleAutoDialogueZone : MonoBehaviour
    {
        [Tooltip("进入区域后播放的对话")]
        public DialogueData dialogue;

        [Tooltip("旁白锚点：留空则锚在玩家头顶（旁白感更强）")]
        public Transform anchorOverride;

        [Tooltip("整局游戏只触发一次")]
        public bool oneShot = true;

        [Tooltip("触发前的延迟，适合「走到这里停一下再说」的情况")]
        public float delay = 0.3f;

        [Tooltip("对话结束后是否禁用本触发器")]
        public bool disableAfterUse = true;

        bool _used;
        bool _pending;

        void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (oneShot && _used) return;
            if (!other.CompareTag("Player")) return;
            if (_pending) return;
            if (dialogue == null || dialogue.LineCount == 0) return;

            _pending = true;
            Invoke(nameof(StartDialogue), Mathf.Max(0f, delay));
        }

        void StartDialogue()
        {
            _pending = false;

            if (DialogueManager.Instance == null) return;
            if (DialogueManager.IsActive) return;

            Transform anchor = anchorOverride != null ? anchorOverride : DialogueManager.ResolvePlayer();
            if (DialogueManager.Instance.Play(dialogue, anchor))
            {
                _used = true;
                if (disableAfterUse) GetComponent<Collider2D>().enabled = false;
            }
        }
    }
}
