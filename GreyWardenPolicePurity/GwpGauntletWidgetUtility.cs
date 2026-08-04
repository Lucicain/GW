using System;
using TaleWorlds.GauntletUI.BaseTypes;

namespace GreyWardenPolicePurity
{
    internal static class GwpGauntletWidgetUtility
    {
        internal static Widget? FindById(Widget root, string id)
        {
            if (string.Equals(root.Id, id, StringComparison.Ordinal))
                return root;

            for (int index = 0; index < root.ChildCount; index++)
            {
                Widget? match = FindById(root.GetChild(index), id);
                if (match != null)
                    return match;
            }

            return null;
        }

        internal static Widget? FindAncestorChildOf<TParent>(Widget widget)
            where TParent : Widget
        {
            Widget? current = widget;
            while (current?.ParentWidget != null)
            {
                if (current.ParentWidget is TParent)
                    return current;

                current = current.ParentWidget;
            }

            return null;
        }
    }
}
