using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Angular;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Ng.Contracts;
using Ng.Core.Compilation.v2;
using Zu.TypeScript;
using Zu.TypeScript.TsTypes;
using Token = Ng.Core.Compilation.v2.Token;

namespace Ng.Core
{
    public static class TypespecificExtensions
    {
        public static IEnumerable<Usage> FindUsages(this TsProject<TypescriptCompilation> tsProject, ImportedModule first)
        {
            var compilations = tsProject.Compiled.Where(c => c.Imports != null &&
                                           c.Imports.Any(i => i.ImportedModules.Contains(first))).ToList();
            List<Usage> positions = new List<Usage>();

            foreach (var compilation in compilations)
            {
                var potentialUsages = compilation.Ast.RootNode.GetDescendants().OfType<Identifier>()
                    .Where(i => i.IdentifierStr.Equals(first.Name))
                    .Where(i => !i.GetAncestors().Any(ancestor => ancestor is ImportDeclaration));
                positions.AddRange(potentialUsages.Select(usage => new Usage
                {
                    Compilation = compilation,
                    Node = usage
                }));
            }

            return positions;
        }

        public static IEnumerable<Usage> FindUsages(this TsProject<TypescriptCompilation> tsProject, TypeAliasDefinition first)
        {
            string fileName = (first.Node.Ast.RootNode as SourceFile).FileName;
            var compilations = tsProject.Compiled.Where(c => c.Imports != null &&
                                           c.Imports
                                               .Where(i => i.IsLocalImport)
                                               .Any(i => (i.AbsolutePath.Equals(fileName) && i.ImportedModules.Any(m => m.Name.Equals(first.Name))) ||
                                                         i.AbsolutePath.Equals(fileName) && i.ImportedModules.Any(m => m.Name.Equals("*"))
                                                         )).ToList();
            List<Usage> positions = new List<Usage>();

            foreach (var compilation in compilations)
            {
                var import = compilation.FindImport(first.Name, fileName);
                if (import == null) continue;
                if (import.ImportedModules.Count == 1 && import.ImportedModules.First().IsNamespaceImport)
                {
                    var namespaceImport = import.ImportedModules.First();
                    var potentialUsages = compilation.Ast.RootNode.GetDescendants().OfType<Identifier>()
                        .Where(i => IsNamespaceImportedClass(first.Name, i, namespaceImport));

                    var usages = potentialUsages.Where(p => p.Parent is NewExpression || p.Parent.Parent is NewExpression).Select(p => p.Parent is NewExpression ? p.Parent : p.Parent.Parent);
                    positions.AddRange(usages.Select(usage => new Usage
                    {
                        Compilation = compilation,
                        Node = usage,
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
                        Node = usage

                    }));
                }
            }

            return positions;
        }
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


        private static bool IsNamespaceImportedClass(TypeScriptClass classToMatch, Identifier identifier,
            ImportedModule namespaceImport)
        {
            return IsNamespaceImportedClass(classToMatch.Name, identifier, namespaceImport);
        }


        private static bool IsNamespaceImportedClass(string name, Identifier identifier, ImportedModule namespaceImport)
        {
            var firstChild = identifier.Parent.Children.FirstOrDefault();
            return identifier.IdentifierStr.Equals(name) && firstChild != null && firstChild.IdentifierStr.Equals(namespaceImport.As);
        }
    }

    public class FindAllReferences : ITraverseTree<TypescriptFile, IEnumerable<TypescriptConstruct>>
    {
        private readonly Token _token;

        public FindAllReferences(Token token)
        {
            _token = token;
        }

        public IEnumerable<TypescriptConstruct> Traverse(IEnumerable<TypescriptFile> input)
        {
            return input.SelectMany(i => i.RunTraverser(new FindAllReferencesInFile(_token)));
        }
    }

    public class ExtractAllComponentOptions: ITraverseTree<TypescriptFile, IEnumerable<ComponentOptions>>
    {
        public IEnumerable<ComponentOptions> Traverse(IEnumerable<TypescriptFile> input)
        {
            return input.SelectMany(i => i.RunTraverser(new ExtractAllComponentOptionsFromFile()));
        }
    }

    public class ExtractAllComponentOptionsFromFile : ITraverseTypescriptFile<IEnumerable<ComponentOptions>>
    {
        public IEnumerable<ComponentOptions> Traverse(TypescriptFile file)
        {
            return file.RootConstruct.Flatten()
                .Where(c => c is TypescriptDecorator && c.Name.Equals("Component"))
                .OfType<TypescriptDecorator>().Select(d => d.Options as ComponentOptions);
        }
    }

    public class ExtractHtmlDocuments : ITraverseTree<TypescriptFile, IEnumerable<HtmlDocument>>
    {
        public IEnumerable<HtmlDocument> Traverse(IEnumerable<TypescriptFile> input)
        {
            return input.SelectMany(i => i.RunTraverser(new ExtractHtmlDocumentsFromFile()));
        }

    }

    public class ExtractHtmlDocumentsFromFile : ITraverseTypescriptFile<IEnumerable<HtmlDocument>>
    {
        public IEnumerable<HtmlDocument> Traverse(TypescriptFile file)
        {
            var components = file.RootConstruct.Flatten().Where(c => c is TypescriptDecorator && c.Name.Equals("Component"));
            return components.OfType<TypescriptDecorator>().Select(this.ExtractHtmlDocument);
        }

        private HtmlDocument ExtractHtmlDocument(TypescriptDecorator decorator)
        {
            var componentOptions = decorator.Options as ComponentOptions;
            var doc = new HtmlDocument();

            if (!string.IsNullOrEmpty(componentOptions.Template))
            {
                doc.LoadHtml(componentOptions.Template);
            } else if (!string.IsNullOrEmpty(componentOptions.TemplateUrl))
            {
                
                var fromRelative = Path.GetFullPath($"{Path.Combine(Path.GetDirectoryName(decorator.ContainingFileName), componentOptions.TemplateUrl.Replace("/", "\\"))}");
                if (!File.Exists(fromRelative))
                {
                    Console.WriteLine($"Template {fromRelative} for component {decorator.ContainingFileName} not found");
                };
                
                doc.LoadHtml(File.ReadAllText(fromRelative));

            }

            return doc;

        }
    }

    public class FindAllReferencesInFile : ITraverseTypescriptFile<IEnumerable<TypescriptConstruct>>
    {
        private readonly Token _token;

        public FindAllReferencesInFile(Token token)
        {
            _token = token;
        }

        public IEnumerable<TypescriptConstruct> Traverse(TypescriptFile file)
        {
            return file.RootConstruct.Flatten().Where(c => c.ExportToken != null).Where(c => c.ExportToken.Equals(_token));
        }
    }

    public interface ITraverseTypescriptFile<TOut>
    {
        TOut Traverse(TypescriptFile file);
    }

    public interface ITraverseTree<T, TOut>
    {
        TOut Traverse(IEnumerable<T> input);
    }

    public class TsProject<T>
    {
        public List<T> Compiled = new List<T>();
        [JsonIgnore]
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

        public TOut RunTraverser<TOut>(ITraverseTree<T, TOut> traverser)
        {
            return traverser.Traverse(this.Compiled);
        }


    }
}