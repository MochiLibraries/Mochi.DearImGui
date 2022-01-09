// Renderer backend for OpenTK
// Based on imgui_impl_opengl3.cpp
// https://github.com/ocornut/imgui/blob/704ab1114aa54858b690711554cf3312fbbcc3fc/backends/imgui_impl_opengl3.cpp

// Implemented features:
//  [x] Renderer: User texture binding. Use 'GLuint' OpenGL texture identifier as void*/ImTextureID. Read the FAQ about ImTextureID!
//  [x] Renderer: Multi-viewport support. Enable with 'io.ConfigFlags |= ImGuiConfigFlags_ViewportsEnable'.
//  [x] Renderer: Desktop GL only: Support for large meshes (64k+ vertices) with 16-bit indices.

//----------------------------------------
// OpenGL    GLSL      GLSL
// version   version   string
//----------------------------------------
//  2.0       110       "#version 110"
//  2.1       120       "#version 120"
//  3.0       130       "#version 130"
//  3.1       140       "#version 140"
//  3.2       150       "#version 150"
//  3.3       330       "#version 330 core"
//  4.0       400       "#version 400 core"
//  4.1       410       "#version 410 core"
//  4.2       420       "#version 410 core"
//  4.3       430       "#version 430 core"
//  ES 2.0    100       "#version 100"      = WebGL 1.0
//  ES 3.0    300       "#version 300 es"   = WebGL 2.0
//----------------------------------------
using OpenTK.Graphics.OpenGL;
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Mochi.DearImGui.OpenTK;

public unsafe sealed class RendererBackend : IDisposable
{
    private Internal.ImGuiContext* Context;
    private GCHandle ThisHandle;

    /// <summary>Extracted at runtime using GL_MAJOR_VERSION, GL_MINOR_VERSION queries (e.g. 320 for GL 3.2)</summary>
    private readonly int GlVersion;
    private readonly string GlslVersionString;
    private int FontTexture;
    private int ShaderHandle;
    /// <summary>Uniforms location</summary>
    private int AttribLocationTex;
    private int AttribLocationProjMtx;
    /// <summary>Vertex attributes location</summary>
    private int AttribLocationVtxPos;
    private int AttribLocationVtxUV;
    private int AttribLocationVtxColor;
    private int VboHandle;
    private int ElementsHandle;
    private nint VertexBufferSize;
    private nint IndexBufferSize;
    private bool HasClipOrigin;

    // ImGui_ImplOpenGL3_Init
    public RendererBackend()
        : this(null)
    { }

