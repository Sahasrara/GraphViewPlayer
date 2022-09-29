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
    public abstract class GraphView : VisualElement, ISelection
    {
        private static StyleSheet s_DefaultStyle;
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

        // Layer class. Used for queries below.
        public class Layer : VisualElement {}

        // Delegates and Callbacks
        internal delegate void ViewTransformChanged(GraphView graphView);
        internal ViewTransformChanged viewTransformChanged { get; set; }

        class ContentViewContainer : VisualElement
        {
            public override bool Overlaps(Rect r)
            {
                return true;
            }
        }

        VisualElement graphViewContainer { get; }
        public VisualElement contentViewContainer { get; private set; }

        public ITransform viewTransform
        {
            get { return contentViewContainer.transform; }
        }

        public void UpdateViewTransform(Vector3 newPosition)
            => UpdateViewTransform(newPosition, viewTransform.scale);
        public void UpdateViewTransform(Vector3 newPosition, Vector3 newScale)
        {
            float validateFloat = newPosition.x + newPosition.y + newPosition.z + newScale.x + newScale.y + newScale.z;
            if (float.IsInfinity(validateFloat) || float.IsNaN(validateFloat))
                return;

            contentViewContainer.transform.position = newPosition;
            contentViewContainer.transform.scale = newScale;

            viewTransformChanged?.Invoke(this);
            OnViewportChanged();
        }

        public enum FrameType
        {
            All = 0,
            Selection = 1,
            Origin = 2
        }

        readonly int k_FrameBorder = 30;

        private readonly Dictionary<int, Layer> m_ContainerLayers;

        public override VisualElement contentContainer // Contains full content, potentially partially visible
        {
            get { return graphViewContainer; }
        }

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
            m_Zoomer = new ContentZoomer();
            this.AddManipulator(m_Zoomer);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new SelectionDragger());

            //
            // Grid Background - Level 1
            //
            hierarchy.Insert(0, new GridBackground());

            //
            // Graph View Container - Level 1
            //
            // Root Container
            graphViewContainer = new() { pickingMode = PickingMode.Ignore };
            graphViewContainer.AddToClassList("graph-view-container");
            hierarchy.Add(graphViewContainer);

            //
            // Content Container - Level 3
            //
            // Content Container
            contentViewContainer = new ContentViewContainer
            {
                pickingMode = PickingMode.Ignore,
                usageHints = UsageHints.GroupTransform
            };
            contentViewContainer.AddToClassList("content-view-container");
            graphViewContainer.Add(contentViewContainer);

            //
            // Other Initialization
            //
            // Cached Queries
            graphElements = contentViewContainer.Query<GraphElement>().Build();
            nodes = contentViewContainer.Query<Node>().Build();
            edges = this.Query<Layer>().Children<Edge>().Build();
            ports = contentViewContainer.Query().Children<Layer>().Descendents<Port>().Build();

            // Selection
            selection = new List<ISelectable>();

            // Layers
            m_ContainerLayers = new();

            // Focus
            focusable = true;
        }
        #endregion

        #region Layers
        void AddLayer(Layer layer, int index)
        {
            m_ContainerLayers.Add(index, layer);

            int indexOfLayer = m_ContainerLayers.OrderBy(t => t.Key).Select(t => t.Value).ToList().IndexOf(layer);

            contentViewContainer.Insert(indexOfLayer, layer);
        }

        public void AddLayer(int index)
        {
            Layer newLayer = new Layer { name = $"Layer {index}", pickingMode = PickingMode.Ignore };

            m_ContainerLayers.Add(index, newLayer);

            int indexOfLayer = m_ContainerLayers.OrderBy(t => t.Key).Select(t => t.Value).ToList().IndexOf(newLayer);

            contentViewContainer.Insert(indexOfLayer, newLayer);
        }

        VisualElement GetLayer(int index)
        {
            return m_ContainerLayers[index];
        }

        internal void ChangeLayer(GraphElement element)
        {
            if (!m_ContainerLayers.ContainsKey(element.layer))
                AddLayer(element.layer);

            bool selected = element.selected;
            if (selected)
                element.UnregisterCallback<DetachFromPanelEvent>(OnSelectedElementDetachedFromPanel);

            GetLayer(element.layer).Add(element);

            if (selected)
                element.RegisterCallback<DetachFromPanelEvent>(OnSelectedElementDetachedFromPanel);
        }
        #endregion

        public UQueryState<GraphElement> graphElements { get; private set; }
        public UQueryState<Node> nodes { get; private set; }
        public UQueryState<Port> ports { get; private set; }
        public UQueryState<Edge> edges { get; private set; }

        #region Zoom
        private ContentZoomer m_Zoomer;

        public float minScale
        {
            get => m_Zoomer.minScale;
            set
            {
                m_Zoomer.minScale = Math.Min(value, ContentZoomer.DefaultMinScale);
                ValidateTransform();
            }
        }

        public float maxScale
        {
            get => m_Zoomer.maxScale;
            set
            {
                m_Zoomer.maxScale = Math.Max(value, ContentZoomer.DefaultMaxScale);
                ValidateTransform();
            }
        }

        public float scaleStep
        {
            get => m_Zoomer.scaleStep;
            set
            {
                m_Zoomer.scaleStep = Math.Min(value, (maxScale - minScale) / 2);
                ValidateTransform();
            }
        }

        public float referenceScale
        {
            get => m_Zoomer.referenceScale;
            set
            {
                m_Zoomer.referenceScale = Math.Clamp(value, minScale, maxScale);
                ValidateTransform();
            }
        }

        public float scale
        {
            get { return viewTransform.scale.x; }
        }

        protected void ValidateTransform()
        {
            if (contentViewContainer == null)
                return;
            Vector3 transformScale = viewTransform.scale;

            transformScale.x = Mathf.Clamp(transformScale.x, minScale, maxScale);
            transformScale.y = Mathf.Clamp(transformScale.y, minScale, maxScale);

            UpdateViewTransform(viewTransform.position, transformScale);
        }
        #endregion

        #region Selection
        // ISelection implementation
        public List<ISelectable> selection { get; protected set; }

        // functions to ISelection extensions
        public virtual void AddToSelection(ISelectable selectable)
        {
            var graphElement = selectable as GraphElement;
            if (graphElement == null)
                return;

            if (selection.Contains(selectable))
                return;

            AddToSelectionNoUndoRecord(graphElement);
        }

        private void AddToSelectionNoUndoRecord(GraphElement graphElement)
        {
            graphElement.selected = true;
            selection.Add(graphElement);
            graphElement.OnSelected();

            // To ensure that the selected GraphElement gets unselected if it is removed from the GraphView.
            graphElement.RegisterCallback<DetachFromPanelEvent>(OnSelectedElementDetachedFromPanel);

            graphElement.MarkDirtyRepaint();
        }

        private void RemoveFromSelectionNoUndoRecord(ISelectable selectable)
        {
            var graphElement = selectable as GraphElement;
            if (graphElement == null)
                return;
            graphElement.selected = false;

            selection.Remove(selectable);
            graphElement.OnUnselected();
            graphElement.UnregisterCallback<DetachFromPanelEvent>(OnSelectedElementDetachedFromPanel);
            graphElement.MarkDirtyRepaint();
        }

        public virtual void RemoveFromSelection(ISelectable selectable)
        {
            var graphElement = selectable as GraphElement;
            if (graphElement == null)
                return;

            if (!selection.Contains(selectable))
                return;

            RemoveFromSelectionNoUndoRecord(selectable);
        }

        private bool ClearSelectionNoUndoRecord()
        {
            foreach (var graphElement in selection.OfType<GraphElement>())
            {
                graphElement.selected = false;

                graphElement.OnUnselected();
                graphElement.UnregisterCallback<DetachFromPanelEvent>(OnSelectedElementDetachedFromPanel);
                graphElement.MarkDirtyRepaint();
            }

            bool selectionWasNotEmpty = selection.Any();
            selection.Clear();

            return selectionWasNotEmpty;
        }

        public virtual void ClearSelection()
        {
            ClearSelectionNoUndoRecord();
        }

        private void OnSelectedElementDetachedFromPanel(DetachFromPanelEvent evt)
        {
            RemoveFromSelectionNoUndoRecord(evt.target as ISelectable);
        }
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
            if (baseEvent is not KeyDownEvent evt) return;
            if (panel.GetCapturingElement(PointerId.mousePointerId) != null)  return;

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
                        Frame(FrameType.Selection);
                        evt.StopPropagation();
                    }
                    break;
            }
        }
        #endregion

        // public static void CollectElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> collectedElementSet, Func<GraphElement, bool> conditionFunc)
        // {
        //     foreach (var element in elements.Where(e => e != null && !collectedElementSet.Contains(e) && conditionFunc(e)))
        //     {
        //         var collectibleElement = element as ICollectibleElement;
        //         collectibleElement?.CollectElements(collectedElementSet, conditionFunc);
        //         collectedElementSet.Add(element);
        //     }
        // }

        // protected internal virtual void CollectCopyableGraphElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> elementsToCopySet)
        // {
        //     CollectElements(elements, elementsToCopySet, e => e.IsCopiable());
        // }

        public virtual void GetCompatiblePorts(ICollection<Port> toPopulate, Port startPort)
        {
            foreach (Port endPort in ports)
            {
                if (startPort.direction != endPort.direction // Input to output only 
                    && startPort.node != endPort.node // Can't connect to self 
                    && endPort.CanConnectToMore() // Has capacity 
                    && !startPort.IsConnectedTo(endPort) // Not already connected
                )
                {
                    toPopulate.Add(endPort);
                }
            }
        }

        #region Add / Remove Elements from Heirarchy
        public void AddElement(GraphElement graphElement)
        {
            int newLayer = graphElement.layer;
            if (!m_ContainerLayers.ContainsKey(newLayer))
            {
                AddLayer(newLayer);
            }
            GetLayer(newLayer).Add(graphElement);
        }

        public void RemoveElement(GraphElement graphElement)
        {
            graphElement.RemoveFromHierarchy();
        }

        // private void CollectDeletableGraphElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> elementsToRemoveSet)
        // {
        //     CollectElements(elements, elementsToRemoveSet, e => (e.capabilities & Capabilities.Deletable) == Capabilities.Deletable);
        // }

        // public virtual void DeleteSelection()
        // {
        //     var elementsToRemoveSet = new HashSet<GraphElement>();
        //
        //     CollectDeletableGraphElements(selection.OfType<GraphElement>(), elementsToRemoveSet);
        //
        //     DeleteElements(elementsToRemoveSet);
        //
        //     selection.Clear();
        // }

        public void ConnectPorts(Port input, Port output)
        {
            AddElement(input.ConnectTo(output));
        }
        #endregion

        #region Framing
        protected void Frame(FrameType frameType)
        {
            Rect rectToFit = contentViewContainer.layout;
            Vector3 frameTranslation = Vector3.zero;
            Vector3 frameScaling = Vector3.one;

            if (frameType == FrameType.Selection &&
                (selection.Count == 0 || !selection.Any(e => e.IsSelectable() && !(e is Edge))))
                frameType = FrameType.All;

            if (frameType == FrameType.Selection)
            {
                rectToFit = CalculateRectToFitSelection(contentViewContainer);
            }
            else if (frameType == FrameType.All)
            {
                rectToFit = CalculateRectToFitAll(contentViewContainer);
            }

            CalculateFrameTransform(rectToFit, layout, k_FrameBorder, out frameTranslation, out frameScaling);
            Matrix4x4.TRS(frameTranslation, Quaternion.identity, frameScaling);
            UpdateViewTransform(frameTranslation, frameScaling);
            contentViewContainer.MarkDirtyRepaint();
        }

        public virtual Rect CalculateRectToFitSelection(VisualElement container)
        {
            Rect rectToFit = container.layout;
            VisualElement graphElement = selection[0] as GraphElement;
            if (graphElement != null)
            {
                // Edges don't have a size. Only their internal EdgeControl have a size.
                if (graphElement is Edge)
                    graphElement = (graphElement as Edge).edgeControl;
                rectToFit = graphElement.ChangeCoordinatesTo(contentViewContainer, graphElement.Rect());
            }

            rectToFit = selection.Cast<GraphElement>()
                .Aggregate(rectToFit, (current, currentGraphElement) =>
                {
                    VisualElement currentElement = currentGraphElement;
                    if (currentGraphElement is Edge)
                        currentElement = (currentGraphElement as Edge).edgeControl;
                    return RectUtils.Encompass(current, currentElement.ChangeCoordinatesTo(contentViewContainer, currentElement.Rect()));
                });

            return rectToFit;
        }

        public virtual Rect CalculateRectToFitAll(VisualElement container)
        {
            Rect rectToFit = container.layout;
            bool reachedFirstChild = false;

            graphElements.ForEach(ge =>
            {
                if (ge is Edge)
                {
                    return;
                }

                if (!reachedFirstChild)
                {
                    rectToFit = ge.ChangeCoordinatesTo(contentViewContainer, ge.Rect());
                    reachedFirstChild = true;
                }
                else
                {
                    rectToFit = RectUtils.Encompass(rectToFit, ge.ChangeCoordinatesTo(contentViewContainer, ge.Rect()));
                }
            });

            return rectToFit;
        }

        private float ZoomRequiredToFrameRect(Rect rectToFit, Rect clientRect, int border)
        {
            // bring slightly smaller screen rect into GUI space
            Rect screenRect = new Rect
            {
                xMin = border,
                xMax = clientRect.width - border,
                yMin = border,
                yMax = clientRect.height - border
            };
            Rect identity = GUIUtility.ScreenToGUIRect(screenRect);
            return Math.Min(identity.width / rectToFit.width, identity.height / rectToFit.height);
        }

        public void CalculateFrameTransform(Rect rectToFit, Rect clientRect, int border, out Vector3 frameTranslation, out Vector3 frameScaling)
        {
            Matrix4x4 m = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            // measure zoom level necessary to fit the canvas rect into the screen rect
            float zoomLevel = ZoomRequiredToFrameRect(rectToFit, clientRect, border);

            // clamp
            zoomLevel = Mathf.Clamp(zoomLevel, minScale, maxScale);

            var transformMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(zoomLevel, zoomLevel, 1.0f));

            var edge = new Vector2(clientRect.width, clientRect.height);
            var origin = new Vector2(0, 0);

            var r = new Rect
            {
                min = origin,
                max = edge
            };

            var parentScale = new Vector3(transformMatrix.GetColumn(0).magnitude,
                transformMatrix.GetColumn(1).magnitude,
                transformMatrix.GetColumn(2).magnitude);
            Vector2 offset = r.center - (rectToFit.center * parentScale.x);

            // Update output values before leaving
            frameTranslation = new Vector3(offset.x, offset.y, 0.0f);
            frameScaling = parentScale;

            GUI.matrix = m;
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
        protected internal abstract void OnEdgeCreate(Edge edge);
        protected internal abstract void OnEdgeDelete(Edge edge);
        protected internal abstract void OnElementMoved(GraphElement element);
        protected internal abstract void OnViewportChanged();
        #endregion
    }

    #region Helpers
    internal static class VisualElementExtensions
    {
        internal static Rect Rect(this VisualElement ve) => new(Vector2.zero, ve.layout.size);
    }
    #endregion
}
