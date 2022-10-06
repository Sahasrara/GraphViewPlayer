// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class RectangleSelector : MouseManipulator
    {
        private readonly RectangleSelect m_Rectangle;
        private bool m_Active;
        private GraphView m_GraphView;

        public RectangleSelector()
        {
            activators.Add(new() { button = MouseButton.LeftMouse });
            activators.Add(new() { button = MouseButton.LeftMouse, modifiers = EventModifiers.Shift });
            activators.Add(new()
            {
                button = MouseButton.LeftMouse,
                modifiers = PlatformUtils.IsMac ? EventModifiers.Command : EventModifiers.Control
            });
            m_Rectangle = new()
            {
                style =
                {
                    position = Position.Absolute,
                    top = 0f,
                    left = 0f,
                    bottom = 0f,
                    right = 0f
                }
            };
            m_Active = false;
        }

        // get the axis aligned bound
        public Rect ComputeAxisAlignedBound(Rect position, Matrix4x4 transform)
        {
            Vector3 min = transform.MultiplyPoint3x4(position.min);
            Vector3 max = transform.MultiplyPoint3x4(position.max);
            return Rect.MinMaxRect(Math.Min(min.x, max.x), Math.Min(min.y, max.y), Math.Max(min.x, max.x),
                Math.Max(min.y, max.y));
        }

        protected override void RegisterCallbacksOnTarget()
        {
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

        private void OnMouseCaptureOutEvent(MouseCaptureOutEvent e)
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
            if (!CanStartManipulation(e)) { return; }

            // Target is not a RUIGraphView
            m_GraphView = target.GetFirstAncestorOfType<GraphView>();
            if (m_GraphView == null) { return; }

            // Clear selection if this is an exclusive select 
            bool additive = e.shiftKey;
            bool subtractive = e.actionKey;
            bool exclusive = !(additive ^ subtractive);
            if (exclusive) { m_GraphView.ClearSelection(); }

            // Add marquee
            m_GraphView.Add(m_Rectangle);
            m_Rectangle.Coordinates = new()
            {
                start = e.localMousePosition,
                end = e.localMousePosition
            };
            m_Active = true;
            target.CaptureMouse();
            e.StopImmediatePropagation();
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            if (!m_Active) { return; }

            if (!CanStopManipulation(e)) { return; }

            m_GraphView.Remove(m_Rectangle);

            m_Rectangle.End = e.localMousePosition;

            // Ignore if the rectangle is infinitely small
            Rect selectionRect = m_Rectangle.SelectionRect;
            if (selectionRect.width != 0 || selectionRect.height != 0)
            {
                selectionRect = ComputeAxisAlignedBound(selectionRect, m_GraphView.viewTransform.matrix.inverse);

                bool additive = e.shiftKey;
                bool subtractive = e.actionKey;
                bool exclusive = !(additive ^ subtractive);
                foreach (GraphElement element in m_GraphView.ElementsAll)
                {
                    Rect localSelRect = m_GraphView.contentViewContainer.ChangeCoordinatesTo(element, selectionRect);
                    if (element.Overlaps(localSelRect))
                    {
                        Debug.Log($"Seleted {exclusive || additive}");
                        element.Selected = exclusive || additive;
                    }
                    else if (exclusive)
                    {
                        Debug.Log("Seleted false");
                        element.Selected = false;
                    }
                }
            }

            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            if (!m_Active) { return; }

            m_Rectangle.End = e.localMousePosition;
            e.StopPropagation();
        }

        private class RectangleSelect : VisualElement
        {
            private Vector2 m_End;
            private Vector2 m_Start;

            internal RectangleSelect()
            {
                AddToClassList("marquee");
                pickingMode = PickingMode.Ignore;
                generateVisualContent = OnGenerateVisualContent;
            }

            internal Vector2 Start
            {
                get => m_Start;
                set
                {
                    m_Start = value;
                    MarkDirtyRepaint();
                }
            }

            internal Vector2 End
            {
                get => m_End;
                set
                {
                    m_End = value;
                    MarkDirtyRepaint();
                }
            }

            internal RectangleCoordinates Coordinates
            {
                get => new() { start = m_Start, end = m_End };
                set
                {
                    m_Start = value.start;
                    m_End = value.end;
                    MarkDirtyRepaint();
                }
            }

            internal Rect SelectionRect =>
                new()
                {
                    min = new(Math.Min(m_Start.x, m_End.x), Math.Min(m_Start.y, m_End.y)),
                    max = new(Math.Max(m_Start.x, m_End.x), Math.Max(m_Start.y, m_End.y))
                };

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