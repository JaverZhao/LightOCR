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
    private CaptureState _state = CaptureState.Idle;

    public CaptureState State => _state;
    public NormalizedImage? FullScreenImage { get; private set; }
    public Rectangle SelectionRect { get; private set; }

    public event Action<CaptureState, CaptureState>? StateChanged;
    public event Action<NormalizedImage>? CaptureCompleted;
    public event Action? CaptureCancelled;

    public bool BeginCapture()
    {
        if (!TryTransition(CaptureState.Idle, CaptureState.Preparing))
            return false;

        var captured = CaptureService.CaptureScreen();
        if (captured == null)
        {
            TransitionTo(CaptureState.Failed);
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

    public NormalizedImage? CompleteSelection()
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

        TransitionTo(CaptureState.Completed);
        CaptureCompleted?.Invoke(region);

        var result = region;
        Reset();
        return result;
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
