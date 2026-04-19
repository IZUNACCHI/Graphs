using System.Collections.Generic;
using NarrativeTool.Core;
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
    /// TextNode editing uses a per-focus-session local undo stack:
    ///  - On focus-in: record originalText, clear local stack.
    ///  - On each value change: push previous value onto local stack, clear redo.
    ///  - Ctrl+Z while focused: pop local stack, restore that value.
    ///  - Ctrl+Shift+Z while focused: redo within local stack.
    ///  - External text changes (bus events) while focused: invalidate local stack.
    ///  - On focus-out: if current != originalText, push a single global
    ///    SetNodeTextCmd representing the net change. Otherwise do nothing.
    ///    Discard local stack either way.
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
        private TextField textField;

        private readonly Dictionary<string, PortView> portViews = new();
        private System.IDisposable textChangedSub;

        // --- Per-focus-session text undo state ---
        // Populated while the text field has focus; discarded on focus-out.
        private string textOriginal;       // value at focus-in
        private bool textFocused;
        private bool textSuppressChange;   // re-entrancy guard while we set value programmatically
        private float textLastChangeTime;  // last burst time; used for 500ms merge
        private readonly Stack<string> textUndoStack = new(); // each entry = value BEFORE the edit it represents
        private readonly Stack<string> textRedoStack = new();
        private const float TextBurstMergeSeconds = 0.1f;

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

        // ===========================================================
        // TextNode text field — per-focus-session local undo stack
        // ===========================================================

        private void BuildTextFieldFor(TextNode tn)
        {
            textField = new TextField { multiline = false, value = tn.Text };
            textField.AddToClassList("nt-node-textfield");

            // Focus-in: snapshot, clear local stacks.
            textField.RegisterCallback<FocusInEvent>(_ =>
            {
                textOriginal = tn.Text ?? "";
                textFocused = true;
                textUndoStack.Clear();
                textRedoStack.Clear();
                textLastChangeTime = 0f;
            });

            // Value change: push "before" onto local undo stack (or merge into
            // last entry if within the 500ms burst window). Mirror the new
            // value into the model so anything reading tn.Text sees current.
            textField.RegisterValueChangedCallback(evt =>
            {
                if (textSuppressChange) return;

                var before = evt.previousValue ?? "";
                var after = evt.newValue ?? "";
                if (before == after) return;

                if (textFocused)
                {
                    bool mergeIntoLast =
                        textUndoStack.Count > 0 &&
                        (Time.unscaledTime - textLastChangeTime) <= TextBurstMergeSeconds;

                    if (!mergeIntoLast)
                    {
                        // Start a new burst — push 'before' as the state to
                        // return to if the user undoes this burst.
                        textUndoStack.Push(before);
                    }
                    // If merging, the burst's baseline already on the stack
                    // is kept — we don't push the intermediate 'before'.
                    textRedoStack.Clear();
                    textLastChangeTime = Time.unscaledTime;
                }

                // Live-sync the model so other readers see current text.
                tn.Text = after;
            });

            // Ctrl+Z / Ctrl+Shift+Z intercepted before the field consumes them.
            // (TrickleDown phase so we see them first.)
            textField.RegisterCallback<KeyDownEvent>(OnTextFieldKeyDown, TrickleDown.TrickleDown);

            // Focus-out: commit net change globally (if any), discard local stack.
            textField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!textFocused) return;
                textFocused = false;

                var final = textField.value ?? "";

                if (final != textOriginal)
                {
                    // Revert the live-sync'd model so the command's Do() actually
                    // performs the change (and publishes the event cleanly).
                    var tnInner = (TextNode)Node;
                    tnInner.Text = textOriginal;

                    Canvas.Commands.Execute(
                        new SetNodeTextCmd(Canvas.Graph, Canvas.Bus, tnInner.Id,
                                           textOriginal, final));
                }

                textUndoStack.Clear();
                textRedoStack.Clear();
            });

            extrasContainer.Add(textField);
        }

        private void OnTextFieldKeyDown(KeyDownEvent e)
        {
            // Delete / Backspace: the TextField handles them as character edits.
            // Stop propagation so the canvas's KeyDown handler doesn't also treat
            // them as "delete selected node".
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                e.StopPropagation();
                return;
            }

            bool ctrl = e.ctrlKey || e.commandKey;
            if (!ctrl) return;
            if (Node is not TextNode tn) return;
            if (textField == null) return;

            if (e.keyCode == KeyCode.Z && !e.shiftKey)
            {
                if (textUndoStack.Count == 0) { e.StopPropagation(); return; }

                var previous = textUndoStack.Pop();
                textRedoStack.Push(textField.value ?? "");
                SetFieldValueSilently(previous);
                tn.Text = previous;
                textLastChangeTime = 0f;
                e.StopPropagation();
                return;
            }

            if (e.keyCode == KeyCode.Z && e.shiftKey)
            {
                if (textRedoStack.Count == 0) { e.StopPropagation(); return; }

                var next = textRedoStack.Pop();
                textUndoStack.Push(textField.value ?? "");
                SetFieldValueSilently(next);
                tn.Text = next;
                textLastChangeTime = 0f;
                e.StopPropagation();
                return;
            }
        }

        /// <summary>
        /// Set the field's value without firing RegisterValueChangedCallback,
        /// AND without our local stack reacting to it.
        /// </summary>
        private void SetFieldValueSilently(string value)
        {
            textSuppressChange = true;
            try { textField.SetValueWithoutNotify(value ?? ""); }
            finally { textSuppressChange = false; }
        }

        // ===========================================================
        // External text change subscription (bus)
        // ===========================================================

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

            // If the field's displayed value already matches, nothing to do.
            if (textField.value == tn.Text) return;

            // External change — the model was mutated by someone other than
            // this field's own value-changed callback (e.g. an undo of a
            // prior SetNodeTextCmd from another session). Invalidate the
            // local stack because its baseline is now wrong.
            if (textFocused)
            {
                textUndoStack.Clear();
                textRedoStack.Clear();
                textOriginal = tn.Text ?? "";
                textLastChangeTime = 0f;
            }

            SetFieldValueSilently(tn.Text);
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