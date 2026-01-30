using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Vector.Core.Plugins;

public class WebSearchPlugin
{
    private readonly string _endpoint;
    private readonly HttpClient _http = new HttpClient();

    public WebSearchPlugin(string endpoint)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    [KernelFunction]
    [Description("Performs a web search using the configured local search endpoint and returns raw results.")]
    public async Task<string> SearchAsync([Description("Search query string.")] string query)
    {
        try
        {
            var resp = await _http.GetAsync(_endpoint + "?q=" + Uri.EscapeDataString(query));
            if (!resp.IsSuccessStatusCode) return "ERROR: search failed";
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }
}
