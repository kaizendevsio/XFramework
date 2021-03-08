using System.Collections.Generic;

namespace XFramework.Identity.Shared.Entity.Properties
{
    public class ThemeColorProp
    {
        public string BaseColor { get; set; }
        public string AccentColor { get; set; } = "danger";
        public List<string> Colors { get; set; }
    }
}