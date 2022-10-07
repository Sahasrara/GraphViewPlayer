// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public abstract class GraphView : VisualElement, ISelector
    {
        private const int k_FrameBorder = 30;
        private const int k_PanAreaWidth = 100;
        private const int k_PanSpeed = 4;
        private const int k_PanInterval = 10;
        private const float k_MinSpeedFactor = 0.5f;
        private const float k_MaxSpeedFactor = 2.5f;
        private const float k_MaxPanSpeed = k_MaxSpeedFactor * k_PanSpeed;
        private const float k_PanAreaWidthAndMinSpeedFactor = k_PanAreaWidth + k_MinSpeedFactor;

        private static StyleSheet s_DefaultStyle;

        private readonly Dictionary<int, Layer> m_ContainerLayers;

        #region Constructor
        protected GraphView()
        {
            //
            // RUIGraphView - Level 0
            //
            // Style
            styleSheets.Add(DefaultStyle);
            AddToClassList("graph-view");

            // Usage Hints
            usageHints = UsageHints.MaskContainer; // Should equate to RenderHints.ClipWithScissors

            // Manipulators
            m_Zoomer = new();
            this.AddManipulator(m_Zoomer);
            this.AddManipulator(new DragAndDropManipulator());

            //
            // Graph View Container & Grid - Level 1
            //
            // Root Container
            GraphViewContainer = new GridBackground();
            hierarchy.Add(GraphViewContainer);

            //
            // Rectangular Selection and Droppable Backstop
            //
            Backstop = new(this);
            GraphViewContainer.Add(Backstop);

            //
            // Content Container - Level 2
            //
            // Content Container
            ContentContainer = new ContentView
            {
                pickingMode = PickingMode.Ignore,
                usageHints = UsageHints.GroupTransform
            };
            ContentContainer.AddToClassList("content-view-container");
            GraphViewContainer.Add(ContentContainer);

            //
            // Other Initialization
            //
            // Cached Queries
            ElementsAll = ContentContainer.Query<GraphElement>().Build();
            ElementsSelected = ContentContainer.Query<GraphElement>().Where(WhereSelected).Build();
            ElementsUnselected = ContentContainer.Query<GraphElement>().Where(WhereUnselected).Build();

            Nodes = ContentContainer.Query<Node>().Build();
            NodesSelected = ContentContainer.Query<Node>().Where(WhereSelected).Build();
            Edges = this.Query<Layer>().Children<BaseEdge>().Build();
            EdgesSelected = this.Query<Layer>().Children<BaseEdge>().Where(WhereSelected).Build();
            Ports = ContentContainer.Query().Children<Layer>().Descendents<BasePort>().Build();

            // Layers
            m_ContainerLayers = new();

            // Panning
            m_PanSchedule = schedule
                .Execute(Pan)
                .Every(k_PanInterval)
                .StartingIn(k_PanInterval);
            m_PanSchedule.Pause();

            // Focus
            focusable = true;
        }
        #endregion

        private static StyleSheet DefaultStyle
        {
            get
            {
                if (s_DefaultStyle == null)
                {
                    s_DefaultStyle = Resources.Load<StyleSheet>("GraphViewPlayer/GraphView");
                }
                return s_DefaultStyle;
            }
        }

        #region Properties
        internal ViewTransformChanged OnViewTransformChanged { get; set; }

        private GraphViewBackstop Backstop { get; }
        private VisualElement GraphViewContainer { get; }
        public VisualElement ContentContainer { get; }
        public ITransform ViewTransform => ContentContainer.transform;

        public UQueryState<Node> Nodes { get; }
        public UQueryState<Node> NodesSelected { get; }
        public UQueryState<BasePort> Ports { get; }
        public UQueryState<BaseEdge> Edges { get; }
        public UQueryState<BaseEdge> EdgesSelected { get; }
        public UQueryState<GraphElement> ElementsAll { get; }
        public UQueryState<GraphElement> ElementsSelected { get; }
        public UQueryState<GraphElement> ElementsUnselected { get; }
        #endregion

        #region Factories
        public virtual BaseEdge CreateEdge() => new Edge();

        public virtual BasePort CreatePort(Orientation orientation, Direction direction, PortCapacity capacity)
            => new(orientation, direction, capacity);
        #endregion

        #region View Transform
        internal delegate void ViewTransformChanged(GraphView graphView);

        public void UpdateViewTransform(Vector3 newPosition)
            => UpdateViewTransform(newPosition, ViewTransform.scale);

        public void UpdateViewTransform(Vector3 newPosition, Vector3 newScale)
        {
            float validateFloat = newPosition.x + newPosition.y + newPosition.z + newScale.x + newScale.y + newScale.z;
            if (float.IsInfinity(validateFloat) || float.IsNaN(validateFloat)) { return; }

            ViewTransform.scale = newScale;
            ViewTransform.position = newPosition;

            OnViewTransformChanged?.Invoke(this);
            OnViewportChanged();
        }
        #endregion

        #region Pan
        private GraphElement m_PanElement;
        private Vector2 m_PanOriginDiff;
        private bool m_PanElementIsNode;
        private readonly IVisualElementScheduledItem m_PanSchedule;

        internal void TrackElementForPan(GraphElement element)
        {
            m_PanOriginDiff = Vector2.zero;
            m_PanElement = element;
            m_PanElementIsNode = element is Node;
            m_PanSchedule.Resume();
        }

        internal Vector2 UntrackElementForPan(GraphElement element, bool resetView = false)
        {
            if (element == m_PanElement)
            {
                m_PanSchedule.Pause();
                m_PanElement = null;
                if (resetView) { UpdateViewTransform((Vector2)ViewTransform.position + m_PanOriginDiff); }
                return m_PanOriginDiff;
            }
            return Vector2.zero;
        }

        private Vector2 GetEffectivePanSpeed(Vector2 mousePos)
        {
            Vector2 effectiveSpeed = Vector2.zero;

            if (mousePos.x <= k_PanAreaWidth)
            {
                effectiveSpeed.x = -((k_PanAreaWidth - mousePos.x) / k_PanAreaWidthAndMinSpeedFactor) * k_PanSpeed;
            }
            else if (mousePos.x >= GraphViewContainer.layout.width - k_PanAreaWidth)
            {
                effectiveSpeed.x = (mousePos.x - (GraphViewContainer.layout.width - k_PanAreaWidth))
                    / k_PanAreaWidthAndMinSpeedFactor * k_PanSpeed;
            }

            if (mousePos.y <= k_PanAreaWidth)
            {
                effectiveSpeed.y = -((k_PanAreaWidth - mousePos.y) / k_PanAreaWidthAndMinSpeedFactor) * k_PanSpeed;
            }
            else if (mousePos.y >= GraphViewContainer.layout.height - k_PanAreaWidth)
            {
                effectiveSpeed.y = (mousePos.y - (GraphViewContainer.layout.height - k_PanAreaWidth))
                    / k_PanAreaWidthAndMinSpeedFactor * k_PanSpeed;
            }

            return Vector2.ClampMagnitude(effectiveSpeed, k_MaxPanSpeed);
        }

        private void Pan()
        {
            // Use element center as the point to test against the bounds of the pan area
            Vector2 elementPositionWorld = m_PanElement.GetGlobalCenter();

            // If the point has entered the bounds of the pan area, calculate how fast we want to pan
            Vector2 speed = GetEffectivePanSpeed(elementPositionWorld);
            if (Vector2.zero == speed) { return; }

            // Record changes in pan
            m_PanOriginDiff += speed;

            // Update the view transform (to pan)
            UpdateViewTransform((Vector2)ViewTransform.position - speed);

            // Speed is scaled according to the current zoom level
            Vector2 localSpeed = speed / CurrentScale;

            // Set position
            if (m_PanElementIsNode)
            {
                // Nodes
                foreach (Node selectedNode in NodesSelected)
                {
                    selectedNode.SetPosition(selectedNode.GetPosition() + localSpeed);
                }
            }
            else
            {
                // Edges
                BaseEdge edge = (BaseEdge)m_PanElement;
                if (edge.IsInputPositionOverriden())
                {
                    edge.SetInputPositionOverride(edge.GetInputPositionOverride() + localSpeed);
                }
                else { edge.SetOutputPositionOverride(edge.GetOutputPositionOverride() + localSpeed); }
            }
        }
        #endregion

        #region Layers
        internal void ChangeLayer(GraphElement element) { GetLayer(element.Layer).Add(element); }

        private VisualElement GetLayer(int index)
        {
            if (!m_ContainerLayers.TryGetValue(index, out Layer layer))
            {
                layer = new() { name = $"Layer {index}" };
                m_ContainerLayers[index] = layer;

                // TODO
                int indexOfLayer = m_ContainerLayers.OrderBy(t => t.Key).Select(t => t.Value).ToList().IndexOf(layer);
                ContentContainer.Insert(indexOfLayer, layer);
            }
            return layer;
        }
        #endregion

        #region Zoom
        private readonly ZoomManipulator m_Zoomer;

        public float MinScale
        {
            get => m_Zoomer.minScale;
            set
            {
                m_Zoomer.minScale = Math.Min(value, ZoomManipulator.DefaultMinScale);
                ValidateTransform();
            }
        }

        public float MaxScale
        {
            get => m_Zoomer.maxScale;
            set
            {
                m_Zoomer.maxScale = Math.Max(value, ZoomManipulator.DefaultMaxScale);
                ValidateTransform();
            }
        }

        public float ScaleStep
        {
            get => m_Zoomer.scaleStep;
            set
            {
                m_Zoomer.scaleStep = Math.Min(value, (MaxScale - MinScale) / 2);
                ValidateTransform();
            }
        }

        public float ReferenceScale
        {
            get => m_Zoomer.referenceScale;
            set
            {
                m_Zoomer.referenceScale = Math.Clamp(value, MinScale, MaxScale);
                ValidateTransform();
            }
        }

        public float CurrentScale => ViewTransform.scale.x;

        protected void ValidateTransform()
        {
            if (ContentContainer == null) { return; }
            Vector3 transformScale = ViewTransform.scale;

            transformScale.x = Mathf.Clamp(transformScale.x, MinScale, MaxScale);
            transformScale.y = Mathf.Clamp(transformScale.y, MinScale, MaxScale);

            UpdateViewTransform(ViewTransform.position, transformScale);
        }
        #endregion

        #region Selection
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

        #region Keybinding
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsUnmodified(EventModifiers modifiers) => modifiers == EventModifiers.None;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCommand(EventModifiers modifiers) => (modifiers & EventModifiers.Command) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsShift(EventModifiers modifiers) => modifiers == EventModifiers.Shift;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFunction(EventModifiers modifiers) => (modifiers & EventModifiers.FunctionKey) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCommandExclusive(EventModifiers modifiers) => modifiers == EventModifiers.Command;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsControlExclusive(EventModifiers modifiers) => modifiers == EventModifiers.Control;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMac()
            => Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;

        [EventInterest(typeof(KeyDownEvent))]
        protected override void ExecuteDefaultAction(EventBase baseEvent)
        {
            if (baseEvent is not KeyDownEvent evt) { return; }
            if (panel.GetCapturingElement(PointerId.mousePointerId) != null) { return; }

            // Check for CMD or CTRL
            switch (evt.keyCode)
            {
                case KeyCode.C:
                    if (IsMac())
                    {
                        if (IsCommandExclusive(evt.modifiers))
                        {
                            OnCopy();
                            evt.StopPropagation();
                        }
                    }
                    else
                    {
                        if (IsControlExclusive(evt.modifiers))
                        {
                            OnCopy();
                            evt.StopPropagation();
                        }
                    }
                    break;
                case KeyCode.V:
                    if (IsMac())
                    {
                        if (IsCommandExclusive(evt.modifiers))
                        {
                            OnPaste();
                            evt.StopPropagation();
                        }
                    }
                    else
                    {
                        if (IsControlExclusive(evt.modifiers))
                        {
                            OnPaste();
                            evt.StopPropagation();
                        }
                    }
                    break;
                case KeyCode.X:
                    if (IsMac())
                    {
                        if (IsCommandExclusive(evt.modifiers))
                        {
                            OnCut();
                            evt.StopPropagation();
                        }
                    }
                    else
                    {
                        if (IsControlExclusive(evt.modifiers))
                        {
                            OnCut();
                            evt.StopPropagation();
                        }
                    }
                    break;
                case KeyCode.D:
                    if (IsMac())
                    {
                        if (IsCommandExclusive(evt.modifiers))
                        {
                            OnDuplicate();
                            evt.StopPropagation();
                        }
                    }
                    else
                    {
                        if (IsControlExclusive(evt.modifiers))
                        {
                            OnDuplicate();
                            evt.StopPropagation();
                        }
                    }
                    break;
                case KeyCode.Z:
                    if (IsMac())
                    {
                        if (IsCommandExclusive(evt.modifiers))
                        {
                            OnUndo();
                            evt.StopPropagation();
                        }
                        else if (IsShift(evt.modifiers) && IsCommand(evt.modifiers))
                        {
                            OnRedo();
                            evt.StopPropagation();
                        }
                    }
                    else
                    {
                        if (IsControlExclusive(evt.modifiers))
                        {
                            OnUndo();
                            evt.StopPropagation();
                        }
                    }
                    break;
                case KeyCode.Y:
                    if (!IsMac() && IsControlExclusive(evt.modifiers))
                    {
                        OnRedo();
                        evt.StopPropagation();
                    }
                    break;
                case KeyCode.Delete:
                    if (IsMac())
                    {
                        if (IsUnmodified(evt.modifiers))
                        {
                            OnDelete();
                            evt.StopPropagation();
                        }
                    }
                    else
                    {
                        if (IsUnmodified(evt.modifiers))
                        {
                            OnDelete();
                            evt.StopPropagation();
                        }
                    }
                    break;
                case KeyCode.Backspace:
                    if (IsMac() && IsCommand(evt.modifiers) && IsFunction(evt.modifiers))
                    {
                        OnDelete();
                        evt.StopPropagation();
                    }
                    break;
                case KeyCode.F:
                    if (IsUnmodified(evt.modifiers))
                    {
                        Frame();
                        evt.StopPropagation();
                    }
                    break;
            }
        }
        #endregion

        #region Add / Remove Elements from Heirarchy
        public void AddElement(GraphElement graphElement)
        {
            graphElement.Graph = this;
            GetLayer(graphElement.Layer).Add(graphElement);
        }

        public void RemoveElement(GraphElement graphElement)
        {
            UntrackElementForPan(graphElement); // Stop panning if we were panning
            graphElement.RemoveFromHierarchy();
            graphElement.Graph = null;
        }
        #endregion

        #region Ports
        public void ConnectPorts(BasePort input, BasePort output) { AddElement(input.ConnectTo(output)); }

        internal void IlluminateCompatiblePorts(BasePort port)
        {
            foreach (BasePort otherPort in Ports) { otherPort.Highlight = port.CanConnectTo(otherPort); }
        }

        internal void IlluminateAllPorts()
        {
            foreach (BasePort otherPort in Ports) { otherPort.Highlight = true; }
        }
        #endregion

        #region Framing
        protected void Frame()
        {
            // Construct rect for selected and unselected elements
            Rect rectToFitSelected = ContentContainer.layout;
            Rect rectToFitUnselected = rectToFitSelected;
            bool reachedFirstSelected = false;
            bool reachedFirstUnselected = false;
            foreach (GraphElement ge in ElementsAll)
            {
                // TODO: edge control is the VisualElement with actual dimensions
                // VisualElement ve = ge is BaseEdge edge ? edge.EdgeControl : ge;
                if (ge.Selected)
                {
                    if (!reachedFirstSelected)
                    {
                        rectToFitSelected = ge.ChangeCoordinatesTo(ContentContainer, ge.Rect());
                        reachedFirstSelected = true;
                    }
                    else
                    {
                        rectToFitSelected = RectUtils.Encompass(rectToFitSelected,
                            ge.ChangeCoordinatesTo(ContentContainer, ge.Rect()));
                    }
                }
                else if (!reachedFirstSelected) // Don't bother if we already have at least one selected item
                {
                    if (!reachedFirstUnselected)
                    {
                        rectToFitUnselected = ge.ChangeCoordinatesTo(ContentContainer, ge.Rect());
                        reachedFirstUnselected = true;
                    }
                    else
                    {
                        rectToFitUnselected = RectUtils.Encompass(rectToFitUnselected,
                            ge.ChangeCoordinatesTo(ContentContainer, ge.Rect()));
                    }
                }
            }

            // Use selection only if possible, otherwise unselected. Failing both, use original content container rect
            Vector3 frameScaling;
            Vector3 frameTranslation;
            if (reachedFirstSelected)
            {
                CalculateFrameTransform(rectToFitSelected, layout, k_FrameBorder, out frameTranslation,
                    out frameScaling);
            }
            else if (reachedFirstUnselected)
            {
                CalculateFrameTransform(rectToFitUnselected, layout, k_FrameBorder, out frameTranslation,
                    out frameScaling);
            }
            else
            {
                // Note: rectToFitSelected will just be the container rect
                CalculateFrameTransform(rectToFitSelected, layout, k_FrameBorder, out frameTranslation,
                    out frameScaling);
            }

            // Update transform
            Matrix4x4.TRS(frameTranslation, Quaternion.identity, frameScaling);
            UpdateViewTransform(frameTranslation, frameScaling);
        }

        private float ZoomRequiredToFrameRect(Rect rectToFit, Rect clientRect, int border)
        {
            // bring slightly smaller screen rect into GUI space
            Rect screenRect = new()
            {
                xMin = border,
                xMax = clientRect.width - border,
                yMin = border,
                yMax = clientRect.height - border
            };
            Rect identity = GUIUtility.ScreenToGUIRect(screenRect);
            return Math.Min(identity.width / rectToFit.width, identity.height / rectToFit.height);
        }

        public void CalculateFrameTransform(Rect rectToFit, Rect clientRect, int border, out Vector3 frameTranslation,
            out Vector3 frameScaling)
        {
            // measure zoom level necessary to fit the canvas rect into the screen rect
            float zoomLevel = ZoomRequiredToFrameRect(rectToFit, clientRect, border);

            // clamp
            zoomLevel = Mathf.Clamp(zoomLevel, MinScale, MaxScale);

            Matrix4x4 transformMatrix =
                Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new(zoomLevel, zoomLevel, 1.0f));

            Vector2 edge = new(clientRect.width, clientRect.height);
            Vector2 origin = new(0, 0);

            Rect r = new()
            {
                min = origin,
                max = edge
            };

            Vector3 parentScale = new(transformMatrix.GetColumn(0).magnitude,
                transformMatrix.GetColumn(1).magnitude,
                transformMatrix.GetColumn(2).magnitude);
            Vector2 offset = r.center - rectToFit.center * parentScale.x;

            // Update output values before leaving
            frameTranslation = new(offset.x, offset.y, 0.0f);
            frameScaling = parentScale;
        }
        #endregion

        #region Command Handlers
        protected internal abstract void OnCopy();
        protected internal abstract void OnCut();
        protected internal abstract void OnPaste();
        protected internal abstract void OnDuplicate();
        protected internal abstract void OnDelete();
        protected internal abstract void OnUndo();
        protected internal abstract void OnRedo();
        protected internal abstract void OnEdgeCreate(BaseEdge edge);
        protected internal abstract void OnEdgeDelete(BaseEdge edge);
        protected internal abstract void OnNodeMoved(Node node);
        protected internal abstract void OnViewportChanged();
        #endregion

        #region Helper Classes
        public class Layer : VisualElement
        {
            public Layer() => pickingMode = PickingMode.Ignore;
        }

        private class ContentView : VisualElement
        {
            public override bool Overlaps(Rect r) => true;
        }
        #endregion
    }
}