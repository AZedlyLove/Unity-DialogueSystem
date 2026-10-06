using System;
using UnityEngine;

namespace PixelDialogue
{
    /// <summary>
    /// 一条对话内容。
    /// </summary>
    [Serializable]
    public struct DialogueLine
    {
        [Tooltip("说话者名字，留空则不显示名字行")]
        public string speakerName;

        [TextArea(2, 6)]
        [Tooltip("这句话的内容。支持 TextMeshPro 富文本标签，例如 <color=#FFD24A>金色</color>")]
        public string text;

        [Tooltip("这句话打完后，是否等待玩家按键才继续。勾掉则自动继续")]
        public bool waitForInput;

        [Tooltip("勾掉 waitForInput 时，打完后停留多少秒自动进入下一句（<0 表示用全局默认值）")]
        public float autoAdvanceDelay;

        public DialogueLine(string text)
        {
            this.speakerName = null;
            this.text = text;
            this.waitForInput = true;
            this.autoAdvanceDelay = -1f;
        }
    }

    /// <summary>
    /// 一段完整对话。在 Project 窗口右键：Create > PixelDialogue > Dialogue Data 创建，
    /// 然后在 NPC 的 DialogueTrigger 上拖进去即可。
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue_", menuName = "PixelDialogue/Dialogue Data", order = 0)]
    public class DialogueData : ScriptableObject
    {
        [Header("识别信息")]
        [Tooltip("谁在说话，用于默认名字显示")]
        public string speakerName = "村民";

        [Tooltip("可选头像，留空则只显示名字")]
        public Sprite portrait;

        [Header("对话内容")]
        public DialogueLine[] lines;

        [Header("本地覆盖（-1 表示用 DialogueManager 的全局默认值）")]
        [Tooltip("每句话之间的额外间隔秒数")]
        public float lineDelay = -1f;

        [Tooltip("整段对话结束后的额外等待秒数")]
        public float endDelay = -1f;

        public int LineCount => lines != null ? lines.Length : 0;

        public DialogueLine GetLine(int index)
        {
            DialogueLine line = lines[index];

            // 名字留空时自动补上 Data 上的默认名字，省得每句都填
            if (string.IsNullOrEmpty(line.speakerName))
                line.speakerName = speakerName;

            return line;
        }

        /// <summary>
        /// 允许在代码里临时跑一段不落资源的对话：
        ///     DialogueManager.Instance.Play(DialogueData.CreateTemp("你好呀"));
        /// </summary>
        public static DialogueData CreateTemp(params string[] texts)
        {
            DialogueData data = CreateInstance<DialogueData>();
            data.speakerName = "";
            data.lines = new DialogueLine[texts.Length];
            for (int i = 0; i < texts.Length; i++)
                data.lines[i] = new DialogueLine(texts[i]);
            return data;
        }
    }
}