    public RendererBackend(string? glslVersion)
    {
        ImGuiIO* io = ImGui.GetIO();
        if (io->BackendRendererUserData != null)
        { throw new InvalidOperationException("A renderer backend has already been initialized for the current Dear ImGui context!"); }

        // Unlike the native bindings we have an object associated with each context so we generally don't use the BackendRendererUserData and instead enforce
        // a 1:1 relationship between backend instances and Dear ImGui contexts. However we still use a GC handle in BackendRendererUserData for our platform callbacks.
        Context = ImGui.GetCurrentContext();

        ThisHandle = GCHandle.Alloc(this, GCHandleType.Weak);
        io->BackendRendererUserData = (void*)GCHandle.ToIntPtr(ThisHandle);

        // Set the backend name
        {
            string name = GetType().FullName ?? nameof(RendererBackend);
            int nameByteCount = Encoding.UTF8.GetByteCount(name) + 1;
            byte* nameP = (byte*)ImGui.MemAlloc((ulong)nameByteCount);
            io->BackendRendererName = nameP;
            Span<byte> nameSpan = new(nameP, nameByteCount);
            int encodedByteCount = Encoding.UTF8.GetBytes(name.AsSpan().Slice(0, nameSpan.Length - 1), nameSpan);
            nameSpan[encodedByteCount] = 0; // Null terminator
        }

        // Query for GL version (e.g. 320 for GL 3.2)
        {
            int major = GL.GetInteger(GetPName.MajorVersion);
            int minor = GL.GetInteger(GetPName.MinorVersion);

            if (major == 0 && minor == 0)
            { throw new NotSupportedException("Ancient versions of OpenGL which do not support version queries are not supported."); }

            GlVersion = major * 100 + minor * 10;
        }

        //TODO: Handle GLES
        if (GlVersion >= 320)
        { io->BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset; } // We can honor the ImDrawCmd::VtxOffset field, allowing for large meshes.

        io->BackendFlags |= ImGuiBackendFlags.RendererHasViewports; // We can create multi-viewports on the Renderer side (optional)

        // Store GLSL version string so we can refer to it later in case we recreate shaders.
        // Note: GLSL version is NOT the same as GL version. Leave this to NULL if unsure.
        if (glslVersion is null)
        {
            //TODO: Handle GLES
            if (OperatingSystem.IsMacOS())
            { glslVersion = "#version 150"; }
            else
            { glslVersion = "#version 130"; }
        }

        GlslVersionString = glslVersion;

        // Detect extensions we support
        HasClipOrigin = GlVersion >= 450;

        //TODO: Handle GLES
        int extensionCount = GL.GetInteger(GetPName.NumExtensions);
        for (int i = 0; i < extensionCount; i++)
        {
            if (GL.GetString(StringNameIndexed.Extensions, i) == "GL_ARB_clip_control")
            { HasClipOrigin = true; }
        }

        if (io->ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        {
            // ImGui_ImplOpenGL3_InitPlatformInterface
            ImGui.GetPlatformIO()->Renderer_RenderWindow = &RenderWindow;

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            static void RenderWindow(ImGuiViewport* viewport, void* renderArg)
            {
                IntPtr userData = (IntPtr)ImGui.GetIO()->BackendRendererUserData;

                if (userData == IntPtr.Zero)
                { throw new InvalidOperationException("The current Dear ImGui context has no associated renderer backend."); }

                if (!viewport->Flags.HasFlag(ImGuiViewportFlags.NoRendererClear))
                {
                    GL.ClearColor(0f, 0f, 0f, 1f);
                    GL.Clear(ClearBufferMask.ColorBufferBit);
                }

                ((RendererBackend)GCHandle.FromIntPtr(userData).Target!).RenderDrawData(viewport->DrawData);
            }
        }
    }

    private void AssertImGuiContext()
    {
        if (Context is null)
        { throw new ObjectDisposedException(typeof(RendererBackend).FullName); }
        else if (ImGui.GetCurrentContext() != Context)
        { throw new InvalidOperationException("The current Dear ImGui context is not the one associated with this renderer backend!"); }
    }

    // ImGui_ImplOpenGL3_NewFrame
    public void NewFrame()
    {
        AssertImGuiContext();

        if (ShaderHandle == 0)
        { CreateDeviceObjects(); }
    }

    // ImGui_ImplOpenGL3_CreateDeviceObjects
    public void CreateDeviceObjects()
    {
        AssertImGuiContext();

        if (ShaderHandle != 0)
        { throw new InvalidOperationException("The device objects have already been created."); }

        // Backup GL state
        int lastTexture = GL.GetInteger(GetPName.TextureBinding2D);
        int lastArrayBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);
        int lastVertexArray = GL.GetInteger(GetPName.VertexArrayBinding);

        // Parse GLSL version string
        int glslVersion = 130;
        const string versionStringPrefix = "#version ";
        if (GlslVersionString.StartsWith(versionStringPrefix))
        {
            ReadOnlySpan<char> versionSpan = GlslVersionString.AsSpan().Slice(versionStringPrefix.Length);
            int spaceIndex = versionSpan.IndexOf(' ');
            if (spaceIndex > 0)
            { versionSpan = versionSpan.Slice(0, spaceIndex); }

            versionSpan = versionSpan.Trim();

            if (int.TryParse(versionSpan, out int parsedVersion))
            { glslVersion = parsedVersion; }
            else
            { Debug.Fail("Version string is malformed!"); }
        }
        else
        { Debug.Fail("Version string is malformed!"); }

        // Select shaders matching our GLSL versions
        string vertexShader;
        string fragmentShader;

        string GetShaderSource(string shaderName)
        {
            shaderName = $"Mochi.DearImGui.OpenTK.Shaders.{shaderName}.glsl";
            using Stream? resourceStream = typeof(RendererBackend).Assembly.GetManifestResourceStream(shaderName);
            if (resourceStream is null)
            { throw new InvalidOperationException($"Failed to load shader '{shaderName}' from the assembly manifest!"); }

            using StreamReader reader = new(resourceStream);
            return $"{GlslVersionString}\n{reader.ReadToEnd().Replace("\r", "")}";
        }

        if (glslVersion < 130)
        {
            vertexShader = GetShaderSource("Vertex120");
            fragmentShader = GetShaderSource("Fragment120");
        }
        else if (glslVersion >= 410)
        {
            vertexShader = GetShaderSource("Vertex410core");
            fragmentShader = GetShaderSource("Fragment410core");
        }
        else if (glslVersion == 300)
        {
            vertexShader = GetShaderSource("Vertex300es");
            fragmentShader = GetShaderSource("Fragment300es");
        }
        else
        {
            vertexShader = GetShaderSource("Vertex130");
            fragmentShader = GetShaderSource("Fragment130");
        }

        // Create shaders
        void CheckShader(int handle, string description)
        {
            GL.GetShader(handle, ShaderParameter.CompileStatus, out int status);
            GL.GetShader(handle, ShaderParameter.InfoLogLength, out int logLength);

            string? compilationLog = null;
            if (logLength > 1)
            { compilationLog = GL.GetShaderInfoLog(handle); }

            if (status == 0)
            {
                string message = $"Failed to compile {description} with GLSL '{GlslVersionString}'.";

                if (compilationLog is not null)
                { message += $"{Environment.NewLine}{compilationLog}"; }

                throw new InvalidOperationException(message);
            }

            if (compilationLog is not null)
            { Console.Error.WriteLine(compilationLog); }
        }

        int vertexShaderHandle = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShaderHandle, vertexShader);
        GL.CompileShader(vertexShaderHandle);
        CheckShader(vertexShaderHandle, "vertex shader");

