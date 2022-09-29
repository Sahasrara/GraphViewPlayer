// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Profiling;

namespace GraphViewPlayer
{
    public class Edge : GraphElement
    {
        private const float k_EndPointRadius = 4.0f;
        private const float k_InterceptWidth = 6.0f;

        private static CustomStyleProperty<int> s_EdgeWidthProperty = new("--edge-width");
        private static CustomStyleProperty<int> s_EdgeWidthSelectedProperty = new("--edge-width-selected");
        private static CustomStyleProperty<Color> s_EdgeColorSelectedProperty = new("--edge-color-selected");
        private static CustomStyleProperty<Color> s_EdgeColorGhostProperty = new("--edge-color-ghost");
        private static CustomStyleProperty<Color> s_EdgeColorProperty = new("--edge-color");

        private static readonly int s_DefaultEdgeWidth = 2;
        private static readonly int s_DefaultEdgeWidthSelected = 2;
        private static readonly Color s_DefaultSelectedColor = new(240 / 255f, 240 / 255f, 240 / 255f);
        private static readonly Color s_DefaultColor = new(146 / 255f, 146 / 255f, 146 / 255f);
        private static readonly Color s_DefaultGhostColor = new(85 / 255f, 85 / 255f, 85 / 255f);

        private Port m_OutputPort;
        private Port m_InputPort;
        private GraphView m_GraphView;

        public bool isGhostEdge { get; set; }

        public Port output
        {
            get => m_OutputPort;
            set => SetPort(ref m_OutputPort, value, SetDrawCapFrom);
        }

        public override bool showInMiniMap => false;

        public Port input
        {
            get => m_InputPort;
            set => SetPort(ref m_InputPort, value, SetDrawCapTo);
        }

        private void SetDrawCapFrom(bool shouldDraw) => edgeControl.drawFromCap = shouldDraw; 
        private void SetDrawCapTo(bool shouldDraw) => edgeControl.drawToCap = shouldDraw; 
        private void SetPort(ref Port portToSet, Port newPort, Action<bool> drawCapSetter)
        {
            if (newPort != portToSet)
            {
                // Clean Up Old Connection
                if (portToSet != null)
                {
                    UntrackGraphElement(portToSet);
                    portToSet.Disconnect(this);
                }
                    
                // Setup New Connection
                portToSet = newPort;
                if (portToSet != null)
                {
                    TrackGraphElement(portToSet);
                    portToSet.Connect(this);
                    drawCapSetter(true);
                }
                else drawCapSetter(false);
                    
                // Mark Dirty
                m_EndPointsDirty = true; 
                OnPortChanged(false);
            } 
        }

        EdgeControl m_EdgeControl;
        public EdgeControl edgeControl
        {
            get
            {
                if (m_EdgeControl == null)
                {
                    m_EdgeControl = CreateEdgeControl();
                }
                return m_EdgeControl;
            }
        }

        public bool InputPositionOverridden => m_InputPositionOverridden;
        private bool m_InputPositionOverridden;
        private bool m_OutputPositionOverridden;
        private Vector2 m_InputPositionOverride;
        private Vector2 m_OutputPositionOverride;

        public void SetInputPositionOverride(Vector2 position) 
            => SetPositionOverride(
                position, ref m_InputPositionOverride, ref m_InputPositionOverridden, ref m_InputPort);

        public void SetOutputPositionOverride(Vector2 position) 
            => SetPositionOverride(
                position, ref m_OutputPositionOverride, ref m_OutputPositionOverridden, ref m_OutputPort);

        public void SetPositionOverride(Vector2 position, ref Vector2 overrideValueToSet, 
            ref bool overrideFlagToSet, ref Port portToUpdate)
        {
            if (overrideFlagToSet || overrideValueToSet != position) m_EndPointsDirty = true;
            overrideFlagToSet = true;
            overrideValueToSet = position;
            UpdateEdgeControl();
            portToUpdate?.UpdateCapColor();
        }

        public void UnsetPositionOverrides()
        {
            if (m_InputPositionOverridden || m_OutputPositionOverridden) m_EndPointsDirty = true;
            m_InputPositionOverridden = false; 
            m_OutputPositionOverridden = false;
            UpdateEdgeControl();
            m_InputPort?.UpdateCapColor();
            m_OutputPort?.UpdateCapColor();
        }

