using System.Collections.Generic;
using Pulumi;
using Pulumi.Random;
using VirtualFinland.Infrastructure.Stacks.Features;

namespace VirtualFinland.Infrastructure.Stacks;

//
// VPC for protected network resources like users-api database
//
public class VFDStack : Stack
{
    public VFDStack()
    {
        var environment = Pulumi.Deployment.Instance.StackName;
        var projectName = Pulumi.Deployment.Instance.ProjectName;

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
        var deployerRole = deployer.InitializeGitHubOIDCProvider(environment, tags, sharedResourceTags);
        DeployerIAMRole = deployerRole.Arn;

        // Setup shared resources
        var sharedAccessKey = new RandomPassword($"{projectName}-sharedAccessKey-{environment}", new RandomPasswordArgs
        {
            Length = 20,
            Special = false
        });
        SharedAccessKey = Pulumi.Output.CreateSecret(sharedAccessKey.Result);
    }

    [Output] public Output<string> DeployerIAMRole { get; set; }
    [Output] public Output<string> SharedAccessKey { get; set; }

}