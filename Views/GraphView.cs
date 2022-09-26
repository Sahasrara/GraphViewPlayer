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
    public struct GraphViewChange
    {
        // Operations Pending
        public List<GraphElement> elementsToRemove;
        public List<Edge> edgesToCreate;

        // Operations Completed
        public List<GraphElement> movedElements;
        public Vector2 moveDelta;
    }

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
// TODO
        public delegate GraphViewChange GraphViewChanged(GraphViewChange graphViewChange);
        public GraphViewChanged graphViewChanged { get; set; }

        private GraphViewChange m_GraphViewChange;
        private List<GraphElement> m_ElementsToRemove;

        public delegate void ViewTransformChanged(GraphView graphView);
        public ViewTransformChanged viewTransformChanged { get; set; }

        // BE AWARE: This is just a stopgap measure to get the minimap notified and should not be used outside of it.
        // This should also get ripped once the minimap is re-written.
        internal Action redrawn { get; set; }

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

            if (viewTransformChanged != null)
                viewTransformChanged(this);
        }

        bool m_FrameAnimate = false;

        public bool isReframable { get; set; }

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
            contentViewContainer= new ContentViewContainer
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
            graphElements = contentViewContainer.Query<GraphElement>().Where(e => !(e is Port)).Build();
            nodes = contentViewContainer.Query<Node>().Build();
            edges = this.Query<Layer>().Children<Edge>().Build();
            ports = contentViewContainer.Query().Children<Layer>().Descendents<Port>().Build();

            // Selection
            selection = new List<ISelectable>();

            // Layers
            m_ContainerLayers = new();

            // Elements to Remove
            m_ElementsToRemove = new();

            // Graph View Change
            m_GraphViewChange.elementsToRemove = m_ElementsToRemove;

            // Framing & Focus
            isReframable = true;
            focusable = true;

            // Event Handlers
            RegisterCallback<ValidateCommandEvent>(OnValidateCommand);
            RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);
            RegisterCallback<AttachToPanelEvent>(OnEnterPanel);
            RegisterCallback<DetachFromPanelEvent>(OnLeavePanel);
        }

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

        public UQueryState<GraphElement> graphElements { get; private set; }
        public UQueryState<Node> nodes { get; private set; }
        public UQueryState<Port> ports { get; private set; }
        public UQueryState<Edge> edges { get; private set; }

        private ContentZoomer m_Zoomer;

        public float minScale
        {
            get => m_Zoomer.minScale;
            set
            {
                m_Zoomer.minScale = value;
                ValidateTransform();
            }
        }

        public float maxScale
        {
            get => m_Zoomer.maxScale;
            set
            {
                m_Zoomer.maxScale = value;
                ValidateTransform();
            }
        }

        public float scaleStep
        {
            get => m_Zoomer.scaleStep;
            set
            {
                m_Zoomer.scaleStep = value;
                ValidateTransform();
            }
        }

        public float referenceScale
        {
            get => m_Zoomer.referenceScale;
            set
            {
                m_Zoomer.referenceScale = value;
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

            viewTransform.scale = transformScale;
        }

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

        void OnEnterPanel(AttachToPanelEvent e)
        {
            if (isReframable && panel != null)
                panel.visualTree.RegisterCallback<KeyDownEvent>(OnKeyDownShortcut);
        }

        void OnLeavePanel(DetachFromPanelEvent e)
        {
            if (isReframable)
                panel.visualTree.UnregisterCallback<KeyDownEvent>(OnKeyDownShortcut);
        }

        void OnKeyDownShortcut(KeyDownEvent evt)
        {
            if (!isReframable)
                return;

            if (panel.GetCapturingElement(PointerId.mousePointerId) != null)
                return;

            Debug.Log(evt.character);
            // switch (evt.character)
            // {
            //     case 'a':
            //         result = FrameAll();
            //         break;
            //
            //     case 'o':
            //         result = FrameOrigin();
            //         break;
            //
            //     case '[':
            //         result = FramePrev();
            //         break;
            //
            //     case ']':
            //         result = FrameNext();
            //         break;
            //     case ' ':
            //         result = OnInsertNodeKeyDown(evt);
            //         break;
            // }
        }

        internal void OnValidateCommand(ValidateCommandEvent evt)
        {
            if (panel.GetCapturingElement(PointerId.mousePointerId) != null)
                return;

            if ((evt.commandName == EventCommandNames.Copy && CanCopySelection())
                || (evt.commandName == EventCommandNames.Paste && CanPaste())
                || (evt.commandName == EventCommandNames.Duplicate && CanDuplicateSelection())
                || (evt.commandName == EventCommandNames.Cut && CanCutSelection())
                || ((evt.commandName == EventCommandNames.Delete || evt.commandName == EventCommandNames.SoftDelete) && CanDeleteSelection()))
            {
                evt.StopPropagation();
                if (evt.imguiEvent != null)
                {
                    evt.imguiEvent.Use();
                }
            }
            else if (evt.commandName == EventCommandNames.FrameSelected)
            {
                evt.StopPropagation();
                if (evt.imguiEvent != null)
                {
                    evt.imguiEvent.Use();
                }
            }
        }

        public enum AskUser
        {
            AskUser,
            DontAskUser
        }

        internal void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            if (panel.GetCapturingElement(PointerId.mousePointerId) != null)
                return;

            if (evt.commandName == EventCommandNames.Copy)
            {
                CopySelection();
                evt.StopPropagation();
            }
            else if (evt.commandName == EventCommandNames.Paste)
            {
                Paste();
                evt.StopPropagation();
            }
            else if (evt.commandName == EventCommandNames.Duplicate)
            {
                DuplicateSelection();
                evt.StopPropagation();
            }
            else if (evt.commandName == EventCommandNames.Cut)
            {
                CutSelection();
                evt.StopPropagation();
            }
            else if (evt.commandName == EventCommandNames.Delete)
            {
                DeleteSelection(AskUser.DontAskUser);
                evt.StopPropagation();
            }
            else if (evt.commandName == EventCommandNames.SoftDelete)
            {
                DeleteSelection(AskUser.AskUser);
                evt.StopPropagation();
            }
            else if (evt.commandName == EventCommandNames.FrameSelected)
            {
                // FrameSelection();
                Debug.Log("FRAME");
                evt.StopPropagation();
            }

            if (evt.isPropagationStopped && evt.imguiEvent != null)
            {
                Debug.Log("IMGUI EVT");
                evt.imguiEvent.Use();
            }
        }

        public static void CollectElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> collectedElementSet, Func<GraphElement, bool> conditionFunc)
        {
            foreach (var element in elements.Where(e => e != null && !collectedElementSet.Contains(e) && conditionFunc(e)))
            {
                var collectibleElement = element as ICollectibleElement;
                collectibleElement?.CollectElements(collectedElementSet, conditionFunc);
                collectedElementSet.Add(element);
            }
        }

        protected internal virtual void CollectCopyableGraphElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> elementsToCopySet)
        {
            CollectElements(elements, elementsToCopySet, e => e.IsCopiable());
        }

        protected abstract bool CanCopySelection();
        protected abstract void CopySelection();
        protected abstract bool CanCutSelection();
        protected abstract void CutSelection();
        protected abstract bool CanPaste();
        protected abstract void Paste();
        protected abstract bool CanDuplicateSelection();
        protected abstract void DuplicateSelection();
        protected abstract bool CanDeleteSelection();
        protected abstract void DeleteSelection(AskUser askUser);

        public virtual List<Port> GetCompatiblePorts(Port startPort)
        {
            return (from p in ports.ToList()
                    where p.direction != startPort.direction 
                          && p.node != startPort.node 
                          && !p.IsConnected(startPort)
                    select p).ToList();
        }

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

        private void CollectDeletableGraphElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> elementsToRemoveSet)
        {
            CollectElements(elements, elementsToRemoveSet, e => (e.capabilities & Capabilities.Deletable) == Capabilities.Deletable);
        }

        public virtual void DeleteSelection()
        {
            var elementsToRemoveSet = new HashSet<GraphElement>();

            CollectDeletableGraphElements(selection.OfType<GraphElement>(), elementsToRemoveSet);

            DeleteElements(elementsToRemoveSet);

            selection.Clear();
        }

        public void DeleteElements(IEnumerable<GraphElement> elementsToRemove)
        {
            m_ElementsToRemove.Clear();
            foreach (GraphElement element in elementsToRemove)
                m_ElementsToRemove.Add(element);

            List<GraphElement> elementsToRemoveList = m_ElementsToRemove;
            if (graphViewChanged != null)
            {
                elementsToRemoveList = graphViewChanged(m_GraphViewChange).elementsToRemove;
            }

            // Notify the ends of connections that the connection is going way.
            foreach (var connection in elementsToRemoveList.OfType<Edge>())
            {
                if (connection.output != null)
                    connection.output.Disconnect(connection);

                if (connection.input != null)
                    connection.input.Disconnect(connection);

                connection.output = null;
                connection.input = null;
            }

            foreach (GraphElement element in elementsToRemoveList)
            {
                RemoveElement(element);
            }
        }
