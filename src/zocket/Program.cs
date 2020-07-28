using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using zocket;
using Process = System.Diagnostics.Process;
namespace Zocket
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<int>(
                    "--port",
                    getDefaultValue: () => 9999,
                    description: "Port to bind to")
            };
            rootCommand.Description = "zocket";
            rootCommand.Handler = CommandHandler.Create<int>(Listen);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static void Listen(int port)
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
            var duplicatedSocket = listenSocket.DuplicateSocket();

            var psi = new ProcessStartInfo("dotnet", "watch run");


            var currentAssembly = Assembly.GetExecutingAssembly().Location;
            var reloadIntegrationPath = Path.GetFullPath(Path.Combine(currentAssembly, "..", "ReloadIntegration.dll"));

            psi.EnvironmentVariables["ZOCKET_LISTEN_FD"] = duplicatedSocket.DangerousGetHandle().ToInt32().ToString();
            psi.EnvironmentVariables["DOTNET_STARTUP_HOOKS"] = reloadIntegrationPath;
            psi.EnvironmentVariables["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "ReloadIntegration";

            var process = Process.Start(psi);
            exitEvent.WaitOne();

            process.Terminate();
        }
    }
}
