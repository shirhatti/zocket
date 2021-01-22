using Microsoft.Win32;
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
                    description: "Port to bind to"),
                new Argument<string>(
                    "command",
                    getDefaultValue: () => @"dotnet run --project C:\Users\jukotali\code\test\WebApp\WebApp.csproj",
                    description: "The command to execute with zocket"
                    )
            };
            rootCommand.Description = "zocket";
            rootCommand.Handler = CommandHandler.Create<int, string>(Listen);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static void Listen(int port, string command)
        {
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                exitEvent.Set();
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

            //psi.EnvironmentVariables["ZOCKET_LISTEN_FD"] = duplicatedSocket.DangerousGetHandle().ToInt32().ToString();
            psi.EnvironmentVariables["DOTNET_STARTUP_HOOKS"] = reloadIntegrationPath;
            psi.EnvironmentVariables["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "ReloadIntegration";

            var process = Process.Start(psi);

            // need event to get pid value.

            // Wait for Event (for pid)

            // read reg key for pid

            // Set event

            // set reg key with file descriptor or info needed to get socket.
            int id = -1;
            while (true)
            {
                var processId = (int?)Registry.CurrentUser.GetValue("zocketprocessid2");
                if (processId != null)
                {
                    id = processId.Value;
                    break;
                }
            }

            var ipEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            using var listenSocket = new Socket(ipEndPoint.AddressFamily,
                                                SocketType.Stream,
                                                ProtocolType.Tcp);
            listenSocket.Bind(ipEndPoint);
            var socketInfo = listenSocket.DuplicateSocketWindows(process.Id);

            // How to set reg key with this info.
            Registry.CurrentUser.SetValue("zockettestname", socketInfo.ProtocolInformation);
            exitEvent.WaitOne();

            process.Kill();
        }
    }
}
