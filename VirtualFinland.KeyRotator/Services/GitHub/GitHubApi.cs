using System.Text.Json;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace VirtualFinland.KeyRotator.Services.GitHub;

public abstract class GitHubApi
{
    public IHttpClientFactory _httpClientFactory;
    static HttpClient? _httpClient;
    public ILambdaLogger _logger;
    protected readonly string _awsSecretName;
    protected readonly string _awsSecretRegion;

    public GitHubApi(IHttpClientFactory httpClientFactory, Settings settings, ILambdaLogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _awsSecretName = settings.SecretName;
        _awsSecretRegion = settings.SecretRegion;
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
    async Task<string> GetGithubAccessToken()
    {
        _logger.LogInformation($"Retrieving GitHub access token from AWS Secrets Manager");

        IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(_awsSecretRegion));

        GetSecretValueRequest request = new GetSecretValueRequest
        {
            SecretId = _awsSecretName,
            VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
        };

        GetSecretValueResponse response;

        try
        {
            response = await client.GetSecretValueAsync(request);
        }
        catch (Exception e)
        {
            // For a list of the exceptions thrown, see
            // https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html
            throw e;
        }

        // Parse JSON response.
        var secretObject = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);

        if (secretObject == null || !secretObject.ContainsKey("CICD_BOT_GITHUB_ACCESS_TOKEN"))
        {
            throw new ArgumentNullException("Secret not found");
        }

        return secretObject["CICD_BOT_GITHUB_ACCESS_TOKEN"];
    }
}