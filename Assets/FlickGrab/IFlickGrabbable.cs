using UnityEngine;

namespace FlickGrab
{
    public interface IFlickGrabbable
    {
        void OnPointerEnter();
        void OnPointerExit();
        void OnAimStart();
        void OnAimCancel();
        void OnFlickGrab(Transform handTransform);
        bool IsInHand { get; }
        void Release(Vector3 throwVelocity);
    }
}
