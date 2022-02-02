namespace XFramework.Client.Shared.Entity.Properties
{
    public class ViewProp
    {
        public ViewProp()
        {
            Toolbar = new() {Visible = true};
            Footer = new() {Title = new TitleProp() {Text = "©2021 XFramework.Client Evolve"}};
            NavigationTab = new();
            SideBar = new();
        }

        public string Title { get => Toolbar.Title.Text; set => Toolbar.Title.Text = value; }
        public ToolbarProp Toolbar { get; set; }
        public FooterProp Footer { get; set; }
        public SideBarProp SideBar { get; set; }
        public NavigationTabProp NavigationTab { get; set; }
        public string CustomClass { get; set; }

    }
}