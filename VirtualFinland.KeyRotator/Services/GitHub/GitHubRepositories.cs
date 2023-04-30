using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;

namespace VirtualFinland.KeyRotator.Services.GitHub;

public class GitHubRepositories
{

    ILambdaLogger _logger;
    Settings _settings;
    GitHubApi _githubApi;


    public GitHubRepositories(Settings settings, ILambdaContext context)
    {
        _logger = context.Logger;
        _settings = settings;
        _githubApi = new GitHubApi(settings, context);
    }

    // <summary>
    // Get all projects that have the environment configured
    // </summary>
    public async Task<List<GitRepository>> GetTargetRepositories()
    {
        var targetRepositories = new List<GitRepository>();

        var githubClient = await _githubApi.getGithubAPIClient();
        var repositories = await GetOrganizationRepositories(_settings.GitHubOrganizationName);

        foreach (var repository in repositories)
        {
            var repositoryEnvironment = await GetRepositoryEnvironment(_settings.GitHubOrganizationName, repository.Name, _settings.Environment);
            if (repositoryEnvironment != null)
            {
                targetRepositories.Add(repository);
            }
        }

        _logger.LogInformation($"Found {targetRepositories.Count} target repositories in organization {_settings.GitHubOrganizationName}");

        return targetRepositories;
    }

    // <summary>
    // https://docs.github.com/en/rest/actions/secrets?apiVersion=2022-11-28#create-or-update-an-environment-secret
    // </summary>
    async Task<List<GitRepository>> GetOrganizationRepositories(string organizationName)
    {
        var githubClient = await _githubApi.getGithubAPIClient();

        var gitRepositories = new List<GitRepository>();
        var perPage = 100;
        var page = 1;
        var baseUri = $"/orgs/{organizationName}/repos";

        // Fetch all repositories
        while (true)
        {
            var uri = $"{baseUri}?per_page={perPage}&page={page}";
            var response = await githubClient.GetAsync(uri);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new ArgumentException($"Failed to fetch repositories :: {responseBody}");
            }

            var repositories = JsonSerializer.Deserialize<GitRepository[]>(responseBody);
            if (repositories == null || repositories.Length == 0)
            {
                break;
            }
            gitRepositories.AddRange(repositories);
            page++;
        }


        return gitRepositories;
    }

    async Task<GitHubResourcePackage?> GetRepositoryEnvironment(string organizationName, string repositoryName, string environment)
    {
        var githubClient = await _githubApi.getGithubAPIClient();

        var uri = $"/repos/{organizationName}/{repositoryName}/environments/{environment}";

        var response = await githubClient.GetAsync(uri);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            return JsonSerializer.Deserialize<GitHubResourcePackage>(responseBody);
        }
        return null;
    }
}


public record GitRepository
{
    [JsonPropertyName("id")]
    public int Id { get; init; } = 0;
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}