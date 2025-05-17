using System.Numerics;
using OpenTK.Windowing.Desktop;

namespace Snooper.UI;

public interface IController
{
    public void Load();
    public void Update(GameWindow wnd, float delta);
    public void TextInput(char c);
    public void Resize(int width, int height);
    public void Render();
}
