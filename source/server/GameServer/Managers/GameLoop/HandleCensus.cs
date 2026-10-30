using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Interop = System.Runtime.InteropServices.Marshal;   // DOL.GS has a Marshal class of its own
using System.Text;

namespace DOL.GS
{
    /// <summary>
    /// What the process's handles are, by kind (Event, File, Thread, Key, ...), for SERVER_MEMORY. The handle count climbs by about a
    /// thousand an hour (2026-10-30: 1,506 to 2,981 in 85 minutes) and nothing said what kind of handle it is. Windows only: the
    /// handle table is read with NtQuerySystemInformation (the extended handle list), and each kind's name with NtQueryObject on a
    /// handle of our own. Reads only; on any failure it reports nothing.
    /// </summary>
    public static class HandleCensus
    {
        private const int SystemExtendedHandleInformation = 64;
        private const int ObjectTypeInformation = 2;
        private const uint StatusInfoLengthMismatch = 0xC0000004;
        private const int HeaderBytes = 16;     // NumberOfHandles + Reserved, one pointer each
        private const int EntryBytes = 40;      // SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX on 64 bits

        [DllImport("ntdll.dll")]
        private static extern uint NtQuerySystemInformation(int informationClass, IntPtr buffer, int length, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern uint NtQueryObject(IntPtr handle, int informationClass, IntPtr buffer, int length, out int returnLength);

        /// <summary>The number of this process's handles by kind name, or null when it cannot be read.</summary>
        public static Dictionary<string, int> Take()
        {
            if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
                return null;

            IntPtr table = IntPtr.Zero;
            try
            {
                int size = 8 * 1024 * 1024;
                uint status;
                int needed;
                while (true)
                {
                    table = Interop.AllocHGlobal(size);
                    status = NtQuerySystemInformation(SystemExtendedHandleInformation, table, size, out needed);
                    if (status != StatusInfoLengthMismatch)
                        break;
                    Interop.FreeHGlobal(table);
                    table = IntPtr.Zero;
                    size = Math.Max(size * 2, needed + 1024 * 1024);
                    if (size > 256 * 1024 * 1024)
                        return null;
                }

                if (status != 0)
                    return null;

                long count = Interop.ReadInt64(table);
                long pid = Environment.ProcessId;
                var byIndex = new Dictionary<int, int>();
                var sample = new Dictionary<int, IntPtr>();
                for (long i = 0; i < count; i++)
                {
                    IntPtr entry = table + HeaderBytes + (int)(i * EntryBytes);
                    if (Interop.ReadInt64(entry, 8) != pid)
                        continue;
                    int typeIndex = Interop.ReadInt16(entry, 30) & 0xFFFF;
                    byIndex[typeIndex] = byIndex.GetValueOrDefault(typeIndex) + 1;
                    if (!sample.ContainsKey(typeIndex))
                        sample[typeIndex] = (IntPtr)Interop.ReadInt64(entry, 16);
                }

                var result = new Dictionary<string, int>();
                foreach (var pair in byIndex)
                {
                    string name = TypeName(sample[pair.Key]) ?? $"type{pair.Key}";
                    result[name] = result.GetValueOrDefault(name) + pair.Value;
                }
                return result;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (table != IntPtr.Zero)
                    Interop.FreeHGlobal(table);
            }
        }

        private static string TypeName(IntPtr handle)
        {
            IntPtr buffer = Interop.AllocHGlobal(1024);
            try
            {
                if (NtQueryObject(handle, ObjectTypeInformation, buffer, 1024, out _) != 0)
                    return null;
                int length = Interop.ReadInt16(buffer, 0);
                IntPtr text = (IntPtr)Interop.ReadInt64(buffer, 8);
                return length > 0 && text != IntPtr.Zero ? Interop.PtrToStringUni(text, length / 2) : null;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Interop.FreeHGlobal(buffer);
            }
        }

        /// <summary>"Event=812 File=301 Thread=44 ..." (the largest kinds first), or "unavailable".</summary>
        public static string Describe(int top = 10)
        {
            Dictionary<string, int> kinds = Take();
            if (kinds == null || kinds.Count == 0)
                return "unavailable";
            var text = new StringBuilder();
            foreach (var pair in kinds.OrderByDescending(p => p.Value).Take(top))
                text.Append(pair.Key).Append('=').Append(pair.Value).Append(' ');
            return text.ToString().TrimEnd();
        }
    }
}
