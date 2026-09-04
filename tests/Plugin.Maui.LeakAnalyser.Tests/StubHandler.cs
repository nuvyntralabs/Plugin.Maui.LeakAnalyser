namespace Plugin.Maui.LeakAnalyser.Tests;

internal class StubHandler : IViewHandler
{
    public bool Disconnected { get; private set; }

    public IView? VirtualView { get; set; }

    IElement? IElementHandler.VirtualView => VirtualView;

    public object? PlatformView { get; set; }

    public object? ContainerView { get; set; }

    public bool HasContainer { get; set; }

    public IMauiContext? MauiContext { get; set; }

    public void SetVirtualView(IView view) => VirtualView = view;

    void IElementHandler.SetVirtualView(IElement view) => VirtualView = view as IView;

    public void SetMauiContext(IMauiContext mauiContext) => MauiContext = mauiContext;

    public void UpdateValue(string property) { }

    public void Invoke(string command, object? args = null) { }

    public virtual void DisconnectHandler() => Disconnected = true;

    public Size GetDesiredSize(double widthConstraint, double heightConstraint) => Size.Zero;

    public void PlatformArrange(Rect frame) { }
}

internal sealed class ThrowingHandler : StubHandler
{
    public override void DisconnectHandler() => throw new ObjectDisposedException("handler");
}
