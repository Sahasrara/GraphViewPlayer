using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class PlayerDragLeaveEvent : PlayerDragAndDropEventBase<PlayerDragLeaveEvent>
    {
        public PlayerDragLeaveEvent() => LocalInit();
        protected override void Init()
        {
            base.Init();
            LocalInit();
        }
        private void LocalInit() => tricklesDown = true;
    }
}
