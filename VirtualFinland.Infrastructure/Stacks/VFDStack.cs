using System.Collections.Generic;
using Pulumi;
using VirtualFinland.Infrastructure.Common;
using VirtualFinland.Infrastructure.Stacks.Features;

namespace VirtualFinland.Infrastructure.Stacks;

//
// VPC for protected network resources like users-api database
//
public class VFDStack : Stack
{
    public VFDStack()
    {
        var config = new Config();

        bool isProductionEnvironment = Pulumi.Deployment.Instance.StackName == Environments.Prod.ToString().ToLowerInvariant();
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

        var sharedResourceTags = new Dictionary<string, string>(tags) { };
        sharedResourceTags["vfd:stack"] = "shared";

        // Setup key deployer role
        var deployer = new Deployer();
        var deployerRole = deployer.InitializeGitHubOIDCProvider(environment, tags, sharedResourceTags);
        this.DeployerIAMRole = deployerRole.Arn;
    }

    [Output] public Output<string> DeployerIAMRole { get; set; }
}