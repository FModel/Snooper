using System.Numerics;
using ImGuiNET;
using Snooper.Core;

namespace Editor.Widgets;

public class NotificationOverlayWidget
{
    private const float FadeOut = 0.35f; // toasts appear instantly, only the exit is faded

    private const float PadX = 10f;
    private const float PadY = 5f;
    private const float IconGap = 7f;
    private const float Gap = 5f;             // vertical gap between two stacked toasts
    private const float StatsClearance = 30f; // clears the fps / disclaimer line along the bottom

    public void Draw(ImDrawListPtr drawList, Vector2 contentPos, Vector2 contentSize, float bottomClearance)
    {
        Notifications.Advance(ImGui.GetIO().DeltaTime);

        var active = Notifications.Active;
        if (active.Count == 0) return;

        var font = ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold];
        var fontSize = ImGui.GetFontSize();
        var height = MathF.Round(fontSize * 1.2f) + PadY * 2f;

        // newest sits at the bottom, older ones stack upwards
        var bottom = contentPos.Y + contentSize.Y - bottomClearance - StatsClearance;
        for (var i = active.Count - 1; i >= 0; i--)
        {
            var notification = active[i];
            var alpha = MathF.Min(1f, (Notifications.Lifetime - notification.Age) / FadeOut);

            var iconWidth = notification.Icon.Length > 0 ? font.CalcTextSizeA(fontSize, float.MaxValue, 0f, notification.Icon).X + IconGap : 0f;
            var textWidth = font.CalcTextSizeA(fontSize, float.MaxValue, 0f, notification.Text).X;
            var width = PadX * 2f + iconWidth + textWidth;

            var min = new Vector2(contentPos.X + MathF.Round((contentSize.X - width) * 0.5f), bottom - height);
            var max = min + new Vector2(width, height);

            // the same plate as the hardware band, a toast is just a piece of the same hud
            drawList.AddRectFilled(min, max, Color(0f, 0f, 0f, 0.78f * alpha));
            drawList.AddRect(min, max, Color(1f, 1f, 1f, 0.12f * alpha));

            var textPos = min + new Vector2(PadX, PadY);
            if (iconWidth > 0f)
            {
                drawList.AddText(font, fontSize, textPos, Color(0.55f, 0.59f, 0.65f, alpha), notification.Icon);
                textPos.X += iconWidth;
            }

            drawList.AddText(font, fontSize, textPos, Color(0.86f, 0.88f, 0.90f, alpha), notification.Text);

            bottom -= height + Gap;
        }
    }

    private static uint Color(float r, float g, float b, float a) => (uint) (a * 255f) << 24 | (uint) (b * 255f) << 16 | (uint) (g * 255f) << 8 | (uint) (r * 255f);
}
