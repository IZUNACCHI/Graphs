using System.Collections.Generic;
using NarrativeTool.Core;
using NarrativeTool.Core.Widgets;
using NarrativeTool.Data;
using NarrativeTool.Data.Commands;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas
{
    /// <summary>
    /// Visual element for one Node. Header = drag handle. Body = port
    /// columns + optional type-specific extras. Implements ISelectable.
    ///
    /// TextNode gets a FlexTextField in its extras area. The field handles
    /// all per-focus-session undo logic internally; this class just wires
    /// up "commit -> SetNodeTextCmd" and forwards external bus-driven
    /// changes back into the field.
    /// </summary>
    public sealed class NodeView : VisualElement, ISelectable
    {
        public Node Node { get; }
        public GraphCanvas Canvas { get; }

        private readonly VisualElement header;
        private readonly Label titleLabel;
        private readonly VisualElement inputsColumn;
        private readonly VisualElement outputsColumn;
        private readonly VisualElement extrasContainer;
        private FlexTextField textField;

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

            header = new VisualElement();
            header.AddToClassList("nt-node-header");
            header.AddToClassList(HeaderClassFor(node.Category));
            Add(header);

            titleLabel = new Label(node.Title);
            titleLabel.AddToClassList("nt-node-title");
            header.Add(titleLabel);

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

            extrasContainer = new VisualElement();
            extrasContainer.AddToClassList("nt-node-extras");
            Add(extrasContainer);

            if (node is TextNode tn) BuildTextFieldFor(tn);

            header.AddManipulator(new DragNodeManipulator(this));

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<AttachToPanelEvent>(_ => SubscribeToBus());
            RegisterCallback<DetachFromPanelEvent>(_ => UnsubscribeFromBus());
        }

        public void SyncPositionFromData()
        {
            style.left = Node.Position.x;
            style.top = Node.Position.y;
        }

        public void SetVisualPosition(Vector2 pos)
        {
            style.left = pos.x;
            style.top = pos.y;
        }

        public Vector2 GetVisualPosition() => new(resolvedStyle.left, resolvedStyle.top);

        public PortView GetPortView(string portId)
        {
            portViews.TryGetValue(portId, out var v);
            return v;
        }

        void ISelectable.OnSelected() => AddToClassList("nt-node--selected");
        void ISelectable.OnDeselected() => RemoveFromClassList("nt-node--selected");

        private void OnPointerDown(PointerDownEvent e)
        {
            if (IsInsideTextField(e.target as VisualElement)) return;

            if (e.button == 0)
            {
                if (e.shiftKey) Canvas.Selection.Toggle(this);
                else if (!Canvas.Selection.IsSelected(this)) Canvas.Selection.SelectOnly(this);
                e.StopPropagation();
            }
            else if (e.button == 1)
            {
                if (!Canvas.Selection.IsSelected(this))
                    Canvas.Selection.SelectOnly(this);

                Canvas.ContextMenu?.Open(new NodeContextTarget(this), e.position);
                e.StopPropagation();
            }
        }

        private static bool IsInsideTextField(VisualElement ve)
        {
            while (ve != null) { if (ve is TextField) return true; ve = ve.parent; }
            return false;
        }

        // ---------- TextNode-specific ----------

        private void BuildTextFieldFor(TextNode tn)
        {
            textField = new FlexTextField(multiline: false) { value = tn.Text };
            textField.AddToClassList("nt-node-textfield");

            // Live-sync tn.Text so anything reading the model mid-edit sees
            // the current typed value. The bus event is only fired when a
            // net-change commit happens via SetNodeTextCmd below.
            textField.RegisterValueChangedCallback(evt =>
            {
                tn.Text = evt.newValue ?? "";
            });

            textField.OnCommit += (oldText, newText) =>
            {
                // We've been live-syncing tn.Text, so before running the
                // command, revert it to oldText. The command's Do() then
                // writes newText and publishes the event cleanly.
                tn.Text = oldText;
                Canvas.Commands.Execute(
                    new SetNodeTextCmd(Canvas.Graph, Canvas.Bus, tn.Id, oldText, newText));
            };

            extrasContainer.Add(textField);
        }

        private void SubscribeToBus()
        {
            if (Node is not TextNode) return;
            if (Canvas?.Bus == null) return;
            textChangedSub = Canvas.Bus.Subscribe<NodeTextChangedEvent>(OnTextChanged);
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
            // Forward external changes into the widget. FlexTextField handles
            // invalidating its local stack if focused.
            textField.SetExternalValue(tn.Text);
        }

        private static string HeaderClassFor(NodeCategory c) => c switch
        {
            NodeCategory.Event => "nt-header-event",
            NodeCategory.Flow => "nt-header-flow",
            NodeCategory.Data => "nt-header-data",
            _ => "nt-header-flow",
        };
    }

    public sealed class NodeContextTarget
    {
        public NodeView NodeView { get; }
        public NodeContextTarget(NodeView nv) { NodeView = nv; }
    }
}