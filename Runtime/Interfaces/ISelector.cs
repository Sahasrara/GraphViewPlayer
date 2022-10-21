using System;
using System.Collections.Generic;

namespace GraphViewPlayer
{
    public interface ISelector
    {
        void SelectAll();
        void ClearSelection();
        void CollectAll(List<ISelectable> toPopulate);
        void CollectSelected(List<ISelectable> toPopulate);
        void CollectUnselected(List<ISelectable> toPopulate);
        void ForEachAll(Action<ISelectable> action);
        void ForEachSelected(Action<ISelectable> action);
        void ForEachUnselected(Action<ISelectable> action);
    }
}