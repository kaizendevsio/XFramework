﻿using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Domain.BO
{
   public class UserAuthStatusBO
    {
        public string Uid { get; set; }
        public AuthStatus Status { get; set; }
    }
}