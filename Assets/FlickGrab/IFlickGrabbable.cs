using UnityEngine;

namespace FlickGrab
{
    /// <summary>
    /// Interface for grabbable objects.
    /// </summary>
    public interface IFlickGrabbable
    {
        /// <summary>Called when the controller ray enters the object.</summary>
        void OnPointerEnter();

        /// <summary>Called when the controller ray exits the object.</summary>
        void OnPointerExit();

        /// <summary>Trigger held while pointing — pulse to signal "flick me".</summary>
        void OnAimStart();

        /// <summary>Trigger released or ray left the object — cancel aim feedback.</summary>
        void OnAimCancel();

        /// <summary>Flick confirmed — object starts flying to the hand.</summary>
        void OnFlickGrab(Transform handTransform);

        /// <summary>True once the object has arrived and is parented to the hand.</summary>
        bool IsInHand { get; }

        /// <summary>Detaches the object from the hand and restores physics.</summary>
        void Release(Vector3 throwVelocity);
    }
}
