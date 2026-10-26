using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;
using Xunit;

namespace WalletFlow.BrowserTests;

[CollectionDefinition("WalletFlow browser tests", DisableParallelization = true)]
public sealed class WalletFlowBrowserCollection : ICollectionFixture<WalletFlowBrowserFixture>
{
    public const string Name = "WalletFlow browser tests";
}

public sealed class WalletFlowBrowserFixture : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> _serverOutput = new();
    private Process? _server;
    private IPlaywright? _playwright;
    private string? _temporaryDirectory;

    public IBrowser Browser { get; private set; } = null!;
    public string BaseAddress { get; private set; } = string.Empty;
    public string ServerOutput => GetServerOutput();

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "chrome",
            Headless = true
        });

        var repositoryRoot = FindRepositoryRoot();
        var appProjectDirectory = Path.Combine(repositoryRoot, "WalletFlow.Blazor", "WalletFlow.Blazor");
        const string configuration = "BrowserTests";
        var appAssembly = Path.Combine(appProjectDirectory, "bin", configuration, "net10.0", "WalletFlow.Blazor.dll");
        if (!File.Exists(appAssembly))
            throw new FileNotFoundException("A aplicação Blazor não foi compilada para os testes de navegador.", appAssembly);

        var port = GetAvailablePort();
        BaseAddress = $"http://127.0.0.1:{port}/";
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"WalletFlow.BrowserTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = appProjectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(appAssembly);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseAddress.TrimEnd('/'));
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = BaseAddress.TrimEnd('/');
        startInfo.Environment["ConnectionStrings__DefaultConnection"] =
            $"Data Source={Path.Combine(_temporaryDirectory, "WalletFlow.BrowserTests.db")}";
        startInfo.Environment["Api__BaseUrl"] = "http://127.0.0.1:1/";

        _server = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _server.OutputDataReceived += (_, eventArgs) => AddOutput(eventArgs.Data);
        _server.ErrorDataReceived += (_, eventArgs) => AddOutput(eventArgs.Data);
        if (!_server.Start())
            throw new InvalidOperationException("Não foi possível iniciar a aplicação Blazor para os testes.");

        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();
        await WaitForServerAsync();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();
        _playwright?.Dispose();

        if (_server is not null)
        {
            if (!_server.HasExited)
            {
                _server.Kill(entireProcessTree: true);
                await _server.WaitForExitAsync();
            }
            _server.Dispose();
        }

        if (_temporaryDirectory is not null &&
            Path.GetFullPath(_temporaryDirectory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private async Task WaitForServerAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var timeoutAt = DateTime.UtcNow.AddSeconds(45);

        while (DateTime.UtcNow < timeoutAt)
        {
            if (_server is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"A aplicação encerrou durante a inicialização.{Environment.NewLine}{GetServerOutput()}");
            }

            try
            {
                using var response = await client.GetAsync(BaseAddress);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Kestrel ainda está iniciando.
            }
            catch (TaskCanceledException)
            {
                // A primeira resposta pode exceder o limite enquanto o banco é migrado.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"A aplicação não respondeu em 45 segundos.{Environment.NewLine}{GetServerOutput()}");
    }

    private void AddOutput(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
            _serverOutput.Enqueue(line);
    }

    private string GetServerOutput() => string.Join(Environment.NewLine, _serverOutput.TakeLast(40));

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WalletFlow.WebApi.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Não foi possível localizar WalletFlow.WebApi.slnx.");
    }
}
