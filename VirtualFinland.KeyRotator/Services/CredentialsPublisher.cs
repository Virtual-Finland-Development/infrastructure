using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services.GitHub;

namespace VirtualFinland.KeyRotator.Services;

class CredentialsPublisher
{
    private ILambdaLogger _logger;
    private GitHubSecrets _gitHubSecrets;
    private GitHubRepositories _gitHubRepositories;
    private readonly string _githubOrganizationName;
    private readonly string _environment;

    public CredentialsPublisher(GitHubSecrets gitHubSecrets, GitHubRepositories gitHubRepositories, Settings settings, ILambdaLogger logger)
    {
        _gitHubSecrets = gitHubSecrets;
        _gitHubRepositories = gitHubRepositories;
        _logger = logger;
        _githubOrganizationName = settings.GitHubOrganizationName;
        _environment = settings.Environment;
    }

    public async Task PublishAccessKey(AccessKey accessKey)
    {
        var repositories = await _gitHubRepositories.GetTargetRepositories();
        _logger.LogInformation($"Publishing key {accessKey.AccessKeyId} to {repositories.Count} target repositories");

        foreach (var repository in repositories)
        {
            _logger.LogInformation($"Publishing to project {repository.Name} ..");

            await _gitHubSecrets.CreateOrUpdateEnvironmentSecret(
                _githubOrganizationName,
                repository.Id,
                _environment,
                "AWS_ACCESS_KEY_ID",
                accessKey.AccessKeyId
            );

            await _gitHubSecrets.CreateOrUpdateEnvironmentSecret(
                _githubOrganizationName,
                repository.Id,
                _environment,
                "AWS_SECRET_ACCESS_KEY",
                accessKey.SecretAccessKey
            );
        }

        _logger.LogInformation($"Key {accessKey.AccessKeyId} fully published");
    }
}
