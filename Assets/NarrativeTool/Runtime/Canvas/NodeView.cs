// ===== File: Assets/NarrativeTool/Runtime/Canvas/NodeView.cs =====
using System.Collections.Generic;
using NarrativeTool.Core;
using NarrativeTool.Data;
using NarrativeTool.Data.Commands;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// Visual element for one Node. Unreal-style: coloured header (the drag
    /// handle) on top, input ports in a left column, output ports in a right
    /// column. TextNode instances also get a single-line text field in the
    /// body, wired to SetNodeTextCmd.
    ///
    /// Drag is attached to the header only, so port clicks, body clicks, and
    /// text-field clicks don't compete with dragging.
    /// </summary>
    public sealed class NodeView : VisualElement
    {
        public Node Node { get; }
        public GraphCanvas Canvas { get; }

        private readonly VisualElement header;
        private readonly Label titleLabel;
        private readonly VisualElement inputsColumn;
        private readonly VisualElement outputsColumn;
        private readonly VisualElement extrasContainer;
        private TextField textField;

        private readonly Dictionary<string, PortView> portViews = new();
        private System.IDisposable textChangedSub;

        public NodeView(Node node, GraphCanvas canvas)
        {
            Node = node;
            Canvas = canvas;

            AddToClassList("nt-node");
            style.position = Position.Absolute;
            SyncPositionFromData();
            style.minWidth = 180;

            // Header — drag handle.
            header = new VisualElement();
            header.AddToClassList("nt-node-header");
            header.AddToClassList(HeaderClassFor(node.Category));
            Add(header);

            titleLabel = new Label(node.Title);
            titleLabel.AddToClassList("nt-node-title");
            header.Add(titleLabel);

            // Body: two columns (inputs | outputs).
            var body = new VisualElement();
            body.AddToClassList("nt-node-body");
            Add(body);

            inputsColumn = new VisualElement();
            inputsColumn.AddToClassList("nt-node-column");
            inputsColumn.AddToClassList("nt-node-inputs");
            body.Add(inputsColumn);

            outputsColumn = new VisualElement();
            outputsColumn.AddToClassList("nt-node-column");
            outputsColumn.AddToClassList("nt-node-outputs");
            body.Add(outputsColumn);

            foreach (var p in node.Inputs)
            {
                var pv = new PortView(p);
                portViews[p.Id] = pv;
                inputsColumn.Add(pv);
            }
            foreach (var p in node.Outputs)
            {
                var pv = new PortView(p);
                portViews[p.Id] = pv;
                outputsColumn.Add(pv);
            }

            // Extras row (below the ports) — node-type-specific widgets.
            extrasContainer = new VisualElement();
            extrasContainer.AddToClassList("nt-node-extras");
            Add(extrasContainer);

            if (node is TextNode tn) BuildTextFieldFor(tn);

            // Drag is on the header — click on body / ports / text field won't drag.
            header.AddManipulator(new DragNodeManipulator(this));

            RegisterCallback<AttachToPanelEvent>(_ => SubscribeToBus());
            RegisterCallback<DetachFromPanelEvent>(_ => UnsubscribeFromBus());
        }

        /// <summary>Sync the view position from <see cref="Node.Position"/>.</summary>
        public void SyncPositionFromData()
        {
            style.left = Node.Position.x;
            style.top = Node.Position.y;
        }

        /// <summary>
        /// Set only the view's visual position without mutating the underlying
        /// Node model. Used by DragNodeManipulator during a drag; the real
        /// model update happens once, as a single MoveNodeCmd, on pointer-up.
        /// </summary>
        public void SetVisualPosition(Vector2 pos)
        {
            style.left = pos.x;
            style.top = pos.y;
        }

        /// <summary>Current visual position (content-layer coords).</summary>
        public Vector2 GetVisualPosition()
        {
            return new Vector2(resolvedStyle.left, resolvedStyle.top);
        }

        public PortView GetPortView(string portId)
        {
            portViews.TryGetValue(portId, out var v);
            return v;
        }

        // ----------- TextNode-specific ----------

        private void BuildTextFieldFor(TextNode tn)
        {
            textField = new TextField
            {
                multiline = false,
                value = tn.Text,
            };
            textField.AddToClassList("nt-node-textfield");

            textField.RegisterValueChangedCallback(evt =>
            {
                var current = tn.Text;
                var next = evt.newValue ?? "";
                if (current == next) return;

                Canvas.Commands.Execute(
                    new SetNodeTextCmd(Canvas.Graph, Canvas.Bus, tn.Id, current, next));
            });

            extrasContainer.Add(textField);
        }

        private void SubscribeToBus()
        {
            if (Node is not TextNode) return;
            var bus = Services.TryGet<EventBus>();
            if (bus == null) return;
            textChangedSub = bus.Subscribe<NodeTextChangedEvent>(OnTextChanged);
        }

        private void UnsubscribeFromBus()
        {
            textChangedSub?.Dispose();
            textChangedSub = null;
        }

        private void OnTextChanged(NodeTextChangedEvent e)
        {
            if (Node is not TextNode tn) return;
            if (e.NodeId != tn.Id) return;
            if (textField == null) return;
            if (textField.value != tn.Text)
                textField.SetValueWithoutNotify(tn.Text);
        }

        private static string HeaderClassFor(NodeCategory c) => c switch
        {
            NodeCategory.Event => "nt-header-event",
            NodeCategory.Flow => "nt-header-flow",
            NodeCategory.Data => "nt-header-data",
            _ => "nt-header-flow",
        };
    }
}