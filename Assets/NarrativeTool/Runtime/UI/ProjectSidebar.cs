using NarrativeTool.Core.ContextMenu;
using NarrativeTool.Data.Project;
using NarrativeTool.UI.Entities;
using NarrativeTool.UI.Graphs;
using NarrativeTool.UI.Variables;
using System;
using UnityEngine.UIElements;

namespace NarrativeTool.UI
{
    public sealed class ProjectSidebar : VisualElement
    {
        // Top zone — Graphs
        private GraphsPanel graphsPanel;

        // Bottom zone — Tabbed panels
        private readonly VisualElement bottomSection;
        private readonly Label variablesTab;
        private readonly Label entitiesTab;
        private readonly VariablesPanel variablesPanel;
        private readonly EntitiesPanel entitiesPanel;

        public event Action<LazyGraph> OnGraphDoubleClicked;

        public ProjectSidebar()
        {
            AddToClassList("nt-sidebar");
            style.flexDirection = FlexDirection.Column;

            // ─── Top half: Graphs panel ───
            graphsPanel = new GraphsPanel();
            graphsPanel.AddToClassList("nt-sidebar-section");
            graphsPanel.style.flexGrow = 1;           // takes half the height
            Add(graphsPanel);

            // ─── Bottom half: Tabbed panels ───
            bottomSection = new VisualElement();
            bottomSection.AddToClassList("nt-sidebar-bottom");
            bottomSection.style.flexGrow = 1;         // takes the other half
            Add(bottomSection);

            // Tab bar
            var tabBar = new VisualElement();
            tabBar.AddToClassList("nt-sidebar-tabbar");
            variablesTab = BuildTab("Variables");
            entitiesTab = BuildTab("Entities");
            tabBar.Add(variablesTab);
            tabBar.Add(entitiesTab);
            bottomSection.Add(tabBar);

            // Panels
            variablesPanel = new VariablesPanel();
            variablesPanel.style.flexGrow = 1;
            entitiesPanel = new EntitiesPanel();
            entitiesPanel.style.flexGrow = 1;
            

            bottomSection.Add(variablesPanel);
            bottomSection.Add(entitiesPanel);

            // Tab click handlers
            variablesTab.RegisterCallback<ClickEvent>(_ => SwitchTab("variables"));
            entitiesTab.RegisterCallback<ClickEvent>(_ => SwitchTab("entities"));

            // Default tab
            SwitchTab("variables");
        }

        private static Label BuildTab(string text)
        {
            var tab = new Label(text);
            tab.AddToClassList("nt-sidebar-tab");
            return tab;
        }

        private void SwitchTab(string id)
        {
            variablesPanel.style.display = id == "variables" ? DisplayStyle.Flex : DisplayStyle.None;
            entitiesPanel.style.display = id == "entities" ? DisplayStyle.Flex : DisplayStyle.None;

            variablesTab.EnableInClassList("nt-sidebar-tab--active", id == "variables");
            entitiesTab.EnableInClassList("nt-sidebar-tab--active", id == "entities");
        }

        public void Bind(ProjectModel project, SessionState session, ContextMenuController contextMenu)
        {
            // Bind the graphs panel at the top
            graphsPanel.Bind(project, session, contextMenu);
            graphsPanel.OnGraphDoubleClicked += lazy => OnGraphDoubleClicked?.Invoke(lazy);

            // Bind the tab panels at the bottom
            variablesPanel.Bind(project, session, contextMenu);
            entitiesPanel.Bind(project, session, contextMenu);
            // Enums panel binding will go here once it’s a real panel
        }

        /// <summary>Refresh just the graph list (e.g., after external change).</summary>
        public void RefreshGraphs()
        {
            graphsPanel?.Refresh();
        }
    }
}