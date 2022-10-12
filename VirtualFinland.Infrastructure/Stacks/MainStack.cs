using System.Collections.Immutable;
using Pulumi;
using Pulumi.Aws.Rds;
using Pulumi.Aws.Rds.Inputs;
using Pulumi.Awsx.Ec2;
using Pulumi.Random;
using VirtualFinland.Infrastructure.Common;

namespace VirtualFinland.Infrastructure.Stacks;

public class MainStack : Stack
{
    public MainStack()
    {

        var config = new Config();

        InputMap<string> tags = new InputMap<string>()
         {
            { "Environment", config.Require("environment") },
            { "Project", config.Require("name") }
         };

        var vpc = new Vpc($"vf-vpc-{config.Require("environment")}", new VpcArgs()
        {
            Tags = tags,
            EnableDnsHostnames = true
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