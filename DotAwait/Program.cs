//using Microsoft.Build.Locator;
//using Microsoft.CodeAnalysis.MSBuild;
//using Microsoft.CodeAnalysis.CSharp;
//
//MSBuildLocator.RegisterDefaults(); // подключаем MSBuild твоей SDK
//
//var workspace = MSBuildWorkspace.Create();
//var projectPath = args[0]; // путь к .csproj
//var project = await workspace.OpenProjectAsync(projectPath);
//
//var compilation = await project.GetCompilationAsync();
//
//Console.WriteLine($"🔍 Project: {project.Name}");
//Console.WriteLine($"🔧 References: {string.Join(", ", project.MetadataReferences.Select(r => r.Display))}");
//
//foreach (var document in project.Documents)
//{
//    var tree = await document.GetSyntaxTreeAsync();
//    var semanticModel = await document.GetSemanticModelAsync();
//
//    Console.WriteLine($"\n📄 File: {document.Name}");
//    var root = await tree.GetRootAsync();
//
//    var classDecls = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();
//    foreach (var classDecl in classDecls)
//    {
//        var symbol = semanticModel.GetDeclaredSymbol(classDecl);
//        Console.WriteLine($"   👤 Class: {symbol?.ToDisplayString()}");
//    }
//}