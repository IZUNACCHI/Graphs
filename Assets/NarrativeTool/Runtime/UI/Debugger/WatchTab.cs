using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Core.EventSystem;
using NarrativeTool.Core.Runtime;
using NarrativeTool.Data.Project;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.UI.Debugger
{
    /// <summary>
    /// Watch tab. Lists user-picked variables / entity-fields and shows their
    /// current runtime value, type, and a "changed this step" indicator that
    /// flashes amber when the value differs from the snapshot taken at the
    /// last NodeEnteredEvent. Right-click a row to override the value or
    /// remove it from the watch list.
    /// </summary>
    public sealed class WatchTab : VisualElement, IDisposable
    {
        private readonly ProjectModel project;
        private readonly RuntimeVariableStore variables;
        private readonly RuntimeEntityStore entities;
        private readonly EventBus bus;
        private readonly ContextMenuController contextMenu;

        private readonly ListView listView;
        private readonly Label footerSummary;

        private readonly List<WatchTarget> watched = new();
        private readonly Dictionary<WatchTarget, object> snapshotAtStep = new();
        private readonly Dictionary<WatchTarget, object> changedSinceStep = new();

        private IDisposable subVarChanged, subEntityChanged, subNodeEntered;

        public WatchTab(ProjectModel project, RuntimeVariableStore variables,
            RuntimeEntityStore entities, EventBus bus, ContextMenuController contextMenu)
        {
            this.project = project;
            this.variables = variables;
            this.entities = entities;
            this.bus = bus;
            this.contextMenu = contextMenu;

            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Column header
            var header = new VisualElement();
            header.AddToClassList("debugger-panel__col-header");
            header.Add(MakeHeaderCell("NAME", flex: 1));
            header.Add(MakeHeaderCell("VALUE", width: 70));
            header.Add(MakeHeaderCell("TYPE", width: 36));
            Add(header);

            // Native, virtualised list (Phase 3 — replaces hand-rolled ScrollView).
            listView = new ListView
            {
                itemsSource = watched,
                fixedItemHeight = 22,
                makeItem = MakeRow,
                bindItem = BindRow,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                showBorder = false,
                reorderable = false,
            };
            listView.style.flexGrow = 1;
            Add(listView);

            // Footer
            var footer = new VisualElement();
            footer.AddToClassList("debugger-panel__footer");
            footerSummary = new Label("0 vars · 0 changed");
            footerSummary.AddToClassList("debugger-panel__footer-label");
            footer.Add(footerSummary);
            var addBtn = new Button(OpenAddPicker) { text = "+ watch" };
            addBtn.AddToClassList("debugger-panel__footer-btn");
            footer.Add(addBtn);
            Add(footer);

            subVarChanged = bus?.Subscribe<VariableRuntimeValueChangedEvent>(OnVariableChanged);
            subEntityChanged = bus?.Subscribe<EntityRuntimeValueChangedEvent>(OnEntityChanged);
            subNodeEntered = bus?.Subscribe<NodeEnteredEvent>(_ => OnNewStep());

            Refresh();
        }

        public void Dispose()
        {
            subVarChanged?.Dispose();
            subEntityChanged?.Dispose();
            subNodeEntered?.Dispose();
            subVarChanged = subEntityChanged = subNodeEntered = null;
        }

        private static Label MakeHeaderCell(string text, float? flex = null, float? width = null)
        {
            var l = new Label(text);
            l.AddToClassList("debugger-panel__col-header-cell");
            if (flex.HasValue) l.style.flexGrow = flex.Value;
            if (width.HasValue) l.style.width = width.Value;
            return l;
        }

        public void AddWatch(WatchTarget t)
        {
            if (watched.Any(x => x.Equals(t))) return;
            watched.Add(t);
            snapshotAtStep[t] = ReadValue(t);
            Refresh();
        }

        public void RemoveWatch(WatchTarget t)
        {
            watched.RemoveAll(x => x.Equals(t));
            snapshotAtStep.Remove(t);
            changedSinceStep.Remove(t);
            Refresh();
        }

        private void OnVariableChanged(VariableRuntimeValueChangedEvent e)
        {
            var t = WatchTarget.Variable(e.Name);
            if (!watched.Contains(t)) return;
            changedSinceStep[t] = e.NewValue;
            Refresh();
        }

        private void OnEntityChanged(EntityRuntimeValueChangedEvent e)
        {
            var t = WatchTarget.EntityField(e.EntityName, e.FieldName);
            if (!watched.Contains(t)) return;
            changedSinceStep[t] = e.NewValue;
            Refresh();
        }

        private void OnNewStep()
        {
            changedSinceStep.Clear();
            foreach (var t in watched)
                snapshotAtStep[t] = ReadValue(t);
            Refresh();
        }

        private object ReadValue(WatchTarget t)
        {
            try
            {
                return t.Kind == WatchTargetKind.Variable
                    ? variables?.GetValue(t.A)
                    : entities?.GetValue(t.A, t.B);
            }
            catch { return null; }
        }

        private void WriteValue(WatchTarget t, object newValue)
        {
            if (t.Kind == WatchTargetKind.Variable)
                variables?.SetValue(t.A, newValue);
            else
                entities?.SetValue(t.A, t.B, newValue);
        }

        private VariableType? ResolveType(WatchTarget t)
        {
            if (t.Kind == WatchTargetKind.Variable)
            {
                if (variables != null && variables.Definitions.TryGetValue(t.A, out var def))
                    return def.Type;
                return null;
            }
            var ent = entities?.GetDefinition(t.A);
            var fld = ent?.Fields.FirstOrDefault(f => f.Name == t.B);
            return fld?.Type;
        }

        private string GetEnumTypeId(WatchTarget t)
        {
            if (t.Kind == WatchTargetKind.Variable)
            {
                if (variables != null && variables.Definitions.TryGetValue(t.A, out var def))
                    return def.EnumTypeId;
                return null;
            }
            var ent = entities?.GetDefinition(t.A);
            var fld = ent?.Fields.FirstOrDefault(f => f.Name == t.B);
            return fld?.EnumTypeId;
        }

        public void Refresh()
        {
            listView.RefreshItems();
            int changedCount = 0;
            foreach (var t in watched)
                if (changedSinceStep.ContainsKey(t)) changedCount++;
            footerSummary.text = $"{watched.Count} vars · {changedCount} changed";
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("debugger-row");

            var nameLabel = new Label();
            nameLabel.AddToClassList("debugger-watch__name");
            nameLabel.name = "name";
            row.Add(nameLabel);

            var valueLabel = new Label();
            valueLabel.AddToClassList("debugger-watch__value");
            valueLabel.name = "value";
            row.Add(valueLabel);

            var typeBadge = new Label();
            typeBadge.AddToClassList("debugger-watch__type-badge");
            typeBadge.name = "type";
            row.Add(typeBadge);

            return row;
        }

        private void BindRow(VisualElement row, int index)
        {
            if (index < 0 || index >= watched.Count) return;
            var t = watched[index];
            var type = ResolveType(t);
            var value = ReadValue(t);
            bool changed = changedSinceStep.ContainsKey(t);

            row.EnableInClassList("debugger-watch__row--changed-amber", changed);

            var nameLabel = row.Q<Label>("name");
            nameLabel.text = t.DisplayName;
            nameLabel.EnableInClassList("debugger-watch__name--unchanged", !changed);

            var valueLabel = row.Q<Label>("value");
            valueLabel.text = FormatValue(value, type, t);
            valueLabel.EnableInClassList("debugger-watch__value--amber", changed);

            var typeBadge = row.Q<Label>("type");
            typeBadge.text = FormatType(type);

            // Reset previous handler (ListView reuses row instances across binds).
            row.UnregisterCallback<MouseDownEvent>(OnRowMouseDown);
            row.userData = t;
            row.RegisterCallback<MouseDownEvent>(OnRowMouseDown);
        }

        private void OnRowMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 1) return;
            if (evt.currentTarget is VisualElement row && row.userData is WatchTarget t)
            {
                contextMenu?.Open(new WatchContextTarget(this, t), evt.mousePosition);
                evt.StopPropagation();
            }
        }

        private string FormatValue(object value, VariableType? type, WatchTarget t)
        {
            if (value == null) return "—";
            if (type == VariableType.Enum && value is string memberId)
            {
                var enumId = GetEnumTypeId(t);
                if (!string.IsNullOrEmpty(enumId))
                {
                    var def = project?.Enums.Items.FirstOrDefault(e => e.Id == enumId);
                    var m = def?.Members.FirstOrDefault(x => x.Id == memberId);
                    if (m != null) return m.Name;
                }
            }
            if (value is bool b) return b ? "true" : "false";
            return value.ToString();
        }

        private static string FormatType(VariableType? t)
            => t switch
            {
                VariableType.Int => "int",
                VariableType.Float => "float",
                VariableType.Bool => "bool",
                VariableType.String => "str",
                VariableType.Enum => "enum",
                _ => "?",
            };

        // ───────── Context menu actions ─────────

        public void BeginEditValue(WatchTarget t)
        {
            var type = ResolveType(t);
            if (!type.HasValue) return;

            var modal = new ValueOverrideModal(t.DisplayName, type.Value, ReadValue(t),
                GetEnumTypeId(t), project,
                committed => { WriteValue(t, committed); Refresh(); });

            // Add the modal to the panel root.
            var root = panel?.visualTree;
            if (root != null) root.Add(modal);
        }

        // ───────── Add-watch picker ─────────

        private void OpenAddPicker()
        {
            var menu = new List<ContextMenuItem>();
            if (variables != null)
            {
                foreach (var name in variables.Definitions.Keys)
                {
                    var captured = name;
                    var t = WatchTarget.Variable(captured);
                    if (watched.Contains(t)) continue;
                    menu.Add(ContextMenuItem.Of($"var · {captured}", () => AddWatch(t)));
                }
            }
            // Read entity definitions straight from the project so the list
            // is correct even if the runtime store hasn't initialised yet.
            if (project != null)
            {
                foreach (var entity in project.Entities.Items)
                {
                    if (entity?.Fields == null || entity.Fields.Count == 0) continue;
                    foreach (var field in entity.Fields)
                    {
                        var t = WatchTarget.EntityField(entity.Name, field.Name);
                        if (watched.Contains(t)) continue;
                        menu.Add(ContextMenuItem.Of(
                            $"entity · {entity.Name}.{field.Name}",
                            () => AddWatch(t)));
                    }
                }
            }
            if (menu.Count == 0)
            {
                menu.Add(ContextMenuItem.Of("(nothing to watch)", null, enabled: false));
            }
            contextMenu?.Open(new WatchPickerTarget(menu), worldBound.center);
        }
    }

    // ───────── Target & menu types ─────────

    public enum WatchTargetKind { Variable, EntityField }

    public readonly struct WatchTarget : IEquatable<WatchTarget>
    {
        public readonly WatchTargetKind Kind;
        public readonly string A;  // variable name OR entity name
        public readonly string B;  // null for variables, field name for entities

        private WatchTarget(WatchTargetKind kind, string a, string b)
        { Kind = kind; A = a; B = b; }

        public static WatchTarget Variable(string name) => new(WatchTargetKind.Variable, name, null);
        public static WatchTarget EntityField(string entity, string field) => new(WatchTargetKind.EntityField, entity, field);

        public string DisplayName => Kind == WatchTargetKind.Variable ? A : $"{A}.{B}";

        public bool Equals(WatchTarget other) => Kind == other.Kind && A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is WatchTarget t && Equals(t);
        public override int GetHashCode() => (Kind, A, B).GetHashCode();
    }

    public sealed class WatchContextTarget
    {
        public WatchTab Tab { get; }
        public WatchTarget Target { get; }
        public WatchContextTarget(WatchTab tab, WatchTarget target) { Tab = tab; Target = target; }
    }

    /// <summary>Used to pop up the variable/entity picker for "+ watch".</summary>
    public sealed class WatchPickerTarget
    {
        public IReadOnlyList<ContextMenuItem> Items { get; }
        public WatchPickerTarget(IReadOnlyList<ContextMenuItem> items) { Items = items; }
    }
}
