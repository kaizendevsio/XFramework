using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace XFramework.Data.BO
{
    public class UserAuthUpdateBO
    {
        public string Uid { get; set; }
        public string PasswordString { get; set; }
        public string NewPasswordString { get; set; }
    }
}
