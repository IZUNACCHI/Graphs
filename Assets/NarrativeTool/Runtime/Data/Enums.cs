namespace NarrativeTool.Data
{
    /// <summary>Which side of a node a port lives on.</summary>
    public enum PortDirection { Input, Output }

    /// <summary>
    /// How many edges a port can hold.
    ///   Single — one edge max (typical for an output flow port like Unreal's "Then")
    ///   Multi  — many edges allowed (typical for an input flow port where multiple
    ///            upstream branches can converge)
    /// </summary>
    public enum PortCapacity { Single, Multi }

    /// <summary>
    /// Visual category for a node, used purely to choose the header colour.
    /// Matches Unreal's convention: events red, flow grey, pure functions green,
    /// data blue, etc. v1 uses Event / Flow / Data.
    /// </summary>
    public enum NodeCategory { Event, Flow, Data }
}