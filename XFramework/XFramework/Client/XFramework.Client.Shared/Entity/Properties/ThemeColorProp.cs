namespace XFramework.Client.Shared.Entity.Properties
{
    public class ThemeColorProp
    {
        public string BaseColor { get; set; }
        public string AccentColor { get; set; } = "danger";
        public string BorderColor { get; set; } = "danger";
        public List<string> Colors { get; set; }
    }
}