// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class Dragger : MouseManipulator
    {
        protected bool m_Active;
        private Vector2 m_Start;

        public Dragger()
        {
            activators.Add(new() { button = MouseButton.LeftMouse });
            panSpeed = new(1, 1);
            clampToParentEdges = false;
            m_Active = false;
        }

        public Vector2 panSpeed { get; set; }

        public bool clampToParentEdges { get; set; }

        protected Rect CalculatePosition(float x, float y, float width, float height)
        {
            Rect rect = new(x, y, width, height);

            if (clampToParentEdges)
            {
                Rect shadowRect = new(Vector2.zero, target.hierarchy.parent.layout.size);
                if (rect.x < shadowRect.xMin) { rect.x = shadowRect.xMin; }
                else if (rect.xMax > shadowRect.xMax) { rect.x = shadowRect.xMax - rect.width; }

                if (rect.y < shadowRect.yMin) { rect.y = shadowRect.yMin; }
                else if (rect.yMax > shadowRect.yMax) { rect.y = shadowRect.yMax - rect.height; }

                // Reset size, we never intended to change them in the first place
                rect.width = width;
                rect.height = height;
            }

            return rect;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
        }

        protected void OnMouseDown(MouseDownEvent e)
        {
            if (m_Active)
            {
                e.StopImmediatePropagation();
                return;
            }

            GraphElement ce = e.target as GraphElement;
            if (ce != null && !ce.IsMovable()) { return; }

            if (CanStartManipulation(e))
            {
                m_Start = e.localMousePosition;

                m_Active = true;
                target.CaptureMouse();
                e.StopPropagation();
            }
        }

        protected void OnMouseMove(MouseMoveEvent e)
        {
            GraphElement ce = e.target as GraphElement;
            if (ce != null && !ce.IsMovable()) { return; }

            if (m_Active)
            {
                Vector2 diff = e.localMousePosition - m_Start;

                if (ce != null)
                {
                    Vector3 targetScale = ce.transform.scale;
                    diff.x *= targetScale.x;
                    diff.y *= targetScale.y;
                }

                Rect rect = CalculatePosition(target.layout.x + diff.x, target.layout.y + diff.y, target.layout.width,
                    target.layout.height);

                target.style.left = rect.x;
                target.style.top = rect.y;

                // target.style.width = rect.width;
                // target.style.height = rect.height;

                e.StopPropagation();
            }
        }

        protected void OnMouseUp(MouseUpEvent e)
        {
            GraphElement ce = e.target as GraphElement;
            if (ce != null && !ce.IsMovable()) { return; }

            if (m_Active)
            {
                if (CanStopManipulation(e))
                {
                    m_Active = false;
                    target.ReleaseMouse();
                    e.StopPropagation();
                }
            }
        }
    }
}