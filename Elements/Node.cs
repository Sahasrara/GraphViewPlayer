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
    public class Node : GraphElement, ICollectibleElement
    {
        public VisualElement mainContainer { get; private set; }
        public VisualElement titleContainer { get; private set; }
        public VisualElement inputContainer { get; private set; }
        public VisualElement outputContainer { get; private set; }

        //This directly contains input and output containers
        public VisualElement topContainer { get; private set; }
        public VisualElement extensionContainer { get; private set; }

        private GraphView m_GraphView;
        // TODO Maybe make protected and move to GraphElement!
        private GraphView graphView
        {
            get
            {
                if (m_GraphView == null)
                {
                    m_GraphView = GetFirstAncestorOfType<GraphView>();
                }
                return m_GraphView;
            }
        }

        private readonly Label m_TitleLabel;
        public override string title
        {
            get { return m_TitleLabel != null ? m_TitleLabel.text : string.Empty; }
            set { if (m_TitleLabel != null) m_TitleLabel.text = value; }
        }

        public UQueryState<Port> inputPorts;
        public UQueryState<Port> outputPorts;

        public override Rect GetPosition()
        {
            if (resolvedStyle.position == Position.Absolute)
                return new Rect(resolvedStyle.left, resolvedStyle.top, layout.width, layout.height);
            return layout;
        }

        public override void SetPosition(Rect newPos)
        {
            style.position = Position.Absolute;
            style.left = newPos.x;
            style.top = newPos.y;
        }

        public virtual Port InstantiatePort(Orientation orientation, Direction direction, Port.Capacity capacity)
        {
            return Port.Create<Edge>(orientation, direction, capacity);
        }

        public Node()
        {
            // Root Container
            mainContainer = this;
           
            // Title Label
            m_TitleLabel = new() { pickingMode = PickingMode.Ignore };
            m_TitleLabel.AddToClassList("node-title-label");

            // Title Container
            titleContainer = new() { pickingMode = PickingMode.Ignore };
            titleContainer.AddToClassList("node-title");
            titleContainer.Add(m_TitleLabel);
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

            elementTypeColor = new Color(0.9f, 0.9f, 0.9f, 0.5f);

            AddToClassList("node");

            capabilities
                |= Capabilities.Selectable
                | Capabilities.Movable
                | Capabilities.Deletable
                | Capabilities.Ascendable
                // | Capabilities.Copiable
                // | Capabilities.Snappable
                // | Capabilities.Groupable
                ;
            usageHints = UsageHints.DynamicTransform;

            // Cache Queries
            inputPorts = inputContainer.Query<Port>().Build();
            outputPorts = outputContainer.Query<Port>().Build();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            AddToClassList("node-selected");
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            RemoveFromClassList("node-selected");
        }

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

        void CollectConnectedEdges(HashSet<GraphElement> edgeSet)
        {
            edgeSet.UnionWith(inputPorts.SelectMany(c => c.connections)
                .Where(d => (d.capabilities & Capabilities.Deletable) != 0));
            edgeSet.UnionWith(outputPorts.SelectMany(c => c.connections)
                .Where(d => (d.capabilities & Capabilities.Deletable) != 0));
        }

        public virtual void CollectElements(HashSet<GraphElement> collectedElementSet, Func<GraphElement, bool> conditionFunc)
        {
            CollectConnectedEdges(collectedElementSet);
        }
    }
}
