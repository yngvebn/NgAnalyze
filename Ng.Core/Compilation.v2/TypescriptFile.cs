using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Angular;
using AutoMapper;
using Newtonsoft.Json;
using Zu.TypeScript;
using Zu.TypeScript.TsTypes;
using Type = System.Type;

namespace Ng.Core.Compilation.v2
{

    [DebuggerDisplay("{Filename}")]
    public class TypescriptFile
    {
        [JsonIgnore]
        public TypeScriptAST Ast { get; set; }

        [JsonIgnore]
        private Node RootNode => this.Ast.RootNode;
        public string RootDirectory { get; set; }


        public string Filename { get; }
        public TypescriptConstruct RootConstruct { get; set; }

        public static TypescriptFile Load(string file, string rootDir)
        {

            return new TypescriptFile(file, rootDir);
        }

        private TypescriptFile(string filename, string rootDirectory)
        {
            Filename = filename;
            Ast = new TypeScriptAST(File.ReadAllText(filename), filename);

            RootDirectory = rootDirectory;
            RootConstruct = new TypescriptConstruct(this.RootNode, rootDirectory);
        }

        public TOut RunTraverser<TOut>(ITraverseTypescriptFile<TOut> traverser)
        {
            return traverser.Traverse(this);
        }
    }

    public enum TypescriptModifier
    {
        Public,
        Private
    }


    public class TypescriptConstructFactory
    {
        public static TypescriptConstruct Resolve(Node child)
        {
            var mapMethod = typeof(Mapper)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(m => m.Name.Equals("Map"))
                .Where(m => m.GetGenericArguments().Length == 1)
                .FirstOrDefault(m => m.GetParameters().Length == 1);

            var mapper = Mapper.Configuration.GetAllTypeMaps()
                .FirstOrDefault(map => map.SourceType == child.GetType());

            if (mapper == null)
            {
                Console.WriteLine($"No mapping for ${child.GetType()}");

                var genericMethod = mapMethod.MakeGenericMethod(typeof(TypescriptConstruct));
                var mapped = genericMethod.Invoke(null, new[] { child as Node });
                return mapped as TypescriptConstruct;
            }
            else
            {
                var genericMethod = mapMethod.MakeGenericMethod(mapper.DestinationType);
                var mapped = genericMethod.Invoke(null, new[] { child });
                return mapped as TypescriptConstruct;
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

            obj.CreateMap<Decorator, TypescriptDecorator>()
                .ConstructUsing(declaration => TypescriptDecorator.Create(declaration, rootDir));

            obj.CreateMap<Node, TypescriptConstruct>()
                .ConstructUsing(declaration => new TypescriptConstruct(declaration, rootDir));
        }
    }
    public class Token : IEqualityComparer<Token>, IEquatable<Token>
    {
        public string AbsolutePath { get; }
        public string Name { get; }

        public bool Equals(Token other)
        {
            return Equals(this, other);
        }

        public override string ToString()
        {
            return $"{AbsolutePath}#{Name}";
        }

        public Token(string absolutePath, string name)
        {
            AbsolutePath = absolutePath;
            Name = name;
        }
        public static Token Create(string absolutePath, string name)
        {
            return new Token(absolutePath, name);
        }

        public bool Equals(Token x, Token y)
        {
            return x != null && y != null && x.ToString().Equals(y.ToString());
        }

        public int GetHashCode(Token obj)
        {
            return obj.ToString().GetHashCode();
        }
    }



    [DebuggerDisplay("{Text} ({NodeKind})")]
    public class TypescriptConstruct
    {
        [JsonIgnore]
        public SourceFile ContainingSourceFile { get; }
        public string ContainingFileName => ContainingSourceFile.FileName;
        public TypescriptModifier Modifier { get; set; }
        public bool IsExported => Node.Children.OfType<Modifier>().Any(m => m.Kind == SyntaxKind.ExportKeyword);
        public string Name => Node.GetDescendants(false).OfType<Identifier>().FirstOrDefault()?.GetText();
        public Token ExportToken => IsExported && !string.IsNullOrEmpty(Name) ? Token.Create(ContainingFileName, Name) : null;

