using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.BarcodeSetupService;
using SenangRetails.Shared.Services.Connectivity;
using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.Services.PaymentService;
using SenangRetails.Shared.Services.PriceGroupService;
using SenangRetails.Shared.Services.PromotionSetupService;
using SenangRetails.Shared.Services.DiscountSetupService;
using SenangRetails.Shared.Services.TaxRateService;

namespace SenangRetails.Shared.Services.DataLayer;

public sealed record LocalDataBootstrapResult(bool Completed, IReadOnlyList<string> Warnings);

public interface ILocalDataBootstrapService
{
    Task<LocalDataBootstrapResult> PrepareBranchAsync(
        BranchItem branch,
        CancellationToken cancellationToken = default);
}

public sealed class LocalDataBootstrapService(
    ILocalDataAvailability localDataAvailability,
    INetworkStatusService network,
    ProductCacheService productCache,
    IPaymentService paymentService,
    IGstTaxRateService taxRateService,
    IPriceGroupService priceGroupService,
    IPromotionSetupService promotionSetupService,
    IBarcodeSetupService barcodeSetupService,
    IDiscountSetupService discountSetupService) : ILocalDataBootstrapService
{
    public async Task<LocalDataBootstrapResult> PrepareBranchAsync(
        BranchItem branch,
        CancellationToken cancellationToken = default)
    {
        if (!localDataAvailability.HasPersistentStore)
            return new(true, []);
        if (!network.IsInternetAvailable)
            return new(false, ["Offline data was not refreshed because internet is unavailable."]);

        var warnings = new List<string>();
        var branchId = branch.BranchID ?? string.Empty;
        var groupId = branch.BranchGroupID ?? string.Empty;
        var customerId = branch.CustomerID ?? string.Empty;

        await RunAsync("products and categories", () => productCache.RefreshAsync(branchId), warnings);
        await RunAsync("payment methods", async () =>
        {
            await paymentService.GetPaymentMethodsAsync(branchId, groupId, customerId);
        }, warnings);
        await RunAsync("tax configuration", async () =>
        {
            await taxRateService.GetRateForTaxCodeAsync(branch.DefaultSalesTaxCodeID, cancellationToken);
        }, warnings);

        // These checkout rules currently use WebView local storage; reading them here ensures
        // their defaults are materialized while JavaScript is available.
        await RunAsync("price groups", async () => { await priceGroupService.GetPriceGroupsAsync(); }, warnings);
        await RunAsync("promotion rules", async () => { await promotionSetupService.GetPromotionsAsync(); }, warnings);
        await RunAsync("discount rules", async () => { await discountSetupService.GetDiscountsAsync(); }, warnings);
        await RunAsync("barcode rules", async () => { await barcodeSetupService.GetRulesAsync(); }, warnings);

        return new(warnings.Count == 0, warnings);
    }

    private static async Task RunAsync(string name, Func<Task> action, List<string> warnings)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not refresh {name}: {ex.Message}");
        }
    }
}
