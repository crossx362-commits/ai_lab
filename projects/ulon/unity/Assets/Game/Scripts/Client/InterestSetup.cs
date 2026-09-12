using System.Collections.Generic;
using System.Reflection;
using FishNet.Component.Observing;
using FishNet.Managing;
using FishNet.Managing.Observing;
using FishNet.Observing;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Client
{
    /// <summary>
    /// FishNet ObserverManager 기본 조건에 거리 컬을 넣는다(기획 §7.3).
    /// 조건 목록은 패키지 비공개 필드라 반사로만 채운다 — 공개 setter가 없다.
    /// 서버가 스폰하기 전에 한 번 적용해야 한다.
    /// </summary>
    public static class InterestSetup
    {
        public static float LastAppliedMeters;
        public static int DefaultConditionCount;

        public static bool Apply(NetworkManager manager)
        {
            if (manager == null)
                return false;
            var om = manager.GetComponent<ObserverManager>();
            if (om == null)
                om = manager.gameObject.AddComponent<ObserverManager>();

            var field = typeof(ObserverManager).GetField(
                "_defaultConditions",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return false;
            if (field.GetValue(om) is not List<ObserverCondition> list)
            {
                list = new List<ObserverCondition>();
                field.SetValue(om, list);
            }

            DistanceCondition dist = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is DistanceCondition found)
                {
                    dist = found;
                    break;
                }
            }
            if (dist == null)
            {
                dist = ScriptableObject.CreateInstance<DistanceCondition>();
                dist.name = "UlonInterestDistance";
                list.Add(dist);
            }

            float meters = InterestRange.Meters;
            dist.SetMaximumDistance(meters);
            LastAppliedMeters = meters;
            DefaultConditionCount = list.Count;
            return true;
        }
    }
}
