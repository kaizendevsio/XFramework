namespace XFramework.Identity.Shared.Entity.Properties
{
    public class ActivityBaseProp
    {
        public ActivityBaseProp()
        {
            Colors = new ThemeColorProp();
        }
        public bool Visible { get; set; }
        public string Color
        {
            get => Colors.AccentColor; set => Colors.AccentColor = value; }
        public ThemeColorProp Colors { get; set; }
    }
}