// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class SelectableManipulator : MouseManipulator
    {
        private ISelectable m_Selectable;

        public SelectableManipulator()
        {
            activators.Add(new() { button = MouseButton.LeftMouse });
            activators.Add(new() { button = MouseButton.LeftMouse, modifiers = EventModifiers.Shift });
            activators.Add(new()
            {
                button = MouseButton.LeftMouse,
                modifiers = PlatformUtils.IsMac ? EventModifiers.Command : EventModifiers.Control
            });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            m_Selectable = target as ISelectable;
            if (m_Selectable == null) { throw new("SelectableManipulator can only be added to ISelectable elements"); }
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            m_Selectable = null;
        }

        protected void OnMouseDown(MouseDownEvent e)
        {
            if (!CanStartManipulation(e)) { return; }

            // Do not stop the propagation since selection should be happening before other use cases
            bool additive = e.shiftKey;
            bool subtractive = e.actionKey;
            bool exclusive = !(additive ^ subtractive);
            if (exclusive)
            {
                if (!m_Selectable.Selected)
                {
                    m_Selectable.Selector.ClearSelection();
                    m_Selectable.Selected = true;
                }
            }
            else { m_Selectable.Selected = !m_Selectable.Selected; }
        }
    }
}