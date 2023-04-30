using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Sodium;

namespace VirtualFinland.KeyRotator.Services.GitHub;

public class GithubSecrets : GitHubApi
{
    public GithubSecrets(Settings settings, ILambdaLogger logger) : base(settings, logger)
    {
    }

    // <summary>
    // Create or update an environment secret for a github repository
    // </summary>
    public async Task CreateOrUpdateEnvironmentSecret(string organizationName, int repositoryId, string environment, string secretName, string secretValue)
    {
        var publicKeyPackage = await GetPublicKeyPackage(repositoryId, environment);
        var secretPackage = MakeSecretPackage(secretValue, publicKeyPackage);

        await PutCreateOrUpdateEnvironmentSecret(repositoryId, environment, secretName, secretPackage);
    }

    // <summary>
    // @see: https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#get-an-environment-public-key
    // </summary>
    async Task<PublicKeyPackage> GetPublicKeyPackage(int repositoryId, string environment)
    {
        var githubClient = await GetGithubAPIClient();
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

    // <summary>
    // @see: https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#create-or-update-a-repository-secret
    // @see: https://github.com/octokit/octokit.net/blob/a3299ac4b45bed5e12be61376748c1533b4627cd/Octokit.Tests.Integration/Clients/RespositorySecretsClientTests.cs#L111
    // </summary>
    UpsertRepositorySecretPackage MakeSecretPackage(string secretValue, PublicKeyPackage publicKeyPackage)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secretValue);
        var publicKey = Convert.FromBase64String(publicKeyPackage.key ?? throw new ArgumentNullException("Public key is null"));
        var sealedPublicKeyBox = SealedPublicKeyBox.Create(secretBytes, publicKey);

        return new UpsertRepositorySecretPackage(Convert.ToBase64String(sealedPublicKeyBox), publicKeyPackage.key_id);
    }

    // <summary>
    // https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#create-or-update-an-environment-secret
    // </summary>
    async Task PutCreateOrUpdateEnvironmentSecret(int repositoryId, string environment, string secretName, UpsertRepositorySecretPackage secretPackage)
    {
        var githubClient = await GetGithubAPIClient();

        var uri = $"/repositories/{repositoryId}/environments/{environment}/secrets/{secretName}";
        var textContent = JsonSerializer.Serialize(secretPackage);
        var content = new StringContent(textContent, Encoding.UTF8, "application/json");

        var response = await githubClient.PutAsync(uri, content);
        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation($"URI: {uri}");
            _logger.LogInformation($"Request body: {textContent}");
            _logger.LogInformation($"Response: {responseContent}");
            _logger.LogInformation(response.ToString());

            throw new ArgumentException($"Failed to create secret for {secretName} in environment {environment}");
        }
    }
}

record GitHubResourcePackage(int? id, string? name);
record PublicKeyPackage(string? key_id, string? key);
record UpsertRepositorySecretPackage(string? encrypted_value, string? key_id);
