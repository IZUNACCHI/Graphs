using NarrativeTool.Canvas.Manipulators;
using NarrativeTool.Core.Binding;
using NarrativeTool.Core.Commands;
using NarrativeTool.Core.Selection;
using NarrativeTool.Core.Utilities;
using NarrativeTool.Core.Widgets;
using NarrativeTool.Data.Graph;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{
    /// <summary>
    /// Represents a visual element for a node in the graph canvas.
    /// </summary>
    public class NodeView : VisualElement, ISelectable
    {
        /// The NodeData this view represents. Should be considered read-only; any changes should be made via Commands to ensure proper undo/redo and event firing.
        public NodeData Node { get; }
        // The GraphCanvas this node belongs to, used for accessing shared services like Commands and Selection.
        public GraphView Canvas { get; }

        // UI elements
        protected readonly VisualElement header;
        protected readonly Label titleLabel;
        protected readonly VisualElement inputsColumn;
        protected readonly VisualElement outputsColumn;
        // Container for property editors generated from [EditableProperty] fields on the NodeData.
        protected readonly VisualElement extrasContainer;

        // If the node is currently being renamed, this is the TextField used for editing the name. We keep a reference so we can check focus and update the title without interfering with user input.
        private TextField renameField;
        private bool isRenaming;

        protected readonly Dictionary<string, PortView> portViews = new();

        // Cache of property editor widgets by property ID, used for updating UI when properties change. Populated in BuildPropertyEditors().
        protected readonly Dictionary<string, VisualElement> propWidgets = new();
        private System.IDisposable propChangedSub;

        // task item for refreshing connected edges after geometry changes. We keep a reference so we can cancel/reschedule if multiple geometry changes happen in quick succession (e.g. during a drag).
        private IVisualElementScheduledItem pendingEdgeRefresh;



        public NodeView(NodeData node, GraphView canvas)
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
            var nodeTypeAttr = Node.GetType().GetCustomAttribute<NodeTypeAttribute>();
            if (nodeTypeAttr != null && ColorUtility.TryParseHtmlString(nodeTypeAttr.Color, out var color))
            {
                header.style.backgroundColor = new StyleColor(color);
            }
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
                var pv = new PortView(p)
                {
                    OwnerNode = this
                };
                portViews[p.Id] = pv;
                inputsColumn.Add(pv);
            }
            foreach (var p in node.Outputs)
            {
                var pv = new PortView(p)
                {
                    OwnerNode = this
                };
                portViews[p.Id] = pv;
                outputsColumn.Add(pv);
            }

            extrasContainer = new VisualElement();
            extrasContainer.AddToClassList("nt-node-extras");
            Add(extrasContainer);

            // Automatically build editors for all [EditableProperty] fields
            BuildPropertyEditors();
            BuildCustomBody();
            propChangedSub = Canvas.Bus.Subscribe<PropertyChangedEvent>(OnPropertyChanged);

            header.AddManipulator(new DragNodeManipulator(this));

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            // Double‑click the header to rename
            header.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0 && e.clickCount == 2)
                {
                    BeginRename();
                    e.StopPropagation();
                }
            });

            // F2 anywhere on the node (or header) also starts rename
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.F2 && !isRenaming)
                {
                    BeginRename();
                    e.StopPropagation();
                }
            });
            RegisterCallback<DetachFromPanelEvent>(OnDetach);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void BuildPropertyEditors()
        {
            var pairs = new List<(PropertyDefinition Meta, PropertyAccessor Accessor)>();

            // Path 1 – built-in C# properties
            pairs.AddRange(PropertyCollector.CollectFromNode(Node));

            // Path 2 – dynamic PropertyValue bag (if any)
            // For custom nodes you'll need a definition to get the metadata list.
            // For built-in nodes, this returns an empty list – no harm.
            var dynamicDefs = GetDynamicPropertyDefinitions(); // you'll build this later
            pairs.AddRange(PropertyCollector.CollectFromPropertyValues(Node.Properties, dynamicDefs));

            VisualElement lastField = null;
            foreach (var (meta, accessor) in pairs)
            {
                var field = PropertyFieldFactory.Create(meta, accessor, Canvas.Commands,
                                                        Canvas.Bus);
                extrasContainer.Add(field);
                propWidgets[meta.Id] = field;
                lastField = field;
            }
            lastField?.AddToClassList("nt-prop-container--last");
        }

        protected virtual void BuildCustomBody() { }

        // Temporary stub – returns empty list, to be replaced when custom types land.
        private List<PropertyDefinition> GetDynamicPropertyDefinitions()
            => new();

        // React to external changes (undo/redo)
        private void OnPropertyChanged(PropertyChangedEvent e)
        {
            if (e.PropertyId == "Title")
            {
                titleLabel.text = Node.Title;
                return;
            }

            if (!propWidgets.TryGetValue(e.PropertyId, out var widget)) return;

            // Only update if the widget is NOT currently focused (the user is editing)
            if (widget is TextField tf && tf.focusController?.focusedElement == tf) return;
            if (widget is BaseField<int> ibf && ibf.focusController?.focusedElement == ibf) return;
            // ... more field types as needed

            // Otherwise sync the widget value from the model
            var value = Node.GetType().GetProperty(e.PropertyId)?.GetValue(Node);
            if (value == null) return;

            if (widget is TextField textField)
                textField.SetValueWithoutNotify(value.ToString());
            else if (widget is IntegerField intField && value is int i)
                intField.SetValueWithoutNotify(i);
            else if (widget is FloatField floatField && value is float f)
                floatField.SetValueWithoutNotify(f);
            else if (widget is Toggle toggle && value is bool b)
                toggle.SetValueWithoutNotify(b);
            else if (widget is DropdownField dropdown)
                dropdown.SetValueWithoutNotify(value.ToString());
        }

     

        // ── Position / Ports ───────────────────────────────
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

        // ── Selection ─────────────────────────────────────
        void ISelectable.OnSelected() => AddToClassList("nt-node--selected");
        void ISelectable.OnDeselected() => RemoveFromClassList("nt-node--selected");

        private void OnPointerDown(PointerDownEvent e)
        {
            if (IsInsideTextField(e.target as VisualElement)) return;

            if (e.button == 0)
            {
                if (e.shiftKey) Canvas.Selection.Toggle(this);
                else if (!Canvas.Selection.IsSelected(this)) Canvas.Selection.SelectOnly(this);
            }
            else if (e.button == 1)
            {
                if (!Canvas.Selection.IsSelected(this))
                    Canvas.Selection.SelectOnly(this);

                Canvas.ContextMenu?.Open(new NodeContextTarget(this), e.position);
            }
        }

        private void OnDetach(DetachFromPanelEvent _)
        {
            propChangedSub?.Dispose();
            propChangedSub = null;
        }

        // ── Edge refresh on geometry change ───────────────────────────
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // Only react to actual size changes, not pure moves
            if (evt.oldRect.size == evt.newRect.size)
                return;

            //cancel any pending refresh, then schedule one at end of frame
            pendingEdgeRefresh?.Pause();
            var edgeRefreshTask = schedule.Execute(() =>
            {
                Canvas?.EdgeLayer.RefreshEdgesForNode(Node.Id);
                pendingEdgeRefresh = null;
            });
            edgeRefreshTask.ExecuteLater(0);
            pendingEdgeRefresh = edgeRefreshTask;
        }

        private static bool IsInsideTextField(VisualElement ve)
        {
            while (ve != null) { if (ve is TextField) return true; ve = ve.parent; }
            return false;
        }

        private static string HeaderClassFor(NodeCategory c) => c switch
        {
            NodeCategory.Event => "nt-header-event",
            NodeCategory.Flow => "nt-header-flow",
            NodeCategory.Data => "nt-header-data",
            _ => "nt-header-flow",
        };

        private void BeginRename()
        {
            if (isRenaming) return;
            isRenaming = true;

            // Hide the title label
            titleLabel.style.display = DisplayStyle.None;

            // Create an inline text field
            renameField = new TextField { value = Node.Title };
            renameField.AddToClassList("nt-node-rename");
            renameField.style.position = Position.Absolute;
            // Place it roughly where the title label was
            renameField.style.left = titleLabel.resolvedStyle.left;
            renameField.style.top = titleLabel.resolvedStyle.top;
            renameField.style.width = titleLabel.resolvedStyle.width + 10;
            header.Add(renameField);

            renameField.schedule.Execute(() =>
            {
                renameField.Focus();
                renameField.SelectAll();
            }).StartingIn(0);

            renameField.RegisterCallback<BlurEvent>(_ => CommitRename());
            renameField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    CommitRename();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    CancelRename();
                    e.StopPropagation();
                }
            });


        }

        private void CommitRename()
        {
            if (!isRenaming) return;
            string newName = renameField.value.Trim();
            if (string.IsNullOrEmpty(newName))
                newName = Node.Title; // revert to original

            // Prevent duplicate names within the same graph
            if (newName != Node.Title && Canvas.Graph.Nodes.Any(n => n.Id != Node.Id && n.Title == newName))
            {
                Debug.LogWarning($"[NodeView] Name '{newName}' already exists in this graph.");
                // Optionally flash the field or just revert silently
                newName = Node.Title;
            }

            if (newName != Node.Title)
            {
                Canvas.Commands.Execute(new SetPropertyCommand(
                    "Title",
                    v => Node.Title = (string)v,
                    Node.Title,
                    newName,
                    Canvas.Bus));
            }

            FinishRename();
        }

        private void CancelRename()
        {
            FinishRename();
        }

        private void FinishRename()
        {
            if (renameField != null)
            {
                renameField.RemoveFromHierarchy();
                renameField = null;
            }
            titleLabel.style.display = DisplayStyle.Flex;
            isRenaming = false;
        }
    }

    public sealed class NodeContextTarget
    {
        public NodeView NodeView { get; }
        public NodeContextTarget(NodeView nv) { NodeView = nv; }
    }
}