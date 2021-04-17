using System;
using System.Text.Json;
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

            var result = await helpService.HttpHelper.GetAsync(new Uri("https://bitcoinfees.earn.com/"), "api/v1/fees/recommended");
            Console.WriteLine(JsonSerializer.Serialize(result));
        }
    }
}