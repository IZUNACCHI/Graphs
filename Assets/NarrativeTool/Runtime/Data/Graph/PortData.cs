namespace NarrativeTool.Data.Graph
{
    /// <summary>
    /// One connection point on a node. Ports belong to a Node and are identified
    /// by (NodeId, PortId). Edges reference ports by that pair.
    ///
    /// TypeTag drives compatibility: two ports can connect iff their TypeTag
    /// strings match. v1 only uses "flow"; data types come later.
    /// </summary>
    public sealed class PortData
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public PortDirection Direction { get; set; }
        public PortCapacity Capacity { get; set; }
        public string TypeTag { get; set; }

        public PortData(string id, string label, PortDirection direction,
                    PortCapacity capacity, string typeTag)
        {
            Id = id; Label = label; Direction = direction;
            Capacity = capacity; TypeTag = typeTag;
        }

        public PortData() { }
    }
}