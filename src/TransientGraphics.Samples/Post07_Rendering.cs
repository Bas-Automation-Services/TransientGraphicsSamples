using Inventor;

namespace TransientGraphics.Samples;

// Post 7: rendering a SurfaceBody as client graphics — one GraphicsNode per body, the body as
// SurfaceGraphics, the edges in black.
public static class Post07_Rendering
{
    // Add `body` to `clientGraphics` as a translucent green preview with black edges.
    // Works for any ClientGraphics host: InteractionGraphics.PreviewClientGraphics during a
    // command, or a document's ClientGraphicsCollection for a longer-lived preview.
    public static GraphicsNode AddPreview(
        ClientGraphics clientGraphics, TransientObjects transientObjects, SurfaceBody body,
        Color? surfaceColor = null)
    {
        // Node ids must be unique within the ClientGraphics: AddNode with an id that is already
        // in use adds a second node with that id instead of throwing. Pick one above every existing node.
        int nextId = 1;
        foreach (GraphicsNode existing in clientGraphics)
            if (existing.Id >= nextId) nextId = existing.Id + 1;

        var node = clientGraphics.AddNode(nextId);
        node.Selectable = true;

        var surface = node.AddSurfaceGraphics(body);
        surface.Color = surfaceColor ?? transientObjects.CreateColor(0, 135, 0, 0.35);

        // Edges in black, via the surface's own displayed edges (one graphics object per node).
        var edgeColor = transientObjects.CreateColor(0, 0, 0);
        for (int i = 1; i <= surface.DisplayedEdges.Count; i++)
            surface.DisplayedEdges[i].Color = edgeColor;

        return node;
    }

    // The alternative: one CurveGraphics per body edge. Same look, more objects, but each edge
    // can be coloured on its own.
    public static GraphicsNode AddPreviewWithCurveEdges(
        ClientGraphics clientGraphics, TransientObjects transientObjects, SurfaceBody body)
    {
        int nextId = 1;
        foreach (GraphicsNode existing in clientGraphics)
            if (existing.Id >= nextId) nextId = existing.Id + 1;

        var node = clientGraphics.AddNode(nextId);
        var surface = node.AddSurfaceGraphics(body);
        surface.Color = transientObjects.CreateColor(0, 135, 0, 0.35);

        var edgeColor = transientObjects.CreateColor(0, 0, 0);
        foreach (Edge edge in body.Edges)
        {
            var curve = node.AddCurveGraphics(edge.Geometry);
            curve.Color = edgeColor;
        }
        return node;
    }

    // Remove everything a host owns.
    public static void ClearAll(ClientGraphics clientGraphics)
    {
        foreach (GraphicsNode node in clientGraphics)
            node.Delete();
    }
}
