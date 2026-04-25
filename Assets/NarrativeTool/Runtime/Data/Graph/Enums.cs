namespace NarrativeTool.Data.Graph
{
    public enum PortDirection { Input, Output }
    public enum PortCapacity { Single, Multi }
    public enum NodeCategory { Event, Flow, Data }

    /// <summary>
    /// How an edge is routed visually. Bezier (default) draws a smooth S-curve
    /// through its ports and waypoints. Orthogonal (deferred — renders same as
    /// Bezier for now) will draw right-angle paths.
    /// </summary>
    public enum EdgeRoutingMode { Bezier, Orthogonal }
}