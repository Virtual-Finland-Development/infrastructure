using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Aws.Route53;
using Pulumi.Aws.Ses;
using Pulumi.Aws.Ses.Inputs;
using VirtualFinland.Infrastructure.Common;

namespace VirtualFinland.Infrastructure.Features;

public class SimpleEmailService
{
    private readonly string? _domainZoneId;
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

        var stackOwnsDomains = config.GetObject<List<string>>("this-stack-owns-domains");
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
        _domainZoneId = config.Get("domain-zone-id") ?? null;
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
        // Stack reference from access-finland app where the domain is owned
        string? zoneId = null;
        if (_domainZoneId != null)
        {
            zoneId = _domainZoneId;
        }
        else
        {
            var envOverride = setup.Environment == "dev" ? "mvp-dev" : setup.Environment;
            var afStack = new StackReference($"{setup.Organization}/access-finland/{envOverride}");
            if (afStack != null)
            {
                var zoneIdish = await afStack.GetValueAsync("domainZoneId");
                if (zoneIdish != null)
                {
                    zoneId = zoneIdish.ToString()!;
                }
            }
        }

        if (string.IsNullOrEmpty(zoneId))
        {
            Console.WriteLine($"ZoneId not found for stack {setup.Environment}");
            return; // Skip for now, needs a second run after the zone is created in the af stack
        }

        foreach (var stackDomain in _stackOwnsDomains)
        {
            await SetupStackDomain(setup, stackDomain, zoneId);
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

    private async Task SetupStackDomain(StackSetup setup, StackOwnsDomain stackDomain, string zoneId)
    {
        var mailFromDomain = $"{_mailFromSubDomain}.{stackDomain.DomainName}";

        // Stack reference from self where the domain identity is created
        var selfStack = new StackReference($"{setup.Organization}/infrastructure/{stackDomain.StackName}");
        var domainVerificationToken = await selfStack.GetValueAsync("SesDomainIdentityVerificationToken");
        if (domainVerificationToken == null)
        {
            Console.WriteLine($"SesDomainIdentityVerificationToken not found for stack {stackDomain.StackName}");
            return;
        }

        var dkimTokensRaw = await selfStack.RequireValueAsync("DkimTokens");
        if (dkimTokensRaw == null)
        {
            Console.WriteLine($"DkimTokens not found for stack {stackDomain.StackName}");
            return;
        }
        var dkimTokens = (ImmutableArray<object>)dkimTokensRaw;

        // Records for mail from domain
        _ = new Record(setup.NameEnvironmentResource("mail-from-record-verification-txt", stackDomain.StackName), new RecordArgs
        {
            Name = mailFromDomain,
            Records = { "v=spf1 include:amazonses.com ~all" },
            Ttl = 600,
            Type = "TXT",
            ZoneId = zoneId,
        });

        _ = new Record(setup.NameEnvironmentResource("mail-from-record-verification-mx", stackDomain.StackName), new RecordArgs
        {
            Name = mailFromDomain,
            Records = { $"10 feedback-smtp.{setup.Region}.amazonses.com" },
            Ttl = 600,
            Type = "MX",
            ZoneId = zoneId,
        });

        // Create DKIM records
        for (var i = 0; i < dkimTokens.Length; i++)
        {
            var dkimToken = dkimTokens[i] as string;
            _ = new Record(setup.NameEnvironmentResource($"dkim-record-verification-{i}", stackDomain.StackName), new RecordArgs
            {
                Name = $"{dkimToken!}._domainkey.{stackDomain.DomainName}",
                Records = { $"{dkimToken}.dkim.amazonses.com" },
                Ttl = 600,
                Type = "CNAME",
                ZoneId = zoneId,
            });
        }

        // Create SES verification record
        _ = new Record(setup.NameEnvironmentResource("ses-verification-record", stackDomain.StackName), new RecordArgs
        {
            Name = $"_amazonses.{stackDomain.DomainName}",
            Records = { domainVerificationToken.ToString()! },
            Ttl = 600,
            Type = "TXT",
            ZoneId = zoneId,
        });

        // Create dmarc record
        _ = new Record(setup.NameEnvironmentResource("ses-dmarc-record", stackDomain.StackName), new RecordArgs
        {
            Name = $"_dmarc.{stackDomain.DomainName}",
            Records = { $"v=DMARC1;p=none;rua=mailto:dmarc@{stackDomain.DomainName};aspf=r" },
            Ttl = 300,
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