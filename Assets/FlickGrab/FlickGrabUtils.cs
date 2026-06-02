using UnityEngine;

namespace FlickGrab
{
    public static class FlickGrabUtils
    {
        public static Vector3 GetParabolicPoint(Vector3 start, Vector3 end, float height, float t)
        {
            Vector3 midPos = Vector3.Lerp(start, end, t);
            float arc = Mathf.Sin(t * Mathf.PI) * height;
            
            return new Vector3(midPos.x, midPos.y + arc, midPos.z);
        }

        public static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
