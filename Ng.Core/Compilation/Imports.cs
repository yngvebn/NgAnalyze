using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Zu.TypeScript.Change;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    public class Imports
    {
        private readonly string _sourceFileName;
        private readonly string _root;
        public ImportDeclaration ImportDeclaration { get; }
        private StringLiteral _pathNode;
        private string _path;
        public string[] Modules => ImportedModules.Select(i => i.Name).ToArray();
        public string FilePath => _path;

        public void Add(IEnumerable<ImportedModule> newImports)
        {
            this.ImportedModules.AddRange(newImports);
        }

        public void Remove(IEnumerable<ImportedModule> newImports)
        {
            this.ImportedModules = ImportedModules.Where(i => !newImports.Any(removed => removed.Equals(i))).ToList();
        }

        public string Serialize()
        {
            return $"import {{ {string.Join(", ", Modules.OrderBy(c => c))} }} from '{_path}';";
        }

        public string RelativeImportPath
        {
            get
            {
                var importPath = new Uri(_sourceFileName, UriKind.Absolute).MakeRelativeUri(new Uri(AbsolutePath, UriKind.Absolute)).ToString();
                importPath = Regex.Replace(importPath, @"\.ts$", "");
                if (!importPath.StartsWith(".")) importPath = "./" + importPath;

                return importPath;
            }
        }

        public string AbsolutePath
        {
            get
            {
                var fromAbsolute = $"{Path.Combine(_root, FilePath.Replace("/", "\\"))}.ts";
                if (File.Exists(fromAbsolute)) return fromAbsolute;

                var fromRelative = Path.GetFullPath($"{Path.Combine(Path.GetDirectoryName(_sourceFileName), FilePath.Replace("/", "\\"))}.ts");
                if (File.Exists(fromRelative)) return fromRelative;
                return null;

            }
        }

        public bool IsLocalImport => File.Exists(AbsolutePath);

        public List<ImportedModule> ImportedModules { get; private set; }

        public Imports(string sourceFileName, string root, ImportDeclaration importDeclaration)
        {
            _sourceFileName = sourceFileName;
            _root = root;
            ImportDeclaration = importDeclaration;
            _pathNode = importDeclaration.Children.OfType<StringLiteral>().First();
            _path = _pathNode.Text;
            var namedImports = importDeclaration.GetDescendants(false).OfType<ImportSpecifier>().Select(i => new ImportedModule(i, _pathNode)).ToArray();
            var namespaceImports = importDeclaration.GetDescendants(false).OfType<NamespaceImport>()
                .Select(i => new ImportedModule(i, _pathNode)).ToArray();
            var allImports = new List<ImportedModule>();
            allImports.AddRange(namedImports);
            allImports.AddRange(namespaceImports);
            ImportedModules = allImports;
        }


        public Imports(string sourceFileName, string root, params ImportedModule[] imports)
        {
            _sourceFileName = sourceFileName;
            _root = root;
            _path = imports.First().Path;
            var allImports = new List<ImportedModule>();
            allImports.AddRange(imports);
            ImportedModules = allImports;
        }


        public string GetAbsoluteImport()
        {
            return Regex.Replace(AbsolutePath.Replace(_root, "").TrimStart('\\', '/').Replace("\\", "/"), @"\.ts$", "");
        }

        public void MakeAbsoluteImport(ChangeAST change, bool skipSelfAndSubdirectories)
        {
            try
            {
                if (skipSelfAndSubdirectories && RelativeImportPath.StartsWith("./") && !RelativeImportPath.Contains("..")) return;
                if (skipSelfAndSubdirectories && !RelativeImportPath.Contains("../")) return;

                change.ChangeNode(_pathNode, $" '{GetAbsoluteImport()}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        public void MakeRelativeImports(ChangeAST change, int maxNumberOfRelativeLevels)
        {
            try
            {
                if (Regex.Matches(RelativeImportPath, @"\.\.").Count <= maxNumberOfRelativeLevels)
                {
                    change.ChangeNode(_pathNode, $" '{RelativeImportPath}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

    }
}