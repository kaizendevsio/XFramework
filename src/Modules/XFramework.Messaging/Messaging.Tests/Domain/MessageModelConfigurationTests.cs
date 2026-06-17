using Messaging.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Contexts;

namespace Messaging.Tests.Domain;

public sealed class MessageModelConfigurationTests
{
    [Test]
    public void MessagingModel_MembershipDeliveryAndReactionUniqueness_Configured()
    {
        using var db = CreateDbContext();

        AssertUniqueIndex<MessageThreadMember>(
            db,
            "UX_MessageThreadMember_Thread_Credential_Active",
            "\"IsDeleted\" = false");

        AssertUniqueIndex<MessageDelivery>(
            db,
            "UX_MessageDelivery_Member_Message_Active",
            "\"IsDeleted\" = false");

        AssertUniqueIndex<MessageReaction>(
            db,
            "UX_MessageReaction_Message_Type_Member_Active",
            "\"IsDeleted\" = false");
    }

    [Test]
    public void MessagingModel_TimelineAndOutboxIndexes_Configured()
    {
        using var db = CreateDbContext();

        AssertIndex<Message>(
            db,
            "IX_Message_Thread_CreatedAt_Id");

        AssertIndex<MessageOutboxEvent>(
            db,
            "IX_MessageOutboxEvent_Tenant_Processed_Occurred");
    }

    private static AppDbContext CreateDbContext()
    {
        _ = typeof(MessageThread).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=xframework_model_test")
            .Options;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = Guid.NewGuid().ToString()
            })
            .Build();

        return new AppDbContext(options, new HttpContextAccessor(), config);
    }

    private static void AssertUniqueIndex<TEntity>(
        AppDbContext db,
        string indexName,
        string filter)
        where TEntity : class
    {
        var index = FindIndex<TEntity>(db, indexName);

        Assert.That(index.IsUnique, Is.True);
        Assert.That(index.GetFilter(), Is.EqualTo(filter));
    }

    private static void AssertIndex<TEntity>(AppDbContext db, string indexName)
        where TEntity : class
    {
        _ = FindIndex<TEntity>(db, indexName);
    }

    private static IIndex FindIndex<TEntity>(AppDbContext db, string indexName)
        where TEntity : class
    {
        var entityType = db.Model.FindEntityType(typeof(TEntity));
        Assert.That(entityType, Is.Not.Null);

        var index = entityType!.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == indexName);

        Assert.That(index, Is.Not.Null, $"Missing index {indexName} on {typeof(TEntity).Name}");
        return index!;
    }
}
