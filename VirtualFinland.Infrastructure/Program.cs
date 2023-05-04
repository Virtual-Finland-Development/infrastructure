using Pulumi;
using VirtualFinland.Infrastructure.Stacks;

return await Deployment.RunAsync<VFDStack>();