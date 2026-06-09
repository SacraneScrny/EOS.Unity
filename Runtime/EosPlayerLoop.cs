using System;
using System.Collections.Generic;
using EOS.Core;
using EOS.Logging;

using UnityEngine.LowLevel;

namespace EOS.Unity
{
    public static class EosPlayerLoop
    {
        struct EosFixedUpdate { }
        struct EosUpdate { }
        struct EosLateUpdate { }

        static bool _installed;

        public static void Install()
        {
            var root = PlayerLoop.GetCurrentPlayerLoop();
            RemoveNodes(ref root);

            InsertInto(ref root, typeof(UnityEngine.PlayerLoop.FixedUpdate), typeof(EosFixedUpdate), OnFixedUpdate, false);
            InsertInto(ref root, typeof(UnityEngine.PlayerLoop.Update), typeof(EosUpdate), OnUpdate, true);
            InsertInto(ref root, typeof(UnityEngine.PlayerLoop.PreLateUpdate), typeof(EosLateUpdate), OnLateUpdate, false);

            PlayerLoop.SetPlayerLoop(root);
            _installed = true;
        }

        public static void Uninstall()
        {
            if (!_installed) return;
            var root = PlayerLoop.GetCurrentPlayerLoop();
            RemoveNodes(ref root);
            PlayerLoop.SetPlayerLoop(root);
            _installed = false;
        }

        static void OnUpdate()
        {
            if (Universe.IsEnabled) Universe.Update(UnityEngine.Time.deltaTime);
        }

        static void OnFixedUpdate()
        {
            if (Universe.IsEnabled) Universe.FixedUpdate(UnityEngine.Time.fixedDeltaTime);
        }

        static void OnLateUpdate()
        {
            if (Universe.IsEnabled) Universe.LateUpdate(UnityEngine.Time.deltaTime);
        }

        static void InsertInto(ref PlayerLoopSystem root, Type stageType, Type marker, PlayerLoopSystem.UpdateFunction fn, bool atStart)
        {
            for (int i = 0; i < root.subSystemList.Length; i++)
            {
                if (root.subSystemList[i].type == null) continue;
                if (root.subSystemList[i].type != stageType) continue;

                var stage = root.subSystemList[i];
                var node = new PlayerLoopSystem { type = marker, updateDelegate = fn };
                var list = new List<PlayerLoopSystem>(stage.subSystemList ?? Array.Empty<PlayerLoopSystem>());
                if (atStart) list.Insert(0, node);
                else list.Add(node);
                stage.subSystemList = list.ToArray();
                root.subSystemList[i] = stage;
                return;
            }
            EosLog.Error($"Failed to insert {marker.Name} into player loop, stage {stageType.Name} not found", nameof(EosPlayerLoop));
        }

        static void RemoveNodes(ref PlayerLoopSystem root)
        {
            for (int i = 0; i < root.subSystemList.Length; i++)
            {
                var stage = root.subSystemList[i];
                if (stage.subSystemList == null) continue;

                var list = new List<PlayerLoopSystem>(stage.subSystemList);
                list.RemoveAll(s => s.type == typeof(EosFixedUpdate)
                    || s.type == typeof(EosUpdate)
                    || s.type == typeof(EosLateUpdate));
                stage.subSystemList = list.ToArray();
                root.subSystemList[i] = stage;
            }
        }
    }
}
