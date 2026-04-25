using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.Entities;
using NarrativeTool.UI.Variables;
using UnityEngine.UIElements;

namespace NarrativeTool.UI
{
    /// <summary>
    /// Left-hand sidebar that hosts the Variables and Entities tabs and
    /// switches between their panels. Each panel binds to the same
    /// ProjectModel/SessionState; only one is visible at a time.
    /// </summary>
    public sealed class ProjectSidebar : VisualElement
    {
        private readonly Label variablesTab;
        private readonly Label entitiesTab;
        private readonly VariablesPanel variablesPanel;
        private readonly EntitiesPanel entitiesPanel;

        public ProjectSidebar()
        {
            AddToClassList("nt-vars");
            style.flexDirection = FlexDirection.Column;

            var tabs = new VisualElement();
            tabs.AddToClassList("nt-vars-tabs");
            variablesTab = new Label("Variables");
            variablesTab.AddToClassList("nt-vars-tab");
            entitiesTab = new Label("Entities");
            entitiesTab.AddToClassList("nt-vars-tab");
            tabs.Add(variablesTab);
            tabs.Add(entitiesTab);
            Add(tabs);

            variablesPanel = new VariablesPanel();
            variablesPanel.style.flexGrow = 1;
            entitiesPanel = new EntitiesPanel();
            entitiesPanel.style.flexGrow = 1;
            Add(variablesPanel);
            Add(entitiesPanel);

            variablesTab.RegisterCallback<ClickEvent>(_ => Switch(showVariables: true));
            entitiesTab.RegisterCallback<ClickEvent>(_ => Switch(showVariables: false));

            Switch(showVariables: true);
        }

        public void Bind(ProjectModel project, SessionState session, ContextMenuController contextMenu)
        {
            variablesPanel.Bind(project, session, contextMenu);
            entitiesPanel.Bind(project, session, contextMenu);
        }

        private void Switch(bool showVariables)
        {
            variablesPanel.style.display = showVariables ? DisplayStyle.Flex : DisplayStyle.None;
            entitiesPanel.style.display = showVariables ? DisplayStyle.None : DisplayStyle.Flex;
            variablesTab.EnableInClassList("nt-vars-tab--active", showVariables);
            entitiesTab.EnableInClassList("nt-vars-tab--active", !showVariables);
        }
    }
}