        [JsonIgnore]
        public Node Node { get; set; }
        public string RootDir { get; }
        public List<TypescriptConstruct> Constructs { get; set; }
        public SyntaxKind NodeKind => Node.Kind;
        public string NodeType => Node.GetType().Name;
        public string Text => Node.GetText();

        public TypescriptConstruct(Node node, string rootDir)
        {
            Node = node;
            RootDir = rootDir;

            var currentParent = Node.Parent;
            if (Node is SourceFile)
            {
                currentParent = Node;
            }
            while (!(currentParent is SourceFile) && currentParent != null)
            {
                currentParent = currentParent.Parent;
            }

            Constructs = Node.Children.Select(TypescriptConstructFactory.Resolve).ToList();
            ContainingSourceFile = currentParent as SourceFile;

        }

        public IEnumerable<TypescriptConstruct> Flatten()
        {
            return Constructs.Concat(Constructs.SelectMany(c => c.Flatten()));
        }
    }
    public class TypescriptDecorator : TypescriptConstruct
    {
        public IDecoratorOptions Options; 

        public TypescriptDecorator(Decorator node, string rootDir) : base(node, rootDir)
        {
            // (\w*?):\s?(.*?)[,\}]
            var options = string.Join(",", node.GetDescendants(false).OfKind(SyntaxKind.ObjectLiteralExpression).First()
                .GetText()
                .Replace("{", "").Replace("}", "")
                .Replace("\r\n", "")
                .Split(',')
                .Select(pair => string.Join(":", pair.Split(':').Select(s => $"\"{s.Replace("'", "").Trim()}\""))));
            
            if (Name.Equals("Component"))
            {
                Options = JsonConvert.DeserializeObject<ComponentOptions>(options);
            }
        }

        public static TypescriptDecorator Create(Decorator node, string rootDir)
        {
            return new TypescriptDecorator(node, rootDir);
        }
    }

    public class TypescriptProperty : TypescriptConstruct
    {
        public TypescriptProperty(ImportDeclaration node, string rootDir) : base(node, rootDir)
        {
        }

        public static TypescriptProperty Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptProperty(node, rootDir);
        }
    }

    public class TypescriptMethod : TypescriptConstruct
    {
        public TypescriptMethod(ImportDeclaration node, string rootDir) : base(node, rootDir)
        {
        }

        public static TypescriptMethod Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptMethod(node, rootDir);
        }
    }

    public class TypescriptInterface : TypescriptConstruct
    {
        public TypescriptInterface(ImportDeclaration node, string rootDir) : base(node, rootDir)
        {
        }

        public static TypescriptInterface Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptInterface(node, rootDir);
        }

    }

    public class TypescriptConst : TypescriptConstruct
    {
        public TypescriptConst(ImportDeclaration node, string rootDir) : base(node, rootDir)
        {
        }

        public static TypescriptConst Create(ImportDeclaration node, string rootDir)
        {
            return new TypescriptConst(node, rootDir);
        }
    }

    public class TypescriptClass : TypescriptConstruct
    {


        public TypescriptClass(ClassDeclaration node, string rootDir) : base(node, rootDir)
        {
        }

        public static TypescriptClass Create(ClassDeclaration node, string rootDir)
        {
            return new TypescriptClass(node, rootDir);
        }
    }

    public class TypescriptEnum : TypescriptConstruct
    {
        public TypescriptEnum(ImportDeclaration node, string rootDir) : base(node, rootDir)
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

