using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;

namespace XFramework.Tests
{
    [TestClass]
    public class HelpersTest
    {
        [TestMethod]
        public async Task TestPost()
        {
            IHelperService helpService = new HelperService();
            var request = new TestModel()
            {
                FastestFee = 123.3m,
                HourFee = 234.23m,
                HalfHourFee = 13.232m
            };
            
            var result1 = await helpService.Http.PostAsync<string>(new Uri("https://asdasdasdsad.free.beeceptor.com"), "api/v1/fees/recommended",request);
            Console.WriteLine(JsonSerializer.Serialize(result1));
        }
        
        [TestMethod]
        public async Task TestGet()
        {
            IHelperService helpService = new HelperService();
            var request = new TestModel()
            {
                FastestFee = 123.3m,
                HourFee = 234.23m,
                HalfHourFee = 13.232m
            };
            
            var result1 = await helpService.Http.GetAsync<string>(new Uri("https://asdasdasdsad.free.beeceptor.com"), "api/v1/fees/recommended");
            Console.WriteLine(JsonSerializer.Serialize(result1));
        }
    }
    
    public class TestModel
    {
        public decimal FastestFee { get; set; }
        public decimal HalfHourFee { get; set; }
        public decimal HourFee { get; set; }
    }
}