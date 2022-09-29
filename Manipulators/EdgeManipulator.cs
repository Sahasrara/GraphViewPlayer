// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class EdgeManipulator : MouseManipulator
    {
        private bool m_Active;
        private Edge m_Edge;
        private Vector2 m_PressPos;
        private Port m_ConnectedPort;
        private EdgeDragHelper m_ConnectedEdgeDragHelper;
        private readonly List<Edge> m_AdditionalEdges;
        private readonly List<EdgeDragHelper> m_AdditionalEdgeDragHelpers;
        private Port m_DetachedPort;
        private bool m_DetachedFromInputPort;
        private static int s_StartDragDistance = 10;
        private MouseDownEvent m_LastMouseDownEvent;

        public EdgeManipulator()
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            m_AdditionalEdges = new();
            m_AdditionalEdgeDragHelpers = new();
            Reset();
        }

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

        private void Reset()
        {
            m_Active = false;
            m_Edge = null;
            m_ConnectedPort = null;
            m_ConnectedEdgeDragHelper = null;
            m_AdditionalEdges.Clear();
            m_AdditionalEdgeDragHelpers.Clear();
            m_DetachedPort = null;
            m_DetachedFromInputPort = false;
        }

        protected void OnMouseDown(MouseDownEvent evt)
        {
            if (m_Active)
            {
                StopDragging();
                evt.StopImmediatePropagation();
                return;
            }

            if (!CanStartManipulation(evt))
            {
                return;
            }

            m_Edge = (evt.target as VisualElement).GetFirstOfType<Edge>();

            m_PressPos = evt.mousePosition;
            target.CaptureMouse();
            evt.StopPropagation();
            m_LastMouseDownEvent = evt;
        }

        protected void OnMouseMove(MouseMoveEvent evt)
        {
            // If the left mouse button is not down then return
            if (m_Edge == null) return;
            evt.StopPropagation();
            
            // If one end of the edge is not already detached then
            if (m_DetachedPort == null)
            {
                // Did we breach the movement threshold to start a drag?
                float delta = (evt.mousePosition - m_PressPos).sqrMagnitude;
                if (delta < (s_StartDragDistance * s_StartDragDistance))  return;

                // Determine which end is the nearest to the mouse position then detach it.
                Vector2 outputPos = new Vector2(m_Edge.output.GetGlobalCenter().x, m_Edge.output.GetGlobalCenter().y);
                Vector2 inputPos = new Vector2(m_Edge.input.GetGlobalCenter().x, m_Edge.input.GetGlobalCenter().y);

                float distanceFromOutput = (m_PressPos - outputPos).sqrMagnitude;
                float distanceFromInput = (m_PressPos - inputPos).sqrMagnitude;
                m_DetachedFromInputPort = distanceFromInput < distanceFromOutput;
                if (m_DetachedFromInputPort)
                {
                    m_ConnectedPort = m_Edge.output;
                    m_DetachedPort = m_Edge.input;
                }
                else
                {
                    m_ConnectedPort = m_Edge.input;
                    m_DetachedPort = m_Edge.output;
                }

                // Use the edge drag helper of the still connected port
                GraphView graphView = m_DetachedPort.GetFirstAncestorOfType<GraphView>();
                m_AdditionalEdgeDragHelpers.Clear();
                m_AdditionalEdges.Clear();
                if (m_DetachedPort.allowMultiDrag)
                {
                    foreach (Edge edge in m_DetachedPort.connections)
                    {
                        if (edge.IsSelected(graphView))
                        {
                            var draggedPort = m_DetachedFromInputPort ? edge.output : edge.input;
                            var edgeDragHelper = draggedPort.portManipulator.edgeDragHelper;
                            m_AdditionalEdgeDragHelpers.Add(edgeDragHelper);
                            m_AdditionalEdges.Add(edge);
                            edgeDragHelper.HandleDragStart(m_LastMouseDownEvent, graphView, draggedPort, edge);
                        }
                    }
                }
                else
                {
                    EdgeDragHelper helper = m_ConnectedPort.portManipulator.edgeDragHelper;
                    m_AdditionalEdges.Add(m_Edge);
                    m_AdditionalEdgeDragHelpers.Add(helper);
                    helper.HandleDragStart(m_LastMouseDownEvent, graphView, m_ConnectedPort, m_Edge);
                }
                m_Active = true;
                m_LastMouseDownEvent = null;
            }

            if (m_Active)
            {
                foreach (var edgeDrag in m_AdditionalEdgeDragHelpers)
                {
                    edgeDrag.HandleDragContinue(evt);
                }
            }
        }

        protected void OnMouseUp(MouseUpEvent evt)
        {
            DitchFocus();
            if (CanStopManipulation(evt))
            {
                target.ReleaseMouse();
                if (m_Active)
                {
                    foreach (var edgeDrag in m_AdditionalEdgeDragHelpers)
                    {
                        edgeDrag.HandleDragEnd(evt);
                    }
                }
                Reset();
                evt.StopPropagation();
            }
        }

        protected void OnKeyDown(KeyDownEvent e)
        {
            if (!m_Active || e.keyCode != KeyCode.Escape)
                return;

            DitchFocus();
            StopDragging();
            e.StopPropagation();
        }

        private void DitchFocus()
        {
            if (target is Edge edge) edge.GetFirstAncestorOfType<GraphView>().Focus();
        }

        void StopDragging()
        {
            foreach (var edgeDrag in m_AdditionalEdgeDragHelpers)
            {
                edgeDrag.HandleDragCancel();
            }

            Reset();
            target.ReleaseMouse();
        }
    }
}
