using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Aws.Route53;
using Pulumi.Aws.Ses;
using VirtualFinland.Infrastructure.Common;

namespace VirtualFinland.Infrastructure.Features;

public class SimpleEmailService
{
    private readonly string? _domainName;
    private readonly string _mailFromSubDomain;
    private readonly List<StackOwnsDomain> _stackOwnsDomains = new();


    public SimpleEmailService()
    {
        var config = new Config("ses");
        _domainName = config.Get("domain-name");
        _mailFromSubDomain = config.Get("mail-from-sub-domain") ?? "ses";

        var stackOwnsDomains = config.GetObject<List<string>>("stack-owned-domains");
        if (stackOwnsDomains != null)
        {
            foreach (var stackOwnsDomain in stackOwnsDomains)
            {
                var stackOwnsDomainParts = stackOwnsDomain.Split(':');
                if (stackOwnsDomainParts.Length != 2)
                    throw new System.Exception("Invalid stack-owned-domains configuration. Expected format: <stack-name>:<domain-name>");

                _stackOwnsDomains.Add(new StackOwnsDomain
                {
                    StackName = stackOwnsDomainParts[0],
                    DomainName = stackOwnsDomainParts[1],
                });
            }
        }
    }

    public DomainIdentity? SetupSes(StackSetup setup)
    {
        if (_domainName == null)
            return null;

        var mailFromDomain = $"{_mailFromSubDomain}.{_domainName}";

        var domainIdentity = new DomainIdentity(setup.NameResource("domain-identity"), new DomainIdentityArgs
        {
            Domain = _domainName,
        });

        _ = new MailFrom(setup.NameResource("mail-from-domain"), new MailFromArgs
        {
            Domain = _domainName,
            MailFromDomain = mailFromDomain,
        });

        // SES Domain Identity Verification
        _ = new DomainIdentityVerification("sesDomainVerification", new DomainIdentityVerificationArgs
        {
            Domain = domainIdentity.Id,
        });

        return domainIdentity;
    }

    public async Task SetupSesDomainRecords(StackSetup setup)
    {
        if (_domainName == null)
            return;

        foreach (var stackDomain in _stackOwnsDomains)
        {
            await SetupStackDomain(setup, stackDomain);
        }
    }

    private async Task SetupStackDomain(StackSetup setup, StackOwnsDomain stackDomain)
    {
        var mailFromDomain = $"{_mailFromSubDomain}.{stackDomain.DomainName}";

        // Stack reference from access-finland app where the domain is owned
        var afStack = new StackReference($"{setup.Organization}/access-finland/{stackDomain.StackName}");
        var zoneIdish = await afStack.GetValueAsync("zoneId");
        if (zoneIdish == null)
            return;
        var zoneId = zoneIdish.ToString()!;

        // Stack reference from self where the domain identity is created
        var selfStack = new StackReference($"{setup.Organization}/infrastructure/{stackDomain.StackName}");
        var domainIdentityIdish = await selfStack.GetValueAsync("SesDomainIdentityId");
        if (domainIdentityIdish == null)
            return;
        var domainIdentityId = domainIdentityIdish.ToString()!;
        var domainIdentity = DomainIdentity.Get("domainIdentity", domainIdentityId);

        // Records for mail from domain
        _ = new Record("mailFromRecordVerificationTxt", new RecordArgs
        {
            Name = mailFromDomain,
            Records = { "\"v=spf1 include:amazonses.com ~all\"" },
            Ttl = 600,
            Type = "TXT",
            ZoneId = zoneId,
        });

        _ = new Record("mailFromRecordVerificationMx", new RecordArgs
        {
            Name = mailFromDomain,
            Records = { "10 feedback-smtp.eu-north-1.amazonses.com" },
            Ttl = 600,
            Type = "MX",
            ZoneId = zoneId,
        });

        // Create DKIM verifications
        var domainDkim = new DomainDkim("domainDkim", new DomainDkimArgs
        {
            Domain = stackDomain.DomainName,
        });

        // Create DKIM records
        domainDkim.DkimTokens.Apply(dkimTokens =>
        {
            for (var i = 0; i < dkimTokens.Length; i++)
            {
                _ = new Record($"dkimRecordVerification-{dkimTokens[i]}", new RecordArgs
                {
                    Name = dkimTokens[i],
                    Records = { dkimTokens[i] },
                    Ttl = 600,
                    Type = "CNAME",
                    ZoneId = zoneId,
                });
            }
            return dkimTokens;
        });

        // Create SES verification record
        var sesRecordVerification = new Record("sesVerificationRecord", new RecordArgs
        {
            Name = $"_amazonses.{stackDomain.DomainName}",
            Records = { domainIdentity.VerificationToken },
            Ttl = 600,
            Type = "TXT",
            ZoneId = zoneId,
        });

        // Create SPF record
        var sesSpfRecord = new Record("sesSpfRecord", new RecordArgs
        {
            Name = stackDomain.DomainName,
            Records = { "v=spf1 include:amazonses.com ~all" },
            Ttl = 60,
            Type = "TXT",
            ZoneId = zoneId,
        });
    }

    private record StackOwnsDomain
    {
        public string DomainName { get; init; } = string.Empty;
        public string StackName { get; init; } = string.Empty;
    }
}