// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;

namespace GraphViewPlayer
{
    public interface ISelectable
    {
        ISelector Selector { get; }
        bool Selected { get; set; }
        bool Overlaps(Rect rectangle);
        bool ContainsPoint(Vector2 localPoint);
    }
}