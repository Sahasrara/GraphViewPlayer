using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    /// <summary>
    /// ContentView houses all graph elements and is the element directly scaled/panned by the GraphView.
    /// ContentView also handles selection based drag and drop for all GraphElements.
    /// </summary>
    internal class ContentView : VisualElement, ISelector, IPositionable, IDropPayload
    {
        private readonly List<GraphElement> m_Selection;
        
        private GraphView m_GraphView;
        private GraphElement m_Dragged;
        
        public ContentView(GraphView graphView)
        {
            AddToClassList("content-view-container");
            pickingMode = PickingMode.Ignore;
            usageHints = UsageHints.GroupTransform;
            
            // Selection
            m_Selection = new();
            
            // Graph View
            m_GraphView = graphView;
            
            // Cached Queries
            ElementsAll = this.Query<GraphElement>().Build();
            ElementsSelected = this.Query<GraphElement>().Where(WhereSelected).Build();
            ElementsUnselected = this.Query<GraphElement>().Where(WhereUnselected).Build();
            Nodes = this.Query<BaseNode>().Build();
            NodesSelected = this.Query<BaseNode>().Where(WhereSelected).Build();
            Edges = this.Query<BaseEdge>().Build();
            EdgesSelected = this.Query<BaseEdge>().Where(WhereSelected).Build();
            Ports = this.Query<BasePort>().Build();
            
            // Event Handlers
            RegisterCallback<DragOfferEvent>(OnDragOffer);
            RegisterCallback<DragBeginEvent>(OnDragBegin);
            RegisterCallback<DragEvent>(OnDrag);
            RegisterCallback<DragEndEvent>(OnDragEnd);
            RegisterCallback<DragCancelEvent>(OnDragCancel);
        }

        #region Selection
        public UQueryState<BaseNode> Nodes { get; }
        public UQueryState<BaseNode> NodesSelected { get; }
        public UQueryState<BasePort> Ports { get; }
        public UQueryState<BaseEdge> Edges { get; }
        public UQueryState<BaseEdge> EdgesSelected { get; }
        public UQueryState<GraphElement> ElementsAll { get; }
        public UQueryState<GraphElement> ElementsSelected { get; }
        public UQueryState<GraphElement> ElementsUnselected { get; }
        
        public void SelectAll()
        {
            foreach (GraphElement ge in ElementsAll) { ge.Selected = true; }
        }

        public void ClearSelection()
        {
            foreach (GraphElement ge in ElementsAll) { ge.Selected = false; }
        }

        public void CollectAll(List<ISelectable> toPopulate)
        {
            foreach (GraphElement ge in ElementsAll) { toPopulate.Add(ge); }
        }

        public void CollectSelected(List<ISelectable> toPopulate)
        {
            foreach (GraphElement ge in ElementsAll)
            {
                if (ge.Selected) { toPopulate.Add(ge); }
            }
        }

        public void CollectUnselected(List<ISelectable> toPopulate)
        {
            foreach (GraphElement ge in ElementsAll)
            {
                if (!ge.Selected) { toPopulate.Add(ge); }
            }
        }

        public void ForEachAll(Action<ISelectable> action) { ElementsAll.ForEach(action); }
        public void ForEachSelected(Action<ISelectable> action) { ElementsSelected.ForEach(action); }
        public void ForEachUnselected(Action<ISelectable> action) { ElementsUnselected.ForEach(action); }

        private bool WhereSelected(ISelectable selectable) => selectable.Selected;
        private bool WhereUnselected(ISelectable selectable) => !selectable.Selected;
        #endregion
        
        #region Drag and Drop
        public void RemoveFromDragSelection(GraphElement e) => m_Selection.Remove(e);

        private void OnDragOffer(DragOfferEvent e)
        {
            // Verify the target can handle the drag
            if (e.GetDraggedElement() is not GraphElement graphElement) { return;}
            
            // Consume the drag event
            e.StopImmediatePropagation();
            
            // Store draggable
            m_Dragged = graphElement;

            // Replace the drag acceptor
            e.AcceptDrag(this);
        }

        private void OnDragBegin(DragBeginEvent e)
        {
            // If we didn't accept a drag offer, exit. This should never happen
            if (m_Dragged == null) throw new("Unexpected drag begin event");
            
            // Swallow event
            e.StopImmediatePropagation();
            
            // Track for panning
            m_GraphView.TrackElementForPan(this);
            
            // Handle drag begin
            if (m_Dragged is BaseEdge edge) HandleDragBegin(e, edge); 
            else if (m_Dragged is BaseNode node) HandleDragBegin(e, node);
        }

        private void HandleDragBegin(DragBeginEvent e, BaseEdge draggedEdge)
        {
            // Determine which end of the edge was dragged
            bool draggedInput;
            if (draggedEdge.Input == null) { draggedInput = true; }
            else if (draggedEdge.Output == null) { draggedInput = false; }
            else
            {
                // Vector2 mousePosition = this.WorldToLocal(e.mousePosition);
                Vector2 inputPos = draggedEdge.Input.GetGlobalCenter();
                Vector2 outputPos = draggedEdge.Output.GetGlobalCenter();
                float distanceFromInput = (e.mousePosition - inputPos).sqrMagnitude;
                float distanceFromOutput = (e.mousePosition - outputPos).sqrMagnitude;
                draggedInput = distanceFromInput < distanceFromOutput;
            }
            
            // Collect selection and set them to drag mode 
            BasePort draggedPort;
            BasePort anchoredPort;
            if (draggedInput)
            {
                draggedPort = draggedEdge.Input;
                anchoredPort = draggedEdge.Output;
                HandleDragBeginEdgeHelper(draggedPort, draggedEdge, SetDraggedEdgeInputOverride);
            }
            else
            {
                draggedPort = draggedEdge.Output;
                anchoredPort = draggedEdge.Input;
                HandleDragBeginEdgeHelper(draggedPort, draggedEdge, SetDraggedEdgeOutputOverride);
            }
            m_GraphView.IlluminateCompatiblePorts(anchoredPort);
            
            // Set user data
            e.SetUserData(this);
        }

        private void HandleDragBeginEdgeHelper(
            BasePort draggedPort, BaseEdge draggedEdge, Action<BaseEdge> positionAction)
        {
            if (draggedPort != null && draggedPort.AllowMultiDrag)
            {
                foreach (BaseEdge edge in draggedPort.Connections)
                {
                    if (edge.Selected)
                    {
                        positionAction(edge);
                        edge.pickingMode = PickingMode.Ignore; 
                        edge.visible = true;
                        m_Selection.Add(edge); 
                    }
                }
            }
            else
            {
                positionAction(draggedEdge);
                draggedEdge.pickingMode = PickingMode.Ignore;
                draggedEdge.visible = true;
                m_Selection.Add(draggedEdge); 
            }
        }
        private void SetDraggedEdgeInputOverride(BaseEdge edge) 
            => edge.SetInputPositionOverride(edge.GetInputPositionOverride()); 
        private void SetDraggedEdgeOutputOverride(BaseEdge edge) 
            => edge.SetOutputPositionOverride(edge.GetOutputPositionOverride()); 

        private void HandleDragBegin(DragBeginEvent e, BaseNode draggedNode)
        {
            // Collect selection
            foreach (BaseNode node in NodesSelected)
            {
                // Add to selection
                m_Selection.Add(node);
                
                // Disable picking
                node.pickingMode = PickingMode.Ignore; 
            }
        }

        private void OnDrag(DragEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();
            
            // Handle drag
            if (m_Dragged is BaseEdge edge) HandleDrag(e, edge); 
            else if (m_Dragged is BaseNode node) HandleDrag(e, node); 
        }

        private void HandleDrag(DragEvent e, BaseNode draggedNode)
        {
            for (int i = 0; i < m_Selection.Count; i++)
            {
                BaseNode node = (BaseNode) m_Selection[i];
                node.ApplyDeltaToPosition(e.mouseDelta / transform.scale);
            }
        }

        private void HandleDrag(DragEvent e, BaseEdge draggedEdge)
        {
            Vector2 newPosition = this.WorldToLocal(e.mousePosition);
            for (int i = 0; i < m_Selection.Count; i++)
            {
                BaseEdge edge = (BaseEdge) m_Selection[i];
                if (edge.IsInputPositionOverriden()) { edge.SetInputPositionOverride(newPosition); }
                else { edge.SetOutputPositionOverride(newPosition); }
            } 
        }

        private void OnDragEnd(DragEndEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();
            
            // Untrack for panning
            m_GraphView.UntrackElementForPan(this);
            
            // Handle drag end
            if (m_Dragged is BaseEdge edge) HandleDragEnd(e, edge); 
            else if (m_Dragged is BaseNode node) HandleDragEnd(e, node);  
            
            // Reset
            Reset();
        }

        private void HandleDragEnd(DragEndEvent e, BaseNode draggedNode)
        {
            for (int i = 0; i < m_Selection.Count; i++)
            {
                BaseNode node = (BaseNode) m_Selection[i];
                // Skip deleted nodes 
                // if (node.parent == null) continue;
                // Re-enable picking
                node.pickingMode = PickingMode.Position;
            } 
        }

        private void HandleDragEnd(DragEndEvent e, BaseEdge draggedEdge)
        {
            for (int i = 0; i < m_Selection.Count; i++)
            {
                BaseEdge edge = (BaseEdge) m_Selection[i];
                // Skip deleted edges 
                // if (edge.parent == null) continue;
                // Re-enable picking
                edge.pickingMode = PickingMode.Position;
            }  
            // Reset ports
            m_GraphView.IlluminateAllPorts();
        }

        private void OnDragCancel(DragCancelEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();
            
            // Untrack for panning
            Vector2 totalDiff = (e.DeltaToDragOrigin - m_GraphView.UntrackElementForPan(this, true)) / transform.scale;
            
            // Handle drag cancel 
            if (m_Dragged is BaseEdge edge) HandleDragCancel(e, edge, totalDiff); 
            else if (m_Dragged is BaseNode node) HandleDragCancel(e, node, totalDiff);   
            
            // Reset
            Reset();
        }

        private void HandleDragCancel(DragCancelEvent e, BaseNode draggedNode, Vector2 totalDragDiff)
        {
            for (int i = 0; i < m_Selection.Count; i++)
            {
                // Grab node
                BaseNode node = (BaseNode) m_Selection[i];
                // Skip deleted nodes
                // if (node.parent == null) continue;
                // Re-enable picking
                node.pickingMode = PickingMode.Position;
                // Reset position before drag
                node.ApplyDeltaToPosition(totalDragDiff);
            }  
        }

        private void HandleDragCancel(DragCancelEvent e, BaseEdge draggedEdge, Vector2 totalDragDiff)
        {
            // Reverse iteration because we alter the collection as we go
            for (int i = m_Selection.Count - 1; i >= 0; i--)
            {
                BaseEdge edge = (BaseEdge) m_Selection[i];
                // Skip deleted edges 
                // if (edge.parent == null) continue;
                // Re-enable picking
                edge.pickingMode = PickingMode.Position;
                // Reset position to before drag if we're dragging an existing edge 
                if (edge.IsRealEdge())
                {
                    edge.ResetLayer();
                    edge.UnsetPositionOverrides();
                    edge.Selected = false;
                }
                // Otherwise this is a candidate edge dragged from a port and should be DESTROYED!!!
                else m_GraphView.RemoveElement(edge);
            }   
            // Reset ports
            m_GraphView.IlluminateAllPorts();
        }

        private void Reset()
        {
            m_Dragged = null;
            m_Selection.Clear();
        }
        #endregion

        #region Position
        public event Action<PositionData> OnPositionChange;
        public Vector2 GetGlobalCenter() => m_Dragged.GetGlobalCenter();
        public Vector2 GetCenter() => m_Dragged.GetCenter();
        public Vector2 GetPosition() => m_Dragged.GetPosition();
        public void SetPosition(Vector2 position)
        {
            for (int i = 0; i < m_Selection.Count; i++)
            {
                GraphElement element = m_Selection[i];
                element.SetPosition(position);
            } 
            OnPositionChange?.Invoke(new());
        } 
        public void ApplyDeltaToPosition(Vector2 delta)
        {
            for (int i = 0; i < m_Selection.Count; i++)
            {
                GraphElement element = m_Selection[i];
                element.ApplyDeltaToPosition(delta);
            }
            OnPositionChange?.Invoke(new());
        }
        #endregion

        #region Drop Payload
        public Type GetPayloadType() => m_Dragged.GetType();
        public IReadOnlyList<GraphElement> GetPayload() => m_Selection;
        #endregion
    }

    public interface IDropPayload
    {
        public Type GetPayloadType();
        public IReadOnlyList<GraphElement> GetPayload();
    }
}
