// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Experimental.GraphView
{
    public abstract class GraphElement : VisualElement, ISelectable
    {
        public Color elementTypeColor { get; set; }

        int m_Layer;
        bool m_LayerIsInline;
        public int layer
        {
            get { return m_Layer; }
            set
            {
                m_LayerIsInline = true;
                if (m_Layer == value)
                    return;
                m_Layer = value;
            }
        }

        public virtual string title
        {
            get { return name; }
            set { throw new NotImplementedException(); }
        }

        public virtual bool showInMiniMap { get; set; } = true;

        public void ResetLayer()
        {
            int prevLayer = m_Layer;
            m_Layer = 0;
            m_LayerIsInline = false;
            customStyle.TryGetValue(s_LayerProperty, out m_Layer);
            UpdateLayer(prevLayer);
        }

        static CustomStyleProperty<int> s_LayerProperty = new CustomStyleProperty<int>("--layer");

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            OnCustomStyleResolved(e.customStyle);
        }

        protected virtual void OnCustomStyleResolved(ICustomStyle style)
        {
            int prevLayer = m_Layer;
            if (!m_LayerIsInline)
                style.TryGetValue(s_LayerProperty, out m_Layer);

            UpdateLayer(prevLayer);
        }

        private void UpdateLayer(int prevLayer)
        {
            if (prevLayer != m_Layer)
            {
                GraphView view = GetFirstAncestorOfType<GraphView>();
                if (view != null)
                {
                    view.ChangeLayer(this);
                }
            }
        }

        private Capabilities m_Capabilities;
        public Capabilities capabilities
        {
            get { return m_Capabilities; }
            set
            {
                if (m_Capabilities == value)
                    return;

                m_Capabilities = value;

                if (IsSelectable() && m_ClickSelector == null)
                {
                    m_ClickSelector = new ClickSelector();
                    this.AddManipulator(m_ClickSelector);
                }
                else if (!IsSelectable() && m_ClickSelector != null)
                {
                    this.RemoveManipulator(m_ClickSelector);
                    m_ClickSelector = null;
                }
            }
        }

        internal ResizeRestriction resizeRestriction { get; set; }

        private bool m_Selected;
        public bool selected
        {
            get { return m_Selected; }
            set
            {
                // Set new value (toggle old value)
                if ((capabilities & Capabilities.Selectable) != Capabilities.Selectable)
                    return;

                if (m_Selected == value)
                    return;

                m_Selected = value;


                if (m_Selected)
                {
                    pseudoStates |= PseudoStates.Checked;
                }
                else
                {
                    pseudoStates &= ~PseudoStates.Checked;
                }
            }
        }

        protected GraphElement()
        {
            ClearClassList();
            AddToClassList("graphElement");
            elementTypeColor = new Color(0.9f, 0.9f, 0.9f, 0.5f);

            viewDataKey = Guid.NewGuid().ToString();

            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        ClickSelector m_ClickSelector;

        public virtual bool IsSelectable()
        {
            return (capabilities & Capabilities.Selectable) == Capabilities.Selectable && visible && resolvedStyle.display != DisplayStyle.None;
        }

        public virtual bool IsMovable()
        {
            return (capabilities & Capabilities.Movable) == Capabilities.Movable;
        }

        public virtual bool IsResizable()
        {
            return (capabilities & Capabilities.Resizable) == Capabilities.Resizable;
        }

        public virtual bool IsDroppable()
        {
            return (capabilities & Capabilities.Droppable) == Capabilities.Droppable;
        }

        public virtual bool IsAscendable()
        {
            return (capabilities & Capabilities.Ascendable) == Capabilities.Ascendable;
        }

        public virtual bool IsRenamable()
        {
            return (capabilities & Capabilities.Renamable) == Capabilities.Renamable;
        }

        public virtual bool IsCopiable()
        {
            return (capabilities & Capabilities.Copiable) == Capabilities.Copiable;
        }

        public virtual bool IsSnappable()
        {
            // stack children snapping is not supported/implemented.
            return !IsStackable() && (capabilities & Capabilities.Snappable) == Capabilities.Snappable;
        }

        public virtual bool IsGroupable()
        {
            // stack children grouping is also not supported.
            return !IsStackable() && ((capabilities & Capabilities.Groupable) == Capabilities.Groupable);
        }

        public virtual bool IsStackable()
        {
            bool capable = (capabilities & Capabilities.Stackable) == Capabilities.Stackable;
            // Dependencies will need to define the cabilities of their stack children nodes,
            // we will make some safe assumptions until dependencies can be updated.
            bool situational = ClassListContains("stack-child-element") || parent is StackNode;
            return capable || situational;
        }

        public virtual Vector3 GetGlobalCenter()
        {
            var globalCenter = layout.center + parent.layout.position;
            return MultiplyMatrix44Point2(ref parent.worldTransformRef, globalCenter);
        }

        // TODO: Temporary transition function.
        public virtual void UpdatePresenterPosition()
        {
            // This can be overridden by derived class to get notified when a manipulator
            // has *finished* changing the layout (size or position) of this element.
        }

        public virtual Rect GetPosition()
        {
            return layout;
        }

        public virtual void SetPosition(Rect newPos)
        {
            layout = newPos;
        }

        public virtual void OnSelected()
        {
            if (IsAscendable() && resolvedStyle.position != Position.Relative)
                BringToFront();
        }

        public virtual void OnUnselected()
        {
        }

        public virtual bool HitTest(Vector2 localPoint)
        {
            return ContainsPoint(localPoint);
        }

        public virtual void Select(VisualElement selectionContainer, bool additive)
        {
            var selection = selectionContainer as ISelection;
            if (selection != null)
            {
                if (!selection.selection.Contains(this))
                {
                    if (!additive)
                    {
                        selection.ClearSelection();
                        selection.AddToSelection(this);
                    }
                    else // prevent heterogenous selections between stack child nodes and other nodes
                    {
                        var selected = selection.selection.Cast<GraphElement>();
                        bool selectionHasChildren = selected.Any(item => item.IsStackable());
                        bool selectionHasSiblings = selected.All(item => item.parent == parent);
                        bool targetIsChild = IsStackable();
                        bool isSelectionHomogenous = !targetIsChild && !selectionHasChildren || targetIsChild && selectionHasSiblings;

                        if (isSelectionHomogenous)
                        {
                            selection.AddToSelection(this);
                        }
                    }
                }
            }
        }

        public virtual void Unselect(VisualElement selectionContainer)
        {
            var selection = selectionContainer as ISelection;
            if (selection != null)
            {
                if (selection.selection.Contains(this))
                {
                    selection.RemoveFromSelection(this);
                }
            }
        }

        public virtual bool IsSelected(VisualElement selectionContainer)
        {
            var selection = selectionContainer as ISelection;
            if (selection != null)
            {
                if (selection.selection.Contains(this))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
