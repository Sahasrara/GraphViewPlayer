// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public abstract class PortManipulator : MouseManipulator
    {
        public abstract EdgeDragHelper edgeDragHelper { get; }
    }

    public class PortManipulator<TEdge> : PortManipulator where TEdge : Edge, new()
    {
        private const float k_ConnectionDistanceTreshold = 10f;
        private readonly EdgeDragHelper m_EdgeDragHelper;
        private bool m_Active;
        private Vector2 m_MouseDownPosition;

        public PortManipulator()
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
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
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
            if (draggedPort == null || !draggedPort.CanConnectToMore()) return;

            GraphView graphView = draggedPort.GetFirstAncestorOfType<GraphView>();
            m_MouseDownPosition = e.localMousePosition;

            TEdge candidateEdge = new TEdge(); 
            candidateEdge.SetPortByDirection(draggedPort);
            m_EdgeDragHelper.HandleDragStart(e, graphView, draggedPort, candidateEdge);
            m_Active = true;
            target.CaptureMouse();
            e.StopPropagation();
        }

        protected virtual void OnMouseMove(MouseMoveEvent e)
        {
            if (!m_Active) return;

            m_EdgeDragHelper.HandleDragContinue(e);
            e.StopPropagation();
        }

        protected virtual void OnMouseUp(MouseUpEvent e)
        {
            DitchFocus();
            if (!m_Active || !CanStopManipulation(e))
                return;

            if (CanPerformConnection(e.localMousePosition))
                m_EdgeDragHelper.HandleDragEnd(e);
            else
                m_EdgeDragHelper.HandleDragCancel();

            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }

        protected virtual void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Escape || !m_Active)
                return;

            DitchFocus();
            m_EdgeDragHelper.HandleDragCancel();

            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
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
