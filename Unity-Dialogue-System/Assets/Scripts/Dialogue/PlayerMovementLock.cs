using System.Collections;
using UnityEngine;

namespace PixelDialogue
{
    /// <summary>
    /// 玩家移动锁定工具。对话进行中把玩家定住，避免一边走一边说话。
    /// 通过"引用计数"实现：多个对话叠在一起也不会提前解锁。
    /// </summary>
    public static class PlayerMovementLock
    {
        static int _depth;
        static readonly System.Collections.Generic.List<Behaviour> _disabled =
            new System.Collections.Generic.List<Behaviour>();

        public static bool IsLocked => _depth > 0;

        /// <summary>锁定玩家输入。disableThese 里填玩家的移动脚本（比如 PlayerController）。</summary>
        public static void Lock(params Behaviour[] disableThese)
        {
            _depth++;

            if (disableThese == null) return;

            foreach (var b in disableThese)
            {
                if (b == null || _disabled.Contains(b)) continue;
                b.enabled = false;
                _disabled.Add(b);
            }
        }

        /// <summary>解除一层锁定；计数归零时恢复所有被禁用的脚本。</summary>
        public static void Unlock()
        {
            _depth = Mathf.Max(0, _depth - 1);
            if (_depth > 0) return;

            foreach (var b in _disabled)
                if (b != null)
                    b.enabled = true;

            _disabled.Clear();
        }

        /// <summary>兜底：退出播放 / 场景切换时清干净，避免编辑器里残留禁用状态。</summary>
        public static void ForceReset()
        {
            _depth = 0;
            foreach (var b in _disabled)
                if (b != null)
                    b.enabled = true;
            _disabled.Clear();
        }
    }
}
