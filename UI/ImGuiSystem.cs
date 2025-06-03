using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.UI.Containers.Textures;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;

namespace Snooper.UI;

public class ImGuiSystem : IResizable
{
    private bool _frameBegun;
    private Vector2 _size;

    private readonly ImGuiFontTexture _fontTexture;
    private readonly VertexArray _vertexArray;
    private readonly ArrayBuffer<ImDrawVert> _vertexBuffer;
    private readonly ElementArrayBuffer<ushort> _indexBuffer;
    private readonly ShaderProgram _shader;

    public ImGuiSystem()
    {
        ImGui.SetCurrentContext(ImGui.CreateContext());

        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.Fonts.Build();

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;
        io.ConfigDockingWithShift = true;
        io.ConfigWindowsMoveFromTitleBarOnly = true;
        io.BackendRendererUserData = 0;

        _fontTexture = new ImGuiFontTexture();
        _vertexArray = new VertexArray();
        _vertexBuffer = new ArrayBuffer<ImDrawVert>(1000);
        _indexBuffer = new ElementArrayBuffer<ushort>(1500);
        _shader = new ShaderProgram(
@"#version 330 core

uniform mat4 projection_matrix;

layout(location = 0) in vec2 in_position;
layout(location = 1) in vec2 in_texCoord;
layout(location = 2) in vec4 in_color;

out vec4 color;
out vec2 texCoord;

void main()
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
    color = in_color;
    texCoord = in_texCoord;
}",
@"#version 330 core

uniform sampler2D in_fontTexture;

in vec4 color;
in vec2 texCoord;

out vec4 outputColor;

void main()
{
    outputColor = color * texture(in_fontTexture, texCoord);
}");
    }

    public void Load()
    {
        // save previous state
        var prevVao = GL.GetInteger(GetPName.VertexArrayBinding);
        var prevVertices = GL.GetInteger(GetPName.ArrayBufferBinding);
        var prevIndices = GL.GetInteger(GetPName.ElementArrayBufferBinding);

        _vertexArray.Generate();
        _vertexArray.Bind();

        _vertexBuffer.Generate();
        _vertexBuffer.Bind();
        _vertexBuffer.SetData();

        _indexBuffer.Generate();
        _indexBuffer.Bind();
        _indexBuffer.SetData();
        
        var stride = _vertexBuffer.Stride;
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);
        GL.EnableVertexAttribArray(2);

        _fontTexture.Generate();

        _shader.Generate();
        _shader.Link();

