using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.OutputGeneration;
using Biohazrd.Transformation.Common;
using Biohazrd.Utilities;
using Mochi.DearImGui.Generator;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("    Mochi.DearImGui.Generator <path-to-dear-imgui-source> <path-to-mochi-dearimgui-native> <path-to-output>");
    return 1;
}

const string canonicalBuildVariant = "Release";
string dotNetRid;
string nativeRuntimeBuildScript;
string importLibraryName;
bool itaniumExportMode;

if (OperatingSystem.IsWindows())
{
    dotNetRid = "win-x64";
    nativeRuntimeBuildScript = "build-native.cmd";
    importLibraryName = "Mochi.DearImGui.Native.lib";
    itaniumExportMode = false;
}
else if (OperatingSystem.IsLinux())
{
    dotNetRid = "linux-x64";
    nativeRuntimeBuildScript = "build-native.sh";
    importLibraryName = "libMochi.DearImGui.Native.so";
    itaniumExportMode = true;
}
else
{
    Console.Error.WriteLine($"'{RuntimeInformation.OSDescription}' is not supported by this generator.");
    return 1;
}

string imGuiSourceDirectoryPath = Path.GetFullPath(args[0]);
string imGuiHeaderFilePath = Path.Combine(imGuiSourceDirectoryPath, "imgui.h");

string dearImGuiNativeRootPath = Path.GetFullPath(args[1]);
string imguiLibFilePath = Path.Combine(dearImGuiNativeRootPath, "..", "bin", "Mochi.DearImGui.Native", dotNetRid, canonicalBuildVariant, importLibraryName);
string imguiInlineExporterFilePath = Path.Combine(dearImGuiNativeRootPath, "InlineExportHelper.gen.cpp");
string nativeBuildScript = Path.Combine(dearImGuiNativeRootPath, nativeRuntimeBuildScript);

string outputDirectoryPath = Path.GetFullPath(args[2]);

if (!Directory.Exists(imGuiSourceDirectoryPath))
{
    Console.Error.WriteLine($"Dear ImGui directory '{imGuiSourceDirectoryPath}' not found.");
    return 1;
}

if (!File.Exists(imGuiHeaderFilePath))
{
    Console.Error.WriteLine($"Dear ImGui header file not found at '{imGuiHeaderFilePath}'.");
    return 1;
}

string imGuiConfigFilePath = Path.Combine(dearImGuiNativeRootPath, "DearImGuiConfig.h");

if (!File.Exists(imGuiConfigFilePath))
{
    Console.Error.WriteLine($"Could not find Dear ImGui config file '{imGuiConfigFilePath}'.");
    return 1;
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

TranslatedLibrary library = libraryBuilder.Create();
TranslatedLibraryConstantEvaluator constantEvaluator = libraryBuilder.CreateConstantEvaluator();

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
library = new MiscFixesTransformation().Transform(library);
library = new LiftAnonymousRecordFieldsTransformation().Transform(library);
library = new AddTrampolineMethodOptionsTransformation(MethodImplOptions.AggressiveInlining).Transform(library);
library = new ImGuiInternalFixupTransformation().Transform(library);
library = new MochiDearImGuiNamespaceTransformation().Transform(library);
library = new RemoveIllegalImVectorReferencesTransformation().Transform(library);
library = new MoveLooseDeclarationsIntoTypesTransformation
(
    (c, d) => d.Namespace == "Mochi.DearImGui" ? "ImGui" : d.Namespace == "Mochi.DearImGui.Internal" ? "ImGuiInternal" : "Globals"
).Transform(library);
library = new AutoNameUnnamedParametersTransformation().Transform(library);
library = new CreateTrampolinesTransformation()
{
    TargetRuntime = TargetRuntime.Net6
}.Transform(library);
library = new ImGuiCreateStringWrappersTransformation().Transform(library);
library = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
library = new DeduplicateNamesTransformation().Transform(library);
library = new OrganizeOutputFilesByNamespaceTransformation("Mochi.DearImGui").Transform(library); // Relies on MochiDearImGuiNamespaceTransformation, MoveLooseDeclarationsIntoTypesTransformation
library = new ImVersionConstantsTransformation(library, constantEvaluator).Transform(library);
library = new VectorTypeTransformation().Transform(library);

// Generate the inline export helper
library = new InlineExportHelper(outputSession, imguiInlineExporterFilePath) { __ItaniumExportMode = itaniumExportMode }.Transform(library);

// Rebuild the native DLL so that the librarian can access a version of the library including the inline-exported functions
Console.WriteLine("Rebuilding Mochi.DearImGui.Native...");
Process.Start(new ProcessStartInfo(nativeBuildScript)
{
    WorkingDirectory = dearImGuiNativeRootPath
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
    CSharpGenerationOptions.Default with
    {
        TargetRuntime = TargetRuntime.Net6,
        InfrastructureTypesNamespace = "Mochi.DearImGui.Infrastructure",
    },
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
return 0;
