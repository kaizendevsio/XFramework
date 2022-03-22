namespace XFramework.Client.Shared.Entity.Properties
{
    public class ToolbarProp : ActivityBaseProp
    {
        public ToolbarProp()
        {
            Title = new();
            BackButton = new();
            SideBarButton = new();
            NotificationButton = new();
        }
        public TitleProp Title { get; set; }
        public NotificationButtonProp NotificationButton { get; set; }
        public BackButtonProp BackButton { get; set; }
        public SideBarButtonProp SideBarButton { get; set; }
    }
}