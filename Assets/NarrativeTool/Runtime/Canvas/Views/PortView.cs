using NarrativeTool.Canvas.Manipulators;
using NarrativeTool.Data;
using NarrativeTool.Data.Graph;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{
    /// <summary>
    /// One row on a NodeView. Now also the entry point for edge creation:
    /// the glyph carries an EdgeCreationManipulator that listens for drag
    /// starts and hands off to the manipulator's session machinery.
    /// </summary>
    public sealed class PortView : VisualElement
    {
        public PortData Port { get; }
        public VisualElement Glyph { get; }
        public Label LabelElement { get; }

        /// <summary>
        /// Owning NodeView. Set by NodeView at construction. The manipulator
        /// uses this to find the canvas for drag-session coordination.
        /// </summary>
        public NodeView OwnerNode { get; set; }

        public PortView(PortData port)
        {
            Port = port;
            AddToClassList("nt-port");
            AddToClassList(port.Direction == PortDirection.Input ? "nt-port-input" : "nt-port-output");

            Glyph = new VisualElement();
            Glyph.AddToClassList("nt-port-glyph");
            Glyph.AddToClassList("nt-port-flow");
            Glyph.pickingMode = PickingMode.Position;

            LabelElement = new Label(port.Label ?? "");
            LabelElement.AddToClassList("nt-port-label");

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

            // Drag from this port's glyph initiates edge creation.
            Glyph.AddManipulator(new EdgeCreationManipulator(this));
        }

        /// <summary>
        /// Center of the port glyph in the coordinate space of the given
        /// element. Used by EdgeView to place endpoints.
        /// </summary>
        public Vector2 GetAnchorIn(VisualElement target)
        {
            var rect = Glyph.worldBound;
            var centerWorld = new Vector2(rect.xMin + rect.width * 0.5f,
                                          rect.yMin + rect.height * 0.5f);
            return target.WorldToLocal(centerWorld);
        }

        // ---------- Visual feedback ----------

        public void SetCompatibleHighlight(bool on)
        {
            if (on) Glyph.AddToClassList("nt-port-glyph--compatible");
            else Glyph.RemoveFromClassList("nt-port-glyph--compatible");
        }
    }
}