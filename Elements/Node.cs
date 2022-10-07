// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class Node : GraphElement, IDraggable
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

        #region Ports
        public virtual void AddPort(BasePort port)
        {
            port.ParentNode = this;
            if (port.Direction == Direction.Input) { InputContainer.Add(port); }
            else { OutputContainer.Add(port); }
        }
        #endregion

        #region IDraggable
        public void OnDragBegin(IDragBeginContext context)
        {
            // Cancel checks 
            if (context.IsCancelled()) { return; }
            if (Graph == null || !IsMovable() || !CanStartManipulation(context.MouseButton, context.MouseModifiers))
            {
                context.CancelDrag();
                return;
            }

            // Track for panning
            Graph.TrackElementForPan(this);
        }

        public void OnDrag(IDragContext context)
        {
            // Cancel checks 
            if (context.IsCancelled()) { return; }
            if (Graph == null || !IsMovable())
            {
                context.CancelDrag();
                return;
            }

            // Handle drag
            foreach (Node node in Graph.NodesSelected)
            {
                node.SetPosition(node.GetPosition() + context.MouseDelta / Graph.CurrentScale);
                Graph.OnNodeMoved(node);
            }
        }

        public void OnDragEnd(IDragEndContext context)
        {
            // Untrack for panning
            Graph.UntrackElementForPan(this);
        }

        public void OnDragCancel(IDragCancelContext context)
        {
            // Untrack for panning
            Vector2 totalDiff = (context.MouseResetDelta - Graph.UntrackElementForPan(this, true)) / Graph.CurrentScale;

            // Reset position
            foreach (Node node in Graph.NodesSelected) { node.SetPosition(node.GetPosition() + totalDiff); }
        }
        
        protected virtual bool CanStartManipulation(MouseButton mouseButton, EventModifiers mouseModifiers)
        {
            if (mouseButton != MouseButton.LeftMouse) { return false; }
            if (mouseModifiers.IsNone()) { return true; }
            return false;
        }
        #endregion
    }
}