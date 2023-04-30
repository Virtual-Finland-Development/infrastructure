using System.Text.Json;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace VirtualFinland.KeyRotator.Services.GitHub;

public class GitHubApi
{
    static HttpClient? _httpClient;
    ILambdaLogger _logger;
    Settings _settings;

    public GitHubApi(Settings settings, ILambdaContext context)
    {
        _logger = context.Logger;
        _settings = settings;
    }

    // <summary>
    // Get a http client with authorization headers for github api
    // </summary>
    public async Task<HttpClient> getGithubAPIClient()
    {
        if (_httpClient == null)
        {
            // Setup HttpClient with default headers for github api
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.github.com");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VirtualFinland.KeyRotator");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetGithubAccessToken()}");
        }
        return _httpClient;
    }

    // <summary>
    // Get a github access token from AWS Secrets Manager
    // </summary>
    async Task<string> GetGithubAccessToken()
    {
        _logger.LogInformation($"Getting github access token from AWS Secrets Manager");
        string secretName = _settings.SecretName;
        string region = _settings.SecretRegion;

        IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

        GetSecretValueRequest request = new GetSecretValueRequest
        {
            SecretId = secretName,
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