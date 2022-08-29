using System.Collections.Generic;
using MediatR;
using NUnit.Framework;

namespace Messaging.Tests.Interfaces;

public interface ITestModel
{
    public void Setup();
    public void Create();
    public void Update();
    public void Delete();
    public void Verify();
    public void Get();
    public void GetList();
}