        int fragmentShaderHandle = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShaderHandle, fragmentShader);
        GL.CompileShader(fragmentShaderHandle);
        CheckShader(fragmentShaderHandle, "fragment shader");

        // Link
        void CheckProgram(int handle, string description)
        {
            GL.GetProgram(handle, GetProgramParameterName.LinkStatus, out int status);
            GL.GetProgram(handle, GetProgramParameterName.InfoLogLength, out int logLength);

            string? compilationLog = null;
            if (logLength > 1)
            { compilationLog = GL.GetProgramInfoLog(handle); }

            if (status == 0)
            {
                string message = $"Failed to link {description} with GLSL '{GlslVersionString}'.";

                if (compilationLog is not null)
                { message += $"{Environment.NewLine}{compilationLog}"; }

                throw new InvalidOperationException(message);
            }

            if (compilationLog is not null)
            { Console.Error.WriteLine(compilationLog); }
        }

        ShaderHandle = GL.CreateProgram();
        GL.AttachShader(ShaderHandle, vertexShaderHandle);
        GL.AttachShader(ShaderHandle, fragmentShaderHandle);
        GL.LinkProgram(ShaderHandle);
        CheckProgram(ShaderHandle, "shader program");

        GL.DetachShader(ShaderHandle, vertexShaderHandle);
        GL.DetachShader(ShaderHandle, fragmentShaderHandle);
        GL.DeleteShader(vertexShaderHandle);
        GL.DeleteShader(fragmentShaderHandle);

        AttribLocationTex = GL.GetUniformLocation(ShaderHandle, "Texture");
        AttribLocationProjMtx = GL.GetUniformLocation(ShaderHandle, "ProjMtx");
        AttribLocationVtxPos = GL.GetAttribLocation(ShaderHandle, "Position");
        AttribLocationVtxUV = GL.GetAttribLocation(ShaderHandle, "UV");
        AttribLocationVtxColor = GL.GetAttribLocation(ShaderHandle, "Color");

        // Create buffers
        GL.GenBuffers(1, out VboHandle);
        GL.GenBuffers(1, out ElementsHandle);

        // Create fonts texture
        CreateFontsTexture();

        // Restore modified GL state
        GL.BindTexture(TextureTarget.Texture2D, lastTexture);
        GL.BindBuffer(BufferTarget.ArrayBuffer, lastArrayBuffer);
        GL.BindVertexArray(lastVertexArray);
    }

    // ImGui_ImplOpenGL3_CreateFontsTexture
    public void CreateFontsTexture()
    {
        AssertImGuiContext();
        ImGuiIO* io = ImGui.GetIO();

        if (FontTexture != 0)
        { throw new InvalidOperationException("The fonts texture has already been created."); }

        // Build texture atlas
        byte* pixels;
        int width;
        int height;
        io->Fonts->GetTexDataAsRGBA32(&pixels, &width, &height);

        // Upload texture to graphics system
        int lastTexture = GL.GetInteger(GetPName.TextureBinding2D);
        GL.GenTextures(1, out FontTexture);
        GL.BindTexture(TextureTarget.Texture2D, FontTexture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0); //TODO: Handle GLES?
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)pixels);

        // Store out identifier
        io->Fonts->SetTexID((void*)FontTexture);

        // Restore state
        GL.BindTexture(TextureTarget.Texture2D, lastTexture);
    }

    // OpenGL3 Render function.
    // Note that this implementation is little overcomplicated because we are saving/setting up/restoring every OpenGL state explicitly.
    // This is in order to be able to run within an OpenGL engine that doesn't do so.
    // ImGui_ImplOpenGL3_RenderDrawData
    public void RenderDrawData(ImDrawData* drawData)
    {
        AssertImGuiContext();

        // Avoid rendering when minimized, scale coordinates for retina displays (screen coordinates != framebuffer coordinates)
        int frameBufferWidth = (int)(drawData->DisplaySize.X * drawData->FramebufferScale.X);
        int frameBufferHeight = (int)(drawData->DisplaySize.Y * drawData->FramebufferScale.Y);
        if (frameBufferWidth <= 0 || frameBufferHeight <= 0)
        { return; }

        // Backup GL state
        int lastActiveTexture = GL.GetInteger(GetPName.ActiveTexture);
        GL.ActiveTexture(TextureUnit.Texture0);
        int lastProgram = GL.GetInteger(GetPName.CurrentProgram);
        int lastTexture = GL.GetInteger(GetPName.TextureBinding2D);
        int lastSampler = GlVersion >= 330 ? GL.GetInteger(GetPName.SamplerBinding) : 0;
        int lastArrayBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);
        int lastVertexArrayObject = GL.GetInteger(GetPName.VertexArrayBinding);
        int* lastPolygonMode = stackalloc int[2];
        GL.GetInteger(GetPName.PolygonMode, lastPolygonMode);
        int* lastViewport = stackalloc int[4];
        GL.GetInteger(GetPName.Viewport, lastViewport);
        int* lastScissorBox = stackalloc int[4];
        GL.GetInteger(GetPName.ScissorBox, lastScissorBox);
        int lastBlendSrcRgb = GL.GetInteger(GetPName.BlendSrcRgb);
        int lastBlendDstRgb = GL.GetInteger(GetPName.BlendDstRgb);
        int lastBlendSrcAlpha = GL.GetInteger(GetPName.BlendSrcAlpha);
        int lastBlendDstAlpha = GL.GetInteger(GetPName.BlendDstAlpha);
        int lastBlendEquationRgb = GL.GetInteger(GetPName.BlendEquationRgb);
        int lastBlendEquationAlpha = GL.GetInteger(GetPName.BlendEquationAlpha);
        bool lastEnableBlend = GL.IsEnabled(EnableCap.Blend);
        bool lastEnableCullFace = GL.IsEnabled(EnableCap.CullFace);
        bool lastEnableDepthTest = GL.IsEnabled(EnableCap.DepthTest);
        bool lastEnableStencilTest = GL.IsEnabled(EnableCap.StencilTest);
        bool lastEnableScissorTest = GL.IsEnabled(EnableCap.ScissorTest);
        bool lastEnablePrimitiveRestart = GlVersion >= 310 ? GL.IsEnabled(EnableCap.PrimitiveRestart) : false;

        // ImGui_ImplOpenGL3_SetupRenderState
        void SetupRenderState(ImDrawData* drawData, int frameBufferWidth, int frameBufferHeight, int vertexArrayObject)
        {
            // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, polygon fill
            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.ScissorTest);
            if (GlVersion >= 310)
            { GL.Disable(EnableCap.PrimitiveRestart); }
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            // Support for GL 4.5 rarely used glClipControl(GL_UPPER_LEFT)
            bool clipOriginLowerLeft = true;
            if (HasClipOrigin)
            {
                if ((ClipOrigin)GL.GetInteger(GetPName.ClipOrigin) == ClipOrigin.UpperLeft)
                { clipOriginLowerLeft = false; }     
            }

            // Setup viewport, orthographic projection matrix
            // Our visible imgui space lies from draw_data->DisplayPos (top left) to draw_data->DisplayPos+data_data->DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
            GL.Viewport(0, 0, frameBufferWidth, frameBufferHeight);
            float left = drawData->DisplayPos.X;
            float right = drawData->DisplayPos.X + drawData->DisplaySize.X;
            float top = drawData->DisplayPos.Y;
            float bottom = drawData->DisplayPos.Y + drawData->DisplaySize.Y;

            // Swap top and bottom if origin is upper left
            if (!clipOriginLowerLeft)
            { (top, bottom) = (bottom, top); }

            Matrix4x4 orthoProjection = new
            (
                2.0f / (right - left), 0.0f, 0.0f, 0.0f,
                0.0f, 2.0f / (top - bottom), 0.0f, 0.0f,
                0.0f, 0.0f, -1.0f, 0.0f,
                (right + left) / (left - right), (top + bottom) / (bottom - top), 0.0f, 1.0f
            );

            GL.UseProgram(ShaderHandle);
            GL.Uniform1(AttribLocationTex, 0);
            GL.UniformMatrix4(AttribLocationProjMtx, 1, false, ref orthoProjection.M11);

            if (GlVersion >= 330)
            { GL.BindSampler(0, 0); } // We use combined texture/sampler state. Applications using GL 3.3 may set that otherwise.

            GL.BindVertexArray(vertexArrayObject);

            // Bind vertex/index buffers and setup attributes for ImDrawVert
            GL.BindBuffer(BufferTarget.ArrayBuffer, VboHandle);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementsHandle);
            GL.EnableVertexAttribArray(AttribLocationVtxPos);
            GL.EnableVertexAttribArray(AttribLocationVtxUV);
            GL.EnableVertexAttribArray(AttribLocationVtxColor);

            ImDrawVert dummyVertex = default;
            IntPtr OffsetOf<T>(ref ImDrawVert vert, ref T field)
                => Unsafe.ByteOffset(ref Unsafe.As<ImDrawVert, T>(ref vert), ref field);

            GL.VertexAttribPointer(AttribLocationVtxPos, 2, VertexAttribPointerType.Float, false, sizeof(ImDrawVert), OffsetOf(ref dummyVertex, ref dummyVertex.pos));
            GL.VertexAttribPointer(AttribLocationVtxUV, 2, VertexAttribPointerType.Float, false, sizeof(ImDrawVert), OffsetOf(ref dummyVertex, ref dummyVertex.uv));
            GL.VertexAttribPointer(AttribLocationVtxColor, 4, VertexAttribPointerType.UnsignedByte, true, sizeof(ImDrawVert), OffsetOf(ref dummyVertex, ref dummyVertex.col));
        }

        // Setup desired GL state
        // Recreate the VAO every time (this is to easily allow multiple GL contexts to be rendered to. VAO are not shared among GL contexts)
        // The renderer would actually work without any VAO bound, but then our VertexAttrib calls would overwrite the default one currently bound.
        int vertexArrayObject = 0;
        GL.GenVertexArrays(1, out vertexArrayObject);
        SetupRenderState(drawData, frameBufferWidth, frameBufferHeight, vertexArrayObject);

        // Will project scissor/clipping rectangles into framebuffer space
        Vector2 clipOffset = drawData->DisplayPos; // (0,0) unless using multi-viewports
        Vector2 clipScale = drawData->FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        // Render command lists
        for (int n = 0; n < drawData->CmdListsCount; n++)
        {
            ImDrawList* commandList = drawData->CmdLists[n];

            // Upload vertex/index buffers
            nint vertexBufferSize = commandList->VtxBuffer.Size * sizeof(ImDrawVert);
            nint indexBufferSize = commandList->IdxBuffer.Size * sizeof(ushort);

            if (VertexBufferSize < vertexBufferSize)
            {
                VertexBufferSize = vertexBufferSize;
                GL.BufferData(BufferTarget.ArrayBuffer, VertexBufferSize, IntPtr.Zero, BufferUsageHint.StreamDraw);
            }

            if (IndexBufferSize < indexBufferSize)
            {
                IndexBufferSize = indexBufferSize;
                GL.BufferData(BufferTarget.ElementArrayBuffer, IndexBufferSize, IntPtr.Zero, BufferUsageHint.StreamDraw);
            }

            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertexBufferSize, (IntPtr)commandList->VtxBuffer.Data);
            GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, indexBufferSize, (IntPtr)commandList->IdxBuffer.Data);

            for (int commandIndex = 0; commandIndex < commandList->CmdBuffer.Size; commandIndex++)
            {
                ImDrawCmd* command = &commandList->CmdBuffer.Data[commandIndex];

                if (command->UserCallback != null)
                {
                    // User callback, registered via ImDrawList::AddCallback()
                    // (ImDrawCallback_ResetRenderState is a special callback value used by the user to request the renderer to reset render state.)
                    if (command->UserCallback == ImDrawCmd.ImDrawCallback_ResetRenderState)
                    { SetupRenderState(drawData, frameBufferWidth, frameBufferHeight, vertexArrayObject); }
                    else
                    { command->UserCallback(commandList, command); }
                }
                else
                {
                    // Project scissor/clipping rectangles into framebuffer space
                    float clipMinX = (command->ClipRect.X - clipOffset.X) * clipScale.X;
                    float clipMinY = (command->ClipRect.Y - clipOffset.Y) * clipScale.Y;
                    float clipMaxX = (command->ClipRect.Z - clipOffset.X) * clipScale.X;
                    float clipMaxY = (command->ClipRect.W - clipOffset.Y) * clipScale.Y;
                    if (clipMaxX <= clipMinX || clipMaxY <= clipMinY)
                    { continue; }

                    // Apply scissor/clipping rectangle (Y is inverted in OpenGL)
                    GL.Scissor((int)clipMinX, (int)((float)frameBufferHeight - clipMaxY), (int)(clipMaxX - clipMinX), (int)(clipMaxY - clipMinY));

                    // Bind texture, draw
                    GL.BindTexture(TextureTarget.Texture2D, (int)command->GetTexID());
                    if (GlVersion >= 320)
                    { GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)command->ElemCount, DrawElementsType.UnsignedShort, (nint)command->IdxOffset * sizeof(ushort), (int)command->VtxOffset); }
                    else
                    { GL.DrawElements(PrimitiveType.Triangles, (int)command->ElemCount, DrawElementsType.UnsignedShort, (nint)command->IdxOffset * sizeof(ushort)); }
                }
            }
        }

        // Destroy the temporary VAO
        GL.DeleteVertexArrays(1, ref vertexArrayObject);

        // Restore modified GL state
        GL.UseProgram(lastProgram);
        GL.BindTexture(TextureTarget.Texture2D, lastTexture);
        if (GlVersion >= 330)
        { GL.BindSampler(0, lastSampler); }
        GL.ActiveTexture((TextureUnit)lastActiveTexture);
        GL.BindVertexArray(lastVertexArrayObject);
        GL.BindBuffer(BufferTarget.ArrayBuffer, lastArrayBuffer);
        GL.BlendEquationSeparate((BlendEquationMode)lastBlendEquationRgb, (BlendEquationMode)lastBlendEquationAlpha);
        GL.BlendFuncSeparate((BlendingFactorSrc)lastBlendSrcRgb, (BlendingFactorDest)lastBlendDstRgb, (BlendingFactorSrc)lastBlendSrcAlpha, (BlendingFactorDest)lastBlendDstAlpha);

        void GlEnable(EnableCap enableCap, bool value)
        {
            if (value)
            { GL.Enable(enableCap); }
            else
            { GL.Disable(enableCap); }
        }

        GlEnable(EnableCap.Blend, lastEnableBlend);
        GlEnable(EnableCap.CullFace, lastEnableCullFace);
        GlEnable(EnableCap.DepthTest, lastEnableDepthTest);
        GlEnable(EnableCap.StencilTest, lastEnableStencilTest);
        GlEnable(EnableCap.ScissorTest, lastEnableScissorTest);

        if (GlVersion >= 310)
        { GlEnable(EnableCap.PrimitiveRestart, lastEnablePrimitiveRestart); }

        GL.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)lastPolygonMode[0]);
        GL.Viewport(lastViewport[0], lastViewport[1], lastViewport[2], lastViewport[3]);
        GL.Scissor(lastScissorBox[0], lastScissorBox[1], lastScissorBox[2], lastScissorBox[3]);
    }

    // ImGui_ImplOpenGL3_DestroyDeviceObjects
    public void DestroyDeviceObjects()
    {
        AssertImGuiContext();

        if (VboHandle != 0)
        {
            GL.DeleteBuffers(1, ref VboHandle);
            VboHandle = 0;
        }

        if (ElementsHandle != 0)
        {
            GL.DeleteBuffers(1, ref ElementsHandle);
            ElementsHandle = 0;
        }

        if (ShaderHandle != 0)
        {
            GL.DeleteProgram(ShaderHandle);
            ShaderHandle = 0;
        }

        DestroyFontsTexture();
    }

    // ImGui_ImplOpenGL3_DestroyFontsTexture
    public void DestroyFontsTexture()
    {
        AssertImGuiContext();

        if (FontTexture != 0)
        {
            GL.DeleteTextures(1, ref FontTexture);
            ImGui.GetIO()->Fonts->SetTexID(null);
            FontTexture = 0;
        }
    }

    // ImGui_ImplOpenGL3_Shutdown
    public void Dispose()
    {
        AssertImGuiContext();
        ImGuiIO* io = ImGui.GetIO();

        ImGui.DestroyPlatformWindows(); // ImGui_ImplOpenGL3_ShutdownPlatformInterface
        ImGui.GetPlatformIO()->Renderer_RenderWindow = null;

        DestroyDeviceObjects();
        ImGui.MemFree(io->BackendRendererName);
        io->BackendRendererName = null;
        io->BackendRendererUserData = null;

        ThisHandle.Free();
        Context = null;
    }
}

