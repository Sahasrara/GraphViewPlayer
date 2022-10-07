// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class Edge : BaseEdge
    {
        public const float k_MinEdgeWidth = 1.75f;
        private const float k_EndPointRadius = 4.0f;
        private const float k_InterceptWidth = 6.0f;
        private const float k_EdgeLengthFromPort = 12.0f;
        private const float k_EdgeTurnDiameter = 16.0f;
        private const float k_EdgeSweepResampleRatio = 4.0f;
        private const int k_EdgeStraightLineSegmentDivisor = 5;
        private const int k_DefaultEdgeWidth = 2;
        private const int k_DefaultEdgeWidthSelected = 2;

        private static readonly CustomStyleProperty<int> s_EdgeWidthProperty = new("--edge-width");
        private static readonly CustomStyleProperty<int> s_EdgeWidthSelectedProperty = new("--edge-width-selected");
        private static readonly CustomStyleProperty<Color> s_EdgeColorSelectedProperty = new("--edge-color-selected");
        private static readonly CustomStyleProperty<Color> s_EdgeColorGhostProperty = new("--edge-color-ghost");
        private static readonly CustomStyleProperty<Color> s_EdgeColorProperty = new("--edge-color");
        private static readonly Color s_DefaultSelectedColor = new(240 / 255f, 240 / 255f, 240 / 255f);
        private static readonly Color s_DefaultColor = new(146 / 255f, 146 / 255f, 146 / 255f);
        private static readonly Color s_DefaultGhostColor = new(85 / 255f, 85 / 255f, 85 / 255f);

        private static readonly Gradient s_Gradient = new();
        private static readonly Stack<VisualElement> s_CapPool = new();
        private readonly List<Vector2> m_LastLocalControlPoints = new();

        // The points that will be rendered. Expressed in coordinates local to the element.
        private readonly List<Vector2> m_RenderPoints = new();
        private float m_CapRadius = 5;
        private bool m_ControlPointsDirty = true;

        private int m_EdgeWidth = 2;
        private VisualElement m_FromCap;
        private Color m_FromCapColor;
        private Color m_InputColor = Color.grey;
        private Orientation m_InputOrientation;
        private Color m_OutputColor = Color.grey;
        private Orientation m_OutputOrientation;
        private bool m_RenderPointsDirty = true;
        private VisualElement m_ToCap;
        private Color m_ToCapColor;

        public Edge()
        {
            ClearClassList();
            AddToClassList("edge");
            m_FromCap = null;
            m_ToCap = null;
            CapRadius = k_EndPointRadius;
            InterceptWidth = k_InterceptWidth;
            generateVisualContent = OnGenerateVisualContent;
        }

        public int EdgeWidthUnselected { get; } = k_DefaultEdgeWidth;
        public int EdgeWidthSelected { get; private set; } = k_DefaultEdgeWidthSelected;
        public Color ColorSelected { get; private set; } = s_DefaultSelectedColor;
        public Color ColorUnselected { get; private set; } = s_DefaultColor;
        public Color ColorGhost { get; private set; } = s_DefaultGhostColor;

        public float InterceptWidth { get; set; } = 5;
        public Vector2[] ControlPoints { get; private set; }

        public override bool Selected
        {
            get => base.Selected;
            set
            {
                if (base.Selected == value) { return; }
                base.Selected = value;
                if (value)
                {
                    if (IsGhostEdge) { Debug.Log("Selected Ghost Edge: this should never be"); }
                    else
                    {
                        InputColor = ColorSelected;
                        OutputColor = ColorSelected;
                        EdgeWidth = EdgeWidthSelected;
                    }
                }
                else
                {
                    if (IsGhostEdge)
                    {
                        InputColor = ColorGhost;
                        OutputColor = ColorGhost;
                    }
                    else
                    {
                        InputColor = ColorUnselected;
                        OutputColor = ColorUnselected;
                    }
                    EdgeWidth = EdgeWidthUnselected;
                }
            }
        }

        public Color InputColor
        {
            get => m_InputColor;
            set
            {
                if (m_InputColor != value)
                {
                    m_InputColor = value;
                    MarkDirtyRepaint();
                }
            }
        }

        public Color OutputColor
        {
            get => m_OutputColor;
            set
            {
                if (m_OutputColor != value)
                {
                    m_OutputColor = value;
                    MarkDirtyRepaint();
                }
            }
        }

        public Color FromCapColor
        {
            get => m_FromCapColor;
            set
            {
                if (m_FromCapColor == value) { return; }
                m_FromCapColor = value;

                if (m_FromCap != null) { m_FromCap.style.backgroundColor = m_FromCapColor; }
                MarkDirtyRepaint();
            }
        }

        public Color ToCapColor
        {
            get => m_ToCapColor;
            set
            {
                if (m_ToCapColor == value) { return; }
                m_ToCapColor = value;

                if (m_ToCap != null) { m_ToCap.style.backgroundColor = m_ToCapColor; }
                MarkDirtyRepaint();
            }
        }

        public float CapRadius
        {
            get => m_CapRadius;
            set
            {
                if (Mathf.Approximately(m_CapRadius, value)) { return; }
                m_CapRadius = value;
                MarkDirtyRepaint();
            }
        }

        public int EdgeWidth
        {
            get => m_EdgeWidth;
            set
            {
                if (m_EdgeWidth == value) { return; }
                m_EdgeWidth = value;
                UpdateLayout(); // The layout depends on the edges width
            }
        }

        public bool DrawFromCap
        {
            get => m_FromCap != null;
            set
            {
                if (!value)
                {
                    if (m_FromCap != null)
                    {
                        m_FromCap.RemoveFromHierarchy();
                        RecycleCap(m_FromCap);
                        m_FromCap = null;
                    }
                }
                else
                {
                    if (m_FromCap == null)
                    {
                        m_FromCap = GetCap();
                        m_FromCap.style.backgroundColor = m_FromCapColor;
                        Add(m_FromCap);
                    }
                }
            }
        }

        public bool DrawToCap
        {
            get => m_ToCap != null;
            set
            {
                if (!value)
                {
                    if (m_ToCap != null)
                    {
                        m_ToCap.RemoveFromHierarchy();
                        RecycleCap(m_ToCap);
                        m_ToCap = null;
                    }
                }
                else
                {
                    if (m_ToCap == null)
                    {
                        m_ToCap = GetCap();
                        m_ToCap.style.backgroundColor = m_ToCapColor;
                        Add(m_ToCap);
                    }
                }
            }
        }

        private static bool Approximately(Vector2 v1, Vector2 v2) =>
            Mathf.Approximately(v1.x, v2.x) && Mathf.Approximately(v1.y, v2.y);

        private static void RecycleCap(VisualElement cap) { s_CapPool.Push(cap); }

        private static VisualElement GetCap()
        {
            VisualElement result;
            if (s_CapPool.Count > 0) { result = s_CapPool.Pop(); }
            else
            {
                result = new();
                result.AddToClassList("edge-cap");
            }

            return result;
        }

        private void UpdateEdgeCaps()
        {
            if (m_FromCap != null)
            {
                Vector2 size = m_FromCap.layout.size;
                if (size.x > 0 && size.y > 0)
                {
                    Rect rect = new(From - size / 2f, size);
                    m_FromCap.style.left = rect.x;
                    m_FromCap.style.top = rect.y;
                    m_FromCap.style.width = rect.width;
                    m_FromCap.style.height = rect.height;
                }
            }
            if (m_ToCap != null)
            {
                Vector2 size = m_ToCap.layout.size;
                if (size.x > 0 && size.y > 0)
                {
                    Rect rect = new(To - size / 2f, size);
                    m_ToCap.style.left = rect.x;
                    m_ToCap.style.top = rect.y;
                    m_ToCap.style.width = rect.width;
                    m_ToCap.style.height = rect.height;
                }
            }
        }

        public virtual void UpdateLayout()
        {
            if (Graph == null) { return; }
            if (m_ControlPointsDirty)
            {
                ComputeControlPoints(); // Computes the control points in parent ( graph ) coordinates
                ComputeLayout(); // Update the element layout based on the control points.
                m_ControlPointsDirty = false;
            }
            UpdateEdgeCaps();
            MarkDirtyRepaint();
        }

        private void RenderStraightLines(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float safeSpan = OutputOrientation == Orientation.Horizontal
                ? Mathf.Abs(p1.x + k_EdgeLengthFromPort - (p4.x - k_EdgeLengthFromPort))
                : Mathf.Abs(p1.y + k_EdgeLengthFromPort - (p4.y - k_EdgeLengthFromPort));

            float safeSpan3 = safeSpan / k_EdgeStraightLineSegmentDivisor;
            float nodeToP2Dist = Mathf.Min(safeSpan3, k_EdgeTurnDiameter);
            nodeToP2Dist = Mathf.Max(0, nodeToP2Dist);

            Vector2 offset = OutputOrientation == Orientation.Horizontal
                ? new(k_EdgeTurnDiameter - nodeToP2Dist, 0)
                : new Vector2(0, k_EdgeTurnDiameter - nodeToP2Dist);

            m_RenderPoints.Add(p1);
            m_RenderPoints.Add(p2 - offset);
            m_RenderPoints.Add(p3 + offset);
            m_RenderPoints.Add(p4);
        }

        protected virtual void UpdateRenderPoints()
        {
            ComputeControlPoints(); // This should have been updated before : make sure anyway.

            if (m_RenderPointsDirty == false && ControlPoints != null) { return; }

            Vector2 p1 = Graph.ContentContainer.ChangeCoordinatesTo(this, ControlPoints[0]);
            Vector2 p2 = Graph.ContentContainer.ChangeCoordinatesTo(this, ControlPoints[1]);
            Vector2 p3 = Graph.ContentContainer.ChangeCoordinatesTo(this, ControlPoints[2]);
            Vector2 p4 = Graph.ContentContainer.ChangeCoordinatesTo(this, ControlPoints[3]);

            // Only compute this when the "local" points have actually changed
            if (m_LastLocalControlPoints.Count == 4)
            {
                if (Approximately(p1, m_LastLocalControlPoints[0]) &&
                    Approximately(p2, m_LastLocalControlPoints[1]) &&
                    Approximately(p3, m_LastLocalControlPoints[2]) &&
                    Approximately(p4, m_LastLocalControlPoints[3]))
                {
                    m_RenderPointsDirty = false;
                    return;
                }
            }

            Profiler.BeginSample("EdgeControl.UpdateRenderPoints");
            m_LastLocalControlPoints.Clear();
            m_LastLocalControlPoints.Add(p1);
            m_LastLocalControlPoints.Add(p2);
            m_LastLocalControlPoints.Add(p3);
            m_LastLocalControlPoints.Add(p4);
            m_RenderPointsDirty = false;

            m_RenderPoints.Clear();

            float diameter = k_EdgeTurnDiameter;

            // We have to handle a special case of the edge when it is a straight line, but not
            // when going backwards in space (where the start point is in front in y to the end point).
            // We do this by turning the line into 3 linear segments with no curves. This also
            // avoids possible NANs in later angle calculations.
            bool sameOrientations = OutputOrientation == InputOrientation;
            if (sameOrientations &&
                ((OutputOrientation == Orientation.Horizontal && Mathf.Abs(p1.y - p4.y) < 2 &&
                  p1.x + k_EdgeLengthFromPort < p4.x - k_EdgeLengthFromPort) ||
                 (OutputOrientation == Orientation.Vertical && Mathf.Abs(p1.x - p4.x) < 2 &&
                  p1.y + k_EdgeLengthFromPort < p4.y - k_EdgeLengthFromPort)))
            {
                RenderStraightLines(p1, p2, p3, p4);
                Profiler.EndSample();
                return;
            }

            bool renderBothCorners = true;

            EdgeCornerSweepValues corner1 = GetCornerSweepValues(p1, p2, p3, diameter, Direction.Output);
            EdgeCornerSweepValues corner2 = GetCornerSweepValues(p2, p3, p4, diameter, Direction.Input);

            if (!ValidateCornerSweepValues(ref corner1, ref corner2))
            {
                if (sameOrientations)
                {
                    RenderStraightLines(p1, p2, p3, p4);
                    Profiler.EndSample();
                    return;
                }

                renderBothCorners = false;

                //we try to do it with a single corner instead
                Vector2 px = OutputOrientation == Orientation.Horizontal ? new(p4.x, p1.y) : new Vector2(p1.x, p4.y);

                corner1 = GetCornerSweepValues(p1, px, p4, diameter, Direction.Output);
            }

            m_RenderPoints.Add(p1);

            if (!sameOrientations && renderBothCorners)
            {
                //if the 2 corners or endpoints are too close, the corner sweep angle calculations can't handle different orientations
                float minDistance = 2 * diameter * diameter;
                if ((p3 - p2).sqrMagnitude < minDistance ||
                    (p4 - p1).sqrMagnitude < minDistance)
                {
                    Vector2 px = (p2 + p3) * 0.5f;
                    corner1 = GetCornerSweepValues(p1, px, p4, diameter, Direction.Output);
                    renderBothCorners = false;
                }
            }

            GetRoundedCornerPoints(m_RenderPoints, corner1, Direction.Output);
            if (renderBothCorners) { GetRoundedCornerPoints(m_RenderPoints, corner2, Direction.Input); }

            m_RenderPoints.Add(p4);
            Profiler.EndSample();
        }

        private bool ValidateCornerSweepValues(ref EdgeCornerSweepValues corner1, ref EdgeCornerSweepValues corner2)
        {
            // Get the midpoint between the two corner circle centers.
            Vector2 circlesMidpoint = (corner1.circleCenter + corner2.circleCenter) / 2;

            // Find the angle to the corner circles midpoint so we can compare it to the sweep angles of each corner.
            Vector2 p2CenterToCross1 = corner1.circleCenter - corner1.crossPoint1;
            Vector2 p2CenterToCirclesMid = corner1.circleCenter - circlesMidpoint;
            double angleToCirclesMid = OutputOrientation == Orientation.Horizontal
                ? Math.Atan2(p2CenterToCross1.y, p2CenterToCross1.x) -
                  Math.Atan2(p2CenterToCirclesMid.y, p2CenterToCirclesMid.x)
                : Math.Atan2(p2CenterToCross1.x, p2CenterToCross1.y) -
                  Math.Atan2(p2CenterToCirclesMid.x, p2CenterToCirclesMid.y);

            if (double.IsNaN(angleToCirclesMid)) { return false; }

            // We need the angle to the circles midpoint to match the turn direction of the first corner's sweep angle.
            angleToCirclesMid = Math.Sign(angleToCirclesMid) * 2 * Mathf.PI - angleToCirclesMid;
            if (Mathf.Abs((float)angleToCirclesMid) > 1.5 * Mathf.PI)
            {
                angleToCirclesMid = -1 * Math.Sign(angleToCirclesMid) * 2 * Mathf.PI + angleToCirclesMid;
            }

            // Calculate the maximum sweep angle so that both corner sweeps and with the tangents of the 2 circles meeting each other.
            float h = p2CenterToCirclesMid.magnitude;
            float p2AngleToMidTangent = Mathf.Acos(corner1.radius / h);

            if (double.IsNaN(p2AngleToMidTangent)) { return false; }

            float maxSweepAngle = Mathf.Abs((float)corner1.sweepAngle) - p2AngleToMidTangent * 2;

            // If the angle to the circles midpoint is within the sweep angle, we need to apply our maximum sweep angle
            // calculated above, otherwise the maximum sweep angle is irrelevant.
            if (Mathf.Abs((float)angleToCirclesMid) < Mathf.Abs((float)corner1.sweepAngle))
            {
                corner1.sweepAngle = Math.Sign(corner1.sweepAngle) *
                                     Mathf.Min(maxSweepAngle, Mathf.Abs((float)corner1.sweepAngle));
                corner2.sweepAngle = Math.Sign(corner2.sweepAngle) *
                                     Mathf.Min(maxSweepAngle, Mathf.Abs((float)corner2.sweepAngle));
            }

            return true;
        }

        private EdgeCornerSweepValues GetCornerSweepValues(
            Vector2 p1, Vector2 cornerPoint, Vector2 p2, float diameter, Direction closestPortDirection)
        {
            EdgeCornerSweepValues corner = new();

            // Calculate initial radius. This radius can change depending on the sharpness of the corner.
            corner.radius = diameter / 2;

            // Calculate vectors from p1 to cornerPoint.
            Vector2 d1Corner = (cornerPoint - p1).normalized;
            Vector2 d1 = d1Corner * diameter;
            float dx1 = d1.x;
            float dy1 = d1.y;

            // Calculate vectors from p2 to cornerPoint.
            Vector2 d2Corner = (cornerPoint - p2).normalized;
            Vector2 d2 = d2Corner * diameter;
            float dx2 = d2.x;
            float dy2 = d2.y;

            // Calculate the angle of the corner (divided by 2).
            float angle = (float)(Math.Atan2(dy1, dx1) - Math.Atan2(dy2, dx2)) / 2;

            // Calculate the length of the segment between the cornerPoint and where
            // the corner circle with given radius meets the line.
            float tan = (float)Math.Abs(Math.Tan(angle));
            float segment = corner.radius / tan;

            // If the segment is larger than the diameter, we need to cap the segment
            // to the diameter and reduce the radius to match the segment. This is what
            // makes the corner turn radii get smaller as the edge corners get tighter.
            if (segment > diameter)
            {
                segment = diameter;
                corner.radius = diameter * tan;
            }

            // Calculate both cross points (where the circle touches the p1-cornerPoint line
            // and the p2-cornerPoint line).
            corner.crossPoint1 = cornerPoint - d1Corner * segment;
            corner.crossPoint2 = cornerPoint - d2Corner * segment;

            // Calculation of the coordinates of the circle center.
            corner.circleCenter = GetCornerCircleCenter(cornerPoint, corner.crossPoint1, corner.crossPoint2, segment,
                corner.radius);

            // Calculate the starting and ending angles.
            corner.startAngle = Math.Atan2(corner.crossPoint1.y - corner.circleCenter.y,
                corner.crossPoint1.x - corner.circleCenter.x);
            corner.endAngle = Math.Atan2(corner.crossPoint2.y - corner.circleCenter.y,
                corner.crossPoint2.x - corner.circleCenter.x);

            // Get the full sweep angle from the starting and ending angles.
            corner.sweepAngle = corner.endAngle - corner.startAngle;

            // If we are computing the second corner (into the input port), we want to start
            // the sweep going backwards.
            if (closestPortDirection == Direction.Input)
            {
                (corner.endAngle, corner.startAngle) = (corner.startAngle, corner.endAngle);
            }

            // Validate the sweep angle so it turns into the correct direction.
            if (corner.sweepAngle > Math.PI) { corner.sweepAngle = -2 * Math.PI + corner.sweepAngle; }
            else if (corner.sweepAngle < -Math.PI) { corner.sweepAngle = 2 * Math.PI + corner.sweepAngle; }

            return corner;
        }

        private Vector2 GetCornerCircleCenter(Vector2 cornerPoint, Vector2 crossPoint1, Vector2 crossPoint2,
            float segment, float radius)
        {
            float dx = cornerPoint.x * 2 - crossPoint1.x - crossPoint2.x;
            float dy = cornerPoint.y * 2 - crossPoint1.y - crossPoint2.y;

            Vector2 cornerToCenterVector = new(dx, dy);

            float L = cornerToCenterVector.magnitude;

            if (Mathf.Approximately(L, 0)) { return cornerPoint; }

            float d = new Vector2(segment, radius).magnitude;
            float factor = d / L;

            return new(cornerPoint.x - cornerToCenterVector.x * factor,
                cornerPoint.y - cornerToCenterVector.y * factor);
        }

        private void GetRoundedCornerPoints(List<Vector2> points, EdgeCornerSweepValues corner,
            Direction closestPortDirection)
        {
            // Calculate the number of points that will sample the arc from the sweep angle.
            int pointsCount = Mathf.CeilToInt((float)Math.Abs(corner.sweepAngle * k_EdgeSweepResampleRatio));
            int sign = Math.Sign(corner.sweepAngle);
            bool backwards = closestPortDirection == Direction.Input;

            for (int i = 0; i < pointsCount; ++i)
            {
                // If we are computing the second corner (into the input port), the sweep is going backwards
                // but we still need to add the points to the list in the correct order.
                float sweepIndex = backwards ? i - pointsCount : i;

                double sweepedAngle = corner.startAngle + sign * sweepIndex / k_EdgeSweepResampleRatio;

                float pointX = (float)(corner.circleCenter.x + Math.Cos(sweepedAngle) * corner.radius);
                float pointY = (float)(corner.circleCenter.y + Math.Sin(sweepedAngle) * corner.radius);

                // Check if we overlap the previous point. If we do, we skip this point so that we
                // don't cause the edge polygons to twist.
                if (i == 0 && backwards)
                {
                    if (OutputOrientation == Orientation.Horizontal)
                    {
                        if (corner.sweepAngle < 0 && points[^1].y > pointY) { continue; }
                        if (corner.sweepAngle >= 0 && points[^1].y < pointY) { continue; }
                    }
                    else
                    {
                        if (corner.sweepAngle < 0 && points[^1].x < pointX) { continue; }
                        if (corner.sweepAngle >= 0 && points[^1].x > pointX) { continue; }
                    }
                }

                points.Add(new(pointX, pointY));
            }
        }

        private void AssignControlPoint(ref Vector2 destination, Vector2 newValue)
        {
            if (!Approximately(destination, newValue))
            {
                destination = newValue;
                m_RenderPointsDirty = true;
            }
        }

        protected virtual void ComputeControlPoints()
        {
            if (m_ControlPointsDirty == false) { return; }

            Profiler.BeginSample("EdgeControl.ComputeControlPoints");

            float offset = k_EdgeLengthFromPort + k_EdgeTurnDiameter;

            // This is to ensure we don't have the edge extending
            // left and right by the offset right when the `from`
            // and `to` are on top of each other.
            float fromToDistance = (To - From).magnitude;
            offset = Mathf.Min(offset, fromToDistance * 2);
            offset = Mathf.Max(offset, k_EdgeTurnDiameter);

            if (ControlPoints == null || ControlPoints.Length != 4) { ControlPoints = new Vector2[4]; }

            AssignControlPoint(ref ControlPoints[0], From);

            if (OutputOrientation == Orientation.Horizontal)
            {
                AssignControlPoint(ref ControlPoints[1], new(From.x + offset, From.y));
            }
            else { AssignControlPoint(ref ControlPoints[1], new(From.x, From.y + offset)); }

            if (InputOrientation == Orientation.Horizontal)
            {
                AssignControlPoint(ref ControlPoints[2], new(To.x - offset, To.y));
            }
            else { AssignControlPoint(ref ControlPoints[2], new(To.x, To.y - offset)); }

            AssignControlPoint(ref ControlPoints[3], To);
            Profiler.EndSample();
        }

        private void ComputeLayout()
        {
            Profiler.BeginSample("EdgeControl.ComputeLayout");
            Vector2 to = ControlPoints[^1];
            Vector2 from = ControlPoints[0];

            Rect rect = new(Vector2.Min(to, from), new(Mathf.Abs(from.x - to.x), Mathf.Abs(from.y - to.y)));

            // Make sure any control points (including tangents, are included in the rect)
            for (int i = 1; i < ControlPoints.Length - 1; ++i)
            {
                if (!rect.Contains(ControlPoints[i]))
                {
                    Vector2 pt = ControlPoints[i];
                    rect.xMin = Math.Min(rect.xMin, pt.x);
                    rect.yMin = Math.Min(rect.yMin, pt.y);
                    rect.xMax = Math.Max(rect.xMax, pt.x);
                    rect.yMax = Math.Max(rect.yMax, pt.y);
                }
            }

            //Make sure that we have the place to display Edges with EdgeControl.k_MinEdgeWidth at the lowest level of zoom.
            // float margin = Mathf.Max(EdgeWidth * 0.5f + 1, k_MinEdgeWidth / Graph.minScale);
            float
                margin = EdgeWidth /
                         Graph.CurrentScale; //Mathf.Max(EdgeWidth * 0.5f + 1, k_MinEdgeWidth / Graph.minScale);
            rect.xMin -= margin;
            rect.yMin -= margin;
            rect.width += margin;
            rect.height += margin;

            if (layout != rect)
            {
                transform.position = new Vector2(rect.x, rect.y);
                style.width = rect.width;
                style.height = rect.height;
                m_RenderPointsDirty = true;
            }
            Profiler.EndSample();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (EdgeWidth <= 0 || Graph == null) { return; }

            UpdateRenderPoints();
            if (m_RenderPoints.Count == 0)
            {
                return; // Don't draw anything
            }

            // Color outColor = this.outputColor;
            Color inColor = InputColor;

            int cpt = m_RenderPoints.Count;
            Painter2D painter2D = mgc.painter2D;

            float width = EdgeWidth;

            // float alpha = 1.0f;
            float zoom = Graph.CurrentScale;

            if (EdgeWidth * zoom < k_MinEdgeWidth)
            {
                // alpha = edgeWidth * zoom / k_MinEdgeWidth;
                width = k_MinEdgeWidth / zoom;
            }

            // k_Gradient.SetKeys(new[]{ new GradientColorKey(outColor, 0),new GradientColorKey(inColor, 1)},new []{new GradientAlphaKey(alpha, 0)});
            painter2D.BeginPath();

            // painter2D.strokeGradient = k_Gradient;
            painter2D.strokeColor = inColor;
            painter2D.lineWidth = width;
            painter2D.MoveTo(m_RenderPoints[0]);

            for (int i = 1; i < cpt; ++i) { painter2D.LineTo(m_RenderPoints[i]); }

            painter2D.Stroke();
        }

        private struct EdgeCornerSweepValues
        {
            public Vector2 circleCenter;
            public double sweepAngle;
            public double startAngle;
            public double endAngle;
            public Vector2 crossPoint1;
            public Vector2 crossPoint2;
            public float radius;
        }

        #region Intersection
        public override bool ContainsPoint(Vector2 localPoint)
        {
            Profiler.BeginSample("EdgeControl.ContainsPoint");

            if (!base.ContainsPoint(localPoint))
            {
                Profiler.EndSample();
                return false;
            }

            // bounding box check succeeded, do more fine grained check by measuring distance to bezier points
            // exclude endpoints

            float capMaxDist = 4 * CapRadius * CapRadius; //(2 * CapRadius)^2
            if ((From - localPoint).sqrMagnitude <= capMaxDist || (To - localPoint).sqrMagnitude <= capMaxDist)
            {
                Profiler.EndSample();
                return false;
            }

            List<Vector2> allPoints = m_RenderPoints;
            if (allPoints.Count > 0)
            {
                //we use squareDistance to avoid sqrts
                float distance = (allPoints[0] - localPoint).sqrMagnitude;
                float interceptWidth2 = InterceptWidth * InterceptWidth;
                for (int i = 0; i < allPoints.Count - 1; i++)
                {
                    Vector2 currentPoint = allPoints[i];
                    Vector2 nextPoint = allPoints[i + 1];

                    Vector2 next2Current = nextPoint - currentPoint;
                    float distanceNext = (nextPoint - localPoint).sqrMagnitude;
                    float distanceLine = next2Current.sqrMagnitude;

                    // if the point is somewhere between the two points
                    if (distance < distanceLine && distanceNext < distanceLine)
                    {
                        //https://en.wikipedia.org/wiki/Distance_from_a_point_to_a_line
                        float d = next2Current.y * localPoint.x -
                                  next2Current.x * localPoint.y + nextPoint.x * currentPoint.y -
                                  nextPoint.y * currentPoint.x;
                        if (d * d < interceptWidth2 * distanceLine)
                        {
                            Profiler.EndSample();
                            return true;
                        }
                    }

                    distance = distanceNext;
                }
            }

            Profiler.EndSample();
            return false;
        }

        public override bool Overlaps(Rect rect)
        {
            if (base.Overlaps(rect))
            {
                for (int a = 0; a < m_RenderPoints.Count - 1; a++)
                {
                    if (RectUtils.IntersectsSegment(rect, m_RenderPoints[a], m_RenderPoints[a + 1])) { return true; }
                }
            }
            return false;
        }
        #endregion

        #region Event Handlers
        protected override void OnCustomStyleResolved(ICustomStyle styles)
        {
            base.OnCustomStyleResolved(styles);

            if (styles.TryGetValue(s_EdgeWidthProperty, out int edgeWidthValue)) { EdgeWidth = edgeWidthValue; }
            if (styles.TryGetValue(s_EdgeWidthSelectedProperty, out int edgeWidthSelectedValue))
            {
                EdgeWidthSelected = edgeWidthSelectedValue;
            }
            if (styles.TryGetValue(s_EdgeColorSelectedProperty, out Color selectColorValue))
            {
                ColorSelected = selectColorValue;
            }
            if (styles.TryGetValue(s_EdgeColorProperty, out Color edgeColorValue)) { ColorUnselected = edgeColorValue; }
            if (styles.TryGetValue(s_EdgeColorGhostProperty, out Color ghostColorValue))
            {
                ColorGhost = ghostColorValue;
            }
        }

        protected override void OnAddedToGraphView()
        {
            base.OnAddedToGraphView();
            Graph.OnViewTransformChanged += MarkDirtyOnTransformChanged;
            OnEdgeChanged();
        }

        protected override void OnRemovedFromGraphView()
        {
            base.OnRemovedFromGraphView();
            Graph.OnViewTransformChanged -= MarkDirtyOnTransformChanged;
        }

        private void MarkDirtyOnTransformChanged(GraphView gv) { MarkDirtyRepaint(); }

        protected override void OnEdgeChanged()
        {
            DrawFromCap = Output == null;
            DrawToCap = Input == null;
            m_ControlPointsDirty = true;
            UpdateLayout();
        }
        #endregion

        public override string ToString() => $"Output({Output}) -> Input({Input})";
    }
}