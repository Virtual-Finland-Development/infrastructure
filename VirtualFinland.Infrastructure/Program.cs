using Pulumi;
using VirtualFinland.Infrastructure.Stacks;

await Deployment.RunAsync<VPCStack>();
await Deployment.RunAsync<KeyRotatorStack>();