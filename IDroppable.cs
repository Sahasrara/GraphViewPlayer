// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public interface IDroppable
    {
        bool IsDroppable();
    }

    public interface IDropTarget
    {
        bool CanAcceptDrop(List<ISelectable> selection);

        // evt.mousePosition will be in global coordinates.
        bool DragUpdated(PlayerDragUpdatedEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource);
        bool DragPerform(PlayerDragPerformEvent evt, IEnumerable<ISelectable> selection, IDropTarget dropTarget, ISelection dragSource);
        bool DragEnter(PlayerDragEnterEvent evt, IEnumerable<ISelectable> selection, IDropTarget enteredTarget, ISelection dragSource);
        bool DragLeave(PlayerDragLeaveEvent evt, IEnumerable<ISelectable> selection, IDropTarget leftTarget, ISelection dragSource);
        bool DragExited();
    }
}
