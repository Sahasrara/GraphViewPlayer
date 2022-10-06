// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class Port : VisualElement, IDraggable, IDroppable, IPositionable
    {
        public enum PortCapacity
        {
            Single,
            Multi
        }

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
        private Color m_PortColor = s_DefaultColor;
        private bool m_PortColorIsInline;

        // protected PortManipulator m_PortManipulator;

        protected Port(Node parent, Orientation portOrientation, Direction portDirection, PortCapacity portCapacity)
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

            Orientation = portOrientation;
            Direction = portDirection;
            Capacity = portCapacity;
            ParentNode = parent;

            // Listen to parent position changes
            parent.OnPositionChange += OnParentPositionChange;

            AddToClassList("port");
            AddToClassList($"port-{portDirection.ToString().ToLower()}");
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        public bool allowMultiDrag { get; set; } = true;

        public Node ParentNode { get; }
        public Orientation Orientation { get; }
        public PortCapacity Capacity { get; }

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
                if (m_Direction != value)
                {
                    RemoveFromClassList(m_Direction.ToString().ToLower());
                    m_Direction = value;
                    AddToClassList(m_Direction.ToString().ToLower());
                }
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

        public virtual IEnumerable<BaseEdge> Connections => m_Connections;

        public virtual bool Connected(bool ignoreCandidateEdges = true)
        {
                foreach (BaseEdge edge in m_Connections)
                {
                    if (ignoreCandidateEdges && edge.IsCandidateEdge()) { continue; }
                    return true;
                }
                return false;
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

        public Edge ConnectTo(Port other) => ConnectTo<Edge>(other);

        public T ConnectTo<T>(Port other) where T : BaseEdge, new()
        {
            if (other == null) { throw new ArgumentNullException("Port.ConnectTo<T>() other argument is null"); }

            if (other.Direction == Direction)
            {
                throw new ArgumentException("Cannot connect two ports with the same direction");
            }

            T edge = new();

            edge.Output = Direction == Direction.Output ? this : other;
            edge.Input = Direction == Direction.Input ? this : other;

            return edge;
        }

        public virtual void Connect(BaseEdge edge)
        {
            if (edge == null) { throw new ArgumentException("The value passed to Port.Connect is null"); }

            if (!m_Connections.Contains(edge))
            {
                m_Connections.Add(edge);
            }

            UpdateCapColor();
        }

        public virtual void Disconnect(BaseEdge edge)
        {
            if (edge == null) { throw new ArgumentException("The value passed to PortPresenter.Disconnect is null"); }

            m_Connections.Remove(edge);
            UpdateCapColor();
        }

        public virtual void DisconnectAll()
        {
            m_Connections.Clear();
            UpdateCapColor();
        }

        public virtual bool CanConnectToMore(bool ignoreCandidateEdges = true) 
            => Capacity == PortCapacity.Multi || !Connected(ignoreCandidateEdges);

        public bool IsConnectedTo(Port other, bool ignoreCandidateEdges = true)
        {
            foreach (BaseEdge e in m_Connections)
            {
                if (ignoreCandidateEdges && e.IsCandidateEdge()) continue;
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

        // TODO This is a workaround to avoid having a generic type for the port as generic types mess with USS.
        public static Port Create<TEdge>(Node parent, Orientation orientation, Direction direction,
            PortCapacity capacity) where TEdge : BaseEdge, new()
        {
            Port port = new(parent, orientation, direction, capacity)
            {
                // m_PortManipulator = new PortManipulator<TEdge>()
            };

            // port.AddManipulator(port.m_PortManipulator);
            return port;
        }

        public bool SameDirection(Port other) => Direction == other.Direction;

        public bool IsOnSameNode(Port other) => other.ParentNode == ParentNode;

        public virtual bool CanConnectTo(Port other, bool ignoreCandidateEdges = true) 
            => !SameDirection(other) 
               && !IsOnSameNode(other) 
               && other.CanConnectToMore(ignoreCandidateEdges) 
               && CanConnectToMore(ignoreCandidateEdges) 
               && !IsConnectedTo(other, ignoreCandidateEdges);

        // public override bool ContainsPoint(Vector2 localPoint)
        // {
        //     Rect lRect = m_ConnectorBox.layout;
        //
        //     Rect boxRect;
        //     if (direction == Direction.Input)
        //     {
        //         boxRect = new(-lRect.xMin, -lRect.yMin,
        //             lRect.width + lRect.xMin, resolvedStyle.height);
        //
        //         boxRect.width += m_ConnectorText.layout.xMin - lRect.xMax;
        //     }
        //     else
        //     {
        //         boxRect = new(0, -lRect.yMin,
        //             resolvedStyle.width - lRect.xMin, resolvedStyle.height);
        //         float leftSpace = lRect.xMin - m_ConnectorText.layout.xMax;
        //
        //         boxRect.xMin -= leftSpace;
        //         boxRect.width += leftSpace;
        //     }
        //
        //     return boxRect.Contains(this.ChangeCoordinatesTo(m_ConnectorBox, localPoint));
        // }

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
            Color portColorValue = Color.clear;
            Color disableColorValue = Color.clear;

            if (!m_PortColorIsInline && evt.customStyle.TryGetValue(s_PortColorProperty, out portColorValue))
            {
                m_PortColor = portColorValue;
                UpdateCapColor();
            }

            if (evt.customStyle.TryGetValue(s_DisabledPortColorProperty, out disableColorValue))
            {
                DisabledPortColor = disableColorValue;
            }

            UpdateConnectorColorAndEnabledState();
        }

        #region Position
        public event Action<PositionData> OnPositionChange;
        public Vector2 GetGlobalCenter() => m_ConnectorBox.LocalToWorld(GetCenter());
        public Vector2 GetCenter() => new Rect(Vector2.zero, m_ConnectorBox.layout.size).center;
        public Vector2 GetPosition() => Vector2.zero;

        public void SetPosition(Vector3 position)
        {
            style.left = new Length(position.x, LengthUnit.Pixel);
            style.top = new Length(position.y, LengthUnit.Pixel);
        }

        private void OnParentPositionChange(PositionData positionData)
        {
            OnPositionChange?.Invoke(new()
            {
                element = this
            });
        }
        #endregion

        #region IDraggable
        public void OnDragBegin(IDragBeginContext context)
        {
            // Cancel checks 
            if (context.IsCancelled()) { return; }
            if (ParentNode == null)
            {
                context.CancelDrag();
                return;
            }
            
            // Create edge
            BaseEdge edge = ParentNode.Graph.CreateEdge();
            
            


        }

        public void OnDrag(IDragContext context)
        {
            // Cancel checks 
            if (context.IsCancelled()) { return; }
            if (ParentNode == null || !CanConnectToMore())
            {
                context.CancelDrag();
                return;
            } 
            
            
        }

        public void OnDragEnd(IDragEndContext context)
        {
            
        }

        public void OnDragCancel(IDragCancelContext context)
        {
            
        }
        #endregion
        
        #region IDroppable
        public void OnDropEnter(IDropEnterContext context)
        {
            if (context.Draggable is BaseEdge edge)
            {
                // Grab dragged port
                Port draggedPort = context.GetUserData() as Port;
                if (draggedPort == null) throw new("Edge user data not set properly"); 
                Port anchoredPort = draggedPort == edge.Input ? edge.Output : edge.Input; 
                
                // Cancel drags from invalid ports
                if (!CanConnectTo(anchoredPort)) return;
                
                // Hover state
                m_ConnectorBoxCap.style.backgroundColor = PortColor; 
            }
        }

        public void OnDrop(IDropContext context)
        {
            // See if we can form a connection 
            if (context.Draggable is BaseEdge draggedEdge)
            {
                // Grab dragged port
                Port draggedPort = context.GetUserData() as Port;
                if (draggedPort == null) throw new("Edge drag didn't set user data properly");
                Port anchoredPort; 
                
                if (draggedPort.allowMultiDrag)
                {
                    foreach (BaseEdge edge in draggedPort.Connections.ToArray())
                    {
                        if (edge.Selected)
                        {
                            anchoredPort = draggedPort == draggedEdge.Input ? edge.Output : edge.Input;
                            ConnectToPortWithEdge(anchoredPort, edge);
                        }
                    }
                }
                else
                {
                    anchoredPort = draggedPort == draggedEdge.Input ? draggedEdge.Output : draggedEdge.Input;
                    ConnectToPortWithEdge(anchoredPort, draggedEdge);
                }
                
                // Update caps
                UpdateCapColor();
            }
        }

        public void OnDropExit(IDropExitContext context)
        {
            if (context.Draggable is BaseEdge)
            {
                UpdateCapColor();
            }
        }

        private void ConnectToPortWithEdge(Port anchoredPort, BaseEdge draggedEdge)
        {
            // Cancel invalid drags or already extant edges  
            if (SameDirection(anchoredPort) 
                || IsOnSameNode(anchoredPort)
                || !CanConnectToMore() 
                || !anchoredPort.CanConnectToMore() 
                || anchoredPort.IsConnectedTo(this, false))
            {
                return;
            }
                
            // Capture the ports being connected
            Port inputPort;
            Port outputPort;
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
                
            // If this is an existing edge, delete it and clean up the temporary edge object
            if (draggedEdge.IsRealEdge()) ParentNode.Graph.OnEdgeDelete(draggedEdge);
            // This was a temporary edge, reset it and remove from the graph
            else ParentNode.Graph.RemoveElement(draggedEdge); 
                
            // Create new edge (reusing the old deleted edge)
            draggedEdge.Input = inputPort;
            draggedEdge.Output = outputPort;
            draggedEdge.Selected = false;
            ParentNode.Graph.OnEdgeCreate(draggedEdge);
        }
        #endregion
        
        public override string ToString() => PortName;
    }
}


// [EventInterest(typeof(MouseEnterEvent), typeof(MouseLeaveEvent), typeof(MouseUpEvent))]
// protected override void ExecuteDefaultAction(EventBase evt)
// {
//     base.ExecuteDefaultAction(evt);
//
//     if (m_ConnectorBox == null || m_ConnectorBoxCap == null) { return; }
//
//     // Only update the box cap background if the port is enabled or highlighted.
//     if (Highlight)
//     {
//         if (evt.eventTypeId == MouseEnterEvent.TypeId())
//         {
//             m_ConnectorBoxCap.style.backgroundColor = PortColor;
//         }
//         else if (evt.eventTypeId == MouseLeaveEvent.TypeId()) { UpdateCapColor(); }
//     }
//     else if (evt.eventTypeId == MouseUpEvent.TypeId())
//     {
//         // When an edge connect ends, we need to clear out the hover states
//         MouseUpEvent mouseUp = (MouseUpEvent)evt;
//         if (!layout.Contains(mouseUp.localMousePosition)) { UpdateCapColor(); }
//     }
// }