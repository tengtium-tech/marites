using Marites.Api;
using Marites.Host;
using Marites.Worker;
using Microsoft.Extensions.Hosting;

if (!HostModeParser.TryParse(args, out var mode))
{
    Console.Error.WriteLine("Usage: Marites.Host <api|worker|all>");
    return 2;
}

switch (mode)
{
    case HostMode.Api:
        await ApiHost.Build().RunAsync();
        break;
    case HostMode.Worker:
        await WorkerHost.Build().RunAsync();
        break;
    case HostMode.All:
        await ApiHost.Build(configureAdditionalServices: services => services.AddMaritesWorker()).RunAsync();
        break;
    default:
        return 2;
}

return 0;
