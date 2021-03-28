﻿using System;
using System.Threading.Tasks;
using IdentityServer.Integration.Interfaces;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Integration.Wrappers
{
    public class RecordsWrapper : IRecordsWrapper
    {
        public Task<Guid> NewLog(string title, string message, LogType logType = LogType.ApplicationServiceLog, GenericPriorityType priorityType = GenericPriorityType.Information)
        {
            throw new NotImplementedException();
        }

        public Task<Guid> NewAuthorizationLog(AuthenticationState authenticationState, Guid cuid)
        {
            throw new NotImplementedException();
        }

        public Task UpdateLog(Guid guid, string title, string message, LogType logType = LogType.ApplicationServiceLog,
            GenericPriorityType priorityType = GenericPriorityType.Information)
        {
            throw new NotImplementedException();
        }
    }
}