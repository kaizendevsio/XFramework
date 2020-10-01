using System;
using Info.Blockchain.API.Client;
using Info.Blockchain.API.Wallet;
using Info.Blockchain.API.Models;
using XFramework.External.Models;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace XFramework.External
{
    public class CoinCap
    {
        public CoinCapApiSettings GetSettings()
        {
            CoinCapApiSettings coinCapApiSettings = new CoinCapApiSettings();
            coinCapApiSettings.ApiUri = new Uri("https://api.coincap.io/");

            return coinCapApiSettings;
        }

        public CoinProperty GetCoinProperty(string coinName)
        {
            HttpUtilities httpUtilities = new HttpUtilities();
            CoinCapApiSettings coinCapApiSettings = GetSettings();
            HttpResponseBO _res = httpUtilities.GetAsync(coinCapApiSettings.ApiUri, "v2/assets/" + coinName).Result;
            CoinProperty coinProperty = JsonConvert.DeserializeObject<CoinProperty>(_res.ResponseResult);

            return coinProperty;

        }
    }
}
