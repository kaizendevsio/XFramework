namespace XFramework.Blazor.Entity.Properties
{
    public class FooterProp : ActivityBaseProp
    {
        public FooterProp()
        {
            Title = new();
        }
        public TitleProp Title { get; set; }
    }
}