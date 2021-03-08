namespace XFramework.Identity.Shared.Entity.Properties
{
    public class ToolbarProp : ActivityBaseProp
    {
        public ToolbarProp()
        {
            Title = new TitleProp();
            BackButton = new BackButtonProp();
            SideBarButton = new SideBarButtonProp();
        }
        public TitleProp Title { get; set; }
        public BackButtonProp BackButton { get; set; }
        public SideBarButtonProp SideBarButton { get; set; }
    }
}