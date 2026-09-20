using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    // 조준/탄도 기준은 모델 파일과 독립적이다. 원래 TankShape 산식을 보존한다.
    public sealed class TankAimRig : MonoBehaviour
    {
        Transform controlTurret, controlBarrel, visualTurret, visualBarrel;
        Vector3 controlHome, visualHome;
        Quaternion turretHome, barrelHome;
        Transform[] pupils;
        public Transform VisualTurret => visualTurret;

        public static void Attach(TankShape s, Transform root,
            ref Transform turret, ref Transform barrel, out Transform fire)
        {
            var rig = root.gameObject.AddComponent<TankAimRig>();
            rig.visualTurret = turret; rig.visualBarrel = barrel;
            rig.turretHome = turret.localRotation; rig.barrelHome = barrel.localRotation;
            rig.visualHome = barrel.localPosition;
            float floor;
            switch (s.Kind)
            {
                case TankKind.Catapult: case TankKind.CrossBow:
                    floor = Mathf.Max(0.9f, s.BodyHeight * 0.95f) * 0.75f + 0.24f; break;
                case TankKind.Cannon:
                    floor = Mathf.Max(0.8f, s.BodyHeight * 0.85f) * 0.8f; break;
                case TankKind.Missile: case TankKind.MultiMissile:
                    floor = Mathf.Max(0.5f, s.TrackHeight * 0.7f) * 1.05f; break;
                case TankKind.Laser: case TankKind.IonAttacker: case TankKind.Poseidon: case TankKind.SecWind:
                    floor = 0.55f + s.BodyHeight * 0.55f * 0.9f; break;
                default:
                    floor = Mathf.Max(0.42f, Mathf.Min(s.TrackHeight * 0.88f,
                        s.BodyLength * 0.5f / Mathf.Max(1, s.WheelCount) * 1.5f)) * 0.95f; break;
            }
            turret = new GameObject("AimTurret_TankShape").transform;
            turret.SetParent(root, false);
            turret.localPosition = new Vector3(0, floor + s.BodyHeight, -s.BodyLength * 0.08f);
            barrel = new GameObject("AimBarrel_TankShape").transform;
            barrel.SetParent(turret, false);
            barrel.localPosition = new Vector3(0, s.TurretHeight * 0.52f, s.TurretRadius * 1.45f * 0.45f);
            float fireZ = s.BarrelLength;
            switch (s.Kind)
            {
                case TankKind.Catapult: fireZ *= 2.2f; break;
                case TankKind.Carrot: fireZ *= 1.25f; break;
                case TankKind.Missile: fireZ = fireZ * 0.84f + 0.6f; break;
                case TankKind.Laser: fireZ += 1.0f; break;
                case TankKind.Poseidon: fireZ += 0.35f; break;
                case TankKind.SecWind: fireZ *= 1.05f; break;
            }
            fire = new GameObject("FirePoint_TankShape").transform;
            fire.SetParent(barrel, false); fire.localPosition = new Vector3(0, 0, fireZ);
            rig.controlTurret = turret; rig.controlBarrel = barrel; rig.controlHome = barrel.localPosition;
            var gaze = new System.Collections.Generic.List<Transform>();
            foreach(var child in root.GetComponentsInChildren<Transform>(true))
                if(child.name=="PupilGaze") gaze.Add(child);
            rig.pupils=gaze.ToArray();
        }

        void LateUpdate()
        {
            if (controlTurret == null) return;
            visualTurret.localRotation = controlTurret.localRotation * turretHome;
            visualBarrel.localRotation = controlBarrel.localRotation * barrelHome;
            visualBarrel.localPosition = visualHome + controlBarrel.localPosition - controlHome;
            var target=controlBarrel.position+controlBarrel.forward*40f;
            if(pupils!=null) foreach(var pupil in pupils)
            {
                var direction=pupil.parent.InverseTransformPoint(target).normalized;
                // Small motion stays inside the authored sclera; never moves the hitbox.
                var offset=new Vector3(Mathf.Clamp(direction.x*.10f,-.10f,.10f),Mathf.Clamp(direction.y*.07f,-.07f,.07f),0);
                pupil.localPosition=Application.isPlaying?Vector3.Lerp(pupil.localPosition,offset,1-Mathf.Exp(-12*Time.deltaTime)):offset;
            }
        }
    }
}
