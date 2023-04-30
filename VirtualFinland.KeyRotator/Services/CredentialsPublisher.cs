using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services.GitHub;

namespace VirtualFinland.KeyRotator.Services;

class CredentialsPublisher
{
    Settings _settings;
    ILambdaLogger _logger;
    GitHubSecrets _gitHubSecrets;
    GitHubRepositories _gitHubRepositories;

    public CredentialsPublisher(Settings settings, ILambdaLogger logger)
    {
        _settings = settings;
        _logger = logger;
        _gitHubSecrets = new GitHubSecrets(settings, logger);
        _gitHubRepositories = new GitHubRepositories(settings, logger);
    }

    public async Task PublishAccessKey(AccessKey accessKey)
    {
        var repositories = await _gitHubRepositories.GetTargetRepositories();
        var environment = _settings.Environment;
        var organizationName = _settings.GitHubOrganizationName;

        foreach (var repository in repositories)
        {
            _logger.LogInformation($"Publishing key {accessKey.AccessKeyId} to project {repository.Name} ..");

            await _gitHubSecrets.CreateOrUpdateEnvironmentSecret(
                organizationName,
                repository.Id,
                environment,
                "AWS_ACCESS_KEY_ID",
                accessKey.AccessKeyId
            );

            await _gitHubSecrets.CreateOrUpdateEnvironmentSecret(
                organizationName,
                repository.Id,
                environment,
                "AWS_SECRET_ACCESS_KEY",
                accessKey.SecretAccessKey
            );
        }

        _logger.LogInformation($"Key {accessKey.AccessKeyId} fully published");
    }
}
