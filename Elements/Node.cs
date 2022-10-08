// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class Node : GraphElement
    {
        public Node()
        {
            // Root Container
            MainContainer = this;

            // Title Label
            TitleLabel = new() { pickingMode = PickingMode.Ignore };
            TitleLabel.AddToClassList("node-title-label");

            // Title Container
            TitleContainer = new() { pickingMode = PickingMode.Ignore };
            TitleContainer.AddToClassList("node-title");
            TitleContainer.Add(TitleLabel);
            hierarchy.Add(TitleContainer);

            // Input Container
            InputContainer = new() { pickingMode = PickingMode.Ignore };
            InputContainer.AddToClassList("node-io-input");

            // Output Container
            OutputContainer = new() { pickingMode = PickingMode.Ignore };
            OutputContainer.AddToClassList("node-io-output");

            // Top Container 
            TopContainer = new() { pickingMode = PickingMode.Ignore };
            TopContainer.AddToClassList("node-io");
            TopContainer.Add(InputContainer);
            TopContainer.Add(OutputContainer);
            hierarchy.Add(TopContainer);

            // Extension Container
            ExtensionContainer = new() { pickingMode = PickingMode.Ignore };
            ExtensionContainer.AddToClassList("node-extension");
            hierarchy.Add(ExtensionContainer);

            // Style
            AddToClassList("node");

            // Capability
            Capabilities |= Capabilities.Selectable
                            | Capabilities.Movable
                            | Capabilities.Deletable
                            | Capabilities.Ascendable
                ;
            usageHints = UsageHints.DynamicTransform;
        }

        protected Label TitleLabel { get; }
        protected VisualElement MainContainer { get; }
        protected VisualElement TitleContainer { get; }
        protected VisualElement TopContainer { get; }
        protected VisualElement InputContainer { get; }
        protected VisualElement OutputContainer { get; }
        public VisualElement ExtensionContainer { get; }

        public override string Title
        {
            get => TitleLabel != null ? TitleLabel.text : string.Empty;
            set
            {
                if (TitleLabel != null) { TitleLabel.text = value; }
            }
        }

        public override bool Selected
        {
            get => base.Selected;
            set
            {
                if (base.Selected == value) { return; }
                base.Selected = value;
                if (value) { AddToClassList("node-selected"); }
                else { RemoveFromClassList("node-selected"); }
            }
        }

        #region Event Handlers
        protected override void OnRemovedFromGraphView()
        {
            base.OnRemovedFromGraphView();
            Selected = false;
            pickingMode = PickingMode.Position;
        }
        #endregion

        #region Ports
        public virtual void AddPort(BasePort port)
        {
            port.ParentNode = this;
            if (port.Direction == Direction.Input) { InputContainer.Add(port); }
            else { OutputContainer.Add(port); }
        }
        #endregion

        #region Position
        public override void SetPosition(Vector2 newPosition)
        {
            base.SetPosition(newPosition);
            Graph?.OnNodeMoved(this);
        }
        #endregion

        #region Drag Events
        [EventInterest(typeof(DragOfferEvent), typeof(DragEvent), typeof(DragEndEvent), typeof(DragCancelEvent))]
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
            // Check if this is a node drag event 
            if (!IsNodeDrag(e) || !IsMovable()) { return; }
            e.StopImmediatePropagation();

            // Accept Drag
            e.AcceptDrag(this);
        }

        private void OnDragBegin(DragBeginEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();

            // Ignore picking
            pickingMode = PickingMode.Ignore;

            // Track for panning
            Graph.TrackElementForPan(this);
        }

        private void OnDrag(DragEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();

            // Handle drag
            foreach (Node node in Graph.NodesSelected)
            {
                node.SetPosition(node.GetPosition() + e.mouseDelta / Graph.CurrentScale);
            }
        }

        private void OnDragEnd(DragEndEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();

            // Untrack for panning
            Graph.UntrackElementForPan(this);

            // Reset picking mode
            foreach (Node node in Graph.NodesSelected) { node.pickingMode = PickingMode.Position; }
        }

        private void OnDragCancel(DragCancelEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();

            // Untrack for panning
            Vector2 totalDiff = (e.DeltaToDragOrigin - Graph.UntrackElementForPan(this, true)) / Graph.CurrentScale;

            // Reset position and picking mode
            foreach (Node node in Graph.NodesSelected)
            {
                node.SetPosition(node.GetPosition() + totalDiff);
                node.pickingMode = PickingMode.Position;
            }
        }

        private bool IsNodeDrag<T>(DragAndDropEvent<T> e) where T : DragAndDropEvent<T>, new()
        {
            if ((MouseButton)e.button != MouseButton.LeftMouse) { return false; }
            if (!e.modifiers.IsNone()) { return false; }
            return true;
        }
        #endregion
    }
}