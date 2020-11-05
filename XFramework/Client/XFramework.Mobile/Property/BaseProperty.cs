using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Mobile.Property
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
