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
    public async Task<GitHubClient> GetGithubClient()
    {
        var githubClient = new GitHubClient(new ProductHeaderValue("Virtual-Finland-Development"));
        var token = await GetGithubAccessToken();
        var tokenAuth = new Credentials(token);
        githubClient.Credentials = tokenAuth;
        return githubClient;
    }

    //
    // @see: https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#create-or-update-a-repository-secret
    // @see: https://github.com/octokit/octokit.net/blob/a3299ac4b45bed5e12be61376748c1533b4627cd/Octokit.Tests.Integration/Clients/RespositorySecretsClientTests.cs#L111
    //
    public UpsertRepositorySecret GetSecretForCreate(string secretValue, SecretsPublicKey key)
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

