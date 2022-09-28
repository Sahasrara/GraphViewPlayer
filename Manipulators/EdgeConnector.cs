// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public abstract class EdgeConnector : MouseManipulator
    {
        public abstract EdgeDragHelper edgeDragHelper { get; }
    }

    public class EdgeConnector<TEdge> : EdgeConnector where TEdge : Edge, new()
    {
        readonly EdgeDragHelper m_EdgeDragHelper;
        private bool m_Active;
        Vector2 m_MouseDownPosition;

        internal const float k_ConnectionDistanceTreshold = 10f;

        public EdgeConnector()
        {
            m_EdgeDragHelper = new EdgeDragHelper<TEdge>();
            m_Active = false;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        public override EdgeDragHelper edgeDragHelper => m_EdgeDragHelper;

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<KeyDownEvent>(OnKeyDown);
            target.RegisterCallback<MouseCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnCaptureOut);
        }

        protected virtual void OnMouseDown(MouseDownEvent e)
        {
            if (m_Active)
            {
                e.StopImmediatePropagation();
                return;
            }

            if (!CanStartManipulation(e))
            {
                return;
            }

            Port draggedPort = target as Port;
            if (draggedPort == null || !draggedPort.CanConnectToMore())
            {
                return;
            }

            m_MouseDownPosition = e.localMousePosition;
            m_EdgeDragHelper.draggedPort = draggedPort;
            m_EdgeDragHelper.edgeCandidate = new TEdge();

            if (m_EdgeDragHelper.HandleMouseDown(e))
            {
                m_Active = true;
                target.CaptureMouse();

                e.StopPropagation();
            }
            else
            {
                m_EdgeDragHelper.Reset();
            }
        }

        void OnCaptureOut(MouseCaptureOutEvent e)
        {
            m_Active = false;
            Abort();
        }

        protected virtual void OnMouseMove(MouseMoveEvent e)
        {
            if (!m_Active) return;

            m_EdgeDragHelper.HandleMouseMove(e);
            e.StopPropagation();
        }

        protected virtual void OnMouseUp(MouseUpEvent e)
        {
            DitchFocus();
            if (!m_Active || !CanStopManipulation(e))
                return;

            if (CanPerformConnection(e.localMousePosition))
                m_EdgeDragHelper.HandleMouseUp(e);
            else
                Abort();

            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }

        protected virtual void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Escape || !m_Active)
                return;

            DitchFocus();
            Abort();

            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }

        void Abort()
        {
            m_EdgeDragHelper.Reset();
        }

        bool CanPerformConnection(Vector2 mousePosition)
        {
            return Vector2.Distance(m_MouseDownPosition, mousePosition) > k_ConnectionDistanceTreshold;
        }

        private void DitchFocus()
        {
            if (target is Port port) port.GetFirstAncestorOfType<GraphView>().Focus();
        }
    }
}
