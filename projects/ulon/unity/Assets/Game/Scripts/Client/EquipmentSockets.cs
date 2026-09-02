using UnityEngine;

namespace Ulon.Client
{
    public sealed class EquipmentSockets : MonoBehaviour
    {
        public Transform RightHand;
        public Transform LeftHand;
        public Transform Head;
        public Transform Back;

        public void Bind(Transform visualRoot)
        {
            if (visualRoot == null)
                return;
            RightHand = FindBone(visualRoot, "handslot.r", "RightHand", "mixamorig:RightHand", "Hand_R", "hand.r");
            LeftHand = FindBone(visualRoot, "handslot.l", "LeftHand", "mixamorig:LeftHand", "Hand_L", "hand.l");
            Head = FindBone(visualRoot, "headslot", "Head", "mixamorig:Head", "head");
            Back = FindBone(visualRoot, "backslot", "Spine2", "mixamorig:Spine2", "chest", "Spine");
        }

        public Transform Attach(GameObject item, Transform socket)
        {
            if (item == null || socket == null)
                return null;
            item.transform.SetParent(socket, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            return item.transform;
        }

        static Transform FindBone(Transform root, params string[] names)
        {
            var bones = root.GetComponentsInChildren<Transform>(true);
            for (int n = 0; n < names.Length; n++)
            {
                string want = names[n];
                for (int i = 0; i < bones.Length; i++)
                {
                    string bn = bones[i].name;
                    if (bn.Equals(want, System.StringComparison.OrdinalIgnoreCase))
                        return bones[i];
                    if (bn.EndsWith(":" + want, System.StringComparison.OrdinalIgnoreCase))
                        return bones[i];
                }
            }
            return null;
        }
    }
}
