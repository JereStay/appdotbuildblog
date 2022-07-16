using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker.Http;

namespace StaticWebAppAuthentication.Api;

public static class StaticWebApiAppAuthorization
{
    public static ClientPrincipal ParseHttpHeaderForClientPrinciple(HttpHeadersCollection headers)
    {
        if (!headers.TryGetValues("x-ms-client-principal", out var header))
        {
            return new ClientPrincipal();
        }

        var data = header.First();
        var decoded = Convert.FromBase64String(data);
        var json = Encoding.UTF8.GetString(decoded);
        var principal = JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return principal ?? new ClientPrincipal();
    }
}