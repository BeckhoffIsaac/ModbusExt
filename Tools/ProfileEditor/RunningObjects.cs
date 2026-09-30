using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ModbusExt.ProfileEditor;

static class RunningObjects
{
    [DllImport("ole32.dll")] static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);
    [DllImport("ole32.dll")] static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable pprot);

    public static List<(string Name, object Obj)> ListDte()
    {
        var result = new List<(string, object)>();
        GetRunningObjectTable(0, out var rot);
        rot.EnumRunning(out var enumMoniker);
        var monikers = new IMoniker[1];
        while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
        {
            CreateBindCtx(0, out var ctx);
            monikers[0].GetDisplayName(ctx, null, out var name);
            if (name.Contains(".DTE.", StringComparison.OrdinalIgnoreCase))
            {
                rot.GetObject(monikers[0], out var obj);
                result.Add((name, obj));
            }
        }
        return result;
    }
}