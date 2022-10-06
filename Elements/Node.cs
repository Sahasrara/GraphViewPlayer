// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class Node : GraphElement, IDraggable //, ICollectibleElement
    {
        public Node()
        {
            // Root Container
            mainContainer = this;

            // Title Label
            titleLabel = new() { pickingMode = PickingMode.Ignore };
            titleLabel.AddToClassList("node-title-label");

            // Title Container
            titleContainer = new() { pickingMode = PickingMode.Ignore };
            titleContainer.AddToClassList("node-title");
            titleContainer.Add(titleLabel);
            hierarchy.Add(titleContainer);

            // Input Container
            inputContainer = new() { pickingMode = PickingMode.Ignore };
            inputContainer.AddToClassList("node-io-input");

            // Output Container
            outputContainer = new() { pickingMode = PickingMode.Ignore };
            outputContainer.AddToClassList("node-io-output");

            // Top Container 
            topContainer = new() { pickingMode = PickingMode.Ignore };
            topContainer.AddToClassList("node-io");
            topContainer.Add(inputContainer);
            topContainer.Add(outputContainer);
            hierarchy.Add(topContainer);

            // Extension Container
            extensionContainer = new() { pickingMode = PickingMode.Ignore };
            extensionContainer.AddToClassList("node-extension");
            hierarchy.Add(extensionContainer);

            // Style
            AddToClassList("node");

            // Capability
            Capabilities |= Capabilities.Selectable
                            | Capabilities.Movable
                            | Capabilities.Deletable
                            | Capabilities.Ascendable
                ;
            usageHints = UsageHints.DynamicTransform;

            // Cache Queries
            // inputPorts = inputContainer.Query<Port>().Build();
            // outputPorts = outputContainer.Query<Port>().Build();
        }

        public Label titleLabel { get; }
        public VisualElement mainContainer { get; }
        public VisualElement titleContainer { get; }
        public VisualElement topContainer { get; }
        public VisualElement inputContainer { get; }
        public VisualElement outputContainer { get; }
        public VisualElement extensionContainer { get; }

        public override string Title
        {
            get => titleLabel != null ? titleLabel.text : string.Empty;
            set
            {
                if (titleLabel != null) { titleLabel.text = value; }
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

        public virtual Port InstantiatePort(Orientation orientation, Direction direction, Port.PortCapacity capacity) =>
            Port.Create<Edge>(this, orientation, direction, capacity);

        // public virtual void CollectElements(HashSet<GraphElement> collectedElementSet,
        //     Func<GraphElement, bool> conditionFunc)
        // {
        //     CollectConnectedEdges(collectedElementSet);
        // }

        // void AddConnectionsToDeleteSet(VisualElement container, ref HashSet<GraphElement> toDelete)
        // {
        //     List<GraphElement> toDeleteList = new List<GraphElement>();
        //     container.Query<Port>().ForEach(elem =>
        //     {
        //         if (elem.connected)
        //         {
        //             foreach (Edge c in elem.connections)
        //             {
        //                 if ((c.capabilities & Capabilities.Deletable) == 0)
        //                     continue;
        //
        //                 toDeleteList.Add(c);
        //             }
        //         }
        //     });
        //
        //     toDelete.UnionWith(toDeleteList);
        // }

        // void DisconnectAll(DropdownMenuAction a)
        // {
        //     HashSet<GraphElement> toDelete = new HashSet<GraphElement>();
        //
        //     AddConnectionsToDeleteSet(inputContainer, ref toDelete);
        //     AddConnectionsToDeleteSet(outputContainer, ref toDelete);
        //     toDelete.Remove(null);
        //
        //     if (graphView != null)
        //     {
        //         graphView.DeleteElements(toDelete);
        //     }
        //     else
        //     {
        //         Debug.Log("Disconnecting nodes that are not in a GraphView will not work.");
        //     }
        // }

        // DropdownMenuAction.Status DisconnectAllStatus(DropdownMenuAction a)
        // {
        //     VisualElement[] containers =
        //     {
        //         inputContainer, outputContainer
        //     };
        //
        //     foreach (var container in containers)
        //     {
        //         var currentInputs = container.Query<Port>().ToList();
        //         foreach (var elem in currentInputs)
        //         {
        //             if (elem.connected)
        //             {
        //                 return DropdownMenuAction.Status.Normal;
        //             }
        //         }
        //     }
        //
        //     return DropdownMenuAction.Status.Disabled;
        // }

        // private void CollectConnectedEdges(HashSet<GraphElement> edgeSet)
        // {
        //     edgeSet.UnionWith(inputPorts.SelectMany(c => c.connections)
        //         .Where(d => (d.Capabilities & Capabilities.Deletable) != 0));
        //     edgeSet.UnionWith(outputPorts.SelectMany(c => c.connections)
        //         .Where(d => (d.Capabilities & Capabilities.Deletable) != 0));
        // }

        #region IDraggable
        public void OnDragBegin(IDragBeginContext context)
        {
            // Cancel checks 
            if (context.IsCancelled()) { return; }
            if (Graph == null || !IsMovable())
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
            foreach (Node node in Graph.NodesSelected)
            {
                node.SetPosition(node.GetPosition() + totalDiff);
            }
        }
        #endregion
    }
}