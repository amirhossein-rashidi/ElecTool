using System;
using System.IO;
using System.Reflection;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// Makes sure ElecTool.Core.dll (next to this DLL) is found no matter how AutoCAD loaded the plugin.
    /// Only assemblies whose name starts with "ElecTool." are handled.
    /// </summary>
    internal static class AssemblyResolver
    {
        private static bool _installed;
        private static string _baseDirectory = string.Empty;

        public static void Install()
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            _baseDirectory = Path.GetDirectoryName(typeof(AssemblyResolver).Assembly.Location) ?? string.Empty;
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        }

        private static Assembly? OnResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name ?? string.Empty;
                if (!name.StartsWith("ElecTool.", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string candidate = Path.Combine(_baseDirectory, name + ".dll");
                return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
