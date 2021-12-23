namespace XFramework.Client.Shared.Entity.Properties
{
    public class ActivityProp
    {
        public ActivityProp()
        {
            Toolbar = new ToolbarProp {Visible = true};
            Footer = new FooterProp(){Title = new TitleProp() {Text = "©2018 LoadManna v.3.0"}};
            NavigationTab = new NavigationTabProp();
            
        }

        public string Title { get => Toolbar.Title.Text; set => Toolbar.Title.Text = value; }
        public string SubTitle { get; set; }
        public ToolbarProp Toolbar { get; set; }
        public FooterProp Footer { get; set; }
        public NavigationTabProp NavigationTab { get; set; }
        public string CustomClass { get; set; }

    }
}