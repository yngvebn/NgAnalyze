using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ng.Contracts;
using Ng.Core;
using Zu.TypeScript;
using Zu.TypeScript.Change;
using Zu.TypeScript.TsTypes;

namespace Ng.App
{
    class Program
    {
        static void Main(string[] args)
        {
            //var specs = Directory.GetFiles(
            //    @"C:\Arbeid\etoto\code\beta.rikstoto.no\src\Rikstoto.Toto\App\Components\", "*component.spec.ts", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
            //foreach (var file in Directory.GetFiles(
            //    @"C:\Arbeid\etoto\code\beta.rikstoto.no\src\Rikstoto.Toto\App\Components\", "*component.ts",
            //    SearchOption.AllDirectories))
            //{
            //    if (!specs.Contains(Path.GetFileNameWithoutExtension(file) + ".spec.ts"))
            //    {
            //        GenerateSpecIfApplicable(file);
            //    }
            //}

            //string fileName =
            //    @"C:\Arbeid\etoto\code\beta.rikstoto.no\src\Rikstoto.Toto\App\Components\MyBets\MyBetsDate\MyBetsRaceday\MyBetsBet\MyBetsBetDetails\Prize\my-bet-prize.component.ts";
            //TypeScriptAST ast = new TypeScriptAST(File.ReadAllText(fileName), fileName);
            var path = @"C:\github\NgAnalyze\TestProject\tsconfig.json";// @"C:\github\beta.rikstoto.no\src\Rikstoto.Toto\tsconfig.app.json";
            TsConfig tsConfig = new TsConfigReader().LoadFromFile(path);
            TsProject project = TsProject.Load(tsConfig);
            var allClasses = project.Compiled.SelectMany(p => p.Classes);

            //var projectWideImports = project.Compiled.SelectMany(c => c.Imports).Select(i => i.AbsolutePath).Distinct().ToList();
            //var allFilenames = project.Files.Select(c => c.SourceStr).Distinct().ToList();
            //var filesNotReferencced = allFilenames.Where(filename =>
            //    projectWideImports.All(import => import != filename)).ToList();

            ImportedModule storeActionType = new ImportedModule("Action", "@ngrx/store");
            var allActions = allClasses.Where(c => c.Inherits.Any(i => i.Equals(storeActionType))).ToList();
            var change = new ChangeAST();
            var firstUsage = allActions.Skip(1).First();
            change.ChangeNode(firstUsage.Node, "/* New value goes here */");
            var newSource = change.GetChangedSource(firstUsage.Compilation.Ast.SourceStr);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(firstUsage.FileName), "changed.ts"), newSource);


            ////foreach (var inherits in firstAction.Classes.First().Inherits)
            ////{
            ////    Console.WriteLine(inherits);
            ////}
            var usages = project.FindUsages(allActions.Skip(1).First()).ToList();


