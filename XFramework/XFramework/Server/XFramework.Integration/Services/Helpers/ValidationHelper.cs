using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace XFramework.Integration.Services.Helpers
{
    public static class ValidationHelper
    {
        public static string ValidatePhoneNumber(this string phoneNumber, bool convertOnly = false)
        {
            if (convertOnly)
            {
                if (phoneNumber.Any(char.IsLetter)) return phoneNumber;
                if (phoneNumber.Contains('+') != true)
                {
                    phoneNumber = $"+63{phoneNumber.Substring(1)}";
                }
                return phoneNumber;
            }
            
            if (string.IsNullOrEmpty(phoneNumber))
            {
                throw new ArgumentException("Phone number is required");
            }
            
            if (phoneNumber.Any(char.IsLetter))
            {
                throw new ArgumentException("Phone number should not contain letters");
            }
            
            if (phoneNumber.Contains('+'))
            {
                if (phoneNumber.Length != 13)
                {
                    throw new ArgumentException("Incorrect Phone number format, format should be +63XXXXXXXXXX or 09XXXXXXXXX");
                }
            }
            else
            {
                if (phoneNumber.Length != 11)
                {
                    throw new ArgumentException("Incorrect Phone number format, format should be +63XXXXXXXXXX or 09XXXXXXXXX");
                }
            }
            if (phoneNumber.Contains('+') != true)
            {
                phoneNumber = $"+63{phoneNumber.Substring(1)}";
            }
            return phoneNumber;
        }

        public static void ValidateEmailAddress(this string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException("Email is required");
            }
            
            if (!email.Contains("@"))
            {
                throw new ArgumentException("Incorrect email format");
            }
        }
        
        public static void ValidatePassword(this string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password is required");
            }
            
            if (!Regex.Match(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$").Success)
            {
                throw new ArgumentException("Weak Password! Use 8 or more characters, with a mix of capital letter, small letter, numbers and symbols");
            }
        }
    }
}