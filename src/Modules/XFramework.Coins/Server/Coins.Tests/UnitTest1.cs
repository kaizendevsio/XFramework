using System;
using Coins.Core.Drivers;
using Coins.Core.Interfaces.Wrappers;
using MediatR;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Coins.Tests
{
    [TestClass]
    public class UnitTest1
    {
        public UnitTest1()
        {
            BlockchainWrapper = new BlockchainInfoDriver( new()
            {
                WalletConfiguration = new()
                {
                    WalletIdentifier = "b9858562-b960-4083-a420-790d54b65a74",
                    WalletPassword = "B5YFDXRpY6sRZHJbQUyS3Dj",
                    PublicKey = "xpub6CuFcXUXBmprC1ecVP3txr4Q4uW3AtaUtq5EBv8xj1TNQn7Yj9X2k5Lpika5Ju7uwfe9K2QbWHR7iwrDutNEFRGYBQWfnPKoa9ixPXZRkEe"
                },
                MinConfirmations = 3,
                ServiceUrl = new Uri("http://127.0.0.1:3000/"),
                ApiUrl = new Uri("https://api.blockchain.info/v2/"),
                ApiCode = "9004fe40-5fd0-411a-ac42-4e820562c673",
                FeeUrl = new Uri("https://bitcoinfees.earn.com/api/v1/")
            });
        }

        public IBtcBlockchainWrapper BlockchainWrapper { get; set; }
        [TestMethod]
        public void TestBulkSend()
        {
            BlockchainWrapper.SendToMany(new()
            {
                new()
                {
                    Id = 1,
                    BtcAmount = 0.0005m,
                    BtcAddress = "3HmvQYSdKdvQ3cuEB7dMCi8f7AuhDEjXYo"
                },
                new()
                {
                    Id = 2,
                    BtcAmount = 0.0005m,
                    BtcAddress = "3HmvQYSdKdvQ3cuEB7dMCi8f7AuhDEjXYo"
                }
            });
        }
    }
}