using Ark.Mobile.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ark.Mobile.Pages.Client.User
{
    public class ScanBase : ActivityBase
    {
        public ScanBase()
        {
            ActivityTitle = "Campaign Access";
            ActivityQuote = "Scan QR Code";
        }
    }
}
