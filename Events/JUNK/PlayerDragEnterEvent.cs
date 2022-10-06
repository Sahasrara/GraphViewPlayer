namespace GraphViewPlayer 
{
    public class PlayerDragEnterEvent : PlayerDragAndDropEventBase<PlayerDragEnterEvent>
    {
        public PlayerDragEnterEvent() => LocalInit();
        protected override void Init()
        {
            base.Init();
            LocalInit();
        }
        private void LocalInit() => tricklesDown = true;
    }
}
