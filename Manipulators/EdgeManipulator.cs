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
//     public class EdgeManipulator : MouseManipulator
//     {
//         private static readonly int s_StartDragDistance = 10;
//         private readonly List<EdgeDragHelper> m_AdditionalEdgeDragHelpers;
//         private bool m_Active;
//         private Port m_ConnectedPort;
//         private bool m_DetachedFromInputPort;
//         private Port m_DetachedPort;
//         private BaseEdge m_Edge;
//         private MouseDownEvent m_LastMouseDownEvent;
//         private Vector2 m_PressPos;
//
//         public EdgeManipulator()
//         {
//             activators.Add(new() { button = MouseButton.LeftMouse });
//             m_AdditionalEdgeDragHelpers = new();
//             Reset();
//         }
//
//         protected override void RegisterCallbacksOnTarget()
//         {
//             target.RegisterCallback<MouseDownEvent>(OnMouseDown);
//             target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
//             target.RegisterCallback<MouseUpEvent>(OnMouseUp);
//             target.RegisterCallback<KeyDownEvent>(OnKeyDown);
//         }
//
//         protected override void UnregisterCallbacksFromTarget()
//         {
//             target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
//             target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
//             target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
//             target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
//         }
//
//         private void Reset()
//         {
//             m_Active = false;
//             m_Edge = null;
//             m_ConnectedPort = null;
//             m_AdditionalEdgeDragHelpers.Clear();
//             m_DetachedPort = null;
//             m_DetachedFromInputPort = false;
//         }
//
//         protected void OnMouseDown(MouseDownEvent evt)
//         {
//             if (m_Active)
//             {
//                 StopDragging();
//                 evt.StopImmediatePropagation();
//                 return;
//             }
//
//             if (!CanStartManipulation(evt)) { return; }
//
//             m_Edge = evt.target as BaseEdge;
//             m_PressPos = evt.mousePosition;
//             target.CaptureMouse();
//             evt.StopPropagation();
//             m_LastMouseDownEvent = evt;
//         }
//
//         protected void OnMouseMove(MouseMoveEvent evt)
//         {
//             // If the left mouse button is not down then return
//             if (m_Edge == null) { return; }
//             evt.StopPropagation();
//
//             // If one end of the edge is not already detached then
//             if (m_DetachedPort == null)
//             {
//                 // Did we breach the movement threshold to start a drag?
//                 float delta = (evt.mousePosition - m_PressPos).sqrMagnitude;
//                 if (delta < s_StartDragDistance * s_StartDragDistance) { return; }
//
//                 // Determine which end is the nearest to the mouse position then detach it.
//                 Vector2 outputPos = new(m_Edge.Output.GetGlobalCenter().x, m_Edge.Output.GetGlobalCenter().y);
//                 Vector2 inputPos = new(m_Edge.Input.GetGlobalCenter().x, m_Edge.Input.GetGlobalCenter().y);
//
//                 float distanceFromOutput = (m_PressPos - outputPos).sqrMagnitude;
//                 float distanceFromInput = (m_PressPos - inputPos).sqrMagnitude;
//                 m_DetachedFromInputPort = distanceFromInput < distanceFromOutput;
//                 if (m_DetachedFromInputPort)
//                 {
//                     m_ConnectedPort = m_Edge.Output;
//                     m_DetachedPort = m_Edge.Input;
//                 }
//                 else
//                 {
//                     m_ConnectedPort = m_Edge.Input;
//                     m_DetachedPort = m_Edge.Output;
//                 }
//
//                 // Use the edge drag helper of the still connected port
//                 m_AdditionalEdgeDragHelpers.Clear();
//                 if (m_DetachedPort.allowMultiDrag)
//                 {
//                     foreach (BaseEdge edge in m_DetachedPort.Connections)
//                     {
//                         if (edge.Selected)
//                         {
//                             Port draggedPort = m_DetachedFromInputPort ? edge.Output : edge.Input;
//                             EdgeDragHelper edgeDragHelper = draggedPort.PortManipulator.edgeDragHelper;
//                             m_AdditionalEdgeDragHelpers.Add(edgeDragHelper);
//                             edgeDragHelper.HandleDragStart(m_LastMouseDownEvent, edge.GraphView, draggedPort, edge);
//                         }
//                     }
//                 }
//                 else
//                 {
//                     Debug.Log("DOES THIS WORK? SHOULDN'T IT BE m_DetachedPort");
//                     EdgeDragHelper helper = m_ConnectedPort.PortManipulator.edgeDragHelper;
//                     m_AdditionalEdgeDragHelpers.Add(helper);
//                     helper.HandleDragStart(m_LastMouseDownEvent, m_Edge.GraphView, m_ConnectedPort, m_Edge);
//                 }
//                 m_Active = true;
//                 m_LastMouseDownEvent = null;
//             }
//
//             if (m_Active)
//             {
//                 foreach (EdgeDragHelper edgeDrag in m_AdditionalEdgeDragHelpers) { edgeDrag.HandleDragContinue(evt); }
//             }
//         }
//
//         protected void OnMouseUp(MouseUpEvent evt)
//         {
//             DitchFocus();
//             if (CanStopManipulation(evt))
//             {
//                 target.ReleaseMouse();
//                 if (m_Active)
//                 {
//                     foreach (EdgeDragHelper edgeDrag in m_AdditionalEdgeDragHelpers) { edgeDrag.HandleDragEnd(evt); }
//                 }
//                 Reset();
//                 evt.StopPropagation();
//             }
//         }
//
//         protected void OnKeyDown(KeyDownEvent e)
//         {
//             if (!m_Active || e.keyCode != KeyCode.Escape) { return; }
//
//             DitchFocus();
//             StopDragging();
//             e.StopPropagation();
//         }
//
//         private void DitchFocus()
//         {
//             if (target is BaseEdge edge) { edge.GetFirstAncestorOfType<GraphView>().Focus(); }
//         }
//
//         private void StopDragging()
//         {
//             foreach (EdgeDragHelper edgeDrag in m_AdditionalEdgeDragHelpers) { edgeDrag.HandleDragCancel(); }
//
//             Reset();
//             target.ReleaseMouse();
//         }
//     }
// }

