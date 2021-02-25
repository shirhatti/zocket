using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Tmds.Linux;

namespace zocket
{
    class PlatformException : Exception
    {
        public PlatformException(int errno)
        {
            HResult = errno;
        }

        public PlatformException() :
            this(LibC.errno)
        { }

        public static void Throw() => throw new PlatformException();
    }
}
