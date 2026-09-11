using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

var client = new HttpClient();
client.BaseAddress = new Uri("https://ebisoftware.com.my:5000/");
var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiI2OTk5MDc2MS1lZGVkLTRkMjItOTJiMS0yYjc1YzlkN2UyODMiLCJzdWIiOiJlYmlkZW1vMkBnbWFpbC5jb20iLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6ImViaWRlbW8yQGdtYWlsLmNvbSIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWUiOiJlYmlkZW1vMkBnbWFpbC5jb20iLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9lbWFpbGFkZHJlc3MiOiJlYmlkZW1vMkBnbWFpbC5jb20iLCJUZW5hbnRNb2RlIjoiU2luZ2xlVGVuYW50IiwiSXNIb3N0VXNlciI6ImZhbHNlIiwiSXNTdXBlckFkbWluIjoidHJ1ZSIsIkNvbXBhbmllcyI6InFOQU1xSnJmWUsiLCJyZWZyZXNoQXQiOiIxNzg5MzUzODYwIiwiZXhwIjoxNzkxNzUzODYwLCJpc3MiOiJodHRwczovL2ViaXNvZnR3YXJlLmNvbS5teSIsImF1ZCI6Imh0dHBzOi8vZWJpc29mdHdhcmUuY29tLm15In0.od4Zn0_-bhlaaAt52Z5PYsuecElr8GccIQaxQxLdEaQ";
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

var listRes = await client.PostAsync("api/Inventory/GetList", new StringContent("{}", Encoding.UTF8, "application/json"));
var respStr = await listRes.Content.ReadAsStringAsync();
Console.WriteLine($"Status: {listRes.StatusCode}");
Console.WriteLine($"Length: {respStr.Length}");
Console.WriteLine($"Snippet: {respStr.Substring(0, Math.Min(300, respStr.Length))}");
var listDoc = JsonNode.Parse(respStr) as JsonObject;
var arr = listDoc?["result"] as JsonArray;
Console.WriteLine($"Total items in GetList: {arr?.Count}");

int withSubCat = 0, withCat = 0, withDiv = 0, withDept = 0, withAppCat = 0;
if (arr != null)
{
    foreach (var node in arr)
    {
        var item = node as JsonObject;
        if (item == null) continue;
        if (!string.IsNullOrEmpty(item["ItemSubCategoryID"]?.ToString())) withSubCat++;
        if (!string.IsNullOrEmpty(item["ItemCategoryID"]?.ToString())) withCat++;
        if (!string.IsNullOrEmpty(item["ItemDivisionID"]?.ToString())) withDiv++;
        if (!string.IsNullOrEmpty(item["ItemDepartmentID"]?.ToString())) withDept++;
        if (!string.IsNullOrEmpty(item["ItemAppCategoryID"]?.ToString())) withAppCat++;
    }
}
Console.WriteLine($"With ItemSubCategoryID: {withSubCat}");
Console.WriteLine($"With ItemCategoryID: {withCat}");
Console.WriteLine($"With ItemDivisionID: {withDiv}");
Console.WriteLine($"With ItemDepartmentID: {withDept}");
Console.WriteLine($"With ItemAppCategoryID: {withAppCat}");

if (withSubCat > 0 || withCat > 0)
{
    foreach (var node in arr)
    {
        var item = node as JsonObject;
        if (!string.IsNullOrEmpty(item?["ItemSubCategoryID"]?.ToString()) || !string.IsNullOrEmpty(item?["ItemCategoryID"]?.ToString()))
        {
            Console.WriteLine($"Found item: {item?["MasterAccountID"]} SubCat={item?["ItemSubCategoryID"]} Cat={item?["ItemCategoryID"]} Div={item?["ItemDivisionID"]} AppCat={item?["ItemAppCategoryID"]}");
            break;
        }
    }
}
