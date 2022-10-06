// using UnityEngine;
//
// namespace GraphViewPlayer
// {
//     public static class PanUtils
//     {
//         internal const int k_PanAreaWidth = 100;
//         internal const int k_PanSpeed = 4;
//         internal const int k_PanInterval = 10;
//         internal const float k_MinSpeedFactor = 0.5f;
//         internal const float k_MaxSpeedFactor = 2.5f;
//         internal const float k_MaxPanSpeed = k_MaxSpeedFactor * k_PanSpeed;
//         
//         public static Vector2 GetEffectivePanSpeed(GraphView graphView, Vector2 mousePos)
//         {
//             Vector2 effectiveSpeed = Vector2.zero;
//    
//             if (mousePos.x <= k_PanAreaWidth)
//                 effectiveSpeed.x = -((k_PanAreaWidth - mousePos.x) / k_PanAreaWidth + k_MinSpeedFactor) * k_PanSpeed;
//             else if (mousePos.x >= graphView.contentContainer.layout.width - k_PanAreaWidth)
//                 effectiveSpeed.x = ((mousePos.x - (graphView.contentContainer.layout.width - k_PanAreaWidth)) 
//                     / k_PanAreaWidth + k_MinSpeedFactor) * k_PanSpeed;
//    
//             if (mousePos.y <= k_PanAreaWidth)
//                 effectiveSpeed.y = -((k_PanAreaWidth - mousePos.y) / k_PanAreaWidth + k_MinSpeedFactor) * k_PanSpeed;
//             else if (mousePos.y >= graphView.contentContainer.layout.height - k_PanAreaWidth)
//                 effectiveSpeed.y = ((mousePos.y - (graphView.contentContainer.layout.height - k_PanAreaWidth)) 
//                     / k_PanAreaWidth + k_MinSpeedFactor) * k_PanSpeed;
//    
//             effectiveSpeed = Vector2.ClampMagnitude(effectiveSpeed, k_MaxPanSpeed);
//    
//             return effectiveSpeed;
//         }
//     }
// }
//
