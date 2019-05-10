using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AutoMapper;
using Zu.TypeScript;
using Zu.TypeScript.TsTypes;

namespace Ng.Core.Compilation.v2
{
    public class TypescriptFile
    {
        public TypeScriptAST Ast { get; set; }
        private Node RootNode => this.Ast.RootNode;
        public string RootDirectory { get; set; }


        public string Filename { get; }
        public List<TypescriptConstruct> Constructs { get; set; }

        public static TypescriptFile Load(string file, string rootDir)
        {
            
            return new TypescriptFile(file, rootDir);
        }

        private TypescriptFile(string filename, string rootDirectory)
        {
            Filename = filename;
            Ast = new TypeScriptAST(File.ReadAllText(filename), filename);
            RootDirectory = rootDirectory;
            Constructs = TypescriptConstructFactory.Resolve(RootNode).ToList();
        }
    }

    public enum TypescriptModifier
    {
        Public,
        Private
    }

  
    public class TypescriptConstructFactory
    {
        public static IEnumerable<TypescriptConstruct> Resolve(Node rootNode)
        {
            var mapMethod = typeof(Mapper)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(m => m.Name.Equals("Map"))
                .Where(m => m.GetGenericArguments().Length == 1)
                .FirstOrDefault(m => m.GetParameters().Length == 1);

            foreach (var child in rootNode.Children)
            {
                var mapper = Mapper.Configuration.GetAllTypeMaps()
                    .FirstOrDefault(map => map.SourceType == child.GetType());
                if (mapper == null)
                {
                    Console.WriteLine($"No mapping for ${child.GetType()}");
                    continue;
                }

                var genericMethod = mapMethod.MakeGenericMethod(mapper.DestinationType);
                var mapped = genericMethod.Invoke(null, new[] { child });
                yield return mapped as TypescriptConstruct;
            }
        }
    }
    public class Mapping
    {
        public static void Configuration(IMapperConfigurationExpression obj, string rootDir)
        {
            obj.CreateMap<ImportDeclaration, TypescriptImport>()
                .ConstructUsing(declaration => TypescriptImport.Create(declaration, rootDir));

            obj.CreateMap<ClassDeclaration, TypescriptClass>()
                .ConstructUsing(declaration => TypescriptClass.Create(declaration, rootDir));
        }
    }
    [DebuggerDisplay("{Node.GetText()}")]
    public class TypescriptConstruct
    {
        public SourceFile ContainingSourceFile { get; }
        public string ContainingFileName => ContainingSourceFile.FileName;
        public TypescriptModifier Modifier { get; set; }
        public bool IsExported => Node.Children.OfType<Modifier>().Any(m => m.Kind == SyntaxKind.ExportKeyword);
        public Node Node { get; set; }
        public string RootDir { get; }
        public List<TypescriptConstruct> Constructs { get; set; }

        protected TypescriptConstruct(Node node, string rootDir)
        {
            Node = node;
            RootDir = rootDir;

            var currentParent = Node.Parent;
            while (!(currentParent is SourceFile) && currentParent != null)
            {

                currentParent = currentParent.Parent;
            }

            ContainingSourceFile = currentParent as SourceFile;

        }
    }

    public class TypescriptProperty : TypescriptConstruct
    {
        public TypescriptProperty(ImportDeclaration node, string rootDir): base(node, rootDir)
        {
        }

        public static TypescriptProperty Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptProperty(node, rootDir);
        }
    }

    public class TypescriptMethod : TypescriptConstruct
    {
        public TypescriptMethod(ImportDeclaration node, string rootDir): base(node, rootDir)
        {
        }

        public static TypescriptMethod Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptMethod(node, rootDir);
        }
    }

    public class TypescriptInterface : TypescriptConstruct
    {
        public TypescriptInterface(ImportDeclaration node, string rootDir): base(node, rootDir)
        {
        }

        public static TypescriptInterface Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptInterface(node, rootDir);
        }

    }

    public class TypescriptConst : TypescriptConstruct
    {
        public TypescriptConst(ImportDeclaration node, string rootDir): base(node, rootDir)
        {
        }

        public static TypescriptConst Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptConst(node, rootDir);
        }
    }

    public class TypescriptClass : TypescriptConstruct
    {
        public string Name => Node.Children.OfType<Identifier>().First().GetText();

        public TypescriptClass(ClassDeclaration node, string rootDir): base(node, rootDir)
        {
        }

        public static TypescriptClass Create(ClassDeclaration node, string rootDir)
        {
            return new TypescriptClass(node, rootDir);
        }
    }

    public class TypescriptEnum : TypescriptConstruct
    {
        public TypescriptEnum(ImportDeclaration node, string rootDir): base(node, rootDir)
        {
        }

        public static TypescriptEnum Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptEnum(node, rootDir);
        }
    }
    
    public class TypescriptImport : TypescriptConstruct
    {
        private readonly StringLiteral _path;
        public string FilePath => _path.Text;
        public List<ImportedModule> ImportedModules { get; }
        public bool IsLocalImport => File.Exists(AbsolutePath);

        public TypescriptImport(ImportDeclaration node, string rootDir): base(node, rootDir)
        {
            _path = node.Children.OfType<StringLiteral>().First();

            var namedImports = node.GetDescendants(false).OfType<ImportSpecifier>().Select(i => new ImportedModule(i, _path)).ToArray();
            var namespaceImports = node.GetDescendants(false).OfType<NamespaceImport>()
                .Select(i => new ImportedModule(i, _path)).ToArray();
            var allImports = new List<ImportedModule>();
            allImports.AddRange(namedImports);
            allImports.AddRange(namespaceImports);
            ImportedModules = allImports;

        }

        public static TypescriptImport Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptImport(node, rootDir);
        }

        public string RelativeImportPath
        {
            get
            {
                var importPath = new Uri(ContainingFileName, UriKind.Absolute).MakeRelativeUri(new Uri(AbsolutePath, UriKind.Absolute)).ToString();
                importPath = Regex.Replace(importPath, @"\.ts$", "");
                if (!importPath.StartsWith(".")) importPath = "./" + importPath;

                return importPath;
            }
        }

        public string AbsolutePath
        {
            get
            {
                var fromAbsolute = $"{Path.Combine(RootDir, FilePath.Replace("/", "\\"))}.ts";
                if (File.Exists(fromAbsolute)) return fromAbsolute;

                var fromRelative = Path.GetFullPath($"{Path.Combine(Path.GetDirectoryName(ContainingFileName), FilePath.Replace("/", "\\"))}.ts");
                if (File.Exists(fromRelative)) return fromRelative;

                return null;
            }
        }
    }
}
