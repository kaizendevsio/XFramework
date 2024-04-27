namespace XFramework.Blazor.Entity.Properties
{
    public class ActivityBaseProp
    {
        public ActivityBaseProp()
        {
            Colors = new();
        }

        public bool? Visible { get; set; }
        public Func<Task>? OnClick { get; set; }
        public string Color
        {
            get => Colors.AccentColor; set => Colors.AccentColor = value; }
        public ThemeColorProp Colors { get; set; }
    }
}