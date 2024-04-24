namespace XFramework.Blazor.Entity.Properties
{
    public class ActivityProp
    {
        public ActivityProp()
        {
            Toolbar = new() {Visible = true};
            Footer = new(){Title = new() {Text = "©2018 LoadManna v.3.0"}};
            NavigationTab = new();
            
        }

        public string Title { get; set; }
        
        
        
        /*public string Title { get => Toolbar.Title.Text; set => Toolbar.Title.Text = value; }*/
        public string SubTitle { get; set; }
        public ToolbarProp Toolbar { get; set; }
        public FooterProp Footer { get; set; }
        public NavigationTabProp NavigationTab { get; set; }
        
        public string CustomClass { get; set; }
        
        


    }
}