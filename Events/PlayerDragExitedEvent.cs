namespace GraphViewPlayer
{
    public class PlayerDragExitedEvent : PlayerDragAndDropEventBase<PlayerDragExitedEvent>
    {
        public PlayerDragExitedEvent() => LocalInit();
        
        protected override void Init()
        {
            base.Init();
            LocalInit();
        }

        private void LocalInit()
        {
            bubbles = true;
            tricklesDown = true;
        }
    } 
}