        public bool IsCandidateEdge() => m_InputPositionOverridden || m_OutputPositionOverridden;

        int m_EdgeWidth = s_DefaultEdgeWidth;
        public int edgeWidth => m_EdgeWidth;

        int m_EdgeWidthSelected = s_DefaultEdgeWidthSelected;
        public int edgeWidthSelected => m_EdgeWidthSelected;

        Color m_SelectedColor = s_DefaultSelectedColor;
        public Color selectedColor => m_SelectedColor;

        Color m_DefaultColor = s_DefaultColor;
        public Color defaultColor => m_DefaultColor;

        Color m_GhostColor = s_DefaultGhostColor;
        public Color ghostColor => m_GhostColor;

        private bool m_EndPointsDirty;

        public Edge()
        {
            ClearClassList();
            AddToClassList("edge");

            Add(edgeControl);

            capabilities |= Capabilities.Selectable | Capabilities.Deletable;

            this.AddManipulator(new EdgeManipulator());
            this.AddManipulator(new ContextualMenuManipulator(null)); // TODO

            RegisterCallback<AttachToPanelEvent>(OnEdgeAttach);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            focusable = true;
        }

        public override bool Overlaps(Rect rectangle)
        {
            if (!UpdateEdgeControl())
                return false;

            return edgeControl.Overlaps(this.ChangeCoordinatesTo(edgeControl, rectangle));
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            Profiler.BeginSample("Edge.ContainsPoint");

            var result = UpdateEdgeControl() &&
                edgeControl.ContainsPoint(this.ChangeCoordinatesTo(edgeControl, localPoint));

            Profiler.EndSample();

            return result;
        }

        public void SetPortByDirection(Port port)
        {
            if (port.direction == Direction.Input) input = port;
            else output = port;
        }

        public void Disconnect()
        {
            output = null;
            input = null;
        }

        public virtual void OnPortChanged(bool isInput)
        {
            edgeControl.outputOrientation = m_OutputPort?.orientation ?? (m_InputPort?.orientation ?? Orientation.Horizontal);
            edgeControl.inputOrientation = m_InputPort?.orientation ?? (m_OutputPort?.orientation ?? Orientation.Horizontal);
            UpdateEdgeControl();
        }

        internal bool ForceUpdateEdgeControl()
        {
            m_EndPointsDirty = true;
            return UpdateEdgeControl();
        }

        public virtual bool UpdateEdgeControl()
        {
            if (m_OutputPort == null && m_InputPort == null && m_InputPositionOverridden && m_OutputPositionOverridden) 
                return false;

            if (m_GraphView == null)
                m_GraphView = GetFirstOfType<GraphView>();

            if (m_GraphView == null)
                return false;

            UpdateEdgeControlEndPoints();
            edgeControl.UpdateLayout();
            UpdateEdgeControlColorsAndWidth();

            return true;
        }

        void UpdateEdgeControlColorsAndWidth()
        {
            if (selected)
            {
                if (isGhostEdge)
                {
                    Debug.Log("Selected Ghost Edge: this should never be");
                }
                else
                {
                    edgeControl.inputColor = selectedColor;
                    edgeControl.outputColor = selectedColor;
                    edgeControl.edgeWidth = edgeWidthSelected; 
                }
            }
            else
            {
                if (isGhostEdge)
                {
                    edgeControl.inputColor = ghostColor;
                    edgeControl.outputColor = ghostColor;
                }
                else
                {
                    edgeControl.inputColor = defaultColor;
                    edgeControl.outputColor = defaultColor;
                }
                edgeControl.edgeWidth = edgeWidth;
            }
        }

        protected override void OnCustomStyleResolved(ICustomStyle styles)
        {
            base.OnCustomStyleResolved(styles);

            if (styles.TryGetValue(s_EdgeWidthProperty, out var edgeWidthValue))
                m_EdgeWidth = edgeWidthValue;
            if (styles.TryGetValue(s_EdgeWidthSelectedProperty, out var edgeWidthSelectedValue))
                m_EdgeWidthSelected = edgeWidthSelectedValue;
            if (styles.TryGetValue(s_EdgeColorSelectedProperty, out var selectColorValue))
                m_SelectedColor = selectColorValue;
            if (styles.TryGetValue(s_EdgeColorProperty, out var edgeColorValue))
                m_DefaultColor = edgeColorValue;
            if (styles.TryGetValue(s_EdgeColorGhostProperty, out var ghostColorValue))
                m_GhostColor = ghostColorValue;

            UpdateEdgeControlColorsAndWidth();
        }

