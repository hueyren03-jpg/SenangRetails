using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EBI.DM;
using SenangRetails.Shared.ApiClient;
using SenangRetails.Shared.Entities;
using SenangRetails.Shared.Models.DTOs;
using SenangRetails.Shared.Models.DTOs.MembersBalanceSummary;
using SenangRetails.Shared.Models.Entities;
using SenangRetails.Shared.Services.Connectivity;
using static SenangRetails.Shared.ApiClient.CustomerAC;

namespace SenangRetails.Shared.Services.CustomerService
{
    public class CustomerService : ICustomerService
    {
        private readonly CustomerAC _customerAC;
        private readonly INetworkStatusService _network;

        public CustomerService(
            CustomerAC customerAc,
            INetworkStatusService network)
        {
            _customerAC = customerAc;
            _network = network;
        }

        public async Task<List<CustomerDM>> SearchCustomer(string keyword)
        {
            if (!_network.IsInternetAvailable)
                return new List<CustomerDM>();

            try
            {
                return await _customerAC.SearchCustomersAsync(keyword) ?? new List<CustomerDM>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CustomerService] Online search error: {ex.Message}");
                return new List<CustomerDM>();
            }
        }

        public async Task<CustomerDM> GetSingleCustomer(string keyword)
        {
            if (!_network.IsInternetAvailable)
                return new CustomerDM();

            try
            {
                return await _customerAC.GetSingleCustomerAsync(keyword) ?? new CustomerDM();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CustomerService] Online GetSingleCustomer error: {ex.Message}");
                return new CustomerDM();
            }
        }

        public async Task<ApiResponseRoot<CustomerCreateSuccess>?> CreateCustomer(CustomerDM customer)
        {
            return await _customerAC.CreateCustomerAsync(customer);
        }

        public async Task<ApiResponseRoot<string>?> UpdateCustomer(CustomerDM customer)
        {
            return await _customerAC.UpdateCustomerAsync(customer);
        }

        public async Task<ApiResponseRoot<CreditResponseDTO>> GetCreditBalance(string id)
        {
            return await _customerAC.FetchCreditBalance(id);
        }

        public async Task<MemberBalanceSummaryResult?> GetMemberBalanceSummaryAsync(string customerId)
        {
            var request = new MemberBalanceSummaryRequest { id = customerId };
            var response = await _customerAC.GetMemberBalanceSummary(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }

            return null;
        }

        public async Task<List<MemberOtherBalanceSummaryResult>?> GetMemberOtherBalanceSummaryAsync(string customerId, DateTime cutOffDate)
        {
            var request = new MemberOtherBalanceSummaryRequest
            {
                id = customerId,
                cutOffDate = cutOffDate
            };

            var response = await _customerAC.GetMemberOtherBalanceSummary(request);

            if (response?.statusCode == 200 && response.result != null)
            {
                return response.result;
            }

            return null;
        }

        public async Task<ApiResponseRoot<Dictionary<string, List<CustomerServiceRecordsDM>>>?> GetCustomerServiceRecordByMonthAsync(string customerId, int year, int month)
        {
            var request = new CustomerServiceRecordRequest
            {
                customerId = customerId,
                year = year,
                month = month
            };
            return await _customerAC.GetCustomerServiceRecordByMonth(request);
        }
    }
}
