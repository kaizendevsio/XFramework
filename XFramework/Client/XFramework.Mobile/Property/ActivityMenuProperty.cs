using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Mobile.Property
{
    public class ActivityMenuProperty : BaseProperty
    {
        public bool IsBackEnabled { get; set; }
        public bool IsLeftButtonEnabled { get; set; }
        public bool IsRightButtonEnabled { get; set; }
        public string ActivityMenuVisibility
        {
            get
            {
                switch (IsBackEnabled)
                {
                    case true:
                        return "block";
                    case false:
                        return "none";
                }
            }
        }

    }
}
