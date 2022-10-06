using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public interface IPositionable
    {
        public event Action<PositionData> OnPositionChange;
        public Vector2 GetGlobalCenter();
        public Vector2 GetCenter();
        public Vector2 GetPosition();
        public void SetPosition(Vector3 position);
    }

    public struct PositionData
    {
        public Vector2 position;
        public VisualElement element;
    }
}