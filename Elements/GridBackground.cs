// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class GridBackground : VisualElement
    {
        private static readonly CustomStyleProperty<float> s_SpacingProperty = new("--grid-spacing");
        private static readonly CustomStyleProperty<int> s_ThickLinesProperty = new("--grid-thick-lines");
        private static readonly CustomStyleProperty<Color> s_LineColorProperty = new("--grid-line-color");
        private static readonly CustomStyleProperty<Color> s_ThickLineColorProperty = new("--grid-thick-line-color");
        private static readonly CustomStyleProperty<Color> s_GridBackgroundColorProperty =
            new("--grid-background-color");

        private static readonly float s_DefaultSpacing = 50f;
        private static readonly int s_DefaultThickLines = 10;
        private static readonly Color s_DefaultLineColor = new(0f, 0f, 0f, 0.18f);
        private static readonly Color s_DefaultThickLineColor = new(0f, 0f, 0f, 0.38f);
        private static readonly Color s_DefaultGridBackgroundColor = new(0.17f, 0.17f, 0.17f, 1.0f);

        private VisualElement m_Container;
        private GraphView m_GraphView;
        private Color m_LineColor = s_DefaultLineColor;

        private float m_Spacing = s_DefaultSpacing;
        private Color m_ThickLineColor = s_DefaultThickLineColor;
        private int m_ThickLines = s_DefaultThickLines;

        public GridBackground()
        {
            AddToClassList("grid-background");
            pickingMode = PickingMode.Ignore;
            style.backgroundColor = s_DefaultGridBackgroundColor;
            this.StretchToParentSize();
            generateVisualContent = OnGenerateVisualContent;
            RegisterCallback<AttachToPanelEvent>(OnAttachEvent);
            RegisterCallback<DetachFromPanelEvent>(DetachFromPanelEvent);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private Vector3 Clip(Rect clipRect, Vector3 toClip)
        {
            if (toClip.x < clipRect.xMin) { toClip.x = clipRect.xMin; }
            if (toClip.x > clipRect.xMax) { toClip.x = clipRect.xMax; }
            if (toClip.y < clipRect.yMin) { toClip.y = clipRect.yMin; }
            if (toClip.y > clipRect.yMax) { toClip.y = clipRect.yMax; }
            return toClip;
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            ICustomStyle newStyle = e.customStyle;
            if (newStyle.TryGetValue(s_SpacingProperty, out float spacing)) { m_Spacing = spacing; }
            if (newStyle.TryGetValue(s_ThickLinesProperty, out int thickLine)) { m_ThickLines = thickLine; }
            if (newStyle.TryGetValue(s_ThickLineColorProperty, out Color thickLineColor))
            {
                m_ThickLineColor = thickLineColor;
            }
            if (newStyle.TryGetValue(s_LineColorProperty, out Color lineColor)) { m_LineColor = lineColor; }
            if (newStyle.TryGetValue(s_GridBackgroundColorProperty, out Color gridColor))
            {
                style.backgroundColor = gridColor;
            }
        }

        private void OnAttachEvent(AttachToPanelEvent evt)
        {
            // Parent must be GraphView
            VisualElement target = parent;
            m_GraphView = target as GraphView;
            if (m_GraphView == null)
            {
                throw new InvalidOperationException("GridBackground can only be added to a GraphView");
            }
            m_Container = m_GraphView.ContentContainer;

            // Listen for Zoom/Pan Changes
            m_GraphView.OnViewTransformChanged += RequestRepaint;
        }

        private void DetachFromPanelEvent(DetachFromPanelEvent evt)
        {
            // Stop Listening for Zoom/Pan Changes
            m_GraphView.OnViewTransformChanged += RequestRepaint;
        }

        private void RequestRepaint(GraphView graphView) => MarkDirtyRepaint();

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect clientRect = m_GraphView.layout;
            Painter2D painter = ctx.painter2D;

            // Since we're always stretch to parent size, we will use (0,0) as (x,y) coordinates
            clientRect.x = 0;
            clientRect.y = 0;

            Vector3 containerScale = new(m_Container.transform.matrix.GetColumn(0).magnitude,
                m_Container.transform.matrix.GetColumn(1).magnitude,
                m_Container.transform.matrix.GetColumn(2).magnitude);
            Vector4 containerTranslation = m_Container.transform.matrix.GetColumn(3);
            Rect containerPosition = m_Container.layout;
            float xSpacingThin = m_Spacing * containerScale.x;
            float xSpacingThick = xSpacingThin * m_ThickLines;
            float ySpacingThin = m_Spacing * containerScale.y;
            float ySpacingThick = ySpacingThin * m_ThickLines;

            // vertical lines
            Vector3 from = new(clientRect.x, clientRect.y, 0.0f);
            Vector3 to = new(clientRect.x, clientRect.height, 0.0f);

            Matrix4x4 tx = Matrix4x4.TRS(containerTranslation, Quaternion.identity, Vector3.one);

            from = tx.MultiplyPoint(from);
            to = tx.MultiplyPoint(to);

            from.x += containerPosition.x * containerScale.x;
            from.y += containerPosition.y * containerScale.y;
            to.x += containerPosition.x * containerScale.x;
            to.y += containerPosition.y * containerScale.y;

            float thickGridLineX = from.x;
            float thickGridLineY = from.y;

            // Update from/to to start at beginning of clientRect
            from.x = from.x % xSpacingThin - xSpacingThin;
            to.x = from.x;
            from.y = clientRect.y;
            to.y = clientRect.y + clientRect.height;
            while (from.x < clientRect.width)
            {
                from.x += xSpacingThin;
                to.x += xSpacingThin;

                painter.strokeColor = m_LineColor;
                painter.lineWidth = 1.0f;
                painter.BeginPath();
                painter.MoveTo(Clip(clientRect, from));
                painter.LineTo(Clip(clientRect, to));
                painter.Stroke();
            }

            float thickLineSpacing = m_Spacing * m_ThickLines;
            from.x = to.x = thickGridLineX % xSpacingThick - xSpacingThick;
            while (from.x < clientRect.width + thickLineSpacing)
            {
                painter.strokeColor = m_ThickLineColor;
                painter.lineWidth = 1.0f;
                painter.BeginPath();
                painter.MoveTo(Clip(clientRect, from));
                painter.LineTo(Clip(clientRect, to));
                painter.Stroke();

                from.x += xSpacingThick;
                to.x += xSpacingThick;
            }

            // horizontal lines
            from = new(clientRect.x, clientRect.y, 0.0f);
            to = new(clientRect.x + clientRect.width, clientRect.y, 0.0f);

            from.x += containerPosition.x * containerScale.x;
            from.y += containerPosition.y * containerScale.y;
            to.x += containerPosition.x * containerScale.x;
            to.y += containerPosition.y * containerScale.y;

            from = tx.MultiplyPoint(from);
            to = tx.MultiplyPoint(to);

            from.y = to.y = from.y % ySpacingThin - ySpacingThin;
            from.x = clientRect.x;
            to.x = clientRect.width;
            while (from.y < clientRect.height)
            {
                from.y += ySpacingThin;
                to.y += ySpacingThin;

                painter.strokeColor = m_LineColor;
                painter.lineWidth = 1.0f;
                painter.BeginPath();
                painter.MoveTo(Clip(clientRect, from));
                painter.LineTo(Clip(clientRect, to));
                painter.Stroke();
            }

            thickLineSpacing = m_Spacing * m_ThickLines;
            from.y = to.y = thickGridLineY % ySpacingThick - ySpacingThick;
            while (from.y < clientRect.height + thickLineSpacing)
            {
                painter.strokeColor = m_ThickLineColor;
                painter.lineWidth = 1.0f;
                painter.BeginPath();
                painter.MoveTo(Clip(clientRect, from));
                painter.LineTo(Clip(clientRect, to));
                painter.Stroke();

                from.y += ySpacingThick;
                to.y += ySpacingThick;
            }
        }
    }
}