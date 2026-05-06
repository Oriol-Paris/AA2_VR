using UnityEngine;

namespace FlickGrab
{
    public static class TrajectoryMath
    {
        /// <summary>
        /// Calculates the velocity needed to hit a target with a parabolic arc.
        /// </summary>
        public static Vector3 GetArcVelocity(Vector3 start, Vector3 end, float height, float gravity)
        {
            float displacementY = end.y - start.y;
            Vector3 displacementXZ = new Vector3(end.x - start.x, 0, end.z - start.z);

            float time = Mathf.Sqrt(-2 * height / gravity) + Mathf.Sqrt(2 * (displacementY - height) / gravity);
            
            // Adjust height if displacementY is higher than height
            if (displacementY > height)
            {
                height = displacementY + 0.5f; // Add a small offset
                time = Mathf.Sqrt(-2 * height / gravity) + Mathf.Sqrt(2 * (displacementY - height) / gravity);
            }

            Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * height);
            Vector3 velocityXZ = displacementXZ / time;

            return velocityXZ + velocityY;
        }
        
        /// <summary>
        /// Simple quadratic Bezier for a more controlled "fake" physics arc.
        /// </summary>
        public static Vector3 GetBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            Vector3 p = uu * p0;
            p += 2 * u * t * p1;
            p += tt * p2;
            return p;
        }
    }
}
