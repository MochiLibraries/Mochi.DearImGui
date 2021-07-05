#define USE_TOP_LEVEL_STATEMENTS // Workaround for https://github.com/dotnet/roslyn/issues/50591
using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.OutputGeneration;
using Biohazrd.Transformation.Common;
using Biohazrd.Utilities;
using InfectedImGui.Generator;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

#if !USE_TOP_LEVEL_STATEMENTS
static class Program { static void Main(string[] args) {
#endif

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("    InfectedImGui.Generator <path-to-dear-imgui-source> <path-to-infectedimgui-native> <path-to-output>");
    return;
}

string imGuiSourceDirectoryPath = Path.GetFullPath(args[0]);
string imGuiHeaderFilePath = Path.Combine(imGuiSourceDirectoryPath, "imgui.h");

string infectedImGuiNativeRootPath = Path.GetFullPath(args[1]);
string imguiLibFilePath = Path.Combine(infectedImGuiNativeRootPath, "build", "Debug", "InfectedImGui.Native.lib");
string imguiInlineExporterFilePath = Path.Combine(infectedImGuiNativeRootPath, "InfectedImGui.cpp");
string infectedImGuiNativeBuildScript = Path.Combine(infectedImGuiNativeRootPath, "Build.cmd");

string outputDirectoryPath = Path.GetFullPath(args[2]);

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
TranslatedLibraryBuilder libraryBuilder = new()
{
    Options = new TranslationOptions()
    {
        // The only template that appears on the public API is ImVector<T>, which we special-case as a C# generic.
        // ImPool<T>, ImChunkStream<T>, and ImSpan<T> do appear on the internal API but for now we just want them to be dropped.
        //TODO: In theory this could be made to work, but there's a few wrinkles that need to be ironed out and these few API points are not a high priority.
        EnableTemplateSupport = false,
    }
};
libraryBuilder.AddCommandLineArgument("--language=c++");
libraryBuilder.AddCommandLineArgument($"-I{imGuiSourceDirectoryPath}");
libraryBuilder.AddCommandLineArgument($"-DIMGUI_USER_CONFIG=\"{imGuiConfigFilePath}\"");
libraryBuilder.AddFile(imGuiHeaderFilePath);
libraryBuilder.AddFile(Path.Combine(imGuiSourceDirectoryPath, "imgui_internal.h"));

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
    BaseOutputDirectory = outputDirectoryPath,
    ConservativeFileLogging = false
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
library = new ImGuiCSharpTypeReductionTransformation().Transform(library);
library = new CSharpBuiltinTypeTransformation().Transform(library);
library = new MiscFixesTransformation().Transform(library);
library = new LiftAnonymousRecordFieldsTransformation().Transform(library);
library = new WrapNonBlittableTypesWhereNecessaryTransformation().Transform(library);
library = new AddTrampolineMethodOptionsTransformation(MethodImplOptions.AggressiveInlining).Transform(library);
library = new ImGuiInternalFixupTransformation().Transform(library);
library = new InfectedImGuiNamespaceTransformation().Transform(library);
library = new RemoveIllegalImVectorReferencesTransformation().Transform(library);
library = new MoveLooseDeclarationsIntoTypesTransformation
(
    (c, d) => d.Namespace == "InfectedImGui" ? "ImGui" : d.Namespace == "InfectedImGui.Internal" ? "ImGuiInternal" : "Globals"
).Transform(library);
library = new AutoNameUnnamedParametersTransformation().Transform(library);
library = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
library = new DeduplicateNamesTransformation().Transform(library);
library = new OrganizeOutputFilesByNamespaceTransformation("InfectedImGui").Transform(library); // Relies on InfectedImGuiNamespaceTransformation, MoveLooseDeclarationsIntoTypesTransformation

// Generate the inline export helper
library = new InlineExportHelper(outputSession, imguiInlineExporterFilePath).Transform(library);

// Rebuild the native DLL so that the librarian can access a version of the library including the inline-exported functions
Console.WriteLine("Rebuilding InfectedImGui.Native...");
Process.Start(new ProcessStartInfo(infectedImGuiNativeBuildScript)
{
    WorkingDirectory = infectedImGuiNativeRootPath
})!.WaitForExit();

// Use librarian to identifiy DLL exports
LinkImportsTransformation linkImports = new()
{
    ErrorOnMissing = true,
    TrackVerboseImportInformation = true,
    WarnOnAmbiguousSymbols = true
};
linkImports.AddLibrary(imguiLibFilePath);
library = linkImports.Transform(library);

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
    library
);

// Write out diagnostics log
DiagnosticWriter diagnostics = new();
diagnostics.AddFrom(library);
diagnostics.AddFrom(brokenDeclarationExtractor);
diagnostics.AddCategory("Generation Diagnostics", generationDiagnostics, "Generation completed successfully");

using StreamWriter diagnosticsOutput = outputSession.Open<StreamWriter>("Diagnostics.log");
diagnostics.WriteOutDiagnostics(diagnosticsOutput, writeToConsole: true);

outputSession.Dispose();

#if !USE_TOP_LEVEL_STATEMENTS
}}
#endif
