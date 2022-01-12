using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mochi.DearImGui;

public static class MochiDearImGui
{
    public enum Variant
    {
        Default,
        Debug,
        Release,
    }

    private static IntPtr NativeRuntimeHandle;

    // This could be public, but it's very hard to use correctly and the developer could currently manually set their own import resolver.
    // Let's wait to expose this based on a demonstrated need so that the API can stay flexible and so if someone thinks/knows they need this they'll feel more inclined to say something.
    /// <summary>Specifies a specific <see cref="NativeLibrary"/> handle to use for the Dear ImGui runtime.</summary>
    /// <remarks>You must call this method before calling any Dear ImGui functions.</remarks>
    private static void UseSpecificRuntime(IntPtr nativeRuntimeHandle)
    {
        if (nativeRuntimeHandle == IntPtr.Zero)
        { throw new ArgumentException("The specified native runtime handle is invalid.", nameof(nativeRuntimeHandle)); }
        else if (NativeRuntimeHandle != IntPtr.Zero)
        { throw new InvalidOperationException("Cannot select a specific runtime after one has already been loaded."); }

        //TODO: We should validate that the native runtime wasn't already loaded
        NativeRuntimeHandle = nativeRuntimeHandle;

        static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
            => NativeRuntimeHandle;

        NativeLibrary.SetDllImportResolver(typeof(MochiDearImGui).Assembly, DllImportResolver);
    }

    /// <summary>Specifies a specific variant of the Dear ImGui runtime to use.</summary>
    /// <remarks>
    /// You must call this method before calling any Dear ImGui functions.
    ///
    /// In order to use a variant, you must manually add the appropriate NuGet package reference.
    /// (For example: To use the debug variant on Windows x64, reference the appropriate version of Mochi.DearImGui.Native.win-x64-debug.)
    ///
    /// This method expects the native runtime layout file provided by the official MochiDearImGui NuGet packages.
    /// </remarks>
    public static void SelectRuntimeVariant(Variant variant)
    {
        string variantPath = "Mochi.DearImGui.Native";

        variantPath = variant switch
        {
            Variant.Default => variantPath,
            Variant.Release => variantPath,
            Variant.Debug => Path.Combine("debug", variantPath),
            _ => throw new ArgumentException("The specified variant is invalid.", nameof(variant))
        };

        if (NativeLibrary.TryLoad(variantPath, typeof(MochiDearImGui).Assembly, DllImportSearchPath.ApplicationDirectory, out IntPtr handle))
        { UseSpecificRuntime(handle); }
        else
        { throw new DllNotFoundException($"Failed to load the {variant.ToString().ToLowerInvariant()} variant of the Dear ImGui runtime '{variantPath}'. Is the appropriate runtime NuGet package installed?"); }
    }
}
