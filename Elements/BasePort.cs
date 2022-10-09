// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class BasePort : VisualElement, IPositionable
    {
        private static readonly CustomStyleProperty<Color> s_PortColorProperty = new("--port-color");
        private static readonly CustomStyleProperty<Color> s_DisabledPortColorProperty = new("--disabled-port-color");

        private static readonly Color s_DefaultColor = new(240 / 255f, 240 / 255f, 240 / 255f);
        private static readonly Color s_DefaultDisabledColor = new(70 / 255f, 70 / 255f, 70 / 255f);

        private readonly HashSet<BaseEdge> m_Connections;
        protected VisualElement m_ConnectorBox;
        protected VisualElement m_ConnectorBoxCap;
        protected Label m_ConnectorText;
        private Direction m_Direction;
        private bool m_Highlight = true;
        private BaseNode m_ParentNode;
        private Color m_PortColor = s_DefaultColor;
        private bool m_PortColorIsInline;

        #region Constructor
        internal BasePort(Orientation orientation, Direction direction, PortCapacity capacity)
        {
            ClearClassList();

            // Label
            m_ConnectorText = new() { pickingMode = PickingMode.Ignore };
            m_ConnectorText.AddToClassList("port-label");
            Add(m_ConnectorText);

            // Cap
            m_ConnectorBoxCap = new() { pickingMode = PickingMode.Ignore };
            m_ConnectorBoxCap.AddToClassList("port-connector-cap");

            // Box
            m_ConnectorBox = new() { pickingMode = PickingMode.Ignore };
            m_ConnectorBox.AddToClassList("port-connector-box");
            m_ConnectorBox.Add(m_ConnectorBoxCap);
            Add(m_ConnectorBox);

            m_Connections = new();

            Orientation = orientation;
            Direction = direction;
            Capacity = capacity;

            AddToClassList("port");
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }
        #endregion

        #region Properties
        public BaseNode ParentNode
        {
            get => m_ParentNode;
            internal set
            {
                if (m_ParentNode == value) { return; }
                if (m_ParentNode != null) { m_ParentNode.OnPositionChange -= OnParentPositionChange; }
                if (value == null) { m_ParentNode = null; }
                else
                {
                    m_ParentNode = value;
                    m_ParentNode.OnPositionChange += OnParentPositionChange;
                }
            }
        }
        
        internal Color CapColor
        {
            get
            {
                if (m_ConnectorBoxCap == null) { return Color.black; }
                return m_ConnectorBoxCap.resolvedStyle.backgroundColor;
            }

            set
            {
                if (m_ConnectorBoxCap != null) { m_ConnectorBoxCap.style.backgroundColor = value; }
            }
        }

        public string PortName
        {
            get => m_ConnectorText.text;
            set => m_ConnectorText.text = value;
        }

        public Direction Direction
        {
            get => m_Direction;
            private set
            {
                RemoveFromClassList($"port-{m_Direction.ToString().ToLower()}");
                m_Direction = value;
                AddToClassList($"port-{m_Direction.ToString().ToLower()}");
            }
        }

        public bool Highlight
        {
            get => m_Highlight;
            set
            {
                if (m_Highlight == value) { return; }

                m_Highlight = value;

                UpdateConnectorColorAndEnabledState();
            }
        }

        public Color PortColor
        {
            get => m_PortColor;
            set
            {
                m_PortColorIsInline = true;
                m_PortColor = value;
                UpdateCapColor();
            }
        }
        
        public Color DisabledPortColor { get; private set; } = s_DefaultDisabledColor;
        public Orientation Orientation { get; }
        public PortCapacity Capacity { get; }
        public bool AllowMultiDrag { get; set; } = true;
        public virtual IEnumerable<BaseEdge> Connections => m_Connections;
        #endregion

        #region Edges
        public virtual bool Connected(bool ignoreCandidateEdges = true)
        {
            foreach (BaseEdge edge in m_Connections)
            {
                if (ignoreCandidateEdges && edge.IsCandidateEdge()) { continue; }
                return true;
            }
            return false;
        }

        public BaseEdge ConnectTo(BasePort other)
        {
            if (other == null) { throw new ArgumentNullException(nameof(other)); }

            if (other.Direction == Direction)
            {
                throw new ArgumentException("Cannot connect two ports with the same direction");
            }
            BaseEdge edge = ParentNode.Graph.CreateEdge();
            edge.Output = Direction == Direction.Output ? this : other;
            edge.Input = Direction == Direction.Input ? this : other;
            return edge;
        }

        public virtual void Connect(BaseEdge edge)
        {
            if (edge == null) { throw new ArgumentException("The value passed to Port.Connect is null"); }

            if (!m_Connections.Contains(edge)) { m_Connections.Add(edge); }

            UpdateCapColor();
        }

        public virtual void Disconnect(BaseEdge edge)
        {
            if (edge == null) { throw new ArgumentException("The value passed to PortPresenter.Disconnect is null"); }

            m_Connections.Remove(edge);
            UpdateCapColor();
        }

        public virtual bool CanConnectToMore(bool ignoreCandidateEdges = true)
            => Capacity == PortCapacity.Multi || !Connected(ignoreCandidateEdges);

        public bool IsConnectedTo(BasePort other, bool ignoreCandidateEdges = true)
        {
            foreach (BaseEdge e in m_Connections)
            {
                if (ignoreCandidateEdges && e.IsCandidateEdge()) { continue; }
                if (Direction == Direction.Output)
                {
                    if (e.Input == other) { return true; }
                }
                else
                {
                    if (e.Output == other) { return true; }
                }
            }
            return false;
        }

        public bool SameDirection(BasePort other) => Direction == other.Direction;

        public bool IsOnSameNode(BasePort other) => other.ParentNode == ParentNode;

        public virtual bool CanConnectTo(BasePort other, bool ignoreCandidateEdges = true)
        {
            // Debug.Log($"{this} - {other} >> same_direction: {SameDirection(other)}," 
            //           + $"same_node: {IsOnSameNode(other)}, "
            //           + $"has_cap: {CanConnectToMore(ignoreCandidateEdges)}, "
            //           + $"other_has_cap: {other.CanConnectToMore(ignoreCandidateEdges)}, "
            //           + $"is_connected: {IsConnectedTo(other, ignoreCandidateEdges)}");
            return !SameDirection(other)
                   && !IsOnSameNode(other)
                   && CanConnectToMore(ignoreCandidateEdges)
                   && other.CanConnectToMore(ignoreCandidateEdges)
                   && !IsConnectedTo(other, ignoreCandidateEdges);
        }
        #endregion

        #region Style
        internal void UpdateCapColor()
        {
            if (Connected()) { m_ConnectorBoxCap.style.backgroundColor = PortColor; }
            else { m_ConnectorBoxCap.style.backgroundColor = StyleKeyword.Null; }
        }

        private void UpdateConnectorColorAndEnabledState()
        {
            if (m_ConnectorBox == null) { return; }

            Color color = Highlight ? m_PortColor : DisabledPortColor;
            m_ConnectorBox.style.borderLeftColor = color;
            m_ConnectorBox.style.borderTopColor = color;
            m_ConnectorBox.style.borderRightColor = color;
            m_ConnectorBox.style.borderBottomColor = color;
            m_ConnectorBox.SetEnabled(Highlight);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (!m_PortColorIsInline && evt.customStyle.TryGetValue(s_PortColorProperty, out Color portColorValue))
            {
                m_PortColor = portColorValue;
                UpdateCapColor();
            }
            if (evt.customStyle.TryGetValue(s_DisabledPortColorProperty, out Color disableColorValue))
            {
                DisabledPortColor = disableColorValue;
            }
            UpdateConnectorColorAndEnabledState();
        }
        #endregion
        
        #region Position
        public event Action<PositionData> OnPositionChange;
        public Vector2 GetGlobalCenter() => m_ConnectorBox.LocalToWorld(GetCenter());
        public Vector2 GetCenter() => new Rect(Vector2.zero, m_ConnectorBox.layout.size).center;
        public Vector2 GetPosition() => Vector2.zero;
        public void SetPosition(Vector2 position) => throw new NotImplementedException();
        public void ApplyDeltaToPosition(Vector2 delta) => throw new NotImplementedException();
        private void OnParentPositionChange(PositionData positionData) 
            => OnPositionChange?.Invoke(new() { element = this });
        #endregion

        #region Event Handlers 
        [EventInterest(typeof(DragOfferEvent), typeof(DropEnterEvent), typeof(DropEvent), typeof(DropExitEvent))]
        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);
            if (evt.eventTypeId == DragOfferEvent.TypeId()) OnDragOffer((DragOfferEvent)evt);
            else if (evt.eventTypeId == DropEnterEvent.TypeId()) OnDropEnter((DropEnterEvent)evt);
            else if (evt.eventTypeId == DropEvent.TypeId()) OnDrop((DropEvent)evt);
            else if (evt.eventTypeId == DropExitEvent.TypeId()) OnDropExit((DropExitEvent)evt);
        }

        private void OnDragOffer(DragOfferEvent e)
        {
            // Check if this is a port drag event 
            if (!IsPortDrag(e) || !CanConnectToMore()) return;
            
            // Create edge
            BaseEdge draggedEdge = ParentNode.Graph.CreateEdge();
            draggedEdge.SetPortByDirection(this);
            draggedEdge.visible = false;
            ParentNode.Graph.AddElement(draggedEdge);
            
            // Accept drag
            e.AcceptDrag(draggedEdge);
            
            // Set threshold
            e.SetDragThreshold(10); 
        }

        private void OnDropEnter(DropEnterEvent e)
        {
            if (e.GetUserData() is IDropPayload dropPayload && typeof(BaseEdge).IsAssignableFrom(dropPayload.GetPayloadType()))
            {
                // Consume event
                e.StopImmediatePropagation();
                
                // Sanity
                if (dropPayload.GetPayload().Count == 0) throw new("Drop payload was unexpectedly empty");
                
                // Grab dragged port
                BaseEdge draggedEdge = (BaseEdge) dropPayload.GetPayload()[0];
                BasePort anchoredPort = draggedEdge.IsInputPositionOverriden() ? draggedEdge.Output : draggedEdge.Input;
                
                // Ignore drags from invalid ports
                if (!CanConnectTo(anchoredPort))
                {
                    return;
                }
                
                // But if it's compatible, light this port up 
                m_ConnectorBoxCap.style.backgroundColor = PortColor;
            }
        }

        private void OnDrop(DropEvent e)
        {
            if (e.GetUserData() is IDropPayload dropPayload && typeof(BaseEdge).IsAssignableFrom(dropPayload.GetPayloadType()))
            {
                // Consume event
                e.StopImmediatePropagation();
                
                // Sanity
                if (dropPayload.GetPayload().Count == 0) throw new("Drop payload was unexpectedly empty");
                
                // Grab dragged port (iterate backwards since this can result in deletions)
                for (int i = dropPayload.GetPayload().Count - 1; i >= 0; i--)
                {
                    // Grab dragged edge and the corresponding anchored port
                    BaseEdge edge = (BaseEdge) dropPayload.GetPayload()[i];
                    BasePort anchoredPort = edge.IsInputPositionOverriden() ? edge.Output : edge.Input;
                    ConnectToPortWithEdge(e, anchoredPort, edge);
                }
                
                // But if it's compatible, update caps as appropriate 
                UpdateCapColor();
            }
        }

        private void OnDropExit(DropExitEvent e)
        {
            if (e.GetUserData() is IDropPayload dropPayload && typeof(BaseEdge).IsAssignableFrom(dropPayload.GetPayloadType()))
            {
                // Consume event
                e.StopImmediatePropagation();
                
                // Sanity
                if (dropPayload.GetPayload().Count == 0) throw new("Drop payload was unexpectedly empty");
                
                // But if it's compatible, update caps as appropriate 
                UpdateCapColor();
            }
        }
        
        private bool IsPortDrag<T>(DragAndDropEvent<T> e) where T : DragAndDropEvent<T>, new()
        {
            if ((MouseButton)e.button != MouseButton.LeftMouse) { return false; }
            if (!e.modifiers.IsNone()) { return false; }
            return true; 
        }
        
        private void ConnectToPortWithEdge(DropEvent e, BasePort anchoredPort, BaseEdge draggedEdge)
        {
            // Cancel invalid drags or already extant edges  
            if (!CanConnectTo(anchoredPort))
            {
                // Real edges will be returned to their former state
                e.CancelDrag();
                return;
            }

            // Capture the ports being connected
            BasePort inputPort;
            BasePort outputPort;
            if (anchoredPort.Direction == Direction.Input)
            {
                inputPort = anchoredPort;
                outputPort = this;
            }
            else
            {
                inputPort = this;
                outputPort = anchoredPort;
            }

            // If this is an existing edge, delete it unless we're reconnecting after a disconnect
            if (draggedEdge.IsRealEdge())
            {
                if (draggedEdge.Input == inputPort && draggedEdge.Output == outputPort)
                {
                    e.CancelDrag();
                    return;
                }
                ParentNode.Graph.ExecuteEdgeDelete(draggedEdge);
            }

            // This was a temporary edge, reset it and remove from the graph
            else { ParentNode.Graph.RemoveElement(draggedEdge); }

            // Create new edge (reusing the old deleted edge)
            draggedEdge.Input = inputPort;
            draggedEdge.Output = outputPort;
            ParentNode.Graph.ExecuteEdgeCreate(draggedEdge);
        }
        #endregion
        
        public override string ToString() => PortName;
    }
}