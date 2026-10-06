using System.Collections;
using UnityEngine;

namespace PixelDialogue
{
    /// <summary>对话运行时的状态机。由 DialogueManager 创建和销毁，不手动挂载。</summary>
    public class DialogueSession : MonoBehaviour
    {
        public enum State { Typing, WaitingForInput, AutoDelay, Closing, Finished }

        public State Current { get; private set; } = State.Typing;

        DialogueData _data;
        Transform _anchor;
        DialogueBubbleView _bubble;
        DialogueManager _manager;
        int _index = -1;
        Coroutine _routine;
        bool _frozen;

        public event System.Action<DialogueSession> Finished;

        /// <summary>由 DialogueManager 决定这次对话要不要锁住玩家。</summary>
        internal void SetFreeze(bool freeze)
        {
            _frozen = freeze && _manager != null && _manager.freezePlayerWhileTalking;
        }

        internal static DialogueSession Create(
            DialogueManager manager, DialogueData data, Transform anchor, DialogueBubbleView bubble)
        {
            var go = new GameObject("~DialogueSession");
            var session = go.AddComponent<DialogueSession>();
            session._manager = manager;
            session._data = data;
            session._anchor = anchor;
            session._bubble = bubble;
            session._frozen = manager.freezePlayerWhileTalking;
            return session;
        }

        void Start()
        {
            if (_frozen)
                PlayerMovementLock.Lock(_manager.playerBehavioursToDisable);
            _routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // 1) 气泡平滑出现
            _bubble.SetSpeedMultiplier(1f);
            _bubble.Show(_data.speakerName, _data.portrait, string.Empty, _anchor);
            yield return new WaitForSecondsRealtime(_manager.bubbleLeadIn);

            // 2) 逐句播放
            if (_data.LineCount == 0)
            {
                yield return new WaitForSecondsRealtime(_manager.minBubbleHold);
            }
            else
            {
                for (int i = 0; i < _data.LineCount; i++)
                {
                    DialogueLine line = _data.GetLine(i);

                    _bubble.SetContent(line.speakerName, _data.portrait, line.text);
                    Current = State.Typing;

                    // 打字阶段：按住键加速；按一下键立刻显示整句
                    while (_bubble.IsTyping)
                    {
                        if (DialogueInput.GetKeyDown(_manager.advanceKey))
                        {
                            _bubble.CompleteTyping();
                            break;
                        }

                        bool holding = DialogueInput.GetKey(_manager.skipTypingKey);
                        _bubble.SetSpeedMultiplier(holding ? _manager.holdToSpeedMultiplier : 1f);
                        yield return null;
                    }
                    _bubble.SetSpeedMultiplier(1f);

                    bool isLast = i >= _data.LineCount - 1;
                    bool waitForInput = line.waitForInput;

                    // 最后一句之后永远等玩家按键，否则气泡会自己跑掉
                    if (isLast) waitForInput = true;

                    if (waitForInput)
                    {
                        Current = State.WaitingForInput;
                        // 吞掉"刚才跳过打字"的那一次按键，避免一句被按两下就翻页
                        yield return null;

                        while (!DialogueInput.GetKeyDown(_manager.advanceKey))
                            yield return null;
                    }
                    else
                    {
                        Current = State.AutoDelay;
                        float delay = line.autoAdvanceDelay >= 0f
                            ? line.autoAdvanceDelay
                            : _manager.defaultAutoAdvanceDelay;

                        float t = 0f;
                        while (t < delay)
                        {
                            if (DialogueInput.GetKeyDown(_manager.advanceKey))
                                break; // 玩家抢着继续
                            t += Time.unscaledDeltaTime;
                            yield return null;
                        }
                    }

                    float lineGap = _data.lineDelay >= 0f ? _data.lineDelay : _manager.defaultLineDelay;
                    if (lineGap > 0f)
                        yield return new WaitForSecondsRealtime(lineGap);
                }
            }

            // 3) 末尾停顿 + 平滑消失
            Current = State.Closing;
            float endGap = _data.endDelay >= 0f ? _data.endDelay : _manager.defaultEndDelay;
            if (endGap > 0f)
                yield return new WaitForSecondsRealtime(endGap);

            _bubble.Hide();
            yield return new WaitForSecondsRealtime(_manager.bubbleFadeOutWait);
            Finish();
        }

        /// <summary>外部强制中断（例如切场景、被打断）。</summary>
        public void Abort()
        {
            if (_routine != null) StopCoroutine(_routine);
            if (_bubble != null) _bubble.Hide();
            Finish();
        }

        void Finish()
        {
            if (Current == State.Finished) return;
            Current = State.Finished;

            if (_frozen)
            {
                PlayerMovementLock.Unlock();
                _frozen = false;
            }

            Finished?.Invoke(this);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_frozen)
            {
                PlayerMovementLock.Unlock();
                _frozen = false;
            }
        }
    }
}
