using System;
using System.Reflection;
using UnityEngine;

namespace PixelDialogue
{
    /// <summary>
    /// 输入兼容层：优先用新 Input System（如果项目装了），否则回退到旧 Input Manager。
    /// 用反射访问新输入系统，所以无论项目用哪套输入方案，这个脚本都能编译通过。
    /// 键位在 DialogueManager 的 Inspector 里配置，不需要改代码。
    /// </summary>
    public static class DialogueInput
    {
        static Type _keyboardType;
        static Type _keyType;
        static MethodInfo _currentKeyboard;
        static MethodInfo _wasPressedThisFrame;
        static MethodInfo _isPressed;
        static PropertyInfo _itemIndexer;
        static object[] _indexArgs;

        static readonly string[] KeyNames =
        {
            "E", "F", "Q", "R", "Space", "Enter", "Return",
            "Z", "X", "C", "V", "Tab", "Escape", "Mouse0", "Mouse1"
        };

        /// <summary>把 "E" / "Space" / "Return" 这类名字翻译成 KeyCode。</summary>
        public static KeyCode ToKeyCode(string name)
        {
            if (string.IsNullOrEmpty(name)) return KeyCode.None;
            if (name.Equals("Return", StringComparison.OrdinalIgnoreCase)) return KeyCode.Return;

            try { return (KeyCode)Enum.Parse(typeof(KeyCode), name, true); }
            catch { return KeyCode.None; }
        }

        /// <summary>把名字翻译成新输入系统的 Key 枚举值（没有新输入系统时返回 null）。</summary>
        static object ToNewKey(string name)
        {
            ResolveNewInput();
            if (_keyType == null || string.IsNullOrEmpty(name)) return null;

            string n = name.Equals("Return", StringComparison.OrdinalIgnoreCase) ? "Enter" : name;
            try { return Enum.Parse(_keyType, n, true); }
            catch { return null; }
        }

        static void ResolveNewInput()
        {
            if (_keyboardType != null || _keyType != null) return;

            try
            {
                _keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
                _keyType = Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");
                if (_keyboardType == null || _keyType == null) return;

                _currentKeyboard = _keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                _wasPressedThisFrame = _keyboardType.GetMethod("wasPressedThisFrame", new[] { _keyType });
                _isPressed = _keyboardType.GetMethod("isPressed", new[] { _keyType });
                _itemIndexer = _keyboardType.GetProperty("Item", new[] { _keyType });
                _indexArgs = new object[1];
            }
            catch
            {
                _keyboardType = null;
                _keyType = null;
            }
        }

        static bool TryNewInput(string keyName, bool wantIsPressed, out bool value)
        {
            value = false;
            ResolveNewInput();
            if (_keyboardType == null || _keyType == null) return false;

            try
            {
                object keyboard = _currentKeyboard?.Invoke(null, null);
                if (keyboard == null) return true; // 已装新输入系统但当前无键盘，视为"没按下"

                object key = ToNewKey(keyName);
                if (key == null) return false;

                if (wantIsPressed)
                {
                    if (_isPressed == null) return false;
                    value = (bool)_isPressed.Invoke(keyboard, new[] { key });
                    return true;
                }

                if (_wasPressedThisFrame != null)
                {
                    value = (bool)_wasPressedThisFrame.Invoke(keyboard, new[] { key });
                    return true;
                }

                if (_itemIndexer != null)
                {
                    _indexArgs[0] = key;
                    object control = _itemIndexer.GetValue(keyboard, _indexArgs);
                    var wasPressedProp = control?.GetType().GetProperty("wasPressedThisFrame");
                    if (wasPressedProp != null)
                    {
                        value = (bool)wasPressedProp.GetValue(control);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>这一帧是否按下（按下瞬间，只触发一次）。</summary>
        public static bool GetKeyDown(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return false;

            if (TryNewInput(keyName, false, out bool newValue))
                return newValue;

            KeyCode code = ToKeyCode(keyName);
            return code != KeyCode.None && Input.GetKeyDown(code);
        }

        /// <summary>是否正被按住（用于按住加速打字）。</summary>
        public static bool GetKey(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return false;

            if (TryNewInput(keyName, true, out bool newValue))
                return newValue;

            KeyCode code = ToKeyCode(keyName);
            return code != KeyCode.None && Input.GetKey(code);
        }

        /// <summary>供 Inspector 下拉框使用的键位候选。</summary>
        public static string[] SupportedKeyNames => KeyNames;
    }
}
