namespace XFramework.Client.Shared.Entity.Properties
{
    public class FooterProp : ActivityBaseProp
    {
        public FooterProp()
        {
            Title = new TitleProp();
        }
        public TitleProp Title { get; set; }
    }
}