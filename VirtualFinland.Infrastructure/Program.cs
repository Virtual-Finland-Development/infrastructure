using System.Collections.Generic;
using Pulumi;
using Pulumi.Random;
using VirtualFinland.Infrastructure.Stacks.Features;

return await Deployment.RunAsync(async () =>
{
    var environment = Deployment.Instance.StackName;
    var projectName = Deployment.Instance.ProjectName;

    var tags = new Dictionary<string, string>()
    {
        {
            "vfd:stack", environment
        },
        {
            "vfd:project", projectName
        }
    };

    var sharedResourceTags = new Dictionary<string, string>(tags)
    {
        ["vfd:stack"] = "shared"
    };

    // Setup key deployer role
    var deployer = new Deployer();
    var deployerRole = await deployer.InitializeGitHubOIDCProviderAsync(environment, tags, sharedResourceTags);

    // Setup shared resources
    var sharedAccessKey = new RandomPassword($"{projectName}-sharedAccessKey-{environment}", new RandomPasswordArgs
    {
        Length = 20,
        Special = false
    });

    return new Dictionary<string, object?>
    {
        { "DeployerIAMRole", deployerRole.Arn },
        { "SharedAccessKey", Output.CreateSecret(sharedAccessKey.Result) },
    };
});