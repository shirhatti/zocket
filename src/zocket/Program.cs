using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using zocket;
using Process = System.Diagnostics.Process;
namespace Zocket
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<int>(
                    "--port",
                    getDefaultValue: () => 9999,
                    description: "Port to bind to"),
                new Argument<string>(
                    "command",
                    getDefaultValue: () => @"dotnet run --project C:\Users\jukotali\code\test\WebApp\WebApp.csproj",
                    description: "The command to execute with zocket"
                    )
            };
            rootCommand.Description = "zocket";
            rootCommand.Handler = CommandHandler.Create<int, string>(Listen);

            return await rootCommand.InvokeAsync(args);
        }

        private static Task Listen(int port, string command)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ListenWindows(port, command);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return ListenLinux(port, command);
            }
            return Task.CompletedTask;
        }

        private static Task ListenLinux(int port, string command)
        {
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                exitEvent.Set();
            };
            var ipEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            using var listenSocket = new Socket(ipEndPoint.AddressFamily,
                                                SocketType.Stream,
                                                ProtocolType.Tcp);
            listenSocket.Bind(ipEndPoint);
            var duplicatedSocket = listenSocket.DuplicateSocketLinux();
            var parsedCommand = command.Split(' ', 2);
            ProcessStartInfo psi = parsedCommand.Length switch
            {
                1 => new ProcessStartInfo(parsedCommand[0]),
                2 => new ProcessStartInfo(parsedCommand[0], parsedCommand[1]),
                _ => throw new ArgumentException(),
            };

            var currentAssembly = Assembly.GetExecutingAssembly().Location;
            var reloadIntegrationPath = Path.GetFullPath(Path.Combine(currentAssembly, "..", "ReloadIntegration.dll"));

            psi.EnvironmentVariables["ZOCKET_LISTEN_FD"] = duplicatedSocket.DangerousGetHandle().ToInt32().ToString();
            psi.EnvironmentVariables["DOTNET_STARTUP_HOOKS"] = reloadIntegrationPath;
            psi.EnvironmentVariables["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "ReloadIntegration";

            var process = Process.Start(psi);
            exitEvent.WaitOne();

            process.Terminate();
            return Task.CompletedTask;
        }

        private static async Task ListenWindows(int port, string command)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            var parsedCommand = command.Split(' ', 2);
            ProcessStartInfo psi = parsedCommand.Length switch
            {
                1 => new ProcessStartInfo(parsedCommand[0]),
                2 => new ProcessStartInfo(parsedCommand[0], parsedCommand[1]),
                _ => throw new ArgumentException(),
            };

            var currentAssembly = Assembly.GetExecutingAssembly().Location;
            var reloadIntegrationPath = Path.GetFullPath(Path.Combine(currentAssembly, "..", "ReloadIntegration.dll"));

            using var listenSocket = SetupSocket(port);

            psi.EnvironmentVariables["DOTNET_STARTUP_HOOKS"] = reloadIntegrationPath;
            psi.EnvironmentVariables["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "ReloadIntegration";
            var pipeName = Guid.NewGuid().ToString();
            psi.EnvironmentVariables["ZOCKET_PIPE_NAME"] = pipeName;
            using var initialProcess = Process.Start(psi);

            // See if initial process has shutdown at anypoint, if so shutdown zocket.
            var initialProcessExit = Task.Run(async () =>
            {
                await initialProcess.WaitForExitAsync();
                cancellationTokenSource.Cancel();
            });

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    using var namedPipeServer = new NamedPipeServerStream(pipeName,
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    await namedPipeServer.WaitForConnectionAsync(cancellationTokenSource.Token);

                    var res = new byte[100];
                    var length = await namedPipeServer.ReadAsync(res, cancellationTokenSource.Token);
                    var pid = BitConverter.ToInt32(new Memory<byte>(res).Slice(0, length).ToArray());

                    // TODO how can we pass this info into other transports (QUIC) s.t. it duplicates rather than creates?

                    var socketInfo = listenSocket.DuplicateSocketWindows(pid);
                    await namedPipeServer.WriteAsync(socketInfo.ProtocolInformation, cancellationTokenSource.Token);

                    var backendProcess = Process.GetProcessById(pid);

                    await backendProcess.WaitForExitAsync(cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    break;
                }
            }

            try
            {
                initialProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // process has already exited.
            }
        }

        private static Socket SetupSocket(int port)
        {
            var ipEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            var listenSocket = new Socket(ipEndPoint.AddressFamily,
                                    SocketType.Stream,
                                    ProtocolType.Tcp);
            listenSocket.Bind(ipEndPoint);
            listenSocket.Listen();
            return listenSocket;
        }
    }
}
