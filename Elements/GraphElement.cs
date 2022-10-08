// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public abstract class GraphElement : VisualElement, ISelectable, IPositionable
    {
        private static readonly CustomStyleProperty<int> s_LayerProperty = new("--layer");
        private GraphView m_GraphView;

        private int m_Layer;
        private bool m_LayerIsInline;
        private bool m_Selected;

        protected GraphElement()
        {
            ClearClassList();
            AddToClassList("graph-element");
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);

            // Setup Manipulators
            this.AddManipulator(new SelectableManipulator());
        }

        public GraphView Graph
        {
            get => m_GraphView;
            internal set
            {
                if (m_GraphView == value) { return; }

                // We want m_GraphView there whenever these events are call so we can do setup/teardown
                if (value == null)
                {
                    OnRemovedFromGraphView();
                    m_GraphView = null;
                }
                else
                {
                    m_GraphView = value;
                    OnAddedToGraphView();
                }
            }
        }

        public virtual string Title
        {
            get => name;
            set => throw new NotImplementedException();
        }

        public Capabilities Capabilities { get; set; }

        #region Graph Events
        protected virtual void OnAddedToGraphView() { }
        protected virtual void OnRemovedFromGraphView() { }
        #endregion

        #region Selectable
        public ISelector Selector => Graph;

        public virtual bool Selected
        {
            get => m_Selected;
            set
            {
                if (m_Selected == value) { return; }
                if (value && Selector != null && IsSelectable())
                {
                    m_Selected = true;
                    if (IsAscendable() && resolvedStyle.position != Position.Relative) { BringToFront(); }
                }
                else { m_Selected = false; }
            }
        }
        #endregion

        #region Layer
        public int Layer
        {
            get => m_Layer;
            set
            {
                if (m_Layer == value) { return; }
                m_Layer = value;
                m_LayerIsInline = true;
                Graph?.ChangeLayer(this);
            }
        }

        public void ResetLayer()
        {
            m_LayerIsInline = false;
            customStyle.TryGetValue(s_LayerProperty, out int styleLayer);
            Layer = styleLayer;
        }
        #endregion

        #region Custom Style
        private void OnCustomStyleResolved(CustomStyleResolvedEvent e) { OnCustomStyleResolved(e.customStyle); }

        protected virtual void OnCustomStyleResolved(ICustomStyle styleOverride)
        {
            if (!m_LayerIsInline) { ResetLayer(); }
        }
        #endregion

        #region Position
        public virtual event Action<PositionData> OnPositionChange;
        public virtual Vector2 GetGlobalCenter() => Graph.ContentContainer.LocalToWorld(GetCenter());
        public virtual Vector2 GetCenter() => layout.center + (Vector2)transform.position;
        public virtual Vector2 GetPosition() => transform.position;

        public virtual void SetPosition(Vector2 newPosition)
        {
            transform.position = newPosition;
            OnPositionChange?.Invoke(new()
            {
                element = this,
                position = newPosition
            });
        }

        public virtual void ApplyDeltaToPosition(Vector2 delta)
        {
            SetPosition((Vector2)transform.position + delta);
        }
        #endregion

        #region Capabilities
        public virtual bool IsMovable() => (Capabilities & Capabilities.Movable) == Capabilities.Movable;
        public virtual bool IsDroppable() => (Capabilities & Capabilities.Droppable) == Capabilities.Droppable;
        public virtual bool IsAscendable() => (Capabilities & Capabilities.Ascendable) == Capabilities.Ascendable;
        public virtual bool IsRenamable() => (Capabilities & Capabilities.Renamable) == Capabilities.Renamable;
        public virtual bool IsCopiable() => (Capabilities & Capabilities.Copiable) == Capabilities.Copiable;
        public virtual bool IsSnappable() => (Capabilities & Capabilities.Snappable) == Capabilities.Snappable;
        public virtual bool IsResizable() => false;
        public virtual bool IsGroupable() => false;
        public virtual bool IsStackable() => false;

        public virtual bool IsSelectable() =>
            (Capabilities & Capabilities.Selectable) == Capabilities.Selectable && visible;
        #endregion
    }
}