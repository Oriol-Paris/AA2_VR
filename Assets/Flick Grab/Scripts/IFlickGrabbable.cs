using UnityEngine;

namespace FlickGrab
{
    /// <summary>
    /// Interface for grabbable objects
    /// </summary>
    public interface IFlickGrabbable
    {
        /// <summary>
        /// Is called when the controller pointer is on the object
        /// </summary>
        void OnPointerEnter();

        /// <summary>
        /// Is called when the controller pointer exits the object
        /// </summary>
        void OnPointerExit();

        /// <summary>
        /// Starts the movement towards the desired hand
        /// </summary>
        void OnFlickGrab(Transform handTransform);
    }
}
