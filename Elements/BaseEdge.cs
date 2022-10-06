// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public abstract class BaseEdge : GraphElement, IDraggable
    {
        protected Port m_InputPort;
        private Vector2 m_InputPortPosition;
        protected bool m_InputPositionOverridden;
        protected Vector2 m_InputPositionOverride;

        protected Port m_OutputPort;
        private Vector2 m_OutputPortPosition;
        protected bool m_OutputPositionOverridden;
        protected Vector2 m_OutputPositionOverride;

        public BaseEdge() =>
            Capabilities
                |= Capabilities.Selectable
                   | Capabilities.Deletable
                   | Capabilities.Movable;


        public bool IsGhostEdge { get; set; }

        public Port Output
        {
            get => m_OutputPort;
            set => SetPort(ref m_OutputPort, value);
        }

        public Port Input
        {
            get => m_InputPort;
            set => SetPort(ref m_InputPort, value);
        }

        public Vector2 From
        {
            get
            {
                if (m_OutputPositionOverridden) { return m_OutputPositionOverride; }
                if (Output != null && Graph != null) return m_OutputPortPosition;
                return Vector2.zero;
            }
        }

        public Vector2 To
        {
            get
            {
                if (m_InputPositionOverridden) { return m_InputPositionOverride; }
                if (Input != null && Graph != null) return m_InputPortPosition;
                return Vector2.zero;
            }
        }

        public Orientation InputOrientation => Input?.Orientation ?? (Output?.Orientation ?? Orientation.Horizontal);
        public Orientation OutputOrientation => Output?.Orientation ?? (Input?.Orientation ?? Orientation.Horizontal);

        private void SetPort(ref Port portToSet, Port newPort) //, Action<bool> drawCapSetter)
        {
            if (newPort != portToSet)
            {
                // Clean Up Old Connection
                if (portToSet != null)
                {
                    UntrackPort(portToSet);
                    portToSet.Disconnect(this);
                }

                // Setup New Connection
                portToSet = newPort;
                if (portToSet != null)
                {
                    TrackPort(portToSet);
                    portToSet.Connect(this);
                }

                // Mark Dirty
                OnEdgeChanged();
            }
        }

        public bool IsRealEdge() => Input != null && Output != null;
        public bool IsCandidateEdge() => m_InputPositionOverridden || m_OutputPositionOverridden;

        #region Event Handlers
        protected virtual void OnGeometryChanged(GeometryChangedEvent e)
        {
            UpdateCachedInputPortPosition();
            UpdateCachedOutputPortPosition();
            OnEdgeChanged();
        }

        protected override void OnAddedToGraphView()
        {
            UpdateCachedInputPortPosition();
            UpdateCachedOutputPortPosition(); 
        }
        
        protected override void OnRemovedFromGraphView()
        {
            Disconnect();
            UnsetPositionOverrides();
        }
        #endregion
        
        #region Ports
        public void SetPortByDirection(Port port)
        {
            if (port.Direction == Direction.Input) { Input = port; }
            else { Output = port; }
        }
        
        public void Disconnect()
        {
            Input = null;
            Output = null;
        }
 
        private void TrackPort(Port port)
        {
            port.OnPositionChange += OnPortPositionChanged;

            VisualElement current = port.hierarchy.parent;
            while (current != null)
            {
                if (current is GraphView.Layer) { break; }

                // if we encounter our node ignore it but continue in the case there are nodes inside nodes
                if (current != port.ParentNode) { current.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged); }

                current = current.hierarchy.parent;
            }
            if (port.ParentNode != null) { port.ParentNode.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged); }

            OnPortPositionChanged(new() { element = port });
        }

        private void UntrackPort(Port port)
        {
            port.OnPositionChange -= OnPortPositionChanged;

            VisualElement current = port.hierarchy.parent;
            while (current != null)
            {
                if (current is GraphView.Layer) { break; }

                // if we encounter our node ignore it but continue in the case there are nodes inside nodes
                if (current != port.ParentNode) { port.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged); }

                current = current.hierarchy.parent;
            }
            if (port.ParentNode != null)
            {
                port.ParentNode.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
        }

        private void OnPortPositionChanged(PositionData changeData)
        {
            Port p = changeData.element as Port;
            if (p != null && Graph != null)
            {
                if (p == m_InputPort) { UpdateCachedInputPortPosition(); }
                else if (p == m_OutputPort) { UpdateCachedOutputPortPosition(); }
                OnEdgeChanged();
            }
        }

        private void UpdateCachedInputPortPosition()
        {
            m_InputPortPosition = Graph.contentViewContainer.WorldToLocal(Input.GetGlobalCenter());
            if (!m_InputPositionOverridden) m_InputPositionOverride = m_InputPortPosition;
        }

        private void UpdateCachedOutputPortPosition()
        {
            m_OutputPortPosition = Graph.contentViewContainer.WorldToLocal(Output.GetGlobalCenter());
            if (!m_OutputPositionOverridden) m_OutputPositionOverride = m_OutputPortPosition;
        }
        #endregion

        #region Position
        protected abstract void OnEdgeChanged();

        public Vector2 GetInputPositionOverride() => m_InputPositionOverride;
        public Vector2 GetOutputPositionOverride() => m_OutputPositionOverride;
        public bool IsInputPositionOverriden() => m_InputPositionOverridden;
        public bool IsOutputPositionOverriden() => m_OutputPositionOverridden;

        public void SetInputPositionOverride(Vector2 position)
            => SetPositionOverride(
                position, out m_InputPositionOverride, out m_InputPositionOverridden, ref m_InputPort);

        public void SetOutputPositionOverride(Vector2 position)
            => SetPositionOverride(
                position, out m_OutputPositionOverride, out m_OutputPositionOverridden, ref m_OutputPort);

        private void SetPositionOverride(Vector2 position, out Vector2 overrideValueToSet,
            out bool overrideFlagToSet, ref Port portToUpdate)
        {
            overrideFlagToSet = true;
            overrideValueToSet = position;
            portToUpdate?.UpdateCapColor();
            OnEdgeChanged();
        }

        public void UnsetPositionOverrides()
        {
            m_InputPositionOverridden = false;
            m_OutputPositionOverridden = false;
            m_InputPort?.UpdateCapColor();
            m_OutputPort?.UpdateCapColor();
            OnEdgeChanged();
        }

        public override Vector2 GetCenter()
        {
            if (m_InputPositionOverridden) return m_InputPositionOverride;
            if (m_OutputPositionOverridden) return m_OutputPositionOverride;
            return base.GetCenter();
        }
        #endregion

        #region IDraggable
        public void OnDragBegin(IDragBeginContext context)
        {
            // Check if we need to cancel
            if (context.IsCancelled()) { return; }
            if (Graph == null || !IsMovable())
            {
                context.CancelDrag();
                return;
            }

            // Set Drag Threshold
            context.SetDragThreshold(10);

            // Track for panning
            Graph.TrackElementForPan(this);
        }

        public void OnDrag(IDragContext context)
        {
            // Check if we need to cancel
            if (context.IsCancelled()) { return; }
            if (Graph == null || !IsMovable())
            {
                context.CancelDrag();
                return;
            }

            // This is the first time we breached the drag threshold
            bool isDragStart = context.GetUserData() == null;
            if (isDragStart)
            {
                // Record detached port (whichever is closest to the mouse)
                Vector2 inputPos = Input.GetGlobalCenter();
                Vector2 outputPos = Output.GetGlobalCenter();
                float distanceFromInput = (context.MousePosition - inputPos).sqrMagnitude;
                float distanceFromOutput = (context.MousePosition - outputPos).sqrMagnitude;
                context.SetUserData(distanceFromInput < distanceFromOutput ? Input : Output);
            }

            // Grab detached port
            Port draggedPort = context.GetUserData() as Port;
            if (draggedPort == null) throw new("Edge drag didn't set user data properly"); 
            // Vector2 newPosition = GetOutputPositionOverride() + context.MouseDelta / Graph.CurrentScale; 
            Vector2 newPosition = Graph.contentViewContainer.WorldToLocal(context.MousePosition);
            if (draggedPort.allowMultiDrag)
            {
                foreach (BaseEdge edge in draggedPort.Connections)
                {
                    if (edge.Selected)
                    {
                        if (draggedPort.Direction == Direction.Input) { edge.SetInputPositionOverride(newPosition); }
                        else { edge.SetOutputPositionOverride(newPosition); }
                    }
                }
            }
            else
            {
                if (draggedPort.Direction == Direction.Output) { SetInputPositionOverride(newPosition); }
                else { SetOutputPositionOverride(newPosition); }
            }

            // Only light compatible anchors when dragging an edge.
            if (isDragStart) IlluminateCompatiblePorts(Input == draggedPort ? Output : Input);
        }

        public void OnDragEnd(IDragEndContext context)
        {
            // Could have been deleted
            if (Graph == null) return;
            
            // Untrack for panning
            Graph.UntrackElementForPan(this);
            
            // Reset position
            foreach (BaseEdge edge in Graph.EdgesSelected)
            {
                edge.UnsetPositionOverrides();
            }
            
            // Reset ports
            IlluminateAllPorts();
        }

        public void OnDragCancel(IDragCancelContext context)
        {
            // Could have been deleted
            if (Graph == null) return;

            // Untrack for panning
            Graph.UntrackElementForPan(this, true);
            
            // Reset position
            foreach (BaseEdge edge in Graph.EdgesSelected)
            {
                edge.UnsetPositionOverrides();
            }
            
            // Reset ports
            IlluminateAllPorts();
        }

        private void IlluminateCompatiblePorts(Port port)
        {
            foreach (Port otherPort in Graph.Ports) otherPort.Highlight = port.CanConnectTo(otherPort);
        }

        private void IlluminateAllPorts()
        {
            foreach (Port otherPort in Graph.Ports) otherPort.Highlight = true;
        }
        #endregion
    }
}