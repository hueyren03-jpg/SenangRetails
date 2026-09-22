using EBI.DM;
using SenangRetails.Shared.Models;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.AuthService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SenangRetails.Shared.ApiClient
{
    public class StaffAC : BaseAC
    {
        private readonly HttpClient _httpClient;
        private readonly IStoreTokenService _tokenService;

        public StaffAC(HttpClient httpClient, IStoreTokenService tokenService)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
        }

        private async Task<bool> SetBearerToken()
        {
            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;
            return CreateBearerAuthAsync(token);
        }

        public async Task<ApiResponse<List<EmployeeDM>>> FetchStaffListAsync()
        {
            if (!await SetBearerToken()) return null;
            var apiResponse = await PostAsync<object, ApiResponse<List<EmployeeDM>>>("api/Employee/GetAllEmployees", null);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var jsonDebug = System.Text.Json.JsonSerializer.Serialize(apiResponse, options);
            Debug.WriteLine("testing1", jsonDebug);
            return apiResponse ?? new ApiResponse<List<EmployeeDM>> { StatusCode = 500, Message = "No response from server" };
        }

        public async Task<ApiResponseRoot<string>> CreateStaffAsync(EmployeeDM payload)
        {
            if (!await SetBearerToken()) return null;

            var wirePayload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                System.Text.Json.JsonSerializer.Serialize(payload))!;
            wirePayload["SaveAction"] = System.Text.Json.JsonSerializer.SerializeToElement("Added");
            wirePayload["IsDirty"] = System.Text.Json.JsonSerializer.SerializeToElement(true);

            var debugOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string payloadJson = System.Text.Json.JsonSerializer.Serialize(wirePayload, debugOptions);

            Console.WriteLine("DEBUG: Sending Payload to api/Employee/CreateRecord:");
            Console.WriteLine(payloadJson);

            return await PostAsync<Dictionary<string, System.Text.Json.JsonElement>, ApiResponseRoot<string>>(
                "api/Employee/CreateRecord", wirePayload);
        }

        public async Task<ApiResponseRoot<string>> PutStaffAsync(EmployeeDM payload)
        {
            if (!await SetBearerToken()) return null;

            var wirePayload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                System.Text.Json.JsonSerializer.Serialize(payload))!;
            wirePayload["SaveAction"] = System.Text.Json.JsonSerializer.SerializeToElement("Changed");
            wirePayload["IsDirty"] = System.Text.Json.JsonSerializer.SerializeToElement(true);

            var debugOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string payloadJson = System.Text.Json.JsonSerializer.Serialize(wirePayload, debugOptions);

            Console.WriteLine("DEBUG: Sending Payload to api/Employee/UpdateRecord:");
            Console.WriteLine(payloadJson);

            return await PutAsync<Dictionary<string, System.Text.Json.JsonElement>, ApiResponseRoot<string>>(
                "api/Employee/UpdateRecord", wirePayload);
        }
    }
}
