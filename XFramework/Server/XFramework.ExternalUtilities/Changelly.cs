using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using XFramework.External.Models;
using Newtonsoft.Json;

namespace XFramework.External
{
    public class Changelly
    {

        public ChangellyApiSettings GetSettings()
        {
            ChangellyApiSettings changellyApiSettings = new ChangellyApiSettings();
            changellyApiSettings.ApiKey = "0c80f631af534ee7a22c62ccb5c2cfc3";
            changellyApiSettings.ApiSecret = "3a444385fea39cda7d4fa178f74e0289a0a3d6707fce2e3382eedc161a249933";
            changellyApiSettings.ApiUrl = "https://api.changelly.com";

            return changellyApiSettings;
        }

        public static string ToHexString(byte[] array)
        {
            StringBuilder hex = new StringBuilder(array.Length * 2);
            foreach (byte b in array)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        public ChangellyResponse CreateTransaction(string SourceCurrency, string TargetCurrency, decimal Amount)
        {
            ChangellyApiSettings changellyApiSettings = GetSettings();
            WebClient Client = new WebClient();
            Encoding U8 = Encoding.UTF8;

            //ChangellyMessage changellyMessage = new ChangellyMessage();
            //changellyMessage.id = "test";
            //changellyMessage.jsonrpc = "2.0";
            //changellyMessage.method = "createTransaction";
            //changellyMessage.Params = new ChangellyParams { from = SourceCurrency, to = TargetCurrency, amount = 1, address = "1DbHRB5aSng6owoUiQN7NgMFRwXbRdRzrV" };


            //         string message = @"{
            //	""jsonrpc"": ""2.0"",
            //	""id"": ""test"",
            //	""method"": ""createTransaction"",
            //	""params"": [
            //                     ""from"": ""eth"",
            //                     ""to"": ""btc"",
            //                     ""address"": ""1DbHRB5aSng6owoUiQN7NgMFRwXbRdRzrV"",
            //                     ""amount"": ""1"",
            //                    ]
            //}";

            //string message = JsonConvert.SerializeObject(changellyMessage);
            string message = @"{""jsonrpc"":""2.0"",""id"":""test"",""method"":""createTransaction"",""params"":{""from"":""" + SourceCurrency + @""",""to"":""" + TargetCurrency + @""",""address"":""16wUekRU1UTr7YZCdxmuGuBKrxLxMZHCdB"",""extraId"":null,""amount"":" + Amount + @"}}";

            HMACSHA512 hmac = new HMACSHA512(U8.GetBytes(changellyApiSettings.ApiSecret));
            byte[] hashmessage = hmac.ComputeHash(U8.GetBytes(message));
            string sign = ToHexString(hashmessage);

            Client.Headers.Set("Content-Type", "application/json");
            Client.Headers.Add("api-key", changellyApiSettings.ApiKey);
            Client.Headers.Add("sign", sign);

            string result = Client.UploadString(changellyApiSettings.ApiUrl, message);
            return JsonConvert.DeserializeObject<ChangellyResponse>(result);
        }

        //private static void Main(string[] args)
        //{
        //	string message = @"{
        //		""jsonrpc"": ""2.0"",
        //		""id"": 1,
        //		""method"": ""getCurrencies"",
        //		""params"": []
        //	}";

        //	HMACSHA512 hmac = new HMACSHA512(U8.GetBytes(apiSecret));
        //	byte[] hashmessage = hmac.ComputeHash(U8.GetBytes(message));
        //	string sign = ToHexString(hashmessage);

        //	Client.Headers.Set("Content-Type", "application/json");
        //	Client.Headers.Add("api-key", apiKey);
        //	Client.Headers.Add("sign", sign);

        //	string result = Client.UploadString(apiUrl, message);
        //	Console.WriteLine(result);
        //}
    }
}
