﻿namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class AddFriendRequest : IAction
    {
        public Guid? CommunityIdentityGuid { get; set; }
    }
}