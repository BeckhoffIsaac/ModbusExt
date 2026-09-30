namespace ModbusExt.ProfileEditor;

sealed class ProfilePou
{
    public required string PlcName { get; init; }
    public required string Path { get; init; }     // TIPC^...^FB_Mb_X — usable with LookupTreeItem
    public required dynamic Item { get; init; }
    public string Name => Path[(Path.LastIndexOf('^') + 1)..];
}

sealed class XaeSession
{
    // PLC tree item sub-types for CreateChild (TwinCAT Automation Interface)
    public static class SubType
    {
        public const int FunctionBlock = 604;
        public const int Method        = 609;
    }

    public sealed record PouTexts(string Declaration, string Body, string? Register);

    readonly dynamic _dte;
    readonly dynamic _sysMan;

    public XaeSession(object dte)
    {
        _dte = dte;
        dynamic projects = _dte.Solution.Projects;
        int n = projects.Count;
        for (int i = 1; i <= n; i++)
        {
            dynamic proj = projects.Item(i);
            try
            {
                dynamic obj = proj.Object;
                if (obj == null) continue;
                obj.LookupTreeItem("TIPC");          // Only the TwinCAT project answers this
                _sysMan = obj;
                return;
            }
            catch { }
        }
        throw new InvalidOperationException("No TwinCAT project found in the open solution.");
    }

    public string SolutionName => (string)_dte.Solution.FullName;

    // ---- Discovery ----

    public List<ProfilePou> FindProfiles()
    {
        var found = new List<ProfilePou>();
        dynamic plcRoot = _sysMan.LookupTreeItem("TIPC");
        int n = plcRoot.ChildCount;
        for (int i = 1; i <= n; i++)
        {
            string plcName = plcRoot.Child[i].Name;
            dynamic project;
            try { project = _sysMan.LookupTreeItem($"TIPC^{plcName}^{plcName} Project"); }
            catch { continue; }                     // PLC node without a project
            Walk(project, plcName, found);
        }
        return found;
    }

    static void Walk(dynamic item, string plcName, List<ProfilePou> found)
    {
        int n = item.ChildCount;
        for (int i = 1; i <= n; i++)
        {
            dynamic child = item.Child[i];
            string? decl = TryDeclaration(child);
            if (!string.IsNullOrWhiteSpace(decl)
                && decl.Contains("FUNCTION_BLOCK", StringComparison.OrdinalIgnoreCase)
                && decl.Contains("EXTENDS FB_ModbusDevice", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(new ProfilePou { PlcName = plcName, Path = (string)child.PathName, Item = child });
                continue;
            }
            Walk(child, plcName, found);            // Folders, and harmlessly the methods of non-profile POUs
        }
    }

    static string? TryDeclaration(dynamic item)
    {
        try { return (string)item.DeclarationText; }
        catch { return null; }
    }

    public string DebugTree()
    {
        var sb = new System.Text.StringBuilder();
        dynamic plcRoot = _sysMan.LookupTreeItem("TIPC");
        Dump(plcRoot, 0, 3, sb);
        return sb.ToString();
    }

    static void Dump(dynamic item, int depth, int maxDepth, System.Text.StringBuilder sb)
    {
        sb.Append(' ', depth * 2).AppendLine((string)item.Name);
        if (depth >= maxDepth) return;
        int n = item.ChildCount;
        for (int i = 1; i <= n; i++) Dump(item.Child[i], depth + 1, maxDepth, sb);
    }

    // ---- Read / write ----

    public static PouTexts Read(ProfilePou p) =>
        new((string)p.Item.DeclarationText, (string)p.Item.ImplementationText, MethodImplementation(p.Item, "Register"));

    public static void Write(ProfilePou p, string declaration, string body, string register)
    {
        p.Item.DeclarationText    = declaration;
        p.Item.ImplementationText = body;
        dynamic? reg = FindChild(p.Item, "Register");
        if (reg == null)
        {
            reg = p.Item.CreateChild("Register", SubType.Method, "", null);
            reg.DeclarationText = "METHOD PRIVATE Register\r\n";
        }
        reg.ImplementationText = register;
    }

    public static string? MethodImplementation(dynamic pou, string methodName)
    {
        dynamic? m = FindChild(pou, methodName);
        return m == null ? null : (string)m.ImplementationText;
    }

    static dynamic? FindChild(dynamic item, string name)
    {
        int n = item.ChildCount;
        for (int i = 1; i <= n; i++)
        {
            dynamic child = item.Child[i];
            if (string.Equals((string)child.Name, name, StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }
}