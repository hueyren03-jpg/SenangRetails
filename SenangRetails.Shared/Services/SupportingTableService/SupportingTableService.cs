using SenangRetails.Shared.Services.DataLayer.Abstractions;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Services.Connectivity;

namespace SenangRetails.Shared.Services.SupportingTableService
{
    public class SupportingTableService : ISupportingTableService
    {
        private readonly SupportingTableAC _ac;
        private readonly INetworkStatusService _network;
        private readonly ILocalJsonCache _cache;

        public SupportingTableService(SupportingTableAC ac, INetworkStatusService network, ILocalJsonCache cache)
        {
            _ac = ac;
            _network = network;
            _cache = cache;
        }

        public async Task<List<SupportingTableItem>?> LoadListByTypeAsync(int typeId)
        {
            // Product classification setup uses the server's current IDs and values.
            if (typeId is 4 or 34 or 53 or 54 or 55 or 56)
            {
                if (!_network.IsInternetAvailable) return null;
                var current = await _ac.LoadListByTypeAsync(typeId);
                return current?.statusCode == 200 ? current.result : null;
            }

            var cacheKey = $"supporting-table:{typeId}";
            if (_network.IsInternetAvailable)
            {
                try
                {
                    var response = await _ac.LoadListByTypeAsync(typeId);
                    if (response?.statusCode == 200 && response.result != null)
                    {
                        await _cache.SetAsync(cacheKey, response.result);
                        return response.result;
                    }
                    System.Diagnostics.Debug.WriteLine($"[SupportingTableService] LoadListByType({typeId}) failed: {response?.message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SupportingTableService] Online load failed: {ex.Message}");
                }
            }

            return await _cache.GetAsync<List<SupportingTableItem>>(cacheKey) ?? new();
        }

        public async Task<(bool Success, string Message)> CreateAsync(SupportingTableModel model)
        {
            if (!_network.IsInternetAvailable)
                return (false, "Internet connection is required to create categories.");
            var response = await _ac.CreateAsync(model);
            if (response?.statusCode == 200)
                return (true, response.message ?? "Category created successfully.");
            return (false, response?.message ?? "Failed to create category.");
        }

        public async Task<(bool Success, string Message)> UpdateAsync(SupportingTableModel model)
        {
            if (!_network.IsInternetAvailable)
                return (false, "Internet connection is required to update categories.");
            var response = await _ac.UpdateAsync(model);
            if (response?.statusCode == 200)
                return (true, response.message ?? "Category updated successfully.");
            return (false, response?.message ?? "Failed to update category.");
        }

        public async Task<(bool Success, string Message)> DeleteAsync(string supportingTableId)
        {
            if (!_network.IsInternetAvailable)
                return (false, "Internet connection is required to delete categories.");
            var response = await _ac.DeleteAsync(supportingTableId);
            if (response?.statusCode == 200)
                return (true, response.message ?? "Category deleted successfully.");
            return (false, response?.message ?? "Failed to delete category.");
        }
    }
}
