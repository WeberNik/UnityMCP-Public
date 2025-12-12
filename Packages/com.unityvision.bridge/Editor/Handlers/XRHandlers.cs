// ============================================================================
// UnityVision Bridge - XR Handlers
// Handlers for XR rig positioning and teleportation
// ============================================================================

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class XRHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class SetXRRigPoseRequest
        {
            public string rigRootPath;
            public Vector3Data position;
            public Vector3Data rotationEuler;
            public bool dryRun = false;
        }

        [Serializable]
        public class SetXRRigPoseResponse
        {
            public bool success;
            public object dryRunPlan;
        }

        [Serializable]
        public class TeleportXRRigToAnchorRequest
        {
            public string rigRootPath;
            public string anchorObjectPath;
            public bool dryRun = false;
        }

        [Serializable]
        public class TeleportXRRigToAnchorResponse
        {
            public bool success;
            public object dryRunPlan;
        }

        #endregion

        public static RpcResponse SetXRRigPose(RpcRequest request)
        {
            var req = request.GetParams<SetXRRigPoseRequest>();

            if (string.IsNullOrEmpty(req.rigRootPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "rigRootPath is required");
            }

            if (req.position == null)
            {
                return RpcResponse.Failure("INVALID_PARAMS", "position is required");
            }

            if (req.rotationEuler == null)
            {
                return RpcResponse.Failure("INVALID_PARAMS", "rotationEuler is required");
            }

            var rigGo = GameObjectHandlers.FindGameObjectByPath(req.rigRootPath);
            if (rigGo == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"XR rig not found: {req.rigRootPath}");
            }

            var position = req.position.ToVector3();
            var rotation = req.rotationEuler.ToVector3();

            // Dry run
            if (req.dryRun)
            {
                return RpcResponse.Success(new SetXRRigPoseResponse
                {
                    success = true,
                    dryRunPlan = new
                    {
                        wouldMove = req.rigRootPath,
                        to = new { x = position.x, y = position.y, z = position.z },
                        rotation = new { x = rotation.x, y = rotation.y, z = rotation.z }
                    }
                });
            }

            Undo.RecordObject(rigGo.transform, $"Set XR Rig Pose");
            rigGo.transform.position = position;
            rigGo.transform.eulerAngles = rotation;
            EditorSceneManager.MarkSceneDirty(rigGo.scene);

            return RpcResponse.Success(new SetXRRigPoseResponse { success = true });
        }

        public static RpcResponse TeleportXRRigToAnchor(RpcRequest request)
        {
            var req = request.GetParams<TeleportXRRigToAnchorRequest>();

            if (string.IsNullOrEmpty(req.rigRootPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "rigRootPath is required");
            }

            if (string.IsNullOrEmpty(req.anchorObjectPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "anchorObjectPath is required");
            }

            var rigGo = GameObjectHandlers.FindGameObjectByPath(req.rigRootPath);
            if (rigGo == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"XR rig not found: {req.rigRootPath}");
            }

            var anchorGo = GameObjectHandlers.FindGameObjectByPath(req.anchorObjectPath);
            if (anchorGo == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"Anchor not found: {req.anchorObjectPath}");
            }

            var anchorPosition = anchorGo.transform.position;
            var anchorRotation = anchorGo.transform.eulerAngles;

            // Dry run
            if (req.dryRun)
            {
                return RpcResponse.Success(new TeleportXRRigToAnchorResponse
                {
                    success = true,
                    dryRunPlan = new
                    {
                        wouldTeleport = req.rigRootPath,
                        to = req.anchorObjectPath,
                        position = new { x = anchorPosition.x, y = anchorPosition.y, z = anchorPosition.z }
                    }
                });
            }

            Undo.RecordObject(rigGo.transform, $"Teleport XR Rig to {req.anchorObjectPath}");
            rigGo.transform.position = anchorPosition;
            rigGo.transform.rotation = anchorGo.transform.rotation;
            EditorSceneManager.MarkSceneDirty(rigGo.scene);

            return RpcResponse.Success(new TeleportXRRigToAnchorResponse { success = true });
        }
    }
}
