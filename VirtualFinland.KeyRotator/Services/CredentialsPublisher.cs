using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services.Github;

namespace VirtualFinland.KeyRotator.Services;

class CredentialsPublisher
{
    ILambdaLogger _logger;
    GitHubService _github;

    public CredentialsPublisher(ILambdaContext context)
    {
        _logger = context.Logger;
        _github = new GitHubService();
    }

    public async void PublishAccessKey(AccessKey accessKey)
    {
        var projects = GetProjects();
        var githubClient = await _github.GetGithubClient();
        var organizationName = "Virtual-Finland-Development";

        foreach (var project in projects)
        {
            _logger.LogLine($"Publishing key {accessKey.AccessKeyId} to project {project.Name}");
            var publicKey = await githubClient.Repository.Actions.Secrets.GetPublicKey(organizationName, project.Name);

            var accessKeyId = _github.GetSecretForCreate(accessKey.AccessKeyId, publicKey);
            await githubClient.Repository.Actions.Secrets.CreateOrUpdate(
                organizationName,
                project.Name,
                "AWS_ACCESS_KEY_ID",
                accessKeyId
            );

            var accessKeySecret = _github.GetSecretForCreate(accessKey.SecretAccessKey, publicKey);
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
}

public record Project
{
    public string Name { get; init; } = "";
}