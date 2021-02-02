using System.Runtime.InteropServices;
using Tmds.Linux;
using zocket;

namespace System.Net.Sockets
{
    static class SocketExtensions
    {
        [DllImport("Ws2_32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern unsafe int WSADuplicateSocket(
            [In] SafeSocketHandle s,
            [In] uint dwProcessId,
            [In] WSAPROTOCOL_INFOW* lpProtocolInfo
        );

        public static unsafe SafeHandle DuplicateSocketLinux(this Socket socket, int? targetProcessId = null)
        {
            var handle = socket.SafeHandle;
            var fd = -1;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
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
            }

            return new FileDescriptorSafeHandle(fd);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal unsafe struct WSAPROTOCOL_INFOW
        {
            private const int WSAPROTOCOL_LEN = 255;

            internal uint dwServiceFlags1;
            internal uint dwServiceFlags2;
            internal uint dwServiceFlags3;
            internal uint dwServiceFlags4;
            internal uint dwProviderFlags;
            internal Guid ProviderId;
            internal uint dwCatalogEntryId;
            internal WSAPROTOCOLCHAIN ProtocolChain;
            internal int iVersion;
            internal AddressFamily iAddressFamily;
            internal int iMaxSockAddr;
            internal int iMinSockAddr;
            internal SocketType iSocketType;
            internal ProtocolType iProtocol;
            internal int iProtocolMaxOffset;
            internal int iNetworkByteOrder;
            internal int iSecurityScheme;
            internal uint dwMessageSize;
            internal uint dwProviderReserved;
            internal fixed char szProtocol[WSAPROTOCOL_LEN + 1];
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct WSAPROTOCOLCHAIN
        {
            private const int MAX_PROTOCOL_CHAIN = 7;

            internal int ChainLen;
            internal fixed uint ChainEntries[MAX_PROTOCOL_CHAIN];
        }

        public static unsafe SocketInformation DuplicateSocketWindows(this Socket socket, int targetProcessId)
        {
            var handle = socket.SafeHandle;
            var socketInformation = new SocketInformation
            {
                ProtocolInformation = new byte[sizeof(WSAPROTOCOL_INFOW)]
            };

            fixed (byte* protocolInfoBytes = socketInformation.ProtocolInformation)
            {
                WSAPROTOCOL_INFOW* lpProtocolInfo = (WSAPROTOCOL_INFOW*)protocolInfoBytes;
                int result = WSADuplicateSocket(handle, (uint)targetProcessId, lpProtocolInfo);
                var error = result == 0 ? SocketError.Success : GetLastSocketError();
            }

            return socketInformation;
        }

        public static SocketError GetLastSocketError()
        {
            int win32Error = Marshal.GetLastWin32Error();
            return (SocketError)win32Error;
        }
    }
}