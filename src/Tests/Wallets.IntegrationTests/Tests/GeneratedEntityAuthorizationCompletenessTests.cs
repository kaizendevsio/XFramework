using FluentAssertions;
using NUnit.Framework;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.Attributes;

namespace GeneratedAuthorizationContractTests.Wallets;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:Wallets")]
[Category("Area:GeneratedAuthorization")]
public sealed class GeneratedEntityAuthorizationCompletenessTests
{
    [Test]
    public void GeneratedWalletEntities_HaveCompleteCanonicalAuthorizationPolicies()
    {
        var registryType = typeof(global::Wallets.Api.Services.IWalletOperationsService)
            .Assembly
            .GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", throwOnError: true)!;
        var entities = (Dictionary<string, Type>)registryType
            .GetMethod("GetDataContextEntityTypes")!
            .Invoke(null, null)!;
        var policies = ((IReadOnlyCollection<GeneratedEntityAuthorizationPolicy>)registryType
                .GetMethod("GetDataContextAuthorizationPolicies")!
                .Invoke(null, null)!)
            .ToDictionary(policy => (policy.EntityTypeName, policy.Operation));
        var expectedEntities = new Dictionary<string, GeneratedEntityExpectation>
        {
            [nameof(CurrencyType)] = new(WalletAuthorizationCapabilities.View, "wallets", EndpointType.Both, "api/currencies"),
            [nameof(ExchangeRate)] = new(WalletAuthorizationCapabilities.View, "wallets", EndpointType.Both, "api/exchange-rates"),
            [nameof(WalletType)] = new(WalletAuthorizationCapabilities.View, "wallets", EndpointType.Both, "api/wallet-types"),
            [nameof(Wallet)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Service, "api/wallets"),
            [nameof(WalletAddress)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/wallet-addresses"),
            [nameof(DepositRequest)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/deposit-requests"),
            [nameof(WithdrawalRequest)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/withdrawal-requests"),
            [nameof(WalletOperation)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/wallet-operations"),
            [nameof(WalletLedgerEntry)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/wallet-ledger-entries"),
            [nameof(WalletBalanceSnapshot)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/wallet-balance-snapshots"),
            [nameof(WalletTransaction)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/wallet-transactions"),
            [nameof(WalletTransactionLineItem)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/wallet-transaction-line-items"),
            [nameof(WalletTransfer)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/wallet-transfers"),
            [nameof(WalletCase)] = new(WalletAuthorizationCapabilities.ReportingView, "wallets.reporting", EndpointType.Both, "api/wallet-cases"),
            [nameof(WalletApprovalRequest)] = new(WalletAuthorizationCapabilities.PolicyManage, "wallets.policy", EndpointType.Both, "api/wallet-approval-requests"),
            [nameof(WalletPolicyRule)] = new(WalletAuthorizationCapabilities.PolicyManage, "wallets.policy", EndpointType.Both, "api/wallet-policy-rules"),
            [nameof(WalletFeeSchedule)] = new(WalletAuthorizationCapabilities.PolicyManage, "wallets.policy", EndpointType.Both, "api/wallet-fee-schedules"),
            [nameof(WalletReconciliationItem)] = new(WalletAuthorizationCapabilities.ReconciliationManage, "wallets.reconciliation", EndpointType.Both, "api/wallet-reconciliation-items"),
            [nameof(WalletReconciliationRun)] = new(WalletAuthorizationCapabilities.ReconciliationManage, "wallets.reconciliation", EndpointType.Both, "api/wallet-reconciliation-runs"),
            [nameof(WalletOutboxMessage)] = new(WalletAuthorizationCapabilities.WebhooksManage, "wallets.webhooks", EndpointType.Both, "api/wallet-outbox-messages"),
            [nameof(WalletPaymentWebhookEvent)] = new(WalletAuthorizationCapabilities.WebhooksManage, "wallets.webhooks", EndpointType.Both, "api/wallet-payment-webhook-events")
        };

        entities.Keys.Should().BeEquivalentTo(expectedEntities.Keys);

        foreach (var (entityName, entityType) in entities)
        {
            var attribute = entityType.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false)
                .Cast<GenerateEndpointsAttribute>()
                .Single();
            var expectation = expectedEntities[entityName];
            attribute.AuthorizationFeature.Should().Be(expectation.Feature);
            attribute.Type.Should().Be(expectation.EndpointType);
            attribute.RoutePrefix.Should().Be(expectation.RoutePrefix);
            var allowsRemoteMutation = Attribute.IsDefined(
                entityType,
                typeof(AllowRemoteDataContextMutationAttribute));
            foreach (var operation in ExpectedOperations(attribute.Actions, allowsRemoteMutation))
            {
                policies.Should().ContainKey((entityName, operation));
                var policy = policies[(entityName, operation)];
                policy.ActorRequirement.Should().Be(XFramework.Integration.Security.ActorRequirement.Required);
                policy.RequiredCapability.Should().BeOneOf(WalletAuthorizationCapabilities.All);
                if (operation == GeneratedEntityOperation.Read)
                {
                    policy.RequiredCapability.Should().Be(expectation.ReadCapability);
                    policy.AuthorizationFeature.Should().Be(expectation.Feature);
                }
                policy.AllowServiceOnly.Should().BeFalse();
                policy.AllowRemoteQuery.Should().BeTrue();
                policy.AllowRemoteMutation.Should().Be(
                    allowsRemoteMutation && operation != GeneratedEntityOperation.Read);
            }
        }
    }

    private sealed record GeneratedEntityExpectation(
        string ReadCapability,
        string Feature,
        EndpointType EndpointType,
        string RoutePrefix);

    private static IEnumerable<GeneratedEntityOperation> ExpectedOperations(
        EndpointActions actions,
        bool allowsRemoteMutation)
    {
        if ((actions & EndpointActions.ReadOnly) != 0)
            yield return GeneratedEntityOperation.Read;
        if (allowsRemoteMutation || (actions & EndpointActions.Create) != 0)
            yield return GeneratedEntityOperation.Create;
        if (allowsRemoteMutation || (actions & EndpointActions.Update) != 0)
            yield return GeneratedEntityOperation.Update;
        if (allowsRemoteMutation || (actions & EndpointActions.Delete) != 0)
            yield return GeneratedEntityOperation.Delete;
    }
}
