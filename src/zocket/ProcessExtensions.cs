using System.Diagnostics;
using static Tmds.Linux.LibC;

namespace zocket
{
    static class ProcessExtensions
    {
        public static void Terminate(this Process process)
        {
            if (process.HasExited)
            {
                return;
            }
            int rv = kill(process.Handle.ToInt32(), SIGTERM);
            if (rv == -1 &&
                errno != ESRCH /* process does not exist, assume it exited */)
            {
                PlatformException.Throw();
            }
        }
    }
}
