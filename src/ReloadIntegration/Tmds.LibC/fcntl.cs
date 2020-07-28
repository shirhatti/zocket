using System.Runtime.InteropServices;

namespace Tmds.Linux
{
    public unsafe static partial class LibC
    {
        public const string libc = "libc.so.6";
        public static int FD_CLOEXEC => 1;
        public static int F_SETFD => 2;

        [DllImport(libc, SetLastError = true)]
        public static extern int fcntl(int fd, int cmd, int arg);
    }
}