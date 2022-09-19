using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Pulumi;
using Pulumi.Aws.Rds;
using Pulumi.Awsx.Ec2;
using Pulumi.Testing;
using VirtualFinland.Infrastructure.Stacks;
using VirtualFinland.UnitTests.Utility;

namespace VirtualFinland.Infrastructure.Testing.Tests
{

    [TestFixture]
    public class MainStackTests
    {

        [Test]
        public async Task ShouldHaveResourcesAsync()
        {
            var resources = await TestUtility.RunAsync<MainStack>();

            resources.Should().NotBeNull();
        }
        
        [Test]
        public async Task ShouldHaveSingleVpcAsync()
        {
            var resources = await TestUtility.RunAsync<MainStack>();
            var vpcs = resources.OfType<Vpc>().ToList();

            vpcs.Count.Should().Be(1, "should be a single VPC");
        }

        [Test]
        public async Task ShouldHaveAuroraServerlessClusterAsync()
        {
            var resources = await TestUtility.RunAsync<MainStack>();
            var dbClusters = resources.OfType<Cluster>().ToList();

            dbClusters.Count().Should().BePositive("should have at least one RDS Cluster");
            dbClusters.Should().ContainSingle(o => o.Engine.GetValueAsync().Result == "aurora-postgresql");
        }

        [Test]
        public async Task ShouldHaveAuroraServerlessInstanceAsync()
        {
            var resources = await TestUtility.RunAsync<MainStack>();
            var dbClustersInstances = resources.OfType<ClusterInstance>().ToList();

            dbClustersInstances.Should().NotBeNull();
            dbClustersInstances.Should().ContainSingle(o => o.InstanceClass.GetValueAsync().Result == "db.serverless");
        }

        [Test]
        public async Task ShouldNotSkipFinalSnapshotForAuroraCluster()
        {
            var resources = await TestUtility.RunAsync<MainStack>();
            var dbClusters = resources.OfType<Cluster>().ToList();

            dbClusters.Should().ContainSingle(o => o.SkipFinalSnapshot.GetValueAsync().Result == false, "should not skip final snapshot for production safety measures");
        }
    }
}
