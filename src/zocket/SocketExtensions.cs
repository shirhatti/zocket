using System.Runtime.InteropServices;
using Tmds.Linux;
using zocket;

namespace System.Net.Sockets
{
    static class SocketExtensions
    {
        public static unsafe SafeHandle DuplicateSocket(this Socket socket)
        {
            var handle = socket.SafeHandle;
            var fd = -1;
            try
            {
                fd = LibC.dup(handle.DangerousGetHandle().ToInt32());
                if (fd == -1)
                {
                    PlatformException.Throw();
                }
            }
            finally
            {
                handle.DangerousRelease();
            }
            return new FileDescriptorSafeHandle(fd);
        }
    }
}