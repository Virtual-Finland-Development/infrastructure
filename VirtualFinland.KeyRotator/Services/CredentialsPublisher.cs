using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services.GitHub;

namespace VirtualFinland.KeyRotator.Services;

class CredentialsPublisher
{
    Settings _settings;
    ILambdaLogger _logger;
    GithubSecrets _githubSecrets;
    GitHubRepositories _githubRepositories;

    public CredentialsPublisher(Settings settings, ILambdaContext context)
    {
        _settings = settings;
        _logger = context.Logger;
        _githubSecrets = new GithubSecrets(settings, context);
        _githubRepositories = new GitHubRepositories(settings, context);
    }

    public async Task PublishAccessKey(AccessKey accessKey)
    {
        var repositories = await _githubRepositories.GetTargetRepositories();
        var environment = _settings.Environment;
        var organizationName = _settings.GitHubOrganizationName;

        // Debug limitter
        // repositories = repositories.Take(3).ToList();

        foreach (var repository in repositories)
        {
            _logger.LogInformation($"Publishing key {accessKey.AccessKeyId} to project {repository.Name} ..");

            await _githubSecrets.CreateOrUpdateEnvironmentSecret(
                organizationName,
                repository.Id,
                environment,
                "AWS_ACCESS_KEY_ID",
                accessKey.AccessKeyId
            );

            await _githubSecrets.CreateOrUpdateEnvironmentSecret(
                organizationName,
                repository.Id,
                environment,
                "AWS_SECRET_ACCESS_KEY",
                accessKey.SecretAccessKey
            );
        }

        _logger.LogInformation("Key published");
    }
}
