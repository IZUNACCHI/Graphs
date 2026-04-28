using NarrativeTool.Canvas;
using NarrativeTool.Data.Graph;
using NarrativeTool.Data.Graph.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarrativeTool.Canvas.Views
{
    [NodeViewOf(typeof(JumperOutNodeData))]
    public sealed class JumperOutNodeView : NodeView
    {
        public JumperOutNodeView(NodeData node, GraphView canvas) : base(node, canvas)
        {
            // ── Double‑click shows a popup list of connected In nodes ──
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0 && e.clickCount == 2)
                {
                    ShowIncomingPopup();
                    e.StopPropagation();
                }
            });
        }

        private List<JumperInNodeData> GetIncomingInNodes()
        {
            return Canvas.Graph.Nodes
                .OfType<JumperInNodeData>()
                .Where(n => n.TargetOutNodeId == Node.Id)
                .ToList();
        }

        /// <summary>
        /// Shows a temporary popup listing connected In nodes. Clicking one pans to it.
        /// </summary>
        private void ShowIncomingPopup()
        {
            var incoming = GetIncomingInNodes();
            if (incoming.Count == 0) return;

            // ── Calculate popup position near the Out node (screen space) ──
            // The node's resolvedStyle.left/top are in world space, convert to screen space
            var worldPos = new Vector2(resolvedStyle.left + resolvedStyle.width,
                                       resolvedStyle.top);   // top‑right corner of the node
            var screenPos = Canvas.WorldToScreen(worldPos);
            // Add a small horizontal offset so the popup appears beside the node
            screenPos += new Vector2(8, 0);

            // ── Build the popup ──
            var popup = new VisualElement();
            popup.AddToClassList("jumper-popup");
            popup.style.position = Position.Absolute;
            popup.style.left = screenPos.x;
            popup.style.top = screenPos.y;
            popup.style.minWidth = 120;
            popup.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            popup.style.borderTopLeftRadius = 4;
            popup.style.borderTopRightRadius = 4;
            popup.style.borderBottomLeftRadius = 4;
            popup.style.borderBottomRightRadius = 4;
            popup.style.paddingTop = 4;
            popup.style.paddingBottom = 4;
            popup.style.paddingLeft = 4;
            popup.style.paddingRight = 4;
            popup.style.flexDirection = FlexDirection.Column;

            foreach (var inNode in incoming)
            {
                var item = new Button { text = inNode.Title };
                item.AddToClassList("jumper-popup-item");
                item.style.backgroundColor = Color.clear;
                item.style.color = Color.white;
                item.style.fontSize = 11;
                item.style.paddingTop = 3;
                item.style.paddingBottom = 3;
                item.style.paddingLeft = 6;
                item.style.paddingRight = 6;
                item.style.unityTextAlign = TextAnchor.MiddleLeft;
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    Canvas.FrameNode(inNode.Id);
                    popup.RemoveFromHierarchy();
                });
                popup.Add(item);
            }

            // Add popup to the GraphView (so it's in screen space, unaffected by pan/zoom)
            Canvas.Add(popup);

            // Dismiss popup when clicking anywhere else
            void Dismiss(PointerDownEvent evt)
            {
                if (!popup.ContainsPoint(popup.WorldToLocal(evt.position)))
                {
                    popup.RemoveFromHierarchy();
                    Canvas.UnregisterCallback<PointerDownEvent>(Dismiss);
                }
            }
            Canvas.RegisterCallback<PointerDownEvent>(Dismiss, TrickleDown.TrickleDown);
        }

        protected override void BuildCustomBody() { }
    }
}