using System.Runtime.InteropServices;

namespace Tmds.Linux
{
    public unsafe static partial class LibC
    {
        public const string libc = "libc.so.6";
        public static int FD_CLOEXEC => 1;
        public static int F_SETFD => 2;

        public static unsafe int errno
            // use the value captured by DllImport
            => Marshal.GetLastWin32Error();

        [DllImport(libc, SetLastError = true)]
        public static extern int close(int fd);

        [DllImport(libc, SetLastError = true)]
        public static extern int dup(int oldfd);

        [DllImport(libc, SetLastError = true)]
        public static extern int fcntl(int fd, int cmd, int arg);
    }
}
