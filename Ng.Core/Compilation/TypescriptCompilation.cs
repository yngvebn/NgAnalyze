using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zu.TypeScript;
using Zu.TypeScript.Change;
using Zu.TypeScript.TsTypes;

namespace Ng.Core
{
    public class TypescriptCompilation
    {

        private Node _root => this.Ast.RootNode;

        //public IEnumerable<ParameterDeclaration> Dependencies => Classes.Children.OfType<ConstructorDeclaration>()?
        //    .SingleOrDefault()?.Children.OfType<ParameterDeclaration>();

        public IEnumerable<ClassDeclaration> _classDeclarations => _root.Children.OfType<ClassDeclaration>();
        public IEnumerable<TypeScriptClass> Classes =>
            _classDeclarations.Select(c => TypeScriptClass.Create(c, this));
        public IEnumerable<string> ClassName => _classDeclarations.Select(c => c.Name.GetText());

        public static TypescriptCompilation CreateCompiled(string fileName, string root)
        {
            return new TypescriptCompilation(fileName, root);
        }

        public TypescriptCompilation(string fileName, string root)
        {
            Ast = new TypeScriptAST(File.ReadAllText(fileName), fileName);
            Root = root;
            FileName = fileName;

        }

        public Imports FindImport(TypeScriptClass cls)
        {
            return Imports
                .Where(i => i.IsLocalImport)
                .SingleOrDefault(i => (i.AbsolutePath.Equals(cls.FileName) && i.ImportedModules.Any(m => m.Name.Equals(cls.Name))) ||
                                      i.AbsolutePath.Equals(cls.FileName) && i.ImportedModules.Any(m => m.Name.Equals("*"))
                );
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
}