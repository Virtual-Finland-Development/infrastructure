using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace VirtualFinland.KeyRotator.Services.GitHub;

public abstract class GitHubApi
{
    private IHttpClientFactory _httpClientFactory;
    private IAmazonSecretsManager _secretsManagerClient;
    private static HttpClient? _httpClient;
    protected ILambdaLogger _logger;
    private readonly string _awsSecretName;

    public GitHubApi(IHttpClientFactory httpClientFactory, IAmazonSecretsManager secretsManagerClient, Settings settings, ILambdaLogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _secretsManagerClient = secretsManagerClient;
        _logger = logger;
        _awsSecretName = settings.SecretName;
    }

    /// <summary>
    /// Get a http client with authorization headers for github api
    /// </summary>
    protected async Task<HttpClient> GetGithubAPIClient()
    {
        if (_httpClient == null)
        {
            // Setup HttpClient with default headers for github api
            _httpClient = _httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri("https://api.github.com");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VirtualFinland.KeyRotator");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetGithubAccessToken()}");
        }
        return _httpClient;
    }

    /// <summary>
    /// Get a github access token from AWS Secrets Manager
    /// </summary>
    private async Task<string> GetGithubAccessToken()
    {
        _logger.LogInformation("Retrieving GitHub access token from AWS Secrets Manager");

        GetSecretValueRequest request = new GetSecretValueRequest
        {
            SecretId = _awsSecretName,
            VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
        };

        GetSecretValueResponse response;

        // For a list of the exceptions thrown, see
        // https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html
        response = await _secretsManagerClient.GetSecretValueAsync(request);

        // Parse JSON response.
        var secretObject = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);

        if (secretObject == null || !secretObject.ContainsKey("CICD_BOT_GITHUB_ACCESS_TOKEN"))
        {
            throw new ArgumentNullException("Secret not found");
        }

        return secretObject["CICD_BOT_GITHUB_ACCESS_TOKEN"];
    }
}