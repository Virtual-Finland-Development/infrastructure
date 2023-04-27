/*
 *	Use this code snippet in your app.
 *	If you need more information about configurations or implementing the sample code, visit the AWS docs:
 *	https://aws.amazon.com/developer/language/net/getting-started
 */

using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Sodium;

namespace VirtualFinland.KeyRotator.Services.Github;

public class GitHubService
{
    HttpClient? _httpClient;

    ILambdaLogger _logger;
    Settings _settings;


    public GitHubService(Settings settings, ILambdaContext context)
    {
        _logger = context.Logger;
        _settings = settings;
    }


    public async Task CreateOrUpdateEnvironmentSecret(string organizationName, string repositoryName, string environment, string secretName, string secretValue)
    {
        var githubClient = await getGithubAPIClient();
        var repositoryId = await GetRepositoryId(organizationName, repositoryName);

        var publicKeyPackage = await GetPublicKeyPackage(repositoryId, environment);
        var secret = GetSecretForCreate(secretValue, publicKeyPackage);

        await PutCreateOrUpdateEnvironmentSecret(repositoryId, environment, secretName, secret);
    }


    //
    // @see: https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#create-or-update-a-repository-secret
    // @see: https://github.com/octokit/octokit.net/blob/a3299ac4b45bed5e12be61376748c1533b4627cd/Octokit.Tests.Integration/Clients/RespositorySecretsClientTests.cs#L111
    //
    UpsertRepositorySecretPackage GetSecretForCreate(string secretValue, PublicKeyPackage publicKeyPackage)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secretValue);
        var publicKey = Convert.FromBase64String(publicKeyPackage.key ?? throw new ArgumentNullException("Public key is null"));
        var sealedPublicKeyBox = SealedPublicKeyBox.Create(secretBytes, publicKey);

        return new UpsertRepositorySecretPackage(Convert.ToBase64String(sealedPublicKeyBox), publicKeyPackage.key_id);
    }

    async Task<HttpClient> getGithubAPIClient()
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

    async Task<string> GetGithubAccessToken()
    {
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

    //
    // https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#create-or-update-an-environment-secret
    //
    async Task PutCreateOrUpdateEnvironmentSecret(int repositoryId, string environment, string secretName, UpsertRepositorySecretPackage secret)
    {
        var githubClient = await getGithubAPIClient();

        var uri = $"/repositories/{repositoryId}/environments/{environment}/secrets/{secretName}";
        var textContent = JsonSerializer.Serialize(secret);
        var content = new StringContent(textContent, Encoding.UTF8, "application/json");

        var response = await githubClient.PutAsync(uri, content);
        if (!response.IsSuccessStatusCode)
        {
            var resposneContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation($"URI: {uri}");
            _logger.LogInformation($"Request body: {textContent}");
            _logger.LogInformation($"Response: {resposneContent}");
            _logger.LogInformation(response.ToString());

            throw new ArgumentException($"Failed to create secret for {secretName} in environment {environment}");
        }
    }

    //
    // @see: https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#get-a-repository
    // 
    async Task<int> GetRepositoryId(string organizationName, string repositoryName)
    {
        var githubClient = await getGithubAPIClient();
        var response = await githubClient.GetAsync($"/repos/{organizationName}/{repositoryName}");
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new ArgumentException($"Failed to fetch repository id :: {responseBody}");
        }

        var responseAsObject = JsonSerializer.Deserialize<GitHubResourcePackage>(responseBody);
        return responseAsObject?.id ?? throw new ArgumentNullException($"Failed to deserialize repository response: {responseBody}");
    }

    //
    // @see: https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#get-an-environment-public-key
    // 
    async Task<PublicKeyPackage> GetPublicKeyPackage(int repositoryId, string environment)
    {
        var githubClient = await getGithubAPIClient();
        var response = await githubClient.GetAsync($"/repositories/{repositoryId}/environments/{environment}/secrets/public-key");
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new ArgumentException($"Failed to fetch public key :: {responseBody}");
        }

        var publicKeyPackage = JsonSerializer.Deserialize<PublicKeyPackage>(responseBody);
        if (publicKeyPackage == null || string.IsNullOrEmpty(publicKeyPackage.key_id) || string.IsNullOrEmpty(publicKeyPackage.key))
        {
            throw new ArgumentNullException($"Failed to deserialize public key response: {responseBody}");
        }

        return publicKeyPackage;
    }
}

record GitHubResourcePackage(int? id);
record PublicKeyPackage(string? key_id, string? key);
record UpsertRepositorySecretPackage(string? encrypted_value, string? key_id);
