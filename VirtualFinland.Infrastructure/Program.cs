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
        //await ses.SetupDomainVerification(setup); // TODO: Might not be needed
    }

    return new Dictionary<string, object?>
    {
        { "DeployerIAMRole", deployerRole.Arn },
        { "SharedAccessKey", Output.CreateSecret(sharedAccessKey.Result) },
        { "SesDomainIdentityVerificationToken", ses.DomainIdentity?.VerificationToken },
        { "DkimTokens", ses.DomainDkim?.DkimTokens },
        { "DnsRecordsCreated", ses.DnsReordsCreated },
    };
});