using System.Threading.Tasks;
using Pulumi;
using VirtualFinland.Infrastructure.Stacks;

return await Deployment.RunAsync<MainStack>();
