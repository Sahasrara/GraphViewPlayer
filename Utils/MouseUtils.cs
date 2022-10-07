using UnityEngine;

namespace GraphViewPlayer
{
    public static class MouseUtils
    {
        public static bool IsNone(this EventModifiers modifiers) => modifiers == EventModifiers.None;
        public static bool IsShift(this EventModifiers modifiers) => (modifiers & EventModifiers.Shift) != 0;
        public static bool IsActionKey(this EventModifiers modifiers)
            => PlatformUtils.IsMac
                ? (modifiers & EventModifiers.Command) != 0
                : (modifiers & EventModifiers.Control) != 0;

        public static bool IsExclusiveShift(this EventModifiers modifiers) => modifiers == EventModifiers.Shift;
        public static bool IsExclusiveActionKey(this EventModifiers modifiers)
            => PlatformUtils.IsMac
                ? modifiers == EventModifiers.Command
                : modifiers == EventModifiers.Control;
    }
}