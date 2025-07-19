using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MoveBase
{
    public static class DrawUtility
    {
        public static void DrawUrl(Rect labelRect, string text, string url)
        {
            Widgets.Label(labelRect, text.Colorize(ColorLibrary.SkyBlue));
            if (Mouse.IsOver(labelRect))
            {
                Vector2 size = Text.CalcSize(text);
                Widgets.DrawLine(
                    new Vector2(labelRect.x, labelRect.y + size.y)
                    , new Vector2(labelRect.x + size.x, labelRect.y + size.y)
                    , ColorLibrary.SkyBlue, 1);
            }

            if (Widgets.ButtonInvisible(labelRect))
            {
                Application.OpenURL(url);
            }
        }

    }
}
