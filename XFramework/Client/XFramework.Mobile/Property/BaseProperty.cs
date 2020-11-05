namespace XFramework.Mobile.Property
{
    public class BaseProperty
    {
        public bool IsVisible { get; set; }
        public string DisplayProperty
        {
            get
            {
                switch (IsVisible)
                {
                    case true:
                        return "Block";
                    default:
                        return "none";
                }
            }
        }
    }
}
