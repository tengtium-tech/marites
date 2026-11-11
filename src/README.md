# C# backend scaffold

## Prerequisites

- .NET 10 SDK

## Run the hosts

From the repository root, choose one entrypoint:

```powershell
dotnet run --project src/Marites.Host -- api
dotnet run --project src/Marites.Host -- worker
dotnet run --project src/Marites.Host -- all
```

The API host uses the standard ASP.NET Core configuration sources. Set `ASPNETCORE_URLS` to override its listen address, for example `http://127.0.0.1:5080`. The worker has no required product settings yet. Press Ctrl+C to request graceful shutdown; hosts allow up to 30 seconds for hosted services to stop.

This is the initial host and dependency-boundary scaffold. Capture, API operations, database connections, and broker connections are not implemented in this increment.
