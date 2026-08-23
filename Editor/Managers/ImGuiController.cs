using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using ImGuiNET;
using ImGuizmoNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.UI;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;

namespace Editor.Managers;

public class ImGuiController : IResizable, IDisposable
{
    private bool _frameBegun;

    private readonly ImGuiFontTexture _fontTexture = new();
    private readonly VertexArray _vao = new();
    private readonly ElementArrayBuffer<ushort> _ebo = new();
    private readonly ArrayBuffer<ImDrawVert> _vbo = new();
    private readonly ShaderProgram _shader = new EmbeddedShader("imgui", Assembly.GetExecutingAssembly());

    public void Load()
    {
        _vao.Generate();
        _ebo.Generate();
        _vbo.Generate();

        // Initial size, will grow as needed
        _ebo.Allocate(45000);
        _vbo.Allocate(30000);

        GL.VertexArrayVertexBuffer(_vao, 0, _vbo, 0, _vbo.Stride);
        GL.VertexArrayElementBuffer(_vao, _ebo);
        GL.VertexArrayAttribFormat(_vao, 0, 2, VertexAttribType.Float, false, 0);
        GL.VertexArrayAttribFormat(_vao, 1, 2, VertexAttribType.Float, false, 8);
        GL.VertexArrayAttribFormat(_vao, 2, 4, VertexAttribType.UnsignedByte, true, 16);
        GL.EnableVertexArrayAttrib(_vao, 0);
        GL.EnableVertexArrayAttrib(_vao, 1);
        GL.EnableVertexArrayAttrib(_vao, 2);
        GL.VertexArrayAttribBinding(_vao, 0, 0);
        GL.VertexArrayAttribBinding(_vao, 1, 0);
        GL.VertexArrayAttribBinding(_vao, 2, 0);

        _fontTexture.Generate();

        _shader.Generate();
        _shader.Link();
        ImGuiDrawCallbacks.Instance.Bind(
            channel => _shader.SetUniform("in_channelSwizzle", channel),
            encode => _shader.SetUniform("in_encodeSrgb", encode));

        CheckForErrors("End of ImGui setup");
    }

    public void Update(GameWindow wnd, float delta)
    {
        if (_frameBegun)
        {
            ImGui.Render();
        }

        var io = ImGui.GetIO();
        io.DeltaTime = delta;

        var mState = wnd.MouseState;
        var kState = wnd.KeyboardState;

        // Only send mouse events to ImGui when cursor is not grabbed
        if (wnd.CursorState != CursorState.Grabbed)
        {
            io.AddMousePosEvent(mState.X, mState.Y);
            io.AddMouseButtonEvent(0, mState[MouseButton.Left]);
            io.AddMouseButtonEvent(1, mState[MouseButton.Right]);
            io.AddMouseButtonEvent(2, mState[MouseButton.Middle]);
            io.AddMouseButtonEvent(3, mState[MouseButton.Button1]);
            io.AddMouseButtonEvent(4, mState[MouseButton.Button2]);
            io.AddMouseWheelEvent(mState.ScrollDelta.X, mState.ScrollDelta.Y);
        }

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

        _frameBegun = true;
        ImGui.NewFrame();
        ImGuizmo.BeginFrame();
    }

    public void Render()
    {
        if (!_frameBegun) return;
        _frameBegun = false;

        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        if (drawData.CmdListsCount == 0) return;

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

        var io = ImGui.GetIO();
        drawData.ScaleClipRects(io.DisplayFramebufferScale);

        using (Profiler.Draw())
        {
            // Setup orthographic projection matrix into our constant buffer
            _shader.Use();
            _shader.SetUniform("projection_matrix", Matrix4x4.CreateOrthographicOffCenter(0.0f, io.DisplaySize.X, io.DisplaySize.Y, 0.0f, -1.0f, 1.0f));
            _shader.SetUniform("in_fontTexture", 0);
            _shader.SetUniform("in_channelSwizzle", -1);
            _shader.SetUniform("in_encodeSrgb", false);
            CheckForErrors("Projection");

            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.ScissorTest);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);

            _vao.Bind();
            _ebo.Bind();
            _vbo.Bind();

            // Render command lists
            for (var i = 0; i < drawData.CmdListsCount; i++)
            {
                var cmd = drawData.CmdLists[i];

                _vbo.Update(cmd.VtxBuffer.Size, cmd.VtxBuffer.Data);
                CheckForErrors($"Data Vert {i}");

                _ebo.Update(cmd.IdxBuffer.Size, cmd.IdxBuffer.Data);
                CheckForErrors($"Data Idx {i}");

                for (var j = 0; j < cmd.CmdBuffer.Size; j++)
                {
                    var pcmd = cmd.CmdBuffer[j];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        if (pcmd.UserCallback == ImGuiDrawCallbacks.Marker)
                        {
                            ImGuiDrawCallbacks.Instance.Invoke((int) pcmd.UserCallbackData);
                        }
                        continue;
                    }

                    GL.BindTextureUnit(0, (uint)pcmd.TextureId);
                    CheckForErrors("Texture");

                    // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                    var clip = pcmd.ClipRect;
                    GL.Scissor((int)clip.X, (int)(io.DisplaySize.Y - clip.W), (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));
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

            ImGuiDrawCallbacks.Instance.Clear();

            _vbo.Unbind();
            _ebo.Unbind();
            _vao.Unbind();
            CheckForErrors("VAO");

            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.ScissorTest);

            _shader.Unuse();
        }

        // Reset state
        GL.BindTexture(TextureTarget.Texture2D, prevTexture2D);
        GL.ActiveTexture((TextureUnit)prevActiveTexture);
        GL.UseProgram(prevProgram);
        GL.Scissor(prevScissorBox[0], prevScissorBox[1], prevScissorBox[2], prevScissorBox[3]);
        GL.BlendEquationSeparate((BlendEquationMode)prevBlendEquationRgb, (BlendEquationMode)prevBlendEquationAlpha);
        GL.BlendFuncSeparate((BlendingFactorSrc)prevBlendFuncSrcRgb, (BlendingFactorDest)prevBlendFuncDstRgb, (BlendingFactorSrc)prevBlendFuncSrcAlpha, (BlendingFactorDest)prevBlendFuncDstAlpha);
        if (prevBlendEnabled) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
        if (prevDepthTestEnabled) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
        if (prevCullFaceEnabled) GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
        if (prevScissorTestEnabled) GL.Enable(EnableCap.ScissorTest); else GL.Disable(EnableCap.ScissorTest);
    }

    private readonly Queue<char> _pressedChars = [];
    public void TextInput(char c)
    {
        _pressedChars.Enqueue(c);
    }

    public void Resize(int newWidth, int newHeight)
    {
        ImGui.GetIO().DisplaySize = new Vector2(newWidth, newHeight);
    }

    [Conditional("DEBUG")]
    private void CheckForErrors(string title)
    {
        ErrorCode error;
        var i = 1;
        while ((error = GL.GetError()) != ErrorCode.NoError)
        {
            Log.Error("{Title} ({I}): {Error}", title, i++, error);
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

    public void Dispose()
    {
        _fontTexture.Dispose();
        _vao.Dispose();
        _ebo.Dispose();
        _vbo.Dispose();
        _shader.Dispose();
    }
}
