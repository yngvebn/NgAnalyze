using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ng.Contracts;
using Zu.TypeScript;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    public static class TypespecificExtensions
    {
        public static IEnumerable<Usage> FindUsages(this TsProject<TypescriptCompilation> tsProject, TypeScriptClass first)
        {
            var compilations = tsProject.Compiled.Where(c => c.Imports != null &&
                                           c.Imports
                                               .Where(i => i.IsLocalImport)
                                               .Any(i => (i.AbsolutePath.Equals(first.FileName) && i.ImportedModules.Any(m => m.Name.Equals(first.Name))) ||
                                                         i.AbsolutePath.Equals(first.FileName) && i.ImportedModules.Any(m => m.Name.Equals("*"))
                                                         )).ToList();
            List<Usage> positions = new List<Usage>();

            foreach (var compilation in compilations)
            {
                var import = compilation.FindImport(first);
                if (import.ImportedModules.Count == 1 && import.ImportedModules.First().IsNamespaceImport)
                {
                    var namespaceImport = import.ImportedModules.First();
                    var potentialUsages = compilation.Ast.RootNode.GetDescendants().OfType<Identifier>()
                        .Where(i => IsNamespaceImportedClass(first, i, namespaceImport));

                    var usages = potentialUsages.Where(p => p.Parent is NewExpression || p.Parent.Parent is NewExpression).Select(p => p.Parent is NewExpression ? p.Parent : p.Parent.Parent);
                    positions.AddRange(usages.Select(usage => new Usage
                    {
                        Compilation = compilation,
                        Node = usage,
                        Lookup = first,
                        IsNamespaceImport = true
                    }));
                }
                else
                {
                    var potentialUsages = compilation.Ast.RootNode.GetDescendants().OfType<Identifier>()
                        .Where(i => i.IdentifierStr.Equals(first.Name));
                    var usages = potentialUsages.Where(p => p.Parent is NewExpression || p.Parent.Parent is NewExpression).Select(p => p.Parent is NewExpression ? p.Parent : p.Parent.Parent);
                    positions.AddRange(usages.Select(usage => new Usage
                    {
                        Compilation = compilation,
                        Node = usage,
                        Lookup = first
                    }));
                }
            }

            return positions;
        }


        private static bool IsNamespaceImportedClass(TypeScriptClass classToMatch, Identifier identifier, ImportedModule namespaceImport)
        {
            var firstChild = identifier.Parent.Children.FirstOrDefault();
            return identifier.IdentifierStr.Equals(classToMatch.Name) && firstChild != null && firstChild.IdentifierStr.Equals(namespaceImport.As);
        }
    }

    public class TsProject<T>
    {
        public List<T> Compiled = new List<T>();
        public List<TypeScriptAST> Files = new List<TypeScriptAST>();

        private readonly TsConfig _tsconfig;

        public static TsProject<T> Load<T>(TsConfig config, Func<string, string, T> createCompiled)
        {
            return new TsProject<T>(config, createCompiled);
        }

        private TsProject(TsConfig tsconfig, Func<string, string, T> createCompiled)
        {
            _tsconfig = tsconfig;

            var projectFiles = GetProjectFiles().ToList();
            int i = 0;
            Console.WriteLine("Loading...");
            var files = new List<TypeScriptAST>();
            Parallel.ForEach(projectFiles, file =>
            {
                i++;
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"Loading {i}/{projectFiles.Count} - {file}");
                files.Add(new TypeScriptAST(file, Path.GetFileName(file)));
            });
            Files = files;
            Console.WriteLine("Compiling...");
            i = 0;
            var compiled = new List<T>();
            Parallel.ForEach(projectFiles, file =>
            {
                i++;
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"Compiling {i}/{projectFiles.Count} - {file}");
                compiled.Add(createCompiled(file, tsconfig.RootDir));
            });
            Compiled = compiled;
        }

        private IEnumerable<string> GetProjectFiles()
        {
            var allDirectories = Directory.GetDirectories(_tsconfig.RootDir, "*.*", SearchOption.AllDirectories)
                .Where(dir => !_tsconfig.IsExcluded(dir));
            foreach (var file in allDirectories.SelectMany(dir => Directory.GetFiles(dir, "*.ts", SearchOption.TopDirectoryOnly)
                .Where(file => !_tsconfig.IsExcluded(file))))
            {
                yield return file;
            }
        }


    }
}