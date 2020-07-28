using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
