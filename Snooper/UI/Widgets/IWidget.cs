namespace Snooper.UI.Widgets;

public interface IWidget
{
    public void Render();
}

public interface IWidget<T> where T : IWidget
{
    public void Render(T? context);
}
