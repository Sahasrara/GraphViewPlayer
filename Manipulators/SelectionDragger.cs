// // Unity C# reference source
// // Copyright (c) Unity Technologies. For terms of use, see
// // https://unity3d.com/legal/licenses/Unity_Reference_Only_License
//
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
// using UnityEngine.UIElements;
//
// namespace GraphViewPlayer
// {
//     public class SelectionDragger : Dragger
//     {
//         private readonly List<VisualElement> m_DropTargetPickList;
//
//         private readonly Dictionary<GraphElement, OriginalPos> m_OriginalPos;
//         private bool m_Dragging;
//
//         private GraphView m_GraphView;
//         private Vector3 m_ItemPanDiff = Vector3.zero;
//         private Vector2 m_MouseDiff = Vector2.zero;
//         private Vector2 m_originalMouse;
//         private Vector3 m_PanDiff = Vector3.zero;
//
//         private IVisualElementScheduledItem m_PanSchedule;
//         private Vector3 m_PanStart = Vector3.zero;
//         private IDropTarget m_PrevDropTarget;
//
//         private bool m_ShiftClicked;
//         private float m_XScale;
//
//         public SelectionDragger()
//         {
//             // TODO
//             snapEnabled = false; //EditorPrefs.GetBool("GraphSnapping", true);
//             activators.Add(new() { button = MouseButton.LeftMouse });
//             activators.Add(new() { button = MouseButton.LeftMouse, modifiers = EventModifiers.Shift });
//             if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
//             {
//                 activators.Add(new() { button = MouseButton.LeftMouse, modifiers = EventModifiers.Command });
//             }
//             else { activators.Add(new() { button = MouseButton.LeftMouse, modifiers = EventModifiers.Control }); }
//
//             panSpeed = new(1, 1);
//             clampToParentEdges = false;
//             m_DropTargetPickList = new();
//             m_OriginalPos = new();
//         }
//
//         // TODO
//         // Snapper m_Snapper = new Snapper();
//         internal bool snapEnabled { get; set; }
//
//         // selectedElement is used to store a unique selection candidate for cases where user clicks on an item not to
//         // drag it but just to reset the selection -- we only know this after the manipulation has ended
//         private GraphElement selectedElement { get; set; }
//         private GraphElement clickedElement { get; set; }
//
//         private IDropTarget GetDropTargetAt(Vector2 mousePosition, IEnumerable<VisualElement> exclusionList)
//         {
//             Vector2 pickPoint = mousePosition;
//             List<VisualElement> pickList = m_DropTargetPickList;
//             pickList.Clear();
//             target.panel.PickAll(pickPoint, pickList);
//
//             IDropTarget dropTarget = null;
//
//             for (int i = 0; i < pickList.Count; i++)
//             {
//                 if (pickList[i] == target && target != m_GraphView) { continue; }
//
//                 VisualElement picked = pickList[i];
//
//                 dropTarget = picked as IDropTarget;
//
//                 if (dropTarget != null)
//                 {
//                     if (exclusionList.Contains(picked)) { dropTarget = null; }
//                     else { break; }
//                 }
//             }
//
//             return dropTarget;
//         }
//
//         protected override void RegisterCallbacksOnTarget()
//         {
//             ISelector selectorContainer = target as ISelector;
//
//             if (selectorContainer == null)
//             {
//                 throw new InvalidOperationException(
//                     "Manipulator can only be added to a control that supports selection");
//             }
//
//             target.RegisterCallback<MouseDownEvent>(OnMouseDown);
//             target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
//             target.RegisterCallback<MouseUpEvent>(OnMouseUp);
//             target.RegisterCallback<KeyDownEvent>(OnKeyDown);
//             target.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
//             m_Dragging = false;
//         }
//
//         protected override void UnregisterCallbacksFromTarget()
//         {
//             target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
//             target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
//             target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
//             target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
//             target.UnregisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
//         }
//
//         private static void SendDragAndDropEvent(IPlayerDragAndDropEvent evt, List<ISelectable> selection,
//             IDropTarget dropTarget, ISelector dragSource)
//         {
//             if (dropTarget == null) { return; }
//
//             EventBase e = evt as EventBase;
//             if (e.eventTypeId == PlayerDragExitedEvent.TypeId()) { dropTarget.DragExited(); }
//             else if (e.eventTypeId == PlayerDragEnterEvent.TypeId())
//             {
//                 dropTarget.DragEnter(evt as PlayerDragEnterEvent, selection, dropTarget, dragSource);
//             }
//             else if (e.eventTypeId == PlayerDragLeaveEvent.TypeId())
//             {
//                 dropTarget.DragLeave(evt as PlayerDragLeaveEvent, selection, dropTarget, dragSource);
//             }
//
//             if (!dropTarget.CanAcceptDrop(selection)) { return; }
//
//             if (e.eventTypeId == PlayerDragPerformEvent.TypeId())
//             {
//                 dropTarget.DragPerform(evt as PlayerDragPerformEvent, selection, dropTarget, dragSource);
//             }
//             else if (e.eventTypeId == PlayerDragUpdatedEvent.TypeId())
//             {
//                 dropTarget.DragUpdated(evt as PlayerDragUpdatedEvent, selection, dropTarget, dragSource);
//             }
//         }
//
//         private void OnMouseCaptureOutEvent(MouseCaptureOutEvent e)
//         {
//             if (m_Active)
//             {
//                 if (m_PrevDropTarget != null && m_GraphView != null)
//                 {
//                     if (m_PrevDropTarget.CanAcceptDrop(m_GraphView.selection)) { m_PrevDropTarget.DragExited(); }
//                 }
//
//                 // Stop processing the event sequence if the target has lost focus, then.
//                 selectedElement = null;
//                 m_PrevDropTarget = null;
//                 m_Active = false;
// /*TODO
//                 if (snapEnabled)
//                 {
//                     m_Snapper.EndSnap(m_GraphView);
//                 }
// */
//             }
//         }
//
//         protected new void OnMouseDown(MouseDownEvent e)
//         {
//             if (m_Active)
//             {
//                 e.StopImmediatePropagation();
//                 return;
//             }
//
//             if (CanStartManipulation(e))
//             {
//                 m_GraphView = target as GraphView;
//
//                 if (m_GraphView == null) { return; }
//
//                 selectedElement = null;
//
//                 // avoid starting a manipulation on a non movable object
//                 clickedElement = e.target as GraphElement;
//                 if (clickedElement == null)
//                 {
//                     VisualElement ve = e.target as VisualElement;
//                     clickedElement = ve.GetFirstAncestorOfType<GraphElement>();
//                     if (clickedElement == null) { return; }
//                 }
//
//                 // Only start manipulating if the clicked element is movable, selected and that the mouse is in its clickable region (it must be deselected otherwise).
//                 if (!clickedElement.IsMovable() ||
//                     !clickedElement.HitTest(clickedElement.WorldToLocal(e.mousePosition))) { return; }
//
//                 // // If we hit this, this likely because the element has just been unselected
//                 // // It is important for this manipulator to receive the event so the previous one did not stop it
//                 // // but we shouldn't let it propagate to other manipulators to avoid a re-selection
//                 // if (!m_GraphView.selection.Contains(clickedElement))
//                 // {
//                 //     e.StopImmediatePropagation();
//                 //     return;
//                 // }
//
//                 selectedElement = clickedElement;
//
//                 m_OriginalPos.Clear();
//
//                 HashSet<GraphElement> elementsToMove = new(m_GraphView.selection.OfType<GraphElement>());
//
//                 foreach (GraphElement ce in elementsToMove)
//                 {
//                     if (ce == null || !ce.IsMovable()) { continue; }
//
//                     Rect geometry = ce.GetPosition();
//                     Rect geometryInContentViewSpace =
//                         ce.hierarchy.parent.ChangeCoordinatesTo(m_GraphView.contentViewContainer, geometry);
//                     m_OriginalPos[ce] = new()
//                     {
//                         pos = geometryInContentViewSpace,
//                         dragStarted = false
//                     };
//                 }
//
//                 m_originalMouse = e.mousePosition;
//                 m_ItemPanDiff = Vector3.zero;
//                 m_PanStart = m_GraphView.viewTransform.position;
//                 if (m_PanSchedule == null)
//                 {
//                     m_PanSchedule = m_GraphView.schedule
//                         .Execute(Pan)
//                         .Every(PanUtils.k_PanInterval)
//                         .StartingIn(PanUtils.k_PanInterval);
//                     m_PanSchedule.Pause();
//                 }
//
// /*TODO
//                 // Checking if the Graph Element we are moving has the snappable Capability
//                 if (selectedElement.IsSnappable())
//                     m_Snapper.BeginSnap(m_GraphView);
// */
//                 m_Active = true;
//
//                 target.CaptureMouse(); // We want to receive events even when mouse is not over ourself.
//                 e.StopImmediatePropagation();
//             }
//         }
//
// /* TODO
//         void ComputeSnappedRect(ref Rect selectedElementProposedGeom, float scale)
//         {
//             // Let the snapper compute a snapped position using the precomputed position relatively to the geometries of all unselected
//             // GraphElements in the GraphView.contentViewContainer's space.
//             Rect geometryInContentViewContainerSpace = selectedElement.parent.ChangeCoordinatesTo(m_GraphView.contentViewContainer, selectedElementProposedGeom);
//             geometryInContentViewContainerSpace = m_Snapper.GetSnappedRect(geometryInContentViewContainerSpace, scale);
//             // Once the snapped position is computed in the GraphView.contentViewContainer's space then
//             // translate it into the local space of the parent of the selected element.
//             selectedElementProposedGeom = m_GraphView.contentViewContainer.ChangeCoordinatesTo(selectedElement.parent, geometryInContentViewContainerSpace);
//         }
// */
//
//         protected new void OnMouseMove(MouseMoveEvent e)
//         {
//             if (!m_Active) { return; }
//
//             if (m_GraphView == null) { return; }
//
//             VisualElement ve = (VisualElement)e.target;
//             Vector2 gvMousePos = ve.ChangeCoordinatesTo(m_GraphView.contentContainer, e.localMousePosition);
//             m_PanDiff = PanUtils.GetEffectivePanSpeed(m_GraphView, gvMousePos);
//
//             if (m_PanDiff != Vector3.zero) { m_PanSchedule.Resume(); }
//             else { m_PanSchedule.Pause(); }
//
//             // We need to monitor the mouse diff "by hand" because we stop positioning the graph elements once the
//             // mouse has gone out.
//             m_MouseDiff = m_originalMouse - e.mousePosition;
//
//             // Handle the selected element
//             Rect selectedElementGeom = GetSelectedElementGeom();
//
//             m_ShiftClicked = e.shiftKey;
// /* TODO
//             if (snapEnabled && !m_ShiftClicked)
//             {
//                 ComputeSnappedRect(ref selectedElementGeom, m_XScale);
//             }
//
//             if (snapEnabled && m_ShiftClicked)
//             {
//                 m_Snapper.ClearSnapLines();
//             }
// */
//             foreach (KeyValuePair<GraphElement, OriginalPos> v in m_OriginalPos)
//             {
//                 GraphElement ce = v.Key;
//
//                 // Protect against stale visual elements that have been deparented since the start of the manipulation
//                 if (ce.hierarchy.parent == null) { continue; }
//
//                 if (!v.Value.dragStarted) { v.Value.dragStarted = true; }
//
//                 SnapOrMoveElement(v, selectedElementGeom);
//             }
//
//             List<ISelectable> selection = m_GraphView.selection;
//
//             // TODO: Replace with a temp drawing or something...maybe manipulator could fake position
//             // all this to let operation know which element sits under cursor...or is there another way to draw stuff that is being dragged?
//
//             IDropTarget dropTarget = GetDropTargetAt(e.mousePosition, selection.OfType<VisualElement>());
//
//             if (m_PrevDropTarget != dropTarget)
//             {
//                 if (m_PrevDropTarget != null)
//                 {
//                     using (PlayerDragLeaveEvent eexit = PlayerDragLeaveEvent.GetPooled(e))
//                     {
//                         SendDragAndDropEvent(eexit, selection, m_PrevDropTarget, m_GraphView);
//                     }
//                 }
//
//                 using (PlayerDragEnterEvent eenter = PlayerDragEnterEvent.GetPooled(e))
//                 {
//                     SendDragAndDropEvent(eenter, selection, dropTarget, m_GraphView);
//                 }
//             }
//
//             using (PlayerDragUpdatedEvent eupdated = PlayerDragUpdatedEvent.GetPooled(e))
//             {
//                 SendDragAndDropEvent(eupdated, selection, dropTarget, m_GraphView);
//             }
//
//             m_PrevDropTarget = dropTarget;
//
//             m_Dragging = true;
//             e.StopPropagation();
//         }
//
//         private void Pan(TimerState ts)
//         {
//             m_GraphView.UpdateViewTransform(m_GraphView.viewTransform.position - m_PanDiff);
//             m_ItemPanDiff += m_PanDiff;
//
//             // Handle the selected element
//             Rect selectedElementGeom = GetSelectedElementGeom();
// /*TODO
//             if (snapEnabled && !m_ShiftClicked)
//             {
//                 ComputeSnappedRect(ref selectedElementGeom, m_XScale);
//             }
// */
//             foreach (KeyValuePair<GraphElement, OriginalPos> v in m_OriginalPos)
//             {
//                 SnapOrMoveElement(v, selectedElementGeom);
//             }
//         }
//
//         private void SnapOrMoveElement(KeyValuePair<GraphElement, OriginalPos> v, Rect selectedElementGeom)
//         {
//             GraphElement ce = v.Key;
//
//             // TODO
//             // if (EditorPrefs.GetBool("GraphSnapping"))
//             // {
//             //     Vector2 geomDiff = selectedElementGeom.position - m_OriginalPos[selectedElement].pos.position;
//             //     Rect ceLayout = ce.GetPosition();
//             //     ce.SetPosition(new Rect(v.Value.pos.x + geomDiff.x, v.Value.pos.y + geomDiff.y, ceLayout.width, ceLayout.height));
//             // }
//             // else
//             // {
//             MoveElement(ce, v.Value.pos);
//
//             // }
//         }
//
//         private Rect GetSelectedElementGeom()
//         {
//             // Handle the selected element
//             Matrix4x4 g = selectedElement.worldTransform;
//             m_XScale = g.m00; //The scale on x is equal to the scale on y because the graphview is not distorted
//             Rect selectedElementGeom = m_OriginalPos[selectedElement].pos;
//
//             // Compute the new position of the selected element using the mouse delta position and panning info
//             selectedElementGeom.x = selectedElementGeom.x - (m_MouseDiff.x - m_ItemPanDiff.x) * panSpeed.x / m_XScale;
//             selectedElementGeom.y = selectedElementGeom.y - (m_MouseDiff.y - m_ItemPanDiff.y) * panSpeed.y / m_XScale;
//             return selectedElementGeom;
//         }
//
//         private void MoveElement(GraphElement element, Rect originalPos)
//         {
//             Matrix4x4 g = element.worldTransform;
//             Vector3 scale = new(g.m00, g.m11, g.m22);
//
//             Rect newPos = new(0, 0, originalPos.width, originalPos.height);
//
//             // Compute the new position of the selected element using the mouse delta position and panning info
//             newPos.x = originalPos.x -
//                        (m_MouseDiff.x - m_ItemPanDiff.x) * panSpeed.x / scale.x * element.transform.scale.x;
//             newPos.y = originalPos.y -
//                        (m_MouseDiff.y - m_ItemPanDiff.y) * panSpeed.y / scale.y * element.transform.scale.y;
//
//             element.SetPosition(m_GraphView.contentViewContainer.ChangeCoordinatesTo(element.hierarchy.parent, newPos));
//         }
//
//         protected new void OnMouseUp(MouseUpEvent evt)
//         {
//             if (m_GraphView == null)
//             {
//                 if (m_Active)
//                 {
//                     target.ReleaseMouse();
//                     selectedElement = null;
//                     m_Active = false;
//                     m_Dragging = false;
//                     m_PrevDropTarget = null;
//                 }
//
//                 return;
//             }
//
//             List<ISelectable> selection = m_GraphView.selection;
//
//             if (CanStopManipulation(evt))
//             {
//                 if (m_Active)
//                 {
//                     if (m_Dragging)
//
//                         // Notify Listeners
//                     {
//                         if (target is GraphView graphView)
//                         {
//                             foreach (GraphElement graphElement in m_OriginalPos.Keys)
//                             {
//                                 graphView.OnElementMoved(graphElement);
//                             }
//                         }
//                     }
//
//                     m_PanSchedule.Pause();
//
//                     if (m_ItemPanDiff != Vector3.zero)
//                     {
//                         Vector3 p = m_GraphView.contentViewContainer.transform.position;
//                         Vector3 s = m_GraphView.contentViewContainer.transform.scale;
//                         m_GraphView.UpdateViewTransform(p, s);
//                     }
//
//                     if (selection.Count > 0 && m_PrevDropTarget != null)
//                     {
//                         if (m_PrevDropTarget.CanAcceptDrop(selection))
//                         {
//                             using (PlayerDragPerformEvent drop = PlayerDragPerformEvent.GetPooled(evt))
//                             {
//                                 SendDragAndDropEvent(drop, selection, m_PrevDropTarget, m_GraphView);
//                             }
//                         }
//                         else
//                         {
//                             using (PlayerDragExitedEvent dexit = PlayerDragExitedEvent.GetPooled(evt))
//                             {
//                                 SendDragAndDropEvent(dexit, selection, m_PrevDropTarget, m_GraphView);
//                             }
//                         }
//                     }
//
// /*TODO
//                     if (snapEnabled)
//                         m_Snapper.EndSnap(m_GraphView);
// */
//                     target.ReleaseMouse();
//                     evt.StopPropagation();
//                 }
//
//                 selectedElement = null;
//                 m_Active = false;
//                 m_PrevDropTarget = null;
//                 m_Dragging = false;
//                 m_PrevDropTarget = null;
//             }
//         }
//
//         private void OnKeyDown(KeyDownEvent e)
//         {
//             if (e.keyCode != KeyCode.Escape || m_GraphView == null || !m_Active) { return; }
//
//             // Reset the items to their original pos.
//             foreach (KeyValuePair<GraphElement, OriginalPos> v in m_OriginalPos) { v.Key.SetPosition(v.Value.pos); }
//
//             // Reset from pan
//             m_PanSchedule.Pause();
//             if (m_ItemPanDiff != Vector3.zero) { m_GraphView.UpdateViewTransform(m_PanStart); }
//
//             using (PlayerDragExitedEvent dexit = PlayerDragExitedEvent.GetPooled())
//             {
//                 List<ISelectable> selection = m_GraphView.selection;
//                 SendDragAndDropEvent(dexit, selection, m_PrevDropTarget, m_GraphView);
//             }
//
//             target.ReleaseMouse();
//             e.StopPropagation();
//         }
//
//         private class OriginalPos
//         {
//             public bool dragStarted;
//             public Rect pos;
//         }
//     }
// }
//

