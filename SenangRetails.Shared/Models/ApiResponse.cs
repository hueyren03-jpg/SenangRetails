using SenangRetails.Shared.Models.Entities;

namespace SenangRetails.Shared.Models
{
    public class ApiResponse<T> : IApiResponse
    {
        // Success properties
        public T? Result { get; set; }
        public string? Message { get; set; }

        // Error properties
        public bool IsError { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }
        public Dictionary<string, object>? Extensions { get; set; }

        // HTTP Status code
        public int StatusCode { get; set; }

        int IApiResponse.statusCode
        {
            get => StatusCode;
            set => StatusCode = value;
        }

        string IApiResponse.message
        {
            get => Message ?? string.Empty;
            set => Message = value;
        }

        // Helper property to check if it's a "no records" error
        public bool IsNoRecordsError =>
            IsError &&
            (Title?.Contains("No records", StringComparison.OrdinalIgnoreCase) == true ||
             Detail?.Contains("No records", StringComparison.OrdinalIgnoreCase) == true);
    }
}
