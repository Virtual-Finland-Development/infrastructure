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
        bool isProductionEnvironment = config.Require("environment") == Environments.Prod.ToString().ToLowerInvariant();
         InputMap<string> tags = new InputMap<string>()
         {
            { "Environment", config.Require("environment") },
            { "Project", config.Require("name") }
         };

        var vpc = new Vpc($"vf-vpc-{config.Require("environment")}", new VpcArgs() {
            Tags = tags
        });

        var dbSubNetGroup = new SubnetGroup("dbsubnets", new()
        {
            SubnetIds = vpc.PublicSubnetIds,
        });

        var password = new RandomPassword("password", new()
        {
            Length = 16,
            Special = true,
            OverrideSpecial = "_%@",
        });

        var rdsCluster = new Cluster($"vf-rds-cluster-{config.Require("environment")}", new()
        {
            ClusterIdentifier = $"vf-{config.Require("environment")}",

            EnableHttpEndpoint = true,
            Engine = "aurora-postgresql",
            EngineMode = "provisioned",
            EngineVersion = "13.7",
            DatabaseName = config.Require("dbName"),
            MasterUsername = config.Require("dbAdmin"),
            MasterPassword = password.Result,
            SkipFinalSnapshot = !isProductionEnvironment, // For production set to FALSE to avoid accidental deletion of the cluster, data safety measure and is the default for AWS.
            DbSubnetGroupName = dbSubNetGroup.Name,
            Serverlessv2ScalingConfiguration = new ClusterServerlessv2ScalingConfigurationArgs
            {
                MaxCapacity = 1,
                MinCapacity = 0.5,
            },
            Tags = tags
        });

        var rdsClusterInstance = new ClusterInstance($"vf-rds-rdsClusterInstance-{config.Require("environment")}", new()
        {
            ClusterIdentifier = rdsCluster.Id,
            InstanceClass = "db.serverless",
            Engine = rdsCluster.Engine,
            EngineVersion = rdsCluster.EngineVersion,
            Tags = tags
        });

        this.VpcId = vpc.VpcId;
        this.PublicSubnetIds = vpc.PublicSubnetIds;
        this.PrivateSubnetIds = vpc.PrivateSubnetIds;
        this.RDSDBInstancePassword = rdsCluster.MasterPassword.Apply(Output.CreateSecret);

        this.RDSClusterEndpoint = rdsCluster.Endpoint;
        this.RDSClusterVPCSecurityGroups = rdsCluster.VpcSecurityGroupIds;
    }
    [Output] public Output<string> VpcId { get; set; }

    [Output] public Output<ImmutableArray<string>> PrivateSubnetIds { get; set; }
    [Output] public Output<ImmutableArray<string>> PublicSubnetIds { get; set; }

    [Output] public Output<string> RDSClusterEndpoint { get; set; }
    [Output] public Output<string> RDSDBInstancePassword { get; set; }
    [Output] public Output<ImmutableArray<string>> RDSClusterVPCSecurityGroups { get; set; }
}