using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.OutputGeneration;
using Biohazrd.Transformation.Common;
using Biohazrd.Utilities;
using InfectedImGui.Generator;
using System;
using System.Collections.Immutable;
using System.IO;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("    InfectedImGui.Generator <path-to-dear-imgui-source> <path-to-output>");
    return;
}

string imGuiSourceDirectoryPath = Path.GetFullPath(args[0]);
string imGuiHeaderFilePath = Path.Combine(imGuiSourceDirectoryPath, "imgui.h");

string outputDirectoryPath = Path.GetFullPath(args[1]);

bool includeExampleImplementations = true;

if (!Directory.Exists(imGuiSourceDirectoryPath))
{
    Console.Error.WriteLine($"Dear ImGui directory '{imGuiSourceDirectoryPath}' not found.");
    return;
}

if (!File.Exists(imGuiHeaderFilePath))
{
    Console.Error.WriteLine($"Dear ImGui header file not found at '{imGuiHeaderFilePath}'.");
    return;
}

// AppContext.BaseDirectory is not perfect here, should use Environment.ApplicationDirectory if it's ever added
// https://github.com/dotnet/runtime/issues/41341
string imGuiConfigFilePath = Path.Combine(AppContext.BaseDirectory, "InfectedImGuiConfig.h");

if (!File.Exists(imGuiConfigFilePath))
{
    Console.Error.WriteLine($"Could not find InfectedImGui config file '{imGuiConfigFilePath}'.");
    return;
}

// Create the library
TranslatedLibraryBuilder libraryBuilder = new();
libraryBuilder.AddCommandLineArgument("--language=c++");
libraryBuilder.AddCommandLineArgument($"-I{imGuiSourceDirectoryPath}");
libraryBuilder.AddCommandLineArgument($"-DIMGUI_USER_CONFIG=\"{imGuiConfigFilePath}\"");
libraryBuilder.AddFile(imGuiHeaderFilePath);

if (includeExampleImplementations)
{
    string backendsPath = Path.Combine(imGuiSourceDirectoryPath, "backends");
    libraryBuilder.AddFile(Path.Combine(backendsPath, "imgui_impl_win32.h"));
    libraryBuilder.AddFile(Path.Combine(backendsPath, "imgui_impl_dx11.h"));
}

TranslatedLibrary library = libraryBuilder.Create();

// Start output session
using OutputSession outputSession = new()
{
    AutoRenameConflictingFiles = true,
    BaseOutputDirectory = outputDirectoryPath
};

// Apply transformations
Console.WriteLine("==============================================================================");
Console.WriteLine("Performing library-specific transformations...");
Console.WriteLine("==============================================================================");

library = new RemoveUnneededDeclarationsTransformation().Transform(library);
library = new ImGuiEnumTransformation().Transform(library);
library = new ImGuiDllNameTransformation().Transform(library);

BrokenDeclarationExtractor brokenDeclarationExtractor = new();
library = brokenDeclarationExtractor.Transform(library);

library = new RemoveExplicitBitFieldPaddingFieldsTransformation().Transform(library);
library = new AddBaseVTableAliasTransformation().Transform(library);
library = new ConstOverloadRenameTransformation().Transform(library);
library = new MakeEverythingPublicTransformation().Transform(library);
library = new RemoveRemainingTypedefsTransformation().Transform(library);

ImGuiCSharpTypeReductionTransformation typeReductionTransformation = new();
int iterations = 0;
do
{
    library = typeReductionTransformation.Transform(library);
    iterations++;
} while (typeReductionTransformation.ConstantArrayTypesCreated > 0);
Console.WriteLine($"Finished reducing types in {iterations} iterations.");

library = new LiftAnonymousUnionFieldsTransformation().Transform(library);
library = new CSharpBuiltinTypeTransformation().Transform(library);
library = new KludgeUnknownClangTypesIntoBuiltinTypesTransformation(emitErrorOnFail: true).Transform(library);
library = new WrapNonBlittableTypesWhereNecessaryTransformation().Transform(library);
library = new DeduplicateNamesTransformation().Transform(library);
library = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library);

// Perform validation
Console.WriteLine("==============================================================================");
Console.WriteLine("Performing post-translation validation...");
Console.WriteLine("==============================================================================");

library = new CSharpTranslationVerifier().Transform(library);

// Remove final broken declarations
library = brokenDeclarationExtractor.Transform(library);

// Emit the translation
Console.WriteLine("==============================================================================");
Console.WriteLine("Emitting translation...");
Console.WriteLine("==============================================================================");
ImmutableArray<TranslationDiagnostic> generationDiagnostics = CSharpLibraryGenerator.Generate
(
    CSharpGenerationOptions.Default with { DumpClangInfo = false },
    outputSession,
    library,
    LibraryTranslationMode.OneFilePerType
);

// Write out diagnostics log
DiagnosticWriter diagnostics = new();
diagnostics.AddFrom(library);
diagnostics.AddFrom(brokenDeclarationExtractor);
diagnostics.AddCategory("Generation Diagnostics", generationDiagnostics, "Generation completed successfully");

using StreamWriter diagnosticsOutput = outputSession.Open<StreamWriter>("Diagnostics.log");
diagnostics.WriteOutDiagnostics(diagnosticsOutput, writeToConsole: true);

outputSession.Dispose();
