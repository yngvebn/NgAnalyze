using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ng.Contracts;
using Zu.TypeScript;
using Zu.TypeScript.Change;
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

        public IEnumerable<object> FindUsages(TypeScriptClass first)
        {
            return this.Compiled.Where(c => c.Imports != null && 
                                            c.Imports
                                                .Where(i => i.IsLocalImport)
                                                .Any(i => i.AbsolutePath.Equals(first.FileName) && i.ImportedModules.Any(m => m.Name.Equals(first.Name))));
        }
    }

    public class TypescriptCompilation
    {

        private Node _root => this.Ast.RootNode;

        //public IEnumerable<ParameterDeclaration> Dependencies => Classes.Children.OfType<ConstructorDeclaration>()?
        //    .SingleOrDefault()?.Children.OfType<ParameterDeclaration>();

        public IEnumerable<ClassDeclaration> _classDeclarations => _root.Children.OfType<ClassDeclaration>();
        public IEnumerable<TypeScriptClass> Classes =>
            _classDeclarations.Select(c => TypeScriptClass.Create(c, this.Imports));
        public IEnumerable<string> ClassName => _classDeclarations.Select(c => c.Name.GetText());

        public static TypescriptCompilation CreateCompiled(string fileName, string root)
        {
            return new TypescriptCompilation()
            {
                Root = root,
                FileName = fileName,
                Ast = new TypeScriptAST(File.ReadAllText(fileName), fileName)
            };
        }

        public string Root { get; set; }

        public string FileName { get; set; }

        public TypeScriptAST Ast { get; set; }

        public Imports[] Imports
        {
            get { return _root.Children.OfType<ImportDeclaration>().Select(declaration => new Imports(FileName, Root, declaration)).ToArray(); }
        }

        public bool HasDecorator(string component)
        {
            return _root.GetDescendants().OfType<Decorator>()?.Any(d => d.Children.First().IdentifierStr.Equals("TypescriptEntity")) ?? false;
        }

        public ChangeAST MakeAbsoluteImports(bool skipSelfAndSubdirectories = false)
        {
            ChangeAST change = new ChangeAST();
            foreach (var import in Imports.Where(i => i.IsLocalImport))
            {
                import.MakeAbsoluteImport(change, skipSelfAndSubdirectories);
            }
            return change;
        }

        public ChangeAST MakeRelativeImports(int maxNumberOfRelativeLevels = Int32.MaxValue)
        {
            ChangeAST change = new ChangeAST();
            foreach (var import in Imports.Where(i => i.IsLocalImport))
            {
                import.MakeRelativeImports(change, maxNumberOfRelativeLevels);
            }
            return change;

        }
    }
    [DebuggerDisplay("{Name}")]
    public class TypeScriptClass
    {
        public string FileName => ((SourceFile) declaration.Parent).FileName;
        public bool Exported => declaration.Children.OfType<Modifier>().Any(m => m.Kind == SyntaxKind.ExportKeyword);
        public string Name => declaration.Children.OfType<Identifier>().First().GetText();

        public IEnumerable<ImportedModule> Inherits => this.declaration.Children.OfType<HeritageClause>()
            .Select(SelectImportFromInheritage);

        private ImportedModule SelectImportFromInheritage(HeritageClause heritageClause)
        {
            var importedModules = _imports.SelectMany(i => i.ImportedModules);
            if (heritageClause.GetDescendants(false).OfType<PropertyAccessExpression>().Any())
            {
                return importedModules.Where(i => !string.IsNullOrEmpty(i.As))
                    .FirstOrDefault(i => i.As.Equals(heritageClause.GetDescendants(false).OfType<PropertyAccessExpression>().First().IdentifierStr));
            }
            else
            {
                var locatedModule = importedModules.FirstOrDefault(i => i.Name.Equals(heritageClause.First.IdentifierStr));
                if (locatedModule == null)
                {
                    Debugger.Break();
                }
                return locatedModule;
            }
        }

        public IEnumerable<TypeScriptProperty> Properties =>
            declaration.Children.OfType<PropertyDeclaration>().Select(TypeScriptProperty.Create);

        private ClassDeclaration declaration;
        private readonly Imports[] _imports;

        public TypeScriptClass(ClassDeclaration declaration, Imports[] imports)
        {
            this.declaration = declaration;
            _imports = imports;
        }

        public static TypeScriptClass Create(ClassDeclaration declaration, Imports[] imports)
        {
            return new TypeScriptClass(declaration, imports);
        }
    }

    public class TypeScriptProperty
    {
        private PropertyDeclaration arg;

        public TypeScriptProperty(PropertyDeclaration arg)
        {
            this.arg = arg;
        }

        public static TypeScriptProperty Create(PropertyDeclaration arg)
        {
            return new TypeScriptProperty(arg);
        }
    }
    
    public class Imports
    {
        private readonly string _sourceFileName;
        private readonly string _root;
        private readonly ImportDeclaration _importDeclaration;
        private StringLiteral _path;
        public string[] Modules => ImportedModules.Select(i => i.Name).ToArray();
        public string FilePath => _path.Text;

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

        public ImportedModule[] ImportedModules { get; private set; }

        public Imports(string sourceFileName, string root, ImportDeclaration importDeclaration)
        {
            _sourceFileName = sourceFileName;
            _root = root;
            _importDeclaration = importDeclaration;
            _path = importDeclaration.Children.OfType<StringLiteral>().First();
            var namedImports = importDeclaration.GetDescendants(false).OfType<ImportSpecifier>().Select(i => new ImportedModule(i, _path)).ToArray();
            var namespaceImports = importDeclaration.GetDescendants(false).OfType<NamespaceImport>()
                .Select(i => new ImportedModule(i, _path)).ToArray();
            var allImports = new List<ImportedModule>();
            allImports.AddRange(namedImports);
            allImports.AddRange(namespaceImports);
            ImportedModules = allImports.ToArray();
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

                change.ChangeNode(_path, $" '{GetAbsoluteImport()}'");
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
                    change.ChangeNode(_path, $" '{RelativeImportPath}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }
    public class ImportedModule : IEqualityComparer<ImportedModule>, IEquatable<ImportedModule>
    {
        private ImportSpecifier _importSpecifier;
        private StringLiteral _path;
        private NamespaceImport i;
        public string As { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }


        public ImportedModule(ImportSpecifier importSpecifier, StringLiteral path)
        {
            _importSpecifier = importSpecifier;
            _path = path;

            Path = _path.GetText().Replace("'", "");
            Name = _importSpecifier.GetText();
        }

        public ImportedModule(string type, string from)
        {
            Name = type;
            Path = from;
        }

        public ImportedModule(NamespaceImport i, StringLiteral path)
        {
            Name = "*";
            As = i.IdentifierStr;
            Path = path.GetText().Replace("'", "");
        }

        public bool Equals(ImportedModule x, ImportedModule y)
        {
            return x.Name.Equals(y.Name) && x.Path.Equals(y.Path);

        }

        public int GetHashCode(ImportedModule obj)
        {
            return $"{obj.Name}#{obj.Path}".GetHashCode();
        }


        public bool Equals(ImportedModule other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(As, other.As) && string.Equals(Name, other.Name) && string.Equals(Path, other.Path);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ImportedModule) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (As != null ? As.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Path != null ? Path.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}