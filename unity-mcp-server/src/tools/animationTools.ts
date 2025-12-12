// ============================================================================
// UnityVision MCP Server - Animation Tools
// Tools for animation inspection and control
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const animationToolDefinitions = [
  {
    name: 'get_animator_state',
    description: `Get the current state of an Animator component.

**Returns:**
- Current playing state
- All parameters with values
- Layer info and weights
- Available states

**Use Cases:**
- "What animation is playing?"
- "What are the animator parameters?"
- Debugging animation state machines`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to GameObject with Animator',
        },
      },
      required: ['gameObjectPath'] as string[],
    },
  },
  {
    name: 'set_animator_parameter',
    description: `Set an Animator parameter value.

**Supports:**
- Float, Int, Bool parameters
- Triggers

**Use Cases:**
- "Set the speed parameter to 2"
- "Trigger the jump animation"
- Testing animation transitions`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to GameObject with Animator',
        },
        parameterName: {
          type: 'string',
          description: 'Name of the parameter',
        },
        value: {
          type: 'string',
          description: 'Value to set (number, "true"/"false", or any for trigger)',
        },
        type: {
          type: 'string',
          enum: ['Float', 'Int', 'Bool', 'Trigger'],
          description: 'Parameter type (auto-detected if not specified)',
        },
      },
      required: ['gameObjectPath', 'parameterName', 'value'] as string[],
    },
  },
  {
    name: 'get_animation_clips',
    description: `Get information about animation clips on an Animator or Animation component.

**Returns:**
- Clip names, lengths, frame rates
- Loop settings
- Animation events
- Animated properties

**Use Cases:**
- "What animations does this character have?"
- "How long is the attack animation?"`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to GameObject with Animator/Animation',
        },
        animatorControllerPath: {
          type: 'string',
          description: 'Alternative: path to AnimatorController asset',
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'play_animation',
    description: `Play a specific animation state (requires Play mode).

**Use Cases:**
- "Play the idle animation"
- Testing specific animation states`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to GameObject with Animator',
        },
        stateName: {
          type: 'string',
          description: 'Name of the animation state to play',
        },
        layer: {
          type: 'number',
          description: 'Animator layer. Default: 0',
          default: 0,
        },
        normalizedTime: {
          type: 'number',
          description: 'Start time (0-1). Default: 0',
          default: 0,
        },
      },
      required: ['gameObjectPath', 'stateName'] as string[],
    },
  },
  {
    name: 'sample_animation',
    description: `Sample an animation at a specific time - pose the character without playing.

**Use Cases:**
- "Show me frame 30 of the attack animation"
- Previewing animation poses in Edit mode`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to GameObject with Animator',
        },
        clipName: {
          type: 'string',
          description: 'Name of the animation clip',
        },
        time: {
          type: 'number',
          description: 'Time in seconds to sample',
        },
        takeScreenshot: {
          type: 'boolean',
          description: 'Take a screenshot after sampling. Default: false',
          default: false,
        },
      },
      required: ['gameObjectPath', 'clipName', 'time'] as string[],
    },
  },
];

export async function getAnimatorState(params: {
  gameObjectPath: string;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_animator_state', params);
}

export async function setAnimatorParameter(params: {
  gameObjectPath: string;
  parameterName: string;
  value: string;
  type?: string;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('set_animator_parameter', params);
}

export async function getAnimationClips(params: {
  gameObjectPath?: string;
  animatorControllerPath?: string;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_animation_clips', params);
}

export async function playAnimation(params: {
  gameObjectPath: string;
  stateName: string;
  layer?: number;
  normalizedTime?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('play_animation', params);
}

export async function sampleAnimation(params: {
  gameObjectPath: string;
  clipName: string;
  time: number;
  takeScreenshot?: boolean;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('sample_animation', params);
}
