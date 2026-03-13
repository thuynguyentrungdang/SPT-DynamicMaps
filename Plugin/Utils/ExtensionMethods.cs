using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DynamicMaps.Utils;

internal static class ExtensionMethods
{
    public static Vector2 ToScreenPos(this EMiniMapPosition pos)
    {
        // 0,0 Bottom left
        // 0,1 Top left
        // 1,1 Top right
        // 1,0 Bottom right

        switch (pos)
        {
            case EMiniMapPosition.TopRight:
                return new Vector2(1, 1);

            case EMiniMapPosition.BottomRight:
                return new Vector2(1, 0);

            case EMiniMapPosition.TopLeft:
                return new Vector2(0, 1);

            case EMiniMapPosition.BottomLeft:
                return new Vector2(0, 0);
        }

        return Vector2.zero;
    }

    public static Vector2 ToScenePivot(this EMiniMapPosition pos)
    {
        // Top right = neg neg
        // Bottom right = neg pos
        // Top left = pos neg
        // Bottom left = pos pos

        switch (pos)
        {
            case EMiniMapPosition.TopRight:
                return new Vector2(-1, -1);

            case EMiniMapPosition.BottomRight:
                return new Vector2(-1, 1);

            case EMiniMapPosition.TopLeft:
                return new Vector2(1, -1);

            case EMiniMapPosition.BottomLeft:
                return new Vector2(1, 1);
        }

        return Vector2.zero;
    }
}
