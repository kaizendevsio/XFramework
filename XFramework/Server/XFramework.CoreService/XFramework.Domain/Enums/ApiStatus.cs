﻿using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Domain.BO
{
    public enum ApiStatus : int
    {
        Success = 200,
        Error = 500,
        BadRequest = 400
    }
}