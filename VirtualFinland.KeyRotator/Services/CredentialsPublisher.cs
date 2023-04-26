using System.Text;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;
using Octokit;
using Sodium;

namespace VirtualFinland.KeyRotator.Services;

class CredentialsPublisher
{
    ILambdaLogger _logger;
    public CredentialsPublisher(ILambdaContext context)
    {
        _logger = context.Logger;
    }

    public async void PublishAccessKey(AccessKey accessKey)
    {
        var projects = GetProjects();
        var githubClient = GetGithubClient();
        var organizationName = "Virtual-Finland-Development";


        foreach (var project in projects)
        {
            _logger.LogLine($"Publishing key {accessKey.AccessKeyId} to project {project.Name}");
            var publicKey = await githubClient.Repository.Actions.Secrets.GetPublicKey(organizationName, project.Name);

            var accessKeyId = GetSecretForCreate(accessKey.AccessKeyId, publicKey);
            await githubClient.Repository.Actions.Secrets.CreateOrUpdate(
                organizationName,
                project.Name,
                "AWS_ACCESS_KEY_ID",
                accessKeyId
                );

            var accessKeySecret = GetSecretForCreate(accessKey.SecretAccessKey, publicKey);
            await githubClient.Repository.Actions.Secrets.CreateOrUpdate(
                organizationName,
                project.Name,
                "AWS_ACCESS_KEY_SECRET",
                accessKeySecret
                );

        }
    }

    List<Project> GetProjects()
    {
        return new List<Project>
        {
            new Project { Name = "virtual-finland" },
            new Project { Name = "testbed-api" }, 
            // ...
        };
    }

    GitHubClient GetGithubClient()
    {
        var githubClient = new GitHubClient(new ProductHeaderValue("vfd-key-rotator"));
        var tokenAuth = new Credentials("token");
        githubClient.Credentials = tokenAuth;
        return githubClient;
    }

    //
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
}

public record Project
{
    public string Name { get; init; } = "";
}