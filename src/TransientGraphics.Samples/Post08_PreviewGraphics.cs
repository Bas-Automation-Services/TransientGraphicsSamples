using Inventor;

namespace TransientGraphics.Samples;

// Post 8: a small preview manager. Nodes are tracked by a stable string key so an update
// replaces just the affected node; placement-only changes set node.Transformation instead of
// rebuilding; BeginUpdate/EndUpdate coalesce viewport refreshes across a batch.
public sealed class Post08_PreviewGraphics(Inventor.Application app, ClientGraphics clientGraphics)
{
    private readonly Dictionary<string, GraphicsNode> _nodesByKey = new();
    private readonly Dictionary<string, string> _contentHashByKey = new();
    private int _nextNodeId = 1;
    private int _updateDepth;

    private TransientObjects To => app.TransientObjects;

    public void AddOrReplace(string key, SurfaceBody body, Color? color = null)
    {
        Remove(key, refresh: false);

        var node = clientGraphics.AddNode(_nextNodeId++);
        node.Selectable = true;

        var surface = node.AddSurfaceGraphics(body);
        surface.Color = color ?? To.CreateColor(0, 135, 0, 0.35);

        var edgeColor = To.CreateColor(0, 0, 0);
        for (int i = 1; i <= surface.DisplayedEdges.Count; i++)
            surface.DisplayedEdges[i].Color = edgeColor;

        _nodesByKey[key] = node;
        Refresh();
    }

    // Skip the rebuild when the inputs that produced the body have not changed. `contentHash`
    // is any stable, cheap digest of those inputs; `build` runs only on a miss.
    public void AddOrReplaceIfChanged(string key, string contentHash, Func<SurfaceBody> build, Color? color = null)
    {
        if (_contentHashByKey.TryGetValue(key, out var existing) && existing == contentHash)
            return;

        AddOrReplace(key, build(), color);
        _contentHashByKey[key] = contentHash;
    }

    // Placement-only change: keep the geometry, move the node.
    public bool Transform(string key, Matrix transform)
    {
        if (!_nodesByKey.TryGetValue(key, out var node))
            return false;

        node.Transformation = transform;
        Refresh();
        return true;
    }

    public void Remove(string key, bool refresh = true)
    {
        if (!_nodesByKey.TryGetValue(key, out var existing))
            return;

        try { existing.Delete(); } catch (System.Runtime.InteropServices.COMException) { /* host already gone */ }
        _nodesByKey.Remove(key);
        _contentHashByKey.Remove(key);
        if (refresh) Refresh();
    }

    public void Clear()
    {
        foreach (var node in _nodesByKey.Values)
        {
            try { node.Delete(); } catch (System.Runtime.InteropServices.COMException) { }
        }
        _nodesByKey.Clear();
        _contentHashByKey.Clear();
        try { app.ActiveView.Update(); } catch (System.Runtime.InteropServices.COMException) { }
    }

    public void BeginUpdate() => _updateDepth++;

    public void EndUpdate()
    {
        _updateDepth--;
        if (_updateDepth == 0) app.ActiveView.Update();
    }

    private void Refresh()
    {
        if (_updateDepth == 0) app.ActiveView.Update();
    }
}
