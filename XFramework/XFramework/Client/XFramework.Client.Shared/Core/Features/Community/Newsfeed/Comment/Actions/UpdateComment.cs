﻿namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class UpdateComment : BaseAction
    {
        public Guid? ContentGuid { get; set; }
        public string Text { get; set; }
    }
}