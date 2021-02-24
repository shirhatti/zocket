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
                new Option<int>(
                    "--tls-port",
                    getDefaultValue: () => 10000),
                new Argument<string>(
                    "command",
                    getDefaultValue: () => "dotnet watch run",
                    description: "The command to execute with zocket"
                    )
            };
            rootCommand.Description = "zocket";
            rootCommand.Handler = CommandHandler.Create<int, int, string>(Listen);

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task Listen(int port, int tlsPort, string command)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var parsedCommand = command.Split(' ', 2);
            ProcessStartInfo psi = parsedCommand.Length switch
            {
                1 => new ProcessStartInfo(parsedCommand[0]),
                2 => new ProcessStartInfo(parsedCommand[0], parsedCommand[1]),
                _ => default
            };

            using var httpListenSocket = SetupSocket(port);
            using var httpsListenSocket = SetupSocket(tlsPort);

            var currentAssembly = Assembly.GetExecutingAssembly().Location;
            var reloadIntegrationPath = Path.GetFullPath(Path.Combine(currentAssembly, "..", "ReloadIntegration.dll"));

            const string dotnetStartHooksName = "DOTNET_STARTUP_HOOKS";
            psi.EnvironmentVariables[dotnetStartHooksName] = AddOrAppend(dotnetStartHooksName, reloadIntegrationPath, Path.PathSeparator);
            const string hostingStartupAssembliesName = "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES";
            psi.EnvironmentVariables[hostingStartupAssembliesName] = AddOrAppend(hostingStartupAssembliesName, "ReloadIntegration", ';');

            string pipeName = default;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pipeName = Guid.NewGuid().ToString();
                psi.EnvironmentVariables["ZOCKET_PIPE_NAME"] = pipeName;

            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var duplicatedHttpSocket = httpListenSocket.DuplicateSocketLinux();
                var duplicatedHttpsSocket = httpsListenSocket.DuplicateSocketLinux();

                psi.EnvironmentVariables["ZOCKET_LISTEN_HTTP_FD"] = duplicatedHttpSocket.DangerousGetHandle().ToInt32().ToString();
                psi.EnvironmentVariables["ZOCKET_LISTEN_HTTPS_FD"] = duplicatedHttpsSocket.DangerousGetHandle().ToInt32().ToString();
            }

            using var initialProcess = Process.Start(psi);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await ListenWindows(cts, httpListenSocket, httpsListenSocket, pipeName, initialProcess);
            }

            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await initialProcess.WaitForExitAsync(cts.Token);
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

        private static async Task ListenWindows(CancellationTokenSource cts, Socket httpListenSocket, Socket httpsListenSocket, string pipeName, Process initialProcess)
        {
            // See if initial process has shutdown at anypoint, if so shutdown zocket.
            var initialProcessExit = Task.Run(async () =>
            {
                await initialProcess.WaitForExitAsync();
                cts.Cancel();
            });
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    using var namedPipeServer = new NamedPipeServerStream(pipeName,
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    await namedPipeServer.WaitForConnectionAsync(cts.Token);

                    var buffer = new byte[16]; // Only need enough for the length of a PID, 16 should be plenty
                    var length = await namedPipeServer.ReadAsync(buffer, cts.Token);
                    var pid = BitConverter.ToInt32(new ReadOnlySpan<byte>(buffer).Slice(0, length));

                    // TODO how can we pass this info into other transports (QUIC) s.t. it duplicates rather than creates?

                    // Send http socket
                    var httpSocketInfo = httpListenSocket.DuplicateSocketWindows(pid);
                    await namedPipeServer.WriteAsync(BitConverter.GetBytes(httpSocketInfo.ProtocolInformation.Length));
                    await namedPipeServer.WriteAsync(httpSocketInfo.ProtocolInformation, cts.Token);

                    // Send https socket
                    var httpsSocketInfo = httpsListenSocket.DuplicateSocketWindows(pid);
                    await namedPipeServer.WriteAsync(BitConverter.GetBytes(httpsSocketInfo.ProtocolInformation.Length));
                    await namedPipeServer.WriteAsync(httpsSocketInfo.ProtocolInformation, cts.Token);

                    var backendProcess = Process.GetProcessById(pid);

                    await backendProcess.WaitForExitAsync(cts.Token);
                }
                catch (Exception)
                {
                    // Ignore exceptions for now.
                    // TODO enable a debug log mode.
                    break;
                }
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
        
        private static string AddOrAppend(string envVarName, string envVarValue, char separator)
        {
            var existing = Environment.GetEnvironmentVariable(envVarName);

            if (!string.IsNullOrEmpty(existing))
            {
                return $"{existing}{separator}{envVarValue}";
            }
            else
            {
                return envVarValue;
            }
        }
    }
}
