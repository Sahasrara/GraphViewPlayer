using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class Selection : GraphElement //  IPositionable
    {
        private readonly GraphView m_GraphView;
        private readonly List<GraphElement> m_Selection;
        private bool m_BreachedThreshold;

        public Selection(GraphView graphView)
        {
            ClearClassList();
            AddToClassList("selection");
            m_GraphView = graphView;
            m_Selection = new();
            Capabilities = Capabilities.Selectable;
            Layer = int.MinValue;
            visible = false;
        }

        #region GraphElement
        public override bool Selected
        {
            get => false;
            set { }
        }
        #endregion

        #region Intersection
        private GraphElement m_LastPicked;

        public override bool ContainsPoint(Vector2 localPoint)
        {
            foreach (GraphElement ge in m_GraphView.ElementsAll)
            {
                if (ge.ContainsPoint(localPoint))
                {
                    m_LastPicked = ge;
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Event Handlers
        [EventInterest(typeof(DragOfferEvent), typeof(DragBeginEvent), typeof(DragEvent), typeof(DragEndEvent),
            typeof(DragCancelEvent))]
        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);
            if (evt.eventTypeId == DragOfferEvent.TypeId()) { OnDragOffer((DragOfferEvent)evt); }
            else if (evt.eventTypeId == DragBeginEvent.TypeId()) { OnDragBegin((DragBeginEvent)evt); }
            else if (evt.eventTypeId == DragEvent.TypeId()) { OnDrag((DragEvent)evt); }
            else if (evt.eventTypeId == DragEndEvent.TypeId()) { OnDragEnd((DragEndEvent)evt); }
            else if (evt.eventTypeId == DragCancelEvent.TypeId()) { OnDragCancel((DragCancelEvent)evt); }
        }

        private void OnDragOffer(DragOfferEvent e)
        {
            // Check if selection can handle drag 
            if (m_LastPicked == null
                || m_LastPicked.parent == null
                || !m_LastPicked.Selected
                || !m_LastPicked.CanHandleSelectionDrag(e)) { return; }

            // Swallow event
            e.StopImmediatePropagation();

            // Accept drag
            e.AcceptDrag(this);

            // Initialize drag
            m_LastPicked.InitializeSelectionDrag(e);

            // Initialize selection
            if (m_LastPicked is Node) { m_Selection.AddRange(m_GraphView.NodesSelected); }
            else { m_Selection.AddRange(m_GraphView.EdgesSelected); }
        }

        private void OnDragBegin(DragBeginEvent e) { }

        private void OnDrag(DragEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();

            // Sanity
            if (m_LastPicked == null || m_LastPicked.parent == null)
            {
                e.CancelDrag();
                return;
            }

            // First drag setup
            if (!m_BreachedThreshold)
            {
                // Track the selected element 
                m_GraphView.TrackElementForPan(m_LastPicked);
                m_BreachedThreshold = true;
            }

            // Drag
            foreach (GraphElement element in m_Selection) { element.HandleSelectionDrag(e); }
        }

        private void OnDragEnd(DragEndEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();

            // Sanity
            if (m_LastPicked == null || m_LastPicked.parent == null)
            {
                e.CancelDrag();
                Reset();
                return;
            }

            // Drag End
            foreach (GraphElement element in m_Selection) { element.HandleSelectionDragEnd(e); }

            // Reset
            Reset();
        }

        private void OnDragCancel(DragCancelEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();

            // Stop panning
            Vector2 panDiff = m_GraphView.UntrackElementForPan(m_LastPicked, true);

            // Drag Cancel 
            foreach (GraphElement element in m_Selection) { element.HandleSelectionDragCancel(e, panDiff); }

            // Reset
            Reset();
        }

        private void Reset()
        {
            m_Selection.Clear();
            m_LastPicked = null;
            m_BreachedThreshold = false;
        }
        #endregion

        #region Position
        public override event Action<PositionData> OnPositionChange;
        public override Vector2 GetCenter() => throw new NotImplementedException();
        public override Vector2 GetPosition() => throw new NotImplementedException();
        public override void SetPosition(Vector2 position) { }

        public override Vector2 GetGlobalCenter()
        {
            if (m_LastPicked == null) { throw new("Tried to get center of empty selection"); }
            return m_LastPicked.GetGlobalCenter();
        }

        public override void ApplyDeltaToPosition(Vector2 delta)
        {
            if (m_LastPicked is Node)
            {
                foreach (Node selectedNode in m_GraphView.NodesSelected) { selectedNode.ApplyDeltaToPosition(delta); }
            }
            else
            {
                foreach (BaseEdge selectedEdge in m_GraphView.EdgesSelected)
                {
                    selectedEdge.ApplyDeltaToPosition(delta);
                }
            }
        }

        public override bool CanHandleSelectionDrag(DragOfferEvent e) => throw new NotImplementedException();
        public override void InitializeSelectionDrag(DragOfferEvent e) { throw new NotImplementedException(); }
        public override void HandleSelectionDrag(DragEvent e) { throw new NotImplementedException(); }
        public override void HandleSelectionDragEnd(DragEndEvent e) { throw new NotImplementedException(); }

        public override void HandleSelectionDragCancel(DragCancelEvent e, Vector2 panDiff)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

    #region Draggable Selection
    public interface IDraggableSelection
    {
        bool CanHandleSelectionDrag(DragOfferEvent e);
        void InitializeSelectionDrag(DragOfferEvent e);
        void HandleSelectionDrag(DragEvent e);
        void HandleSelectionDragEnd(DragEndEvent e);
        void HandleSelectionDragCancel(DragCancelEvent e, Vector2 panDiff);
    }
    #endregion
}