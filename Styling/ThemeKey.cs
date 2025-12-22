using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace StruxureGuard.Styling;

public static class ThemeKey
{
    /// <summary>
    /// Stable-ish key for a control: FormName.Control.SubControl...
    /// If a control has no Name, we fall back to TypeName + sibling index to reduce collisions.
    /// </summary>
    public static string ForControl(Control c)
    {
        var form = c.FindForm();
        var formName = form?.Name ?? form?.GetType().Name ?? "UnknownForm";

        var parts = new List<string>();

        Control? cur = c;
        while (cur is not null && cur.FindForm() == form)
        {
            parts.Add(GetPart(cur));
            cur = cur.Parent;
        }

        parts.Reverse();

        return $"{formName}.{string.Join(".", parts)}";
    }

    private static string GetPart(Control c)
    {
        if (!string.IsNullOrWhiteSpace(c.Name))
            return c.Name;

        // No Name => Type + index among siblings of the same type+name (so it's less collision-prone)
        var typeName = c.GetType().Name;

        if (c.Parent is null)
            return typeName;

        var siblings = c.Parent.Controls
            .Cast<Control>()
            .Where(x => string.IsNullOrWhiteSpace(x.Name) && x.GetType() == c.GetType())
            .ToList();

        var idx = siblings.IndexOf(c);
        if (idx < 0) idx = 0;

        return $"{typeName}#{idx}";
    }
}
