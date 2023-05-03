using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Core;
using VirtualFinland.KeyRotator.Services.GitHub;

namespace VirtualFinland.KeyRotator.Services;

class CredentialsPublisher
{
    private ILambdaLogger _logger;
    private GitHubApi _gitHubApi;
    private readonly string _githubOrganizationName;
    private readonly string _environment;

    public CredentialsPublisher(GitHubApi gitHubApi, Settings settings, ILambdaLogger logger)
    {
        _gitHubApi = gitHubApi;
        _logger = logger;
        _githubOrganizationName = settings.GitHubOrganizationName;
        _environment = settings.Environment;
    }

    public async Task PublishAccessKey(AccessKey accessKey)
    {
        var repositories = await _gitHubApi.Repositories.GetTargetRepositories();
        _logger.LogInformation($"Publishing key {accessKey.AccessKeyId} to {repositories.Count} target repositories");

        foreach (var repository in repositories)
        {
            _logger.LogInformation($"Publishing to project {repository.Name} ..");

            await _gitHubApi.Secrets.CreateOrUpdateEnvironmentSecret(
                _githubOrganizationName,
                repository.Id,
                _environment,
                "AWS_ACCESS_KEY_ID",
                accessKey.AccessKeyId
            );

            await _gitHubApi.Secrets.CreateOrUpdateEnvironmentSecret(
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
