// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public abstract class EdgeDragHelper
    {
        public abstract void HandleDragStart(MouseDownEvent evt, GraphView graphView, Port port, Edge edge);
        public abstract void HandleDragContinue(MouseMoveEvent evt);
        public abstract void HandleDragEnd(MouseUpEvent evt);
        public abstract void HandleDragCancel();
    }

    public class EdgeDragHelper<TEdge> : EdgeDragHelper where TEdge : Edge, new()
    {
        private bool m_WasPanned;
        private Vector3 m_PanDiff = Vector3.zero;
        private Vector3 m_PanStart = Vector3.zero;
        private IVisualElementScheduledItem m_PanSchedule;
        
        protected List<Port> m_CompatiblePorts;
        protected Edge m_GhostEdge;
        protected GraphView m_GraphView;
        protected Edge m_EdgeCandidate;
        protected Port m_DraggedPort;

        public bool resetPositionOnPan { get; set; }

        public EdgeDragHelper()
        {
            resetPositionOnPan = true;
            m_CompatiblePorts = new();
        }

        public override void HandleDragStart(MouseDownEvent evt, GraphView graphView, Port port, Edge edge)
        {
            // Update dragged port, edge candidate, and graph view
            m_DraggedPort = port;
            m_EdgeCandidate = edge;
            m_GraphView = graphView;

            // Update edge drawing
            if (m_DraggedPort.direction == Direction.Output) m_EdgeCandidate.SetInputPositionOverride(evt.mousePosition);
            else m_EdgeCandidate.SetOutputPositionOverride(evt.mousePosition);
            
            // Only light compatible anchors when dragging an edge.
            m_GraphView.GetCompatiblePorts(m_CompatiblePorts, m_DraggedPort);
            m_GraphView.ports.ForEach(StartDraggingPort); // Don't Highlight
            m_CompatiblePorts.ForEach(StopDraggingPort); // Highlight

            // Setup for panning
            m_PanStart = m_GraphView.viewTransform.position; 
            if (m_PanSchedule == null)
            {
                m_PanSchedule = m_GraphView.schedule
                    .Execute(Pan)
                    .Every(PanUtils.k_PanInterval)
                    .StartingIn(PanUtils.k_PanInterval);
                m_PanSchedule.Pause();
            }
            m_WasPanned = false;

            // Set edge candidate layer and add to graph 
            m_EdgeCandidate.layer = Int32.MaxValue;
            m_GraphView.AddElement(m_EdgeCandidate);
        }

        public override void HandleDragContinue(MouseMoveEvent evt)
        {
            // Panning
            VisualElement ve = (VisualElement)evt.target;
            Vector2 gvMousePos = ve.ChangeCoordinatesTo(m_GraphView.contentContainer, evt.localMousePosition);
            m_PanDiff = PanUtils.GetEffectivePanSpeed(m_GraphView, gvMousePos);
            if (m_PanDiff != Vector3.zero) m_PanSchedule.Resume();
            else m_PanSchedule.Pause();

            // Update edge drawing
            Vector2 mousePosition = evt.mousePosition;
            if (m_DraggedPort.direction == Direction.Output) m_EdgeCandidate.SetInputPositionOverride(mousePosition);
            else m_EdgeCandidate.SetOutputPositionOverride(mousePosition);

            // Draw ghost edge if possible port exists.
            Port endPort = GetEndPort(mousePosition);
            if (endPort != null)
            {
                if (m_GhostEdge == null)
                {
                    m_GhostEdge = new TEdge();
                    m_GhostEdge.isGhostEdge = true;
                    m_GhostEdge.pickingMode = PickingMode.Ignore;
                    m_GraphView.AddElement(m_GhostEdge);
                }

                if (m_EdgeCandidate.InputPositionOverridden)
                {
                    m_GhostEdge.input = endPort;
                    m_GhostEdge.output = m_EdgeCandidate.output;
                }
                else
                {
                    m_GhostEdge.input = m_EdgeCandidate.input;
                    m_GhostEdge.output = endPort;
                }
            }
            else CleanGhostEdge();
        }

        public override void HandleDragEnd(MouseUpEvent evt)
        {
            // Did mouse release on port?
            Port endPort = GetEndPort(evt.mousePosition);

            // This is a connection
            bool edgeCandidateWasExistingEdge = m_EdgeCandidate.input != null && m_EdgeCandidate.output != null;
            if (endPort != null)
            {
                // Find new input/output ports
                Port inputPort;
                Port outputPort;
                if (endPort.direction == Direction.Output)
                {
                    inputPort = m_EdgeCandidate.input;
                    outputPort = endPort;
                }
                else
                {
                    inputPort = endPort;
                    outputPort = m_EdgeCandidate.output; 
                }

                if (edgeCandidateWasExistingEdge)
                {
                    // Existing edge is being deleted and connected elsewhere so request a deletion 
                    m_GraphView.OnEdgeDelete(m_EdgeCandidate);
                    // TODO: delete this, should be handled by OnEdgeDelete
                    m_EdgeCandidate.Disconnect();
                }

                // Set ports on the new edge
                m_EdgeCandidate.input = inputPort;
                m_EdgeCandidate.output = outputPort;

                // Create new edge
                m_GraphView.OnEdgeCreate(m_EdgeCandidate);
                // TODO: remove what's below. OnEdgeCreate should tackle that.
                m_GraphView.AddElement(m_EdgeCandidate);
            }
            // This is a disconnection
            else
            {
                // This was an existing edge and so much be deleted 
                if (edgeCandidateWasExistingEdge)
                {
                    m_GraphView.OnEdgeDelete(m_EdgeCandidate);
                    // TODO
                    m_EdgeCandidate.Disconnect();
                }
            }
            HandleDragCancel();
        }

        public override void HandleDragCancel()
        {
            // Reset the highlights.
            m_GraphView?.ports.ForEach(StopDraggingPort);

            // Clear Compatible Ports
            m_CompatiblePorts.Clear();

            // Clean up ghost edge.
            CleanGhostEdge();

            // Reset view if we didn't connect, or we always want a reset
            m_PanSchedule?.Pause();
            if (m_WasPanned && resetPositionOnPan)
            {
                m_GraphView.UpdateViewTransform(m_PanStart, m_GraphView.viewTransform.scale);
            }

            // Clean up candidate edges
            if (m_EdgeCandidate != null)
            {
                m_EdgeCandidate.UnsetPositionOverrides();
                m_EdgeCandidate.ResetLayer();

                // This was only an edge candidate, not previously used as a real edge
                if (m_EdgeCandidate.input == null || m_EdgeCandidate.output == null)
                {
                    m_EdgeCandidate.Disconnect();
                    m_GraphView.RemoveElement(m_EdgeCandidate);
                }
            }

            m_DraggedPort = null;
            m_EdgeCandidate = null;
            m_GraphView = null; 
        }

        private void Pan(TimerState ts)
        {
            m_GraphView.UpdateViewTransform(m_GraphView.viewTransform.position - m_PanDiff);
            m_EdgeCandidate.ForceUpdateEdgeControl();
            m_WasPanned = true; 
        }

        private Port GetEndPort(Vector2 mousePosition)
        {
            if (m_GraphView == null)
                return null;

            Port endPort = null;

            foreach (Port compatiblePort in m_CompatiblePorts)
            {
                Rect bounds = compatiblePort.worldBound;
                float hitboxExtraPadding = bounds.height;

                if (compatiblePort.orientation == Orientation.Horizontal)
                {
                    // Add extra padding for mouse check to the left of input port or right of output port.
                    if (compatiblePort.direction == Direction.Input)
                    {
                        // Move bounds to the left by hitboxExtraPadding and increase width
                        // by hitboxExtraPadding.
                        bounds.x -= hitboxExtraPadding;
                        bounds.width += hitboxExtraPadding;
                    }
                    else if (compatiblePort.direction == Direction.Output)
                    {
                        // Just add hitboxExtraPadding to the width.
                        bounds.width += hitboxExtraPadding;
                    }
                }

                // Check if mouse is over port.
                if (bounds.Contains(mousePosition))
                {
                    endPort = compatiblePort;
                    break;
                }
            }

            return endPort;
        }

        private void CleanGhostEdge()
        {
            if (m_GhostEdge != null)
            {
                // TODO pool these? Or just ditch them entirely???
                m_GraphView?.RemoveElement(m_GhostEdge);
                m_GhostEdge.input = null;
                m_GhostEdge.output = null;
                m_GhostEdge = null;
            }
        }

        private void StartDraggingPort(Port p) => p.OnStartEdgeDragging();
        private void StopDraggingPort(Port p) => p.OnStopEdgeDragging();
    }
}