        // restore previous state
        GL.BindVertexArray(prevVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, prevVertices);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, prevIndices);

        CheckForErrors("End of ImGui setup");
    }

    public void Update(GameWindow wnd, float delta)
    {
        if (_frameBegun)
        {
            ImGui.Render();
        }

        UpdateIo(wnd, delta);

        _frameBegun = true;
        ImGui.NewFrame();
    }

    private readonly Queue<char> _pressedChars = [];
    public void TextInput(char c)
    {
        _pressedChars.Enqueue(c);
    }

    public void Resize(int newWidth, int newHeight)
    {
        _size = new Vector2(newWidth, newHeight);
    }

    private void UpdateIo(GameWindow wnd, float delta)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = _size;
        io.DeltaTime = delta;

        var mState = wnd.MouseState;
        var kState = wnd.KeyboardState;

        io.AddMousePosEvent(mState.X, mState.Y);
        io.AddMouseButtonEvent(0, mState[MouseButton.Left]);
        io.AddMouseButtonEvent(1, mState[MouseButton.Right]);
        io.AddMouseButtonEvent(2, mState[MouseButton.Middle]);
        io.AddMouseButtonEvent(3, mState[MouseButton.Button1]);
        io.AddMouseButtonEvent(4, mState[MouseButton.Button2]);
        io.AddMouseWheelEvent(mState.ScrollDelta.X, mState.ScrollDelta.Y);

        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (key == Keys.Unknown) continue;
            io.AddKeyEvent(TranslateKey(key), kState.IsKeyDown(key));
        }

        while (_pressedChars.TryDequeue(out char c))
        {
            io.AddInputCharacter(c);
        }

        io.KeyShift = kState.IsKeyDown(Keys.LeftShift) || kState.IsKeyDown(Keys.RightShift);
        io.KeyCtrl = kState.IsKeyDown(Keys.LeftControl) || kState.IsKeyDown(Keys.RightControl);
        io.KeyAlt = kState.IsKeyDown(Keys.LeftAlt) || kState.IsKeyDown(Keys.RightAlt);
        io.KeySuper = kState.IsKeyDown(Keys.LeftSuper) || kState.IsKeyDown(Keys.RightSuper);
    }

    public void Render()
    {
        if (_frameBegun)
        {
            _frameBegun = false;
            ImGui.Render();
            RenderImDrawData(ImGui.GetDrawData());
        }
    }

    private void RenderImDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0) return;

        var prevVao = GL.GetInteger(GetPName.VertexArrayBinding);
        var prevArrayBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);
        var prevProgram = GL.GetInteger(GetPName.CurrentProgram);
        var prevBlendEnabled = GL.GetBoolean(GetPName.Blend);
        var prevScissorTestEnabled = GL.GetBoolean(GetPName.ScissorTest);
        var prevBlendEquationRgb = GL.GetInteger(GetPName.BlendEquationRgb);
        var prevBlendEquationAlpha = GL.GetInteger(GetPName.BlendEquationAlpha);
        var prevBlendFuncSrcRgb = GL.GetInteger(GetPName.BlendSrcRgb);
        var prevBlendFuncSrcAlpha = GL.GetInteger(GetPName.BlendSrcAlpha);
        var prevBlendFuncDstRgb = GL.GetInteger(GetPName.BlendDstRgb);
        var prevBlendFuncDstAlpha = GL.GetInteger(GetPName.BlendDstAlpha);
        var prevCullFaceEnabled = GL.GetBoolean(GetPName.CullFace);
        var prevDepthTestEnabled = GL.GetBoolean(GetPName.DepthTest);
        var prevActiveTexture = GL.GetInteger(GetPName.ActiveTexture);
        GL.ActiveTexture(TextureUnit.Texture0);
        var prevTexture2D = GL.GetInteger(GetPName.TextureBinding2D);

        Span<int> prevScissorBox = stackalloc int[4];
        unsafe
        {
            fixed (int* iptr = &prevScissorBox[0])
            {
                GL.GetInteger(GetPName.ScissorBox, iptr);
            }
        }

        // Setup orthographic projection matrix into our constant buffer
        var io = ImGui.GetIO();
        _shader.Use();
        _shader.SetUniform("projection_matrix", Matrix4x4.CreateOrthographicOffCenter(0.0f, io.DisplaySize.X, io.DisplaySize.Y, 0.0f, -1.0f, 1.0f));
        _shader.SetUniform("in_fontTexture", 0);
        CheckForErrors("Projection");

        drawData.ScaleClipRects(io.DisplayFramebufferScale);

        GL.Enable(EnableCap.Blend);
        GL.Enable(EnableCap.ScissorTest);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);

        // Render command lists
        _vertexArray.Bind();
        _vertexBuffer.Bind();
        _indexBuffer.Bind();
        for (var i = 0; i < drawData.CmdListsCount; i++)
        {
            var cmd = drawData.CmdLists[i];

            _vertexBuffer.Update(cmd.VtxBuffer.Size, cmd.VtxBuffer.Data);
            CheckForErrors($"Data Vert {i}");

            _indexBuffer.Update(cmd.IdxBuffer.Size, cmd.IdxBuffer.Data);
            CheckForErrors($"Data Idx {i}");

            for (var j = 0; j < cmd.CmdBuffer.Size; j++)
            {
                var pcmd = cmd.CmdBuffer[j];
                if (pcmd.UserCallback != IntPtr.Zero) throw new NotImplementedException();

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);
                CheckForErrors("Texture");

                // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                var clip = pcmd.ClipRect;
                GL.Scissor((int)clip.X, (int)(_size.Y - clip.W), (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));
                CheckForErrors("Scissor");

                if (io.BackendFlags.HasFlag(ImGuiBackendFlags.RendererHasVtxOffset))
                {
                    GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (IntPtr)(pcmd.IdxOffset * sizeof(ushort)), unchecked((int)pcmd.VtxOffset));
                }
                else
                {
                    GL.DrawElements(BeginMode.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (int)pcmd.IdxOffset * sizeof(ushort));
                }
                CheckForErrors("Draw");
            }
        }
        CheckForErrors("VAO");

        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.ScissorTest);

        // Reset state
        GL.BindTexture(TextureTarget.Texture2D, prevTexture2D);
        GL.ActiveTexture((TextureUnit)prevActiveTexture);
        GL.UseProgram(prevProgram);
        GL.BindVertexArray(prevVao);
        GL.Scissor(prevScissorBox[0], prevScissorBox[1], prevScissorBox[2], prevScissorBox[3]);
        GL.BindBuffer(BufferTarget.ArrayBuffer, prevArrayBuffer);
        GL.BlendEquationSeparate((BlendEquationMode)prevBlendEquationRgb, (BlendEquationMode)prevBlendEquationAlpha);
        GL.BlendFuncSeparate((BlendingFactorSrc)prevBlendFuncSrcRgb, (BlendingFactorDest)prevBlendFuncDstRgb, (BlendingFactorSrc)prevBlendFuncSrcAlpha, (BlendingFactorDest)prevBlendFuncDstAlpha);
        if (prevBlendEnabled) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
        if (prevDepthTestEnabled) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
        if (prevCullFaceEnabled) GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
        if (prevScissorTestEnabled) GL.Enable(EnableCap.ScissorTest); else GL.Disable(EnableCap.ScissorTest);
    }

    private void CheckForErrors(string title)
    {
        ErrorCode error;
        var i = 1;
        while ((error = GL.GetError()) != ErrorCode.NoError)
        {
            Console.WriteLine($"{title} ({i++}): {error}");
        }
    }

    private ImGuiKey TranslateKey(Keys key)
    {
        if (key is >= Keys.D0 and <= Keys.D9)
            return key - Keys.D0 + ImGuiKey._0;

        if (key is >= Keys.A and <= Keys.Z)
            return key - Keys.A + ImGuiKey.A;

        if (key is >= Keys.KeyPad0 and <= Keys.KeyPad9)
            return key - Keys.KeyPad0 + ImGuiKey.Keypad0;

        if (key is >= Keys.F1 and <= Keys.F24)
            return key - Keys.F1 + ImGuiKey.F24;

        return key switch
        {
            Keys.Tab => ImGuiKey.Tab,
            Keys.Left => ImGuiKey.LeftArrow,
            Keys.Right => ImGuiKey.RightArrow,
            Keys.Up => ImGuiKey.UpArrow,
            Keys.Down => ImGuiKey.DownArrow,
            Keys.PageUp => ImGuiKey.PageUp,
            Keys.PageDown => ImGuiKey.PageDown,
            Keys.Home => ImGuiKey.Home,
            Keys.End => ImGuiKey.End,
            Keys.Insert => ImGuiKey.Insert,
            Keys.Delete => ImGuiKey.Delete,
            Keys.Backspace => ImGuiKey.Backspace,
            Keys.Space => ImGuiKey.Space,
            Keys.Enter => ImGuiKey.Enter,
            Keys.Escape => ImGuiKey.Escape,
            Keys.Apostrophe => ImGuiKey.Apostrophe,
            Keys.Comma => ImGuiKey.Comma,
            Keys.Minus => ImGuiKey.Minus,
            Keys.Period => ImGuiKey.Period,
            Keys.Slash => ImGuiKey.Slash,
            Keys.Semicolon => ImGuiKey.Semicolon,
            Keys.Equal => ImGuiKey.Equal,
            Keys.LeftBracket => ImGuiKey.LeftBracket,
            Keys.Backslash => ImGuiKey.Backslash,
            Keys.RightBracket => ImGuiKey.RightBracket,
            Keys.GraveAccent => ImGuiKey.GraveAccent,
            Keys.CapsLock => ImGuiKey.CapsLock,
            Keys.ScrollLock => ImGuiKey.ScrollLock,
            Keys.NumLock => ImGuiKey.NumLock,
            Keys.PrintScreen => ImGuiKey.PrintScreen,
            Keys.Pause => ImGuiKey.Pause,
            Keys.KeyPadDecimal => ImGuiKey.KeypadDecimal,
            Keys.KeyPadDivide => ImGuiKey.KeypadDivide,
            Keys.KeyPadMultiply => ImGuiKey.KeypadMultiply,
            Keys.KeyPadSubtract => ImGuiKey.KeypadSubtract,
            Keys.KeyPadAdd => ImGuiKey.KeypadAdd,
            Keys.KeyPadEnter => ImGuiKey.KeypadEnter,
            Keys.KeyPadEqual => ImGuiKey.KeypadEqual,
            Keys.LeftShift => ImGuiKey.ModShift,
            Keys.LeftControl => ImGuiKey.LeftCtrl,
            Keys.LeftAlt => ImGuiKey.LeftAlt,
            Keys.LeftSuper => ImGuiKey.LeftSuper,
            Keys.RightShift => ImGuiKey.RightShift,
            Keys.RightControl => ImGuiKey.RightCtrl,
            Keys.RightAlt => ImGuiKey.RightAlt,
            Keys.RightSuper => ImGuiKey.RightSuper,
            Keys.Menu => ImGuiKey.Menu,
            _ => ImGuiKey.None
        };
    }
}