        public TypescriptImport(ImportDeclaration node, string rootDir) : base(node, rootDir)
        {
            _path = node.Children.OfType<StringLiteral>().First();

            var namedImports = node.GetDescendants(false).OfType<ImportSpecifier>().Select(i => new ImportedModule(i, _path, AbsolutePath)).ToArray();
            var namespaceImports = node.GetDescendants(false).OfType<NamespaceImport>()
                .Select(i => new ImportedModule(i, _path, AbsolutePath)).ToArray();
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
                if (String.IsNullOrEmpty(AbsolutePath)) return null;
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

namespace Angular
{
    public interface IDecoratorOptions { }

    public class ComponentOptions: IDecoratorOptions
    {
        /**
* The change-detection strategy to use for this component.
*
* When a component is instantiated, Angular creates a change detector,
* which is responsible for propagating the component's bindings.
* The strategy is one of
* - `ChangeDetectionStrategy#OnPush` sets the strategy to `CheckOnce` (on demand).
* - `ChangeDetectionStrategy#Default` sets the strategy to `CheckAlways`.
*/
        [JsonIgnore]
        public object ChangeDetection { get; set; }
        /**
         * Defines the set of injectable objects that are visible to its view DOM children.
         * See [example](#injecting-a-class-with-a-view-provider).
         *
         */
        public object ViewProviders { get; set; }
        /**
         * The module ID of the module that contains the component.
         * The component must be able to resolve relative URLs for templates and styles.
         * SystemJS exposes the `__moduleName` variable within each module.
         * In CommonJS, this can  be set to `module.id`.
         *
         */
        public object ModuleId { get; set; }
        /**
         * The relative path or absolute URL of a template file for an Angular component.
         * If provided, do not supply an inline template using `template`.
         *
         */
        public string TemplateUrl { get; set; }
        /**
         * An inline template for an Angular component. If provided,
         * do not supply a template file using `templateUrl`.
         *
         */
        public string Template { get; set; }
        /**
         * One or more relative paths or absolute URLs for files containing CSS stylesheets to use
         * in this component.
         */
        public object StyleUrls { get; set; }
        /**
         * One or more inline CSS stylesheets to use
         * in this component.
         */
        public object Styles { get; set; }
        /**
         * One or more animation `trigger()` calls, containing
         * `state()` and `transition()` definitions.
         * See the [Animations guide](/guide/animations) and animations API documentation.
         *
         */
        public object Animations { get; set; }
        /**
  * An encapsulation policy for the template and CSS styles. One of { get; set; }
* - `ViewEncapsulation.Native` { get; set; }
* - `ViewEncapsulation.Emulated` { get; set; }
* emulates the native behavior.
* - `ViewEncapsulation.None` { get; set; }
         * encapsulation.
* - `ViewEncapsulation.ShadowDom` { get; set; }
         *
         * If not supplied, the value is taken from `CompilerOptions`. The default compiler option is
         * `ViewEncapsulation.Emulated`.
         *
         * If the policy is set to `ViewEncapsulation.Emulated` and the component has no `styles`
         * or `styleUrls` specified, the policy is automatically switched to `ViewEncapsulation.None`.
         */
        public object Encapsulation { get; set; }
        /**
         * Overrides the default encapsulation start and end delimiters (`{{` and `}}`)
         */
        public object Interpolation { get; set; }
        /**
         * A set of components that should be compiled along with
         * this component. For each component listed here,
         * Angular creates a {@link ComponentFactory} and stores it in the
         * {@link ComponentFactoryResolver}.
         */
        public object EntryComponents { get; set; }
        /**
         * True to preserve or false to remove potentially superfluous whitespace characters
         * from the compiled template. Whitespace characters are those matching the `\s`
         * character class in JavaScript regular expressions. Default is false, unless
         * overridden in compiler options.
         */
        public object PreserveWhitespaces { get; set; }

        /**
    * The CSS selector that identifies this directive in a template
    * and triggers instantiation of the directive.
    *
public object      * Declare as one of the following { get; set; }
    *
public object      * - `element-name` { get; set; }
public object      * - `.class` { get; set; }
public object      * - `[attribute]` { get; set; }
public object      * - `[attribute=value]` { get; set; }
public object      * - ` { get; set; }
public object      * - `selector1, selector2` { get; set; }
    *
    * Angular only allows directives to apply on CSS selectors that do not cross
    * element boundaries.
    *
    * For the following template HTML, a directive with an `input[type=text]` selector,
    * would be instantiated only on the `<input type="text">` element.
    *
    * ```html
    * <form>
    *   <input type="text">
    *   <input type="radio">
    * <form>
    * ```
    *
    */
        public string Selector { get; set; }
        /**
         * Enumerates the set of data-bound input properties for a directive
         *
         * Angular automatically updates input properties during change detection.
         * The `inputs` property defines a set of `directiveProperty` to `bindingProperty`
public object      * configuration { get; set; }
         *
         * - `directiveProperty` specifies the component property where the value is written.
         * - `bindingProperty` specifies the DOM property where the value is read from.
         *
         * When `bindingProperty` is not provided, it is assumed to be equal to `directiveProperty`.
         * @usageNotes
         *
         * ### Example
         *
         * The following example creates a component with two data-bound properties.
         *
         * ```typescript
         * @Component({
public object      *   selector { get; set; }
public object      *   inputs { get; set; }
public object      *   template { get; set; }
public object      *     Bank Name { get; set; }
public object      *     Account Id { get; set; }
         *   `
         * })
         * class BankAccount {
public object      *   bankName { get; set; }
public object      *   id { get; set; }
         *
         * ```
         *
         */
        public object Inputs { get; set; }
        /**
         * Enumerates the set of event-bound output properties.
         *
         * When an output property emits an event, an event handler attached to that event
         * in the template is invoked.
         *
         * The `outputs` property defines a set of `directiveProperty` to `bindingProperty`
public object      * configuration { get; set; }
         *
         * - `directiveProperty` specifies the component property that emits events.
         * - `bindingProperty` specifies the DOM property the event handler is attached to.
         *
         * @usageNotes
         *
         * ### Example
         *
         * ```typescript
         * @Directive({
public object      *   selector { get; set; }
public object      *   exportAs { get; set; }
         * })
         * class ChildDir {
         * }
         *
         * @Component({
public object      *   selector { get; set; }
public object      *   template { get; set; }
         * })
         * class MainComponent {
         * }
         * ```
         *
         */
        public object Outputs { get; set; }
        /**
         * Configures the [injector](guide/glossary#injector) of this
         * directive or component with a [token](guide/glossary#di-token)
         * that maps to a [provider](guide/glossary#provider) of a dependency.
         */
        public object Providers { get; set; }
        /**
         * Defines the name that can be used in the template to assign this directive to a variable.
         *
         * @usageNotes
         *
         * ### Simple Example
         *
         * ```
         * @Directive({
public object      *   selector { get; set; }
public object      *   exportAs { get; set; }
         * })
         * class ChildDir {
         * }
         *
         * @Component({
public object      *   selector { get; set; }
public object      *   template { get; set; }
         * })
         * class MainComponent {
         * }
         * ```
         *
         */
        public object ExportAs { get; set; }
        /**
         * Configures the queries that will be injected into the directive.
         *
         * Content queries are set before the `ngAfterContentInit` callback is called.
         * View queries are set before the `ngAfterViewInit` callback is called.
         *
         * @usageNotes
         *
         * ### Example
         *
         * The following example shows how queries are defined
public object      * and when their results are available in lifecycle hooks { get; set; }
         *
         * ```
         * @Component({
public object      *   selector { get; set; }
public object      *   queries { get; set; }
public object      *     contentChildren { get; set; }
public object      *     viewChildren { get; set; }
         *   },
public object      *   template { get; set; }
         * })
         * class SomeDir {
public object      *   contentChildren { get; set; }
public object      *   viewChildren { get; set; }
         *
         *   ngAfterContentInit() {
         *     // contentChildren is set
         *   }
         *
         *   ngAfterViewInit() {
         *     // viewChildren is set
         *   }
         * }
         * ```
         *
         * @Annotation
         */
        public object Queries { get; set; }
        public object Key { get; set; }
        /**
         * Maps class properties to host element bindings for properties,
         * attributes, and events, using a set of key-value pairs.
         *
         * Angular automatically checks host property bindings during change detection.
         * If a binding changes, Angular updates the directive's host element.
         *
         * When the key is a property of the host element, the property value is
         * the propagated to the specified DOM property.
         *
         * When the key is a static attribute in the DOM, the attribute value
         * is propagated to the specified property in the host element.
         *
public object      * For event handling { get; set; }
         * - The key is the DOM event that the directive listens to.
         * To listen to global events, add the target to the event name.
         * The target can be `window`, `document` or `body`.
         * - The value is the statement to execute when the event occurs. If the
         * statement evaluates to `false`, then `preventDefault` is applied on the DOM
         * event. A handler method can refer to the `$event` local variable.
         *
         */
        public object Host { get; set; }

        /**
         * If true, this directive/component will be skipped by the AOT compiler and so will always be
         * compiled using JIT.
         *
         * This exists to support future Ivy work and has no effect currently.
         */
        public object Jit { get; set; }

    }
}
