using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.Common;
using System.Data;

namespace EmulatorLauncher.Common
{
    public static class SQLiteInteropManager
    {
        public static void InstallSQLiteInteropDll()
        {
            string dllName = Path.Combine(Path.GetDirectoryName(typeof(SQLiteInteropManager).Assembly.Location), "SQLite.Interop.dll");
            int platform = IntPtr.Size;

            if (File.Exists(dllName) && Kernel32.IsX64(dllName) == (IntPtr.Size == 8))
                return;

            if (File.Exists(dllName))
            {
                try { File.Delete(dllName); }
                catch { }
            }

            FileTools.ExtractGZipBytes(IntPtr.Size == 8 ? Properties.Resources.SQLite_Interop_x64 : Properties.Resources.SQLite_Interop_x86, dllName);
        }

        public static T[] ReadObjects<T>(this IDataReader reader) where T : new()
        {
            var cols = GetColumnIndices(reader);

            var properties = new Dictionary<int,System.Reflection.PropertyInfo>();

            foreach (var prop in typeof(T).GetProperties())
            {
                int index;
                if (cols.TryGetValue(prop.Name, out index))
                    properties.Add(index, prop);
            }

            List<T> ret = new List<T>();

            if (properties.Count == 0)
                return ret.ToArray();

            while (reader.Read())
            {
                T instance = new T();

                foreach (var prop in properties)
                {
                    object value = reader.GetValue(prop.Key);
                    if (value == System.DBNull.Value || value == null)
                        continue;

                    try 
                    { 
                        prop.Value.SetValue(instance, Convert.ChangeType(value, prop.Value.PropertyType), null); 
                    }
                    catch { }
                }

                ret.Add(instance);
            }

            return ret.ToArray();
        }

        static Dictionary<string, int> GetColumnIndices(IDataReader reader)
        {
            Dictionary<string, int> columnIndices = new Dictionary<string, int>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                string columnName = reader.GetName(i);
                columnIndices.Add(columnName, i);
            }

            return columnIndices;
        }

    }
}
