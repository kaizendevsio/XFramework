using System.Collections.Generic;

namespace XFramework.Identity.Shared.Entity.ViewModels.Profile
{
    public class VerificationsVm
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleInitial { get; set; }
        public string RegisteredContactNumber { get; set; }
        public string EmailAddress { get; set; }
        public string BusinessType { get; set; }
        public string BusinessName { get; set; }
        public string BusinessAddress { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public string Region { get; set; }
        public string Video { get; set; }
        public string StoreFrontPhoto { get; set; }
        public string ValidIDPhoto { get; set; }
        public string SelfTakenPhoto { get; set; }
        public List<IdTypeVm> IdTypes { get; set; } = new List<IdTypeVm>()
        {
            new () {Name = "Passport"},
            new () {Name = "Postal ID"},
            new () {Name = "Voter's ID"}
          
        };

        public string BusinessPermit { get; set; }
        public string TermsAndConditions { get; set; }
        
    }
    
}