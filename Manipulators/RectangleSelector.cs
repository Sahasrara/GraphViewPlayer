// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class RectangleSelector : MouseManipulator
    {
        private readonly RectangleSelect m_Rectangle;
        private bool m_Active;
        private GraphView m_GraphView;
        private List<ISelectable> m_NewSelection;

        public RectangleSelector()
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Command });
            }
            else
            {
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Control });
            }
            m_Rectangle = new RectangleSelect();
            m_Rectangle.style.position = Position.Absolute;
            m_Rectangle.style.top = 0f;
            m_Rectangle.style.left = 0f;
            m_Rectangle.style.bottom = 0f;
            m_Rectangle.style.right = 0f;
            m_Active = false;
            m_NewSelection = new();
        }

        // get the axis aligned bound
        public Rect ComputeAxisAlignedBound(Rect position, Matrix4x4 transform)
        {
            Vector3 min = transform.MultiplyPoint3x4(position.min);
            Vector3 max = transform.MultiplyPoint3x4(position.max);
            return Rect.MinMaxRect(Math.Min(min.x, max.x), Math.Min(min.y, max.y), Math.Max(min.x, max.x), Math.Max(min.y, max.y));
        }

        protected override void RegisterCallbacksOnTarget()
        {
            var graphView = target as GraphView;
            if (graphView == null)
            {
                throw new InvalidOperationException("Manipulator can only be added to a GraphView");
            }

            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
        }

        void OnMouseCaptureOutEvent(MouseCaptureOutEvent e)
        {
            if (m_Active)
            {
                m_Rectangle.RemoveFromHierarchy();
                m_Active = false;
            }
        }

        private void OnMouseDown(MouseDownEvent e)
        {
            // Not Active
            if (m_Active)
            {
                e.StopImmediatePropagation();
                return;
            }

            // Not an event we care about
            if (!CanStartManipulation(e)) return;

            // Target is not a RUIGraphView
            m_GraphView = base.target as GraphView;
            if (m_GraphView == null) return;

            // Don't start marquee unless the click started on the GraphView
            if (e.target is not GraphView) return;

            // Clear selection unless this was an action key + click
            if (!e.actionKey) m_GraphView.ClearSelection();

            // Add marquee
            m_GraphView.Add(m_Rectangle);
            m_Rectangle.coordinates = new()
            {
                start = e.localMousePosition,
                end = e.localMousePosition,
            };
            m_Active = true;
            target.CaptureMouse();
            e.StopImmediatePropagation();
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            if (!m_Active)
                return;

            var graphView = target as GraphView;
            if (graphView == null)
                return;

            if (!CanStopManipulation(e))
                return;

            graphView.Remove(m_Rectangle);

            m_Rectangle.end = e.localMousePosition;

            var selectionRect = new Rect()
            {
                min = new Vector2(Math.Min(m_Rectangle.start.x, m_Rectangle.end.x), Math.Min(m_Rectangle.start.y, m_Rectangle.end.y)),
                max = new Vector2(Math.Max(m_Rectangle.start.x, m_Rectangle.end.x), Math.Max(m_Rectangle.start.y, m_Rectangle.end.y))
            };

            selectionRect = ComputeAxisAlignedBound(selectionRect, graphView.viewTransform.matrix.inverse);

            List<ISelectable> selection = graphView.selection;

            // a copy is necessary because AddToSelection might cause a SendElementToFront which will change the order.
            graphView.graphElements.ForEach(child =>
            {
                var localSelRect = graphView.contentViewContainer.ChangeCoordinatesTo(child, selectionRect);
                if (child.IsSelectable() && child.Overlaps(localSelRect))
                {
                    m_NewSelection.Add(child);
                }
            });

            foreach (var selectable in m_NewSelection)
            {
                if (selection.Contains(selectable))
                {
                    if (e.actionKey) // invert selection on shift only
                        graphView.RemoveFromSelection(selectable);
                }
                else
                    graphView.AddToSelection(selectable);
            }
            m_NewSelection.Clear();
            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            if (!m_Active)
                return;

            m_Rectangle.end = e.localMousePosition;
            e.StopPropagation();
        }

        private class RectangleSelect : VisualElement
        {
            private Vector2 m_Start;
            private Vector2 m_End;

           internal Vector2 start
            {
                get => m_Start;
                set
                {
                    m_Start = value;
                    MarkDirtyRepaint();
                }
            }

            internal Vector2 end
            {
                get => m_End;
                set
                {
                    m_End = value;
                    MarkDirtyRepaint();
                }
            }

            internal RectangleCoordinates coordinates
            {
                get => new() { start = m_Start, end = m_End };
                set
                {
                    m_Start = value.start;
                    m_End = value.end;
                    MarkDirtyRepaint();
                }
            }

            internal Rect SelectionRect
            {
                get => new()
                {
                    min = new(Math.Min(m_Start.x, m_End.x), Math.Min(m_Start.y, m_End.y)),
                    max = new(Math.Max(m_Start.x, m_End.x), Math.Max(m_Start.y, m_End.y)),
                };
            }

            internal RectangleSelect()
            {
                AddToClassList("marquee");
                pickingMode = PickingMode.Ignore;
                generateVisualContent = OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext ctx)
            {
                Rect selectionRect = SelectionRect;
                Painter2D painter = ctx.painter2D;
                painter.lineWidth = 1.0f;
                painter.strokeColor = Color.white;
                painter.fillColor = Color.gray;
                painter.BeginPath();
                painter.MoveTo(new(selectionRect.xMin, selectionRect.yMin));
                painter.LineTo(new(selectionRect.xMax, selectionRect.yMin));
                painter.LineTo(new(selectionRect.xMax, selectionRect.yMax));
                painter.LineTo(new(selectionRect.xMin, selectionRect.yMax));
                painter.LineTo(new(selectionRect.xMin, selectionRect.yMin));
                painter.Stroke();
                painter.Fill();
            }

            internal struct RectangleCoordinates
            {
                internal Vector2 start;
                internal Vector2 end;
            }
        }
    }
}
