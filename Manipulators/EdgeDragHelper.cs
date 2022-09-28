// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public abstract class EdgeDragHelper
    {
        public abstract Edge edgeCandidate { get; set; }
        public abstract Port draggedPort { get; set; }
        public abstract bool HandleMouseDown(MouseDownEvent evt);
        public abstract void HandleMouseMove(MouseMoveEvent evt);
        public abstract void HandleMouseUp(MouseUpEvent evt);
        public abstract void Reset(bool didConnect = false);

        internal const int k_PanAreaWidth = 100;
        internal const int k_PanSpeed = 4;
        internal const int k_PanInterval = 10;
        internal const float k_MinSpeedFactor = 0.5f;
        internal const float k_MaxSpeedFactor = 2.5f;
        internal const float k_MaxPanSpeed = k_MaxSpeedFactor * k_PanSpeed;
    }

    public class EdgeDragHelper<TEdge> : EdgeDragHelper where TEdge : Edge, new()
    {
        protected List<Port> m_CompatiblePorts;
        private Edge m_GhostEdge;
        protected GraphView m_GraphView;

        private IVisualElementScheduledItem m_PanSchedule;
        private Vector3 m_PanDiff = Vector3.zero;
        private bool m_WasPanned;

        public bool resetPositionOnPan { get; set; }

        public EdgeDragHelper()
        {
            resetPositionOnPan = true;
            m_CompatiblePorts = new();
            Reset();
        }

        public override Edge edgeCandidate { get; set; }

        public override Port draggedPort { get; set; }

        public override void Reset(bool didConnect = false)
        {
            if (m_CompatiblePorts.Count() > 0)
            {
                // Reset the highlights.
                m_GraphView.ports.ForEach((p) => {
                    p.OnStopEdgeDragging();
                });

                // Clear Compatible Ports
                m_CompatiblePorts.Clear();
            }

            // Clean up ghost edge.
            CleanGhostEdge();

            if (m_WasPanned)
            {
                if (!resetPositionOnPan || didConnect)
                {
                    m_GraphView.UpdateViewTransform(
                        m_GraphView.viewTransform.position, m_GraphView.viewTransform.scale);
                }
            }

            if (m_PanSchedule != null)
                m_PanSchedule.Pause();

            if (draggedPort != null && !didConnect)
            {
                draggedPort = null;
            }

            if (edgeCandidate != null)
            {
                m_GraphView.RemoveElement(edgeCandidate);
                edgeCandidate.input = null;
                edgeCandidate.output = null;
                edgeCandidate = null;
            }

            m_GraphView = null;
        }

        public override bool HandleMouseDown(MouseDownEvent evt)
        {
            Vector2 mousePosition = evt.mousePosition;

            if ((draggedPort == null) || (edgeCandidate == null))
            {
                return false;
            }

            m_GraphView = draggedPort.GetFirstAncestorOfType<GraphView>();

            if (m_GraphView == null)
            {
                return false;
            }

            if (edgeCandidate.parent == null)
            {
                m_GraphView.AddElement(edgeCandidate);
            }

            bool startFromOutput = (draggedPort.direction == Direction.Output);

            edgeCandidate.candidatePosition = mousePosition;

            if (startFromOutput)
            {
                edgeCandidate.output = draggedPort;
                edgeCandidate.input = null;
            }
            else
            {
                edgeCandidate.output = null;
                edgeCandidate.input = draggedPort;
            }

            m_GraphView.GetCompatiblePorts(m_CompatiblePorts, draggedPort);

            // Only light compatible anchors when dragging an edge.
            m_GraphView.ports.ForEach((p) =>  {
                p.OnStartEdgeDragging();
            });

            foreach (Port compatiblePort in m_CompatiblePorts)
            {
                compatiblePort.highlight = true;
            }

            edgeCandidate.UpdateEdgeControl();

            if (m_PanSchedule == null)
            {
                m_PanSchedule = m_GraphView.schedule.Execute(Pan).Every(k_PanInterval).StartingIn(k_PanInterval);
                m_PanSchedule.Pause();
            }
            m_WasPanned = false;

            edgeCandidate.layer = Int32.MaxValue;

            return true;
        }

        internal Vector2 GetEffectivePanSpeed(Vector2 mousePos)
        {
            Vector2 effectiveSpeed = Vector2.zero;

            if (mousePos.x <= k_PanAreaWidth)
                effectiveSpeed.x = -(((k_PanAreaWidth - mousePos.x) / k_PanAreaWidth) + 0.5f) * k_PanSpeed;
            else if (mousePos.x >= m_GraphView.contentContainer.layout.width - k_PanAreaWidth)
                effectiveSpeed.x = (((mousePos.x - (m_GraphView.contentContainer.layout.width - k_PanAreaWidth)) / k_PanAreaWidth) + 0.5f) * k_PanSpeed;

            if (mousePos.y <= k_PanAreaWidth)
                effectiveSpeed.y = -(((k_PanAreaWidth - mousePos.y) / k_PanAreaWidth) + 0.5f) * k_PanSpeed;
            else if (mousePos.y >= m_GraphView.contentContainer.layout.height - k_PanAreaWidth)
                effectiveSpeed.y = (((mousePos.y - (m_GraphView.contentContainer.layout.height - k_PanAreaWidth)) / k_PanAreaWidth) + 0.5f) * k_PanSpeed;

            effectiveSpeed = Vector2.ClampMagnitude(effectiveSpeed, k_MaxPanSpeed);

            return effectiveSpeed;
        }

        public override void HandleMouseMove(MouseMoveEvent evt)
        {
            var ve = (VisualElement)evt.target;
            Vector2 gvMousePos = ve.ChangeCoordinatesTo(m_GraphView.contentContainer, evt.localMousePosition);
            m_PanDiff = GetEffectivePanSpeed(gvMousePos);

            if (m_PanDiff != Vector3.zero)
            {
                m_PanSchedule.Resume();
            }
            else
            {
                m_PanSchedule.Pause();
            }

            Vector2 mousePosition = evt.mousePosition;

            edgeCandidate.candidatePosition = mousePosition;

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

                if (edgeCandidate.output == null)
                {
                    m_GhostEdge.input = edgeCandidate.input;
                    m_GhostEdge.output = endPort;
                }
                else
                {
                    m_GhostEdge.input = endPort;
                    m_GhostEdge.output = edgeCandidate.output;
                }
            }
            else CleanGhostEdge();
        }

        private void Pan(TimerState ts)
        {
            m_GraphView.viewTransform.position -= m_PanDiff;
            edgeCandidate.ForceUpdateEdgeControl();
            m_WasPanned = true;
        }

        public override void HandleMouseUp(MouseUpEvent evt)
        {
            Vector2 mousePosition = evt.mousePosition;

            // Reset the highlights.
            m_GraphView.ports.ForEach((p) => {
                p.OnStopEdgeDragging();
            });

            // Clean up ghost edges.
            CleanGhostEdge();

            Port endPort = GetEndPort(mousePosition);

            // If it is an existing valid edge then delete and notify the model (using DeleteElements()).
            if (edgeCandidate.input != null && edgeCandidate.output != null)
            {
                // Save the current input and output before deleting the edge as they will be reset
                Port oldInput = edgeCandidate.input;
                Port oldOutput = edgeCandidate.output;
            
                Debug.Log("HERE");
                // m_GraphView.OnEdgeDelete(edgeCandidate);
                edgeCandidate.Disconnect();
                m_GraphView.RemoveElement(edgeCandidate);
            
                // Restore the previous input and output
                edgeCandidate = new TEdge();
                edgeCandidate.input = oldInput;
                edgeCandidate.output = oldOutput;
            }
            // otherwise, if it is an temporary edge then just remove it as it is not already known by the model
            else
            {
                m_GraphView.RemoveElement(edgeCandidate);
            }

            bool didConnect;
            // This is a connection
            if (endPort != null)
            {
                if (endPort.direction == Direction.Output)
                    edgeCandidate.output = endPort;
                else
                    edgeCandidate.input = endPort;

                // Notify 
                m_GraphView.AddElement(edgeCandidate);
                Debug.Log("NEW CONNECTION");
                // m_GraphView.OnEdgeCreate(edgeCandidate);
                
                didConnect = true;
            }
            // This is a disconnection
            else
            {
                edgeCandidate.output = null;
                edgeCandidate.input = null;
                didConnect = false;
            }

            edgeCandidate.ResetLayer();
            edgeCandidate = null;
            m_CompatiblePorts.Clear();
            Reset(didConnect);
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
    }
}
