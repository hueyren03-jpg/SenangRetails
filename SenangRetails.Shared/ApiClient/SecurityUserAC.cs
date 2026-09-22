using EBI.DM;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;

namespace SenangRetails.Shared.ApiClient
{
    public class SecurityUserAC : BaseAC
    {
        private readonly IStoreTokenService _tokenService;

        public SecurityUserAC(IStoreTokenService tokenService)
        {
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponseRoot<Security_UserDM>?> GetUserByEmailAsync(string email)
        {
            if (!await SetBearerToken()) return null;
            return await PostAsync<object, ApiResponseRoot<Security_UserDM>>(
                "api/Security_User/GetUserByEmail", new { Id = email });
        }

        public async Task<ApiResponseRoot<object>?> UpdateRecordAsync(Security_UserDM model)
        {
            if (!await SetBearerToken()) return null;

            var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                System.Text.Json.JsonSerializer.Serialize(model))!;
            payload["SaveAction"] = System.Text.Json.JsonSerializer.SerializeToElement(2);
            payload["IsDirty"] = System.Text.Json.JsonSerializer.SerializeToElement(true);

            return await PutAsync<Dictionary<string, System.Text.Json.JsonElement>, ApiResponseRoot<object>>(
                "api/Security_User/UpdateRecord", payload);
        }
    }
}
