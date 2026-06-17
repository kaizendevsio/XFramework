using System.Collections;
using System.Linq.Expressions;
using System.Security.Claims;
using Community.Api.Services;
using Community.Domain.Shared.Contracts;
using Community.Domain.Shared.Contracts.Requests;
using Community.Domain.Shared.Contracts.Responses;
using Community.Domain.Shared.Enums;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;

namespace Community.Tests.Services;

[TestFixture]
public sealed class CommunitySecurityTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CredentialId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid IdentityInfoId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid IdentityId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherIdentityId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Test]
    public async Task GetRequiredAsync_TokenWithoutTenantClaim_DerivesTenantFromCredential()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AuthLookupDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AuthLookupDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Tenants.Add(new Tenant
        {
            Id = TenantId,
            TenantId = TenantId,
            Name = "Test Tenant",
            IsEnabled = true
        });
        dbContext.IdentityInformation.Add(new IdentityInformation
        {
            Id = IdentityInfoId,
            TenantId = TenantId,
            IdentityName = "Requester",
            IsEnabled = true
        });
        dbContext.IdentityCredentials.Add(new IdentityCredential
        {
            Id = CredentialId,
            IdentityInfoId = IdentityInfoId,
            TenantId = TenantId,
            UserName = "requester",
            IsEnabled = true
        });
        await dbContext.SaveChangesAsync();

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, CredentialId.ToString())], "TestAuth");
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var requestContext = new CommunityRequestContext(
            httpContextAccessor,
            dbContext,
            new FakeDataContext());

        var result = await requestContext.GetRequiredAsync(new RequestMetadata { TenantId = TenantId });

        result.IsSuccess.Should().BeTrue();
        result.Data!.CredentialId.Should().Be(CredentialId);
        result.Data.TenantId.Should().Be(TenantId);
        httpContextAccessor.HttpContext.User.FindFirst("tenantId")?.Value.Should().Be(TenantId.ToString());
    }

    [Test]
    public async Task GetRequiredAsync_MetadataTenantMismatch_ReturnsForbidden()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AuthLookupDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AuthLookupDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Tenants.Add(new Tenant
        {
            Id = TenantId,
            TenantId = TenantId,
            Name = "Test Tenant",
            IsEnabled = true
        });
        dbContext.IdentityInformation.Add(new IdentityInformation
        {
            Id = IdentityInfoId,
            TenantId = TenantId,
            IdentityName = "Requester",
            IsEnabled = true
        });
        dbContext.IdentityCredentials.Add(new IdentityCredential
        {
            Id = CredentialId,
            IdentityInfoId = IdentityInfoId,
            TenantId = TenantId,
            UserName = "requester",
            IsEnabled = true
        });
        await dbContext.SaveChangesAsync();

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, CredentialId.ToString())], "TestAuth");
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        var requestContext = new CommunityRequestContext(
            httpContextAccessor,
            dbContext,
            new FakeDataContext());

        var result = await requestContext.GetRequiredAsync(new RequestMetadata { TenantId = OtherTenantId });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        httpContextAccessor.HttpContext.User.FindFirst("tenantId").Should().BeNull();
    }

    [Test]
    public async Task UpdateCommunityIdentityAsync_SpoofedProfile_ReturnsForbidden()
    {
        var dataContext = new FakeDataContext();
        var requesterContext = RequesterContext();
        dataContext.Seed(CurrentIdentity());

        var service = new CommunityService(
            dataContext,
            requesterContext,
            NullLogger<CommunityService>.Instance);

        var result = await service.UpdateCommunityIdentityAsync(new UpdateCommunityIdentityRequest
        {
            Id = OtherIdentityId,
            CommunityIdentityTypeId = Guid.NewGuid()
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        dataContext.Updated.Should().BeEmpty();
    }

    [Test]
    public async Task CreateContentAsync_SpoofedIdentityId_ReturnsForbidden()
    {
        var dataContext = new FakeDataContext();
        dataContext.Seed(
            CurrentIdentity(),
            new CommunityContentType { Id = Guid.NewGuid(), TenantId = TenantId, Name = "Post" });

        var service = CreateContentService(dataContext);

        var result = await service.CreateContentAsync(new CreateContentRequest
        {
            IdentityId = OtherIdentityId,
            TypeId = dataContext.Items<CommunityContentType>().Single().Id,
            Text = "spoofed"
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        dataContext.Added.OfType<CommunityContent>().Should().BeEmpty();
    }

    [Test]
    public async Task CreateContentAsync_CurrentIdentity_SetsTenantFromAuthenticatedContext()
    {
        var dataContext = new FakeDataContext();
        var typeId = Guid.NewGuid();
        dataContext.Seed(
            CurrentIdentity(),
            new CommunityContentType { Id = typeId, TenantId = TenantId, Name = "Post" });

        var service = CreateContentService(dataContext);

        var result = await service.CreateContentAsync(new CreateContentRequest
        {
            Metadata = new RequestMetadata { TenantId = TenantId },
            TypeId = typeId,
            Text = "owned by current identity"
        });

        result.IsSuccess.Should().BeTrue();
        var content = dataContext.Added.OfType<CommunityContent>().Single();
        content.TenantId.Should().Be(TenantId);
        content.SocialMediaIdentityId.Should().Be(IdentityId);
    }

    [Test]
    public async Task CreateContentReactionAsync_BlockedAuthor_ReturnsForbidden()
    {
        var dataContext = new FakeDataContext();
        var contentId = Guid.NewGuid();
        var reactionTypeId = Guid.NewGuid();
        dataContext.Seed(
            CurrentIdentity(),
            new CommunityContent
            {
                Id = contentId,
                TenantId = TenantId,
                SocialMediaIdentityId = OtherIdentityId,
                TypeId = Guid.NewGuid(),
                IsEnabled = true
            },
            new CommunityContentReactionType { Id = reactionTypeId, TenantId = TenantId, Name = "Like", Emoji = "+" });

        var service = CreateContentService(dataContext, blocked: true);

        var result = await service.CreateContentReactionAsync(new CreateContentReactionRequest
        {
            ContentId = contentId,
            TypeId = reactionTypeId
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        dataContext.Added.OfType<CommunityContentReaction>().Should().BeEmpty();
    }

    [Test]
    public async Task CreateConnectionAsync_SaveFailure_ReturnsFailure()
    {
        var dataContext = new FakeDataContext();
        var typeId = Guid.NewGuid();
        dataContext.Seed(
            CurrentIdentity(),
            new CommunityIdentity { Id = OtherIdentityId, TenantId = TenantId, CredentialId = Guid.NewGuid(), IsEnabled = true },
            new CommunityConnectionType { Id = typeId, TenantId = TenantId, Name = "Follow" });
        dataContext.QueueSaveResult(DataContextResult.Failure("database unavailable", 500));

        var notificationService = new StubNotificationService();
        var service = new ConnectionService(
            dataContext,
            notificationService,
            RequesterContext(),
            NullLogger<ConnectionService>.Instance);

        var result = await service.CreateConnectionAsync(new CreateConnectionRequest
        {
            TargetIdentityId = OtherIdentityId,
            TypeId = typeId
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.Message.Should().Be("database unavailable");
        dataContext.Added.OfType<CommunityConnection>().Single().TenantId.Should().Be(TenantId);
        notificationService.Created.Should().BeFalse();
    }

    [Test]
    public async Task MarkNotificationsReadAsync_NotificationOwnedByOtherIdentity_ReturnsNotFound()
    {
        var notificationId = Guid.NewGuid();
        var dataContext = new FakeDataContext();
        var notification = new CommunityNotification
        {
            Id = notificationId,
            TenantId = TenantId,
            RecipientIdentityId = OtherIdentityId,
            ActorIdentityId = IdentityId,
            Type = "Follow",
            IsEnabled = true,
            IsRead = false
        };
        dataContext.Seed(CurrentIdentity(), notification);

        var service = new NotificationService(
            dataContext,
            RequesterContext(),
            NullLogger<NotificationService>.Instance);

        var result = await service.MarkNotificationsReadAsync(new MarkNotificationsReadRequest
        {
            NotificationIds = [notificationId]
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        notification.IsRead.Should().BeFalse();
    }

    private static ContentService CreateContentService(FakeDataContext dataContext, bool blocked = false) =>
        new(
            dataContext,
            new StubConnectionService(blocked),
            RequesterContext(),
            NullLogger<ContentService>.Instance);

    private static StubCommunityRequestContext RequesterContext() =>
        new(new CommunityRequester(CredentialId, TenantId, CurrentIdentity()));

    private static CommunityIdentity CurrentIdentity() => new()
    {
        Id = IdentityId,
        TenantId = TenantId,
        CredentialId = CredentialId,
        IsEnabled = true,
        HandleName = "Current"
    };

    private sealed class AuthLookupDbContext(DbContextOptions<AuthLookupDbContext> options) : DbContext(options)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<IdentityInformation> IdentityInformation => Set<IdentityInformation>();
        public DbSet<IdentityCredential> IdentityCredentials => Set<IdentityCredential>();
    }

    private sealed class StubCommunityRequestContext(CommunityRequester requester) : ICommunityRequestContext
    {
        public Task<Result<CommunityRequester>> GetRequiredAsync(
            RequestMetadata? metadata,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CommunityRequester>.Success(requester));

        public Task<Result<CommunityRequester>> GetRequiredIdentityAsync(
            RequestMetadata? metadata,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CommunityRequester>.Success(requester));
    }

    private sealed class StubConnectionService(bool blocked) : IConnectionService
    {
        public Task<Result<CmdResponse>> CreateConnectionAsync(
            CreateConnectionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<CmdResponse>> DeleteConnectionAsync(
            DeleteConnectionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsBlockedAsync(Guid identityA, Guid identityB, CancellationToken cancellationToken = default) =>
            Task.FromResult(blocked);

        public Task<HashSet<Guid>> GetBlockedIdentityIdsAsync(Guid identityId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HashSet<Guid>());
    }

    private sealed class StubNotificationService : INotificationService
    {
        public bool Created { get; private set; }

        public Task<Result<CmdResponse>> MarkNotificationsReadAsync(
            MarkNotificationsReadRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<GetNotificationsResponse>> GetNotificationsAsync(
            GetNotificationsRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<CmdResponse>> CreateNotificationAsync(
            Guid tenantId,
            Guid recipientIdentityId,
            Guid actorIdentityId,
            NotificationType type,
            Guid? referenceId,
            string message,
            CancellationToken cancellationToken = default)
        {
            Created = true;
            return Task.FromResult(Result<CmdResponse>.Success(new CmdResponse()));
        }
    }

    private sealed class FakeDataContext : IDataContext
    {
        private readonly Dictionary<Type, IList> _sets = [];
        private readonly Queue<DataContextResult> _saveResults = [];

        public List<object> Added { get; } = [];
        public List<object> Updated { get; } = [];

        public void Seed(params object[] entities)
        {
            foreach (var entity in entities)
            {
                var set = GetOrCreateSet(entity.GetType());
                set.Add(entity);
            }
        }

        public List<T> Items<T>() where T : class
        {
            if (!_sets.TryGetValue(typeof(T), out var set))
            {
                set = new List<T>();
                _sets[typeof(T)] = set;
            }

            return (List<T>)set;
        }

        private IList GetOrCreateSet(Type type)
        {
            if (!_sets.TryGetValue(type, out var set))
            {
                var listType = typeof(List<>).MakeGenericType(type);
                set = (IList)Activator.CreateInstance(listType)!;
                _sets[type] = set;
            }

            return set;
        }

        public void QueueSaveResult(DataContextResult result) => _saveResults.Enqueue(result);

        public IRemoteQuery<T> Query<T>() where T : class =>
            new FakeRemoteQuery<T>(Items<T>().AsQueryable());

        public void Add<T>(T entity) where T : class
        {
            if (entity is BaseModel { Id: var id } model && id == Guid.Empty)
            {
                model.Id = Guid.NewGuid();
            }

            Items<T>().Add(entity);
            Added.Add(entity);
        }

        public void Update<T>(T entity) where T : class => Updated.Add(entity);

        public void Remove<T>(T entity) where T : class => Items<T>().Remove(entity);

        public Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default) =>
            Task.FromResult(_saveResults.Count > 0 ? _saveResults.Dequeue() : DataContextResult.Success());
    }

    private sealed class FakeRemoteQuery<T>(IQueryable<T> queryable) : IRemoteQuery<T>
        where T : class
    {
        private IQueryable<T> _queryable = queryable;

        public IRemoteQuery<T> Where(Expression<Func<T, bool>> predicate)
        {
            _queryable = _queryable.Where(predicate);
            return this;
        }

        public IRemoteQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _queryable = _queryable.OrderBy(keySelector);
            return this;
        }

        public IRemoteQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _queryable = _queryable.OrderByDescending(keySelector);
            return this;
        }

        public IRemoteQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _queryable = ((IOrderedQueryable<T>)_queryable).ThenBy(keySelector);
            return this;
        }

        public IRemoteQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _queryable = ((IOrderedQueryable<T>)_queryable).ThenByDescending(keySelector);
            return this;
        }

        public IRemoteQuery<T> Skip(int count)
        {
            _queryable = _queryable.Skip(count);
            return this;
        }

        public IRemoteQuery<T> Take(int count)
        {
            _queryable = _queryable.Take(count);
            return this;
        }

        public IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector) => this;

        public IRemoteQuery<T> Distinct()
        {
            _queryable = _queryable.Distinct();
            return this;
        }

        public IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _queryable = _queryable.AsEnumerable().DistinctBy(keySelector.Compile()).AsQueryable();
            return this;
        }

        public IRemoteQuery<T> NoCache() => this;

        public Task<List<T>> ToListAsync(CancellationToken ct = default) =>
            Task.FromResult(_queryable.ToList());

        public Task<T?> FirstOrDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(_queryable.FirstOrDefault());

        public Task<T?> SingleOrDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(_queryable.SingleOrDefault());

        public async IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var item in _queryable)
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }

        public Task<int> CountAsync(CancellationToken ct = default) =>
            Task.FromResult(_queryable.Count());

        public Task<bool> AnyAsync(CancellationToken ct = default) =>
            Task.FromResult(_queryable.Any());

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_queryable.Any(predicate));

        public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(_queryable.All(predicate));

        public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) =>
            Task.FromResult(_queryable.Select(selector).Min());

        public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) =>
            Task.FromResult(_queryable.Select(selector).Max());

        public Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) =>
            Task.FromResult(_queryable.AsEnumerable().MinBy(keySelector.Compile()));

        public Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) =>
            Task.FromResult(_queryable.AsEnumerable().MaxBy(keySelector.Compile()));

        public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) =>
            Task.FromResult(_queryable.Sum(selector));

        public Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) =>
            Task.FromResult((double)_queryable.Average(selector));

        public Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
            Expression<Func<T, TKey>> keySelector,
            CancellationToken ct = default)
        {
            var compiled = keySelector.Compile();
            return Task.FromResult(_queryable
                .AsEnumerable()
                .GroupBy(compiled)
                .Select(g => new GroupResult<TKey, T> { Key = g.Key, Items = g.ToList() })
                .ToList());
        }
    }
}