            //var firstUsage = usages.First();
            //var change = new ChangeAST();
            //change.ChangeNode(firstUsage.Node, "/* New value goes here */");
            //var newSource = change.GetChangedSource(firstUsage.Compilation.Ast.SourceStr);
            //File.WriteAllText(firstUsage.Compilation.FileName, newSource);
        }

        private static void GenerateSpecIfApplicable(string file)
        {
            TypeScriptAST ast = new TypeScriptAST(File.ReadAllText(file), file);
            var constructor = ast.RootNode.GetDescendants().OfType<ConstructorDeclaration>().SingleOrDefault();
            if (constructor != null)
            {
                Console.WriteLine("Found constructor. Checking for parameters");
                if (constructor.Children.OfType<ParameterDeclaration>().Any())
                {
                    Console.WriteLine("No parameterless constructor. Skipping");
                    return;
                }
            }

            var methods = ast.GetDescendants().OfType<MethodDeclaration>();
            if (!methods.Any())
            {
                Console.WriteLine("No methods. Skipping");
                return;

            }
            Console.WriteLine($"Create spec for {file}");
            // Collect information
            var relativeFileNameForImport = $"./{Path.GetFileNameWithoutExtension(file)}";
            var classDeclaration = ast.RootNode.GetDescendants().OfType<ClassDeclaration>().SingleOrDefault();
            if (classDeclaration.Children.Any(c => c.Kind == SyntaxKind.AbstractKeyword))
            {
                Console.WriteLine("Abstract - skipping");
                return;
            }
            var className = classDeclaration.IdentifierStr;
            StringBuilder spec = new StringBuilder();

            spec.AppendLine($"import {{ {className} }} from '{relativeFileNameForImport}';");
            spec.AppendLine("");
            spec.AppendLine($"describe('{className}', () => {{");
            spec.AppendLine($"\tlet instance: {className} = null;");
            spec.AppendLine($"\tbeforeEach(() => {{");
            spec.AppendLine($"\t\tinstance = new {className}();");
            spec.AppendLine($"\t}});");
            spec.AppendLine("");

            spec.AppendLine($"\tit('can be created', () => {{");
            spec.AppendLine($"\t\texpect(instance).toBeTruthy();");
            spec.AppendLine($"\t}});");
            foreach (var method in methods.Where(m => m.Children.Any(c => c.Kind == SyntaxKind.PublicKeyword)))
            {
                spec.AppendLine("");
                spec.AppendLine($"\tdescribe('{method.IdentifierStr}', () => {{");


                var parameterDeclarations = method.Children.OfType<ParameterDeclaration>().ToList();
                if (parameterDeclarations.Count() > 1)
                {
                    spec.AppendLine($"\t\tit('Can be invoked', () => {{");
                    foreach (var param in parameterDeclarations)
                    {
                        spec.AppendLine($"\t\t\t// {param.IdentifierStr}: {param.Last.Kind}");
                    }
                    spec.AppendLine($"\t\t}});");

                }
                else if (!parameterDeclarations.Any())
                {
                    spec.AppendLine($"\t\tit('Can be invoked', () => {{");
                    spec.AppendLine($"\t\t\tinstance.{method.IdentifierStr}();");
                    spec.AppendLine($"\t\t}});");
                }
                else
                {
                    foreach (var testValue in GetTestValues(parameterDeclarations.Single()))
                    {
                        spec.AppendLine($"\t\tit(`Can be invoked with {testValue}`, () => {{");
                        spec.AppendLine($"\t\t\tconst result = instance.{method.IdentifierStr}({testValue});");
                        spec.AppendLine($"\t\t\texpect(result);");
                        spec.AppendLine($"\t\t}});");
                    }
                }


                spec.AppendLine($"\t}});");

            }
            spec.AppendLine($"}});");

            var fileContents = spec.ToString().Replace("\t", "    ");
            var saveToPath = Path.GetDirectoryName(file);
            var specFilename = Path.GetFileNameWithoutExtension(file) + ".spec.ts";
            File.WriteAllText(Path.Combine(saveToPath, specFilename), fileContents);

        }

        private static IEnumerable GetTestValues(ParameterDeclaration single)
        {
            Random r = new Random();
            yield return "undefined";
            yield return "null";
            if (single.Last.Kind == SyntaxKind.NumberKeyword)
            {
                yield return Int16.MaxValue;
                yield return Int16.MinValue;
                yield return 0;
                yield return r.Next() * 1000;
            }
            else if (single.Last.Kind == SyntaxKind.BooleanKeyword)
            {
                yield return "false";
                yield return "true";
            }
            else if (single.Last.Kind == SyntaxKind.StringKeyword)
            {
                yield return $"'{Guid.NewGuid().ToString("N").Substring(r.Next(1), r.Next(22))}'";
                yield return $"'{Guid.NewGuid().ToString("N").Substring(r.Next(1), r.Next(22))}'";
                yield return $"'{Guid.NewGuid().ToString("N").Substring(r.Next(1), r.Next(22))}'";
            }
        }
    }
}
