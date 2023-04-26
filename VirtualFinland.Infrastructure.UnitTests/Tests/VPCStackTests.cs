using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Pulumi.Awsx.Ec2;
using VirtualFinland.Infrastructure.Stacks;
using VirtualFinland.UnitTests.Utility;

namespace VirtualFinland.Infrastructure.Testing.Tests
{

    [TestFixture]
    public class VFDStackTests
    {

        [Test]
        public async Task ShouldHaveResourcesAsync()
        {
            var resources = await TestUtility.RunAsync<VFDStack>();

            resources.Should().NotBeNull();
        }

        [Test]
        public async Task ShouldHaveSingleVpcAsync()
        {
            var resources = await TestUtility.RunAsync<VFDStack>();
            var vpcs = resources.OfType<Vpc>().ToList();

            vpcs.Count.Should().Be(1, "should be a single VPC");
        }
    }
}
