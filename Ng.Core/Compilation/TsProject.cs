using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ng.Contracts;
using Zu.TypeScript;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    public class TsProject
    {
        public List<TypescriptCompilation> Compiled = new List<TypescriptCompilation>();
        public List<TypeScriptAST> Files = new List<TypeScriptAST>();

        private readonly TsConfig _tsconfig;

        public static TsProject Load(TsConfig config)
        {
            return new TsProject(config);
        }

        private TsProject(TsConfig tsconfig)
        {
            _tsconfig = tsconfig;

            var projectFiles = GetProjectFiles().ToList();
            int i = 0;
            Console.WriteLine("Loading...");
            Files = projectFiles.Select(file =>
            {
                i++;
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine($"Loading {i}/{projectFiles.Count} - {file}");
                return new TypeScriptAST(file, Path.GetFileName(file));
            }).ToList();
            Console.WriteLine("Compiling...");
            i = 0;

            Compiled = projectFiles.Select(file =>
                {
                    i++;
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.WriteLine($"Compiling {i}/{projectFiles.Count} - {file}");
                    return TypescriptCompilation.CreateCompiled(file, tsconfig.RootDir);
                })
                .ToList();
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

        public IEnumerable<Usage> FindUsages(TypeScriptClass first)
        {
            var compilations =  this.Compiled.Where(c => c.Imports != null && 
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
                    var usages = compilation.Ast.RootNode.GetDescendants().OfType<Identifier>()
                        .Where(i => IsNamespaceImportedClass(first, i, namespaceImport)).Select(p => p.GetAncestors().OfType<CallExpression>().FirstOrDefault()).Where(f => f != null);
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
                    var usages = compilation.Ast.RootNode.GetDescendants().OfType<Identifier>()
                        .Where(i => i.IdentifierStr.Equals(first.Name)).Select(p => p.GetAncestors().OfType<CallExpression>().FirstOrDefault()).Where(f => f != null);
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
}