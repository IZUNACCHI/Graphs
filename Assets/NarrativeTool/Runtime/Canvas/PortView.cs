using NarrativeTool.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// One row on a NodeView: a port glyph (arrow for flow ports) and a label.
    /// Input port glyph sits on the left edge; output port glyph sits on the
    /// right edge. The glyph element is used as the anchor point for edges.
    /// </summary>
    public sealed class PortView : VisualElement
    {
        public Port Port { get; }
        public VisualElement Glyph { get; }
        public Label LabelElement { get; }

        public PortView(Port port)
        {
            Port = port;
            AddToClassList("nt-port");
            AddToClassList(port.Direction == PortDirection.Input ? "nt-port-input" : "nt-port-output");

            Glyph = new VisualElement();
            Glyph.AddToClassList("nt-port-glyph");
            Glyph.AddToClassList("nt-port-flow"); // v1: only flow ports

            LabelElement = new Label(port.Label ?? "");
            LabelElement.AddToClassList("nt-port-label");

            // Order within the row depends on side
            if (port.Direction == PortDirection.Input)
            {
                Add(Glyph);
                Add(LabelElement);
            }
            else
            {
                Add(LabelElement);
                Add(Glyph);
            }
        }

        /// <summary>
        /// Return the centre of the port glyph, expressed in the coordinate space
        /// of the given target element (typically the canvas content layer or the
        /// edge layer). Used by EdgeLayer to know where to start and end beziers.
        /// </summary>
        public Vector2 GetAnchorIn(VisualElement target)
        {
            var rect = Glyph.worldBound;
            var centerWorld = new Vector2(rect.xMin + rect.width * 0.5f,
                                          rect.yMin + rect.height * 0.5f);
            return target.WorldToLocal(centerWorld);
        }
    }
}