using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using static Tmds.Linux.LibC;

[assembly: HostingStartup(typeof(ReloadIntegration.ReloadHostingStartup))]

namespace ReloadIntegration
{
    public class ReloadHostingStartup : IHostingStartup
    {
        internal const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
        public const string startupHookEnvironmentVariable = "DOTNET_STARTUP_HOOKS";
        public void Configure(IWebHostBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            ClearStartupHookEnvironmentVariable();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                uint listenHttpFd, listenHttpsFd;
                try
                {
                    listenHttpFd = uint.Parse(Environment.GetEnvironmentVariable("ZOCKET_LISTEN_HTTP_FD"));
                    listenHttpsFd = uint.Parse(Environment.GetEnvironmentVariable("ZOCKET_LISTEN_HTTPS_FD"));
                    builder.ConfigureServices(services =>
                    {
                        services.PostConfigure<KestrelServerOptions>(options =>
                        {
                            options.ListenHandle(listenHttpFd);
                            options.ListenHandle(listenHttpsFd, options => options.UseHttps());
                        });
                    });
                }
                catch (FormatException)
                {
                    return;
                }

                // We do this to prevent leaking a socket
                fcntl((int)listenHttpFd, F_SETFD, FD_CLOEXEC);
                fcntl((int)listenHttpsFd, F_SETFD, FD_CLOEXEC);

            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // TODO Verify that the port passed in is the same as the one in launch settings
                // TODO make this async? (no async startup though)
                var pipeName = Environment.GetEnvironmentVariable("ZOCKET_PIPE_NAME");
                var namedPipeServer = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                namedPipeServer.Connect();
                namedPipeServer.Write(BitConverter.GetBytes(Process.GetCurrentProcess().Id));

                // ProtocolInformation is usually ~630 bytes. TODO may need a read loop here to make sure we have all bytes
                var buffer = new byte[1024];

                // Read size, then read SocketInfo
                var length = namedPipeServer.Read(buffer, 0, sizeof(int));
                var socketInfoLength = BitConverter.ToInt32(buffer, 0);
                length = namedPipeServer.Read(buffer, 0, socketInfoLength);
                var socketInfo = new SocketInformation()
                {
                    ProtocolInformation = (new Memory<byte>(buffer).Slice(0, length)).ToArray()
                };

                // Shouldn't need to dispose of socket as Kestrel will dispose for us?
                var httpSocket = new Socket(socketInfo);

                // Repeat for second socketInfo
                length = namedPipeServer.Read(buffer, 0, sizeof(int));
                socketInfoLength = BitConverter.ToInt32(buffer, 0);
                length = namedPipeServer.Read(buffer, 0, socketInfoLength);
                socketInfo = new SocketInformation()
                {
                    ProtocolInformation = (new Memory<byte>(buffer).Slice(0, length)).ToArray()
                };
                var httpsSocket = new Socket(socketInfo);

                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<KestrelServerOptions>(options =>
                    {
                        options.ListenHandle((uint)httpSocket.Handle);
                        options.ListenHandle((uint)httpsSocket.Handle, options => options.UseHttps());
                    });
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
