using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.OutputGeneration;
using Biohazrd.Transformation.Common;
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
libraryBuilder.AddFile(imGuiHeaderFilePath);

TranslatedLibrary library = libraryBuilder.Create();

// Start output session
using OutputSession outputSession = new OutputSession()
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
using StreamWriter diagnosticsOutput = outputSession.Open<StreamWriter>("Diagnostics.log");

void OutputDiagnostic(in TranslationDiagnostic diagnostic)
{
    WriteDiagnosticToConsole(diagnostic);
    WriteDiagnosticToWriter(diagnostic, diagnosticsOutput);
}

diagnosticsOutput.WriteLine("==============================================================================");
diagnosticsOutput.WriteLine("Parsing Diagnostics");
diagnosticsOutput.WriteLine("==============================================================================");

foreach (TranslationDiagnostic parsingDiagnostic in library.ParsingDiagnostics)
{ OutputDiagnostic(parsingDiagnostic); }

diagnosticsOutput.WriteLine("==============================================================================");
diagnosticsOutput.WriteLine("Translation Diagnostics");
diagnosticsOutput.WriteLine("==============================================================================");

foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
{
    if (declaration.Diagnostics.Length > 0)
    {
        diagnosticsOutput.WriteLine($"--------------- {declaration.GetType().Name} {declaration.Name} ---------------");

        foreach (TranslationDiagnostic diagnostic in declaration.Diagnostics)
        { OutputDiagnostic(diagnostic); }
    }
}

if (brokenDeclarationExtractor.BrokenDeclarations.Length > 0)
{
    diagnosticsOutput.WriteLine("==============================================================================");
    diagnosticsOutput.WriteLine("Broken Declarations");
    diagnosticsOutput.WriteLine("==============================================================================");

    foreach (TranslatedDeclaration declaration in brokenDeclarationExtractor.BrokenDeclarations)
    {
        diagnosticsOutput.WriteLine($"=============== {declaration.GetType().Name} {declaration.Name} ===============");

        foreach (TranslationDiagnostic diagnostic in declaration.Diagnostics)
        { OutputDiagnostic(diagnostic); }
    }
}

diagnosticsOutput.WriteLine("==============================================================================");
diagnosticsOutput.WriteLine("Generation Diagnostics");
diagnosticsOutput.WriteLine("==============================================================================");

if (generationDiagnostics.Length == 0)
{ diagnosticsOutput.WriteLine("Generation completed successfully."); }
else
{
    foreach (TranslationDiagnostic diagnostic in generationDiagnostics)
    { OutputDiagnostic(diagnostic); }
}

outputSession.Dispose();

static void WriteDiagnosticToConsole(in TranslationDiagnostic diagnostic)
{
    TextWriter output;
    ConsoleColor oldForegroundColor = Console.ForegroundColor;
    ConsoleColor oldBackgroundColor = Console.BackgroundColor;

    try
    {
        switch (diagnostic.Severity)
        {
            case Severity.Ignored:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                output = Console.Out;
                break;
            case Severity.Note:
                output = Console.Out;
                break;
            case Severity.Warning:
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                output = Console.Error;
                break;
            case Severity.Error:
                Console.ForegroundColor = ConsoleColor.DarkRed;
                output = Console.Error;
                break;
            case Severity.Fatal:
            default:
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.DarkRed;
                output = Console.Error;
                break;
        }

        WriteDiagnosticToWriter(diagnostic, output);
    }
    finally
    {
        Console.BackgroundColor = oldBackgroundColor;
        Console.ForegroundColor = oldForegroundColor;
    }
}

static void WriteDiagnosticToWriter(in TranslationDiagnostic diagnostic, TextWriter output)
{
    if (!diagnostic.Location.IsNull)
    {
        string fileName = Path.GetFileName(diagnostic.Location.SourceFile);
        if (diagnostic.Location.Line != 0)
        { output.WriteLine($"{diagnostic.Severity} at {fileName}:{diagnostic.Location.Line}: {diagnostic.Message}"); }
        else
        { output.WriteLine($"{diagnostic.Severity} at {fileName}: {diagnostic.Message}"); }
    }
    else
    { output.WriteLine($"{diagnostic.Severity}: {diagnostic.Message}"); }
}
