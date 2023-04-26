using System.Collections.Immutable;
using Pulumi;
using Pulumi.Awsx.Ec2;
using Pulumi.Awsx.Ec2.Inputs;
using VirtualFinland.Infrastructure.Common;

namespace VirtualFinland.Infrastructure.Stacks;

//
// VPC for protected network resources like users-api database
//
public class VPCStack : Stack
{
    public VPCStack()
    {
        var config = new Config();

        bool isProductionEnvironment = Pulumi.Deployment.Instance.StackName == Environments.Prod.ToString().ToLowerInvariant();
        var environment = Pulumi.Deployment.Instance.StackName;
        var projectName = Pulumi.Deployment.Instance.ProjectName;

        InputMap<string> tags = new InputMap<string>()
        {
            {
                "Environment", environment
            },
            {
                "Project", projectName
            }
        };

        var vpc = new Vpc($"vf-vpc-{environment}", new VpcArgs()
        {
            Tags = tags,
            EnableDnsHostnames = true,
            NatGateways = new NatGatewayConfigurationArgs
            {
                Strategy = isProductionEnvironment ? NatGatewayStrategy.OnePerAz : NatGatewayStrategy.Single
            }
        });

        this.VpcId = vpc.VpcId;
        this.VpcName = Output.Create(vpc.GetResourceName());
        this.PublicSubnetIds = vpc.PublicSubnetIds;
        this.PrivateSubnetIds = vpc.PrivateSubnetIds;
    }
    [Output] public Output<string> VpcId { get; set; }

    [Output] public Output<string> VpcName { get; set; }

    [Output] public Output<ImmutableArray<string>> PrivateSubnetIds { get; set; }
    [Output] public Output<ImmutableArray<string>> PublicSubnetIds { get; set; }
}