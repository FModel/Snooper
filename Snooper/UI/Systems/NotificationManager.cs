using System.Numerics;
using ImGuiNET;

namespace Snooper.UI.Systems;

public class NotificationManager
{
    private readonly List<Notification> _notifications = [];
    
    public void PushNotification(string title, string message, float duration = 3f, Func<float>? progress = null)
    {
        _notifications.Add(new Notification(title, message, duration, progress));
    }
    
    public void DrawNotifications()
    {
        var deltaTime = ImGui.GetIO().DeltaTime;
        const float width = 300f;
        const float margin = 15f;
        const float spacing = 10f;
        const float fadeTime = 0.3f;
        
        var viewport = ImGui.GetMainViewport();
        var yOffset = 0f;

        for (var i = _notifications.Count - 1; i >= 0; i--)
        {
            var n = _notifications[i];
            n.Elapsed += deltaTime;
            
            var isFadingOut = n.Elapsed > n.Duration;
            if (n.Progress != null) isFadingOut &= n.Progress.Invoke() >= 1f;
            n.Alpha = isFadingOut ? MathF.Max(0f, n.Alpha - deltaTime / fadeTime) : MathF.Min(1f, n.Alpha + deltaTime / fadeTime);

            if (n.Alpha <= 0f && isFadingOut)
            {
                _notifications.RemoveAt(i);
                continue;
            }

            var pos = new Vector2(viewport.Pos.X + margin, viewport.Pos.Y + viewport.Size.Y - margin - yOffset);
            ImGui.SetNextWindowPos(pos, ImGuiCond.Always, new Vector2(0, 1));
            ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, float.MaxValue));

            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, n.Alpha);

            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse |
                                           ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize;

            ImGui.Begin($"##Notification{i}", flags);
            ImGui.TextColored(new Vector4(1, 1, 0.7f, 1), n.Title);
            ImGui.TextWrapped(n.Message);
            if (n.Progress != null)
            {
                ImGui.Spacing();
                ImGui.ProgressBar(n.Progress.Invoke(), new Vector2(width - 20, 0));
            }

            yOffset += ImGui.GetWindowSize().Y + spacing;
            ImGui.End();
            ImGui.PopStyleVar();
        }
    }
    
    private class Notification(string title, string message, float duration, Func<float>? progress)
    {
        public readonly string Title = title;
        public readonly string Message = message;
        public readonly float Duration = duration; // how long to show before fade out
        public readonly Func<float>? Progress = progress; // null = no progress bar
        
        internal float Elapsed;
        internal float Alpha;
    }
}