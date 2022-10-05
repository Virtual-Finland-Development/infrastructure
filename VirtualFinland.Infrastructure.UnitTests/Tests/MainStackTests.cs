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
    }
}
