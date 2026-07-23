using LightOCR.App.Models;
using Serilog;
using System.Drawing;

namespace LightOCR.App.Services;

public enum CaptureState
{
    Idle,
    Preparing,
    Selecting,
    Selected,
    Recognizing,
    Completed,
    Cancelled,
    Failed
}

public sealed class CaptureSessionService
{
    private readonly Func<NormalizedImage?> _captureScreen;
    private CaptureState _state = CaptureState.Idle;

    public CaptureSessionService(Func<NormalizedImage?>? captureScreen = null)
    {
        _captureScreen = captureScreen ?? CaptureService.CaptureScreen;
    }

    public CaptureState State => _state;
    public NormalizedImage? FullScreenImage { get; private set; }
    public Rectangle SelectionRect { get; private set; }

    public event Action<CaptureState, CaptureState>? StateChanged;
    public event Func<NormalizedImage, Task>? CaptureCompleted;
    public event Action? CaptureCancelled;

    public bool BeginCapture()
    {
        if (!TryTransition(CaptureState.Idle, CaptureState.Preparing))
            return false;

        var captured = _captureScreen();
        if (captured == null)
        {
            TransitionTo(CaptureState.Failed);
            Reset();
            return false;
        }

        FullScreenImage = captured;
        TransitionTo(CaptureState.Selecting);
        return true;
    }

    public void UpdateSelection(Rectangle rect)
    {
        SelectionRect = rect;
        if (_state == CaptureState.Selecting && rect.Width > 5 && rect.Height > 5)
        {
            TransitionTo(CaptureState.Selected);
        }
        else if (_state == CaptureState.Selected && (rect.Width <= 5 || rect.Height <= 5))
        {
            TransitionTo(CaptureState.Selecting);
        }
    }

    public void CancelCapture()
    {
        if (_state == CaptureState.Idle || _state == CaptureState.Cancelled)
            return;

        TransitionTo(CaptureState.Cancelled);
        CaptureCancelled?.Invoke();
        Reset();
    }

    public async Task<NormalizedImage?> CompleteSelectionAsync()
    {
        if (_state != CaptureState.Selected) return null;

        TransitionTo(CaptureState.Recognizing);

        if (FullScreenImage == null)
        {
            TransitionTo(CaptureState.Failed);
            Reset();
            return null;
        }

        var region = CaptureService.CaptureRegion(FullScreenImage, SelectionRect);
        if (region == null)
        {
            TransitionTo(CaptureState.Failed);
            Reset();
            return null;
        }

        try
        {
            var handlers = CaptureCompleted?.GetInvocationList()
                .Cast<Func<NormalizedImage, Task>>()
                .ToArray() ?? [];
            foreach (var handler in handlers)
                await handler(region);

            TransitionTo(CaptureState.Completed);
            return region;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Capture completion handler failed");
            TransitionTo(CaptureState.Failed);
            return null;
        }
        finally
        {
            Reset();
        }
    }

    public void Reset()
    {
        FullScreenImage = null;
        SelectionRect = Rectangle.Empty;
        TransitionTo(CaptureState.Idle);
    }

    private bool TryTransition(CaptureState from, CaptureState to)
    {
        if (_state != from) return false;
        TransitionTo(to);
        return true;
    }

    private void TransitionTo(CaptureState newState)
    {
        var old = _state;
        _state = newState;
        StateChanged?.Invoke(old, newState);
        Log.Debug("Capture state: {Old} → {New}", old, newState);
    }
}
