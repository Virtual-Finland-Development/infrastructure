using System.Collections.Generic;
using Pulumi;
using Pulumi.Random;
using VirtualFinland.Infrastructure.Common;
using VirtualFinland.Infrastructure.Features;

return await Deployment.RunAsync(async () =>
{

    var setup = new StackSetup();

    // Setup key deployer role
    var deployerRole = await Deployer.InitializeGitHubOIDCProvider(setup);

    // Setup shared resources
    var sharedAccessKey = new RandomPassword(setup.NameResource("sharedAccessKey"), new RandomPasswordArgs
    {
        Length = 20,
        Special = false
    });

    // Setup SES
    var ses = new SimpleEmailService();
    var sesDomainIdentity = ses.SetupSes(setup);
    await ses.SetupSesDomainRecords(setup);

    return new Dictionary<string, object?>
    {
        { "DeployerIAMRole", deployerRole.Arn },
        { "SharedAccessKey", Output.CreateSecret(sharedAccessKey.Result) },
        { "SesDomainIdentityId", sesDomainIdentity?.Id }
    };
});