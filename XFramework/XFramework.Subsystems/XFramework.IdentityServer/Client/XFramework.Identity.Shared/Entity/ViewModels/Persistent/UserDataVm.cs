﻿namespace XFramework.Identity.Shared.Entity.ViewModels.Persistent
{
    public class UserDataVm
    {
        public string ContactNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string PasswordString { get; set; }
        public string ConfirmPasswordString { get; set; }
        public bool Agree { get; set; }
    }
}