/*
 *	Use this code snippet in your app.
 *	If you need more information about configurations or implementing the sample code, visit the AWS docs:
 *	https://aws.amazon.com/developer/language/net/getting-started
 */

using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Octokit;
using Sodium;

namespace VirtualFinland.KeyRotator.Services.Github;

public class GitHubService
{
    HttpClient? _httpClient;


    public async Task CreateOrUpdateEnvironmentSecret(string organizationName, string repositoryName, string environment, string secretName, string secretValue)
    {
        // https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#get-a-repository
        var repositoryId = (await getGithubResponsePayloadItem($"/repos/{organizationName}/{repositoryName}", new List<string> { "id" })).FirstOrDefault() ?? "";
        // https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#get-an-environment-public-key
        var publicKeyPackage = await getGithubResponsePayloadItem($"/repositories/{repositoryId}/environments/{environment}/secrets/public-key", new List<string> { "key_id", "key", });
        var secret = GetSecretForCreate(secretValue, new SecretsPublicKey(publicKeyPackage[0], publicKeyPackage[1]));
        // https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#create-or-update-an-environment-secret
        var githubClient = await getGithubAPIClient();
        await githubClient.PostAsync($"/repositories/{repositoryId}/environments/{environment}/secrets/{secretName}", new StringContent(JsonSerializer.Serialize(secret), Encoding.UTF8, "application/json"));
    }


    //
    // @see: https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#create-or-update-a-repository-secret
    // @see: https://github.com/octokit/octokit.net/blob/a3299ac4b45bed5e12be61376748c1533b4627cd/Octokit.Tests.Integration/Clients/RespositorySecretsClientTests.cs#L111
    //
    UpsertRepositorySecret GetSecretForCreate(string secretValue, SecretsPublicKey key)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secretValue);
        var publicKey = Convert.FromBase64String(key.Key);
        var sealedPublicKeyBox = SealedPublicKeyBox.Create(secretBytes, publicKey);

        var upsertValue = new UpsertRepositorySecret
        {
            EncryptedValue = Convert.ToBase64String(sealedPublicKeyBox),
            KeyId = key.KeyId

        };

        return upsertValue;
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

    async Task<List<string>> getGithubResponsePayloadItem(string url, List<string> keys)
    {
        var githubClient = await getGithubAPIClient();
        var response = await githubClient.GetAsync(url);
        var responsePayload = await response.Content.ReadAsStringAsync();
        var responsePayloadJson = JsonSerializer.Deserialize<JsonElement>(responsePayload);
        var result = new List<string>();
        foreach (var key in keys)
        {
            result.Add(responsePayloadJson.GetProperty(key).GetString() ?? "");
        }
        return result;
    }

    async Task<string> GetGithubAccessToken()
    {
        string secretName = "Github/rotatorbot";
        string region = "eu-north-1";

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

