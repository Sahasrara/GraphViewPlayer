// // Unity C# reference source
// // Copyright (c) Unity Technologies. For terms of use, see
// // https://unity3d.com/legal/licenses/Unity_Reference_Only_License
//
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UIElements;
//
// namespace GraphViewPlayer
// {
//     public abstract class EdgeDragHelper
//     {
//         public abstract void HandleDragStart(MouseDownEvent evt, GraphView graphView, Port port, BaseEdge edge);
//         public abstract void HandleDragContinue(MouseMoveEvent evt);
//         public abstract void HandleDragEnd(MouseUpEvent evt);
//         public abstract void HandleDragCancel();
//     }
//
//     public class EdgeDragHelper<TEdge> : EdgeDragHelper where TEdge : BaseEdge, new()
//     {
//         protected List<Port> m_CompatiblePorts;
//         protected Port m_DraggedPort;
//         protected BaseEdge m_EdgeCandidate;
//         protected BaseEdge m_GhostEdge;
//         protected GraphView m_GraphView;
//         private Vector3 m_PanDiff = Vector3.zero;
//         private IVisualElementScheduledItem m_PanSchedule;
//         private Vector3 m_PanStart = Vector3.zero;
//         private bool m_WasPanned;
//
//         public EdgeDragHelper()
//         {
//             resetPositionOnPan = true;
//             m_CompatiblePorts = new();
//         }
//
//         public bool resetPositionOnPan { get; set; }
//
//         public override void HandleDragStart(MouseDownEvent evt, GraphView graphView, Port port, BaseEdge edge)
//         {
//             // Update dragged port, edge candidate, and graph view
//             m_DraggedPort = port;
//             m_EdgeCandidate = edge;
//             m_GraphView = graphView;
//
//             // Update edge drawing
//             if (m_DraggedPort.Direction == Direction.Output)
//             {
//                 m_EdgeCandidate.SetInputPositionOverride(evt.mousePosition);
//             }
//             else { m_EdgeCandidate.SetOutputPositionOverride(evt.mousePosition); }
//
//             // Only light compatible anchors when dragging an edge.
//             m_GraphView.GetCompatiblePorts(m_CompatiblePorts, m_DraggedPort);
//             m_GraphView.Ports.ForEach(StartDraggingPort); // Don't Highlight
//             m_CompatiblePorts.ForEach(StopDraggingPort); // Highlight
//
//             // Setup for panning
//             m_PanStart = m_GraphView.viewTransform.position;
//             if (m_PanSchedule == null)
//             {
//                 m_PanSchedule = m_GraphView.schedule
//                     .Execute(Pan)
//                     .Every(PanUtils.k_PanInterval)
//                     .StartingIn(PanUtils.k_PanInterval);
//                 m_PanSchedule.Pause();
//             }
//             m_WasPanned = false;
//
//             // Set edge candidate layer and add to graph 
//             m_EdgeCandidate.Layer = int.MaxValue;
//             m_GraphView.AddElement(m_EdgeCandidate);
//         }
//
//         public override void HandleDragContinue(MouseMoveEvent evt)
//         {
//             // Panning
//             VisualElement ve = (VisualElement)evt.target;
//             Vector2 gvMousePos = ve.ChangeCoordinatesTo(m_GraphView.contentContainer, evt.localMousePosition);
//             m_PanDiff = PanUtils.GetEffectivePanSpeed(m_GraphView, gvMousePos);
//             if (m_PanDiff != Vector3.zero) { m_PanSchedule.Resume(); }
//             else { m_PanSchedule.Pause(); }
//
//             // Update edge drawing
//             Vector2 mousePosition = evt.mousePosition;
//             if (m_DraggedPort.Direction == Direction.Output)
//             {
//                 m_EdgeCandidate.SetInputPositionOverride(mousePosition);
//             }
//             else { m_EdgeCandidate.SetOutputPositionOverride(mousePosition); }
//
//             // Draw ghost edge if possible port exists.
//             Port endPort = GetEndPort(mousePosition);
//             if (endPort != null)
//             {
//                 if (m_GhostEdge == null)
//                 {
//                     m_GhostEdge = new TEdge();
//                     m_GhostEdge.IsGhostEdge = true;
//                     m_GhostEdge.pickingMode = PickingMode.Ignore;
//                     m_GraphView.AddElement(m_GhostEdge);
//                 }
//
//                 if (m_EdgeCandidate.InputPositionOverridden)
//                 {
//                     m_GhostEdge.Input = endPort;
//                     m_GhostEdge.Output = m_EdgeCandidate.Output;
//                 }
//                 else
//                 {
//                     m_GhostEdge.Input = m_EdgeCandidate.Input;
//                     m_GhostEdge.Output = endPort;
//                 }
//             }
//             else { CleanGhostEdge(); }
//         }
//
//         public override void HandleDragEnd(MouseUpEvent evt)
//         {
//             // Did mouse release on port?
//             Port endPort = GetEndPort(evt.mousePosition);
//
//             // This is a connection
//             bool edgeCandidateWasExistingEdge = m_EdgeCandidate.Input != null && m_EdgeCandidate.Output != null;
//             if (endPort != null)
//             {
//                 // Find new input/output ports
//                 Port inputPort;
//                 Port outputPort;
//                 if (endPort.Direction == Direction.Output)
//                 {
//                     inputPort = m_EdgeCandidate.Input;
//                     outputPort = endPort;
//                 }
//                 else
//                 {
//                     inputPort = endPort;
//                     outputPort = m_EdgeCandidate.Output;
//                 }
//
//                 if (edgeCandidateWasExistingEdge)
//                 {
//                     // Existing edge is being deleted and connected elsewhere so request a deletion 
//                     m_GraphView.OnEdgeDelete(m_EdgeCandidate);
//
//                     // TODO: delete this, should be handled by OnEdgeDelete
//                     m_EdgeCandidate.Disconnect();
//                 }
//
//                 // Set ports on the new edge
//                 m_EdgeCandidate.Input = inputPort;
//                 m_EdgeCandidate.Output = outputPort;
//
//                 // Create new edge
//                 m_GraphView.OnEdgeCreate(m_EdgeCandidate);
//
//                 // TODO: remove what's below. OnEdgeCreate should tackle that.
//                 m_GraphView.AddElement(m_EdgeCandidate);
//             }
//
//             // This is a disconnection
//             else
//             {
//                 // This was an existing edge and so much be deleted 
//                 if (edgeCandidateWasExistingEdge)
//                 {
//                     m_GraphView.OnEdgeDelete(m_EdgeCandidate);
//
//                     // TODO
//                     m_EdgeCandidate.Disconnect();
//                 }
//             }
//             HandleDragCancel();
//         }
//
//         public override void HandleDragCancel()
//         {
//             // Reset the highlights.
//             m_GraphView?.Ports.ForEach(StopDraggingPort);
//
//             // Clear Compatible Ports
//             m_CompatiblePorts.Clear();
//
//             // Clean up ghost edge.
//             CleanGhostEdge();
//
//             // Reset view if we didn't connect, or we always want a reset
//             m_PanSchedule?.Pause();
//             if (m_WasPanned && resetPositionOnPan)
//             {
//                 m_GraphView.UpdateViewTransform(m_PanStart, m_GraphView.viewTransform.scale);
//             }
//
//             // Clean up candidate edges
//             if (m_EdgeCandidate != null)
//             {
//                 m_EdgeCandidate.UnsetPositionOverrides();
//                 m_EdgeCandidate.ResetLayer();
//
//                 // This was only an edge candidate, not previously used as a real edge
//                 if (m_EdgeCandidate.Input == null || m_EdgeCandidate.Output == null)
//                 {
//                     m_EdgeCandidate.Disconnect();
//                     m_GraphView.RemoveElement(m_EdgeCandidate);
//                 }
//             }
//
//             m_DraggedPort = null;
//             m_EdgeCandidate = null;
//             m_GraphView = null;
//         }
//
//         private void Pan(TimerState ts)
//         {
//             m_GraphView.UpdateViewTransform(m_GraphView.viewTransform.position - m_PanDiff);
//             m_EdgeCandidate.ForceUpdateEdgeControl();
//             m_WasPanned = true;
//         }
//
//         private Port GetEndPort(Vector2 mousePosition)
//         {
//             if (m_GraphView == null) { return null; }
//
//             Port endPort = null;
//
//             foreach (Port compatiblePort in m_CompatiblePorts)
//             {
//                 Rect bounds = compatiblePort.worldBound;
//                 float hitboxExtraPadding = bounds.height;
//
//                 if (compatiblePort.Orientation == Orientation.Horizontal)
//                 {
//                     // Add extra padding for mouse check to the left of input port or right of output port.
//                     if (compatiblePort.Direction == Direction.Input)
//                     {
//                         // Move bounds to the left by hitboxExtraPadding and increase width
//                         // by hitboxExtraPadding.
//                         bounds.x -= hitboxExtraPadding;
//                         bounds.width += hitboxExtraPadding;
//                     }
//                     else if (compatiblePort.Direction == Direction.Output)
//                     {
//                         // Just add hitboxExtraPadding to the width.
//                         bounds.width += hitboxExtraPadding;
//                     }
//                 }
//
//                 // Check if mouse is over port.
//                 if (bounds.Contains(mousePosition))
//                 {
//                     endPort = compatiblePort;
//                     break;
//                 }
//             }
//
//             return endPort;
//         }
//
//         private void CleanGhostEdge()
//         {
//             if (m_GhostEdge != null)
//             {
//                 // TODO pool these? Or just ditch them entirely???
//                 m_GraphView?.RemoveElement(m_GhostEdge);
//                 m_GhostEdge.Input = null;
//                 m_GhostEdge.Output = null;
//                 m_GhostEdge = null;
//             }
//         }
//
//         private void StartDraggingPort(Port p) => p.OnStartEdgeDragging();
//         private void StopDraggingPort(Port p) => p.OnStopEdgeDragging();
//     }
// }

