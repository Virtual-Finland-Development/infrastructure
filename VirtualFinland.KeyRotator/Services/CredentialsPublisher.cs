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

    public async void PublishAccessKey(AccessKey accessKey, string environment)
    {
        var projects = GetProjects();
        var organizationName = "Virtual-Finland-Development";

        foreach (var project in projects)
        {
            _logger.LogLine($"Publishing key {accessKey.AccessKeyId} to project {project.Name}");

            await _github.CreateOrUpdateEnvironmentSecret(
                organizationName,
                project.Name,
                environment,
                "AWS_ACCESS_KEY_ID",
                accessKey.AccessKeyId
            );

            await _github.CreateOrUpdateEnvironmentSecret(
                organizationName,
                project.Name,
                environment,
                "AWS_ACCESS_KEY_SECRET",
                accessKey.SecretAccessKey
            );
        }
    }

    List<Project> GetProjects()
    {
        return new List<Project>
        {
            new Project { Name = "status-info-api" },
        };
    }
}

public record Project
{
    public string Name { get; init; } = "";
}