using System;
using MediatR;
using Messaging.Integration.Drivers;
using Messaging.Integration.Interfaces;
using Messaging.Tests.Interfaces;
using NUnit.Framework;
using XFramework.Integration.Drivers;
using XFramework.Integration.Services;

namespace Messaging.Tests;


public class Message : ITestModel
{
    [SetUp]
    public void Setup()
    {
        
        
    }

    [Test]
    public void Create()
    {
        //return;
        Assert.Pass();
    }

    [Test]
    public void Update()
    {
        Assert.Pass();
        throw new System.NotImplementedException();
    }

    [Test]
    public void Delete()
    {
        throw new System.NotImplementedException();
    }

    [Test]
    public void Verify()
    {
        throw new System.NotImplementedException();
    }

    [Test]
    public void Get()
    {
        throw new System.NotImplementedException();
    }

    [Test]
    public void GetList()
    {
        throw new System.NotImplementedException();
    }
}