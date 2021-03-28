namespace XFramework.Identity.Shared.Entity.Properties
{
    public class ActivityProp
    {
        public ActivityProp()
        {
            Toolbar = new ToolbarProp {Visible = true};
        }

        public string Title { get; set; }
        /*public string Title { get => Toolbar.Title.Text; set => Toolbar.Title.Text = value; }*/
        public string SubTitle { get; set; }
        public ToolbarProp Toolbar { get; set; }


    }
}