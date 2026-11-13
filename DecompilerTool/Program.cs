using System;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace DecompilerTool;

class Program
{
    static int Main(string[] args)
    {
        // Usage: DecompilerTool <input.dll> <outputDir> [typeNameFilter]
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: DecompilerTool <input.dll> <outputDir> [typeNameFilter]");
            return 1;
        }

        string input = Path.GetFullPath(args[0]);
        string outDir = Path.GetFullPath(args[1]);
        string filter = args.Length > 2 ? args[2] : null;

        var settings = new DecompilerSettings(LanguageVersion.CSharp10_0)
        {
            ThrowOnAssemblyResolveErrors = false,
        };

        var decompiler = new CSharpDecompiler(input, settings);

        Directory.CreateDirectory(outDir);

        var typeSystem = decompiler.TypeSystem;
        var allTypes = typeSystem.MainModule.TypeDefinitions
            .Where(t => !t.IsCompilerGenerated() && !t.Name.StartsWith("<"))
            .ToList();

        int count = 0;
        foreach (var type in allTypes)
        {
            string fullName = type.FullName;
            if (filter != null && !fullName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                string code = decompiler.DecompileTypeAsString(new FullTypeName(type.FullName));

                string safeName = fullName.Replace('`', '_').Replace('/', '.');
                string subDir = Path.Combine(outDir, type.Namespace.Replace('.', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(subDir);
                string file = Path.Combine(subDir, safeName.Replace('.', '_') + ".cs");
                File.WriteAllText(file, code);
                count++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {fullName}: {ex.Message}");
            }
        }

        Console.WriteLine($"Decompiled {count} types -> {outDir}");
        return 0;
    }
}
