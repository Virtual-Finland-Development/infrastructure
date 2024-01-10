
using System;
using System.Collections.Generic;
using Pulumi;
namespace VirtualFinland.Infrastructure.Common;

public class StackSetup
{
    public string Environment;
    public string ProjectName;
    public string Organization;

    public Dictionary<string, string> Tags;

    public Dictionary<string, string> SharedResourceTags;

    public StackSetup()
    {
        Environment = Deployment.Instance.StackName;
        ProjectName = Deployment.Instance.ProjectName;
        Organization = Deployment.Instance.OrganizationName;

        Tags = new()
        {
            {
                "vfd:stack", Environment
            },
            {
                "vfd:project", ProjectName
            }
        };

        SharedResourceTags = new(Tags)
        {
            ["vfd:stack"] = "shared"
        };
    }

    public string NameResource(string name)
    {
        return NameEnvironmentResource(name, Environment);
    }

    public string NameEnvironmentResource(string name, string environment)
    {
        return $"{ProjectName}-{name}-{environment}";
    }
}