using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
namespace RibbonStudio
{
    public sealed class ApiRequestRejected : Exception { public readonly int Status; public ApiRequestRejected(int status, string message) : base(message) { Status = status; } }
    public sealed class ApiClient : IDisposable
    {
        readonly HttpClient client; public readonly string Address;
        public ApiClient(string address)
        {
            var uri = new Uri(address);
            if (uri.Scheme != "http" && uri.Scheme != "https")
                throw new Exception("后端地址必须使用 HTTP 或 HTTPS");
            Address = address.TrimEnd('/');
            client = new HttpClient { BaseAddress = new Uri(Address + "/"), Timeout = TimeSpan.FromMinutes(3) };
        }
        async Task<byte[]> Send(string route, object value, bool post)
        {
            using (var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, route.TrimStart('/')))
            {
                if (post)
                    request.Content = new StringContent(Json.Write(value), Encoding.UTF8, "application/json");
                using (var response = await client.SendAsync(request))
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        string message = Encoding.UTF8.GetString(bytes);
                        try
                        {
                            var data = Json.Read<System.Collections.Generic.Dictionary<string, object>>(message);
                            if (data.ContainsKey("message"))
                                message = Convert.ToString(data["message"]);
                            if (data.ContainsKey("issues"))
                                message += "\n" + Json.Write(data["issues"]);
                        }
                        catch { }
                        throw new ApiRequestRejected((int)response.StatusCode, ((int)response.StatusCode) + " · " + message);
                    }
                    return bytes;
                }
            }
        }
        public async Task<T> Get<T>(string route)
        {
            return Json.Read<T>(Encoding.UTF8.GetString(await Send(route, null, false)));
        }
        public async Task<T> Post<T>(string route, object value)
        {
            return Json.Read<T>(Encoding.UTF8.GetString(await Send("api/v1/" + route, value, true)));
        }
        public async Task<T> PostV2<T>(string route, object value)
        {
            return Json.Read<T>(Encoding.UTF8.GetString(await Send("api/v2/" + route, value, true)));
        }
        public async Task<string> Text(string route, object value)
        {
            return Encoding.UTF8.GetString(await Send("api/v1/" + route, value, true));
        }
        public Task<byte[]> Bytes(string route, object value)
        {
            return Send("api/v1/" + route, value, true);
        }
        public void Dispose()
        {
            client.Dispose();
        }
    }
}
