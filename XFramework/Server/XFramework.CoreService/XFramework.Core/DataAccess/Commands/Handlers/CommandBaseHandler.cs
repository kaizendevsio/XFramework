using System;
using System.Collections.Generic;
using System.Text;
using AutoMapper;
using XFramework.Core.Interfaces;

namespace XFramework.Core.DataAccess.Commands.Handlers
{
    public class CommandBaseHandler
    {
        protected IDataLayer DataLayer;
        protected IMapper Mapper;
    }
}
