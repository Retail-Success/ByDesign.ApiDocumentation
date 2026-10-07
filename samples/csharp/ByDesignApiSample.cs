// ByDesign API v2 - C# sample
//
// Requires: .NET 10 SDK.
// Supply your settings (values come from the ByDesign back office) as environment variables or
// command-line arguments. Command-line arguments take precedence.
//
//   Environment variable     Argument
//   BYDESIGN_API_URL         --api-url         e.g. https://your-api-url
//   BYDESIGN_CLIENT_ID       --client-id
//   BYDESIGN_CLIENT_SECRET   --client-secret
//   BYDESIGN_API_KEY         --api-key
//
// Run with:
//   dotnet run ByDesignApiSample.cs
//   dotnet run ByDesignApiSample.cs -- --api-url https://your-api-url --client-id ... --client-secret ... --api-key ...
//
// Arguments are visible in your shell history and process list, so prefer environment variables for the secret values.
//
// To use this in an existing project, copy ByDesignClientOptions, ByDesignTokenCache and ByDesignClient
// and drop the #: directive below. To register ByDesignClient as a typed HttpClient:
//
//   builder.Services.AddSingleton(new ByDesignClientOptions { ApiUrl = ..., ClientId = ..., ClientSecret = ..., ApiKey = ... });
//   builder.Services.AddSingleton<ByDesignTokenCache>();
//   builder.Services.AddHttpClient<ByDesignClient>();
//
// Typed clients are created per resolve, so the token cache must be a singleton to share one token across them.

#:property PublishAot=false

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var settings = ParseArguments(args);
string? missing = null;
string Setting(string argument, string environmentVariable)
{
    var value = settings.GetValueOrDefault(argument) ?? Environment.GetEnvironmentVariable(environmentVariable);
    if (string.IsNullOrWhiteSpace(value)) missing += $"\n  --{argument} or {environmentVariable}";
    return value ?? "";
}

var options = new ByDesignClientOptions
{
    ApiUrl = Setting("api-url", "BYDESIGN_API_URL"),
    ClientId = Setting("client-id", "BYDESIGN_CLIENT_ID"),
    ClientSecret = Setting("client-secret", "BYDESIGN_CLIENT_SECRET"),
    ApiKey = Setting("api-key", "BYDESIGN_API_KEY"),
};
if (missing is not null)
{
    Console.Error.WriteLine($"Missing required settings. Supply each as an argument or environment variable:{missing}");
    return 1;
}

using var http = new HttpClient();
var client = new ByDesignClient(http, options);

// GET request: list inventory categories (requires api/Read)
var categories = await client.SendAsync(HttpMethod.Get, "/api/v2/inventory/categorydetail");
Console.WriteLine(categories);

// POST request: look up customers by email (requires api/Write)
var customers = await client.SendAsync(HttpMethod.Post, "/api/v2/customer/lookup", new { Email = "jane.doe@example.com" });
Console.WriteLine(customers);

return 0;

// Accepts "--name value" and "--name=value"
static Dictionary<string, string> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) continue;
        var name = args[i][2..];
        var separator = name.IndexOf('=');
        if (separator >= 0)
            result[name[..separator]] = name[(separator + 1)..];
        else if (i + 1 < args.Length)
            result[name] = args[++i];
    }
    return result;
}

/// <summary>Settings for <see cref="ByDesignClient"/>, from the ByDesign back office.</summary>
public sealed class ByDesignClientOptions
{
    public required string ApiUrl { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string ApiKey { get; init; }
    public string Scope { get; init; } = "api/Read api/Write";
}

/// <summary>Caches the access token so it can be shared by every <see cref="ByDesignClient"/> instance. Register as a singleton.</summary>
public sealed class ByDesignTokenCache
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(HttpClient http, Uri tokenUri, ByDesignClientOptions options, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Reuse the token until one minute before it expires
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt.AddMinutes(-1))
                return _accessToken;

            using var response = await http.PostAsync(tokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["scope"] = options.Scope,
            }), cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Empty token response.");
            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Discards the cached token so the next request fetches a new one.</summary>
    public void Invalidate() => _accessToken = null;

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>
/// Minimal ByDesign API client that caches its access token and retries on 401 and 429.
/// Can be registered as a typed HttpClient; see the comments at the top of this file.
/// </summary>
public sealed class ByDesignClient
{
    private readonly HttpClient _http;
    private readonly ByDesignClientOptions _options;
    private readonly ByDesignTokenCache _tokenCache;
    private readonly Uri _baseUri;

    /// <param name="http">The HttpClient to send requests with. Its BaseAddress is not used; requests go to <see cref="ByDesignClientOptions.ApiUrl"/>.</param>
    /// <param name="options">Your ByDesign API settings.</param>
    /// <param name="tokenCache">A shared token cache. If omitted, this instance keeps its own.</param>
    public ByDesignClient(HttpClient http, ByDesignClientOptions options, ByDesignTokenCache? tokenCache = null)
    {
        _http = http;
        _options = options;
        _tokenCache = tokenCache ?? new ByDesignTokenCache();
        _baseUri = new Uri(options.ApiUrl.TrimEnd('/') + "/");
    }

    public async Task<string> SendAsync(HttpMethod method, string path, object? body = null, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, Resolve(path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken));
            request.Headers.Add("x-api-key", _options.ApiKey);
            if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
            {
                _tokenCache.Invalidate(); // Token expired or was revoked: fetch a new one and retry once
                continue;
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt <= 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                continue;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {content}");
            return content;
        }
    }

    private Task<string> GetTokenAsync(CancellationToken cancellationToken) =>
        _tokenCache.GetTokenAsync(_http, Resolve("/oauth2/token"), _options, cancellationToken);

    private Uri Resolve(string path) => new(_baseUri, path.TrimStart('/'));
}
