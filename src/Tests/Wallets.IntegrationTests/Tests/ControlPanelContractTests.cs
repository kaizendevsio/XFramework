using System.Text.RegularExpressions;
using XFramework.TestInfrastructure;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.Wallets)]
[Category(TestCategories.ControlPanelContract)]
public sealed class ControlPanelContractTests
{
    private static readonly string[] FinancialWorkflowEntities =
    [
        "Wallet",
        "WalletTransaction",
        "WalletOperation",
        "WalletLedgerEntry",
        "WalletBalanceSnapshot",
        "WalletApprovalRequest",
        "DepositRequest",
        "WithdrawalRequest",
        "WalletCase",
        "WalletOutboxMessage",
        "WalletPaymentWebhookEvent",
        "WalletReconciliationRun",
        "WalletReconciliationItem",
        "WalletPolicyRule",
        "WalletFeeSchedule"
    ];

    private static readonly string[] WalletPickerPages =
    [
        "Wallets.razor",
        "DepositRequests.razor",
        "WithdrawalRequests.razor",
        "BatchOperations.razor",
        "WalletDetail.razor",
        "RefundsDisputes.razor"
    ];

    private static readonly (string Page, string DefaultTab, string[] Tabs)[] MultiGridTabbedFinancePages =
    [
        ("PolicyDecisions.razor", "policy-rules", ["policy-rules", "fee-schedules", "policy-decisions", "fee-ledger"]),
        ("RefundsDisputes.razor", "open-cases", ["open-cases", "refund-operations", "transactions"]),
        ("Statements.razor", "wallet-balances", ["wallet-balances", "statement-lines"]),
        ("Reconciliation.razor", "balance-snapshots", ["balance-snapshots", "reconciliation-items"]),
        ("OutboxWebhooks.razor", "outbox-messages", ["outbox-messages", "webhook-audit"])
    ];

    [Test]
    public void WalletsFinancePages_FinancialWorkflowMutations_DoNotUseDirectRemoteDataContextMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = GetFinancePagesRoot(repositoryRoot);