#region Framing
/*
        public EventPropagation FrameAll()
        {
            return Frame(FrameType.All);
        }

        public EventPropagation FrameSelection()
        {
            return Frame(FrameType.Selection);
        }

        public EventPropagation FrameOrigin()
        {
            return Frame(FrameType.Origin);
        }

        public EventPropagation FramePrev()
        {
            if (contentViewContainer.childCount == 0)
                return EventPropagation.Continue;

            List<GraphElement> childrenList = graphElements.ToList().Where(e => e.IsSelectable() && !(e is Edge)).OrderByDescending(e => e.controlid).ToList();
            return FramePrevNext(childrenList);
        }

        public EventPropagation FrameNext()
        {
            if (contentViewContainer.childCount == 0)
                return EventPropagation.Continue;

            List<GraphElement> childrenList = graphElements.ToList().Where(e => e.IsSelectable() && !(e is Edge)).OrderBy(e => e.controlid).ToList();
            return FramePrevNext(childrenList);
        }

        public EventPropagation FramePrev(Func<GraphElement, bool> predicate)
        {
            if (this.contentViewContainer.childCount == 0)
                return EventPropagation.Continue;
            List<GraphElement> list = graphElements.ToList().Where(predicate).ToList();
            list.Reverse();
            return this.FramePrevNext(list);
        }

        public EventPropagation FrameNext(Func<GraphElement, bool> predicate)
        {
            if (this.contentViewContainer.childCount == 0)
                return EventPropagation.Continue;
            return this.FramePrevNext(graphElements.ToList().Where(predicate).ToList());
        }

        // TODO: Do we limit to GraphElements or can we tab through ISelectable's?
        EventPropagation FramePrevNext(List<GraphElement> childrenList)
        {
            GraphElement graphElement = null;

            // Start from current selection, if any
            if (selection.Count != 0)
                graphElement = selection[0] as GraphElement;

            int idx = childrenList.IndexOf(graphElement);

            if (idx >= 0 && idx < childrenList.Count - 1)
                graphElement = childrenList[idx + 1];
            else
                graphElement = childrenList[0];

            // New selection...
            ClearSelection();
            AddToSelection(graphElement);

            // ...and frame this new selection
            return Frame(FrameType.Selection);
        }

        EventPropagation Frame(FrameType frameType)
        {
            Rect rectToFit = contentViewContainer.layout;
            Vector3 frameTranslation = Vector3.zero;
            Vector3 frameScaling = Vector3.one;

            if (frameType == FrameType.Selection &&
                (selection.Count == 0 || !selection.Any(e => e.IsSelectable() && !(e is Edge))))
                frameType = FrameType.All;

            if (frameType == FrameType.Selection)
            {
                VisualElement graphElement = selection[0] as GraphElement;
                if (graphElement != null)
                {
                    // Edges don't have a size. Only their internal EdgeControl have a size.
                    if (graphElement is Edge)
                        graphElement = (graphElement as Edge).edgeControl;
                    rectToFit = graphElement.ChangeCoordinatesTo(contentViewContainer, graphElement.rect());
                }

                rectToFit = selection.Cast<GraphElement>()
                    .Aggregate(rectToFit, (current, currentGraphElement) =>
                    {
                        VisualElement currentElement = currentGraphElement;
                        if (currentGraphElement is Edge)
                            currentElement = (currentGraphElement as Edge).edgeControl;
                        return RectUtils.Encompass(current, currentElement.ChangeCoordinatesTo(contentViewContainer, currentElement.rect()));
                    });
                CalculateFrameTransform(rectToFit, layout, k_FrameBorder, out frameTranslation, out frameScaling);
            }
            else if (frameType == FrameType.All)
            {
                rectToFit = CalculateRectToFitAll(contentViewContainer);
                CalculateFrameTransform(rectToFit, layout, k_FrameBorder, out frameTranslation, out frameScaling);
            } // else keep going if (frameType == FrameType.Origin)

            if (m_FrameAnimate)
            {
                // TODO Animate framing
                // RMAnimation animation = new RMAnimation();
                // parent.Animate(parent)
                //       .Lerp(new string[] {"m_Scale", "m_Translation"},
                //             new object[] {parent.scale, parent.translation},
                //             new object[] {frameScaling, frameTranslation}, 0.08f);
            }
            else
            {
                Matrix4x4.TRS(frameTranslation, Quaternion.identity, frameScaling);

                UpdateViewTransform(frameTranslation, frameScaling);
            }

            contentViewContainer.MarkDirtyRepaint();

            UpdatePersistedViewTransform();

            return EventPropagation.Stop;
        }

        public virtual Rect CalculateRectToFitAll(VisualElement container)
        {
            Rect rectToFit = container.layout;
            bool reachedFirstChild = false;

            graphElements.ForEach(ge =>
            {
                if (ge is Edge || ge is Port)
                {
                    return;
                }

                if (!reachedFirstChild)
                {
                    rectToFit = ge.ChangeCoordinatesTo(contentViewContainer, ge.rect());
                    reachedFirstChild = true;
                }
                else
                {
                    rectToFit = RectUtils.Encompass(rectToFit, ge.ChangeCoordinatesTo(contentViewContainer, ge.rect()));
                }
            });

            return rectToFit;
        }

        public static void CalculateFrameTransform(Rect rectToFit, Rect clientRect, int border, out Vector3 frameTranslation, out Vector3 frameScaling)
        {
            // bring slightly smaller screen rect into GUI space
            var screenRect = new Rect
            {
                xMin = border,
                xMax = clientRect.width - border,
                yMin = border,
                yMax = clientRect.height - border
            };

            Matrix4x4 m = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
            Rect identity = GUIUtility.ScreenToGUIRect(screenRect);

            // measure zoom level necessary to fit the canvas rect into the screen rect
            float zoomLevel = Math.Min(identity.width / rectToFit.width, identity.height / rectToFit.height);

            // clamp
            zoomLevel = Mathf.Clamp(zoomLevel, ContentZoomer.DefaultMinScale, 1.0f);

            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(zoomLevel, zoomLevel, 1.0f));

            var edge = new Vector2(clientRect.width, clientRect.height);
            var origin = new Vector2(0, 0);

            var r = new Rect
            {
                min = origin,
                max = edge
            };

            var parentScale = new Vector3(transform.GetColumn(0).magnitude,
                transform.GetColumn(1).magnitude,
                transform.GetColumn(2).magnitude);
            Vector2 offset = r.center - (rectToFit.center * parentScale.x);

            // Update output values before leaving
            frameTranslation = new Vector3(offset.x, offset.y, 0.0f);
            frameScaling = parentScale;

            GUI.matrix = m;
        }
*/
#endregion
    }

    #region Helpers
    internal static class EventCommandNames
    {
        public const string Cut = "Cut";
        public const string Copy = "Copy";
        public const string Paste = "Paste";
        public const string SelectAll = "SelectAll";
        public const string DeselectAll = "DeselectAll";
        public const string InvertSelection = "InvertSelection";
        public const string Duplicate = "Duplicate";
        public const string Rename = "Rename";
        public const string Delete = "Delete";
        public const string SoftDelete = "SoftDelete";
        public const string Find = "Find";
        public const string SelectChildren = "SelectChildren";
        public const string SelectPrefabRoot = "SelectPrefabRoot";
        public const string UndoRedoPerformed = "UndoRedoPerformed";
        public const string OnLostFocus = "OnLostFocus";
        public const string NewKeyboardFocus = "NewKeyboardFocus";
        public const string ModifierKeysChanged = "ModifierKeysChanged";
        public const string EyeDropperUpdate = "EyeDropperUpdate";
        public const string EyeDropperClicked = "EyeDropperClicked";
        public const string EyeDropperCancelled = "EyeDropperCancelled";
        public const string ColorPickerChanged = "ColorPickerChanged";
        public const string FrameSelected = "FrameSelected";
        public const string FrameSelectedWithLock = "FrameSelectedWithLock";
    }

    internal static class VisualElementExtensions
    {
        internal static Rect rect(this VisualElement ve) => new(Vector2.zero, ve.layout.size);
    }
    #endregion
}
