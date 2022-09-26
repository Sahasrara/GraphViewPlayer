using UnityEngine.UIElements;

namespace GraphViewPlayer
{
     public abstract class PlayerDragAndDropEventBase<T> 
         : MouseEventBase<T>, IPlayerDragAndDropEvent where T : PlayerDragAndDropEventBase<T>, new() {}
}