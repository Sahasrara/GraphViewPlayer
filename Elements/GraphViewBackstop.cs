using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    internal class GraphViewBackstop : VisualElement, IDroppable, IDraggable
    {
        private readonly GraphView m_GraphView;
        private readonly Marquee m_Marquee;

        internal GraphViewBackstop(GraphView graphView)
        {
            AddToClassList("backstop");
            m_Marquee = new();
            m_GraphView = graphView;
        }

        #region IDroppable
        public void OnDropEnter(IDropEnterContext context) { }

        public void OnDrop(IDropContext context)
        {
            if (context.GetUserData() is BaseEdge draggedEdge)
            {
                for (int i = 0; i < draggedEdge.DraggedEdges.Count; i++)
                {
                    // Grab dragged edge and the corresponding anchored port
                    BaseEdge edge = draggedEdge.DraggedEdges[i];

                    // Delete real edge
                    if (edge.IsRealEdge()) { m_GraphView.OnEdgeDelete(edge); }

                    // Delete candidate edge
                    else { m_GraphView.RemoveElement(edge); }
                }

                // Reset port highlights
                m_GraphView.IlluminateAllPorts();
            }
        }

        public void OnDropExit(IDropExitContext context) { }
        #endregion

        #region IDraggable
        public void OnDragBegin(IDragBeginContext context)
        {
            if (!CanStartManipulation(context.MouseButton, context.MouseModifiers))
            {
                context.CancelDrag();
                return;
            }

            // Clear selection if this is an exclusive select 
            bool additive = context.MouseModifiers.IsShift();
            bool subtractive = context.MouseModifiers.IsActionKey();
            bool exclusive = !(additive ^ subtractive);
            if (exclusive) { m_GraphView.ClearSelection(); }

            // Create marquee
            m_GraphView.Add(m_Marquee);
            Vector2 position = m_GraphView.WorldToLocal(context.MousePosition);
            m_Marquee.Coordinates = new()
            {
                start = position,
                end = position
            };
        }

        public void OnDrag(IDragContext context)
        {
            m_Marquee.End = m_GraphView.WorldToLocal(context.MousePosition);
        }

        public void OnDragEnd(IDragEndContext context)
        {
            m_Marquee.RemoveFromHierarchy();

            // Ignore if the rectangle is infinitely small
            Rect selectionRect = m_Marquee.SelectionRect;
            if (selectionRect.size == Vector2.zero) { return; }

            bool additive = context.MouseModifiers.IsShift();
            bool subtractive = context.MouseModifiers.IsActionKey();
            bool exclusive = !(additive ^ subtractive);
            foreach (GraphElement element in m_GraphView.ElementsAll)
            {
                Rect localSelRect = m_GraphView.ChangeCoordinatesTo(element, selectionRect);
                if (element.Overlaps(localSelRect)) { element.Selected = exclusive || additive; }
                else if (exclusive) { element.Selected = false; }
            }
        }

        public void OnDragCancel(IDragCancelContext context) { m_Marquee.RemoveFromHierarchy(); }

        private bool CanStartManipulation(MouseButton mouseButton, EventModifiers mouseModifiers)
        {
            if (mouseButton != MouseButton.LeftMouse) { return false; }
            if (mouseModifiers.IsNone()) { return true; }
            if (mouseModifiers.IsExclusiveShift()) { return true; }
            if (mouseModifiers.IsExclusiveActionKey()) { return true; }
            return false;
        }
        #endregion
    }
}
