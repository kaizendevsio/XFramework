using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
[NonParallelizable]
public sealed partial class RemoteDataContextConcurrencyTests
{
    private FieldInfo _wrapperMapField = null!;
    private object? _originalWrapperMap;

    [SetUp]
    public void SetUp()
    {
        _wrapperMapField = typeof(RemoteDataContext).GetField(
            "_wrapperMap",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        _originalWrapperMap = _wrapperMapField.GetValue(null);
        _wrapperMapField.SetValue(null, new Dictionary<string, string>
        {
            [nameof(ConcurrentMutationEntity)] = typeof(BlockingMutationWrapper).FullName!
        });
    }

    [TearDown]
    public void TearDown() => _wrapperMapField.SetValue(null, _originalWrapperMap);

    [Test]
    public async Task SaveChangesAsync_WhenMutationArrivesDuringSave_DoesNotDropOrDuplicateIt()
    {
        var wrapper = new BlockingMutationWrapper();
        using var services = new ServiceCollection().AddSingleton(wrapper).BuildServiceProvider();
        var context = new RemoteDataContext(services);
        var first = new ConcurrentMutationEntity { Id = Guid.NewGuid() };
        var second = new ConcurrentMutationEntity { Id = Guid.NewGuid() };

        context.Add(first);
        var firstSave = context.SaveChangesAsync();
        await wrapper.FirstRequestEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        context.Add(second);
        var secondSave = context.SaveChangesAsync();
        wrapper.ReleaseFirstRequest.SetResult();

        (await firstSave).IsSuccess.Should().BeTrue();
        (await secondSave).IsSuccess.Should().BeTrue();
        wrapper.Batches.SelectMany(batch => batch).Should().Equal(first.Id, second.Id);
        wrapper.Batches.Should().OnlyContain(batch => batch.Length == 1);
    }

    [MemoryPackable]
    public sealed partial class ConcurrentMutationEntity : IHasId
    {
        public Guid Id { get; set; }
    }

    private sealed class BlockingMutationWrapper : IDataContextServiceWrapper
    {
        public TaskCompletionSource FirstRequestEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstRequest { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<Guid[]> Batches { get; } = [];

        public Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async Task<byte[]> ExecuteChangesAsync(
            byte[] saveChangesRequestBytes,
            CancellationToken ct = default)
        {
            var request = MemoryPackSerializer.Deserialize<SaveChangesRequest>(saveChangesRequestBytes)!;
            Batches.Add(request.Changes
                .Select(change => MemoryPackSerializer.Deserialize<ConcurrentMutationEntity>(change.SerializedEntity)!.Id)
                .ToArray());

            if (Batches.Count == 1)
            {
                FirstRequestEntered.SetResult();
                await ReleaseFirstRequest.Task.WaitAsync(ct);
            }

            return MemoryPackSerializer.Serialize(DataContextResult.Success());
        }

        public async IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(
            byte[] queryDescriptorBytes,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
