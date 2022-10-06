using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    /// <summary>
    ///     During bubble events, prevent all mouse activity below this manipulator's target.
    /// </summary>
    public class ClickBlockManipulator : Manipulator
    {
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            target.RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            target.RegisterCallback<MouseOutEvent>(OnMouseOut);
            target.RegisterCallback<MouseOverEvent>(OnMouseOver);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
            target.UnregisterCallback<MouseEnterEvent>(OnMouseEnter);
            target.UnregisterCallback<MouseOutEvent>(OnMouseOut);
            target.UnregisterCallback<MouseOverEvent>(OnMouseOver);
        }

        private void OnMouseDown(MouseDownEvent e)
        {
            Debug.Log("Blocker");
            e.PreventDefault();
            e.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            e.PreventDefault();
            e.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            e.PreventDefault();
            e.StopPropagation();
        }

        private void OnMouseLeave(MouseLeaveEvent e)
        {
            e.PreventDefault();
            e.StopPropagation();
        }

        private void OnMouseEnter(MouseEnterEvent e)
        {
            e.PreventDefault();
            e.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent e)
        {
            e.PreventDefault();
            e.StopPropagation();
        }

        private void OnMouseOver(MouseOverEvent e)
        {
            e.PreventDefault();
            e.StopPropagation();
        }
    }
}