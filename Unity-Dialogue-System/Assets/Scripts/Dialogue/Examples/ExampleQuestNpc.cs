using UnityEngine;
using PixelDialogue;

namespace PixelDialogue.Examples
{
    /// <summary>
    /// 示例：任务 NPC。对话结束后推进任务状态，玩家再次对话时会说不同的话。
    /// 把这个脚本和 DialogueTrigger 一起挂在 NPC 上。
    /// </summary>
    [RequireComponent(typeof(DialogueTrigger))]
    public class ExampleQuestNpc : MonoBehaviour
    {
        public enum QuestState { NotStarted, InProgress, Completed }

        [Header("三套对话")]
        public DialogueData firstTalkDialogue;
        public DialogueData whileInProgressDialogue;
        public DialogueData completedDialogue;

        [Header("需要收集的物品数量")]
        public int requiredItems = 3;

        public QuestState State { get; private set; } = QuestState.NotStarted;

        int _collected;
        DialogueTrigger _trigger;

        void Awake()
        {
            _trigger = GetComponent<DialogueTrigger>();
        }

        void OnEnable()
        {
            _trigger.DialogueFinished += OnDialogueFinished;
        }

        void OnDisable()
        {
            _trigger.DialogueFinished -= OnDialogueFinished;
        }

        /// <summary>由其他脚本调用，比如玩家拾取物品时。</summary>
        public void CollectItem()
        {
            if (State == QuestState.Completed) return;

            _collected++;
            if (_collected >= requiredItems)
                State = QuestState.InProgress;
        }

        void OnDialogueFinished(DialogueTrigger trigger)
        {
            switch (State)
            {
                case QuestState.NotStarted:
                    // 第一次说完话，任务开始
                    State = QuestState.InProgress;
                    _collected = 0;
                    trigger.dialogue = whileInProgressDialogue; // 换下一套对话
                    break;

                case QuestState.InProgress:
                    if (_collected >= requiredItems)
                    {
                        State = QuestState.Completed;
                        trigger.dialogue = completedDialogue;
                        OnQuestCompleted();
                    }
                    else
                    {
                        // 还没收集够，下次再催一次
                        Debug.Log($"还需要 {requiredItems - _collected} 个物品。");
                    }
                    break;

                case QuestState.Completed:
                    break;
            }
        }

        void OnQuestCompleted()
        {
            Debug.Log("[ExampleQuestNpc] 任务完成！这里可以给奖励、开传送门、播特效等。");
        }
    }
}
