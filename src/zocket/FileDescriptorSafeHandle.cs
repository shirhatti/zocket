using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tmds.Linux;

namespace zocket
{
    class FileDescriptorSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public FileDescriptorSafeHandle(int fd) : this((IntPtr)fd) { }
        public FileDescriptorSafeHandle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }
        protected override bool ReleaseHandle()
        {
            var rv = LibC.close(handle.ToInt32());
            if (rv == -1)
            {
                PlatformException.Throw();
            }
            return true;
        }
    }
}