        public override void OnSelected()
        {
            UpdateEdgeControlColorsAndWidth();
        }

        public override void OnUnselected()
        {
            UpdateEdgeControlColorsAndWidth();
        }

        protected virtual EdgeControl CreateEdgeControl()
        {
            return new EdgeControl
            {
                capRadius = k_EndPointRadius,
                interceptWidth = k_InterceptWidth
            };
        }

        Vector2 GetPortPosition(Port p)
        {
            Vector2 pos = p.GetGlobalCenter();
            pos = this.WorldToLocal(pos);
            return pos;
        }

        void TrackGraphElement(Port port)
        {
            if (port.panel != null) // if the panel is null therefore the port is not yet attached to its hierarchy, so postpone the register
            {
                DoTrackGraphElement(port);
            }

            port.RegisterCallback<AttachToPanelEvent>(OnPortAttach);
            port.RegisterCallback<DetachFromPanelEvent>(OnPortDetach);
        }

        void OnPortDetach(DetachFromPanelEvent e)
        {
            Port port = (Port)e.target;
            DoUntrackGraphElement(port);
        }

        void OnPortAttach(AttachToPanelEvent e)
        {
            Port port = (Port)e.target;
            DoTrackGraphElement(port);
        }

        void OnEdgeAttach(AttachToPanelEvent e)
        {
            UpdateEdgeControl();
        }

        void UntrackGraphElement(Port port)
        {
            port.UnregisterCallback<AttachToPanelEvent>(OnPortAttach);
            port.UnregisterCallback<DetachFromPanelEvent>(OnPortDetach);
            DoUntrackGraphElement(port);
        }

        void DoTrackGraphElement(Port port)
        {
            port.RegisterCallback<GeometryChangedEvent>(OnPortGeometryChanged);

            VisualElement current = port.hierarchy.parent;
            while (current != null)
            {
                if (current is GraphView.Layer)
                {
                    break;
                }
                if (current != port.node) // if we encounter our node ignore it but continue in the case there are nodes inside nodes
                {
                    current.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                }

                current = current.hierarchy.parent;
            }
            if (port.node != null)
                port.node.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        void DoUntrackGraphElement(Port port)
        {
            port.UnregisterCallback<GeometryChangedEvent>(OnPortGeometryChanged);

            VisualElement current = port.hierarchy.parent;
            while (current != null)
            {
                if (current is GraphView.Layer)
                {
                    break;
                }
                if (current != port.node) // if we encounter our node ignore it but continue in the case there are nodes inside nodes
                {
                    port.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                }

                current = current.hierarchy.parent;
            }
            if (port.node != null)
                port.node.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnPortGeometryChanged(GeometryChangedEvent evt)
        {
            Port p = evt.target as Port;

            if (p != null)
            {
                if (p == m_InputPort)
                {
                    edgeControl.to = GetPortPosition(p);
                }
                else if (p == m_OutputPort)
                {
                    edgeControl.from = GetPortPosition(p);
                }
            }

            UpdateEdgeControl();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            ForceUpdateEdgeControl();
        }

        private void UpdateEdgeControlEndPoints()
        {
            if (!m_EndPointsDirty) return;
            Profiler.BeginSample("Edge.UpdateEdgeControlEndPoints");

            // Input Location 
            if (m_InputPositionOverridden) edgeControl.to = this.WorldToLocal(m_InputPositionOverride);
            else if (m_InputPort != null) edgeControl.to = GetPortPosition(m_InputPort);
            
            // Output Location
            if (m_OutputPositionOverridden)  edgeControl.from = this.WorldToLocal(m_OutputPositionOverride);
            else if (m_OutputPort != null) edgeControl.from = GetPortPosition(m_OutputPort);
            
            m_EndPointsDirty = false;
            Profiler.EndSample();
        }
    }
}
