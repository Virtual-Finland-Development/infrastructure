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
    if (ses.IsDeployable())
    {
        ses.SetupDomainIndentity(setup);
        await ses.SetupDomainRecords(setup);
        await ses.SetupDomainVerification(setup);
    }

    return new Dictionary<string, object?>
    {
        { "DeployerIAMRole", deployerRole.Arn },
        { "SharedAccessKey", Output.CreateSecret(sharedAccessKey.Result) },
        { "SesDomainIdentityId", ses.DomainIdentity?.Id },
        { "DkimTokens", ses.DomainDkim?.DkimTokens },
        { "DnsRecordsCreated", ses.DnsReordsCreated },
    };
});