// ============================================================================
// UnityVision MCP Server - XR Tools
// Tools for XR rig positioning and teleportation
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  SetXRRigPoseInput,
  SetXRRigPoseOutput,
  TeleportXRRigToAnchorInput,
  TeleportXRRigToAnchorOutput,
} from '../types.js';

export const xrToolDefinitions = [
  {
    name: 'set_xr_rig_pose',
    description: 'Set the position and rotation of an XR rig. Useful for positioning the VR camera for consistent screenshots.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        rigRootPath: {
          type: 'string',
          description: 'Path to the XR rig root GameObject (e.g., "XR Rig" or "XR Origin").',
        },
        position: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          required: ['x', 'y', 'z'],
          description: 'World position to set the rig to.',
        },
        rotationEuler: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          required: ['x', 'y', 'z'],
          description: 'World rotation (Euler angles) to set the rig to.',
        },
        dryRun: {
          type: 'boolean',
          description: 'If true, returns a plan without actually moving the rig.',
          default: false,
        },
      },
      required: ['rigRootPath', 'position', 'rotationEuler'],
    },
  },
  {
    name: 'teleport_xr_rig_to_anchor',
    description: 'Teleport an XR rig to a teleport anchor GameObject. The rig will be positioned at the anchor\'s location.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        rigRootPath: {
          type: 'string',
          description: 'Path to the XR rig root GameObject.',
        },
        anchorObjectPath: {
          type: 'string',
          description: 'Path to the teleport anchor GameObject.',
        },
        dryRun: {
          type: 'boolean',
          description: 'If true, returns a plan without actually teleporting.',
          default: false,
        },
      },
      required: ['rigRootPath', 'anchorObjectPath'],
    },
  },
];

export async function setXRRigPose(
  params: SetXRRigPoseInput
): Promise<SetXRRigPoseOutput> {
  const client = getBridgeClient();
  return client.call<SetXRRigPoseInput, SetXRRigPoseOutput>(
    'set_xr_rig_pose',
    {
      rigRootPath: params.rigRootPath,
      position: params.position,
      rotationEuler: params.rotationEuler,
      dryRun: params.dryRun ?? false,
    }
  );
}

export async function teleportXRRigToAnchor(
  params: TeleportXRRigToAnchorInput
): Promise<TeleportXRRigToAnchorOutput> {
  const client = getBridgeClient();
  return client.call<TeleportXRRigToAnchorInput, TeleportXRRigToAnchorOutput>(
    'teleport_xr_rig_to_anchor',
    {
      rigRootPath: params.rigRootPath,
      anchorObjectPath: params.anchorObjectPath,
      dryRun: params.dryRun ?? false,
    }
  );
}
