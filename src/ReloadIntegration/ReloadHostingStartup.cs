using Microsoft.AspNetCore.Hosting;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using static Tmds.Linux.LibC;

[assembly: HostingStartup(typeof(ReloadIntegration.ReloadHostingStartup))]

namespace ReloadIntegration
{
    public class ReloadHostingStartup : IHostingStartup
    {
        public const string startupHookEnvironmentVariable = "DOTNET_STARTUP_HOOKS";
        public void Configure(IWebHostBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            ClearStartupHookEnvironmentVariable();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                uint listenFd;
                try
                {
                    var listenFdAsString = Environment.GetEnvironmentVariable("ZOCKET_LISTEN_FD");
                    listenFd = uint.Parse(listenFdAsString);
                    builder.ConfigureKestrel(options =>
                    {
                        options.ListenHandle(listenFd);
                    });
                }
                catch (FormatException)
                {
                    return;
                }

                // We do this to prevent leaking a socket
                fcntl(Convert.ToInt32(listenFd), F_SETFD, FD_CLOEXEC);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Verify that the port passed in is the same as the one in launch settings
                var pipeName = Environment.GetEnvironmentVariable("ZOCKET_PIPE_NAME");
                var namedPipeServer = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                namedPipeServer.Connect();
                namedPipeServer.Write(BitConverter.GetBytes(Process.GetCurrentProcess().Id));
                var buffer = new byte[1024];
                var res = namedPipeServer.Read(buffer);

                var socketInfo = new SocketInformation()
                { ProtocolInformation = (new Memory<byte>(buffer).Slice(0, res)).ToArray() };

                var socket = new Socket(socketInfo);
                builder.ConfigureKestrel(options =>
                {
                    options.ListenHandle((uint)socket.Handle);
                });
                
            }
        }

        private static void ClearStartupHookEnvironmentVariable()
        {
            var startupHooks = Environment.GetEnvironmentVariable(startupHookEnvironmentVariable);
            var currentAssembly = Assembly.GetExecutingAssembly().Location;
            var index = startupHooks.IndexOf(currentAssembly);
            if (index >= 0)
            {
                startupHooks = startupHooks.Remove(index, currentAssembly.Length);
            }
            Environment.SetEnvironmentVariable(startupHookEnvironmentVariable, startupHooks);
        }
    }
}
