using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class DragAndDropManipulator : Manipulator
    {
        private readonly List<VisualElement> m_PickList;
        private bool m_BreachedDragThreshold;
        private DragAndDropContext m_CurrentDragContext;
        private IDraggable m_Draggable;
        private bool m_MouseDown;

        #region Constructor
        public DragAndDropManipulator() => m_PickList = new();
        #endregion

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
            target.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
            target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        #region Context
        private class DragAndDropContext : IDragBeginContext, IDragContext, IDragEndContext, IDragCancelContext,
            IDropEnterContext, IDropContext, IDropExitContext
        {
            private static readonly ObjectPool<DragAndDropContext> s_Pool = new(Create, Release, Destroy);
            private Vector2 m_MousePosition;
            private bool m_MousePositionInitialized;
            private object m_UserData;

            private DragAndDropContext() => CancelRequested = false;

            public IDroppable Droppable { get; internal set; }
            internal int DragThreshold { get; private set; }
            internal bool CancelRequested { get; private set; }
            public IDraggable Draggable { get; internal set; }
            public Vector2 MouseDelta => MousePosition - MousePositionPrevious;
            public Vector2 MouseResetDelta => MouseOrigin - MousePosition;
            public Vector2 MouseOrigin { get; internal set; }

            public Vector2 MousePosition
            {
                get => m_MousePosition;
                internal set
                {
                    // The first time we set this, MousePositionPrevious needs to hold the same value or delta values
                    // will be calculated incorrectly
                    if (m_MousePositionInitialized) { MousePositionPrevious = m_MousePosition; }
                    else
                    {
                        MousePositionPrevious = value;
                        m_MousePositionInitialized = true;
                    }
                    m_MousePosition = value;
                }
            }

            public Vector2 MousePositionPrevious { get; private set; }
            public MouseButton MouseButton { get; internal set; }
            public EventModifiers MouseModifiers { get; internal set; }

            public object GetUserData() => m_UserData;
            public void SetUserData(object userData) { m_UserData = userData; }
            public bool IsCancelled() => CancelRequested;
            public void CancelDrag() => CancelRequested = true;
            public void SetDragThreshold(int threshold) => DragThreshold = Mathf.Max(threshold, 0);

            internal static DragAndDropContext AcquireFromPool() => s_Pool.Get();
            internal static void ReleaseFromPool(DragAndDropContext toRelease) => s_Pool.Release(toRelease);

            private static DragAndDropContext Create() => new();

            private static void Release(DragAndDropContext toRelease)
            {
                toRelease.m_UserData = null;
                toRelease.m_MousePositionInitialized = false;
                toRelease.Droppable = null;
                toRelease.Draggable = null;
                toRelease.MouseOrigin = Vector2.zero;
                toRelease.MousePosition = Vector2.zero;
                toRelease.MousePositionPrevious = Vector2.zero;
                toRelease.MouseButton = MouseButton.LeftMouse;
                toRelease.MouseModifiers = EventModifiers.None;
                toRelease.CancelRequested = false;
                toRelease.DragThreshold = 0;
            }

            private static void Destroy(DragAndDropContext toDestroy) { }
        }
        #endregion

        #region Event Handlers
        private void OnMouseDown(MouseDownEvent e)
        {
            // Don't tolerate clicking other buttons while dragging 
            if (m_MouseDown)
            {
                CancelDrag(e);
                return;
            }

            // Pick top level draggable 
            m_Draggable = e.target as IDraggable;
            if (m_Draggable == null) { return; }

            // Create draggable event context
            m_MouseDown = true;
            m_CurrentDragContext = DragAndDropContext.AcquireFromPool();
            m_CurrentDragContext.MouseButton = (MouseButton)e.button;
            m_CurrentDragContext.MouseModifiers = e.modifiers;
            m_CurrentDragContext.MouseOrigin = e.mousePosition;
            m_CurrentDragContext.MousePosition = e.mousePosition;
            m_CurrentDragContext.Draggable = m_Draggable;
            m_Draggable.OnDragBegin(m_CurrentDragContext);

            // Capturing ensures we retain the mouse events even if the mouse leaves the target.
            target.CaptureMouse();
            e.StopImmediatePropagation();

            // Check for cancel request 
            if (m_CurrentDragContext.CancelRequested) { Reset(); }
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            // If the mouse isn't down, bail
            if (!m_MouseDown) { return; }

            // Check if user requested a cancel
            if (m_CurrentDragContext.CancelRequested)
            {
                CancelDrag(e);
                return;
            }

            // Consume this event since we're still tracking this drag (or potential drag)
            e.StopImmediatePropagation();

            // Update context and continue drag
            m_CurrentDragContext.MouseButton = (MouseButton)e.button;
            m_CurrentDragContext.MouseModifiers = e.modifiers;
            m_CurrentDragContext.MousePosition = e.mousePosition;

            // Check if we're starting a drag
            if (!m_BreachedDragThreshold)
            {
                // Have we breached the drag threshold?
                if (Mathf.Abs(m_CurrentDragContext.MouseResetDelta.magnitude) < m_CurrentDragContext.DragThreshold)
                {
                    // Not time yet to start dragging
                    return;
                }

                // We are starting a drag!
                m_BreachedDragThreshold = true;
            }

            // We breached the threshold, time to drag
            m_Draggable.OnDrag(m_CurrentDragContext);

            // Check for cancel request
            if (m_CurrentDragContext.CancelRequested)
            {
                CancelDrag(e);
                return;
            }

            // Look for droppable
            IDroppable droppable = PickFirstOfType<IDroppable>(e.mousePosition);
            if (droppable == null) { CheckForDragExit(); }
            else
            {
                // Drop exit
                CheckForDragExit();

                // Drop enter
                m_CurrentDragContext.Droppable = droppable;
                m_CurrentDragContext.Droppable.OnDropEnter(m_CurrentDragContext);
            }
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            // If the mouse isn't down or we're not dragging
            if (!m_MouseDown) { return; }

            // Check if user requested a cancel
            if (m_CurrentDragContext.CancelRequested)
            {
                CancelDrag(e);
                return;
            }

            // Consume this event since we're still tracking this drag (or potential drag)
            e.StopImmediatePropagation();

            // Update context 
            m_CurrentDragContext.MouseButton = (MouseButton)e.button;
            m_CurrentDragContext.MouseModifiers = e.modifiers;
            m_CurrentDragContext.MousePosition = e.mousePosition;

            // Check for drop
            if (m_CurrentDragContext.Droppable != null)
            {
                m_CurrentDragContext.Droppable.OnDrop(m_CurrentDragContext);
                m_CurrentDragContext.Droppable.OnDropExit(m_CurrentDragContext);
            }

            // Check if user requested a cancel
            Debug.Log($"Finished drop, shoudl cancel? {m_CurrentDragContext.CancelRequested}");
            if (m_CurrentDragContext.CancelRequested)
            {
                CancelDrag(e);
                return;
            }

            // Complete drag
            m_Draggable.OnDragEnd(m_CurrentDragContext);

            // Reset everything
            Reset();
        }

        private void OnMouseCaptureOutEvent(MouseCaptureOutEvent e)
        {
            if (m_MouseDown) { CancelDrag(e); }
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            // Only listen to ESC while dragging
            if (e.keyCode != KeyCode.Escape || !m_MouseDown) { return; }

            // Notify the draggable 
            CancelDrag(e);
        }
        #endregion

        #region Helpers
        private T PickFirstOfType<T>(Vector2 position, params VisualElement[] exclude) where T : class
        {
            target.panel.PickAll(position, m_PickList);
            if (m_PickList.Count == 0) { return null; }
            T toFind = null;
            foreach (VisualElement visualElement in m_PickList)
            {
                if (exclude.Length > 0 && Array.IndexOf(exclude, visualElement) == -1) { continue; }
                if (visualElement is T found)
                {
                    toFind = found;
                    break;
                }
            }
            m_PickList.Clear();
            return toFind;
        }

        private void CancelDrag(EventBase e)
        {
            // Only need to notify if we breached the drag threshold so they can reset
            if (m_BreachedDragThreshold)
            {
                m_Draggable.OnDragCancel(m_CurrentDragContext);
                CheckForDragExit();
            }
            Reset();
            e.StopImmediatePropagation();
        }

        private void CancelDrag<T>(MouseEventBase<T> e) where T : MouseEventBase<T>, new()
        {
            // Only need to notify if we breached the drag threshold so they can reset
            Debug.Log($"Starting cancel {m_BreachedDragThreshold}");
            if (m_BreachedDragThreshold)
            {
                m_CurrentDragContext.MouseButton = (MouseButton)e.button;
                m_CurrentDragContext.MouseModifiers = e.modifiers;
                m_CurrentDragContext.MousePosition = e.mousePosition;
                m_Draggable.OnDragCancel(m_CurrentDragContext);
                CheckForDragExit();
            }
            Reset();
            e.StopImmediatePropagation();
        }

        private void CheckForDragExit()
        {
            if (m_CurrentDragContext.Droppable != null)
            {
                m_CurrentDragContext.Droppable.OnDropExit(m_CurrentDragContext);
                m_CurrentDragContext.Droppable = null;
            }
        }

        private void Reset()
        {
            if (m_CurrentDragContext != null)
            {
                DragAndDropContext.ReleaseFromPool(m_CurrentDragContext);
                m_CurrentDragContext = null;
            }
            m_MouseDown = false;
            m_BreachedDragThreshold = false;
            m_Draggable = null;
            target.ReleaseMouse();
            m_PickList.Clear();
        }
        #endregion
    }

    #region Context Interfaces
    public interface IDragAndDropContext
    {
        public IDraggable Draggable { get; }
        public Vector2 MouseOrigin { get; }
        public Vector2 MousePosition { get; }
        public Vector2 MousePositionPrevious { get; }
        public Vector2 MouseDelta { get; }
        public Vector2 MouseResetDelta { get; }
        public MouseButton MouseButton { get; }
        public EventModifiers MouseModifiers { get; }
        public object GetUserData();
        public bool IsCancelled();
        public void CancelDrag();
    }

    public interface IBaseDragContext : IDragAndDropContext
    {
        public void SetUserData(object userData);
    }

    public interface IDragContext : IBaseDragContext
    {
    }

    public interface IDragBeginContext : IBaseDragContext
    {
        public void SetDragThreshold(int threshold);
    }

    public interface IDragEndContext : IBaseDragContext
    {
    }

    public interface IDragCancelContext : IBaseDragContext
    {
    }

    public interface IDropEnterContext : IDragAndDropContext
    {
    }

    public interface IDropContext : IDragAndDropContext
    {
    }

    public interface IDropExitContext : IDragAndDropContext
    {
    }
    #endregion

    #region IDraggable and IDroppable
    public interface IDraggable
    {
        void OnDragBegin(IDragBeginContext context);
        void OnDrag(IDragContext context);
        void OnDragEnd(IDragEndContext context);
        void OnDragCancel(IDragCancelContext context);
    }

    public interface IDroppable
    {
        void OnDropEnter(IDropEnterContext context);
        void OnDrop(IDropContext context);
        void OnDropExit(IDropExitContext context);
    }
    #endregion
}