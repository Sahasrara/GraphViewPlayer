// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.StyleSheets;

namespace GraphViewPlayer
{
    public class Port : GraphElement
    {
        private static CustomStyleProperty<Color> s_PortColorProperty = new CustomStyleProperty<Color>("--port-color");
        private static CustomStyleProperty<Color> s_DisabledPortColorProperty = new CustomStyleProperty<Color>("--disabled-port-color");

        private static readonly Color s_DefaultColor = new Color(240 / 255f, 240 / 255f, 240 / 255f);
        private static readonly Color s_DefaultDisabledColor = new Color(70 / 255f, 70 / 255f, 70 / 255f);

        protected EdgeConnector m_EdgeConnector;

        protected VisualElement m_ConnectorBox;
        protected Label m_ConnectorText;

        protected VisualElement m_ConnectorBoxCap;

        protected GraphView m_GraphView;

        public override bool showInMiniMap => false;

        public bool allowMultiDrag { get; set; } = true;

        internal Color capColor
        {
            get
            {
                if (m_ConnectorBoxCap == null)
                    return Color.black;
                return m_ConnectorBoxCap.resolvedStyle.backgroundColor;
            }

            set
            {
                if (m_ConnectorBoxCap != null)
                    m_ConnectorBoxCap.style.backgroundColor = value;
            }
        }

        public string portName
        {
            get { return m_ConnectorText.text; }
            set { m_ConnectorText.text = value; }
        }

        internal bool alwaysVisible { get; set; }

        private bool m_portCapLit;
        public bool portCapLit
        {
            get
            {
                return m_portCapLit;
            }
            set
            {
                if (value == m_portCapLit)
                    return;
                m_portCapLit = value;
                UpdateCapColor();
            }
        }

