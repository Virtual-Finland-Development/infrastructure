using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Aws.Route53;
using Pulumi.Aws.Ses;
using Pulumi.Aws.Ses.Inputs;
using VirtualFinland.Infrastructure.Common;

namespace VirtualFinland.Infrastructure.Features;

public class SimpleEmailService
{
    private readonly StackSetup _setup;
    private readonly string? _domainZoneId;
    private readonly string? _domainName;
    private readonly string _mailFromSubDomain;
    private readonly bool _createDomainRecords;
    private readonly List<StackOwnsDomain> _createDomainRecordsFor = new();

    public DomainIdentity? DomainIdentity { get; private set; }
    public DomainDkim? DomainDkim { get; private set; }

    public SimpleEmailService(StackSetup setup)
    {
        _setup = setup;

        var config = new Config("ses");
        _domainName = config.Get("domain-name") ?? "";
        _mailFromSubDomain = config.Get("mail-from-sub-domain") ?? "ses";
        _domainZoneId = config.Get("domain-zone-id") ?? null;
        _createDomainRecords = config.GetBoolean("create-domain-records") ?? false;

        var createDomainRecordsFor = config.GetObject<List<string>>("also-create-domain-records-for");
        if (createDomainRecordsFor != null)
        {
            foreach (var stackOwnsDomain in createDomainRecordsFor)
            {
                var stackOwnsDomainParts = stackOwnsDomain?.Split(':');
                if (stackOwnsDomainParts != null && stackOwnsDomainParts.Length == 2)
                {
                    _createDomainRecordsFor.Add(new StackOwnsDomain
                    {
                        StackName = stackOwnsDomainParts[0],
                        DomainName = stackOwnsDomainParts[1],
                    });
                }
            }
        }

        if (_createDomainRecords && _createDomainRecordsFor.Find(x => x.StackName == setup.Environment) == null)
        {
            _createDomainRecordsFor.Add(new StackOwnsDomain
            {
                StackName = setup.Environment,
                DomainName = _domainName,
            });
        }
    }

    public bool IsDeployable()
    {
        return !string.IsNullOrEmpty(_domainName);
    }

    public void SetupDomainIndentity()
    {
        DomainIdentity = new DomainIdentity(_setup.NameResource("domain-identity"), new DomainIdentityArgs
        {
            Domain = _domainName!,
        });

        _ = new MailFrom(_setup.NameResource("mail-from-domain"), new MailFromArgs
        {
            Domain = _domainName!,
            MailFromDomain = $"{_mailFromSubDomain}.{_domainName}",
        });

        // Create DKIM verifications
        DomainDkim = new DomainDkim(_setup.NameResource("domain-dkim"), new DomainDkimArgs
        {
            Domain = _domainName!,
        });

        _ = new ConfigurationSet(_setup.NameResource("ses-configuration-set"), new ConfigurationSetArgs
        {
            Name = "af-ses-configuration-set",
            DeliveryOptions = new ConfigurationSetDeliveryOptionsArgs
            {
                TlsPolicy = "Require",
            },
        });
    }

    public async Task SetupDomainRecords()
    {
        if (_createDomainRecordsFor.Count == 0)
        {
            Console.WriteLine("Skipping DNS records: stack does not own domains");
            return;
        }

        // Stack reference from access-finland app where the domain is owned
        string? zoneId = null;
        if (_domainZoneId != null)
        {
            zoneId = _domainZoneId;
        }
        else
        {
            var afStack = new StackReference($"{_setup.Organization}/access-finland/{_setup.Environment}");
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
            Console.WriteLine($"ZoneId not found for stack {_setup.Environment}");
            return; // Skip for now, needs a second run after the zone is created in the af stack
        }

        foreach (var stackDomain in _createDomainRecordsFor)
        {
            await SetupStackDomain(stackDomain, zoneId);
        }
    }