        var offenders = Directory.EnumerateFiles(pagesRoot.FullName, "*.razor", SearchOption.TopDirectoryOnly)
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return FinancialWorkflowEntities
                    .SelectMany(entity => FindDirectMutations(repositoryRoot, path, text, entity));
            })
            .ToArray();

        offenders.Should().BeEmpty("Wallets financial workflows must go through Wallets service wrappers or API workflows");
    }

    [Test]
    public void WalletsFinancePages_EntityDependencyPickers_DefineAdvancedSearchColumnsFiltersAndScopes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = GetFinancePagesRoot(repositoryRoot);

        foreach (var page in WalletPickerPages)
        {
            var text = File.ReadAllText(Path.Combine(pagesRoot.FullName, page));

            text.Should().Contain("<XfEntityPicker", $"{page} should select related Wallets entities through the shared picker");
            text.Should().Contain("AdvancedColumns=\"@", $"{page} pickers should define domain-specific advanced columns");
            text.Should().Contain("AdvancedFilters=\"@", $"{page} pickers should define domain-specific advanced filters");
            text.Should().Contain("AdvancedSearchScope=", $"{page} pickers should explain the Wallets-specific search scope");
            text.Should().NotContain("Label=\"Wallet ID\"", $"{page} should not ask admins to paste raw wallet IDs");
            text.Should().NotContain("Label=\"Original Transaction ID\"", $"{page} should not ask admins to paste raw transaction IDs");
        }
    }

    [Test]
    public void WalletsFinancePages_EntityDependencyPickers_DoNotExposeUnsafeCreateNewActions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = GetFinancePagesRoot(repositoryRoot);

        var offenders = WalletPickerPages
            .Select(page => (Page: page, Text: File.ReadAllText(Path.Combine(pagesRoot.FullName, page))))
            .Where(page => page.Text.Contains("ShowCreate=\"true\"", StringComparison.Ordinal)
                || page.Text.Contains("ShowCreate=\"@true\"", StringComparison.Ordinal))
            .Select(page => page.Page)
            .ToArray();

        offenders.Should().BeEmpty("Wallets dependency creation must stay behind existing service/API workflows unless a picker safely owns creation");
    }

    [Test]
    public void WalletsFinancePages_EntityDependencyPickers_EnableClearSelection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = GetFinancePagesRoot(repositoryRoot);

        var offenders = WalletPickerPages
            .SelectMany(page =>
            {
                var text = File.ReadAllText(Path.Combine(pagesRoot.FullName, page));
                return Regex.Matches(text, @"<XfEntityPicker[\s\S]*?/>")
                    .Cast<Match>()
                    .Select((match, index) => (Page: page, Picker: index + 1, Text: match.Value));
            })
            .Where(picker => !picker.Text.Contains("AllowClear=\"true\"", StringComparison.Ordinal)
                || !picker.Text.Contains("ClearText=", StringComparison.Ordinal))
            .Select(picker => $"{picker.Page} picker #{picker.Picker}")
            .ToArray();

        offenders.Should().BeEmpty("Wallets entity dependency pickers should let admins clear mistaken selections where the workflow continues to own creation/mutation");
    }

    [Test]
    public void WalletsMultiGridFinancePages_UseTabsInsteadOfStackedGridSections()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = GetFinancePagesRoot(repositoryRoot);

        foreach (var (page, defaultTab, tabs) in MultiGridTabbedFinancePages)
        {
            var text = File.ReadAllText(Path.Combine(pagesRoot.FullName, page));

            text.Should().Contain($"<BbTabs DefaultValue=\"{defaultTab}\"",
                $"{page} should group multiple data-heavy sections in tabs");
            text.Should().Contain("<BbTabsList>");

            foreach (var tab in tabs)
            {
                text.Should().Contain($"<BbTabsTrigger Value=\"{tab}\"");
                text.Should().Contain($"<BbTabsContent Value=\"{tab}\"");

                var content = Regex.Match(
                    text,
                    $@"<BbTabsContent\b(?=[^>]*\bValue=""{Regex.Escape(tab)}"")[\s\S]*?</BbTabsContent>");

                content.Success.Should().BeTrue($"{page} should render tab content for {tab}");
                Regex.Matches(content.Value, @"<BbDataGrid\b").Count.Should().Be(1,
                    $"{page} should keep the {tab} grid isolated in its own tab panel");
            }

            Regex.Matches(text, @"<BbTabsContent\b").Count.Should().Be(tabs.Length,
                $"{page} should have one tab panel for each data-heavy section");
            Regex.Matches(text, @"<BbDataGrid\b").Count.Should().Be(tabs.Length,
                $"{page} should keep each data-heavy grid in its own tab panel");

            var firstTabsIndex = text.IndexOf("<BbTabs", StringComparison.Ordinal);
            var lastTabsCloseIndex = text.LastIndexOf("</BbTabs>", StringComparison.Ordinal);
            firstTabsIndex.Should().BeGreaterThanOrEqualTo(0);
            lastTabsCloseIndex.Should().BeGreaterThan(firstTabsIndex);

            var outsideTabs = string.Concat(
                text.AsSpan(0, firstTabsIndex).ToString(),
                text.AsSpan(lastTabsCloseIndex + "</BbTabs>".Length).ToString());

            Regex.Matches(outsideTabs, @"<BbDataGrid\b").Count.Should().Be(0,
                $"{page} should not stack sibling grids outside the tabbed surface");
        }
    }

    [Test]
    public void WalletDetail_UsesSectionSidebarNavigation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = GetFinancePagesRoot(repositoryRoot);
        var layoutRoot = GetLayoutRoot(repositoryRoot);

        var detailPage = File.ReadAllText(Path.Combine(pagesRoot.FullName, "WalletDetail.razor"));
        var mainLayout = File.ReadAllText(Path.Combine(layoutRoot.FullName, "MainLayout.razor"));
        var sidebar = File.ReadAllText(Path.Combine(layoutRoot.FullName, "WalletDetailSidebar.razor"));

        detailPage.Should().Contain("@page \"/finance/wallets/{Id:guid}/{Section}\"",
            "wallet detail subpages should route to focused sections instead of one crammed page");
        detailPage.Should().Contain("CurrentSection == \"summary\"");
        detailPage.Should().Contain("CurrentSection == \"workflows\"");
        detailPage.Should().Contain("CurrentSection == \"transactions\"");
        detailPage.Should().Contain("CurrentSection == \"rules\"");

        mainLayout.Should().Contain("TryGetWalletDetailRoute", "wallet detail routes should replace the main module nav with the detail sidebar");
        mainLayout.Should().Contain("<WalletDetailSidebar WalletId=\"@walletRouteId\" />");

        sidebar.Should().Contain("Wallet List");
        sidebar.Should().Contain("SectionHref(\"summary\")");
        sidebar.Should().Contain("SectionHref(\"workflows\")");
        sidebar.Should().Contain("SectionHref(\"transactions\")");
        sidebar.Should().Contain("SectionHref(\"rules\")");
    }

    private static DirectoryInfo GetFinancePagesRoot(DirectoryInfo repositoryRoot) =>
        new(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "ControlPanel.Server",
            "Components",
            "Pages",
            "Finance"));

    private static DirectoryInfo GetLayoutRoot(DirectoryInfo repositoryRoot) =>
        new(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Presentation",
            "ControlPanel.Server",
            "Components",
            "Layout"));

    private static IEnumerable<string> FindDirectMutations(
        DirectoryInfo repositoryRoot,
        string path,
        string text,
        string entity)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot.FullName, path);
        var operations = new[] { "Add", "Update", "Remove" };

        foreach (var operation in operations)
        {
            var inlinePattern = $@"DataContext\.{operation}\s*\(\s*new\s+{Regex.Escape(entity)}\b";
            if (Regex.IsMatch(text, inlinePattern, RegexOptions.Multiline))
                yield return $"{relativePath} directly {operation.ToLowerInvariant()}s {entity}";

            var genericPattern = $@"DataContext\.{operation}\s*<\s*{Regex.Escape(entity)}\s*>";
            if (Regex.IsMatch(text, genericPattern, RegexOptions.Multiline))
                yield return $"{relativePath} directly {operation.ToLowerInvariant()}s {entity}";
        }

        foreach (Match declaration in Regex.Matches(
            text,
            $@"(?:var|{Regex.Escape(entity)})\s+(\w+)\s*=\s*new\s+{Regex.Escape(entity)}\b",
            RegexOptions.Multiline))
        {
            var variableName = Regex.Escape(declaration.Groups[1].Value);
            foreach (var operation in operations)
            {
                var variableMutationPattern = $@"DataContext\.{operation}\s*\(\s*{variableName}\s*\)";
                if (Regex.IsMatch(text, variableMutationPattern, RegexOptions.Multiline))
                    yield return $"{relativePath} directly {operation.ToLowerInvariant()}s {entity}";
            }
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
                return current;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate XFramework repository root.");
    }
}
