using UnityEngine;

namespace FlickGrab
{
    /// <summary>
    /// Utility class for movement calculations
    /// </summary>
    public static class FlickGrabUtils
    {
        /// <summary>
        /// Calculates a position in a parabolic trajectory between two points
        /// </summary>
        public static Vector3 GetParabolicPoint(Vector3 start, Vector3 end, float height, float t)
        {
            Vector3 midPos = Vector3.Lerp(start, end, t);
            float arc = Mathf.Sin(t * Mathf.PI) * height;
            
            return new Vector3(midPos.x, midPos.y + arc, midPos.z);
        }

        /// <summary>
        /// Applies a smoothing curve to the parameter t
        /// </summary>
        public static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
