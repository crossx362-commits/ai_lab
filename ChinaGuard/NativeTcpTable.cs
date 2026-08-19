using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace ChinaGuard
{
    class ConnRow
    {
        public int Pid;
        public IPAddress Local;
        public int LocalPort;
        public IPAddress Remote;
        public int RemotePort;
        public string State;
    }

    // GetExtendedTcpTable P/Invoke 로 현재 TCP 연결 목록 조회 (v4 + v6)
    static class NativeTcpTable
    {
        const int AF_INET = 2;
        const int AF_INET6 = 23;
        const int TCP_TABLE_OWNER_PID_ALL = 5;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen,
            bool sort, int ipVersion, int tableClass, uint reserved);

        [StructLayout(LayoutKind.Sequential)]
        struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            public uint localPort;
            public uint remoteAddr;
            public uint remotePort;
            public uint owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MIB_TCP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] localAddr;
            public uint localScopeId;
            public uint localPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] remoteAddr;
            public uint remoteScopeId;
            public uint remotePort;
            public uint state;
            public uint owningPid;
        }

        static readonly Dictionary<uint, string> StateNames = new Dictionary<uint, string>
        {
            { 1, "CLOSED" }, { 2, "LISTEN" }, { 3, "SYN_SENT" }, { 4, "SYN_RCVD" },
            { 5, "ESTABLISHED" }, { 6, "FIN_WAIT1" }, { 7, "FIN_WAIT2" }, { 8, "CLOSE_WAIT" },
            { 9, "CLOSING" }, { 10, "LAST_ACK" }, { 11, "TIME_WAIT" }, { 12, "DELETE_TCB" },
        };

        public static List<ConnRow> GetConnections()
        {
            var rows = new List<ConnRow>();
            CollectV4(rows);
            CollectV6(rows);
            return rows;
        }

        static void CollectV4(List<ConnRow> rows)
        {
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(buf, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) != 0)
                    return;
                int count = Marshal.ReadInt32(buf);
                IntPtr ptr = (IntPtr)((long)buf + 4);
                int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                for (int i = 0; i < count; i++)
                {
                    var row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(ptr, typeof(MIB_TCPROW_OWNER_PID));
                    rows.Add(new ConnRow
                    {
                        Pid = (int)row.owningPid,
                        Local = new IPAddress(row.localAddr),
                        LocalPort = NetPort(row.localPort),
                        Remote = new IPAddress(row.remoteAddr),
                        RemotePort = NetPort(row.remotePort),
                        State = StateName(row.state)
                    });
                    ptr = (IntPtr)((long)ptr + rowSize);
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        static void CollectV6(List<ConnRow> rows)
        {
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0);
            IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(buf, ref size, true, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0) != 0)
                    return;
                int count = Marshal.ReadInt32(buf);
                IntPtr ptr = (IntPtr)((long)buf + 4);
                int rowSize = Marshal.SizeOf(typeof(MIB_TCP6ROW_OWNER_PID));
                for (int i = 0; i < count; i++)
                {
                    var row = (MIB_TCP6ROW_OWNER_PID)Marshal.PtrToStructure(ptr, typeof(MIB_TCP6ROW_OWNER_PID));
                    rows.Add(new ConnRow
                    {
                        Pid = (int)row.owningPid,
                        Local = new IPAddress(row.localAddr),
                        LocalPort = NetPort(row.localPort),
                        Remote = new IPAddress(row.remoteAddr),
                        RemotePort = NetPort(row.remotePort),
                        State = StateName(row.state)
                    });
                    ptr = (IntPtr)((long)ptr + rowSize);
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        static int NetPort(uint dwPort)
        {
            return ((int)(dwPort & 0xFF) << 8) | (int)((dwPort >> 8) & 0xFF);
        }

        static string StateName(uint state)
        {
            string name;
            return StateNames.TryGetValue(state, out name) ? name : state.ToString();
        }
    }
}