    private async Task SetupStackDomain(StackOwnsDomain stackDomain, string zoneId)
    {
        var mailFromDomain = $"{_mailFromSubDomain}.{stackDomain.DomainName}";
        var recordTokens = await ResolveStacksDomainRecordTokens(stackDomain.StackName);
        if (recordTokens == null)
        {
            Console.WriteLine($"Skipping DNS records: stack {stackDomain.StackName} does not own domain {stackDomain.DomainName}");
            return;
        }

        // Records for mail from domain
        _ = new Record(_setup.NameEnvironmentResource("mail-from-record-verification-txt", stackDomain.StackName), new RecordArgs
        {
            Name = mailFromDomain,
            Records = { "v=spf1 include:amazonses.com ~all" },
            Ttl = 600,
            Type = "TXT",
            ZoneId = zoneId,
        });

        _ = new Record(_setup.NameEnvironmentResource("mail-from-record-verification-mx", stackDomain.StackName), new RecordArgs
        {
            Name = mailFromDomain,
            Records = { $"10 feedback-smtp.{_setup.Region}.amazonses.com" },
            Ttl = 600,
            Type = "MX",
            ZoneId = zoneId,
        });

        // Create DKIM records
        recordTokens.DkimTokens.Apply(dkimTokens =>
        {
            for (var i = 0; i < dkimTokens.Length; i++)
            {
                var dkimToken = dkimTokens[i] as string;
                _ = new Record(_setup.NameEnvironmentResource($"dkim-record-verification-{i}", stackDomain.StackName), new RecordArgs
                {
                    Name = $"{dkimToken!}._domainkey.{stackDomain.DomainName}",
                    Records = { $"{dkimToken}.dkim.amazonses.com" },
                    Ttl = 600,
                    Type = "CNAME",
                    ZoneId = zoneId,
                });
            }
            return dkimTokens;
        });

        // Create SES verification record
        recordTokens.DomainVerificationToken.Apply(domainVerificationToken =>
        {
            _ = new Record(_setup.NameEnvironmentResource("ses-verification-record", stackDomain.StackName), new RecordArgs
            {
                Name = $"_amazonses.{stackDomain.DomainName}",
                Records = { domainVerificationToken.ToString()! },
                Ttl = 600,
                Type = "TXT",
                ZoneId = zoneId,
            });
            return domainVerificationToken;
        });

        // Create dmarc record
        _ = new Record(_setup.NameEnvironmentResource("ses-dmarc-record", stackDomain.StackName), new RecordArgs
        {
            Name = $"_dmarc.{stackDomain.DomainName}",
            Records = { $"v=DMARC1;p=none;rua=mailto:dmarc@{stackDomain.DomainName};aspf=r" },
            Ttl = 300,
            Type = "TXT",
            ZoneId = zoneId,
        });
    }

    private async Task<StackDomainRecordTokens?> ResolveStacksDomainRecordTokens(string stackName)
    {
        if (stackName == _setup.Environment)
        {
            return new StackDomainRecordTokens
            {
                DomainVerificationToken = DomainIdentity!.VerificationToken,
                DkimTokens = DomainDkim!.DkimTokens,
            };
        }

        // Stack reference from self where the domain identity is created
        var selfStack = new StackReference($"{_setup.Organization}/infrastructure/{stackName}");
        var domainVerificationToken = await selfStack.GetValueAsync("SesDomainIdentityVerificationToken");
        if (domainVerificationToken == null)
        {
            Console.WriteLine($"SesDomainIdentityVerificationToken not found for stack {stackName}");
            return null;
        }

        var dkimTokensRaw = await selfStack.RequireValueAsync("DkimTokens");
        if (dkimTokensRaw == null)
        {
            Console.WriteLine($"DkimTokens not found for stack {stackName}");
            return null;
        }
        var dkimTokens = (ImmutableArray<object>)dkimTokensRaw;

        return new StackDomainRecordTokens
        {
            DomainVerificationToken = Output.Create(domainVerificationToken.ToString()!),
            DkimTokens = Output.Create(dkimTokens.Select(x => x.ToString()!).ToImmutableArray()),
        };
    }

    private record StackOwnsDomain
    {
        public string DomainName { get; init; } = string.Empty;
        public string StackName { get; init; } = string.Empty;
    }

    private record StackDomainRecordTokens
    {
        public Output<string> DomainVerificationToken { get; init; } = default!;
        public Output<ImmutableArray<string>> DkimTokens { get; init; } = default!;
    }
}