        public Direction direction
        {
            get { return m_Direction; }
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

        public Orientation orientation { get; private set; }

        public enum Capacity
        {
            Single,
            Multi
        }

        public Capacity capacity { get; private set; }

        public EdgeConnector edgeConnector
        {
            get { return m_EdgeConnector; }
        }

        public object source { get; set; }

        private bool m_Highlight = true;
        public bool highlight
        {
            get
            {
                return m_Highlight;
            }
            set
            {
                if (m_Highlight == value)
                    return;

                m_Highlight = value;

                UpdateConnectorColorAndEnabledState();
            }
        }
        public virtual void OnStartEdgeDragging()
        {
            highlight = false;
        }

        public virtual void OnStopEdgeDragging()
        {
            highlight = true;
        }

        private HashSet<Edge> m_Connections;
        private Direction m_Direction;

        public virtual IEnumerable<Edge> connections
        {
            get
            {
                return m_Connections;
            }
        }

        public virtual bool connected
        {
            get
            {
                return m_Connections.Count > 0;
            }
        }

        Color m_PortColor = s_DefaultColor;
        bool m_PortColorIsInline;
        public Color portColor
        {
            get { return m_PortColor; }
            set
            {
                m_PortColorIsInline = true;
                m_PortColor = value;
                UpdateCapColor();
            }
        }

        Color m_DisabledPortColor = s_DefaultDisabledColor;
        public Color disabledPortColor
        {
            get { return m_DisabledPortColor; }
        }

        internal Action<Port> OnConnect;
        internal Action<Port> OnDisconnect;

        public Edge ConnectTo(Port other)
        {
            return ConnectTo<Edge>(other);
        }

        public T ConnectTo<T>(Port other) where T : Edge, new()
        {
            if (other == null)
                throw new ArgumentNullException("Port.ConnectTo<T>() other argument is null");

            if (other.direction == this.direction)
                throw new ArgumentException("Cannot connect two ports with the same direction");

            var edge = new T();

            edge.output = direction == Direction.Output ? this : other;
            edge.input = direction == Direction.Input ? this : other;

            this.Connect(edge);
            other.Connect(edge);

            return edge;
        }

        public virtual void Connect(Edge edge)
        {
            if (edge == null)
                throw new ArgumentException("The value passed to Port.Connect is null");

            if (!m_Connections.Contains(edge))
                m_Connections.Add(edge);

            OnConnect?.Invoke(this);
            UpdateCapColor();
        }

        public virtual void Disconnect(Edge edge)
        {
            if (edge == null)
                throw new ArgumentException("The value passed to PortPresenter.Disconnect is null");

            m_Connections.Remove(edge);
            OnDisconnect?.Invoke(this);
            UpdateCapColor();
        }

        public virtual void DisconnectAll()
        {
            m_Connections.Clear();
            OnDisconnect?.Invoke(this);
            UpdateCapColor();
        }

        public virtual bool IsConnected(Port other)
        {
            foreach (Edge e in m_Connections)
            {
                if (direction == Direction.Output)
                {
                    if (e.input == other)  return true;
                }
                else
                {
                    if (e.output == other) return true;
                }
            }
            return false;
        }

        private class DefaultEdgeConnectorListener : IEdgeConnectorListener
        {
            private GraphViewChange m_GraphViewChange;
            private List<Edge> m_EdgesToCreate;
            private List<GraphElement> m_EdgesToDelete;

            public DefaultEdgeConnectorListener()
            {
                m_EdgesToCreate = new List<Edge>();
                m_EdgesToDelete = new List<GraphElement>();

                m_GraphViewChange.edgesToCreate = m_EdgesToCreate;
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position) {}
            public void OnDrop(GraphView graphView, Edge edge)
            {
                m_EdgesToCreate.Clear();
                m_EdgesToCreate.Add(edge);

                // We can't just add these edges to delete to the m_GraphViewChange
                // because we want the proper deletion code in GraphView to also
                // be called. Of course, that code (in DeleteElements) also
                // sends a GraphViewChange.
                m_EdgesToDelete.Clear();
                if (edge.input.capacity == Capacity.Single)
                    foreach (Edge edgeToDelete in edge.input.connections)
                        if (edgeToDelete != edge)
                            m_EdgesToDelete.Add(edgeToDelete);
                if (edge.output.capacity == Capacity.Single)
                    foreach (Edge edgeToDelete in edge.output.connections)
                        if (edgeToDelete != edge)
                            m_EdgesToDelete.Add(edgeToDelete);
                if (m_EdgesToDelete.Count > 0)
                    graphView.DeleteElements(m_EdgesToDelete);

                var edgesToCreate = m_EdgesToCreate;
                if (graphView.graphViewChanged != null)
                {
                    edgesToCreate = graphView.graphViewChanged(m_GraphViewChange).edgesToCreate;
                }

                foreach (Edge e in edgesToCreate)
                {
                    graphView.AddElement(e);
                    edge.input.Connect(e);
                    edge.output.Connect(e);
                }
            }
        }

        // TODO This is a workaround to avoid having a generic type for the port as generic types mess with USS.
        public static Port Create<TEdge>(Orientation orientation, Direction direction, Capacity capacity) where TEdge : Edge, new()
        {
            var connectorListener = new DefaultEdgeConnectorListener();
            var port = new Port(orientation, direction, capacity)
            {
                m_EdgeConnector = new EdgeConnector<TEdge>(connectorListener),
            };
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }

        protected Port(Orientation portOrientation, Direction portDirection, Capacity portCapacity)
        {
            // currently we don't want to be styled as .graphElement since we're contained in a Node
            ClearClassList();

            // Label
            m_ConnectorText = new Label();
            m_ConnectorText.AddToClassList("port-label");
            this.Add(m_ConnectorText);

            // Cap
            m_ConnectorBoxCap = new();
            m_ConnectorBoxCap.AddToClassList("port-connector-cap");

            // Box
            m_ConnectorBox = new();
            m_ConnectorBox.AddToClassList("port-connector-box");
            m_ConnectorBox.Add(m_ConnectorBoxCap);
            this.Add(m_ConnectorBox);

            m_Connections = new HashSet<Edge>();

            orientation = portOrientation;
            direction = portDirection;
            capacity = portCapacity;

            AddToClassList("port");
            AddToClassList($"port-{portDirection.ToString().ToLower()}");
        }

        public Node node
        {
            get { return GetFirstAncestorOfType<Node>(); }
        }

        public override Vector3 GetGlobalCenter()
            => m_ConnectorBox.LocalToWorld(
                new Rect(Vector2.zero, m_ConnectorBox.layout.size).center);

        public override bool ContainsPoint(Vector2 localPoint)
        {
            Rect lRect = m_ConnectorBox.layout;

            Rect boxRect;
            if (direction == Direction.Input)
            {
                boxRect = new Rect(-lRect.xMin, -lRect.yMin,
                    lRect.width + lRect.xMin, resolvedStyle.height);

                boxRect.width += m_ConnectorText.layout.xMin - lRect.xMax;
            }
            else
            {
                boxRect = new Rect(0, -lRect.yMin,
                    resolvedStyle.width - lRect.xMin, resolvedStyle.height);
                float leftSpace = lRect.xMin - m_ConnectorText.layout.xMax;

                boxRect.xMin -= leftSpace;
                boxRect.width += leftSpace;
            }

            return boxRect.Contains(this.ChangeCoordinatesTo(m_ConnectorBox, localPoint));
        }

        internal void UpdateCapColor()
        {
            if (portCapLit || connected)
            {
                m_ConnectorBoxCap.style.backgroundColor = portColor;
            }
            else
            {
                m_ConnectorBoxCap.style.backgroundColor = StyleKeyword.Null;
            }
        }

        private void UpdateConnectorColorAndEnabledState()
        {
            if (m_ConnectorBox == null)
                return;

            var color = highlight ? m_PortColor : m_DisabledPortColor;
            m_ConnectorBox.style.borderLeftColor = color;
            m_ConnectorBox.style.borderTopColor = color;
            m_ConnectorBox.style.borderRightColor = color;
            m_ConnectorBox.style.borderBottomColor = color;
            m_ConnectorBox.SetEnabled(highlight);
        }

        [EventInterest(typeof(MouseEnterEvent), typeof(MouseLeaveEvent), typeof(MouseUpEvent))]
        protected override void ExecuteDefaultAction(EventBase evt)
        {
            base.ExecuteDefaultAction(evt);

            if (m_ConnectorBox == null || m_ConnectorBoxCap == null)
            {
                return;
            }

            // Only update the box cap background if the port is enabled or highlighted.
            if (highlight)
            {
                if (evt.eventTypeId == MouseEnterEvent.TypeId())
                {
                    m_ConnectorBoxCap.style.backgroundColor = portColor;
                }
                else if (evt.eventTypeId == MouseLeaveEvent.TypeId())
                {
                    UpdateCapColor();
                }
            }
            else if (evt.eventTypeId == MouseUpEvent.TypeId())
            {
                // When an edge connect ends, we need to clear out the hover states
                var mouseUp = (MouseUpEvent)evt;
                if (!layout.Contains(mouseUp.localMousePosition))
                {
                    UpdateCapColor();
                }
            }
        }

        protected override void OnCustomStyleResolved(ICustomStyle styles)
        {
            base.OnCustomStyleResolved(styles);

            Color portColorValue = Color.clear;
            Color disableColorValue = Color.clear;

            if (!m_PortColorIsInline && styles.TryGetValue(s_PortColorProperty, out portColorValue))
            {
                m_PortColor = portColorValue;
                UpdateCapColor();
            }

            if (styles.TryGetValue(s_DisabledPortColorProperty, out disableColorValue))
                m_DisabledPortColor = disableColorValue;

            UpdateConnectorColorAndEnabledState();
        }
    }
}
