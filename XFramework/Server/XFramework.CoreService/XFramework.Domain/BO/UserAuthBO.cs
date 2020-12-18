using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace XFramework.Domain.BO
{
   public class UserAuthBO
    {
        public string Uid { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string PasswordString { get; set; }
    }
}
