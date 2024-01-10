using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Aws.Route53;
using Pulumi.Aws.Ses;
using Pulumi.Aws.Ses.Inputs;
using VirtualFinland.Infrastructure.Common;

namespace VirtualFinland.Infrastructure.Features;

public class SimpleEmailService
{
    private readonly string _domainName;
    private readonly string _mailFromSubDomain;
    private readonly List<StackOwnsDomain> _stackOwnsDomains = new();
    private readonly string? _domainOwnedByStack;

    public DomainIdentity? DomainIdentity { get; private set; }
    public DomainDkim? DomainDkim { get; private set; }
    public bool DnsReordsCreated { get; private set; } = false;


    public SimpleEmailService()
    {
        var config = new Config("ses");
        _domainName = config.Get("domain-name") ?? "";
        _mailFromSubDomain = config.Get("mail-from-sub-domain") ?? "ses";

        var stackOwnsDomains = config.GetObject<List<string>>("stack-owned-domains");
        if (stackOwnsDomains != null)
        {
            foreach (var stackOwnsDomain in stackOwnsDomains)
            {
                var stackOwnsDomainParts = stackOwnsDomain?.Split(':');
                if (stackOwnsDomainParts != null && stackOwnsDomainParts.Length == 2)
                {
                    _stackOwnsDomains.Add(new StackOwnsDomain
                    {
                        StackName = stackOwnsDomainParts[0],
                        DomainName = stackOwnsDomainParts[1],
                    });
                }
            }
        }

        _domainOwnedByStack = config.Get("domain-owned-by-stack") ?? null;
    }

    public bool IsDeployable()
    {
        return !string.IsNullOrEmpty(_domainName);
    }

    public void SetupDomainIndentity(StackSetup setup)
    {
        DomainIdentity = new DomainIdentity(setup.NameResource("domain-identity"), new DomainIdentityArgs
        {
            Domain = _domainName,
        });

        _ = new MailFrom(setup.NameResource("mail-from-domain"), new MailFromArgs
        {
            Domain = _domainName,
            MailFromDomain = $"{_mailFromSubDomain}.{_domainName}",
        });

        // Create DKIM verifications
        DomainDkim = new DomainDkim(setup.NameResource("domain-dkim"), new DomainDkimArgs
        {
            Domain = _domainName,
        });

        _ = new ConfigurationSet(setup.NameResource("ses-configuration-set"), new ConfigurationSetArgs
        {
            Name = "af-ses-configuration-set",
            DeliveryOptions = new ConfigurationSetDeliveryOptionsArgs
            {
                TlsPolicy = "Require",
            },
        });
    }

    public async Task SetupDomainRecords(StackSetup setup)
    {
        foreach (var stackDomain in _stackOwnsDomains)
        {
            await SetupStackDomain(setup, stackDomain);
        }
    }

    public async Task SetupDomainVerification(StackSetup setup)
    {
        bool recordsReady;
        if (_domainOwnedByStack == setup.Environment)
        {
            recordsReady = DnsReordsCreated;
        }
        else
        {
            var themsStackRef = new StackReference($"{setup.Organization}/infrastructure/{_domainOwnedByStack}");
            var dnsRecordsCreated = await themsStackRef.GetValueAsync("DnsReordsCreated");
            recordsReady = dnsRecordsCreated != null && (bool)dnsRecordsCreated;
        }

        if (!recordsReady)
            return;

        _ = new DomainIdentityVerification(setup.NameResource("ses-domain-verification"), new DomainIdentityVerificationArgs
        {
            Domain = _domainName,
        });
    }

    private async Task SetupStackDomain(StackSetup setup, StackOwnsDomain stackDomain)
    {
        var mailFromDomain = $"{_mailFromSubDomain}.{stackDomain.DomainName}";

        // Stack reference from access-finland app where the domain is owned
        var afStack = new StackReference($"{setup.Organization}/access-finland/{stackDomain.StackName}");
        var zoneIdish = await afStack.GetValueAsync("zoneId");
        if (zoneIdish == null)
            return; // Skip for now, needs a second run after the zone is created in the af stack
        var zoneId = zoneIdish.ToString()!;

        // Stack reference from self where the domain identity is created
        var selfStack = new StackReference($"{setup.Organization}/infrastructure/{stackDomain.StackName}");
        var domainIdentityIdish = await selfStack.GetValueAsync("SesDomainIdentityId");
        if (domainIdentityIdish == null)
            return;
        var domainIdentityId = domainIdentityIdish.ToString()!;
        var domainIdentity = DomainIdentity.Get("domainIdentity", domainIdentityId);

        var dkimTokensRaw = await selfStack.RequireValueAsync("DkimTokens");
        var dkimTokens = (List<string>)dkimTokensRaw; // There should be a better way to do this
        if (dkimTokens == null)
            return;

        // Records for mail from domain
        _ = new Record(setup.NameEnvironmentResource("mail-from-record-verification-txt", stackDomain.StackName), new RecordArgs
        {
            Name = mailFromDomain,
            Records = { "\"v=spf1 include:amazonses.com ~all\"" },
            Ttl = 600,
            Type = "TXT",
            ZoneId = zoneId,
        });

        _ = new Record(setup.NameEnvironmentResource("mail-from-record-verification-mx", stackDomain.StackName), new RecordArgs
        {
            Name = mailFromDomain,
            Records = { "10 feedback-smtp.eu-north-1.amazonses.com" },
            Ttl = 600,
            Type = "MX",
            ZoneId = zoneId,
        });

        // Create DKIM records
        for (var i = 0; i < dkimTokens.Count; i++)
        {
            var dkimToken = dkimTokens[i];
            _ = new Record(setup.NameEnvironmentResource($"dkim-record-verification-{i}", stackDomain.StackName), new RecordArgs
            {
                Name = dkimToken,
                Records = { dkimToken },
                Ttl = 600,
                Type = "CNAME",
                ZoneId = zoneId,
            });
        }

        // Create SES verification record
        _ = new Record(setup.NameEnvironmentResource("ses-verification-record", stackDomain.StackName), new RecordArgs
        {
            Name = $"_amazonses.{stackDomain.DomainName}",
            Records = { domainIdentity.VerificationToken },
            Ttl = 600,
            Type = "TXT",
            ZoneId = zoneId,
        });

        // Create SPF record
        _ = new Record(setup.NameEnvironmentResource("ses-spf-record", stackDomain.StackName), new RecordArgs
        {
            Name = stackDomain.DomainName,
            Records = { "v=spf1 include:amazonses.com ~all" },
            Ttl = 60,
            Type = "TXT",
            ZoneId = zoneId,
        });

        DnsReordsCreated = true;
    }

    private record StackOwnsDomain
    {
        public string DomainName { get; init; } = string.Empty;
        public string StackName { get; init; } = string.Empty;
    }
}